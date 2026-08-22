import jsPDF from 'jspdf';
import { getSetting } from './settings';
import QRCode from 'qrcode';
import db from './connections';

async function generateCardPdf(
	data: Set<string>,
	rowsPerPage = 5,
	colsPerPage = 2,
	saveOverReturn = true,
	printBackPage = false,
	faqItems: Array<{ q: string; a: string }> = []
) {
	const doc = new jsPDF({ unit: 'mm', format: 'a4' });

	const bankName = (await getSetting('display_name')) || 'Einstein Bank';

	const pageWidth = doc.internal.pageSize.getWidth();
	const pageHeight = doc.internal.pageSize.getHeight();

	const marginTop = 5; // The space from the top of the paper to the top of the first row of cards
	const marginBottom = 5; // The space from the bottom of the last row of cards to the bottom of the paper
	const marginLeft = 15; // The space from the left edge of the paper to the left side of cards
	const marginRight = 15; // The space from the right side of cards to the right edge of the paper

	const availableHeight = pageHeight - marginTop - marginBottom;
	const availableWidth = pageWidth - marginLeft - marginRight;
	const cardHeight = availableHeight / rowsPerPage;
	const cardWidth = availableWidth / colsPerPage;
	const padW = 0;
	const padH = 0;
	const innerW = cardWidth - padW * 2;
	const innerH = cardHeight - padH * 2;

	let x = marginLeft;
	let y = marginTop;
	let cardCount = 0;

	const logoDataUrl = await fetch('/bank-black.png')
		.then((r) => r.blob())
		.then(
			(b) =>
				new Promise<string>((res) => {
					const fr = new FileReader();
					fr.onload = () => res(String(fr.result));
					fr.readAsDataURL(b);
				})
		);

	// Fetch all codes in parallel and sort them after the ids afterwards
	let codes: any[] = [];
	await Promise.all(
		Array.from(data).map(async (codeId) => {
			const code = await db.collection('account_validation').getOne(codeId, {
				expand: 'account,account.user',
				requestKey: null
			});
			codes.push(code);
		})
	);

	// Check if the code.expand.account.expand.user.name ends with a number, if so, sort by that number. All non numbered names will be sorted to the end of the list
	codes.sort((a, b) => {
		const nameA = a.expand.account.expand.user.name || '';
		const nameB = b.expand.account.expand.user.name || '';
		const numA = parseInt(nameA.match(/\d+$/)?.[0] || '0');
		const numB = parseInt(nameB.match(/\d+$/)?.[0] || '0');
		return numA - numB;
	});

	// Pre-generate all QR codes so they can be reused on the back page
	const qrCache = new Map<string, string>();
	await Promise.all(
		codes.map(async (code) => {
			qrCache.set(code.id, await generateQRCode(code));
		})
	);

	// Tracks which codes are on the current front page for back-page generation
	let currentPageCodes: any[] = [];

	for (const codeId of data) {
		const code = codes.find((c) => c.id === codeId);
		if (!code) continue;

		currentPageCodes.push(code);

		// --- Card container (light) ---
		doc.setFillColor(255, 255, 255);
		doc.roundedRect(x + padW, y + padH, innerW, innerH, 0, 0, 'F');

		// Border
		doc.setDrawColor(0, 0, 0);
		doc.setLineWidth(0.35);
		doc.roundedRect(x + padW, y + padH, innerW, innerH, 0, 0, 'S');

		// Header strip
		const headerH = 14;
		doc.setFillColor(199, 188, 185);
		doc.roundedRect(x + padW, y + padH, innerW, headerH, 0, 0, 'F');
		// Clean up bottom corners of header strip (keeps nice top rounding)
		doc.setFillColor(199, 188, 185);
		doc.rect(x + padW, y + padH + 3, innerW, headerH - 3, 'F');

		// --- Header text ---
		doc.setTextColor(25, 25, 25);
		doc.setFont('Helvetica', 'bold');
		doc.setFontSize(11);
		doc.text(bankName, x + padW + 18, y + padH + 6);

		doc.setTextColor(90, 90, 90);
		doc.setFont('Helvetica', 'normal');
		doc.setFontSize(8);
		doc.text('AEG - ULM', x + padW + 18, y + padH + 10);

		// Logo (only if you have a data URL)
		if (logoDataUrl) {
			const logoX = x + padW + 5;
			const logoY = y + padH + 2;
			const logoS = 9.5;
			doc.addImage(logoDataUrl, 'PNG', logoX, logoY, logoS, logoS);
		}

		// --- Body labels ---
		const leftX = x + padW + 8;
		const topY = y + padH + headerH + 10;

		// Cardholder label
		doc.setFont('Helvetica', 'normal');
		doc.setFontSize(7);
		doc.setTextColor(120, 120, 120);
		doc.text('KARTENHALTER', leftX, topY);

		// Cardholder value
		const holder = String(code.expand.account.expand.user.name || '');
		doc.setFont('Helvetica', 'bold');
		doc.setFontSize(10);
		doc.setTextColor(25, 25, 25);
		doc.text(holder, leftX, topY + 5);

		// Account label
		doc.setFont('Helvetica', 'normal');
		doc.setFontSize(7);
		doc.setTextColor(120, 120, 120);
		doc.text('KONTONUMMER', leftX, topY + 11);

		// Card Number label
		doc.setFont('Helvetica', 'normal');
		doc.setFontSize(7);
		doc.setTextColor(120, 120, 120);
		doc.text('KARTENNUMMER', leftX, topY + 21);

		// Account number value
		const rawAccountNumber = String(code.expand.account.id || '');
		const formattedAccountNumber = rawAccountNumber.replace(/(\w{4})(\w{4})/, '$1 - $2');
		const rawCardNumber = String(code.number || '');
		const formattedCardNumber = rawCardNumber
			.replace(/(\w{4})(\w{4})(\w{4})(\w{4})/, '$1 $2 $3 $4')
			.replaceAll(' ', ' • ');
		doc.setFont('Helvetica', 'bold');
		doc.setFontSize(10);
		doc.setTextColor(25, 25, 25);
		doc.text(formattedAccountNumber, leftX, topY + 16);

		// Card number value
		doc.text(formattedCardNumber, leftX, topY + 26);

		// --- QR (bottom right) ---
		const qrSize = 28;
		const qrX = x + padW + innerW - qrSize - 8;
		const qrY = y + padH + innerH - qrSize - 8;

		const qrDataUrl = qrCache.get(code.id);
		if (qrDataUrl) {
			// White backing so it prints clean on any printer
			doc.setFillColor(255, 255, 255);
			doc.roundedRect(qrX - 1.2, qrY - 1.2, qrSize + 2.4, qrSize + 2.4, 2, 2, 'F');

			doc.setDrawColor(220, 220, 220);
			doc.setLineWidth(0.3);
			doc.roundedRect(qrX - 1.2, qrY - 1.2, qrSize + 2.4, qrSize + 2.4, 2, 2, 'S');

			doc.addImage(qrDataUrl, 'PNG', qrX, qrY, qrSize, qrSize);
		}

		// --- Next card position ---
		cardCount++;
		x += cardWidth;

		if (cardCount % colsPerPage === 0) {
			x = marginLeft;
			y += cardHeight;

			if (cardCount % (colsPerPage * rowsPerPage) === 0 && cardCount !== codes.length) {
				if (printBackPage) {
					drawBackPage(
						doc,
						cardWidth,
						cardHeight,
						currentPageCodes,
						colsPerPage,
						logoDataUrl,
						bankName,
						faqItems,
						marginTop,
						marginBottom,
						marginLeft,
						marginRight
					);
				}
				doc.addPage();
				x = marginLeft;
				y = marginTop;
				currentPageCodes = [];
			}
		}
	}

	if (printBackPage && currentPageCodes.length > 0) {
		drawBackPage(
			doc,
			cardWidth,
			cardHeight,
			currentPageCodes,
			colsPerPage,
			logoDataUrl,
			bankName,
			faqItems,
			marginTop,
			marginBottom,
			marginLeft,
			marginRight
		);
	}

	if (saveOverReturn) {
		doc.save('cards.pdf');
	} else {
		return doc;
	}
}

function drawBackPage(
	doc: jsPDF,
	cardWidth: number,
	cardHeight: number,
	pageCards: any[],
	colsPerPage: number,
	logoDataUrl: string,
	bankName: string,
	faqItems: Array<{ q: string; a: string }>,
	marginTop: number,
	marginBottom: number,
	marginLeft: number,
	marginRight: number
) {
	const innerW = cardWidth;
	const innerH = cardHeight;
	const headerH = 14;
	const footerH = 8;

	doc.addPage();

	for (let i = 0; i < pageCards.length; i++) {
		const row = Math.floor(i / colsPerPage);
		const col = i % colsPerPage;
		// Mirror columns horizontally so backs align with fronts when printing duplex
		const mirroredCol = colsPerPage - 1 - col;

		const x = marginLeft + mirroredCol * cardWidth;
		const y = marginTop + row * cardHeight;

		// --- White card base ---
		doc.setFillColor(255, 255, 255);
		doc.rect(x, y, innerW, innerH, 'F');

		// Border
		doc.setDrawColor(0, 0, 0);
		doc.setLineWidth(0.35);
		doc.rect(x, y, innerW, innerH, 'S');

		// --- Header strip (matches front card) ---
		doc.setFillColor(199, 188, 185);
		doc.rect(x, y, innerW, headerH, 'F');

		if (logoDataUrl) {
			doc.addImage(logoDataUrl, 'PNG', x + 5, y + 2, 9.5, 9.5);
		}
		doc.setTextColor(25, 25, 25);
		doc.setFont('Helvetica', 'bold');
		doc.setFontSize(11);
		doc.text(bankName, x + 18, y + 6);
		doc.setFont('Helvetica', 'normal');
		doc.setFontSize(8);
		doc.setTextColor(90, 90, 90);
		doc.text('AEG - ULM', x + 18, y + 10);

		// --- FAQ body ---
		const bodyTop = y + headerH + 4;
		const leftX = x + 8;
		const activeFaq = faqItems.filter((f) => f.q || f.a);

		if (activeFaq.length > 0) {
			let curY = bodyTop;
			for (const item of activeFaq) {
				if (item.q) {
					doc.setFont('Helvetica', 'bold');
					doc.setFontSize(6.5);
					doc.setTextColor(30, 30, 30);
					doc.text(item.q, leftX, curY);
					curY += 4.5;
				}
				if (item.a) {
					doc.setFont('Helvetica', 'normal');
					doc.setFontSize(6);
					doc.setTextColor(90, 90, 90);
					doc.text(item.a, leftX + 3, curY);
					curY += 4;
				}
				curY += 2.5;
			}
		}

		// --- Footer strip ---
		doc.setFillColor(199, 188, 185);
		doc.rect(x, y + innerH - footerH, innerW, footerH, 'F');

		// Thin top border on footer
		doc.setDrawColor(170, 160, 157);
		doc.setLineWidth(0.2);
		doc.line(x, y + innerH - footerH, x + innerW, y + innerH - footerH);

		doc.setFont('Helvetica', 'normal');
		doc.setFontSize(7);
		doc.setTextColor(50, 50, 50);
		doc.text('www.yourdomain.com', x + innerW / 2, y + innerH - footerH + 5, { align: 'center' });
	}
}

async function generateQRCode(data: any) {
	try {
		const codeData = {
			code: data.security,
			sender: data.expand.account.id
		};

		// Light QR style: black on white, no transparency (more reliable for printers)
		return await QRCode.toDataURL(JSON.stringify(codeData), {
			type: 'image/png',
			margin: 1,
			width: 260,
			errorCorrectionLevel: 'Q',
			color: { dark: '#000000', light: '#FFFFFF' }
		});
	} catch (err) {
		console.error('generateQRCode error', err);
		return '';
	}
}

export { generateCardPdf, generateQRCode };

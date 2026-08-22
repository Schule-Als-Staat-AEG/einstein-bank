<script lang="ts">
	import db from '$lib/connections';
	import AdminSite from '$modules/AdminSite.svelte';
	import jsPDF from 'jspdf';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';

	let signatureInput: string = '';
	let fileInput: HTMLInputElement;
	let csvSeparator: string = ','; // Toggle between "," and ";"

	onMount(async () => {
		if (!browser) return;
	});

	function parseCSVLine(line: string, separator: string = ','): string[] {
		const fields: string[] = [];
		let current = '';
		let inQuotes = false;

		for (let i = 0; i < line.length; i++) {
			const ch = line[i];
			if (ch === '"') {
				if (inQuotes && line[i + 1] === '"') {
					current += '"';
					i++;
				} else {
					inQuotes = !inQuotes;
				}
			} else if (ch === separator && !inQuotes) {
				fields.push(current.trim());
				current = '';
			} else {
				current += ch;
			}
		}
		fields.push(current.trim());
		return fields;
	}

	function parseCSV(csvContent: string, separator: string = ','): Record<string, string>[] {
		csvContent = csvContent.replace(/^\uFEFF/, ''); // Remove UTF-8 BOM if present
		csvContent = csvContent.replace(/\r\n/g, '\n').replace(/\r/g, '\n'); // Normalize line endings
		csvContent = csvContent.replace(/&amp;/g, '&'); // Decode HTML entities if present
		csvContent = csvContent.replace(/&lt;/g, '<').replace(/&gt;/g, '>');
		csvContent = csvContent.replace(/&quot;/g, '"').replace(/&#39;/g, "'");
		csvContent = csvContent.replace(/\\n/g, '\n'); // Handle escaped newlines
		csvContent = csvContent.replace(/\\r/g, '\r').replace(/\\t/g, '\t'); // Handle other escaped characters
		csvContent = csvContent.replace(/\\{2}/g, '\\'); // Handle escaped backslashes
		csvContent = csvContent.replace(/^\s+|\s+$/g, ''); // Trim leading/trailing whitespace
		const lines = csvContent.split('\n').filter((line) => line.trim());
		if (lines.length < 2) return [];

		const headers = parseCSVLine(lines[0], separator);
		const fieldsToSkip = [
			'Abteilung',
			'Institution',
			'Antworten',
			'Kurs',
			'Gruppe',
			'Nutzer-ID',
			'Anmeldename'
		];
		const rows: Record<string, string>[] = [];

		for (let i = 1; i < lines.length; i++) {
			const values = parseCSVLine(lines[i], separator);
			const row: Record<string, string> = {};

			headers.forEach((header, index) => {
				if (!fieldsToSkip.includes(header)) {
					row[header] = values[index] || '';
				}
			});

			if (Object.values(row).some((v) => v)) {
				rows.push(row);
			}
		}

		return rows;
	}

	function stripQPrefix(key: string): string {
		return key.replace(/^Q\d+_/, '');
	}

	function generatePDF(data: Record<string, string>[]) {
		if (data.length === 0) {
			alert('Keine Daten zum Konvertieren');
			return;
		}

		const doc = new jsPDF({ compress: false });
		doc.setFont('helvetica', 'normal');
		const pageHeight = doc.internal.pageSize.getHeight();
		const pageWidth = doc.internal.pageSize.getWidth();
		const margin = 15;
		const valueIndent = margin + 3;
		const labelWidth = pageWidth - 2 * margin;
		const valueWidth = pageWidth - valueIndent - margin;
		const checkboxAreaStart = pageHeight - 28;
		const lh = 5;

		const HEADER_FIELDS = new Set(['Q01_Unternehmensname', 'Vollständiger Name', 'Abgegeben:']);

		const addCheckboxArea = (nr: number) => {
			const sep = checkboxAreaStart + 1;
			const y = checkboxAreaStart + 8;
			const cs = 4;
			const sp = 48;

			doc.setDrawColor(160, 160, 160);
			doc.setLineWidth(0.3);
			doc.line(margin, sep, pageWidth - margin, sep);

			doc.setFontSize(7.5);
			doc.setFont(undefined, 'normal');
			doc.setTextColor(150, 150, 150);
			doc.text(`Antrag ${nr}`, pageWidth - margin, sep + 4, { align: 'right' });
			doc.setTextColor(0, 0, 0);

			doc.setFontSize(10);
			doc.setDrawColor(0, 0, 0);
			doc.setLineWidth(0.2);

			doc.rect(margin, y, cs, cs);
			doc.text('Angenommen', margin + cs + 2.5, y + 3.3);

			doc.rect(margin + sp, y, cs, cs);
			doc.text('Abgelehnt', margin + sp + cs + 2.5, y + 3.3);

			doc.rect(margin + sp * 2, y, cs, cs);
			doc.text('Zusammenfassbar', margin + sp * 2 + cs + 2.5, y + 3.3);
		};

		const drawTopBanner = (doc: any, pageWidth: number) => {
			const bannerHeight = 22;
			const stripeWidth = 18;
			// draw alternating diagonal parallelograms across top
			for (let x = -bannerHeight; x < pageWidth + bannerHeight; x += stripeWidth) {
				const points = {
					tl: { x: x, y: 0 },
					tr: { x: x + stripeWidth, y: 0 },
					br: { x: x + stripeWidth - bannerHeight, y: bannerHeight },
					bl: { x: x - bannerHeight, y: bannerHeight }
				};

				// Alternate colors: black / yellow
				const idx = Math.round(x / stripeWidth);
				if (idx % 2 === 0) {
					doc.setFillColor(255, 204, 0); // yellow
				} else {
					doc.setFillColor(0, 0, 0); // black
				}

				// draw as two triangles forming a parallelogram
				doc.triangle(
					points.tl.x,
					points.tl.y,
					points.tr.x,
					points.tr.y,
					points.br.x,
					points.br.y,
					'F'
				);
				doc.triangle(
					points.tl.x,
					points.tl.y,
					points.br.x,
					points.br.y,
					points.bl.x,
					points.bl.y,
					'F'
				);
			}

			// Draw white box behind text for readability
			const text = 'In Bearbeitung';
			doc.setFontSize(12);
			doc.setFont(undefined, 'bold');
			const textWidth = doc.getTextWidth ? doc.getTextWidth(text) : text.length * 6;
			const paddingX = 10;
			const paddingY = 6;
			const boxW = textWidth + paddingX * 2;
			const boxH = 12 + paddingY;
			const boxX = (pageWidth - boxW) / 2;
			const boxY = (bannerHeight - boxH) / 2;

			doc.setFillColor(255, 255, 255);
			doc.setDrawColor(0, 0, 0);
			doc.rect(boxX, boxY, boxW, boxH, 'F');

			doc.setTextColor(0, 0, 0);
			doc.text(text, pageWidth / 2, boxY + boxH / 2 + 2, { align: 'center' });
		};
		const ensureSpace = (needed: number, nr: number, y: number): number => {
			if (y + needed > checkboxAreaStart) {
				addCheckboxArea(nr);
				doc.addPage();
				return 15;
			}
			return y;
		};

		const writeField = (label: string, value: string, nr: number, yIn: number): number => {
			let y = yIn;
			const lines = value.split(/\n/);

			y = ensureSpace(lh + 2, nr, y);
			doc.setFontSize(9);
			doc.setFont(undefined, 'bold');
			doc.setTextColor(60, 60, 60);
			doc.text(label, margin, y);
			doc.setTextColor(0, 0, 0);
			y += lh;

			doc.setFont(undefined, 'normal');
			doc.setFontSize(10);

			for (const part of lines) {
				const wrapped: string[] = doc.splitTextToSize(part.trim() || ' ', valueWidth);
				for (const wl of wrapped) {
					y = ensureSpace(lh, nr, y);
					doc.text(wl, valueIndent, y);
					y += lh;
				}
			}
			return y + 2;
		};

		data.forEach((row, idx) => {
			if (idx > 0) doc.addPage();
			let y = 18;

			// If a "Fertig" field exists and indicates NOT finished, draw a banner.
			const fertigValue = row['Fertig'];
			let finished = true;
			if (typeof fertigValue !== 'undefined') {
				finished = /^(true|1|yes|ja|fertig|done|y)$/i.test((fertigValue || '').trim());
			}
			if (!finished) {
				drawTopBanner(doc, pageWidth);
				y = Math.max(y, 30); // leave space below banner
			}

			const company = (row['Q01_Unternehmensname'] || '').trim() || `Antrag ${idx + 1}`;
			const person = (row['Vollständiger Name'] || '').trim();
			const date = (row['Abgegeben:'] || '').trim();

			// Company name heading
			doc.setFontSize(15);
			doc.setFont(undefined, 'bold');
			doc.setTextColor(0, 0, 0);
			const companyLines: string[] = doc.splitTextToSize(company, labelWidth);
			for (const cl of companyLines) {
				doc.text(cl, margin, y);
				y += 7;
			}

			// Person + Date subtitle
			if (person || date) {
				doc.setFontSize(9);
				doc.setFont(undefined, 'normal');
				doc.setTextColor(100, 100, 100);
				const sub = [person, date].filter(Boolean).join('   |   ');
				doc.text(sub, margin, y);
				doc.setTextColor(0, 0, 0);
				y += 5;
			}

			// Separator
			doc.setDrawColor(0, 0, 0);
			doc.setLineWidth(0.5);
			doc.line(margin, y, pageWidth - margin, y);
			y += 7;
			doc.setLineWidth(0.2);

			delete row['Fertig'];

			// Fields
			for (const [key, value] of Object.entries(row)) {
				if (!value.trim() || HEADER_FIELDS.has(key)) continue;
				y = writeField(stripQPrefix(key), value, idx + 1, y);
			}

			addCheckboxArea(idx + 1);
		});

		const timestamp = new Date().toISOString().slice(0, 10);
		doc.save(`Unternehmensantraege_${timestamp}.pdf`);
	}

	function handleCSVImport(event: Event) {
		const input = event.target as HTMLInputElement;
		const file = input.files?.[0];

		if (!file) return;

		const reader = new FileReader();
		reader.onload = (e) => {
			try {
				const arrayBuffer = e.target?.result as ArrayBuffer;
				const bytes = new Uint8Array(arrayBuffer);
				const isUtf8Bom = bytes[0] === 0xef && bytes[1] === 0xbb && bytes[2] === 0xbf;
				const decoder = new TextDecoder(isUtf8Bom ? 'utf-8' : 'windows-1252');
				const csvContent = decoder.decode(arrayBuffer);
				const parsedData = parseCSV(csvContent, csvSeparator);
				console.log('Parsed CSV Data:', parsedData);
				generatePDF(parsedData);
			} catch (error) {
				console.error('Error processing CSV:', error);
				alert("Error processing CSV file. Please ensure it's in the correct format.");
			}
		};

		reader.readAsArrayBuffer(file);
		// Reset input
		if (fileInput) {
			fileInput.value = '';
		}
	}

	function testTransaction() {
		fetch('http://localhost:7002/api/admin/transaction', {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
				Authorization: `Bearer ${db.authStore.token}`
			},
			body: JSON.stringify({
				sender: '0000-0001',
				receiver: '0000-0002',
				amount: '50.00',
				description: 'Debug transaction from admin debug page',
				moveMoney: true,
				logTransaction: true
			})
		})
			.then((response) => {
				if (response.ok) {
					alert('Debug transaction sent successfully!');
				} else {
					alert('Failed to send debug transaction.');
				}
			})
			.catch((error) => {
				console.error('Error sending debug transaction:', error);
				alert('Error sending debug transaction.');
			});
	}
	function readSignature() {
		// Funktion zum Auslesen und Verifizieren der Signatur
		fetch(
			`http://localhost:7002/api/verify-signature?signature=${encodeURIComponent(signatureInput)}`,
			{
				method: 'GET',
				headers: {
					'Content-Type': 'application/json'
				}
			}
		)
			.then((response) => response.json())
			.then((data) => {
				console.log(JSON.parse(data.payload));
			})
			.catch((error) => {
				console.error('Error verifying signature:', error);
				alert('Error verifying signature.');
			});
	}
	async function migrateToUserData() {
		if (!confirm('Are you sure you want to migrate users to user_data?')) return;
		let users = await db.collection('users').getFullList();
		let counter = 0;

		await Promise.all(
			users.map(async (user) => {
				await db.collection('user_data').create({
					id: user.id,
					user: user.id,
					registered: user.initialPasswordSet,
					firstTimeLogin: user.firstTimeLogin,
					financeTrackingAccess: user.role == "employee" || user.role == "manager",
					warehouseAccess: user.role == "warehouse" || user.role == "manager",
					euroSpent: 0,
					euroAvailable: 55.00
				});
				db.collection('users').update(user.id, {
					data: user.id
				});
				counter++;
				console.log(counter);
			})
		);
		console.log(`Migrated ${counter} users to user_data.`);
	}
	async function migrateToStats() {
		if (!confirm('Are you sure you want to migrate users to account_stats?')) return;
		let users = await db.collection('accounts').getFullList();
		let counter = 0;

		await Promise.all(
			users.map(async (user) => {
				await db.collection("account_stats").create({
					id: user.id,
					account: user.id,
					incomeLog: [],
					expenseLog: []
				});
				counter++;
				console.log(counter);
			})
		);
		console.log(`Migrated ${counter} users to stats.`);
	}
</script>

<title>Admin - Debug</title>

<AdminSite>
	<h1>Debug Page</h1>
	<p>This is a debug page for admin users.</p>
	<div class="flex flex-col gap-2">
		<button class="btn btn-success" on:click={testTransaction}
			>Send Debug Transaction to local server</button
		>
		<input
			type="text"
			bind:value={signatureInput}
			autocomplete="off"
			placeholder="Enter signature"
		/>
		<button class="btn btn-primary" on:click={readSignature}>Read Signature</button>

		<div class="divider"></div>

		<h2>CSV to PDF Converter</h2>
		<div class="form-control">
			<!-- svelte-ignore a11y_label_has_associated_control -->
			<label class="label">
				<span class="label-text">CSV Separator Format</span>
			</label>
			<div class="flex gap-4">
				<label class="label cursor-pointer">
					<input
						type="radio"
						name="separator"
						value=","
						bind:group={csvSeparator}
						class="radio radio-sm"
					/>
					<span class="label-text ml-2">Comma (,)</span>
				</label>
				<label class="label cursor-pointer">
					<input
						type="radio"
						name="separator"
						value=";"
						bind:group={csvSeparator}
						class="radio radio-sm"
					/>
					<span class="label-text ml-2">Semi-colon (;)</span>
				</label>
			</div>
		</div>
		<input
			bind:this={fileInput}
			type="file"
			accept=".csv"
			on:change={handleCSVImport}
			class="file-input file-input-bordered w-full"
		/>
		<button class="btn btn-info" on:click={() => fileInput?.click()}>
			Import CSV & Convert to PDF
		</button>
		<button class="btn btn-warning" on:click={migrateToStats}>
			Migrate Users to account_stats
		</button>
	</div>
</AdminSite>

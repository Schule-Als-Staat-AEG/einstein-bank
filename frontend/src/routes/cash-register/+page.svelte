<script lang="ts">
	import { browser } from '$app/environment';
	import { getAuthStore, isSuperuser } from '$lib/auth';
	import db, { server } from '$lib/connections';
	import { getSetting } from '$lib/settings';
	import Currency from '$modules/Currency.svelte';
	import Site from '$modules/Site.svelte';
	import { onDestroy, onMount } from 'svelte';
	import {
		ShoppingCart,
		Trash2,
		CreditCard,
		Plus,
		Minus,
		Camera,
		X,
		CheckCircle,
		Info,
		Receipt,
		ScanLine,
		Pencil,
		QrCode,
		Snowflake,
		Tag,
		Settings
	} from '@lucide/svelte';
	import { fetchCaptcha, solveCaptcha } from '$lib/captcha';
	import PinInputModal from '$modules/modals/PinInputModal.svelte';

	// Products
	let products: Set<any> = new Set();
	let card: Set<any> = new Set();

	let ownAccount: any = { id: '' };
	let ownUser: any = { name: '' };

	let paymentModal: HTMLDialogElement;
	let confirmationModal: HTMLDialogElement;
	let successModal: HTMLDialogElement;
	let pinModal: PinInputModal;

	let productModal: HTMLDialogElement;
	let editProductModal: HTMLDialogElement;
	let createProductModal: HTMLDialogElement;

	let discountModal: HTMLDialogElement;
	let extraCostModal: HTMLDialogElement;

	let settingsModal: HTMLDialogElement;

	let createProductName = '';
	let createProductPrice = 0;

	let editProductName = '';
	let editProductPrice = 0;
	let editProductID = '';

	let productModalPage = 1;
	let productModalPerPage = 5;
	let productModalFiler = '';

	let confirmationLoading = false;
	let confirmationError = false;
	let confirmationErrorMessage = 'Salve, da lief was schief.';

	let enteredPin = '';

	// QR Scanner
	let codeReader = null;
	let videoEl;
	let cameras = [];
	let currentCameraIndex = 0;
	let isScanning = false;
	let isStarting = false;
	let scannerError = '';
	let lastRaw = '';
	let lastUrl = null;
	let urlError = '';

	// Page
	let tabsEl: any;

	let paymentData: any = { sender: '', code: '' };
	let senderID = '';
	let senderAccount: any = null;
	let senderData: any = null;
	let senderUserData: any = null;

	// Tax calculation [DEPRECATED, user /api/calculate-tax instead]
	let _taxRate = 0; // decimal, e.g. 0.19 for 19%
	let _subtotal = 0;
	let _taxableAmount = 0;
	let _totalTax = 0;
	let _totalDeduction = 0;
	let totalTax = 0;
	let totalDeduction = 0;

	let taxEnabled = true;

	// For generating QR code for phone payments
	let qrCodeData = '';
	let qrCodeImage = '';

	let timeToLive = 1 * 1000; // 1 Second, the time to wait until fetching the tax updates
	let nextUpdate = Date.now() + timeToLive;
	let updateTax = false;
	let taxTimer: any;

	// Bonus cost:
	$: totalDiscount = enteredDiscount / 100; // In percentage, e.g. 0.1 for 10% discount
	let totalExtraCost = 0; // In Currency

	let enteredDiscount: number = 0;

	let askForTip = false;
	let tipSettings: number = 0;

	// Listens for the Transaction to be processed (Only happens for Phone Payments)
	function registerTransactionListener() {
		if (!browser) return;
		console.log('Registering transaction listener for cash payment...');
		db.collection('transactions').subscribe('*', function (e) {
			if (e.action === 'create') {
				// Check data validity
				if (
					e.record.receiver == ownAccount.id &&
					e.record.received >= subtotal - 0.01 &&
					e.record.received <= subtotal + 0.01
				) {
					successModal.showModal();
					paymentModal.close();
					db.collection('transactions').unsubscribe('*');
				}
			}
		});
	}

	onMount(async () => {
		if (!browser) return;
		ownUser = getAuthStore().record;
		ownAccount = await db.collection('accounts').getFirstListItem(`user = "${ownUser.id}"`);
		taxTimer = setInterval(() => {
			if (updateTax && Date.now() >= nextUpdate) {
				updateTax = false;
				getTaxForCard();
				nextUpdate = Date.now() + timeToLive;
			}
		}, 50);
		await fetchProducts();
	});
	onDestroy(() => {
		if (browser) {
			clearInterval(taxTimer);
			stopScanner();
			db.collection('transactions').unsubscribe('*');
		}
	});

	async function fetchProducts() {
		const result = await db.collection('products').getFullList({
			filter: `company = "${ownUser.id}"`
		});
		products = new Set(result);
		card = new Set(products);
		card.forEach((product) => {
			product.quantity = 0;
		});
	}
	//#region Card management
	function addToCard(product) {
		product.quantity += 1;
		card = card;
		refreshTax();
	}
	function removeFromCard(product) {
		if (product.quantity > 0) {
			product.quantity -= 1;
		}
		card = card;
		refreshTax();
	}
	function clearCard(confirmClear = false) {
		if (!confirmClear) {
			if (!confirm('Möchten Sie den Warenkorb wirklich leeren?')) {
				return;
			}
		}
		card.forEach((product) => {
			product.quantity = 0;
		});
		totalExtraCost = 0;
		enteredDiscount = 0;
		tipSettings = 0;
		card = card;
		refreshTax();
	}
	//#endregion

	//#region Modal management
	function openPaymentModal() {
		if (!browser) return;
		getTaxForCard();
		startScanner();
		selectTab(0);
		// svelte
		tabsEl.querySelectorAll("input[name='payment_tabs']")[0].checked = true; // Select first tab by default, DO NOT REMOVE CAUSE OF THE LISTENER
		paymentModal.showModal();
	}
	function closePaymentModal() {
		stopScanner();
		db.collection('transactions').unsubscribe('*');
		paymentModal.close();
	}
	function openConfirmationModal() {
		confirmationModal.showModal();
		// Further processing with data can be done here
	}
	//#endregion

	//#region QR Scanner
	function tryParseCode(text) {
		try {
			const data = JSON.parse(text);
			if (data && data.code && data.sender) {
				return data;
			}
		} catch (e) {
			console.warn('Invalid QR code data', e);
		}
		return null;
	}
	async function startScanner(deviceId = null) {
		if (isStarting) return;
		isStarting = true;
		scannerError = '';

		try {
			// dynamic import
			const mod = await import('@zxing/library');
			const { BrowserMultiFormatReader } = mod;
			if (!codeReader) codeReader = new BrowserMultiFormatReader();

			// list devices
			let devices = [];
			try {
				devices = await codeReader.listVideoInputDevices();
			} catch (e) {
				devices = [];
			}
			cameras = devices.map((d) => ({ id: d.deviceId, label: d.label || d.deviceId }));

			if (!deviceId) {
				// prefer back-facing
				const back = cameras.findIndex((c) => /back|rear|environment/i.test(c.label));
				currentCameraIndex = back >= 0 ? back : 0;
				deviceId = cameras[currentCameraIndex]?.id || null;
			}

			// Add video constraints for better scanning
			const constraints = {
				video: {
					deviceId: deviceId ? { exact: deviceId } : undefined,
					facingMode: deviceId ? undefined : { ideal: 'environment' },
					width: { ideal: 1280 },
					height: { ideal: 720 }
				}
			};

			// start decoding to the video element with hints
			const hints = new Map();
			const formats = await import('@zxing/library').then((m) => m.BarcodeFormat);
			hints.set(2, [formats.QR_CODE]); // DecodeHintType.POSSIBLE_FORMATS = 2

			await codeReader.decodeFromConstraints(
				constraints,
				videoEl,
				(result, err) => {
					if (result && result.getText() !== lastUrl) {
						lastRaw = result.getRawBytes()?.toString() || '';
						lastUrl = result.getText();
						handleResult(result.getText());
					}
				},
				hints
			);

			isScanning = true;
		} catch (e) {
			console.error('startScanner error', e);
			scannerError = String(e?.message || e);
		} finally {
			isStarting = false;
		}
	}
	async function stopScanner() {
		try {
			if (codeReader) {
				try {
					codeReader.reset();
				} catch (e) {}
				codeReader = null;
			}
			if (videoEl && videoEl.srcObject) {
				const s = videoEl.srcObject;
				s.getTracks().forEach((t) => t.stop());
				videoEl.srcObject = null;
			}
		} catch (e) {
			console.warn(e);
		}
		isScanning = false;
	}

	async function flipCamera() {
		if (!cameras || cameras.length === 0) return;
		currentCameraIndex = (currentCameraIndex + 1) % cameras.length;
		await stopScanner();
		await startScanner(cameras[currentCameraIndex].id);
	}
	//#endregion

	// This Method is called when a QR code is held up to the camera and successfully scanned
	// This could also be called OnCodeScanned or something like that
	async function handleResult(text) {
		console.log('Scanned code:', text);
		const data = tryParseCode(text);
		if (data) {
			console.log('Parsed data:', data);
			// Handle the parsed data here
			// Close the modal after successful scan
			paymentData = data;
			lastRaw = '';
			lastUrl = null;
			// Destroy the scanner instance
			stopScanner();

			closePaymentModal();
			if (await checkPinRequired(paymentData.sender)) {
				openPinModal();
				return;
			}
			openConfirmationModal();
			getTaxForCardAsCustomer();
			getAccountForConfirmation();
		}
	}

	async function checkPinRequired(account: string): Promise<boolean> {
		const data = await fetch(`${server}/api/code-required`, {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json'
			},
			body: JSON.stringify({ accountID: account })
		});
		if (data.ok) {
			const result = await data.json();
			return result.codeRequired;
		} else {
			throw new Error('Failed to check if code is required');
		}
	}

	// When this is called the money is gone
	async function confirmPayment(sender, code, amount) {
		// Note paymentData should have the sender and code from the QR
		// They are paymentData.sender and paymentData.code
		if (!browser) return;
		if (!sender || !code || !amount) {
			alert('Ungültige Zahlungsdaten!');
			return;
		}
		if (ownAccount.id === sender && !isSuperuser()) {
			alert('Du kannst nicht an dich selbst zahlen!');
			return;
		}
		if (!ownAccount.id) {
			alert('Dein Konto (Empfänger) konnte nicht gefunden werden!');
			return;
		}
		let description = getDescription();
		// Proceed with payment processing...
		senderID = sender;
		// Generate Captcha
		const { captchaID, question } = await fetchCaptcha();
		const captchaAnswer = await solveCaptcha(question);

		fetch(`${server}/api/transaction-with-code`, {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
				Authorization: `Bearer ${getAuthStore().token}`
			},
			credentials: 'omit',
			body: JSON.stringify({
				sender: sender,
				receiver: ownAccount.id,
				amount: amount,
				code: code,
				description: description,
				vat: true,
				captchaID: captchaID,
				captchaAnswer: captchaAnswer,
				pin: paymentData.pin || null
			})
		})
			.then(async (rsp) => {
				if (!rsp.ok) {
					const errorData = await rsp.json();
					throw new Error(errorData.message || 'Zahlung fehlgeschlagen');
				}
				let res = await rsp.json();
				if (!res.valid) {
					throw new Error(res.message || 'Zahlung fehlgeschlagen');
				}
				senderAccount = await db.collection('accounts').getOne(paymentData.sender);
				successModal.showModal();
				lastRaw = '';
				lastUrl = null;
				// Clear the card after successful payment
				clearCard(true);
				enteredDiscount = 0;
				totalExtraCost = 0;
				tipSettings = 0;
				getTaxForCard(); // Force update immediately after payment for next user
			})
			.catch((error) => {
				alert('Fehler bei der Zahlung: ' + error.message);
			});

		confirmationModal.close();
	}

	async function abortPayment() {
		stopScanner();
		tipSettings = 0;
		confirmationModal.close();
	}
	function openPinModal() {
		pinModal.resetPin();
		pinModal.showModal();
	}
	async function submitPin(pin: string) {
		await fetch(`${server}/api/validate-pin`, {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json'
			},
			body: JSON.stringify({ accountID: paymentData.sender, pin })
		})
			.then(async (response) => {
				if (response.ok) {
					paymentData.pin = pin;
					pinModal.closeModal();
					openConfirmationModal();
					getTaxForCardAsCustomer();
					getAccountForConfirmation();
				} else {
					alert('Falscher PIN!');
					pinModal.resetPin();
					openPinModal();
				}
			})
			.catch((error) => {
				alert('Fehler bei der PIN-Validierung: ' + error.message);
				pinModal.resetPin();
				openPinModal();
			});
	}
	function handlePinSubmit(event) {
		const { pin } = event.detail;
		// Make your API call here
		submitPin(pin);
	}

	//#region Tabs for the payment modal, switching between bank card payment and phone payment (with QR code)
	function selectTab(tab: number) {
		switch (tab) {
			case 0:
				// Bank card payment
				db.collection('transactions').unsubscribe('*');
				break;
			case 1:
				// Cash payment
				registerTransactionListener();
				createQRCode();
				break;
		}
	}
	async function createQRCode() {
		let amount = subtotal;

		const code = await getQRCode(amount);
		qrCodeData = JSON.stringify({ id: code });

		// Generate QR code image
		try {
			const QRCode = await import('qrcode');
			qrCodeImage = await QRCode.toDataURL(qrCodeData, {
				width: 256,
				margin: 2,
				color: {
					dark: '#000000',
					light: '#FFFFFF'
				}
			});
		} catch (error) {
			console.error('Failed to generate QR code:', error);
			qrCodeImage = '';
		}
	}
	async function getQRCode(amount: number): Promise<string> {
		let result;
		try {
			result = await db
				.collection('transaction_codes')
				.getFirstListItem(`receiver="${ownAccount.id}"`, { requestKey: null });
		} catch (e) {
			result = null;
		}
		if (result != null) {
			const record = await db.collection('transaction_codes').update(
				result.id,
				{
					amount: amount,
					description: getDescription()
				},
				{ requestKey: null }
			);
			return record.id as string;
		} else {
			const record = await db.collection('transaction_codes').create(
				{
					receiver: ownAccount.id,
					amount: amount,
					description: getDescription()
				},
				{ requestKey: null }
			);
			return record.id as string;
		}
	}
	//#endregion

	// This just generates the description for the payment
	function getDescription(): string {
		let description = 'Zahlung an ' + (ownUser.name ?? '') + '\n';
		// Add all items to description
		Array.from(card).forEach((product) => {
			if (product.quantity > 0) {
				description += `${product.quantity}x ${product.name}\n`;
			}
		});
		if (totalExtraCost > 0) {
			description += `Zusätzliche Kosten: ${totalExtraCost.toFixed(2)}\n`;
		}
		if (totalDiscount > 0) {
			description += `Rabatt: ${enteredDiscount}%\n`;
		}
		if (askForTip && tipSettings > 0) {
			description += `Trinkgeld: ${tipSettings === 1 ? '10%' : tipSettings === 2 ? '15%' : '20%'}\n`;
		}
		return description;
	}

	// This loads the Information for the paypal like confirm modal so that the user actually knows what they have and what they are paying for
	async function getAccountForConfirmation() {
		confirmationLoading = true;
		let data = null;
		try {
			data = await fetch(`${server}/api/account-with-code`, {
				headers: {
					Authorization: `Bearer ${getAuthStore().token}`,
					'Content-Type': 'application/json'
				},
				method: 'POST',
				body: JSON.stringify({
					code: paymentData.code,
					account: paymentData.sender,
					pin: paymentData.pin
				})
			});
			console.log(data);
			data = data.ok ? await data.json() : null;
			if (!data.valid) {
				confirmationError = true;
				confirmationErrorMessage = 'Fehler beim Abrufen des Kontos.';
				confirmationLoading = false;
				return;
			}
			senderData = data.account;
			senderUserData = data.user;
			confirmationError = false;
		} catch (error) {
			confirmationLoading = false;
			confirmationError = true;
			confirmationErrorMessage = 'Fehler beim Abrufen des Kontos für Bestätigung.';
			return;
		} finally {
			confirmationLoading = false;
		}
	}
	//#region Tax calculation
	async function getTaxForCard() {
		let data: any = await fetch(`${server}/api/calculate-tax`, {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
				Authorization: `Bearer ${getAuthStore().token}`
			},
			body: JSON.stringify({ amount: subtotal, vat: true, income: false })
		});
		data = data.ok ? await data.json() : null;
		if (data) {
			totalTax = data.totalTaxAmount;
			totalDeduction = subtotal + totalTax;
		} else {
			totalTax = 0;
			totalDeduction = subtotal;
		}
	}
	async function getTaxForCardAsCustomer() {
		let data: any = await fetch(`${server}/api/calculate-tax`, {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
				Authorization: `Bearer ${getAuthStore().token}`
			},
			body: JSON.stringify({
				amount: subtotal,
				vat: true,
				income: false,
				code: paymentData.code,
				sender: paymentData.sender,
				pin: paymentData.pin
			})
		});
		data = data.ok ? await data.json() : null;
		if (data) {
			totalTax = data.totalTaxAmount;
			totalDeduction = subtotal + totalTax;
		} else {
			totalTax = 0;
			totalDeduction = subtotal;
		}
	}
	// Force a tax refresh
	function refreshTax() {
		nextUpdate = Date.now() + timeToLive;
		updateTax = true;
	}
	//#endregion

	//#region Product Management
	function openProductModal() {
		productModal.showModal();
	}
	function openCreateProductModal() {
		createProductName = '';
		createProductPrice = 0;
		createProductModal.showModal();
	}
	function openEditProductModal(product: any) {
		editProductName = product.name;
		editProductPrice = product.price;
		editProductID = product.id;
		editProductModal.showModal();
	}
	async function saveEditedProduct(product: any) {
		if (!editProductName || editProductPrice <= 0) {
			alert('Ungültige Produktdaten!');
			return;
		}
		await db.collection('products').update(product.id, {
			name: editProductName,
			price: editProductPrice
		});
		await fetchProducts();
		editProductModal.close();
	}
	async function createProduct() {
		if (!createProductName || createProductPrice <= 0) {
			alert('Ungültige Produktdaten!');
			return;
		}
		await db
			.collection('products')
			.create({
				name: createProductName,
				price: createProductPrice,
				company: ownUser.id
			})
			.catch((error) => {
				console.log(error);
				console.log(error.data.data);
				if (error.data.data.company.code == 'validation_not_unique') {
					alert('Du hast bereits ein Produkt mit diesem Namen!');
					return;
				}
				alert('Fehler beim Erstellen des Produkts: ' + error.message);
			});
		await fetchProducts();
		createProductModal.close();
	}
	async function deleteProduct(product: any) {
		if (!confirm(`Möchten Sie das Produkt "${product.name}" wirklich löschen?`)) {
			return;
		}
		await db.collection('products').delete(product.id);
		await fetchProducts();
	}
	// Update the product in the db
	function updateProduct() {
		if (!editProductName || editProductPrice <= 0) {
			alert('Ungültige Produktdaten!');
			return;
		}
		db.collection('products')
			.update(editProductID, {
				name: editProductName,
				price: editProductPrice
			})
			.then(() => {
				editProductModal.close();
				fetchProducts();
			})
			.catch((error) => {
				console.log(error);
				console.log(error.data.data);
				if (error.data.data.company.code == 'validation_not_unique') {
					alert('Du hast bereits ein Produkt mit diesem Namen!');
					return;
				}
				alert('Fehler beim Aktualisieren des Produkts: ' + error.message);
			});
	}
	$: productsForModal = Array.from(products)
		.filter((p) => p.name.toLowerCase().includes(productModalFiler.toLowerCase()))
		.slice((productModalPage - 1) * productModalPerPage, productModalPage * productModalPerPage);

	$: productModalPageCount = Math.ceil(
		Array.from(products).filter((p) =>
			p.name.toLowerCase().includes(productModalFiler.toLowerCase())
		).length / productModalPerPage
	);

	//#endregion

	//#region Bonus management
	function openDiscountModal() {
		discountModal.showModal();
	}
	function openExtraCostModal() {
		extraCostModal.showModal();
	}
	//#endregion

	//#region Settings management
	function openSettingsModal() {
		settingsModal.showModal();
	}
	//#endregion
	$: subtotal =
		(Array.from(card).reduce(
			(total, product) => total + product.price * (product.quantity || 0),
			0
		) +
			totalExtraCost) *
		(1 - totalDiscount) *
		tipMultiplier;
	$: tipMultiplier =
		1 + (tipSettings === 1 ? 0.1 : tipSettings === 2 ? 0.15 : tipSettings === 3 ? 0.2 : 0);
	$: taxableAmount = subtotal;
</script>

<title>Kassensystem</title>
<Site>
	<div class="flex flex-col gap-4">
		<!-- Header -->
		<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
			<div class="flex flex-col md:flex-row md:items-center md:justify-between gap-3">
				<div class="flex items-center gap-3">
					<div
						class="w-11 h-11 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center shadow"
					>
						<ShoppingCart size={20} />
					</div>
					<div class="flex flex-col">
						<div class="flex flex-row">
							<h1 class="font-bold text-2xl leading-tight">Kassenübersicht</h1>
						</div>
						<p class="text-sm opacity-70 flex items-center gap-2">
							<Info size={16} />
							<span>Verwalte deine Produkte und führe Zahlungen durch.</span>
						</p>
					</div>
				</div>

				<div class="flex flex-col sm:flex-row gap-2">
					<button class="btn btn-outline" on:click={openSettingsModal}>
						<Settings size={18} />
						Einstellungen
					</button>
					<button class="btn btn-primary btn-outline" on:click={openProductModal}>
						<Pencil size={18} />
						Produkte verwalten
					</button>
				</div>
			</div>
		</div>

		<!-- Products Grid -->
		<div class="grid grid-cols-1 lg:grid-cols-3 gap-4">
			<!-- Available Products -->
			<div class="lg:col-span-2 flex flex-col">
				<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
					<div class="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-3">
						<h2 class="font-bold text-lg mb-4 flex items-center gap-2">
							<Receipt size={18} />
							Verfügbare Produkte
						</h2>
					</div>

					<div
						class="space-y-2 max-h-150 overflow-y-auto pr-1 grid grid-cols-1 xl:grid-cols-2 3xl:grid-cols-3 gap-3"
					>
						{#each card as product}
							<div
								class="rounded-2xl bg-base-100 border border-base-300 p-4 hover:shadow-md transition-shadow"
							>
								<div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
									<div class="flex-1 min-w-0">
										<h3 class="font-bold truncate">{product.name}</h3>
										<p class="text-xs opacity-60 font-mono truncate">ID: {product.id}</p>
										<div class="flex items-center gap-2 mt-2">
											<span class="text-lg font-semibold">{product.price}</span>
											<Currency size={18} />
										</div>
									</div>

									<div class="flex items-center gap-2">
										<button
											class="btn btn-circle btn-sm btn-outline"
											on:click={() => removeFromCard(product)}
											disabled={product.quantity === 0}
										>
											<Minus size={16} />
										</button>
										<input
											type="number"
											class="input input-bordered w-16 text-center rounded-lg"
											bind:value={product.quantity}
											min="0"
											on:change={() => (product.quantity = Math.max(0, product.quantity))}
										/>
										<button
											class="btn btn-circle btn-sm btn-primary"
											on:click={() => addToCard(product)}
										>
											<Plus size={16} />
										</button>
									</div>
								</div>
							</div>
						{/each}

						{#if card.size === 0}
							<div class="text-center py-8 opacity-70 col-span-full">
								<Receipt size={32} class="mx-auto mb-2 opacity-50" />
								<p>Keine Produkte verfügbar</p>
							</div>
						{/if}
					</div>
					<div class="divider flex"></div>
					<div class="flex gap-3 justify-center">
						<button class="btn btn-warning" on:click={openDiscountModal}>
							Rabatt hinzufügen
						</button>
						<button class="btn btn-accent" on:click={openExtraCostModal}>
							Zusätzliche Kosten hinzufügen
						</button>
					</div>
				</div>
			</div>

			<!-- Cart Summary -->
			<div>
				<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4">
					<div class="flex items-center justify-between">
						<h2 class="font-bold text-lg mb-4 flex items-center gap-2">
							<ShoppingCart size={18} />
							Warenkorb
						</h2>
						{#if Array.from(card).filter((p) => p.quantity > 0).length > 0}
							<p>{Array.from(card).filter((p) => p.quantity > 0).length} Artikel</p>
						{/if}
					</div>

					<div class="space-y-2 max-h-150 overflow-y-auto pr-1 gap-3">
						{#each Array.from(card).filter((p) => p.quantity > 0) as product}
							<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
								<div class="flex items-start justify-between gap-3">
									<div class="flex-1 min-w-0">
										<h3 class="font-bold truncate">{product.name}</h3>
										<div class="flex items-center gap-4 mt-2 text-sm">
											<div class="flex items-center gap-1">
												<span class="opacity-70">Preis:</span>
												<span class="font-semibold">{product.price}</span>
												<Currency size={14} />
											</div>
											<div class="flex items-center gap-1">
												<span class="opacity-70">Menge:</span>
												<span class="font-semibold">{product.quantity}</span>
											</div>
										</div>
									</div>

									<div class="text-right">
										<div class="text-sm opacity-70">Gesamt</div>
										<div class="flex items-center gap-1">
											<span class="text-lg font-bold">{product.price * product.quantity}</span>
											<Currency size={18} />
										</div>
									</div>
								</div>
							</div>
						{/each}
						{#if totalExtraCost > 0}
							<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
								<div class="flex items-start justify-between gap-3">
									<div class="flex-1 min-w-0">
										<h3 class="font-bold truncate">Zusätzliche Kosten</h3>
										<div class="flex items-center gap-4 mt-2 text-sm">
											<div class="flex items-center gap-1">
												<span class="opacity-70">Preis:</span>
												<span class="font-semibold">{totalExtraCost}</span>
												<Currency size={14} />
											</div>
										</div>
									</div>

									<div class="text-right">
										<div class="text-sm opacity-70">Gesamt</div>
										<div class="flex items-center gap-1">
											<span class="text-lg font-bold">{totalExtraCost}</span>
											<Currency size={18} />
										</div>
									</div>
								</div>
							</div>
						{/if}

						{#if Array.from(card).filter((p) => p.quantity > 0).length === 0}
							<div class="text-center py-8 opacity-70">
								<ShoppingCart size={32} class="mx-auto mb-2 opacity-50" />
								<p>Warenkorb ist leer</p>
								<p class="text-sm mt-1">Füge Produkte hinzu</p>
							</div>
						{/if}
					</div>

					{#if Array.from(card).filter((p) => p.quantity > 0).length > 0}
						{#if enteredDiscount > 0}
							<div class="rounded-2xl bg-base-100 border border-base-300 p-4 mb-2">
								<div class="flex items-center justify-between">
									<span class="font-bold text-lg">Vor Rabatt:</span>
									<div class="flex items-center gap-2">
										<span class="text-2xl font-bold"
											>{Array.from(card).reduce(
												(total, product) => total + product.price * (product.quantity || 0),
												0
											) + totalExtraCost}</span
										>
										<Currency size={24} />
									</div>
								</div>
							</div>
							<div class="rounded-2xl bg-base-100 border border-base-300 p-4 mb-2">
								<div class="flex items-center justify-between">
									<span class="font-bold text-lg">Rabatt:</span>
									<div class="flex items-center gap-2">
										<span class="text-2xl font-bold">{enteredDiscount}%</span>
									</div>
								</div>
							</div>
						{/if}
						<div class="divider flex"></div>
						<div class="flex flex-col 2xl:flex-row items-center justify-between">
							<span class="font-bold text-lg">Gesamtsumme:</span>
							<div class="flex items-center gap-2">
								<span class="text-2xl font-bold">{subtotal.toFixed(2)}</span>
								<Currency size={24} />
							</div>
						</div>
						<div class="flex flex-col 2xl:flex-row gap-3 justify-between mt-4">
							<button class="btn btn-error btn-outline" on:click={() => clearCard(false)}>
								<Trash2 size={18} />
								Warenkorb leeren
							</button>
							<button class="btn btn-primary" on:click={openPaymentModal}>
								<CreditCard size={18} />
								Zur Kasse
							</button>
						</div>
					{/if}
				</div>
			</div>
		</div>
	</div>
</Site>

<!-- Payment Modal -->
<dialog id="paymentModal" class="modal" bind:this={paymentModal}>
	<div class="modal-box w-full max-w-2xl rounded-2xl border border-base-300">
		<header class="flex items-start justify-between gap-3 mb-2">
			<div>
				<h3 class="text-xl font-bold flex items-center gap-2">
					<CreditCard size={20} />
					Bezahlen
				</h3>
				<p class="text-sm opacity-70 mt-1">Wähle aus, wie du bezahlen möchtest</p>
			</div>
			<button class="btn btn-ghost btn-sm btn-circle" on:click={closePaymentModal}>
				<X size={18} />
			</button>
		</header>

		<div class="tabs tabs-border" bind:this={tabsEl} role="tablist">
			<input
				type="radio"
				name="payment_tabs"
				class="tab"
				style={'background-color: #00000000;'}
				aria-label="Zahlen per Bankkarte"
				checked
				on:click={() => selectTab(0)}
			/>

			<div class="tab-content border-base-300 bg-base-100 p-6">
				<div class="mt-4">
					{#if cameras.length > 1}
						<button class="btn btn-outline w-full mb-3 rounded-2xl" on:click={flipCamera}>
							<Camera size={18} />
							Kamera wechseln ({currentCameraIndex + 1}/{cameras.length})
						</button>
					{/if}

					<!-- Scanner -->
					<div
						class="rounded-2xl bg-base-100 border border-base-300 overflow-hidden relative aspect-video"
					>
						<video bind:this={videoEl} autoplay playsinline class="w-full h-full object-cover"
						></video>
						{#if isScanning}
							<div class="absolute inset-0 pointer-events-none">
								<div class="absolute inset-0 border-4 border-primary/30 rounded-2xl"></div>
								<div
									class="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-48 h-48 border-4 border-primary rounded-2xl"
								>
									<ScanLine
										size={32}
										class="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 text-primary animate-pulse"
									/>
								</div>
							</div>
						{/if}
						{#if isStarting}
							<div class="absolute inset-0 bg-base-300/80 flex items-center justify-center">
								<span class="loading loading-spinner loading-lg"></span>
							</div>
						{/if}
					</div>

					{#if scannerError}
						<div class="alert alert-error mt-3 rounded-2xl">
							<X size={18} />
							<span>Fehler: {scannerError}</span>
						</div>
					{/if}
				</div>

				{#if taxEnabled}
					<div class="mt-4 rounded-2xl bg-base-100 border border-base-300 p-4">
						<div class="font-bold mb-3 flex items-center gap-2">
							<Receipt size={18} />
							Steuer-Details
						</div>
						<div class="space-y-2 text-sm">
							<div class="flex items-center justify-between">
								<span class="opacity-70">Betrag:</span>
								<span class="flex items-center gap-1 font-semibold">
									{subtotal}
									<Currency size={16} />
								</span>
							</div>
							<div class="flex items-center justify-between">
								<span class="opacity-70">Steuerbetrag:</span>
								<span class="flex items-center gap-1 font-semibold">
									{totalTax}
									<Currency size={16} />
								</span>
							</div>
							<div class="divider my-2"></div>
							<div class="flex items-center justify-between">
								<span class="font-bold">Zu bezahlen:</span>
								<span class="flex items-center gap-2 text-lg font-bold">
									{totalDeduction}
									<Currency size={20} />
								</span>
							</div>
						</div>
						<p class="text-xs opacity-60 mt-3 hidden">
							Diese Rechnung enthält nicht den Steuerfreibetrag. Der angezeigte Betrag ist das
							Maximum, welches abgebucht werden kann.
						</p>
					</div>
				{/if}

				<div class="mt-4 rounded-2xl bg-base-100 border border-base-300 p-3">
					<p class="text-sm opacity-70">Du zahlst an:</p>
					<p class="font-bold">{ownUser?.name ?? ''}</p>
					<p class="text-xs opacity-60 font-mono">Konto: {ownAccount.id ?? ''}</p>
				</div>

				<div class="modal-action">
					<button class="btn btn-error btn-outline" on:click={closePaymentModal}>
						<X size={18} />
						Abbrechen
					</button>
				</div>
			</div>

			<!--- Pay with Phone Part --->
			<input
				type="radio"
				name="payment_tabs"
				class="tab"
				style={'background-color: #00000000;'}
				aria-label="Zahlen per App"
				on:click={() => selectTab(1)}
			/>

			<div class="tab-content border-base-300 bg-base-100 p-6">
				<p class="font-bold mb-2">
					Wichtig: Nutzte den 'Scannen' Tab auf der Webseite und NICHT den Standart QR-Code Scanner
				</p>
				<div class="rounded-2xl bg-base-100 border border-base-300 p-4 text-center">
					{#if qrCodeImage}
						<img src={qrCodeImage} alt="QR Code" class="w-64 h-64 mx-auto rounded-2xl" />
						<p class="text-sm opacity-70 mt-3">QR-Code bereit zum Scannen</p>
					{:else}
						<div
							class="w-32 h-32 bg-base-200 border-2 border-dashed border-base-300 rounded-2xl mx-auto flex items-center justify-center"
						>
							<QrCode size={48} class="opacity-50" />
						</div>
						<p class="text-sm opacity-70 mt-3">QR-Code wird generiert...</p>
					{/if}
				</div>
				{#if taxEnabled}
					<div class="mt-4 rounded-2xl bg-base-100 border border-base-300 p-4">
						<div class="font-bold mb-3 flex items-center gap-2">
							<Receipt size={18} />
							Steuer-Details
						</div>
						<div class="space-y-2 text-sm">
							<div class="flex items-center justify-between">
								<span class="opacity-70">Betrag:</span>
								<span class="flex items-center gap-1 font-semibold">
									{subtotal}
									<Currency size={16} />
								</span>
							</div>
							<div class="flex items-center justify-between">
								<span class="opacity-70">Steuer:</span>
								<span class="flex items-center gap-1 font-semibold">
									{totalTax}
									<Currency size={16} />
								</span>
							</div>
							<div class="divider my-2"></div>
							<div class="flex items-center justify-between">
								<span class="font-bold">Zu bezahlen:</span>
								<span class="flex items-center gap-2 text-lg font-bold">
									{totalDeduction}
									<Currency size={20} />
								</span>
							</div>
						</div>
						<p class="text-xs opacity-60 mt-3 hidden">
							Diese Rechnung enthält nicht den Steuerfreibetrag. Der angezeigte Betrag ist das
							Maximum, welches abgebucht werden kann.
						</p>
					</div>
				{/if}

				<div class="mt-4 rounded-2xl bg-base-100 border border-base-300 p-3">
					<p class="text-sm opacity-70">Du zahlst an:</p>
					<p class="font-bold">{ownUser?.name ?? ''}</p>
					<p class="text-xs opacity-60 font-mono">Konto: {ownAccount.id ?? ''}</p>
				</div>

				<div class="modal-action">
					<button class="btn btn-error btn-outline" on:click={closePaymentModal}>
						<X size={18} />
						Abbrechen
					</button>
				</div>
			</div>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button on:click={closePaymentModal}>close</button>
	</form>
</dialog>

<!-- Confirm Modal V.2 -->
<dialog id="confirmationModal" class="modal" bind:this={confirmationModal}>
	<div class="modal-box w-full max-w-xl rounded-2xl border border-base-300">
		<header class="flex items-start justify-between gap-3">
			<div>
				<h3 class="text-xl font-bold flex items-center gap-2">
					<CheckCircle size={20} />
					Zahlung bestätigen
				</h3>
			</div>
		</header>
		<div class="mt-4 p-4">
			{#if confirmationLoading}
				<div class="flex items-center justify-center py-10">
					<span class="loading loading-spinner loading-huge"></span>
				</div>
			{:else}
				{#if confirmationError}
					<div class="alert alert-error rounded-2xl">
						<X size={18} />
						<span>{confirmationErrorMessage}</span>
					</div>
				{:else}
					<div class="space-y-4">
						<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
							<div class="flex items-start justify-between gap-4">
								<div>
									<p class="text-xs opacity-60 mb-2">ABSENDERKONTO</p>
									<div class="flex items-center gap-3">
										<div
											class="w-12 h-12 rounded-full bg-primary/20 flex items-center justify-center"
										>
											<span class="text-lg font-bold text-primary"
												>{senderUserData?.name?.charAt(0) ?? '?'}</span
											>
										</div>
										<div>
											<p class="font-semibold">{senderUserData?.name ?? 'Unbekannt'}</p>
											<p class="text-xs opacity-60 font-mono">{senderData?.id ?? 'ID'}</p>
										</div>
									</div>
								</div>
								<div class="text-right">
									<p class="text-xs opacity-60 mb-1">KONTOSTAND</p>
									<p class="text-2xl font-bold flex items-center justify-end gap-2">
										{Number(senderData?.balance ?? 0).toFixed(2)}
										<Currency size={24} />
									</p>
								</div>
							</div>
						</div>

						<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
							<p class="text-xs opacity-60 mb-3">ZAHLUNGSÜBERSICHT</p>
							<div class="space-y-2 text-sm">
								<div class="flex items-center justify-between">
									<span>Aktueller Kontostand</span>
									<span class="font-semibold flex items-center gap-1">
										{Number(senderData?.balance ?? 0).toFixed(2)}
										<Currency size={14} />
									</span>
								</div>
								<div class="flex items-center justify-between">
									<span>- Kosten des Warenkorbs</span>
									<span class="font-semibold flex items-center gap-1">
										-{Number(subtotal).toFixed(2)}
										<Currency size={14} />
									</span>
								</div>
								<div class="flex items-center justify-between">
									<span>- Steuern</span>
									<span class="font-semibold flex items-center gap-1">
										-{Number(taxEnabled ? totalTax : 0).toFixed(2)}
										<Currency size={14} />
									</span>
								</div>
								<div class="divider my-1"></div>
								<div class="flex items-center justify-between">
									<span class="font-bold">Verbleibender Kontostand</span>
									<span class="font-bold flex items-center gap-1">
										{Number(
											(
												Number(senderData?.balance ?? 0) -
												Number(subtotal) -
												Number(taxEnabled ? totalTax : 0)
											).toFixed(2)
										)}
										<Currency size={16} />
									</span>
								</div>
								<div class="divider my-1"></div>
								<div>
									<p class="text-xs opacity-60">EMPFÄNGER</p>
									<p class="font-semibold">{ownUser?.name ?? 'Unbekannt'}</p>
									<p class="text-xs opacity-60 font-mono">{ownAccount?.id ?? 'ID'}</p>
								</div>
							</div>
						</div>
						{#if askForTip}
							<div class="rounded-2xl bg-base-100 border border-base-300 p-4 md:p-5 shadow-sm">
								<div class="mb-3">
									<p class="text-xs font-semibold tracking-wide opacity-60">TRINKGELD</p>
									<p class="text-sm opacity-70">Wähle optional einen Betrag aus.</p>
								</div>

								<div class="grid grid-cols-2 md:grid-cols-4 gap-2">
									<label
										class="cursor-pointer flex flex-col items-center justify-center gap-2 border border-base-300 rounded-xl p-3 hover:border-primary transition-colors"
									>
										<span class="font-medium">10%</span>
										<input
											type="radio"
											name="tip"
											class="radio radio-primary radio-sm"
											checked={tipSettings === 1}
											on:click={() => {
												tipSettings = 1;
												refreshTax();
											}}
										/>
									</label>

									<label
										class="cursor-pointer flex flex-col items-center justify-center gap-2 border border-base-300 rounded-xl p-3 hover:border-primary transition-colors"
									>
										<span class="font-medium">15%</span>
										<input
											type="radio"
											name="tip"
											class="radio radio-primary radio-sm"
											checked={tipSettings === 2}
											on:click={() => {
												tipSettings = 2;
												refreshTax();
											}}
										/>
									</label>

									<label
										class="cursor-pointer flex flex-col items-center justify-center gap-2 border border-base-300 rounded-xl p-3 hover:border-primary transition-colors"
									>
										<span class="font-medium">20%</span>
										<input
											type="radio"
											name="tip"
											class="radio radio-primary radio-sm"
											checked={tipSettings === 3}
											on:click={() => {
												tipSettings = 3;
												refreshTax();
											}}
										/>
									</label>

									<label
										class="cursor-pointer flex flex-col items-center justify-center gap-2 border border-base-300 rounded-xl p-3 hover:border-primary transition-colors"
									>
										<span class="font-medium text-center">Kein Trinkgeld</span>
										<input
											type="radio"
											name="tip"
											class="radio radio-primary radio-sm"
											checked={tipSettings === 0}
											on:click={() => {
												tipSettings = 0;
												refreshTax();
											}}
										/>
									</label>
								</div>
							</div>
						{/if}
					</div>
					<p class="text-sm opacity-70 text-center w-full mt-2">
						Bei dem Bestätigen der Zahlung stimmst du den <a
							href="/policies/terms-of-service"
							class="link link-primary">Nutzungsbedingungen</a
						> zu.
					</p>
				{/if}
			{/if}
		</div>

		<div class="modal-action">
			{#if senderData?.frozen}
				<button
					class="btn btn-error"
					on:click={() => confirmPayment(paymentData.sender, paymentData.code, subtotal)}
					disabled
				>
					<Snowflake size={18} />
					Konto eingefroren - Zahlen nicht möglich
				</button>
			{:else}
				<button
					class="btn btn-primary"
					on:click={() => confirmPayment(paymentData.sender, paymentData.code, subtotal)}
					disabled={confirmationLoading || confirmationError || senderData?.frozen}
				>
					<CheckCircle size={18} />
					Bestätigen & bezahlen
				</button>
			{/if}
			<button class="btn btn-outline" on:click={abortPayment}>
				<X size={18} />
				Abbrechen
			</button>
		</div>
	</div>
	<!-- Modal Backdrop -->
	<form method="dialog" class="modal-backdrop">
		<button on:click={abortPayment}>close</button>
	</form>
</dialog>

<!-- Success Modal -->
<dialog id="successModal" class="modal" bind:this={successModal}>
	<div class="modal-box w-full max-w-md rounded-2xl border border-base-300">
		<div class="text-center">
			<div
				class="w-16 h-16 rounded-full bg-success/20 flex items-center justify-center mx-auto mb-4"
			>
				<CheckCircle size={32} class="text-success" />
			</div>
			<h3 class="text-xl font-bold">Zahlung erfolgreich</h3>
			<p class="text-sm opacity-70 mt-2">Die Zahlung wurde erfolgreich durchgeführt.</p>
			<p class="text-sm opacity-70 mt-1">Zeige dieses Fenster dem Kassierer</p>

			<!-- {#if senderAccount}
        <div class="mt-4 rounded-2xl bg-base-200 border border-base-300 p-4">
          <div class="text-sm opacity-70">Verbleibender Kontostand</div>
          <div class="flex items-center justify-center gap-2 mt-1">
            <span class="text-2xl font-bold">{senderAccount.balance ?? 0}</span>
            <Currency size={24} />
          </div>
        </div>
      {/if} -->
		</div>

		<div class="modal-action justify-center">
			<button class="btn btn-primary" on:click={() => successModal.close()}>
				<CheckCircle size={18} />
				Fertig
			</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Products Modal -->
<dialog id="productModal" class="modal" bind:this={productModal}>
	<div class="modal-box w-full max-w-3xl rounded-2xl border border-base-300">
		<header class="flex items-start justify-between gap-3 mb-2">
			<div>
				<h3 class="text-xl font-bold flex items-center gap-2">
					<Receipt size={20} />
					Produktverwaltung
				</h3>
				<p class="text-sm opacity-70 mt-1">Hier kannst du deine Produkte verwalten</p>
			</div>
			<button class="btn btn-ghost btn-sm btn-circle" on:click={() => productModal.close()}>
				<X size={18} />
			</button>
		</header>

		<input
			type="text"
			placeholder="Produkte suchen..."
			class="input input-bordered w-full rounded-lg mb-4"
			bind:value={productModalFiler}
		/>

		<!-- Product management content goes here -->
		<div class="overflow-x-auto rounded-2xl border border-base-300">
			<table class="table table-zebra w-full">
				<thead>
					<tr>
						<th>Name</th>
						<th>Preis</th>
						<th>Aktionen</th>
					</tr>
				</thead>
				<tbody>
					{#each productsForModal as product}
						<tr>
							<td>{product.name}</td>
							<td
								><div class="flex flex-row items-center gap-1">
									{product.price}
									<Currency size={16} />
								</div></td
							>
							<td>
								<button class="btn btn-sm" on:click={() => openEditProductModal(product)}>
									<Pencil size={16} />
								</button>
								<button class="btn btn-sm btn-error" on:click={() => deleteProduct(product)}>
									<Trash2 size={16} />
								</button>
							</td>
						</tr>
					{/each}
				</tbody>
			</table>
		</div>

		<div class="flex w-full items-center justify-center mt-4">
			<div class="join">
				{#if productModalPage > 1}
					<button class="join-item btn" on:click={() => productModalPage--}>«</button>
				{:else}
					<button class="join-item btn" disabled>«</button>
				{/if}
				<button class="join-item btn">Seite {productModalPage}</button>
				{#if productModalPage < productModalPageCount}
					<button class="join-item btn" on:click={() => productModalPage++}>»</button>
				{:else}
					<button class="join-item btn" disabled>»</button>
				{/if}
			</div>
		</div>

		<div class="modal-action flex justify-between">
			<button class="btn btn-success" on:click={openCreateProductModal}>
				<Plus size={18} />
				Neues Produkt
			</button>
			<button class="btn btn-outline" on:click={() => productModal.close()}>
				<X size={18} />
				Schließen
			</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Create Product Modal -->
<dialog id="createProductModal" class="modal" bind:this={createProductModal}>
	<div class="modal-box w-full max-w-md rounded-2xl border border-base-300">
		<header class="flex items-start justify-between gap-3 mb-2">
			<div>
				<h3 class="text-xl font-bold flex items-center gap-2">
					<Plus size={20} />
					Neues Produkt erstellen
				</h3>
			</div>
			<button class="btn btn-ghost btn-sm btn-circle" on:click={() => createProductModal.close()}>
				<X size={18} />
			</button>
		</header>

		<div class="space-y-4">
			<div>
				<!-- svelte-ignore a11y_label_has_associated_control -->
				<label class="block text-sm font-medium mb-1">Produktname</label>
				<input
					type="text"
					class="input input-bordered w-full rounded-lg"
					bind:value={createProductName}
					placeholder="z.B. Schokoriegel"
				/>
			</div>
			<div>
				<!-- svelte-ignore a11y_label_has_associated_control -->
				<label class="block text-sm font-medium mb-1">Preis</label>
				<div class="flex flex-row gap-1">
					<input
						type="number"
						class="input input-bordered w-full rounded-lg"
						bind:value={createProductPrice}
						placeholder="z.B. 1.50"
						min="0"
						step="0.01"
					/>
					<Currency size={36}></Currency>
				</div>
			</div>
		</div>

		<div class="modal-action">
			<button class="btn btn-primary" on:click={createProduct}>
				<CheckCircle size={18} />
				Erstellen
			</button>
			<button class="btn btn-outline" on:click={() => createProductModal.close()}>
				<X size={18} />
				Abbrechen
			</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Edit Product Modal -->
<dialog id="editProductModal" class="modal" bind:this={editProductModal}>
	<div class="modal-box w-full max-w-md rounded-2xl border border-base-300">
		<header class="flex items-start justify-between gap-3 mb-2">
			<div>
				<h3 class="text-xl font-bold flex items-center gap-2">
					<Pencil size={20} />
					Produkt bearbeiten
				</h3>
			</div>
			<button class="btn btn-ghost btn-sm btn-circle" on:click={() => editProductModal.close()}>
				<X size={18} />
			</button>
		</header>

		<div class="space-y-4">
			<div>
				<!-- svelte-ignore a11y_label_has_associated_control -->
				<label class="block text-sm font-medium mb-1">Produktname</label>
				<input
					type="text"
					class="input input-bordered w-full rounded-lg"
					bind:value={editProductName}
					placeholder="z.B. Schokoriegel"
				/>
			</div>
			<div>
				<!-- svelte-ignore a11y_label_has_associated_control -->
				<label class="block text-sm font-medium mb-1">Preis</label>
				<div class="flex flex-row gap-1">
					<input
						type="number"
						class="input input-bordered w-full rounded-lg"
						bind:value={editProductPrice}
						placeholder="z.B. 1.50"
						min="0"
						step="0.01"
					/>
					<Currency size={36}></Currency>
				</div>
			</div>
		</div>

		<div class="modal-action">
			<button class="btn btn-primary" on:click={updateProduct}>
				<CheckCircle size={18} />
				Speichern
			</button>
			<button class="btn btn-outline" on:click={() => editProductModal.close()}>
				<X size={18} />
				Abbrechen
			</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Discount Modal -->
<dialog id="discountModal" class="modal" bind:this={discountModal}>
	<div class="modal-box w-full max-w-md rounded-2xl border border-base-300">
		<header class="flex items-start justify-between gap-3 mb-2">
			<div>
				<h3 class="text-xl font-bold flex items-center gap-2">
					<Tag size={20} />
					Rabatt hinzufügen
				</h3>
			</div>
			<button class="btn btn-ghost btn-sm btn-circle" on:click={() => discountModal.close()}>
				<X size={18} />
			</button>
		</header>

		<!-- Discount form content goes here -->
		<p class="font-medium opacity-70 mb-1">Rabatt in Prozent (0% - 99%):</p>
		<div class="flex justify-between items-center gap-3">
			<input
				type="number"
				class="input input-bordered w-full rounded-lg"
				bind:value={enteredDiscount}
				placeholder="Rabatt in Prozent"
				min="0"
				max="99"
				step="1"
				on:input={(e) => {
					if (enteredDiscount < 0) enteredDiscount = 0;
					if (enteredDiscount > 99) enteredDiscount = 99;
				}}
			/>
			<p class="text-lg font-bold">%</p>
		</div>

		<div class="modal-action">
			<button
				class="btn btn-error"
				on:click={() => {
					enteredDiscount = 0;
					discountModal.close();
				}}
			>
				<X size={18} />
				Rabatt entfernen
			</button>
			<button
				class="btn btn-primary"
				on:click={() => {
					discountModal.close();
				}}
			>
				<CheckCircle size={18} />
				Fertig
			</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Additional Costs Modal -->
<dialog id="extraCostModal" class="modal" bind:this={extraCostModal}>
	<div class="modal-box w-full max-w-md rounded-2xl border border-base-300">
		<header class="flex items-start justify-between gap-3 mb-2">
			<div>
				<h3 class="text-xl font-bold flex items-center gap-2">
					<Plus size={20} />
					Zusätzliche Kosten hinzufügen
				</h3>
			</div>
			<button class="btn btn-ghost btn-sm btn-circle" on:click={() => extraCostModal.close()}>
				<X size={18} />
			</button>
		</header>

		<!-- Extra cost form content goes here -->
		<p class="font-medium opacity-70 mb-1">Betrag der zusätzlichen Kosten:</p>
		<div class="flex justify-between items-center gap-3">
			<input
				type="number"
				class="input input-bordered w-full rounded-lg"
				bind:value={totalExtraCost}
				placeholder="Betrag"
				min="0"
				step="0.01"
				on:input={(e) => {
					if (totalExtraCost < 0) totalExtraCost = 0;
				}}
			/>
			<Currency size={36}></Currency>
		</div>

		<div class="modal-action">
			<button
				class="btn btn-error"
				on:click={() => {
					totalExtraCost = 0;
					extraCostModal.close();
				}}
			>
				<X size={18} />
				Entfernen
			</button>
			<button
				class="btn btn-primary"
				on:click={() => {
					extraCostModal.close();
				}}
			>
				<CheckCircle size={18} />
				Fertig
			</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Settings Modal -->
<dialog id="settingsModal" class="modal" bind:this={settingsModal}>
	<div class="modal-box w-full max-w-md rounded-2xl border border-base-300">
		<header class="flex items-start justify-between gap-3 mb-2">
			<div>
				<h3 class="text-xl font-bold flex items-center gap-2">
					<Settings size={20} />
					Einstellungen
				</h3>
			</div>
			<button class="btn btn-ghost btn-sm btn-circle" on:click={() => settingsModal.close()}>
				<X size={18} />
			</button>
		</header>

		<!-- Settings form content goes here -->
		<div class="flex flex-col gap-2">
			<label class="label cursor-pointer flex justify-between">
				<span class="label-text">Nach Trinkgeld Fragen</span>
				<input type="checkbox" class="toggle" bind:checked={askForTip} />
			</label>
		</div>

		<div class="modal-action">
			<button
				class="btn btn-primary"
				on:click={() => {
					settingsModal.close();
				}}
			>
				<CheckCircle size={18} />
				Fertig
			</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Pin Modal -->

<PinInputModal bind:this={pinModal} on:submit={handlePinSubmit} />

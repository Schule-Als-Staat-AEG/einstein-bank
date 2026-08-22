<script lang="ts">
	import Site from '$modules/Site.svelte';
	import Currency from '$modules/Currency.svelte';
	import { browser } from '$app/environment';
	import { getAuthStore } from '$lib/auth';
	import { onDestroy, onMount } from 'svelte';
	import { Camera, X, ScanLine, QrCode, Info, Plus, Calculator, ReceiptText } from '@lucide/svelte';
	import db from '$lib/connections';
	import { goto } from '$app/navigation';

	// Tab management
	let activeTab = 'scan';

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

	// QR Code Creation
	let amount: number = 0;
	let amountText: string = '';
	let description: string = '';
	let qrModal: HTMLDialogElement;
	let qrCodeData: string = '';
	let qrCodeImage: string = '';
	let authAccount: any = null;

	onMount(async () => {
		if (browser) {
			// Get auth account for QR code creation
			try {
				authAccount = await db
					.collection('accounts')
					.getFirstListItem(`user="${getAuthStore().record.id}"`, { expand: 'user' });
			} catch (e) {
				console.error('Failed to load account', e);
			}

			// Start scanner if on scan tab
			if (activeTab === 'scan') {
				startScanner();
			}
		} else {
			return;
		}
	});
	onDestroy(() => {
		if (browser) {
			stopScanner();
		}
	});

	// Switch tabs
	function switchTab(tab: string) {
		if (activeTab === 'scan' && tab !== 'scan') {
			stopScanner();
		}
		activeTab = tab;
		if (tab === 'scan' && browser) {
			setTimeout(() => startScanner(), 100);
		}
	}

	function tryParseCode(text: string) {
		console.log('Trying to parse code:', text);
		try {
			const data = JSON.parse(text);
			if (data && data.id) {
				return data.id;
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

	function handleResult(text: string) {
		console.log('Scanned code:', text);
		const code = tryParseCode(text);
		if (code) {
			console.log('Parsed code:', code);
			onCodeScanned(code);
			lastRaw = '';
			lastUrl = null;
		}
	}

	async function onCodeScanned(code: string) {
		// TODO: Implement code scanning logic
		const codeData = await db.collection('transaction_codes').getOne(code, { requestKey: null });
		if (codeData != null) {
			console.log('Code data:', codeData);
			stopScanner();
			goto(
				`/transfer/confirm?amount=${encodeURIComponent(codeData.amount)}&description=${encodeURIComponent(codeData.description)}&receiver=${encodeURIComponent(codeData.receiver)}`
			);
		} else {
			console.warn('No code found for scanned value');
		}
	}

	// QR Code Creation
	async function getQRCode(): Promise<string> {
		let result;
		try {
			result = await db
				.collection('transaction_codes')
				.getFirstListItem(`receiver="${authAccount.id}"`, { requestKey: null });
		} catch (e) {
			result = null;
		}
		if (result != null) {
			const record = await db.collection('transaction_codes').update(
				result.id,
				{
					amount: amount,
					description: description
				},
				{ requestKey: null }
			);
			return record.id as string;
		} else {
			const record = await db.collection('transaction_codes').create(
				{
					receiver: authAccount.id,
					amount: amount,
					description: description
				},
				{ requestKey: null }
			);
			return record.id as string;
		}
	}

	async function createQRCode() {
		amount = Number(amountText.replace(',', '.').replace(/[^0-9.]/g, '')) || 0;

		const code = await getQRCode();
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

		qrModal.showModal();
	}

	function closeQRModal() {
		qrModal.close();
	}

	$: canCreateCode = amount > 0;
</script>

<title>Scannen</title>
<Site>
	<div class="flex flex-col gap-4">
		<!-- Header -->
		<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
			<div class="flex flex-col md:flex-row md:items-center md:justify-between gap-3">
				<div class="flex items-center gap-3">
					<div
						class="w-11 h-11 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center shadow"
					>
						<QrCode size={20} />
					</div>
					<div class="flex flex-col">
						<h1 class="font-bold text-2xl leading-tight">QR-Code Manager</h1>
						<p class="text-sm opacity-70 flex items-center gap-2">
							<Info size={16} />
							<span>Scanne QR-Codes oder erstelle neue</span>
						</p>
					</div>
				</div>
			</div>

			<!-- Tabs -->
			<div class="mt-4">
				<div class="tabs tabs-boxed bg-base-100">
					<button
						class="tab {activeTab === 'scan' ? 'tab-active' : ''}"
						on:click={() => switchTab('scan')}
					>
						<ScanLine size={16} class="mr-2" />
						Scanner
					</button>
					<button
						class="tab {activeTab === 'create' ? 'tab-active' : ''}"
						on:click={() => switchTab('create')}
					>
						<Plus size={16} class="mr-2" />
						Code erstellen
					</button>
				</div>
			</div>
		</div>

		{#if activeTab === 'scan'}
			<!-- Scanner Tab -->
			<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
				<h2 class="font-bold text-lg mb-4 flex items-center gap-2">
					<ScanLine size={18} />
					Kamera Scanner
				</h2>

				<div class="mx-auto max-w-2xl">
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
				</div>

				{#if cameras.length > 1}
					<div class="mt-3 flex justify-center">
						<button class="btn btn-outline" on:click={flipCamera}>
							<Camera size={18} />
							Kamera wechseln ({currentCameraIndex + 1}/{cameras.length})
						</button>
					</div>
				{/if}

				{#if scannerError}
					<div class="alert alert-error mt-3 rounded-2xl">
						<X size={18} />
						<span>Fehler: {scannerError}</span>
					</div>
				{/if}

				<!-- Instructions -->
				<div class="mt-4 rounded-2xl bg-base-100 border border-base-300 p-4">
					<h3 class="font-bold mb-3 flex items-center gap-2">
						<Info size={18} />
						Anweisungen
					</h3>
					<div class="space-y-2 text-sm">
						<p>• Halte deinen QR-Code in den Rahmen</p>
						<p>• Stelle sicher, dass der Code gut beleuchtet ist</p>
						<p>• Warte bis der Scanner den Code automatisch erkennt</p>
						<p class="text-error font-bold">
							• Achtung: Dieser Scanner benötigt einen Überweisungscode und KEINE Bankkarte.
						</p>
					</div>
				</div>
			</div>
		{:else if activeTab === 'create'}
			<!-- Create Code Tab -->
			<div class="grid grid-cols-1 lg:grid-cols-2 gap-4">
				<!-- Form -->
				<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
					<h2 class="font-bold text-lg flex items-center gap-2">
						<Plus size={18} />
						QR-Code erstellen
					</h2>

					<div class="mt-4 flex flex-col gap-4">
						<!-- Amount -->
						<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
							<label class="text-sm opacity-70 flex items-center gap-2">
								<Calculator size={16} />
								<span>Betrag *</span>
							</label>

							<div class="mt-3 flex items-center flex-row gap-2">
								<input
									type="text"
									placeholder="Betrag"
									class="input input-bordered w-full rounded-2xl"
									bind:value={amountText}
									on:input={() => {
										amount = Number(amountText.replace(',', '.').replace(/[^0-9.]/g, '')) || 0;
									}}
								/>
								<div
									class="w-12 h-12 rounded-2xl bg-base-200 border border-base-300 flex items-center justify-center"
								>
									<Currency size={26} />
								</div>
							</div>
						</div>

						<!-- Description -->
						<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
							<label class="text-sm opacity-70 flex items-center gap-2">
								<ReceiptText size={16} />
								<span>Beschreibung</span>
							</label>

							<textarea
								placeholder="z.B. Verkauf von Produkten / Dienstleistung …"
								class="textarea textarea-bordered w-full rounded-2xl mt-3"
								bind:value={description}
							></textarea>
						</div>

						<!-- Actions -->
						<button
							class="btn btn-primary w-full"
							on:click={createQRCode}
							disabled={!canCreateCode}
						>
							<QrCode size={18} />
							QR-Code erstellen
						</button>
					</div>
				</div>

				<!-- Preview -->
				<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
					<h2 class="font-bold text-lg flex items-center gap-2">
						<Info size={18} />
						Vorschau
					</h2>

					<div class="mt-4 rounded-2xl bg-base-100 border border-base-300 p-4">
						<div class="text-sm opacity-70">Ersteller</div>
						<div class="mt-1 font-bold truncate">
							{authAccount?.expand?.user?.name ?? '—'}
						</div>
						<div class="text-xs opacity-60 font-mono truncate mt-1">
							{authAccount?.id ? `Konto-ID: ${authAccount.id}` : ''}
						</div>

						<div class="divider my-3"></div>

						<div class="flex items-center justify-between">
							<div class="text-sm opacity-70">Betrag</div>
							<div class="flex items-center gap-2">
								<span class="text-xl font-bold">{amount || 0}</span>
								<Currency size={18} />
							</div>
						</div>

						{#if description}
							<div class="divider my-3"></div>
							<div class="text-sm opacity-70">Beschreibung</div>
							<div class="mt-1 text-sm">{description}</div>
						{/if}
					</div>
				</div>
			</div>
		{/if}
	</div>

	<!-- QR Code Modal -->
	<dialog class="modal" bind:this={qrModal}>
		<div class="modal-box w-full max-w-md rounded-2xl border border-base-300">
			<header class="flex items-start justify-between gap-3">
				<div>
					<h3 class="text-xl font-bold flex items-center gap-2">
						<QrCode size={20} />
						QR-Code erstellt
					</h3>
					<p class="text-sm opacity-70 mt-1">Code wurde erfolgreich generiert</p>
				</div>
				<button class="btn btn-ghost btn-sm btn-circle" on:click={closeQRModal}>
					<X size={18} />
				</button>
			</header>

			<div class="mt-4">
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

				<div class="mt-4 rounded-2xl bg-base-200 border border-base-300 p-4">
					<div class="text-sm opacity-70">Tipp:</div>
					<code class="text-sm mt-1"
						>Gebe den QR-Code an eine andere Person weiter, um den Betrag zu überweisen</code
					>
				</div>
			</div>

			<div class="modal-action">
				<button class="btn btn-error" on:click={closeQRModal}>
					<X size={18} />
					Schließen
				</button>
			</div>
		</div>

		<form method="dialog" class="modal-backdrop">
			<button on:click={closeQRModal}>close</button>
		</form>
	</dialog>
</Site>

<script lang="ts">
	import { goto } from '$app/navigation';
	import { browser } from '$app/environment';
	import { onDestroy, onMount, tick } from 'svelte';
	import { CircleX, Eye, EyeOff, Info } from '@lucide/svelte';
	import db, { server } from '$lib/connections';

	type Step = 'scan' | 'infos' | 'password' | 'pin' | 'finished';
	type AlertKind = 'error' | 'info' | null;

	interface FormState {
		identity: string;
		security: string;
		companyName: string;
		password: string;
		passwordConfirm: string;
	}

	interface AccountState {
		username: string | null;
		isCompany: boolean;
	}

	interface AlertState {
		kind: AlertKind;
		message: string;
	}

	interface CameraOption {
		id: string;
		label: string;
	}

	interface ScannerState {
		cameras: CameraOption[];
		selectedCameraId: string;
		isScanning: boolean;
		isStarting: boolean;
		visible: boolean;
		error: string;
		lastText: string | null;
	}

	interface PublicAccount {
		name?: string;
		isCompany?: boolean;
		companyName?: string;
	}

	const emptyForm = (): FormState => ({
		identity: '',
		security: '',
		companyName: '',
		password: '',
		passwordConfirm: ''
	});

	const emptyAccount = (): AccountState => ({
		username: null,
		isCompany: false
	});

	let step: Step = 'scan';
	let form = emptyForm();
	let account = emptyAccount();
	let alert: AlertState = { kind: null, message: '' };

	let loading = false;
	let showPassword = false;

	let pinInputs: HTMLInputElement[] = [];
	let pinValues = ['', '', '', ''];

	let videoEl: HTMLVideoElement;
	let codeReader: any = null;

	let scanner: ScannerState = {
		cameras: [],
		selectedCameraId: '',
		isScanning: false,
		isStarting: false,
		visible: true,
		error: '',
		lastText: null
	};

	$: pin = pinValues.join('');
	$: isPinValid = /^\d{4}$/.test(pin);
	$: canContinueFromInfos = !account.isCompany || form.companyName.trim().length > 0;
	$: canContinueFromPassword =
		form.password.length > 0 && form.password === form.passwordConfirm;

	onMount(async () => {
		if (!browser) return;
		await tick();
		await startScanner();
	});

	onDestroy(() => {
		if (!browser) return;
		void stopScanner();
	});

	function updateForm(patch: Partial<FormState>) {
		form = { ...form, ...patch };
	}

	function updateScanner(patch: Partial<ScannerState>) {
		scanner = { ...scanner, ...patch };
	}

	function showAlert(kind: Exclude<AlertKind, null>, message: string) {
		alert = { kind, message };
	}

	function clearAlert() {
		alert = { kind: null, message: '' };
	}

	function resetScannedAccount() {
		form = {
			...form,
			identity: '',
			security: '',
			companyName: ''
		};
		account = emptyAccount();
		updateScanner({
			lastText: null,
			error: '',
			visible: true
		});
	}

	async function goToStep(nextStep: Step) {
		clearAlert();

		if (nextStep === step) {
			if (
				nextStep === 'scan' &&
				!scanner.isScanning &&
				!scanner.isStarting
			) {
				await tick();
				await startScanner(scanner.selectedCameraId || null);
			}
			return;
		}

		if (step === 'scan') {
			await stopScanner();
		}

		step = nextStep;

		if (nextStep === 'scan') {
			updateScanner({ visible: true, lastText: null });
			await tick();
			await startScanner(scanner.selectedCameraId || null);
		}
	}

	function tryParseCode(text: string): { sender: string; code: string } | null {
		try {
			const data = JSON.parse(text) as Record<string, unknown>;

			const sender =
				data.sender ??
				data.senderID ??
				data.senderId ??
				data.sender_id ??
				data.id;

			const code =
				data.code ??
				data.security ??
				data.codeId ??
				data.code_id ??
				data.id;

			if (sender && code) {
				return {
					sender: String(sender),
					code: String(code)
				};
			}
		} catch {
			// The scanned value was not JSON.
		}

		return null;
	}

	async function startScanner(deviceId: string | null = null) {
		if (!browser || scanner.isStarting || scanner.isScanning || step !== 'scan') {
			return;
		}

		updateScanner({
			isStarting: true,
			visible: true,
			error: ''
		});

		try {
			const {
				BarcodeFormat,
				BrowserMultiFormatReader,
				DecodeHintType
			} = await import('@zxing/library');

			const hints = new Map();
			hints.set(DecodeHintType.POSSIBLE_FORMATS, [BarcodeFormat.QR_CODE]);

			codeReader = new BrowserMultiFormatReader(hints);

			let devices: MediaDeviceInfo[] = [];

			try {
				devices = await codeReader.listVideoInputDevices();
			} catch {
				devices = [];
			}

			const cameras: CameraOption[] = devices.map((camera, index) => ({
				id: camera.deviceId,
				label: camera.label || `Kamera ${index + 1}`
			}));

			const requestedCameraExists =
				deviceId && cameras.some((camera) => camera.id === deviceId);

			let selectedCameraId = requestedCameraExists ? deviceId! : '';

			if (!selectedCameraId) {
				const preferredCamera = cameras.find((camera) =>
					/back|rear|environment|rück|hinten/i.test(camera.label)
				);

				selectedCameraId = preferredCamera?.id ?? cameras[0]?.id ?? '';
			}

			updateScanner({
				cameras,
				selectedCameraId
			});

			const constraints: MediaStreamConstraints = {
				video: {
					deviceId: selectedCameraId
						? { exact: selectedCameraId }
						: undefined,
					facingMode: selectedCameraId
						? undefined
						: { ideal: 'environment' },
					width: { ideal: 1280 },
					height: { ideal: 720 }
				}
			};

			await codeReader.decodeFromConstraints(
				constraints,
				videoEl,
				async (result: any) => {
					if (!result) return;

					const text = result.getText();

					if (!text || text === scanner.lastText) return;

					updateScanner({ lastText: text });
					await handleResult(text);
				}
			);

			updateScanner({ isScanning: true });
		} catch (error) {
			const message =
				error instanceof Error ? error.message : String(error);

			updateScanner({
				error: message,
				isScanning: false
			});
		} finally {
			updateScanner({ isStarting: false });
		}
	}

	async function stopScanner() {
		try {
			if (codeReader) {
				try {
					codeReader.reset();
				} catch {
					// The reader may already have been reset.
				}

				codeReader = null;
			}

			const stream = videoEl?.srcObject as MediaStream | null;

			if (stream) {
				for (const track of stream.getTracks()) {
					track.stop();
				}

				videoEl.srcObject = null;
			}
		} catch (error) {
			console.warn('Scanner konnte nicht vollständig beendet werden:', error);
		} finally {
			updateScanner({
				isScanning: false,
				isStarting: false
			});
		}
	}

	async function handleCameraChange(event: Event) {
		const selectedCameraId = (event.currentTarget as HTMLSelectElement).value;

		if (
			!selectedCameraId ||
			selectedCameraId === scanner.selectedCameraId
		) {
			return;
		}

		updateScanner({ selectedCameraId });
		await stopScanner();
		await tick();
		await startScanner(selectedCameraId);
	}

	async function restartScanner() {
		clearAlert();
		resetScannedAccount();
		await stopScanner();
		await tick();
		await startScanner(scanner.selectedCameraId || null);
	}

	async function handleResult(text: string) {
		const parsed = tryParseCode(text);

		if (!parsed) {
			showAlert('error', 'Der gescannte QR-Code ist ungültig.');
			updateScanner({ lastText: null });
			return;
		}

		loading = true;
		updateScanner({ visible: false });

		try {
			await stopScanner();

			const publicAccount = await db
				.collection('public_accounts')
				.getOne<PublicAccount>(parsed.sender);

			account = {
				username: publicAccount.name ?? parsed.sender,
				isCompany: Boolean(publicAccount.isCompany)
			};

			form = {
				...form,
				identity: parsed.sender,
				security: parsed.code,
				companyName: publicAccount.isCompany
					? publicAccount.companyName ?? ''
					: ''
			};

			await goToStep('infos');
		} catch (error) {
			const message =
				error instanceof Error ? error.message : String(error);

			showAlert(
				'error',
				`Das Konto konnte nicht geladen werden: ${message}`
			);

			updateScanner({
				visible: true,
				lastText: null
			});

			await tick();
			await startScanner(scanner.selectedCameraId || null);
		} finally {
			loading = false;
		}
	}

	async function handleBack() {
		clearAlert();

		switch (step) {
			case 'scan':
				await stopScanner();
				goto('/login');
				break;

			case 'infos':
				resetScannedAccount();
				await goToStep('scan');
				break;

			case 'password':
				await goToStep('infos');
				break;

			case 'pin':
				await goToStep('password');
				break;
		}
	}

	function continueFromInfos() {
		if (!canContinueFromInfos) {
			showAlert(
				'error',
				'Bitte geben Sie einen Namen für Ihr Unternehmen ein.'
			);
			return;
		}

		void goToStep('password');
	}

	function handlePinInput(index: number, event: Event) {
		const input = event.currentTarget as HTMLInputElement;
		const value = input.value.replace(/\D/g, '').slice(-1);

		pinValues[index] = value;
		pinValues = [...pinValues];
		input.value = value;

		if (value && index < pinValues.length - 1) {
			pinInputs[index + 1]?.focus();
		}
	}

	function handlePinKeydown(index: number, event: KeyboardEvent) {
		if (event.key === 'Backspace') {
			event.preventDefault();

			if (pinValues[index]) {
				pinValues[index] = '';
				pinValues = [...pinValues];
				pinInputs[index].value = '';
				return;
			}

			if (index > 0) {
				pinValues[index - 1] = '';
				pinValues = [...pinValues];
				pinInputs[index - 1].value = '';
				pinInputs[index - 1]?.focus();
			}
		}

		if (event.key === 'ArrowLeft' && index > 0) {
			pinInputs[index - 1]?.focus();
		}

		if (event.key === 'ArrowRight' && index < pinValues.length - 1) {
			pinInputs[index + 1]?.focus();
		}

		if (event.key === 'Enter') {
			void submitPin();
		}
	}

	async function submitPin() {
		if (!isPinValid) {
			showAlert(
				'error',
				'Die PIN muss genau 4 Ziffern enthalten.'
			);
			return;
		}

		await createAccount();
	}

	async function createAccount() {
		clearAlert();
		loading = true;

		try {
			const response = await fetch(`${server}/api/password-with-code`, {
				method: 'POST',
				headers: {
					'Content-Type': 'application/json'
				},
				body: JSON.stringify({
					accountID: form.identity,
					security: form.security,
					password: form.password,
					passwordConfirm: form.passwordConfirm,
					pin,
					companyName: form.companyName.trim()
				})
			});

			if (!response.ok) {
				let message = 'Fehler bei der Kontoerstellung.';

				try {
					const data = await response.json();
					message = data?.message || message;
				} catch {
					// Keep the fallback message when the response is not JSON.
				}

				throw new Error(message);
			}

			await goToStep('finished');
		} catch (error) {
			showAlert(
				'error',
				error instanceof Error ? error.message : String(error)
			);
		} finally {
			loading = false;
		}
	}
</script>

<svelte:head>
	<title>Login</title>
</svelte:head>

<div
	class="login-screen relative flex w-full items-center justify-center overflow-hidden bg-[#1E1E21]"
>
	<div class="pointer-events-none absolute inset-0 -z-10">
		<div
			class="absolute -left-10 -top-15 h-96 w-96 rounded-full bg-[#E34747]/25 blur-[140px]"
		></div>
		<div
			class="absolute -bottom-30 -right-20 h-130 w-130 rounded-full bg-[#E34747]/10 blur-[180px]"
		></div>
	</div>

	<form
		class="login-card animate-fade w-full max-w-md rounded-2xl border border-white/10 bg-white/5 p-8 shadow-2xl backdrop-blur-xl"
		id="login-form"
		on:submit|preventDefault
	>
		<button
			type="button"
			class="button btn border-white/10 bg-white/5 text-white shadow-none"
			on:click={handleBack}
		>
			&lt;
		</button>

		<div class="mb-6 flex justify-center">
			<div class="rounded-2xl border border-white/10 bg-white/5 p-4 backdrop-blur-xl">
				<svg class="h-12 w-12 text-[#E34747]" viewBox="0 0 24 24">
					<path
						fill="currentColor"
						d="M12 2L2 7v2h20V7zm8 6H4v11h16zm-7 3h2v5h-2zm-4 0h2v5H9z"
					/>
				</svg>
			</div>
		</div>

		<h1 class="text-center text-3xl font-bold text-white">Einstein Bank</h1>
		<p class="mb-6 mt-1 text-center text-sm text-gray-400">Willkommen!</p>

		{#if loading}
			<div class="flex w-full flex-row justify-center">
				<span class="loading loading-infinity loading-xl text-success"></span>
			</div>
			<p class="text-center text-sm text-gray-400">Wir sind gleich soweit...</p>
		{:else}
			{#if step === 'scan'}
				<p class="text-center text-sm text-gray-400">
					Bitte scanne den QR-Code auf deiner Bankkarte, um fortzufahren.
					Dafür brauchen wir Zugriff auf deine Kamera.
				</p>
				<p class="text-center text-sm text-gray-400">
					Es werden keine Bilder oder Videoaufnahmen gespeichert.
				</p>
				<p class="mb-4 text-center text-sm text-gray-400">
					Tipp: Bei Problemen hilft es, die Karte etwas zu bewegen.
				</p>
			{/if}

			{#if alert.kind === 'error'}
				<div
					class="mb-4 flex items-center gap-3 rounded-xl border border-red-700/40 bg-red-900/30 px-4 py-3 text-sm text-red-300"
				>
					<CircleX class="h-5 w-5 shrink-0" />
					<span>{alert.message}</span>
				</div>
			{:else if alert.kind === 'info'}
				<div
					class="mb-4 flex items-center gap-3 rounded-xl border border-green-700/40 bg-green-900/30 px-4 py-3 text-sm text-green-300"
				>
					<Info class="h-5 w-5 shrink-0" />
					<span>{alert.message}</span>
				</div>
			{/if}

			{#if step === 'scan'}
				<div class="flex flex-col items-center gap-3">
					{#if scanner.visible}
						<div class="w-full overflow-hidden rounded-lg bg-black/60">
							<video
								bind:this={videoEl}
								autoplay
								muted
								playsinline
								class="h-60 w-full object-cover"
							></video>
						</div>
					{/if}

					{#if scanner.cameras.length > 0 && scanner.visible}
						<label class="form-control w-full">
							<span class="label-text mb-1 text-sm text-gray-400">
								Kamera
							</span>
							<select
								class="select select-bordered w-full border-white/10 bg-white/5 text-white"
								value={scanner.selectedCameraId}
								on:change={handleCameraChange}
								disabled={scanner.isStarting}
							>
								{#each scanner.cameras as camera}
									<option value={camera.id}>{camera.label}</option>
								{/each}
							</select>
						</label>
					{/if}

					<button
						type="button"
						class="button btn w-full border-white/10 bg-white/5 text-white"
						on:click={restartScanner}
						disabled={scanner.isStarting}
					>
						{scanner.isStarting ? 'Kamera wird gestartet…' : 'Neustarten'}
					</button>

					{#if scanner.error}
						<div class="text-sm text-red-300">{scanner.error}</div>
					{/if}
				</div>
			{:else if step === 'infos'}
				<div class="mt-2 w-full rounded-lg border border-white/10 bg-white/5 p-4 text-white">
					<div class="font-medium">
						Nutzername: {account.username || form.identity}
					</div>
					<div class="text-sm text-gray-400">
						Kontonummer: {form.identity}
					</div>
				</div>

				{#if account.isCompany}
					<p class="mt-2 text-center text-sm text-gray-400">
						Bitte geben Sie den Namen Ihres Unternehmens ein. Dieser Name
						wird anderen Personen bei Transaktionen angezeigt. Ihr
						Nutzername zur Anmeldung bleibt unverändert.
					</p>

					<input
						type="text"
						placeholder="Firmenname"
						class="mb-4 mt-2 w-full rounded-lg border border-white/10 bg-white/5 px-4 py-3 text-white transition focus:outline-none focus:ring-2 focus:ring-[#E34747]/50"
						value={form.companyName}
						on:input={(event) =>
							updateForm({
								companyName: (event.currentTarget as HTMLInputElement).value
							})}
						on:keydown={(event) => {
							if (event.key === 'Enter') {
								event.preventDefault();
								continueFromInfos();
							}
						}}
					/>
				{/if}

				<button
					type="button"
					on:click={continueFromInfos}
					class="button mt-4 flex h-14 w-full items-center justify-center rounded-xl border border-white/10 bg-white/5 text-lg font-semibold text-white transition"
				>
					Weiter
				</button>
			{:else if step === 'password'}
				<div class="flex flex-col items-center gap-3">
					<p class="text-center text-sm text-gray-400">
						Bitte erstelle ein Passwort für dein Konto. Dieses benötigst du
						für das Online-Banking. Bewahre es an einem sicheren Ort auf.
					</p>

					<div class="relative w-full">
						<input
							type={showPassword ? 'text' : 'password'}
							placeholder="Passwort"
							autocomplete="new-password"
							value={form.password}
							on:input={(event) =>
								updateForm({
									password: (event.currentTarget as HTMLInputElement).value
								})}
							class="w-full rounded-lg border border-white/10 bg-white/5 px-4 py-3 pr-12 text-white transition focus:outline-none focus:ring-2 focus:ring-[#E34747]/50"
						/>

						<button
							type="button"
							class="absolute right-4 top-1/2 -translate-y-1/2 text-gray-400 transition hover:text-gray-200"
							on:click={() => (showPassword = !showPassword)}
							aria-label={showPassword ? 'Passwort verbergen' : 'Passwort anzeigen'}
						>
							{#if showPassword}
								<EyeOff class="h-5 w-5" />
							{:else}
								<Eye class="h-5 w-5" />
							{/if}
						</button>
					</div>

					<input
						type={showPassword ? 'text' : 'password'}
						placeholder="Passwort bestätigen"
						autocomplete="new-password"
						value={form.passwordConfirm}
						on:input={(event) =>
							updateForm({
								passwordConfirm: (event.currentTarget as HTMLInputElement).value
							})}
						class="w-full rounded-lg border border-white/10 bg-white/5 px-4 py-3 text-white transition focus:outline-none focus:ring-2 focus:ring-[#E34747]/50"
					/>

					<button
						type="button"
						on:click={() => void goToStep('pin')}
						class="button mt-4 flex h-14 w-full items-center justify-center rounded-xl border border-white/10 bg-white/5 text-lg font-semibold text-white transition disabled:cursor-not-allowed disabled:border-white/20 disabled:bg-white/10 disabled:text-gray-400"
						disabled={!canContinueFromPassword}
					>
						Weiter
					</button>
				</div>
			{:else if step === 'pin'}
				<div class="flex flex-col items-center gap-3">
					<p class="text-center text-sm text-gray-400">
						Zuletzt benötigst du eine vierstellige PIN für deine Karte.
						Diese benötigst du zum Bezahlen und Geldabheben. Teile sie
						niemandem mit.
					</p>

					<div class="mb-8 flex justify-center gap-4">
						{#each pinValues as _, index}
							<input
								type="password"
								inputmode="numeric"
								maxlength="1"
								class="input input-bordered h-16 w-16 bg-base-200 text-center text-2xl font-bold focus:bg-base-100"
								bind:this={pinInputs[index]}
								value={pinValues[index]}
								on:input={(event) => handlePinInput(index, event)}
								on:keydown={(event) => handlePinKeydown(index, event)}
								placeholder="•"
								autocomplete="off"
							/>
						{/each}
					</div>

					<button
						type="button"
						class="button mt-4 flex h-14 w-full items-center justify-center rounded-xl border border-[#E34747] bg-[#E34747] text-lg font-semibold text-white transition disabled:cursor-not-allowed disabled:border-[#E34747]/50 disabled:bg-[#E34747]/50 disabled:text-gray-400"
						disabled={!isPinValid}
						on:click={submitPin}
					>
						Konto einrichten
					</button>
				</div>
			{:else if step === 'finished'}
				<div class="flex flex-col items-center gap-3">
					<p class="text-center text-lg font-semibold text-green-400">
						Konto erfolgreich erstellt!
					</p>
					<p class="text-center text-sm text-gray-400">
						Du kannst dich nun anmelden.
					</p>
					<p class="text-center text-sm text-gray-400">
						Gib im Feld „Nutzername“ {account.username} und im Feld
						„Passwort“ das von dir gewählte Passwort ein.
					</p>

					<button
						type="button"
						on:click={() =>
							goto(
								'/login?' +
									new URLSearchParams({
										username: account.username ?? ''
									}).toString()
							)}
						class="button mt-4 flex h-14 w-full items-center justify-center rounded-xl border border-[#E34747] bg-[#E34747] text-lg font-semibold text-white transition"
					>
						Zum Login
					</button>
				</div>
			{/if}
		{/if}
	</form>
</div>

<style>
	* {
		user-select: none;
	}

	.login-screen {
		min-height: 100vh;
		min-height: 100svh;
		min-height: 100dvh;

		padding-top: max(16px, env(safe-area-inset-top));
		padding-bottom: max(16px, env(safe-area-inset-bottom));
		padding-left: max(16px, env(safe-area-inset-left));
		padding-right: max(16px, env(safe-area-inset-right));
	}

	.login-card {
		max-height: calc(
			100dvh - 32px - env(safe-area-inset-top) - env(safe-area-inset-bottom)
		);
		overflow: auto;
		-webkit-overflow-scrolling: touch;
	}

	input,
	select {
		font-size: 16px;
	}

	.animate-fade {
		animation: fadeIn 0.35s ease-out;
	}

	@keyframes fadeIn {
		from {
			opacity: 0;
			transform: translateY(10px);
		}

		to {
			opacity: 1;
			transform: translateY(0);
		}
	}

	.button:hover {
		cursor: pointer !important;
	}
</style>

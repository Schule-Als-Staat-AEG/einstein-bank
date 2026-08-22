<script lang="ts">
	import { getAuthStore } from '$lib/auth';
	import db from '$lib/connections';
	import QRCode from 'qrcode';
	import Site from '$modules/Site.svelte';
	import ChangePinModal from '$modules/modals/ChangePinModal.svelte';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';

	import {
		BanIcon,
		CheckIcon,
		QrCodeIcon,
		ShieldIcon,
		LockIcon,
		UnlockIcon,
		CreditCardIcon,
		SparklesIcon,
		ShieldOff,
		ShieldCheck,
		CircleQuestionMark,
		Pen
	} from '@lucide/svelte';

	let account: any = null;
	let code: any = null;
	let qrDataUrl: string | null = null;

	let bankName: string = 'EINSTEIN BANK';
	let bankSub: string = 'AEG • ULM';

	let changePinModal: any = null;
	let showQR: boolean = false;
	let isHovering: boolean = false;

	onMount(async () => {
		if (!browser) return;
		try {
			const acc = await db
				.collection('accounts')
				.getFirstListItem(`user="${getAuthStore().record.id}"`, { expand: 'user' });

			account = acc;

			const c = await db
				.collection('account_validation')
				.getFirstListItem(`account="${account.id}"`);

			code = c;

			try {
				const codeData = { code: code.security, sender: account.id };
				qrDataUrl = await QRCode.toDataURL(JSON.stringify(codeData), {
					type: 'image/png',
					margin: 1,
					width: 420,
					errorCorrectionLevel: 'M',
					color: { dark: '#0b0b0b', light: '#FFFFFF00' } // transparent bg
				});
			} catch (err) {
				console.error('QR generation failed', err);
			}
		} catch (e) {
			console.error('load failed', e);
		}
	});

	function lockCard() {
		if (!code) return;
		db.collection('account_validation')
			.update(code.id, { invalidate: true })
			.then(() => (code.invalidate = true));
	}

	function unlockCard() {
		if (!code) return;
		db.collection('account_validation')
			.update(code.id, { invalidate: false })
			.then(() => (code.invalidate = false));
	}

	function maskedCardId(id: string) {
		// show first 4 and last 4
		if (!id) return '---- ---- ---- ----';
		const clean = String(id).replace(/\s+/g, '');
		const a = clean.slice(0, 4).padEnd(4, '-');
		const b = clean.slice(4, 8).padEnd(4, '-');
		const c = clean.slice(-4).padStart(4, '-');
		return `${a} ${b} •••• ${c}`;
	}
	function requirePin() {
		if (!code) return;
		db.collection('account_validation')
			.update(code.id, { codeRequired: true })
			.then(() => (code.codeRequired = true));
	}
	function doNotRequirePin() {
		if (!code) return;
		db.collection('account_validation')
			.update(code.id, { codeRequired: false })
			.then(() => (code.codeRequired = false));
	}

	async function handleChangePinSubmit(event: CustomEvent) {
		const { newPin } = event.detail;

		try {
			await db.collection('account_validation').update(code.id, { pin: newPin });
			code.pin = newPin; // Update local state
			alert('PIN erfolgreich geändert');
			changePinModal?.closeModal();
		} catch (err) {
			console.error('PIN change failed:', err);
			alert('Fehler beim Ändern des PINs');
		}
	}

	$: status = code?.frozen ? 'frozen' : code?.invalidate ? 'locked' : code ? 'active' : 'unknown';
</script>

<title>Mein QR-Code</title>
<Site>
	<div class="min-h-[70vh] flex flex-col items-center justify-center gap-6 px-3">
		<!-- Header -->
		<div class="w-full max-w-5xl rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
			<div class="flex flex-col md:flex-row md:items-center md:justify-between gap-3">
				<div class="flex items-center gap-3">
					<div
						class="w-11 h-11 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center shadow"
					>
						<CreditCardIcon size={20} />
					</div>
					<div class="flex flex-col">
						<h1 class="font-bold text-2xl leading-tight">Mein QR-Code / Karte</h1>
						<div class="text-sm opacity-70 flex items-center gap-2">
							<QrCodeIcon size={16} />
							<span>Nutze den QR-Code zum schnellen Bezahlen oder Identifizieren.</span>
						</div>
					</div>
				</div>

				<div class="flex items-center gap-2">
					{#if status === 'active'}
						<span class="badge badge-success badge-outline flex items-center gap-2">
							<CheckIcon size={14} /> Aktiv
						</span>
					{:else if status === 'locked'}
						<span class="badge badge-error badge-outline flex items-center gap-2">
							<BanIcon size={14} /> Gesperrt
						</span>
					{:else if status === 'frozen'}
						<span class="badge badge-error flex items-center gap-2">
							<ShieldIcon size={14} /> Eingefroren
						</span>
					{:else}
						<span class="badge badge-ghost">Lade…</span>
					{/if}
				</div>
			</div>
		</div>

		<!-- Main -->
		<div class="w-full max-w-5xl grid grid-cols-1 lg:grid-cols-2 gap-4">
			<!-- Card preview -->
			<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
				<div class="flex items-center justify-between">
					<h2 class="font-bold text-lg flex items-center gap-2">
						<SparklesIcon size={18} />
						Karten-Vorschau
					</h2>
				</div>

				<div class="mt-4 flex justify-center">
					<!-- Credit card replica (frosted + accent) -->
					<!-- svelte-ignore a11y_no_static_element_interactions -->
					<div
						class="w-88 h-52 rounded-3xl overflow-hidden relative text-white shadow-lg border border-white/10"
						style="
							background:
								radial-gradient(1200px 400px at 10% 0%, rgba(255,255,255,0.18), rgba(255,255,255,0) 60%),
								radial-gradient(800px 500px at 100% 100%, rgba(255,0,70,0.20), rgba(255,255,255,0) 55%),
								linear-gradient(135deg, rgba(18,18,22,1) 0%, rgba(28,28,34,1) 45%, rgba(16,16,20,1) 100%);
						"
						on:mouseenter={() => (isHovering = true)}
						on:mouseleave={() => (isHovering = false)}
					>
						<!-- subtle glow border -->
						<div
							class="absolute inset-0 pointer-events-none"
							style="box-shadow: inset 0 0 0 1px rgba(255,255,255,0.08);"
						></div>

						<!-- top row -->
						<div class="absolute top-4 left-4 right-4 flex items-start justify-between">
							<div class="flex flex-col">
								<p class="text-xs font-bold tracking-[0.18em] opacity-90">{bankName}</p>
								<p class="text-[10px] opacity-70 mt-1">{bankSub}</p>
							</div>
						</div>

						<!-- card id -->
						<div class="absolute left-4 bottom-16">
							<p class="text-[10px] opacity-70 tracking-[0.12em]">KARTENNUMMER</p>
							<p class="font-mono text-base tracking-widest mt-1">
								{account ? maskedCardId(code?.number) : '---- ---- ---- ----'}
							</p>
						</div>

						<!-- holder -->
						<div class="absolute left-4 bottom-5">
							<p class="text-[10px] opacity-70 tracking-[0.12em]">KARTENHALTER</p>
							<p class="font-bold text-xs mt-1">
								{account?.expand?.user?.name ? account.expand.user.name.toUpperCase() : 'N/A'}
							</p>
						</div>

						<!-- QR area -->
						<!-- svelte-ignore a11y_click_events_have_key_events -->
						<div
							class="absolute right-4 bottom-4 w-21.5 h-21.5 rounded-2xl bg-white/10 border border-white/15 p-2 backdrop-blur flex items-center justify-center cursor-pointer transition-all"
							on:click={() => (showQR = !showQR)}
							style="filter: {showQR || isHovering ? 'blur(0px)' : 'blur(8px)'};"
						>
							{#if qrDataUrl}
								<img
									src={qrDataUrl}
									alt="QR Code"
									class="w-full h-full object-contain rounded-lg bg-white/90 p-1"
								/>
							{:else}
								<div
									class="w-full h-full rounded-lg bg-white/10 flex items-center justify-center text-xs opacity-80"
								>
									QR
								</div>
							{/if}
						</div>

						<!-- status dot -->
						<div class="absolute right-4 top-4 flex items-center gap-2">
							<!-- svelte-ignore element_invalid_self_closing_tag -->
							<div
								class="w-2.5 h-2.5 rounded-full"
								class:bg-success={status === 'active'}
								class:bg-error={status === 'locked' || status === 'frozen'}
								class:bg-warning={status === 'unknown'}
							/>
							<span class="text-[11px] opacity-70">
								{#if status === 'active'}Aktiv{/if}
								{#if status === 'locked'}GESPERRT{/if}
								{#if status === 'frozen'}EINGEFROREN{/if}
								{#if status === 'unknown'}LADEN{/if}
							</span>
						</div>
					</div>
				</div>

				<!-- Alerts -->
				<div class="mt-5 space-y-2">
					{#if code && code.invalidate}
						<div role="alert" class="alert alert-error">
							<BanIcon />
							<span><b>Achtung:</b> Deine Karte ist aktuell gesperrt (von dir).</span>
						</div>
					{/if}
					{#if code && code.frozen}
						<div role="alert" class="alert alert-error">
							<ShieldIcon />
							<span><b>Achtung:</b> Deine Karte wurde von der Bank eingefroren.</span>
						</div>
					{/if}
					{#if code && !code.frozen && !code.invalidate}
						<div role="alert" class="alert alert-success">
							<CheckIcon />
							<span>Die Karte ist aktiv und einsatzbereit.</span>
						</div>
					{/if}
					{#if code && code.codeRequired}
						<div role="alert" class="alert alert-info">
							<ShieldCheck />
							<span
								>Der Karten-Pin ist aktiviert. Du musst vor Zahlungen mit deiner Karte den Pin
								eingeben.</span
							>
						</div>
					{/if}
					{#if code && !code.codeRequired}
						<div role="alert" class="alert alert-warning">
							<ShieldOff />
							<span
								>Achtung: Der Karten-Pin ist deaktiviert. Jeder kann mit deiner Karte bezahlen, ohne
								einen Pin einzugeben.</span
							>
						</div>
					{/if}
				</div>
			</div>

			<!-- Actions / Details -->
			<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
				<h2 class="font-bold text-lg flex items-center gap-2">
					<ShieldIcon size={18} />
					Sicherheit & Aktionen
				</h2>

				<div class="mt-4 grid grid-cols-1 gap-3">
					<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
						<div class="text-sm opacity-60">Konto</div>
						<div class="mt-1 flex flex-col gap-1">
							<div class="font-bold truncate">{account?.expand?.user?.name ?? '—'}</div>
							<div class="font-mono text-xs opacity-70 truncate">
								Konto-ID: {account?.id ?? '—'}
							</div>
						</div>
					</div>

					<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
						<div class="flex items-center justify-between gap-3">
							<div>
								<div class="text-sm opacity-60">Karte sperren</div>
								<div class="text-sm opacity-80 mt-1">
									Sperre deine Karte sofort, falls du sie nicht nutzen willst.
								</div>
							</div>

							{#if code && !code.invalidate}
								<button
									class="btn btn-error btn-outline"
									on:click={lockCard}
									disabled={!code || code.frozen}
								>
									<LockIcon size={18} />
									Sperren
								</button>
							{:else}
								<button
									class="btn btn-success btn-outline"
									on:click={unlockCard}
									disabled={!code || code.frozen}
								>
									<UnlockIcon size={18} />
									Entsperren
								</button>
							{/if}
						</div>

						{#if code?.frozen}
							<div class="mt-3 text-xs opacity-70">
								<b>Hinweis:</b> Wenn die Bank die Karte eingefroren hat, kannst du sie nicht selbst entsperren.
							</div>
						{/if}
					</div>
					<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
						<div class="flex items-center justify-between gap-3">
							<div>
								<div class="text-sm opacity-60">Karten-Pin verwalten</div>
								<div class="text-sm opacity-80 mt-1 flex flex-col gap-1">
									<p>Verwalte den Pin deiner Karte.</p>
									<p class="text-xs opacity-70">
										Hinweis: Wenn du die Pin aktivierst, musst du vor Zahlungen mit deiner Karte den
										Pin eingeben.
									</p>
								</div>
							</div>
							<div class="flex flex-col gap-2">
								{#if code && !code.codeRequired}
									<button class="btn btn-success btn-outline flex flex-row" on:click={requirePin}>
										<ShieldCheck size={18} />
										Aktivieren
									</button>
								{:else}
									<button
										class="btn btn-error btn-outline flex flex-row"
										on:click={doNotRequirePin}
									>
										<ShieldOff size={18} />
										Deaktivieren
									</button>
								{/if}
								<button
									class="btn btn-ghost btn-outline"
									on:click={() => {
										changePinModal?.setCurrentPin(code?.pin || '');
										changePinModal?.showModal();
									}}
								>
									<Pen size={16} />
									<span class="text-xs">Pin ändern</span>
								</button>
							</div>
						</div>

						{#if code?.frozen}
							<div class="mt-3 text-xs opacity-70">
								<b>Hinweis:</b> Wenn die Bank die Karte eingefroren hat, kannst du sie nicht selbst entsperren.
							</div>
						{/if}
					</div>
				</div>
			</div>
		</div>
	</div>
</Site>

<ChangePinModal bind:this={changePinModal} on:submit={handleChangePinSubmit} />

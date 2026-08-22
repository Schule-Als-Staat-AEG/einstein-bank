<script lang="ts">
	import { browser } from '$app/environment';
	import { goto } from '$app/navigation';
	import { getAuthStore, isSuperuser } from '$lib/auth';
	import db, { server } from '$lib/connections';
	import { getSetting } from '$lib/settings';
	import Currency from '$modules/Currency.svelte';
	import Site from '$modules/Site.svelte';
	import { onMount } from 'svelte';

	import {
		ShieldCheck,
		Info,
		UserRound,
		Hash,
		FileText,
		Calculator,
		ReceiptText,
		ArrowLeft,
		BadgeCheck,
		Loader2
	} from '@lucide/svelte';
	import { fetchCaptcha, solveCaptcha } from '$lib/captcha';
	import PopupModal from '$modules/modals/PopupModal.svelte';
	import { translateError } from '$lib/errorTranslate';

	let receiver: any = null;
	let amount: number = 0;
	let description: string = '';

	let popupModal: PopupModal;

	let transfering: boolean = false;

	let taxRate: number = 0;
	let taxFreePool: number = 0;

	let taxableAmount: number = 0;
	let totalTax: number = 0;
	let totalDeduction: number = 0;

	let isIncome: boolean = false;
	let isVat: boolean = false;

	let authAccount: any = null;
	let enableTaxation: boolean = true;

	let receipientAccount: any = null;

	let loading = true;

	async function computeTax() {
		console.log('Computing tax for transfer', {
			sender: authAccount?.id,
			receiver,
			amount,
			isVat,
			isIncome
		});
		const result = await fetch(`${server}/api/calculate-tax`, {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
				Authorization: `Bearer ${getAuthStore().token}`
			},
			body: JSON.stringify({
				sender: authAccount.id,
				receiver: receiver,
				amount: amount,
				vat: isVat,
				incomeTax: isIncome
			})
		});
		if (!result.ok) {
			totalTax = 0;
			totalDeduction = amount;
			return;
		}
		const data = await result.json();
		console.log('Tax calculation result:', data);
		totalTax = data.totalTaxAmount;
		totalDeduction = data.totalDeduction;
		totalDeduction = amount + totalTax;
		totalDeduction = Math.round((totalDeduction + Number.EPSILON) * 100) / 100; // Round to 2 decimal places
	}

	onMount(async () => {
		if (!browser) return;

		loading = true;

		const urlParams = new URLSearchParams(window.location.search);
		receiver = urlParams.get('receiver');
		amount = Number(urlParams.get('amount'));
		description = urlParams.get('description') || '';
		isIncome = urlParams.get('isIncome') === 'true';
		isVat = urlParams.get('isVat') === 'true';

		try {
			authAccount = await db
				.collection('accounts')
				.getFirstListItem(`user="${getAuthStore().record.id}"`, {
					expand: 'user',
					requestKey: null
				});

			if (authAccount?.taxRate > 0) taxRate = authAccount.taxRate;
			else taxRate = parseFloat((await getSetting('tax_transaction_amount')) || '0');

			enableTaxation = (await getSetting('tax_enabled')) == 'true';

			receipientAccount = await db.collection('public_accounts').getOne(receiver);
			await computeTax();
		} catch (e) {
			console.error('confirm init failed', e);
		} finally {
			loading = false;
		}
	});

	function cancelTransfer() {
		goto(
			'/transfer?receiver=' +
				encodeURIComponent(receiver) +
				'&amount=' +
				encodeURIComponent(amount) +
				'&description=' +
				encodeURIComponent(description) +
				'&isIncome=' +
				encodeURIComponent(isIncome) +
				'&isVat=' +
				encodeURIComponent(isVat)
		);
	}

	async function confirmTransfer() {
		if (!isSuperuser()) {
			if (isNaN(amount) || amount <= 0) {
				alert('Bitte gib einen gültigen Betrag ein.');
				return;
			}
			if (!receiver) {
				alert('Bitte wähle einen Empfänger.');
				return;
			}
		}

		transfering = true;

		try {
			const authStore = getAuthStore().record;
			const account = await db
				.collection('accounts')
				.getFirstListItem(`user="${authStore.id}"`, { requestKey: null });

			const { captchaID, question } = await fetchCaptcha();
			const captchaAnswer = solveCaptcha(question);

			const data = {
				sender: account.id,
				receiver: receiver,
				amount: amount,
				description: description,
				vat: isVat,
				incomeTax: isIncome,
				captchaID: captchaID,
				captchaAnswer: captchaAnswer
			};

			const rsp = await fetch(`${server}/api/transaction`, {
				method: 'POST',
				headers: {
					'Content-Type': 'application/json',
					Authorization: `Bearer ${db.authStore.token}`
				},
				body: JSON.stringify(data)
			});
			const response = await rsp.json();

			if (rsp.ok) {
				goto(
					'/transfer/success?receiver=' +
						encodeURIComponent(receiver) +
						'&amount=' +
						encodeURIComponent(amount) +
						'&description=' +
						encodeURIComponent(description) +
						'&isIncome=' +
						encodeURIComponent(isIncome) +
						'&isVat=' +
						encodeURIComponent(isVat) +
						'&id=' +
						encodeURIComponent(response.transactionID)
				);
			} else {
				console.log('transfer failed', response);
				popupModal.alertDialog('Fehler', 'Fehler bei der Überweisung!\nFehler Nachricht: ' + translateError(response.message));
				transfering = false;
			}
		} catch (e) {
			console.error('transfer failed', e.detail);
			popupModal.alertDialog('Fehler', 'Fehler bei der Überweisung. Bitte versuche es später erneut.');
			transfering = false;
		}
	}
</script>

<title>Transaktion bestätigen</title>
<Site>
	<div class="flex flex-col gap-4">
		<!-- Header -->
		<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
			<div class="flex flex-col md:flex-row md:items-center md:justify-between gap-3">
				<div class="flex items-center gap-3">
					<div
						class="w-11 h-11 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center shadow"
					>
						<ShieldCheck size={20} />
					</div>
					<div class="flex flex-col">
						<h1 class="font-bold text-2xl leading-tight">Überweisung bestätigen</h1>
						<p class="text-sm opacity-70 flex items-center gap-2">
							<Info size={16} />
							<span>Prüfe die Daten und bestätige die Abbuchung.</span>
						</p>
					</div>
				</div>

				<button class="btn btn-ghost" on:click={cancelTransfer} disabled={transfering}>
					<ArrowLeft size={18} />
					Zurück
				</button>
			</div>
		</div>

		{#if loading}
			<div
				class="rounded-2xl bg-base-200 border border-base-300 p-8 flex items-center justify-center gap-3"
			>
				<span class="loading loading-spinner loading-lg"></span>
				<span class="opacity-70">Daten werden geladen…</span>
			</div>
		{:else}
			<div class="grid grid-cols-1 lg:grid-cols-2 gap-4">
				<!-- Summary -->
				<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
					<h2 class="font-bold text-lg flex items-center gap-2">
						<BadgeCheck size={18} />
						Zusammenfassung
					</h2>

					<div class="mt-4 grid grid-cols-1 gap-3">
						<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
							<div class="text-sm opacity-60 flex items-center gap-2">
								<UserRound size={16} />
								<span>Empfänger</span>
							</div>
							<div class="flex flex-row mt-1 gap-1">
								<div class="font-bold break-all">
									{receipientAccount?.isCompany
										? receipientAccount?.companyName
											? receipientAccount?.companyName
											: receipientAccount?.name || 'Unknown'
										: receipientAccount?.name || 'Unknown'}
								</div>
								<div>-</div>
								<div class="break-all opacity-50 font-mono">{receipientAccount?.id ?? '—'}</div>
							</div>
						</div>

						<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
							<div class="text-sm opacity-60 flex items-center gap-2">
								<Calculator size={16} />
								<span>Betrag</span>
							</div>
							<div class="mt-2 flex items-center gap-2">
								<span class="text-3xl font-bold">{totalDeduction}</span>
								<Currency size={22} />
							</div>
						</div>

						<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
							<div class="text-sm opacity-60 flex items-center gap-2">
								<ReceiptText size={16} />
								<span>Verwendungszweck</span>
							</div>
							<div class="mt-2 whitespace-pre-wrap opacity-90">
								{description ? description : 'Kein Verwendungszweck angegeben'}
							</div>
						</div>
					</div>

					<div class="mt-5 flex items-center justify-between">
						<button
							class="btn btn-ghost btn-error"
							on:click={cancelTransfer}
							disabled={transfering}
						>
							Abbrechen
						</button>

						<button
							class="btn btn-success"
							on:click={confirmTransfer}
							disabled={!receiver || !amount || transfering}
						>
							{#if transfering}
								<span class="loading loading-spinner"></span>
								Überweise…
							{:else}
								Überweisung tätigen
							{/if}
						</button>
					</div>

					<div class="mt-3 text-xs opacity-60">
						Tipp: Wenn dir etwas komisch vorkommt, geh zurück und prüfe Empfänger + Betrag nochmal.
					</div>
				</div>

				<!-- Tax / Deduction -->
				<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
					<h2 class="font-bold text-lg flex items-center gap-2">
						<FileText size={18} />
						Abbuchung & Steuern
					</h2>

					{#if enableTaxation}
						<div class="mt-4 rounded-2xl bg-base-100 border border-base-300 p-4">
							<div class="flex items-center justify-between">
								<div class="text-sm opacity-60">Steuersatz</div>
								<span class="badge badge-ghost">{Math.round((taxRate || 0) * 10000) / 100}%</span>
							</div>

							<div class="mt-4 space-y-2 text-sm">
								<div class="flex items-center justify-between">
									<span class="opacity-70">Überweisung</span>
									<span class="font-semibold flex items-center gap-1"
										>{amount || 0} <Currency size={14} /></span
									>
								</div>

								<div class="flex items-center justify-between">
									<span class="opacity-70">Steuerbetrag</span>
									<span class="font-semibold flex items-center gap-1"
										>{totalTax || 0} <Currency size={14} /></span
									>
								</div>

								<div class="divider my-2"></div>

								<div class="flex items-center justify-between">
									<span class="opacity-70">Gesamtabbuchung</span>
									<span class="text-xl font-bold flex items-center gap-2"
										>{totalDeduction || 0} <Currency size={18} /></span
									>
								</div>
							</div>

							<div class="mt-3 text-xs opacity-60">
								Die Steuer wird zusätzlich zum Betrag abgezogen.
							</div>
						</div>
					{:else}
						<div class="mt-4 rounded-2xl bg-base-100 border border-base-300 p-4">
							<div class="font-bold">Steuer ist deaktiviert</div>
							<div class="text-sm opacity-70 mt-1">Es wird nur der Betrag abgebucht.</div>

							<div class="mt-4 flex items-center justify-between">
								<span class="opacity-70">Gesamtabbuchung</span>
								<span class="text-xl font-bold flex items-center gap-2"
									>{amount || 0} <Currency size={18} /></span
								>
							</div>
						</div>
					{/if}

					<div class="mt-4 rounded-2xl bg-base-100 border border-base-300 p-4">
						<div class="text-sm opacity-60 flex items-center gap-2">
							<Hash size={16} />
							<span>Hinweis</span>
						</div>
						<div class="text-sm opacity-80 mt-2">
							Nach Bestätigung wird die Überweisung sofort ausgeführt.
						</div>
					</div>
				</div>
			</div>
		{/if}
	</div>
</Site>

<PopupModal bind:this={popupModal} />
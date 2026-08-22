<script lang="ts">
	import { browser } from '$app/environment';
	import { goto } from '$app/navigation';
	import { getAuthStore, isSuperuser } from '$lib/auth';
	import db, { server } from '$lib/connections';
	import { getSetting } from '$lib/settings';
	import Currency from '$modules/Currency.svelte';
	import Site from '$modules/Site.svelte';
	import AccountSelectModal from '$modules/modals/AccountSelectModal.svelte';
	import { onMount } from 'svelte';

	import {
		Send,
		UserRound,
		ArrowRight,
		X,
		ShieldCheck,
		Calculator,
		ReceiptText,
		Info
	} from '@lucide/svelte';

	// Transaction data
	let recepientAccount: any = null;
	let amount: number = 0;
	let amountText: string = '';
	let description: string = '';

	let taxRate: number = 0;
	let taxFreePool: number = 0;

	let taxableAmount: number = 0;
	let totalTax: number = 0;
	let totalDeduction: number = 0;

	// Computed remaining balance
	let remainingBalance: number = 0;
	let authAccount: any = null;

	// Modal data
	let accountSelectModal: any;

	let enableTaxation: boolean = true;

	let isIncome: boolean = false;
	let isVat: boolean = false;

	function selectRecepient(account: any) {
		recepientAccount = account;
	}

	function openModal() {
		accountSelectModal?.showModal();
	}

	onMount(async () => {
		if (!browser) return;

		// Prefill from query params
		const urlParams = new URLSearchParams(window.location.search);
		const receiverParam = urlParams.get('receiver');
		const amountParam = urlParams.get('amount');
		const descriptionParam = urlParams.get('description');
		const isIncomeParam = urlParams.get('isIncome');
		const isVatParam = urlParams.get('isVat');

		if (amountParam) amount = Number(amountParam);
		amountText = amount ? amount.toString() : '0';
		if (descriptionParam) description = descriptionParam;
		if (isIncomeParam) isIncome = isIncomeParam === 'true';
		if (isVatParam) isVat = isVatParam === 'true';

		// Fetch auth account (needed for filtering + balance + taxation)
		try {
			authAccount = await db
				.collection('accounts')
				.getFirstListItem(`user="${getAuthStore().record.id}"`, { expand: 'user' });

			calculateRemainingBalance();

			// tax settings
			if (authAccount.taxRate > 0) taxRate = authAccount.taxRate;
			else taxRate = parseFloat((await getSetting('tax_transaction_amount')) || '0');

			taxFreePool = parseFloat(authAccount.taxFreePool);
			enableTaxation = (await getSetting('tax_enabled')) == 'true';

			// Set admin mode for AccountSelectModal
			if (isSuperuser()) {
				accountSelectModal.adminMode = true;
			}

			// Receiver prefill (optional id)
			if (receiverParam) {
				try {
					recepientAccount = await db.collection('public_accounts').getOne(receiverParam);
				} catch (e) {
					// if it's not an id, keep as null (user can search)
					recepientAccount = null;
				}
			}
		} catch (e) {
			console.error('init failed', e);
		}
	});

	function checkData() {
		goto(
			'/transfer/confirm?receiver=' +
				encodeURIComponent(recepientAccount.id) +
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

	async function calculateRemainingBalance() {
		if (!browser || !authAccount) return;

		if (!isSuperuser()) {
			amount = Number(amountText.replace(',', '.').replace(/[^0-9.]/g, '')) || 0; // Allow comma or dot as decimal separator, remove other characters
			amount = Math.round((amount + Number.EPSILON) * 100) / 100; // Round to 2 decimal places
		} else {
			amount = Number(amountText.replace(',', '.').replace(/[^0-9.-]/g, '')) || 0; // Superuser can also input negative amounts
			amount = Math.round((amount + Number.EPSILON) * 100) / 100; // Round to 2 decimal places
		}
		remainingBalance = Number(authAccount.balance || 0) - Number(amount || 0);
		remainingBalance = Math.round((remainingBalance + Number.EPSILON) * 100) / 100; // Round to 2 decimal places

		await fetch(`${server}/api/calculate-tax`, {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
				Authorization: 'Bearer ' + getAuthStore().token
			},
			body: JSON.stringify({
				sender: authAccount.id,
				amount: amount,
				vat: isVat,
				incomeTax: isIncome
			})
		})
			.then((res) => {
				return res.json();
			})
			.then((data) => {
				console.log('calculateRemainingBalance', data);
				totalTax = data.totalTaxAmount;
				totalDeduction = amount + totalTax;
				totalDeduction = Math.round((totalDeduction + Number.EPSILON) * 100) / 100; // Round to 2 decimal places
			})
			.catch((e) => {
				console.error('calculateRemainingBalance failed', e);
			});
	}

	$: canProceed =
		recepientAccount != null &&
		amount !== 0 &&
		(!isSuperuser() ? amount > 0 : true) &&
		(!isSuperuser() ? remainingBalance >= 0 : true) &&
		(!isSuperuser() && authAccount?.frozen ? false : true);
</script>

<title>Transaktionen</title>
<Site>
	<div class="flex flex-col gap-4">
		<!-- Header -->
		<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
			<div class="flex flex-col md:flex-row md:items-center md:justify-between gap-3">
				<div class="flex items-center gap-3">
					<div
						class="w-11 h-11 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center shadow"
					>
						<Send size={20} />
					</div>
					<div class="flex flex-col">
						<h1 class="font-bold text-2xl leading-tight">Überweisung</h1>
						<p class="text-sm opacity-70 flex items-center gap-2">
							<Info size={16} />
							<span>Erstelle eine neue Überweisung mit Vorschau auf Steuern & Abbuchung.</span>
						</p>
					</div>
				</div>

				<button class="btn btn-ghost" on:click={() => goto('/dashboard')}>
					<X size={18} />
					Abbrechen
				</button>
			</div>

			<div class="mt-4 grid grid-cols-1 md:grid-cols-4 gap-3">
				<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
					<div class="text-sm opacity-60">Kontostand</div>
					<div class="mt-1 flex items-center gap-2">
						<span class="text-2xl font-bold">{authAccount?.balance?.toFixed(2) ?? 0}</span>
						<Currency size={22} />
						{#if authAccount?.frozen}
							<span class="badge badge-error">Eingefroren</span>
						{/if}
					</div>
				</div>

				<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
					<div class="text-sm opacity-60">Verbleibend (vor Steuer)</div>
					<div class="mt-1 flex flex-row items-center gap-2">
						<span class="text-2xl font-bold">{remainingBalance.toFixed(2)}</span>
						<Currency size={22} />
					</div>
				</div>

				<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
					<div class="text-sm opacity-60">Verbleibend (nach Steuer)</div>
					<div class="mt-1 flex flex-row items-center gap-2">
						<span class="text-2xl font-bold"
							>{((authAccount?.balance || 0) - (totalDeduction || 0)).toFixed(2)}</span
						>
						<Currency size={22} />
					</div>
				</div>

				<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
					<div class="text-sm opacity-60">Transaktionssteuer</div>
					<div class="mt-1 flex items-center gap-2">
						<span class="text-2xl font-bold">{Math.round((taxRate || 0) * 10000) / 100}%</span>
						<span class="badge badge-ghost">{enableTaxation ? 'aktiv' : 'aus'}</span>
					</div>
				</div>
			</div>
		</div>

		<!-- Form + Preview -->
		<div class="grid grid-cols-1 lg:grid-cols-2 gap-4">
			<!-- Form -->
			<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
				<h2 class="font-bold text-lg flex items-center gap-2">
					<ShieldCheck size={18} />
					Überweisung erstellen
				</h2>

				<div class="mt-4 flex flex-col gap-4">
					<!-- Receiver -->
					<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
						<label class="text-sm opacity-70 flex items-center gap-2">
							<UserRound size={16} />
							<span>Empfänger *</span>
						</label>

						<div class="mt-3">
							<button
								class="btn btn-outline w-full justify-between rounded-2xl"
								on:click={openModal}
							>
								<span class="truncate">
									{#if recepientAccount}
										{recepientAccount?.name ?? recepientAccount?.id}
									{:else}
										Empfänger auswählen
									{/if}
								</span>
								<span class="badge badge-ghost">Suchen</span>
							</button>

							{#if recepientAccount}
								<div class="mt-2 text-xs opacity-70 font-mono truncate">
									Konto-ID: {recepientAccount.id} • User-ID: {recepientAccount?.user ?? '-'}
								</div>
							{/if}
						</div>
						{#if recepientAccount == null}
							<div class="mt-1">
								<span class="text-error font-bold italic mt-2"
									>Bitte wähle einen Empfänger aus.</span
								>
							</div>
						{/if}
					</div>

					<!-- Amount -->
					<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
						<label class="text-sm opacity-70 flex items-center gap-2">
							<Calculator size={16} />
							<span>Betrag *</span>
						</label>

						<div class="mt-3 flex items-center flex-row gap-2">
							<input
								type="text"
								inputmode="decimal"
								autocomplete="off"
								placeholder="Betrag"
								class="input input-bordered w-full rounded-2xl"
								bind:value={amountText}
								on:input={calculateRemainingBalance}
							/>
							<div
								class="w-12 h-12 rounded-2xl bg-base-200 border border-base-300 flex items-center justify-center"
							>
								<Currency size={26} />
							</div>
						</div>

						<div class="mt-2 text-sm opacity-70 flex flex-row">
							Verbleibend: <span class="font-bold ml-1">{remainingBalance}</span>
							<Currency size={20} />
						</div>
						{#if !isSuperuser() && remainingBalance < 0}
							<span class="text-error font-bold italic"
								>Du hast nicht genügend Guthaben für diese Überweisung.</span
							>
						{/if}
						{#if !isSuperuser() && amount <= 0}
							<span class="text-error font-bold italic">Der Betrag muss größer als 0 sein.</span>
						{/if}
					</div>

					<!-- Description -->
					<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
						<label class="text-sm opacity-70 flex items-center gap-2">
							<ReceiptText size={16} />
							<span>Verwendungszweck</span>
						</label>

						<textarea
							placeholder="z.B. Miete Januar / Ticket / Rückzahlung …"
							class="textarea textarea-bordered w-full rounded-2xl mt-3"
							bind:value={description}
						></textarea>
					</div>

					<!-- svelte-ignore a11y_no_noninteractive_tabindex -->
					<div tabindex="0" class="collapse collapse-arrow bg-base-100 border-base-300 border">
						<div
							class="collapse-title font-semibold"
							hidden={!getAuthStore()?.record?.isCompany && !isSuperuser()}
						>
							Steuereinstellungen
						</div>
						<div class="collapse-content text-sm">
							<span>Falls du über dieses Menu Einkommen überweist, kreuze "Einkommen" an.</span>
							<div class="divider"></div>
							<div class="flex flex-row justify-between">
								<span>Einkommen</span>
								<input
									type="checkbox"
									class="toggle"
									autocomplete="off"
									bind:checked={isIncome}
									on:change={calculateRemainingBalance}
								/>
							</div>
							<div class="flex-row justify-between mt-2 hidden">
								<span>Mehrwertsteuerpflichtig</span>
								<input
									type="checkbox"
									class="toggle"
									autocomplete="off"
									bind:checked={isVat}
									on:change={calculateRemainingBalance}
								/>
							</div>
						</div>
					</div>
					<!-- Actions -->
					<div class="flex items-center justify-between">
						<button class="btn btn-ghost btn-error" on:click={() => goto('/dashboard')}>
							<X size={18} />
							Abbrechen
						</button>

						<button class="btn btn-primary" on:click={checkData} disabled={!canProceed}>
							Weiter
							<ArrowRight size={18} />
						</button>
					</div>
				</div>
			</div>

			<!-- Preview / Tax -->
			<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
				<h2 class="font-bold text-lg flex items-center gap-2">
					<Info size={18} />
					Vorschau
				</h2>

				<div class="mt-4 rounded-2xl bg-base-100 border border-base-300 p-4">
					<div class="text-sm opacity-70">Empfänger</div>
					<div class="mt-1 font-bold truncate">
						{recepientAccount?.name ?? '—'}
					</div>
					<div class="text-xs opacity-60 font-mono truncate mt-1">
						{recepientAccount?.id ? `Konto-ID: ${recepientAccount.id}` : ''}
					</div>

					<div class="divider my-3"></div>

					<div class="flex items-center justify-between">
						<div class="text-sm opacity-70">Betrag</div>
						<div class="flex items-center gap-2">
							<span class="text-xl font-bold">{amount || 0}</span>
							<Currency size={18} />
						</div>
					</div>

					<div class="mt-2 text-sm opacity-70 flex flex-row">
						Verbleibend (vor Steuer): <span class="font-bold ml-1">{remainingBalance}</span>
						<Currency size={20} />
					</div>

					<div class="mt-2 text-sm opacity-70 flex flex-row">
						Verbleibend (nach Steuer): <span class="font-bold ml-1"
							>{((authAccount?.balance || 0) - (totalDeduction || 0)).toFixed(2)}</span
						>
						<Currency size={20} />
					</div>
				</div>

				{#if enableTaxation}
					<div class="mt-4 rounded-2xl bg-base-100 border border-base-300 p-4">
						<div class="flex items-center justify-between">
							<div class="font-bold">Steuer-Details</div>
							<span class="badge badge-ghost">{Math.round((taxRate || 0) * 10000) / 100}%</span>
						</div>

						<div class="mt-3 space-y-2 text-sm">
							<div class="flex items-center justify-between">
								<span class="opacity-70">Überweisung</span>
								<span class="font-semibold flex items-center gap-1">
									{amount || 0}
									<Currency size={14} />
								</span>
							</div>

							<div class="flex items-center justify-between">
								<span class="opacity-70">Steuerbetrag</span>
								<span class="font-semibold flex items-center gap-1">
									{totalTax || 0}
									<Currency size={14} />
								</span>
							</div>

							<div class="divider my-2"></div>

							<div class="flex items-center justify-between">
								<span class="opacity-70">Gesamtabbuchung</span>
								<span class="text-lg font-bold flex items-center gap-2">
									{totalDeduction || 0}
									<Currency size={18} />
								</span>
							</div>
						</div>

						<div class="mt-3 text-xs opacity-60">
							Hinweis: Die Steuer wird zusätzlich zum Betrag abgezogen.
						</div>
					</div>
				{:else}
					<div class="mt-4 rounded-2xl bg-base-100 border border-base-300 p-4">
						<div class="font-bold">Steuer ist deaktiviert</div>
						<div class="text-sm opacity-70 mt-1">Es wird nur der Betrag abgebucht.</div>
					</div>
				{/if}
			</div>
		</div>
	</div>

	<!-- Recipient Modal -->
	<AccountSelectModal
		bind:this={accountSelectModal}
		on:select={(e) => selectRecepient(e.detail)}
		adminMode={isSuperuser()}
	/>
</Site>

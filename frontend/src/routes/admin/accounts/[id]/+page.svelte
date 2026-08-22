<script lang="ts">
	import { page } from '$app/state';
	import db from '$lib/connections';
	import AdminSite from '$modules/AdminSite.svelte';
	import Currency from '$modules/Currency.svelte';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';
	import { goto } from '$app/navigation';
	import {
		Save,
		Snowflake,
		ShieldCheck,
		TrendingUp,
		Wallet,
		ArrowRightLeft,
		Building2,
		Percent,
		DollarSign,
		AlertCircle,
		User,
		Settings2,
		ChevronRight
	} from '@lucide/svelte';

	let id = page.params.id;

	let record: any = null;
	let taxData: any = null;
	let loading = true;
	let saving = false;
	let error: string | null = null;
	let success: string | null = null;

	// Form fields - Account
	let balance = 0;
	let frozen = false;

	// Tax
	let taxExempt = false;
	let taxFreePool = 0;

	// VAT
	let vatExempt = false;
	let vatOverride = -1;

	// Income
	let incomeExempt = false;
	let incomeOverride = -1;

	// Wealth
	let wealthExempt = false;

	// Transaction
	let transactionExempt = false;
	let transactionAmountOverride = -1;
	let transactionFreeOverride = -1;

	// Company
	let companyExempt = false;
	let companyOverride = -1;

	// Global
	let globalMultiplier = 100;

	// UI state
	let openAccount = true;
	let openGlobal = true;
	let openVat = false;
	let openIncome = false;
	let openWealth = false;
	let openTransaction = false;
	let openCompany = false;

	onMount(async () => {
		if (!browser) return;
		await fetchUser();
	});

	async function fetchUser() {
		loading = true;
		error = null;
		success = null;

		try {
			record = await db.collection('accounts').getOne(id, { expand: 'user' });

			try {
				taxData = await db.collection('user_tax_data').getFirstListItem(`user = "${id}"`);
			} catch {
				taxData = null;
			}

			balance = Number(record.balance) || 0;
			frozen = Boolean(record.frozen);

			if (taxData) {
				taxExempt = Boolean(taxData.taxExempt);
				taxFreePool = Number(taxData.taxFreePool) || 0;

				vatExempt = Boolean(taxData.vatExempt);
				vatOverride = taxData.vatOverride === -1 ? -1 : Number(taxData.vatOverride) * 100;

				incomeExempt = Boolean(taxData.incomeExempt);
				incomeOverride = taxData.incomeOverride === -1 ? -1 : Number(taxData.incomeOverride) * 100;

				wealthExempt = Boolean(taxData.wealthExempt);

				transactionExempt = Boolean(taxData.transactionExempt);
				transactionAmountOverride =
					taxData.transactionAmountOverride === -1
						? -1
						: Number(taxData.transactionAmountOverride) * 100;
				transactionFreeOverride = Number(taxData.transactionFreeOverride) || -1;

				companyExempt = Boolean(taxData.companyExempt);
				companyOverride =
					taxData.companyOverride === -1 ? -1 : Number(taxData.companyOverride) * 100;

				globalMultiplier =
					taxData.globalMultiplier === undefined ? 100 : Number(taxData.globalMultiplier) * 100;
			}

			loading = false;
		} catch (e: any) {
			error = e?.message || 'Fehler beim Laden der Daten';
			loading = false;
		}
	}

	async function saveChanges() {
		saving = true;
		error = null;
		success = null;

		try {
			await db.collection('accounts').update(id, {
				balance: Number(balance),
				frozen: Boolean(frozen)
			});

			const taxDataPayload = {
				user: id,
				taxExempt: Boolean(taxExempt),
				taxFreePool: Number(taxFreePool),

				vatExempt: Boolean(vatExempt),
				vatOverride: vatOverride === -1 ? -1 : Number(vatOverride) / 100,

				incomeExempt: Boolean(incomeExempt),
				incomeOverride: incomeOverride === -1 ? -1 : Number(incomeOverride) / 100,

				wealthExempt: Boolean(wealthExempt),

				transactionExempt: Boolean(transactionExempt),
				transactionAmountOverride:
					transactionAmountOverride === -1 ? -1 : Number(transactionAmountOverride) / 100,
				transactionFreeOverride: Number(transactionFreeOverride),

				companyExempt: Boolean(companyExempt),
				companyOverride: companyOverride === -1 ? -1 : Number(companyOverride) / 100,

				globalMultiplier: Number(globalMultiplier) / 100
			};

			if (taxData) {
				await db.collection('user_tax_data').update(taxData.id, taxDataPayload);
			} else {
				await db.collection('user_tax_data').create(taxDataPayload);
			}

			success = 'Änderungen gespeichert.';
			await fetchUser();
		} catch (e: any) {
			error = e?.message || 'Fehler beim Speichern';
		} finally {
			saving = false;
		}
	}

	function pill(value: number) {
		if (value === -1) return 'Standard';
		return `${value}%`;
	}

	function moneyPill(value: number) {
		if (value === -1) return 'Standard';
		return String(value);
	}
</script>

<title>Admin - Konto bearbeiten</title>

<AdminSite>
	<div class="max-w-6xl mx-auto p-3 sm:p-5 space-y-4">
		{#if loading}
			<div class="card bg-base-200 border border-base-300">
				<div class="card-body">
					<div class="flex items-center gap-3">
						<span class="loading loading-spinner"></span>
						<span>Lade Kontodaten...</span>
					</div>
				</div>
			</div>
		{:else if error && !record}
			<div class="alert alert-error">
				<AlertCircle size={20} />
				<span>{error}</span>
			</div>
		{:else}
			<!-- Top header -->
			<div class="card bg-base-200 border border-base-300">
				<div class="card-body p-4 sm:p-6">
					<div class="flex flex-col gap-4">
						<div class="flex items-start justify-between gap-3">
							<div class="min-w-0">
								<div class="flex items-center gap-2">
									<Settings2 size={18} class="opacity-70" />
									<h1 class="text-xl sm:text-2xl font-bold">Konto und Steuern</h1>
								</div>

								<div class="mt-2 flex flex-wrap items-center gap-2">
									<span class="badge badge-ghost">
										<User size={14} class="mr-1 opacity-70" />
										{record?.expand?.user?.name || 'Unbekannt'}
									</span>
									<span class="badge badge-ghost badge-sm break-all">{id}</span>

									{#if frozen}
										<span class="badge badge-info">Eingefroren</span>
									{/if}
									{#if taxExempt}
										<span class="badge badge-error">Steuerfrei</span>
									{/if}
								</div>
							</div>

							<div class="hidden sm:flex items-center gap-2">
								<button class="btn btn-outline" on:click={() => goto('/admin/accounts')}
									>Zurück</button
								>
								<button class="btn btn-error" on:click={saveChanges} disabled={saving}>
									{#if saving}
										<span class="loading loading-spinner loading-sm"></span>
									{:else}
										<Save size={18} />
									{/if}
									Speichern
								</button>
							</div>
						</div>

						{#if success}
							<div class="alert alert-success">
								<ShieldCheck size={20} />
								<span>{success}</span>
							</div>
						{/if}

						{#if error}
							<div class="alert alert-error">
								<AlertCircle size={20} />
								<span>{error}</span>
							</div>
						{/if}

						<!-- Mobile sticky actions -->
						<div class="sm:hidden grid grid-cols-2 gap-2">
							<button class="btn btn-outline" on:click={() => goto('/admin/accounts')}
								>Zurück</button
							>
							<button class="btn btn-error" on:click={saveChanges} disabled={saving}>
								{#if saving}
									<span class="loading loading-spinner loading-sm"></span>
								{:else}
									<Save size={18} />
								{/if}
								Speichern
							</button>
						</div>
					</div>
				</div>
			</div>

			<!-- Content grid -->
			<div class="grid grid-cols-1 lg:grid-cols-12 gap-4">
				<!-- Left: quick overview -->
				<div class="lg:col-span-4 space-y-4">
					<div class="card bg-base-200 border border-base-300">
						<div class="card-body p-4 sm:p-5">
							<h2 class="font-semibold flex items-center gap-2">
								<Wallet size={18} />
								Übersicht
							</h2>

							<div class="mt-3 grid grid-cols-1 gap-3">
								<div class="bg-base-100 border border-base-300 rounded-box p-3">
									<div class="text-xs opacity-70">Kontostand</div>
									<div class="mt-1 flex items-center justify-between gap-2">
										<div class="font-bold text-lg break-all">{Number(balance).toFixed(2)}</div>
										<Currency size={22} class="opacity-80" />
									</div>
								</div>

								<div class="bg-base-100 border border-base-300 rounded-box p-3">
									<div class="text-xs opacity-70">Global</div>
									<div class="mt-1 flex flex-wrap gap-2">
										<span class="badge badge-ghost">Multiplikator: {Number(globalMultiplier)}%</span
										>
										{#if taxExempt}
											<span class="badge badge-error">Voll befreit</span>
										{:else}
											<span class="badge badge-success">Aktiv</span>
										{/if}
									</div>
								</div>

								<div class="bg-base-100 border border-base-300 rounded-box p-3">
									<div class="text-xs opacity-70">Overrides</div>
									<div class="mt-1 flex flex-wrap gap-2">
										<span class="badge badge-ghost">VAT: {pill(vatOverride)}</span>
										<span class="badge badge-ghost">Einkommen: {pill(incomeOverride)}</span>
										<span class="badge badge-ghost"
											>Transaktion: {pill(transactionAmountOverride)}</span
										>
										<span class="badge badge-ghost">Unternehmen: {pill(companyOverride)}</span>
									</div>
								</div>

								<div class="bg-base-100 border border-base-300 rounded-box p-3">
									<div class="text-xs opacity-70">Pools und Limits</div>
									<div class="mt-1 flex flex-wrap gap-2">
										<span class="badge badge-ghost"
											>TX Steuer Freigrenze: {moneyPill(taxFreePool)}</span
										>
										<span class="badge badge-ghost"
											>Transaktion Freigrenze: {moneyPill(transactionFreeOverride)}</span
										>
									</div>
								</div>
							</div>

							<div class="mt-4 text-xs opacity-70">Standard heißt: Systemwerte verwenden.</div>
						</div>
					</div>
				</div>

				<!-- Right: editable sections -->
				<div class="lg:col-span-8 space-y-4">
					<!-- Account -->
					<div class="collapse collapse-arrow bg-base-200 border border-base-300 rounded-box">
						<input type="checkbox" bind:checked={openAccount} />
						<div class="collapse-title font-semibold flex items-center gap-2">
							<Wallet size={18} />
							Kontoeinstellungen
						</div>
						<div class="collapse-content">
							<div class="grid grid-cols-1 md:grid-cols-2 gap-3">
								<div class="form-control">
									<!-- svelte-ignore a11y_label_has_associated_control -->
									<label class="label">
										<span class="label-text font-semibold">Kontostand</span>
									</label>
									<div class="join w-full">
										<input
											type="number"
											step="0.01"
											class="input input-bordered join-item w-full"
											autocomplete="off"
											bind:value={balance}
										/>
										<span class="btn btn-ghost join-item pointer-events-none">
											<Currency size={18} />
										</span>
									</div>
								</div>

								<div class="form-control flex flex-col">
									<!-- svelte-ignore a11y_label_has_associated_control -->
									<label class="label">
										<span class="label-text font-semibold">Status</span>
									</label>
									<label
										class="label cursor-pointer justify-start gap-3 bg-base-100 border border-base-300 rounded-box p-3"
									>
										<input
											type="checkbox"
											class="checkbox checkbox-info bg-base-100"
											bind:checked={frozen}
											autocomplete="off"
										/>
										<span class="label-text flex items-center gap-2">
											<Snowflake size={18} />
											Konto eingefroren
										</span>
									</label>
								</div>
							</div>
						</div>
					</div>

					<!-- Global -->
					<div class="collapse collapse-arrow bg-base-200 border border-base-300 rounded-box">
						<input type="checkbox" bind:checked={openGlobal} />
						<div class="collapse-title font-semibold flex items-center gap-2">
							<ShieldCheck size={18} />
							Global
						</div>
						<div class="collapse-content">
							<div class="grid grid-cols-1 md:grid-cols-2 gap-3">
								<div class="form-control">
									<label
										class="label cursor-pointer justify-start gap-3 bg-base-100 border border-base-300 rounded-box p-3"
									>
										<input
											type="checkbox"
											class="checkbox checkbox-error bg-base-100"
											bind:checked={taxExempt}
											autocomplete="off"
										/>
										<div class="min-w-0">
											<div class="font-semibold">Vollständig befreit</div>
											<div class="text-xs opacity-70">Überschreibt alles.</div>
										</div>
									</label>
								</div>

								<div class="form-control">
									<label class="label">
										<span class="label-text font-semibold flex items-center gap-2">
											<Percent size={16} />
											Multiplikator
										</span>
									</label>
									<div class="join w-full">
										<input
											type="number"
											step="1"
											class="input input-bordered join-item w-full"
											bind:value={globalMultiplier}
										/>
										<span class="btn btn-ghost join-item pointer-events-none">%</span>
									</div>
									<!-- svelte-ignore a11y_label_has_associated_control -->
									<label class="label">
										<span class="label-text-alt opacity-70">100% normal. 50% halb.</span>
									</label>
								</div>
							</div>
						</div>
					</div>

					<!-- VAT -->
					<div class="collapse collapse-arrow bg-base-200 border border-base-300 rounded-box">
						<input type="checkbox" bind:checked={openVat} />
						<div class="collapse-title font-semibold flex items-center gap-2">
							<TrendingUp size={18} />
							Mehrwertsteuer (VAT)
						</div>
						<div class="collapse-content">
							<div class="grid grid-cols-1 md:grid-cols-2 gap-3">
								<div class="form-control">
									<label
										class="label cursor-pointer justify-start gap-3 bg-base-100 border border-base-300 rounded-box p-3"
									>
										<input
											type="checkbox"
											class="checkbox bg-base-100"
											bind:checked={vatExempt}
											autocomplete="off"
										/>
										<div class="min-w-0">
											<div class="font-semibold">Befreit</div>
											<div class="text-xs opacity-70">Keine VAT.</div>
										</div>
									</label>
								</div>

								<div class="form-control">
									<label class="label">
										<span class="label-text font-semibold flex items-center gap-2">
											<Percent size={16} />
											Satz
										</span>
									</label>
									<div class="join w-full">
										<input
											type="number"
											step="0.1"
											class="input input-bordered join-item w-full"
											bind:value={vatOverride}
											disabled={vatExempt}
										/>
										<span class="btn btn-ghost join-item pointer-events-none">%</span>
									</div>
									<!-- svelte-ignore a11y_label_has_associated_control -->
									<label class="label">
										<span class="label-text-alt opacity-70">-1 = Standard</span>
									</label>
								</div>
							</div>
						</div>
					</div>

					<!-- Income -->
					<div class="collapse collapse-arrow bg-base-200 border border-base-300 rounded-box">
						<input type="checkbox" bind:checked={openIncome} />
						<div class="collapse-title font-semibold flex items-center gap-2">
							<DollarSign size={18} />
							Einkommenssteuer
						</div>
						<div class="collapse-content">
							<div class="grid grid-cols-1 md:grid-cols-2 gap-3">
								<div class="form-control">
									<label
										class="label cursor-pointer justify-start gap-3 bg-base-100 border border-base-300 rounded-box p-3"
									>
										<input
											type="checkbox"
											class="checkbox bg-base-100"
											bind:checked={incomeExempt}
											autocomplete="off"
										/>
										<div class="min-w-0">
											<div class="font-semibold">Befreit</div>
											<div class="text-xs opacity-70">Keine Einkommenssteuer.</div>
										</div>
									</label>
								</div>

								<div class="form-control">
									<label class="label">
										<span class="label-text font-semibold flex items-center gap-2">
											<Percent size={16} />
											Satz
										</span>
									</label>
									<div class="join w-full">
										<input
											type="number"
											step="0.1"
											class="input input-bordered join-item w-full"
											bind:value={incomeOverride}
											disabled={incomeExempt}
										/>
										<span class="btn btn-ghost join-item pointer-events-none">%</span>
									</div>
									<!-- svelte-ignore a11y_label_has_associated_control -->
									<label class="label">
										<span class="label-text-alt opacity-70">-1 = Standard</span>
									</label>
								</div>
							</div>
						</div>
					</div>

					<!-- Wealth -->
					<div class="collapse collapse-arrow bg-base-200 border border-base-300 rounded-box">
						<input type="checkbox" bind:checked={openWealth} />
						<div class="collapse-title font-semibold flex items-center gap-2">
							<Wallet size={18} />
							Vermögenssteuer
						</div>
						<div class="collapse-content">
							<div class="grid grid-cols-1 md:grid-cols-2 gap-3">
								<div class="form-control md:col-span-2">
									<label
										class="label cursor-pointer justify-start gap-3 bg-base-100 border border-base-300 rounded-box p-3"
									>
										<input
											type="checkbox"
											class="checkbox bg-base-100"
											bind:checked={wealthExempt}
											autocomplete="off"
										/>
										<div class="min-w-0">
											<div class="font-semibold">Befreit</div>
											<div class="text-xs opacity-70">Keine Vermögenssteuer.</div>
										</div>
									</label>
								</div>
							</div>
						</div>
					</div>

					<!-- Transaction -->
					<div class="collapse collapse-arrow bg-base-200 border border-base-300 rounded-box">
						<input type="checkbox" bind:checked={openTransaction} />
						<div class="collapse-title font-semibold flex items-center gap-2">
							<ArrowRightLeft size={18} />
							Transaktionssteuer
						</div>
						<div class="collapse-content">
							<div class="grid grid-cols-1 md:grid-cols-2 gap-3">
								<div class="form-control md:col-span-2">
									<label
										class="label cursor-pointer justify-start gap-3 bg-base-100 border border-base-300 rounded-box p-3"
									>
										<input
											type="checkbox"
											class="checkbox bg-base-100"
											bind:checked={transactionExempt}
											autocomplete="off"
										/>
										<div class="min-w-0">
											<div class="font-semibold">Befreit</div>
											<div class="text-xs opacity-70">Keine Transaktionssteuer.</div>
										</div>
									</label>
								</div>

								<div class="form-control">
									<label class="label">
										<span class="label-text font-semibold flex items-center gap-2">
											<Percent size={16} />
											Satz
										</span>
									</label>
									<div class="join w-full">
										<input
											type="number"
											step="0.1"
											class="input input-bordered join-item w-full"
											bind:value={transactionAmountOverride}
											disabled={transactionExempt}
										/>
										<span class="btn btn-ghost join-item pointer-events-none">%</span>
									</div>
									<!-- svelte-ignore a11y_label_has_associated_control -->
									<label class="label">
										<span class="label-text-alt opacity-70">-1 = Standard</span>
									</label>
								</div>

								<div class="form-control">
									<label class="label">
										<span class="label-text font-semibold flex items-center gap-2">
											<DollarSign size={16} />
											Tax Free Pool
										</span>
									</label>
									<div class="join w-full">
										<input
											type="number"
											step="1"
											class="input input-bordered join-item w-full"
											bind:value={taxFreePool}
											disabled={transactionExempt}
										/>
										<span class="btn btn-ghost join-item pointer-events-none">
											<Currency size={18} />
										</span>
									</div>
									<!-- svelte-ignore a11y_label_has_associated_control -->
									<label class="label">
										<span class="label-text-alt opacity-70"
											>Guthaben für steuerfreie Transaktionen.</span
										>
									</label>
								</div>

								<div class="form-control md:col-span-2">
									<label class="label">
										<span class="label-text font-semibold flex items-center gap-2">
											<DollarSign size={16} />
											Freigrenze
										</span>
									</label>
									<div class="join w-full">
										<input
											type="number"
											step="1"
											class="input input-bordered join-item w-full"
											bind:value={transactionFreeOverride}
											disabled={transactionExempt}
										/>
										<span class="btn btn-ghost join-item pointer-events-none">
											<Currency size={18} />
										</span>
									</div>
									<!-- svelte-ignore a11y_label_has_associated_control -->
									<label class="label">
										<span class="label-text-alt opacity-70">-1 = Standard</span>
									</label>
								</div>
							</div>
						</div>
					</div>

					<!-- Company -->
					<div class="collapse collapse-arrow bg-base-200 border border-base-300 rounded-box">
						<input
							type="checkbox"
							class="bg-base-100"
							bind:checked={openCompany}
							autocomplete="off"
						/>
						<div class="collapse-title font-semibold flex items-center gap-2">
							<Building2 size={18} />
							Unternehmenssteuer
						</div>
						<div class="collapse-content">
							<div class="grid grid-cols-1 md:grid-cols-2 gap-3">
								<div class="form-control">
									<label
										class="label cursor-pointer justify-start gap-3 bg-base-100 border border-base-300 rounded-box p-3"
									>
										<input
											type="checkbox"
											class="checkbox bg-base-100"
											bind:checked={companyExempt}
											autocomplete="off"
										/>
										<div class="min-w-0">
											<div class="font-semibold">Befreit</div>
											<div class="text-xs opacity-70">Keine Unternehmenssteuer.</div>
										</div>
									</label>
								</div>

								<div class="form-control">
									<label class="label">
										<span class="label-text font-semibold flex items-center gap-2">
											<Percent size={16} />
											Satz
										</span>
									</label>
									<div class="join w-full">
										<input
											type="number"
											step="0.1"
											class="input input-bordered join-item w-full"
											bind:value={companyOverride}
											disabled={companyExempt}
										/>
										<span class="btn btn-ghost join-item pointer-events-none">%</span>
									</div>
									<!-- svelte-ignore a11y_label_has_associated_control -->
									<label class="label">
										<span class="label-text-alt opacity-70">-1 = Standard</span>
									</label>
								</div>
							</div>
						</div>
					</div>

					<!-- Bottom actions -->
					<div class="card bg-base-200 border border-base-300">
						<div class="card-body p-4 sm:p-5">
							<div class="flex items-center justify-between gap-3">
								<div class="text-sm opacity-70 flex items-center gap-2">
									<ChevronRight size={16} class="opacity-70" />
									Änderungen wirken sofort nach dem Speichern.
								</div>

								<div class="flex gap-2">
									<button class="btn btn-outline" on:click={() => goto('/admin/accounts')}>
										Abbrechen
									</button>
									<button class="btn btn-error" on:click={saveChanges} disabled={saving}>
										{#if saving}
											<span class="loading loading-spinner loading-sm"></span>
										{:else}
											<Save size={18} />
										{/if}
										Speichern
									</button>
								</div>
							</div>
						</div>
					</div>
				</div>
			</div>
		{/if}
	</div>
</AdminSite>

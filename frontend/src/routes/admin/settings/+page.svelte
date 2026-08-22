<script lang="ts">
	import db, { server } from '$lib/connections';
	import { getFile, getSetting } from '$lib/settings';
	import AdminSite from '$modules/AdminSite.svelte';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';
	import {
		Coins,
		Image as ImageIcon,
		Receipt,
		RefreshCcw,
		Clock,
		Save,
		X,
		HandCoins,
		CheckCircle2,
		AlertCircle,
		Cctv
	} from '@lucide/svelte';
	import { deleteAllCaches } from '$lib/caching';
	import AccountSelectModal from '$modules/modals/AccountSelectModal.svelte';

	// ==================== STATE ====================
	let activeTab: 'display' | 'automation' | 'taxes' = 'display';

	// Display Settings
	let displayName = '';
	let logoFileUrl = '';
	let logoUpload: File | null = null;
	let logoFiles: FileList | null = null;
	let contactEmail = '';
	let passwordsChangeable = false;
	let notifyTransactions = false;

	// Currency Settings
	let currencyIsFile = false;
	let currencyString = '';
	let currencyFileUrl = '';
	let currencyUpload: File | null = null;
	let currencyInputFiles: FileList | null = null;

	// Automation Settings
	let handleRecurringPayments = false;
	let recurringPaymentsUpdateTimer = 0;
	let recurringResetHours = 0;
	let recurringResetMinutes = 0;
	let recurringResetSeconds = 0;

	let enableWarehouse = false;
	let warehouseAccount = '';

	let taxTrackingEmail = '';
	let enableTaxTrackingEmail = false;

	// Tax Settings
	let enableTaxation = false;
	let taxCollectionAccount = '';
	let enableIncomeTax = false;
	let enableTransactionTax = false;
	let transactionTaxPercentage: number = 0;
	let transactionFreeRate: number = 0;
	let transactionPoolHours = 0;
	let transactionPoolMinutes = 0;
	let transactionPoolSeconds = 0;
	let enableVat = false;
	let vatRatePercent: number = 0;
	let enableWealthTax = false;
	let taxWealthResetHours = 0;
	let taxWealthResetMinutes = 0;
	let taxWealthResetSeconds = 0;
	let enableCompanyTax = false;

	let userInsert: 'tax' | 'warehouse' = 'tax';
	let accountSelectModal: AccountSelectModal;

	// Income Tax Brackets
	interface IncomeTaxBracket {
		id: string;
		from: number;
		amount: number;
	}
	let incomeTaxBrackets: IncomeTaxBracket[] = [];
	let editingBracketId: string | null = null;
	let newBracketFrom = 0;
	let newBracketAmount = 0;
	let incomeTaxBracketModal: HTMLDialogElement;

	// Wealth Tax Brackets
	interface WealthTaxBracket {
		id: string;
		from: number;
		amount: number;
		company: boolean;
	}
	let wealthTaxBrackets: WealthTaxBracket[] = [];
	let editingWealthBracketId: string | null = null;
	let newWealthBracketFrom = 0;
	let newWealthBracketAmount = 0;
	let newWealthBracketCompany = false;
	let wealthTaxBracketModal: HTMLDialogElement;

	let openModal: 'display' | 'currency' | 'recurring' | 'warehouse' | 'taxes' | null = null;

	// Utility Functions
	function fileFromInput(files: FileList | null) {
		return files?.length ? files[0] : null;
	}

	function onLogoFileInput() {
		logoUpload = fileFromInput(logoFiles);
	}

	function onCurrencyFileInput() {
		currencyUpload = fileFromInput(currencyInputFiles);
	}

	function removeLogoUpload() {
		logoUpload = null;
		logoFiles = null;
	}

	function removeCurrencyUpload() {
		currencyUpload = null;
		currencyInputFiles = null;
	}

	function secondsToHMS(total: number) {
		total = Math.max(0, Math.floor(total));
		const h = Math.floor(total / 3600);
		const m = Math.floor((total % 3600) / 60);
		const s = total % 60;
		return { h, m, s };
	}

	function hmsToSeconds(h: number, m: number, s: number) {
		return Math.max(0, Math.floor(h) * 3600 + Math.floor(m) * 60 + Math.floor(s));
	}

	function toPercent(value: number) {
		return value * 100;
	}

	function fromPercent(percent: number) {
		return percent / 100;
	}

	// Load All Settings
	async function loadAll() {
		const curr = await getSetting('currency');
		currencyIsFile = curr === 'file';
		currencyFileUrl = currencyIsFile ? await getFile('currency') : '';
		currencyString = currencyIsFile ? '' : curr || '';

		displayName = (await getSetting('display_name')) || '';
		logoFileUrl = (await getFile('logo').catch(() => '')) || '';
		contactEmail = (await getSetting('contact_email', true)) || '';
		notifyTransactions = (await getSetting('notify_transactions', true)) === 'true';
		passwordsChangeable = (await getSetting('passwords_changeable', true)) === 'true';

		handleRecurringPayments = (await getSetting('handle_recurring_payments', true)) === 'true';
		recurringPaymentsUpdateTimer =
			parseInt((await getSetting('recurring_payments_update_timer', true)) || '0') || 0;
		const recurringReset =
			parseInt((await getSetting('recurring_payments_reset', true)) || '0') || 0;
		({
			h: recurringResetHours,
			m: recurringResetMinutes,
			s: recurringResetSeconds
		} = secondsToHMS(recurringReset));

		enableWarehouse = (await getSetting('warehouse_enable', true)) === 'true';
		warehouseAccount = (await getSetting('warehouse_account', true)) || '';

		enableTaxation = (await getSetting('tax_enabled', true)) === 'true';
		taxCollectionAccount = (await getSetting('tax_collection_account', true)) || '';
		enableIncomeTax = (await getSetting('tax_income_enable', true)) === 'true';
		enableTransactionTax = (await getSetting('tax_transaction_enable', true)) === 'true';
		transactionTaxPercentage = toPercent(
			parseFloat((await getSetting('tax_transaction_amount', true)) || '0') || 0
		);
		transactionFreeRate = parseFloat((await getSetting('tax_transaction_free', true)) || '0') || 0;
		const transactionReset = parseInt((await getSetting('tax_pool_reset', true)) || '0') || 0;
		({
			h: transactionPoolHours,
			m: transactionPoolMinutes,
			s: transactionPoolSeconds
		} = secondsToHMS(transactionReset));
		enableVat = (await getSetting('tax_vat_enable', true)) === 'true';
		vatRatePercent = toPercent(parseFloat((await getSetting('tax_vat_amount', true)) || '0') || 0);
		enableWealthTax = (await getSetting('tax_wealth_enable', true)) === 'true';
		const wealthReset = parseInt((await getSetting('tax_wealth_deduct_time', true)) || '0') || 0;
		({
			h: taxWealthResetHours,
			m: taxWealthResetMinutes,
			s: taxWealthResetSeconds
		} = secondsToHMS(wealthReset));
		enableCompanyTax = (await getSetting('tax_company_enable', true)) === 'true';

		taxTrackingEmail = (await getSetting('case_email_receiver', true)) || '';
		enableTaxTrackingEmail = (await getSetting('case_send_email', true)) === 'true';
		await loadIncomeTaxBrackets();
		await loadWealthTaxBrackets();
	}

	// Income Tax Bracket Functions
	async function loadIncomeTaxBrackets() {
		try {
			const records = await db.collection('income_tax_brackets').getFullList({
				sort: 'from'
			});
			incomeTaxBrackets = records.map((r: any) => ({
				id: r.id,
				from: r.from,
				amount: r.amount
			}));
		} catch (error) {
			console.error('Error loading income tax brackets:', error);
			incomeTaxBrackets = [];
		}
	}

	async function createIncomeTaxBracket() {
		try {
			await db.collection('income_tax_brackets').create({
				from: newBracketFrom,
				amount: fromPercent(newBracketAmount)
			});
			newBracketFrom = 0;
			newBracketAmount = 0;
			await loadIncomeTaxBrackets();
			closeIncomeTaxBracketModal();
		} catch (error) {
			alert('Fehler beim Erstellen der Steuerklasse: ' + error);
		}
		saveAny();
	}

	async function updateIncomeTaxBracket() {
		if (!editingBracketId) return;

		try {
			await db.collection('income_tax_brackets').update(editingBracketId, {
				from: newBracketFrom,
				amount: fromPercent(newBracketAmount)
			});
			editingBracketId = null;
			newBracketFrom = 0;
			newBracketAmount = 0;
			await loadIncomeTaxBrackets();
			closeIncomeTaxBracketModal();
		} catch (error) {
			alert('Fehler beim Aktualisieren der Steuerklasse: ' + error);
		}
		saveAny();
	}

	async function deleteIncomeTaxBracket(id: string) {
		if (!confirm('Möchtest du diese Steuerklasse wirklich löschen?')) {
			return;
		}

		try {
			await db.collection('income_tax_brackets').delete(id);
			await loadIncomeTaxBrackets();
		} catch (error) {
			alert('Fehler beim Löschen der Steuerklasse: ' + error);
		}
		saveAny();
	}

	function startEditBracket(bracket: IncomeTaxBracket) {
		editingBracketId = bracket.id;
		newBracketFrom = bracket.from;
		newBracketAmount = toPercent(bracket.amount);
	}

	function cancelEditBracket() {
		editingBracketId = null;
		newBracketFrom = 0;
		newBracketAmount = 0;
		closeIncomeTaxBracketModal();
	}

	function openIncomeTaxBracketModal() {
		incomeTaxBracketModal?.showModal();
	}

	function closeIncomeTaxBracketModal() {
		incomeTaxBracketModal?.close();
	}

	function openCreateBracketModal() {
		editingBracketId = null;
		newBracketFrom = 0;
		newBracketAmount = 0;
		openIncomeTaxBracketModal();
	}

	function openEditBracketModal(bracket: IncomeTaxBracket) {
		editingBracketId = bracket.id;
		newBracketFrom = bracket.from;
		newBracketAmount = toPercent(bracket.amount);
		openIncomeTaxBracketModal();
	}

	// Wealth Tax Bracket Functions
	async function loadWealthTaxBrackets() {
		try {
			const records = await db.collection('wealth_tax_brackets').getFullList({
				sort: 'from'
			});
			wealthTaxBrackets = records.map((r: any) => ({
				id: r.id,
				from: r.from,
				amount: r.amount,
				company: r.company
			}));
		} catch (error) {
			console.error('Error loading wealth tax brackets:', error);
			wealthTaxBrackets = [];
		}
	}

	async function createWealthTaxBracket() {
		try {
			await db.collection('wealth_tax_brackets').create({
				from: newWealthBracketFrom,
				amount: fromPercent(newWealthBracketAmount),
				company: newWealthBracketCompany
			});
			newWealthBracketFrom = 0;
			newWealthBracketAmount = 0;
			newWealthBracketCompany = false;
			await loadWealthTaxBrackets();
			closeWealthTaxBracketModal();
		} catch (error) {
			alert('Fehler beim Erstellen der Vermögenssteuerklasse: ' + error);
		}
		saveAny();
	}

	async function updateWealthTaxBracket() {
		if (!editingWealthBracketId) return;

		try {
			await db.collection('wealth_tax_brackets').update(editingWealthBracketId, {
				from: newWealthBracketFrom,
				amount: fromPercent(newWealthBracketAmount),
				company: newWealthBracketCompany
			});
			editingWealthBracketId = null;
			newWealthBracketFrom = 0;
			newWealthBracketAmount = 0;
			newWealthBracketCompany = false;
			await loadWealthTaxBrackets();
			closeWealthTaxBracketModal();
		} catch (error) {
			alert('Fehler beim Aktualisieren der Vermögenssteuerklasse: ' + error);
		}
		saveAny();
	}

	async function deleteWealthTaxBracket(id: string) {
		if (!confirm('Möchtest du diese Vermögenssteuerklasse wirklich löschen?')) {
			return;
		}

		try {
			await db.collection('wealth_tax_brackets').delete(id);
			await loadWealthTaxBrackets();
		} catch (error) {
			alert('Fehler beim Löschen der Vermögenssteuerklasse: ' + error);
		}
		saveAny();
	}

	function openWealthTaxBracketModal() {
		wealthTaxBracketModal?.showModal();
	}

	function closeWealthTaxBracketModal() {
		wealthTaxBracketModal?.close();
	}

	function openCreateWealthBracketModal(company: boolean = false) {
		editingWealthBracketId = null;
		newWealthBracketFrom = 0;
		newWealthBracketAmount = 0;
		newWealthBracketCompany = company;
		openWealthTaxBracketModal();
	}

	function openEditWealthBracketModal(bracket: WealthTaxBracket) {
		editingWealthBracketId = bracket.id;
		newWealthBracketFrom = bracket.from;
		newWealthBracketAmount = toPercent(bracket.amount);
		newWealthBracketCompany = bracket.company;

		openWealthTaxBracketModal();
	}

	function onAccountSelect(event) {
		const account = event.detail;
		if (userInsert === 'tax') {
			taxCollectionAccount = account.id;
		} else if (userInsert === 'warehouse') {
			warehouseAccount = account.id;
		}
		saveAny();
		accountSelectModal.close();
	}

	// Reset Functions
	const resetDisplay = () => {
		logoUpload = null;
		logoFiles = null;
		loadAll();
	};
	const resetCurrency = () => {
		currencyUpload = null;
		currencyInputFiles = null;
		loadAll();
	};
	const resetRecurring = () => loadAll();
	const resetWarehouse = () => loadAll();
	const resetTaxes = () => loadAll();
	const resetTaxTracking = () => loadAll();

	// Aliases for old function names
	const resetAnzeige = resetDisplay;
	const resetSteuern = resetTaxes;

	// Save Functions
	async function saveDisplay() {
		const disp: any = await db.collection('settings').getFirstListItem(`key="display_name"`);
		await db.collection('settings').update(disp.id, { value: displayName });

		const logoRec: any = await db.collection('settings').getFirstListItem(`key="logo"`);
		if (logoUpload) {
			const form = new FormData();
			form.append('value', 'file');
			form.append('file', logoUpload);
			await db.collection('settings').update(logoRec.id, form);
		}

		const email: any = await db.collection('settings').getFirstListItem(`key="contact_email"`);
		await db.collection('settings').update(email.id, { value: contactEmail });

		const notifyRec: any = await db
			.collection('settings')
			.getFirstListItem(`key="notify_transactions"`);
		await db
			.collection('settings')
			.update(notifyRec.id, { value: notifyTransactions ? 'true' : 'false' });

		const passwordsChangeableRec: any = await db
			.collection('settings')
			.getFirstListItem(`key="passwords_changeable"`);
		await db
			.collection('settings')
			.update(passwordsChangeableRec.id, { value: passwordsChangeable ? 'true' : 'false' });

		await saveAny();
		await loadAll();
		openModal = null;
	}

	async function saveCurrency() {
		const rec: any = await db.collection('settings').getFirstListItem(`key="currency"`);
		if (currencyUpload) {
			const form = new FormData();
			form.append('value', 'file');
			form.append('file', currencyUpload);
			await db.collection('settings').update(rec.id, form);
		} else {
			await db.collection('settings').update(rec.id, { value: currencyString });
		}
		await saveAny();
		await loadAll();
	}

	// Alias for backward compatibility
	const saveAnzeige = saveDisplay;

	async function saveRecurring() {
		const handleRec: any = await db
			.collection('settings')
			.getFirstListItem(`key="handle_recurring_payments"`);
		await db
			.collection('settings')
			.update(handleRec.id, { value: handleRecurringPayments ? 'true' : 'false' });

		const updateRec: any = await db
			.collection('settings')
			.getFirstListItem(`key="recurring_payments_update_timer"`);
		await db
			.collection('settings')
			.update(updateRec.id, { value: String(Math.max(0, recurringPaymentsUpdateTimer)) });

		const resetRec: any = await db
			.collection('settings')
			.getFirstListItem(`key="recurring_payments_reset"`);
		const secs = hmsToSeconds(recurringResetHours, recurringResetMinutes, recurringResetSeconds);
		await db.collection('settings').update(resetRec.id, { value: String(secs) });
		await saveAny();
		await loadAll();
	}

	async function saveWarehouse() {
		const rec = await db.collection('settings').getFirstListItem(`key="warehouse_enable"`);
		await db.collection('settings').update(rec.id, { value: enableWarehouse ? 'true' : 'false' });

		const collRec: any = await db
			.collection('settings')
			.getFirstListItem(`key="warehouse_account"`);
		await db.collection('settings').update(collRec.id, { value: warehouseAccount });
		await saveAny();
		await loadAll();
	}

	async function saveTaxes() {
		const enableRec: any = await db.collection('settings').getFirstListItem(`key="tax_enabled"`);
		await db
			.collection('settings')
			.update(enableRec.id, { value: enableTaxation ? 'true' : 'false' });

		const collRec: any = await db
			.collection('settings')
			.getFirstListItem(`key="tax_collection_account"`);
		await db.collection('settings').update(collRec.id, { value: taxCollectionAccount });

		let data: any = await db.collection('settings').getFirstListItem(`key="tax_income_enable"`);
		await db.collection('settings').update(data.id, { value: enableIncomeTax ? 'true' : 'false' });

		data = await db.collection('settings').getFirstListItem(`key="tax_transaction_enable"`);
		await db
			.collection('settings')
			.update(data.id, { value: enableTransactionTax ? 'true' : 'false' });

		data = await db.collection('settings').getFirstListItem(`key="tax_transaction_amount"`);
		await db
			.collection('settings')
			.update(data.id, { value: String(fromPercent(transactionTaxPercentage)) });

		data = await db.collection('settings').getFirstListItem(`key="tax_transaction_free"`);
		await db.collection('settings').update(data.id, { value: String(transactionFreeRate) });

		data = await db.collection('settings').getFirstListItem(`key="tax_pool_reset"`);
		const transactionSecs = hmsToSeconds(
			transactionPoolHours,
			transactionPoolMinutes,
			transactionPoolSeconds
		);
		await db.collection('settings').update(data.id, { value: String(transactionSecs) });

		data = await db.collection('settings').getFirstListItem(`key="tax_vat_enable"`);
		await db.collection('settings').update(data.id, { value: enableVat ? 'true' : 'false' });

		data = await db.collection('settings').getFirstListItem(`key="tax_vat_amount"`);
		await db.collection('settings').update(data.id, { value: String(fromPercent(vatRatePercent)) });

		data = await db.collection('settings').getFirstListItem(`key="tax_wealth_enable"`);
		await db.collection('settings').update(data.id, { value: enableWealthTax ? 'true' : 'false' });

		data = await db.collection('settings').getFirstListItem(`key="tax_wealth_deduct_time"`);
		const wealthSecs = hmsToSeconds(
			taxWealthResetHours,
			taxWealthResetMinutes,
			taxWealthResetSeconds
		);
		await db.collection('settings').update(data.id, { value: String(wealthSecs) });

		data = await db.collection('settings').getFirstListItem(`key="tax_company_enable"`);
		await db.collection('settings').update(data.id, { value: enableCompanyTax ? 'true' : 'false' });
		await saveAny();
		await loadAll();
	}

	async function saveTaxTracking() {
		const email: any = await db
			.collection('settings')
			.getFirstListItem(`key="case_email_receiver"`);
		await db.collection('settings').update(email.id, { value: taxTrackingEmail });

		const enableEmail: any = await db
			.collection('settings')
			.getFirstListItem(`key="case_send_email"`);
		await db
			.collection('settings')
			.update(enableEmail.id, { value: enableTaxTrackingEmail ? 'true' : 'false' });

		await saveAny();
		await loadAll();
	}

	// Alias for backward compatibility
	const saveSteuern = saveTaxes;

	function saveAny() {
		fetch(server + '/api/admin/pull-settings', {
			method: 'POST',
			headers: { Authorization: `Bearer ${db.authStore.token}` }
		});
		deleteAllCaches();
	}

	onMount(async () => {
		if (!browser) return;
		await loadAll();
	});
</script>

<title>Admin - Setup</title>
<AdminSite>
	<div class="flex flex-col gap-1 mb-8">
		<h1 class="text-4xl font-bold tracking-tight">Einstellungen</h1>
		<p class="text-base-content/70">Verwalte alle Systemkonfigurationen zentral</p>
	</div>

	<!-- Tab Navigation -->
	<div class="tabs tabs-bordered mb-8 border-b-2">
		<button
			class="tab {activeTab === 'display' ? 'tab-active' : ''} gap-2"
			on:click={() => (activeTab = 'display')}
			><ImageIcon size={20} /><span class="hidden sm:inline">Anzeige</span></button
		>
		<button
			class="tab {activeTab === 'automation' ? 'tab-active' : ''} gap-2"
			on:click={() => (activeTab = 'automation')}
			><HandCoins size={20} /><span class="hidden sm:inline">Automatisierung</span></button
		>
		<button
			class="tab {activeTab === 'taxes' ? 'tab-active' : ''} gap-2"
			on:click={() => (activeTab = 'taxes')}
			><Receipt size={20} /><span class="hidden sm:inline">Steuern</span></button
		>
	</div>

	<!-- DISPLAY TAB -->
	{#if activeTab === 'display'}
		<div class="space-y-8">
			<!-- Display Settings Card -->
			<div class="card bg-base-100 border border-base-300">
				<div class="card-body">
					<div class="flex items-center gap-3 mb-6 pb-4 border-b border-base-300">
						<div class="p-3 rounded-lg bg-primary/10 border border-primary/20">
							<ImageIcon size={24} class="text-primary" />
						</div>
						<div>
							<h3 class="font-bold text-lg">Anzeige & Branding</h3>
							<p class="text-xs text-base-content/60">Konfiguriere das Aussehen der Anwendung</p>
						</div>
					</div>
					<div class="space-y-4">
						<div class="space-y-2">
							<!-- svelte-ignore a11y_label_has_associated_control -->
							<label class="label"
								><span class="label-text font-semibold">Anwendungsname</span></label
							>
							<input
								class="input input-bordered w-full"
								type="text"
								bind:value={displayName}
								placeholder="Einstein Bank"
							/>
							<p class="text-xs text-base-content/60">Der Name wird überall angezeigt</p>
						</div>
						<div class="divider"></div>
						<div class="space-y-2">
							<!-- svelte-ignore a11y_label_has_associated_control -->
							<label class="label"><span class="label-text font-semibold">Logo</span></label>
							<input
								class="file-input file-input-bordered w-full"
								type="file"
								bind:files={logoFiles}
								on:input={onLogoFileInput}
							/>
							<p class="text-xs text-base-content/60">PNG oder JPG (empfohlen: quadratisch)</p>
							{#if logoUpload || logoFileUrl}<div class="alert alert-success mt-2">
									<div class="flex items-center gap-2">
										<CheckCircle2 size={18} /><span class="text-sm"
											>{logoUpload ? 'Neue Datei bereit zum Speichern' : 'Logo konfiguriert'}</span
										>
									</div>
								</div>{/if}
						</div>
						<div class="divider"></div>
						<div class="space-y-2">
							<!-- svelte-ignore a11y_label_has_associated_control -->
							<label class="label"
								><span class="label-text font-semibold">Kontakt Email Adresse</span></label
							>
							<input
								class="input input-bordered w-full"
								type="text"
								bind:value={contactEmail}
								placeholder="Kontakt Email-Adresse"
							/>
							<p class="text-xs text-base-content/60">Email-Adresse für Kontaktanfragen</p>
						</div>
						<div class="divider"></div>
						<div class="space-y-2">
							<!-- svelte-ignore a11y_label_has_associated_control -->
							<label class="label"
								><span class="label-text font-semibold">Passwörter änderbar</span></label
							>
							<div
								class="flex items-center gap-3 p-3 rounded-lg bg-base-200/50 border border-base-300"
							>
								<input
									type="checkbox"
									class="toggle toggle-success"
									bind:checked={passwordsChangeable}
								/>
								<span class="text-sm">{passwordsChangeable ? 'Aktiviert' : 'Deaktiviert'}</span>
							</div>
							<p class="text-xs text-base-content/60">
								Stelle ein, ob die Nutzer ihre Passwörter ändern können
							</p>
						</div>
						<div class="divider"></div>
						<div class="space-y-2">
							<!-- svelte-ignore a11y_label_has_associated_control -->
							<label class="label"
								><span class="label-text font-semibold">Transaktionsbenachrichtigungen</span></label
							>
							<div
								class="flex items-center gap-3 p-3 rounded-lg bg-base-200/50 border border-base-300"
							>
								<input
									type="checkbox"
									class="toggle toggle-success"
									bind:checked={notifyTransactions}
								/>
								<span class="text-sm">{notifyTransactions ? 'Aktiviert' : 'Deaktiviert'}</span>
							</div>
							<p class="text-xs text-base-content/60">
								Erhalte Benachrichtigungen bei Transaktionen
							</p>
						</div>
						<div class="flex gap-2 pt-4">
							<button
								class="btn btn-ghost"
								on:click={() => {
									logoUpload = null;
									logoFiles = null;
									loadAll();
								}}><RefreshCcw size={16} />Neu laden</button
							>
							<button class="btn btn-primary" on:click={saveDisplay}
								><Save size={16} />Speichern</button
							>
						</div>
					</div>
				</div>
			</div>

			<!-- Currency Settings Card -->
			<div class="card bg-base-100 border border-base-300">
				<div class="card-body">
					<div class="flex items-center gap-3 mb-6 pb-4 border-b border-base-300">
						<div class="p-3 rounded-lg bg-warning/10 border border-warning/20">
							<Coins size={24} class="text-warning" />
						</div>
						<div>
							<h3 class="font-bold text-lg">Währung</h3>
							<p class="text-xs text-base-content/60">Wähle ein Symbol oder Logo</p>
						</div>
					</div>
					<div class="space-y-4">
						<div class="space-y-2">
							<!-- svelte-ignore a11y_label_has_associated_control -->
							<label class="label"><span class="label-text font-semibold">Textsymbol</span></label>
							<input
								class="input input-bordered w-full"
								type="text"
								bind:value={currencyString}
								placeholder="€"
								disabled={currencyIsFile && !!currencyFileUrl && !currencyUpload}
							/>
							<p class="text-xs text-base-content/60">Kurze Werte (1-3 Zeichen) wirken am besten</p>
						</div>
						<div class="divider"></div>
						<div class="space-y-2">
							<!-- svelte-ignore a11y_label_has_associated_control -->
							<label class="label"><span class="label-text font-semibold">Datei Logo</span></label>
							<input
								class="file-input file-input-bordered w-full"
								type="file"
								bind:files={currencyInputFiles}
								on:input={onCurrencyFileInput}
							/>
							<p class="text-xs text-base-content/60">PNG, JPG oder SVG</p>
							{#if currencyUpload || currencyFileUrl}<div class="alert alert-success mt-2">
									<div class="flex items-center gap-2">
										<CheckCircle2 size={18} /><span class="text-sm"
											>{currencyUpload
												? 'Neue Datei bereit zum Speichern'
												: 'Datei konfiguriert'}</span
										>
									</div>
								</div>{/if}
						</div>
						<div class="flex gap-2 pt-4">
							<button
								class="btn btn-ghost"
								on:click={() => {
									currencyUpload = null;
									currencyInputFiles = null;
									loadAll();
								}}><RefreshCcw size={16} />Neu laden</button
							>
							<button class="btn btn-primary" on:click={saveCurrency}
								><Save size={16} />Speichern</button
							>
						</div>
					</div>
				</div>
			</div>
		</div>
	{/if}

	<!-- AUTOMATION TAB -->
	{#if activeTab === 'automation'}
		<div class="space-y-8">
			<!-- Recurring Payments Card -->
			<div class="card bg-base-100 border border-base-300">
				<div class="card-body">
					<div class="flex items-center gap-3 mb-6 pb-4 border-b border-base-300">
						<div class="p-3 rounded-lg bg-accent/10 border border-accent/20">
							<Clock size={24} class="text-accent" />
						</div>
						<div>
							<h3 class="font-bold text-lg">Wiederkehrende Zahlungen</h3>
							<p class="text-xs text-base-content/60">Automatische Transaktionen</p>
						</div>
					</div>
					<div class="space-y-4">
						<div class="space-y-2">
							<!-- svelte-ignore a11y_label_has_associated_control -->
							<label class="label"><span class="label-text font-semibold">Aktivieren</span></label>
							<div
								class="flex items-center gap-3 p-3 rounded-lg bg-base-200/50 border border-base-300"
							>
								<input
									type="checkbox"
									class="toggle toggle-success"
									bind:checked={handleRecurringPayments}
								/>
								<span class="text-sm">{handleRecurringPayments ? 'Aktiviert' : 'Deaktiviert'}</span>
							</div>
						</div>
						<div class="divider"></div>
						<div class="space-y-2">
							<!-- svelte-ignore a11y_label_has_associated_control -->
							<label class="label"
								><span class="label-text font-semibold">Update Intervall (ms)</span></label
							>
							<input
								class="input input-bordered w-full"
								type="number"
								bind:value={recurringPaymentsUpdateTimer}
								placeholder="1000"
							/>
						</div>
						<div class="divider"></div>
						<div class="space-y-2">
							<!-- svelte-ignore a11y_label_has_associated_control -->
							<label class="label"
								><span class="label-text font-semibold">Täglich Neu laden um (HH:MM:SS)</span
								></label
							>
							<div class="grid grid-cols-3 gap-2">
								<div class="flex flex-col">
									<p class="fieldset-legend text-xs mt-1">Stunden</p>
									<input
										class="input input-bordered w-full text-center text-sm"
										type="number"
										bind:value={recurringResetHours}
										min="0"
										max="23"
										placeholder="HH"
									/>
								</div>
								<div class="flex flex-col">
									<p class="fieldset-legend text-xs mt-1">Minuten</p>
									<input
										class="input input-bordered w-full text-center text-sm"
										type="number"
										bind:value={recurringResetMinutes}
										min="0"
										max="59"
										placeholder="MM"
									/>
								</div>
								<div class="flex flex-col">
									<p class="fieldset-legend text-xs mt-1">Sekunden</p>
									<input
										class="input input-bordered w-full text-center text-sm"
										type="number"
										bind:value={recurringResetSeconds}
										min="0"
										max="59"
										placeholder="SS"
									/>
								</div>
							</div>
						</div>
						<div class="flex gap-2 pt-4">
							<button class="btn btn-ghost" on:click={resetRecurring}
								><RefreshCcw size={16} />Neu laden</button
							>
							<button class="btn btn-primary" on:click={saveRecurring}
								><Save size={16} />Speichern</button
							>
						</div>
					</div>
				</div>
			</div>

			<!-- Warehouse Card -->
			<div class="card bg-base-100 border border-base-300">
				<div class="card-body">
					<div class="flex items-center gap-3 mb-6 pb-4 border-b border-base-300">
						<div class="p-3 rounded-lg bg-secondary/10 border border-secondary/20">
							<HandCoins size={24} class="text-secondary" />
						</div>
						<div>
							<h3 class="font-bold text-lg">Warenlager</h3>
							<p class="text-xs text-base-content/60">Warenlager-Funktionalität</p>
						</div>
					</div>
					<div class="space-y-4">
						<div class="space-y-2">
							<!-- svelte-ignore a11y_label_has_associated_control -->
							<label class="label"><span class="label-text font-semibold">Aktivieren</span></label>
							<div
								class="flex items-center gap-3 p-3 rounded-lg bg-base-200/50 border border-base-300"
							>
								<input
									type="checkbox"
									class="toggle toggle-success"
									bind:checked={enableWarehouse}
								/>
								<span class="text-sm">{enableWarehouse ? 'Aktiviert' : 'Deaktiviert'}</span>
							</div>
						</div>
						<div class="divider"></div>
						<div class="space-y-2">
							<!-- svelte-ignore a11y_label_has_associated_control -->
							<label class="label"
								><span class="label-text font-semibold">Warenlager Account</span></label
							>
							<button
								class="btn btn-bordered w-full justify-start"
								on:click={() => {
									userInsert = 'warehouse';
									accountSelectModal.showModal();
								}}
								disabled={!enableWarehouse}
								>{warehouseAccount
									? `Account ID: ${warehouseAccount}`
									: 'Account auswählen'}</button
							>
							<p class="text-xs text-base-content/60">Account für Warenlager Verwaltung</p>
						</div>
						<div class="flex gap-2 pt-4">
							<button class="btn btn-ghost" on:click={resetWarehouse}
								><RefreshCcw size={16} />Neu laden</button
							>
							<button class="btn btn-primary" on:click={saveWarehouse}
								><Save size={16} />Speichern</button
							>
						</div>
					</div>
				</div>
			</div>
			<!-- Tax Tracking Card -->
			<div class="card bg-base-100 border border-base-300">
				<div class="card-body">
					<div class="flex items-center gap-3 mb-6 pb-4 border-b border-base-300">
						<div class="p-3 rounded-lg bg-secondary/10 border border-secondary/20">
							<Cctv size={24} class="text-secondary" />
						</div>
						<div>
							<h3 class="font-bold text-lg">Steuerverfolgung</h3>
							<p class="text-xs text-base-content/60">Steuersystem-Funktionalität</p>
						</div>
					</div>
					<div class="space-y-4">
						<div class="space-y-2">
							<!-- svelte-ignore a11y_label_has_associated_control -->
							<label class="label"
								><span class="label-text font-semibold">Email Aktivieren</span></label
							>
							<div
								class="flex items-center gap-3 p-3 rounded-lg bg-base-200/50 border border-base-300"
							>
								<input
									type="checkbox"
									class="toggle toggle-success"
									bind:checked={enableTaxTrackingEmail}
								/>
								<span class="text-sm">{enableTaxTrackingEmail ? 'Aktiviert' : 'Deaktiviert'}</span>
							</div>
						</div>
						<div class="space-y-2">
							<!-- svelte-ignore a11y_label_has_associated_control -->
							<label class="label"
								><span class="label-text font-semibold">Steuerverfolgung Email Adresse</span></label
							>
							<input
								class="input input-bordered w-full"
								type="text"
								bind:value={taxTrackingEmail}
								placeholder="Email-Adresse für Steuerverfolgung"
							/>
							<p class="text-xs text-base-content/60">Email-Adresse für Steuerverfolgung</p>
						</div>
						<div class="flex gap-2 pt-4">
							<button class="btn btn-ghost" on:click={resetTaxTracking}
								><RefreshCcw size={16} />Neu laden</button
							>
							<button class="btn btn-primary" on:click={saveTaxTracking}
								><Save size={16} />Speichern</button
							>
						</div>
					</div>
				</div>
			</div>
		</div>
	{/if}

	<!-- TAXES TAB -->
	{#if activeTab === 'taxes'}
		<div class="space-y-8">
			<!-- Tax System Overview -->
			<div class="card bg-base-100 border border-base-300">
				<div class="card-body">
					<div class="flex items-center gap-3 mb-6 pb-4 border-b border-base-300">
						<div class="p-3 rounded-lg bg-info/10 border border-info/20">
							<Receipt size={24} class="text-info" />
						</div>
						<div>
							<h3 class="font-bold text-lg">Steuersystem</h3>
							<p class="text-xs text-base-content/60">Alle Steuerarten und Regeln</p>
						</div>
					</div>
					<div class="space-y-4">
						<div class="space-y-2">
							<div class="flex items-center gap-2">
								<input
									type="checkbox"
									class="checkbox checkbox-primary"
									bind:checked={enableTaxation}
								/>
								<span class="font-semibold">Steuersystem aktivieren</span>
							</div>
						</div>
						<div class="divider"></div>
						<div class="space-y-2">
							<!-- svelte-ignore a11y_label_has_associated_control -->
							<label class="label"><span class="label-text font-semibold">Sammelkonto</span></label>
							<button
								class="btn btn-bordered w-full justify-start"
								on:click={() => {
									userInsert = 'tax';
									accountSelectModal.showModal();
								}}
								disabled={!enableTaxation}
								>{taxCollectionAccount
									? `Account ID: ${taxCollectionAccount}`
									: 'Account auswählen'}</button
							>
							<p class="text-xs text-base-content/60">Account, auf dem Steuern gesammelt werden</p>
						</div>
						<div class="divider"></div>

						<!-- Einkommensteuer -->
						<div class="space-y-3">
							<div class="flex items-center gap-2">
								<input
									type="checkbox"
									class="checkbox"
									bind:checked={enableIncomeTax}
									disabled={!enableTaxation}
								/>
								<span class="font-semibold">Einkommensteuer</span>
							</div>
							{#if enableIncomeTax && enableTaxation}
								<div class="ml-6 space-y-4">
									<!-- Income Tax Brackets Table -->
									<div class="overflow-x-auto">
										<table class="table table-sm">
											<thead>
												<tr>
													<th>Mindesteinkommen</th>
													<th>Steuersatz (%)</th>
													<th>Aktionen</th>
												</tr>
											</thead>
											<tbody>
												{#each incomeTaxBrackets as bracket (bracket.id)}
													<tr>
														<td>{bracket.from}</td>
														<td>{toPercent(bracket.amount).toFixed(2)}</td>
														<td>
															<button
																class="btn btn-xs btn-ghost"
																on:click={() => openEditBracketModal(bracket)}>Bearbeiten</button
															>
															<button
																class="btn btn-xs btn-ghost text-error"
																on:click={() => deleteIncomeTaxBracket(bracket.id)}>Löschen</button
															>
														</td>
													</tr>
												{/each}
											</tbody>
										</table>
									</div>

									<!-- Add Bracket Button -->
									<div class="flex gap-2 pt-4">
										<button class="btn btn-sm btn-primary" on:click={openCreateBracketModal}
											>+ Neue Steuerklasse</button
										>
									</div>
								</div>
							{/if}
						</div>
						<div class="divider"></div>

						<!-- Transaktionssteuer -->
						<div class="space-y-3">
							<div class="flex items-center gap-2">
								<input
									type="checkbox"
									class="checkbox"
									bind:checked={enableTransactionTax}
									disabled={!enableTaxation}
								/>
								<span class="font-semibold">Transaktionssteuer</span>
							</div>
							{#if enableTransactionTax && enableTaxation}
								<div class="ml-6 space-y-3">
									<!-- svelte-ignore a11y_label_has_associated_control -->
									<div>
										<label class="label"
											><span class="label-text text-sm">Prozentsatz (%)</span></label
										><input
											class="input input-bordered w-full input-sm"
											type="number"
											bind:value={transactionTaxPercentage}
											step="0.01"
										/>
									</div>
									<!-- svelte-ignore a11y_label_has_associated_control -->
									<div>
										<label class="label"
											><span class="label-text text-sm">Kostenlos bis zum Betrag</span></label
										><input
											class="input input-bordered w-full input-sm"
											type="number"
											bind:value={transactionFreeRate}
											step="0.01"
										/>
									</div>
									<!-- svelte-ignore a11y_label_has_associated_control -->
									<div>
										<label class="label"
											><span class="label-text text-sm">Pool-Neu laden (HH:MM:SS)</span></label
										>
										<div class="grid grid-cols-3 gap-2">
											<input
												class="input input-bordered input-sm text-center"
												type="number"
												bind:value={transactionPoolHours}
												min="0"
												max="23"
												placeholder="HH"
											/>
											<input
												class="input input-bordered input-sm text-center"
												type="number"
												bind:value={transactionPoolMinutes}
												min="0"
												max="59"
												placeholder="MM"
											/>
											<input
												class="input input-bordered input-sm text-center"
												type="number"
												bind:value={transactionPoolSeconds}
												min="0"
												max="59"
												placeholder="SS"
											/>
										</div>
									</div>
								</div>
							{/if}
						</div>
						<div class="divider"></div>

						<!-- Mehrwertsteuer -->
						<div class="space-y-3">
							<div class="flex items-center gap-2">
								<input
									type="checkbox"
									class="checkbox"
									bind:checked={enableVat}
									disabled={!enableTaxation}
								/>
								<span class="font-semibold">Mehrwertsteuer (MwSt)</span>
							</div>
							{#if enableVat && enableTaxation}
								<!-- svelte-ignore a11y_label_has_associated_control -->
								<div class="ml-6">
									<label class="label"><span class="label-text text-sm">MwSt Satz (%)</span></label
									><input
										class="input input-bordered w-full input-sm"
										type="number"
										bind:value={vatRatePercent}
										step="0.01"
									/>
								</div>
							{/if}
						</div>
						<div class="divider"></div>

						<!-- Vermögenssteuer -->
						<div class="space-y-3">
							<div class="flex items-center gap-2">
								<input
									type="checkbox"
									class="checkbox"
									bind:checked={enableWealthTax}
									disabled={!enableTaxation}
								/>
								<span class="font-semibold">Vermögenssteuer</span>
							</div>
							{#if enableWealthTax && enableTaxation}
								<div class="ml-6 space-y-4">
									<p class="text-sm text-muted-foreground font-bold">
										Vermögenssteuer auf das Vermögen der Privatpersonen
									</p>
									<!-- Wealth Tax Brackets Table -->
									<div class="overflow-x-auto">
										<table class="table table-sm">
											<thead>
												<tr>
													<th>Minimales Vermögen</th>
													<th>Steuersatz (%)</th>
													<th>Aktionen</th>
												</tr>
											</thead>
											<tbody>
												{#each wealthTaxBrackets as bracket (bracket.id)}
													{#if !bracket.company}
														<tr>
															<td>{bracket.from}</td>
															<td>{toPercent(bracket.amount).toFixed(2)}</td>
															<td>
																<button
																	class="btn btn-xs btn-ghost"
																	on:click={() => openEditWealthBracketModal(bracket)}
																	>Bearbeiten</button
																>
																<button
																	class="btn btn-xs btn-ghost text-error"
																	on:click={() => deleteWealthTaxBracket(bracket.id)}
																	>Löschen</button
																>
															</td>
														</tr>
													{/if}
												{/each}
											</tbody>
										</table>
									</div>

									<!-- Add Bracket Button -->
									<div class="flex gap-2 pt-4">
										<button
											class="btn btn-sm btn-primary"
											on:click={() => openCreateWealthBracketModal(false)}
											>+ Neue Steuerklasse</button
										>
									</div>

									<p class="text-sm text-muted-foreground font-bold">
										Vermögenssteuer auf das Vermögen der Unternehmen
									</p>

									<div class="overflow-x-auto">
										<table class="table table-sm">
											<thead>
												<tr>
													<th>Minimales Vermögen</th>
													<th>Steuersatz (%)</th>
													<th>Aktionen</th>
												</tr>
											</thead>
											<tbody>
												{#each wealthTaxBrackets as bracket (bracket.id)}
													{#if bracket.company}
														<tr>
															<td>{bracket.from}</td>
															<td>{toPercent(bracket.amount).toFixed(2)}</td>
															<td>
																<button
																	class="btn btn-xs btn-ghost"
																	on:click={() => openEditWealthBracketModal(bracket)}
																	>Bearbeiten</button
																>
																<button
																	class="btn btn-xs btn-ghost text-error"
																	on:click={() => deleteWealthTaxBracket(bracket.id)}
																	>Löschen</button
																>
															</td>
														</tr>
													{/if}
												{/each}
											</tbody>
										</table>
									</div>

									<!-- Add Bracket Button -->
									<div class="flex gap-2 pt-4">
										<button
											class="btn btn-sm btn-primary"
											on:click={() => openCreateWealthBracketModal(true)}
											>+ Neue Steuerklasse</button
										>
									</div>

									<div class="divider"></div>

									<!-- Reset Time -->
									<!-- svelte-ignore a11y_label_has_associated_control -->
									<div>
										<label class="label"
											><span class="label-text text-sm">Täglich abziehen um (HH:MM:SS)</span></label
										>
										<div class="grid grid-cols-3 gap-2">
											<input
												class="input input-bordered input-sm text-center"
												type="number"
												bind:value={taxWealthResetHours}
												min="0"
												max="23"
												placeholder="HH"
											/>
											<input
												class="input input-bordered input-sm text-center"
												type="number"
												bind:value={taxWealthResetMinutes}
												min="0"
												max="59"
												placeholder="MM"
											/>
											<input
												class="input input-bordered input-sm text-center"
												type="number"
												bind:value={taxWealthResetSeconds}
												min="0"
												max="59"
												placeholder="SS"
											/>
										</div>
									</div>
								</div>
							{/if}
						</div>
						<div class="divider"></div>

						<!-- Unternehmenssteuer
						<div class="space-y-2">
							<div class="flex items-center gap-2">
								<input type="checkbox" class="checkbox" bind:checked={enableCompanyTax} disabled={!enableTaxation} />
								<span class="font-semibold">Unternehmenssteuer</span>
							</div>
						</div>
						-->

						<div class="flex gap-2 pt-4">
							<button class="btn btn-ghost" on:click={resetTaxes}
								><RefreshCcw size={16} />Neu laden</button
							>
							<button class="btn btn-primary" on:click={saveTaxes}
								><Save size={16} />Speichern</button
							>
						</div>
					</div>
				</div>
			</div>
		</div>
	{/if}

	<!-- Income Tax Bracket Modal -->
	<dialog id="income_tax_bracket_modal" class="modal" bind:this={incomeTaxBracketModal}>
		<div class="modal-box">
			<h3 class="text-lg font-bold">
				{editingBracketId ? 'Steuerklasse bearbeiten' : 'Neue Steuerklasse hinzufügen'}
			</h3>
			<div class="py-4 space-y-4">
				<div>
					<label class="label" for="bracket_from"
						><span class="label-text">Mindesteinkommen</span></label
					>
					<input
						id="bracket_from"
						class="input input-bordered w-full"
						type="number"
						bind:value={newBracketFrom}
						placeholder="0"
						step="0.01"
					/>
				</div>
				<div>
					<label class="label" for="bracket_amount"
						><span class="label-text">Steuersatz (%)</span></label
					>
					<input
						id="bracket_amount"
						class="input input-bordered w-full"
						type="number"
						bind:value={newBracketAmount}
						placeholder="0"
						min="0"
						max="100"
						step="0.01"
					/>
				</div>
			</div>
			<div class="modal-action">
				{#if editingBracketId}
					<button class="btn btn-primary" on:click={updateIncomeTaxBracket}>Aktualisieren</button>
				{:else}
					<button class="btn btn-primary" on:click={createIncomeTaxBracket}>Hinzufügen</button>
				{/if}
				<form method="dialog">
					<button class="btn">Abbrechen</button>
				</form>
			</div>
		</div>
		<form method="dialog" class="modal-backdrop">
			<button>close</button>
		</form>
	</dialog>

	<!-- Wealth Tax Bracket Modal -->
	<dialog id="wealth_tax_bracket_modal" class="modal" bind:this={wealthTaxBracketModal}>
		<div class="modal-box">
			<h3 class="text-lg font-bold">
				{editingWealthBracketId
					? 'Vermögenssteuerklasse bearbeiten'
					: 'Neue Vermögenssteuerklasse hinzufügen'}
			</h3>
			<div class="py-4 space-y-4">
				<div>
					<label class="label" for="wealth_bracket_from"
						><span class="label-text">Minimales Vermögen</span></label
					>
					<input
						id="wealth_bracket_from"
						class="input input-bordered w-full"
						type="number"
						bind:value={newWealthBracketFrom}
						placeholder="0"
						step="0.01"
					/>
				</div>
				<div>
					<label class="label" for="wealth_bracket_amount"
						><span class="label-text">Steuersatz (%)</span></label
					>
					<input
						id="wealth_bracket_amount"
						class="input input-bordered w-full"
						type="number"
						bind:value={newWealthBracketAmount}
						placeholder="0"
						min="0"
						max="100"
						step="0.01"
					/>
				</div>
				<div class="flex items-center gap-2">
					<input
						id="wealth_bracket_company"
						type="checkbox"
						class="checkbox"
						bind:checked={newWealthBracketCompany}
					/>
					<label for="wealth_bracket_company" class="label-text"
						>Für Unternehmen anstatt für Privatpersonen</label
					>
				</div>
			</div>
			<div class="modal-action">
				{#if editingWealthBracketId}
					<button class="btn btn-primary" on:click={updateWealthTaxBracket}>Aktualisieren</button>
				{:else}
					<button class="btn btn-primary" on:click={createWealthTaxBracket}>Hinzufügen</button>
				{/if}
				<form method="dialog">
					<button class="btn">Abbrechen</button>
				</form>
			</div>
		</div>
		<form method="dialog" class="modal-backdrop">
			<button>close</button>
		</form>
	</dialog>
</AdminSite>
<AccountSelectModal bind:this={accountSelectModal} on:select={onAccountSelect} adminMode={true} />

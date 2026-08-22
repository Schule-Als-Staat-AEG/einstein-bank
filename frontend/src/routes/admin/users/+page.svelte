<script lang="ts">
	import { browser } from '$app/environment';
	import db, { server } from '$lib/connections';
	import { getSetting } from '$lib/settings';
	import AdminPanel from '$modules/AdminPanel.svelte';
	import {
	Euro,
		Eye,
		EyeClosed,
		Fingerprint,
		Forklift,
		LockIcon,
		PenIcon,
		TrashIcon,
		UserIcon,
		UserKey,

		UserRoundX

	} from '@lucide/svelte';
	import { onMount } from 'svelte';
	import { getAuthStore } from '$lib/auth';
	import { generateCardPdf } from '$lib/cards';
	import { toSafeQuery } from '$lib/query';

	let users: any[] = [];

	// Selection as Set
	let selectedUsers: Set<string> = new Set();

	let perPage = 30;
	let currentPage = 1;
	let maxPage = 1;
	let totalItems = 0;

	let filter = '';
	let filterDebounced = '';
	let searchTimer: any = null;
	let advancedFilter = false;
	let filterID = "";
	let filterEmail = "";
	let filterName = "";
	let filterCompanyName = "";
	let filterCompanies: "all" | "companies" | "individuals" = "all";

	let sortBy = 'name';
	let sortDirection = 'asc';

	let loading = false;
	let error: string | null = null;

	// Create modal state
	let createModal: HTMLDialogElement;
	let createIsCompany = false;
	let createCompanyName = '';
	let createName = '';
	let createPassword = '';
	let createEmail = '';
	let createLocked = false;
	let createWarehouseAccess = false;
	let createFinanceTrackingAccess = false;
	let createHidden = false;
	let createRegistered = false;
	let createAccountNumber = '';
	let createRequirePin = true;
	let createPin = '';
	let mfaEnabled = false;
	let mfaRequired = false;
	let createEuroSpent = 0;
	let createEuroAvailable = 0;

	let autoGeneratePassword = '';
	let showAutoGenPassword: HTMLDialogElement;

	// Edit modal state
	let editModal: HTMLDialogElement;
	let editUserId = '';
	let editUser: any = null;
	let editName = '';
	let editEmail = '';
	let editIsCompany = false;
	let editCompanyName = '';
	let editPassword = '';
	let editIsLocked = false;
	let editWarehouseAccess = false;
	let editFinanceTrackingAccess = false;
	let editHidden = false;
	let editRegistered = false;
	let editMFAEnabled = false;
	let editMFARequired = false;
	let editEuroSpent = 0;
	let editEuroAvailable = 0;

	// Bulk create
	let massCreateModal: HTMLDialogElement;
	let massCreateIsCompany = false;
	let massCreateIsLocked = false;
	let massCreatePrefix = '';
	let massCreateCount = 0;
	let massCreateStartNumber = 0;
	let massCreatePassword = '';
	let massCreateWarehouseAccess = false;
	let massCreateFinanceTrackingAccess = false;
	let massCreateSaveFileName = 'bulk_created_users.csv';
	let massCreateHidden = false;
	let massCreateRegistered = false;
	let massCreateGeneratePin = true;
	let massCreatePrintBackSide = false;
	let massCreateRunning = false;
	let massCreateReportModal: HTMLDialogElement;
	let massCreateReportLink = '';
	let massCreateEuroSpent = 0;
	let massCreateEuroAvailable = 0;
	let bulkCreatePDF: any = null;

	// Mass edit
	let massEditModal: HTMLDialogElement;
	let massEditPassword = '';
	let massEditUpdateLocked = false;
	let massEditUpdateCompany = false;
	let massEditUpdateHidden = false;
	let massEditUpdateRegistered = false;
	let massEditIsLocked = false;
	let massEditIsCompany = false;
	let massEditUpdateWarehouseAccess = false;
	let massEditUpdateFinanceTrackingAccess = false;
	let massEditWarehouseAccess = false;
	let massEditFinanceTrackingAccess = false;
	let massEditHidden = false;
	let massEditRegistered = false;
	let massEditUpdateMFAEnabled = false;
	let massEditUpdateMFARequired = false;
	let massEditMFAEnabled = false;
	let massEditMFARequired = false;
	let massEditUpdateEuroSpent = false;
	let massEditUpdateEuroAvailable = false;
	let massEditEuroSpent = 0;
	let massEditEuroAvailable = 0;

	// Mass delete
	let massDeleteModal: HTMLDialogElement;

	let highlightUserId = '';

	function escapePB(str: string) {
		return String(str ?? '')
			.replaceAll('\\', '\\\\')
			.replaceAll('"', '\\"');
	}

	function formatDate(dateStr: string | null) {
		if (!dateStr) return 'N/A';
		const d = new Date(dateStr);
		if (isNaN(d.getTime())) return 'N/A';
		return d.toLocaleDateString();
	}
	function formatTime(dateStr: string | null) {
		if (!dateStr) return '';
		const d = new Date(dateStr);
		if (isNaN(d.getTime())) return '';
		return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
	}

	onMount(async () => {
		if (!browser) return;

		// highlight from URL
		const urlParams = new URLSearchParams(window.location.search);
		highlightUserId = urlParams.get('id') ?? '';

		await fetchUsers(true);
	});

	function onSearchInput() {
		if (searchTimer) clearTimeout(searchTimer);
		searchTimer = setTimeout(() => {
			filterDebounced = filter;
			currentPage = 1;
			fetchUsers(false);
		}, 250);
	}

	function getFilterString()
	{
		if (!advancedFilter)
		{
			return 	filter ? `name ~ "${filter}" || companyName ~ "${filter}" || email ~ "${filter}"` : '';
		}
		else
		{
			let filters: string[] = [];
			if (filterID) filters.push(`id ~ "${filterID}"`);
			if (filterEmail) filters.push(`email ~ "${filterEmail}"`);
			if (filterName) filters.push(`name ~ "${filterName}"`);
			if (filterCompanyName) filters.push(`companyName ~ "${filterCompanyName}"`);
			if (filterCompanies === 'companies') filters.push(`isCompany = true`);
			if (filterCompanies === 'individuals') filters.push(`isCompany = false`);

			return filters.join(' && ');
		}
	}

	async function fetchUsers(force = false) {
		if (!browser) return;
		if (loading && !force) return;

		loading = true;
		error = null;

		try {
			if (perPage < 5) perPage = 30;

			const f = escapePB(filterDebounced.trim());

			const sortPrefix = sortDirection === 'desc' ? '-' : '+';
			const result = await db.collection('users').getList(currentPage, perPage, {
				sort: `${sortPrefix}${sortBy}`,
				filter: toSafeQuery(getFilterString()),
				requestKey: null,
				expand: "data"
			});

			maxPage = result.totalPages || 1;
			totalItems = result.totalItems || 0;

			// clamp page
			if (currentPage > maxPage) {
				currentPage = maxPage;
				return await fetchUsers(true);
			}

			users = result.items || [];

			// ensure highlight user is in list and pinned to top
			if (highlightUserId) {
				const found = users.find((u) => u?.id === highlightUserId);
				if (!found) {
					try {
						const one = await db.collection('users').getOne(highlightUserId, { requestKey: null, expand: "data" });
						users = [one, ...users];
					} catch {
						// ignore
					}
				} else {
					users = [found, ...users.filter((u) => u?.id !== highlightUserId)];
				}
			}
		} catch (e: any) {
			error = e?.message ?? 'Unbekannter Fehler beim Laden.';
		} finally {
			loading = false;
		}
	}

	function goToPage(p: number) {
		if (p < 1) p = 1;
		if (p > maxPage) p = maxPage;
		currentPage = p;
		fetchUsers(false);
	}

	// -------- Create --------
	function openCreateModal() {
		createEmail = '';
		createPassword = '';
		createName = '';
		createIsCompany = false;
		createCompanyName = '';
		createLocked = false;
		createWarehouseAccess = false;
		createFinanceTrackingAccess = false;
		createHidden = false;
		createRegistered = false;
		createAccountNumber = '';
		createRequirePin = true;
		createPin = '';
		mfaEnabled = false;
		mfaRequired = false;
		createEuroSpent = 0;
		createEuroAvailable = 0;
		createModal.showModal();
	}

	async function createUser() {
		if (!createName || createName.includes('admin#') || createName.includes('#atm#')) {
			alert('Bitte geben Sie einen gültigen Benutzernamen ein.');
			return;
		}
		// Check if account number format is correct
		// Format: [0-9]{4}-[0-9]{4}
		if (createAccountNumber != '' && !/^(\d{4}-\d{4})$/.test(createAccountNumber)) {
			alert('Ungültiges Kontonummer-Format. Bitte verwenden Sie das Format XXXX-XXXX.');
			return;
		}

		if (createRequirePin && (!createPin || !/^\d{4}$/.test(createPin))) {
			alert(
				"Bitte geben Sie eine gültige 4-stellige PIN ein, da 'PIN erforderlich' aktiviert ist."
			);
			return;
		}

		let email = createEmail;
		if (!email) {
			email = `${createName.replace(/\s+/g, '_').toLowerCase()}@yourdomain.com`;
		}

		let pw = createPassword;
		let autoGenerated = false;

		if (!pw) {
			autoGenerated = true;
			autoGeneratePassword = Math.random().toString(36).slice(-6);
			pw = autoGeneratePassword;
		}

		try {
			const data = await db.collection('users').create(
				{
					verified: true,
					name: createName,
					email: email,
					password: pw,
					passwordConfirm: pw,
					isCompany: createIsCompany,
					locked: createLocked,
					companyName: createIsCompany ? createCompanyName : '',
					_hidden: createHidden,
					mfaEnabled: mfaEnabled,
					mfaRequired: mfaRequired
				},
				{ requestKey: null }
			);

			let accountData = {
				id: undefined,
				user: data.id,
				balance: 0,
				frozen: false
			};
			accountData.id = createAccountNumber || undefined;

			const userData = await db.collection('user_data').create(
				{
					id: data.id,
					user: data.id,
					registered: createRegistered ? 1 : 0,
					warehouseAccess: createWarehouseAccess ? 1 : 0,
					financeTrackingAccess: createFinanceTrackingAccess ? 1 : 0,
					euroSpent: createEuroSpent,
					euroAvailable: createEuroAvailable
				},
				{ requestKey: null }
			);

			db.collection("users").update(data.id, { data: userData.id }, { requestKey: null });

			const account = await db.collection('accounts').create(accountData, { requestKey: null });

			await db.collection('user_tax_data').create(
				{
					user: account.id,
					taxExempt: false,
					vatExempt: false,
					incomeExempt: false,
					wealthExempt: false,
					transactionExempt: false,
					companyExempt: false,
					vatOverride: -1,
					incomeOverride: -1,
					wealthAmountOverride: -1,
					wealthLimitOverride: -1,
					transactionAmountOverride: -1,
					transactionFreeOverride: -1,
					companyOverride: -1,
					globalMultiplier: 1.0,
					taxFreePool: 0
				},
				{ requestKey: null }
			);

			// only once
			let code = await regenerateCode(data.id);

			await db
				.collection('account_validation')
				.update(
					code.id,
					{ codeRequired: createRequirePin, pin: createRequirePin ? createPin : null },
					{ requestKey: null }
				);

			createModal.close();
			await fetchUsers(true);

			if (autoGenerated) showAutoGenPassword.showModal();
		} catch (err: any) {
			alert('Fehler bei der Erstellung des Benutzers: ' + (err?.message ?? err));
		}
	}

	// -------- Edit --------
	function openEditModal(userId: string) {
		editUserId = userId;
		editUser = users.find((u) => u?.id === userId) ?? null;

		editName = '';
		editEmail = '';
		editPassword = '';
		editWarehouseAccess = editUser?.expand?.data?.warehouseAccess;
		editFinanceTrackingAccess = editUser?.expand?.data?.financeTrackingAccess;

		editIsCompany = Boolean(editUser?.isCompany);
		editIsLocked = Boolean(editUser?.locked);
		editCompanyName = editUser?.companyName ?? '';

		editHidden = Boolean(editUser?._hidden);
		editRegistered = Boolean(editUser?.expand?.data?.registered);
		editMFAEnabled = Boolean(editUser?.mfaEnabled);
		editMFARequired = Boolean(editUser?.mfaRequired);

		editEuroSpent = editUser?.expand?.data?.euroSpent ?? 0;
		editEuroAvailable = editUser?.expand?.data?.euroAvailable ?? 0;

		editModal.showModal();
	}

	async function saveEdits() {
		if (!editUserId) return;

		const updateData: any = {};
		const updateUserData: any = {};

		if (editName) updateData.name = editName;
		if (editEmail) updateData.email = editEmail;

		if (editPassword) {
			updateData.password = editPassword;
			updateData.passwordConfirm = editPassword;
		}

		updateData.locked = editIsLocked;
		updateUserData.registered = editRegistered;
		updateData.isCompany = editIsCompany;
		updateData.companyName = editIsCompany ? editCompanyName || 'Company' : null;
		updateUserData.warehouseAccess = editWarehouseAccess;
		updateUserData.financeTrackingAccess = editFinanceTrackingAccess;
		updateData._hidden = editHidden;
		updateData.mfaEnabled = editMFAEnabled;
		updateData.mfaRequired = editMFARequired;

		updateUserData.registered = editRegistered ? 1 : 0;
		updateUserData.euroSpent = editEuroSpent;
		updateUserData.euroAvailable = editEuroAvailable;

		try {
			await db.collection('users').update(editUserId, updateData, { requestKey: null });
			await db.collection('user_data').update(editUserId, updateUserData, { requestKey: null });

			editModal.close();
			editUserId = '';
			editUser = null;
			editHidden = false;

			await fetchUsers(true);
		} catch (err: any) {
			alert('Fehler beim Aktualisieren des Benutzers: ' + (err?.message ?? err));
		}
	}

	// -------- Selection --------
	function toggleUserSelection(userId: string) {
		if (selectedUsers.has(userId)) selectedUsers.delete(userId);
		else selectedUsers.add(userId);
		selectedUsers = new Set(selectedUsers);
	}

	function toggleSelectAll() {
		if (users.length === 0) return;
		if (selectedUsers.size === users.length) selectedUsers.clear();
		else users.forEach((u) => selectedUsers.add(u.id));
		selectedUsers = new Set(selectedUsers);
	}

	$: allSelected = users.length > 0 && selectedUsers.size === users.length;

	// -------- Delete --------
	async function deleteUser(userId: string) {
		if (
			!confirm(
				'Sind Sie sicher, dass Sie diesen Benutzer löschen möchten? Diese Aktion kann nicht rückgängig gemacht werden.\n\nBenutzer-ID: ' +
					userId
			)
		) {
			return;
		}

		try {
			await db.collection('users').delete(userId, { requestKey: null });
			await fetchUsers(true);
		} catch (err: any) {
			alert('Fehler beim Löschen des Benutzers: ' + (err?.message ?? err));
		}
	}

	// -------- Bulk Create --------
	function openMassCreateModal() {
		massCreatePrefix = '';
		massCreateCount = 0;
		massCreateStartNumber = 0;
		massCreatePassword = '';
		massCreateIsCompany = false;
		massCreateIsLocked = false;
		massCreateFinanceTrackingAccess = false;
		massCreateWarehouseAccess = false;
		massCreateGeneratePin = true;
		massCreatePrintBackSide = false;
		massCreateSaveFileName = 'bulk_created_users.csv';
		massCreateHidden = false;
		massCreateRegistered = false;
		massCreateRunning = false;
		massCreateEuroAvailable = 0;
		massCreateEuroSpent = 0;
		massCreateModal.showModal();
	}

	async function bulkCreateUsers() {
		if (!massCreatePrefix || massCreateCount <= 0) {
			alert('Prefix und Anzahl müssen gesetzt sein.');
			return;
		}
		massCreateRunning = true;

		const createdRows: {
			idx: number;
			name: string;
			email: string;
			password: string;
			pin: string;
		}[] = [];

		// light concurrency limit
		const concurrency = 20;
		const tasks: Array<() => Promise<void>> = [];

		let generatedCodes: string[] = [];

		for (let i = 0; i < massCreateCount; i++) {
			tasks.push(async () => {
				const userName = `${massCreatePrefix}${massCreateStartNumber + i}`;
				const pw = massCreatePassword || Math.random().toString(36).slice(-6);

				const email = `${userName.replace(/\s+/g, '_').toLowerCase()}@yourdomain.com`;

				const userData = {
					verified: true,
					name: userName,
					email: email,
					password: pw,
					passwordConfirm: pw,
					isCompany: massCreateIsCompany,
					locked: massCreateIsLocked,
					companyName: massCreateIsCompany ? 'Bulk Created Company' : null,
					_hidden: massCreateHidden
				};

				const userDataData = {
					registered: massCreateRegistered ? 1 : 0,
					warehouseAccess: massCreateWarehouseAccess ? 1 : 0,
					financeTrackingAccess: massCreateFinanceTrackingAccess ? 1 : 0,
					euroSpent: massCreateEuroSpent,
					euroAvailable: massCreateEuroAvailable
				}

				try {
					const u = await db.collection('users').create(userData, { requestKey: null });

					const acc = await db.collection('accounts').create(
						{
							user: u.id,
							balance: 0,
							frozen: false
						},
						{ requestKey: null }
					);

					const ud = await db.collection('user_data').create(
						{
							id: u.id,
							user: u.id,
							...userDataData
						},
						{ requestKey: null }
					);
					db.collection('users').update(u.id, { data: ud.id }, { requestKey: null });
					// account created
					await db.collection('user_tax_data').create(
						{
							user: acc.id,
							taxExempt: false,
							vatExempt: false,
							incomeExempt: false,
							wealthExempt: false,
							transactionExempt: false,
							companyExempt: false,
							vatOverride: -1,
							incomeOverride: -1,
							wealthAmountOverride: -1,
							wealthLimitOverride: -1,
							transactionAmountOverride: -1,
							transactionFreeOverride: -1,
							companyOverride: -1,
							globalMultiplier: 1.0,
							taxFreePool: 0
						},
						{ requestKey: null }
					);

					let code = await regenerateCode(u.id);

					generatedCodes.push(code.id);

					createdRows.push({
						idx: i + 1,
						name: userName,
						email,
						password: pw,
						pin: code.pin ?? ''
					});
				} catch (err: any) {
					alert(`Fehler bei Bulk-User "${userName}": ` + (err?.message ?? err));
				}
			});
		}

		// run tasks with concurrency
		await runWithConcurrency(tasks, concurrency);

		// Clear empty strings
		generatedCodes = generatedCodes.filter((code) => code);

		await createBulkCSVReport(createdRows, generatedCodes);
		massCreateModal.close();
		massCreateRunning = false;
		await fetchUsers(true);
	}

	async function runWithConcurrency(tasks: Array<() => Promise<void>>, limit: number) {
		const pool: Promise<void>[] = [];
		for (const t of tasks) {
			const p = t();
			pool.push(p);
			if (pool.length >= limit) {
				await Promise.race(pool).catch(() => {});
				// clean resolved promises
				for (let i = pool.length - 1; i >= 0; i--) {
					if ((pool[i] as any).status === 'fulfilled') pool.splice(i, 1);
				}
			}
		}
		await Promise.allSettled(pool);
	}

	async function createBulkCSVReport(
		rows: { idx: number; name: string; email: string; password: string; pin: string }[],
		codes: string[]
	) {
		const sep = ';';
		let csv = `Nr${sep}Benutzername${sep}E-Mail${sep}Passwort${sep}Karten-PIN\r\n`;
		for (const r of rows.sort((a, b) => a.idx - b.idx)) {
			let fourLetterPin = r.pin.toString().padStart(4, '0'); // Make sure that the pin always has 4 digits. If the number has less than 4 digits, pad it with leading zeros.
			csv += `${r.idx}${sep}${r.name}${sep}${r.email}${sep}${r.password}${sep}[${fourLetterPin}]\r\n`;
		}
		const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
		const url = URL.createObjectURL(blob);
		massCreateReportLink = url;

		console.log('Generating bulk PDF with codes:', codes);
		bulkCreatePDF = await generateCardPdf(new Set(codes), 5, 2, false, massCreatePrintBackSide);
		massCreateReportModal.showModal();
	}

	// -------- Mass Edit / Delete --------
	async function massUpdate() {
		if (selectedUsers.size === 0) return;

		const updateData: any = {};
		const updateUserData: any = {};

		if (massEditPassword) {
			updateData.password = massEditPassword;
			updateData.passwordConfirm = massEditPassword;
		}
		if (massEditUpdateLocked) {
			updateData.locked = massEditIsLocked;
		}
		if (massEditUpdateRegistered) {
			updateUserData.registered = massEditRegistered;
		}
		if (massEditUpdateCompany) {
			updateData.isCompany = massEditIsCompany;
			updateData.companyName = massEditIsCompany ? 'Mass Edited Company' : null;
		}
		if (massEditUpdateFinanceTrackingAccess) {
			updateUserData.financeTrackingAccess = massEditFinanceTrackingAccess;
		}
		if (massEditUpdateWarehouseAccess) {
			updateUserData.warehouseAccess = massEditWarehouseAccess;
		}
		if (massEditUpdateMFAEnabled) {
			updateData.mfaEnabled = massEditMFAEnabled;
		}
		if (massEditUpdateMFARequired) {
			updateData.mfaRequired = massEditMFARequired;
		}

		if (massEditUpdateHidden) {
			updateData._hidden = massEditHidden;
		}

		if (massEditRegistered) {
			updateUserData.registered = massEditRegistered ? 1 : 0;
		}
		if (massEditUpdateEuroSpent) {
			updateUserData.euroSpent = massEditEuroSpent;
		}
		if (massEditUpdateEuroAvailable) {
			updateUserData.euroAvailable = massEditEuroAvailable;
		}

		if (Object.keys(updateData).length === 0 && Object.keys(updateUserData).length === 0) {
			massEditModal.close();
			return;
		}

		try {
			await Promise.all(
				Array.from(selectedUsers).map((id) =>
					db.collection('users').update(id, updateData, { requestKey: null })
				)
			);

			await Promise.all(
				Array.from(selectedUsers).map((id) =>
					db.collection('user_data').update(id, updateUserData, { requestKey: null })
				)
			);

			massEditModal.close();

			// reset
			massEditPassword = '';
			massEditUpdateLocked = false;
			massEditUpdateCompany = false;
			massEditIsLocked = false;
			massEditIsCompany = false;
			massEditUpdateFinanceTrackingAccess = false;
			massEditUpdateWarehouseAccess = false;
			massEditWarehouseAccess = false;
			massEditFinanceTrackingAccess = false;
			massEditUpdateHidden = false;
			massEditHidden = false;
			massEditUpdateRegistered = false;
			massEditRegistered = false;
			massEditUpdateMFAEnabled = false;
			massEditMFAEnabled = false;
			massEditUpdateMFARequired = false;
			massEditMFARequired = false;
			massEditUpdateEuroSpent = false;
			massEditUpdateEuroAvailable = false;
			massEditEuroSpent = 0;
			massEditEuroAvailable = 0;

			selectedUsers.clear();
			selectedUsers = new Set();

			await fetchUsers(true);
		} catch (err: any) {
			alert('Fehler bei der Massenbearbeitung: ' + (err?.message ?? err));
		}
	}

	async function massDeleteUsers() {
		if (selectedUsers.size === 0) return;

		try {
			await Promise.all(
				Array.from(selectedUsers).map((id) =>
					db.collection('users').delete(id, { requestKey: null })
				)
			);

			massDeleteModal.close();
			selectedUsers.clear();
			selectedUsers = new Set();

			await fetchUsers(true);
		} catch (err: any) {
			alert('Fehler bei der Massenlöschung: ' + (err?.message ?? err));
		}
	}

	async function regenerateCode(userID: string) {
		try {
			const rsp = await fetch(`${server}/api/admin/regenerate-key`, {
				method: 'POST',
				headers: {
					'Content-Type': 'application/json',
					Authorization: `Bearer ${db.authStore.token}`
				},
				body: JSON.stringify({ accountID: userID, generatePin: massCreateGeneratePin })
			});
			if (rsp.ok) {
				const data = await rsp.json();
				return data;
			}
			return '';
		} catch (err: any) {
			alert('Fehler beim Regenerieren des Codes: ' + (err?.message ?? err));
			return '';
		}
	}
</script>

<title>Admin - Benutzer</title>
<AdminPanel
	title="Benutzerverwaltung"
	description="Verwaltung aller Systembenutzer"
	showCreateButton={true}
	showMassCreateButton={true}
	showMassEditButton={true}
	showMassDeleteButton={true}
	massCreateButtonText="Massenhaft erstellen"
	massEditButtonText="Massenbearbeitung"
	massDeleteButtonText="Massenlöschung"
	{totalItems}
	selectedItems={selectedUsers.size}
	on:create={() => openCreateModal()}
	on:massCreate={() => openMassCreateModal()}
	on:massEdit={() => massEditModal.showModal()}
	on:massDelete={() => massDeleteModal.showModal()}
>
	<svelte:fragment slot="filter">
		<div class="flex gap-3 flex-row justify-between">
			<div class="flex w-full">
				<input
					class="input input-bordered w-full"
					type="text"
					placeholder="Nach Name, Firmenname oder E-Mail filtern..."
					bind:value={filter}
					autocomplete="off"
					on:input={onSearchInput}
					disabled={advancedFilter}
				/>
				<button
					class="btn ml-2 btn-outline"
					on:click={() => {
						advancedFilter = !advancedFilter;
						currentPage = 1;
						fetchUsers(true);
					}}
					title={advancedFilter ? 'Erweiterte Filter deaktivieren' : 'Erweiterte Filter aktivieren'}
				>
				Erweiterter Filter: {advancedFilter ? 'AN' : 'AUS'}
				</button>
			</div>

			<div class="flex gap-2 flex-row items-center">
				<select
					class="select select-bordered"
					bind:value={perPage}
					on:change={() => fetchUsers(true)}
				>
					<option value="10">10 / Seite</option>
					<option selected value="30">30 / Seite</option>
					<option value="50">50 / Seite</option>
					<option value="100">100 / Seite</option>
					<option value="200">200 / Seite</option>
					<option value="500">500 / Seite</option>
					<option value="1000">1000 / Seite</option>
					<option value="2000">2000 / Seite</option>
					<option value="5000">5000 / Seite</option>
				</select>

				<select
					class="select select-bordered"
					bind:value={sortBy}
					on:change={async () => {
						currentPage = 1;
						await fetchUsers();
					}}
				>
					<option value="id">ID</option>
					<option value="name">Name</option>
					<option value="email">E-Mail</option>
					<option value="isCompany">Firma</option>
					<option value="companyName">Firmenname</option>
					<option value="locked">Gesperrt</option>
					<option value="_hidden">Versteckt</option>
					<option value="registered">Registriert</option>
					<option value="mfaEnabled">MFA aktiviert</option>
					<option value="created">Erstellt</option>
					<option value="updated">Aktualisiert</option>
				</select>

				<button
					class="btn btn-bordered btn-sm"
					on:click={async () => {
						sortDirection = sortDirection === 'asc' ? 'desc' : 'asc';
						currentPage = 1;
						await fetchUsers();
					}}
					title={sortDirection === 'asc'
						? 'Aufsteigend sortiert, klicken zum Umkehren'
						: 'Absteigend sortiert, klicken zum Umkehren'}
				>
					{sortDirection === 'asc' ? '↑' : '↓'}
				</button>

				<div class="flex-1"></div>
			</div>
		</div>
		{#if advancedFilter}
		<div class="flex gap-3 flex-row mt-4">
			<input
				class="input input-bordered w-full"
				type="text"
				placeholder="Nach ID filtern..."
				bind:value={filterID}
				autocomplete="off"
				on:input={onSearchInput}
			/>
			<input
				class="input input-bordered w-full"
				type="text"
				placeholder="Nach E-Mail filtern..."
				bind:value={filterEmail}
				autocomplete="off"
				on:input={onSearchInput}
			/>
			<input
				class="input input-bordered w-full"
				type="text"
				placeholder="Nach Name filtern..."
				bind:value={filterName}
				autocomplete="off"
				on:input={onSearchInput}
			/>
			<input
				class="input input-bordered w-full"
				type="text"
				placeholder="Nach Firmenname filtern..."
				bind:value={filterCompanyName}
				autocomplete="off"
				on:input={onSearchInput}
			/>
			<select class="select select-bordered w-full" bind:value={filterCompanies} on:change={onSearchInput}>
				<option value="all" selected>Alle</option>
				<option value="companies">Nur Firmen</option>
				<option value="individuals">Nur Privatpersonen</option>
			</select>
		</div>
		{/if}
		{#if error}
			<div class="alert alert-error"><span>{error}</span></div>
		{/if}
		{#if loading}
			<div class="text-sm opacity-70">Lade Benutzer…</div>
		{/if}
	</svelte:fragment>

	<div class="overflow-x-auto rounded-2xl border border-base-300">
		<table class="table table-zebra">
			<thead>
				<tr>
					<th class="w-10">
						<label>
							<input
								type="checkbox"
								class="checkbox bg-base-100"
								on:change={toggleSelectAll}
								checked={allSelected}
							/>
						</label>
					</th>
					<th>Name</th>
					<th>Firmeninfo</th>
					<th>Status</th>
					<th>Berechtigungen</th>
					<th>Warenlager (Eurostand)</th>
					<th>Datum(e)</th>
					<th class="text-right">Aktionen</th>
				</tr>
			</thead>

			<tbody>
				{#if !loading && users.length === 0}
					<tr>
						<td colspan="7" class="text-center opacity-70 py-8">Keine Benutzer gefunden.</td>
					</tr>
				{/if}

				{#each users as user (user.id)}
					<tr class={highlightUserId === user.id ? 'bg-base-200/70' : ''}>
						<th>
							<label>
								<input
									type="checkbox"
									class="checkbox bg-base-100"
									on:change={() => toggleUserSelection(user.id)}
									checked={selectedUsers.has(user.id)}
								/>
							</label>
						</th>

						<td>
							<div class="flex items-center gap-3">
								<div class="avatar">
									<div
										class="mask mask-squircle h-12 w-12 items-center flex justify-center bg-base-200 border border-base-300"
									>
										<UserIcon class="opacity-80" />
									</div>
								</div>

								<div>
									{#if user.id === getAuthStore().record.id}
										<div class="font-bold text-accent">{user.name} (Sie)</div>
									{:else}
										<div class="font-bold">{user.name}</div>
									{/if}
									<div class="text-sm opacity-50 font-mono">{user.id}</div>
								</div>
							</div>
						</td>

						<td>
							{user.isCompany ? 'Firma: Geschäftskonto' : 'Privatkonto'}
							<br />
							<span class="badge badge-ghost badge-sm"
								>{user.companyName ? user.companyName : 'N/A'}</span
							>
						</td>

						<td>
							<div class="flex gap-2 text-center align-center">
								{#if user.locked}
									<LockIcon class="text-error" />
								{/if}
								{#if user._hidden}
									<EyeClosed class="text-warning" />
								{/if}
								{#if !user.expand?.data?.registered}
									<UserRoundX class="text-warning" />
								{/if}
								{#if !user.locked && !user._hidden && !user.mfaEnabled && !user.mfaRequired && user.expand?.data?.registered}
									<span class="opacity-50">—</span>
								{/if}
								{#if user.mfaRequired}
									<Fingerprint class="text-success" />
								{:else if user.mfaEnabled}
									<Fingerprint class="text-info" />
								{/if}
							</div>
						</td>

						<td>
							{#if user.expand.data?.financeTrackingAccess == false && user.expand.data?.warehouseAccess == false}
								<span class="badge badge-neutral badge-sm flex flex-row">
									<UserIcon class="w-4 h-4 mr-1" />
									<span>Benutzer</span>
								</span>
							{:else if user.expand.data?.financeTrackingAccess == true && user.expand.data?.warehouseAccess == false}
								<span class="badge badge-primary badge-sm flex flex-row">
									<UserKey class="w-4 h-4 mr-1" />
									<span>Mitarbeiter</span>
								</span>
							{:else if user.expand.data?.warehouseAccess == true && user.expand.data?.financeTrackingAccess == false}
								<span class="badge badge-secondary badge-sm flex flex-row">
									<Forklift class="w-4 h-4 mr-1" />
									<span>Warenlager Mitarbeiter</span>
								</span>
							{:else if user.expand.data?.warehouseAccess == true && user.expand.data?.financeTrackingAccess == true}
								<span class="badge badge-accent badge-sm flex flex-row">
									<UserKey class="w-4 h-4 mr-1" />
									<span>Manager</span>
								</span>
							{/if}
						</td>

						<td>
							<div class="flex flex-row">
								<p>{user.expand.data?.euroSpent}</p><Euro size={20}/><p>/{user.expand.data?.euroAvailable != -1 ? user.expand.data?.euroAvailable : '∞'}</p><Euro size={20}/>
							</div>
						</td>

						<td>
							<div class="flex gap-6">
								<div>
									<p class="text-xs opacity-60">Erstellt:</p>
									<p class="font-medium">{formatDate(user.created)}</p>
									<p class="text-sm opacity-60 mt-1">{formatTime(user.created)}</p>
								</div>
								<div>
									<p class="text-xs opacity-60">Aktualisiert:</p>
									<p class="font-medium">{formatDate(user.updated)}</p>
									<p class="text-sm opacity-60 mt-1">{formatTime(user.updated)}</p>
								</div>
							</div>
						</td>

						<th class="text-right">
							<button
								class="btn btn-ghost btn-xs"
								on:click={() => openEditModal(user.id)}
								title="Bearbeiten"
							>
								<PenIcon class="w-4 h-4" />
							</button>
							<button
								class="btn btn-ghost btn-xs btn-error"
								on:click={() => deleteUser(user.id)}
								title="Löschen"
							>
								<TrashIcon class="w-4 h-4" />
							</button>
						</th>
					</tr>
				{/each}
			</tbody>
		</table>
	</div>
	<div class="flex justify-center mt-4">
		<div class="join">
			<button
				class="join-item btn"
				on:click={() => goToPage(currentPage - 1)}
				disabled={currentPage <= 1}>«</button
			>
			<button class="join-item btn btn-ghost">Seite {currentPage} / {maxPage}</button>
			<button
				class="join-item btn"
				on:click={() => goToPage(currentPage + 1)}
				disabled={currentPage >= maxPage}>»</button
			>
		</div>
	</div>
</AdminPanel>

<!-- Create User Modal -->
<dialog class="modal" bind:this={createModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Benutzer erstellen</h3>

		<!-- svelte-ignore a11y_label_has_associated_control -->
		<label class="label mt-2"><span class="label-text">Name</span></label>
		<input
			type="text"
			class="input input-bordered w-full"
			autocomplete="off"
			placeholder="Benutzername"
			bind:value={createName}
		/>

		<!-- svelte-ignore a11y_label_has_associated_control -->
		<label class="label mt-2"><span class="label-text">E-Mail</span></label>
		<input
			type="text"
			class="input input-bordered w-full"
			autocomplete="off"
			placeholder="leer lassen für automatisch"
			bind:value={createEmail}
		/>

		<!-- svelte-ignore a11y_label_has_associated_control -->
		<label class="label mt-2"><span class="label-text">Passwort</span></label>
		<input
			type="text"
			class="input input-bordered w-full"
			autocomplete="off"
			placeholder="leer lassen für automatisch"
			bind:value={createPassword}
		/>

		<div class="flex gap-2 mt-2">
			<div class="flex items-center gap-2">
				<input
					type="checkbox"
					class="checkbox bg-base-100"
					bind:checked={mfaEnabled}
					id="createMFAEnabled"
				/>
				<label for="createMFAEnabled" class="select-none">MFA aktiviert</label>
			</div>
			<div class="flex items-center gap-2">
				<input
					type="checkbox"
					class="checkbox bg-base-100"
					bind:checked={mfaRequired}
					id="createMFARequired"
				/>
				<label for="createMFARequired" class="select-none">MFA erforderlich</label>
			</div>
		</div>

		<div class="flex justify-between items-center mt-4">
			<span>Ist Firma:</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={createIsCompany} />
		</div>

		{#if createIsCompany}
			<!-- svelte-ignore a11y_label_has_associated_control -->
			<label class="label mt-2"><span class="label-text">Firmenname</span></label>
			<input
				type="text"
				class="input input-bordered w-full"
				autocomplete="off"
				placeholder="Firmenname"
				bind:value={createCompanyName}
			/>
			<p class="label mt-2"><span class="label-text">Euro verfügbar (Warenlager)</span></p>
			<div class="flex flex-row gap-2 items-center">
				<input type="number" class="input input-bordered w-full" bind:value={createEuroAvailable} placeholder="Euro verfügbar" />
				<Euro size={20} />
			</div>
			<p class="label mt-2"><span class="label-text">Euro benutzt (Warenlager)</span></p>
			<div class="flex flex-row gap-2 items-center">
				<input type="number" class="input input-bordered w-full" bind:value={createEuroSpent} placeholder="Euro benutzt" />
				<Euro size={20} />
			</div>
		{/if}

		<div class="flex justify-between items-center mt-4">
			<span>Konto gesperrt:</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={createLocked} />
		</div>

		<div class="flex justify-between items-center mt-4">
			<span>Konto versteckt:</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={createHidden} />
		</div>

		<div class="flex justify-between items-center mt-4">
			<span>Konto registriert:</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={createRegistered} />
		</div>

		<div class="flex flex-col mt-4">
			<span class="mb-1 fieldset-label">Berechtigungen</span>
			<div class="flex gap-2">
				<div class="flex items-center gap-2">
					<input
						type="checkbox"
						class="checkbox bg-base-100"
						bind:checked={createWarehouseAccess}
						id="createWarehouseAccess"
					/>
					<label for="createWarehouseAccess" class="select-none">Warenlager Zugriff</label>
				</div>
				<div class="flex items-center gap-2">
					<input
						type="checkbox"
						class="checkbox bg-base-100"
						bind:checked={createFinanceTrackingAccess}
						id="createFinanceTrackingAccess"
					/>
					<label for="createFinanceTrackingAccess" class="select-none">Finanztracking Zugriff</label>
				</div>
			</div>
		</div>

		<div class="flex flex-col mt-4">
			<!-- svelte-ignore a11y_label_has_associated_control -->
			<label class="label"><span class="label-text">Kontonummer (optional)</span></label>
			<input
				type="text"
				class="input input-bordered w-full"
				autocomplete="off"
				placeholder="z.B. 1234-5678"
				bind:value={createAccountNumber}
			/>
		</div>

		<div class="flex items-center gap-2 mt-4">
			<input
				type="checkbox"
				class="checkbox bg-base-100"
				bind:checked={createRequirePin}
				id="createRequirePin"
			/>
			<label for="createRequirePin" class="select-none">PIN erforderlich</label>
		</div>

		{#if createRequirePin}
			<!-- svelte-ignore a11y_label_has_associated_control -->
			<label class="label mt-2"><span class="label-text">4-stellige PIN</span></label>
			<input
				type="text"
				class="input input-bordered w-full"
				autocomplete="off"
				placeholder="z.B. 1234"
				bind:value={createPin}
			/>
		{/if}

		<div class="modal-action">
			<button class="btn btn-primary" on:click={createUser}>Benutzer erstellen</button>
			<button class="btn btn-error btn-outline" on:click={() => createModal.close()}
				>Abbrechen</button
			>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop"><button>close</button></form>
</dialog>

<!-- Password Show Modal -->
<dialog class="modal" bind:this={showAutoGenPassword}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Benutzer erfolgreich erstellt</h3>

		<p class="py-4">Automatisch erzeugtes Passwort:</p>
		<div class="alert alert-info">
			<div><span class="font-mono">{autoGeneratePassword}</span></div>
		</div>

		<div class="modal-action mt-4">
			<button
				class="btn btn-info btn-outline"
				on:click={() => navigator.clipboard.writeText(autoGeneratePassword)}
			>
				Passwort kopieren
			</button>
			<button class="btn btn-primary btn-outline" on:click={() => showAutoGenPassword.close()}
				>Schließen</button
			>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop"><button>close</button></form>
</dialog>

<!-- Edit Modal -->
<dialog class="modal" bind:this={editModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Benutzer bearbeiten</h3>

		<p class="mt-2 opacity-70 text-sm">ID: <span class="font-mono">{editUserId}</span></p>
		<p class="mb-3 italic opacity-70 text-sm">Feld leer lassen, um aktuellen Wert zu behalten</p>

		<!-- svelte-ignore a11y_label_has_associated_control -->
		<label class="label"><span class="label-text">Name</span></label>
		<input
			type="text"
			class="input input-bordered w-full"
			autocomplete="off"
			placeholder={'Aktuell: ' + (editUser?.name ?? '')}
			bind:value={editName}
		/>

		<!-- svelte-ignore a11y_label_has_associated_control -->
		<label class="label mt-2"><span class="label-text">E-Mail</span></label>
		<input
			type="text"
			class="input input-bordered w-full"
			autocomplete="off"
			placeholder={'Aktuell: ' + (editUser?.email ?? '')}
			bind:value={editEmail}
		/>

		<!-- svelte-ignore a11y_label_has_associated_control -->
		<label class="label mt-2"><span class="label-text">Passwort</span></label>
		<input
			type="text"
			class="input input-bordered w-full"
			autocomplete="off"
			placeholder="(nicht angezeigt)"
			bind:value={editPassword}
		/>

		<div class="flex gap-2 mt-2">
			<div class="flex items-center gap-2">
				<input
					type="checkbox"
					class="checkbox bg-base-100"
					bind:checked={editMFAEnabled}
					id="editMFAEnabled"
				/>
				<label for="editMFAEnabled" class="select-none">MFA aktiviert</label>
			</div>
			<div class="flex items-center gap-2">
				<input
					type="checkbox"
					class="checkbox bg-base-100"
					bind:checked={editMFARequired}
					id="editMFARequired"
				/>
				<label for="editMFARequired" class="select-none">MFA erforderlich</label>
			</div>
		</div>

		<div class="flex justify-between items-center mt-4">
			<span>Ist Firma:</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={editIsCompany} />
		</div>

		{#if editIsCompany}
			<!-- svelte-ignore a11y_label_has_associated_control -->
			<label class="label mt-2"><span class="label-text">Firmenname</span></label>
			<input
				type="text"
				class="input input-bordered w-full"
				autocomplete="off"
				placeholder="Firmenname"
				bind:value={editCompanyName}
			/>
			<p class="label mt-2"><span class="label-text">Euro verfügbar (Warenlager)</span></p>
			<div class="flex flex-row gap-2 items-center">
				<input type="number" class="input input-bordered w-full" bind:value={editEuroAvailable} placeholder="Euro verfügbar" />
				<Euro size={20} />
			</div>
			<p class="label mt-2"><span class="label-text">Euro benutzt (Warenlager)</span></p>
			<div class="flex flex-row gap-2 items-center">
				<input type="number" class="input input-bordered w-full" bind:value={editEuroSpent} placeholder="Euro benutzt" />
				<Euro size={20} />
			</div>
		{/if}

		<div class="flex justify-between items-center mt-4">
			<span>Konto gesperrt:</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={editIsLocked} />
		</div>

		<div class="flex justify-between items-center mt-4">
			<span>Konto versteckt:</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={editHidden} />
		</div>

		<div class="flex justify-between items-center mt-4">
			<span>Konto registriert:</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={editRegistered} />
		</div>

		<div class="flex flex-col mt-4">
			<span class="mb-1 fieldset-label">Berechtigungen</span>
			<div class="flex gap-2">
				<div class="flex items-center gap-2">
					<input
						type="checkbox"
						class="checkbox bg-base-100"
						bind:checked={editWarehouseAccess}
						id="editWarehouseAccess"
					/>
					<label for="editWarehouseAccess" class="select-none">Warenlager Zugriff</label>
				</div>
				<div class="flex items-center gap-2">
					<input
						type="checkbox"
						class="checkbox bg-base-100"
						bind:checked={editFinanceTrackingAccess}
						id="editFinanceTrackingAccess"
					/>
					<label for="editFinanceTrackingAccess" class="select-none">Finanztracking Zugriff</label>
				</div>
			</div>
		</div>

		<div class="modal-action mt-4">
			<button class="btn btn-primary btn-outline" on:click={() => editModal.close()}
				>Schließen</button
			>
			<button class="btn btn-success" on:click={saveEdits}>Änderungen speichern</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop"><button>close</button></form>
</dialog>

<!-- Bulk create modal -->
<dialog class="modal" bind:this={massCreateModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Bulk Create Users</h3>

		<input
			class="input input-bordered w-full mt-4"
			autocomplete="off"
			placeholder="Prefix z.B. LK_ oder AEG_"
			bind:value={massCreatePrefix}
		/>
		<input
			class="input input-bordered w-full mt-4"
			autocomplete="off"
			placeholder="Number of Users to Create"
			type="number"
			bind:value={massCreateCount}
		/>
		<input
			class="input input-bordered w-full mt-4"
			autocomplete="off"
			placeholder="Start Number / Offset"
			type="number"
			bind:value={massCreateStartNumber}
		/>
		<input
			class="input input-bordered w-full mt-4"
			autocomplete="off"
			placeholder="Password (leer = auto)"
			bind:value={massCreatePassword}
		/>

		<div class="flex justify-between items-center mt-4">
			<span>Firmenkonten:</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={massCreateIsCompany} />
		</div>

		{#if massCreateIsCompany}
			<p class="label mt-2"><span class="label-text">Euro verfügbar (Warenlager)</span></p>
			<div class="flex flex-row gap-2 items-center">
				<input type="number" class="input input-bordered w-full" bind:value={massCreateEuroAvailable} placeholder="Euro verfügbar" />
				<Euro size={20} />
			</div>
			<p class="label mt-2"><span class="label-text">Euro benutzt (Warenlager)</span></p>
			<div class="flex flex-row gap-2 items-center">
				<input type="number" class="input input-bordered w-full" bind:value={massCreateEuroSpent} placeholder="Euro benutzt" />
				<Euro size={20} />
			</div>
		{/if}

		<div class="flex justify-between items-center mt-4">
			<span>Konten gesperrt:</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={massCreateIsLocked} />
		</div>

		<div class="flex justify-between items-center mt-4">
			<span>Konten versteckt:</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={massCreateHidden} />
		</div>

		<div class="flex justify-between items-center mt-4">
			<span>Konten registriert:</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={massCreateRegistered} />
		</div>

		<div class="flex flex-col mt-4">
			<span class="mb-1 fieldset-label">Berechtigungen</span>
			<div class="flex gap-2">
				<div class="flex items-center gap-2">
					<input
						type="checkbox"
						class="checkbox bg-base-100"
						bind:checked={massCreateWarehouseAccess}
						id="massCreateWarehouseAccess"
					/>
					<label for="massCreateWarehouseAccess" class="select-none">Warenlager Zugriff</label>
				</div>
				<div class="flex items-center gap-2">
					<input
						type="checkbox"
						class="checkbox bg-base-100"
						bind:checked={massCreateFinanceTrackingAccess}
						id="massCreateFinanceTrackingAccess"
					/>
					<label for="massCreateFinanceTrackingAccess" class="select-none">Finanztracking Zugriff</label>
				</div>
			</div>
		</div>

		<p class="mt-4 opacity-70">CSV Datei Name:</p>
		<input
			class="input input-bordered w-full mt-2"
			placeholder="bulk_created_users.csv"
			autocomplete="off"
			bind:value={massCreateSaveFileName}
		/>

		<div class="flex items-center gap-2 mt-4">
			<input
				type="checkbox"
				class="checkbox bg-base-100"
				bind:checked={massCreateGeneratePin}
				id="massCreateGeneratePin"
			/>
			<label for="massCreateGeneratePin" class="select-none">Karten-PINs generieren</label>
		</div>

		<div class="flex items-center gap-2 mt-2">
			<input
				type="checkbox"
				class="checkbox bg-base-100"
				bind:checked={massCreatePrintBackSide}
				id="massCreatePrintBackSide"
			/>
			<label for="massCreatePrintBackSide" class="select-none">Kartenrückseite generieren</label>
		</div>

		<div class="modal-action">
			<button class="btn btn-primary btn-outline" on:click={() => massCreateModal.close()}
				>Close</button
			>
			<button class="btn btn-success" on:click={bulkCreateUsers} disabled={massCreateRunning}
				>Start</button
			>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop"><button>close</button></form>
</dialog>

<!-- Bulk Create Report Modal -->
<dialog class="modal" bind:this={massCreateReportModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Bulk Create Users Report</h3>
		<p class="py-4 text-sm opacity-70">Report ist bereit. CSV downloaden:</p>

		<a class="btn btn-primary" href={massCreateReportLink} download={massCreateSaveFileName}
			>Download CSV</a
		>

		<p class="text-sm opacity-70 mt-2">
			Karten sind ebenfalls generiert und können hier als PDF heruntergeladen werden:
		</p>

		<button class="btn btn-secondary" on:click={bulkCreatePDF.save('cards.pdf')}
			>Download Karten PDF</button
		>

		<div class="modal-action mt-4">
			<button class="btn btn-primary btn-outline" on:click={() => massCreateReportModal.close()}
				>Close</button
			>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop"><button>close</button></form>
</dialog>

<!-- Mass Edit Modal -->
<dialog class="modal" bind:this={massEditModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Nutzer massenhaft bearbeiten</h3>

		<input
			class="input input-bordered w-full mt-4"
			placeholder="Neues Passwort (leer = überspringen)"
			bind:value={massEditPassword}
		/>

		<div class="flex gap-2 mt-2">
			<div class="flex w-full items-center gap-2 justify-between">
				<label for="massEditMFAEnabled" class="select-none">MFA aktiviert aktualisieren</label>
				<input
					type="checkbox"
					class="checkbox bg-base-100"
					bind:checked={massEditUpdateMFAEnabled}
					id="massEditMFAEnabled"
				/>
			</div>
		</div>
		{#if massEditUpdateMFAEnabled}
			<div class="flex items-center gap-2 mt-2 justify-between">
				<label for="massEditMFAEnabledValue" class="select-none">MFA aktiviert setzen</label>
				<input
					type="checkbox"
					class="checkbox bg-base-100"
					bind:checked={massEditMFAEnabled}
					id="massEditMFAEnabledValue"
				/>
			</div>
		{/if}
		<div class="flex gap-2 mt-2">
			<div class="flex w-full items-center gap-2 justify-between">
				<label for="massEditMFARequired" class="select-none">MFA erforderlich aktualisieren</label>
				<input
					type="checkbox"
					class="checkbox bg-base-100"
					bind:checked={massEditUpdateMFARequired}
					id="massEditMFARequired"
				/>
			</div>
		</div>
		{#if massEditUpdateMFARequired}
			<div class="flex items-center gap-2 mt-2 justify-between">
				<label for="massEditMFARequiredValue" class="select-none">MFA erforderlich setzen</label>
				<input
					type="checkbox"
					class="checkbox bg-base-100"
					bind:checked={massEditMFARequired}
					id="massEditMFARequiredValue"
				/>
			</div>
		{/if}

		<div class="flex justify-between items-center mt-4">
			<span>Firma aktualisieren:</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={massEditUpdateCompany} />
		</div>

		{#if massEditUpdateCompany}
			<div class="flex justify-between items-center mt-3">
				<span>Als Firma setzten:</span>
				<input type="checkbox" class="checkbox bg-base-100" bind:checked={massEditIsCompany} />
			</div>
		{/if}

		<div class="flex justify-between items-center mt-4">
			<span>Euro verfügbar aktualisieren:</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={massEditUpdateEuroAvailable} />
		</div>

		{#if massEditUpdateEuroAvailable}
			<div class="flex flex-col mt-3">
				<span>Euro verfügbar setzen:</span>
				<div class="flex flex-row items-center">
					<input type="number" class="input bg-base-100 w-full" bind:value={massEditEuroAvailable} />
					<Euro size={20} class="ml-1" />
				</div>
			</div>
		{/if}

		<div class="flex justify-between items-center mt-4">
			<span>Euro verwendet aktualisieren:</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={massEditUpdateEuroSpent} />
		</div>


		{#if massEditUpdateEuroSpent}
			<div class="flex flex-col mt-3">
				<span>Euro verwendet setzen:</span>
				<div class="flex flex-row items-center">
					<input type="number" class="input bg-base-100 w-full" bind:value={massEditEuroSpent} />
					<Euro size={20} class="ml-1" />
				</div>
			</div>
		{/if}

		<div class="flex justify-between items-center mt-4">
			<span>Gesperrt aktualisieren:</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={massEditUpdateLocked} />
		</div>

		{#if massEditUpdateLocked}
			<div class="flex justify-between items-center mt-3">
				<span>Als gesperrt setzen:</span>
				<input type="checkbox" class="checkbox bg-base-100" bind:checked={massEditIsLocked} />
			</div>
		{/if}

		<div class="flex justify-between items-center mt-4">
			<span>Versteckt aktualisieren:</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={massEditUpdateHidden} />
		</div>

		{#if massEditUpdateHidden}
			<div class="flex justify-between items-center mt-3">
				<span>Als versteckt setzen:</span>
				<input type="checkbox" class="checkbox bg-base-100" bind:checked={massEditHidden} />
			</div>
		{/if}

		<div class="flex justify-between items-center mt-4">
			<span>Registriert akutalisieren:</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={massEditUpdateRegistered} />
		</div>

		{#if massEditUpdateRegistered}
			<div class="flex justify-between items-center mt-3">
				<span>Als registriert setzen:</span>
				<input type="checkbox" class="checkbox bg-base-100" bind:checked={massEditRegistered} />
			</div>
		{/if}

		<div class="flex justify-between items-center mt-4">
			<span>Warenlager Zugriff aktualisieren:</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={massEditUpdateWarehouseAccess} />
		</div>

		{#if massEditUpdateWarehouseAccess}
			<div class="flex justify-between items-center mt-3">
				<span>Warenlager Zugriff:</span>
				<input type="checkbox" class="checkbox bg-base-100" bind:checked={massEditWarehouseAccess} />
			</div>
		{/if}

		<div class="flex justify-between items-center mt-4">
			<span>Finanztracking Zugriff aktualisieren:</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={massEditUpdateFinanceTrackingAccess} />
		</div>

		{#if massEditUpdateFinanceTrackingAccess}
			<div class="flex justify-between items-center mt-3">
				<span>Finanztracking Zugriff:</span>
				<input type="checkbox" class="checkbox bg-base-100" bind:checked={massEditFinanceTrackingAccess} />
			</div>
		{/if}

		<div class="modal-action mt-4">
			<button class="btn btn-primary btn-outline" on:click={() => massEditModal.close()}
				>Schließen</button
			>
			<button class="btn btn-success" on:click={massUpdate}>Anwenden</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop"><button>close</button></form>
</dialog>

<!-- Mass delete Modal -->
<dialog class="modal" bind:this={massDeleteModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Nutzer massenhaft löschen</h3>
		<p class="py-4">Sicher? Das kann nicht rückgängig gemacht werden.</p>

		<div class="max-h-44 overflow-auto rounded-xl border border-base-300 p-3">
			{#each Array.from(selectedUsers) as userId}
				<p class="text-sm text-error">- {userId}</p>
			{/each}
		</div>

		<div class="modal-action">
			<button class="btn btn-primary btn-outline" on:click={() => massDeleteModal.close()}
				>Abbrechen</button
			>
			<button class="btn btn-error" on:click={massDeleteUsers}>Ausgewählte Nutzer löschen</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop"><button>close</button></form>
</dialog>

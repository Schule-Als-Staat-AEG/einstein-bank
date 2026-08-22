<script lang="ts">
	import db, { server } from '$lib/connections';
	import AdminPanel from '$modules/AdminPanel.svelte';
	import Currency from '$modules/Currency.svelte';
	import AccountSelectModal from '$modules/modals/AccountSelectModal.svelte';
	import {
		ArrowLeftRightIcon,
		ClipboardTypeIcon,
		HandCoinsIcon,
		NotebookPenIcon,
		PenIcon,
		TrashIcon,
		SearchIcon,
		DownloadIcon,
		PlusIcon,
		XIcon,
		Settings2Icon,
		UsersIcon,
		BanknoteX
	} from '@lucide/svelte';
	import { marked } from 'marked';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';

	// ---------- List / paging ----------
	let filter = '';
	let perPage = 30;
	let page = 1;
	let maxPage = 1;
	let totalItems = 0;

	// ---------- Advanced search ----------
	let advancedSearchOpen = false;

	let filterID = '';
	let filterSenderID = '';
	let filterReceiverID = '';
	let filterDescription = '';
	let filterMinAmount: number | '' = '';
	let filterMaxAmount: number | '' = '';
	let filterSenderName = '';
	let filterReceiverName = '';
	let filterSystem = '';
	let filterStatus = '';

	// IMPORTANT: datetime-local binds to string, not Date
	let filterMinDate = ''; // "2026-01-16T12:30"
	let filterMaxDate = '';

	// ---------- Data ----------
	let transactions: any[] = [];
	let adminGroups: any[] = [];
	let selectedTransactions: Set<string> = new Set();

	// ---------- Modals ----------
	let newModal: HTMLDialogElement;
	let editModal: HTMLDialogElement;
	let massEditModal: HTMLDialogElement;
	let massDeleteModal: HTMLDialogElement;
	let csvExportModal: HTMLDialogElement;
	let refundModal: HTMLDialogElement;
	let accountSelectModal: AccountSelectModal;

	// ---------- Create ----------
	let createTransactionTab = 0; // 0=Transfer+Log, 1=Transfer only, 2=Log only
	let createTransactionGroupMode: 'single' | 'sender_group' | 'receiver_group' = 'single';
	let createTransactionSenderID = '';
	let createTransactionReceiverID = '';
	let createTransactionSenderGroupID = '';
	let createTransactionReceiverGroupID = '';
	let createTransactionAmount = 0;
	let createTransactionTax = false;
	let createTransactionIncomeTax = false;
	let createTransactionVAT = false;
	let createTransactionTransactionTax = false;
	let createTransactionDescription = '';
	let createTransactionSystem = '';
	let createTransactionStatus = '';

	// ---------- Edit ----------
	let editTransactionID = '';
	let editTransactionSenderID = '';
	let editTransactionReceiverID = '';
	let editTransactionAmount = 0;
	let editTransactionDescription = '';
	let editTransactionSignature = '';
	let editTransactionSystem = '';
	let editTransactionTimestamp = '';
	let editTransactionStatus = '';

	// ---------- Mass Edit ----------
	let massEditUpdateTransactionSenderID = false;
	let massEditTransactionSenderID = '';
	let massEditUpdateTransactionReceiverID = false;
	let massEditTransactionReceiverID = '';
	let massEditUpdateTransactionAmount = false;
	let massEditTransactionAmount = 0;

	let massEditUpdateTransactionDescription = false;
	let massEditTransactionDescription = '';

	let massEditUpdateTransactionSystem = false;
	let massEditTransactionSystem = '';

	let massEditUpdateTransactionStatus = false;
	let massEditTransactionStatus = '';

	// ---------- Refund ----------
	let refundTransaction: any = null;
	let refundMethod: 'erase' | 'new' = 'new'; // "erase" = Original Transaktion löschen und undo machen, new = Neue Refund Transaktion erstellen
	let refundDescription = ''; // Nur bei new

	// ---------- Account Select ----------
	let pendingAccountSelectTarget = '';

	// ---------- Export ----------
	let csvLink = '';

	// ---------- UX helpers ----------
	let isLoading = false;
	let lastErr = '';
	let debounceTimer: any = null;

	onMount(async () => {
		if (!browser) return;
		await Promise.all([fetchTransactions(), fetchAdminGroups()]);
	});

	async function fetchAdminGroups() {
		try {
			const records = await db.collection('admin_groups').getList(1, 200, {
				sort: '-created',
				expand: 'users'
			});

			adminGroups = records.items;
		} catch (e: any) {
			console.error(e);
			lastErr = 'Gruppen konnten nicht geladen werden.';
		}
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

	async function toPlainText(markdown: string) {
		const html = await marked.parse(markdown || '');
		const withoutTags = html.replace(/<[^>]+>/g, ' ');
		return withoutTags.replace(/\s+/g, ' ').trim();
	}

	function escPB(str: string) {
		// PocketBase filter strings use quotes; escape safely
		return String(str ?? '')
			.replaceAll('\\', '\\\\')
			.replaceAll('"', '\\"')
			.trim();
	}

	function scheduleFetchTransactions(delay = 250) {
		clearTimeout(debounceTimer);
		debounceTimer = setTimeout(() => {
			page = 1;
			fetchTransactions();
		}, delay);
	}

	function resetCreateTransactionState() {
		createTransactionTab = 0;
		createTransactionGroupMode = 'single';
		createTransactionSenderID = '';
		createTransactionReceiverID = '';
		createTransactionSenderGroupID = '';
		createTransactionReceiverGroupID = '';
		createTransactionAmount = 0;
		createTransactionTax = false;
		createTransactionIncomeTax = false;
		createTransactionVAT = false;
		createTransactionTransactionTax = false;
		createTransactionDescription = '';
		createTransactionSystem = '';
		createTransactionStatus = '';
	}

	function getSelectedCreateGroup() {
		const groupID =
			createTransactionGroupMode === 'sender_group'
				? createTransactionSenderGroupID
				: createTransactionReceiverGroupID;

		return adminGroups.find((group) => group.id === groupID) ?? null;
	}

	async function resolveAccountIDFromUser(user: any) {
		const account = await db.collection('accounts').getFirstListItem(`user.id="${user.id}"`, {
			requestKey: null
		});

		if (!account?.id) {
			throw new Error(`Kein Konto für Benutzer ${user?.name ?? user?.id ?? 'unbekannt'} gefunden.`);
		}

		return account.id;
	}

	async function buildCreateTargets() {
		if (createTransactionGroupMode === 'single') {
			if (!createTransactionSenderID || !createTransactionReceiverID) {
				throw new Error('Bitte Sender und Empfänger auswählen.');
			}

			return [{ sender: createTransactionSenderID, receiver: createTransactionReceiverID }];
		}

		const group = getSelectedCreateGroup();
		if (!group) {
			throw new Error('Bitte eine Gruppe auswählen.');
		}

		const users = group.expand?.users ?? [];
		if (users.length === 0) {
			throw new Error('Die ausgewählte Gruppe enthält keine Benutzer.');
		}

		const accountIDs = await Promise.all(users.map((user: any) => resolveAccountIDFromUser(user)));
		const uniqueAccountIDs = [...new Set(accountIDs)];

		if (createTransactionGroupMode === 'sender_group') {
			if (!createTransactionReceiverID) {
				throw new Error('Bitte einen Empfänger auswählen.');
			}

			return uniqueAccountIDs.map((sender) => ({ sender, receiver: createTransactionReceiverID }));
		}

		if (!createTransactionSenderID) {
			throw new Error('Bitte einen Sender auswählen.');
		}

		return uniqueAccountIDs.map((receiver) => ({ sender: createTransactionSenderID, receiver }));
	}

	function buildFilterString() {
		if (advancedSearchOpen) {
			const filters: string[] = [];

			if (filterID) filters.push(`id ~ "${escPB(filterID)}"`);
			if (filterSenderID) filters.push(`sender.id ~ "${escPB(filterSenderID)}"`);
			if (filterReceiverID) filters.push(`receiver.id ~ "${escPB(filterReceiverID)}"`);
			if (filterDescription) filters.push(`description ~ "${escPB(filterDescription)}"`);

			if (filterMinAmount !== '') filters.push(`amount >= ${Number(filterMinAmount)}`);
			if (filterMaxAmount !== '') filters.push(`amount <= ${Number(filterMaxAmount)}`);

			if (filterSenderName) filters.push(`sender.user.name ~ "${escPB(filterSenderName)}"`);
			if (filterReceiverName) filters.push(`receiver.user.name ~ "${escPB(filterReceiverName)}"`);

			if (filterMinDate) filters.push(`created >= "${new Date(filterMinDate).toISOString()}"`);
			if (filterMaxDate) filters.push(`created <= "${new Date(filterMaxDate).toISOString()}"`);

			if (filterSystem) filters.push(`system ~ "${escPB(filterSystem)}"`);
			if (filterStatus) filters.push(`status ~ "${escPB(filterStatus)}"`);

			return filters.length ? filters.join(' && ') : '';
		}

		const q = escPB(filter);
		if (!q) return '';
		return [
			`id ~ "${q}"`,
			`sender ~ "${q}"`,
			`receiver ~ "${q}"`,
			`description ~ "${q}"`,
			`sender.user.name ~ "${q}"`,
			`receiver.user.name ~ "${q}"`,
			`sender.user.id ~ "${q}"`,
			`receiver.user.id ~ "${q}"`
		].join(' || ');
	}

	async function fetchTransactions() {
		try {
			isLoading = true;
			lastErr = '';

			const filterString = buildFilterString();
			const records = await db.collection('transactions').getList(page, perPage, {
				sort: '-created',
				filter: filterString,
				expand: 'sender,receiver,sender.user,receiver.user'
			});

			await Promise.all(
				records.items.map(async (item: any) => {
					item.plainDescription = await toPlainText(item.description);
				})
			);

			transactions = records.items;
			maxPage = records.totalPages;
			totalItems = records.totalItems;

			// keep selection only for visible rows
			const visible = new Set(transactions.map((t) => t.id));
			selectedTransactions = new Set([...selectedTransactions].filter((id) => visible.has(id)));
		} catch (e: any) {
			console.error(e);
			lastErr = 'Transaktionen konnten nicht geladen werden.';
		} finally {
			isLoading = false;
		}
	}

	function pageUp() {
		if (page >= maxPage) return;
		page += 1;
		fetchTransactions();
	}
	function pageDown() {
		if (page <= 1) return;
		page -= 1;
		fetchTransactions();
	}

	function toggleTransactionSelection(transactionID: string) {
		if (selectedTransactions.has(transactionID)) selectedTransactions.delete(transactionID);
		else selectedTransactions.add(transactionID);
		selectedTransactions = new Set(selectedTransactions);
	}
	function toggleAllTransactionSelection() {
		if (transactions.length === 0) return;
		const allSelected = selectedTransactions.size === transactions.length;
		selectedTransactions = allSelected ? new Set() : new Set(transactions.map((t) => t.id));
	}

	function resetAdvanced() {
		filterID = '';
		filterSenderID = '';
		filterReceiverID = '';
		filterDescription = '';
		filterMinAmount = '';
		filterMaxAmount = '';
		filterSenderName = '';
		filterReceiverName = '';
		filterMinDate = '';
		filterMaxDate = '';
		filterSystem = '';
		filterStatus = '';
		scheduleFetchTransactions(0);
	}

	function selectAccount(account: any) {
		const id = account?.id ?? '';

		if (pendingAccountSelectTarget === 'create_sender') createTransactionSenderID = id;
		else if (pendingAccountSelectTarget === 'create_receiver') createTransactionReceiverID = id;
		else if (pendingAccountSelectTarget === 'edit_sender') editTransactionSenderID = id;
		else if (pendingAccountSelectTarget === 'edit_receiver') editTransactionReceiverID = id;
		else if (pendingAccountSelectTarget === 'mass_edit_sender') massEditTransactionSenderID = id;
		else if (pendingAccountSelectTarget === 'mass_edit_receiver')
			massEditTransactionReceiverID = id;

		pendingAccountSelectTarget = '';
	}

	// ---------- Modals ----------
	function openNewModal() {
		resetCreateTransactionState();
		newModal.showModal();
	}
	function openEditModal(transaction: any) {
		editTransactionID = transaction.id;
		editTransactionSenderID = transaction.sender;
		editTransactionReceiverID = transaction.receiver;
		editTransactionAmount = transaction.amount;
		editTransactionDescription = transaction.plainDescription;
		editTransactionSignature = transaction.signature;
		editTransactionSystem = transaction.system;
		editTransactionTimestamp = transaction.created;
		editTransactionStatus = transaction.status;
		editModal.showModal();
	}
	function openMassEditModal() {
		massEditModal.showModal();
	}
	function openMassDeleteModal() {
		massDeleteModal.showModal();
	}
	function openCSVExportModal() {
		exportCSV();
		csvExportModal.showModal();
	}
	function openAccountSelectModal(target: string) {
		pendingAccountSelectTarget = target;
		accountSelectModal.show();
	}

	// ---------- Database logic ----------
	async function saveEditTransaction() {
		const rsp: any = await fetch(`${server}/api/admin/generate-signature`, {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
				Authorization: `Bearer ${db.authStore.token}`
			},
			body: JSON.stringify({
				sender: editTransactionSenderID,
				receiver: editTransactionReceiverID,
				amount: editTransactionAmount,
				received: editTransactionAmount,
				description: editTransactionDescription,
				system: editTransactionSystem,
				timestamp: editTransactionTimestamp
			})
		}).catch(() => null);

		if (!rsp?.ok) {
			alert('Fehler beim Generieren der Signatur.');
			return;
		}
		const data = await rsp.json();

		await db
			.collection('transactions')
			.update(editTransactionID, {
				sender: editTransactionSenderID,
				receiver: editTransactionReceiverID,
				amount: editTransactionAmount,
				received: editTransactionAmount,
				description: editTransactionDescription,
				signature: data.signature,
				system: editTransactionSystem,
				status: editTransactionStatus
			})
			.then(() => fetchTransactions());

		editModal.close();
	}

	async function bulkUpdate() {
		await Promise.all(
			Array.from(selectedTransactions).map(async (id) => {
				const tx = transactions.find((t) => t.id === id);
				if (!tx) return;

				let currentAmount = tx.amount;

				const updateData: any = {};

				// apply edits
				if (massEditUpdateTransactionSenderID) updateData.sender = massEditTransactionSenderID;
				if (massEditUpdateTransactionReceiverID)
					updateData.receiver = massEditTransactionReceiverID;

				if (massEditUpdateTransactionAmount) {
					updateData.amount = massEditTransactionAmount;
					currentAmount = massEditTransactionAmount;
				}

				if (massEditUpdateTransactionDescription)
					updateData.description = massEditTransactionDescription;
				if (massEditUpdateTransactionSystem) updateData.system = massEditTransactionSystem;
				if (massEditUpdateTransactionStatus) updateData.status = massEditTransactionStatus;

				// signature needs the final full payload
				const newData: any = {
					sender: massEditUpdateTransactionSenderID ? massEditTransactionSenderID : tx.sender,
					receiver: massEditUpdateTransactionReceiverID
						? massEditTransactionReceiverID
						: tx.receiver,
					amount: massEditUpdateTransactionAmount ? massEditTransactionAmount : tx.amount,
					received: massEditUpdateTransactionAmount ? massEditTransactionAmount : tx.amount,
					description: massEditUpdateTransactionDescription
						? massEditTransactionDescription
						: tx.plainDescription,
					system: massEditUpdateTransactionSystem ? massEditUpdateTransactionSystem : tx.system,
					timestamp: tx.created // Timestamp should not be editable, use created time for signature to prevent forgery with custom timestamps
				};

				const rsp = await fetch(`${server}/api/admin/generate-signature`, {
					method: 'POST',
					headers: {
						'Content-Type': 'application/json',
						Authorization: `Bearer ${db.authStore.token}`
					},
					body: JSON.stringify(newData)
				});

				if (!rsp.ok) {
					alert('Fehler beim Generieren der Signatur für Transaktion ID: ' + id);
					return;
				}
				const data = await rsp.json();
				updateData.signature = data.signature;

				await db.collection('transactions').update(id, updateData, { requestKey: null });
			})
		);

		await fetchTransactions();
		selectedTransactions = new Set();
		massEditModal.close();

		// reset
		massEditUpdateTransactionSenderID = false;
		massEditTransactionSenderID = '';
		massEditUpdateTransactionReceiverID = false;
		massEditTransactionReceiverID = '';
		massEditUpdateTransactionAmount = false;
		massEditTransactionAmount = 0;
		massEditUpdateTransactionDescription = false;
		massEditTransactionDescription = '';
		massEditUpdateTransactionSystem = false;
		massEditTransactionSystem = '';
		massEditUpdateTransactionStatus = false;
		massEditTransactionStatus = '';
	}

	function deleteTransaction(transactionID: string) {
		if (
			!confirm('Sind Sie sicher? Das kann nicht rückgängig gemacht werden.\nID: ' + transactionID)
		)
			return;
		db.collection('transactions')
			.delete(transactionID)
			.then(() => fetchTransactions());
	}

	async function createTransaction() {
		try {
			const targets = await buildCreateTargets();

			for (const target of targets) {
				let transactionData: any = {};
				if (createTransactionTab == 0) {
					transactionData.moveMoney = true;
					transactionData.logTransaction = true;
				} else if (createTransactionTab == 1) {
					transactionData.moveMoney = true;
					transactionData.logTransaction = false;
				} else if (createTransactionTab == 2) {
					transactionData.moveMoney = false;
					transactionData.logTransaction = true;
				}

				transactionData.sender = target.sender;
				transactionData.receiver = target.receiver;
				transactionData.amount = createTransactionAmount;
				transactionData.description = createTransactionDescription;
				transactionData.system = createTransactionSystem;
				transactionData.status = createTransactionStatus;
				transactionData.received = createTransactionAmount;
				transactionData.vat = createTransactionVAT;
				transactionData.incomeTax = createTransactionIncomeTax;
				transactionData.transactionTax = createTransactionTransactionTax;
				transactionData.applyTaxes = createTransactionTax;

				const rsp = await fetch(`${server}/api/admin/transaction`, {
					method: 'POST',
					headers: {
						'Content-Type': 'application/json',
						Authorization: `Bearer ${db.authStore.token}`
					},
					body: JSON.stringify(transactionData)
				});

				if (!rsp.ok) throw new Error('bad rsp');
			}

			alert(
				targets.length === 1
					? 'Transaktion erfolgreich erstellt.'
					: `${targets.length} Transaktionen erfolgreich erstellt.`
			);
			newModal.close();
			await fetchTransactions();
		} catch (error) {
			alert(error instanceof Error ? error.message : 'Fehler beim Erstellen der Transaktion.');
		}
	}

	function massDelete() {
		selectedTransactions.forEach((id) => {
			db.collection('transactions').delete(id, { requestKey: null });
		});
		selectedTransactions = new Set();
		fetchTransactions();
		massDeleteModal.close();
	}

	function exportCSV() {
		let csvContent = 'data:text/csv;charset=utf-8,';
		let csvHeader =
			'ID;Sender;Empfänger;Beschreibung;System;Status;Betrag;Erhalten;Signatur;Erstellt\n';
		csvContent += csvHeader;

		transactions.forEach((tx) => {
			let row = `${tx.id};${tx.sender};${tx.receiver};"${String(tx.plainDescription).replaceAll('"', '""')}";${tx.system};${tx.status};${tx.amount};${tx.received};${tx.signature};${tx.created}\n`;
			csvContent += row;
		});

		csvLink = encodeURI(csvContent);
	}
	function openRefundModal(transaction: any) {
		refundTransaction = transaction;
		refundDescription = '';
		refundMethod = 'new';
		refundModal.showModal();
	}
	function submitRefund() {
		fetch(server + '/api/admin/refund-transaction', {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
				Authorization: `Bearer ${db.authStore.token}`
			},
			body: JSON.stringify({
				transaction: refundTransaction.id,
				method: refundMethod,
				description: refundDescription
			})
		})
			.then((rsp) => {
				if (!rsp.ok) throw new Error('bad rsp');
				alert('Rückerstattung erfolgreich.');
				refundModal.close();
				fetchTransactions();
			})
			.catch(() => {
				alert('Fehler bei der Rückerstattung.');
			});
	}
</script>

<title>Admin - Transaktionen</title>
<AdminPanel
	title="Transaktionen"
	description="Verwaltung aller Transaktionen"
	{totalItems}
	selectedItems={selectedTransactions.size}
		on:massCreate={() => openNewModal()}
	on:massEdit={() => openMassEditModal()}
	on:massDelete={() => openMassDeleteModal()}
	on:export={() => openCSVExportModal()}
	on:create={() => openNewModal()}
	showMassEditButton={true}
	showMassDeleteButton={true}
	showCSVExportButton={true}
	showCreateButton={true}
	showMassCreateButton={true}
	massDeleteButtonText="Löschen"
	massEditButtonText="Bearbeiten"
	createButtonText="Neue Transaktion"
>
	<!-- Search Bar -->
	<svelte:fragment slot="filter">
		<div class="card bg-base-100 border border-base-300 shadow-sm mb-4">
			<div class="card-body p-4">
				<div class="flex flex-col gap-3 md:flex-row md:items-center">
					<label class="input input-bordered flex items-center gap-2 w-full">
						<SearchIcon class="w-4 h-4 opacity-70" />
						<input
							class="grow"
							type="text"
							placeholder="Suchen nach ID, Sender, Empfänger, Name, Beschreibung…"
							bind:value={filter}
							autocomplete="off"
							disabled={advancedSearchOpen}
							on:input={() => scheduleFetchTransactions()}
						/>
						{#if filter && !advancedSearchOpen}
							<button
								class="btn btn-ghost btn-xs"
								on:click={() => ((filter = ''), scheduleFetchTransactions(0))}
							>
								<XIcon class="w-4 h-4" />
							</button>
						{/if}
					</label>

					<div class="flex gap-2">
						<button
							class="btn btn-outline"
							on:click={() => (
								(advancedSearchOpen = !advancedSearchOpen),
								scheduleFetchTransactions(0)
							)}
						>
							<Settings2Icon class="w-4 h-4" />
							Erweiterte Suche
						</button>

						{#if advancedSearchOpen}
							<button class="btn btn-ghost" on:click={resetAdvanced}>
								<XIcon class="w-4 h-4" /> Zurücksetzen
							</button>
						{/if}
					</div>

					<select
						class="select select-bordered max-w-fit ml-4"
						bind:value={perPage}
						on:change={() => scheduleFetchTransactions(0)}
					>
						<option value="15">15 / Seite</option>
						<option value="30">30 / Seite</option>
						<option value="50">50 / Seite</option>
						<option value="100">100 / Seite</option>
						<option value="200">200 / Seite</option>
						<option value="500">500 / Seite</option>
						<option value="1000">1000 / Seite</option>
						<option value="2000">2000 / Seite</option>
						<option value="5000">5000 / Seite</option>
						<option value="10000">10000 / Seite</option>
						<option value="20000">20000 / Seite</option>
					</select>
				</div>

				{#if advancedSearchOpen}
					<div class="divider my-3"></div>

					<div class="grid grid-cols-1 md:grid-cols-3 gap-2">
						<input
							class="input input-bordered w-full"
							type="text"
							autocomplete="off"
							placeholder="Transaktions-ID"
							bind:value={filterID}
							on:input={() => scheduleFetchTransactions()}
						/>
						<input
							class="input input-bordered w-full"
							type="text"
							autocomplete="off"
							placeholder="Sender-ID (Konto)"
							bind:value={filterSenderID}
							on:input={() => scheduleFetchTransactions()}
						/>
						<input
							class="input input-bordered w-full"
							type="text"
							autocomplete="off"
							placeholder="Empfänger-ID (Konto)"
							bind:value={filterReceiverID}
							on:input={() => scheduleFetchTransactions()}
						/>

						<input
							class="input input-bordered w-full md:col-span-2"
							type="text"
							autocomplete="off"
							placeholder="Beschreibung"
							bind:value={filterDescription}
							on:input={() => scheduleFetchTransactions()}
						/>
						<div class="join w-full">
							<input
								class="input input-bordered join-item w-1/2"
								type="number"
								autocomplete="off"
								placeholder="Min Betrag"
								bind:value={filterMinAmount}
								on:input={() => scheduleFetchTransactions()}
							/>
							<input
								class="input input-bordered join-item w-1/2"
								type="number"
								autocomplete="off"
								placeholder="Max Betrag"
								bind:value={filterMaxAmount}
								on:input={() => scheduleFetchTransactions()}
							/>
						</div>

						<input
							class="input input-bordered w-full"
							type="text"
							autocomplete="off"
							placeholder="Sender Name"
							bind:value={filterSenderName}
							on:input={() => scheduleFetchTransactions()}
						/>
						<input
							class="input input-bordered w-full"
							type="text"
							autocomplete="off"
							placeholder="Empfänger Name"
							bind:value={filterReceiverName}
							on:input={() => scheduleFetchTransactions()}
						/>

						<div class="join w-full md:col-span-3">
							<input
								class="input input-bordered join-item w-1/2"
								type="datetime-local"
								autocomplete="off"
								bind:value={filterMinDate}
								on:input={() => scheduleFetchTransactions()}
							/>
							<input
								class="input input-bordered join-item w-1/2"
								type="datetime-local"
								autocomplete="off"
								bind:value={filterMaxDate}
								on:input={() => scheduleFetchTransactions()}
							/>
						</div>
						<div class="join w-full">
							<input
								class="input input-bordered join-item w-1/2"
								type="text"
								autocomplete="off"
								placeholder="System"
								bind:value={filterSystem}
								on:input={() => scheduleFetchTransactions()}
							/>
							<!-- Selection for Status: "flagged, pending, approved"-->
							<select
								class="select select-bordered join-item w-1/2 bg-base-100"
								bind:value={filterStatus}
								on:change={() => scheduleFetchTransactions()}
							>
								<option value="">Status (alle)</option>
								<option value="under_audit">Under Audit</option>
								<option value="flagged">Flagged</option>
								<option value="pending">Pending</option>
								<option value="approved">Approved</option>
							</select>
						</div>
					</div>
				{/if}
			</div>
		</div>
	</svelte:fragment>
	<!-- Header -->
	<div class="mb-4">
		{#if lastErr}
			<div class="alert alert-error mt-3">
				<XIcon class="w-5 h-5" />
				<span>{lastErr}</span>
			</div>
		{/if}
	</div>

	<!-- Table -->
	<div class="card bg-base-100">
		<div class="card-body p-0">
			<div class="overflow-x-auto rounded-2xl border border-base-300">
				<table class="table table-zebra">
					<thead>
						<tr>
							<th class="w-12">
								<label>
									<input
										type="checkbox"
										class="checkbox bg-base-100"
										on:change={toggleAllTransactionSelection}
										checked={transactions.length > 0 &&
											selectedTransactions.size === transactions.length}
									/>
								</label>
							</th>
							<th class="min-w-45">ID</th>
							<th class="min-w-55">Sender</th>
							<th class="min-w-55">Empfänger</th>
							<th class="min-w-65">Beschreibung</th>
							<th class="min-w-55">Überwiesen / Steuer / Erhalten</th>
							<th class="min-w-47.5">Signatur</th>
							<th class="min-w-35">Status</th>
							<th class="min-w-35">Datum</th>
							<th class="w-28 text-right">Aktionen</th>
						</tr>
					</thead>

					<tbody>
						{#if isLoading}
							<tr>
								<td colspan="9" class="py-10">
									<div class="flex justify-center items-center gap-3 opacity-70">
										<span class="loading loading-spinner"></span>
										<span>Lade Transaktionen…</span>
									</div>
								</td>
							</tr>
						{:else if transactions.length === 0}
							<tr>
								<td colspan="9" class="py-10">
									<div class="flex justify-center items-center gap-3 opacity-70">
										<span>Keine Transaktionen gefunden.</span>
									</div>
								</td>
							</tr>
						{:else}
							{#each transactions as transaction}
								<tr class:selected={selectedTransactions.has(transaction.id)}>
									<th>
										<label>
											<input
												type="checkbox"
												class="checkbox bg-base-100"
												on:change={() => toggleTransactionSelection(transaction.id)}
												checked={selectedTransactions.has(transaction.id)}
											/>
										</label>
									</th>

									<td>
										<div class="flex flex-col">
											<span class="font-mono text-sm opacity-80">{transaction.id}</span>
											<span class="text-xs opacity-60">TX</span>
										</div>
									</td>

									<td>
										<div class="flex flex-col gap-1">
											<span class="font-medium"
												>{transaction.expand?.sender?.expand?.user?.name ?? '—'}</span
											>
											<div class="flex flex-wrap gap-1">
												<span class="badge badge-ghost badge-sm">{transaction.sender}</span>
												<span class="badge badge-ghost badge-sm"
													>{transaction.expand?.sender?.expand?.user?.id ?? '—'}</span
												>
											</div>
										</div>
									</td>

									<td>
										<div class="flex flex-col gap-1">
											<span class="font-medium"
												>{transaction.expand?.receiver?.expand?.user?.name ?? '—'}</span
											>
											<div class="flex flex-wrap gap-1">
												<span class="badge badge-ghost badge-sm">{transaction.receiver}</span>
												<span class="badge badge-ghost badge-sm"
													>{transaction.expand?.receiver?.expand?.user?.id ?? '—'}</span
												>
											</div>
										</div>
									</td>

									<td class="max-w-105">
										<p class="line-clamp-3">{transaction.plainDescription}</p>
									</td>

									<td>
										<div class="flex flex-col gap-1">
											<div class="flex items-center gap-1">
												<span class="font-medium">{transaction.amount}</span><Currency />
											</div>
											<div class="flex items-center gap-1 opacity-70">
												<span>-{transaction.amount - transaction.received}</span><Currency />
											</div>
											<div class="flex items-center gap-1">
												<span class="font-medium">{transaction.received}</span><Currency />
											</div>
										</div>
									</td>

									<td class="font-mono text-sm">
										<span class="opacity-80">{String(transaction.signature).substring(0, 16)}…</span
										>
									</td>

									<td>
										{#if transaction.status === 'flagged'}
											<span class="badge badge-error">Flagged</span>
										{:else if transaction.status === 'pending'}
											<span class="badge badge-warning">Pending</span>
										{:else if transaction.status === 'approved'}
											<span class="badge badge-success">Approved</span>
										{:else if transaction.status === 'under_audit'}
											<span class="badge badge-neutral">Under Audit</span>
										{:else}
											<span class="badge badge-ghost">—</span>
										{/if}
									</td>

									<td>
										<div class="flex flex-col">
											<p class="text-xs opacity-60">Erstellt</p>
											<p class="font-medium">{formatDate(transaction.created)}</p>
											<p class="text-sm opacity-60">{formatTime(transaction.created)}</p>
										</div>
									</td>

									<td class="text-right">
										<div class="join">
											<button
												class="btn btn-ghost btn-xs join-item"
												on:click={() => openEditModal(transaction)}
												title="Bearbeiten"
											>
												<PenIcon class="w-4 h-4" />
											</button>
											<button
												class="btn btn-ghost btn-xs join-item"
												on:click={() => navigator.clipboard.writeText(transaction.signature)}
												title="Signatur kopieren"
											>
												<ClipboardTypeIcon class="w-4 h-4" />
											</button>
											<button
												class="btn btn-ghost btn-xs join-item btn-warning"
												on:click={() => openRefundModal(transaction)}
												title="Abbuchung rückerstatten"
											>
												<BanknoteX class="w-4 h-4" />
											</button>
											<button
												class="btn btn-ghost btn-xs btn-error join-item"
												on:click={() => deleteTransaction(transaction.id)}
												title="Löschen"
											>
												<TrashIcon class="w-4 h-4" />
											</button>
										</div>
									</td>
								</tr>
							{/each}
						{/if}
					</tbody>
				</table>
			</div>
		</div>
	</div>
	<div class="flex justify-center mt-4">
		<div class="join">
			<button class="join-item btn" on:click={pageDown} disabled={page <= 1}> « </button>
			<button class="join-item btn btn-ghost">
				Seite <span class="font-mono mx-1">{page}</span> / <span class="font-mono">{maxPage}</span>
			</button>
			<button class="join-item btn" on:click={pageUp} disabled={page >= maxPage}> » </button>
		</div>
	</div>
</AdminPanel>

<!-- ------------------ Modals ------------------ -->

<!-- New Transaction Modal -->
<dialog id="createModal" class="modal" bind:this={newModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Neue Transaktion</h3>

		<div class="tabs tabs-lift mb-6 justify-center w-full mt-2">
			<button
				class="tab {createTransactionGroupMode === 'single' ? 'tab-active' : ''}"
				on:click={() => (createTransactionGroupMode = 'single')}
			>
				Einzeln
			</button>
			<button
				class="tab {createTransactionGroupMode === 'sender_group' ? 'tab-active' : ''}"
				on:click={() => (createTransactionGroupMode = 'sender_group')}
			>
				Sender als Gruppe
			</button>
			<button
				class="tab {createTransactionGroupMode === 'receiver_group' ? 'tab-active' : ''}"
				on:click={() => (createTransactionGroupMode = 'receiver_group')}
			>
				Empfänger als Gruppe
			</button>
		</div>

		<div class="tabs tabs-lift mb-6 justify-center w-full mt-2">
			<button
				class="tab {createTransactionTab === 0 ? 'tab-active' : ''}"
				on:click={() => (createTransactionTab = 0)}
			>
				Überweisung + Notieren
			</button>
			<button
				class="tab {createTransactionTab === 1 ? 'tab-active' : ''}"
				on:click={() => (createTransactionTab = 1)}
			>
				Nur Überweisung
			</button>
			<button
				class="tab {createTransactionTab === 2 ? 'tab-active' : ''}"
				on:click={() => (createTransactionTab = 2)}
			>
				Nur Notieren
			</button>
		</div>

		{#if createTransactionGroupMode === 'sender_group'}
			<div role="alert" class="alert alert-info mb-4">
				<UsersIcon class="w-6 h-6" />
				<span>Jedes Konto der Sender-Gruppe überweist an ein gemeinsames Empfängerkonto.</span>
			</div>
		{:else if createTransactionGroupMode === 'receiver_group'}
			<div role="alert" class="alert alert-info mb-4">
				<UsersIcon class="w-6 h-6" />
				<span>Ein Senderkonto überweist an jedes Konto der Empfänger-Gruppe.</span>
			</div>
		{/if}

		{#if createTransactionTab === 0}
			<div role="alert" class="alert alert-success mb-4">
				<ArrowLeftRightIcon class="w-6 h-6" />
				<span>Geld überweisen & Transaktion notieren</span>
			</div>
		{:else if createTransactionTab === 1}
			<div role="alert" class="alert alert-warning mb-4">
				<HandCoinsIcon class="w-6 h-6" />
				<span>Geld überweisen ohne Transaktion zu notieren</span>
			</div>
		{:else}
			<div role="alert" class="alert alert-error mb-4">
				<NotebookPenIcon class="w-6 h-6" />
				<span>Nur notieren (kein Geldtransfer)</span>
			</div>
		{/if}

		<div class="grid grid-cols-1 gap-3">
			{#if createTransactionGroupMode === 'sender_group'}
				<select
					class="select select-bordered w-full bg-base-100"
					bind:value={createTransactionSenderGroupID}
				>
					<option value="">Sender-Gruppe auswählen</option>
					{#each adminGroups as group}
						<option value={group.id}>
							{group.name} ({group.expand?.users?.length ?? 0})
						</option>
					{/each}
				</select>
			{:else}
				<button
					class="btn btn-primary btn-outline w-full"
					on:click={() => openAccountSelectModal('create_sender')}
				>
					<UsersIcon class="w-4 h-4" />
					{createTransactionSenderID
						? `Sender: ${createTransactionSenderID}`
						: 'Sender auswählen'}
				</button>
			{/if}

			{#if createTransactionGroupMode === 'receiver_group'}
				<select
					class="select select-bordered w-full bg-base-100"
					bind:value={createTransactionReceiverGroupID}
				>
					<option value="">Empfänger-Gruppe auswählen</option>
					{#each adminGroups as group}
						<option value={group.id}>
							{group.name} ({group.expand?.users?.length ?? 0})
						</option>
					{/each}
				</select>
			{:else}
				<button
					class="btn btn-primary btn-outline w-full"
					on:click={() => openAccountSelectModal('create_receiver')}
				>
					<UsersIcon class="w-4 h-4" />
					{createTransactionReceiverID
						? `Empfänger: ${createTransactionReceiverID}`
						: 'Empfänger auswählen'}
				</button>
			{/if}

			<label class="input input-bordered flex items-center gap-2 w-full">
				<input
					type="number"
					class="grow"
					placeholder="Betrag"
					autocomplete="off"
					bind:value={createTransactionAmount}
				/>
				<Currency size={22} />
			</label>

			{#if createTransactionTab !== 1}
				<textarea
					class="textarea textarea-bordered w-full"
					rows="3"
					placeholder="Beschreibung"
					autocomplete="off"
					bind:value={createTransactionDescription}
				></textarea>
			{/if}

			{#if createTransactionTab !== 1}
				<textarea
					class="textarea textarea-bordered w-full"
					rows="3"
					placeholder="System"
					bind:value={createTransactionSystem}
				></textarea>
			{/if}

			{#if createTransactionTab !== 1}
				<select
					class="select select-bordered w-full bg-base-100"
					bind:value={createTransactionStatus}
				>
					<option value="under_audit">Under Audit</option>
					<option value="flagged">Flagged</option>
					<option value="pending" selected>Pending</option>
					<option value="approved">Approved</option>
				</select>
			{/if}

			<div class="flex items-center justify-between">
				<span>Besteuern</span>
				<input
					type="checkbox"
					class="toggle toggle-primary bg-base-100"
					bind:checked={createTransactionTax}
				/>
			</div>

			{#if createTransactionTax}
				<label class="flex items-center justify-between">
					<span>VAT</span>
					<input
						type="checkbox"
						class="toggle toggle-primary bg-base-100"
						bind:checked={createTransactionVAT}
					/>
				</label>
			{/if}
			{#if createTransactionTax}
				<label class="flex items-center justify-between">
					<span>Einkommen</span>
					<input
						type="checkbox"
						class="toggle toggle-primary bg-base-100"
						bind:checked={createTransactionIncomeTax}
					/>
				</label>
			{/if}
			{#if createTransactionTax}
				<label class="flex items-center justify-between">
					<span>Überweisung</span>
					<input
						type="checkbox"
						class="toggle toggle-primary bg-base-100"
						bind:checked={createTransactionTransactionTax}
					/>
				</label>
			{/if}
		</div>

		<div class="modal-action">
			<button class="btn btn-ghost" on:click={() => newModal.close()}>Abbrechen</button>
			<button class="btn btn-success" on:click={createTransaction}>Erstellen</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Edit Transaction Modal -->
<dialog id="editModal" class="modal" bind:this={editModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Transaktion bearbeiten</h3>

		<div class="grid grid-cols-1 gap-3 mt-3">
			<button
				class="btn btn-primary btn-outline w-full"
				on:click={() => openAccountSelectModal('edit_sender')}
			>
				<UsersIcon class="w-4 h-4" />
				{editTransactionSenderID ? `Sender: ${editTransactionSenderID}` : 'Sender auswählen'}
			</button>

			<button
				class="btn btn-primary btn-outline w-full"
				on:click={() => openAccountSelectModal('edit_receiver')}
			>
				<UsersIcon class="w-4 h-4" />
				{editTransactionReceiverID
					? `Empfänger: ${editTransactionReceiverID}`
					: 'Empfänger auswählen'}
			</button>

			<input
				type="number"
				class="input input-bordered w-full"
				placeholder="Betrag"
				bind:value={editTransactionAmount}
			/>
			<textarea
				class="textarea textarea-bordered w-full"
				rows="3"
				placeholder="Beschreibung"
				bind:value={editTransactionDescription}
			></textarea>
			<textarea
				class="textarea textarea-bordered w-full"
				rows="3"
				placeholder="System"
				bind:value={editTransactionSystem}
			></textarea>
			<select class="select select-bordered w-full bg-base-100" bind:value={editTransactionStatus}>
				<option value="under_audit">Under Audit</option>
				<option value="flagged">Flagged</option>
				<option value="pending">Pending</option>
				<option value="approved">Approved</option>
			</select>
			<div class="rounded-box border border-base-300 p-3">
				<div class="flex items-center justify-between gap-2">
					<span class="font-bold">Signatur</span>
					<button
						class="btn btn-ghost btn-xs"
						on:click={() => navigator.clipboard.writeText(editTransactionSignature)}
					>
						<ClipboardTypeIcon class="w-4 h-4" /> kopieren
					</button>
				</div>
				<p class="opacity-70 break-all text-sm mt-2">{editTransactionSignature}</p>
			</div>
		</div>

		<div class="modal-action">
			<button class="btn btn-ghost" on:click={() => editModal.close()}>Abbrechen</button>
			<button class="btn btn-success" on:click={saveEditTransaction}>Speichern</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Mass Edit Transaction Modal -->
<dialog id="massEditModal" class="modal" bind:this={massEditModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Massenbearbeitung</h3>

		<div class="space-y-3 mt-4">
			<div class="flex items-center justify-between">
				<span>Sender aktualisieren</span>
				<input
					type="checkbox"
					class="toggle toggle-primary bg-base-100"
					bind:checked={massEditUpdateTransactionSenderID}
				/>
			</div>
			{#if massEditUpdateTransactionSenderID}
				<button
					class="btn btn-primary btn-outline w-full"
					on:click={() => openAccountSelectModal('mass_edit_sender')}
				>
					{massEditTransactionSenderID
						? `Sender: ${massEditTransactionSenderID}`
						: 'Sender auswählen'}
				</button>
			{/if}

			<div class="flex items-center justify-between">
				<span>Empfänger aktualisieren</span>
				<input
					type="checkbox"
					class="toggle toggle-primary bg-base-100"
					bind:checked={massEditUpdateTransactionReceiverID}
				/>
			</div>
			{#if massEditUpdateTransactionReceiverID}
				<button
					class="btn btn-primary btn-outline w-full"
					on:click={() => openAccountSelectModal('mass_edit_receiver')}
				>
					{massEditTransactionReceiverID
						? `Empfänger: ${massEditTransactionReceiverID}`
						: 'Empfänger auswählen'}
				</button>
			{/if}

			<div class="flex items-center justify-between">
				<span>Betrag aktualisieren</span>
				<input
					type="checkbox"
					class="toggle toggle-primary bg-base-100"
					bind:checked={massEditUpdateTransactionAmount}
				/>
			</div>
			{#if massEditUpdateTransactionAmount}
				<input
					type="number"
					class="input input-bordered w-full"
					placeholder="Betrag"
					bind:value={massEditTransactionAmount}
				/>
			{/if}

			<div class="flex items-center justify-between">
				<span>Beschreibung aktualisieren</span>
				<input
					type="checkbox"
					class="toggle toggle-primary bg-base-100"
					bind:checked={massEditUpdateTransactionDescription}
				/>
			</div>
			{#if massEditUpdateTransactionDescription}
				<textarea
					class="textarea textarea-bordered w-full"
					rows="3"
					placeholder="Beschreibung"
					bind:value={massEditTransactionDescription}
				></textarea>
			{/if}

			<div class="flex items-center justify-between">
				<span>System aktualisieren</span>
				<input
					type="checkbox"
					class="toggle toggle-primary bg-base-100"
					bind:checked={massEditUpdateTransactionSystem}
				/>
			</div>
			{#if massEditUpdateTransactionSystem}
				<textarea
					class="textarea textarea-bordered w-full"
					rows="3"
					placeholder="System"
					bind:value={massEditTransactionSystem}
				></textarea>
			{/if}

			<div class="flex items-center justify-between">
				<span>Status aktualisieren</span>
				<input
					type="checkbox"
					class="toggle toggle-primary bg-base-100"
					bind:checked={massEditUpdateTransactionStatus}
				/>
			</div>

			{#if massEditUpdateTransactionStatus}
				<select
					class="select select-bordered w-full bg-base-100"
					bind:value={massEditTransactionStatus}
				>
					<option value="under_audit">Under Audit</option>
					<option value="flagged">Flagged</option>
					<option value="pending">Pending</option>
					<option value="approved">Approved</option>
				</select>
			{/if}
		</div>

		<div class="modal-action">
			<button class="btn btn-ghost" on:click={() => massEditModal.close()}>Abbrechen</button>
			<button class="btn btn-success" on:click={bulkUpdate}>Speichern</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Mass Delete Transaction Modal -->
<dialog id="massDeleteModal" class="modal" bind:this={massDeleteModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Massenlöschung</h3>
		<p class="py-4">
			Sind Sie sicher, dass Sie die ausgewählten Transaktionen löschen möchten? Das kann nicht
			rückgängig gemacht werden.
		</p>

		<div class="modal-action">
			<button class="btn btn-ghost" on:click={() => massDeleteModal.close()}>Abbrechen</button>
			<button class="btn btn-error" on:click={massDelete}>Löschen</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- CSV Export Modal -->
<dialog id="csvExportModal" class="modal" bind:this={csvExportModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">CSV Export</h3>
		<p class="py-4">Die CSV-Datei wurde erstellt.</p>

		<a class="btn btn-success w-full" href={csvLink} download="transactions_export.csv">
			<DownloadIcon class="w-4 h-4" />
			CSV herunterladen
		</a>

		<div class="modal-action">
			<button class="btn btn-ghost" on:click={() => csvExportModal.close()}>Schließen</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Refund Transaction Modal -->
<dialog id="refundModal" class="modal" bind:this={refundModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Refund Transaktion</h3>

		<div class="flex w-full">
			<div class="join w-full">
				<button
					class="btn join-item w-1/2 {refundMethod === 'new' ? 'btn-primary' : 'btn-outline'}"
					on:click={() => (refundMethod = 'new')}
				>
					Neue Transaktion
				</button>
				<button
					class="btn join-item w-1/2 {refundMethod === 'erase' ? 'btn-primary' : 'btn-outline'}"
					on:click={() => (refundMethod = 'erase')}
				>
					Alte Transaktion löschen
				</button>
			</div>
		</div>

		{#if refundMethod === 'erase'}
			<div role="alert" class="alert alert-error mt-4">
				<TrashIcon class="w-6 h-6" />
				<span
					>Die ursprüngliche Transaktion wird gelöscht und die Beträge werden zurücküberwiesen.</span
				>
			</div>
		{:else}
			<div role="alert" class="alert alert-success mt-4">
				<ArrowLeftRightIcon class="w-6 h-6" />
				<span
					>Es wird eine neue Transaktion erstellt, die die ursprüngliche abbucht und den Betrag
					zurückerstattet.</span
				>
			</div>
			<textarea
				class="textarea textarea-bordered w-full mt-4"
				rows="3"
				placeholder="Beschreibung für die Refund-Transaktion"
				bind:value={refundDescription}
			></textarea>
		{/if}

		<div class="modal-action">
			<button class="btn btn-ghost" on:click={() => refundModal.close()}>Abbrechen</button>
			<button class="btn btn-success" on:click={submitRefund}>Refund einreichen</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<AccountSelectModal
	bind:this={accountSelectModal}
	adminMode={true}
	on:select={(e) => selectAccount(e.detail)}
/>

<style>
	/* purely visual, no @apply */
	tr.selected td,
	tr.selected th {
		background: color-mix(
			in oklab,
			var(--fallback-b1, oklch(var(--b1))) 70%,
			var(--fallback-p, oklch(var(--p))) 30%
		);
	}
</style>

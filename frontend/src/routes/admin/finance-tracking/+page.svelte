<script lang="ts">
	import db, { server } from '$lib/connections';
	import AdminPanel from '$modules/AdminPanel.svelte';
	import Currency from '$modules/Currency.svelte';
	import AccountSelectModal from '$modules/modals/AccountSelectModal.svelte';
	import UserSelectModal from '$modules/modals/UserSelectModal.svelte';
	import { Info, Trash2, X } from '@lucide/svelte';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';

	let userSelectModal: UserSelectModal;
	let accountSelectModal: AccountSelectModal;

	let tab: 'assigned' | 'archived' = 'assigned';

	let cases = [];
	let loading = false;

	let page = 1;
	let perPage = 30;
	let totalPages = 1;
	let totalItems = 0;

	let caseModalOpen = false;
	let selectedCase: any = null;
	let editingCase: any = null;
	let caseModalElement: HTMLDialogElement;

	onMount(async () => {
		if (!browser) return;
		await fetchCases();
	});

	function formatDate(value: any) {
		if (!value) return '-';
		return new Date(value).toLocaleString();
	}

	function formatAmount(value: any) {
		if (value === null || value === undefined) return '-';
		const num = Number(value);
		if (Number.isNaN(num)) return value;
		return num.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
	}

	async function switchTab(newTab: 'assigned' | 'archived') {
		tab = newTab;
		console.log('Switched tab to', tab);
		await fetchCases();
	}
	async function fetchCases() {
		loading = true;
		if (tab === 'assigned') {
			const data = await db.collection('assigned_cases').getList(page, perPage, {
				sort: '-created',
				expand:
					'handler,transaction,taxLog,transaction.sender,transaction.receiver,transaction.sender.user,transaction.receiver.user'
			});
			cases = data.items;
			totalPages = data.totalPages;
			totalItems = data.totalItems;
		} else {
			const data = await db.collection('archived_cases').getList(page, perPage, {
				sort: '-created',
				expand:
					'handler,transaction,taxLog,transaction.sender,transaction.receiver,transaction.sender.user,transaction.receiver.user'
			});
			cases = data.items;
			totalPages = data.totalPages;
			totalItems = data.totalItems;
		}
		cases = [...cases];
		loading = false;
	}
	function deleteCase(id) {
		if (
			confirm(
				'Möchtest du diesen Fall wirklich löschen? Diese Aktion kann nicht rückgängig gemacht werden.'
			)
		) {
			db.collection(tab === 'assigned' ? 'assigned_cases' : 'archived_cases')
				.delete(id)
				.then(() => {
					fetchCases();
				});
		}
	}

	async function openCase(caseItem) {
		selectedCase = await db
			.collection(tab === 'assigned' ? 'assigned_cases' : 'archived_cases')
			.getOne(caseItem.id, {
				expand:
					'handler,transaction,taxLog,transaction.sender,transaction.receiver,transaction.sender.user,transaction.receiver.user'
			});
		editingCase = JSON.parse(JSON.stringify(selectedCase));
		caseModalElement?.showModal();
	}

	function closeModal() {
		caseModalElement?.close();
		selectedCase = null;
		editingCase = null;
	}

	async function saveCaseChanges() {
		try {
			const updateData = {
				handler: editingCase.handler,
				reason: editingCase.reason
			};

			await db
				.collection(tab === 'assigned' ? 'assigned_cases' : 'archived_cases')
				.update(editingCase.id, updateData);
			await fetchCases();
			alert('Fall erfolgreich aktualisiert!');
			closeModal();
		} catch (error) {
			alert('Fehler beim Aktualisieren des Falls: ' + error.message);
		}
	}

	function onHandlerSelected(event: CustomEvent) {
		editingCase.handler = event.detail.id;
		userSelectModal.closeModal();
	}
	async function submitCase(illegal: boolean) {
		if (
			confirm(`Möchtest du diesen Fall wirklich als ${illegal ? 'illegal' : 'legal'} markieren?`)
		) {
			const response = await fetch(server + '/api/employee/submit-case', {
				method: 'POST',
				headers: {
					'Content-Type': 'application/json',
					Authorization: 'Bearer ' + db.authStore.token
				},
				body: JSON.stringify({
					id: selectedCase.id,
					illegal: illegal,
					reason: selectedCase.reason
				})
			});
			const data = await response.json();
			if (data.success) {
				alert('Fall erfolgreich aktualisiert!');
				selectedCase = null;
				caseModalElement?.close();
				await fetchCases(); // Refresh the case list
			} else {
				alert('Fehler beim Aktualisieren des Falls: ' + data.message);
			}
		}
	}
</script>

<!-- svelte-ignore a11y_missing_attribute -->
<AdminPanel
	title="Finanztracking"
	showCreateButton={false}
	showMassEditButton={true}
	showMassDeleteButton={true}
	showCSVExportButton={true}
	showPDFExportButton={true}
	showCSVImportButton={false}
	{totalItems}
>
	<svelte:fragment slot="filter">
		<div class="flex flex-col">
			<div class="flex flex-row">
				<div role="tablist" class="tabs tabs-border">
					<input
						type="radio"
						name="menu"
						class="tab"
						aria-label="Zugewiesene Fälle"
						checked={tab === 'assigned'}
						on:click={() => switchTab('assigned')}
					/>
					<input
						type="radio"
						name="menu"
						class="tab"
						aria-label="Archivierte Fälle"
						checked={tab === 'archived'}
						on:click={() => switchTab('archived')}
					/>
				</div>
			</div>
			<div class="flex flex-row"></div>
		</div>
	</svelte:fragment>

	<div>
		<div class="overflow-x-auto">
			<table class="table table-zebra w-full">
				<thead>
					<tr>
						<th>ID</th>
						<th>Transaktion</th>
						<th>Bearbeiter</th>
						<th>Steuerprotokoll</th>
						<th>Erstellt am</th>
						<th>Aktionen</th>
					</tr>
				</thead>
				<tbody>
					{#each cases as caseItem}
						<tr>
							<td>{caseItem.id}</td>
							<td>
								<div class="flex flex-col">
									<span class="flex flex-row"
										>{caseItem.expand.transaction.amount} <Currency></Currency></span
									>
								</div>
							</td>
							<td>{caseItem.handler ? caseItem.expand.handler.name : 'Kein Bearbeiter'}</td>
							<td>{caseItem.taxLog ? `ID: ${caseItem.taxLog}` : 'Kein Steuerprotokoll'}</td>
							<td>{new Date(caseItem.created).toLocaleString()}</td>
							<td>
								<button class="btn btn-sm btn-primary" on:click={() => openCase(caseItem)}
									><Info></Info></button
								>
								<button class="btn btn-sm btn-error" on:click={() => deleteCase(caseItem.id)}
									><Trash2 /></button
								>
							</td>
						</tr>
					{/each}
				</tbody>
			</table>
		</div>
	</div>

	<!-- Case Management Modal -->
	{#if selectedCase && editingCase}
		<dialog bind:this={caseModalElement} class="modal">
			<div class="modal-box w-full max-w-4xl max-h-screen overflow-y-auto">
				<div class="flex items-start justify-between gap-3 sticky top-0 bg-base-100 pb-4 border-b">
					<div>
						<h3 class="text-lg font-bold">Fall {selectedCase?.id} verwalten</h3>
						<p class="text-sm opacity-70">Bearbeiten Sie die Falldetails.</p>
					</div>
					<button class="btn btn-ghost btn-sm" on:click={closeModal} aria-label="Schließen">
						<X size={18} />
					</button>
				</div>

				<div class="space-y-6 mt-4">
					<!-- Handler Selection -->
					<div class="card border border-base-300 bg-base-100">
						<div class="card-body">
							<h3 class="card-title">Bearbeiter</h3>
							<div class="flex items-center gap-2 justify-between">
								<div class="text-sm">
									<p class="mb-1">
										<strong>Aktuell:</strong>
										{editingCase.expand?.handler?.name ?? 'Nicht zugewiesen'}
									</p>
									{#if tab === 'archived'}
										{#if editingCase.illegal}
											<span class="badge badge-error">Illegal</span>
										{:else}
											<span class="badge badge-success">Legal</span>
										{/if}
									{/if}
								</div>
								<button class="btn btn-sm btn-outline" on:click={() => userSelectModal.show()}>
									Bearbeiter ändern
								</button>
							</div>
						</div>
					</div>

					<!-- Transaction Details -->
					<div class="grid grid-cols-1 gap-4 md:grid-cols-2">
						<div class="stat rounded-box border border-base-300 bg-base-100">
							<div class="stat-title">Betrag</div>
							<div class="stat-value text-lg">
								{formatAmount(selectedCase.expand?.transaction?.amount)}
							</div>
							<div class="stat-desc">
								Angekommen: {formatAmount(selectedCase.expand?.transaction?.received)}
							</div>
						</div>
						<div class="stat rounded-box border border-base-300 bg-base-100">
							<div class="stat-title">Erstellt</div>
							<div class="stat-value text-lg">
								{formatDate(selectedCase.expand?.transaction?.created)}
							</div>
						</div>
						<div class="stat rounded-box border border-base-300 bg-base-100">
							<div class="stat-title">Sender ID</div>
							<div class="stat-value text-lg">
								{selectedCase.expand?.transaction?.sender ?? '-'}
							</div>
						</div>
						<div class="stat rounded-box border border-base-300 bg-base-100">
							<div class="stat-title">Receiver ID</div>
							<div class="stat-value text-lg">
								{selectedCase.expand?.transaction?.receiver ?? '-'}
							</div>
						</div>
					</div>

					<!-- Transaction Details Card -->
					<div class="card border border-base-300 bg-base-100">
						<div class="card-body">
							<h3 class="card-title">Transaktionsdetails</h3>
							<div class="space-y-2 text-sm">
								<p>
									<strong>Transaktions-ID:</strong>
									{selectedCase.expand?.transaction?.id ?? '-'}
								</p>
								<p>
									<strong>Beschreibung:</strong>
									{selectedCase.expand?.transaction?.description ?? '-'}
								</p>
								<p><strong>System:</strong></p>
								<pre class="mockup-code text-xs whitespace-pre-wrap break-all p-4"><code
										>{selectedCase.expand?.transaction?.system ?? '-'}</code
									></pre>
								<p><strong>Signatur:</strong></p>
								<pre class="mockup-code text-xs whitespace-pre-wrap break-all p-4"><code
										>{selectedCase.expand?.transaction?.signature ?? '-'}</code
									></pre>
							</div>
						</div>
					</div>

					<!-- Tax Data -->
					<div class="card border border-base-300 bg-base-100">
						<div class="card-body">
							<h3 class="card-title">Steuerdaten</h3>
							<div class="overflow-x-auto">
								<table class="table table-sm">
									<tbody>
										<tr
											><th>Ürsprünglicher Betrag</th><td
												>{formatAmount(selectedCase.expand?.taxLog?.originalAmount)}</td
											></tr
										>
										<tr
											><th>Mehrwertsteuer</th><td
												>{formatAmount(selectedCase.expand?.taxLog?.vatAmount)}</td
											></tr
										>
										<tr
											><th>Einkommensteuer</th><td
												>{formatAmount(selectedCase.expand?.taxLog?.incomeTaxAmount)}</td
											></tr
										>
										<tr
											><th>Transaktionssteuer</th><td
												>{formatAmount(selectedCase.expand?.taxLog?.transactionTaxAmount)}</td
											></tr
										>
										<tr
											><th>Steuerfreier Pool verwendet</th><td
												>{formatAmount(selectedCase.expand?.taxLog?.taxFreePoolUsed)}</td
											></tr
										>
										<tr
											><th>Neuer Steuerfreier Pool</th><td
												>{formatAmount(selectedCase.expand?.taxLog?.newTaxFreePool)}</td
											></tr
										>
										<tr
											><th>Gesamtsteuer</th><td
												>{formatAmount(selectedCase.expand?.taxLog?.totalTaxAmount)}</td
											></tr
										>
										<tr
											><th>Vorheriges Einkommen</th><td
												>{formatAmount(selectedCase.expand?.taxLog?.previousIncomeReceived)}</td
											></tr
										>
									</tbody>
								</table>
							</div>
						</div>
					</div>

					<!-- Sender Details -->
					<div class="card border border-base-300 bg-base-100">
						<div class="card-body">
							<h3 class="card-title">Sender</h3>
							<div class="space-y-1 text-sm">
								<p>
									<strong>Account ID:</strong>
									{selectedCase.expand?.transaction?.expand?.sender?.id ?? '-'}
								</p>
								<p>
									<strong>Gefroren:</strong>
									{selectedCase.expand?.transaction?.expand?.sender?.frozen ? 'Ja' : 'Nein'}
								</p>
								<p>
									<strong>Kontostand:</strong>
									{formatAmount(selectedCase.expand?.transaction?.expand?.sender?.balance)}
								</p>
								<p>
									<strong>Name:</strong>
									{selectedCase.expand?.transaction?.expand?.sender?.expand?.user?.name ?? '-'}
								</p>
								<p>
									<strong>Firma:</strong>
									{selectedCase.expand?.transaction?.expand?.sender?.expand?.user?.isCompany
										? selectedCase.expand?.transaction?.expand?.sender?.expand?.user?.companyName
										: '-'}
								</p>
								<p>
									<strong>Gesperrt:</strong>
									{selectedCase.expand?.transaction?.expand?.sender?.expand?.user?.locked
										? 'Ja'
										: 'Nein'}
								</p>
							</div>
						</div>
					</div>

					<!-- Receiver Details -->
					<div class="card border border-base-300 bg-base-100">
						<div class="card-body">
							<h3 class="card-title">Empfänger</h3>
							<div class="space-y-1 text-sm">
								<p>
									<strong>Account ID:</strong>
									{selectedCase.expand?.transaction?.expand?.receiver?.id ?? '-'}
								</p>
								<p>
									<strong>Gefroren:</strong>
									{selectedCase.expand?.transaction?.expand?.receiver?.frozen ? 'Ja' : 'Nein'}
								</p>
								<p>
									<strong>Kontostand:</strong>
									{formatAmount(selectedCase.expand?.transaction?.expand?.receiver?.balance)}
								</p>
								<p>
									<strong>Name:</strong>
									{selectedCase.expand?.transaction?.expand?.receiver?.expand?.user?.name ?? '-'}
								</p>
								<p>
									<strong>Firma:</strong>
									{selectedCase.expand?.transaction?.expand?.receiver?.expand?.user?.isCompany
										? selectedCase.expand?.transaction?.expand?.receiver?.expand?.user?.companyName
										: '-'}
								</p>
								<p>
									<strong>Gesperrt:</strong>
									{selectedCase.expand?.transaction?.expand?.receiver?.expand?.user?.locked
										? 'Ja'
										: 'Nein'}
								</p>
							</div>
						</div>
					</div>

					<!-- reason -->
					<div class="card border border-base-300 bg-base-100">
						<div class="card-body">
							<h3 class="card-title">Grund</h3>
							<textarea
								class="textarea textarea-bordered w-full"
								placeholder="Fallnotizen..."
								bind:value={editingCase.reason}
								rows="5"
							></textarea>
						</div>
					</div>
				</div>

				<!-- Modal Actions -->
				<div
					class="modal-action sticky bottom-0 bg-base-100 border-t mt-6 pt-4 w-full justify-between"
				>
					{#if tab === 'assigned'}
						<div class="flex flex-row gap-2">
							<button class="btn btn-error" on:click={() => submitCase(true)}>Illegal</button>
							<button class="btn btn-success" on:click={() => submitCase(false)}>Legal</button>
						</div>
					{:else}
						<div></div>
					{/if}
					<div class="flex flex-row gap-2">
						<button class="btn btn-outline" on:click={closeModal}>Abbrechen</button>
						<button class="btn btn-primary" on:click={saveCaseChanges}>Änderungen speichern</button>
					</div>
				</div>
			</div>
			<form method="dialog" class="modal-backdrop">
				<!-- svelte-ignore a11y_consider_explicit_label -->
				<button></button>
			</form>
		</dialog>
	{/if}

	<!-- User Select Modal for Handler -->
	<UserSelectModal bind:this={userSelectModal} adminMode={true} on:select={onHandlerSelected} />
</AdminPanel>

<script lang="ts">
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';
	import AdminPanel from '$modules/AdminPanel.svelte';
	import AccountSelectModal from '$modules/modals/AccountSelectModal.svelte';
	import db, { server } from '$lib/connections';
	import Currency from '$modules/Currency.svelte';
	import { Carta } from 'carta-md';
	import { MarkdownEditor } from 'carta-md';
	import { emoji } from '@cartamd/plugin-emoji';
	import { slash } from '@cartamd/plugin-slash';
	import { code } from '@cartamd/plugin-code';
	import '$lib/styles/github.scss';

	let recurringTransactions: any[] = [];
	let selectedRecurring: Set<string> = new Set();
	let page = 1;
	let perPage = 20;
	let totalPages = 1;
	let totalItems = 0;
	let loading = true;
	let filter = '';

	// Edit / Create state
	let isEditing = false;
	let editId: string | null = null;
	let senderAccountID = '';
	let receiverAccount: any = null;
	let amount = 0;
	let maxAmountEnabled = false;
	let maxAmount = -1;
	let description = '';
	let days: string[] = [];
	let timeString = '00:00';
	let timeSeconds = 0;
	let timezone = '';

	// Modals
	let accountSelectorModal: AccountSelectModal;
	let createEditModal: HTMLDialogElement;
	let deleteConfirmModal: HTMLDialogElement;
	let massDeleteModal: HTMLDialogElement;
	let selectingFor: 'sender' | 'receiver' = 'receiver';

	const WEEKDAYS = ['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'];

	const carta = new Carta({ sanitizer: false, extensions: [emoji(), slash(), code()] });

	onMount(async () => {
		if (!browser) return;
		await fetchRecurringTransactions();
		// default timezone detection
		try {
			timezone = Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
		} catch (e) {
			timezone = 'UTC';
		}
	});

	async function fetchRecurringTransactions(p = 1) {
		page = p;
		loading = true;
		let filterString = '';
		if (filter && filter.trim()) {
			const q = filter.replace(/"/g, '');
			filterString = `id ~ "${q}" || receiver ~ "${q}" || sender ~ "${q}" || description ~ "${q}"`;
		}
		const res = await db.collection('recurring_transactions').getList(page, perPage, {
			sort: '-created',
			filter: filterString || undefined,
			expand: 'sender,receiver,sender.user,receiver.user'
		});
		recurringTransactions = res.items;
		totalPages = res.totalPages || 1;
		totalItems = res.totalItems || 0;
		loading = false;
	}

	// Account selector
	function openAccountSelector(forField: 'sender' | 'receiver') {
		selectingFor = forField;
		accountSelectorModal.showModal();
	}
	function handleAccountSelect(event: any) {
		const acc = event.detail;
		if (selectingFor === 'receiver') receiverAccount = acc;
		else senderAccountID = acc.id;
	}

	function openCreateModal() {
		isEditing = false;
		editId = null;
		senderAccountID = '';
		receiverAccount = null;
		amount = 0;
		maxAmount = -1;
		maxAmountEnabled = false;
		description = '';
		days = [];
		timeString = '00:00';
		createEditModal.showModal();
	}
	function openEditModal(item: any) {
		isEditing = true;
		editId = item.id;
		senderAccountID = item.sender || '';
		receiverAccount = item.expand?.receiver || { id: item.receiver };
		amount = item.amount || 0;
		if (typeof item.maxAmount === 'number' && item.maxAmount >= 0) {
			maxAmount = item.maxAmount;
			maxAmountEnabled = true;
		} else {
			maxAmount = -1;
			maxAmountEnabled = false;
		}
		description = item.description || '';
		days = item.days || [];
		timeSeconds = item.time || 0;
		timeString = secondsToTimeString(timeSeconds);
		timezone = item.timezone || timezone;
		createEditModal.showModal();
	}

	function toggleDay(day: string) {
		days = days.includes(day) ? days.filter((d) => d !== day) : [...days, day];
	}
	function timeStringToSeconds(t: string) {
		const [hh = 0, mm = 0] = t.split(':').map(Number);
		return ((hh || 0) * 3600 + (mm || 0) * 60) % 86400;
	}
	function secondsToTimeString(s: number) {
		const local = ((Number(s || 0) % 86400) + 86400) % 86400;
		const hh = Math.floor(local / 3600)
			.toString()
			.padStart(2, '0');
		const mm = Math.floor((local % 3600) / 60)
			.toString()
			.padStart(2, '0');
		return `${hh}:${mm}`;
	}

	async function saveRecurring() {
		if (!receiverAccount) return alert('Empfänger auswählen');
		timeSeconds = timeStringToSeconds(timeString);
		const payload: any = {
			sender: senderAccountID,
			receiver: receiverAccount?.id,
			amount: Number(amount),
			maxAmount: maxAmountEnabled ? Number(maxAmount) : -1,
			description,
			days,
			time: timeSeconds,
			paid: 0,
			timezone
		};
		try {
			let res: any;
			if (isEditing && editId)
				res = await db.collection('recurring_transactions').update(editId, payload);
			else res = await db.collection('recurring_transactions').create(payload);
			// attempt server-side sync
			try {
				await fetch(server + `/api/sync-recurring-transaction?id=${res.id}`, {
					method: 'POST',
					mode: 'no-cors'
				});
			} catch (e) {}
			createEditModal.close();
			await fetchRecurringTransactions(page);
		} catch (e) {
			console.error('save error', e);
			alert('Fehler beim Speichern');
		}
	}

	// Delete
	let toDeleteId: string | null = null;
	function confirmDelete(id: string) {
		toDeleteId = id;
		deleteConfirmModal.showModal();
	}
	async function doDelete() {
		if (!toDeleteId) return;
		try {
			await db.collection('recurring_transactions').delete(toDeleteId);
			deleteConfirmModal.close();
			toDeleteId = null;
			await fetchRecurringTransactions(page);
		} catch (e) {
			console.error(e);
			alert('Fehler beim Löschen');
		}
	}

	// Manual refresh (call server sync endpoint)
	async function manualRefresh(id: string) {
		try {
			await fetch(server + `/api/sync-recurring-transaction?id=${id}`, {
				method: 'POST',
				mode: 'no-cors'
			});
			alert('Refresh request sent');
		} catch (e) {
			console.error(e);
			alert('Fehler beim Aufrufen der Synchronisation');
		}
	}

	function pageUp() {
		if (page < totalPages) fetchRecurringTransactions(page + 1);
	}
	function pageDown() {
		if (page > 1) fetchRecurringTransactions(page - 1);
	}

	function toggleRecurringSelection(id: string) {
		if (selectedRecurring.has(id)) selectedRecurring.delete(id);
		else selectedRecurring.add(id);
		selectedRecurring = new Set(selectedRecurring);
	}
	function toggleAllRecurringSelection() {
		if (selectedRecurring.size === recurringTransactions.length) selectedRecurring.clear();
		else recurringTransactions.forEach((r) => selectedRecurring.add(r.id));
		selectedRecurring = new Set(selectedRecurring);
	}

	function openMassDelete() {
		if (selectedRecurring.size === 0) return;
		massDeleteModal.showModal();
	}

	async function massDeleteSelected() {
		const ids = Array.from(selectedRecurring);
		try {
			await Promise.all(
				ids.map(async (id) => {
					await db.collection('recurring_transactions').delete(id, { requestKey: null });
					try {
						await fetch(server + `/api/sync-recurring-transaction?id=${id}`, {
							method: 'POST',
							mode: 'no-cors'
						});
					} catch (e) {}
				})
			);
			selectedRecurring.clear();
			selectedRecurring = new Set();
			massDeleteModal.close();
			await fetchRecurringTransactions(page);
		} catch (e) {
			console.error('mass delete error', e);
			alert('Fehler bei der Massenlöschung');
		}
	}
</script>

<title>Admin - Wiederkehrende Transaktionen</title>
<AdminPanel
	title="Wiederkehrende Transaktionen"
	description="Verwaltung aller Daueraufträge"
	showCreateButton={true}
	showMassDeleteButton={true}
	massDeleteButtonText="Massenlöschung"
	{totalItems}
	selectedItems={selectedRecurring.size}
	on:create={() => openCreateModal()}
	on:massDelete={() => massDeleteModal.showModal()}
>
	<svelte:fragment slot="filter">
		<div class="flex gap-3 flex-row justify-between">
			<div class="flex w-full">
				<input
					class="input input-bordered w-full"
					type="text"
					placeholder="Nach ID, Name oder Beschreibung filtern..."
					bind:value={filter}
					autocomplete="off"
					on:input={() => fetchRecurringTransactions(1)}
				/>
			</div>

			<div class="flex gap-2 flex-wrap">
				<select
					class="select select-bordered"
					bind:value={perPage}
					on:change={() => fetchRecurringTransactions(1)}
				>
					<option value="10">10 / Seite</option>
					<option selected value="20">20 / Seite</option>
					<option value="50">50 / Seite</option>
					<option value="100">100 / Seite</option>
				</select>

				<div class="flex-1"></div>
			</div>
		</div>
		{#if loading}
			<div class="text-sm opacity-70">Lade Daueraufträge…</div>
		{/if}
	</svelte:fragment>

	<div class="overflow-x-auto rounded-2xl border border-base-300">
		<table class="table table-zebra">
			<thead>
				<tr>
					<th
						><input
							type="checkbox"
							class="checkbox bg-base-100"
							on:change={toggleAllRecurringSelection}
							checked={selectedRecurring.size === recurringTransactions.length}
						/></th
					>
					<th>ID</th>
					<th>Absender</th>
					<th>Empfänger</th>
					<th>Tage</th>
					<th>Zeit</th>
					<th>Betrag</th>
					<th>Zeitzone</th>
					<th>Aktionen</th>
				</tr>
			</thead>
			<tbody>
				{#each recurringTransactions as r}
					<tr>
						<th
							><input
								type="checkbox"
								class="checkbox bg-base-100"
								on:change={() => toggleRecurringSelection(r.id)}
								checked={selectedRecurring.has(r.id)}
							/></th
						>
						<td class="font-mono text-sm">{r.id}</td>
						<td>{r.expand?.sender?.user?.name || r.sender}</td>
						<td>{r.expand?.receiver?.user?.name || r.receiver}</td>
						<td>{r.days?.length ? r.days.join(', ') : '—'}</td>
						<td>{r.time != null ? secondsToTimeString(r.time) : '—'}</td>
						<td class="flex flex-row align-middle"
							><p>{r.amount}</p>
							<Currency /></td
						>
						<td class="text-sm">{r.timezone || '—'}</td>
						<td>
							<div class="flex gap-2">
								<button class="btn btn-sm" on:click={() => openEditModal(r)}>Bearbeiten</button>
								<button class="btn btn-sm btn-error" on:click={() => confirmDelete(r.id)}
									>Löschen</button
								>
								<button class="btn btn-sm btn-ghost" on:click={() => manualRefresh(r.id)}
									>Refresh</button
								>
							</div>
						</td>
					</tr>
				{/each}
			</tbody>
		</table>
	</div>

	<div class="flex justify-center mt-4">
		<div class="join">
			<button class="join-item btn" on:click={pageDown} disabled={page <= 1}> « </button>
			<button class="join-item btn btn-ghost">
				Seite <span class="font-mono mx-1">{page}</span> /
				<span class="font-mono">{totalPages}</span>
			</button>
			<button class="join-item btn" on:click={pageUp} disabled={page >= totalPages}> » </button>
		</div>
	</div>

	<!-- Account selector modal -->
	<AccountSelectModal bind:this={accountSelectorModal} on:select={handleAccountSelect} />

	<!-- Create / Edit Modal -->
	<dialog class="modal" bind:this={createEditModal} id="createEdit">
		<div class="modal-box w-full max-w-3xl">
			<h3 class="text-lg font-bold">
				{isEditing ? 'Dauerauftrag bearbeiten' : 'Neuen Dauerauftrag erstellen'}
			</h3>
			{#if isEditing}
				<p class="mt-2">Dauerauftrag ID: {editId}</p>
				<p class="mb-2 italic">Feld leer lassen, um aktuellen Wert zu behalten</p>
			{/if}

			<div>
				<fieldset>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="label mb-2"><span class="label-text">Absender (ID)</span></label>
					<div class="flex gap-2">
						<input
							type="text"
							class="input input-bordered w-full"
							placeholder={senderAccountID ? `Sender ID '${senderAccountID}'` : 'Sender ID'}
							bind:value={senderAccountID}
							autocomplete="off"
						/>
						<button
							class="btn"
							on:click={(e) => {
								e.preventDefault();
								openAccountSelector('sender');
							}}>Auswählen</button
						>
					</div>
				</fieldset>

				<fieldset>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="label mb-2 mt-2"><span class="label-text">Empfänger</span></label>
					<div class="flex gap-2">
						<input
							type="text"
							class="input input-bordered w-full"
							placeholder={receiverAccount
								? receiverAccount.user?.name || receiverAccount.id
								: 'Empfänger ID'}
							value={receiverAccount ? receiverAccount.user?.name || receiverAccount.id : ''}
							readonly
							autocomplete="off"
						/>
						<button
							class="btn"
							on:click={(e) => {
								e.preventDefault();
								openAccountSelector('receiver');
							}}>Auswählen</button
						>
					</div>
				</fieldset>

				<fieldset>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="label mb-2 mt-2"><span class="label-text">Betrag</span></label>
					<input
						type="number"
						class="input input-bordered w-full"
						placeholder={isEditing ? `Betrag '${amount}'` : 'Betrag'}
						bind:value={amount}
						autocomplete="off"
					/>
				</fieldset>

				<div class="flex flex-row items-center mt-4 mb-2">
					<span class="mr-2">Maximalbetrag festlegen</span>
					<input
						type="checkbox"
						class="checkbox bg-base-100"
						bind:checked={maxAmountEnabled}
						id="maxToggle"
					/>
					{#if maxAmountEnabled}
						<input
							type="number"
							class="input input-bordered w-40 ml-4"
							bind:value={maxAmount}
							autocomplete="off"
						/>
					{/if}
				</div>

				<fieldset>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="label mb-2 mt-2"><span class="label-text">Beschreibung</span></label>
					<div>
						<MarkdownEditor mode="tabs" bind:value={description} {carta} theme="github" />
					</div>
				</fieldset>

				<fieldset>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="label mb-2 mt-2"><span class="label-text">Wochentage</span></label>
					<div class="flex gap-2">
						{#each WEEKDAYS as d}
							<button
								class="btn btn-sm"
								on:click={(e) => {
									e.preventDefault();
									toggleDay(d);
								}}
								class:btn-primary={days.includes(d)}>{d.slice(0, 2)}</button
							>
						{/each}
					</div>
				</fieldset>

				<fieldset>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="label mb-2 mt-2"><span class="label-text">Uhrzeit</span></label>
					<input type="time" class="input input-bordered w-full" bind:value={timeString} />
				</fieldset>

				<fieldset>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="label mb-2 mt-2"><span class="label-text">Zeitzone</span></label>
					<input class="input input-bordered w-full" bind:value={timezone} />
				</fieldset>
			</div>

			<div class="modal-action mt-4">
				<button class="btn btn-primary btn-outline" on:click={() => createEditModal.close()}
					>Schließen</button
				>
				<button
					class="btn btn-success"
					on:click={(e) => {
						e.preventDefault();
						saveRecurring();
					}}>{isEditing ? 'Änderungen speichern' : 'Erstellen'}</button
				>
			</div>
		</div>
		<form method="dialog" class="modal-backdrop"><button>close</button></form>
	</dialog>

	<!-- Delete confirmation -->
	<dialog class="modal" bind:this={deleteConfirmModal} id="deleteConfirm">
		<form method="dialog" class="modal-box">
			<h3 class="font-bold text-lg">Dauerauftrag löschen</h3>
			<p class="py-4">
				Möchtest du diesen Dauerauftrag wirklich löschen? Diese Aktion kann nicht rückgängig gemacht
				werden.
			</p>
			<div class="modal-action">
				<button class="btn" on:click={() => deleteConfirmModal.close()}>Abbrechen</button>
				<button
					class="btn btn-error"
					on:click={(e) => {
						e.preventDefault();
						doDelete();
					}}>Löschen</button
				>
			</div>
		</form>
		<form method="dialog" class="modal-backdrop"><button>close</button></form>
	</dialog>

	<!-- Mass delete Modal -->
	<dialog class="modal" bind:this={massDeleteModal}>
		<div class="modal-box">
			<h3 class="font-bold text-lg">Massenlöschung</h3>
			<p class="py-4">
				Möchtest du {selectedRecurring.size} Dauerauftrag(e) wirklich löschen? Diese Aktion kann nicht
				rückgängig gemacht werden.
			</p>
			<div class="modal-action">
				<button class="btn" on:click={() => massDeleteModal.close()}>Abbrechen</button>
				<button
					class="btn btn-error"
					on:click={(e) => {
						e.preventDefault();
						massDeleteSelected();
					}}>Löschen</button
				>
			</div>
		</div>
		<form method="dialog" class="modal-backdrop"><button>close</button></form>
	</dialog>
</AdminPanel>

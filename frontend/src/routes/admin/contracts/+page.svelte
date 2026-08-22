<script lang="ts">
	import { browser } from '$app/environment';
	import db, { server } from '$lib/connections';
	import cronToGerman from '$lib/cron';
	import AdminPanel from '$modules/AdminPanel.svelte';
	import Currency from '$modules/Currency.svelte';
	import AccountSelectModal from '$modules/modals/AccountSelectModal.svelte';
	import { Pen, Trash2, UsersIcon } from '@lucide/svelte';
	import { onMount } from 'svelte';

	let totalItems = 0;
	let totalPages = 0;
	let contracts = [];
	let groups: any[] = [];
	let page = 1;
	let perPage = 30;
	let searchInput = '';
	let selectedContracts = new Set<string>();

	let editModal: HTMLDialogElement;
	let massCreateModal: HTMLDialogElement;
	let massEditModal: HTMLDialogElement;
	let createModal: HTMLDialogElement;
	let deleteModal: HTMLDialogElement;
	let massDeleteModal: HTMLDialogElement;

	let accountSelectModal: AccountSelectModal;
	let accountSelectTarget:
		| 'create_sender'
		| 'create_receiver'
		| 'edit_sender'
		| 'edit_receiver'
		| 'mass_create_receiver' = 'create_sender';

	let createContractSenderID: string = '';
	let createContractReceiverID: string = '';
	let createContractSenderName: string = '';
	let createContractReceiverName: string = '';
	let createContractAmount: number = 0;
	let createContractTitle: string = '';
	let createContractDescription: string = '';
	let createContractCron: string = '';
	let createContractMaximum: number = 1;
	let createContractExecuted: number = 0;
	let createContractCompleted: boolean = false;

	let massCreateGroupID: string = '';
	let massCreateReceiverID: string = '';
	let massCreateReceiverName: string = '';
	let massCreateTitle: string = '';
	let massCreateDescription: string = '';
	let massCreateAmount: number = 0;
	let massCreateCron: string = '';
	let massCreateMaximum: number = 1;
	let massCreateExecuted: number = 0;
	let massCreateCompleted: boolean = false;

	let editContractID: string = '';
	let editContractSenderID: string = '';
	let editContractReceiverID: string = '';
	let editContractSenderName: string = '';
	let editContractReceiverName: string = '';
	let editContractAmount: number = 0;
	let editContractTitle: string = '';
	let editContractDescription: string = '';
	let editContractCron: string = '';
	let editContractMaximum: number = 1;
	let editContractExecuted: number = 0;
	let editContractCompleted: boolean = false;

	let massEditTitle: string = '';
	let massEditUpdateTitle: boolean = false;
	let massEditDescription: string = '';
	let massEditUpdateDescription: boolean = false;
	let massEditAmount: number = 0;
	let massEditUpdateAmount: boolean = false;
	let massEditCron: string = '';
	let massEditUpdateCron: boolean = false;
	let massEditMaximum: number = 1;
	let massEditUpdateMaximum: boolean = false;
	let massEditExecuted: number = 0;
	let massEditUpdateExecuted: boolean = false;
	let massEditCompleted: boolean = false;
	let massEditUpdateCompleted: boolean = false;

	let deleteContractID: string = '';
	let deleteContractTitle: string = '';
	let deleteContractDescription: string = '';
	let deleteContractSenderName: string = '';
	let deleteContractReceiverName: string = '';

	let attemptedCron: string = '';

	$: selectedMassCreateGroup = groups.find((group) => group.id === massCreateGroupID);
	$: selectedMassCreateGroupUsers = selectedMassCreateGroup?.expand?.users ?? [];

	onMount(async () => {
		if (!browser) return;
		await Promise.all([fetchContracts(), fetchGroups()]);
	});
	async function fetchGroups() {
		const data = await db.collection('admin_groups').getList(1, 200, {
			sort: '-created',
			expand: 'users'
		});
		groups = data.items;
	}
	async function fetchContracts() {
		let data = await db.collection('contracts').getList(page, perPage, {
			filter: searchInput
				? `id~"${searchInput}" || title~"${searchInput}" || description~"${searchInput}"`
				: undefined,
			sort: '-created',
			expand: 'sender,receiver,sender.user,receiver.user'
		});
		contracts = data.items;
		totalItems = data.totalItems;
		totalPages = data.totalPages;
	}
	function truncate(str: string, maxLength: number) {
		if (str.length <= maxLength) return str;
		return str.slice(0, maxLength) + '...';
	}
	function openAccountSelectModal(
		target: 'create_sender' | 'create_receiver' | 'edit_sender' | 'edit_receiver'
	) {
		accountSelectTarget = target;
		accountSelectModal.showModal();
	}
	function handleAccountSelected(account: any) {
		account = account.detail;
		if (accountSelectTarget === 'create_sender') {
			createContractSenderID = account.id;
			createContractSenderName = account.expand.user.name;
		} else if (accountSelectTarget === 'create_receiver') {
			createContractReceiverID = account.id;
			createContractReceiverName = account.expand.user.name;
		} else if (accountSelectTarget === 'mass_create_receiver') {
			massCreateReceiverID = account.id;
			massCreateReceiverName = account.expand.user.name;
		} else if (accountSelectTarget === 'edit_sender') {
			editContractSenderID = account.id;
			editContractSenderName = account.expand.user.name;
		} else if (accountSelectTarget === 'edit_receiver') {
			editContractReceiverID = account.id;
			editContractReceiverName = account.expand.user.name;
		}
	}
	async function updateContract(contractID: string) {
		await db.collection('contracts').update(contractID, {
			sender: editContractSenderID,
			receiver: editContractReceiverID,
			title: editContractTitle,
			description: editContractDescription,
			amount: editContractAmount,
			cron: editContractCron,
			maximum: editContractMaximum,
			executed: editContractExecuted,
			completed: editContractCompleted
		});
		await sendServerUpdate(contractID);
		await fetchContracts();
		editModal.close();
	}
	function openEditModal(contract) {
		editContractID = contract.id;
		editContractSenderID = contract.sender;
		editContractReceiverID = contract.receiver;
		editContractSenderName = contract.expand.sender.expand.user.isCompany
			? contract.expand.sender.expand.user.companyName
			: contract.expand.sender.expand.user.name;
		editContractReceiverName = contract.expand.receiver.expand.user.isCompany
			? contract.expand.receiver.expand.user.companyName
			: contract.expand.receiver.expand.user.name;
		editContractTitle = contract.title;
		editContractDescription = contract.description;
		editContractAmount = contract.amount;
		editContractCron = contract.cron;
		editContractMaximum = contract.maximum;
		editContractExecuted = contract.executed;
		editContractCompleted = contract.completed;
		tryCron();
		editModal.showModal();
	}
	function tryCron() {
		attemptedCron = cronToGerman(editContractCron, false);
	}
	function openCreateModal() {
		createContractSenderID = '';
		createContractReceiverID = '';
		createContractSenderName = '';
		createContractReceiverName = '';
		createContractAmount = 0;
		createContractTitle = '';
		createContractDescription = '';
		createContractCron = '0 12 * * 1-5';
		createContractMaximum = -1;
		createContractExecuted = 0;
		createContractCompleted = false;
		attemptedCron = cronToGerman(createContractCron, false);
		createModal.showModal();
	}
	function openMassCreateModal() {
		massCreateGroupID = '';
		massCreateReceiverID = '';
		massCreateReceiverName = '';
		massCreateTitle = '';
		massCreateDescription = '';
		massCreateAmount = 0;
		massCreateCron = '0 12 * * 1-5';
		massCreateMaximum = -1;
		massCreateExecuted = 0;
		massCreateCompleted = false;
		attemptedCron = cronToGerman(massCreateCron, false);
		massCreateModal.showModal();
	}
	function openMassCreateReceiverModal() {
		accountSelectTarget = 'mass_create_receiver';
		accountSelectModal.showModal();
	}
	async function createContract() {
		let data = await db.collection('contracts').create({
			sender: createContractSenderID,
			receiver: createContractReceiverID,
			title: createContractTitle,
			description: createContractDescription,
			amount: createContractAmount,
			cron: createContractCron,
			maximum: createContractMaximum,
			executed: createContractExecuted,
			completed: createContractCompleted
		});
		await sendServerUpdate(data.id);
		await fetchContracts();
		createModal.close();
	}
	async function createGroupContracts() {
		if (!massCreateGroupID) {
			alert('Bitte wählen Sie eine Gruppe aus.');
			return;
		}
		if (!massCreateReceiverID) {
			alert('Bitte wählen Sie einen Empfänger aus.');
			return;
		}
		if (selectedMassCreateGroupUsers.length === 0) {
			alert('Die ausgewählte Gruppe enthält keine Benutzer.');
			return;
		}

		const senderAccounts = await Promise.all(
			selectedMassCreateGroupUsers.map(async (user) => {
				const accountResult = await db.collection('accounts').getList(1, 1, {
					filter: `user = \"${user.id}\"`,
					expand: 'user'
				});
				return accountResult.items[0] ?? null;
			})
		);

		const validSenderAccounts = senderAccounts.filter((account) => account !== null);
		const missingAccounts = selectedMassCreateGroupUsers.filter(
			(_, index) => senderAccounts[index] === null
		);

		if (validSenderAccounts.length === 0) {
			alert('Für die ausgewählte Gruppe wurden keine Konten gefunden.');
			return;
		}

		if (missingAccounts.length > 0) {
			alert(
				`Für ${missingAccounts.length} Gruppenmitglied${missingAccounts.length === 1 ? '' : 'er'} konnte kein Konto gefunden werden. Diese Mitglieder werden übersprungen.`
			);
		}

		massCreateModal.close();

		await Promise.all(
			validSenderAccounts.map(async (senderAccount: any) => {
				const createdContract = await db.collection('contracts').create({
					sender: senderAccount.id,
					receiver: massCreateReceiverID,
					title: massCreateTitle,
					description: massCreateDescription,
					amount: massCreateAmount,
					cron: massCreateCron,
					maximum: massCreateMaximum,
					executed: massCreateExecuted,
					completed: massCreateCompleted
				});
				await sendServerUpdate(createdContract.id);
			})
		);

		await fetchContracts();
	}
	function toggleSelectAll(event) {
		if (event.target.checked) {
			contracts.forEach((contract) => selectedContracts.add(contract.id));
		} else {
			selectedContracts.clear();
		}
		selectedContracts = new Set(selectedContracts); // Trigger reactivity
	}
	function toggleSelectContract(contractID: string) {
		if (selectedContracts.has(contractID)) {
			selectedContracts.delete(contractID);
		} else {
			selectedContracts.add(contractID);
		}
		selectedContracts = new Set(selectedContracts); // Trigger reactivity
	}
	function openDeleteModal(contract) {
		deleteContractID = contract.id;
		deleteContractTitle = contract.title;
		deleteContractDescription = contract.description;
		deleteContractSenderName = contract.expand.sender.expand.user.isCompany
			? contract.expand.sender.expand.user.companyName
			: contract.expand.sender.expand.user.name;
		deleteContractReceiverName = contract.expand.receiver.expand.user.isCompany
			? contract.expand.receiver.expand.user.companyName
			: contract.expand.receiver.expand.user.name;
		deleteModal.showModal();
	}
	async function deleteContract(contractID: string) {
		await db.collection('contracts').delete(contractID);
		await sendServerUpdate(contractID);
		await fetchContracts();
		deleteModal.close();
	}
	async function sendServerUpdate(contractID: string) {
		const data = await fetch(`${server}/api/sync-contract?id=${contractID}`, {
			method: 'POST',
			headers: {
				Authorization: `Bearer ${db.authStore.token}`,
				'Content-Type': 'application/json'
			}
		});
		if (!data.ok) {
			console.error('Failed to sync contract with server');
			alert(
				'Fehler beim Synchronisieren des Vertrags mit dem Server. Bitte aktualisieren Sie die Seite, um die neuesten Daten zu sehen.'
			);
		}
	}
	function showMassDeleteModal() {
		if (selectedContracts.size === 0) {
			alert('Bitte wählen Sie mindestens einen Vertrag aus, um fortzufahren.');
			return;
		}
		massDeleteModal.showModal();
	}
	async function massDeleteContracts() {
		await Promise.all(
			Array.from(selectedContracts).map(async (id) => {
				await db.collection('contracts').delete(id);
				await sendServerUpdate(id);
			})
		);
		await fetchContracts();
		massDeleteModal.close();
	}
	async function massUpdateContracts() {
		if (selectedContracts.size === 0) {
			alert('Bitte wählen Sie mindestens einen Vertrag aus, um fortzufahren.');
			return;
		}
		await Promise.all(
			Array.from(selectedContracts).map(async (id) => {
				let updateData: any = {};
				if (massEditUpdateTitle) updateData.title = massEditTitle;
				if (massEditUpdateDescription) updateData.description = massEditDescription;
				if (massEditUpdateAmount) updateData.amount = massEditAmount;
				if (massEditUpdateCron) updateData.cron = massEditCron;
				if (massEditUpdateMaximum) updateData.maximum = massEditMaximum;
				if (massEditUpdateExecuted) updateData.executed = massEditExecuted;
				if (massEditUpdateCompleted) updateData.completed = massEditCompleted;
				await db.collection('contracts').update(id, updateData);
				await sendServerUpdate(id);
			})
		);
		await fetchContracts();
		massEditModal.close();
	}
	function showMassEditModal() {
		if (selectedContracts.size === 0) {
			alert('Bitte wählen Sie mindestens einen Vertrag aus, um fortzufahren.');
			return;
		}
		massEditTitle = '';
		massEditUpdateTitle = false;
		massEditDescription = '';
		massEditUpdateDescription = false;
		massEditAmount = 0;
		massEditUpdateAmount = false;
		massEditCron = '';
		massEditUpdateCron = false;
		massEditMaximum = 1;
		massEditUpdateMaximum = false;
		massEditExecuted = 0;
		massEditUpdateExecuted = false;
		massEditCompleted = false;
		massEditUpdateCompleted = false;
		tryCron();
		massEditModal.showModal();
	}
</script>

<AdminPanel
	title="Verträge"
	description="Verträge verwalten und erstellen und einsehen."
	showCreateButton={true}
	showMassDeleteButton={true}
	showMassEditButton={true}
	showMassCreateButton={true}
	createButtonText="Neuen Vertrag erstellen"
	massCreateButtonText="Verträge für Gruppe erstellen"
	{totalItems}
	on:create={openCreateModal}
	on:massCreate={openMassCreateModal}
	on:massDelete={showMassDeleteModal}
	on:massEdit={showMassEditModal}
	selectedItems={selectedContracts.size}
>
	<svelte:fragment slot="filter">
		<div class="flex gap-2 flex-row flex-wrap items-center">
			<input
				type="text"
				placeholder="Nach Nutzer, Konto-ID oder Vertrags-ID suchen…"
				class="input input-bordered flex-1 min-w-48"
				autocomplete="off"
				bind:value={searchInput}
				on:input={fetchContracts}
			/>
			<select
				class="select select-bordered"
				bind:value={perPage}
				on:change={() => fetchContracts()}
			>
				<option value={10}>10 / Seite</option>
				<option value={15}>15 / Seite</option>
				<option value={30}>30 / Seite</option>
				<option value={50}>50 / Seite</option>
				<option value={100}>100 / Seite</option>
				<option value={200}>200 / Seite</option>
				<option value={500}>500 / Seite</option>
				<option value={1000}>1000 / Seite</option>
				<option value={2000}>2000 / Seite</option>
				<option value={5000}>5000 / Seite</option>
			</select>
		</div>
	</svelte:fragment>
	<div>
		<table class="table table-zebra w-full">
			<thead>
				<tr>
					<th
						><input
							type="checkbox"
							class="checkbox"
							on:change={toggleSelectAll}
							checked={selectedContracts.size === contracts.length && contracts.length > 0}
						/></th
					>
					<th>ID</th>
					<th>Parteien</th>
					<th>Titel</th>
					<th>Beschreibung</th>
					<th>Geldmenge</th>
					<th>CRON</th>
					<th>Status</th>
					<th>Abgeschlossen</th>
					<th>Aktionen</th>
				</tr>
			</thead>
			<tbody>
				{#each contracts as contract}
					<tr>
						<td
							><input
								type="checkbox"
								class="checkbox"
								checked={selectedContracts.has(contract.id)}
								on:change={() => {
									toggleSelectContract(contract.id);
								}}
							/></td
						>
						<td class="font-mono">{contract.id}</td>
						<td>
							<div class="flex flex-col gap-1">
								<div class="flex items-center gap-2">
									<span class="font-bold"
										>{contract.expand.sender.expand.user.isCompany
											? contract.expand.sender.expand.user.companyName
											: contract.expand.sender.expand.user.name}</span
									>
									<span class="text-sm text-gray-500">({contract.sender})</span>
								</div>
								<div class="flex items-center gap-2">
									<span class="font-bold"
										>{contract.expand.receiver.expand.user.isCompany
											? contract.expand.receiver.expand.user.companyName
											: contract.expand.receiver.expand.user.name}</span
									>
									<span class="text-sm text-gray-500">({contract.receiver})</span>
								</div>
							</div>
						</td>
						<td>{contract.title}</td>
						<td>{truncate(contract.description, 100)}</td>
						<td
							><div class="flex flex-row items-center gap-2">
								{contract.amount}<Currency></Currency>
							</div></td
						>
						<td>{contract.cron}</td>
						<td
							><div class="flex flex-row items-center gap-2">
								{contract.executed}/{contract.maximum != -1 ? contract.maximum : 'Unbegrenzt'}
							</div></td
						>
						<td
							>{#if contract.completed}<span class="badge badge-success">Abgeschlossen</span
								>{:else}<span class="badge badge-error">Laufend</span>{/if}</td
						>
						<td>
							<div class="flex flex-row gap-2">
								<button class="btn btn-primary btn-sm" on:click={() => openEditModal(contract)}
									><Pen></Pen></button
								>
								<button class="btn btn-error btn-sm" on:click={() => openDeleteModal(contract)}
									><Trash2></Trash2></button
								>
							</div>
						</td>
					</tr>
				{/each}
			</tbody>
		</table>

		<!-- Pagination controls would go here -->
	</div>
</AdminPanel>

<!-- Mass Create Contract Dialog -->
<dialog class="modal" bind:this={massCreateModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Verträge für Gruppe erstellen</h3>

		<fieldset class="mt-4">
			<label for="group" class="block text-sm font-medium">Gruppe</label>
			<select id="group" class="select select-bordered w-full" bind:value={massCreateGroupID}>
				<option value="">Gruppe auswählen</option>
				{#each groups as group}
					<option value={group.id}>
						{group.name} ({group.expand?.users?.length ?? 0} Benutzer)
					</option>
				{/each}
			</select>
		</fieldset>

		<button
			class="btn btn-primary btn-outline w-full mt-2"
			on:click={openMassCreateReceiverModal}
		>
			<UsersIcon class="w-4 h-4" />
			{massCreateReceiverID
				? `Empfänger: ${massCreateReceiverName} (${massCreateReceiverID})`
				: 'Empfänger auswählen'}
		</button>

		<fieldset class="mt-4">
			<label for="mass-create-title" class="block text-sm font-medium">Titel</label>
			<input
				id="mass-create-title"
				type="text"
				class="input input-bordered w-full"
				bind:value={massCreateTitle}
			/>
		</fieldset>

		<fieldset class="mt-4">
			<label for="mass-create-description" class="block text-sm font-medium">Beschreibung</label>
			<textarea
				id="mass-create-description"
				class="textarea textarea-bordered w-full"
				bind:value={massCreateDescription}
			></textarea>
		</fieldset>

		<fieldset class="mt-4">
			<label for="mass-create-amount" class="block text-sm font-medium">Geldmenge</label>
			<div class="flex flex-row items-center gap-2">
				<input
					id="mass-create-amount"
					type="number"
					class="input input-bordered w-full"
					bind:value={massCreateAmount}
				/>
				<Currency size={36}></Currency>
			</div>
		</fieldset>

		<fieldset class="mt-4">
			<label for="mass-create-cron" class="block text-sm font-medium">CRON-Ausdruck</label>
			<input
				id="mass-create-cron"
				type="text"
				class="input input-bordered w-full"
				bind:value={massCreateCron}
				on:input={() =>
					(attemptedCron = massCreateCron ? cronToGerman(massCreateCron, false) : '')}
			/>
		</fieldset>

		<p class="text-sm opacity-70 mt-2">{attemptedCron}</p>

		<fieldset class="mt-4">
			<label for="mass-create-maximum" class="block text-sm font-medium"
				>Maximale Ausführungen (-1 für unbegrenzt)</label
			>
			<input
				id="mass-create-maximum"
				type="number"
				class="input input-bordered w-full"
				bind:value={massCreateMaximum}
			/>
		</fieldset>

		<fieldset class="mt-4">
			<label for="mass-create-executed" class="block text-sm font-medium">Bereits ausgeführt</label>
			<input
				id="mass-create-executed"
				type="number"
				class="input input-bordered w-full"
				bind:value={massCreateExecuted}
			/>
		</fieldset>

		<fieldset class="mt-4">
			<label class="flex items-center gap-2 cursor-pointer select-none">
				<input type="checkbox" class="checkbox" bind:checked={massCreateCompleted} />
				Verträge als abgeschlossen markieren
			</label>
		</fieldset>

		<div class="modal-action">
			<button class="btn btn-primary btn-outline" on:click={() => massCreateModal.close()}
				>Abbrechen</button
			>
			<button class="btn btn-success" on:click={createGroupContracts}>Erstellen</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Create Contract Dialog -->
<dialog class="modal" bind:this={createModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Vertrag erstellen</h3>

		<button
			class="btn btn-primary btn-outline w-full mt-2"
			on:click={() => openAccountSelectModal('create_sender')}
		>
			<UsersIcon class="w-4 h-4" />
			{createContractSenderID
				? `Sender: ${createContractSenderName} (${createContractSenderID})`
				: 'Sender auswählen'}
		</button>
		<button
			class="btn btn-primary btn-outline w-full mt-2"
			on:click={() => openAccountSelectModal('create_receiver')}
		>
			<UsersIcon class="w-4 h-4" />
			{createContractReceiverID
				? `Empfänger: ${createContractReceiverName} (${createContractReceiverID})`
				: 'Empfänger auswählen'}
		</button>

		<fieldset class="mt-4">
			<label for="title" class="block text-sm font-medium">Titel</label>
			<input
				id="title"
				type="text"
				class="input input-bordered w-full"
				bind:value={createContractTitle}
			/>
		</fieldset>

		<fieldset class="mt-4">
			<label for="description" class="block text-sm font-medium">Beschreibung</label>
			<textarea
				id="description"
				class="textarea textarea-bordered w-full"
				bind:value={createContractDescription}
			></textarea>
		</fieldset>

		<fieldset class="mt-4">
			<label for="amount" class="block text-sm font-medium">Geldmenge</label>
			<div class="flex flex-row items-center gap-2">
				<input
					id="amount"
					type="number"
					class="input input-bordered w-full"
					bind:value={createContractAmount}
				/>
				<Currency size={36}></Currency>
			</div>
		</fieldset>

		<fieldset class="mt-4">
			<label for="cron" class="block text-sm font-medium">CRON-Ausdruck</label>
			<input
				id="cron"
				type="text"
				class="input input-bordered w-full"
				bind:value={createContractCron}
				on:input={() =>
					(attemptedCron = createContractCron ? cronToGerman(createContractCron, false) : '')}
			/>
		</fieldset>

		<p class="text-sm opacity-70 mt-2">{attemptedCron}</p>

		<fieldset class="mt-4">
			<label for="maximum" class="block text-sm font-medium"
				>Maximale Ausführungen (-1 für unbegrenzt)</label
			>
			<input
				id="maximum"
				type="number"
				class="input input-bordered w-full"
				bind:value={createContractMaximum}
			/>
		</fieldset>

		<fieldset class="mt-4">
			<label for="executed" class="block text-sm font-medium">Bereits ausgeführt</label>
			<input
				id="executed"
				type="number"
				class="input input-bordered w-full"
				bind:value={createContractExecuted}
			/>
		</fieldset>

		<fieldset class="mt-4">
			<label class="flex items-center gap-2 cursor-pointer select-none">
				<input type="checkbox" class="checkbox" bind:checked={createContractCompleted} />
				Vertrag als abgeschlossen markieren
			</label>
		</fieldset>

		<div class="modal-action">
			<button class="btn btn-primary btn-outline" on:click={() => createModal.close()}
				>Abbrechen</button
			>
			<button class="btn btn-success" on:click={createContract}>Erstellen</button>
		</div>
	</div>
	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Edit Contract Dialog -->
<dialog class="modal" bind:this={editModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Vertrag bearbeiten</h3>

		<button
			class="btn btn-primary btn-outline w-full mt-2"
			on:click={() => openAccountSelectModal('create_sender')}
		>
			<UsersIcon class="w-4 h-4" />
			{editContractSenderID
				? `Sender: ${editContractSenderName} (${editContractSenderID})`
				: 'Sender auswählen'}
		</button>

		<button
			class="btn btn-primary btn-outline w-full mt-2"
			on:click={() => openAccountSelectModal('create_receiver')}
		>
			<UsersIcon class="w-4 h-4" />
			{editContractReceiverID
				? `Empfänger: ${editContractReceiverName} (${editContractReceiverID})`
				: 'Empfänger auswählen'}
		</button>

		<fieldset class="mt-4">
			<label for="title" class="block text-sm font-medium">Titel</label>
			<input
				id="title"
				type="text"
				class="input input-bordered w-full"
				bind:value={editContractTitle}
			/>
		</fieldset>

		<fieldset class="mt-4">
			<label for="description" class="block text-sm font-medium">Beschreibung</label>
			<textarea
				id="description"
				class="textarea textarea-bordered w-full"
				bind:value={editContractDescription}
			></textarea>
		</fieldset>

		<fieldset class="mt-4">
			<label for="amount" class="block text-sm font-medium">Geldmenge</label>
			<div class="flex flex-row items-center gap-2">
				<input
					id="amount"
					type="number"
					class="input input-bordered w-full"
					bind:value={editContractAmount}
				/>
				<Currency size={36}></Currency>
			</div>
		</fieldset>

		<fieldset class="mt-4">
			<label for="cron" class="block text-sm font-medium">CRON-Ausdruck</label>
			<input
				id="cron"
				type="text"
				class="input input-bordered w-full"
				bind:value={editContractCron}
				on:input={() =>
					(attemptedCron = editContractCron ? cronToGerman(editContractCron, false) : '')}
			/>
		</fieldset>

		<p class="text-sm opacity-70 mt-2">{attemptedCron}</p>

		<fieldset class="mt-4">
			<label for="maximum" class="block text-sm font-medium"
				>Maximale Ausführungen (-1 für unbegrenzt)</label
			>
			<input
				id="maximum"
				type="number"
				class="input input-bordered w-full"
				bind:value={editContractMaximum}
			/>
		</fieldset>

		<fieldset class="mt-4">
			<label for="executed" class="block text-sm font-medium">Bereits ausgeführt</label>
			<input
				id="executed"
				type="number"
				class="input input-bordered w-full"
				bind:value={editContractExecuted}
			/>
		</fieldset>

		<fieldset class="mt-4">
			<label class="flex items-center gap-2 cursor-pointer select-none">
				<input type="checkbox" class="checkbox" bind:checked={editContractCompleted} />
				Vertrag als abgeschlossen markieren
			</label>
		</fieldset>

		<div class="modal-action">
			<button class="btn btn-primary btn-outline" on:click={() => editModal.close()}
				>Abbrechen</button
			>
			<button class="btn btn-error" on:click={() => updateContract(editContractID)}
				>Aktualisieren</button
			>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Mass Edit Modal -->
<dialog class="modal" bind:this={massEditModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Mehrere Verträge bearbeiten</h3>

		<fieldset class="mt-4">
			<label for="title" class="block text-sm font-medium">Titel</label>
			<div class="flex items-center gap-2 mt-1">
				<input type="checkbox" class="checkbox" bind:checked={massEditUpdateTitle} />
				<input
					id="title"
					type="text"
					class="input input-bordered w-full"
					bind:value={massEditTitle}
					placeholder="Neuer Titel für ausgewählte Verträge"
					disabled={!massEditUpdateTitle}
				/>
			</div>
		</fieldset>

		<fieldset class="mt-4">
			<label for="description" class="block text-sm font-medium">Beschreibung</label>
			<div class="flex items-center gap-2 mt-1">
				<input type="checkbox" class="checkbox" bind:checked={massEditUpdateDescription} />
				<input
					id="description"
					type="text"
					class="input input-bordered w-full"
					bind:value={massEditDescription}
					placeholder="Neue Beschreibung für ausgewählte Verträge"
					disabled={!massEditUpdateDescription}
				/>
			</div>
		</fieldset>

		<fieldset class="mt-4">
			<label for="amount" class="block text-sm font-medium">Geldmenge</label>
			<div class="flex items-center gap-2 mt-1">
				<input type="checkbox" class="checkbox" bind:checked={massEditUpdateAmount} />
				<div class="flex flex-row items-center gap-2 w-full">
					<input
						id="amount"
						type="number"
						class="input input-bordered w-full"
						bind:value={massEditAmount}
						placeholder="Neue Geldmenge für ausgewählte Verträge"
						disabled={!massEditUpdateAmount}
					/>
					<Currency size={36}></Currency>
				</div>
			</div>
		</fieldset>

		<fieldset class="mt-4">
			<label for="cron" class="block text-sm font-medium">CRON-Ausdruck</label>
			<div class="flex items-center gap-2 mt-1">
				<input type="checkbox" class="checkbox" bind:checked={massEditUpdateCron} />
				<input
					id="cron"
					type="text"
					class="input input-bordered w-full"
					bind:value={massEditCron}
					placeholder="Neuer CRON-Ausdruck für ausgewählte Verträge"
					disabled={!massEditUpdateCron}
					on:input={() =>
						(attemptedCron = massEditUpdateCron ? cronToGerman(massEditCron, false) : '')}
				/>
			</div>
		</fieldset>

		<p class="text-sm opacity-70 mt-2">{attemptedCron}</p>

		<fieldset class="mt-4">
			<label for="maximum" class="block text-sm font-medium"
				>Maximale Ausführungen (-1 für unbegrenzt)</label
			>
			<div class="flex items-center gap-2 mt-1">
				<input type="checkbox" class="checkbox" bind:checked={massEditUpdateMaximum} />
				<input
					id="maximum"
					type="number"
					class="input input-bordered w-full"
					bind:value={massEditMaximum}
					placeholder="Neues Maximum für ausgewählte Verträge"
					disabled={!massEditUpdateMaximum}
				/>
			</div>
		</fieldset>

		<fieldset class="mt-4">
			<label for="executed" class="block text-sm font-medium">Bereits ausgeführt</label>
			<div class="flex items-center gap-2 mt-1">
				<input type="checkbox" class="checkbox" bind:checked={massEditUpdateExecuted} />
				<input
					id="executed"
					type="number"
					class="input input-bordered w-full"
					bind:value={massEditExecuted}
					placeholder="Neue Anzahl bereits ausgeführter Ausführungen für ausgewählte Verträge"
					disabled={!massEditUpdateExecuted}
				/>
			</div>
		</fieldset>

		<fieldset class="mt-4">
			<label class="flex items-center gap-2 cursor-pointer select-none">
				<input type="checkbox" class="checkbox" bind:checked={massEditUpdateCompleted} />
				<input
					type="checkbox"
					class="checkbox"
					bind:checked={massEditCompleted}
					disabled={!massEditUpdateCompleted}
				/>
				Verträge als abgeschlossen markieren
			</label>
		</fieldset>

		<div class="modal-action">
			<button class="btn btn-primary btn-outline" on:click={() => massEditModal.close()}
				>Abbrechen</button
			>
			<button class="btn btn-error" on:click={massUpdateContracts}>Aktualisieren</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Contract Delete Modal -->
<dialog class="modal" bind:this={deleteModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Vertrag löschen</h3>
		<p class="py-4">
			Möchten Sie den Vertrag "<strong>{deleteContractTitle}</strong>" wirklich löschen?
		</p>
		<p class="py-2"><strong>ID:</strong> {deleteContractID}</p>
		<p class="py-2"><strong>Beschreibung:</strong> {deleteContractDescription}</p>
		<p class="py-2"><strong>Sender:</strong> {deleteContractSenderName}</p>
		<p class="py-2"><strong>Empfänger:</strong> {deleteContractReceiverName}</p>
		<div class="modal-action">
			<button class="btn btn-primary btn-outline" on:click={() => deleteModal.close()}
				>Abbrechen</button
			>
			<button class="btn btn-error" on:click={() => deleteContract(deleteContractID)}
				>Löschen</button
			>
		</div>
	</div>
	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Mass Delete Modal -->
<dialog class="modal" bind:this={massDeleteModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Mehrere Verträge löschen</h3>
		<p class="py-4">Möchten Sie die ausgewählten Verträge wirklich löschen?</p>
		<div class="modal-action">
			<button class="btn btn-primary btn-outline" on:click={() => massDeleteModal.close()}
				>Abbrechen</button
			>
			<button class="btn btn-error" on:click={massDeleteContracts}>Löschen</button>
		</div>
	</div>
	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>
<!-- Account Select Modal -->
<AccountSelectModal
	bind:this={accountSelectModal}
	on:select={handleAccountSelected}
	adminMode={true}
/>

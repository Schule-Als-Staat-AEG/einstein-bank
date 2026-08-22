<script lang="ts">
	import db from '$lib/connections';
	import { navigateToNotifications } from '$lib/notifications';
	import AdminPanel from '$modules/AdminPanel.svelte';
	import { Pen, Trash } from '@lucide/svelte';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';

	let page = 1;
	let perPage = 30;
	let pages = 1;
	let filter = '';
	let loading = true;
	let totalItems = 0;

	let notifications: any[] = [];
	let selectedNotifications: Set<string> = new Set();

	let userInsertLocation: string = '';
	let users: any[] = [];
	let userFilter: string = '';
	let userPages = 1;
	let userPage = 1;
	let userPerPage = 50;

	let createModal: HTMLDialogElement;
	let editModal: HTMLDialogElement;
	let deleteConfirmModal: HTMLDialogElement;
	let bulkDeleteConfirmModal: HTMLDialogElement;
	let manageUsersModal: HTMLDialogElement;

	let createTitle = '';
	let createContent = '';
	let createLink = '';
	let createUserId = '';
	let createIsRead = false;

	let editNotificationId = '';
	let editTitle = '';
	let editContent = '';
	let editLink = '';
	let editUserId = '';
	let editIsRead = false;

	let deleteNotificationId = '';

	// Notify all
	let notifyAllModal: HTMLDialogElement;
	let allUsers: any[] = [];
	let notifyAllTitle = '';
	let notifyAllContent = '';
	let notifyAllLink = '';
	let notifyAllIsRead = false;

	onMount(async () => {
		if (!browser) return;
		await fetchNotifications();
	});

	async function fetchAllUsers() {
		const result = await db.collection('users').getFullList({
			fields: 'id'
		});
		allUsers = result;
	}

	// Fetch Functions
	async function fetchNotifications() {
		loading = true;
		const result = await db.collection('notifications').getList(page, perPage, {
			sort: '+isRead,-created',
			filter: `content ~ "${filter}" || title ~ "${filter}" || user.name ~ "${filter}"`,
			expand: 'user'
		});
		pages = result.totalPages;
		notifications = result.items;
		loading = false;
	}
	function goToPage(newPage: number) {
		if (newPage < 1 || newPage > pages) return;
		page = newPage;
		fetchNotifications();
	}
	async function fetchUsers() {
		const result = await db.collection('users').getList(userPage, userPerPage, {
			sort: '+name',
			filter: `name ~ "${userFilter}"`
		});
		userPages = result.totalPages;
		totalItems = result.totalItems || 0;
		users = result.items;
	}

	// Dialog Functions
	function openCreateModal() {
		// Reset form fields
		createTitle = '';
		createContent = '';
		createLink = '';
		createUserId = '';
		createIsRead = false;
		createModal.showModal();
	}
	function openEditModal(notification: any) {
		// You can set some state here if needed
		editNotificationId = notification.id;
		editTitle = notification.title;
		editContent = notification.content;
		editLink = notification.link;
		editUserId = notification.user;
		editIsRead = notification.isRead;
		editModal.showModal();
	}
	function openDeleteConfirmModal(notification: any) {
		// You can set some state here if needed
		deleteNotificationId = notification.id;
		deleteConfirmModal.showModal();
	}
	function openBulkDeleteConfirmModal() {
		// You can set some state here if needed
		bulkDeleteConfirmModal.showModal();
	}
	function openSelectUserModal(location: string) {
		fetchUsers();
		userInsertLocation = location;
		manageUsersModal.showModal();
	}

	// Action Functions
	function setUser(id: string) {
		if (userInsertLocation === 'create') {
			createUserId = id;
		} else if (userInsertLocation === 'edit') {
			editUserId = id;
		}
		manageUsersModal.close();
	}

	function createNotification() {
		createModal.close();
		db.collection('notifications')
			.create({
				title: createTitle,
				content: createContent,
				link: createLink,
				user: createUserId,
				isRead: createIsRead
			})
			.then(() => {
				fetchNotifications();
			});
	}
	function editNotification() {
		editModal.close();
		db.collection('notifications')
			.update(editNotificationId, {
				title: editTitle,
				content: editContent,
				link: editLink,
				user: editUserId,
				isRead: editIsRead
			})
			.then(() => {
				fetchNotifications();
			});
	}
	function deleteNotification() {
		deleteConfirmModal.close();
		db.collection('notifications')
			.delete(deleteNotificationId, { requestKey: null })
			.then(() => {
				fetchNotifications();
			});
	}
	function bulkDeleteNotifications() {
		bulkDeleteConfirmModal.close();
		Promise.all(
			Array.from(selectedNotifications).map(async (id) => {
				await db.collection('notifications').delete(id, { requestKey: null });
			})
		).then(() => {
			selectedNotifications.clear();
			fetchNotifications();
		});
	}
	function toggleSelectNotification(id: string) {
		if (selectedNotifications.has(id)) {
			selectedNotifications.delete(id);
		} else {
			selectedNotifications.add(id);
		}
		selectedNotifications = new Set(selectedNotifications);
	}
	function toggleSelectAll() {
		if (selectedNotifications.size === notifications.length) {
			selectedNotifications.clear();
		} else {
			notifications.forEach((notification) => {
				selectedNotifications.add(notification.id);
			});
		}
		selectedNotifications = new Set(selectedNotifications);
	}
	function navigateTo(link: string) {
		navigateToNotifications(link);
	}
	function openNotifyAllModal() {
		notifyAllTitle = '';
		notifyAllContent = '';
		notifyAllLink = '';
		notifyAllIsRead = false;
		fetchAllUsers();
		notifyAllModal.showModal();
	}
	function notifyAll() {
		notifyAllModal.close();
		Promise.all(
			allUsers.map((user) => {
				return db.collection('notifications').create(
					{
						title: notifyAllTitle,
						content: notifyAllContent,
						link: notifyAllLink,
						user: user.id,
						isRead: notifyAllIsRead
					},
					{ requestKey: null }
				);
			})
		).then(() => {
			fetchNotifications();
		});
	}
</script>

<title>Admin - Benachrichtigungen</title>
<AdminPanel
	title="Benachrichtigungen"
	description="Verwaltung aller Benachrichtigungen"
	showCreateButton={true}
	showMassCreateButton={true}
	showMassDeleteButton={true}
	selectedItems={selectedNotifications.size}
	{totalItems}
	on:create={() => openCreateModal()}
	on:massDelete={() => openBulkDeleteConfirmModal()}
	on:massCreate={() => openNotifyAllModal()}
	massCreateButtonText="Alle benachrichtigen"
>
	<svelte:fragment slot="filter">
		<div class="flex gap-2 justify-between items-center w-full">
			<input
				type="text"
				placeholder="Suchen..."
				class="input input-bordered w-full"
				autocomplete="off"
				bind:value={filter}
				on:input={() => {
					page = 1;
					fetchNotifications();
				}}
			/>
			<select
				class="select select-bordered"
				bind:value={perPage}
				on:change={() => {
					page = 1;
					fetchNotifications();
				}}
			>
				<option value="15">15 / Seite</option>
				<option value="30" selected>30 / Seite</option>
				<option value="50">50 / Seite</option>
				<option value="100">100 / Seite</option>
				<option value="200">200 / Seite</option>
				<option value="500">500 / Seite</option>
				<option value="1000">1000 / Seite</option>
				<option value="2000">2000 / Seite</option>
				<option value="5000">5000 / Seite</option>
			</select>

			<div class="flex-1"></div>
		</div>
		{#if loading}
			<div class="text-sm opacity-70">Lade Benachrichtigungen…</div>
		{/if}
	</svelte:fragment>

	{#if !loading}
		<table class="table">
			<!-- head -->
			<thead>
				<tr>
					<th>
						<label>
							<input
								type="checkbox"
								class="checkbox bg-base-100"
								on:change={toggleSelectAll}
								checked={selectedNotifications.size === notifications.length}
							/>
						</label>
					</th>
					<th>Nutzer</th>
					<th>Titel</th>
					<th>Inhalt</th>
					<th>Gelesen</th>
					<th>Erstellt</th>
					<th>Aktionen</th>
				</tr>
			</thead>
			<tbody>
				{#each notifications as notification}
					<tr>
						<th>
							<label>
								<input
									type="checkbox"
									class="checkbox bg-base-100"
									on:change={() => toggleSelectNotification(notification.id)}
									checked={selectedNotifications.has(notification.id)}
								/>
							</label>
						</th>
						<td>
							<div class="flex flex-col gap-1">
								<div class="font-bold">{notification.expand.user.name}</div>
								<div class="text-sm opacity-50 badge badge-sm badge-ghost">{notification.user}</div>
							</div>
						</td>
						<td>
							{notification.title}
						</td>
						<td>{@html notification.content}</td>
						<td>
							{#if notification.isRead}
								<span class="badge badge-success badge-sm">Gelesen</span>
							{:else}
								<span class="badge badge-error badge-sm">Ungelesen</span>
							{/if}
						</td>
						<td>
							{new Date(notification.created).toLocaleString()}
						</td>
						<th>
							<div class="flex flex-row gap-2">
								<button class="btn btn-ghost btn-xs" on:click={() => openEditModal(notification)}
									><Pen></Pen></button
								>
								<button
									class="btn btn-ghost btn-xs btn-error"
									on:click={() => openDeleteConfirmModal(notification)}><Trash></Trash></button
								>
							</div>
						</th>
					</tr>
				{/each}
			</tbody>
		</table>
	{/if}

	<div class="flex justify-center mt-4">
		<div class="join">
			<button class="join-item btn" on:click={() => goToPage(page - 1)} disabled={page <= 1}>
				«
			</button>
			<button class="join-item btn btn-ghost">
				Seite <span class="font-mono mx-1">{page}</span> / <span class="font-mono">{pages}</span>
			</button>
			<button class="join-item btn" on:click={() => goToPage(page + 1)} disabled={page >= pages}>
				»
			</button>
		</div>
	</div>
</AdminPanel>

<!-- Select User Modal -->
<dialog bind:this={manageUsersModal} class="modal">
	<form method="dialog" class="modal-box w-11/12 max-w-5xl">
		<h3 class="font-bold text-lg mb-4">Nutzer auswählen</h3>
		<input
			type="text"
			placeholder="Suchen..."
			class="input input-bordered w-full"
			bind:value={userFilter}
			on:input={() => fetchUsers()}
		/>
		<div class="overflow-x-auto mt-4">
			<table class="table">
				<!-- head -->
				<thead>
					<tr>
						<th>Nutzername</th>
						<th>E-Mail</th>
					</tr>
				</thead>
				<tbody>
					{#each users as user}
						<tr class="hover:bg-base-300 cursor-pointer" on:click={() => setUser(user.id)}>
							<td>
								<div class="flex flex-col gap-1">
									<div class="font-bold">{user.name}</div>
									<div class="text-sm opacity-50 badge badge-sm badge-ghost">{user.id}</div>
								</div>
							</td>
							<td>{user.email}</td>
						</tr>
					{/each}
				</tbody>
			</table>
			<div class="flex justify-center mt-4">
				<div class="join">
					{#if userPage > 1}
						<button
							class="join-item btn"
							on:click={() => {
								userPage -= 1;
								fetchUsers();
							}}>«</button
						>
					{:else}
						<button class="join-item btn btn-disabled">«</button>
					{/if}
					<button class="join-item btn">Seite {userPage}</button>
					{#if userPage < userPages}
						<button
							class="join-item btn"
							on:click={() => {
								userPage += 1;
								fetchUsers();
							}}>»</button
						>
					{:else}
						<button class="join-item btn btn-disabled">»</button>
					{/if}
				</div>
			</div>
		</div>
		<div class="modal-action">
			<button class="btn btn-outline btn-secondary" on:click={() => manageUsersModal.close()}
				>Schließen</button
			>
		</div>
	</form>
</dialog>

<!-- Create Notification Modal -->
<dialog id="createNotificationModal" class="modal" bind:this={createModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold mb-2">Neue Benachrichtigung erstellen</h3>
		{#if createUserId}
			<button class="btn w-full btn-primary mb-2" on:click={() => openSelectUserModal('create')}
				>Nutzer ändern (Aktuell: {createUserId})</button
			>
		{:else}
			<button class="btn w-full btn-primary mb-2" on:click={() => openSelectUserModal('create')}
				>Nutzer auswählen</button
			>
		{/if}
		<input
			type="text"
			placeholder="Titel"
			class="input input-bordered w-full mb-2"
			bind:value={createTitle}
		/>
		<textarea
			placeholder="Inhalt (HTML erlaubt -> Svelte @html)"
			class="textarea textarea-bordered w-full mb-2"
			bind:value={createContent}
		></textarea>
		<input
			type="text"
			placeholder="Link (optional)"
			class="input input-bordered w-full mb-2"
			bind:value={createLink}
		/>
		<label class="flex w-full justify-between gap-2 mb-4">
			<span>Als gelesen markieren</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={createIsRead} />
		</label>
		<div class="modal-action">
			<button class="btn btn-outline btn-secondary" on:click={() => createModal.close()}
				>Abbrechen</button
			>
			<button class="btn btn-primary" on:click={createNotification}>Erstellen</button>
		</div>
	</div>
	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Edit Notification Modal -->
<dialog id="editNotificationModal" class="modal" bind:this={editModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold mb-2">Benachrichtigung bearbeiten</h3>
		{#if editUserId}
			<button class="btn w-full btn-primary mb-2" on:click={() => openSelectUserModal('edit')}
				>Nutzer ändern (Aktuell: {editUserId})</button
			>
		{:else}
			<button class="btn w-full btn-primary mb-2" on:click={() => openSelectUserModal('edit')}
				>Nutzer auswählen</button
			>
		{/if}
		<input
			type="text"
			placeholder="Titel"
			class="input input-bordered w-full mb-2"
			bind:value={editTitle}
		/>
		<textarea
			placeholder="Inhalt (HTML erlaubt -> Svelte @html)"
			class="textarea textarea-bordered w-full mb-2"
			bind:value={editContent}
		></textarea>
		<input
			type="text"
			placeholder="Link (optional)"
			class="input input-bordered w-full mb-2"
			bind:value={editLink}
		/>
		<label class="flex w-full justify-between gap-2 mb-4">
			<span>Als gelesen markieren</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={editIsRead} />
		</label>
		<div class="modal-action">
			<button class="btn btn-outline btn-secondary" on:click={() => editModal.close()}
				>Abbrechen</button
			>
			<button class="btn btn-primary" on:click={editNotification}>Speichern</button>
		</div>
	</div>
	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Delete Confirmation Modal -->
<dialog id="deleteConfirmModal" class="modal" bind:this={deleteConfirmModal}>
	<div class="modal-box">
		<h3 class="font-bold text-lg mb-4">Benachrichtigung löschen</h3>
		<p>
			Sind Sie sicher, dass Sie diese Benachrichtigung löschen möchten? Diese Aktion kann nicht
			rückgängig gemacht werden.
		</p>
		<div class="modal-action">
			<button class="btn btn-outline btn-secondary" on:click={() => deleteConfirmModal.close()}
				>Abbrechen</button
			>
			<button class="btn btn-error" on:click={deleteNotification}>Löschen</button>
		</div>
	</div>
	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Bulk Delete Confirmation Modal -->
<dialog id="bulkDeleteConfirmModal" class="modal" bind:this={bulkDeleteConfirmModal}>
	<div class="modal-box">
		<h3 class="font-bold text-lg mb-4">Ausgewählte Benachrichtigungen löschen</h3>
		<p>
			Sind Sie sicher, dass Sie die ausgewählten Benachrichtigungen löschen möchten? Diese Aktion
			kann nicht rückgängig gemacht werden.
		</p>
		<div class="modal-action">
			<button class="btn btn-outline btn-secondary" on:click={() => bulkDeleteConfirmModal.close()}
				>Abbrechen</button
			>
			<button class="btn btn-error" on:click={bulkDeleteNotifications}>Löschen</button>
		</div>
	</div>
	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<dialog id="createNotificationModal" class="modal" bind:this={notifyAllModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold mb-2">Neue Globale Benachrichtigung erstellen</h3>
		<div class="alert alert-warning mb-2">
			<p>Achtung: Dies wird ab ~50 Nutzern sehr lange dauern.</p>
		</div>
		<input
			type="text"
			placeholder="Titel"
			class="input input-bordered w-full mb-2"
			bind:value={notifyAllTitle}
		/>
		<textarea
			placeholder="Inhalt (HTML erlaubt -> Svelte @html)"
			class="textarea textarea-bordered w-full mb-2"
			bind:value={notifyAllContent}
		></textarea>
		<input
			type="text"
			placeholder="Link (optional)"
			class="input input-bordered w-full mb-2"
			bind:value={notifyAllLink}
		/>
		<label class="flex w-full justify-between gap-2 mb-4">
			<span>Als gelesen markieren</span>
			<input type="checkbox" class="checkbox bg-base-100" bind:checked={notifyAllIsRead} />
		</label>
		<div class="modal-action">
			<button class="btn btn-outline btn-secondary" on:click={() => notifyAllModal.close()}
				>Abbrechen</button
			>
			<button class="btn btn-primary" on:click={notifyAll}>Erstellen</button>
		</div>
	</div>
	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

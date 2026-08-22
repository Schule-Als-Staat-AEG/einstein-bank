<script lang="ts">
	import db from "$lib/connections";
	import AdminPanel from "$modules/AdminPanel.svelte";
	import UserSelectModal from "$modules/modals/UserSelectModal.svelte";
	import { onMount } from "svelte";

	let filter: string = "";
	let page: number = 1;
	let perPage: number = 30;
	let totalPages: number = 1;
	let totalItems: number = 0;

	let userSelectModal: UserSelectModal;

	let createModal: HTMLDialogElement;
	let editModal: HTMLDialogElement;
	let deleteModal: HTMLDialogElement;

	let groupName: string = "";
	let groupUsers: any[] = [];
	let groupId: string = "";
	let groups: any[] = [];

	let userPage: number = 1;
	const usersPerPage: number = 5;

	$: userTotalPages = Math.max(
		1,
		Math.ceil(groupUsers.length / usersPerPage)
	);

	$: visibleGroupUsers = groupUsers.slice(
		(userPage - 1) * usersPerPage,
		userPage * usersPerPage
	);

	onMount(async () => {
		await fetchGroups();
	});

	async function fetchGroups() {
		const data = await db.collection("admin_groups").getList(page, perPage, {
			filter: filter ? `name~'${filter}'` : undefined,
			sort: "-created",
			expand: "users"
		});

		groups = data.items;
		totalItems = data.totalItems;
		totalPages = data.totalPages;
	}

	function handleUserSelect(event: CustomEvent) {
		const selectedUser = event.detail;

		if (groupUsers.some((user) => user.id === selectedUser.id)) {
			alert("Benutzer ist bereits in der Gruppe.");
			return;
		}

		groupUsers = [...groupUsers, selectedUser];
	}

	function removeUser(userId: string) {
		groupUsers = groupUsers.filter((user) => user.id !== userId);

		const newTotalPages = Math.max(
			1,
			Math.ceil(groupUsers.length / usersPerPage)
		);

		if (userPage > newTotalPages) {
			userPage = newTotalPages;
		}
	}

	function openCreateModal() {
		groupName = "";
		groupUsers = [];
		groupId = "";
		userPage = 1;

		createModal.showModal();
	}

	async function createGroup() {
		if (!groupName.trim()) {
			alert("Gruppenname darf nicht leer sein.");
			return;
		}

		try {
			await db.collection("admin_groups").create({
				name: groupName.trim(),
				users: groupUsers.map((user) => user.id)
			});

			createModal.close();
			await fetchGroups();
		} catch (err) {
			console.error(err);
			alert("Fehler beim Erstellen der Gruppe.");
		}
	}

	function openEditModal(group: any) {
		groupName = group.name;
		groupUsers = group.expand?.users ?? [];
		groupId = group.id;
		userPage = 1;

		editModal.showModal();
	}

	async function editGroup() {
		if (!groupName.trim()) {
			alert("Gruppenname darf nicht leer sein.");
			return;
		}

		try {
			await db.collection("admin_groups").update(groupId, {
				name: groupName.trim(),
				users: groupUsers.map((user) => user.id)
			});

			editModal.close();
			await fetchGroups();
		} catch (err) {
			console.error(err);
			alert("Fehler beim Bearbeiten der Gruppe.");
		}
	}

	function openDeleteModal(group: any) {
		groupName = group.name;
		groupId = group.id;

		deleteModal.showModal();
	}

	async function deleteGroup() {
		try {
			await db.collection("admin_groups").delete(groupId);

			deleteModal.close();
			await fetchGroups();
		} catch (err) {
			console.error(err);
			alert("Fehler beim Löschen der Gruppe.");
		}
	}
</script>

<AdminPanel
	showCreateButton={true}
	{totalItems}
	on:create={openCreateModal}
>
	<svelte:fragment slot="filter">
		<div class="flex flex-col gap-2 md:flex-row">
			<div class="flex w-full gap-2">
				<input
					type="text"
					placeholder="Gruppenname"
					class="input input-bordered w-full"
					bind:value={filter}
				/>

				<select
					class="select select-bordered w-96 max-w-fit"
					bind:value={perPage}
				>
					<option value={10}>10 pro Seite</option>
					<option value={20}>20 pro Seite</option>
					<option value={30}>30 pro Seite</option>
					<option value={50}>50 pro Seite</option>
					<option value={100}>100 pro Seite</option>
					<option value={200}>200 pro Seite</option>
					<option value={500}>500 pro Seite</option>
				</select>
			</div>
		</div>
	</svelte:fragment>

	<div class="overflow-x-auto">
		<table class="table table-zebra w-full">
			<thead>
				<tr>
					<th>Gruppenname</th>
					<th>Nutzer</th>
					<th>Erstellt am</th>
					<th>Aktionen</th>
				</tr>
			</thead>

			<tbody>
				{#each groups as group}
				    {@const users = group.expand?.users ?? []}	
                    <tr>
						<td>{group.name}</td>

						<td>

							Total: {users.length}

							{#if users.length > 0}
								|
								{users
									.slice(0, 5)
									.map((user) =>
										user.isCompany
											? user.companyName
											: user.name
									)
									.join(", ")}
							{/if}
						</td>

						<td>{new Date(group.created).toLocaleString()}</td>

						<td>
							<div class="flex gap-2">
								<button
									type="button"
									class="btn btn-sm btn-primary"
									on:click={() => openEditModal(group)}
								>
									Bearbeiten
								</button>

								<button
									type="button"
									class="btn btn-sm btn-error"
									on:click={() => openDeleteModal(group)}
								>
									Löschen
								</button>
							</div>
						</td>
					</tr>
				{/each}
			</tbody>
		</table>
	</div>

	<UserSelectModal
		bind:this={userSelectModal}
		adminMode={true}
		on:select={handleUserSelect}
	/>

	<!-- Create Group Modal -->
	<dialog bind:this={createModal} class="modal">
		<div class="modal-box w-11/12 max-w-2xl">
			<div class="flex items-start justify-between gap-4">
				<div>
					<h3 class="text-xl font-bold">Gruppe erstellen</h3>
					<p class="mt-1 text-sm opacity-60">
						Erstelle eine neue Gruppe und füge Benutzer hinzu.
					</p>
				</div>

				<div class="badge badge-primary badge-outline">
					{groupUsers.length} Benutzer
				</div>
			</div>

			<div class="form-control mt-5">
				<label class="label" for="create-group-name">
					<span class="label-text font-medium">Gruppenname</span>
				</label>

				<input
					id="create-group-name"
					type="text"
					class="input input-bordered w-full"
					bind:value={groupName}
					placeholder="Gruppenname"
					autocomplete="off"
				/>
			</div>

			<div class="divider">Mitglieder</div>

			<div class="flex items-center justify-between gap-4">
				<p class="text-sm opacity-70">
					Benutzer in dieser Gruppe
				</p>

				<button
					type="button"
					class="btn btn-sm btn-primary"
					on:click={() => userSelectModal.showModal()}
				>
					Benutzer hinzufügen
				</button>
			</div>

			<div class="mt-3 min-h-64">
				{#if groupUsers.length === 0}
					<div class="hero min-h-56 rounded-box bg-base-200">
						<div class="hero-content text-center">
							<div>
								<p class="font-semibold">
									Keine Benutzer ausgewählt
								</p>

								<p class="mt-1 text-sm opacity-60">
									Füge Benutzer über die Schaltfläche oben hinzu.
								</p>
							</div>
						</div>
					</div>
				{:else}
					<div class="flex flex-col gap-2">
						{#each visibleGroupUsers as user}
							<div
								class="flex items-center justify-between gap-3 rounded-box border border-base-300 bg-base-200 p-3"
							>
								<div class="min-w-0">
									<p class="truncate font-medium">
										{user.isCompany
											? `${user.companyName} (${user.name})`
											: user.name}
									</p>

									<p class="truncate text-xs opacity-50">
										{user.id}
									</p>
								</div>

								<button
									type="button"
									class="btn btn-sm btn-ghost text-error"
									on:click={() => removeUser(user.id)}
								>
									Entfernen
								</button>
							</div>
						{/each}
					</div>
				{/if}
			</div>

			{#if userTotalPages > 1}
				<div class="mt-4 flex items-center justify-between">
					<span class="text-sm opacity-60">
						Seite {userPage} von {userTotalPages}
					</span>

					<div class="join">
						<button
							type="button"
							class="btn join-item btn-sm"
							disabled={userPage === 1}
							on:click={() => userPage--}
						>
							‹
						</button>

						<button
							type="button"
							class="btn join-item btn-sm"
						>
							{userPage}
						</button>

						<button
							type="button"
							class="btn join-item btn-sm"
							disabled={userPage === userTotalPages}
							on:click={() => userPage++}
						>
							›
						</button>
					</div>
				</div>
			{/if}

			<div class="modal-action">
				<button
					type="button"
					class="btn btn-ghost"
					on:click={() => createModal.close()}
				>
					Abbrechen
				</button>

				<button
					type="button"
					class="btn btn-primary"
					on:click={createGroup}
				>
					Erstellen
				</button>
			</div>
		</div>

		<form method="dialog" class="modal-backdrop">
			<button>Schließen</button>
		</form>
	</dialog>

	<!-- Edit Group Modal -->
	<dialog bind:this={editModal} class="modal">
		<div class="modal-box w-11/12 max-w-2xl">
			<div class="flex items-start justify-between gap-4">
				<div>
					<h3 class="text-xl font-bold">Gruppe bearbeiten</h3>
					<p class="mt-1 text-sm opacity-60">
						Bearbeite den Gruppennamen und die Mitglieder.
					</p>
				</div>

				<div class="badge badge-primary badge-outline">
					{groupUsers.length} Benutzer
				</div>
			</div>

			<div class="form-control mt-5">
				<label class="label" for="edit-group-name">
					<span class="label-text font-medium">Gruppenname</span>
				</label>

				<input
					id="edit-group-name"
					type="text"
					class="input input-bordered w-full"
					bind:value={groupName}
					placeholder="Gruppenname"
					autocomplete="off"
				/>
			</div>

			<div class="divider">Mitglieder</div>

			<div class="flex items-center justify-between gap-4">
				<p class="text-sm opacity-70">
					Benutzer in dieser Gruppe
				</p>

				<button
					type="button"
					class="btn btn-sm btn-primary"
					on:click={() => userSelectModal.showModal()}
				>
					Benutzer hinzufügen
				</button>
			</div>

			<div class="mt-3 min-h-64">
				{#if groupUsers.length === 0}
					<div class="hero min-h-56 rounded-box bg-base-200">
						<div class="hero-content text-center">
							<div>
								<p class="font-semibold">
									Keine Benutzer ausgewählt
								</p>

								<p class="mt-1 text-sm opacity-60">
									Füge Benutzer über die Schaltfläche oben hinzu.
								</p>
							</div>
						</div>
					</div>
				{:else}
					<div class="flex flex-col gap-2">
						{#each visibleGroupUsers as user}
							<div
								class="flex items-center justify-between gap-3 rounded-box border border-base-300 bg-base-200 p-3"
							>
								<div class="min-w-0">
									<p class="truncate font-medium">
										{user.isCompany
											? `${user.companyName} (${user.name})`
											: user.name}
									</p>

									<p class="truncate text-xs opacity-50">
										{user.id}
									</p>
								</div>

								<button
									type="button"
									class="btn btn-sm btn-ghost text-error"
									on:click={() => removeUser(user.id)}
								>
									Entfernen
								</button>
							</div>
						{/each}
					</div>
				{/if}
			</div>

			{#if userTotalPages > 1}
				<div class="mt-4 flex items-center justify-between">
					<span class="text-sm opacity-60">
						Seite {userPage} von {userTotalPages}
					</span>

					<div class="join">
						<button
							type="button"
							class="btn join-item btn-sm"
							disabled={userPage === 1}
							on:click={() => userPage--}
						>
							‹
						</button>

						<button
							type="button"
							class="btn join-item btn-sm"
						>
							{userPage}
						</button>

						<button
							type="button"
							class="btn join-item btn-sm"
							disabled={userPage === userTotalPages}
							on:click={() => userPage++}
						>
							›
						</button>
					</div>
				</div>
			{/if}

			<div class="modal-action">
				<button
					type="button"
					class="btn btn-ghost"
					on:click={() => editModal.close()}
				>
					Abbrechen
				</button>

				<button
					type="button"
					class="btn btn-primary"
					on:click={editGroup}
				>
					Speichern
				</button>
			</div>
		</div>

		<form method="dialog" class="modal-backdrop">
			<button>Schließen</button>
		</form>
	</dialog>

	<!-- Delete Group Modal -->
	<dialog bind:this={deleteModal} class="modal">
		<div class="modal-box">
			<h3 class="text-xl font-bold">Gruppe löschen</h3>

			<div class="alert alert-warning mt-5">
				<span>
					Möchten Sie die Gruppe
					<strong>„{groupName}“</strong>
					wirklich löschen?
				</span>
			</div>

			<div class="modal-action">
				<button
					type="button"
					class="btn btn-ghost"
					on:click={() => deleteModal.close()}
				>
					Abbrechen
				</button>

				<button
					type="button"
					class="btn btn-error"
					on:click={deleteGroup}
				>
					Löschen
				</button>
			</div>
		</div>

		<form method="dialog" class="modal-backdrop">
			<button>Schließen</button>
		</form>
	</dialog>
</AdminPanel>
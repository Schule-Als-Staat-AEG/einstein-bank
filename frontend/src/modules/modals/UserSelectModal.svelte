<script lang="ts">
	import { browser } from '$app/environment';
	import db from '$lib/connections';
	import { ChevronLeft, ChevronRight, Search, Store, UserRound, X } from '@lucide/svelte';
	import { createEventDispatcher } from 'svelte';
	import { onMount } from 'svelte';

	onMount(async () => {
		if (!browser) return;
	});

	function showModal() {
		console.log('showing modal');
		page = 1;
		filter = '';
		exportUserModal.showModal();
		fetchUsers();
	}
	function closeModal() {
		exportUserModal.close();
	}
	function show() {
		showModal();
	}
	function close() {
		closeModal();
	}

	let dispatch = createEventDispatcher();

	let exportUserModal: HTMLDialogElement;
	let page: number = 1;
	let perPage: number = 5;
	let totalPages: number = 1;
	let filter: string = '';
	let companyOnly: boolean = false;

	let selectButton: string = 'Auswählen';

	let userSearchResults: any[] = [];

	let adminMode: boolean = false;
	let loadingUsers: boolean = false;

	function selectUser(user) {
		dispatch('select', user);
		exportUserModal.close();
	}
	async function fetchUsers() {
		loadingUsers = true;
		if (adminMode) {
			let result = await db.collection('users').getList(page, perPage, {
				requestKey: null,
				filter: `name ~ "${filter}" || email ~ "${filter}" || id ~ "${filter}" || companyName ~ "${filter}"${companyOnly ? ' && isCompany = true' : ''}`,
				sort: 'name'
			});
			userSearchResults = result.items;
			totalPages = result.totalPages;
		} else {
			let result = await db.collection('public_users').getList(page, perPage, {
				requestKey: null,
				filter: `name ~ "${filter}" || companyName ~ "${filter}"${companyOnly ? ' && isCompany = true' : ''}`,
				sort: 'name'
			});
			userSearchResults = result.items;
			totalPages = result.totalPages;
		}
		loadingUsers = false;
	}
	function pageUp() {
		if (page < totalPages) {
			page++;
			fetchUsers();
		}
	}
	function pageDown() {
		if (page > 1) {
			page--;
			fetchUsers();
		}
	}

	export { show, close, showModal, closeModal, adminMode, perPage, companyOnly, selectButton };
</script>

<dialog id="userSelectModal" class="modal" bind:this={exportUserModal}>
	<div class="modal-box w-full max-w-3xl rounded-2xl border border-base-300">
		<header class="flex items-start justify-between gap-3">
			<div>
				<h3 class="text-lg font-bold">
					{#if companyOnly}Firma{:else}Benutzer{/if} auswählen
				</h3>
				<p class="text-sm opacity-70">Suche nach dem Namen.</p>
			</div>
			<button
				class="btn btn-ghost btn-sm"
				on:click={() => exportUserModal.close()}
				aria-label="Schließen"
			>
				<X size={18} />
			</button>
		</header>

		<div class="mt-4">
			<label class="input input-bordered w-full flex items-center gap-2 rounded-2xl">
				<Search size={18} class="opacity-60" />
				<input
					type="text"
					class="grow"
					placeholder="Namen eingeben..."
					bind:value={filter}
					on:input={() => {
						page = 1;
						fetchUsers();
					}}
					autocomplete="off"
				/>
			</label>
		</div>

		<div class="mt-4 max-h-96 overflow-y-auto space-y-2 pr-1">
			{#if loadingUsers}
				<div
					class="rounded-2xl bg-base-200 border border-base-300 p-6 flex items-center justify-center"
				>
					<span class="loading loading-spinner loading-md"></span>
				</div>
			{:else}
				{#each userSearchResults as user}
					<button
						class="btn btn-outline btn-block justify-between rounded-2xl h-auto py-3"
						on:click={() => selectUser(user)}
					>
						<div class="flex items-center gap-3 min-w-0">
							<div
								class="w-10 h-10 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center shrink-0"
							>
								{#if user?.isCompany}
									<Store size={16} />
								{:else}
									<UserRound size={16} />
								{/if}
							</div>
							<div class="flex flex-col items-start min-w-0">
								<span class="font-bold truncate"
									>{#if user?.isCompany}{user?.companyName != "" ? user?.companyName : user?.name ?? "Unbekannt"}{:else}{user?.name ?? 'Unbekannt'}{/if}</span
								>
								<span class="text-xs font-mono opacity-70 truncate">
									ID: {user.id}{#if adminMode}
										• Email: {user.email}{/if}
								</span>
							</div>
						</div>
						<span class="badge badge-ghost">{selectButton}</span>
					</button>
				{/each}

				{#if !userSearchResults?.length}
					<div class="text-sm opacity-70 text-center py-6">
						Keine Treffer. Suchbegriff anpassen.
					</div>
				{/if}
			{/if}
		</div>

		<div class="modal-action items-center">
			<div class="flex-1 flex justify-center">
				<div class="join">
					<button class="join-item btn" on:click={pageDown} disabled={page <= 1}>
						<ChevronLeft size={18} />
					</button>
					<button class="join-item btn btn-ghost">
						Seite <span class="font-mono mx-1">{page}</span> /
						<span class="font-mono">{totalPages}</span>
					</button>
					<button class="join-item btn" on:click={pageUp} disabled={page >= totalPages}>
						<ChevronRight size={18} />
					</button>
				</div>
			</div>
		</div>
		<div class="flex flex-row w-full justify-end">
			<button class="btn btn-error btn-outline" on:click={() => exportUserModal.close()}>
				<X size={18} />
				Abbrechen
			</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

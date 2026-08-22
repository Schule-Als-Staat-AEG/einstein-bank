<script lang="ts">
	import { browser } from '$app/environment';
	import db from '$lib/connections';
	import Currency from '$modules/Currency.svelte';
	import { ChevronLeft, ChevronRight, Search, Store, UserRound, X } from '@lucide/svelte';
	import { createEventDispatcher } from 'svelte';
	import { onMount } from 'svelte';

	function showModal() {
		console.log('showing modal');
		page = 1;
		filter = '';
		exportAccountModal.showModal();
		fetchAccounts();
	}
	function closeModal() {
		exportAccountModal.close();
	}
	function show() {
		showModal();
	}
	function close() {
		closeModal();
	}

	let dispatch = createEventDispatcher();

	let exportAccountModal: HTMLDialogElement;
	let page: number = 1;
	let perPage: number = 5;
	let totalPages: number = 1;
	let filter: string = '';

	let accountSearchResults: any[] = [];

	let adminMode: boolean = false;
	let loadingAccounts: boolean = false;

	function selectAccount(account) {
		dispatch('select', account);
		exportAccountModal.close();
	}
	async function fetchAccounts() {
		loadingAccounts = true;
		if (adminMode) {
			let result = await db.collection('accounts').getList(page, perPage, {
				requestKey: null,
				filter: `user.name ~ "${filter}" || user.email ~ "${filter}" || id ~ "${filter}" || user.companyName ~ "${filter}"`,
				sort: 'user.name',
				expand: 'user'
			});
			accountSearchResults = result.items;
			totalPages = result.totalPages;
		} else {
			let result = await db.collection('public_accounts').getList(page, perPage, {
				requestKey: null,
				filter: `name ~ "${filter}" || companyName ~ "${filter}" || id ~ "${filter}"`,
				sort: 'name'
			});
			accountSearchResults = result.items;
			totalPages = result.totalPages;
		}
		loadingAccounts = false;
	}
	function pageUp() {
		if (page < totalPages) {
			page++;
			fetchAccounts();
		}
	}
	function pageDown() {
		if (page > 1) {
			page--;
			fetchAccounts();
		}
	}

	export { show, close, showModal, closeModal, adminMode, perPage, filter };

	onMount(async () => {
		if (!browser) return;
	});
</script>

<dialog id="accountSelectModal" class="modal" bind:this={exportAccountModal}>
	<div class="modal-box w-full max-h-4xl h-fit max-w-3xl rounded-2xl border border-base-300">
		<header class="flex items-start justify-between gap-3">
			<div>
				<h3 class="text-lg font-bold">Konto auswählen</h3>
				<p class="text-sm opacity-70">Suche nach dem Namen oder der Kontonummer.</p>
			</div>
			<button
				class="btn btn-ghost btn-sm"
				on:click={() => exportAccountModal.close()}
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
					placeholder="Namen oder Kontonummer eingeben..."
					bind:value={filter}
					on:input={() => {
						page = 1;
						fetchAccounts();
					}}
					autocomplete="off"
				/>
			</label>
		</div>

		<div class="mt-4 max-h-fit overflow-y-auto space-y-2 pr-1">
			{#if loadingAccounts}
				<div
					class="rounded-2xl bg-base-200 border border-base-300 p-6 flex items-center justify-center"
				>
					<span class="loading loading-spinner loading-md"></span>
				</div>
			{:else}
				{#each accountSearchResults as account}
					<button
						class="btn btn-outline btn-block justify-between rounded-2xl h-auto py-3"
						on:click={() => selectAccount(account)}
					>
						<div class="flex items-center gap-3 min-w-0">
							<div
								class="w-10 h-10 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center shrink-0"
							>
								{#if adminMode}
									{#if account?.expand.user.isCompany}
										<Store size={16} />
									{:else}
										<UserRound size={16} />
									{/if}
								{:else}
									{#if account?.isCompany}
										<Store size={16} />
									{:else}
										<UserRound size={16} />
									{/if}
								{/if}
							</div>
							<div class="flex flex-col items-start min-w-0">
								{#if adminMode}
									<span class="font-bold truncate"
										>{#if account?.expand.user.isCompany}{account?.expand.user.companyName != "" ? account?.expand.user.companyName : account?.expand.user.name ?? "Unbekannt"}{:else}{account?.expand.user.name ?? 'Unbekannt'}{/if}</span>
								{:else}
									<span class="font-bold truncate"
										>{#if account?.isCompany}{account?.companyName != "" ? account?.companyName : account?.name ?? "Unbekannt"}{:else}{account?.name ?? 'Unbekannt'}{/if}</span
									>
								{/if}
								<span class="text-xs font-mono opacity-70 truncate">
									Kontonummer: {account.id}
									{#if adminMode}
										• Email: {account?.expand.user.email}{/if}
								</span>
								{#if adminMode}
									<span class="text-xs font-mono opacity-70 truncate flex items-center">
										<p>Kontostand: {account.balance}</p>
										<Currency size={16} />
									</span>
								{/if}
							</div>
						</div>
						<span class="badge badge-ghost">Auswählen</span>
					</button>
				{/each}

				{#if !accountSearchResults?.length}
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
			<button class="btn btn-error btn-outline" on:click={() => exportAccountModal.close()}>
				<X size={18} />
				Abbrechen
			</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

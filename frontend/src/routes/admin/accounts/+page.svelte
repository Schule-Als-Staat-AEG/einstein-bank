<!-- #region Script -->
<script lang="ts">
	import AdminPanel from '$modules/AdminPanel.svelte';
	import { onMount } from 'svelte';
	import db from '$lib/connections';
	import Currency from '$modules/Currency.svelte';
	import {
		CircleQuestionMark,
		Handshake,
		LandmarkIcon,
		PenIcon,
		SnowflakeIcon
	} from '@lucide/svelte';
	import { getAuthStore } from '$lib/auth';
	import { browser } from '$app/environment';
	import { getSetting } from '$lib/settings';
	import { goto } from '$app/navigation';

	let accounts: any[] = [];
	let selectedAccounts: Set<string> = new Set();

	let page = 1;
	let perPage = 30;
	let maxPage = 1;
	let totalItems = 0;

	let filter = '';
	let filterDebounced = '';

	let sortBy = 'user.name';
	let sortDirection = 'asc';

	let defaultTaxRate = 0;

	let loading = false;
	let error: string | null = null;

	let editModal: HTMLDialogElement;
	let editUserID = '';
	let editBalance = 0;
	let editFrozen = false;
	let editTrusted = false;

	let massEditModal: HTMLDialogElement;
	let massEditBalance = 0;
	let massEditUpdateFrozen = false;
	let massEditFrozen = false;
	let massEditUpdateBalance = false;
	let massEditUpdateTaxExempt = false;
	let massEditTaxExempt = false;
	let massEditUpdateTaxFreePool = false;
	let massEditTaxFreePool = 0;
	let massEditUpdateTaxRate = false;
	let massEditTaxRate = 0;
	let massEditUpdateTrusted = false;
	let massEditTrusted = false;

	let activeUserName = '';
	let highlightAccountId = '';

	let searchTimer: any = null;

	onMount(async () => {
		if (!browser) return;

		const authStore = getAuthStore();
		activeUserName = authStore.record?.name ?? '';

		// URL highlight
		const urlParams = new URLSearchParams(window.location.search);
		highlightAccountId = urlParams.get('id') ?? '';

		await fetchAccounts(true);
	});

	function escapePB(str: string) {
		// PocketBase filter strings are sensitive to quotes
		return String(str ?? '')
			.replaceAll('\\', '\\\\')
			.replaceAll('"', '\\"');
	}

	function onSearchInput() {
		if (searchTimer) clearTimeout(searchTimer);
		searchTimer = setTimeout(() => {
			filterDebounced = filter;
			page = 1;
			fetchAccounts(false);
		}, 250);
	}

	async function fetchAccounts(force = false) {
		if (!browser) return;
		if (loading && !force) return;

		loading = true;
		error = null;

		try {
			const f = escapePB(filterDebounced.trim());

			const sortPrefix = sortDirection === 'desc' ? '-' : '+';
			const rsp = await db.collection('accounts').getList(page, perPage, {
				sort: `${sortPrefix}${sortBy}`,
				filter: f
					? `user.id ~ "${f}" || user.name ~ "${f}" || user.email ~ "${f}" || id ~ "${f}"`
					: '',
				expand: 'user'
			});

			maxPage = rsp.totalPages || 1;
			totalItems = rsp.totalItems || 0;
			accounts = rsp.items || [];

			// If URL has an account id, ensure it exists in the list and is expanded
			if (highlightAccountId) {
				const found = accounts.find((a) => a?.id === highlightAccountId);

				if (!found) {
					// Load it separately and pin to top
					try {
						const one = await db
							.collection('accounts')
							.getOne(highlightAccountId, { expand: 'user' });
						accounts = [one, ...accounts];
					} catch {
						// ignore if not found
					}
				} else {
					// move to top
					accounts = [found, ...accounts.filter((a) => a?.id !== highlightAccountId)];
				}
			}
		} catch (e: any) {
			error = e?.message ?? 'Unbekannter Fehler beim Laden.';
		} finally {
			loading = false;
		}
	}

	async function updateAccount(accountID: string) {
		try {
			await db.collection('accounts').update(accountID, {
				balance: Number(editBalance.toFixed(2)),
				frozen: editFrozen,
				trusted: editTrusted
			});

			await fetchAccounts(true);
			editModal.close();
		} catch (e: any) {
			error = e?.message ?? 'Update fehlgeschlagen.';
		}
	}

	function openEditModal(account: any) {
		editUserID = account.id;
		editBalance = Number(account.balance?.toFixed(2) ?? 0);
		editFrozen = Boolean(account.frozen);
		editTrusted = Boolean(account.trusted);

		editModal.showModal();
	}

	function toggleAccountSelection(accountID: string) {
		if (selectedAccounts.has(accountID)) selectedAccounts.delete(accountID);
		else selectedAccounts.add(accountID);
		selectedAccounts = new Set(selectedAccounts);
	}

	function toggleAllAccountsSelection() {
		if (accounts.length === 0) return;

		if (selectedAccounts.size === accounts.length) selectedAccounts.clear();
		else accounts.forEach((a) => selectedAccounts.add(a.id));

		selectedAccounts = new Set(selectedAccounts);
	}

	async function massUpdateAccounts() {
		if (selectedAccounts.size === 0) return;

		const updateData: any = {};
		if (massEditUpdateBalance) updateData.balance = Number(massEditBalance.toFixed(2));
		if (massEditUpdateFrozen) updateData.frozen = massEditFrozen;
		if (massEditUpdateTrusted) updateData.trusted = massEditTrusted;

		// nothing selected to update
		if (Object.keys(updateData).length === 0) {
			massEditModal.close();
			return;
		}

		try {
			await Promise.all(
				Array.from(selectedAccounts).map((id) => db.collection('accounts').update(id, updateData))
			);

			await fetchAccounts(true);
			massEditModal.close();

			// reset
			massEditBalance = 0;
			massEditUpdateBalance = false;
			massEditUpdateFrozen = false;
			massEditFrozen = false;
			selectedAccounts.clear();
			selectedAccounts = new Set();
		} catch (e: any) {
			error = e?.message ?? 'Mass-Update fehlgeschlagen.';
		}
	}

	function pageUp() {
		if (page < maxPage) {
			page += 1;
			fetchAccounts(false);
		}
	}

	function pageDown() {
		if (page > 1) {
			page -= 1;
			fetchAccounts(false);
		}
	}

	async function toggleFrozen(account: any) {
		try {
			await db.collection('accounts').update(account.id, { frozen: !account.frozen });
			await fetchAccounts(false);
		} catch (e: any) {
			error = e?.message ?? 'Update fehlgeschlagen.';
		}
	}

	async function toggleTrusted(account: any) {
		try {
			await db.collection('accounts').update(account.id, { trusted: !account.trusted });
			await fetchAccounts(false);
		} catch (e: any) {
			error = e?.message ?? 'Update fehlgeschlagen.';
		}
	}

	$: allSelected = accounts.length > 0 && selectedAccounts.size === accounts.length;
</script>
<!-- #endregion Script -->

<!-- #region HTML -->
<title>Admin - Konten</title>
<AdminPanel
	title="Konten"
	description="Verwaltung aller Konten"
	showCreateButton={false}
	showMassCreateButton={false}
	showMassEditButton={true}
	showMassDeleteButton={false}
	massEditButtonText="Ausgewählte bearbeiten"
	{totalItems}
	selectedItems={selectedAccounts.size}
	on:massEdit={() => massEditModal.showModal()}
>
	<svelte:fragment slot="filter">
		<div class="flex gap-3 flex-row justify-between items-center">
			<div class="flex w-full">
				<input
					class="input input-bordered w-full"
					type="text"
					placeholder="Suchen nach ID, Name, Mail..."
					autocomplete="off"
					bind:value={filter}
					on:input={onSearchInput}
				/>
			</div>

			<div class="flex flex-row items-center gap-2">
				<select
					class="select select-bordered"
					bind:value={perPage}
					on:change={async () => {
						page = 1;
						await fetchAccounts();
					}}
				>
					<option value={15}>10 / Seite</option>
					<option value={30}>30 / Seite</option>
					<option value={50}>50 / Seite</option>
					<option value={100}>100 / Seite</option>
					<option value={200}>200 / Seite</option>
					<option value={500}>500 / Seite</option>
					<option value={1000}>1000 / Seite</option>
					<option value={2000}>2000 / Seite</option>
					<option value={5000}>5000 / Seite</option>
				</select>

				<select
					class="select select-bordered"
					bind:value={sortBy}
					on:change={async () => {
						page = 1;
						await fetchAccounts();
					}}
				>
					<option value="id">Kontonummer</option>
					<option value="user.name">Name</option>
					<option value="balance">Kontostand</option>
					<option value="frozen">Gefroren</option>
					<option value="trusted">Vertrauenswürdig</option>
				</select>

				<button
					class="btn btn-bordered btn-sm"
					on:click={async () => {
						sortDirection = sortDirection === 'asc' ? 'desc' : 'asc';
						page = 1;
						await fetchAccounts();
					}}
					title={sortDirection === 'asc'
						? 'Aufsteigend sortiert, klicken zum Umkehren'
						: 'Absteigend sortiert, klicken zum Umkehren'}
				>
					{sortDirection === 'asc' ? '↑' : '↓'}
				</button>

				<div class="flex-1"></div>
			</div>

			{#if error}
				<div class="alert alert-error">
					<span>{error}</span>
				</div>
			{/if}

			{#if loading}
				<div class="text-sm opacity-70">Lade Konten…</div>
			{/if}
		</div>
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
								autocomplete="off"
								on:change={toggleAllAccountsSelection}
								checked={allSelected}
							/>
						</label>
					</th>
					<th>ID</th>
					<th>Nutzer</th>
					<th>Kontostand</th>
					<th>Status</th>
					<th class="text-right">Aktionen</th>
				</tr>
			</thead>

			<tbody>
				{#if !loading && accounts.length === 0}
					<tr>
						<td colspan="7" class="text-center opacity-70 py-8">Keine Konten gefunden.</td>
					</tr>
				{/if}

				{#each accounts as account (account.id)}
					<tr class={highlightAccountId === account.id ? 'bg-base-200/70' : ''}>
						<th>
							<label>
								<input
									type="checkbox"
									class="checkbox bg-base-100"
									autocomplete="off"
									on:change={() => toggleAccountSelection(account.id)}
									checked={selectedAccounts.has(account.id)}
								/>
							</label>
						</th>

						<td>
							<div class="text-sm opacity-70 font-mono">{account.id}</div>
						</td>

						<td>
							{#if account?.expand?.user?.name === activeUserName}
								<span class="font-bold text-info">{account.expand.user.name} (Sie)</span>
							{:else}
								{account?.expand?.user?.name ?? 'Unbekannt'}
							{/if}
							<br />
							<span class="badge badge-ghost badge-sm">{account?.expand?.user?.id ?? '—'}</span>
						</td>

						<td>
							<div class="flex flex-row items-center gap-1">
								{Number(account.balance.toFixed(2) ?? 0)}
								<Currency />
							</div>
						</td>

						<td>
							<div class="flex items-center gap-2">
								{#if account.frozen}
									<SnowflakeIcon class="w-5 h-5 text-info" />
								{/if}
								{#if account.trusted}
									<Handshake class="w-5 h-5 text-success" />
								{/if}
								{#if !account.frozen && !account.trusted}
									<span class="opacity-60 text-sm">—</span>
								{/if}
							</div>
						</td>

						<td class="text-right">
							<div class="flex justify-end gap-2">
								<button
									class="btn btn-ghost btn-xs"
									on:click={() => openEditModal(account)}
									title="Bearbeiten"
								>
									<PenIcon class="w-4 h-4" />
								</button>

								<button
									class="btn btn-ghost btn-xs text-info"
									on:click={() => toggleFrozen(account)}
									title="Einfrieren umschalten"
								>
									<SnowflakeIcon class="w-4 h-4" />
								</button>

								<button
									class="btn btn-ghost btn-xs text-success"
									on:click={() => goto(`/admin/accounts/${account.id}`)}
									title="Konto anschauen"
								>
									<LandmarkIcon class="w-4 h-4" />
								</button>
							</div>
						</td>
					</tr>
				{/each}
			</tbody>
		</table>
	</div>
	<div class="flex justify-center mt-4">
		<div class="join">
			<button class="join-item btn" on:click={pageDown} disabled={page <= 1}>«</button>
			<button class="join-item btn btn-ghost">Seite {page} / {maxPage}</button>
			<button class="join-item btn" on:click={pageUp} disabled={page >= maxPage}>»</button>
		</div>
	</div>
</AdminPanel>
<!-- #endregion HTML -->
<!-- #region Modal + Dialog -->
<dialog class="modal" bind:this={editModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Konto bearbeiten</h3>

		<p class="opacity-70 text-sm mt-3">Kontostand:</p>
		<div class="flex items-center gap-2 mt-2">
			<input
				type="number"
				class="input input-bordered w-full"
				bind:value={editBalance}
				placeholder="Kontostand"
				autocomplete="off"
			/>
			<Currency size={32} />
		</div>

		<div class="flex items-center justify-between mt-4">
			<span>Konto eingefroren:</span>
			<input
				type="checkbox"
				class="checkbox bg-base-100"
				bind:checked={editFrozen}
				autocomplete="off"
			/>
		</div>

		<div class="flex items-center justify-between mt-4">
			<span>Konto vertrauenswürdig:</span>
			<input
				type="checkbox"
				class="checkbox bg-base-100"
				bind:checked={editTrusted}
				autocomplete="off"
			/>
		</div>

		<div class="modal-action">
			<button class="btn btn-primary btn-outline" on:click={() => editModal.close()}
				>Abbrechen</button
			>
			<button class="btn btn-error" on:click={() => updateAccount(editUserID)}>Aktualisieren</button
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
		<h3 class="text-lg font-bold">Mehrere Konten bearbeiten</h3>

		<div class="mt-4 space-y-4">
			<div class="flex items-center justify-between">
				<span>Kontostand aktualisieren:</span>
				<input
					type="checkbox"
					class="checkbox bg-base-100"
					bind:checked={massEditUpdateBalance}
					autocomplete="off"
				/>
			</div>
			{#if massEditUpdateBalance}
				<div class="flex items-center gap-2">
					<input
						type="number"
						class="input input-bordered w-full"
						bind:value={massEditBalance}
						placeholder="Neuer Kontostand"
						autocomplete="off"
					/>
					<Currency size={32} />
				</div>
			{/if}

			<div class="flex items-center justify-between">
				<span>Konto eingefroren aktualisieren:</span>
				<input
					type="checkbox"
					class="checkbox bg-base-100"
					bind:checked={massEditUpdateFrozen}
					autocomplete="off"
				/>
			</div>
			{#if massEditUpdateFrozen}
				<div class="flex items-center justify-between">
					<span>Neuer Eingefroren-Status:</span>
					<input
						type="checkbox"
						class="checkbox bg-base-100"
						bind:checked={massEditFrozen}
						autocomplete="off"
					/>
				</div>
			{/if}

			<div class="flex items-center justify-between">
				<span>Konto vertrauenswürdig aktualisieren:</span>
				<input
					type="checkbox"
					class="checkbox bg-base-100"
					bind:checked={massEditUpdateTrusted}
					autocomplete="off"
				/>
			</div>
			{#if massEditUpdateTrusted}
				<div class="flex items-center justify-between">
					<span>Neuer Vertrauenswürdig-Status:</span>
					<input
						type="checkbox"
						class="checkbox bg-base-100"
						bind:checked={massEditTrusted}
						autocomplete="off"
					/>
				</div>
			{/if}
		</div>

		<div class="modal-action">
			<button class="btn btn-primary btn-outline" on:click={() => massEditModal.close()}
				>Abbrechen</button
			>
			<button class="btn btn-error" on:click={massUpdateAccounts}>Aktualisieren</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>
<!-- #endregion Modal + Dialog -->

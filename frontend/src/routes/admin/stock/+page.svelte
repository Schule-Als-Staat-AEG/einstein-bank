<!-- #region Script -->
<script lang="ts">
	import AdminPanel from '$modules/AdminPanel.svelte';
	import { onMount } from 'svelte';
	import db from '$lib/connections';
	import Currency from '$modules/Currency.svelte';
	import { PenIcon, Trash2Icon, PlusIcon, Package2Icon, Euro } from '@lucide/svelte';
	import { browser } from '$app/environment';

	let stockItems: any[] = [];
	let selectedItems: Set<string> = new Set();

	let page = 1;
	let perPage = 30;
	let maxPage = 1;
	let totalItems = 0;

	let filter = '';
	let filterDebounced = '';

	let loading = false;
	let error: string | null = null;

	// Create Modal
	let createModal: HTMLDialogElement;
	let createName = '';
	let createDescription = '';
	let createPrice = 0;
	let createEuroPrice = 0;
	let createMax = -1;
	let createCategory = '';

	// Edit Modal
	let editModal: HTMLDialogElement;
	let editItemID = '';
	let editName = '';
	let editDescription = '';
	let editPrice = 0;
	let editEuroPrice = 0;
	let editMax = -1;
	let editCategory = '';

	// Mass Edit Modal
	let massEditModal: HTMLDialogElement;
	let massEditUpdatePrice = false;
	let massEditPrice = 0;
	let massEditUpdateEuroPrice = false;
	let massEditEuroPrice = 0;
	let massEditUpdateMax = false;
	let massEditMax = -1;

	// Delete Confirmation Modal
	let deleteModal: HTMLDialogElement;
	let deleteItemID = '';
	let deleteItemName = '';

	// Mass Delete Modal
	let massDeleteModal: HTMLDialogElement;

	let highlightItemId = '';
	let searchTimer: any = null;

	const categoryOptions = [
		{ value: 'fruits_and_vegetables', label: 'Obst & Gemüse' },
		{ value: 'dairy', label: 'Milchprodukte & Eier' },
		{ value: 'meat', label: 'Wurst und Fleischprodukte' },
		{ value: 'frozen', label: 'Tiefkühl' },
		{ value: 'baked', label: 'Brot, Cerealien & Aufstriche' },
		{ value: 'cooking_and_baking', label: 'Kochen & Backen' },
		{ value: 'oil_and_spices', label: 'Öle & Gewürze' },
		{ value: 'sweet_and_salty', label: 'Snacks und Süßigkeiten' },
		{ value: 'coffee_and_cakao', label: 'Tee & Kakao' },
		{ value: 'drinks', label: 'Getränke' },
		{ value: 'other', label: 'Sonstiges' }
	];

	function getCategoryLabel(category: string | null | undefined) {
		return categoryOptions.find((option) => option.value === category)?.label ?? 'Ohne Kategorie';
	}

	onMount(async () => {
		if (!browser) return;

		// URL highlight
		const urlParams = new URLSearchParams(window.location.search);
		highlightItemId = urlParams.get('id') ?? '';

		await fetchStockItems(true);
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
			fetchStockItems(false);
		}, 250);
	}

	async function fetchStockItems(force = false) {
		if (!browser) return;
		if (loading && !force) return;

		loading = true;
		error = null;

		try {
			const f = escapePB(filterDebounced.trim());

			const rsp = await db.collection('stock').getList(page, perPage, {
				sort: '+name',
				filter: f ? `id ~ "${f}" || name ~ "${f}" || description ~ "${f}"` : '',
				requestKey: null
			});

			maxPage = rsp.totalPages || 1;
			stockItems = rsp.items || [];
			totalItems = rsp.totalItems || 0;

			// If URL has an item id, ensure it exists in the list and is expanded
			if (highlightItemId) {
				const found = stockItems.find((item) => item?.id === highlightItemId);

				if (!found) {
					// Load it separately and pin to top
					try {
						const one = await db.collection('stock').getOne(highlightItemId, { requestKey: null });
						stockItems = [one, ...stockItems];
					} catch {
						// ignore if not found
					}
				} else {
					// move to top
					stockItems = [found, ...stockItems.filter((item) => item?.id !== highlightItemId)];
				}
			}
		} catch (e: any) {
			error = e?.message ?? 'Unbekannter Fehler beim Laden.';
		} finally {
			loading = false;
		}
	}

	async function createItem() {
		try {
			await db.collection('stock').create(
				{
					name: createName,
					description: createDescription,
					price: createPrice,
					euroPrice: createEuroPrice,
					max: createMax,
					category: createCategory
				},
				{ requestKey: null }
			);

			await fetchStockItems(true);
			createModal.close();
			resetCreateForm();
		} catch (e: any) {
			error = e?.message ?? 'Erstellen fehlgeschlagen.';
		}
	}

	function resetCreateForm() {
		createName = '';
		createDescription = '';
		createPrice = 0;
		createEuroPrice = 0;
		createMax = -1;
		createCategory = '';
	}

	async function updateItem() {
		try {
			await db.collection('stock').update(
				editItemID,
				{
					name: editName,
					description: editDescription,
					price: editPrice,
					euroPrice: editEuroPrice,
					max: editMax,
					category: editCategory
				},
				{ requestKey: null }
			);

			await fetchStockItems(true);
			editModal.close();
		} catch (e: any) {
			error = e?.message ?? 'Update fehlgeschlagen.';
		}
	}

	function openEditModal(item: any) {
		editItemID = item.id;
		editName = item.name || '';
		editDescription = item.description || '';
		editPrice = Number(item.price ?? 0);
		editEuroPrice = Number(item.euroPrice ?? 0);
		editMax = Number(item.max ?? -1);
		editCategory = item.category || '';

		editModal.showModal();
	}

	function openDeleteModal(item: any) {
		deleteItemID = item.id;
		deleteItemName = item.name || 'Unbekannter Artikel';
		deleteModal.showModal();
	}

	async function deleteItem() {
		try {
			await db.collection('stock').delete(deleteItemID, { requestKey: null });
			await fetchStockItems(true);
			deleteModal.close();
		} catch (e: any) {
			error = e?.message ?? 'Löschen fehlgeschlagen.';
		}
	}

	function toggleItemSelection(itemID: string) {
		if (selectedItems.has(itemID)) selectedItems.delete(itemID);
		else selectedItems.add(itemID);
		selectedItems = new Set(selectedItems);
	}

	function toggleAllItemsSelection() {
		if (stockItems.length === 0) return;

		if (selectedItems.size === stockItems.length) selectedItems.clear();
		else stockItems.forEach((item) => selectedItems.add(item.id));

		selectedItems = new Set(selectedItems);
	}

	async function massUpdateItems() {
		if (selectedItems.size === 0) return;

		const updateData: any = {};
		if (massEditUpdatePrice) updateData.price = massEditPrice;
		if (massEditUpdateMax) updateData.max = massEditMax;

		// nothing selected to update
		if (Object.keys(updateData).length === 0) {
			massEditModal.close();
			return;
		}

		try {
			await Promise.all(
				Array.from(selectedItems).map((id) =>
					db.collection('stock').update(id, updateData, { requestKey: null })
				)
			);

			await fetchStockItems(true);
			massEditModal.close();

			// reset
			massEditPrice = 0;
			massEditMax = -1;
			massEditEuroPrice = 0;
			massEditUpdatePrice = false;
			massEditUpdateMax = false;
			massEditUpdateEuroPrice = false;
			selectedItems.clear();
			selectedItems = new Set();
		} catch (e: any) {
			error = e?.message ?? 'Mass-Update fehlgeschlagen.';
		}
	}

	async function massDeleteItems() {
		if (selectedItems.size === 0) return;

		try {
			await Promise.all(
				Array.from(selectedItems).map((id) =>
					db.collection('stock').delete(id, { requestKey: null })
				)
			);

			await fetchStockItems(true);
			massDeleteModal.close();

			// reset
			selectedItems.clear();
			selectedItems = new Set();
		} catch (e: any) {
			error = e?.message ?? 'Mass-Löschen fehlgeschlagen.';
		}
	}

	function pageUp() {
		if (page < maxPage) {
			page += 1;
			fetchStockItems(false);
		}
	}

	function pageDown() {
		if (page > 1) {
			page -= 1;
			fetchStockItems(false);
		}
	}

	function formatMax(max: number) {
		return max === -1 ? 'Unbegrenzt' : String(max);
	}

	function stripHtml(html: string) {
		return html?.replace(/<[^>]*>/g, '') || '';
	}

	$: allSelected = stockItems.length > 0 && selectedItems.size === stockItems.length;
</script>
<!-- #endregion Script -->

<!-- #region HTML -->
<svelte:head>
	<title>Admin - Lager</title>
</svelte:head>

<AdminPanel
	title="Lager"
	description="Verwaltung des Lagers und aller Artikel"
	showCreateButton={true}
	showMassEditButton={true}
	showMassDeleteButton={true}
	massEditButtonText="Ausgewählte bearbeiten"
	massDeleteButtonText="Ausgewählte löschen"
	{totalItems}
	selectedItems={selectedItems.size}
	on:create={() => createModal.showModal()}
	on:massEdit={() => massEditModal.showModal()}
	on:massDelete={() => massDeleteModal.showModal()}
>
	<svelte:fragment slot="filter">
		<div class="flex gap-3 flex-row justify-between">
			<div class="flex w-full">
				<input
					class="input input-bordered w-full"
					type="text"
					autocomplete="off"
					placeholder="Suchen nach ID, Name, Beschreibung..."
					bind:value={filter}
					on:input={onSearchInput}
				/>
			</div>

			<div class="flex gap-2 flex-wrap">
				<select
					class="select select-bordered"
					bind:value={perPage}
					on:change={() => {
						page = 1;
						fetchStockItems(true);
					}}
				>
					<option value="15">15 / Seite</option>
					<option value="30">30 / Seite</option>
					<option value="50">50 / Seite</option>
					<option value="100">100 / Seite</option>
					<option value="200">200 / Seite</option>
					<option value="500">500 / Seite</option>
					<option value="1000">1000 / Seite</option>
					<option value="2000">2000 / Seite</option>
				</select>

				<div class="flex-1"></div>
			</div>
		</div>
		{#if error}
			<div class="alert alert-error">
				<span>{error}</span>
			</div>
		{/if}
		{#if loading}
			<div class="text-sm opacity-70">Lade Artikel…</div>
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
								on:change={toggleAllItemsSelection}
								checked={allSelected}
							/>
						</label>
					</th>
					<th>Name</th>
					<th>Kategorie</th>
					<th>Beschreibung</th>
					<th>Preis</th>
					<th>Euro Preis</th>
					<th>Maximum</th>
					<th class="text-right">Aktionen</th>
				</tr>
			</thead>

			<tbody>
				{#if !loading && stockItems.length === 0}
					<tr>
						<td colspan="7" class="text-center opacity-70 py-8">Keine Artikel gefunden.</td>
					</tr>
				{/if}

				{#each stockItems as item (item.id)}
					<tr class={highlightItemId === item.id ? 'bg-base-200/70' : ''}>
						<th>
							<label>
								<input
									type="checkbox"
									class="checkbox bg-base-100"
									on:change={() => toggleItemSelection(item.id)}
									checked={selectedItems.has(item.id)}
								/>
							</label>
						</th>

						<td>
							<div class="font-medium">{item.name || 'Unbekannt'}</div>
							<div class="text-sm opacity-70 font-mono">{item.id}</div>
						</td>

						<td>
							<span class="badge badge-outline badge-sm">{getCategoryLabel(item.category)}</span>
						</td>

						<td>
							<div class="max-w-md">
								{#if item.description}
									<div class="text-sm opacity-80 line-clamp-2" title={stripHtml(item.description)}>
										{@html item.description}
									</div>
								{:else}
									<span class="opacity-60 text-sm">Keine Beschreibung</span>
								{/if}
							</div>
						</td>

						<td>
							<div class="flex gap-1 flex-row items-center">
								<p class="font-mono">{Number(item.price ?? 0)}</p>
								<Currency />
							</div>
						</td>

						<td>
							<div class="flex gap-1 flex-row items-center">
								<p class="font-mono">{Number(item.euroPrice ?? 0)}</p>
								<Euro size={14} />
							</div>
						</td>

						<td class="font-mono">
							{formatMax(Number(item.max ?? -1))}
						</td>

						<td class="text-right">
							<div class="flex justify-end gap-2">
								<button
									class="btn btn-ghost btn-xs"
									on:click={() => openEditModal(item)}
									title="Bearbeiten"
								>
									<PenIcon class="w-4 h-4" />
								</button>

								<button
									class="btn btn-ghost btn-xs text-error"
									on:click={() => openDeleteModal(item)}
									title="Löschen"
								>
									<Trash2Icon class="w-4 h-4" />
								</button>
							</div>
						</td>
					</tr>
				{/each}
			</tbody>
		</table>
	</div>

	<div class="flex justify-center mt-5">
		<div class="join">
			<button class="join-item btn" on:click={pageDown} disabled={page <= 1}>«</button>
			<button class="join-item btn btn-ghost"
				>Seite <span class="font-mono mx-1">{page}</span> /
				<span class="font-mono">{maxPage}</span></button
			>
			<button class="join-item btn" on:click={pageUp} disabled={page >= maxPage}>»</button>
		</div>
	</div>
</AdminPanel>
<!-- #endregion HTML -->

<!-- #region Modals -->
<!-- Create Modal -->
<dialog class="modal" bind:this={createModal}>
	<div class="modal-box max-w-2xl">
		<h3 class="text-lg font-bold">Artikel erstellen</h3>

		<div class="space-y-4 mt-4">
			<div>
				<p class="opacity-70 text-sm mb-2">Name:</p>
				<input
					type="text"
					class="input input-bordered w-full"
					autocomplete="off"
					bind:value={createName}
					placeholder="Artikel Name"
				/>
			</div>

			<div>
				<p class="opacity-70 text-sm mb-2">Beschreibung:</p>
				<textarea
					class="textarea textarea-bordered w-full h-24"
					autocomplete="off"
					bind:value={createDescription}
					placeholder="Beschreibung (HTML erlaubt)"
				></textarea>
			</div>

			<div>
				<p class="opacity-70 text-sm mb-2">Kategorie:</p>
				<select class="select select-bordered w-full" bind:value={createCategory}>
					<option value="">Ohne Kategorie</option>
					{#each categoryOptions as category}
						<option value={category.value}>{category.label}</option>
					{/each}
				</select>
			</div>

			<div>
				<p class="opacity-70 text-sm mb-2">Preis:</p>
				<div class="flex items-center gap-2">
					<input
						type="number"
						step="0.01"
						class="input input-bordered w-full"
						autocomplete="off"
						bind:value={createPrice}
						placeholder="0.00"
					/>
					<Currency size={32} />
				</div>
			</div>

			<div>
				<p class="opacity-70 text-sm mb-2">Preis (Euro):</p>
				<div class="flex items-center gap-2">
					<input
						type="number"
						step="0.01"
						class="input input-bordered w-full"
						autocomplete="off"
						bind:value={createEuroPrice}
						placeholder="0.00"
					/>
					<Euro size={32} />
				</div>
			</div>

			<div>
				<p class="opacity-70 text-sm mb-2">Maximum (-1 für unbegrenzt):</p>
				<input
					type="number"
					class="input input-bordered w-full"
					autocomplete="off"
					bind:value={createMax}
					placeholder="-1"
				/>
			</div>
		</div>

		<div class="modal-action">
			<button
				class="btn btn-outline"
				on:click={() => {
					createModal.close();
					resetCreateForm();
				}}>Abbrechen</button
			>
			<button class="btn btn-primary" on:click={createItem} disabled={!createName.trim()}
				>Erstellen</button
			>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Edit Modal -->
<dialog class="modal" bind:this={editModal}>
	<div class="modal-box max-w-2xl">
		<h3 class="text-lg font-bold">Artikel bearbeiten</h3>

		<div class="space-y-4 mt-4">
			<div>
				<p class="opacity-70 text-sm mb-2">Name:</p>
				<input
					type="text"
					class="input input-bordered w-full"
					autocomplete="off"
					bind:value={editName}
					placeholder="Artikel Name"
				/>
			</div>

			<div>
				<p class="opacity-70 text-sm mb-2">Beschreibung:</p>
				<textarea
					class="textarea textarea-bordered w-full h-24"
					autocomplete="off"
					bind:value={editDescription}
					placeholder="Beschreibung (HTML erlaubt)"
				></textarea>
			</div>

			<div>
				<p class="opacity-70 text-sm mb-2">Kategorie:</p>
				<select class="select select-bordered w-full" bind:value={editCategory}>
					<option value="">Ohne Kategorie</option>
					{#each categoryOptions as category}
						<option value={category.value}>{category.label}</option>
					{/each}
				</select>
			</div>

			<div>
				<p class="opacity-70 text-sm mb-2">Preis:</p>
				<div class="flex items-center gap-2">
					<input
						type="number"
						step="0.01"
						class="input input-bordered w-full"
						autocomplete="off"
						bind:value={editPrice}
						placeholder="0.00"
					/>
					<Currency size={32} />
				</div>
			</div>

			<div>
				<p class="opacity-70 text-sm mb-2">Preis (Euro):</p>
				<div class="flex items-center gap-2">
					<input
						type="number"
						step="0.01"
						class="input input-bordered w-full"
						autocomplete="off"
						bind:value={editEuroPrice}
						placeholder="0.00"
					/>
					<Euro size={32} />
				</div>
			</div>

			<div>
				<p class="opacity-70 text-sm mb-2">Maximum (-1 für unbegrenzt):</p>
				<input
					type="number"
					class="input input-bordered w-full"
					autocomplete="off"
					bind:value={editMax}
					placeholder="-1"
				/>
			</div>
		</div>

		<div class="modal-action">
			<button class="btn btn-outline" on:click={() => editModal.close()}>Abbrechen</button>
			<button class="btn btn-primary" on:click={updateItem} disabled={!editName.trim()}
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
		<h3 class="text-lg font-bold">Mehrere Artikel bearbeiten</h3>

		<div class="mt-4 space-y-4">
			<div class="flex items-center justify-between">
				<span>Preis aktualisieren:</span>
				<input type="checkbox" class="checkbox bg-base-100" bind:checked={massEditUpdatePrice} />
			</div>
			{#if massEditUpdatePrice}
				<div class="flex items-center gap-2">
					<input
						type="number"
						step="0.01"
						class="input input-bordered w-full"
						autocomplete="off"
						bind:value={massEditPrice}
						placeholder="Neuer Preis"
					/>
					<Currency size={32} />
				</div>
			{/if}

			<div class="flex items-center justify-between">
				<span>Euro Preis aktualisieren:</span>
				<input type="checkbox" class="checkbox bg-base-100" bind:checked={massEditUpdateEuroPrice} />
			</div>

			{#if massEditUpdateEuroPrice}
				<div class="flex items-center gap-2">
					<input
						type="number"
						step="0.01"
						class="input input-bordered w-full"
						autocomplete="off"
						bind:value={massEditEuroPrice}
						placeholder="Neuer Euro Preis"
					/>
					<Euro size={32} />
				</div>
			{/if}

			<div class="flex items-center justify-between">
				<span>Maximum aktualisieren:</span>
				<input type="checkbox" class="checkbox bg-base-100" bind:checked={massEditUpdateMax} />
			</div>
			{#if massEditUpdateMax}
				<input
					type="number"
					class="input input-bordered w-full"
					autocomplete="off"
					bind:value={massEditMax}
					placeholder="Neues Maximum (-1 für unbegrenzt)"
				/>
			{/if}
		</div>

		<div class="modal-action">
			<button class="btn btn-outline" on:click={() => massEditModal.close()}>Abbrechen</button>
			<button class="btn btn-primary" on:click={massUpdateItems}>Aktualisieren</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Delete Modal -->
<dialog class="modal" bind:this={deleteModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Artikel löschen</h3>
		<p class="py-4">
			Sind Sie sicher, dass Sie "{deleteItemName}" löschen möchten? Diese Aktion kann nicht
			rückgängig gemacht werden.
		</p>

		<div class="modal-action">
			<button class="btn btn-outline" on:click={() => deleteModal.close()}>Abbrechen</button>
			<button class="btn btn-error" on:click={deleteItem}>Löschen</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Mass Delete Modal -->
<dialog class="modal" bind:this={massDeleteModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Mehrere Artikel löschen</h3>
		<p class="py-4">
			Sind Sie sicher, dass Sie {selectedItems.size} Artikel löschen möchten? Diese Aktion kann nicht
			rückgängig gemacht werden.
		</p>

		<div class="modal-action">
			<button class="btn btn-outline" on:click={() => massDeleteModal.close()}>Abbrechen</button>
			<button class="btn btn-error" on:click={massDeleteItems}>Alle löschen</button>
		</div>
	</div>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>
<!-- #endregion Modals -->

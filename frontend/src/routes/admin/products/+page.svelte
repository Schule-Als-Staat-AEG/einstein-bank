<script lang="ts">
	import db from '$lib/connections';
	import AdminPanel from '$modules/AdminPanel.svelte';
	import Currency from '$modules/Currency.svelte';
	import UserSelectModal from '$modules/modals/UserSelectModal.svelte';
	import { Pen, Trash } from '@lucide/svelte';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';

	let page: number = 1;
	let perPage: number = 30;
	let pages: number = 1;
	let filter: string = '';
	let loading = false;
	let totalItems: number = 0;

	let products: any[] = [];
	let selectedProducts: Set<string> = new Set();

	let createCompany = '';
	let createName = '';
	let createPrice = 0;

	let editProductCompany = '';
	let editProductName = '';
	let editProductPrice = 0;
	let editProductId = '';

	let deleteProductId: string = '';
	let deleteProductName: string = '';

	let companyInsertLocation: string = '';
	let companies: any[] = [];
	let companyFilter: string = '';

	let createModal: HTMLDialogElement;
	let editModal: HTMLDialogElement;
	let companySelectorModal: UserSelectModal;
	let deleteConfirmModal: HTMLDialogElement;
	let bulkDeleteConfirmModal: HTMLDialogElement;

	onMount(async () => {
		if (!browser) return;
		await fetchProducts();
	});
	async function fetchProducts() {
		loading = true;
		const result = await db.collection('products').getList(page, perPage, {
			sort: '-company,-created',
			filter:
				filter && filter.trim()
					? `name ~ "${filter}" || company.name ~ "${filter}" || id ~ "${filter}"`
					: undefined,
			requestKey: null,
			expand: 'company'
		});
		pages = result.totalPages;
		products = result.items;
		totalItems = result.totalItems;
		loading = false;
	}
	async function fetchCompanies() {
		const result = await db.collection('users').getFullList(1000, {
			filter: `isCompany = true && (name ~ "${companyFilter}" || id ~ "${companyFilter}" || companyName ~ "${companyFilter}")`,
			sort: '+companyName,-created'
		});
		companies = result;
	}
	async function createProduct() {
		createModal.close();
		if (createCompany == '') {
			alert('Bitte wählen Sie eine Firma aus.');
			return;
		}
		if (createName.trim() == '') {
			alert('Bitte geben Sie einen Produktnamen ein.');
			return;
		}
		await db.collection('products').create({
			name: createName,
			price: createPrice,
			company: createCompany
		});
		await fetchProducts();
	}
	async function editProduct(id: string) {
		editModal.close();
		await db.collection('products').update(id, {
			name: editProductName,
			price: editProductPrice,
			company: editProductCompany
		});
		await fetchProducts();
	}
	async function deleteProduct() {
		await db.collection('products').delete(deleteProductId, { requestKey: null });
		deleteConfirmModal.close();
		deleteProductId = '';
		deleteProductName = '';
		await fetchProducts();
	}
	async function bulkDeleteProducts() {
		bulkDeleteConfirmModal.close();
		await Promise.all(
			Array.from(selectedProducts).map(async (id) => {
				await db.collection('products').delete(id, { requestKey: null });
			})
		);
		selectedProducts.clear();
		selectedProducts = new Set();
		await fetchProducts();
	}
	function setCompany(id: string) {
		if (companyInsertLocation == 'create') {
			createCompany = id;
		} else if (companyInsertLocation == 'edit') {
			editProductCompany = id;
		}
	}
	function openCompanySelector(location: string) {
		companyInsertLocation = location;
		companyFilter = '';
		fetchCompanies();
		companySelectorModal.showModal();
	}
	function openEditModal(product: any) {
		editProductName = product.name;
		editProductPrice = product.price;
		editProductCompany = product.company;
		editProductId = product.id;
		editModal.showModal();
	}
	function openDeleteConfirmModal(id: string, name: string) {
		deleteProductId = id;
		deleteProductName = name;
		deleteConfirmModal.showModal();
	}
	function openCreateModal() {
		createName = '';
		createPrice = 0;
		createCompany = '';
		createModal.showModal();
	}
	function openBulkDeleteConfirmModal() {
		// You can set some state here if needed
		bulkDeleteConfirmModal.showModal();
	}
	function toggleSelectProduct(id: string) {
		if (selectedProducts.has(id)) {
			selectedProducts.delete(id);
		} else {
			selectedProducts.add(id);
		}
		selectedProducts = new Set(selectedProducts);
	}
	function toggleSelectAll() {
		if (selectedProducts.size === products.length) {
			selectedProducts.clear();
		} else {
			products.forEach((product) => {
				selectedProducts.add(product.id);
			});
		}
		selectedProducts = new Set(selectedProducts);
	}
	function goToPage(newPage: number) {
		if (newPage < 1 || newPage > pages) return;
		page = newPage;
		fetchProducts();
	}
</script>

<title>Admin - Produkte</title>
<AdminPanel
	title="Produkte"
	description="Verwaltung aller Produkte"
	showCreateButton={true}
	showMassDeleteButton={true}
	massDeleteButtonText="Massenlöschung"
	{totalItems}
	selectedItems={selectedProducts.size}
	on:create={() => openCreateModal()}
	on:massDelete={() => openBulkDeleteConfirmModal()}
>
	<svelte:fragment slot="filter">
		<div class="flex gap-3 flex-row justify-between">
			<div class="flex w-full">
				<input
					class="input input-bordered w-full"
					type="text"
					placeholder="Nach Name, Firma oder ID filtern..."
					bind:value={filter}
					autocomplete="off"
					on:input={() => {
						page = 1;
						fetchProducts();
					}}
				/>
			</div>

			<div class="flex gap-2 flex-wrap">
				<select
					class="select select-bordered"
					bind:value={perPage}
					on:change={() => fetchProducts()}
				>
					<option value="10">10 / Seite</option>
					<option selected value="30">30 / Seite</option>
					<option value="50">50 / Seite</option>
					<option value="100">100 / Seite</option>
				</select>

				<div class="flex-1"></div>
			</div>
		</div>
		{#if loading}
			<div class="text-sm opacity-70">Lade Produkte…</div>
		{/if}
	</svelte:fragment>

	<div class="overflow-x-auto rounded-2xl border border-base-300">
		<table class="table table-zebra">
			<!-- head -->
			<thead>
				<tr>
					<th>
						<label>
							<input
								type="checkbox"
								class="checkbox bg-base-100"
								on:change={toggleSelectAll}
								checked={selectedProducts.size === products.length}
							/>
						</label>
					</th>
					<th>ID</th>
					<th>Firma</th>
					<th>Name</th>
					<th>Preis</th>
					<th>Aktionen</th>
				</tr>
			</thead>
			<tbody>
				{#each products as product}
					<tr>
						<th>
							<label>
								<input
									type="checkbox"
									class="checkbox bg-base-100"
									on:change={() => toggleSelectProduct(product.id)}
									checked={selectedProducts.has(product.id)}
								/>
							</label>
						</th>
						<td class="font-mono opacity-70">
							{product.id}
						</td>
						<td>
							<div class="font-semibold">{product.expand.company.companyName}</div>
							<span class="badge badge-ghost badge-sm">{product.company}</span>
						</td>
						<td>{product.name}</td>
						<td>
							<div class="flex flex-row items-center gap-1">
								<p>{product.price}</p>
								<Currency></Currency>
							</div>
						</td>
						<td>
							<div class="flex flex-row gap-2">
								<button class="btn btn-ghost btn-sm" on:click={() => openEditModal(product)}
									><Pen size={16}></Pen></button
								>
								<button
									class="btn btn-ghost btn-sm btn-error"
									on:click={() => openDeleteConfirmModal(product.id, product.name)}
									><Trash size={16}></Trash></button
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

<!-- Create Product Modal -->
<dialog id="create_product_modal" class="modal" bind:this={createModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Neues Produkt erstellen</h3>
		{#if createCompany == ''}
			<button
				class="btn btn-primary mt-4 w-full"
				on:click={() => {
					openCompanySelector('create');
				}}>Firma auswählen</button
			>
		{:else}
			<button
				class="btn btn-secondary mt-4 w-full"
				on:click={() => {
					openCompanySelector('create');
				}}>Firma ausgewählt (ID: {createCompany}) - Ändern</button
			>
		{/if}
		<p class="text-sm opacity-70 mt-4">Produktname:</p>
		<input
			type="text"
			placeholder="Produktname"
			class="input input-bordered w-full mt-1"
			autocomplete="off"
			bind:value={createName}
		/>
		<p class="text-sm opacity-70 mt-4">Preis:</p>
		<div class="flex flex-row justify-between w-full">
			<input
				type="number"
				placeholder="Preis"
				class="input input-bordered w-full mt-1"
				autocomplete="off"
				bind:value={createPrice}
			/>
			<Currency class="ml-2 mt-1" size={40}></Currency>
		</div>
		<div class="modal-action">
			<button class="btn btn-outline btn-error" on:click={() => createModal.close()}
				>Abbrechen</button
			>
			<button class="btn btn-success" on:click={() => createProduct()}>Erstellen</button>
		</div>
	</div>
	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Edit Product Modal -->
<dialog id="edit_product_modal" class="modal" bind:this={editModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">Produkt bearbeiten</h3>
		{#if editProductCompany == ''}
			<button
				class="btn btn-primary mt-4 w-full"
				on:click={() => {
					openCompanySelector('edit');
				}}>Firma auswählen</button
			>
		{:else}
			<button
				class="btn btn-secondary mt-4 w-full"
				on:click={() => {
					openCompanySelector('edit');
				}}>Firma ausgewählt (ID: {editProductCompany}) - Ändern</button
			>
		{/if}
		<p class="text-sm opacity-70 mt-2">Produktname:</p>
		<input
			type="text"
			placeholder="Produktname"
			class="input input-bordered w-full mt-1"
			autocomplete="off"
			bind:value={editProductName}
		/>
		<p class="text-sm opacity-70 mt-2">Preis:</p>
		<div class="flex flex-row justify-between w-full">
			<input
				type="number"
				placeholder="Preis"
				class="input input-bordered w-full mt-1"
				autocomplete="off"
				bind:value={editProductPrice}
			/>
			<Currency class="ml-2 mt-1" size={40}></Currency>
		</div>
		<div class="modal-action">
			<button class="btn btn-outline btn-error" on:click={() => editModal.close()}>Abbrechen</button
			>
			<button class="btn btn-success" on:click={() => editProduct(editProductId)}>Speichern</button>
		</div>
	</div>
	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Delete Confirm Modal -->
<dialog id="delete_confirm_modal" class="modal" bind:this={deleteConfirmModal}>
	<form method="dialog" class="modal-box">
		<h3 class="text-lg font-bold">Produkt löschen</h3>
		<p class="py-4">
			Möchtest du "{deleteProductName}" wirklich löschen? Diese Aktion kann nicht rückgängig gemacht
			werden.
		</p>
		<div class="modal-action">
			<button class="btn btn-outline" on:click={() => deleteConfirmModal.close()}>Abbrechen</button>
			<button
				class="btn btn-error"
				on:click={(e) => {
					e.preventDefault();
					deleteProduct();
				}}>Löschen</button
			>
		</div>
	</form>
	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<!-- Bulk Delete Confirm Modal -->
<dialog id="bulk_delete_confirm_modal" class="modal" bind:this={bulkDeleteConfirmModal}>
	<form method="dialog" class="modal-box">
		<h3 class="text-lg font-bold">Produkte löschen</h3>
		<p class="py-4">
			Möchtest du {selectedProducts.size} Produkt(e) wirklich löschen? Diese Aktion kann nicht rückgängig
			gemacht werden.
		</p>
		<div class="modal-action">
			<button class="btn btn-outline" on:click={() => bulkDeleteConfirmModal.close()}
				>Abbrechen</button
			>
			<button
				class="btn btn-error"
				on:click={(e) => {
					e.preventDefault();
					bulkDeleteProducts();
				}}>Löschen</button
			>
		</div>
	</form>
	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<UserSelectModal
	bind:this={companySelectorModal}
	companyOnly={true}
	on:select={(e) => setCompany(e.detail.id)}
	adminMode={true}
/>

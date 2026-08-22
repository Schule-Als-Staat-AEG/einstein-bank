<script lang="ts">
	import { getAuthStore } from '$lib/auth';
	import db from '$lib/connections';
	import Currency from '$modules/Currency.svelte';
	import Site from '$modules/Site.svelte';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';

	import {
		Package,
		Search,
		Plus,
		Trash2,
		Pencil,
		Hash,
		Tag,
		Coins,
		ChevronLeft,
		ChevronRight,
		X,
		Save
	} from '@lucide/svelte';

	// Page vars
	let perPage: number = 30;
	let page: number = 1;
	let totalPages: number = 1;
	let filter: string = '';

	let products: any[] = [];
	let selectedProducts: Set<string> = new Set();

	let createProductModal: HTMLDialogElement;
	let createProductName: string = '';
	let createProductPrice: number = 0;

	let editProductModal: HTMLDialogElement;
	let editProduct: any = null;
	let editProductName: string = '';
	let editProductPrice: number = 0;

	let loading = true;
	let saving = false;

	let searchTimer: any = null;

	onMount(async () => {
		if (!browser) return;
		await fetchProducts();
	});

	async function fetchProducts() {
		loading = true;

		db.collection('products')
			.getList(page, perPage, {
				filter: `company.id = "${getAuthStore().record.id}" && name ~ "${filter}"`,
				sort: '+name'
			})
			.then((paged) => {
				products = paged.items || [];
				totalPages = paged.totalPages || 1;

				// keep only selections that still exist on this page
				selectedProducts = new Set(
					[...selectedProducts].filter((id) => products.some((p) => p.id === id))
				);

				loading = false;
			})
			.catch((e) => {
				console.error('fetchProducts failed', e);
				products = [];
				totalPages = 1;
				selectedProducts = new Set();
				loading = false;
			});
	}

	function onFilterInput() {
		if (searchTimer) clearTimeout(searchTimer);
		searchTimer = setTimeout(() => {
			page = 1;
			fetchProducts();
		}, 220);
	}

	function pageUp() {
		if (page < totalPages) {
			page += 1;
			fetchProducts();
		}
	}

	function pageDown() {
		if (page > 1) {
			page -= 1;
			fetchProducts();
		}
	}

	function toggleSelected(productId: string) {
		if (selectedProducts.has(productId)) selectedProducts.delete(productId);
		else selectedProducts.add(productId);
		selectedProducts = new Set(selectedProducts);
	}

	function toggleAllSelected() {
		if (products.length > 0 && selectedProducts.size === products.length) selectedProducts.clear();
		else products.forEach((p) => selectedProducts.add(p.id));
		selectedProducts = new Set(selectedProducts);
	}

	async function deleteSelected() {
		if (!selectedProducts.size) return;

		if (
			!confirm(
				`Möchtest du wirklich die ausgewählten ${selectedProducts.size} Produkte löschen? Diese Aktion kann nicht rückgängig gemacht werden.`
			)
		)
			return;

		await Promise.all(
			Array.from(selectedProducts).map(async (productId) => {
				await db.collection('products').delete(productId);
			})
		);

		selectedProducts = new Set();
		await fetchProducts();
	}

	function deleteProduct(productId: string) {
		if (
			!confirm(
				`Möchtest du dieses Produkt wirklich löschen? Diese Aktion kann nicht rückgängig gemacht werden.`
			)
		)
			return;

		db.collection('products')
			.delete(productId)
			.then(async () => fetchProducts());
	}

	function openCreateProductModal() {
		createProductName = '';
		createProductPrice = 0;
		createProductModal.showModal();
	}

	async function createProduct() {
		if (saving) return;
		if (!createProductName.trim()) return alert('Bitte gib einen Produktnamen ein.');
		if (isNaN(createProductPrice) || Number(createProductPrice) < 0)
			return alert('Bitte gib einen gültigen Preis ein.');

		saving = true;

		try {
			await db.collection('products').create({
				name: createProductName.trim(),
				price: Number(createProductPrice),
				company: getAuthStore().record.id
			});
			await fetchProducts();
			createProductModal.close();
		} catch (e) {
			console.error('createProduct failed', e);
			alert('Fehler beim Erstellen.');
		} finally {
			saving = false;
		}
	}

	function openEditProductModal(product: any) {
		editProduct = product;
		editProductName = product?.name ?? '';
		editProductPrice = Number(product?.price ?? 0);
		editProductModal.showModal();
	}

	async function saveEditedProduct() {
		if (!editProduct?.id) return;
		if (saving) return;
		if (!editProductName.trim()) return alert('Bitte gib einen Produktnamen ein.');
		if (isNaN(editProductPrice) || Number(editProductPrice) < 0)
			return alert('Bitte gib einen gültigen Preis ein.');

		saving = true;

		try {
			await db.collection('products').update(editProduct.id, {
				name: editProductName.trim(),
				price: Number(editProductPrice)
			});
			await fetchProducts();
			editProductModal.close();
		} catch (e) {
			console.error('saveEditedProduct failed', e);
			alert('Fehler beim Speichern.');
		} finally {
			saving = false;
		}
	}

	$: allSelected = products.length > 0 && selectedProducts.size === products.length;
</script>

<title>Produkte</title>
<Site>
	<div class="flex flex-col gap-4">
		<!-- Header -->
		<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
			<div class="flex flex-col md:flex-row md:items-center md:justify-between gap-3">
				<div class="flex items-center gap-3">
					<div
						class="w-11 h-11 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center shadow"
					>
						<Package size={20} />
					</div>
					<div class="flex flex-col">
						<h1 class="font-bold text-2xl leading-tight">Produkte verwalten</h1>
						<p class="text-sm opacity-70">
							Produkte für dein Kassensystem – schnell suchen, bearbeiten und entfernen.
						</p>
					</div>
				</div>

				<div class="flex items-center gap-2 flex-wrap">
					<button class="btn btn-success" on:click={openCreateProductModal}>
						<Plus size={18} />
						Neues Produkt
					</button>

					<button
						class="btn btn-outline btn-error"
						on:click={deleteSelected}
						disabled={!selectedProducts.size}
					>
						<Trash2 size={18} />
						Ausgewählte löschen
					</button>
				</div>
			</div>

			<div class="mt-4 flex flex-col md:flex-row gap-2 md:items-center">
				<label class="input input-bordered w-full flex items-center gap-2 rounded-2xl">
					<Search size={18} class="opacity-60" />
					<input
						type="text"
						class="grow"
						placeholder="Produkte filtern…"
						bind:value={filter}
						on:input={onFilterInput}
						autocomplete="off"
					/>
				</label>

				<div class="flex items-center gap-2">
					{#if loading}
						<span class="loading loading-spinner"></span>
					{/if}
					<span class="badge badge-ghost">
						Seite <span class="font-mono mx-1">{page}</span> /
						<span class="font-mono">{totalPages}</span>
					</span>
					<span class="badge badge-ghost">
						Markiert: <span class="font-mono ml-1">{selectedProducts.size}</span>
					</span>
				</div>
			</div>
		</div>

		<!-- Table -->
		<div class="rounded-2xl bg-base-200 border border-base-300 shadow overflow-hidden">
			{#if loading}
				<div class="p-10 flex items-center justify-center">
					<span class="loading loading-spinner loading-lg"></span>
				</div>
			{:else}
				<div class="overflow-x-auto">
					<table class="table">
						<thead>
							<tr>
								<th class="w-10">
									<label>
										<input
											type="checkbox"
											class="checkbox"
											on:change={toggleAllSelected}
											checked={allSelected}
										/>
									</label>
								</th>
								<th class="whitespace-nowrap">
									<div class="flex items-center gap-2">
										<Hash size={16} class="opacity-60" />
										<span>ID</span>
									</div>
								</th>
								<th class="whitespace-nowrap">
									<div class="flex items-center gap-2">
										<Tag size={16} class="opacity-60" />
										<span>Name</span>
									</div>
								</th>
								<th class="whitespace-nowrap">
									<div class="flex items-center gap-2">
										<Coins size={16} class="opacity-60" />
										<span>Preis</span>
									</div>
								</th>
								<th class="whitespace-nowrap">Aktionen</th>
							</tr>
						</thead>

						<tbody>
							{#if !products.length}
								<tr>
									<td colspan="5" class="py-10 text-center opacity-70">
										Keine Produkte gefunden.
									</td>
								</tr>
							{:else}
								{#each products as product}
									<tr class="hover:bg-base-300/40">
										<td>
											<label>
												<input
													type="checkbox"
													class="checkbox"
													on:change={() => toggleSelected(product.id)}
													checked={selectedProducts.has(product.id)}
												/>
											</label>
										</td>

										<td class="font-mono opacity-70 max-w-[16rem] truncate">{product.id}</td>

										<td class="font-semibold">{product.name}</td>

										<td>
											<div class="flex items-center gap-1">
												<p class="font-semibold">{product.price}</p>
												<Currency size={18} />
											</div>
										</td>

										<td>
											<div class="flex items-center gap-2">
												<button
													class="btn btn-ghost btn-sm"
													on:click={() => openEditProductModal(product)}
													aria-label="Bearbeiten"
												>
													<Pencil size={18} />
												</button>
												<button
													class="btn btn-ghost btn-sm btn-error"
													on:click={() => deleteProduct(product.id)}
													aria-label="Löschen"
												>
													<Trash2 size={18} />
												</button>
											</div>
										</td>
									</tr>
								{/each}
							{/if}
						</tbody>
					</table>
				</div>

				<!-- Pagination -->
				<div class="p-4 flex justify-center">
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
			{/if}
		</div>
	</div>

	<!-- Create Product Modal -->
	<dialog id="createProductModal" class="modal" bind:this={createProductModal}>
		<div class="modal-box w-full max-w-xl rounded-2xl border border-base-300">
			<header class="flex items-start justify-between gap-3">
				<div>
					<h3 class="text-lg font-bold">Neues Produkt</h3>
					<p class="text-sm opacity-70">Lege ein Produkt für dein Kassensystem an.</p>
				</div>
				<button
					class="btn btn-ghost btn-sm"
					on:click={() => createProductModal.close()}
					aria-label="Schließen"
				>
					<X size={18} />
				</button>
			</header>

			<div class="mt-5 flex flex-col gap-4">
				<div class="rounded-2xl bg-base-200 border border-base-300 p-4">
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="text-sm opacity-70">Name</label>
					<input
						type="text"
						placeholder="Produktname"
						class="input input-bordered w-full rounded-2xl mt-2"
						bind:value={createProductName}
						autocomplete="off"
					/>
				</div>

				<div class="rounded-2xl bg-base-200 border border-base-300 p-4">
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="text-sm opacity-70">Preis</label>
					<div class="mt-2 flex items-center gap-2">
						<input
							type="number"
							placeholder="Preis"
							class="input input-bordered w-full rounded-2xl"
							bind:value={createProductPrice}
							autocomplete="off"
						/>
						<div
							class="w-12 h-12 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center"
						>
							<Currency size={26} />
						</div>
					</div>
				</div>
			</div>

			<div class="modal-action">
				<button
					class="btn btn-ghost btn-error"
					on:click={() => createProductModal.close()}
					disabled={saving}
				>
					Abbrechen
				</button>
				<button class="btn btn-success" on:click={createProduct} disabled={saving}>
					{#if saving}
						<span class="loading loading-spinner"></span>
						Speichern…
					{:else}
						<Save size={18} />
						Produkt erstellen
					{/if}
				</button>
			</div>
		</div>

		<form method="dialog" class="modal-backdrop">
			<button>close</button>
		</form>
	</dialog>

	<!-- Edit Product Modal -->
	<dialog id="editProductModal" class="modal" bind:this={editProductModal}>
		<div class="modal-box w-full max-w-xl rounded-2xl border border-base-300">
			<header class="flex items-start justify-between gap-3">
				<div>
					<h3 class="text-lg font-bold">Produkt bearbeiten</h3>
					<p class="text-sm opacity-70">
						ID: <span class="font-mono text-xs">{editProduct?.id ?? '-'}</span>
					</p>
				</div>
				<button
					class="btn btn-ghost btn-sm"
					on:click={() => editProductModal.close()}
					aria-label="Schließen"
				>
					<X size={18} />
				</button>
			</header>

			<div class="mt-5 flex flex-col gap-4">
				<div class="rounded-2xl bg-base-200 border border-base-300 p-4">
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="text-sm opacity-70">Name</label>
					<input
						type="text"
						placeholder="Produktname"
						class="input input-bordered w-full rounded-2xl mt-2"
						bind:value={editProductName}
						autocomplete="off"
					/>
				</div>

				<div class="rounded-2xl bg-base-200 border border-base-300 p-4">
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="text-sm opacity-70">Preis</label>
					<div class="mt-2 flex items-center gap-2">
						<input
							type="number"
							placeholder="Preis"
							class="input input-bordered w-full rounded-2xl"
							bind:value={editProductPrice}
							autocomplete="off"
						/>
						<div
							class="w-12 h-12 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center"
						>
							<Currency size={26} />
						</div>
					</div>
				</div>
			</div>

			<div class="modal-action">
				<button
					class="btn btn-ghost btn-error"
					on:click={() => editProductModal.close()}
					disabled={saving}
				>
					Abbrechen
				</button>
				<button class="btn btn-success" on:click={saveEditedProduct} disabled={saving}>
					{#if saving}
						<span class="loading loading-spinner"></span>
						Speichern…
					{:else}
						<Save size={18} />
						Speichern
					{/if}
				</button>
			</div>
		</div>

		<form method="dialog" class="modal-backdrop">
			<button>close</button>
		</form>
	</dialog>
</Site>

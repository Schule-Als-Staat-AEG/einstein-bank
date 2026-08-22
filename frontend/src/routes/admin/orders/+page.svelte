<script lang="ts">
	import db, { server } from '$lib/connections';
	import AdminPanel from '$modules/AdminPanel.svelte';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';
	import {
		Pen,
		BadgeQuestionMark,
		HandCoins,
		Trash2,
		PlusCircle,
		MinusCircle,
		User,

		Euro

	} from '@lucide/svelte';
	import Currency from '$modules/Currency.svelte';
	import { getSetting } from '$lib/settings';
	import UserSelectModal from '$modules/modals/UserSelectModal.svelte';

	let perPage = 30;
	let page = 1;
	let totalPages = 1;
	let totalItems = 0;
	let loading = true;

	let orders: any[] = [];
	let selectedOrders: Set<string> = new Set();

	// Create Modal
	let createModal: HTMLDialogElement;
	let newOrderCompany: any = null;
	let newOrderItems: any[] = [];
	let newOrderStatus: string = 'requested';
	let newOrderExtras: string = "";
	let newOrderExtraCost: number = 0;
	let newOrderExtraEuroCost: number = 0;
	let newOrderMoveMoney = true;
	let newOrderMoveEuro = true;

	// Edit Modal
	let editModal: HTMLDialogElement;
	let editOrderId: string = '';
	let editOrderCompany: any = null;
	let editOrderItems: any[] = [];
	let editOrderUpdates: any[] = [];
	let editOrderExtras: string = "";
	let editOrderExtraCost: number = 0;
	let editOrderExtraEuroCost: number = 0;
	let editOrderMoveMoney = true;
	let editOrderMoveEuro = true;
	let editOrderStatus: string = 'requested';

	// Mass Delete Modal
	let massDeleteModal: HTMLDialogElement;
	let massDeleteOrders: any[] = [];
	let massDeleteMoveMoney = true;
	let massDeleteMoveEuro = true;
	let massDeleteTransactionAction: "keep" | "delete" | "update" = "keep";

	// Delete Modal
	let deleteModal: HTMLDialogElement;
	let deleteOrder: any = null;
	let deleteMoveMoney = true;
	let deleteMoveEuro = true;
	let deleteTransactionAction: "keep" | "delete" | "update" = "keep";

	// Select User Modal
	let selectUserModal: UserSelectModal;
	let selectUserFilter: string = '';
	let selectUserPerPage: number = 5;
	let selectUserPage: number = 1;
	let selectUserTarget: 'create' | 'edit' | 'massEdit' = 'create';

	// Select Product Modal
	let selectProductModal: HTMLDialogElement;
	let selectProductFilter: string = '';
	let selectProductCategory: string = 'all';
	let selectProductProducts: any[] = [];
	let selectProductPerPage: number = 10;
	let selectProductPage: number = 1;
	let selectProductTotalPages: number = 1;
	let selectProductTotalItems: number = 0;
	let selectProductTarget: 'create' | 'edit' | 'massEdit' = 'create';
	let selectProductLoading: boolean = false;

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

	let massDeleteCheck1: HTMLInputElement;
	let massDeleteCheck2: HTMLInputElement;
	let massDeleteCheck3: HTMLInputElement;

	let deleteCheck1: HTMLInputElement;
	let deleteCheck2: HTMLInputElement;
	let deleteCheck3: HTMLInputElement;

	onMount(async () => {
		if (!browser) return;
		await fetchOrders();
	});
	async function fetchOrders() {
		loading = true;
		try {
			orders = [];
			const data = await db.collection('orders').getList(page, perPage, {
				sort: '-status,-company,-created',
				expand: 'company,items,items.item,transaction',
				requestKey: null
			});
			orders = data.items;
			orders = [...orders];
			totalPages = data.totalPages;
			totalItems = data.totalItems;
			console.log(orders);
		} catch (error) {
			console.error('Fehler beim Laden der Bestellungen:', error);
		} finally {
			loading = false;
		}
	}
	async function fetchProducts() {
		try {
			selectProductLoading = true;
			const filterParts = [`name~"${selectProductFilter}"`];
			if (selectProductCategory !== 'all') {
				filterParts.push(`category="${selectProductCategory}"`);
			}

			const data = await db.collection('stock').getList(selectProductPage, selectProductPerPage, {
				sort: 'name',
				filter: filterParts.join(' && '),
				fields: 'id,name,description,price,euroPrice,category'
			});
			selectProductProducts = data.items;
			selectProductTotalItems = data.totalItems;
			selectProductTotalPages = data.totalPages;
		} catch (error) {
			console.error('Fehler beim Laden der Produkte:', error);
		} finally {
			selectProductLoading = false;
		}
	}
	// Create Modal Functions
	function openCreateModal() {
		newOrderCompany = null;
		newOrderItems = [];
		newOrderStatus = 'requested';
		newOrderExtras = "";
		newOrderExtraCost = 0;
		newOrderExtraEuroCost = 0;
		newOrderMoveEuro = true;
		newOrderMoveMoney = true;
		createModal.showModal();
	}

	async function createNewOrder() {
		const rsp = await fetch(server + '/api/admin/order', {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
				Authorization: `Bearer ${db.authStore.token}`
			},
			body: JSON.stringify({
				userID: newOrderCompany ? newOrderCompany.id : null,
				items: newOrderItems.map((item) => ({
					item: item.item,
					available: item.available,
					amount: item.amount,
					action: 'create'
				})),
				status: newOrderStatus,
				extras: newOrderExtras,
				extraCost: newOrderExtraCost,
				extraEuroCost: newOrderExtraEuroCost,
				applyMoneyChanges: newOrderMoveMoney,
				applyEuroChanges: newOrderMoveEuro,
				action: 'create'
			})
		});
		createModal.close();
		await fetchOrders();
	}

	// Other
	function openEditModal(order: any) {
		editOrderId = order.id;
		editOrderCompany = order.expand.company || null;
		editOrderItems = order.expand.items || [];
		editOrderStatus = order.status;
		editOrderExtras = order.extras || "";
		editOrderExtraCost = order.extraCost || 0;
		editOrderExtraEuroCost = order.extraEuroCost || 0;
		editOrderMoveMoney = true;
		editOrderMoveEuro = true;
		editOrderUpdates = [];
		editModal.showModal();
	}
	function openSelectUserModal(target: 'create' | 'edit') {
		selectUserTarget = target;
		selectUserModal.showModal();
	}
	function openSelectProductModal(target: 'create' | 'edit') {
		selectProductTarget = target;
		selectProductFilter = '';
		selectProductCategory = 'all';
		selectProductProducts = [];
		selectProductPage = 1;
		selectProductTotalPages = 1;
		selectProductTotalItems = 0;
		selectProductModal.showModal();
		fetchProducts();
	}
	function openDeleteModal(order: any) {
		deleteOrder = order;
		deleteTransactionAction = "keep";
		deleteMoveMoney = false;
		deleteMoveEuro = false;
		deleteModal.showModal();
	}
	async function confirmDeleteOrder() {
		await fetch(server + '/api/admin/order', {
			method: 'POST',
			headers: {
				Authorization: `Bearer ${db.authStore.token}`
			},
			body: JSON.stringify({
				orderID: deleteOrder.id,
				action: "delete",
				applyMoneyChanges: deleteMoveMoney,
				applyEuroChanges: deleteMoveEuro,
				transactionAction: deleteTransactionAction || "keep"
			})
		});
		deleteModal.close();
		await fetchOrders();
	}
	async function goToPage(newPage: number) {
		if (newPage < 1 || newPage > totalPages) return;
		page = newPage;
		await fetchOrders();
	}
	function toggleSelectOrder(orderId: string) {
		if (selectedOrders.has(orderId)) {
			selectedOrders.delete(orderId);
		} else {
			selectedOrders.add(orderId);
		}
		selectedOrders = new Set(selectedOrders);
	}
	function toggleSelectAllOrders() {
		if (selectedOrders.size === orders.length) {
			selectedOrders.clear();
		} else {
			selectedOrders = new Set(orders.map((order) => order.id));
		}
		selectedOrders = new Set(selectedOrders);
	}
	function onUserSelect(user: any) {
		if (selectUserTarget === 'create') {
			newOrderCompany = user;
		} else if (selectUserTarget === 'edit') {
			editOrderCompany = user;
		}
		selectUserModal.close();
	}
	async function updateOrder() {
		const data = await fetch(server + '/api/admin/order', {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
				Authorization: `Bearer ${db.authStore.token}`
			},
			body: JSON.stringify({
				orderID: editOrderId,
				userID: editOrderCompany ? editOrderCompany.id : null,
				items: editOrderUpdates.map((item) => ({
					item: item.item,
					available: item.available,
					amount: item.amount,
					action: item.action
				})),
				status: editOrderStatus,
				extras: editOrderExtras,
				extraCost: editOrderExtraCost,
				extraEuroCost: editOrderExtraEuroCost,
				applyMoneyChanges: editOrderMoveMoney,
				applyEuroChanges: editOrderMoveEuro,
				action: 'update'
			})
		});
		editOrderUpdates = [];
		editOrderItems = [];
		editModal.close();
		await fetchOrders();
	}

	async function bulkDeleteOrders() {
		await Promise.all(
			Array.from(selectedOrders).map(async (orderId) => {
				await fetch(server + '/api/admin/order', {
					method: 'POST',
					headers: {
						Authorization: `Bearer ${db.authStore.token}`
					},
					body: JSON.stringify({
						orderID: orderId,
						action: "delete",
						applyMoneyChanges: massDeleteMoveMoney,
						applyEuroChanges: massDeleteMoveEuro,
						transactionAction: massDeleteTransactionAction || "keep"
					})
				});
			})
		);
		massDeleteModal.close();
		await fetchOrders();
		selectedOrders = new Set();
	}
	function createAddItem(product: any) {
		newOrderItems.push({
			item: product.id,
			available: 1,
			amount: 1,
			action: 'create',
			expand: {
				item: product
			}
		});
		newOrderItems = [...newOrderItems];
	}
	function createUpdateAmount(item: any, newAmount: number) {
		if (newAmount < 1) return;
		item.available = newAmount;
		item.amount = newAmount;
		newOrderItems = [...newOrderItems];
	}
	function createRemoveItem(item: any) {
		newOrderItems = newOrderItems.filter((i) => i !== item);
	}
	function editAddItem(product: any) {
		// Delete any updates marked as delete with the same item id
		editOrderUpdates = editOrderUpdates.filter((item) => !(item.action === 'delete' && item.item === product.id));

		let existingItem = editOrderItems.findIndex((item) => item.item === product.id);
		let existingUpdate = editOrderUpdates.findIndex((item) => item.item === product.id);

		if (existingItem !== -1)
		{
			editOrderItems[existingItem].available += 1;
			editOrderItems[existingItem].amount += 1;
		} else
		{
			editOrderItems.push({
				item: product.id,
				available: 1,
				amount: 1,
				action: 'create',
				expand: {
					item: product
				}
			});
		}

		if (existingUpdate !== -1) {
			editOrderUpdates[existingUpdate].available += 1;
			editOrderUpdates[existingUpdate].amount += 1;
			editOrderUpdates[existingUpdate].action = 'update';
		} else {
			
			editOrderUpdates.push({
				item: product.id,
				available: 1,
				amount: 1,
				action: 'create',
			});
		}
		editOrderUpdates = [...editOrderUpdates];
		editOrderItems = [...editOrderItems];
	}
	function editUpdateAmount(product: any, amount: number)
	{
		if (amount <= 0)
		{
			editRemoveItem(product);
			return;	
		}
		// Delete any updates marked as delete with the same item id
		editOrderUpdates = editOrderUpdates.filter((item) => !(item.action === 'delete' && item.item === product.item));

		let existingItem = editOrderItems.findIndex((item) => item.item === product.item);
		let existingUpdate = editOrderUpdates.findIndex((item) => item.item === product.item);

		if (existingItem !== -1)
		{
			editOrderItems[existingItem].available = amount;
			editOrderItems[existingItem].amount = amount;
		} else
		{
			editOrderItems.push({
				item: product.item,
				available: amount,
				amount: amount,
				action: 'create',
				expand: {
					item: product.expand.item
				}
			});
			editOrderUpdates.push({
				item: product.item,
				available: amount,
				amount: amount,
				action: 'create',
			});
		}
		if (existingUpdate !== -1) {
			editOrderUpdates[existingUpdate].available = amount;
			editOrderUpdates[existingUpdate].amount = amount;
			editOrderUpdates[existingUpdate].action = 'update';
		} else
		{
			editOrderUpdates.push({
				item: product.item,
				available: amount,
				amount: amount,
				action: 'update',
			});
		}
		editOrderItems = [...editOrderItems];
		editOrderUpdates = [...editOrderUpdates];
	}
	function editRemoveItem(product: any)
	{
		// Create the update
		editOrderUpdates.push({
			item: product.item,
			available: 0,
			amount: 0,
			action: 'delete',
		});

		// Search for a create or update action and delete it
		editOrderUpdates = editOrderUpdates.filter((item) => !(item.item === product.item && (item.action === 'create' || item.action === 'update')));

		// Search for the item and delete it
		editOrderItems = editOrderItems.filter((item) => item.item !== product.item);

		editOrderItems = [...editOrderItems];
		editOrderUpdates = [...editOrderUpdates];
	}
	function setDeleteAction(action)
	{
		if (action == "keep")
		{
			deleteCheck1.checked = true;
			deleteCheck2.checked = false;
			deleteCheck3.checked = false;
			deleteTransactionAction = "keep";
		}
		else if (action == "delete")
		{
			deleteCheck1.checked = false;
			deleteCheck2.checked = true;
			deleteCheck3.checked = false;
			deleteTransactionAction = "delete";
		}
		else if (action == "update")
		{
			deleteCheck1.checked = false;
			deleteCheck2.checked = false;
			deleteCheck3.checked = true;
			deleteTransactionAction = "update";
		}
	}
	function setMassDeleteAction(action)
	{
		if (action == "keep")
		{
			massDeleteCheck1.checked = true;
			massDeleteCheck2.checked = false;
			massDeleteCheck3.checked = false;
			massDeleteTransactionAction = "keep";
		}
		else if (action == "delete")
		{
			massDeleteCheck1.checked = false;
			massDeleteCheck2.checked = true;
			massDeleteCheck3.checked = false;
			massDeleteTransactionAction = "delete";
		}
		else if (action == "update")
		{
			massDeleteCheck1.checked = false;
			massDeleteCheck2.checked = false;
			massDeleteCheck3.checked = true;
			massDeleteTransactionAction = "update";
		}
	}
</script>

<title>Admin - Bestellungen</title>
<AdminPanel
	title="Bestellungen"
	description="Verwaltung aller Bestellungen"
	showCreateButton={true}
	showMassEditButton={false}
	showMassDeleteButton={true}
	massEditButtonText="Ausgewählte bearbeiten"
	massDeleteButtonText="Ausgewählte löschen"
	{totalItems}
	selectedItems={selectedOrders.size}
	on:create={() => openCreateModal()}
	on:massDelete={() => massDeleteModal.showModal()}
>
	<svelte:fragment slot="filter">
		<div class="flex gap-3 flex-row justify-between">
			<div class="flex w-full">
				<input
					class="input input-bordered w-full"
					type="text"
					placeholder="Suchen nach Bestellnummer oder Firma..."
					autocomplete="off"
				/>
			</div>

			<div class="flex gap-2 flex-wrap">
				<select
					class="select select-bordered"
					bind:value={perPage}
					on:change={() => {
						page = 1;
						fetchOrders();
					}}
				>
					<option value="15">15 / Seite</option>
					<option value="30">30 / Seite</option>
					<option value="50">50 / Seite</option>
					<option value="100">100 / Seite</option>
					<option value="250">250 / Seite</option>
					<option value="500">500 / Seite</option>
					<option value="1000">1000 / Seite</option>
					<option value="2000">2000 / Seite</option>
					<option value="5000">5000 / Seite</option>
				</select>

				<div class="flex-1"></div>
			</div>
		</div>
		{#if loading}
			<div class="text-sm opacity-70">Lade Bestellungen…</div>
		{/if}
	</svelte:fragment>

	{#if !loading}
		<div class="overflow-x-auto rounded-2xl border border-base-300">
			<table class="table table-zebra w-full">
				<thead>
					<tr>
						<th
							><input
								type="checkbox"
								class="checkbox bg-base-100"
								on:change={toggleSelectAllOrders}
								checked={selectedOrders.size === orders.length}
							/></th
						>
						<th>Bestellnummer</th>
						<th>Firma</th>
						<th>Status</th>
						<th>Transaktion</th>
						<th>Kosten</th>
						<th>Erstellt/Bearbeitet</th>
						<th>Aktionen</th>
					</tr>
				</thead>
				<tbody>
					{#each orders as order}
						<tr>
							<td
								><input
									type="checkbox"
									class="checkbox bg-base-100"
									on:change={() => toggleSelectOrder(order.id)}
									checked={selectedOrders.has(order.id)}
								/></td
							>
							<td class="font-mono font-bold">{order.id}</td>
							<td>
								<div class="flex flex-col">
									<p>
										{order.expand.company.companyName != ''
											? order.expand.company.companyName
											: 'Unbekannt'}
									</p>
									<span class="badge badge-ghost badge-sm">{order.company}</span>
								</div>
							</td>
							{#if order.status === 'requested'}
								<td><span class="badge badge-warning">Angefragt</span></td>
							{:else if order.status === 'approved'}
								<td><span class="badge badge-primary">Akzeptiert</span></td>
							{:else if order.status === 'rejected'}
								<td><span class="badge badge-error">Abgelehnt</span></td>
							{:else if order.status === 'collectable'}
								<td
									><span
										class="badge badge-secondary tooltip"
										data-tip="Die Bestellung ist bereit für die Abholung. Gehe dazu in das Warenlager."
										>Bereit zum Abholen <BadgeQuestionMark size={16}></BadgeQuestionMark></span
									></td
								>
							{:else if order.status === 'collected'}
								<td><span class="badge badge-success">Abgeholt</span></td>
							{:else}
								<td>{order.status}</td>
							{/if}
							<td>{order.transaction}</td>
							<td><div class="flex flex-row items-center">{order.cost?.toFixed(2)} <Currency /></div></td>
							<td
								><div class="flex flex-col">
									<p>{new Date(order.created).toLocaleString()}</p>
									<p>{new Date(order.updated).toLocaleString()}</p>
								</div></td
							>
							<td>
								<div>
									<button class="btn btn-xs btn-ghost" on:click={() => openEditModal(order)}
										><Pen></Pen></button
									>
									<button
										class="btn btn-xs btn-ghost btn-error"
										on:click={() => openDeleteModal(order)}><Trash2></Trash2></button
									>
								</div>
							</td>
						</tr>
					{/each}
				</tbody>
			</table>
		</div>
	{/if}

	<div class="flex justify-center mt-4">
		<div class="join">
			<button class="join-item btn" on:click={() => goToPage(page - 1)} disabled={page <= 1}>
				«
			</button>
			<button class="join-item btn btn-ghost">
				Seite <span class="font-mono mx-1">{page}</span> /
				<span class="font-mono">{totalPages}</span>
			</button>
			<button
				class="join-item btn"
				on:click={() => goToPage(page + 1)}
				disabled={page >= totalPages}
			>
				»
			</button>
		</div>
	</div>

	<!-- Select User Modal -->
	<UserSelectModal
		bind:this={selectUserModal}
		on:select={(e) => onUserSelect(e.detail)}
		adminMode={true}
		perPage={selectUserPerPage}
		companyOnly={true}
	/>

	<!-- Add Product Modal -->
	<dialog id="select_product_modal" class="modal" bind:this={selectProductModal}>
		<div class="modal-box max-w-3xl">
			<h3 class="text-lg font-bold mb-2">Produkt auswählen</h3>
			<div class="grid gap-2 md:grid-cols-[1fr_280px] mb-2">
				<input
					type="text"
					placeholder="Name filtern..."
					class="input input-bordered w-full"
					autocomplete="off"
					bind:value={selectProductFilter}
					on:input={() => fetchProducts()}
				/>
				<select class="select select-bordered w-full" bind:value={selectProductCategory} on:change={() => fetchProducts()}>
					<option value="all">Alle Kategorien</option>
					{#each categoryOptions as category}
						<option value={category.value}>{category.label}</option>
					{/each}
				</select>
			</div>
			{#if !selectProductLoading}
				<div class="overflow-x-auto rounded-2xl border border-base-300">
					<table class="table table-zebra w-full">
						<thead>
							<tr>
								<th>Name</th>
								<th>Kategorie</th>
								<th>Beschreibung</th>
								<th>Kosten</th>
								<th>Euro</th>
								<th>Aktionen</th>
							</tr>
						</thead>
						<tbody>
							{#each selectProductProducts as product}
								<tr>
									<td>{product.name}</td>
									<td>
										<span class="badge badge-outline badge-sm">
											{categoryOptions.find((category) => category.value === product.category)?.label || 'Ohne Kategorie'}
										</span>
									</td>
									<td>{@html product.description || 'Keine Beschreibung'}</td>
									<td>
										<div class="flex flex-row">
											<p>{product.price.toFixed(2)}</p>
											<Currency />
										</div>
									</td>
									<td>
										<div class="flex flex-row">
											<p>{product.euroPrice.toFixed(2)}</p>
											<Euro size={19} />
										</div>
									</td>
									<td>
										<button
											class="btn btn-xs btn-ghost btn-success"
											on:click={() => {
												if (selectProductTarget === 'create') {
													createAddItem(product);
												} else if (selectProductTarget === 'edit') {
													editAddItem(product);
												}
												selectProductModal.close();
											}}>Hinzufügen</button
										>
									</td>
								</tr>
							{/each}
						</tbody>
					</table>
				</div>
				<div class="mt-3 text-sm opacity-70">
					Seite {selectProductPage} von {selectProductTotalPages}
				</div>
				<div class="flex flex-row justify-center w-full mt-4">
					<div class="join">
						<button
							class="join-item btn"
							disabled={selectProductPage <= 1}
							on:click={() => {
								selectProductPage--;
								fetchProducts();
							}}>«</button
						>
						<button class="join-item btn" disabled>{selectProductPage}</button>
						<button
							class="join-item btn"
							disabled={selectProductPage >= selectProductTotalPages}
							on:click={() => {
								selectProductPage++;
								fetchProducts();
							}}>»</button
						>
					</div>
				</div>
			{/if}
			<div class="modal-action">
				<button class="btn btn-error" on:click={() => selectProductModal.close()}>Abbrechen</button>
			</div>
		</div>
		<form method="dialog" class="modal-backdrop">
			<button>close</button>
		</form>
	</dialog>
	<!-- Create new Modal -->
	<dialog id="create_modal" class="modal" bind:this={createModal}>
		<div class="modal-box max-w-5xl">
			<h3 class="text-lg font-bold mb-2">Neue Bestellung anlegen</h3>
			<!-- svelte-ignore a11y_label_has_associated_control -->
			<label class="fieldset-label mb-1">Firma:</label>
			<button class="btn btn-primary w-full" on:click={() => openSelectUserModal('create')}
				>{newOrderCompany != null
					? `Firma: ${newOrderCompany.companyName}`
					: 'Firma auswählen'}</button
			>
			<div class="flex flex-row justify-between w-full items-center mt-4">
				<!-- svelte-ignore a11y_label_has_associated_control -->
				<label class="fieldset-label">Bestellte Artikel:</label>
				<button class="btn btn-xs btn-outline" on:click={() => openSelectProductModal('create')}
					>Artikel hinzufügen</button
				>
			</div>
			<div class="overflow-x-auto rounded-2xl border border-base-300 mt-2">
				<table class="table table-zebra w-full">
					<thead>
						<tr>
							<th>Name</th>
							<th>Beschreibung</th>
							<th>Kosten [1|n]</th>
							<th>Euro [1|n]</th>
							<th>Aktionen</th>
						</tr>
					</thead>
					<tbody>
						{#each newOrderItems as item, index}
							<tr>
								<td>{item.expand.item.name}</td>
								<td>{@html item.expand.item.description}</td>
								<td>
									<div class="flex flex-row">
										<p>
											{item.expand.item.price.toFixed(2) * 1} | {(item.expand.item.price.toFixed(2) * item.available).toFixed(2)}
										</p>
										<Currency />
									</div>
								</td>
								<td>
									<div class="flex flex-row">
										<p>
											{item.expand.item.euroPrice.toFixed(2) * 1} | {(item.expand.item.euroPrice.toFixed(2) * item.available).toFixed(2)}
										</p>
										<Euro size={20} />
									</div>
								</td>
								<td class="max-w-fit">
									<div class="flex flex-row items-center gap-1">
										<button
											class="btn btn-xs btn-error btn-ghost"
											on:click={() => createUpdateAmount(item, item.available - 1)}
											><MinusCircle></MinusCircle></button
										>
										<input
											type="number"
											class="input input-bordered w-16 mx-2"
											min="1"
											bind:value={item.available}
											on:change={(e) => createUpdateAmount(item, parseInt(e.currentTarget.value))}
										/>
										<button
											class="btn btn-xs btn-success btn-ghost"
											on:click={() => createUpdateAmount(item, item.available + 1)}
											><PlusCircle></PlusCircle></button
										>
										<div class="divider divider-horizontal ml-1 mr-1"></div>
										<button
											class="btn btn-xs btn-ghost btn-error"
											on:click={() => createRemoveItem(item)}><Trash2></Trash2></button
										>
									</div>
								</td>
							</tr>
						{/each}
					</tbody>
					<caption class=" bg-base-300 p-4 table-caption caption-bottom">
						<div class="flex flex-row text-bottom w-full gap-2 items-center">
							<p class="font-bold">Gesamt:</p>
							<div>
								<div class="flex flex-row items-center">
									<p>{newOrderItems.reduce((acc, item) => acc + item.expand.item.price * item.available, 0).toFixed(2)}</p>
									<Currency size={24} />
								</div>
							</div>
							<div>
								<div class="flex flex-row items-center">
									<p>{newOrderItems.reduce((acc, item) => acc + item.expand.item.euroPrice * item.available, 0).toFixed(2)}</p>
									<Euro size={24} />
								</div>
							</div>
						</div>
					</caption>
				</table>
			</div>
			<div class="flex flex-col">
				<!-- svelte-ignore a11y_label_has_associated_control -->
				<label class="fieldset-label mb-1 mt-4">Status:</label>
				<select class="select select-bordered w-full" bind:value={newOrderStatus}>
					<option value="requested">Angefragt</option>
					<option value="approved">Akzeptiert</option>
					<option value="rejected">Abgelehnt</option>
					<option value="collectable">Bereit zum Abholen</option>
					<option value="collected">Abgeholt</option>
				</select>
			</div>

			<div class="shadow rounded mt-4 p-4">
				<h2 class="font-md font-bold opacity-70">Zusatzwünsche:</h2>
				<textarea class="textarea textarea-bordered w-full mt-2" placeholder="Zusatzwünsche..." bind:value={newOrderExtras}></textarea>
				<div class="divider"></div>
				<div class="flex flex-row gap-2">
					<div class="flex flex-col w-full">
						<p class="fieldset-label mb-1">Zusatzkosten:</p>
						<div class="flex flex-row items-center">
							<input type="number" class="input input-bordered w-full" placeholder="0.00" bind:value={newOrderExtraCost} />
							<Currency size={24} class="ml-2" />
						</div>
					</div>
					<div class="divider divider-horizontal"></div>
					<div class="flex flex-col w-full">
						<p class="fieldset-label mb-1">Zusatzkosten in Euro:</p>
						<div class="flex flex-row items-center">
							<input type="number" class="input input-bordered w-full" placeholder="0.00" bind:value={newOrderExtraEuroCost} />
							<Euro size={24} class="ml-2" />
						</div>
					</div>
				</div>
			</div>

			<div class="shadow rounded mt-4 p-4">
				<h2 class="font-md font-bold opacity-70">Transaktionseinstellungen:</h2>
				<p class="text-sm opacity-70 text-error">Lass die Einstellungen aktiviert außer du weißt was du tust!</p>
				<div class="divider mt-1 mb-1"></div>
				<div class="flex flex-row gap-2">
					<div class="flex flex-row w-full justify-between items-center">
						<p class="fieldset-label mb-1">Geld bewegen:</p>
						<input type="checkbox" class="toggle" placeholder="0.00" bind:checked={newOrderMoveMoney} />
					</div>
					<div class="divider divider-horizontal"></div>
					<div class="flex flex-row w-full justify-between items-center">
						<p class="fieldset-label mb-1">Euro bewegen:</p>
						<input type="checkbox" class="toggle" placeholder="0.00" bind:checked={newOrderMoveEuro} />
					</div>
				</div>
			</div>
			
			<div class="modal-action">
				<button class="btn btn-error" on:click={() => createModal.close()}>Abbrechen</button>
				<button class="btn btn-success" on:click={() => createNewOrder()} disabled={newOrderCompany == null}>Erstellen</button>
			</div>
		</div>
		<form method="dialog" class="modal-backdrop">
			<button>close</button>
		</form>
	</dialog>
	<!-- Edit Modal -->
	<dialog id="edit_modal" class="modal" bind:this={editModal}>
		<div class="modal-box max-w-4xl">
			<h3 class="text-lg font-bold mb-2">Bestellung bearbeiten</h3>
			<!-- svelte-ignore a11y_label_has_associated_control -->
			<label class="fieldset-label mb-1">Firma:</label>
			<button class="btn btn-primary w-full" on:click={() => openSelectUserModal('edit')}
				>{editOrderCompany != null
					? `Firma: ${editOrderCompany.companyName}`
					: 'Firma auswählen'}</button
			>
			<div class="flex flex-row justify-between w-full items-center mt-4">
				<!-- svelte-ignore a11y_label_has_associated_control -->
				<label class="fieldset-label">Bestellte Artikel:</label>
				<button class="btn btn-xs btn-outline" on:click={() => openSelectProductModal('edit')}
					>Artikel hinzufügen</button
				>
			</div>
			<div class="overflow-x-auto rounded-2xl border border-base-300 mt-2">
				<table class="table table-zebra w-full">
					<thead>
						<tr>
							<th>Name</th>
							<th>Beschreibung</th>
							<th>Kosten [1|n]</th>
							<th>Euro [1|n]</th>
							<th>Aktionen</th>
						</tr>
					</thead>
					<tbody>
						{#each editOrderItems as item, index}
							<tr>
								<td>{item.expand.item.name}</td>
								<td>{@html item.expand.item.description}</td>
								<td>
									<div class="flex flex-row">
										<p>
											{item.expand.item.price.toFixed(2) * 1} | {(item.expand.item.price.toFixed(2) * item.available).toFixed(2)}
										</p>
										<Currency />
									</div>
								</td>
								<td>
									<div class="flex flex-row">
										<p>
											{item.expand.item.euroPrice.toFixed(2) * 1} | {(item.expand.item.euroPrice.toFixed(2) * item.available).toFixed(2)}
										</p>
										<Euro size={20} />
									</div>
								</td>
								<td class="max-w-fit">
									<div class="flex flex-row items-center gap-1">
										<button
											class="btn btn-xs btn-error btn-ghost"
											on:click={() => editUpdateAmount(item, item.available - 1)}
											><MinusCircle></MinusCircle></button
										>
										<input
											type="number"
											class="input input-bordered w-16 mx-2"
											min="1"
											bind:value={item.available}
											on:change={(e) => editUpdateAmount(item, parseInt(e.currentTarget.value))}
										/>
										<button
											class="btn btn-xs btn-success btn-ghost"
											on:click={() => editUpdateAmount(item, item.available + 1)}
											><PlusCircle></PlusCircle></button
										>
										<div class="divider divider-horizontal ml-1 mr-1"></div>
										<button
											class="btn btn-xs btn-ghost btn-error"
											on:click={() => editRemoveItem(item)}><Trash2></Trash2></button
										>
									</div>
								</td>
							</tr>
						{/each}
					</tbody>
					<caption class=" bg-base-300 p-4 table-caption caption-bottom">
						<div class="flex flex-row text-bottom w-full gap-2 items-center">
							<p class="font-bold">Gesamt:</p>
							<div>
								<div class="flex flex-row items-center">
									<p>{editOrderItems.reduce((acc, item) => acc + item.expand.item.price * item.available, 0).toFixed(2)}</p>
									<Currency size={24} />
								</div>
							</div>
							<div>
								<div class="flex flex-row items-center">
									<p>{editOrderItems.reduce((acc, item) => acc + item.expand.item.euroPrice * item.available, 0).toFixed(2)}</p>
									<Euro size={24} />
								</div>
							</div>
						</div>
					</caption>
				</table>
			</div>
			<div class="flex flex-col">
				<!-- svelte-ignore a11y_label_has_associated_control -->
				<label class="fieldset-label mb-1 mt-4">Status:</label>
				<select class="select select-bordered w-full" bind:value={editOrderStatus}>
					<option value="requested">Angefragt</option>
					<option value="approved">Akzeptiert</option>
					<option value="rejected">Abgelehnt</option>
					<option value="collectable">Bereit zum Abholen</option>
					<option value="collected">Abgeholt</option>
				</select>
			</div>

			<div class="shadow rounded mt-4 p-4">
				<h2 class="font-md font-bold opacity-70">Zusatzwünsche:</h2>
				<textarea class="textarea textarea-bordered w-full mt-2" placeholder="Zusatzwünsche..." bind:value={editOrderExtras}></textarea>
				<div class="divider"></div>
				<div class="flex flex-row gap-2">
					<div class="flex flex-col w-full">
						<p class="fieldset-label mb-1">Zusatzkosten:</p>
						<div class="flex flex-row items-center">
							<input type="number" class="input input-bordered w-full" placeholder="0.00" bind:value={editOrderExtraCost} />
							<Currency size={24} class="ml-2" />
						</div>
					</div>
					<div class="divider divider-horizontal"></div>
					<div class="flex flex-col w-full">
						<p class="fieldset-label mb-1">Zusatzkosten in Euro:</p>
						<div class="flex flex-row items-center">
							<input type="number" class="input input-bordered w-full" placeholder="0.00" bind:value={editOrderExtraEuroCost} />
							<Euro size={24} class="ml-2" />
						</div>
					</div>
				</div>
			</div>

			<div class="shadow rounded mt-4 p-4">
				<h2 class="font-md font-bold opacity-70">Transaktionseinstellungen:</h2>
				<p class="text-sm opacity-70 text-error">Lass die Einstellungen aktiviert außer du weißt was du tust!</p>
				<div class="divider mt-1 mb-1"></div>
				<div class="flex flex-row gap-2">
					<div class="flex flex-row w-full justify-between items-center">
						<p class="fieldset-label mb-1">Geld bewegen:</p>
						<input type="checkbox" class="toggle" placeholder="0.00" bind:checked={editOrderMoveMoney} />
					</div>
					<div class="divider divider-horizontal"></div>
					<div class="flex flex-row w-full justify-between items-center">
						<p class="fieldset-label mb-1">Euro bewegen:</p>
						<input type="checkbox" class="toggle" placeholder="0.00" bind:checked={editOrderMoveEuro} />
					</div>
				</div>
			</div>

			<div class="modal-action">
				<button class="btn btn-error" on:click={() => editModal.close()}>Abbrechen</button>
				<button class="btn btn-success" on:click={() => updateOrder()}>Speichern</button>
			</div>
		</div>
		<form method="dialog" class="modal-backdrop">
			<button>close</button>
		</form>
	</dialog>
	
	<!-- Mass Delete Modal -->
	<dialog id="mass_delete_modal" class="modal" bind:this={massDeleteModal}>
		<div class="modal-box max-w-4xl">
			<h3 class="text-lg font-bold mb-2">Bestellungen löschen</h3>
			<p>
				Durch das Löschen der Bestellungen gehen alle Daten verloren und können nicht
				wiederhergestellt werden.
			</p>
			<p>Sind sie sicher, dass sie fortfahren wollen?</p>
			<div class="box mt-4 mb-2">
				<p class="font-mono">Bestellungen:</p>
				<div class="overflow-x-auto rounded-2xl border border-base-300 mt-2">
					<table class="table table-zebra w-full">
						<thead>
							<tr>
								<th>Bestellnummer</th>
								<th>Firma</th>
								<th>Status</th>
								<th>Bezahlt</th>
								<th>Kosten</th>
							</tr>
						</thead>
						<tbody>
							{#each orders.filter((o) => selectedOrders.has(o.id)) as order}
								<tr>
									<td class="font-mono font-bold">{order.id}</td>
									<td
										>{order.expand.company.companyName != ''
											? order.expand.company.companyName
											: 'Unbekannt'}</td
									>
									{#if order.status === 'requested'}
										<td><span class="badge badge-warning">Angefragt</span></td>
									{:else if order.status === 'approved'}
										<td><span class="badge badge-primary">Akzeptiert</span></td>
									{:else if order.status === 'rejected'}
										<td><span class="badge badge-error">Abgelehnt</span></td>
									{:else if order.status === 'collectable'}
										<td
											><span
												class="badge badge-secondary tooltip"
												data-tip="Die Bestellung ist bereit für die Abholung. Gehe dazu in das Warenlager."
												>Bereit zum Abholen <BadgeQuestionMark size={16}></BadgeQuestionMark></span
											></td
										>
									{:else if order.status === 'collected'}
										<td><span class="badge badge-success">Abgeholt</span></td>
									{:else}
										<td>{order.status}</td>
									{/if}
									<td>{order.paid ? 'Ja' : 'Nein'}</td>
									<td>{order.cost?.toFixed(2)} €</td>
								</tr>
							{/each}
						</tbody>
					</table>
				</div>
			</div>

			<div class="flex flex-row gap-2 w-full justify-center">
				<div class="join">
					<input class="join-item btn" type="radio" name="massDelete" bind:this={massDeleteCheck1} aria-label="Beibehalten" checked={massDeleteTransactionAction == "keep"} on:click={() => setMassDeleteAction("keep")} />
					<input class="join-item btn" type="radio" name="massDelete" bind:this={massDeleteCheck2} aria-label="Werte auf Null setzen" checked={massDeleteTransactionAction == "update"} on:click={() => setMassDeleteAction("update")} />
					<input class="join-item btn" type="radio" name="massDelete" bind:this={massDeleteCheck3} aria-label="Löschen" checked={massDeleteTransactionAction == "delete"} on:click={() => setMassDeleteAction("delete")} />
				</div>
			</div>

			<div class="flex flex-col gap-2 mt-2">
				<div class="flex flex-row justify-between items-center">
					<p>Geld zurücküberweisen</p>
					<input type="checkbox" class="toggle" placeholder="0.00" bind:checked={massDeleteMoveMoney} />
				</div>
				<div class="flex flex-row justify-between items-center">
					<p>Euro zurücküberweisen</p>
					<input type="checkbox" class="toggle" placeholder="0.00" bind:checked={massDeleteMoveEuro} />
				</div>
			</div>

			<div class="modal-action">
				<button class="btn btn-neutral" on:click={() => massDeleteModal.close()}>Abbrechen</button>
				<button class="btn btn-error" on:click={() => bulkDeleteOrders()}>Löschen</button>
			</div>
		</div>
		<form method="dialog" class="modal-backdrop">
			<button>close</button>
		</form>
	</dialog>
	<!-- Delete Modal -->
	<dialog id="delete_modal" class="modal" bind:this={deleteModal}>
		<div class="modal-box">
			<h3 class="text-lg font-bold mb-2">Bestellung {deleteOrder ? deleteOrder.id : ''} löschen</h3>
			<p>
				Durch das Löschen der Bestellung gehen alle Daten verloren und können nicht
				wiederhergestellt werden.
			</p>
			<p>Sind sie sicher, dass sie fortfahren wollen?</p>

			<p class="font-bold mt-2 mb-2">Was soll mit der Transaktion geschehen?</p>
			
			<div class="flex flex-row gap-2 w-full justify-center">
				<div class="join">
					<input class="join-item btn" type="radio" name="delete" bind:this={deleteCheck1} aria-label="Beibehalten" checked={deleteTransactionAction == "keep"} on:click={() => setDeleteAction("keep")} />
					<input class="join-item btn" type="radio" name="delete" bind:this={deleteCheck2} aria-label="Werte auf Null setzen" checked={deleteTransactionAction == "update"} on:click={() => setDeleteAction("update")} />
					<input class="join-item btn" type="radio" name="delete" bind:this={deleteCheck3} aria-label="Löschen" checked={deleteTransactionAction == "delete"} on:click={() => setDeleteAction("delete")} />
				</div>
			</div>

			<div class="flex flex-col gap-2 mt-2">
				<div class="flex flex-row justify-between items-center">
					<p>Geld zurücküberweisen</p>
					<input type="checkbox" class="toggle" placeholder="0.00" bind:checked={deleteMoveMoney} />
				</div>
				<div class="flex flex-row justify-between items-center">
					<p>Euro zurücküberweisen</p>
					<input type="checkbox" class="toggle" placeholder="0.00" bind:checked={deleteMoveEuro} />
				</div>
			</div>

			<div class="modal-action">
				<button class="btn btn-neutral" on:click={() => deleteModal.close()}>Abbrechen</button>
				<button class="btn btn-error" on:click={() => confirmDeleteOrder()}>Löschen</button>
			</div>
		</div>
		<form method="dialog" class="modal-backdrop">
			<button>close</button>
		</form>
	</dialog>
</AdminPanel>

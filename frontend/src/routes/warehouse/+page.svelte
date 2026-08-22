<script lang="ts">
	import { browser } from '$app/environment';
	import { getAuthStore } from '$lib/auth';
	import db, { server } from '$lib/connections';
	import Currency from '$modules/Currency.svelte';
	import Site from '$modules/Site.svelte';
	import { onMount } from 'svelte';
	import { BadgeQuestionMark, ChevronRight, Euro, Eye, Pen, Trash } from '@lucide/svelte';
	import {
		ArrowLeftRight,
		ClipboardList,
		Minus,
		Package,
		Plus,
		Search,
		ShoppingCart,
		ChevronLeft
	} from '@lucide/svelte';
	import PopupModal from '$modules/modals/PopupModal.svelte';

	let popupModal: PopupModal;

	let products = [];
	let card = [];
	let orders = [];

	type ExtraWishItem = {
		articleNumber: string;
		amount: number;
	};

	type ParsedExtraWishContent = {
		items: ExtraWishItem[];
		notes: string;
	};

	const categoryOptions = [
        { value: 'fruits_and_vegetables', label: 'Obst & Gemüse' },
        { value: 'dairy', label: 'Milchprodukte & Eier' },
    	{ value: 'meat', label: 'Wurst und Fleischprodukte' },
        { value: 'frozen', label: 'Tiefkühl' },
        { value: 'baked', label: 'Brot, Cerealien & Aufstriche' },
        { value: 'cooking_and_baking', label: 'Kochen & Backen' },
        { value: 'oil_and_spices', label: 'Öle & Gewürze' },
        { value: 'sweet_and_salty', label: 'Snacks und Süßigkeiten' },
        { value: 'coffee_and_cacao', label: 'Tee & Kakao' },
        { value: 'drinks', label: 'Getränke' },
        { value: 'other', label: 'Sonstiges' }
    ];

	let extraWishItems: ExtraWishItem[] = [createEmptyExtraWishItem()];
	let extraWishNotes = '';
	let selectedOrderExtraWishItems: ExtraWishItem[] = [createEmptyExtraWishItem()];
	let selectedOrderExtraWishNotes = '';
	let selectedCategory = 'all';

	let page = 1;
	let perPage = 50;
	let totalPages = 1;

	let placingOrder = false;
	let loading = true;
	let search = '';

	let editModal: HTMLDialogElement;
	let viewModal: HTMLDialogElement;

	let userData: any = null;
	let userAccount: any = null;

	let selectedOrder = null;
	let activeTab: 'products' | 'cart' | 'orders' = 'products';

	onMount(async () => {
		if (!browser) return;
		userData = await db.collection("user_data").getOne(getAuthStore().record.id, { requestKey: null });
		userAccount = await db.collection("accounts").getFirstListItem(`user = "${getAuthStore().record.id}"`);
		await fetchData();
		await selectCategory('all');
	});

	async function updateUserData() {
		userData = await db.collection("user_data").getOne(getAuthStore().record.id, { requestKey: null });
	}

	$: filteredProducts = products.filter((product) => {
		const q = search.trim().toLowerCase();

		if (!q) return true;

		return (
			product.name?.toLowerCase().includes(q) ||
			product.description?.toLowerCase().includes(q)
		);
	});

	$: groupedProducts = groupProductsByCategory(filteredProducts);
	$: extrasText = serializeExtraWishContent(extraWishItems, extraWishNotes);
	$: selectedOrderExtrasText = serializeExtraWishContent(
		selectedOrderExtraWishItems,
		selectedOrderExtraWishNotes
	);
	async function selectCategory(category: string) {
		if (selectedCategory === category) return;

		selectedCategory = category;

		// A category can have fewer pages than the previous selection.
		page = 1;

		await fetchProducts();
	}

	function createEmptyExtraWishItem(): ExtraWishItem {
		return {
			articleNumber: '',
			amount: 1
		};
	}

	function parseExtraWishContent(value: string | null | undefined, ensureRow = false): ParsedExtraWishContent {
		const normalizedLines = (value ?? '').replace(/\r/g, '').split('\n');
		const separatorIndex = normalizedLines.findIndex((line) => line.trim() === '---');
		const itemLines = separatorIndex === -1 ? normalizedLines : normalizedLines.slice(0, separatorIndex);
		const notesLines = separatorIndex === -1 ? [] : normalizedLines.slice(separatorIndex + 1);

		const items = itemLines
			.map((line) => line.trim())
			.filter(Boolean)
			.map((line) => {
				const stripped = line.replace(/^\[/, '').replace(/\]$/, '').trim();
				const match = stripped.match(/^(.*?)(?:\s*[x×]\s*)(\d+)$/i);

				if (match) {
					return {
						articleNumber: match[1].trim(),
						amount: Math.max(1, parseInt(match[2], 10) || 1)
					};
				}

				return {
					articleNumber: line,
					amount: 1
				};
			})
			.filter((item) => item.articleNumber.length > 0);

		return {
			items: items.length > 0 || !ensureRow ? items : [createEmptyExtraWishItem()],
			notes: notesLines.join('\n').trim()
		};
	}

	function serializeExtraWishContent(items: ExtraWishItem[], notes = '') {
		const serializedItems = items
			.map((item) => {
				const articleNumber = item.articleNumber.trim();
				if (!articleNumber) return '';

				const amount = Number.isFinite(Number(item.amount)) ? Math.max(1, Math.floor(Number(item.amount))) : 1;
				return `[${articleNumber} x ${amount}]`;
			})
			.filter(Boolean)
			.join('\n');

		const cleanNotes = notes.trim();

		if (!serializedItems && !cleanNotes) {
			return '';
		}

		if (!cleanNotes) {
			return [serializedItems, '---'].filter(Boolean).join('\n');
		}

		if (!serializedItems) {
			return ['---', cleanNotes].join('\n');
		}

		return [serializedItems, '---', cleanNotes].join('\n');
	}

	function updateExtraWishItem(source: 'new' | 'edit', index: number, field: keyof ExtraWishItem, value: string | number) {
		const targetItems = source === 'new' ? [...extraWishItems] : [...selectedOrderExtraWishItems];
		const currentItem = targetItems[index] ?? createEmptyExtraWishItem();

		targetItems[index] = {
			...currentItem,
			[field]: field === 'amount' ? Math.max(1, Number(value) || 1) : String(value)
		};

		if (source === 'new') {
			extraWishItems = targetItems;
		} else {
			selectedOrderExtraWishItems = targetItems;
		}
	}

	function updateExtraWishNotes(source: 'new' | 'edit', value: string) {
		if (source === 'new') {
			extraWishNotes = value;
		} else {
			selectedOrderExtraWishNotes = value;
		}
	}

	function addExtraWishItem(source: 'new' | 'edit') {
		if (source === 'new') {
			extraWishItems = [...extraWishItems, createEmptyExtraWishItem()];
		} else {
			selectedOrderExtraWishItems = [...selectedOrderExtraWishItems, createEmptyExtraWishItem()];
		}
	}

	function removeExtraWishItem(source: 'new' | 'edit', index: number) {
		const targetItems = source === 'new' ? [...extraWishItems] : [...selectedOrderExtraWishItems];
		targetItems.splice(index, 1);

		if (targetItems.length === 0) {
			targetItems.push(createEmptyExtraWishItem());
		}

		if (source === 'new') {
			extraWishItems = targetItems;
		} else {
			selectedOrderExtraWishItems = targetItems;
		}
	}

	function getCategoryLabel(category: string | null | undefined) {
		return categoryOptions.find((option) => option.value === category)?.label ?? 'Ohne Kategorie';
	}

	function getCategoryOrder(category: string | null | undefined) {
		const index = categoryOptions.findIndex((option) => option.value === category);
		return index === -1 ? 999 : index;
	}

	async function goToProductPage(targetPage: number) {
		if (
			targetPage < 1 ||
			targetPage > totalPages ||
			targetPage === page
		) {
			return;
		}

		page = targetPage;
		await fetchProducts();
	}

	async function prevProductPage() {
		await goToProductPage(page - 1);
	}

	async function nextProductPage() {
		await goToProductPage(page + 1);
	}

	function groupProductsByCategory(items: any[]) {
		const groups = new Map<string, { key: string; label: string; items: any[] }>();

		items.forEach((product) => {
			const key = product.category || 'uncategorized';
			const label = getCategoryLabel(product.category);

			if (!groups.has(key)) {
				groups.set(key, { key, label, items: [] });
			}

			groups.get(key)?.items.push(product);
		});

		return [...groups.values()].sort((a, b) => {
			if (a.key === 'uncategorized') return 1;
			if (b.key === 'uncategorized') return -1;
			return getCategoryOrder(a.key) - getCategoryOrder(b.key) || a.label.localeCompare(b.label);
		});
	}

	async function fetchProducts() {
		if (!browser) return;

		loading = true;

		try {
			const filter =
				selectedCategory === 'all'
					? ''
					: `category = "${selectedCategory}"`;

			const res = await db.collection('stock').getList(page, perPage, {
				filter,
				sort: '+category,+name',
				requestKey: null
			});

			products = res.items;
			totalPages = res.totalPages;
		} catch (error) {
			console.error('Produkte konnten nicht geladen werden:', error);

			products = [];
			totalPages = 0;
		} finally {
			loading = false;
		}
	}

	async function fetchOrders() {
		if (!browser) return;

		try {
			const res = await db.collection('orders').getFullList(200, {
				filter: `company = "${getAuthStore().record.id}"`,
				sort: '+status,+created',
				requestKey: null
			});

			orders = res.sort((a, b) => {
				const statusOrder = [
					'collectable',
					'requested',
					'approved',
					'collected',
					'rejected'
				];

				const statusComparison =
					statusOrder.indexOf(a.status) -
					statusOrder.indexOf(b.status);

				if (statusComparison !== 0) {
					return statusComparison;
				}

				return (
					new Date(a.created).getTime() -
					new Date(b.created).getTime()
				);
			});
		} catch (error) {
			console.error('Bestellungen konnten nicht geladen werden:', error);
			orders = [];
		}
	}
	$: allOrders = orders;

	async function fetchData() {
		if (!browser) return;

		await Promise.all([
			fetchProducts(),
			fetchOrders()
		]);
	}

	$: cartTotal = card.reduce((total, item) => total + item.price * item.quantity, 0);
	$: cartEuroValue = card.reduce((total, item) => total + item.euroPrice * item.quantity, 0);

	function getCartQuantity(productId: string) {
		return card.find((item) => item.id === productId)?.quantity || 0;
	}

	function addToCard(product) {
		if (!browser) return;

		const existingItem = card.find((item) => item.id === product.id);

		if (existingItem) {
			if (product.max === -1 || existingItem.quantity < product.max) {
				existingItem.quantity += 1;
			}
		} else {
			card.push({ ...product, quantity: 1 });
		}

		card = [...card];
	}

	function removeFromCard(product) {
		if (!browser) return;

		const existingItem = card.find((item) => item.id === product.id);

		if (existingItem) {
			if (existingItem.quantity > 1) {
				existingItem.quantity -= 1;
			} else {
				card = card.filter((item) => item.id !== product.id);
			}
		}

		card = [...card];
	}

	function setAmount(product, amount: number) {
		if (!browser) return;

		const cleanAmount = Number.isFinite(amount) ? amount : 0;
		const existingItem = card.find((item) => item.id === product.id);

		if (existingItem) {
			if (cleanAmount > 0 && (product.max === -1 || cleanAmount <= product.max)) {
				existingItem.quantity = cleanAmount;
			} else {
				card = card.filter((item) => item.id !== product.id);
			}
		} else {
			if (cleanAmount > 0 && (product.max === -1 || cleanAmount <= product.max)) {
				card.push({ ...product, quantity: cleanAmount });
			}
		}

		card = [...card];
	}

	async function placeOrder() {
		if (!browser || card.length === 0 && !extraWishNotes && !extrasText) return;

		try {
			const existingOrder = await db
				.collection('orders')
				.getFirstListItem(`company = "${getAuthStore().record.id}" && status = "requested"`);

			if (existingOrder && !extrasText) {
				await updateOrder(existingOrder);
				return;
			}
		} catch {}

		placingOrder = true;

		try {

			let items: any[] = card.map((item) => ({
				item: item.id,
				amount: item.quantity,
			}));

			let orderRequest = {
				items: items,
				extras: extrasText,
				user: getAuthStore().record.id,
			}
			
			const rsp = await fetch(`${server}/api/warehouse/create-order`, {
				method: 'POST',
				headers: {
					'Content-Type': 'application/json',
					Authorization: `Bearer ${db.authStore.token}`
				},
				body: JSON.stringify(orderRequest)
			});

			if (!rsp.ok) {
				const errorData = await rsp.json();
				let errorMessage = "";
				if (errorData.message == "Insufficient funds.") {
					errorMessage = "Du besitzt nicht genügend Guthaben auf deinem Konto. Entferne einige Artikel aus deinem Warenkorb und versuche es erneut.";
				} else if (errorData.message == "Insufficient euro funds.") {
					errorMessage = "Du besitzt nicht genügend Euro auf deinem Konto. Entferne einige Artikel aus deinem Warenkorb und versuche es erneut. Alternativ kannst du im Warenlager oder bei info@yourdomain.com um eine Erhöhung deines Euro Limits bitten.";
				} else {
					errorMessage = errorData.message || "Es ist ein Fehler beim Aufgeben der Bestellung aufgetreten. Bitte versuche es später erneut.";
				}
				await popupModal.showDialog("Fehler beim Aufgeben der Bestellung", errorMessage || "Es ist ein Fehler beim Aufgeben der Bestellung aufgetreten. Bitte versuche es später erneut.", {
					close: true
				});
			}
			card = [];
			activeTab = 'orders';
			await fetchData();
			await updateUserData();
		} catch {
			alert('Fehler beim Aufgeben der Bestellung.');
		} finally {
			placingOrder = false;
		}
	}

	async function deleteOrder(order) {
		if (!browser) return;
		if ((await popupModal.showDialog("Bestellung löschen", "Möchtest du diese Bestellung wirklich löschen?", {
			confirm: true,
			cancel: true
		})) != 'confirm') return;

		await fetch(`${server}/api/warehouse/delete-order`, {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
				Authorization: `Bearer ${db.authStore.token}`
			},
			body: JSON.stringify({ id: order.id, user: getAuthStore().record.id })
		});

		await fetchData();
		await updateUserData();
	}

	async function updateOrder(order) {
		if (!browser) return;

		try {
			const orderItems = card.map((item) => ({
				id: item.id,
				item: item.id,
				amount: item.quantity,
			}));

			const createdItems = [];

			let updateOrderRequests: any[] = [];

			await Promise.all(
				orderItems.map((item) => {
					return db
						.collection('order_items')
						.getFirstListItem(`order = "${order.id}" && item = "${item.item}"`)
						.then((existingItem) => {
							let itemUpdateRequest = {
								action: 'update',
								id: existingItem.id,
								amount: existingItem.amount + item.amount
							}
							updateOrderRequests.push(itemUpdateRequest);
							return existingItem.amount + item.amount;
						}
							
						)
						.then((res) => {
							const cardItem = card.find((i) => i.id === item.item);
							if (cardItem) {
								cardItem.quantity = res;
							}
						})
						.catch(async () => {
							let itemCreateRequest = {
								action: 'create',
								item: item.item,
								amount: item.amount,
							}
							updateOrderRequests.push(itemCreateRequest);
						});
				})
			);

			const data = await fetch(`${server}/api/warehouse/update-order`, {
				method: 'POST',
				headers: {
					'Content-Type': 'application/json',
					Authorization: `Bearer ${db.authStore.token}`
				},
				body: JSON.stringify({ id: order.id, items: updateOrderRequests, extras: selectedOrderExtrasText, user: getAuthStore().record.id })
			});
			if (!data.ok) {
				const errorData = await data.json();
				let errorMessage = "";
				if (errorData.message == "Insufficient funds.") {
					errorMessage = "Du besitzt nicht genügend Guthaben auf deinem Konto. Entferne einige Artikel aus deinem Warenkorb und versuche es erneut.";
				} else if (errorData.message == "Insufficient euro funds to cover the updated order cost.") {
					errorMessage = "Du besitzt nicht genügend Euro auf deinem Konto. Entferne einige Artikel aus deinem Warenkorb und versuche es erneut. Alternativ kannst du im Warenlager oder bei info@yourdomain.com um eine Erhöhung deines Euro Limits bitten.";
				} else {
					errorMessage = errorData.message || "Es ist ein Fehler beim Aktualisieren der Bestellung aufgetreten. Bitte versuche es später erneut.";
				}
				await popupModal.showDialog("Fehler beim Aktualisieren der Bestellung", errorMessage || "Es ist ein Fehler beim Aktualisieren der Bestellung aufgetreten. Bitte versuche es später erneut.", {
					close: true
				});
			} else {
				await popupModal.showDialog("Bestellung aktualisiert", "Die Bestellung wurde erfolgreich aktualisiert.", {
					close: true
				});
			}
			card = [];
			activeTab = 'orders';
			await fetchData();
			await updateUserData();
		} catch {
			await popupModal.showDialog("Fehler beim Aktualisieren der Bestellung", "Es ist ein Fehler beim Aktualisieren der Bestellung aufgetreten. Bitte versuche es später erneut.", {
				close: true
			});
		}
	}

	async function editOrder(order) {
		selectedOrder = await db.collection('orders').getOne(order.id, {
			expand: 'items,items.item'
		});

		const parsedExtraWishes = parseExtraWishContent(selectedOrder.extras, true);
		selectedOrderExtraWishItems = parsedExtraWishes.items;
		selectedOrderExtraWishNotes = parsedExtraWishes.notes;

		selectedOrder.expand.items = selectedOrder.expand.items.map((item) => ({
			...item,
			dirty: false
		}));

		editModal.showModal();
	}

	async function viewOrder(order) {
		selectedOrder = await db.collection('orders').getOne(order.id, {
			expand: 'items'
		});

		const parsedExtraWishes = parseExtraWishContent(selectedOrder.extras, false);
		selectedOrderExtraWishItems = parsedExtraWishes.items;
		selectedOrderExtraWishNotes = parsedExtraWishes.notes;

		viewModal.showModal();
	}

	function setEditAmount(orderItem, amount) {
		if (!browser || !selectedOrder?.expand?.items) return;

		const cleanAmount = Number.isFinite(amount) ? Math.max(1, amount) : 1;
		const updatedItems = selectedOrder.expand.items.map((item) =>
			item.id === orderItem.id
				? {
					...item,
					amount: cleanAmount,
					available: cleanAmount,
					dirty: true
				}
				: item
		);

		// Only update the local modal state. The backend is still updated
		// exclusively when the user presses the Save button.
		selectedOrder = {
			...selectedOrder,
			expand: {
				...selectedOrder.expand,
				items: updatedItems
			}
		};
	}

	$: editedOrderTotal = (selectedOrder?.expand?.items ?? []).reduce((total, orderItem) => {
		const product = orderItem.expand?.item;
		return total + (product?.price ?? 0) * (orderItem.amount ?? 0);
	}, 0);

	$: editedOrderEuroTotal = (selectedOrder?.expand?.items ?? []).reduce((total, orderItem) => {
		const product = orderItem.expand?.item;
		return total + (product?.euroPrice ?? 0) * (orderItem.amount ?? 0);
	}, 0);

	async function updateEditedOrder() {
		if (!browser) return;

		try {
			const dirtyItems = selectedOrder.expand.items.filter((item) => item.dirty);

			let updateItems = dirtyItems.map((item) => ({
				id: item.id,
				action: 'update',
				amount: item.amount,
			}));

			const data = await fetch(`${server}/api/warehouse/update-order`, {
				method: 'POST',
				headers: {
					'Content-Type': 'application/json',
					Authorization: `Bearer ${db.authStore.token}`
				},
				body: JSON.stringify({ id: selectedOrder.id, items: updateItems, extras: selectedOrderExtrasText, user: getAuthStore().record.id })
			});

			const allItems: any[] = selectedOrder.expand.items;
			const cost = allItems.reduce((total, item) => {
				const product = products.find((p) => p.id === item.item);
				if (product) return total + product.price * item.amount;
				return total;
			}, 0);

			if (!data.ok) {
				const errorData = await data.json();
				let errorMessage = "";
				if (errorData.message == "Insufficient funds.") {
					errorMessage = "Du besitzt nicht genügend Guthaben auf deinem Konto. Entferne einige Artikel aus deinem Warenkorb und versuche es erneut.";
				} else if (errorData.message == "Insufficient euro funds to cover the updated order cost.") {
					errorMessage = "Du besitzt nicht genügend Euro auf deinem Konto. Entferne einige Artikel aus deinem Warenkorb und versuche es erneut. Alternativ kannst du im Warenlager oder bei info@yourdomain.com um eine Erhöhung deines Euro Limits bitten.";
				} else {
					errorMessage = errorData.message || "Es ist ein Fehler beim Aktualisieren der Bestellung aufgetreten. Bitte versuche es später erneut.";
				}
				popupModal.showDialog("Fehler beim Aktualisieren der Bestellung", errorMessage || "Es ist ein Fehler beim Aktualisieren der Bestellung aufgetreten. Bitte versuche es später erneut.", {
					close: true
				});
			} else {
				popupModal.showDialog("Bestellung aktualisiert", "Die Bestellung wurde erfolgreich aktualisiert.", {
					close: true
				});
			}
			editModal.close();
			await fetchData();
		} catch (error) {
			alert('Fehler beim Aktualisieren der Bestellung.');
		}
	}
	/*
	function getEuroColor()
	{
		if (!browser) { return; }
		let maxEuro = userData?.euroAvailable || 0;
		let spentEuro = userData?.euroSpent || 0;

		let percentage = (spentEuro / maxEuro) * 100;

		if (percentage < 50) {
			return 'text-success';
		} else if (percentage < 80) {
			return 'text-warning';
		} else {
			return 'text-error';
		}
	}
	*/
	$: getEuroColor = () => {
		if (!browser) { return ''; }
		let maxEuro = userData?.euroAvailable || 0;
		let spentEuro = userData?.euroSpent || 0;

		let percentage = (spentEuro / maxEuro) * 100;

		if (percentage < 50) {
			return 'text-success';
		} else if (percentage < 80) {
			return 'text-warning';
		} else {
			return 'text-error';
		}
	};
</script>

<title>Warenlager</title>

<Site>
	<div class="flex min-h-screen min-w-0 flex-col bg-base-200/30">
		<!-- Sticky Header -->
		<header class="sticky top-0 z-1 border-b border-base-300 bg-base-100/95 shadow-sm backdrop-blur">
			<div class="mx-auto flex w-full max-w-[1800px] flex-col gap-3 px-3 py-3 sm:px-4 lg:flex-row lg:items-center">
				<div class="flex min-w-0 flex-1 items-center gap-2.5">
					<div class="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-primary/15 text-primary">
						<Package size={19} />
					</div>
					<div class="min-w-0">
						<h1 class="truncate text-base font-bold leading-tight sm:text-lg">Warenlager</h1>
						<p class="hidden text-xs text-base-content/50 sm:block">Produkte, Warenkorb & Bestellungen</p>
					</div>
				</div>

				<div class="flex min-w-0 flex-col gap-2 sm:flex-row sm:items-center lg:ml-auto">
					<div class="order-2 flex min-w-0 items-center gap-1.5 overflow-x-auto pb-0.5 sm:order-1 sm:pb-0">
						<div class="flex shrink-0 items-center gap-1 rounded-full bg-base-200 px-2.5 py-1 text-xs font-medium">
							<Package size={11} class="text-primary" />
							{products.length} Produkte
						</div>
						<div class="flex shrink-0 items-center gap-1 rounded-full bg-base-200 px-2.5 py-1 text-xs font-medium {getEuroColor()}">
							{#if userData?.euroAvailable != -1}
								<span class="sm:hidden">{(userData?.euroAvailable - userData?.euroSpent).toFixed(2)}</span>
								<span class="hidden sm:inline">{userData?.euroSpent.toFixed(2)} / {userData?.euroAvailable.toFixed(2)}</span>
								<Euro size={13} />
								<span class="hidden xl:inline">verbleibend</span>
							{:else}
								∞ <Euro size={13} /> <span class="hidden sm:inline">Kein Limit</span>
							{/if}
						</div>
						{#if card.length > 0}
							<button type="button" class="flex shrink-0 items-center gap-1 rounded-full bg-warning/20 px-2.5 py-1 text-xs font-bold text-warning-content" on:click={() => (activeTab = 'cart')}>
								<ShoppingCart size={11} />
								{card.length}
							</button>
						{/if}
					</div>

					<label class="input input-sm input-bordered order-1 flex w-full min-w-0 items-center gap-1.5 sm:order-2 sm:w-64 lg:w-72">
						<Search size={14} class="shrink-0 opacity-40" />
						<input type="text" class="min-w-0 grow text-sm" placeholder="Produkte suchen..." bind:value={search} autocomplete="off" />
					</label>
				</div>
			</div>

			<nav class="mx-auto hidden w-full max-w-[1800px] border-t border-base-300 md:flex">
				<button type="button" class="desktop-tab" class:tab-active={activeTab === 'products'} on:click={() => (activeTab = 'products')}>
					<Package size={15} />
					Produkte
				</button>
				<button type="button" class="desktop-tab" class:tab-active={activeTab === 'cart'} on:click={() => (activeTab = 'cart')}>
					<ShoppingCart size={15} />
					Warenkorb
					{#if card.length > 0}
						<span class="ml-1 rounded-full bg-warning px-1.5 py-0.5 text-[10px] font-bold text-warning-content">{card.length}</span>
					{/if}
				</button>
				<button type="button" class="desktop-tab" class:tab-active={activeTab === 'orders'} on:click={() => (activeTab = 'orders')}>
					<ClipboardList size={15} />
					Bestellungen
				</button>
			</nav>
		</header>

		<!-- Main content -->
		<main class="mx-auto w-full max-w-[1800px] min-w-0 p-3 pb-28 sm:p-4 md:p-6 md:pb-6">
			<!-- ==================== PRODUCTS TAB ==================== -->
			{#if activeTab === 'products'}
				<div class="grid min-w-0 gap-4 xl:grid-cols-[minmax(0,1fr)_300px]">
					<!-- Product grid -->
					<div>
						<div class="mb-4 rounded-2xl border border-base-300 bg-base-100 p-3 shadow-sm">
							<label class="form-control md:hidden">
								<span class="label-text mb-1 text-xs font-medium text-base-content/60">Kategorie</span>
								<select class="select select-bordered select-sm w-full" value={selectedCategory} on:change={(event) => selectCategory(event.currentTarget.value)}>
									<option value="all">Alle Kategorien</option>
									{#each categoryOptions as category}
										<option value={category.value}>{category.label}</option>
									{/each}
								</select>
							</label>

							<div class="hidden flex-wrap gap-2 md:flex">
								<button type="button" class="btn btn-sm" class:btn-primary={selectedCategory === 'all'} class:btn-ghost={selectedCategory !== 'all'} class:btn-outline={selectedCategory !== 'all'} on:click={() => selectCategory('all')}>
									Alle Kategorien
								</button>
								{#each categoryOptions as category}
									<button type="button" class="btn btn-sm" class:btn-primary={selectedCategory === category.value} class:btn-ghost={selectedCategory !== category.value} class:btn-outline={selectedCategory !== category.value} on:click={() => selectCategory(category.value)}>
										{category.label}
									</button>
								{/each}
							</div>
						</div>

						<div class="mb-4 flex flex-col gap-3 rounded-2xl border border-base-300 bg-base-100 px-3 py-3 shadow-sm sm:flex-row sm:items-center sm:justify-between sm:px-4" hidden={groupedProducts.length === 0 || totalPages <= 1}>
							<div class="text-sm text-base-content/70">
								Seite {page} von {totalPages}
							</div>
							<div class="join grid w-full grid-cols-2 sm:flex sm:w-auto">
								<button
									type="button"
									class="btn btn-sm join-item"
									on:click={prevProductPage}
									disabled={page <= 1 || loading}
								>
									<ChevronLeft size={16} />
									Vorherige
								</button>
								<button
									type="button"
									class="btn btn-sm join-item"
									on:click={nextProductPage}
									disabled={page >= totalPages || loading}
								>
									Nächste
									<ChevronRight size={16} />
								</button>
							</div>
						</div>

						{#if loading}
							<div class="flex items-center justify-center py-24">
								<span class="loading loading-spinner loading-lg text-primary"></span>
							</div>
						{:else if groupedProducts.length === 0}
							<div
								class="rounded-2xl border border-dashed border-base-300 bg-base-200/50 py-16 text-center"
							>
								<Package size={36} class="mx-auto mb-2 opacity-20" />
								<p class="text-sm text-base-content/50">Keine Produkte gefunden.</p>
							</div>
						{:else}
							<div class="space-y-6">
								{#each groupedProducts as group}
									<div>
										<div class="mb-3 flex items-center justify-between gap-3">
											<h3 class="text-sm font-semibold text-base-content/70">{group.label}</h3>
											<span class="badge badge-ghost">{group.items.length}</span>
										</div>
										<div class="grid min-w-0 gap-3 sm:grid-cols-2 2xl:grid-cols-3">
											{#each group.items as product}
												{@const inCart = card.find((item) => item.id === product.id)?.quantity || 0}
												<div
													class="product-card relative min-w-0 rounded-2xl border bg-base-100 p-3 shadow-sm transition-all duration-200 hover:shadow-md sm:p-4"
													class:border-primary={inCart > 0}
													class:border-base-300={inCart === 0}
												>
													{#if inCart > 0}
														<div class="absolute bottom-3 left-0 top-3 w-1 rounded-r-full bg-primary"></div>
													{/if}

													<div class="mb-2 flex items-start gap-2">
														<div class="min-w-0 flex-1">
															<h3 class="break-words font-semibold leading-snug">{product.name}</h3>
															<div class="mt-1 flex flex-wrap items-center gap-2">
																<div class="flex items-center gap-0.5 text-sm font-bold text-primary">
																	{product.price}
																	<Currency size={13} />
																</div>
																<span class="badge badge-outline badge-sm max-w-full truncate">{getCategoryLabel(product.category)}</span>
															</div>
														</div>
														<span class="shrink-0 rounded-full bg-base-200 px-2 py-0.5 font-medium text-base-content/60">
															<div class="flex flex-row items-center justify-center gap-0.5 text-sm">
																{product.euroPrice} <Euro size={13.5} />
															</div>
														</span>
													</div>

													<div class="mb-3 min-h-9 text-xs text-base-content/60 line-clamp-2">
														{@html product.description}
													</div>

													<div class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
														{#if inCart > 0}
															<span class="text-xs font-semibold text-primary">{inCart} im Warenkorb</span>
														{:else}
															<span></span>
														{/if}
														<div class="join grid w-full grid-cols-[44px_minmax(0,1fr)_44px] sm:flex sm:w-auto">
															<button
																type="button"
																class="btn btn-ghost join-item h-10 w-10 border border-base-300 p-0"
																on:click={() => {
																	removeFromCard(product);
																}}
															>
																<Minus size={14} />
															</button>
															<input
																type="number"
																min="0"
																max={product.max !== -1 ? product.max : undefined}
																class="input input-bordered join-item h-10 min-w-0 w-full text-center disable-browser-updown sm:w-14"
																value={card.find((item) => item.id === product.id)?.quantity || 0}
																on:input={(e) => setAmount(product, parseInt(e.currentTarget.value))}
																autocomplete="off"
															/>
															<button
																type="button"
																class="btn btn-primary join-item h-10 w-10 p-0"
																on:click={() => {
																	addToCard(product);
																}}
															>
																<Plus size={14} />
															</button>
														</div>
													</div>
												</div>
											{/each}
										</div>
									</div>
								{/each}
							</div>
						{/if}
					</div>

					<!-- Cart sidebar (xl+) -->
					<div class="hidden xl:block">
						<div
							class="sticky top-6 rounded-2xl border border-base-300 bg-base-100 shadow-sm overflow-hidden"
						>
							<div
								class="flex items-center justify-between border-b border-base-300 bg-base-200 px-4 py-3"
							>
								<div>
									<h2 class="text-sm font-semibold">Warenkorb</h2>
									<p class="text-xs text-base-content/50">{card.length} Positionen</p>
								</div>
								<div
									class="flex h-8 w-8 items-center justify-center rounded-lg bg-warning/20 text-warning"
								>
									<ShoppingCart size={16} />
								</div>
							</div>

							<div class="p-4">
								{#if card.length === 0}
									<div
										class="rounded-xl border border-dashed border-base-300 py-8 text-center text-sm text-base-content/40"
									>
										Warenkorb leer
									</div>
								{:else}
									<div class="max-h-64 space-y-2 overflow-auto">
										{#each card as item}
											<div
												class="flex items-center justify-between gap-2 rounded-xl bg-base-200 px-3 py-2.5"
											>
												<div class="min-w-0 flex-1">
													<div class="truncate text-sm font-medium">{item.name}</div>
													<div class="flex items-center gap-0.5 text-xs text-base-content/50">
														{item.quantity} × {item.price}
														<Currency size={10} />
													</div>
												</div>
												<div
													class="shrink-0 text-sm font-bold text-warning flex items-center gap-0.5"
												>
													{item.price * item.quantity}
													<Currency size={12} />
												</div>
											</div>
										{/each}
									</div>
								{/if}

								<div class="my-3 border-t border-base-300"></div>

								<div class="mb-4 flex items-center justify-between font-semibold">
									<span>Gesamt</span>
									<span class="flex items-center gap-0.5 text-warning">
										{cartTotal}
										<Currency size={15} />
									</span>
								</div>
								<div class="mb-4 flex items-center justify-between font-semibold">
									<span>Euro Betrag</span>
									<span class="flex items-center gap-0.5 text-warning">
										{cartEuroValue.toFixed(2)}
										<Euro size={15} />
									</span>
								</div>

								<div class="mb-4 flex items-center justify-between font-semibold">
									<span class="text-sm">Guthaben</span>
									<span class="flex items-center gap-0.5 text-error text-sm">
										{userAccount?.balance}
										<Currency size={14} />
									</span>
								</div>

								<div class="grid gap-2">
									<button
										type="button"
										class="btn btn-warning btn-sm w-full"
										on:click={() => (activeTab = 'cart')}
										disabled={card.length === 0}
									>
										Zur Kasse
									</button>
									<button
										type="button"
										class="btn btn-ghost btn-sm w-full"
										on:click={() => (card = [])}
										disabled={card.length === 0}
									>
										Leeren
									</button>
								</div>
							</div>
						</div>
					</div>
				</div>
			{/if}

			<!-- ==================== CART TAB ==================== -->
			{#if activeTab === 'cart'}
				<div class="grid min-w-0 gap-4 xl:grid-cols-[minmax(0,1fr)_300px]">
					<div class="flex flex-col">
						<!-- Cart items -->
						<div class="rounded-2xl border border-base-300 bg-base-100 shadow-sm overflow-hidden">
							<div class="flex items-center gap-3 border-b border-base-300 bg-base-200 px-4 py-3">
								<div
									class="flex h-8 w-8 items-center justify-center rounded-lg bg-warning/30 text-warning"
								>
									<ShoppingCart size={16} />
								</div>
								<div>
									<h2 class="text-sm font-semibold">Warenkorb</h2>
									<p class="text-xs text-base-content/50">{card.length} Artikel ausgewählt</p>
								</div>
							</div>

							{#if card.length === 0}
								<div class="py-16 text-center">
									<ShoppingCart size={40} class="mx-auto mb-3 opacity-20" />
									<p class="text-sm text-base-content/50">Dein Warenkorb ist leer.</p>
									<button
										type="button"
										class="btn btn-primary btn-sm mt-4"
										on:click={() => (activeTab = 'products')}
									>
										Produkte ansehen
									</button>
								</div>
							{:else}
								<!-- Mobile: card list -->
								<div class="divide-y divide-base-300 md:hidden">
									{#each card as item}
										<div class="p-3 sm:p-4">
											<div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
												<div class="min-w-0">
													<div class="font-medium">{item.name}</div>
													<div class="mt-0.5 text-xs text-base-content/50">
														{@html item.description}
													</div>
												</div>
												<div class="shrink-0 text-right">
													<div class="flex items-center justify-end gap-0.5 font-bold text-warning">
														{item.price * item.quantity}
														<Currency size={13} />
													</div>
													<div
														class="mt-0.5 flex items-center justify-end gap-0.5 text-xs text-base-content/50"
													>
														{item.price}
														<Currency size={10} />
														je Stk.
													</div>
												</div>
											</div>
											<div class="flex items-center justify-stretch sm:justify-end">
												<div class="join grid w-full grid-cols-[44px_minmax(0,1fr)_44px] sm:flex sm:w-auto">
													<button
														type="button"
														class="btn btn-ghost join-item h-11 w-11 border border-base-300 p-0"
														on:click={() => removeFromCard(item)}
													>
														<Minus size={15} />
													</button>
													<input
														type="number"
														min="0"
														max={item.max !== -1 ? item.max : undefined}
														class="input input-bordered join-item h-11 min-w-0 w-full text-center disable-browser-updown sm:w-16"
														value={item.quantity}
														on:input={(e) => setAmount(item, parseInt(e.currentTarget.value))}
														autocomplete="off"
													/>
													<button
														type="button"
														class="btn btn-primary join-item h-11 w-11 p-0"
														on:click={() => addToCard(item)}
													>
														<Plus size={15} />
													</button>
												</div>
											</div>
										</div>
									{/each}
								</div>

								<!-- Desktop: table -->
								<div class="hidden overflow-x-auto md:block">
									<table class="table">
										<thead class="bg-base-200 text-xs">
											<tr>
												<th>Produkt</th>
												<th>Einzelpreis</th>
												<th>Menge</th>
												<th>Europreis</th>
												<th>Gesamt</th>
											</tr>
										</thead>
										<tbody>
											{#each card as item}
												<tr class="hover">
													<td>
														<div class="font-medium">{item.name}</div>
														<div class="text-xs text-base-content/50">{@html item.description}</div>
													</td>
													<td>
														<div class="flex items-center gap-0.5 text-sm">
															{item.price}
															<Currency size={12} />
														</div>
													</td>
													<td>
														<div class="join">
															<button
																type="button"
																class="btn btn-sm join-item"
																on:click={() => removeFromCard(item)}
															>
																<Minus size={14} />
															</button>
															<input
																type="number"
																min="0"
																max={item.max !== -1 ? item.max : undefined}
																class="input input-sm input-bordered join-item w-16 text-center disable-browser-updown"
																value={item.quantity}
																on:input={(e) => setAmount(item, parseInt(e.currentTarget.value))}
																autocomplete="off"
															/>
															<button
																type="button"
																class="btn btn-sm join-item btn-primary"
																on:click={() => addToCard(item)}
															>
																<Plus size={14} />
															</button>
														</div>
													</td>
													<td>
														<div class="flex items-center gap-0.5 text-sm">
															{item.euroPrice * item.quantity}
															<Euro size={12} />
														</div>
													</td>
													<td>
														<div class="flex items-center gap-0.5 font-bold text-warning">
															{item.price * item.quantity}
															<Currency size={12} />
														</div>
													</td>
												</tr>
											{/each}
										</tbody>
									</table>
								</div>
							{/if}
						</div>

						<div role="alert" class="alert alert-warning mt-4 items-start text-sm shadow-sm"><span>Hinweis: Alle Produkte sowie Preise sind vom Rewe. Wir können keine anderen Produkte/Preise anbieten. Mengenangaben können leicht abweichen.</span></div>

						<div class="mt-4 rounded-xl border border-base-300 bg-base-100 p-4 shadow-sm">
							<div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
								<div>
									<h1 class="text-md font-bold">Extrawünsche</h1>
									<p class="text-sm opacity-70">
										Lege hier extra Wünsche aus dem Rewe an. Bitte benutze die beiden Felder. Für Bemerkungen, kannst du das Textfeld darunter verwenden.
									</p>
								</div>
								<button type="button" class="btn btn-sm btn-primary w-full sm:w-auto" on:click={() => addExtraWishItem('new')}>
									<Plus size={14} />
									Eintrag hinzufügen
								</button>
							</div>

							<div class="mt-4 space-y-3">
								{#each extraWishItems as item, index}
									<div class="grid gap-2 rounded-xl bg-base-200 p-3 sm:grid-cols-[minmax(0,1fr)_100px_auto] sm:items-end">
										<label class="form-control w-full">
											<div class="label py-0">
												<span class="label-text text-xs opacity-60">Artikelname oder Nummer</span>
											</div>
											<input
												type="text"
												class="input input-bordered input-sm w-full"
												placeholder="z. B. 123456 oder Apfel"
												value={item.articleNumber}
												on:input={(e) => updateExtraWishItem('new', index, 'articleNumber', e.currentTarget.value)}
												autocomplete="off"
											/>
										</label>

										<label class="form-control w-full">
											<div class="label py-0">
												<span class="label-text text-xs opacity-60">Menge</span>
											</div>
											<input
												type="number"
												min="1"
												class="input input-bordered input-sm w-full"
												value={item.amount}
												on:input={(e) => updateExtraWishItem('new', index, 'amount', e.currentTarget.value)}
												autocomplete="off"
											/>
										</label>

										<button type="button" class="btn btn-sm btn-error btn-outline w-full sm:w-auto" on:click={() => removeExtraWishItem('new', index)}>
											<Trash size={14} />
										</button>
									</div>
								{/each}
							</div>

							<div class="my-4 border-t border-base-300"></div>

							<div class="grid gap-3 rounded-xl bg-base-200 p-3">
								<div>
									<div class="mb-1 text-sm font-semibold">Weitere Wünsche</div>
									<p class="text-xs opacity-60">
										Schreibe hier weitere Wünsche oder Hinweise, die du dem Warenlager Team mitteilen möchtest. Diese werden in der Bestellung vermerkt.
									</p>
								</div>
								<textarea
									class="textarea textarea-bordered w-full"
									rows="3"
									placeholder="Weitere Wünsche oder Hinweise..."
									bind:value={extraWishNotes}
								></textarea>
							</div>

							<div class="mt-4 rounded-xl border border-dashed border-base-300 bg-base-200/50 p-3 text-sm">
								<div class="mb-1 font-semibold">Formatvorschau</div>
								<pre class="whitespace-pre-wrap break-words text-xs opacity-80">{extrasText || 'Keine Extrawünsche'}</pre>
							</div>
						</div>
					</div>

					<!-- Order summary panel -->
					<div
						class="rounded-2xl border border-base-300 bg-base-100 shadow-sm overflow-hidden xl:sticky xl:top-6 xl:self-start"
					>
						<div class="border-b border-base-300 bg-base-200 px-4 py-3">
							<h2 class="text-sm font-semibold">Bestellung abschließen</h2>
							<p class="mt-0.5 text-xs text-base-content/50">Direkt aus dem Warenkorb senden</p>
						</div>
						<div class="p-4">
							<div class="mb-4 space-y-2 rounded-xl bg-base-200 p-3">
								<div class="flex justify-between text-sm text-base-content/60">
									<span>Artikel</span>
									<span>{card.length}</span>
								</div>
								<div class="flex items-center justify-between font-bold">
									<span>Gesamt</span>
									<span class="flex items-center gap-0.5 text-warning">
										{cartTotal}
										<Currency size={14} />
									</span>
								</div>
								<div class="mb-4 flex items-center justify-between font-semibold">
									<span>Euro Betrag</span>
									<span class="flex items-center gap-0.5 text-warning">
										{cartEuroValue.toFixed(2)}
										<Euro size={15} />
									</span>
								</div>
								<div class="flex items-center justify-between font-semibold">
									<span>Guthaben</span>
									<span class="flex items-center gap-0.5 text-error text-sm">
										{userAccount?.balance}
										<Currency size={14} />
									</span>
								</div>
							</div>

							<div
								class="mb-4 rounded-xl border border-base-300 bg-base-200 px-3 py-2.5 text-xs text-base-content/70"
							>
								Falls bereits eine Bestellung im Status „Angefragt" existiert, wird diese erweitert.
							</div>

							<div class="grid gap-2">
								<button
									type="button"
									class="btn btn-primary btn-sm w-full"
									on:click={placeOrder}
									disabled={(card.length === 0 && !extraWishNotes && !extrasText) || placingOrder}
								>
									{#if placingOrder}
										<span class="loading loading-spinner loading-xs"></span>
										Wird verarbeitet...
									{:else}
										Bestellung abschließen
									{/if}
								</button>
								<button
									type="button"
									class="btn btn-ghost btn-sm w-full"
									on:click={() => (activeTab = 'products')}
								>
									Weiter einkaufen
								</button>
							</div>
						</div>
					</div>
				</div>
			{/if}

			<!-- ==================== ORDERS TAB ==================== -->
			{#if activeTab === 'orders'}
				<div class="rounded-2xl border border-base-300 bg-base-100 shadow-sm overflow-hidden">
					<div class="flex items-center gap-3 border-b border-base-300 bg-base-200 px-4 py-3">
						<div
							class="flex h-8 w-8 items-center justify-center rounded-lg bg-secondary/30 text-secondary"
						>
							<ClipboardList size={16} />
						</div>
						<div>
							<h2 class="text-sm font-semibold">Bestellungen</h2>
							<p class="text-xs text-base-content/50">{orders.length} Einträge</p>
						</div>
					</div> 

					{#if orders.length === 0}
						<div class="py-16 text-center">
							<ClipboardList size={40} class="mx-auto mb-3 opacity-20" />
							<p class="text-sm text-base-content/50">Noch keine Bestellungen vorhanden.</p>
							<button
								type="button"
								class="btn btn-primary btn-sm mt-4"
								on:click={() => (activeTab = 'products')}
							>
								Jetzt bestellen
							</button>
						</div>
					{:else}
						<!-- Mobile: card list -->
						<div class="divide-y divide-base-300 md:hidden">
							{#each orders as order}
								<div class="p-3 sm:p-4">
									<div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
										<div class="min-w-0">
											<div class="break-all font-mono text-xs text-base-content/40">{order.id}</div>
											<div class="mt-1 text-xs text-base-content/60">
												{new Date(order.created).toLocaleDateString()}
											</div>
										</div>
										<div class="shrink-0 text-right">
											<div class="flex items-center justify-end gap-0.5 font-bold">
												{order.cost + order.extraCost}
												<Currency size={13} />
											</div>
											<div class="mt-1">
												{#if order.status === 'requested'}
													<span class="badge badge-warning badge-sm">Angefragt</span>
												{:else if order.status === 'approved'}
													<span class="badge badge-info badge-sm">Akzeptiert</span>
												{:else if order.status === 'rejected'}
													<span class="badge badge-error badge-sm">Abgelehnt</span>
												{:else if order.status === 'collectable'}
													<span class="badge badge-success badge-sm">Abholbereit</span>
												{:else if order.status === 'collected'}
													<span class="badge badge-neutral badge-sm">Abgeholt</span>
												{:else}
													<span class="badge badge-ghost badge-sm">{order.status}</span>
												{/if}
											</div>
										</div>
									</div>
									<div class="flex items-center justify-end gap-2">
										{#if order.status === 'requested' || order.status === 'rejected' || order.status === 'collected'}
											<button
												type="button"
												class="btn btn-sm btn-error btn-outline"
												on:click={() => deleteOrder(order)}
											>
												<Trash size={15} />
											</button>
										{/if}
										{#if order.status === 'requested'}
											<button
												type="button"
												class="btn btn-sm btn-warning"
												on:click={() => editOrder(order)}
											>
												<Pen size={15} />
											</button>
										{:else}
											<button
												type="button"
												class="btn btn-sm btn-primary"
												on:click={() => viewOrder(order)}
											>
												<Eye size={15} />
											</button>
										{/if}
										{#if order.transaction}
											<button
												type="button"
												class="btn btn-sm btn-ghost"
												on:click={() =>
													window.open(`/transactions?id=${order.transaction}`, '_blank')}
											>
												<ArrowLeftRight size={15} />
											</button>
										{/if}
									</div>
								</div>
							{/each}
						</div>

						<!-- Desktop: table -->
						<div class="hidden overflow-x-auto md:block">
							<table class="table">
								<thead class="bg-base-200 text-xs">
									<tr>
										<th>Bestellnummer</th>
										<th>Preis</th>
										<th>Status</th>
										<th>Datum</th>
										<th class="text-right">Aktionen</th>
									</tr>
								</thead>
								<tbody>
									{#each allOrders as order}
										<tr class="hover">
											<td class="font-mono text-xs opacity-60">{order.id}</td>
											<td>
												<div class="flex items-center gap-0.5 font-medium">
													{order.cost + order.extraCost}
													<Currency size={12} />
												</div>
											</td>
											<td>
												{#if order.status === 'requested'}
													<span class="badge badge-warning">Angefragt</span>
												{:else if order.status === 'approved'}
													<span class="badge badge-info">Akzeptiert</span>
												{:else if order.status === 'rejected'}
													<span class="badge badge-error">Abgelehnt</span>
												{:else if order.status === 'collectable'}
													<span
														class="badge badge-success tooltip gap-1"
														data-tip="Die Bestellung ist bereit für die Abholung."
													>
														Abholbereit
														<BadgeQuestionMark size={12} />
													</span>
												{:else if order.status === 'collected'}
													<span class="badge badge-neutral">Abgeholt</span>
												{:else}
													<span class="badge badge-ghost">{order.status}</span>
												{/if}
											</td>
											<td class="text-sm text-base-content/60"
												>{new Date(order.created).toLocaleDateString()}</td
											>
											<td>
												<div class="flex justify-end gap-2">
													{#if order.status === 'requested' || order.status === 'rejected' || order.status === 'collected'}
														<button
															type="button"
															class="btn btn-sm btn-error btn-outline"
															on:click={() => deleteOrder(order)}
														>
															<Trash size={15} />
														</button>
													{/if}
													{#if order.status === 'requested'}
														<button
															type="button"
															class="btn btn-sm btn-warning"
															on:click={() => editOrder(order)}
														>
															<Pen size={15} />
														</button>
													{:else}
														<button
															type="button"
															class="btn btn-sm btn-primary"
															on:click={() => viewOrder(order)}
														>
															<Eye size={15} />
														</button>
													{/if}
													{#if order.transaction}
														<button
															type="button"
															class="btn btn-sm btn-ghost"
															on:click={() =>
																window.open(`/transactions?id=${order.transaction}`, '_blank')}
														>
															<ArrowLeftRight size={15} />
														</button>
													{/if}
												</div>
											</td>
										</tr>
									{/each}
								</tbody>
							</table>
						</div>
					{/if}
				</div>
			{/if}
		</main>

		<!-- Mobile bottom navigation -->
		<nav
			class="fixed bottom-0 left-0 right-0 z-40 border-t border-base-300 bg-base-100/95 pb-[env(safe-area-inset-bottom)] shadow-[0_-4px_16px_rgba(0,0,0,0.08)] backdrop-blur md:hidden"
		>
			<div class="mx-auto grid max-w-lg grid-cols-3">
				<button
					type="button"
					class="mobile-nav-item"
					class:mobile-nav-active={activeTab === 'products'}
					on:click={() => (activeTab = 'products')}
				>
					<Package size={22} />
					<span>Produkte</span>
				</button>

				<button
					type="button"
					class="mobile-nav-item relative"
					class:mobile-nav-active={activeTab === 'cart'}
					on:click={() => (activeTab = 'cart')}
				>
					<ShoppingCart size={22} />
					<span>Warenkorb</span>
					{#if card.length > 0}
						<span
							class="absolute right-[22%] top-2 flex h-4 min-w-4 items-center justify-center rounded-full bg-warning px-1 text-[10px] font-bold text-warning-content"
						>
							{card.length}
						</span>
					{/if}
				</button>

				<button
					type="button"
					class="mobile-nav-item"
					class:mobile-nav-active={activeTab === 'orders'}
					on:click={() => (activeTab = 'orders')}
				>
					<ClipboardList size={22} />
					<span>Bestellungen</span>
				</button>
			</div>
		</nav>
	</div>

	<!-- Edit Modal -->
	<dialog id="editModal" class="modal modal-bottom sm:modal-middle" bind:this={editModal}>
		<div class="modal-box max-h-[92dvh] w-full max-w-4xl overflow-y-auto rounded-t-2xl p-3 sm:rounded-box sm:p-6">
			<h3 class="text-lg font-bold">Bestellung bearbeiten</h3>
			<div class="divider"></div>

			{#if selectedOrder}
				<div class="overflow-x-auto rounded-xl border border-base-300">
					<table class="table min-w-[760px]">
						<thead class="bg-base-200">
							<tr>
								<th>Name</th>
								<th>Beschreibung</th>
								<th>Preis [Stk|Insgesamt]</th>
								<th>Europreis [Stk|Insgesamt]</th>
								<th>Menge</th>
							</tr>
						</thead>
						<tbody>
							{#each selectedOrder.expand?.items ?? [] as orderItem (orderItem.id)}
								{@const product = orderItem.expand?.item}
								{#if product}
									<tr>
										<th>{product.name}</th>
										<td>{@html product.description}</td>
										<td>
											<div class="flex items-center gap-0.5">
												{product.price}
												<Currency size={12} />
												|
												{product.price * orderItem.amount}
												<Currency size={12} />
											</div>
										</td>
										<td>
											<div class="flex items-center gap-0.5">
												{product.euroPrice.toFixed(2)}
												<Euro size={12} />
												|
												{(product.euroPrice * orderItem.amount).toFixed(2)}
												<Euro size={12} />
											</div>
										</td>
										<td>
											<input
												type="number"
												value={orderItem.amount}
												min="1"
												class="input input-bordered input-sm w-20"
												on:input={(e) =>
													setEditAmount(orderItem, parseInt(e.currentTarget.value, 10))}
												autocomplete="off"
											/>
										</td>
									</tr>
								{/if}
							{/each}
						</tbody>
					</table>
				</div>

				<div class="mt-3 grid w-full gap-2 rounded-xl bg-base-200 p-3 md:grid-cols-[1fr_auto_1fr] md:items-center md:gap-4 md:bg-transparent md:p-0">
					<div class="flex w-full items-center justify-between gap-4 font-semibold">
						<span>Neuer Gesamtpreis</span>

						<span class="flex items-center gap-1 text-warning">
							{editedOrderTotal}
							<Currency size={14} />
						</span>
					</div>

					<div class="divider my-0 md:divider-horizontal md:mx-0 md:h-8"></div>

					<div class="flex w-full items-center justify-between gap-4 font-semibold">
						<span>Neuer Eurobetrag</span>

						<span class="flex items-center gap-1 text-warning">
							{editedOrderEuroTotal.toFixed(2)}
							<Euro size={14} />
						</span>
					</div>
				</div>

				<div class="mt-4 rounded-xl border border-base-300 bg-base-100 p-4 shadow-sm">
					<div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
						<div>
							<h2 class="text-sm font-semibold text-base-content/70">Extrawünsche</h2>
							<p class="text-xs text-base-content/50">Füge hier extra Wünsche aus dem Rewe an.</p>
						</div>
						<button type="button" class="btn btn-sm btn-primary w-full sm:w-auto" on:click={() => addExtraWishItem('edit')}>
							<Plus size={14} />
							Eintrag hinzufügen
						</button>
					</div>

					<div class="mt-4 space-y-3">
						{#each selectedOrderExtraWishItems as item, index}
							<div class="grid gap-2 rounded-xl bg-base-200 p-3 sm:grid-cols-[minmax(0,1fr)_100px_auto] sm:items-end">
								<label class="form-control w-full">
									<div class="label py-0">
										<span class="label-text text-xs opacity-60">Artikelname oder Nummer</span>
									</div>
									<input
										type="text"
										class="input input-bordered input-sm w-full"
										placeholder="z. B. 123456 oder Apfel"
										value={item.articleNumber}
										on:input={(e) => updateExtraWishItem('edit', index, 'articleNumber', e.currentTarget.value)}
										autocomplete="off"
									/>
								</label>

								<label class="form-control w-full">
									<div class="label py-0">
										<span class="label-text text-xs opacity-60">Menge</span>
									</div>
									<input
										type="number"
										min="1"
										class="input input-bordered input-sm w-full"
										value={item.amount}
										on:input={(e) => updateExtraWishItem('edit', index, 'amount', e.currentTarget.value)}
										autocomplete="off"
									/>
								</label>

								<button type="button" class="btn btn-sm btn-error btn-outline w-full sm:w-auto" on:click={() => removeExtraWishItem('edit', index)}>
									<Trash size={14} />
								</button>
							</div>
						{/each}
					</div>

					<div class="my-4 border-t border-base-300"></div>

					<div class="grid gap-3 rounded-xl bg-base-200 p-3">
						<div>
							<div class="mb-1 text-sm font-semibold">Weitere Wünsche</div>
							<p class="text-xs opacity-60">Schreibe hier weitere Wünsche oder Hinweise, die du dem Warenlager Team mitteilen möchtest.</p>
						</div>
						<textarea
							class="textarea textarea-bordered w-full"
							rows="3"
							placeholder="Weitere Wünsche oder Hinweise..."
							bind:value={selectedOrderExtraWishNotes}
						></textarea>
					</div>

					<div class="mt-4 rounded-xl border border-dashed border-base-300 bg-base-200/50 p-3 text-xs">
						<pre class="whitespace-pre-wrap break-words opacity-80">{selectedOrderExtrasText || 'Keine Extrawünsche'}</pre>
					</div>
				</div>

				<div class="mt-4 grid grid-cols-2 gap-2 sm:flex sm:justify-end">
					<button type="button" class="btn btn-ghost w-full sm:w-auto" on:click={() => editModal.close()}
						>Schließen</button
					>
					<button type="button" class="btn btn-primary w-full sm:w-auto" on:click={updateEditedOrder}
						>Speichern</button
					>
				</div>
			{/if}
		</div>

		<form method="dialog" class="modal-backdrop">
			<button>close</button>
		</form>
	</dialog>

	<!-- View Modal -->
	<dialog id="viewModal" class="modal modal-bottom sm:modal-middle" bind:this={viewModal}>
		<div class="modal-box max-h-[92dvh] w-full max-w-4xl overflow-y-auto rounded-t-2xl p-3 sm:rounded-box sm:p-6">
			<h3 class="text-lg font-bold">Bestellung ansehen</h3>
			<div class="divider"></div>

			{#if selectedOrder}
				<div class="overflow-x-auto rounded-xl border border-base-300">
					<table class="table min-w-[760px]">
						<thead class="bg-base-200">
							<tr>
								<th>Name</th>
								<th>Beschreibung</th>
								<th>Preis</th>
								<th>Euro</th>
								<th>Menge</th>
							</tr>
						</thead>
						<tbody>
							{#each selectedOrder.items as itemId}
								{#await db
									.collection('order_items')
									.getOne(itemId, { requestKey: null }) then orderItem}
									{#await db
										.collection('stock')
										.getOne(orderItem.item, { requestKey: null }) then product}
										<tr>
											<th>{product.name}</th>
											<td>{@html product.description ? product.description : '-'}</td>
											<td>
												<div class="flex items-center gap-0.5">
													{product.price}
													<Currency size={12} />
												</div>
											</td>
											<td>
												<div class="flex items-center gap-0.5">
													{product.euroPrice.toFixed(2)}
													<Euro size={12} />
												</div>
											</td>
											<td>{orderItem.available}/{orderItem.amount}</td>
										</tr>
									{/await}
								{/await}
							{/each}
						</tbody>
					</table>
				</div>

				<div class="shadow mt-2 mb-2 p-4 rounded">
					<h2 class="text-sm font-semibold text-base-content/50 mb-1">Extrawünsche:</h2>
					<div class="divider mt-1 mb-1"></div>
					{#if selectedOrderExtraWishItems.length > 0}
						<div class="space-y-2">
							{#each selectedOrderExtraWishItems as item}
								<div class="flex items-center justify-between gap-3 rounded-lg bg-base-200 px-3 py-2 text-sm">
									<span class="font-medium">{item.articleNumber}</span>
									<span class="badge badge-outline">x {item.amount}</span>
								</div>
							{/each}
						</div>
					{/if}
					{#if selectedOrderExtraWishNotes}
						{#if selectedOrderExtraWishItems.length > 0}
							<div class="my-3 border-t border-base-300"></div>
						{/if}
						<p class="whitespace-pre-wrap text-sm text-base-content/75">{selectedOrderExtraWishNotes}</p>
					{/if}
					{#if !selectedOrderExtraWishItems.length && !selectedOrderExtraWishNotes}
						<p class="text-sm text-base-content/75">Keine Extrawünsche</p>
					{/if}
				</div>

				{#if selectedOrder?.extraCost || selectedOrder?.extraEuroCost}
				<div class="shadow mb-2 rounded">
					<p class="text-sm font-semibold text-base-content/50 p-4">Zusatzkosten:</p>
					<div class="divider mt-0 mb-0"></div>
					<div class="flex flex-col">
						<p class="text-sm text-base-content/75 flex flex-row items-center gap-1 p-4 pb-1 pt-2">
							{#if selectedOrder?.extraCost}
								Zusatzkosten (Steine): {selectedOrder.extraCost} <Currency size={12} />
							{/if}
						</p>
						<p class="text-sm text-base-content/75 flex flex-row items-center gap-1 p-4 pt-1">
							{#if selectedOrder?.extraEuroCost}
								Zusatzkosten (Euro): {selectedOrder.extraEuroCost.toFixed(2)} <Euro size={12} />
							{/if}
						</p>
					</div>
				</div>
				{/if}
				<div class="mt-4 flex justify-stretch sm:justify-end">
					<button type="button" class="btn btn-ghost w-full sm:w-auto" on:click={() => viewModal.close()}
						>Schließen</button
					>
				</div>
			{/if}
		</div>

		<form method="dialog" class="modal-backdrop">
			<button>close</button>
		</form>
	</dialog>
</Site>

<style>
	:global(.prose p) {
		margin: 0;
	}

	/* Desktop tab bar */
	:global(.desktop-tab) {
		display: flex;
		align-items: center;
		gap: 0.375rem;
		padding: 0.625rem 1.25rem;
		font-size: 0.8125rem;
		font-weight: 500;
		color: oklch(var(--bc) / 0.6);
		border-bottom: 2px solid transparent;
		transition: all 0.15s ease;
		white-space: nowrap;
	}

	:global(.desktop-tab:hover) {
		color: oklch(var(--bc) / 1);
		background-color: oklch(var(--b2) / 1);
	}

	:global(.desktop-tab.tab-active) {
		color: oklch(var(--p) / 1);
		border-bottom-color: oklch(var(--p) / 1);
		background-color: oklch(var(--p) / 0.05);
	}

	/* Mobile bottom nav */
	:global(.mobile-nav-item) {
		position: relative;
		display: flex;
		flex-direction: column;
		align-items: center;
		justify-content: center;
		gap: 0.2rem;
		padding: 0.625rem 0.5rem;
		font-size: 0.625rem;
		font-weight: 500;
		color: oklch(var(--bc) / 0.5);
		transition: all 0.15s ease;
		min-height: 60px;
		touch-action: manipulation;
	}

	:global(.mobile-nav-item.mobile-nav-active) {
		color: oklch(var(--p) / 1);
	}

	:global(.mobile-nav-item.mobile-nav-active svg) {
		color: oklch(var(--p) / 1);
	}

	@media (max-width: 767px) {
		:global(.modal-box) {
			overscroll-behavior: contain;
		}

		:global(.product-card .join .btn) {
			min-height: 2.5rem;
		}
	}

	/* Spin button hide */
	.disable-browser-updown::-webkit-outer-spin-button,
	.disable-browser-updown::-webkit-inner-spin-button {
		-webkit-appearance: none;
		margin: 0;
	}
</style>

<PopupModal bind:this={popupModal} />
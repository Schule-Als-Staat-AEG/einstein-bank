<script lang="ts">
	import db, { server } from '$lib/connections';
	import Currency from '$modules/Currency.svelte';
	import Site from '$modules/Site.svelte';
	import {
		BadgeQuestionMark,
		Check,
		Info,
		PackagePlus,
		X,
		ChevronLeft,
		ChevronRight,
		ClipboardList,
		PackageCheck,
		Truck,
		List,
		Printer,
		CheckCheck,
		Pencil,
		Receipt,
		Plus,
		Search,
		CheckCircle,
		Trash2,

		Euro,

		RotateCw,

		User



	} from '@lucide/svelte';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';

	type OrderStatus = 'requested' | 'approved' | 'rejected' | 'collectable' | 'collected' | string;

	let page = 1;
	let perPage = 30;
	let totalPages = 1;

	let selectedOrder: any = null;
	let selectedItems: any[] = [];

	let viewOrderModal: HTMLDialogElement;
	let warehouseItemSelectModal: HTMLDialogElement;

	let activeTab: 'all' | 'requested' | 'approved' | 'collectable' = 'all';
	let loading = false;

	let currentOrders: any[] = [];

	// Pipeline counts
	let countRequested = 0;
	let countApproved = 0;
	let countCollectable = 0;

	let stockModal: HTMLDialogElement;
	let editProductModal: HTMLDialogElement;
	let createProductModal: HTMLDialogElement;
	let userManagementModal: HTMLDialogElement;
	let userManagementEditModal: HTMLDialogElement;

	let createProductName = '';
	let createProductPrice = 0;
	let createProductEuroPrice = 0;
	let createProductDescription = '';
	let createProductMax = -1;
	let createProductCategory = '';

	let editProductName = '';
	let editProductPrice = 0;
	let editProductEuroPrice = 0;
	let editProductDescription = '';
	let editProductMax = -1;
	let editProductID = '';
	let editProductCategory = '';

	let productModalPage = 1;
	let productModalPerPage = 5;
	let productModalFiler = '';

	let userManagementPage = 1;
	let userManagementPerPage = 10;
	let userManagementTotalPages = 1;
	let userManagementFilter = '';

	let userManagementUsers: any[] = [];
	let userManagementSelectedUser: any = null;
	let editUserEuroLimit = 0;
	let editUserEuroSpent = 0;

	let orderFilter = '';

	let extraCostDirty = false;
	let extrasDirty = false;
	let warehouseItemID = '';
	let warehouseItemAmount = 1;
	let warehouseItemSaving = false;
	let selectedWarehouseProduct: any = null;

	let warehouseItemSearch = '';
	let warehouseItemCategory = 'all';
	let warehouseItemPage = 1;
	let warehouseItemPerPage = 20;
	let warehouseItemTotalPages = 1;
	let warehouseItemTotalItems = 0;
	let warehouseItemResults: any[] = [];
	let warehouseItemResultsLoading = false;
	let warehouseItemSearchTimer: ReturnType<typeof setTimeout> | null = null;
	let warehouseItemRequestSerial = 0;

	let orderMetaSaving = false;

	let stock: Set<any> = new Set();

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

	function getCategoryLabel(category: string | null | undefined) {
		return categoryOptions.find((option) => option.value === category)?.label ?? 'Ohne Kategorie';
	}

	onMount(async () => {
		if (!browser) return;
		loading = true;
		await loadActiveTab();
		await refreshCounts();
		await fetchStock();
	});

	async function refreshCounts() {
		try {
			const [r, a, c] = await Promise.all([
				db.collection('orders').getList(1, 1, { filter: `status="requested"`, requestKey: null }),
				db.collection('orders').getList(1, 1, { filter: `status="approved"`, requestKey: null }),
				db.collection('orders').getList(1, 1, { filter: `status="collectable"`, requestKey: null })
			]);
			countRequested = r.totalItems;
			countApproved = a.totalItems;
			countCollectable = c.totalItems;
		} catch {}
	}

	async function enrichOrdersWithCompany(items: any[]) {
		await Promise.all(
			items.map(async (item) => {
				try {
					const company = await db
						.collection('public_users')
						.getOne(item.company, { requestKey: null });
					item.companyName = company.companyName || company.name || 'Unbekannt';
					item.userName = company.name || 'Unbekannt';
				} catch {
					item.companyName = 'Unbekannt';
					item.userName = 'Unbekannt';
				}
			})
		);
	}

	function sortOrdersNewest(items: any[]) {
		return [...items].sort((a, b) => new Date(b.created).getTime() - new Date(a.created).getTime());
	}

	async function fetchOrdersByFilter(filter = '', sortNewest = true) {
		loading = true;
		try {
			const result = await db.collection('enriched_orders').getList(page, perPage, {
				sort: '-created,status',
				filter,
				requestKey: null
			});
			await enrichOrdersWithCompany(result.items);
			totalPages = result.totalPages;
			if (sortNewest) {
				currentOrders = sortOrdersNewest(result.items);
			} else {
				currentOrders = result.items;
			}
		} finally {
			loading = false;
		}
	}

	async function fetchAllOrders() {
		activeTab = 'all';
		await fetchOrdersByFilter(`(companyName ~ "${orderFilter}" || userName ~ "${orderFilter}")`, false);
	}
	async function fetchRequestedOrders() {
		activeTab = 'requested';
		await fetchOrdersByFilter(`status="requested" && (companyName ~ "${orderFilter}" || userName ~ "${orderFilter}")`);
	}
	async function fetchApprovedOrders() {
		activeTab = 'approved';
		await fetchOrdersByFilter(`status="approved" && (companyName ~ "${orderFilter}" || userName ~ "${orderFilter}")`);
	}
	async function fetchCollectableOrders() {
		activeTab = 'collectable';
		await fetchOrdersByFilter(`status="collectable" && (companyName ~ "${orderFilter}" || userName ~ "${orderFilter}")`);
	}

	async function loadActiveTab() {
		if (activeTab === 'all') return await fetchAllOrders();
		if (activeTab === 'requested') return await fetchRequestedOrders();
		if (activeTab === 'approved') return await fetchApprovedOrders();
		if (activeTab === 'collectable') return await fetchCollectableOrders();
	}

	async function acceptOrder(orderId: string) {
		await fetch(`${server}/api/warehouse/set-order`, {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
				Authorization: `Bearer ${db.authStore.token}`
			},
			body: JSON.stringify({ id: orderId, status: 'approved' })
		});
		await loadActiveTab();
		await refreshCounts();
	}

	async function rejectOrder(orderId: string) {
		await fetch(`${server}/api/warehouse/set-order`, {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
				Authorization: `Bearer ${db.authStore.token}`
			},
			body: JSON.stringify({ id: orderId, status: 'rejected' })
		});
		await loadActiveTab();
		await refreshCounts();
	}

	async function markOrderCollectable(orderId: string) {
		await fetch(`${server}/api/warehouse/set-order`, {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
				Authorization: `Bearer ${db.authStore.token}`
			},
			body: JSON.stringify({ id: orderId, status: 'collectable' })
		});
		await loadActiveTab();
		await refreshCounts();
	}

	async function markOrderCollected(orderId: string) {
		try {
			await fetch(`${server}/api/warehouse/set-order`, {
				method: 'POST',
				headers: {
					'Content-Type': 'application/json',
					Authorization: `Bearer ${db.authStore.token}`
				},
				body: JSON.stringify({ id: orderId, status: 'collected' })
			});
			alert('Bestellung erfolgreich als abgeholt markiert und archiviert.');
		} catch (error: any) {
			alert('Fehler beim Bezahlen der Bestellung: ' + (error?.message || 'Unbekannter Fehler'));
		}
		await loadActiveTab();
		await refreshCounts();
	}

	async function reloadSelectedOrderDetails() {
		if (!selectedOrder?.id) return;

		const [order, items] = await Promise.all([
			db.collection('orders').getOne(selectedOrder.id, { requestKey: null }),
			db.collection('order_items').getFullList({
				filter: `order="${selectedOrder.id}"`,
				expand: 'item',
				requestKey: null
			})
		]);

		selectedOrder = { ...selectedOrder, ...order };
		selectedItems = items;
	}

	async function showOrderDetails(order: any) {
		selectedOrder = {
			...order,
			extras: order.extras ?? '',
			extraCost: Number(order.extraCost ?? 0),
			extraEuroCost: Number(order.extraEuroCost ?? 0)
		};
		extrasDirty = false;
		extraCostDirty = false;
		warehouseItemID = '';
		warehouseItemAmount = 1;
		selectedWarehouseProduct = null;
		await reloadSelectedOrderDetails();
		viewOrderModal.showModal();
	}

	async function approveAllOrders() {
		if (!confirm('Möchten Sie wirklich alle angefragten Bestellungen akzeptieren?')) return;
		const result = await db
			.collection('orders')
			.getFullList({ filter: `status="requested"`, requestKey: null });
		await Promise.all(
			result.map(async (o: any) =>
				await fetch(`${server}/api/warehouse/set-order`, {
					method: 'POST',
					headers: {
						'Content-Type': 'application/json',
						Authorization: `Bearer ${db.authStore.token}`
					},
					body: JSON.stringify({ id: o.id, status: 'approved' })
				})
			)
		);
		await fetchRequestedOrders();
		await refreshCounts();
	}

	async function markAllOrdersCollectable() {
		if (!confirm('Möchten Sie wirklich alle akzeptierten Bestellungen als angekommen markieren?'))
			return;
		const result = await db
			.collection('orders')
			.getFullList({ filter: `status="approved"`, requestKey: null });
		await Promise.all(
			result.map(async (o: any) =>
				await fetch(`${server}/api/warehouse/set-order`, {
					method: 'POST',
					headers: {
						'Content-Type': 'application/json',
						Authorization: `Bearer ${db.authStore.token}`
					},
					body: JSON.stringify({ id: o.id, status: 'collectable' })
				})
			)
		);
		await fetchApprovedOrders();
		await refreshCounts();
	}

	function getStatusColor(status: OrderStatus): string {
		if (status === 'requested') return 'text-warning bg-warning/10 border-warning/30';
		if (status === 'approved') return 'text-primary bg-primary/10 border-primary/30';
		if (status === 'rejected') return 'text-error bg-error/10 border-error/30';
		if (status === 'collectable') return 'text-secondary bg-secondary/10 border-secondary/30';
		if (status === 'collected') return 'text-success bg-success/10 border-success/30';
		return 'text-base-content/50 bg-base-200 border-base-300';
	}

	function getStatusLabel(status: OrderStatus) {
		if (status === 'requested') return 'Angefragt';
		if (status === 'approved') return 'Akzeptiert';
		if (status === 'rejected') return 'Abgelehnt';
		if (status === 'collectable') return 'Abholbereit';
		if (status === 'collected') return 'Abgeholt';
		return status;
	}

	function stripHtml(html: string) {
		return html?.replace(/<[^>]*>/g, '').trim() || '';
	}

	async function printShoppingList() {
		const itemMap: { itemId: string; name: string; description: string; amount: number }[] = [];
		const orders = await db
			.collection('orders')
			.getFullList({ filter: `status="approved"`, requestKey: null });
		const orderItems = await Promise.all(
			orders.map((order: any) =>
				db
					.collection('order_items')
					.getFullList({ filter: `order="${order.id}"`, expand: 'item', requestKey: null })
			)
		);
		orderItems.forEach((items) => {
			items.forEach((item: any) => {
				const existing = itemMap.find((i) => i.itemId === item.item);
				if (existing) {
					existing.amount += item.available || 0;
				} else {
					itemMap.push({
						itemId: item.item,
						name: item.expand?.item?.name || 'Unbekannt',
						description: stripHtml(item.expand?.item?.description),
						amount: item.available || 0
					});
				}
			});
		});

		const shoppingItems = itemMap
			.filter((item) => item.amount > 0)
			.sort((a, b) => a.name.localeCompare(b.name, 'de'));
		const { jsPDF } = await import('jspdf');
		const doc = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' });
		const pageWidth = doc.internal.pageSize.getWidth();
		const pageHeight = doc.internal.pageSize.getHeight();
		const margin = 14;
		const contentWidth = pageWidth - margin * 2;
		const columnGap = 6;
		const columnWidth = (contentWidth - columnGap) / 2;
		const rowGap = 4;
		const rowPadding = 4;
		const tableTop = 42;
		const tableBottom = pageHeight - 16;
		const checkboxSize = 3.5;
		const nameWidth = columnWidth - 22;

		doc.setFillColor(20, 24, 31);
		doc.rect(0, 0, pageWidth, 28, 'F');
		doc.setTextColor(255, 255, 255);
		doc.setFont('helvetica', 'bold');
		doc.setFontSize(20);
		doc.text('Einkaufsliste', margin, 16);
		doc.setFont('helvetica', 'normal');
		doc.setFontSize(9.5);
		const now = new Date();
		doc.text(
			`Erstellt am ${now.toLocaleDateString('de-DE')} um ${now.toLocaleTimeString('de-DE', { hour: '2-digit', minute: '2-digit' })}`,
			pageWidth - margin,
			11,
			{ align: 'right' }
		);
		doc.text(`Positionen: ${shoppingItems.length}`, pageWidth - margin, 18, { align: 'right' });

		doc.setTextColor(255, 255, 255);
		doc.setDrawColor(255, 255, 255);
		doc.setLineWidth(0.4);
		doc.line(margin, 30, pageWidth - margin, 30);

		doc.setTextColor(60, 60, 60);
		doc.setFillColor(241, 245, 249);
		doc.setDrawColor(203, 213, 225);
		doc.setFont('helvetica', 'bold');
		doc.setFontSize(10);
		doc.rect(margin, tableTop - 8, contentWidth, 8, 'F');
		doc.text('Nr.', margin + 2, tableTop - 2.5);
		doc.text('Artikel', margin + 12, tableTop - 2.5);
		doc.text('Menge', pageWidth - margin - 2, tableTop - 2.5, { align: 'right' });

		let y = tableTop;
		let rowIndex = 1;

		const drawHeader = () => {
			doc.setTextColor(60, 60, 60);
			doc.setFillColor(241, 245, 249);
			doc.setDrawColor(203, 213, 225);
			doc.setFont('helvetica', 'bold');
			doc.setFontSize(10);
			doc.rect(margin, tableTop - 8, contentWidth, 8, 'F');
			doc.text('Nr.', margin + 2, tableTop - 2.5);
			doc.text('Artikel', margin + 12, tableTop - 2.5);
			doc.text('Menge', pageWidth - margin - 2, tableTop - 2.5, { align: 'right' });
		};

		const addPageFooter = () => {
			doc.setFont('helvetica', 'normal');
			doc.setFontSize(8);
			doc.setTextColor(120, 120, 120);
			doc.text(`Seite ${doc.getNumberOfPages()}`, pageWidth - margin, pageHeight - 7, {
				align: 'right'
			});
			doc.text(`Paul sieht IKEA – und denkt: Ich mach das selbst.`, margin, pageHeight - 7);
		};

		drawHeader();

		const descWidth = columnWidth - 22;

		const measure = (item: { name: string; description: string }) => {
			const nameLines = doc.splitTextToSize(item.name, nameWidth);
			const descLines = item.description ? doc.splitTextToSize(item.description, descWidth) : [];
			return { nameLines, descLines };
		};

		for (let index = 0; index < shoppingItems.length; index += 2) {
			const leftItem = shoppingItems[index];
			const rightItem = shoppingItems[index + 1];
			const leftMeasure = measure(leftItem);
			const rightMeasure = rightItem ? measure(rightItem) : { nameLines: [], descLines: [] };
			const textHeight = (measure: { nameLines: string[]; descLines: string[] }) =>
				measure.nameLines.length * 4.6 + (measure.descLines.length > 0 ? measure.descLines.length * 3.4 + 1.5 : 0);
			const cellHeight = Math.max(
				Math.max(textHeight(leftMeasure), textHeight(rightMeasure)) + rowPadding * 2,
				13
			);

			if (y + cellHeight > tableBottom) {
				addPageFooter();
				doc.addPage();
				y = tableTop;
				drawHeader();
			}

			const top = y - 6.5;
			const leftX = margin;
			const rightX = margin + columnWidth + columnGap;

			const drawCell = (
				item: { name: string; description: string; amount: number },
				cellX: number,
				cellIndex: number
			) => {
				const { nameLines, descLines } = measure(item);
				const fill = cellIndex % 2 === 0 ? [249, 250, 251] : [255, 255, 255];
				doc.setFillColor(fill[0], fill[1], fill[2]);
				doc.setDrawColor(226, 232, 240);
				doc.rect(cellX, top, columnWidth, cellHeight, 'FD');

				doc.setDrawColor(148, 163, 184);
				doc.setLineWidth(0.3);
				const checkboxX = cellX + 4.5;
				const contentX = checkboxX + checkboxSize + 4;
				const contentY = top + rowPadding + 3;
				doc.rect(checkboxX, top + (cellHeight - checkboxSize) / 2, checkboxSize, checkboxSize);

				doc.setFont('helvetica', 'normal');
				doc.setTextColor(15, 23, 42);
				doc.setFontSize(10.5);
				doc.text(nameLines, contentX, contentY);

				if (descLines.length > 0) {
					doc.setFont('helvetica', 'normal');
					doc.setFontSize(8);
					doc.setTextColor(100, 116, 139);
					doc.text(descLines, contentX, contentY + nameLines.length * 4.6 + 1);
				}

				doc.setFont('helvetica', 'bold');
				doc.setFontSize(9.5);
				doc.setTextColor(15, 23, 42);
				doc.text(`${item.amount}x`, cellX + columnWidth - 2, top + cellHeight / 2 + 1.3, {
					align: 'right'
				});
			};

			drawCell(leftItem, leftX, 0);
			if (rightItem) {
				drawCell(rightItem, rightX, 1);
			}

			y += cellHeight + rowGap;
			rowIndex += 2;
		}

		addPageFooter();
		doc.save('einkaufsliste.pdf');
	}

	async function sendWarehouseOrderUpdate(payload: Record<string, any>) {
		const response = await fetch(`${server}/api/warehouse/set-order`, {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
				Authorization: `Bearer ${db.authStore.token}`
			},
			body: JSON.stringify(payload)
		});

		let result: any = null;
		try {
			result = await response.json();
		} catch {
			// The backend may return an empty/non-JSON error body.
		}

		if (!response.ok || result?.valid === false) {
			throw new Error(result?.message || `Serverfehler (${response.status})`);
		}

		return result;
	}

	function updateItemLocally(itemId: string, value: number) {
		const item = selectedItems.find((i) => i.id === itemId);
		if (!item || !Number.isFinite(value)) return;

		if (item.warehouseAdded) {
			const amount = Math.max(1, Math.trunc(value));
			item.amount = amount;
			item.available = amount;
		} else {
			item.available = Math.max(0, Math.min(item.amount, Math.trunc(value)));
		}

		selectedItems = [...selectedItems];
	}

	async function updateItemAvailable(itemId: string, value: number) {
		const item = selectedItems.find((i) => i.id === itemId);
		if (!item) return;

		updateItemLocally(itemId, value);

		try {
			await sendWarehouseOrderUpdate({
				id: selectedOrder.id,
				items: [
					item.warehouseAdded
						? { id: itemId, action: 'update', amount: item.amount }
						: { id: itemId, action: 'update', available: item.available }
				]
			});
			await reloadSelectedOrderDetails();
		} catch (error: any) {
			alert('Artikel konnte nicht aktualisiert werden: ' + (error?.message || 'Unbekannter Fehler'));
			await reloadSelectedOrderDetails();
		}
	}

	function escapePocketBaseFilterValue(value: string) {
		return value.replace(/\\/g, '\\\\').replace(/"/g, '\\"');
	}

	async function fetchWarehouseItemOptions() {
		const requestSerial = ++warehouseItemRequestSerial;
		warehouseItemResultsLoading = true;

		try {
			const filterParts: string[] = [];
			const cleanSearch = warehouseItemSearch.trim();

			if (cleanSearch) {
				const escapedSearch = escapePocketBaseFilterValue(cleanSearch);
				filterParts.push(
					`(name ~ "${escapedSearch}" || description ~ "${escapedSearch}" || id = "${escapedSearch}")`
				);
			}

			if (warehouseItemCategory !== 'all') {
				const escapedCategory = escapePocketBaseFilterValue(warehouseItemCategory);
				filterParts.push(`category = "${escapedCategory}"`);
			}

			const result = await db.collection('stock').getList(
				warehouseItemPage,
				warehouseItemPerPage,
				{
					fields: 'id,name,description,price,max,euroPrice,category',
					filter: filterParts.join(' && '),
					sort: '+category,+name',
					requestKey: null
				}
			);

			// A newer search may have completed while this request was running.
			if (requestSerial !== warehouseItemRequestSerial) return;

			warehouseItemResults = result.items;
			warehouseItemTotalPages = Math.max(1, result.totalPages);
			warehouseItemTotalItems = result.totalItems;

			if (warehouseItemPage > warehouseItemTotalPages) {
				warehouseItemPage = warehouseItemTotalPages;
				await fetchWarehouseItemOptions();
			}
		} catch (error) {
			if (requestSerial !== warehouseItemRequestSerial) return;

			console.error('Artikel konnten nicht geladen werden:', error);
			warehouseItemResults = [];
			warehouseItemTotalPages = 1;
			warehouseItemTotalItems = 0;
		} finally {
			if (requestSerial === warehouseItemRequestSerial) {
				warehouseItemResultsLoading = false;
			}
		}
	}

	async function openWarehouseItemSelectModal() {
		warehouseItemSearch = '';
		warehouseItemCategory = 'all';
		warehouseItemPage = 1;
		warehouseItemSelectModal.showModal();
		await fetchWarehouseItemOptions();
	}

	function scheduleWarehouseItemSearch() {
		warehouseItemPage = 1;

		if (warehouseItemSearchTimer) {
			clearTimeout(warehouseItemSearchTimer);
		}

		warehouseItemSearchTimer = setTimeout(() => {
			fetchWarehouseItemOptions();
		}, 250);
	}

	async function changeWarehouseItemCategory(category: string) {
		warehouseItemCategory = category;
		warehouseItemPage = 1;
		await fetchWarehouseItemOptions();
	}

	async function changeWarehouseItemPage(targetPage: number) {
		if (
			targetPage < 1 ||
			targetPage > warehouseItemTotalPages ||
			targetPage === warehouseItemPage ||
			warehouseItemResultsLoading
		) {
			return;
		}

		warehouseItemPage = targetPage;
		await fetchWarehouseItemOptions();
	}

	function selectWarehouseProduct(product: any) {
		selectedWarehouseProduct = product;
		warehouseItemID = product.id;
		warehouseItemSelectModal.close();
	}

	async function addWarehouseItem() {
		if (!selectedOrder?.id || !warehouseItemID || warehouseItemAmount <= 0) {
			alert('Bitte wählen Sie einen Artikel und eine gültige Menge aus.');
			return;
		}

		warehouseItemSaving = true;
		try {
			await sendWarehouseOrderUpdate({
				id: selectedOrder.id,
				items: [
					{
						action: 'create',
						item: warehouseItemID,
						amount: Math.trunc(warehouseItemAmount)
					}
				]
			});
			warehouseItemID = '';
			warehouseItemAmount = 1;
			selectedWarehouseProduct = null;
			await reloadSelectedOrderDetails();
		} catch (error: any) {
			alert('Artikel konnte nicht hinzugefügt werden: ' + (error?.message || 'Unbekannter Fehler'));
		} finally {
			warehouseItemSaving = false;
		}
	}

	async function deleteWarehouseItem(itemId: string) {
		if (!confirm('Diesen vom Warenlager hinzugefügten Artikel wirklich entfernen?')) return;

		try {
			await sendWarehouseOrderUpdate({
				id: selectedOrder.id,
				items: [{ id: itemId, action: 'delete' }]
			});
			await reloadSelectedOrderDetails();
		} catch (error: any) {
			alert('Artikel konnte nicht entfernt werden: ' + (error?.message || 'Unbekannter Fehler'));
		}
	}

	async function saveOrderDetailsChanges() {
		if (!selectedOrder?.id || (!extrasDirty && !extraCostDirty)) return true;

		const payload: Record<string, any> = { id: selectedOrder.id };

		if (extrasDirty) {
			payload.extras = selectedOrder.extras ?? '';
		}

		if (extraCostDirty) {
			payload.extraCost = Number(selectedOrder.extraCost ?? 0);
			payload.extraEuroCost = Number(selectedOrder.extraEuroCost ?? 0);
		}

		orderMetaSaving = true;
		try {
			await sendWarehouseOrderUpdate(payload);
			extrasDirty = false;
			extraCostDirty = false;
			await reloadSelectedOrderDetails();
			return true;
		} catch (error: any) {
			alert('Änderungen konnten nicht gespeichert werden: ' + (error?.message || 'Unbekannter Fehler'));
			return false;
		} finally {
			orderMetaSaving = false;
		}
	}

	async function switchTab(tab: 'all' | 'requested' | 'approved' | 'collectable') {
		page = 1;
		activeTab = tab;
		await loadActiveTab();
	}

	async function prevPage() {
		if (page <= 1) return;
		page -= 1;
		await loadActiveTab();
	}

	async function nextPage() {
		if (page >= totalPages) return;
		page += 1;
		await loadActiveTab();
	}

	//#region Stock Management
	async function fetchStock() {
		const result = await db.collection('stock').getFullList({
			fields: 'id,name,description,price,max,euroPrice,category'
		});
		stock = new Set(result);
	}

	function openstockModal() {
		stockModal.showModal();
	}

	function openCreateProductModal() {
		createProductName = '';
		createProductPrice = 0;
		createProductDescription = '';
		createProductMax = -1;
		createProductEuroPrice = 0;
		createProductCategory = '';
		createProductModal.showModal();
	}

	function openEditProductModal(product: any) {
		editProductName = product.name;
		editProductPrice = product.price;
		editProductDescription = product.description;
		editProductMax = product.max;
		editProductID = product.id;
		editProductEuroPrice = product.euroPrice;
		editProductCategory = product.category || '';
		editProductModal.showModal();
	}

	async function saveEditedProduct(product: any) {
		if (!editProductName || editProductPrice <= 0) {
			alert('Ungültige Produktdaten!');
			return;
		}
		await db.collection('stock').update(product.id, {
			name: editProductName,
			price: editProductPrice,
			euroPrice: editProductEuroPrice,
			description: editProductDescription,
			max: editProductMax,
			category: editProductCategory
		});
		await fetchStock();
		editProductModal.close();
	}

	async function createProduct() {
		if (!createProductName || createProductPrice <= 0) {
			alert('Ungültige Produktdaten!');
			return;
		}
		await db
			.collection('stock')
			.create({
				name: createProductName,
				price: createProductPrice,
				euroPrice: createProductEuroPrice,
				description: createProductDescription,
				max: createProductMax,
				category: createProductCategory
			})
			.catch((error) => {
				console.log(error);
				console.log(error.data.data);
				alert('Fehler beim Erstellen des Produkts: ' + error.message);
			});
		await fetchStock();
		createProductModal.close();
	}

	async function deleteProduct(product: any) {
		if (!confirm(`Möchten Sie das Produkt "${product.name}" wirklich löschen?`)) {
			return;
		}
		await db.collection('stock').delete(product.id);
		await fetchStock();
	}

	// Update the product in the db
	function updateProduct() {
		if (!editProductName || editProductPrice <= 0) {
			alert('Ungültige Produktdaten!');
			return;
		}
		db.collection('stock')
			.update(editProductID, {
				name: editProductName,
				price: editProductPrice,
				euroPrice: editProductEuroPrice,
				description: editProductDescription,
				max: editProductMax,
				category: editProductCategory
			})
			.then(() => {
				editProductModal.close();
				fetchStock();
			})
			.catch((error) => {
				console.log(error);
				console.log(error.data.data);
				alert('Fehler beim Aktualisieren des Produkts: ' + error.message);
			});
	}
	async function onOrderModalClose() {
		await saveOrderDetailsChanges();
	}
	function openUserManagementModal() {
		fetchUserData();
		userManagementModal.showModal();
	}
	async function fetchUserData() {
		const result = await db.collection('warehouse_user_data').getList(userManagementPage, userManagementPerPage, {
			filter: `name ~ "${userManagementFilter}" || companyName ~ "${userManagementFilter}"`,
			requestKey: null
		});
		userManagementUsers = result.items;
		userManagementTotalPages = result.totalPages;
	}
	function editUser(user: any) {
		userManagementSelectedUser = user;
		editUserEuroLimit = user.euroAvailable;
		editUserEuroSpent = user.euroSpent;
		userManagementEditModal.showModal();
	}
	async function updateUserEuroLimit() {
		await db.collection("user_data").update(userManagementSelectedUser.id, {
			euroAvailable: editUserEuroLimit,
			euroSpent: editUserEuroSpent
		});
		userManagementEditModal.close();
		fetchUserData();
	}

	$: productsForModal = Array.from(stock)
		.filter((p) => p.name.toLowerCase().includes(productModalFiler.toLowerCase()))
		.slice((productModalPage - 1) * productModalPerPage, productModalPage * productModalPerPage);

	$: productModalPageCount = Math.ceil(
		Array.from(stock).filter((p) => p.name.toLowerCase().includes(productModalFiler.toLowerCase()))
			.length / productModalPerPage
	);

	//#endregion
</script>

<title>Logistik</title>

<Site>
	<div class="mx-auto flex max-w-7xl flex-col gap-4 p-4 md:p-5">
		<!-- Header -->
		<div class="grid grid-cols-1 lg:grid-cols-2 items-center justify-between gap-4">
			<div class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
				<div class="flex items-center gap-3">
				<div class="rounded-xl bg-primary/10 p-2.5 text-primary">
					<ClipboardList size={20} />
				</div>
				<div>
					<h1 class="text-lg font-bold leading-tight">Logistik</h1>
					<p class="text-xs text-base-content/50">Bestellungen verwalten</p>
				</div>
				</div>
			</div>
			
			<div class="flex flex-col sm:flex-row gap-2 justify-end">
				<button class="btn btn-primary btn-outline w-full sm:w-auto" on:click={openstockModal}>
				<Receipt size={18} />
				<Pencil size={18} />
				Produkte verwalten
				</button>
				<button class="btn btn-secondary btn-outline w-full sm:w-auto" on:click={openUserManagementModal}>
				<Euro size={18} />
				<Pencil size={18} />
				Eurolimits verwalten
				</button>
			</div>
		</div>


		<!-- Pipeline Status Bar -->
		<div class="grid grid-cols-3 gap-2 sm:gap-3">
			<button
				type="button"
				class="group rounded-xl border p-2.5 sm:p-3 text-left transition-all duration-150
					{activeTab === 'requested'
					? 'border-warning/40 bg-warning/10'
					: 'border-base-300 bg-base-100 hover:border-warning/30 hover:bg-warning/5'}"
				on:click={() => switchTab('requested')}
			>
				<div class="flex items-start justify-between gap-1 sm:gap-2">
					<div class="flex flex-col gap-0.5 min-w-0">
						<span
							class="text-[10px] sm:text-[11px] font-medium uppercase tracking-wide text-base-content/50 truncate"
							>Anfragen</span
						>
						<span class="text-xl sm:text-2xl font-bold text-warning leading-none"
							>{countRequested}</span
						>
					</div>
					<div class="mt-0.5 rounded-lg bg-warning/10 p-1 sm:p-1.5 text-warning shrink-0">
						<BadgeQuestionMark size={14} />
					</div>
				</div>
				<p class="mt-1 sm:mt-1.5 text-[10px] sm:text-[11px] text-base-content/40 hidden sm:block">
					Warten auf Freigabe
				</p>
			</button>

			<button
				type="button"
				class="group rounded-xl border p-2.5 sm:p-3 text-left transition-all duration-150
					{activeTab === 'approved'
					? 'border-primary/40 bg-primary/10'
					: 'border-base-300 bg-base-100 hover:border-primary/30 hover:bg-primary/5'}"
				on:click={() => switchTab('approved')}
			>
				<div class="flex items-start justify-between gap-1 sm:gap-2">
					<div class="flex flex-col gap-0.5 min-w-0">
						<span
							class="text-[10px] sm:text-[11px] font-medium uppercase tracking-wide text-base-content/50 truncate"
							>Akzeptiert</span
						>
						<span class="text-xl sm:text-2xl font-bold text-primary leading-none"
							>{countApproved}</span
						>
					</div>
					<div class="mt-0.5 rounded-lg bg-primary/10 p-1 sm:p-1.5 text-primary shrink-0">
						<PackageCheck size={14} />
					</div>
				</div>
				<p class="mt-1 sm:mt-1.5 text-[10px] sm:text-[11px] text-base-content/40 hidden sm:block">
					Werden vorbereitet
				</p>
			</button>

			<button
				type="button"
				class="group rounded-xl border p-2.5 sm:p-3 text-left transition-all duration-150
					{activeTab === 'collectable'
					? 'border-secondary/40 bg-secondary/10'
					: 'border-base-300 bg-base-100 hover:border-secondary/30 hover:bg-secondary/5'}"
				on:click={() => switchTab('collectable')}
			>
				<div class="flex items-start justify-between gap-1 sm:gap-2">
					<div class="flex flex-col gap-0.5 min-w-0">
						<span
							class="text-[10px] sm:text-[11px] font-medium uppercase tracking-wide text-base-content/50 truncate"
							>Abholbereit</span
						>
						<span class="text-xl sm:text-2xl font-bold text-secondary leading-none"
							>{countCollectable}</span
						>
					</div>
					<div class="mt-0.5 rounded-lg bg-secondary/10 p-1 sm:p-1.5 text-secondary shrink-0">
						<Truck size={14} />
					</div>
				</div>
				<p class="mt-1 sm:mt-1.5 text-[10px] sm:text-[11px] text-base-content/40 hidden sm:block">
					Warten auf Abholung
				</p>
			</button>
		</div>

		<!-- Main Content Card -->
		<div class="rounded-2xl border border-base-300 bg-base-100 shadow-sm">
			<!-- Card Tabs + Actions -->
			<div class="border-b border-base-300">
				<div class="flex flex-row">
					<div class="flex items-center gap-2 px-3 py-2 justify-between w-full">
						<input type="text" placeholder="Suche nach Firmennamen" class="input input-sm input-bordered w-full" bind:value={orderFilter} on:input={() => (loadActiveTab())} />
						<select class="select select-sm select-bordered w-32" on:change={(e) => {perPage = parseInt((e.target as HTMLSelectElement).value); loadActiveTab();}}>
							<option value="10">10 / Seite</option>
							<option value="20">20 / Seite</option>
							<option value="30" selected>30 / Seite</option>
							<option value="50">50 / Seite</option>
							<option value="100">100 / Seite</option>
							<option value="200">200 / Seite</option>
							<option value="500">500 / Seite</option>
						</select>
					</div>
				</div>
				<!-- Tab row -->
				<div class="flex flex-row justify-between">
					<div class="flex items-end overflow-x-auto px-2 pt-2 gap-0.5 scrollbar-none ">
						{#each [{ key: 'all', label: 'Alle', icon: List }, { key: 'requested', label: 'Anfragen', icon: BadgeQuestionMark, count: countRequested }, { key: 'approved', label: 'Zusammenfassung', icon: PackageCheck, count: countApproved }, { key: 'collectable', label: 'Abholungen', icon: Truck, count: countCollectable }] as t}
							<button
								type="button"
								role="tab"
								class="relative flex shrink-0 items-center gap-1.5 rounded-t-lg px-3 py-2.5 text-sm transition-colors duration-150
									{activeTab === t.key
									? 'border-b-2 border-primary bg-primary/5 font-semibold text-primary'
									: 'text-base-content/60 hover:text-base-content hover:bg-base-200'}"
								on:click={() => switchTab(t.key as any)}
							>
								<svelte:component this={t.icon} size={14} />
								<span class="hidden sm:inline">{t.label}</span>
								{#if t.count && t.count > 0}
									<span
										class="rounded-full bg-primary text-primary-content text-[10px] font-bold px-1.5 py-0 leading-4 min-w-4.5 text-center"
									>
										{t.count}
									</span>
								{/if}
							</button>
						{/each}
					</div>
				</div>

				<!-- Bulk Actions -->
				{#if (activeTab === 'requested' && countRequested > 0) || (activeTab === 'approved' && countApproved > 0)}
					<div
						class="flex flex-wrap items-center gap-2 px-3 py-2 border-t border-base-300 bg-base-200/50"
					>
						{#if activeTab === 'requested' && countRequested > 0}
							<button
								type="button"
								class="btn btn-sm btn-success gap-1.5"
								on:click={approveAllOrders}
							>
								<CheckCheck size={14} />
								Alle akzeptieren
							</button>
						{/if}
						{#if activeTab === 'approved' && countApproved > 0}
							<button
								type="button"
								class="btn btn-sm btn-outline btn-secondary gap-1.5"
								on:click={printShoppingList}
							>
								<Printer size={14} />
								Einkaufsliste
							</button>
							<button
								type="button"
								class="btn btn-sm btn-secondary gap-1.5"
								on:click={markAllOrdersCollectable}
							>
								<PackagePlus size={14} />
								Alle angekommen
							</button>
						{/if}
					</div>
				{/if}
			</div>

			<!-- Table or Empty -->
			<div class="p-4">
				{#if loading}
					<div class="flex items-center justify-center py-14">
						<span class="loading loading-spinner loading-md text-primary"></span>
					</div>
				{:else if currentOrders.length === 0}
					<div
						class="flex flex-col items-center justify-center gap-2 rounded-xl border border-dashed border-base-300 bg-base-200/50 py-14 text-center"
					>
						<PackageCheck size={28} class="text-base-content/25" />
						<p class="text-sm font-medium text-base-content/40">
							Keine Bestellungen in dieser Ansicht
						</p>
					</div>
				{:else}
					<div class="overflow-x-auto rounded-xl border border-base-300">
						<table class="table table-sm">
							<thead class="bg-base-200 text-xs uppercase tracking-wide text-base-content/50">
								<tr>
									{#if activeTab === 'collectable' || activeTab === 'all'}
										<th class="hidden md:table-cell w-28">Nr.</th>
									{/if}
									<th>Unternehmen</th>
									<th class="hidden sm:table-cell">Kosten</th>
									<th class="hidden sm:table-cell">Euro-Kosten</th>
									<th class="hidden md:table-cell">Datum</th>
									<th class="hidden sm:table-cell">Status</th>
									<th class="text-right">Aktionen</th>
								</tr>
							</thead>
							<tbody>
								{#each currentOrders as order}
									<tr class="group hover:bg-base-200/40 transition-colors duration-100">
										{#if activeTab === 'collectable' || activeTab === 'all'}
											<td class="hidden md:table-cell font-mono text-[11px] text-base-content/40"
												>{order.id.slice(0, 8)}…</td
											>
										{/if}

										<td>
											<div class="flex flex-col leading-tight">
												<span class="font-semibold text-sm">{order.companyName}</span>
												<span class="text-xs text-base-content/45">{order.userName}</span>
												<!-- Status only visible on mobile (hidden on sm+) -->
												<span
													class="mt-1 sm:hidden inline-flex w-fit items-center rounded-full border px-2 py-0.5 text-[10px] font-medium {getStatusColor(
														order.status
													)}"
												>
													{getStatusLabel(order.status)}
												</span>
											</div>
										</td>

										<td class="hidden sm:table-cell">
											<div class="flex items-center">
												<span class="font-semibold tabular-nums">{order.cost + order.extraCost}</span
												>
												<Currency />
											</div>
										</td>

										<td class="hidden sm:table-cell">
											<div class="flex items-center">
												<span class="font-semibold tabular-nums">{order.euroCost + order.extraEuroCost}</span
												>
												<Euro size={14} />
											</div>
										</td>

										<td class="hidden md:table-cell text-sm text-base-content/60 tabular-nums">
											{new Date(order.created).toLocaleDateString('de-DE', {
												day: '2-digit',
												month: '2-digit',
												year: '2-digit'
											})}
										</td>

										<td class="hidden sm:table-cell">
											<span
												class="inline-flex items-center rounded-full border px-2 py-0.5 text-[11px] font-medium {getStatusColor(
													order.status
												)}"
											>
												{getStatusLabel(order.status)}
											</span>
										</td>

										<td>
											<div class="flex items-center justify-end gap-1.5">
												<button
													type="button"
													class="btn btn-xs btn-info gap-1"
													title="Details anzeigen"
													on:click={() => showOrderDetails(order)}
												>
													<Info size={14} />
												</button>

												{#if order.status === 'requested'}
													<button
														type="button"
														class="btn btn-xs btn-success gap-1"
														title="Bestellung akzeptieren"
														on:click={() => acceptOrder(order.id)}
													>
														<Check size={13} />
														<span class="hidden sm:inline">Ok</span>
													</button>
													<button
														type="button"
														class="btn btn-xs btn-error btn-outline gap-1"
														title="Bestellung ablehnen"
														on:click={() => rejectOrder(order.id)}
													>
														<X size={13} />
													</button>
												{/if}

												{#if order.status === 'approved'}
													<button
														type="button"
														class="btn btn-xs btn-secondary gap-1"
														title="Bestellung angekommen"
														on:click={() => markOrderCollectable(order.id)}
													>
														<PackagePlus size={13} />
														<span class="hidden sm:inline">Angekommen</span>
													</button>
												{/if}

												{#if order.status === 'collectable'}
													<button
														type="button"
														class="btn btn-xs btn-success gap-1"
														title="Abgeholt & Zahlung autorisieren"
														on:click={() => markOrderCollected(order.id)}
													>
														<Check size={13} />
														<span class="hidden sm:inline">Abgeholt</span>
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

				<!-- Pagination -->
				{#if totalPages > 1}
					<div class="mt-4 flex items-center justify-between text-sm text-base-content/50">
						<span>{currentOrders.length} Einträge · Seite {page} / {totalPages}</span>
						<div class="join">
							<button class="join-item btn btn-sm" on:click={prevPage} disabled={page <= 1}>
								<ChevronLeft size={14} />
							</button>
							<button class="join-item btn btn-sm pointer-events-none">{page} / {totalPages}</button
							>
							<button
								class="join-item btn btn-sm"
								on:click={nextPage}
								disabled={page >= totalPages}
							>
								<ChevronRight size={14} />
							</button>
						</div>
					</div>
				{:else if !loading && currentOrders.length > 0}
					<p class="mt-3 text-center text-xs text-base-content/35">
						{currentOrders.length} Einträge
					</p>
				{/if}
			</div>
		</div>
	</div>

	<!-- Order Details Modal -->
	<dialog
		class="modal modal-bottom sm:modal-middle"
		bind:this={viewOrderModal}
		on:close={async () => {
			await onOrderModalClose();
			await refreshCounts();
			await loadActiveTab();
		}}
	>
		<div class="modal-box max-w-6xl p-0 overflow-hidden w-full">
			<!-- Modal Header -->
			<div
				class="flex items-center justify-between border-b border-base-300 bg-base-200 px-4 py-3 sm:px-6 sm:py-4"
			>
				<div class="min-w-0">
					<h3 class="text-base font-bold">Bestellungsdetails</h3>
					{#if selectedOrder}
						<p class="mt-0.5 font-mono text-[11px] text-base-content/45 truncate">
							{selectedOrder.id}
						</p>
					{/if}
				</div>
				<div class="flex items-center gap-2 shrink-0 ml-2">
					{#if selectedOrder}
						<span
							class="hidden sm:inline-flex items-center rounded-full border px-2.5 py-1 text-xs font-medium {getStatusColor(
								selectedOrder.status
							)}"
						>
							{getStatusLabel(selectedOrder.status)}
						</span>
					{/if}
					<button class="btn btn-sm btn-ghost btn-square" on:click={() => viewOrderModal.close()}>
						<X size={16} />
					</button>
				</div>
			</div>

			<div class="p-4 sm:p-6 space-y-4 sm:space-y-5 overflow-y-auto max-h-[70dvh]">
				<!-- Extra description -->
				{#if selectedOrder?.description}
					<div class="rounded-xl border border-base-300 bg-base-200 p-3 sm:p-4">
						<p class="mb-1.5 text-xs font-semibold uppercase tracking-wide text-base-content/50">
							Zusatz-Notiz
						</p>
						<p class="font-mono text-sm text-base-content/75 break-word">
							{selectedOrder.description}
						</p>
					</div>
				{/if}

				<!-- Items Table -->
				<div class="overflow-x-auto rounded-xl border border-base-300 -mx-1 sm:mx-0">
					<table class="table table-sm">
						<thead class="bg-base-200 text-xs uppercase tracking-wide text-base-content/50">
							<tr>
								<th>Artikel</th>
								<th class="hidden lg:table-cell">Beschreibung</th>
								<th class="text-center">Menge</th>
								<th class="text-center">Verfügbar</th>
								<th class="hidden sm:table-cell text-right">Preis/Stk.</th>
								<th class="hidden sm:table-cell text-right">Gesamt</th>
								<th class="hidden sm:table-cell text-right">Euro Preis/Stk.</th>
								<th class="hidden sm:table-cell text-right">Euro Gesamt</th>
								<th class="text-center">Aktion</th>
							</tr>
						</thead>
						<tbody>
							{#each selectedItems as item}
								<tr class="hover:bg-base-200/30">
									<td class="font-semibold text-sm max-w-36 sm:max-w-none">
										<div class="flex flex-col items-start gap-1">
											<span class="block truncate">{item.expand?.item?.name}</span>
											{#if item.warehouseAdded}
												<span class="badge badge-info badge-outline badge-xs">
													Vom Warenlager ergänzt
												</span>
											{/if}
										</div>
										<span class="sm:hidden block text-xs text-base-content/45 mt-0.5 tabular-nums">
											{(item.expand?.item?.price || 0) * (item.available || 0)}
											<Currency />
										</span>
									</td>
									<td class="hidden lg:table-cell max-w-48 text-xs text-base-content/55 truncate"
										>{@html item.expand?.item?.description ?? '—'}</td
									>
									<td class="text-center tabular-nums font-medium">{item.amount}</td>
									<td class="text-center">
										{#if selectedOrder?.status !== 'collected' && selectedOrder?.status !== 'rejected'}
											<input
												class="input input-sm input-bordered w-16 sm:w-20 text-center tabular-nums"
												type="number"
												min={item.warehouseAdded ? 1 : 0}
												step={1}
												max={item.warehouseAdded ? undefined : item.amount}
												value={Number(item.available)}
												on:input={(e) =>
													updateItemLocally(item.id, Number(e.currentTarget.value))}
												on:change={(e) =>
													updateItemAvailable(item.id, Number(e.currentTarget.value))}
												disabled={!item.warehouseAdded && selectedOrder?.status === 'requested'}
											/>
										{:else}
											<span class="tabular-nums font-medium">{item.available}</span>
										{/if}
										{#if item.warehouseAdded}
											<span class="block mt-1 text-[10px] text-info">mit Menge gekoppelt</span>
										{/if}
									</td>
									<td class="hidden sm:table-cell text-right tabular-nums text-sm">
										<div class="flex flex-row justify-end">
											{item.expand?.item?.price}
											<Currency />
										</div>
									</td>
									<td class="text-right tabular-nums text-sm text-base-content/60">
										<div class="flex flex-row justify-end">
											<span class="text-base-content"
												>{(item.expand?.item?.price || 0) * (item.amount || 0)}</span
											>
											<span class="mx-0.5 text-base-content/30">|</span>
											<p>{(item.expand?.item?.price || 0) * (item.available || 0)}</p>
											<Currency />
										</div>
									</td>
									<td class="hidden sm:table-cell text-right tabular-nums text-sm">
										<div class="flex flex-row justify-end">
											{item.expand?.item?.euroPrice}
											<Euro size={22} />
										</div>
									</td>
									<td class="text-right tabular-nums text-sm text-base-content/60">
										<div class="flex flex-row justify-end">
											<span class="text-base-content"
												>{(item.expand?.item?.euroPrice || 0) * (item.amount || 0)}</span
											>
											<span class="mx-0.5 text-base-content/30">|</span>
											<p>{(item.expand?.item?.euroPrice || 0) * (item.available || 0)}</p>
											<Euro size={22} />
										</div>
									</td>
									<td class="text-center">
										{#if item.warehouseAdded && selectedOrder?.status !== 'collected' && selectedOrder?.status !== 'rejected'}
											<button
												type="button"
												class="btn btn-xs btn-error btn-outline btn-square"
												title="Lagerartikel entfernen"
												on:click={() => deleteWarehouseItem(item.id)}
											>
												<Trash2 size={13} />
											</button>
										{:else}
											<span class="text-base-content/20">—</span>
										{/if}
									</td>
								</tr>
							{/each}
						</tbody>
					</table>
				</div>

				{#if selectedOrder?.status !== 'collected' && selectedOrder?.status !== 'rejected'}
					<div class="rounded-xl border border-info/25 bg-info/5 p-3 sm:p-4">
						<div class="mb-3">
							<h2 class="text-sm font-semibold">Zusätzlichen Artikel hinzufügen</h2>
							<p class="mt-0.5 text-xs text-base-content/50">
								Diese Position wird als vom Warenlager hinzugefügt markiert.
							</p>
						</div>
						<div class="grid grid-cols-1 gap-2 sm:grid-cols-[minmax(0,1fr)_7rem_auto]">
							<button
								type="button"
								class="btn btn-sm btn-outline h-auto min-h-8 justify-start px-3 py-2 text-left"
								on:click={openWarehouseItemSelectModal}
							>
								<Search size={15} class="shrink-0" />
								{#if selectedWarehouseProduct}
									<span class="min-w-0 flex-1">
										<span class="block truncate font-semibold">
											{selectedWarehouseProduct.name}
										</span>
										<span class="block truncate text-[11px] font-normal opacity-60">
											{getCategoryLabel(selectedWarehouseProduct.category)}
											· {selectedWarehouseProduct.price} Steine
											· {selectedWarehouseProduct.euroPrice ?? 0} €
										</span>
									</span>
								{:else}
									<span class="font-normal">Artikel suchen und auswählen …</span>
								{/if}
							</button>
							<input
								type="number"
								class="input input-sm input-bordered w-full"
								min={1}
								step={1}
								bind:value={warehouseItemAmount}
								aria-label="Menge"
							/>
							<button
								type="button"
								class="btn btn-sm btn-info"
								on:click={addWarehouseItem}
								disabled={warehouseItemSaving || !warehouseItemID || warehouseItemAmount <= 0}
							>
								{#if warehouseItemSaving}
									<span class="loading loading-spinner loading-xs"></span>
								{:else}
									<Plus size={15} />
								{/if}
								Hinzufügen
							</button>
						</div>
					</div>
				{/if}

				<div class="rounded-xl border border-base-300 bg-base-100 p-3 sm:p-4">
					<div class="mb-2 flex items-center justify-between gap-2">
						<div>
							<h2 class="text-sm font-semibold">Extrawünsche</h2>
							<p class="mt-0.5 text-xs text-base-content/50">
								Das Warenlager kann diesen Text ergänzen oder bearbeiten.
							</p>
						</div>
						<button
							type="button"
							class="btn btn-xs btn-primary"
							on:click={saveOrderDetailsChanges}
							disabled={orderMetaSaving || (!extrasDirty && !extraCostDirty)}
						>
							{#if orderMetaSaving}
								<span class="loading loading-spinner loading-xs"></span>
							{:else}
								<Check size={13} />
							{/if}
							Speichern
						</button>
					</div>
					<textarea
						class="textarea textarea-bordered min-h-24 w-full resize-y"
						value={selectedOrder?.extras ?? ''}
						on:input={(e) => {
							selectedOrder.extras = e.currentTarget.value;
							extrasDirty = true;
						}}
						placeholder="Extrawünsche oder Hinweise zur Bestellung …"
						disabled={selectedOrder?.status === 'collected' || selectedOrder?.status === 'rejected'}
					></textarea>
				</div>

				<div class="rounded-xl border border-base-300 bg-base-100 p-3 sm:p-4">
					<div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
						<label class="form-control">
							<span class="label-text mb-1 text-sm font-bold">Extra Kosten (Steine)</span>
							<input
								type="number"
								class="input input-sm input-bordered w-full"
								value={Number(selectedOrder?.extraCost ?? 0)}
								on:input={(e) => {
									selectedOrder.extraCost = Number(e.currentTarget.value);
									extraCostDirty = true;
								}}
								min={0}
								disabled={selectedOrder?.status === 'requested' || selectedOrder?.status === 'collected' || selectedOrder?.status === 'rejected'}
							/>
						</label>
						<label class="form-control">
							<span class="label-text mb-1 text-sm font-bold">Extra Kosten (Euro)</span>
							<input
								type="number"
								class="input input-sm input-bordered w-full"
								value={Number(selectedOrder?.extraEuroCost ?? 0)}
								on:input={(e) => {
									selectedOrder.extraEuroCost = Number(e.currentTarget.value);
									extraCostDirty = true;
								}}
								min={0}
								step="0.01"
								disabled={selectedOrder?.status === 'requested' || selectedOrder?.status === 'collected' || selectedOrder?.status === 'rejected'}
							/>
						</label>
					</div>
				</div>

				<div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
					<p class="text-xs text-base-content/40">
						Menge = Angefragt · Verfügbar = geliefert · Gesamt: ang. Preis | Preis verfügbar
					</p>
					<div class="flex items-center gap-1.5 text-sm font-bold">
						<span class="text-base-content/50 font-normal">Gesamt:</span>
						{(selectedOrder?.cost || 0) + (selectedOrder?.extraCost || 0)}
						<Currency />
						<span class="text-base-content/50 font-normal">Euro Gesamt:</span>
						{(selectedOrder?.euroCost || 0) + (selectedOrder?.extraEuroCost || 0)}
						<Euro />
						{#if selectedOrder?.status === 'requested'}
							<button
								type="button"
								class="btn btn-xs btn-success gap-1"
								title="Bestellung akzeptieren"
								on:click={() => acceptOrder(selectedOrder?.id)}
							>
								<Check size={13} />
								<span class="hidden sm:inline">Ok</span>
							</button>
							<button
								type="button"
								class="btn btn-xs btn-error btn-outline gap-1"
								title="Bestellung ablehnen"
								on:click={() => rejectOrder(selectedOrder?.id)}
							>
								<X size={13} />Ablehnen
							</button>
						{/if}

						{#if selectedOrder?.status === 'approved'}
							<button
								type="button"
								class="btn btn-xs btn-secondary gap-1"
								title="Bestellung angekommen"
								on:click={() => markOrderCollectable(selectedOrder?.id)}
							>
								<PackagePlus size={13} />
								<span class="hidden sm:inline">Angekommen</span>
							</button>
						{/if}

						{#if selectedOrder?.status === 'collectable'}
							<button
								type="button"
								class="btn btn-xs btn-success gap-1"
								title="Abgeholt & Zahlung autorisieren"
								on:click={() => markOrderCollected(selectedOrder?.id)}
							>
								<Check size={13} />
								<span class="hidden sm:inline">Abgeholt</span>
							</button>
						{/if}
					</div>
				</div>
			</div>
		</div>

		<form method="dialog" class="modal-backdrop">
			<button>close</button>
		</form>
	</dialog>


	<!-- Warehouse Item Select Modal -->
	<dialog
		id="warehouseItemSelectModal"
		class="modal"
		bind:this={warehouseItemSelectModal}
	>
		<div class="modal-box flex max-h-[88dvh] w-full max-w-5xl flex-col rounded-2xl border border-base-300 p-0">
			<header class="flex items-start justify-between gap-3 border-b border-base-300 bg-base-200 px-4 py-3 sm:px-6 sm:py-4">
				<div class="min-w-0">
					<h3 class="flex items-center gap-2 text-lg font-bold">
						<Search size={19} />
						Artikel auswählen
					</h3>
					<p class="mt-1 text-xs text-base-content/55">
						Die Artikelliste wird serverseitig durchsucht und seitenweise geladen.
					</p>
				</div>
				<button
					type="button"
					class="btn btn-ghost btn-sm btn-circle"
					on:click={() => warehouseItemSelectModal.close()}
				>
					<X size={18} />
				</button>
			</header>

			<div class="grid gap-2 border-b border-base-300 p-3 sm:grid-cols-[minmax(0,1fr)_15rem_8rem] sm:p-4">
				<label class="input input-sm input-bordered flex w-full items-center gap-2">
					<Search size={14} class="shrink-0 opacity-45" />
					<input
						type="search"
						class="min-w-0 grow"
						placeholder="Name, Beschreibung oder ID suchen …"
						bind:value={warehouseItemSearch}
						on:input={scheduleWarehouseItemSearch}
						on:keydown={(event) => {
							if (event.key === 'Enter') {
								event.preventDefault();
								if (warehouseItemSearchTimer) clearTimeout(warehouseItemSearchTimer);
								warehouseItemPage = 1;
								fetchWarehouseItemOptions();
							}
						}}
						autocomplete="off"
					/>
				</label>

				<select
					class="select select-sm select-bordered w-full"
					value={warehouseItemCategory}
					on:change={(event) => changeWarehouseItemCategory(event.currentTarget.value)}
				>
					<option value="all">Alle Kategorien</option>
					{#each categoryOptions as category}
						<option value={category.value}>{category.label}</option>
					{/each}
				</select>

				<select
					class="select select-sm select-bordered w-full"
					value={warehouseItemPerPage}
					on:change={async (event) => {
						warehouseItemPerPage = Number(event.currentTarget.value);
						warehouseItemPage = 1;
						await fetchWarehouseItemOptions();
					}}
				>
					<option value={10}>10 / Seite</option>
					<option value={20}>20 / Seite</option>
					<option value={30}>30 / Seite</option>
					<option value={50}>50 / Seite</option>
				</select>
			</div>

			<div class="min-h-0 flex-1 overflow-auto">
				{#if warehouseItemResultsLoading}
					<div class="flex min-h-64 items-center justify-center">
						<span class="loading loading-spinner loading-lg text-primary"></span>
					</div>
				{:else if warehouseItemResults.length === 0}
					<div class="flex min-h-64 flex-col items-center justify-center px-6 text-center">
						<Search size={34} class="mb-2 opacity-20" />
						<p class="font-semibold">Keine Artikel gefunden</p>
						<p class="mt-1 text-sm text-base-content/50">
							Ändere den Suchbegriff oder die ausgewählte Kategorie.
						</p>
					</div>
				{:else}
					<div class="overflow-x-auto">
						<table class="table table-sm">
							<thead class="sticky top-0 z-10 bg-base-200 text-xs uppercase tracking-wide text-base-content/50">
								<tr>
									<th>Artikel</th>
									<th>Kategorie</th>
									<th class="hidden md:table-cell">Beschreibung</th>
									<th class="text-right">Preis</th>
									<th class="text-right">Euro</th>
									<th class="text-right">Auswahl</th>
								</tr>
							</thead>
							<tbody>
								{#each warehouseItemResults as product}
									<tr
										class="cursor-pointer hover:bg-base-200/60 {warehouseItemID === product.id
											? 'bg-primary/10'
											: ''}"
										on:dblclick={() => selectWarehouseProduct(product)}
									>
										<td>
											<div class="max-w-64">
												<div class="truncate font-semibold">{product.name}</div>
												<div class="mt-0.5 truncate font-mono text-[10px] text-base-content/35">
													{product.id}
												</div>
											</div>
										</td>
										<td>
											<span class="badge badge-outline badge-sm whitespace-nowrap">
												{getCategoryLabel(product.category)}
											</span>
										</td>
										<td class="hidden max-w-80 md:table-cell">
											<div class="line-clamp-2 text-xs text-base-content/60">
												{@html product.description || '<span class="opacity-50">Keine Beschreibung</span>'}
											</div>
										</td>
										<td class="text-right">
											<span class="inline-flex items-center gap-0.5 whitespace-nowrap font-medium">
												{product.price}
												<Currency size={13} />
											</span>
										</td>
										<td class="text-right">
											<span class="inline-flex items-center gap-0.5 whitespace-nowrap">
												{Number(product.euroPrice ?? 0).toFixed(2)}
												<Euro size={13} />
											</span>
										</td>
										<td class="text-right">
											<button
												type="button"
												class="btn btn-sm btn-primary"
												on:click={() => selectWarehouseProduct(product)}
											>
												<Check size={14} />
												Auswählen
											</button>
										</td>
									</tr>
								{/each}
							</tbody>
						</table>
					</div>
				{/if}
			</div>

			<footer class="flex flex-col gap-3 border-t border-base-300 bg-base-100 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
				<div class="text-xs text-base-content/55">
					{warehouseItemTotalItems} Artikel · Seite {warehouseItemPage} von
					{warehouseItemTotalPages}
				</div>
				<div class="join">
					<button
						type="button"
						class="btn btn-sm join-item"
						on:click={() => changeWarehouseItemPage(warehouseItemPage - 1)}
						disabled={warehouseItemPage <= 1 || warehouseItemResultsLoading}
					>
						<ChevronLeft size={15} />
						Zurück
					</button>
					<button type="button" class="btn btn-sm join-item pointer-events-none">
						{warehouseItemPage}
					</button>
					<button
						type="button"
						class="btn btn-sm join-item"
						on:click={() => changeWarehouseItemPage(warehouseItemPage + 1)}
						disabled={warehouseItemPage >= warehouseItemTotalPages || warehouseItemResultsLoading}
					>
						Weiter
						<ChevronRight size={15} />
					</button>
				</div>
			</footer>
		</div>

		<form method="dialog" class="modal-backdrop">
			<button>close</button>
		</form>
	</dialog>

	<!-- Stock Modal -->
	<dialog id="stockModal" class="modal" bind:this={stockModal}>
		<div class="modal-box w-full max-w-5xl rounded-2xl border border-base-300">
			<header class="flex items-start justify-between gap-3 mb-2">
				<div>
					<h3 class="text-xl font-bold flex items-center gap-2">
						<Receipt size={20} />
						Lagerverwaltung
					</h3>
					<p class="text-sm opacity-70 mt-1">
						Hier kannst du die im Warenlager verfügbaren Produkte bearbeiten
					</p>
				</div>
				<button class="btn btn-ghost btn-sm btn-circle" on:click={() => stockModal.close()}>
					<X size={18} />
				</button>
			</header>

			<div class="flex flex-row items-center justify-between mb-4 gap-2">
			<input
				type="text"
				placeholder="Produkte suchen..."
				class="input input-bordered w-full rounded-lg"
				bind:value={productModalFiler}
			/>
			<select class="select select-bordered w-fit rounded-lg" bind:value={productModalPerPage}>
				<option value="5" selected>5 / Seite</option>
				<option value="10">10 / Seite</option>
				<option value="20">20 / Seite</option>
				<option value="30">30 / Seite</option>
				<option value="50">50 / Seite</option>
				<option value="100">100 / Seite</option>
				<option value="200">200 / Seite</option>
				<option value="500">500 / Seite</option>
			</select>
			</div>

			<!-- Stock management content goes here -->
			<div class="overflow-x-auto rounded-2xl border border-base-300">
				<table class="table table-zebra w-full">
					<thead>
						<tr>
							<th>Name</th>
							<th>Kategorie</th>
							<th>Beschreibung</th>
							<th>Preis</th>
							<th>Euro</th>
							<th>Maximum</th>
							<th>Aktionen</th>
						</tr>
					</thead>
					<tbody>
						{#each productsForModal as product}
							<tr>
								<td>{product.name}</td>
								<td>
									<span class="badge badge-outline badge-sm">{getCategoryLabel(product.category)}</span>
								</td>
								{#if product.description}
									<td>{@html product.description}</td>
								{:else}
									<td><span class="opacity-60 text-sm">Keine Beschreibung</span></td>
								{/if}
								<td
									><div class="flex flex-row items-center gap-1">
										{product.price}
										<Currency size={16} />
									</div></td
								>
								<td>
									<div class="flex items-center gap-0.5">
										{product?.euroPrice?.toFixed(2)}
										<Euro size={15} />
									</div>
								</td>
								<td>{product.max != -1 ? product.max : 'Unbegrenzt'}</td>
								<td>
									<button class="btn btn-sm" on:click={() => openEditProductModal(product)}>
										<Pencil size={16} />
									</button>
									<button class="btn btn-sm btn-error" on:click={() => deleteProduct(product)}>
										<Trash2 size={16} />
									</button>
								</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>

			<div class="flex w-full items-center justify-center mt-4">
				<div class="join">
					{#if productModalPage > 1}
						<button class="join-item btn" on:click={() => productModalPage--}>«</button>
					{:else}
						<button class="join-item btn" disabled>«</button>
					{/if}
					<button class="join-item btn">Seite {productModalPage}</button>
					{#if productModalPage < productModalPageCount}
						<button class="join-item btn" on:click={() => productModalPage++}>»</button>
					{:else}
						<button class="join-item btn" disabled>»</button>
					{/if}
				</div>
			</div>

			<div class="modal-action flex justify-between">
				<button class="btn btn-success" on:click={openCreateProductModal}>
					<Plus size={18} />
					Neues Produkt
				</button>
				<button class="btn btn-outline" on:click={() => stockModal.close()}>
					<X size={18} />
					Schließen
				</button>
			</div>
		</div>

		<form method="dialog" class="modal-backdrop">
			<button>close</button>
		</form>
	</dialog>

	<!-- Create Product Modal -->
	<dialog id="createProductModal" class="modal" bind:this={createProductModal}>
		<div class="modal-box w-full max-w-md rounded-2xl border border-base-300">
			<header class="flex items-start justify-between gap-3 mb-2">
				<div>
					<h3 class="text-xl font-bold flex items-center gap-2">
						<Plus size={20} />
						Neues Produkt erstellen
					</h3>
				</div>
				<button class="btn btn-ghost btn-sm btn-circle" on:click={() => createProductModal.close()}>
					<X size={18} />
				</button>
			</header>

			<div class="space-y-4">
				<div>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="block text-sm font-medium mb-1">Produktname</label>
					<input
						type="text"
						class="input input-bordered w-full rounded-lg"
						bind:value={createProductName}
						placeholder="z.B. Butter"
					/>
				</div>
				<div>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="block text-sm font-medium mb-1">Beschreibung</label>
					<input
						type="text"
						class="input input-bordered w-full rounded-lg"
						bind:value={createProductDescription}
						placeholder="z.B. 250g"
					/>
				</div>
				<div>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="block text-sm font-medium mb-1">Kategorie</label>
					<select class="select select-bordered w-full rounded-lg" bind:value={createProductCategory}>
						<option value="">Ohne Kategorie</option>
						{#each categoryOptions as category}
							<option value={category.value}>{category.label}</option>
						{/each}
					</select>
				</div>
				<div>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="block text-sm font-medium mb-1">Preis</label>
					<div class="flex flex-row gap-1">
						<input
							type="number"
							class="input input-bordered w-full rounded-lg"
							bind:value={createProductPrice}
							placeholder="z.B. 5"
							min="0"
							step="0.01"
						/>
						<Currency size={36}></Currency>
					</div>
				</div>
				<div>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="block text-sm font-medium mb-1">Euro Preis</label>
					<div class="flex flex-row gap-1">
						<input
							type="number"
							class="input input-bordered w-full rounded-lg"
							bind:value={createProductEuroPrice}
							placeholder="z.B. 5"
							min="0"
							step="0.01"
						/>
						<Euro size={40}></Euro>
					</div>
				</div>
				<div>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="block text-sm font-medium mb-1">Maximale Menge (-1 = Unbegrenzt)</label>
					<input
						type="number"
						class="input input-bordered w-full rounded-lg"
						bind:value={createProductMax}
						placeholder="z.B. 3"
						min="0"
					/>
				</div>
			</div>

			<div class="modal-action">
				<button class="btn btn-primary" on:click={createProduct}>
					<CheckCircle size={18} />
					Erstellen
				</button>
				<button class="btn btn-outline" on:click={() => createProductModal.close()}>
					<X size={18} />
					Abbrechen
				</button>
			</div>
		</div>

		<form method="dialog" class="modal-backdrop">
			<button>close</button>
		</form>
	</dialog>

	<!-- Edit Product Modal -->
	<dialog id="editProductModal" class="modal" bind:this={editProductModal}>
		<div class="modal-box w-full max-w-md rounded-2xl border border-base-300">
			<header class="flex items-start justify-between gap-3 mb-2">
				<div>
					<h3 class="text-xl font-bold flex items-center gap-2">
						<Pencil size={20} />
						Produkt bearbeiten
					</h3>
				</div>
				<button class="btn btn-ghost btn-sm btn-circle" on:click={() => editProductModal.close()}>
					<X size={18} />
				</button>
			</header>

			<div class="space-y-4">
				<div>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="block text-sm font-medium mb-1">Produktname</label>
					<input
						type="text"
						class="input input-bordered w-full rounded-lg"
						bind:value={editProductName}
						placeholder="z.B. Butter"
					/>
				</div>
				<div>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="block text-sm font-medium mb-1">Beschreibung</label>
					<input
						type="text"
						class="input input-bordered w-full rounded-lg"
						bind:value={editProductDescription}
						placeholder="z.B. 250g"
					/>
				</div>
				<div>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="block text-sm font-medium mb-1">Kategorie</label>
					<select class="select select-bordered w-full rounded-lg" bind:value={editProductCategory}>
						<option value="">Ohne Kategorie</option>
						{#each categoryOptions as category}
							<option value={category.value}>{category.label}</option>
						{/each}
					</select>
				</div>
				<div>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="block text-sm font-medium mb-1">Preis</label>
					<div class="flex flex-row gap-1">
						<input
							type="number"
							class="input input-bordered w-full rounded-lg"
							bind:value={editProductPrice}
							placeholder="z.B. 5"
							min="0"
							step="0.01"
						/>
						<Currency size={36}></Currency>
					</div>
				</div>
				<div>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="block text-sm font-medium mb-1">Euro Preis</label>
					<div class="flex flex-row gap-1">
						<input
							type="number"
							class="input input-bordered w-full rounded-lg"
							bind:value={editProductEuroPrice}
							placeholder="z.B. 5"
							min="0"
							step="0.01"
						/>
						<Euro size={40}></Euro>
					</div>
				</div>
				<div>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="block text-sm font-medium mb-1">Maximale Menge (-1 = Unbegrenzt)</label>
					<input
						type="number"
						class="input input-bordered w-full rounded-lg"
						bind:value={editProductMax}
						placeholder="z.B. 3"
						min="0"
					/>
				</div>
			</div>

			<div class="modal-action">
				<button class="btn btn-primary" on:click={updateProduct}>
					<CheckCircle size={18} />
					Speichern
				</button>
				<button class="btn btn-outline" on:click={() => editProductModal.close()}>
					<X size={18} />
					Abbrechen
				</button>
			</div>
		</div>

		<form method="dialog" class="modal-backdrop">
			<button>close</button>
		</form>
	</dialog>
	<!-- User Management -->
	<dialog id="userManagementModal" class="modal" bind:this={userManagementModal}>
		<div class="modal-box w-full max-w-3xl rounded-2xl border border-base-300">
			<header class="flex items-start justify-between gap-3 mb-2">
				<div>
					<h3 class="text-xl font-bold flex items-center gap-2">
						<User size={20} />
						Benutzerverwaltung
					</h3>
					<p class="text-sm opacity-70 mt-1">
						Hier kannst du die Euro Limits der Firmen verwalten
					</p>
				</div>
				<button class="btn btn-ghost btn-sm btn-circle" on:click={() => userManagementModal.close()}>
					<X size={18} />
				</button>
			</header>

			<div class="flex flex-row items-center gap-2 mb-2">
				<input type="text" placeholder="Suche nach Firmennamen" class="input input-sm input-bordered w-full rounded-2xl" bind:value={userManagementFilter} on:input={() => (fetchUserData())} />
				<select class="select select-sm select-bordered w-32 rounded-2xl" on:change={(e) => {userManagementPerPage = parseInt((e.target as HTMLSelectElement).value); fetchUserData();}}>
					<option value="10" selected>10 / Seite</option>
					<option value="20">20 / Seite</option>
					<option value="30">30 / Seite</option>
					<option value="50">50 / Seite</option>
					<option value="100">100 / Seite</option>
				</select>
			</div>

			<!-- User management content goes here -->
			<div class="overflow-x-auto rounded-2xl border border-base-300">
				<table class="table table-zebra w-full">
					<thead>
						<tr>
							<th>Name</th>
							<th>Euro Status</th>
							<th>Aktionen</th>
						</tr>
					</thead>
					<tbody>
						{#each userManagementUsers as user}
							<tr>
								<td>
									<div class="flex flex-col">
										<span class="font-semibold">{user.companyName}</span>
										<span class="text-xs text-base-content/45">{user.name}</span>
									</div>
								</td>
								<td>
									<div class="flex flex-row items-center gap-1">
										{user.euroSpent}
										<Euro size={15} />
										/
										{user.euroAvailable != -1 ? user.euroAvailable : '∞'}
										<Euro size={15} />
									</div>
								</td>
								<td>
									<button class="btn btn-sm" on:click={() => editUser(user)}>
										<Pencil size={16} />
									</button>
								</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>
			<div class="flex w-full items-center justify-center mt-4">
				<div class="join">
					<button class="join-item btn" on:click={() => {userManagementPage--; fetchUserData();}} disabled={userManagementPage <= 1}>«</button>
					<button class="join-item btn">Seite {userManagementPage}</button>
					<button class="join-item btn" on:click={() => {userManagementPage++; fetchUserData();}} disabled={userManagementPage >= userManagementTotalPages}>»</button>
				</div>
			</div>
		</div>
		<form method="dialog" class="modal-backdrop">
			<button>close</button>
		</form>
	</dialog>
	<!-- Edit Euro Modal -->
	<dialog id="editEuroModal" class="modal" bind:this={userManagementEditModal}>
		<div class="modal-box w-full max-w-md rounded-2xl border border-base-300">
			<header class="flex items-start justify-between gap-3 mb-2">
				<div>
					<h3 class="text-xl font-bold flex items-center gap-2">
						<Euro size={20} />
						Euro Limit bearbeiten
					</h3>
				</div>
				<button class="btn btn-ghost btn-sm btn-circle" on:click={() => userManagementEditModal.close()}>
					<X size={18} />
				</button>
			</header>

			<div class="space-y-4">
				<div>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="block text-sm font-medium mb-1">Neues Euro Limit</label>
					<input
						type="number"
						class="input input-bordered w-full rounded-lg"
						bind:value={editUserEuroLimit}
						min="-1"
						step="0.01"
					/>
					<p class="text-xs text-base-content/50 mt-1">Gib -1 ein, um ein unbegrenztes Limit zu setzen.</p>
					<p class="block text-sm font-medium mb-1 mt-4">Euro ausgeben</p>
					<input
						type="number"
						class="input input-bordered w-full rounded-lg"
						bind:value={editUserEuroSpent}
						min="0"
						step="0.01"
					/>
				</div>
			</div>

			<div class="modal-action">
				<button class="btn btn-primary" on:click={updateUserEuroLimit}>
					<CheckCircle size={18} />
					Speichern
				</button>
				<button class="btn btn-outline" on:click={() => userManagementEditModal.close()}>
					<X size={18} />
					Abbrechen
				</button>
			</div>
		</div>
		<form method="dialog" class="modal-backdrop">
			<button>close</button>
		</form>
	</dialog>
</Site>

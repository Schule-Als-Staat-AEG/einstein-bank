<script lang="ts">
	import { browser } from '$app/environment';
	import { getAuthStore } from '$lib/auth';
	import db from '$lib/connections';
	import { navigateToNotifications } from '$lib/notifications';
	import { getSetting } from '$lib/settings';
	import Site from '$modules/Site.svelte';
	import { onMount } from 'svelte';

	import {
		Bell,
		Search,
		CheckCheck,
		Trash2,
		Eye,
		EyeOff,
		ExternalLink,
		Clock,
		ChevronLeft,
		ChevronRight,
		Info
	} from '@lucide/svelte';

	let displayName: string;

	let page = 1;
	let perPage = 30;
	let pages = 1;

	let notifications: any[] = [];
	let selectedNotifications: Set<string> = new Set();

	let loading = true;
	let filter = '';

	function formatDate(dateString: string) {
		try {
			return new Date(dateString).toLocaleString();
		} catch (e) {
			return String(dateString || '');
		}
	}

	function isAllSelected() {
		return notifications.length > 0 && selectedNotifications.size === notifications.length;
	}

	onMount(async () => {
		if (!browser) return;
		loading = true;

		displayName = await getSetting('display_name');
		document.title = 'Benachrichtigungen - ' + displayName;

		await fetchNotifications();
		loading = false;
	});

	async function fetchNotifications() {
		loading = true;
		const result = await db.collection('notifications').getList(page, perPage, {
			sort: '+isRead,-created',
			filter: `user.id = "${getAuthStore().record.id}"`,
			requestKey: null
		});

		pages = result.totalPages;
		notifications = result.items || [];
		selectedNotifications = new Set(
			[...selectedNotifications].filter((id) => notifications.some((n) => n.id === id))
		);

		loading = false;
	}

	function toggleSelectNotification(id: string) {
		if (selectedNotifications.has(id)) selectedNotifications.delete(id);
		else selectedNotifications.add(id);
		selectedNotifications = new Set(selectedNotifications);
	}

	function toggleSelectAll() {
		if (isAllSelected()) selectedNotifications.clear();
		else notifications.forEach((n) => selectedNotifications.add(n.id));
		selectedNotifications = new Set(selectedNotifications);
	}

	function navigateTo(link: string) {
		navigateToNotifications(link);
	}

	async function deleteSelected() {
		if (!selectedNotifications.size) return;
		if (!confirm('Möchtest du die ausgewählten Benachrichtigungen wirklich löschen?')) return;

		await Promise.all(
			Array.from(selectedNotifications).map(async (id) => {
				await db.collection('notifications').delete(id, { requestKey: null });
			})
		);

		selectedNotifications = new Set();
		await fetchNotifications();
	}

	async function markSelectedAsRead() {
		if (!selectedNotifications.size) return;

		await Promise.all(
			Array.from(selectedNotifications).map(async (id) => {
				await db.collection('notifications').update(id, { isRead: true }, { requestKey: null });
			})
		);

		selectedNotifications = new Set();
		await fetchNotifications();
	}

	function markAsRead(id: string) {
		db.collection('notifications')
			.update(id, { isRead: true }, { requestKey: null })
			.then(() => fetchNotifications());
	}

	function markAsUnread(id: string) {
		db.collection('notifications')
			.update(id, { isRead: false }, { requestKey: null })
			.then(() => fetchNotifications());
	}

	function deleteNotification(id: string) {
		if (!confirm('Möchtest du diese Benachrichtigung wirklich löschen?')) return;
		db.collection('notifications')
			.delete(id, { requestKey: null })
			.then(() => fetchNotifications());
	}

	function goToPage(newPage: number) {
		if (newPage < 1 || newPage > pages) return;
		page = newPage;
		fetchNotifications();
	}

	// Lightweight local filter (UI-only) – DB filter kannst du später ergänzen, wenn du willst.
	$: visibleNotifications =
		filter.trim().length === 0
			? notifications
			: notifications.filter((n) => {
					const t = (n.title || '').toLowerCase();
					const c = (n.content || '').toLowerCase();
					const f = filter.toLowerCase();
					return t.includes(f) || c.includes(f);
				});

	$: unreadCount = notifications.filter((n) => !n.isRead).length;
</script>

<title>Benachrichtigungen</title>
<Site>
	<div class="flex flex-col gap-4">
		<!-- Header -->
		<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
			<div class="flex flex-col md:flex-row md:items-center md:justify-between gap-3">
				<div class="flex items-center gap-3">
					<div
						class="w-11 h-11 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center shadow"
					>
						<Bell size={20} />
					</div>
					<div class="flex flex-col">
						<h1 class="font-bold text-2xl leading-tight">Benachrichtigungen</h1>
						<p class="text-sm opacity-70 flex items-center gap-2">
							<Info size={16} />
							<span>{unreadCount} ungelesen • Seite {page} / {pages}</span>
						</p>
					</div>
				</div>

				<div class="flex flex-wrap items-center gap-2">
					<button
						class="btn btn-outline btn-error"
						on:click={deleteSelected}
						disabled={!selectedNotifications.size}
					>
						<Trash2 size={18} />
						Ausgewählte löschen
					</button>

					<button
						class="btn btn-primary btn-outline"
						on:click={markSelectedAsRead}
						disabled={!selectedNotifications.size}
					>
						<CheckCheck size={18} />
						Als gelesen markieren
					</button>
				</div>
			</div>

			<div class="mt-4 flex flex-col md:flex-row gap-2 md:items-center">
				<label class="input input-bordered w-full flex items-center gap-2 rounded-2xl">
					<Search size={18} class="opacity-60" />
					<input
						type="text"
						class="grow"
						placeholder="Suchen (Titel oder Inhalt)"
						bind:value={filter}
						autocomplete="off"
					/>
				</label>

				<div class="flex items-center gap-2">
					{#if loading}
						<span class="loading loading-spinner"></span>
					{/if}
					<span class="badge badge-ghost">
						Markiert: <span class="font-mono ml-1">{selectedNotifications.size}</span>
					</span>
				</div>
			</div>
		</div>

		<!-- Content -->
		{#if loading}
			<div
				class="rounded-2xl bg-base-200 border border-base-300 p-8 flex items-center justify-center"
			>
				<span class="loading loading-spinner loading-lg"></span>
			</div>
		{:else}
			{#if !visibleNotifications.length}
				<div class="rounded-2xl bg-base-200 border border-base-300 p-8 text-center shadow">
					<div class="font-bold text-lg">Keine Benachrichtigungen</div>
					<div class="opacity-70 mt-1">
						Aktuell gibt es hier nichts (oder dein Filter ist zu streng).
					</div>
				</div>
			{:else}
				<!-- Table wrapper -->
				<div class="rounded-2xl bg-base-200 border border-base-300 shadow overflow-hidden">
					<div class="overflow-x-auto">
						<table class="table">
							<thead>
								<tr>
									<th class="w-10">
										<label>
											<input
												type="checkbox"
												class="checkbox"
												on:change={toggleSelectAll}
												checked={isAllSelected()}
											/>
										</label>
									</th>
									<th>Titel</th>
									<th>Info</th>
									<th class="whitespace-nowrap">Zeit</th>
									<th class="whitespace-nowrap">Aktionen</th>
								</tr>
							</thead>

							<tbody>
								{#each visibleNotifications as n}
									<tr class="hover:bg-base-300/40">
										<td>
											<label>
												<input
													type="checkbox"
													class="checkbox"
													on:change={() => toggleSelectNotification(n.id)}
													checked={selectedNotifications.has(n.id)}
												/>
											</label>
										</td>

										<td class="max-w-88">
											<div class="flex items-center gap-2">
												{#if !n.isRead}
													<span class="badge badge-success badge-xs"></span>
												{:else}
													<span class="badge badge-ghost badge-xs"></span>
												{/if}

												<div class="min-w-0">
													<div>
														{#if !n.isRead}
															<strong>{n.title}</strong>
														{:else}
															{n.title}
														{/if}
													</div>
													<div class="text-xs opacity-60 font-mono truncate">#{n.id}</div>
												</div>
											</div>
										</td>

										<td class="max-w-xl">
											<div class="min-w-0">
												{#if !n.isRead}
													<strong class="block break-after-auto">{@html n.content}</strong>
												{:else}
													<div class="break-after-auto">{@html n.content}</div>
												{/if}
											</div>
										</td>

										<td class="whitespace-nowrap">
											<div class="flex items-center gap-2 opacity-80">
												<Clock size={16} />
												<span>{formatDate(n.created)}</span>
											</div>
										</td>

										<td class="whitespace-nowrap">
											<div class="flex items-center gap-2">
												{#if !n.isRead}
													<div class="tooltip" data-tip="Als gelesen markieren">
														<button class="btn btn-ghost btn-sm" on:click={() => markAsRead(n.id)}>
															<Eye size={18} />
														</button>
													</div>
												{:else}
													<div class="tooltip" data-tip="Als ungelesen markieren">
														<button
															class="btn btn-ghost btn-sm"
															on:click={() => markAsUnread(n.id)}
														>
															<EyeOff size={18} />
														</button>
													</div>
												{/if}

												<div class="tooltip" data-tip="Löschen">
													<button
														class="btn btn-ghost btn-sm btn-error"
														on:click={() => deleteNotification(n.id)}
													>
														<Trash2 size={18} />
													</button>
												</div>

												<div class="tooltip" data-tip="Öffnen">
													<button
														class="btn btn-ghost btn-sm btn-primary"
														on:click={() => {
															markAsRead(n.id);
															navigateTo(n.link);
														}}
													>
														<ExternalLink size={18} />
													</button>
												</div>
											</div>
										</td>
									</tr>
								{/each}
							</tbody>
						</table>
					</div>

					<!-- Pagination -->
					<div class="p-4 flex justify-center">
						<div class="join">
							<button
								class="join-item btn"
								on:click={() => goToPage(page - 1)}
								disabled={page <= 1}
							>
								<ChevronLeft size={18} />
							</button>
							<button class="join-item btn btn-ghost">
								Seite <span class="font-mono mx-1">{page}</span> /
								<span class="font-mono">{pages}</span>
							</button>
							<button
								class="join-item btn"
								on:click={() => goToPage(page + 1)}
								disabled={page >= pages}
							>
								<ChevronRight size={18} />
							</button>
						</div>
					</div>
				</div>
			{/if}
		{/if}
	</div>
</Site>

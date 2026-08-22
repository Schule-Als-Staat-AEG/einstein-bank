<script lang="ts">
	import {
		ShelvingUnit,
		DoorOpenIcon,
		ServerIcon,
		ArrowLeftRightIcon,
		Building2Icon,
		CreditCardIcon,
		HomeIcon,
		MenuIcon,
		QrCodeIcon,
		UserIcon,
		XIcon,
		BellIcon,
		BookMarkedIcon,
		HandCoinsIcon,
		SignatureIcon,
		RefreshCcwIcon,
		ShieldUserIcon,
		PackageIcon,
		ScrollText,
		Settings,
		LifeBuoy,
		FileQuestionMark,
		Forklift,
		Archive,

		Users

	} from '@lucide/svelte';
	import '../routes/layout.css';
	import { onMount, onDestroy } from 'svelte';
	import db from '$lib/connections';
	import { browser } from '$app/environment';
	import { marked } from 'marked';
	import { goto } from '$app/navigation';
	import { get } from 'svelte/store';
	import { getFile, getSetting } from '$lib/settings';
	import {
		_superuserAuthenticateAs,
		_superuserSetAuth,
		checkAuthRefresh,
		getAuthStore,
		isSuperuser,
		logout
	} from '$lib/auth';
	import FirstRunModal from './modals/FirstRunModal.svelte';

	// Responsive sidebar: visible on desktop, expandable on small screens
	export let groups = [
		{
			label: 'Übersicht',
			items: [{ label: 'Hauptmenü', href: '/admin', icon: HomeIcon }]
		},
		{
			label: 'Benutzer & Konten',
			items: [
				{ label: 'Benutzer', href: '/admin/users', icon: UserIcon },
				{ label: 'Konten', href: '/admin/accounts', icon: CreditCardIcon },
				{ label: 'Karten', href: '/admin/cards', icon: QrCodeIcon },
				{ label: 'Gruppen', href: '/admin/groups', icon: Users },
				{ label: 'Signatur auslesen', href: '/admin/signatures', icon: SignatureIcon }
			]
		},
		{
			label: 'Finanzen & Transaktionen',
			items: [
				{ label: 'Transaktionen', href: '/admin/transactions', icon: ArrowLeftRightIcon },
				{ label: 'Wiederkehrende Transakt.', href: '/admin/recurring', icon: RefreshCcwIcon },
				{ label: 'Steuer-Logs', href: '/admin/tax-reports', icon: ScrollText },
				{ label: 'Steuer-Fälle', href: '/admin/finance-tracking', icon: Archive },
				{ label: 'Verträge', href: '/admin/contracts', icon: BookMarkedIcon }
			]
		},
		{
			label: 'Produkte & Lager',
			items: [
				{ label: 'Produkte', href: '/admin/products', icon: PackageIcon },
				{ label: 'Lager-Produkte', href: '/admin/stock', icon: ShelvingUnit },
				{ label: 'Bestellungen', href: '/admin/orders', icon: Forklift }
			]
		},
		{
			label: 'Support & Kommunikation',
			items: [
				{ label: 'Benachrichtigungen', href: '/admin/notifications', icon: BellIcon },
				{ label: 'Hilfecenter', href: '/admin/help', icon: FileQuestionMark },
				{ label: 'Support-Tickets', href: '/admin/tickets', icon: LifeBuoy }
			]
		},
		{
			label: 'Administration',
			admin: true,
			items: [
				{ label: 'Setup', href: '/admin/settings', icon: Settings },
				{ label: 'Server', href: '/admin/server', icon: ServerIcon }
			]
		}
	];

	export let drawerOpen: boolean = false; /* bound to drawer checkbox */
	let showNotifications: boolean = false;
	let hasNewNotifications: boolean = false; // Set to true to show blue dot
	let isLoggedIn: boolean = false;
	let isAdmin: boolean = false;
	let hasAvatar: boolean = false;
	let avatarUrl: string = '';
	let username: string = '';
	let displayName: string = 'Einstein Bank';

	let notifications: any[] = [];

	let logo: string = '';
	let logoLight: string = '';

	let impersonationModal: HTMLDialogElement;
	let impersonationFilter: string = '';
	let impersonationNameFilter: string = '';
	let impersonationEmailFilter: string = '';
	let impersonationCompanyFilter: string = '';
	let impersonationIdFilter: string = '';
	let impersonationAccountType: 'all' | 'private' | 'company' = 'all';
	let impersonationVerification: 'all' | 'verified' | 'unverified' = 'all';
	let impersonationSort: string = '+name';
	let impersonationPerPage: number = 25;
	let impersonationSelectedUser: any = null;
	let impersonationUsers: any[] = [];
	let impersonationPage: number = 1;
	let impersonationPages: number = 1;
	let impersonationTotalItems: number = 0;
	let impersonationLoading: boolean = false;
	let impersonationFetchTimeout: ReturnType<typeof setTimeout> | undefined;

	// Settings Modal stuff
	let settingsModal: HTMLDialogElement;
	let oldPassword: string = '';
	let newPassword: string = '';
	let newPasswordConfirm: string = '';

	let theme: string = 'light';

	let companyName: string = '';

	let listenAdminTicketNotifications: boolean = false;
	let showNewTicket: boolean = false;

	let firstRunModal: FirstRunModal;

	function switchTheme() {
		if (!browser) return;
		const currentTheme = document.documentElement.dataset.theme || 'light';
		const newTheme = currentTheme === 'light' ? 'dark' : 'light';
		document.documentElement.dataset.theme = newTheme;
		localStorage.setItem('theme', newTheme);
	}
	onDestroy(() => {
		if (impersonationFetchTimeout) clearTimeout(impersonationFetchTimeout);
	});
	onMount(async () => {
		if (!browser) return;
		theme = localStorage.getItem('theme') || 'light';
		checkAuthRefresh();
		if (!db.authStore.isValid) {
			goto('/login');
			return;
		}
		logo = await getFile('logo');
		logoLight = await getFile('logo_dark'); // For Dark mode
		if (isSuperuser()) {
			isAdmin = true;
		} else {
			goto('/dashboard');
		}
		if (localStorage.getItem('adminticketnotifications') === 'true') {
			listenAdminTicketNotifications = true;
			db.collection('support_tickets').subscribe('*', (e) => {
				if (e.action === 'create' && e.record.status == 'open') {
					// Show message for 5 seconds
					showNewTicket = true;
					setTimeout(() => {
						showNewTicket = false;
					}, 5000);
				}
			});
		}
		impersonationSelectedUser = getAuthStore()?.record;
		companyName = getAuthStore()?.record.companyName || '';
		displayName = (await getSetting('display_name')) || 'Einstein Bank';
	});
	function Logout() {
		if (!browser) return;
		logout();
		goto('/login');
	}
	function markAsRead(notification) {
		if (!browser) return;
		if (!notification.read) {
			notification.read = true;
			// Here you would also send a request to the backend to mark it as read
		}
	}
	function onNotificationClick(destination) {
		if (!browser) return;
		if (destination) {
			goto(destination);
		}
	}
	function toPlainText(markdown) {
		if (!browser) return;
		const html = marked.parse(markdown || '');
		const tempDiv: any = document.createElement('div');
		tempDiv.innerHTML = html;
		return tempDiv.textContent || tempDiv.innerText || '';
	}
	function escapePocketBaseFilterValue(value: string) {
		return value.trim().replaceAll('\\', '\\\\').replaceAll('"', '\\"');
	}

	function buildImpersonationFilter() {
		const filters: string[] = [];
		const general = escapePocketBaseFilterValue(impersonationFilter);
		const name = escapePocketBaseFilterValue(impersonationNameFilter);
		const email = escapePocketBaseFilterValue(impersonationEmailFilter);
		const company = escapePocketBaseFilterValue(impersonationCompanyFilter);
		const id = escapePocketBaseFilterValue(impersonationIdFilter);

		if (general) {
			filters.push(
				`(name~"${general}" || email~"${general}" || companyName~"${general}" || id~"${general}")`
			);
		}
		if (name) filters.push(`name~"${name}"`);
		if (email) filters.push(`email~"${email}"`);
		if (company) filters.push(`companyName~"${company}"`);
		if (id) filters.push(`id="${id}"`);

		if (impersonationAccountType === 'company') filters.push('isCompany=true');
		if (impersonationAccountType === 'private') filters.push('isCompany=false');
		if (impersonationVerification === 'verified') filters.push('verified=true');
		if (impersonationVerification === 'unverified') filters.push('verified=false');

		return filters.join(' && ');
	}

	function scheduleImpersonationFetch(resetPage: boolean = true) {
		if (!browser) return;
		if (resetPage) impersonationPage = 1;

		if (impersonationFetchTimeout) clearTimeout(impersonationFetchTimeout);
		impersonationFetchTimeout = setTimeout(() => fetchImpersonationUsers(), 250);
	}

	function openImpersonationModal() {
		if (!browser) return;
		impersonationPage = 1;
		fetchImpersonationUsers();
		impersonationModal.showModal();
	}

	async function fetchImpersonationUsers() {
		if (!browser) return;
		impersonationLoading = true;

		try {
			const result = await db.collection('users').getList(
				impersonationPage,
				impersonationPerPage,
				{
					filter: buildImpersonationFilter() || undefined,
					sort: impersonationSort
				}
			);

			impersonationUsers = result.items;
			impersonationPages = Math.max(1, result.totalPages);
			impersonationTotalItems = result.totalItems;

			if (impersonationPage > impersonationPages) {
				impersonationPage = impersonationPages;
				await fetchImpersonationUsers();
			}
		} catch (error) {
			console.error('Fehler beim Laden der Nutzer:', error);
			impersonationUsers = [];
			impersonationPages = 1;
			impersonationTotalItems = 0;
		} finally {
			impersonationLoading = false;
		}
	}

	function resetImpersonationFilters() {
		impersonationFilter = '';
		impersonationNameFilter = '';
		impersonationEmailFilter = '';
		impersonationCompanyFilter = '';
		impersonationIdFilter = '';
		impersonationAccountType = 'all';
		impersonationVerification = 'all';
		impersonationSort = '+name';
		impersonationPerPage = 25;
		impersonationPage = 1;
		fetchImpersonationUsers();
	}
	function selectUserToImpersonate(user) {
		if (!browser) return;
		return () => {
			impersonationSelectedUser = user;
		};
	}
	async function impersonate(selectAsDefault: boolean = false) {
		if (!browser) return;
		await _superuserAuthenticateAs(impersonationSelectedUser.id);
		if (selectAsDefault) {
			await _superuserSetAuth(impersonationSelectedUser.id);
		}
		// Reload the page to reflect changes
		if (browser) location.reload();
	}
	function closeImpersonationModal() {
		if (!browser) return;
		impersonationSelectedUser = getAuthStore()?.record;
		impersonationModal.close();
	}
	function closeSettingsModal() {
		if (!browser) return;
		settingsModal.close();
	}
	function updatePassword() {
		if (!browser) return;
		db.collection('users')
			.update(
				getAuthStore()?.record.id,
				{
					oldPassword,
					password: newPassword,
					passwordConfirm: newPasswordConfirm
				},
				{ requestKey: null }
			)
			.then(() => {
				alert('Passwort erfolgreich geändert!');
				oldPassword = '';
				newPassword = '';
				newPasswordConfirm = '';
				settingsModal.close();
			})
			.catch((error) => {
				alert('Fehler beim Ändern des Passworts: ' + error.message);
			});
	}
	function toggleListenToTickets() {
		if (!browser) return;
		listenAdminTicketNotifications = !listenAdminTicketNotifications;
		localStorage.setItem('adminticketnotifications', listenAdminTicketNotifications.toString());
		if (listenAdminTicketNotifications) {
			db.collection('support_tickets').subscribe('*', (e) => {
				if (e.action === 'create' && e.record.status == 'open') {
					// Show message for 5 seconds
					showNewTicket = true;
					setTimeout(() => {
						showNewTicket = false;
					}, 5000);
				}
			});
		} else {
			db.collection('support_tickets').unsubscribe();
		}
	}
	$: passwordValid =
		newPassword.length >= 8 &&
		newPassword === newPasswordConfirm &&
		newPassword !== oldPassword &&
		oldPassword.length > 0;
</script>

<div class="sidebar bg-base-100">
	<div class="drawer lg:drawer-open min-h-screen">
		<input id="my-drawer-3" type="checkbox" class="drawer-toggle" bind:checked={drawerOpen} />
		<div class="drawer-content flex flex-col">
			<!-- Navbar -->
			<div class="navbar bg-base-100 border-b border-base-300 sticky top-0 z-30">
				<!-- Left: Mobile menu button -->
				<div class="flex-none lg:hidden mr-2">
					<label for="my-drawer-3" class="btn btn-square btn-ghost">
						<MenuIcon />
					</label>
				</div>

				<!-- Center: Title (optional, can be removed or customized) -->
				<div class="flex-1">
					<span class="text-xl font-bold lg:hidden">{displayName}</span>
				</div>

				<!-- Right: Notifications + Avatar -->
				<div class="flex-none gap-2">
					<!-- Notifications button with dropdown -->
					<div class="dropdown dropdown-end">
						<!-- svelte-ignore a11y_no_noninteractive_tabindex -->
						<label tabindex="0" class="btn btn-ghost btn-circle">
							<div class="indicator">
								<BellIcon class="w-5 h-5" />
								{#if hasNewNotifications}
									<span class="badge badge-xs badge-primary indicator-item"></span>
								{/if}
							</div>
						</label>
						<!-- svelte-ignore a11y_no_noninteractive_tabindex -->
						<div
							tabindex="0"
							class="dropdown-content card card-compact bg-base-100 z-50 mt-3 w-80 sm:w-80 md:w-100 lg:w-120 shadow-xl border border-base-300"
						>
							<div class="card-body">
								<h3 class="card-title">Benachrichtigungen</h3>
								<!-- Info: Add notification content here -->
								{#if notifications.length > 0}
									{#each notifications as notification}
										<!-- make the entire notification a button and include description -->
										<button
											class="w-full text-left border-b border-base-300 pb-2 mb-2 p-2 hover:bg-base-200 rounded-md"
											on:click={() => onNotificationClick(notification.destination)}
											on:mouseenter={() => markAsRead(notification)}
											on:touchstart={() => markAsRead(notification)}
										>
											<div class="flex justify-between items-start">
												<div class="font-semibold">{notification.title}</div>
												{#if !notification.read}
													<span class="badge badge-primary badge-sm">Neu</span>
												{/if}
											</div>
											<div class="text-sm text-base-content/70 mt-1">{notification.message}</div>
											{#if notification.description}
												<div class="text-sm text-base-content/60 mt-1">
													{toPlainText(notification.description)}
												</div>
											{/if}
											<div class="text-xs text-base-content/50 mt-2">
												{new Date(notification.created).toLocaleString()}
											</div>
										</button>
									{/each}
								{:else}
									<div class="text-sm text-base-content/70">Keine neuen Benachrichtigungen</div>
								{/if}
								<button
									class="btn btn-primary btn-sm mt-2 w-full"
									on:click={() => {
										if (browser) window.location.href = '/notifications';
									}}
								>
									Alle Benachrichtigungen anzeigen
								</button>
								<button class="btn btn-secondary btn-sm mt-2 w-full">
									Alle als gelesen markieren
								</button>
							</div>
						</div>
					</div>

					<!-- Avatar dropdown -->
					<div class="dropdown dropdown-end">
						<!-- svelte-ignore a11y_no_noninteractive_tabindex -->
						<label tabindex="0" class="btn btn-ghost btn-circle avatar">
							<div class="w-10 rounded-full">
								{#if hasAvatar}
									<img src={avatarUrl} alt="Avatar" />
								{:else}
									<div class="w-full h-full bg-base-200 flex items-center justify-center">
										<ShieldUserIcon class="w-6 h-6" />
									</div>
								{/if}
							</div>
						</label>
						<!-- svelte-ignore a11y_no_noninteractive_tabindex -->
						<ul
							tabindex="0"
							class="dropdown-content menu bg-base-100 rounded-box z-50 mt-3 w-52 p-2 shadow-xl border border-base-300"
						>
							<!-- <li><a href="/profile">Profil</a></li> -->
							<li><button on:click={() => settingsModal.showModal()}>Einstellungen</button></li>
							<div class="divider m-0"></div>
							<li><button on:click={() => firstRunModal.open()}>Einführung</button></li>
							<li><a href="/help">FAQ</a></li>
							<li><a href="/support">Support</a></li>
							<li><a href="/floorplan">Gebäudeplan</a></li>
							<div class="divider m-0"></div>
							<li><button on:click={Logout}>Abmelden</button></li>
						</ul>
					</div>
				</div>
			</div>

			<!-- Page content via slot -->
			<div class="flex-1 p-4">
				<slot />
			</div>
		</div>
		<div class="drawer-side border-r border-base-300 z-50">
			<label for="my-drawer-3" aria-label="close sidebar" class="drawer-overlay"></label>
			<div style="position: absolute; top: 1rem; right: 1rem; z-index: 51;" class="lg:hidden">
				{#if drawerOpen}
					<button class="btn btn-square btn-ghost" on:click={() => (drawerOpen = false)}>
						<XIcon />
					</button>
				{/if}
			</div>
			<ul class="menu menu-compact bg-base-100 min-h-full w-65 p-3 flex flex-col gap-0">
				<!-- Logo Section -->
				<li class="mb-3 flex justify-center">
					<a
						href="/dashboard"
						class="flex items-center justify-center gap-3 hover:bg-transparent focus:bg-transparent active:bg-transparent"
					>
						{#if theme === 'dark'}
							<img
								src={logoLight}
								alt="Logo"
								class="w-12 h-12 object-contain rounded-xl shadow border border-base-300 shrink-0"
							/>
						{:else}
							<img
								src={logo}
								alt="Logo"
								class="w-12 h-12 object-contain rounded-xl shadow border border-base-300 shrink-0"
							/>
						{/if}
						<div class="flex flex-col min-w-0">
							<span class="font-bold text-lg truncate">{displayName}</span>
							<span class="text-xs text-base-content/40 font-medium uppercase tracking-wider"
								>Admin</span
							>
						</div>
					</a>
				</li>

				<!-- Menu Groups -->
				{#each groups as group, i}
					<!-- Group label -->
					<li class="px-2 {i === 0 ? 'pt-0' : 'pt-2'} pb-0.5 pointer-events-none">
						<span
							class="text-[10px] font-bold uppercase tracking-[0.14em] select-none p-0
                        {group.admin ? 'text-warning/70' : 'text-base-content/35'}"
						>
							{group.label}
						</span>
					</li>

					{#if group.admin}
						<!-- Admin group: wrapped in a distinct container -->
						<li class="p-0">
							<div
								class="bg-warning/5 border border-warning/20 rounded-xl p-1 grid grid-cols-2 gap-0"
							>
								{#each group.items as item}
									<a
										href={item.href}
										class="flex items-center justify-center gap-2 px-2 py-1.5 rounded-lg font-semibold text-sm text-center
                                           transition-colors text-warning/90 hover:bg-warning/15 hover:no-underline"
									>
										<svelte:component this={item.icon} class="w-4 h-4 text-warning/80 shrink-0" />
										<span class="truncate">{item.label}</span>
									</a>
								{/each}
							</div>
						</li>
					{:else}
						<!-- Regular group items -->
						{#each group.items as item}
							<li class="mb-0.5">
								<a
									href={item.href}
									class="flex items-center gap-2.5 px-2.5 py-1.5 rounded-lg font-semibold text-sm
                                       transition-colors text-primary hover:bg-base-200"
								>
									<svelte:component this={item.icon} class="w-4 h-4 text-primary shrink-0" />
									<span class="truncate">{item.label}</span>
								</a>
							</li>
						{/each}
					{/if}

					{#if i < groups.length - 1}
						<div class="divider my-0 opacity-40"></div>
					{/if}
				{/each}

				<!-- Footer -->
				<div class="mt-auto pt-3 border-t border-base-300">
					<div class="flex flex-col gap-1">
						<a
							href="/dashboard"
							class="flex items-center gap-2.5 px-2.5 py-2 rounded-lg font-semibold text-sm
                               transition-colors text-base-content/60 hover:bg-base-200 hover:text-base-content"
						>
							<DoorOpenIcon class="w-4 h-4 shrink-0" />
							<span>Zurück zum Dashboard</span>
						</a>
						<div class="flex justify-center">
							<button
								on:click={() => open(`${db.baseURL}/_/`, '_blank')}
								class="btn btn-ghost btn-xs text-base-content/40 hover:text-base-content w-full justify-center"
							>
								Pocketbase öffnen
							</button>
						</div>
					</div>
				</div>
			</ul>
		</div>
	</div>
</div>
<dialog bind:this={impersonationModal} class="modal">
	<div class="modal-box max-w-6xl w-11/12">
		<div class="flex flex-wrap items-center justify-between gap-3 mb-4">
			<div class="flex flex-row items-center gap-2">
				<h3 class="font-bold text-lg">Impersonation</h3>
				<div
					class="tooltip tooltip-bottom"
					data-tip="Als Superuser sind Sie an kein Nutzerkonto gebunden und können/müssen sich über andere Konten einloggen. Hier können Sie auswählen, welches Konto Sie übernehmen möchten."
				>
					<div class="btn btn-sm">?</div>
				</div>
			</div>
			<div class="text-sm text-base-content/60">
				{impersonationTotalItems} Nutzer gefunden
			</div>
		</div>

		<label class="input input-bordered flex items-center gap-2 w-full">
			<input
				type="search"
				placeholder="Über Name, E-Mail, Firmenname oder ID suchen"
				class="grow"
				bind:value={impersonationFilter}
				on:input={() => scheduleImpersonationFetch()}
			/>
			{#if impersonationLoading}
				<span class="loading loading-spinner loading-sm"></span>
			{/if}
		</label>

		<div class="collapse collapse-arrow bg-base-200 border border-base-300 mt-3">
			<input type="checkbox" />
			<div class="collapse-title font-semibold">Erweiterte Filter</div>
			<div class="collapse-content">
				<div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-3 pt-2">
					<label class="form-control">
						<span class="label-text mb-1">Name</span>
						<input
							type="text"
							class="input input-bordered w-full"
							placeholder="Name enthält …"
							bind:value={impersonationNameFilter}
							on:input={() => scheduleImpersonationFetch()}
						/>
					</label>

					<label class="form-control">
						<span class="label-text mb-1">E-Mail</span>
						<input
							type="text"
							class="input input-bordered w-full"
							placeholder="E-Mail enthält …"
							bind:value={impersonationEmailFilter}
							on:input={() => scheduleImpersonationFetch()}
						/>
					</label>

					<label class="form-control">
						<span class="label-text mb-1">Firmenname</span>
						<input
							type="text"
							class="input input-bordered w-full"
							placeholder="Firmenname enthält …"
							bind:value={impersonationCompanyFilter}
							on:input={() => scheduleImpersonationFetch()}
						/>
					</label>

					<label class="form-control">
						<span class="label-text mb-1">Exakte Nutzer-ID</span>
						<input
							type="text"
							class="input input-bordered w-full font-mono"
							placeholder="ID"
							bind:value={impersonationIdFilter}
							on:input={() => scheduleImpersonationFetch()}
						/>
					</label>

					<label class="form-control">
						<span class="label-text mb-1">Kontotyp</span>
						<select
							class="select select-bordered w-full"
							bind:value={impersonationAccountType}
							on:change={() => scheduleImpersonationFetch()}
						>
							<option value="all">Alle Konten</option>
							<option value="private">Privatpersonen</option>
							<option value="company">Unternehmen</option>
						</select>
					</label>

					<label class="form-control">
						<span class="label-text mb-1">Verifizierung</span>
						<select
							class="select select-bordered w-full"
							bind:value={impersonationVerification}
							on:change={() => scheduleImpersonationFetch()}
						>
							<option value="all">Alle</option>
							<option value="verified">Verifiziert</option>
							<option value="unverified">Nicht verifiziert</option>
						</select>
					</label>

					<label class="form-control">
						<span class="label-text mb-1">Sortierung</span>
						<select
							class="select select-bordered w-full"
							bind:value={impersonationSort}
							on:change={() => scheduleImpersonationFetch()}
						>
							<option value="+name">Name A–Z</option>
							<option value="-name">Name Z–A</option>
							<option value="-created">Neueste zuerst</option>
							<option value="+created">Älteste zuerst</option>
							<option value="+companyName">Firmenname A–Z</option>
						</select>
					</label>

					<label class="form-control">
						<span class="label-text mb-1">Einträge pro Seite</span>
						<select
							class="select select-bordered w-full"
							bind:value={impersonationPerPage}
							on:change={() => scheduleImpersonationFetch()}
						>
							<option value={10}>10</option>
							<option value={25}>25</option>
							<option value={50}>50</option>
							<option value={100}>100</option>
						</select>
					</label>
				</div>

				<div class="flex justify-end mt-3">
					<button type="button" class="btn btn-ghost btn-sm" on:click={resetImpersonationFilters}>
						Filter zurücksetzen
					</button>
				</div>
			</div>
		</div>

		<div class="mt-4 max-h-96 overflow-y-auto min-h-32">
			{#if impersonationLoading && impersonationUsers.length === 0}
				<div class="flex justify-center items-center h-32">
					<span class="loading loading-spinner loading-lg"></span>
				</div>
			{:else if impersonationUsers.length === 0}
				<div class="alert">
					<span>Keine Nutzer entsprechen den ausgewählten Filtern.</span>
				</div>
			{:else}
				{#each impersonationUsers as user}
					<button
						class:btn-success={impersonationSelectedUser?.id === user.id}
						class:btn-outline={impersonationSelectedUser?.id === user.id}
						class="btn shadow btn-block justify-start mb-2 h-fit py-3"
						on:click={selectUserToImpersonate(user)}
					>
						<div class="flex flex-col items-start h-fit w-full gap-1 text-left">
							<div class="flex flex-wrap items-center gap-2 w-full">
								<span class="font-bold">{user.name || 'Ohne Namen'}</span>
								{#if user.isCompany}
									<span class="badge badge-primary badge-sm">Unternehmen</span>
								{:else}
									<span class="badge badge-ghost badge-sm">Privat</span>
								{/if}
								{#if user.verified}
									<span class="badge badge-success badge-sm">Verifiziert</span>
								{:else}
									<span class="badge badge-warning badge-sm">Nicht verifiziert</span>
								{/if}
							</div>
							{#if user.companyName}
								<span class="text-sm">{user.companyName}</span>
							{/if}
							{#if user.email}
								<span class="text-sm text-base-content/70">{user.email}</span>
							{/if}
							<span class="text-xs font-mono text-base-content/60">ID: {user.id}</span>
						</div>
					</button>
				{/each}
			{/if}
		</div>

		<div class="flex flex-wrap w-full mt-4 items-center justify-between gap-3">
			<span class="text-sm text-base-content/60">
				Seite {impersonationPage} von {impersonationPages}
			</span>
			<div class="join">
				<button
					class="join-item btn"
					disabled={impersonationPage <= 1 || impersonationLoading}
					on:click={() => {
						impersonationPage--;
						fetchImpersonationUsers();
					}}>«</button
				>
				<button class="join-item btn" disabled>{impersonationPage}</button>
				<button
					class="join-item btn"
					disabled={impersonationPage >= impersonationPages || impersonationLoading}
					on:click={() => {
						impersonationPage++;
						fetchImpersonationUsers();
					}}>»</button
				>
			</div>
		</div>

		<div class="modal-action">
			<button type="button" class="btn btn-error" on:click={closeImpersonationModal}>
				Abbrechen
			</button>
			<button
				type="button"
				class="btn btn-success"
				disabled={!impersonationSelectedUser || impersonationLoading}
				on:click={() => impersonate(false)}>Nutzer wechseln</button
			>
			<button
				type="button"
				class="btn btn-accent"
				disabled={!impersonationSelectedUser || impersonationLoading}
				on:click={() => impersonate(true)}>Wechseln + Standard setzen</button
			>
		</div>
	</div>
	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<dialog bind:this={settingsModal} class="modal">
	<div class="modal-box max-w-5xl w-11/12">
		<div class="flex flex-row gap-2 mb-4">
			<h3 class="font-bold text-lg">Einstellungen</h3>
			<div
				class="tooltip tooltip-bottom"
				data-tip="Hier kannst du deine Kontoeinstellungen ändern."
			>
				<div class="btn btn-sm">?</div>
			</div>
		</div>
		<div class="flex flex-row items-center justify-between gap-4 mb-4">
			<span class="font-bold">Thema wechseln (Hell/Dunkel)</span>
			<button class="btn btn-outline" on:click={switchTheme}>Thema wechseln</button>
		</div>
		<div class="flex flex-row items-center justify-between gap-4 mb-4">
			<span class="font-bold">Nutzer impersonieren</span>
			<button class="btn btn-outline" on:click={openImpersonationModal}>Impersonation</button>
		</div>
		<div class="flex flex-row items-center justify-between gap-4 mb-4">
			<span class="font-bold">Benachrichtigungen für neue Support Tickets</span>
			<label class="cursor-pointer">
				<input
					type="checkbox"
					class="toggle toggle-primary"
					checked={listenAdminTicketNotifications}
					on:change={toggleListenToTickets}
				/>
			</label>
		</div>
		{#if getAuthStore()?.record.isCompany}
			<div class="divider"></div>
			<div class="flex flex-row items-center justify-between gap-4 mb-4">
				<span class="font-bold">Firmenname: </span>
				<input
					type="text"
					placeholder="Neuer Firmenname"
					class="input input-bordered"
					bind:value={companyName}
				/>
				<button
					class="btn btn-outline"
					on:click={async () => {
						await db
							.collection('users')
							.update(
								getAuthStore()?.record.id,
								{ companyName: companyName },
								{ requestKey: null }
							);
						alert('Firmenname aktualisiert!');
					}}>Ändern</button
				>
			</div>
		{/if}
		<div class="divider"></div>
		<div class="box box-bordered rounded-lg p-0 mb-4">
			<p class="font-bold">Passwort ändern</p>
			<div class="flex flex-col gap-4 mt-4">
				<input
					type="password"
					placeholder="Altes Passwort"
					class="input input-bordered w-full"
					bind:value={oldPassword}
				/>
				<input
					type="password"
					placeholder="Neues Passwort"
					class="input input-bordered w-full"
					bind:value={newPassword}
				/>
				<input
					type="password"
					placeholder="Neues Passwort bestätigen"
					class="input input-bordered w-full"
					bind:value={newPasswordConfirm}
				/>
			</div>
			<p class="fieldset-label mt-2">
				Achtung: Bei dem Verlusst von dem Passwort kann dies nur über die Geschäftsstelle
				zurückgesetzt werden.
			</p>
			<p class="fieldset-label mt-1">Das Passwort muss mindestens 8 Zeichen lang sein.</p>
			<div class="flex flex-row justify-end w-full mt-4">
				<button class="btn btn-primary" disabled={!passwordValid} on:click={updatePassword}
					>Passwort ändern</button
				>
			</div>
		</div>
		<div class="modal-action">
			<button type="button" class="btn btn-error" on:click={closeSettingsModal}>Schließen</button>
		</div>
	</div>
	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<FirstRunModal bind:this={firstRunModal} />

{#if showNewTicket}
	<div class="toast toast-bottom toast-center z-100 transition-all mt-20">
		<div class="alert alert-info">
			<span>Neues Support Ticket erhalten</span>
		</div>
	</div>
{/if}

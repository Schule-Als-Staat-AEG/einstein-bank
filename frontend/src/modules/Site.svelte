<script lang="ts">
	import {
		Warehouse,
		ForkliftIcon,
		ArrowLeftRightIcon,
		Building2Icon,
		CreditCardIcon,
		HomeIcon,
		LifeBuoyIcon,
		MenuIcon,
		QrCodeIcon,
		ShieldCheckIcon,
		UserIcon,
		XIcon,
		BellIcon,
		BookMarkedIcon,
		HandCoinsIcon,
		ShieldUserIcon,
		Package,
		PackageIcon,
		StoreIcon,
		RefreshCcw,
		ScanQrCodeIcon,
		Locate,
		Signature
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
		getUserData,
		isSuperuser,
		logout,

		refreshStores

	} from '$lib/auth';
	import { isAtm, setAtmMode, setAtmLogoutTime, getAtmLogoutTime } from '$lib/atm';
	import { navigateToNotifications } from '$lib/notifications';
	import { page } from '$app/state';
	import FirstRunModal from './modals/FirstRunModal.svelte';

	// Responsive sidebar: visible on desktop, expandable on small screens
	export let items = [
		{ label: 'Hauptmenü', href: '/dashboard', icon: HomeIcon },
		{ label: 'Transaktionen', href: '/transactions', icon: ArrowLeftRightIcon },
		{ label: 'Daueraufträge', href: '/recurring', icon: RefreshCcw },
		// { label: "Geld überweisen", href: "/transfer", icon: HandCoinsIcon },
		{ label: 'Mein QR-Code', href: '/qr-code', icon: QrCodeIcon },
		{ label: 'Scannen', href: '/scan', icon: ScanQrCodeIcon }
	];

	export let open: boolean = false; /* bound to drawer checkbox */
	let showNotifications: boolean = false;
	let hasNewNotifications: boolean = false; // Set to true to show blue dot
	let isLoggedIn: boolean = false;
	let isAdmin: boolean = false;
	let hasAvatar: boolean = false;
	let avatarUrl: string = '';
	let username: string = '';
	let displayName: string = 'Einstein Bank';
	let language = 'de';

	let listenAdminTicketNotifications: boolean = false;
	let showNewTicket: boolean = false;

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

	let setCompanyNameModal: HTMLDialogElement;
	let setCompanyNameInput: string = '';

	let theme: string = 'light';

	// Settings Modal stuff
	let settingsModal: HTMLDialogElement;
	let oldPassword: string = '';
	let newPassword: string = '';
	let newPasswordConfirm: string = '';

	let companyName: string = '';

	let pageLoadTimestamp = Date.now(); // In case of ATM Mode this can
	let pageLogoutTimestamp = Date.now() + getAtmLogoutTime() * 1000; // Default to current time + 2 minutes
	let isAtmMode: boolean = false;
	let logoutActive: boolean = false; // To control whether the logout process should continue or not

	let logoutTimeString: string = '';
	let timeUpdateVar: number = 0; // To trigger reactive updates for the logout timer

	let firstRunModal: FirstRunModal;

	let passwordsChangeable: boolean = false;

	function switchTheme() {
		if (!browser) return;

		const currentTheme = document.documentElement.dataset.theme || 'light';
		const newTheme = currentTheme === 'light' ? 'dark' : 'light';
		document.documentElement.dataset.theme = newTheme;
		if (localStorage) localStorage.setItem('theme', newTheme);
	}
	onDestroy(() => {
		logoutActive = false; // Stop the logout process when the component is destroyed
		if (impersonationFetchTimeout) clearTimeout(impersonationFetchTimeout);
	});
	onMount(async () => {
		if (!browser) return;

		pageLoadTimestamp = Date.now(); // Update timestamp to force reactivity if needed
		pageLogoutTimestamp = Date.now() + getAtmLogoutTime() * 1000; // Set logout timestamp based on current time + ATM logout duration
		logoutActive = true; // Activate logout process
		waitForLogout(); // Start waiting for logout in case of ATM mode - Check for ATM Mode is done in the function itself

		theme = localStorage.getItem('theme') || 'light';
		language = localStorage.getItem('locale') || 'de';
		if (!db.authStore.isValid) {
			goto('/login');
			return;
		}
		checkAuthRefresh();
		logo = await getFile('logo');
		logoLight = await getFile('logo_dark'); // For Dark mode
		displayName = (await getSetting('display_name')) || 'Einstein Bank';

		companyName = getAuthStore()?.record.companyName || '';

		const data = await db.collection('contracts').getList(1, 1, {
			filter: `sender.user.id="${getAuthStore()?.record.id}" || receiver.user.id="${getAuthStore()?.record.id}"`
		});

		if (data.totalItems > 0) {
			items.push({ label: 'Verträge', href: '/contracts', icon: Signature });
			items = [...items];
		}

		if (getAuthStore()?.record.isCompany) {
			items.push({ label: 'Kassensystem', href: '/cash-register', icon: StoreIcon });
			// Force the user to set a name if they haven't already
			items = [...items];
			if (!companyName && !isSuperuser()) {
				setCompanyNameInput = '';
				setCompanyNameModal.showModal();
			}
		}
		if ((await getSetting('warehouse_enable')) === 'true') {
			if (getAuthStore()?.record.isCompany) {
				items.push({ label: 'Warenlager', href: '/warehouse', icon: ForkliftIcon });
			}
			if (getUserData()?.warehouseAccess == true) {
				items.push({ label: '[SYSTEM] Logistik', href: '/warehouse-management', icon: Warehouse });
			}
			items = [...items];
		}
		if (getUserData()?.financeTrackingAccess == true) {
			items.push({ label: '[SYSTEM] Finanzbehörde', href: '/finance-tracking', icon: Locate });
			items = [...items];
		}
		if (isSuperuser()) {
			isAdmin = true;
			passwordsChangeable = true;
			items.push({ label: 'Admin Dashboard', href: '/admin', icon: ShieldCheckIcon });
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
			items = [...items];
		} else {
			isAdmin = false;
			if (!getUserData().firstTimeLogin) {
				firstRunModal.open();
				await db.collection('user_data').update(
					getUserData()?.id,
					{ firstTimeLogin: true },
					{ requestKey: null }
				);
				await refreshStores();
			}
			passwordsChangeable = (await getSetting('passwords_changeable')) === 'true';
		}
		impersonationSelectedUser = getAuthStore()?.record;
		// Fetch notifications
		db.collection('notifications')
			.getList(1, 3, {
				sort: '+isRead,-created',
				filter: `user.id="${getAuthStore()?.record.id}" && isRead=false`
			})
			.then((result) => {
				notifications = result.items;
				hasNewNotifications = notifications.some((n) => !n.read);
			})
			.catch((error) => {});
	});
	async function waitForLogout() {
		if (!browser) return;
		if (!isAtm()) return;

		logoutActive = true;
		isAtmMode = true;

		console.log('ATM Mode aktiv. Automatischer Logout in ' + getAtmLogoutTime() + ' Sekunden.');

		while (Date.now() < pageLogoutTimestamp && logoutActive) {
			await new Promise((resolve) => setTimeout(resolve, 1000)); // Check every second
			timeUpdateVar++; // Increment to trigger reactive update for the logout timer
			timeUpdateVar = 0;
			console.log(
				'Aktuelles Logout-Zeitstempel: ' +
					pageLogoutTimestamp +
					' | Zeit bis Logout: ' +
					Math.max(0, Math.floor((pageLogoutTimestamp - Date.now()) / 1000)) +
					' Sekunden'
			);
		}
		if (!logoutActive) {
			console.log('Logout Timer Cancelled.');
			return;
		}
		if (Date.now() >= pageLogoutTimestamp) {
			Logout();
		} else {
			waitForLogout(); // Continue waiting if the timestamp was updated/reset
		}
	}
	function Logout() {
		if (!browser) return;
		logout();
		goto('/login');
	}
	function toPlainText(markdown) {
		if (!browser) return;
		const html = marked.parse(markdown || '');
		const tempDiv: any = document.createElement('div');
		tempDiv.innerHTML = html;
		return tempDiv.textContent || tempDiv.innerText || '';
	}
	function markAsRead(notification) {
		if (!browser) return;
		if (!notification.read) {
			notification.read = true;
			// Update hasNewNotifications
			hasNewNotifications = notifications.some((n) => !n.read);
			db.collection('notifications').update(
				notification.id,
				{ isRead: true },
				{ requestKey: null }
			);
		}
	}
	function onNotificationClick(destination) {
		if (!browser) return;
		if (destination) {
			navigateToNotifications(destination);
		}
	}
	async function markAllAsRead() {
		if (!browser) return;
		await Promise.all(
			notifications.map(async (notification) => {
				notification.read = true;
				// Here you would also send a request to the backend to mark all as read
				await db
					.collection('notifications')
					.update(notification.id, { isRead: true }, { requestKey: null });
			})
		);
		db.collection('notifications')
			.getList(1, 3, {
				sort: '+isRead,-created',
				filter: `user.id="${getAuthStore()?.record.id}" && isRead=false`
			})
			.then((result) => {
				notifications = result.items;
				hasNewNotifications = notifications.some((n) => !n.read);
			})
			.catch((error) => {});
		hasNewNotifications = false;
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
	function resetLogoutTimer() {
		if (!browser) return;
		pageLogoutTimestamp = Date.now() + getAtmLogoutTime() * 1000;
		console.log('Logout-Timer zurückgesetzt. Neuer Logout in ' + getAtmLogoutTime() + ' Sekunden.');
		console.log('Aktuelles Logout-Zeitstempel: ' + pageLogoutTimestamp);
	}
	async function submitCompanyName() {
		if (!browser) return;
		if (!setCompanyNameInput.trim()) {
			alert('Bitte geben Sie einen Firmennamen ein.');
			return;
		}
		try {
			await db
				.collection('users')
				.update(
					getAuthStore()?.record.id,
					{ companyName: setCompanyNameInput },
					{ requestKey: null }
				);
			companyName = setCompanyNameInput;
			setCompanyNameInput = '';
			setCompanyNameModal.close();
		} catch (error) {
			alert('Fehler beim Speichern des Firmennamens: ' + error.message);
		}
	}
	function setLanguage(lang: string) {
		if (!browser) return;
		language = lang;
		localStorage.setItem('locale', lang);
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
	$: timeUntilLogout =
		Math.max(0, Math.floor((pageLogoutTimestamp - Date.now()) / 1000)) + timeUpdateVar;
</script>

<div class="sidebar bg-base-100">
	<div class="drawer xl:drawer-open">
		<input id="my-drawer-3" type="checkbox" class="drawer-toggle" bind:checked={open} />
		<div class="drawer-content flex flex-col">
			<!-- Navbar -->
			<div class="navbar bg-base-100 border-b border-base-300 sticky top-0 z-30">
				<!-- Left: Mobile menu button -->
				<div class="flex-none xl:hidden mr-2">
					<label for="my-drawer-3" class="btn btn-square btn-ghost">
						<MenuIcon />
					</label>
				</div>

				<!-- Center: Title (optional, can be removed or customized) -->
				<div class="flex-1">
					<span class="text-xl font-bold xl:hidden">{displayName}</span>
				</div>

				{#if isAtmMode}
					<!-- svelte-ignore a11y_click_events_have_key_events -->
					<!-- svelte-ignore a11y_no_static_element_interactions -->
					<div
						class="flex-1 select-none cursor-pointer"
						on:click={() => {
							resetLogoutTimer();
						}}
					>
						<span class="text-xs text-error font-bold">
							Automatischer Logout in {timeUntilLogout} Sekunden (Klicke zum Verlängern)
						</span>
					</div>
				{/if}

				<!-- Right: Notifications + Avatar -->
				<div class="flex-none gap-2">
					<!-- Notifications button with dropdown -->
					<!-- svelte-ignore a11y_click_events_have_key_events -->
					<div class="dropdown dropdown-end">
						<!-- svelte-ignore a11y_no_noninteractive_tabindex -->
						<!-- svelte-ignore a11y_click_events_have_key_events -->
						<!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
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
											on:click={() => {
												markAsRead(notification.id);
												onNotificationClick(notification.destination);
											}}
											on:mouseenter={() => markAsRead(notification.id)}
											on:touchstart={() => markAsRead(notification.id)}
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
								<button class="btn btn-secondary btn-sm mt-2 w-full" on:click={markAllAsRead}>
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
										{#if !isAdmin}
											<UserIcon class="w-6 h-6" />
										{:else}
											<ShieldUserIcon class="w-6 h-6" />
										{/if}
									</div>
								{/if}
							</div>
						</label>
						<!-- svelte-ignore a11y_no_noninteractive_tabindex -->
						<ul
							tabindex="0"
							class="dropdown-content menu bg-base-100 rounded-box z-5000 mt-3 w-52 p-2 shadow-xl border border-base-300"
						>
							<!-- <li><a href="/profile">Profil</a></li> -->
							<li><button on:click={() => settingsModal.showModal()}>Einstellungen</button></li>
							<div class="divider m-0"></div>
							<li><button on:click={() => firstRunModal.open()}>Einführung</button></li>
							<li><a href="/help">FAQ</a></li>
							<li><a href="/support">Support</a></li>
							<li><button on:click={() => goto(`/floorplan?refer=` + encodeURIComponent(window.location.href))}>Gebäudeplan</button></li>
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
				{#if open}
					<button class="btn btn-square btn-ghost" on:click={() => (open = false)}>
						<XIcon />
					</button>
				{/if}
			</div>
			<ul class="menu bg-base-100 min-h-full w-80 p-4 flex flex-col">
				<!-- Logo Section -->
				<li class="mb-6 flex justify-center">
					<a href="/dashboard" class="flex items-center justify-center">
						{#if theme === 'dark'}
							<img
								src={logoLight}
								alt="Logo"
								class="w-14 h-14 object-contain rounded-xl shadow border border-base-300"
							/>
						{:else}
							<img
								src={logo}
								alt="Logo"
								class="w-14 h-14 object-contain rounded-xl shadow border border-base-300"
							/>
						{/if}
						<div class="flex flex-col min-w-0">
							<span class="font-bold text-xl truncate">{displayName}</span>
						</div>
					</a>
				</li>
				<!-- Menu Items -->
				{#each items as item}
					<li class="mb-3">
						<a
							href={item.href}
							class="flex items-center gap-2 px-4 py-4 rounded-lg font-semibold text-base bg-base-200 transition-colors text-primary"
						>
							<svelte:component this={item.icon} class="w-5 h-5 text-primary" />
							<span class="truncate">{item.label}</span>
						</a>
					</li>
				{/each}
				<!-- Bottom legal links -->
				<li class="mt-auto">
					<div class="flex flex-row gap-2 px-2">
						<a href="/policies/imprint" class="btn btn-ghost btn-sm justify-start">Impressum</a>
						<a href="/policies/privacy" class="btn btn-ghost btn-sm justify-start">Datenschutz</a>
						<a href="/policies/terms-of-service" class="btn btn-ghost btn-sm justify-start">AGBs</a>
					</div>
				</li>
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
		<!-- 
        <div class="flex justify-between items-center gap-4 mb-4 w-full">
            <span class="font-bold">Sprache wechseln (Demnächst)</span>
            <div class="flex flex-row items-center gap-4 mb-4">
                <div class="join">
                    <input class="join-item btn" type="radio" name="language" aria-label="Deutsch" checked={language === "de"} on:click={setLanguage.bind(null, "de")}/>
                    <input class="join-item btn" type="radio" name="language" aria-label="Deutsch-Chen" checked={language === "ch"} on:click={setLanguage.bind(null, "ch")}/>
                    <input class="join-item btn" type="radio" name="language" aria-label="Englisch" checked={language === "en"} on:click={setLanguage.bind(null, "en")}/>
                </div>
            </div>
        </div> 
        -->
		{#if isAdmin}
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
		{/if}
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
		{#if passwordsChangeable}
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
		{/if}
		<div class="modal-action">
			<button type="button" class="btn btn-error" on:click={closeSettingsModal}>Schließen</button>
		</div>
	</div>
	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<dialog bind:this={setCompanyNameModal} class="modal modal-sm">
	<div class="modal-box">
		<h3 class="font-bold text-lg mb-4">Firmennamen einrichten</h3>
		<p class="text-sm text-base-content/70 mb-4">
			Bitte geben Sie den Namen Ihres Unternehmen ein. Dieser Name wird für andere bei Transaktionen
			angezeigt. Ihr Nutzername zur Anmeldung bleibt gleich. Dies ist erforderlich, um das System zu
			nutzen.
		</p>
		<input
			type="text"
			placeholder="Firmenname"
			class="input input-bordered w-full mb-4"
			bind:value={setCompanyNameInput}
			on:keydown={(e) => {
				if (e.key === 'Enter') submitCompanyName();
			}}
		/>
		<div class="modal-action">
			<button type="button" class="btn btn-primary" on:click={submitCompanyName}>Bestätigen</button>
		</div>
	</div>
</dialog>

<FirstRunModal bind:this={firstRunModal} />

{#if showNewTicket}
	<div class="toast toast-bottom toast-center z-100 transition-all mt-20">
		<div class="alert alert-info">
			<span>Neues Support Ticket erhalten</span>
		</div>
	</div>
{/if}

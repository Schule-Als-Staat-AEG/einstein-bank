<script lang="ts">
	import { browser } from '$app/environment';
	import { getAuthStore } from '$lib/auth';
	import db, { server } from '$lib/connections';
	import Site from '$modules/Site.svelte';
	import { onMount } from 'svelte';
	import { Carta } from 'carta-md';
	import { MarkdownEditor } from 'carta-md';
	import { emoji } from '@cartamd/plugin-emoji';
	import { slash } from '@cartamd/plugin-slash';
	import { code } from '@cartamd/plugin-code';
	import '$lib/styles/github.scss';
	import { marked } from 'marked';
	import Currency from '$modules/Currency.svelte';

	// Icons (lucide)
	import {
		Plus,
		Repeat,
		Search,
		Clock,
		CalendarDays,
		MapPin,
		Pencil,
		Trash2,
		ShieldAlert,
		ArrowLeft,
		ArrowRight,
		UserRound,
		BadgeCheck,
		Info
	} from '@lucide/svelte';
	import { CircleQuestionMark } from '@lucide/svelte';
	import AccountSelectModal from '$modules/modals/AccountSelectModal.svelte';

	const WEEKDAYS = ['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'];
	const WEEKDAY_LABELS: Record<string, string> = {
		monday: 'Mo',
		tuesday: 'Di',
		wednesday: 'Mi',
		thursday: 'Do',
		friday: 'Fr',
		saturday: 'Sa',
		sunday: 'So'
	};
	const WEEKDAY_FULL: Record<string, string> = {
		monday: 'Montag',
		tuesday: 'Dienstag',
		wednesday: 'Mittwoch',
		thursday: 'Donnerstag',
		friday: 'Freitag',
		saturday: 'Samstag',
		sunday: 'Sonntag'
	};

	let TIMEZONES: string[] = [];
	try {
		const sv = (Intl as any).supportedValuesOf?.('timeZone');
		if (Array.isArray(sv) && sv.length) TIMEZONES = sv;
	} catch (e) {}
	if (!TIMEZONES.length) {
		TIMEZONES = [
			'UTC',
			'Europe/Berlin',
			'Europe/London',
			'Europe/Paris',
			'America/New_York',
			'America/Los_Angeles',
			'Asia/Tokyo',
			'Asia/Shanghai',
			'Asia/Kolkata',
			'Australia/Sydney'
		];
	}

	function weekdayLabel(d: string) {
		return WEEKDAY_LABELS[d] || d[0] || d;
	}
	function weekdayFull(d: string) {
		return WEEKDAY_FULL[d] || d;
	}

	let recurringTransactions: any[] = [];
	let page = 1;
	let perPage = 10;
	let totalPages = 1;
	let loading = true;

	let authAccount: any = null;

	// Create / Edit form state
	let isEditing = false;
	let editId: string | null = null;
	let receiverAccount: any = null;
	let amount = 0;
	let maxAmount = -1;
	let maxAmountEnabled = false;
	let description = ''; // markdown
	let days: string[] = [];
	let timeString = '00:00';
	let timeSeconds = 0;
	let timezone = '';
	let localTimezone = 0;
	let vat = false;
	let incomeTax = false;

	// Modals
	let accountSelectorModal: AccountSelectModal;
	let createEditModal: HTMLDialogElement;
	let deleteConfirmModal: HTMLDialogElement;
	let selectingFor: 'sender' | 'receiver' = 'receiver';

	// Account selector state
	let accountSearch = '';

	const carta = new Carta({
		sanitizer: false,
		extensions: [emoji(), slash(), code()]
	});

	onMount(async () => {
		if (!browser) return;

		try {
			authAccount = await db
				.collection('accounts')
				.getFirstListItem(`user="${getAuthStore().record.id}"`, { expand: 'user' });
		} catch (e) {
			authAccount = null;
		}

		localTimezone = -new Date().getTimezoneOffset() / 60;

		try {
			timezone =
				Intl.DateTimeFormat().resolvedOptions().timeZone ||
				`UTC${localTimezone >= 0 ? '+' + localTimezone : localTimezone}`;
		} catch (e) {
			timezone = `UTC${localTimezone >= 0 ? '+' + localTimezone : localTimezone}`;
		}

		await fetchRecurringTransactions();
		loading = false;
	});

	async function fetchRecurringTransactions(p = 1) {
		page = p;
		loading = true;
		const filter = `sender.user.id = "${getAuthStore().record.id}"`;
		const res = await db.collection('recurring_transactions').getList(page, perPage, {
			sort: '-created',
			filter
		});
		recurringTransactions = res.items;
		totalPages = res.totalPages || 1;
		await Promise.all(
			recurringTransactions.map(async (r) => {
				r.sender_record = await db
					.collection('public_accounts')
					.getOne(r.sender, { requestKey: null });
				r.receiver_record = await db
					.collection('public_accounts')
					.getOne(r.receiver, { requestKey: null });
			})
		);
		loading = false;
	}

	// Account selector
	function openAccountSelector(forField: 'sender' | 'receiver') {
		selectingFor = forField;
		accountSearch = '';
		accountSelectorModal.showModal();
	}

	function selectAccount(acc: any) {
		receiverAccount = acc;
		accountSelectorModal.close();
	}

	// Form helpers
	function openCreateModal() {
		isEditing = false;
		editId = null;
		amount = 0;
		maxAmount = -1;
		maxAmountEnabled = false;
		description = '';
		days = [];
		timeString = '00:00';
		receiverAccount = null;
		vat = false;
		incomeTax = false;
		createEditModal.showModal();
	}

	function openEditModal(item: any) {
		isEditing = true;
		editId = item.id;
		receiverAccount = item.expand?.receiver || { id: item.receiver };
		amount = item.amount || 0;
		vat = item.vatApplicable || false;
		incomeTax = item.incomeApplicable || false;
		if (typeof item.maxAmount === 'number' && item.maxAmount >= 0) {
			maxAmount = item.maxAmount;
			maxAmountEnabled = true;
		} else {
			maxAmount = -1;
			maxAmountEnabled = false;
		}

		description = item.description || '';
		days = item.days || [];
		timeSeconds = item.time || 0;

		if (typeof item.timezone === 'string') timezone = item.timezone;
		else if (typeof item.timezone === 'number')
			timezone = `UTC${item.timezone >= 0 ? '+' + item.timezone : item.timezone}`;

		timeString = secondsToTimeString(timeSeconds);
		createEditModal.showModal();
	}

	function toggleDay(day: string) {
		if (days.includes(day)) days = days.filter((d) => d !== day);
		else days = [...days, day];
	}

	function timeStringToSeconds(t: string) {
		const [hh = 0, mm = 0] = t.split(':').map(Number);
		const localSecs = (hh || 0) * 3600 + (mm || 0) * 60;
		return ((localSecs % 86400) + 86400) % 86400;
	}

	function secondsToTimeString(s: number) {
		const local = ((Number(s || 0) % 86400) + 86400) % 86400;
		const hh = Math.floor(local / 3600)
			.toString()
			.padStart(2, '0');
		const mm = Math.floor((local % 3600) / 60)
			.toString()
			.padStart(2, '0');
		return `${hh}:${mm}`;
	}

	function prettyReceiver(r: any) {
		return r.name ?? r.receiver;
	}

	async function saveRecurring() {
		if (!receiverAccount) return alert('Empfänger auswählen');

		timeSeconds = timeStringToSeconds(timeString);

		let senderAccountID = (
			await db
				.collection('accounts')
				.getFirstListItem(`user="${getAuthStore().record.id}"`, { requestKey: null })
		).id;

		const payload: any = {
			sender: senderAccountID,
			receiver: receiverAccount?.id,
			amount: Number(amount),
			maxAmount: Number(maxAmount),
			description,
			days,
			time: timeSeconds,
			paid: 0,
			timezone,
			vatApplicable: vat,
			incomeApplicable: incomeTax
		};
		console.log('payload', payload);
		try {
			let res: any;
			if (isEditing && editId)
				res = await db.collection('recurring_transactions').update(editId, payload);
			else res = await db.collection('recurring_transactions').create(payload);

			console.log('saved', res);

			try {
				await fetch(`${server}/api/sync-recurring-transaction?id=${res.id}`, {
					method: 'POST',
					mode: 'no-cors'
				});
			} catch (e) {}

			createEditModal.close();
			await fetchRecurringTransactions(page);
		} catch (e) {
			console.error('save error', e);
			alert('Fehler beim Speichern');
		}
	}

	// Delete
	let toDeleteId: string | null = null;
	function confirmDelete(id: string) {
		toDeleteId = id;
		deleteConfirmModal.showModal();
	}

	async function doDelete() {
		if (!toDeleteId) return;
		try {
			await db.collection('recurring_transactions').delete(toDeleteId);
			try {
				await fetch(`${server}/api/sync-recurring-transaction?id=${toDeleteId}`, {
					method: 'POST',
					mode: 'no-cors'
				});
			} catch (e) {}
			deleteConfirmModal.close();
			toDeleteId = null;
			await fetchRecurringTransactions(page);
		} catch (e) {
			console.error('delete error', e);
			alert('Fehler beim Löschen');
		}
	}
</script>

<title>Daueraufträge</title>
<Site>
	<div class="flex flex-col gap-4">
		<!-- Header -->
		<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
			<div class="flex flex-col md:flex-row md:items-center md:justify-between gap-3">
				<div class="flex items-center gap-3">
					<div
						class="w-11 h-11 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center shadow"
					>
						<Repeat size={20} />
					</div>
					<div class="flex flex-col">
						<h1 class="font-bold text-2xl leading-tight">Daueraufträge</h1>
						<div class="text-sm opacity-70 flex items-center gap-2">
							<Info size={16} />
							<span>
								Gerät-Zeitzone wird genutzt (Standard: <span class="font-mono">{timezone}</span>)
							</span>
						</div>
					</div>
				</div>

				<div class="flex items-center gap-2">
					<button class="btn btn-primary" on:click={openCreateModal}>
						<Plus size={18} />
						Neuen Dauerauftrag
					</button>
				</div>
			</div>

			<div class="mt-4 grid grid-cols-1 md:grid-cols-3 gap-3">
				<div class="bg-base-100 border border-base-300 rounded-2xl p-4 shadow-sm">
					<div class="text-sm opacity-60">Aktive Aufträge</div>
					<div class="text-2xl font-bold mt-1">{recurringTransactions?.length ?? 0}</div>
				</div>
				<div class="bg-base-100 border border-base-300 rounded-2xl p-4 shadow-sm">
					<div class="text-sm opacity-60">Zeitzonen-Hinweis</div>
					<div class="mt-1 text-sm leading-snug opacity-80">
						Es werden keine Geodaten gespeichert. Für DE typischerweise nur <span class="font-mono"
							>Europe/Berlin</span
						>.
					</div>
				</div>
				<div class="bg-base-100 border border-base-300 rounded-2xl p-4 shadow-sm">
					<div class="text-sm opacity-60">Anzeige</div>
					<div class="mt-1 text-sm opacity-80 flex items-center gap-2">
						<Clock size={16} />
						<span>
							Uhrzeit wird in deiner lokalen Zeit angezeigt (<span class="font-mono"
								>UTC{localTimezone >= 0 ? '+' + localTimezone : localTimezone}</span
							>)
						</span>
					</div>
				</div>
			</div>
		</div>

		{#if loading}
			<div
				class="rounded-2xl bg-base-200 border border-base-300 p-6 flex items-center justify-center"
			>
				<span class="loading loading-spinner loading-lg"></span>
			</div>
		{:else}
			{#if !recurringTransactions?.length}
				<div class="rounded-2xl bg-base-200 border border-base-300 p-8 text-center shadow">
					<div class="flex justify-center mb-3">
						<div
							class="w-12 h-12 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center"
						>
							<BadgeCheck size={20} />
						</div>
					</div>
					<div class="font-bold text-lg">Noch keine Daueraufträge</div>
					<div class="opacity-70 mt-1">
						Erstelle deinen ersten Dauerauftrag in wenigen Sekunden.
					</div>
					<div class="mt-4">
						<button class="btn btn-primary" on:click={openCreateModal}>
							<Plus size={18} />
							Dauerauftrag erstellen
						</button>
					</div>
				</div>
			{:else}
				<!-- List -->
				<div class="space-y-3">
					{#each recurringTransactions as r}
						<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
							<div class="flex flex-col md:flex-row md:items-start md:justify-between gap-4">
								<!-- Left -->
								<div class="flex-1 min-w-0">
									<div class="flex items-start gap-3">
										<div
											class="w-10 h-10 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center shrink-0"
										>
											<UserRound size={18} />
										</div>

										<div class="min-w-0">
											<div class="flex flex-wrap items-center gap-2">
												<span class="opacity-70 text-sm">An</span>
												<span class="font-bold truncate">{prettyReceiver(r)}</span>
												<span class="badge badge-ghost font-mono">Konto {r.receiver}</span>
											</div>

											<div class="mt-2 flex flex-wrap items-center gap-2">
												<span class="badge badge-outline flex items-center gap-2">
													<span class="opacity-70">Betrag</span>
													<span class="font-bold">{r.amount}</span>
													<Currency size={18} />
												</span>

												{#if typeof r.maxAmount === 'number' && r.maxAmount >= 0}
													<span class="badge badge-warning badge-outline">
														Anzahl der Durchführungen: {r.maxAmount}
													</span>
												{:else}
													<span class="badge badge-ghost opacity-70">Kein Limit</span>
												{/if}
											</div>

											{#if r.description}
												<div class="mt-3 prose max-w-full">
													{@html marked(r.description || '')}
												</div>
											{/if}
										</div>
									</div>
								</div>

								<!-- Right -->
								<div class="flex flex-col gap-2 md:items-end">
									<div class="flex items-center gap-2 flex-wrap justify-start md:justify-end">
										<span class="badge badge-ghost flex items-center gap-2">
											<CalendarDays size={14} />
											{#if r.days?.length}
												{#each r.days as d}
													<span title={weekdayFull(d)} class="font-semibold">{weekdayLabel(d)}</span
													>
												{/each}
											{:else}
												<span class="opacity-70">keine Tage</span>
											{/if}
										</span>

										<span class="badge badge-ghost flex items-center gap-2">
											<Clock size={14} />
											<span class="font-mono">{secondsToTimeString(r.time || 0)}</span>
										</span>

										<span class="badge badge-ghost flex items-center gap-2">
											<MapPin size={14} />
											<span class="font-mono">{r.timezone ?? timezone}</span>
										</span>
									</div>

									<div class="flex gap-2 mt-1">
										<button class="btn btn-sm btn-outline" on:click={() => openEditModal(r)}>
											<Pencil size={16} />
											Bearbeiten
										</button>
										<button
											class="btn btn-sm btn-error btn-outline"
											on:click={() => confirmDelete(r.id)}
										>
											<Trash2 size={16} />
											Löschen
										</button>
									</div>
								</div>
							</div>
						</div>
					{/each}

					<!-- Pagination -->
					<div class="flex justify-center pt-2">
						<div class="join">
							<button
								class="join-item btn"
								on:click={() => fetchRecurringTransactions(Math.max(1, page - 1))}
								disabled={page <= 1}
								aria-label="Zurück"
							>
								<ArrowLeft size={18} />
							</button>
							<button class="join-item btn btn-ghost">
								Seite <span class="font-mono">{page}</span> /
								<span class="font-mono">{totalPages}</span>
							</button>
							<button
								class="join-item btn"
								on:click={() => fetchRecurringTransactions(Math.min(totalPages, page + 1))}
								disabled={page >= totalPages}
								aria-label="Weiter"
							>
								<ArrowRight size={18} />
							</button>
						</div>
					</div>
				</div>
			{/if}
		{/if}
	</div>

	<!-- Account selector modal -->
	<AccountSelectModal
		bind:this={accountSelectorModal}
		on:select={(e) => selectAccount(e.detail)}
		filter={accountSearch}
	/>

	<!-- Create / Edit Modal -->
	<dialog class="modal" bind:this={createEditModal} id="createEdit">
		<form method="dialog" class="modal-box w-full max-w-3xl rounded-2xl border border-base-300">
			<div class="flex items-start justify-between gap-3">
				<div>
					<h3 class="font-bold text-lg">
						{isEditing ? 'Dauerauftrag bearbeiten' : 'Neuen Dauerauftrag erstellen'}
					</h3>
					<div class="text-sm opacity-70 mt-1 flex items-center gap-2">
						<ShieldAlert size={16} />
						<span>* Bei Dauerüberweisungen fallen Steuern zusätzlich zum Betrag an.</span>
					</div>
				</div>
				<span class="badge badge-ghost">{isEditing ? 'Edit' : 'Create'}</span>
			</div>

			<div class="mt-5 grid grid-cols-1 md:grid-cols-2 gap-4">
				<!-- Receiver -->
				<div class="md:col-span-2">
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="label mb-1"><span class="label-text">Empfänger *</span></label>
					<button
						class="btn btn-outline w-full justify-between rounded-2xl"
						on:click={(e) => {
							e.preventDefault();
							openAccountSelector('receiver');
						}}
					>
						<span class="truncate">
							{#if receiverAccount}
								{receiverAccount?.name ?? receiverAccount.name ?? receiverAccount.id}
							{:else}
								Account auswählen
							{/if}
						</span>
						<span class="badge badge-ghost">Suchen</span>
					</button>
				</div>

				<!-- Days -->
				<div class="md:col-span-2">
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="label mb-1"><span class="label-text">Wochentage</span></label>
					<div class="flex flex-row gap-2 flex-nowrap overflow-auto">
						{#each WEEKDAYS as wd}
							<button
								class="btn btn-circle {days.includes(wd) ? 'btn-primary' : 'btn-outline'}"
								title={weekdayFull(wd)}
								on:click={(e) => {
									e.preventDefault();
									toggleDay(wd);
								}}
							>
								{weekdayLabel(wd)}
							</button>
						{/each}
					</div>
				</div>

				<!-- Time -->
				<div>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="label mb-1"><span class="label-text">Uhrzeit</span></label>
					<label class="input input-bordered flex items-center gap-2 rounded-2xl">
						<Clock size={18} class="opacity-60" />
						<input type="time" class="grow" bind:value={timeString} />
					</label>
				</div>

				<!-- Amount -->
				<div>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="label mb-1"><span class="label-text">Betrag</span></label>
					<div class="flex items-center gap-2">
						<input
							type="number"
							class="input input-bordered w-full rounded-2xl"
							bind:value={amount}
						/>
						<div
							class="w-12 h-12 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center"
						>
							<Currency size={26} />
						</div>
					</div>
				</div>

				<!-- Max Amount -->
				<div class="md:col-span-2">
					<label class="label mb-1"
						><span class="label-text">Maximale Überweisungen</span><button
							class="btn btn-ghost btn-xs tooltip tooltip-bottom"
							data-tip="Der Dauerauftrag wird automatisch abgebrochen, wenn diese Anzahl an Überweisungen erreicht wird."
							><CircleQuestionMark></CircleQuestionMark></button
						></label
					>
					<div class="flex items-center gap-3">
						<input
							type="checkbox"
							class="toggle"
							bind:checked={maxAmountEnabled}
							on:change={() => {
								if (!maxAmountEnabled) maxAmount = -1;
								else if (maxAmount < 0) maxAmount = 0;
							}}
						/>
						{#if !maxAmountEnabled}
							<input
								type="text"
								class="input input-bordered w-full rounded-2xl"
								autocomplete="off"
								value="Kein Limit festgelegt"
								disabled
							/>
						{:else}
							<input
								type="number"
								class="input input-bordered w-full rounded-2xl"
								autocomplete="off"
								bind:value={maxAmount}
							/>
						{/if}
					</div>
				</div>

				<!-- Description -->
				<div class="md:col-span-2">
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="label mb-1"><span class="label-text">Beschreibung</span></label>
					<div class="rounded-2xl border border-base-300 overflow-hidden bg-base-100">
						<MarkdownEditor mode="tabs" bind:value={description} {carta} theme="github" />
					</div>
				</div>

				<!-- svelte-ignore a11y_no_noninteractive_tabindex -->
				<div class="md:col-span-2">
					<div
						tabindex="0"
						class="collapse collapse-arrow bg-base-100 border-base-300 border w-full"
					>
						<div class="collapse-title font-semibold w-full">Erweiterte Einstellungen</div>
						<div class="collapse-content text-sm">
							<span
								>Hier kannst du optionale Einstellungen vornehmen. Falls du über dieses Menu
								Einkommen überweist, kreuze "Einkommen" an. Je nach Auswahl kann die
								Steuerberechnung angepasst werden. Bei anderen Transaktionen können diese Optionen
								ignoriert werden.</span
							>
							<div class="divider"></div>
							<div class="flex flex-row justify-between">
								<span>Einkommen</span>
								<input type="checkbox" class="toggle" bind:checked={incomeTax} />
							</div>
							<div class="flex-row justify-between mt-2 hidden">
								<span>Mehrwertsteuerpflichtig</span>
								<input type="checkbox" class="toggle" bind:checked={vat} />
							</div>
						</div>
					</div>
				</div>

				<!-- Timezone -->
				<div class="md:col-span-2">
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label class="label mb-1"><span class="label-text">Zeitzone</span></label>
					<details class="bg-base-100 border border-base-300 p-3 rounded-2xl">
						<summary class="text-sm opacity-70 flex items-center gap-2 cursor-pointer">
							<MapPin size={16} />
							<span class="font-mono">{timezone}</span>
						</summary>
						<div class="mt-3">
							<input
								type="search"
								list="tzlist"
								class="input input-bordered w-full rounded-2xl"
								placeholder="Suche oder wähle Zeitzone"
								bind:value={timezone}
								autocomplete="off"
							/>
							<datalist id="tzlist">
								{#each TIMEZONES as tz}
									<option value={tz}></option>
								{/each}
							</datalist>
						</div>
					</details>
				</div>
			</div>

			<div class="modal-action mt-5">
				<button class="btn btn-ghost" on:click={() => createEditModal.close()}>Abbrechen</button>
				<button
					class="btn btn-primary"
					on:click={(e) => {
						e.preventDefault();
						saveRecurring();
					}}
				>
					{isEditing ? 'Speichern' : 'Erstellen'}
				</button>
			</div>
		</form>
		<form method="dialog" class="modal-backdrop"><button>close</button></form>
	</dialog>

	<!-- Delete confirmation -->
	<dialog class="modal" bind:this={deleteConfirmModal} id="deleteConfirm">
		<form method="dialog" class="modal-box rounded-2xl border border-base-300">
			<div class="flex items-center gap-3">
				<div
					class="w-10 h-10 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center"
				>
					<Trash2 size={18} />
				</div>
				<div>
					<h3 class="font-bold text-lg">Dauerauftrag löschen</h3>
					<p class="text-sm opacity-70">Diese Aktion kann nicht rückgängig gemacht werden.</p>
				</div>
			</div>

			<div class="mt-4 bg-base-200 border border-base-300 rounded-2xl p-4">
				<p class="opacity-80">Möchtest du diesen Dauerauftrag wirklich löschen?</p>
			</div>

			<div class="modal-action">
				<button class="btn btn-ghost">Abbrechen</button>
				<button
					class="btn btn-error"
					on:click={(e) => {
						e.preventDefault();
						doDelete();
					}}
				>
					Löschen
				</button>
			</div>
		</form>
		<form method="dialog" class="modal-backdrop"><button>close</button></form>
	</dialog>
</Site>

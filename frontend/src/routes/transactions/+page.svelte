<script lang="ts">
	import { browser } from '$app/environment';
	import { goto } from '$app/navigation';
	import { getAuthStore } from '$lib/auth';
	import db from '$lib/connections';
	import Currency from '$modules/Currency.svelte';
	import Site from '$modules/Site.svelte';
	import { onMount } from 'svelte';

	import {
		ArrowUpRight,
		ArrowDownLeft,
		Repeat,
		Search,
		Plus,
		Copy,
		Info,
		CalendarClock,
		UserRound,
		ShieldCheck,
		Hash,
		FileText,
		Fingerprint,
		Monitor,
		X
	} from '@lucide/svelte';

	let filter: string = '';
	let transactions: any[] = [];
	let enrichedTransactions: any[] = [];
	let user: any = null;
	let page = 1;
	let perPage = 30;
	let totalPages = 0;

	let infoModal: HTMLDialogElement;
	let selectedTransaction: any = null;
	let loading = true;

	// debounce for search
	let searchTimer: any = null;

	function typeBadgeClass(type: string) {
		if (type === 'Eingehend') return 'badge badge-sm badge-success badge-outline';
		if (type === 'Ausgehend') return 'badge badge-sm badge-error badge-outline';
		return 'badge badge-sm badge-info badge-outline';
	}

	function typeIcon(type: string) {
		if (type === 'Eingehend') return ArrowDownLeft;
		if (type === 'Ausgehend') return ArrowUpRight;
		return Repeat;
	}

	function safeDate(d: any) {
		try {
			return new Date(d).toLocaleString();
		} catch (e) {
			return String(d ?? '');
		}
	}

	function safeDesc(desc: string) {
		const t = String(desc || '').trim();
		if (!t) return 'Keine Beschreibung vorhanden.';
		const cut = 90;
		return t.length > cut ? t.slice(0, cut).trimEnd() + '…' : t;
	}

	function copyText(txt: string) {
		try {
			navigator.clipboard?.writeText(txt);
		} catch (e) {}
	}

	onMount(async () => {
		if (!browser) return;
		user = getAuthStore().record;
		await fetchTransactions();

		// URL params: ?id=
		const urlParams = new URLSearchParams(window.location.search);
		const transactionId = urlParams.get('id');
		if (transactionId) showDetails(transactionId);
	});

	async function fetchTransactions() {
		loading = true;

		// Load transactions
		let result = await db.collection('public_transactions').getList(page, perPage, {
			sort: '-created',
			filter: `(sender ~ "${filter}" || receiver ~ "${filter}" || sender_name ~ "${filter}" || receiver_name ~ "${filter}") && (sender_id="${user.id}" || receiver_id="${user.id}")`
		});
		console.log('fetchTransactions result', result);
		transactions = result.items;
		totalPages = result.totalPages;

		enrichedTransactions = await Promise.all(
			transactions.map(async (tx) => {
				let receiver: any = await db
					.collection('public_accounts')
					.getOne(tx.receiver, { requestKey: null });
				let sender: any = await db
					.collection('public_accounts')
					.getOne(tx.sender, { requestKey: null });

				let type: string;
				// Note sender.user IS the user id while sender.expand.user is the record
				// Same for receiver
				if (sender.user === user.id) type = 'Ausgehend';
				else if (receiver.user === user.id) type = 'Eingehend';
				else type = 'Transfer';

				return {
					...tx,
					senderName: sender.isCompany
						? sender?.companyName != ''
							? sender?.companyName
							: sender?.name || 'Unknown'
						: sender?.name || 'Unknown',
					receiverName: receiver.isCompany
						? receiver?.companyName != ''
							? receiver?.companyName
							: receiver?.name || 'Unknown'
						: receiver?.name || 'Unknown',
					sender_record: sender,
					receiver_record: receiver,
					isQr:
						type === 'Ausgehend' && tx.system?.includes('##QR_CODE_AUTHORIZATION_ACKNOWLEDGED##')
							? true
							: false,
					type
				};
			})
		);
		enrichedTransactions.sort(
			(a, b) => new Date(b.created).getTime() - new Date(a.created).getTime()
		);
		enrichedTransactions = [...enrichedTransactions];
		loading = false;
	}

	function onSearchInput() {
		if (searchTimer) clearTimeout(searchTimer);
		searchTimer = setTimeout(() => {
			page = 1;
			fetchTransactions();
		}, 220);
	}

	async function showDetails(transactionId: string) {
		selectedTransaction = enrichedTransactions.find((tx) => tx.id === transactionId);

		if (!selectedTransaction) {
			await db
				.collection('public_transactions')
				.getOne(transactionId, { requestKey: null })
				.then(async (tx) => {
					let receiver: any = await db
						.collection('public_accounts')
						.getOne(tx.receiver, { requestKey: null });
					let sender: any = await db
						.collection('public_accounts')
						.getOne(tx.sender, { requestKey: null });

					let type: string;
					if (sender?.user === user) type = 'Ausgehend';
					else if (receiver?.user === user) type = 'Eingehend';
					else type = 'Transfer';

					selectedTransaction = {
						...tx,
						senderName: sender.isCompany
							? sender?.companyName != ''
								? sender?.companyName
								: sender?.name || 'Unknown'
							: sender?.name || 'Unknown',
						receiverName: receiver.isCompany
							? receiver?.companyName != ''
								? receiver?.companyName
								: receiver?.name || 'Unknown'
							: receiver?.name || 'Unknown',
						sender_record: sender,
						receiver_record: receiver,
						isQr:
							type === 'Ausgehend' && tx.system?.includes('##QR_CODE_AUTHORIZATION_ACKNOWLEDGED##')
								? true
								: false,
						type
					};

					infoModal?.showModal();
				})
				.catch((e) => console.error('getOne failed', e));

			return;
		}

		infoModal?.showModal();
	}
</script>

<title>Transaktionen</title>
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
						<h1 class="font-bold text-2xl leading-tight">Transaktionen</h1>
						<p class="text-sm opacity-70 flex items-center gap-2">
							<Info size={16} />
							<span>Hier siehst du alle Ein- und Ausgänge deines Kontos.</span>
						</p>
					</div>
				</div>

				<button class="btn btn-primary" on:click={() => goto('/transfer')}>
					<Plus size={18} />
					Neue Transaktion
				</button>
			</div>

			<div class="mt-4 flex flex-col md:flex-row gap-2">
				<label class="input input-bordered w-full flex items-center gap-2 rounded-2xl">
					<Search size={18} class="opacity-60" />
					<input
						type="text"
						class="grow"
						placeholder="Suchen (Name oder Konto-ID)"
						bind:value={filter}
						on:input={onSearchInput}
						autocomplete="off"
					/>
				</label>

				<div class="flex items-center gap-2">
					<span class="badge badge-ghost">
						Seite <span class="font-mono mx-1">{page}</span> /
						<span class="font-mono">{Math.max(1, totalPages || 1)}</span>
					</span>
					{#if loading}
						<span class="loading loading-spinner"></span>
					{/if}
				</div>
			</div>
		</div>

		<!-- Content -->
		{#if loading}
			<div
				class="rounded-2xl bg-base-200 border border-base-300 p-6 flex items-center justify-center"
			>
				<span class="loading loading-spinner loading-lg"></span>
			</div>
		{:else}
			{#if !enrichedTransactions?.length}
				<div class="rounded-2xl bg-base-200 border border-base-300 p-8 text-center shadow">
					<div class="flex justify-center mb-3">
						<div
							class="w-12 h-12 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center"
						>
							<ShieldCheck size={20} />
						</div>
					</div>
					<div class="font-bold text-lg">Keine Transaktionen gefunden</div>
					<div class="opacity-70 mt-1">
						Passe deinen Filter an oder erstelle eine neue Transaktion.
					</div>
					<div class="mt-4">
						<button class="btn btn-primary" on:click={() => goto('/transfer')}>
							<Plus size={18} />
							Transaktion erstellen
						</button>
					</div>
				</div>
			{:else}
				<div
					class="grid grid-cols-1 lg:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4 3xl:grid-cols-5 gap-3"
				>
					{#each enrichedTransactions as tx}
						{@const Icon = typeIcon(tx.type)}
						<div
							class="rounded-2xl bg-base-200 border border-base-300 shadow hover:shadow-lg transition-shadow"
						>
							<div class="p-4 md:p-5 flex flex-col gap-3">
								<!-- top row -->
								<div class="flex items-start justify-between gap-3">
									<div class="flex items-start gap-3 min-w-0">
										<div
											class="w-11 h-11 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center shrink-0"
										>
											<Icon size={20} />
										</div>
										<div class="min-w-0">
											<div class="flex items-center gap-2 flex-wrap">
												<h3 class="font-bold text-lg leading-tight">{tx.type}</h3>
												<span class={typeBadgeClass(tx.type)}>{tx.type}</span>
												{#if tx.isQr}
													<span class="badge badge-sm badge-outline badge-primary">QR-Code</span>
												{/if}
											</div>
											<div class="text-sm opacity-70 flex items-center gap-2 mt-0.5">
												<CalendarClock size={16} />
												<span>{safeDate(tx.created)}</span>
											</div>
										</div>
									</div>

									<!-- amount -->
									<div class="text-right shrink-0">
										{#if tx.type === 'Ausgehend'}
											<div class="text-xl font-bold flex items-center gap-1 text-error">
												<span>-{tx.amount}</span>
												<Currency size={22} />
											</div>
										{:else if tx.type === 'Eingehend'}
											<div class="text-xl font-bold flex items-center gap-1 text-success">
												<span>+{tx.received}</span>
												<Currency size={22} />
											</div>
										{:else}
											<div class="text-xl font-bold flex items-center gap-1">
												<span>{tx.amount}</span>
												<Currency size={22} />
											</div>
										{/if}
									</div>
								</div>

								<!-- parties -->
								<div class="rounded-2xl bg-base-100 border border-base-300 p-3">
									<div class="flex items-center gap-2 text-sm">
										<UserRound size={16} class="opacity-60" />
										<span class="opacity-70">Von</span>
										<span class="font-semibold truncate">{tx.senderName}</span>
									</div>
									<div class="flex items-center gap-2 text-sm mt-1">
										<UserRound size={16} class="opacity-60" />
										<span class="opacity-70">An</span>
										<span class="font-semibold truncate">{tx.receiverName}</span>
									</div>
								</div>

								<!-- description -->
								<div class="rounded-2xl bg-base-100 border border-base-300 p-3">
									<div class="text-xs opacity-70 flex items-center gap-2">
										<FileText size={14} />
										<span>Beschreibung</span>
									</div>
									<p class="mt-2 text-sm whitespace-pre-wrap">{safeDesc(tx.description)}</p>
								</div>

								<!-- actions -->
								<div class="flex items-center justify-end gap-2">
									<button class="btn btn-sm btn-outline" on:click={() => showDetails(tx.id)}>
										<Info size={16} />
										Details
									</button>
									<button class="btn btn-sm btn-primary" on:click={() => copyText(tx.id)}>
										<Copy size={16} />
										Kopieren
									</button>
								</div>
							</div>
						</div>
					{/each}
				</div>

				<!-- Pagination -->
				{#if totalPages > 1}
					<div class="flex justify-center pt-2">
						<div class="join">
							<button
								class="join-item btn"
								on:click={() => {
									if (page > 1) {
										page -= 1;
										fetchTransactions();
									}
								}}
								disabled={page === 1}
							>
								« Zurück
							</button>
							<button class="join-item btn btn-ghost">
								Seite <span class="font-mono mx-1">{page}</span> von
								<span class="font-mono">{totalPages}</span>
							</button>
							<button
								class="join-item btn"
								on:click={() => {
									if (page < totalPages) {
										page += 1;
										fetchTransactions();
									}
								}}
								disabled={page === totalPages}
							>
								Weiter »
							</button>
						</div>
					</div>
				{/if}
			{/if}
		{/if}
	</div>

	<!-- Details Modal -->
	<dialog id="infoModal" class="modal" bind:this={infoModal} aria-modal="true">
		<div class="modal-box w-full max-w-3xl rounded-2xl border border-base-300">
			<header class="flex items-start justify-between gap-4">
				<div class="min-w-0">
					<h3 class="text-xl font-bold flex items-center gap-2">
						<Info size={18} />
						Transaktionsübersicht
					</h3>
					{#if selectedTransaction}
						<p class="text-sm opacity-70 mt-1 flex items-center gap-2">
							<Hash size={16} />
							<span class="truncate">ID:</span>
							<span class="font-mono text-xs truncate">{selectedTransaction.id}</span>
						</p>
					{/if}
				</div>

				<div class="flex items-center gap-2 shrink-0">
					<button
						class="btn btn-ghost btn-sm"
						on:click={() => copyText(selectedTransaction?.id)}
						aria-label="ID kopieren"
					>
						<Copy size={16} />
						ID kopieren
					</button>
					{#if selectedTransaction?.status !== 'under_audit'}
						{#if selectedTransaction?.status == 'flagged'}
							<span
								class="badge badge-error badge-sm tooltip tooltip-left"
								data-tip="Anomalie gefunden! Eine Person wird diese Überweisung überprüfen. Keine Sorge, die Überweisung wurde trotzdem durchgeführt."
							></span>
						{:else}
							<span class="badge badge-ghost badge-sm"></span>
						{/if}

						{#if selectedTransaction?.status == 'pending'}
							<span
								class="badge badge-warning badge-sm tooltip tooltip-left"
								data-tip="Überprüfung ausstehend! Der Transaktions-Server überprüft die Überweisung auf mögliche Probleme."
							></span>
						{:else}
							<span class="badge badge-ghost badge-sm"></span>
						{/if}

						{#if selectedTransaction?.status == 'approved'}
							<span
								class="badge badge-success badge-sm tooltip tooltip-left"
								data-tip="Genehmigt! Der Transaktions-Server hat die Überweisung überprüft und genehmigt. Hurra!"
							></span>
						{:else}
							<span class="badge badge-ghost badge-sm"></span>
						{/if}
					{:else}
						<span
							class="badge badge-neutral badge-md tooltip tooltip-left"
							data-tip="Es wird eine Finanzüberprüfung durchgeführt da Verdacht auf Steuerhinterziehung besteht. Wir hoffen, dass wir uns geirrt haben. Ein Besuch von einem Mitarbeiter der Finanzbehörde ist zu erwarten."
							>Unter Prüfung</span
						>
					{/if}

					<button
						class="btn btn-ghost btn-sm"
						aria-label="Schließen"
						on:click={() => infoModal.close()}
					>
						<X size={18} />
					</button>
				</div>
			</header>

			<div class="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
				<!-- Parties -->
				<div class="rounded-2xl bg-base-200 border border-base-300 p-4">
					<div class="text-sm opacity-70 flex items-center gap-2">
						<UserRound size={16} />
						<span>Parteien</span>
					</div>

					<div class="mt-3">
						<p class="text-xs opacity-60">Von</p>
						<div class="flex flex-wrap items-center gap-2">
							<p class="font-semibold">{selectedTransaction?.senderName}</p>
							<span class="badge badge-ghost font-mono">{selectedTransaction?.sender}</span>
						</div>
					</div>

					<div class="mt-3">
						<p class="text-xs opacity-60">An</p>
						<div class="flex flex-wrap items-center gap-2">
							<p class="font-semibold">{selectedTransaction?.receiverName}</p>
							<span class="badge badge-ghost font-mono">{selectedTransaction?.receiver}</span>
						</div>
					</div>
				</div>

				<!-- Amount -->
				<div class="rounded-2xl bg-base-200 border border-base-300 p-4">
					<div class="flex items-start justify-between gap-3">
						<div>
							<p class="text-sm opacity-70">Betrag</p>
							{#if selectedTransaction?.type === 'Ausgehend'}
								<div class="flex items-center gap-2 mt-2 text-error">
									<p class="text-2xl font-bold">-{selectedTransaction?.amount}</p>
									<Currency size={22} />
								</div>
							{:else if selectedTransaction?.type === 'Eingehend'}
								<div class="flex items-center gap-2 mt-2 text-success">
									<p class="text-2xl font-bold">+{selectedTransaction?.received}</p>
									<Currency size={22} />
								</div>
							{:else}
								<div class="flex items-center gap-2 mt-2">
									<p class="text-2xl font-bold">{selectedTransaction?.amount}</p>
									<Currency size={22} />
								</div>
							{/if}
						</div>

						<div class="text-right">
							<span class={typeBadgeClass(selectedTransaction?.type || '')}>
								{selectedTransaction?.type}
							</span>
							<p class="text-sm opacity-70 mt-2 flex items-center gap-2 justify-end">
								<CalendarClock size={16} />
								<span>{selectedTransaction ? safeDate(selectedTransaction.created) : ''}</span>
							</p>
						</div>
					</div>
				</div>

				<!-- Description (collapsible) -->
				<details
					class="rounded-2xl bg-base-200 border border-base-300 p-0 md:col-span-2 overflow-hidden"
					open
				>
					<summary class="p-4 text-sm opacity-70 flex items-center gap-2 cursor-pointer">
						<FileText size={16} />
						<span>Beschreibung</span>
					</summary>
					<div class="p-4 pt-0">
						<p class="whitespace-pre-wrap mt-1">
							{selectedTransaction?.description.trim() || 'Keine Beschreibung vorhanden.'}
						</p>
					</div>
				</details>

				<!-- System (collapsible) -->
				<details
					class="rounded-2xl bg-base-200 border border-base-300 p-0 md:col-span-2 overflow-hidden"
				>
					<summary class="p-4 text-sm opacity-70 flex items-center gap-2 cursor-pointer">
						<Monitor size={16} />
						<span>System</span>
					</summary>
					<div class="p-4 pt-0">
						<p class="whitespace-pre-wrap mt-1">
							{selectedTransaction?.system || 'Keine Systeminformationen vorhanden.'}
						</p>
					</div>
				</details>

				<!-- Signature (collapsible) -->
				<details
					class="rounded-2xl bg-base-200 border border-base-300 p-0 md:col-span-2 overflow-hidden"
				>
					<summary class="p-4 text-sm opacity-70 flex items-center gap-2 font-mono cursor-pointer">
						<Fingerprint size={16} />
						<span>Signatur</span>
					</summary>
					<div class="p-4 pt-0">
						<p class="mt-1 font-mono text-xs opacity-70 break-all select-none">
							{selectedTransaction?.signature || 'Keine Signatur vorhanden.'}
						</p>
					</div>
				</details>
			</div>

			<div class="modal-action mt-2">
				<button class="btn btn-outline" on:click={() => infoModal.close()}>Schließen</button>
			</div>
		</div>

		<form method="dialog" class="modal-backdrop">
			<button>close</button>
		</form>
	</dialog>
</Site>

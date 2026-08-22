<script lang="ts">
	import PocketBase from 'pocketbase';
	import { onMount } from 'svelte';
	import { getAuthStore } from '$lib/auth';
	import Site from '$modules/Site.svelte';
	import { goto } from '$app/navigation';
	import Currency from '$modules/Currency.svelte';
	import { browser } from '$app/environment';
	import db, { server } from '$lib/connections';

	import {
		ArrowUpRight,
		ArrowDownLeft,
		Wallet,
		ShieldAlert,
		QrCode,
		ReceiptText,
		Activity,
		Send,
		Sparkles,
		TriangleAlert
	} from '@lucide/svelte';
	import BarChart from '$lib/components/BarChart.svelte';
	import LineChart from '$lib/components/LineChart.svelte';
	import { readCache, writeCache } from '$lib/caching';

	let localUser: any = null;

	let account: any = null;
	let transactions: any[] = [];
	let loading = true;
	let loadingTransactions = true;
	let error: string | null = null;

	let wealthTaxAffected = false;
	let wealthTaxToDeduct = 0;

	// -----------------------------
	// UI + CHART DERIVED STATE
	// -----------------------------
	const DAY_MS = 24 * 60 * 60 * 1000;

	function toDayKey(d: Date) {
		const y = d.getFullYear();
		const m = String(d.getMonth() + 1).padStart(2, '0');
		const day = String(d.getDate()).padStart(2, '0');
		return `${y}-${m}-${day}`;
	}

	// Split lists
	$: incoming = (transactions || []).filter((t) => t.receiver === account?.id);
	$: outgoing = (transactions || []).filter((t) => t.sender === account?.id);

	// Sums
	$: incomingSum = incoming.reduce((s, t) => s + (Number(t.amount) || 0), 0);
	$: outgoingSum = outgoing.reduce((s, t) => s + (Number(t.amount) || 0), 0);
	$: netSum = incomingSum - outgoingSum;

	function fmtMoney(v: number) {
		const n = Number.isFinite(v) ? v : 0;
		return n.toLocaleString('de-DE', {
			minimumFractionDigits: 0,
			maximumFractionDigits: 2
		});
	}

	function fmtDate(ts: string) {
		const d = new Date(ts);
		return Number.isNaN(d.getTime()) ? '-' : d.toLocaleDateString('de-DE');
	}

	// 7-day net by day (incoming - outgoing)
	$: netByDay = (() => {
		const days = 7;
		const now = new Date();
		const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
		const start = new Date(today.getTime() - (days - 1) * DAY_MS);

		const map = new Map<string, number>();
		for (let i = 0; i < days; i++) {
			const d = new Date(start.getTime() + i * DAY_MS);
			map.set(toDayKey(d), 0);
		}

		for (const t of transactions || []) {
			const d = new Date(t.created);
			const dd = new Date(d.getFullYear(), d.getMonth(), d.getDate());
			if (dd < start || dd > today) continue;

			const key = toDayKey(dd);
			const amt = Number(t.amount) || 0;

			// net: incoming positive, outgoing negative
			if (t.receiver === account?.id) map.set(key, (map.get(key) || 0) + amt);
			if (t.sender === account?.id) map.set(key, (map.get(key) || 0) - amt);
		}

		return [...map.entries()].map(([day, value]) => ({ day, value }));
	})();

	// Build simple bar chart geometry
	$: netBars = (() => {
		const data = netByDay || [];
		const values = data.map((d) => d.value);
		const maxAbs = Math.max(1, ...values.map((v) => Math.abs(v)));

		return data.map((d, i) => {
			const h = Math.abs(d.value) / maxAbs; // 0..1
			const sign = d.value >= 0 ? 1 : -1;
			return { ...d, i, h, sign };
		});
	})();

	// Running balance line chart over last N transactions
	$: runningSeries = (() => {
		if (!account?.id || !Array.isArray(transactions)) return [];

		const slice = transactions;

		let bal = Number(account.balance) || 0;
		const points: { i: number; bal: number; created: string }[] = [];

		// reconstruct "before oldest"
		const reversed = [...slice].reverse();
		for (let i = 0; i < reversed.length; i++) {
			const t = reversed[i];
			const amt = Number(t.amount) || 0;

			const delta = t.receiver === account.id ? amt : t.sender === account.id ? -amt : 0;

			const prev = bal - delta;
			bal = prev;
		}

		// forward series
		let forwardBal = bal;
		for (let i = 0; i < reversed.length; i++) {
			const t = reversed[i];
			const amt = Number(t.amount) || 0;
			const delta = t.receiver === account.id ? amt : t.sender === account.id ? -amt : 0;
			forwardBal += delta;
			points.push({ i, bal: forwardBal, created: t.created });
		}

		return points;
	})();

	$: linePath = (() => {
		const pts = runningSeries || [];
		if (pts.length < 2) return '';

		const min = Math.min(...pts.map((p) => p.bal));
		const max = Math.max(...pts.map((p) => p.bal));
		const span = Math.max(1, max - min);

		// viewBox 0 0 400 120
		const W = 400;
		const H = 120;
		const padX = 12;
		const padY = 12;
		const innerW = W - padX * 2;
		const innerH = H - padY * 2;

		const x = (i: number) => padX + (i / (pts.length - 1)) * innerW;
		const y = (v: number) => padY + (1 - (v - min) / span) * innerH;

		let d = `M ${x(0)} ${y(pts[0].bal)}`;
		for (let i = 1; i < pts.length; i++) d += ` L ${x(i)} ${y(pts[i].bal)}`;
		return d;
	})();

	// Chart.js data for bar chart
	$: barChartLabels = netByDay.map((d) => {
		const parts = d.day.split('-');
		return `${parts[2]}.${parts[1]}`;
	});

	$: barChartValues = netByDay.map((d) => Number(d.value) || 0);

	// Chart.js data for line chart
	$: lineChartLabels = runningSeries.map((p) => fmtDate(p.created));
	$: lineChartValues = runningSeries.map((p) => Number(p.bal) || 0);

	// -----------------------------
	// MAIN FETCH LOGIC (UNCHANGED)
	// -----------------------------
	onMount(async () => {
		if (!browser) return;
		try {
			localUser = getAuthStore().record;
			if (!localUser) {
				error = 'Not logged in to PocketBase.';
				loading = false;
				return;
			}

			let acc = null;
			try {
				acc = await db.collection('accounts').getFirstListItem(`user="${localUser.id}"`);
			} catch (err) {
				console.warn('No account found.');
			}

			if (!acc) {
				account = {
					owner: localUser?.name || 'Unknown User',
					iban: '—',
					balance: 0,
					frozen: false
				};
				loading = false;
				return;
			}

			const iban = acc.id
				.replace('-', '')
				.padEnd(22, '0')
				.replace(/(.{4})/g, '$1 ')
				.trim();

			account = {
				id: acc.id,
				owner: localUser?.name || 'User',
				iban,
				balance: acc.balance,
				frozen: acc.frozen
			};

			loading = false;

			try {
				let senderTransactions = await db.collection('public_transactions').getFullList({
					filter: `sender="${acc.id}"`,
					sort: '-created',
					expand: 'sender,receiver'
				});
				let receiverTransactions = await db.collection('public_transactions').getFullList({
					filter: `receiver="${acc.id}"`,
					sort: '-created',
					expand: 'sender,receiver'
				});
				transactions = [...senderTransactions, ...receiverTransactions];

				transactions.sort((a, b) => new Date(b.created).getTime() - new Date(a.created).getTime());

				await Promise.all(
					transactions.map(async (tx, index) => {
						let pureDescription = tx.description || '';
						let isQrCode = false;
						// Does the system contain ##QR_CODE_AUTHORIZATION_ACKNOWLEDGED##
						isQrCode =
							tx.system?.includes('##QR_CODE_AUTHORIZATION_ACKNOWLEDGED##') && tx.sender === acc.id
								? true
								: false;

						transactions[index] = {
							...tx,
							pureDescription,
							isQrCode
						};
					})
				).then(() => {
					loadingTransactions = false;
				});
			} catch (txErr) {
				console.error('Could not load transactions:', txErr);
			}

			loadingTransactions = false;
		} catch (err: any) {
			console.error(err);
			error = err.message;
			loading = false;
		}

		// Check for wealth tax liability
		const lastWealthCheck = await readCache('lastWealthCheck', 5 * 60 * 1000);
		const lastWealthBalance = await readCache('lastWealthBalance', 5 * 60 * 1000);
		const lastWealthAffected = await readCache('lastWealthAffected', 5 * 60 * 1000);
		const lastWealthDeductable = await readCache('lastWealthDeductable', 5 * 60 * 1000);

		if (lastWealthCheck == null || lastWealthBalance == null || lastWealthAffected == null) {
			await calculateWealthTax();
		}

		if (account && account.balance !== undefined) {
			if (lastWealthCheck && lastWealthBalance !== undefined) {
				const balanceAtLastCheck = Number(lastWealthBalance);

				// Re-check if balance has changed by more than 500 or if it's been more than 60 minutes
				if (balanceAtLastCheck != account.balance?.toString()) {
					await calculateWealthTax();
				} else {
					wealthTaxAffected = lastWealthAffected === '1' ? true : false;
					wealthTaxToDeduct = lastWealthDeductable ? Number(lastWealthDeductable) : 0;
				}
			}
		}
	});
	async function calculateWealthTax() {
		const rsp = await fetch(`${server}/api/calculate-wealth-tax`, {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
				Authorization: `${getAuthStore().token}`
			},
			body: JSON.stringify({
				accountId: account.id
			})
		});
		const data = await rsp.json();
		wealthTaxAffected = data.liable;
		wealthTaxToDeduct = data.wealthTax || 0;

		writeCache('lastWealthAffected', wealthTaxAffected ? '1' : '0');
		writeCache('lastWealthBalance', account.balance?.toString() || '0');
		writeCache('lastWealthCheck', new Date().toISOString());
		writeCache('lastWealthDeductable', wealthTaxToDeduct.toString());
	}
</script>

<title>Dashboard</title>
<Site>
	<div class="p-4 md:p-6 space-y-5">
		{#if error}
			<div class="alert alert-error shadow-lg border border-base-300">
				<span class="font-semibold">{error}</span>
			</div>
		{/if}

		{#if loading}
			<div class="card bg-base-200 shadow-xl border border-base-300">
				<div class="card-body">
					<div class="flex items-center gap-3 opacity-80">
						<span class="loading loading-spinner loading-sm"></span>
						<span>Lade Konto...</span>
					</div>
				</div>
			</div>
		{:else}
			<!-- HEADER -->
			<div class="card bg-base-200 shadow-xl border border-base-300 overflow-hidden">
				<div class="card-body">
					<div class="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
						<div class="min-w-0">
							<div class="flex items-center gap-2">
								<div
									class="w-10 h-10 rounded-2xl bg-base-300 border border-base-300 flex items-center justify-center"
								>
									<Sparkles size={18} />
								</div>
								<div class="min-w-0">
									<h1 class="text-2xl md:text-3xl font-extrabold leading-tight truncate">
										Banking Dashboard
									</h1>
									<p class="opacity-70 text-sm truncate">
										Willkommen zurück, <span class="font-semibold">{account.owner}</span>
										<span class="opacity-50">•</span>
										<span class="font-mono text-xs opacity-70">{account.iban}</span>
									</p>
								</div>
							</div>

							{#if account?.frozen}
								<div
									class="mt-3 inline-flex items-center gap-2 px-3 py-1 rounded-full border border-warning/30 bg-warning/10 text-warning text-xs font-semibold"
								>
									<ShieldAlert size={14} />
									Konto ist aktuell eingefroren
								</div>
							{/if}
						</div>

						<div class="flex flex-wrap gap-2">
							<button class="btn btn-error shadow-lg" on:click={() => goto('/transfer')}>
								<Send size={16} />
								Neue Überweisung
							</button>
						</div>
					</div>

					<!-- ACCOUNT STRIP -->
					<div class="mt-4 grid grid-cols-1 lg:grid-cols-3 gap-3">
						<div class="rounded-2xl border border-base-300 bg-base-300/40 p-4 lg:col-span-3">
							<div class="flex items-start justify-between gap-4">
								<div class="min-w-0">
									<div class="flex items-center gap-2 opacity-70 text-xs">
										<Wallet size={14} />
										<span>Kontostand</span>
										<span class="badge badge-outline badge-sm">Echtzeit</span>
										{#if wealthTaxAffected}
											<!--
                                            <div class="ml-4 px-2 py-1 rounded-full bg-error/10 border border-error/20 text-error text-xs font-semibold">
                                                -{fmtMoney(wealthTaxToDeduct)} <Currency size={12} class="inline -mt-0.5" />
                                                <span class="ml-1">Wealth Tax</span>
                                            </div>
                                            -->
											<div
												class="badge badge-error tooltip tooltip-bottom"
												data-tip="Vermögensteuer fällig! Achtung: Dein Konto ist von der täglichen Vermögensteuer betroffen - Derzeitige Reduktion: -{fmtMoney(
													wealthTaxToDeduct
												)}"
											>
												<TriangleAlert size={15} /> Vermögensteuer fällig
											</div>
										{/if}
									</div>
									<div class="mt-2 flex items-end gap-2">
										<div class="text-3xl md:text-4xl font-extrabold leading-none">
											{fmtMoney(Number(account.balance) || 0)}
										</div>
										<div class="pb-1">
											<Currency size={26} />
										</div>
									</div>
									<div class="mt-2 text-xs opacity-60">
										Account ID: <span class="font-mono">{account.id || '-'}</span>
									</div>
								</div>
								<div
									class="hidden md:flex items-center justify-center w-12 h-12 rounded-2xl bg-base-200 border border-base-300"
								>
									<Activity size={18} />
								</div>
							</div>
						</div>
					</div>
				</div>
			</div>

			<!-- KPIs -->
			<div class="grid grid-cols-1 md:grid-cols-3 gap-4">
				<div class="card bg-base-200 shadow-xl border border-base-300">
					<div class="card-body">
						<div class="flex items-center justify-between">
							<div class="flex items-center gap-3">
								<div
									class="w-10 h-10 rounded-2xl bg-success/10 border border-success/20 flex items-center justify-center text-success"
								>
									<ArrowDownLeft size={18} />
								</div>
								<div>
									<div class="text-xs opacity-70">Einnahmen</div>
									<div class="text-xl font-extrabold text-success">
										+{fmtMoney(incomingSum)}
									</div>
								</div>
							</div>
							<div class="text-xs opacity-60">{incoming.length} Tx</div>
						</div>
					</div>
				</div>

				<div class="card bg-base-200 shadow-xl border border-base-300">
					<div class="card-body">
						<div class="flex items-center justify-between">
							<div class="flex items-center gap-3">
								<div
									class="w-10 h-10 rounded-2xl bg-error/10 border border-error/20 flex items-center justify-center text-error"
								>
									<ArrowUpRight size={18} />
								</div>
								<div>
									<div class="text-xs opacity-70">Ausgaben</div>
									<div class="text-xl font-extrabold text-error">
										-{fmtMoney(outgoingSum)}
									</div>
								</div>
							</div>
							<div class="text-xs opacity-60">{outgoing.length} Tx</div>
						</div>
					</div>
				</div>

				<div class="card bg-base-200 shadow-xl border border-base-300">
					<div class="card-body">
						<div class="flex items-center justify-between">
							<div class="flex items-center gap-3">
								<div
									class={'w-10 h-10 rounded-2xl border flex items-center justify-center ' +
										(netSum >= 0
											? 'bg-success/10 border-success/20 text-success'
											: 'bg-error/10 border-error/20 text-error')}
								>
									<Activity size={18} />
								</div>
								<div>
									<div class="text-xs opacity-70">Gesamt (Delta)</div>
									<div
										class={'text-xl font-extrabold ' +
											(netSum >= 0 ? 'text-success' : 'text-error')}
									>
										{netSum >= 0 ? '+' : '-'}{fmtMoney(Math.abs(netSum))}
									</div>
								</div>
							</div>
							<div class="text-xs opacity-60">30 Tage</div>
						</div>
					</div>
				</div>
			</div>

			<!-- CHARTS -->
			<div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
				<!-- NET 7D BAR CHART -->
				<div class="card bg-base-200 shadow-xl border border-base-300 xl:col-span-2">
					<div class="card-body">
						<div class="flex items-center justify-between gap-4">
							<div>
								<h2 class="card-title flex items-center gap-2">
									<Activity size={18} />
									Einnahmen vs. Ausgaben
								</h2>
								<p class="text-xs opacity-60">
									Letzte 7 Tage • + Einnahmen, − Ausgaben (Netto pro Tag)
								</p>
							</div>
							<div class="badge badge-outline">7D</div>
						</div>

						{#if loadingTransactions}
							<div class="mt-4 rounded-2xl border border-base-300 bg-base-300/40 p-4">
								<div class="flex items-center gap-3 opacity-80">
									<span class="loading loading-spinner loading-sm"></span>
									<span class="text-sm">Lade Statistiken...</span>
								</div>
								<div class="mt-3 skeleton h-24 w-full"></div>
							</div>
						{:else}
							<div class="mt-4 rounded-2xl border border-base-300 bg-base-300/40 p-4">
								<BarChart labels={barChartLabels} values={barChartValues} />
								<div class="flex items-center justify-between text-[11px] opacity-60 mt-2">
									<span class="font-mono">{netByDay?.[0]?.day}</span>
									<span class="font-mono">{netByDay?.[netByDay.length - 1]?.day}</span>
								</div>
							</div>
						{/if}
					</div>
				</div>

				<!-- RUNNING BALANCE LINE -->
				<div class="card bg-base-200 shadow-xl border border-base-300">
					<div class="card-body">
						<div class="flex items-center justify-between gap-4">
							<div>
								<h2 class="card-title flex items-center gap-2">
									<Wallet size={18} />
									Laufender Kontostand
								</h2>
								<p class="text-xs opacity-60">
									Letzte {Math.min(40, transactions?.length || 0)} Transaktionen
								</p>
							</div>
							<div class="badge badge-outline">Live</div>
						</div>

						<div class="mt-4 rounded-2xl border border-base-300 bg-base-300/40 p-4">
							{#if !lineChartValues || lineChartValues.length < 2}
								<p class="text-sm opacity-70">
									Es sind nicht genug Transaktionen für diese Übersicht vorhanden.
								</p>
							{:else}
								<LineChart labels={lineChartLabels} values={lineChartValues} />
								<div class="flex items-center justify-between text-[11px] opacity-60 mt-2">
									<span>{runningSeries?.[0]?.created ? fmtDate(runningSeries[0].created) : ''}</span
									>
									<span
										>{runningSeries?.[runningSeries.length - 1]?.created
											? fmtDate(runningSeries[runningSeries.length - 1].created)
											: ''}</span
									>
								</div>
							{/if}
						</div>
					</div>
				</div>
			</div>

			<!-- TRANSACTIONS: INCOMING / OUTGOING -->
			<div class="grid grid-cols-1 xl:grid-cols-2 gap-4">
				<!-- INCOMING -->
				<div class="card bg-base-200 shadow-xl border border-base-300">
					<div class="card-body">
						<div class="flex items-center justify-between gap-3">
							<h2 class="card-title flex items-center gap-2">
								<ArrowDownLeft size={18} class="text-success" />
								Eingehend
							</h2>
							<span class="badge badge-success badge-outline">
								+{fmtMoney(incomingSum)}
								<Currency />
							</span>
						</div>

						{#if loadingTransactions}
							<div class="mt-3 space-y-3">
								<div class="skeleton h-16 w-full"></div>
								<div class="skeleton h-16 w-full"></div>
								<div class="skeleton h-16 w-full"></div>
							</div>
						{:else if incoming.length === 0}
							<p class="opacity-60 mt-3">Keine eingehenden Transaktionen vorhanden.</p>
						{:else}
							<div class="space-y-3 mt-4">
								{#each incoming.slice(0, 3) as t}
									<button
										class="w-full text-left rounded-2xl border border-base-300 bg-base-300/40 hover:bg-base-300 transition p-4 flex items-center justify-between gap-4"
										on:click={() => goto(`/transactions?id=${t.id}`)}
									>
										<div class="min-w-0">
											<div class="flex items-center gap-2 min-w-0">
												<div
													class="w-9 h-9 rounded-2xl bg-success/10 border border-success/20 text-success flex items-center justify-center shrink-0"
												>
													<ArrowDownLeft size={16} />
												</div>
												<div class="min-w-0">
													<p class="font-semibold truncate">
														{t.pureDescription || 'Eingehende Transaktion'}
													</p>
													<p class="opacity-70 text-xs truncate">
														{fmtDate(t.created)} • Von
														<span class="font-mono"
															>{t.sender_is_company
																? t.sender_company_name || 'Unknown'
																: t.sender_name || 'Unknown'}</span
														>
														• {t.sender}
														{#if t.isQrCode}
															<span class="badge badge-info badge-xs ml-2">
																<QrCode size={12} class="inline -mt-0.5" />
																QR
															</span>
														{/if}
													</p>
												</div>
											</div>
										</div>

										<div class="text-right shrink-0">
											<div
												class="font-extrabold text-success whitespace-nowrap flex items-center gap-2 justify-end"
											>
												+{fmtMoney(Math.abs(Number(t.amount) || 0))}
												<Currency />
											</div>
										</div>
									</button>
								{/each}
							</div>
						{/if}
					</div>
				</div>

				<!-- OUTGOING -->
				<div class="card bg-base-200 shadow-xl border border-base-300">
					<div class="card-body">
						<div class="flex items-center justify-between gap-3">
							<h2 class="card-title flex items-center gap-2">
								<ArrowUpRight size={18} class="text-error" />
								Ausgehend
							</h2>
							<span class="badge badge-error badge-outline">
								-{fmtMoney(outgoingSum)}
								<Currency />
							</span>
						</div>

						{#if loadingTransactions}
							<div class="mt-3 space-y-3">
								<div class="skeleton h-16 w-full"></div>
								<div class="skeleton h-16 w-full"></div>
								<div class="skeleton h-16 w-full"></div>
							</div>
						{:else if outgoing.length === 0}
							<p class="opacity-60 mt-3">Keine ausgehenden Transaktionen vorhanden.</p>
						{:else}
							<div class="space-y-3 mt-4">
								{#each outgoing.slice(0, 3) as t}
									<button
										class="w-full text-left rounded-2xl border border-base-300 bg-base-300/40 hover:bg-base-300 transition p-4 flex items-center justify-between gap-4"
										on:click={() => goto(`/transactions?id=${t.id}`)}
									>
										<div class="min-w-0">
											<div class="flex items-center gap-2 min-w-0">
												<div
													class="w-9 h-9 rounded-2xl bg-error/10 border border-error/20 text-error flex items-center justify-center shrink-0"
												>
													<ArrowUpRight size={16} />
												</div>
												<div class="min-w-0">
													<p class="font-semibold truncate">
														{@html t.pureDescription || 'Ausgehende Transaktion'}
													</p>
													<p class="opacity-70 text-xs truncate">
														{fmtDate(t.created)} • An
														<span class="font-mono"
															>{t.receiver_is_company
																? t.receiver_company_name || 'Unknown'
																: t.receiver_name || 'Unknown'}</span
														>
														• {t.receiver}
														{#if t.isQrCode}
															<span class="badge badge-info badge-xs ml-2">
																<QrCode size={12} class="inline -mt-0.5" />
																QR
															</span>
														{/if}
													</p>
												</div>
											</div>
										</div>

										<div class="text-right shrink-0">
											<div
												class="font-extrabold text-error whitespace-nowrap flex items-center gap-2 justify-end"
											>
												-{fmtMoney(Math.abs(Number(t.amount) || 0))}
												<Currency />
											</div>
										</div>
									</button>
								{/each}
							</div>
						{/if}
					</div>
				</div>
			</div>

			<!-- ALL RECENT TRANSACTIONS -->
			<div class="card bg-base-200 shadow-xl border border-base-300">
				<div class="card-body">
					<div class="flex flex-col md:flex-row md:items-center md:justify-between gap-2">
						<h2 class="card-title flex items-center gap-2">
							<ReceiptText size={18} />
							Letzte Transaktionen
						</h2>
						<div class="text-xs opacity-60">{transactions?.length || 0} insgesamt</div>
					</div>

					{#if loadingTransactions}
						<div class="mt-4 space-y-3">
							<div class="skeleton h-12 w-full"></div>
							<div class="skeleton h-12 w-full"></div>
							<div class="skeleton h-12 w-full"></div>
							<div class="skeleton h-12 w-full"></div>
						</div>
					{:else if transactions.length === 0}
						<p class="opacity-60 mt-3">Noch keine Transaktionen.</p>
					{:else}
						<div class="mt-4 overflow-x-auto">
							<table class="table table-zebra">
								<thead>
									<tr>
										<th>Datum</th>
										<th>Beschreibung</th>
										<th>Richtung</th>
										<th class="text-right">Betrag</th>
									</tr>
								</thead>
								<tbody>
									{#each transactions.slice(0, 5) as t}
										<tr
											on:click={() => goto(`/transactions?id=${t.id}`)}
											class="cursor-pointer hover:bg-base-300"
										>
											<td class="whitespace-nowrap text-sm opacity-80">
												{fmtDate(t.created)}
											</td>
											<td class="min-w-65">
												<div class="font-semibold truncate">
													{@html t.pureDescription || 'Transaktion'}
												</div>
												<div class="flex items-center gap-2 text-xs opacity-60 font-mono truncate">
													<span class="truncate">{t.sender} → {t.receiver}</span>
													{#if t.isQrCode}
														<span class="badge badge-info badge-xs">
															<QrCode size={12} class="inline -mt-0.5" />
															QR
														</span>
													{/if}
												</div>
											</td>
											<td class="text-sm">
												<span
													class={'inline-flex items-center gap-2 ' +
														(t.sender === account.id ? 'text-error' : 'text-success')}
												>
													{#if t.sender === account.id}
														<ArrowUpRight size={14} />
														Ausgehend
													{:else}
														<ArrowDownLeft size={14} />
														Eingehend
													{/if}
												</span>
											</td>
											<td
												class={'text-right font-extrabold whitespace-nowrap ' +
													(t.sender === account.id ? 'text-error' : 'text-success')}
											>
												<div class="flex items-center gap-2 justify-end">
													<span>
														{t.sender === account.id ? '-' : '+'}
														{#if t.sender === account.id}
															{fmtMoney(t.amount)}
														{:else}
															{fmtMoney(t.received)}
														{/if}
													</span>
													<Currency />
												</div>
											</td>
										</tr>
									{/each}
								</tbody>
							</table>
						</div>
					{/if}
				</div>
			</div>
		{/if}
	</div>
</Site>

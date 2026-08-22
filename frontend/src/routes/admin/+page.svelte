<script lang="ts">
	import AdminSite from '$modules/AdminSite.svelte';
	import { onDestroy, onMount, tick } from 'svelte';
	import db from '$lib/connections';
	import Currency from '$modules/Currency.svelte';
	import {
		Activity,
		SearchIcon,
		ShieldCheckIcon,
		SnowflakeIcon,
		CircleDollarSignIcon,
		UserIcon,
		RefreshCcwIcon,
		Forklift,
		UserKey
	} from '@lucide/svelte';
	import { getAuthStore } from '$lib/auth';
	import { browser } from '$app/environment';
	import { getFile } from '$lib/settings';
	import TransactionStatistics from '$modules/TransactionStatistics.svelte';

	type Account = any;

	type StatisticKey = 'inflation' | 'gdp';
	type StatisticView = 'hourly' | 'daily';
	type StatisticChartType = 'line' | 'bar' | 'area';
	type DailyAggregation = 'average' | 'sum' | 'last';

	type StatisticRecord = {
		id: string;
		key: string;
		value?: number | string;
		timestamp: string;
		created?: string;
		[key: string]: any;
	};

	type StatisticPoint = {
		label: string;
		value: number;
		timestamp: Date;
	};

	/*
	 * Change this if your numeric stats field has another name,
	 * for example "amount".
	 */
	const STAT_VALUE_FIELD = 'value';

	/*
	 * false: 5.2 means 5.2%
	 * true:  0.052 means 5.2%
	 */
	const INFLATION_STORED_AS_FRACTION = false;

	const hours = Array.from({ length: 24 }, (_, index) => index);

	const numberFormatter = new Intl.NumberFormat('de-DE', {
		maximumFractionDigits: 2
	});

	const axisFormatter = new Intl.NumberFormat('de-DE', {
		notation: 'compact',
		maximumFractionDigits: 1
	});

	const hourlyDateFormatter = new Intl.DateTimeFormat('de-DE', {
		day: '2-digit',
		month: '2-digit',
		hour: '2-digit',
		minute: '2-digit'
	});

	const dailyDateFormatter = new Intl.DateTimeFormat('de-DE', {
		day: '2-digit',
		month: '2-digit',
		year: 'numeric'
	});

	let accounts: Account[] = [];
	let page = 1;
	let perPage = 30;
	let maxPage = 1;
	let filter = '';

	let loading = false;
	let error: string | null = null;

	let activeUserName = '';
	const nf = new Intl.NumberFormat('de-DE');
	let debounceTimer: ReturnType<typeof setTimeout> | null = null;

	let logoLight: string | null = null;

	// System totals
	let totalsLoaded = false;
	let totalsLoading = false;
	let totalsError: string | null = null;

	let totalMoney = 0;
	let totalFrozenMoney = 0;
	let totalMoneyInCirculation = 0;
	let totalRegistrated = 0;
	let totalUsers = 0;
	let transactionsToday = 0;
	let movedMoney = 0;

	// Economic statistics
	let statisticRecords: StatisticRecord[] = [];
	let statisticsLoaded = false;
	let statisticsLoading = false;
	let statisticsError: string | null = null;

	let statisticView: StatisticView = 'hourly';
	let dailyAggregation: DailyAggregation = 'average';

	let startHour = 0;
	let endHour = 23;

	let inflationChartType: StatisticChartType = 'line';
	let gdpChartType: StatisticChartType = 'bar';

	let inflationSeries: StatisticPoint[] = [];
	let gdpSeries: StatisticPoint[] = [];

	let latestInflation: number | null = null;
	let latestGdp: number | null = null;

	let inflationCanvas: HTMLCanvasElement | null = null;
	let gdpCanvas: HTMLCanvasElement | null = null;

	let ChartJS: any = null;
	let inflationChart: any = null;
	let gdpChart: any = null;
	let chartRenderTimer: ReturnType<typeof setTimeout> | null = null;

	type StatisticDisplayMode = 'value' | 'change';

	let inflationDisplayMode: StatisticDisplayMode = 'value';
	let gdpDisplayMode: StatisticDisplayMode = 'value';

	let inflationDisplaySeries: StatisticPoint[] = [];
	let gdpDisplaySeries: StatisticPoint[] = [];

	$: inflationSeries = buildStatisticSeries(
		'inflation',
		statisticRecords,
		statisticView,
		startHour,
		endHour,
		dailyAggregation
	);

	$: inflationDisplaySeries = applyDisplayMode(
		inflationSeries,
		inflationDisplayMode
	);

	$: gdpDisplaySeries = applyDisplayMode(
		gdpSeries,
		gdpDisplayMode
	);

	$: latestInflation =
		inflationDisplaySeries.length > 0
			? inflationDisplaySeries[inflationDisplaySeries.length - 1].value
			: null;

	$: latestGdp =
		gdpDisplaySeries.length > 0
			? gdpDisplaySeries[gdpDisplaySeries.length - 1].value
			: null;

	$: if (browser && ChartJS && statisticsLoaded) {
		queueChartRender(
			inflationDisplaySeries,
			gdpDisplaySeries,
			inflationChartType,
			gdpChartType,
			inflationDisplayMode,
			gdpDisplayMode
		);
	}

	onMount(async () => {
		if (!browser) return;

		logoLight = await getFile('logo');

		const authStore = getAuthStore();
		activeUserName = authStore?.record?.name ?? 'Admin';

		await Promise.all([
			fetchAccounts(),
			fetchStatistics()
		]);

		try {
			const chartModule = await import('chart.js/auto');
			ChartJS = chartModule.default;
		} catch (chartError: any) {
			statisticsError =
				chartError?.message ??
				'Chart.js konnte nicht geladen werden.';
		}
	});

	onDestroy(() => {
		if (debounceTimer) {
			clearTimeout(debounceTimer);
		}

		if (chartRenderTimer) {
			clearTimeout(chartRenderTimer);
		}

		inflationChart?.destroy();
		gdpChart?.destroy();
	});

	async function fetchAccounts() {
		loading = true;
		error = null;

		try {
			const safe = (filter ?? '')
				.replaceAll('"', '\\"')
				.trim();

			const rsp = await db.collection('accounts').getList(page, perPage, {
				sort: '+user.name',
				filter: safe
					? `user.id ~ "${safe}" || user.name ~ "${safe}" || user.email ~ "${safe}" || id ~ "${safe}"`
					: '',
				expand: 'user,user.data'
			});

			maxPage = rsp.totalPages;
			accounts = rsp.items;
		} catch (e: any) {
			error = e?.message ?? 'Fehler beim Laden der Konten.';
		} finally {
			loading = false;
		}
	}

	async function fetchStatistics() {
		statisticsLoading = true;
		statisticsError = null;

		try {
			const records = await db
				.collection('stats')
				.getFullList({
					filter: 'key = "inflation" || key = "gdp"',
					sort: '+timestamp'
				});

			statisticRecords = records as unknown as StatisticRecord[];
			statisticsLoaded = true;
		} catch (e: any) {
			statisticsError =
				e?.message ??
				'Fehler beim Laden der Wirtschaftsstatistik.';

			statisticRecords = [];
			statisticsLoaded = true;
		} finally {
			statisticsLoading = false;
		}
	}

	function onFilterInput(value: string) {
		filter = value;

		if (debounceTimer) {
			clearTimeout(debounceTimer);
		}

		debounceTimer = setTimeout(async () => {
			page = 1;
			await fetchAccounts();
		}, 250);
	}

	async function pageUp() {
		if (page >= maxPage) return;

		page += 1;
		await fetchAccounts();
	}

	async function pageDown() {
		if (page <= 1) return;

		page -= 1;
		await fetchAccounts();
	}

	function formatMoney(value: number): string {
		return (value ?? 0)
			.toFixed(2)
			.toString()
			.replace('.', ',')
			.replace(/\B(?=(\d{3})+(?!\d))/g, '.');
	}

	async function calculateTotals() {
		totalsLoading = true;
		totalsError = null;

		try {
			const listMoney = await db
				.collection('accounts')
				.getFullList({
					fields: 'balance,frozen'
				});

			let total = 0;
			let frozen = 0;
			let circulating = 0;

			for (const account of listMoney as any[]) {
				const balance = Number(account?.balance ?? 0);

				total += balance;

				if (account?.frozen) {
					frozen += balance;
				} else {
					circulating += balance;
				}
			}

			totalMoney = Number(total.toFixed(2));
			totalFrozenMoney = Number(frozen.toFixed(2));
			totalMoneyInCirculation = Number(circulating.toFixed(2));

			const listUsers = await db
				.collection('user_data')
				.getFullList({
					fields: 'registered'
				});

			totalRegistrated = 0;

			for (const user of listUsers as any[]) {
				if (user?.registered == 1) {
					totalRegistrated += 1;
				}
			}

			totalUsers = listUsers.length;

			const startOfToday = new Date();
			startOfToday.setHours(0, 0, 0, 0);

			const listTransactions = await db
				.collection('transactions')
				.getFullList({
					filter: `created >= "${startOfToday.toISOString()}"`
				});

			transactionsToday = listTransactions.length;

			movedMoney = listTransactions.reduce(
				(sum, transaction) =>
					sum + Number(transaction.amount ?? 0),
				0
			);

			totalsLoaded = true;
		} catch (e: any) {
			totalsError =
				e?.message ??
				'Fehler bei der Berechnung.';
		} finally {
			totalsLoading = false;
		}
	}

	function parseStatisticDate(value: string | undefined): Date | null {
		if (!value) return null;

		let date = new Date(value);

		if (!Number.isNaN(date.getTime())) {
			return date;
		}

		// Fallback for timestamps such as "2026-07-17 10:00:00.000Z"
		date = new Date(value.replace(' ', 'T'));

		return Number.isNaN(date.getTime())
			? null
			: date;
	}

	function readStatisticValue(
		record: StatisticRecord,
		key: StatisticKey
	): number | null {
		const rawValue = record[STAT_VALUE_FIELD];

		let value: number;

		if (typeof rawValue === 'number') {
			value = rawValue;
		} else {
			value = Number(
				String(rawValue ?? '')
					.trim()
					.replace(',', '.')
			);
		}

		if (!Number.isFinite(value)) {
			return null;
		}

		if (
			key === 'inflation' &&
			INFLATION_STORED_AS_FRACTION
		) {
			value *= 100;
		}

		return value;
	}

	function hourIsIncluded(
		hour: number,
		from: number,
		to: number
	): boolean {
		/*
		 * Normal range:
		 * 08–12 includes 08, 09, 10, 11 and 12.
		 *
		 * Overnight range:
		 * 22–06 includes 22–23 and 00–06.
		 */
		if (from <= to) {
			return hour >= from && hour <= to;
		}

		return hour >= from || hour <= to;
	}

	function getLocalDateKey(date: Date): string {
		const year = date.getFullYear();
		const month = String(date.getMonth() + 1).padStart(2, '0');
		const day = String(date.getDate()).padStart(2, '0');

		return `${year}-${month}-${day}`;
	}

	function aggregateValues(
		values: Array<{
			value: number;
			timestamp: Date;
		}>,
		aggregation: DailyAggregation
	): number {
		if (values.length === 0) return 0;

		if (aggregation === 'sum') {
			return values.reduce(
				(sum, item) => sum + item.value,
				0
			);
		}

		if (aggregation === 'last') {
			const sorted = [...values].sort(
				(a, b) =>
					a.timestamp.getTime() -
					b.timestamp.getTime()
			);

			return sorted[sorted.length - 1].value;
		}

		const total = values.reduce(
			(sum, item) => sum + item.value,
			0
		);

		return total / values.length;
	}

	function buildStatisticSeries(
		key: StatisticKey,
		records: StatisticRecord[],
		view: StatisticView,
		fromHour: number,
		toHour: number,
		aggregation: DailyAggregation
	): StatisticPoint[] {
		const parsedRecords = records
			.filter((record) => record.key === key)
			.map((record) => {
				const timestamp = parseStatisticDate(
					record.timestamp ?? record.created
				);

				const value = readStatisticValue(record, key);

				return {
					timestamp,
					value
				};
			})
			.filter(
				(
					item
				): item is {
					timestamp: Date;
					value: number;
				} =>
					item.timestamp !== null &&
					item.value !== null
			)
			.sort(
				(a, b) =>
					a.timestamp.getTime() -
					b.timestamp.getTime()
			);

		/*
		* GDP:
		* Insert zero for every missing hour.
		*
		* Inflation:
		* Keep only the actually recorded values.
		*/
		const matchingRecords =
			key === 'gdp'
				? fillMissingGdpHours(
						parsedRecords,
						fromHour,
						toHour
					)
				: parsedRecords.filter((item) =>
						hourIsIncluded(
							item.timestamp.getHours(),
							fromHour,
							toHour
						)
					);

		if (view === 'hourly') {
			return matchingRecords.map((item) => ({
				label: hourlyDateFormatter.format(
					item.timestamp
				),
				value: item.value,
				timestamp: item.timestamp
			}));
		}

		const grouped = new Map<
			string,
			{
				date: Date;
				values: Array<{
					value: number;
					timestamp: Date;
				}>;
			}
		>();

		for (const item of matchingRecords) {
			const dateKey = getLocalDateKey(item.timestamp);

			if (!grouped.has(dateKey)) {
				grouped.set(dateKey, {
					date: new Date(
						item.timestamp.getFullYear(),
						item.timestamp.getMonth(),
						item.timestamp.getDate()
					),
					values: []
				});
			}

			grouped.get(dateKey)?.values.push(item);
		}

		return Array.from(grouped.values())
			.sort(
				(a, b) =>
					a.date.getTime() -
					b.date.getTime()
			)
			.map((group) => ({
				label: dailyDateFormatter.format(group.date),
				value: aggregateValues(
					group.values,
					aggregation
				),
				timestamp: group.date
			}));
	}

	function queueChartRender(
		_inflationSeries: StatisticPoint[],
		_gdpSeries: StatisticPoint[],
		_inflationType: StatisticChartType,
		_gdpType: StatisticChartType,
		_inflationDisplayMode: StatisticDisplayMode,
		_gdpDisplayMode: StatisticDisplayMode
	) {
		if (chartRenderTimer) {
			clearTimeout(chartRenderTimer);
		}

		chartRenderTimer = setTimeout(() => {
			void renderCharts();
		}, 0);
	}

	async function renderCharts() {
		await tick();

		inflationChart?.destroy();
		gdpChart?.destroy();

		inflationChart = null;
		gdpChart = null;

		if (!ChartJS) return;

		if (
			inflationCanvas &&
			inflationDisplaySeries.length > 0
		) {
			inflationChart = createChart(
				inflationCanvas,
				inflationDisplaySeries,
				'Inflation',
				'inflation',
				inflationChartType,
				inflationDisplayMode,
				'#E34747'
			);
		}

		if (
			gdpCanvas &&
			gdpDisplaySeries.length > 0
		) {
			gdpChart = createChart(
				gdpCanvas,
				gdpDisplaySeries,
				'Bruttoinlandsprodukt',
				'gdp',
				gdpChartType,
				gdpDisplayMode,
				'#F59E0B'
			);
		}
	}

	function createChart(
		canvas: HTMLCanvasElement,
		series: StatisticPoint[],
		label: string,
		key: StatisticKey,
		displayType: StatisticChartType,
		displayMode: StatisticDisplayMode,
		color: string
	) {
		const chartType =
			displayType === 'bar'
				? 'bar'
				: 'line';

		const showPoints =
			chartType === 'line' &&
			series.length <= 100;

		const datasetLabel =
			displayMode === 'change'
				? `${label} – Veränderung`
				: label;

		return new ChartJS(canvas, {
			type: chartType,
			data: {
				labels: series.map((point) => point.label),
				datasets: [
					{
						label: datasetLabel,
						data: series.map((point) => point.value),

						borderColor: color,
						backgroundColor:
							displayType === 'area'
								? `${color}30`
								: displayType === 'bar'
									? `${color}99`
									: `${color}18`,

						fill: displayType === 'area',
						tension: chartType === 'line' ? 0.3 : 0,

						borderWidth: 2,
						borderRadius: chartType === 'bar' ? 7 : 0,

						pointRadius: showPoints ? 2.5 : 0,
						pointHoverRadius: 5,
						pointBackgroundColor: color,
						pointBorderColor: color,

						maxBarThickness: 36,
						spanGaps: true
					}
				]
			},
			options: {
				responsive: true,
				maintainAspectRatio: false,

				animation: {
					duration: 250
				},

				interaction: {
					mode: 'index',
					intersect: false
				},

				plugins: {
					legend: {
						display: false
					},

					tooltip: {
						displayColors: false,

						callbacks: {
							label: (context: any) => {
								const value = Number(
									context.parsed?.y ??
										context.raw ??
										0
								);

								return `${datasetLabel}: ${formatStatisticValue(
									value,
									key,
									displayMode
								)}`;
							}
						}
					}
				},

				scales: {
					x: {
						grid: {
							display: false
						},

						ticks: {
							color: 'rgba(148, 163, 184, 0.8)',
							autoSkip: true,
							maxTicksLimit: 10,
							maxRotation: 0
						},

						border: {
							color: 'rgba(148, 163, 184, 0.2)'
						}
					},

					y: {
						beginAtZero: displayMode === 'change',

						grid: {
							color: 'rgba(148, 163, 184, 0.12)'
						},

						ticks: {
							color: 'rgba(148, 163, 184, 0.8)',

							callback: (value: string | number) =>
								formatAxisValue(
									Number(value),
									key,
									displayMode
								)
						},

						border: {
							display: false
						}
					}
				}
			}
		});
	}

	function formatStatisticValue(
		value: number,
		key: StatisticKey,
		displayMode: StatisticDisplayMode
	): string {
		if (displayMode === 'change') {
			const prefix = value > 0 ? '+' : '';

			return `${prefix}${numberFormatter.format(value)} %`;
		}

		if (key === 'inflation') {
			return `${numberFormatter.format(value)} %`;
		}

		return numberFormatter.format(value);
	}

	function formatAxisValue(
		value: number,
		key: StatisticKey,
		displayMode: StatisticDisplayMode
	): string {
		const formatted = axisFormatter.format(value);

		if (
			displayMode === 'change' ||
			key === 'inflation'
		) {
			return `${formatted} %`;
		}

		return formatted;
	}

	function formatHour(hour: number): string {
		return `${String(hour).padStart(2, '0')}:00`;
	}

	function aggregationLabel(): string {
		if (statisticView === 'hourly') {
			return 'Einzelne Stundenwerte';
		}

		switch (dailyAggregation) {
			case 'sum':
				return 'Summe pro Tag';

			case 'last':
				return 'Letzter Wert pro Tag';

			default:
				return 'Durchschnitt pro Tag';
		}
	}
	const HOUR_IN_MS = 60 * 60 * 1000;

	function fillMissingGdpHours(
		records: Array<{
			value: number;
			timestamp: Date;
		}>,
		fromHour: number,
		toHour: number
	): Array<{
		value: number;
		timestamp: Date;
	}> {
		if (records.length === 0) {
			return [];
		}

		const recordsByHour = new Map<
			number,
			{
				value: number;
				timestamp: Date;
			}
		>();

		for (const record of records) {
			const hourTimestamp =
				Math.floor(record.timestamp.getTime() / HOUR_IN_MS) *
				HOUR_IN_MS;

			/*
			* If multiple records exist for the same hour,
			* the latest encountered record is used.
			*/
			recordsByHour.set(hourTimestamp, {
				value: record.value,
				timestamp: new Date(hourTimestamp)
			});
		}

		const timestamps = Array.from(recordsByHour.keys());
		const firstHour = Math.min(...timestamps);
		const lastHour = Math.max(...timestamps);

		const result: Array<{
			value: number;
			timestamp: Date;
		}> = [];

		for (
			let currentHour = firstHour;
			currentHour <= lastHour;
			currentHour += HOUR_IN_MS
		) {
			const timestamp = new Date(currentHour);

			if (
				!hourIsIncluded(
					timestamp.getHours(),
					fromHour,
					toHour
				)
			) {
				continue;
			}

			result.push(
				recordsByHour.get(currentHour) ?? {
					value: 0,
					timestamp
				}
			);
		}

		return result;
	}
	function applyDisplayMode(
		series: StatisticPoint[],
		mode: StatisticDisplayMode
	): StatisticPoint[] {
		if (mode === 'value') {
			return series;
		}

		return series.map((point, index) => {
			if (index === 0) {
				return {
					...point,
					value: 0
				};
			}

			const previousValue = series[index - 1].value;

			/*
			* Percentage change from zero is undefined.
			* Returning zero prevents Infinity or NaN in the chart.
			*/
			if (previousValue === 0) {
				return {
					...point,
					value: 0
				};
			}

			const percentageChange =
				((point.value - previousValue) / Math.abs(previousValue)) *
				100;

			return {
				...point,
				value: Number(percentageChange.toFixed(2))
			};
		});
	}
	
</script>

<title>Admin – Dashboard</title>

<AdminSite>
	<div class="mx-auto w-full max-w-7xl space-y-5 px-4 py-8">
		<!-- Header -->
		<div class="flex items-center justify-between">
			<div class="flex items-center gap-4">
				<div
					class="grid h-12 w-12 place-items-center rounded-2xl border border-base-300 bg-base-100/70"
				>
					<img
						src={logoLight}
						alt="Logo"
						class="h-10 w-10 rounded-xl border border-base-300 object-contain shadow"
					/>
				</div>

				<div>
					<div class="flex items-center gap-2">
						<h1 class="text-2xl font-bold tracking-tight">
							Admin Dashboard
						</h1>

						<span
							class="rounded-full border border-base-300 bg-base-100/60 px-2.5 py-0.5 text-xs text-slate-400"
						>
							Nur Ansicht
						</span>
					</div>

					<p class="mt-0.5 text-sm text-slate-400">
						{db?.authStore?.record?.email} ·
						{activeUserName}
					</p>
				</div>
			</div>
		</div>

		{#if error || totalsError}
			<div
				class="rounded-2xl border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-200"
			>
				{error ?? totalsError}
			</div>
		{/if}

		<!-- System statistics -->
		<div
			class="overflow-hidden rounded-3xl border border-base-300 bg-base-100/50"
		>
			<div
				class="flex items-center justify-between border-b border-base-300 px-5 py-4"
			>
				<div class="flex items-center gap-3">
					<Activity size={16} class="text-slate-400" />

					<span class="font-semibold">
						Systemstatistik
					</span>

					<span class="text-xs text-slate-500">
						Berechnung erfolgt auf Anfrage
					</span>
				</div>

				<button
					class="flex h-9 items-center gap-2 rounded-xl border border-base-300 bg-base-100/70 px-3 text-sm transition-colors hover:border-[#E34747]/60 disabled:opacity-40"
					on:click={calculateTotals}
					disabled={totalsLoading}
				>
					<RefreshCcwIcon
						size={14}
						class={totalsLoading ? 'animate-spin' : ''}
					/>

					{totalsLoading
						? 'Berechne…'
						: totalsLoaded
							? 'Aktualisieren'
							: 'Berechnen'}
				</button>
			</div>

			<div
				class="grid grid-cols-2 gap-px bg-base-300 md:grid-cols-3"
			>
				<div class="bg-base-100/80 p-5">
					<div
						class="mb-3 flex items-center gap-2 text-xs text-slate-400"
					>
						<CircleDollarSignIcon size={13} />
						Geld in Umlauf
					</div>

					<div class="text-2xl font-bold tabular-nums">
						{totalsLoaded
							? formatMoney(totalMoneyInCirculation)
							: '—'}
					</div>

					<div class="mt-1 text-xs text-slate-500">
						Gesamt minus gefroren
					</div>
				</div>

				<div class="bg-base-100/80 p-5">
					<div
						class="mb-3 flex items-center gap-2 text-xs text-slate-400"
					>
						<SnowflakeIcon size={13} />
						Gefrorenes Geld
					</div>

					<div class="text-2xl font-bold tabular-nums">
						{totalsLoaded
							? formatMoney(totalFrozenMoney)
							: '—'}
					</div>

					<div class="mt-1 text-xs text-slate-500">
						Konten mit Status „Frozen“
					</div>
				</div>

				<div class="bg-base-100/80 p-5">
					<div
						class="mb-3 flex items-center gap-2 text-xs text-slate-400"
					>
						<CircleDollarSignIcon size={13} />
						Gesamtgeldmenge
					</div>

					<div class="text-2xl font-bold tabular-nums">
						{totalsLoaded
							? formatMoney(totalMoney)
							: '—'}
					</div>

					<div class="mt-1 text-xs text-slate-500">
						Summe aller Kontostände
					</div>
				</div>

				<div class="bg-base-100/80 p-5">
					<div
						class="mb-3 flex items-center gap-2 text-xs text-slate-400"
					>
						<UserIcon size={13} />
						Registrierte Benutzer
					</div>

					<div class="text-2xl font-bold tabular-nums">
						{totalsLoaded
							? `${totalRegistrated} / ${totalUsers}`
							: '—'}
					</div>

					<div class="mt-1 text-xs text-slate-500">
						Aktivierte von gesamt
					</div>
				</div>

				<div class="bg-base-100/80 p-5">
					<div
						class="mb-3 flex items-center gap-2 text-xs text-slate-400"
					>
						<Activity size={13} />
						Transaktionen heute
					</div>

					<div class="text-2xl font-bold tabular-nums">
						{totalsLoaded
							? transactionsToday
							: '—'}
					</div>

					<div class="mt-1 text-xs text-slate-500">
						Anzahl seit Mitternacht
					</div>
				</div>

				<div class="bg-base-100/80 p-5">
					<div
						class="mb-3 flex items-center gap-2 text-xs text-slate-400"
					>
						<CircleDollarSignIcon size={13} />
						Überwiesenes Geld
					</div>

					<div class="text-2xl font-bold tabular-nums">
						{totalsLoaded
							? formatMoney(movedMoney)
							: '—'}
					</div>

					<div class="mt-1 text-xs text-slate-500">
						Summe heute überwiesener Beträge
					</div>
				</div>
			</div>

			{#if totalsLoading}
				<div
					class="flex items-center gap-2 border-t border-base-300 px-5 py-3 text-xs text-slate-400"
				>
					<span class="loading loading-dots loading-xs"></span>
					Berechne Statistiken…
				</div>
			{/if}
		</div>

		<!-- Economic charts -->
		<div
			class="overflow-hidden rounded-3xl border border-base-300 bg-base-100/50"
		>
			<div
				class="flex flex-col gap-3 border-b border-base-300 px-5 py-4 md:flex-row md:items-center md:justify-between"
			>
				<div class="flex items-center gap-3">
					<Activity size={16} class="text-slate-400" />

					<div>
						<div class="flex items-center gap-2">
							<span class="font-semibold">
								Wirtschaftsentwicklung
							</span>

							<span
								class="rounded-lg border border-base-300 bg-base-100/60 px-2 py-0.5 text-xs text-slate-400"
							>
								{statisticRecords.length}
								Messwerte
							</span>
						</div>

						<div class="mt-0.5 text-xs text-slate-500">
							Inflation und Bruttoinlandsprodukt
						</div>
					</div>
				</div>

				<button
					class="flex h-9 items-center justify-center gap-2 rounded-xl border border-base-300 bg-base-100/70 px-3 text-sm transition-colors hover:border-[#E34747]/60 disabled:opacity-40"
					on:click={fetchStatistics}
					disabled={statisticsLoading}
				>
					<RefreshCcwIcon
						size={14}
						class={statisticsLoading ? 'animate-spin' : ''}
					/>

					{statisticsLoading
						? 'Lade…'
						: 'Aktualisieren'}
				</button>
			</div>

			<!-- Filters -->
			<div
				class="grid gap-4 border-b border-base-300 bg-base-100/30 px-5 py-4 sm:grid-cols-2 lg:grid-cols-4"
			>
				<div>
					<!-- svelte-ignore a11y_label_has_associated_control -->
					<label
						class="mb-1.5 block text-xs font-medium text-slate-400"
					>
						Ansicht
					</label>

					<div
						class="grid grid-cols-2 gap-1 rounded-xl border border-base-300 bg-base-100/70 p-1"
					>
						<button
							class={`h-8 rounded-lg text-sm transition-colors ${
								statisticView === 'hourly'
									? 'bg-[#E34747]/15 text-[#E34747]'
									: 'text-slate-400 hover:bg-base-200'
							}`}
							on:click={() => {
								statisticView = 'hourly';
							}}
						>
							Stündlich
						</button>

						<button
							class={`h-8 rounded-lg text-sm transition-colors ${
								statisticView === 'daily'
									? 'bg-[#E34747]/15 text-[#E34747]'
									: 'text-slate-400 hover:bg-base-200'
							}`}
							on:click={() => {
								statisticView = 'daily';
							}}
						>
							Täglich
						</button>
					</div>
				</div>

				<div>
					<label
						for="statistics-start-hour"
						class="mb-1.5 block text-xs font-medium text-slate-400"
					>
						Erste Stunde
					</label>

					<select
						id="statistics-start-hour"
						class="h-10 w-full rounded-xl border border-base-300 bg-base-100/70 px-3 text-sm outline-none transition-colors focus:border-[#E34747]/60"
						bind:value={startHour}
					>
						{#each hours as hour}
							<option value={hour}>
								{formatHour(hour)}
							</option>
						{/each}
					</select>
				</div>

				<div>
					<label
						for="statistics-end-hour"
						class="mb-1.5 block text-xs font-medium text-slate-400"
					>
						Letzte Stunde
					</label>

					<select
						id="statistics-end-hour"
						class="h-10 w-full rounded-xl border border-base-300 bg-base-100/70 px-3 text-sm outline-none transition-colors focus:border-[#E34747]/60"
						bind:value={endHour}
					>
						{#each hours as hour}
							<option value={hour}>
								{formatHour(hour)}
							</option>
						{/each}
					</select>
				</div>

				<div>
					<label
						for="daily-aggregation"
						class="mb-1.5 block text-xs font-medium text-slate-400"
					>
						Tagesberechnung
					</label>

					<select
						id="daily-aggregation"
						class="h-10 w-full rounded-xl border border-base-300 bg-base-100/70 px-3 text-sm outline-none transition-colors focus:border-[#E34747]/60 disabled:cursor-not-allowed disabled:opacity-40"
						bind:value={dailyAggregation}
						disabled={statisticView !== 'daily'}
					>
						<option value="average">
							Durchschnitt
						</option>

						<option value="sum">
							Summe
						</option>

						<option value="last">
							Letzter Wert
						</option>
					</select>
				</div>
			</div>

			<div
				class="flex flex-wrap items-center gap-x-4 gap-y-1 border-b border-base-300 px-5 py-3 text-xs text-slate-500"
			>
				<span>
					Stundenfilter:
					<strong class="font-medium text-slate-300">
						{formatHour(startHour)}
						bis
						{formatHour(endHour)}
					</strong>
				</span>

				<span>·</span>

				<span>{aggregationLabel()}</span>

				{#if startHour > endHour}
					<span
						class="rounded-md bg-amber-500/10 px-2 py-0.5 text-amber-400"
					>
						Zeitraum geht über Mitternacht
					</span>
				{/if}
			</div>

			{#if statisticsError}
				<div
					class="m-5 rounded-2xl border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-200"
				>
					{statisticsError}
				</div>
			{/if}

			<div class="grid gap-px bg-base-300 xl:grid-cols-2">
				<!-- Inflation -->
				<div class="min-w-0 bg-base-100/80 p-5">
					<div class="mb-5 space-y-3">
					<!-- Title, information and latest value -->
					<div class="flex items-start justify-between gap-4">
						<div class="min-w-0">
							<div class="text-sm font-semibold">
								Inflation
							</div>

							<div
								class="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-slate-500"
							>
								<span>
									{inflationDisplaySeries.length}
									Datenpunkte
								</span>

								<span aria-hidden="true">·</span>

								<span>
									{statisticView === 'hourly'
										? 'Stundenwerte'
										: 'Tageswerte'}
								</span>

								<span aria-hidden="true">·</span>

								<span>
									{inflationDisplayMode === 'value'
										? 'Tatsächlicher Wert'
										: 'Veränderung'}
								</span>
							</div>
						</div>

						<div class="shrink-0 text-right">
							<div class="text-xs text-slate-500">
								{inflationDisplayMode === 'value'
									? 'Letzter Wert'
									: 'Letzte Veränderung'}
							</div>

							<div
								class="font-mono text-lg font-bold text-[#E34747]"
							>
								{latestInflation !== null
									? formatStatisticValue(
											latestInflation,
											'inflation',
											inflationDisplayMode
										)
									: '—'}
							</div>
						</div>
					</div>

					<!-- Display mode and chart type -->
					<div
						class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between"
					>
						<div
							class="grid w-full grid-cols-2 gap-1 rounded-xl border border-base-300 bg-base-100/70 p-1 sm:w-auto sm:min-w-72"
							role="group"
							aria-label="Inflationsanzeige"
						>
							<button
								type="button"
								class={`h-8 rounded-lg px-3 text-sm transition-colors ${
									inflationDisplayMode === 'value'
										? 'bg-[#E34747]/15 font-medium text-[#E34747]'
										: 'text-slate-400 hover:bg-base-200 hover:text-slate-200'
								}`}
								aria-pressed={inflationDisplayMode === 'value'}
								on:click={() => {
									inflationDisplayMode = 'value';
								}}
							>
								Tatsächlicher Wert
							</button>

							<button
								type="button"
								class={`h-8 rounded-lg px-3 text-sm transition-colors ${
									inflationDisplayMode === 'change'
										? 'bg-[#E34747]/15 font-medium text-[#E34747]'
										: 'text-slate-400 hover:bg-base-200 hover:text-slate-200'
								}`}
								aria-pressed={inflationDisplayMode === 'change'}
								on:click={() => {
									inflationDisplayMode = 'change';
								}}
							>
								Veränderung in %
							</button>
						</div>

						<select
							aria-label="Diagrammtyp für Inflation"
							class="h-10 w-full rounded-xl border border-base-300 bg-base-100/70 px-3 text-sm outline-none transition-colors focus:border-[#E34747]/60 sm:w-auto sm:min-w-32"
							bind:value={inflationChartType}
						>
							<option value="line">Linie</option>
							<option value="area">Fläche</option>
							<option value="bar">Balken</option>
						</select>
					</div>
				</div>

					<div class="relative h-80 w-full">
						{#if statisticsLoading && !statisticsLoaded}
							<div
								class="absolute inset-0 grid place-items-center text-sm text-slate-400"
							>
								<div class="flex items-center gap-2">
									<span
										class="loading loading-spinner loading-sm"
									></span>

									Lade Inflationsdaten…
								</div>
							</div>
						{:else if inflationDisplaySeries.length === 0}
							<div
								class="absolute inset-0 grid place-items-center rounded-2xl border border-dashed border-base-300 text-sm text-slate-500"
							>
								Keine Inflationswerte für diesen Filter.
							</div>
						{:else}
							<canvas
								bind:this={inflationCanvas}
								aria-label="Inflationsdiagramm"
							></canvas>
						{/if}
					</div>
				</div>

				<!-- GDP -->
				<div class="min-w-0 bg-base-100/80 p-5">
					<div class="mb-5 space-y-3">
					<!-- Title, information and latest value -->
					<div class="flex items-start justify-between gap-4">
						<div class="min-w-0">
							<div class="text-sm font-semibold">
								Bruttoinlandsprodukt
							</div>

							<div
								class="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-slate-500"
							>
								<span>
									{gdpDisplaySeries.length}
									Datenpunkte
								</span>

								<span aria-hidden="true">·</span>

								<span>
									{statisticView === 'hourly'
										? 'Stundenwerte'
										: 'Tageswerte'}
								</span>

								<span aria-hidden="true">·</span>

								<span>
									{gdpDisplayMode === 'value'
										? 'Tatsächlicher Wert'
										: 'Veränderung'}
								</span>
							</div>
						</div>

						<div class="shrink-0 text-right">
							<div class="text-xs text-slate-500">
								{gdpDisplayMode === 'value'
									? 'Letzter Wert'
									: 'Letzte Veränderung'}
							</div>

							<div
								class="flex items-center justify-end gap-1 font-mono text-lg font-bold text-amber-400"
							>
								{#if latestGdp !== null}
									{formatStatisticValue(
										latestGdp,
										'gdp',
										gdpDisplayMode
									)}

									{#if gdpDisplayMode === 'value'}
										<Currency size={16} />
									{/if}
								{:else}
									—
								{/if}
							</div>
						</div>
					</div>

					<!-- Display mode and chart type -->
					<div
						class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between"
					>
						<div
							class="grid w-full grid-cols-2 gap-1 rounded-xl border border-base-300 bg-base-100/70 p-1 sm:w-auto sm:min-w-72"
							role="group"
							aria-label="BIP-Anzeige"
						>
							<button
								type="button"
								class={`h-8 rounded-lg px-3 text-sm transition-colors ${
									gdpDisplayMode === 'value'
										? 'bg-amber-500/15 font-medium text-amber-400'
										: 'text-slate-400 hover:bg-base-200 hover:text-slate-200'
								}`}
								aria-pressed={gdpDisplayMode === 'value'}
								on:click={() => {
									gdpDisplayMode = 'value';
								}}
							>
								Tatsächlicher Wert
							</button>

							<button
								type="button"
								class={`h-8 rounded-lg px-3 text-sm transition-colors ${
									gdpDisplayMode === 'change'
										? 'bg-amber-500/15 font-medium text-amber-400'
										: 'text-slate-400 hover:bg-base-200 hover:text-slate-200'
								}`}
								aria-pressed={gdpDisplayMode === 'change'}
								on:click={() => {
									gdpDisplayMode = 'change';
								}}
							>
								Veränderung in %
							</button>
						</div>

						<select
							aria-label="Diagrammtyp für das Bruttoinlandsprodukt"
							class="h-10 w-full rounded-xl border border-base-300 bg-base-100/70 px-3 text-sm outline-none transition-colors focus:border-amber-500/60 sm:w-auto sm:min-w-32"
							bind:value={gdpChartType}
						>
							<option value="line">Linie</option>
							<option value="area">Fläche</option>
							<option value="bar">Balken</option>
						</select>
					</div>
				</div>

					<div class="relative h-80 w-full">
						{#if statisticsLoading && !statisticsLoaded}
							<div
								class="absolute inset-0 grid place-items-center text-sm text-slate-400"
							>
								<div class="flex items-center gap-2">
									<span
										class="loading loading-spinner loading-sm"
									></span>

									Lade BIP-Daten…
								</div>
							</div>
						{:else if gdpDisplaySeries.length === 0}
							<div
								class="absolute inset-0 grid place-items-center rounded-2xl border border-dashed border-base-300 text-sm text-slate-500"
							>
								Keine BIP-Werte für diesen Filter.
							</div>
						{:else}
							<canvas
								bind:this={gdpCanvas}
								aria-label="Diagramm des Bruttoinlandsprodukts"
							></canvas>
						{/if}
					</div>
				</div>
			</div>

			<div
				class="flex items-center gap-2 border-t border-base-300 px-5 py-3 text-xs text-slate-500"
			>
				<ShieldCheckIcon size={12} />

				Der Stundenfilter wird vor der Tagesberechnung
				angewendet.
			</div>
		</div>

		<!-- Transaction statistics -->
		<TransactionStatistics />

		<!-- Accounts table -->
		<div
			class="overflow-hidden rounded-3xl border border-base-300 bg-base-100/50"
		>
			<div
				class="flex flex-col gap-3 border-b border-base-300 px-5 py-4 sm:flex-row sm:items-center sm:justify-between"
			>
				<div class="flex items-center gap-3">
					<UserIcon size={16} class="text-slate-400" />

					<span class="font-semibold">
						Konten
					</span>

					<span
						class="rounded-lg border border-base-300 bg-base-100/60 px-2 py-0.5 text-xs text-slate-400"
					>
						{accounts.length} Einträge · Seite
						{page}/{maxPage}
					</span>
				</div>

				<div class="flex flex-wrap items-center gap-2">
					<div class="relative">
						<div
							class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"
						>
							<SearchIcon size={14} />
						</div>

						<input
							class="h-9 w-full rounded-xl border border-base-300 bg-base-100/70 pl-8 pr-3 text-sm placeholder:text-slate-500 outline-none transition-colors focus:border-[#E34747]/60 sm:w-72"
							placeholder="Name, Mail, ID…"
							value={filter}
							autocomplete="off"
							on:input={(event) =>
								onFilterInput(
									(
										event.target as HTMLInputElement
									).value
								)}
						/>
					</div>

					<select
						class="h-9 rounded-xl border border-base-300 bg-base-100/70 px-3 text-sm outline-none transition-colors focus:border-[#E34747]/60"
						bind:value={perPage}
						on:change={async () => {
							page = 1;
							await fetchAccounts();
						}}
					>
						{#each [
							10,
							30,
							50,
							100,
							200,
							500,
							1000,
							2000
						] as amount}
							<option value={amount}>
								{amount} / Seite
							</option>
						{/each}
					</select>

					<button
						class="h-9 rounded-xl border border-base-300 bg-base-100/70 px-3 text-sm transition-colors hover:border-[#E34747]/60 disabled:opacity-30"
						on:click={pageDown}
						disabled={page <= 1 || loading}
						aria-label="Vorherige Seite"
						title="Vorherige Seite"
					>
						←
					</button>

					<button
						class="h-9 rounded-xl border border-base-300 bg-base-100/70 px-3 text-sm transition-colors hover:border-[#E34747]/60 disabled:opacity-30"
						on:click={pageUp}
						disabled={page >= maxPage || loading}
						aria-label="Nächste Seite"
						title="Nächste Seite"
					>
						→
					</button>
				</div>
			</div>

			<div class="overflow-x-auto">
				<table class="min-w-full text-sm">
					<thead>
						<tr
							class="border-b border-base-300 bg-base-100/40 text-xs font-medium uppercase tracking-wide text-slate-400"
						>
							<th class="px-5 py-3 text-left">
								Benutzer
							</th>

							<th class="px-5 py-3 text-left">
								E-Mail
							</th>

							<th class="px-5 py-3 text-left">
								User ID
							</th>

							<th class="px-5 py-3 text-left">
								Account ID
							</th>

							<th class="px-5 py-3 text-right">
								Guthaben
							</th>
						</tr>
					</thead>

					<tbody class="divide-y divide-base-300/60">
						{#if loading}
							<tr>
								<td
									class="px-5 py-10 text-center text-slate-400"
									colspan="5"
								>
									<span
										class="loading loading-spinner loading-sm mr-2 align-middle"
									></span>

									Lade Konten…
								</td>
							</tr>
						{:else if accounts.length === 0}
							<tr>
								<td
									class="px-5 py-10 text-center text-slate-400"
									colspan="5"
								>
									Keine Konten gefunden.
								</td>
							</tr>
						{:else}
							{#each accounts as account}
								<tr
									class="transition-colors hover:bg-white/3"
								>
									<td class="px-5 py-3">
										<div
											class="flex items-center gap-3"
										>
											<div
												class="grid h-8 w-8 shrink-0 place-items-center rounded-xl border border-base-300 bg-base-100/70"
											>
												{#if account.expand?.user?.expand
													.data
													.financeTrackingAccess ==
													true &&
													account.expand?.user
														?.expand.data
														.warehouseAccess ==
														false}
													<UserKey
														size={14}
														color="#E34747"
													/>
												{:else if account.expand?.user
													?.expand.data
													.warehouseAccess ==
													true &&
													account.expand?.user
														?.expand.data
														.financeTrackingAccess ==
														false}
													<Forklift
														size={14}
														color="#F59E0B"
													/>
												{:else if account.expand?.user
													?.expand.data
													.financeTrackingAccess ==
													true &&
													account.expand?.user
														?.expand.data
														.warehouseAccess ==
														true}
													<UserKey
														size={14}
														color="#22c55e"
													/>
												{:else}
													<UserIcon size={14} />
												{/if}
											</div>

											<div>
												<div
													class="font-medium leading-tight"
												>
													{account.expand?.user
														?.name ?? '—'}
												</div>

												<div
													class="font-mono text-xs text-slate-500"
												>
													{account.expand?.user
														?.id ?? '—'}
												</div>
											</div>
										</div>
									</td>

									<td
										class="px-5 py-3 text-slate-300"
									>
										{account.expand?.user?.email ??
											'—'}
									</td>

									<td class="px-5 py-3">
										<span
											class="text-xs tabular-nums text-slate-500"
										>
											{account.expand?.user?.id ??
												'—'}
										</span>
									</td>

									<td class="px-5 py-3">
										<span
											class="inline-flex rounded-lg border border-base-300 bg-base-100/70 px-2 py-1 font-mono text-xs"
										>
											{account.id}
										</span>
									</td>

									<td
										class="px-5 py-3 text-right"
									>
										<div
											class="inline-flex rounded-lg border border-base-300 bg-base-100/70 px-2 py-1 font-mono text-xs"
										>
											{nf.format(
												account.balance ?? 0
											)}
										</div>
									</td>
								</tr>
							{/each}
						{/if}
					</tbody>
				</table>
			</div>

			<div
				class="flex items-center gap-2 border-t border-base-300 px-5 py-3 text-xs text-slate-500"
			>
				<ShieldCheckIcon size={12} />
				Nur-Lese-Ansicht — keine Änderungen möglich
			</div>
		</div>
	</div>
</AdminSite>
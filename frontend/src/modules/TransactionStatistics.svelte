<script lang="ts">
	import { browser } from '$app/environment';
	import db from '$lib/connections';
	import TransactionMetricButtons from './TransactionMetricButtons.svelte';
	import {
		Activity,
		ArrowLeftRight,
		ChartPie,
		CircleDollarSign,
		Clock3,
		RefreshCcw,
		ShieldCheck
	} from '@lucide/svelte';
	import { onDestroy, onMount, tick } from 'svelte';

	type TransactionRange = '24h' | '7d' | '30d' | 'all';
	type TransactionMetric = 'count' | 'amount';
	type TransactionDataMode = 'approved' | 'all';
	type TransactionTrendType = 'line' | 'area' | 'bar';
	type FlowType =
		| 'business_to_business'
		| 'business_to_personal'
		| 'personal_to_personal'
		| 'personal_to_business';

	type TransactionStatisticRecord = {
		id: string;
		created: string;
		sender_account_id: string;
		receiver_account_id: string;
		sender_user_id: string;
		receiver_user_id: string;
		sender_name: string;
		receiver_name: string;
		sender_is_company: boolean | number;
		receiver_is_company: boolean | number;
		flow_type: FlowType;
		amount: number | string;
		received: number | string;
		difference: number | string;
		status: string;
		system: string;
		is_system: boolean | number;
		is_self_transfer: boolean | number;
	};

	type NormalizedTransaction = {
		id: string;
		timestamp: Date;
		flowType: FlowType;
		amount: number;
		received: number;
		difference: number;
		status: string;
		isSystem: boolean;
		isSelfTransfer: boolean;
	};

	type ChartEntry = {
		key: string;
		label: string;
		value: number;
	};

	const TRANSACTION_STATISTICS_COLLECTION = 'transaction_statistics';

	const RANGE_OPTIONS: Array<{ value: TransactionRange; label: string }> = [
		{ value: '24h', label: '24 Std.' },
		{ value: '7d', label: '7 Tage' },
		{ value: '30d', label: '30 Tage' },
		{ value: 'all', label: 'Gesamt' }
	];

	const FLOW_ORDER: Array<{ key: FlowType; label: string }> = [
		{ key: 'business_to_business', label: 'Unternehmen → Unternehmen' },
		{ key: 'business_to_personal', label: 'Unternehmen → Privat' },
		{ key: 'personal_to_personal', label: 'Privat → Privat' },
		{ key: 'personal_to_business', label: 'Privat → Unternehmen' }
	];

	const STATUS_ORDER = [
		'approved',
		'pending',
		'flagged',
		'under_audit',
		'denied'
	];

	const FLOW_COLORS = ['#E34747', '#F59E0B', '#3B82F6', '#22C55E'];
	const STATUS_COLORS = ['#22C55E', '#F59E0B', '#FB923C', '#A855F7', '#EF4444'];

	const numberFormatter = new Intl.NumberFormat('de-DE', {
		maximumFractionDigits: 2
	});

	const compactFormatter = new Intl.NumberFormat('de-DE', {
		notation: 'compact',
		maximumFractionDigits: 1
	});

	const dayFormatter = new Intl.DateTimeFormat('de-DE', {
		day: '2-digit',
		month: '2-digit'
	});

	const hourFormatter = new Intl.DateTimeFormat('de-DE', {
		day: '2-digit',
		month: '2-digit',
		hour: '2-digit'
	});

	const monthFormatter = new Intl.DateTimeFormat('de-DE', {
		month: 'short',
		year: 'numeric'
	});

	let rawRecords: TransactionStatisticRecord[] = [];
	let transactions: NormalizedTransaction[] = [];
	let analyzedTransactions: NormalizedTransaction[] = [];

	let loading = false;
	let loaded = false;
	let error: string | null = null;

	let selectedRange: TransactionRange = '24h';
	let dataMode: TransactionDataMode = 'approved';
	let flowMetric: TransactionMetric = 'count';
	let trendMetric: TransactionMetric = 'amount';
	let hourMetric: TransactionMetric = 'count';
	let trendType: TransactionTrendType = 'area';

	let totalTransactions = 0;
	let analyzedTransactionCount = 0;
	let totalAmount = 0;
	let averageAmount = 0;
	let totalDifference = 0;
	let approvalRate = 0;

	let flowData: ChartEntry[] = [];
	let statusData: ChartEntry[] = [];
	let trendData: ChartEntry[] = [];
	let hourData: ChartEntry[] = [];

	let flowCanvas: HTMLCanvasElement | null = null;
	let statusCanvas: HTMLCanvasElement | null = null;
	let trendCanvas: HTMLCanvasElement | null = null;
	let hourCanvas: HTMLCanvasElement | null = null;

	let ChartJS: any = null;
	let flowChart: any = null;
	let statusChart: any = null;
	let trendChart: any = null;
	let hourChart: any = null;
	let renderTimer: ReturnType<typeof setTimeout> | null = null;

	$: transactions = rawRecords
		.map(normalizeTransaction)
		.filter((record): record is NormalizedTransaction => record !== null);

	$: analyzedTransactions =
		dataMode === 'approved'
			? transactions.filter((transaction) => transaction.status === 'approved')
			: transactions;

	$: totalTransactions = transactions.length;
	$: analyzedTransactionCount = analyzedTransactions.length;
	$: totalAmount = analyzedTransactions.reduce(
		(sum, transaction) => sum + transaction.amount,
		0
	);
	$: averageAmount =
		analyzedTransactionCount > 0
			? totalAmount / analyzedTransactionCount
			: 0;
	$: totalDifference = analyzedTransactions.reduce(
		(sum, transaction) => sum + transaction.difference,
		0
	);
	$: approvalRate =
		totalTransactions > 0
			? (transactions.filter((transaction) => transaction.status === 'approved').length /
					totalTransactions) *
				100
			: 0;

	$: flowData = buildFlowData(analyzedTransactions, flowMetric);
	$: statusData = buildStatusData(transactions);
	$: trendData = buildTrendData(analyzedTransactions, trendMetric, selectedRange);
	$: hourData = buildHourData(analyzedTransactions, hourMetric);

	$: if (browser && ChartJS && loaded) {
		queueChartRender(
			flowData,
			statusData,
			trendData,
			hourData,
			flowMetric,
			trendMetric,
			hourMetric,
			trendType
		);
	}

	onMount(async () => {
		if (!browser) return;

		try {
			const chartModule = await import('chart.js/auto');
			ChartJS = chartModule.default;
		} catch (chartError: any) {
			error = chartError?.message ?? 'Chart.js konnte nicht geladen werden.';
		}

		//await fetchTransactionStatistics();
	});

	onDestroy(() => {
		if (renderTimer) {
			clearTimeout(renderTimer);
		}

		destroyCharts();
	});

	async function fetchTransactionStatistics() {
		loading = true;
		error = null;

		try {
			const filter = buildRangeFilter(selectedRange);

			const records = await db.collection(TRANSACTION_STATISTICS_COLLECTION).getFullList({
				sort: '+created',
				filter
			});

			rawRecords = records as unknown as TransactionStatisticRecord[];
			loaded = true;
		} catch (fetchError: any) {
			error =
				fetchError?.message ??
				'Die Transaktionsstatistik konnte nicht geladen werden.';
			rawRecords = [];
			loaded = true;
		} finally {
			loading = false;
		}
	}

	async function changeRange(range: TransactionRange) {
		if (selectedRange === range || loading) return;

		selectedRange = range;
		await fetchTransactionStatistics();
	}

	function buildRangeFilter(range: TransactionRange): string {
		if (range === 'all') return '';

		const duration =
			range === '24h'
				? 24 * 60 * 60 * 1000
				: range === '7d'
					? 7 * 24 * 60 * 60 * 1000
					: 30 * 24 * 60 * 60 * 1000;

		const start = new Date(Date.now() - duration);
		return `created >= "${start.toISOString()}"`;
	}

	function normalizeTransaction(
		record: TransactionStatisticRecord
	): NormalizedTransaction | null {
		const timestamp = parseDate(record.created);
		if (!timestamp) return null;

		return {
			id: record.id,
			timestamp,
			flowType: record.flow_type,
			amount: toNumber(record.amount),
			received: toNumber(record.received),
			difference: toNumber(record.difference),
			status: record.status || 'pending',
			isSystem: toBoolean(record.is_system),
			isSelfTransfer: toBoolean(record.is_self_transfer)
		};
	}

	function parseDate(value: string): Date | null {
		let date = new Date(value);

		if (!Number.isNaN(date.getTime())) {
			return date;
		}

		date = new Date(value.replace(' ', 'T'));
		return Number.isNaN(date.getTime()) ? null : date;
	}

	function toNumber(value: number | string | null | undefined): number {
		const number = Number(String(value ?? 0).replace(',', '.'));
		return Number.isFinite(number) ? number : 0;
	}

	function toBoolean(value: boolean | number): boolean {
		return value === true || value === 1;
	}

	function metricValue(
		transaction: NormalizedTransaction,
		metric: TransactionMetric
	): number {
		return metric === 'count' ? 1 : transaction.amount;
	}

	function buildFlowData(
		records: NormalizedTransaction[],
		metric: TransactionMetric
	): ChartEntry[] {
		const values = new Map<FlowType, number>();

		for (const transaction of records) {
			values.set(
				transaction.flowType,
				(values.get(transaction.flowType) ?? 0) + metricValue(transaction, metric)
			);
		}

		return FLOW_ORDER.map((flow) => ({
			key: flow.key,
			label: flow.label,
			value: values.get(flow.key) ?? 0
		}));
	}

	function buildStatusData(records: NormalizedTransaction[]): ChartEntry[] {
		const values = new Map<string, number>();

		for (const transaction of records) {
			values.set(transaction.status, (values.get(transaction.status) ?? 0) + 1);
		}

		return STATUS_ORDER.map((status) => ({
			key: status,
			label: statusLabel(status),
			value: values.get(status) ?? 0
		}));
	}

	function buildHourData(
		records: NormalizedTransaction[],
		metric: TransactionMetric
	): ChartEntry[] {
		const values = Array.from({ length: 24 }, () => 0);

		for (const transaction of records) {
			values[transaction.timestamp.getHours()] += metricValue(transaction, metric);
		}

		return values.map((value, hour) => ({
			key: String(hour),
			label: `${String(hour).padStart(2, '0')}:00`,
			value
		}));
	}

	function buildTrendData(
		records: NormalizedTransaction[],
		metric: TransactionMetric,
		range: TransactionRange
	): ChartEntry[] {
		const now = new Date();
		const earliest = records.length > 0 ? records[0].timestamp : now;
		const spanInDays = Math.max(
			1,
			(now.getTime() - earliest.getTime()) / (24 * 60 * 60 * 1000)
		);

		const bucketType: 'hour' | 'day' | 'month' =
			range === '24h'
				? 'hour'
				: range === 'all' && spanInDays > 120
					? 'month'
					: 'day';

		const values = new Map<number, number>();

		for (const transaction of records) {
			const bucket = getBucketStart(transaction.timestamp, bucketType);
			values.set(
				bucket.getTime(),
				(values.get(bucket.getTime()) ?? 0) + metricValue(transaction, metric)
			);
		}

		let start: Date;

		if (range === '24h') {
			start = getBucketStart(new Date(now.getTime() - 23 * 60 * 60 * 1000), 'hour');
		} else if (range === '7d') {
			start = getBucketStart(new Date(now.getTime() - 6 * 24 * 60 * 60 * 1000), 'day');
		} else if (range === '30d') {
			start = getBucketStart(new Date(now.getTime() - 29 * 24 * 60 * 60 * 1000), 'day');
		} else {
			start = getBucketStart(earliest, bucketType);
		}

		const end = getBucketStart(now, bucketType);
		const result: ChartEntry[] = [];

		for (
			let cursor = new Date(start);
			cursor.getTime() <= end.getTime();
			cursor = addBucket(cursor, bucketType)
		) {
			result.push({
				key: cursor.toISOString(),
				label: formatBucketLabel(cursor, bucketType),
				value: values.get(cursor.getTime()) ?? 0
			});
		}

		return result;
	}

	function getBucketStart(date: Date, type: 'hour' | 'day' | 'month'): Date {
		if (type === 'hour') {
			return new Date(
				date.getFullYear(),
				date.getMonth(),
				date.getDate(),
				date.getHours()
			);
		}

		if (type === 'month') {
			return new Date(date.getFullYear(), date.getMonth(), 1);
		}

		return new Date(date.getFullYear(), date.getMonth(), date.getDate());
	}

	function addBucket(date: Date, type: 'hour' | 'day' | 'month'): Date {
		const next = new Date(date);

		if (type === 'hour') {
			next.setHours(next.getHours() + 1);
		} else if (type === 'month') {
			next.setMonth(next.getMonth() + 1);
		} else {
			next.setDate(next.getDate() + 1);
		}

		return next;
	}

	function formatBucketLabel(date: Date, type: 'hour' | 'day' | 'month'): string {
		if (type === 'hour') return hourFormatter.format(date);
		if (type === 'month') return monthFormatter.format(date);
		return dayFormatter.format(date);
	}

	function statusLabel(status: string): string {
		switch (status) {
			case 'approved':
				return 'Genehmigt';
			case 'pending':
				return 'Ausstehend';
			case 'flagged':
				return 'Markiert';
			case 'under_audit':
				return 'In Prüfung';
			case 'denied':
				return 'Abgelehnt';
			default:
				return status;
		}
	}

	function hasValues(data: ChartEntry[]): boolean {
		return data.some((entry) => entry.value !== 0);
	}

	function formatMetricValue(value: number, metric: TransactionMetric): string {
		return metric === 'count'
			? `${numberFormatter.format(value)} Transaktionen`
			: `${numberFormatter.format(value)} E`;
	}

	function formatMoney(value: number): string {
		return `${numberFormatter.format(value)} E`;
	}

	function queueChartRender(
		_flowData: ChartEntry[],
		_statusData: ChartEntry[],
		_trendData: ChartEntry[],
		_hourData: ChartEntry[],
		_flowMetric: TransactionMetric,
		_trendMetric: TransactionMetric,
		_hourMetric: TransactionMetric,
		_trendType: TransactionTrendType
	) {
		if (renderTimer) {
			clearTimeout(renderTimer);
		}

		renderTimer = setTimeout(() => {
			void renderCharts();
		}, 0);
	}

	async function renderCharts() {
		await tick();
		destroyCharts();

		if (!ChartJS) return;

		if (flowCanvas && hasValues(flowData)) {
			flowChart = createPieChart(flowCanvas, flowData, flowMetric);
		}

		if (statusCanvas && hasValues(statusData)) {
			statusChart = createStatusChart(statusCanvas, statusData);
		}

		if (trendCanvas && trendData.length > 0) {
			trendChart = createTrendChart(trendCanvas, trendData, trendMetric, trendType);
		}

		if (hourCanvas && hourData.length > 0) {
			hourChart = createHourChart(hourCanvas, hourData, hourMetric);
		}
	}

	function destroyCharts() {
		flowChart?.destroy();
		statusChart?.destroy();
		trendChart?.destroy();
		hourChart?.destroy();

		flowChart = null;
		statusChart = null;
		trendChart = null;
		hourChart = null;
	}

	function createPieChart(
		canvas: HTMLCanvasElement,
		data: ChartEntry[],
		metric: TransactionMetric
	) {
		return new ChartJS(canvas, {
			type: 'pie',
			data: {
				labels: data.map((entry) => entry.label),
				datasets: [
					{
						data: data.map((entry) => entry.value),
						backgroundColor: FLOW_COLORS,
						borderColor: 'rgba(15, 23, 42, 0.45)',
						borderWidth: 2,
						hoverOffset: 6
					}
				]
			},
			options: pieOptions(metric)
		});
	}

	function createStatusChart(canvas: HTMLCanvasElement, data: ChartEntry[]) {
		return new ChartJS(canvas, {
			type: 'doughnut',
			data: {
				labels: data.map((entry) => entry.label),
				datasets: [
					{
						data: data.map((entry) => entry.value),
						backgroundColor: STATUS_COLORS,
						borderColor: 'rgba(15, 23, 42, 0.45)',
						borderWidth: 2,
						hoverOffset: 6
					}
				]
			},
			options: {
				...pieOptions('count'),
				cutout: '64%'
			}
		});
	}

	function pieOptions(metric: TransactionMetric) {
		return {
			responsive: true,
			maintainAspectRatio: false,
			plugins: {
				legend: {
					position: 'bottom',
					labels: {
						color: 'rgba(203, 213, 225, 0.9)',
						usePointStyle: true,
						boxWidth: 9,
						padding: 16
					}
				},
				tooltip: {
					callbacks: {
						label: (context: any) =>
							`${context.label}: ${formatMetricValue(Number(context.raw ?? 0), metric)}`
					}
				}
			}
		};
	}

	function createTrendChart(
		canvas: HTMLCanvasElement,
		data: ChartEntry[],
		metric: TransactionMetric,
		type: TransactionTrendType
	) {
		const chartType = type === 'bar' ? 'bar' : 'line';

		return new ChartJS(canvas, {
			type: chartType,
			data: {
				labels: data.map((entry) => entry.label),
				datasets: [
					{
						label: metric === 'count' ? 'Transaktionen' : 'Transaktionsvolumen',
						data: data.map((entry) => entry.value),
						borderColor: '#E34747',
						backgroundColor:
							type === 'area'
								? 'rgba(227, 71, 71, 0.18)'
								: 'rgba(227, 71, 71, 0.72)',
						fill: type === 'area',
						tension: chartType === 'line' ? 0.3 : 0,
						borderWidth: 2,
						borderRadius: chartType === 'bar' ? 6 : 0,
						pointRadius: data.length <= 50 && chartType === 'line' ? 2.5 : 0,
						pointHoverRadius: 5,
						maxBarThickness: 34
					}
				]
			},
			options: cartesianOptions(metric)
		});
	}

	function createHourChart(
		canvas: HTMLCanvasElement,
		data: ChartEntry[],
		metric: TransactionMetric
	) {
		return new ChartJS(canvas, {
			type: 'bar',
			data: {
				labels: data.map((entry) => entry.label),
				datasets: [
					{
						label: metric === 'count' ? 'Transaktionen' : 'Transaktionsvolumen',
						data: data.map((entry) => entry.value),
						backgroundColor: 'rgba(245, 158, 11, 0.72)',
						borderColor: '#F59E0B',
						borderWidth: 1.5,
						borderRadius: 6,
						maxBarThickness: 26
					}
				]
			},
			options: cartesianOptions(metric)
		});
	}

	function cartesianOptions(metric: TransactionMetric) {
		return {
			responsive: true,
			maintainAspectRatio: false,
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
						label: (context: any) =>
							formatMetricValue(Number(context.parsed?.y ?? context.raw ?? 0), metric)
					}
				}
			},
			scales: {
				x: {
					grid: {
						display: false
					},
					ticks: {
						color: 'rgba(148, 163, 184, 0.82)',
						autoSkip: true,
						maxTicksLimit: 12,
						maxRotation: 0
					},
					border: {
						color: 'rgba(148, 163, 184, 0.18)'
					}
				},
				y: {
					beginAtZero: true,
					grid: {
						color: 'rgba(148, 163, 184, 0.11)'
					},
					ticks: {
						color: 'rgba(148, 163, 184, 0.82)',
						precision: metric === 'count' ? 0 : undefined,
						callback: (value: string | number) =>
							metric === 'count'
								? numberFormatter.format(Number(value))
								: `${compactFormatter.format(Number(value))} E`
					},
					border: {
						display: false
					}
				}
			}
		};
	}
</script>

<div class="overflow-hidden rounded-3xl border border-base-300 bg-base-100/50">
	<div class="flex flex-col gap-4 border-b border-base-300 px-5 py-4 xl:flex-row xl:items-center xl:justify-between">
		<div class="flex items-center gap-3">
			<ArrowLeftRight size={16} class="text-slate-400" />

			<div>
				<div class="flex flex-wrap items-center gap-2">
					<span class="font-semibold">Transaktionsstatistik</span>

					<span class="rounded-lg border border-base-300 bg-base-100/60 px-2 py-0.5 text-xs text-slate-400">
						{totalTransactions} Einträge
					</span>
				</div>

				<div class="mt-0.5 text-xs text-slate-500">
					Zahlungswege, Status, Entwicklung und Tageszeiten
				</div>
			</div>
		</div>

		<div class="flex flex-col gap-2 sm:flex-row sm:flex-wrap sm:items-center xl:justify-end">
			<div class="grid grid-cols-4 gap-1 rounded-xl border border-base-300 bg-base-100/70 p-1">
				{#each RANGE_OPTIONS as option}
					<button
						type="button"
						class={`h-8 rounded-lg px-2 text-xs transition-colors sm:px-3 ${
							selectedRange === option.value
								? 'bg-[#E34747]/15 font-medium text-[#E34747]'
								: 'text-slate-400 hover:bg-base-200 hover:text-slate-200'
						}`}
						disabled={loading}
						on:click={() => changeRange(option.value)}
					>
						{option.label}
					</button>
				{/each}
			</div>

			<div class="grid grid-cols-2 gap-1 rounded-xl border border-base-300 bg-base-100/70 p-1">
				<button
					type="button"
					class={`h-8 rounded-lg px-3 text-xs transition-colors ${
						dataMode === 'approved'
							? 'bg-[#E34747]/15 font-medium text-[#E34747]'
							: 'text-slate-400 hover:bg-base-200 hover:text-slate-200'
					}`}
					on:click={() => {
						dataMode = 'approved';
					}}
				>
					Nur genehmigt
				</button>

				<button
					type="button"
					class={`h-8 rounded-lg px-3 text-xs transition-colors ${
						dataMode === 'all'
							? 'bg-[#E34747]/15 font-medium text-[#E34747]'
							: 'text-slate-400 hover:bg-base-200 hover:text-slate-200'
					}`}
					on:click={() => {
						dataMode = 'all';
					}}
				>
					Alle Status
				</button>
			</div>

			<button
				type="button"
				class="flex h-10 items-center justify-center gap-2 rounded-xl border border-base-300 bg-base-100/70 px-3 text-sm transition-colors hover:border-[#E34747]/60 disabled:opacity-40"
				disabled={loading}
				on:click={fetchTransactionStatistics}
			>
				<RefreshCcw size={14} class={loading ? 'animate-spin' : ''} />
				{loading ? 'Lade…' : 'Aktualisieren'}
			</button>
		</div>
	</div>

	{#if error}
		<div class="m-5 rounded-2xl border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-200">
			{error}
		</div>
	{/if}

	<div class="grid grid-cols-2 gap-px bg-base-300 md:grid-cols-3 xl:grid-cols-5">
		<div class="bg-base-100/80 p-5">
			<div class="mb-3 flex items-center gap-2 text-xs text-slate-400">
				<Activity size={13} /> Transaktionen
			</div>
			<div class="text-2xl font-bold tabular-nums">{totalTransactions}</div>
			<div class="mt-1 text-xs text-slate-500">Alle Status im Zeitraum</div>
		</div>

		<div class="bg-base-100/80 p-5">
			<div class="mb-3 flex items-center gap-2 text-xs text-slate-400">
				<CircleDollarSign size={13} /> Volumen
			</div>
			<div class="text-2xl font-bold tabular-nums">{formatMoney(totalAmount)}</div>
			<div class="mt-1 text-xs text-slate-500">
				{analyzedTransactionCount} berücksichtigte Vorgänge
			</div>
		</div>

		<div class="bg-base-100/80 p-5">
			<div class="mb-3 flex items-center gap-2 text-xs text-slate-400">
				<CircleDollarSign size={13} /> Durchschnitt
			</div>
			<div class="text-2xl font-bold tabular-nums">{formatMoney(averageAmount)}</div>
			<div class="mt-1 text-xs text-slate-500">Pro berücksichtigter Transaktion</div>
		</div>

		<div class="bg-base-100/80 p-5">
			<div class="mb-3 flex items-center gap-2 text-xs text-slate-400">
				<ShieldCheck size={13} /> Genehmigungsquote
			</div>
			<div class="text-2xl font-bold tabular-nums">
				{numberFormatter.format(approvalRate)} %
			</div>
			<div class="mt-1 text-xs text-slate-500">Genehmigt von allen Status</div>
		</div>

		<div class="col-span-2 bg-base-100/80 p-5 md:col-span-1">
			<div class="mb-3 flex items-center gap-2 text-xs text-slate-400">
				<CircleDollarSign size={13} /> Differenz
			</div>
			<div class="text-2xl font-bold tabular-nums">{formatMoney(totalDifference)}</div>
			<div class="mt-1 text-xs text-slate-500">Betrag minus empfangener Wert</div>
		</div>
	</div>

	<div class="grid gap-px bg-base-300 xl:grid-cols-2">
		<div class="min-w-0 bg-base-100/80 p-5">
			<div class="mb-4 flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
				<div>
					<div class="flex items-center gap-2 text-sm font-semibold">
						<ChartPie size={15} class="text-slate-400" /> Zahlungswege
					</div>
					<div class="mt-1 text-xs text-slate-500">
						Unternehmen und Privatpersonen nach Sender und Empfänger
					</div>
				</div>

				<TransactionMetricButtons bind:value={flowMetric} />
			</div>

			<div class="relative h-80 w-full">
				{#if loading && !loaded}
					<div class="absolute inset-0 grid place-items-center text-sm text-slate-400">
						<span class="loading loading-spinner loading-sm"></span>
					</div>
				{:else if !hasValues(flowData)}
					<div class="absolute inset-0 grid place-items-center rounded-2xl border border-dashed border-base-300 text-sm text-slate-500">
						Keine Transaktionen für diese Auswahl.
					</div>
				{:else}
					<canvas bind:this={flowCanvas} aria-label="Kreisdiagramm der Zahlungswege"></canvas>
				{/if}
			</div>
		</div>

		<div class="min-w-0 bg-base-100/80 p-5">
			<div class="mb-4">
				<div class="flex items-center gap-2 text-sm font-semibold">
					<ShieldCheck size={15} class="text-slate-400" /> Statusverteilung
				</div>
				<div class="mt-1 text-xs text-slate-500">
					Enthält unabhängig vom Analysefilter immer alle Status
				</div>
			</div>

			<div class="relative h-80 w-full">
				{#if loading && !loaded}
					<div class="absolute inset-0 grid place-items-center text-sm text-slate-400">
						<span class="loading loading-spinner loading-sm"></span>
					</div>
				{:else if !hasValues(statusData)}
					<div class="absolute inset-0 grid place-items-center rounded-2xl border border-dashed border-base-300 text-sm text-slate-500">
						Keine Statusdaten für diesen Zeitraum.
					</div>
				{:else}
					<canvas bind:this={statusCanvas} aria-label="Ringdiagramm der Transaktionsstatus"></canvas>
				{/if}
			</div>
		</div>

		<div class="min-w-0 bg-base-100/80 p-5">
			<div class="mb-4 flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
				<div>
					<div class="flex items-center gap-2 text-sm font-semibold">
						<Activity size={15} class="text-slate-400" /> Entwicklung
					</div>
					<div class="mt-1 text-xs text-slate-500">
						Fehlende Zeitabschnitte werden mit 0 dargestellt
					</div>
				</div>

				<div class="flex flex-wrap items-center gap-2">
					<TransactionMetricButtons bind:value={trendMetric} />

					<select
						aria-label="Diagrammtyp für Transaktionsentwicklung"
						class="h-10 rounded-xl border border-base-300 bg-base-100/70 px-3 text-sm outline-none transition-colors focus:border-[#E34747]/60"
						bind:value={trendType}
					>
						<option value="line">Linie</option>
						<option value="area">Fläche</option>
						<option value="bar">Balken</option>
					</select>
				</div>
			</div>

			<div class="relative h-80 w-full">
				{#if loading && !loaded}
					<div class="absolute inset-0 grid place-items-center text-sm text-slate-400">
						<span class="loading loading-spinner loading-sm"></span>
					</div>
				{:else}
					<canvas bind:this={trendCanvas} aria-label="Diagramm der Transaktionsentwicklung"></canvas>
				{/if}
			</div>
		</div>

		<div class="min-w-0 bg-base-100/80 p-5">
			<div class="mb-4 flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
				<div>
					<div class="flex items-center gap-2 text-sm font-semibold">
						<Clock3 size={15} class="text-slate-400" /> Aktivität nach Uhrzeit
					</div>
					<div class="mt-1 text-xs text-slate-500">
						Verteilung auf die 24 Stunden des Tages
					</div>
				</div>

				<TransactionMetricButtons bind:value={hourMetric} />
			</div>

			<div class="relative h-80 w-full">
				{#if loading && !loaded}
					<div class="absolute inset-0 grid place-items-center text-sm text-slate-400">
						<span class="loading loading-spinner loading-sm"></span>
					</div>
				{:else}
					<canvas bind:this={hourCanvas} aria-label="Balkendiagramm der Transaktionen nach Uhrzeit"></canvas>
				{/if}
			</div>
		</div>
	</div>

	<div class="flex items-center gap-2 border-t border-base-300 px-5 py-3 text-xs text-slate-500">
		<ShieldCheck size={12} />
		Die Statusgrafik verwendet alle Transaktionen; die übrigen Grafiken folgen dem gewählten Statusfilter.
	</div>
</div>
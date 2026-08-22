<script lang="ts">
	import { onMount } from 'svelte';
	import {
		Chart as ChartJS,
		CategoryScale,
		LinearScale,
		LineElement,
		PointElement,
		LineController,
		Title,
		Tooltip,
		Legend,
		Filler
	} from 'chart.js';

	ChartJS.register(
		CategoryScale,
		LinearScale,
		LineElement,
		PointElement,
		LineController,
		Title,
		Tooltip,
		Legend,
		Filler
	);

	export let labels: string[] = [];
	export let values: number[] = [];
	export let lineColor = 'rgba(59, 130, 246, 0.7)';
	export let fillColor = 'rgba(59, 130, 246, 0.08)';

	let canvas: HTMLCanvasElement;
	let chart: ChartJS | null = null;

	onMount(() => {
		if (!canvas) return;

		chart = new ChartJS(canvas, {
			type: 'line',
			data: {
				labels,
				datasets: [
					{
						label: 'Kontostand',
						data: values,
						borderColor: lineColor,
						backgroundColor: fillColor,
						fill: true,
						tension: 0.4,
						pointRadius: 0,
						pointHoverRadius: 6,
						borderWidth: 3
					}
				]
			},
			options: {
				responsive: true,
				maintainAspectRatio: false,
				plugins: {
					legend: {
						display: false
					},
					tooltip: {
						callbacks: {
							label: function (context) {
								let label = context.parsed.y || 0;
								return 'Kontostand: ' + label.toFixed(2);
							}
						}
					}
				},
				scales: {
					y: {
						beginAtZero: false,
						ticks: {
							callback: function (value) {
								return (Number(value) || 0).toFixed(0);
							}
						}
					},
					x: {
						display: false
					}
				}
			}
		});

		return () => {
			if (chart) {
				chart.destroy();
			}
		};
	});

	$: if (chart) {
		chart.data.labels = labels;
		chart.data.datasets[0].data = values;
		chart.update();
	}
</script>

<div class="w-full h-28">
	<canvas class="block w-full h-full max-w-full" bind:this={canvas}></canvas>
</div>

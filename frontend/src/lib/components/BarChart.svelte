<script lang="ts">
	import { onMount } from 'svelte';
	import {
		Chart as ChartJS,
		CategoryScale,
		LinearScale,
		BarElement,
		Title,
		Tooltip,
		Legend,
		Filler
	} from 'chart.js';
	import { BarController } from 'chart.js';

	ChartJS.register(
		CategoryScale,
		LinearScale,
		BarElement,
		BarController,
		Title,
		Tooltip,
		Legend,
		Filler
	);

	export let labels: string[] = [];
	export let values: number[] = [];
	export let positiveColor = 'rgba(74, 222, 128, 0.6)';
	export let negativeColor = 'rgba(239, 68, 68, 0.3)';

	let canvas: HTMLCanvasElement;
	let chart: ChartJS | null = null;

	onMount(() => {
		if (!canvas) return;

		const backgroundColors = values.map((v) => (v >= 0 ? positiveColor : negativeColor));

		chart = new ChartJS(canvas, {
			type: 'bar',
			data: {
				labels,
				datasets: [
					{
						label: 'Netto pro Tag',
						data: values,
						backgroundColor: backgroundColors,
						borderRadius: 4,
						borderSkipped: false
					}
				]
			},
			options: {
				responsive: true,
				maintainAspectRatio: false,
				indexAxis: 'x',
				plugins: {
					legend: {
						display: false
					},
					tooltip: {
						callbacks: {
							label: function (context) {
								let label = context.parsed.y || 0;
								return (label >= 0 ? '+ ' : '− ') + label.toFixed(2);
							}
						}
					}
				},
				scales: {
					y: {
						beginAtZero: true,
						ticks: {
							callback: function (value) {
								return ((Number(value) || 0) >= 0 ? '+' : '') + (Number(value) || 0);
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
		chart.data.datasets[0].backgroundColor = values.map((v) =>
			v >= 0 ? positiveColor : negativeColor
		);
		chart.update();
	}
</script>

<div class="w-full h-28">
	<canvas class="block w-full h-full max-w-full" bind:this={canvas}></canvas>
</div>

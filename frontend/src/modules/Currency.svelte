<script>
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';
	import PocketBase from 'pocketbase';
	import { getFile, getSetting } from '$lib/settings';
	import db from '$lib/connections';

	let currency = '';
	let currencyImage = '';

	// size: can be a number (pixels) or a string with unit (e.g. "1.25rem" or "24px")
	export let size = 20;

	// computed CSS dimension string, updated when `size` changes
	$: dimension = (() => {
		if (typeof size === 'number') return `${size}px`;
		if (typeof size === 'string') {
			const n = Number(size);
			if (!isNaN(n)) return `${n}px`; // numeric string -> px
			return size; // assume already has unit
		}
		return '20px';
	})();

	onMount(async () => {
		if (!browser) return;
		try {
			const settingsItem = await getSetting('currency');
			if (settingsItem) {
				currency = settingsItem;
				if (currency === 'file') {
					// getFile might be async — await if it returns a promise
					try {
						const fileUrl = await getFile('currency', { thumb: '100x100' });
						currencyImage = fileUrl;
					} catch (e) {
						console.error('Error loading currency file:', e);
						currencyImage = '';
					}
				}
			} else {
				currency = '€';
			}
		} catch (error) {
			console.error('Error fetching currency content:', error);
			currency = '€';
		}
	});
</script>

<!-- Class is applied to this div -->
<div class={$$props.class}>
	{#if currency === 'file' && currencyImage}
		<img
			src={currencyImage}
			alt="Currency"
			style="width: {dimension}; height: {dimension}; object-fit: contain;"
		/>
	{:else}
		<span
			style="display:inline-block; width: {dimension}; height: {dimension}; line-height: {dimension}; text-align:center; font-size: calc({dimension} * 0.75);"
		>
			{currency}
		</span>
	{/if}
</div>

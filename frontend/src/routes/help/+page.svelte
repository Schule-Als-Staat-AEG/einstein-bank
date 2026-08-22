<script lang="ts">
	import { browser } from '$app/environment';
	import db from '$lib/connections';
	import Site from '$modules/Site.svelte';
	import { onMount } from 'svelte';

	let loggedIn: boolean = false;
	let helpItems: any[] = [];

	onMount(async () => {
		if (!browser) return;
		loggedIn = db.authStore.isValid;
		await fetchHelpItems();
	});

	async function fetchHelpItems() {
		const results = await db.collection('help_items').getFullList(500, {
			sort: '+position'
		});
		helpItems = results;
	}
</script>

<title>Hilfe</title>
{#if loggedIn}
	<Site>
		<div class="w-full items-center justify-center flex flex-col gap-2">
			<h1 class="text-3xl font-bold mt-6 text-primary">Hilfe & Support</h1>
			<p class="text-base-content/70">Hier kannst du Hilfe zu verschiedenen Themen finden.</p>

			<div class="join join-vertical bg-base-100 w-full mt-4 max-w-4xl shadow-lg">
				{#each helpItems as item}
					<div
						class="collapse collapse-arrow join-item border-base-300 border hover:bg-base-200/50 transition-colors"
					>
						<input type="radio" name="help" />
						<div class="collapse-title font-semibold text-lg text-base-content">{item.title}</div>
						<div class="collapse-content text-sm text-base-content/80">
							<div
								class="help-content p-4 bg-base-200/40 rounded-md border border-base-300 transition-shadow hover:shadow-lg hover:bg-base-200/60 cursor-pointer"
							>
								{@html item.content}
							</div>
						</div>
					</div>
				{/each}
			</div>
			<p class="text-base-content/70">Du hast hier keine Antwort auf deine Frage gefunden?</p>
			<a href="/support/new" class="text-blue-500 hover:underline">Support kontaktieren</a>
		</div>
	</Site>
{:else}
	<div class="w-full items-center justify-center flex flex-col gap-2 px-4">
		<h1 class="text-3xl font-bold mt-6 text-primary">Hilfe & Support</h1>
		<p class="text-base-content/70">Hier kannst du Hilfe zu verschiedenen Themen finden.</p>

		<div class="join join-vertical bg-base-100 w-full mt-4 max-w-4xl shadow-lg">
			{#each helpItems as item}
				<div
					class="collapse collapse-arrow join-item border-base-300 border hover:bg-base-200/50 transition-colors"
				>
					<input type="radio" name="help" />
					<div class="collapse-title font-semibold text-lg text-base-content">{item.title}</div>
					<div class="collapse-content text-sm text-base-content/80">
						<div
							class="help-content p-4 bg-base-200/40 rounded-md border border-base-300 transition-shadow hover:shadow-lg hover:bg-base-200/60 cursor-pointer"
						>
							{@html item.content}
						</div>
					</div>
				</div>
			{/each}
		</div>
	</div>
{/if}

<style>
	:global(.help-content) {
		transition:
			box-shadow 0.15s ease,
			transform 0.12s ease,
			background-color 0.12s ease;
	}
	:global(.help-content a) {
		color: rgba(59, 130, 246, 1);
		text-decoration: underline;
	}
	:global(.help-content pre) {
		background: rgba(0, 0, 0, 0.04);
		padding: 0.75rem;
		border-radius: 0.375rem;
		overflow: auto;
	}
	:global(.help-content code) {
		background: rgba(0, 0, 0, 0.04);
		padding: 0.125rem 0.25rem;
		border-radius: 0.25rem;
		font-family:
			ui-monospace, SFMono-Regular, Menlo, Monaco, 'Roboto Mono', 'Courier New', monospace;
		font-size: 0.875em;
	}
</style>

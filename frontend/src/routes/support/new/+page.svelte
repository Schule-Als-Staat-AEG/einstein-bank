<script lang="ts">
	import { getAuthStore } from '$lib/auth';
	import db from '$lib/connections';
	import Site from '$modules/Site.svelte';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';
	import { goto } from '$app/navigation';

	import { Carta, MarkdownEditor } from 'carta-md';
	import { emoji } from '@cartamd/plugin-emoji';
	import { slash } from '@cartamd/plugin-slash';
	import { code } from '@cartamd/plugin-code';
	import '$lib/styles/github.scss';

	let title = '';
	let content = '';
	let selectedCategory = '';
	let loading = false;

	let categoryModal: HTMLDialogElement;

	const categories = [
		{ value: 'money_problem', label: 'Geldproblem/Betrug', color: 'error' },
		{ value: 'harassment', label: 'Belästigung/Beleidigung/Illegales Verhalten', color: 'warning' },
		{ value: 'question', label: 'Frage', color: 'info' },
		{ value: 'technical', label: 'Technisches Problem/Bug', color: 'success' },
		{ value: 'other', label: 'Anderes', color: 'accent' }
	] as const;

	const carta = new Carta({
		sanitizer: false,
		extensions: [emoji(), slash(), code()]
	});

	onMount(() => {
		if (!browser) return;
		document.title = 'Neues Support Ticket';
	});

	function openCategoryModal() {
		categoryModal?.showModal();
	}

	function selectCategory(cat: string) {
		selectedCategory = cat;
		categoryModal?.close();
	}

	function getCategoryLabel(cat: string) {
		return categories.find((c) => c.value === cat)?.label || 'Kategorie wählen';
	}

	function getCategoryColor(cat: string) {
		return categories.find((c) => c.value === cat)?.color || 'ghost';
	}

	function badgeClassFor(cat: string) {
		// DaisyUI expects: badge badge-error / badge-warning / ...
		return `badge badge-${getCategoryColor(cat)} badge-sm`;
	}

	async function createTicket() {
		if (loading) return;

		if (!title.trim()) {
			alert('Bitte gib einen Titel ein.');
			return;
		}
		if (!content.trim()) {
			alert('Bitte gib eine Beschreibung ein.');
			return;
		}
		if (!selectedCategory) {
			alert('Bitte wähle eine Kategorie aus.');
			return;
		}

		loading = true;

		try {
			const ticket = await db.collection('support_tickets').create({
				title,
				content,
				category: selectedCategory,
				status: 'open',
				user: getAuthStore().record.id
			});

			await goto(`/support/${ticket.id}`);
		} catch (error) {
			console.error('Error creating ticket:', error);
			alert('Fehler beim Erstellen des Tickets. Bitte versuche es erneut.');
		} finally {
			loading = false;
		}
	}
</script>

<title>Neues Support Ticket</title>
<Site>
	<div class="flex flex-col gap-4">
		<div class="flex items-start justify-between gap-3">
			<div>
				<h1 class="text-2xl font-bold">Neues Support Ticket erstellen</h1>
				<p class="text-sm opacity-70">
					Beschreibe dein Anliegen so detailliert wie möglich, damit wir dir schnell helfen können.
				</p>
			</div>
		</div>

		<!-- Title -->
		<div class="form-control">
			<!-- svelte-ignore a11y_label_has_associated_control -->
			<label class="label">
				<span class="label-text font-semibold">Titel</span>
			</label>
			<input
				type="text"
				class="input input-bordered w-full"
				placeholder="z.B. Problem mit Überweisung"
				bind:value={title}
				disabled={loading}
			/>
		</div>

		<!-- Category -->
		<div class="form-control">
			<!-- svelte-ignore a11y_label_has_associated_control -->
			<label class="label">
				<span class="label-text font-semibold">Kategorie</span>
			</label>

			<button
				type="button"
				class="btn btn-outline w-full justify-start"
				on:click={openCategoryModal}
				disabled={loading}
			>
				{#if selectedCategory}
					<span class={badgeClassFor(selectedCategory)}>{getCategoryLabel(selectedCategory)}</span>
				{:else}
					<span class="opacity-50">Kategorie auswählen</span>
				{/if}
			</button>
		</div>

		<!-- Description -->
		<div class="form-control">
			<!-- svelte-ignore a11y_label_has_associated_control -->
			<label class="label">
				<span class="label-text font-semibold">Beschreibung</span>
			</label>

			<div class="bg-base-200 p-4 rounded-2xl border border-base-300">
				<MarkdownEditor mode="tabs" bind:value={content} {carta} theme="github" />
				<p class="text-sm opacity-50 mt-2 italic">Dieser Editor unterstützt Markdown</p>
			</div>
		</div>

		<!-- Submit -->
		<div class="flex gap-2 justify-end mt-2">
			<a href="/support" class="btn btn-ghost" aria-disabled={loading}>Abbrechen</a>
			<button class="btn btn-primary" on:click={createTicket} disabled={loading}>
				{#if loading}
					<span class="loading loading-spinner loading-sm"></span>
					Erstelle…
				{:else}
					Ticket erstellen
				{/if}
			</button>
		</div>
	</div>
</Site>

<!-- Category Selection Modal -->
<dialog class="modal" bind:this={categoryModal}>
	<form method="dialog" class="modal-box">
		<h3 class="font-bold text-lg mb-4">Kategorie auswählen</h3>

		<div class="flex flex-col gap-2">
			{#each categories as cat}
				<button
					type="button"
					class="btn btn-outline justify-start"
					on:click={() => selectCategory(cat.value)}
				>
					<span class={`badge badge-${cat.color} badge-sm`}>{cat.label}</span>
				</button>
			{/each}
		</div>

		<div class="modal-action">
			<button class="btn">Schließen</button>
		</div>
	</form>

	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

<script lang="ts">
	import { getAuthStore } from '$lib/auth';
	import db from '$lib/connections';
	import Site from '$modules/Site.svelte';
	import { ExternalLink } from '@lucide/svelte';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';
	import { goto } from '$app/navigation';

	let tickets: any[] = [];
	let loading = true;

	onMount(async () => {
		if (!browser) return;
		document.title = 'Support';
		await fetchTickets();
		loading = false;
	});

	async function fetchTickets() {
		tickets = await db.collection('support_tickets').getFullList({
			sort: '-status,-created',
			filter: `user.id = "${getAuthStore().record.id}"`
		});
	}

	function formatDate(dateString: string) {
		const date = new Date(dateString);
		return date.toLocaleDateString('de-DE', {
			year: 'numeric',
			month: '2-digit',
			day: '2-digit',
			hour: '2-digit',
			minute: '2-digit'
		});
	}

	function limitText(text: string = '', maxLength: number) {
		if (!text) return '';
		if (text.length <= maxLength) return text;
		return text.slice(0, maxLength) + '...';
	}

	function getCategoryLabel(category: string) {
		switch (category) {
			case 'money_problem':
				return 'Geldproblem / Betrug';
			case 'harassment':
				return 'Belästigung / Illegales Verhalten';
			case 'question':
				return 'Frage';
			case 'technical':
				return 'Technisches Problem / Bug';
			default:
				return 'Anderes';
		}
	}

	function getCategoryColor(category: string) {
		switch (category) {
			case 'money_problem':
				return 'var(--color-error)';
			case 'harassment':
				return 'var(--color-warning)';
			case 'question':
				return 'var(--color-info)';
			case 'technical':
				return 'var(--color-success)';
			default:
				return 'var(--color-accent)';
		}
	}

	function toPlainText(htmlString: string = '') {
		const tempDiv = document.createElement('div');
		tempDiv.innerHTML = htmlString;
		return tempDiv.textContent || '';
	}
</script>

<title>Support</title>
<Site>
	<div class="flex justify-between items-center mb-4">
		<div>
			<h1 class="text-2xl font-bold">Support</h1>
			<p class="opacity-70">Hier findest du deine Support-Anfragen</p>
		</div>
		<a href="/support/new" class="btn btn-primary"> Neues Ticket erstellen </a>
	</div>

	{#if loading}
		<div class="text-center opacity-70 py-10">Lade Tickets…</div>
	{:else if tickets.length === 0}
		<div class="text-center opacity-70 py-10">Du hast noch keine Support-Tickets erstellt.</div>
	{:else}
		<div class="overflow-x-auto mt-4">
			<table class="table">
				<thead>
					<tr>
						<th>ID</th>
						<th>Titel & Kategorie</th>
						<th>Info</th>
						<th>Erstellt & Status</th>
						<th>Aktionen</th>
					</tr>
				</thead>
				<tbody>
					{#each tickets as ticket}
						<tr>
							<td class="font-mono opacity-70">
								#{ticket.id}
							</td>

							<td>
								<div class="flex flex-col gap-1">
									<span>{limitText(ticket.title, 30)}</span>
									<span
										class="badge badge-sm badge-ghost"
										style="background-color: {getCategoryColor(ticket.category)}"
									>
										{getCategoryLabel(ticket.category)}
									</span>
								</div>
							</td>

							<td class="opacity-80">
								{limitText(toPlainText(ticket.content), 50)}
							</td>

							<td>
								{formatDate(ticket.created)}
								<br />

								{#if ticket.status === 'open'}
									<span class="badge badge-sm badge-warning">Offen</span>
								{:else if ticket.status === 'in_progress'}
									<span class="badge badge-sm badge-info">In Bearbeitung</span>
								{:else if ticket.status === 'closed'}
									<span class="badge badge-sm badge-success">Geschlossen</span>
								{/if}
							</td>

							<td>
								<button
									class="btn btn-ghost btn-xs"
									on:click={() => goto('/support/' + ticket.id)}
									title="Ticket öffnen"
								>
									<ExternalLink size={16} />
								</button>
							</td>
						</tr>
					{/each}
				</tbody>
			</table>
		</div>
	{/if}
</Site>

<script lang="ts">
	import { browser } from '$app/environment';
	import db from '$lib/connections';
	import AdminPanel from '$modules/AdminPanel.svelte';
	import { Briefcase, ExternalLink, LockOpen, TrashIcon, Lock } from '@lucide/svelte';
	import { onMount, tick } from 'svelte';

	let tickets: any[] = [];
	let page = 1;
	let perPage = 30;
	let totalPages = 1;
	let loading = true;
	let totalItems = 0;

	let filter = '';
	onMount(async () => {
		if (!browser) return;
		document.title = 'Admin - Support Tickets';
		await fetchTickets();
	});
	async function fetchTickets() {
		loading = true;
		// Get paginated tickets
		const result = await db.collection('support_tickets').getList(page, perPage, {
			filter: `title~"${filter}" || user.name~"${filter}" || id~"${filter}"`,
			sort: '-created'
		});
		totalPages = result.totalPages;
		totalItems = result.totalItems || 0;
		tickets = result.items;

		await Promise.all(
			tickets.map(async (ticket) => {
				// Get the last user message to check if read is false
				const messages = await db.collection('support_messages').getFullList({
					filter: `ticket="${ticket.id}" && sender!="admin"`,
					sort: '-created',
					limit: 1,
					requestKey: null
				});
				ticket.unread = messages.length > 0 && messages[0].read === false;
			})
		);
		tickets = tickets.sort((a, b) => {
			const statusOrder = { in_progress: 0, open: 1, closed: 2 };
			if (statusOrder[a.status] !== statusOrder[b.status]) {
				return statusOrder[a.status] - statusOrder[b.status];
			}
			return new Date(b.created).getTime() - new Date(a.created).getTime();
		});
		tickets = [...tickets]; // Trigger reactivity
		loading = false;
	}
	function goToPage(newPage: number) {
		if (newPage < 1 || newPage > totalPages) return;
		page = newPage;
		fetchTickets();
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
	function limitText(text: string, maxLength: number) {
		if (text.length <= maxLength) return text;
		return text.slice(0, maxLength) + '...';
	}
	function getCategoryLabel(category: string) {
		switch (category) {
			default:
				return 'Anderes';
			case 'money_problem':
				return 'Geldproblem/Betrug';
			case 'harassment':
				return 'Belästigung/Beleidigung/Illegales Verhalten';
			case 'question':
				return 'Frage';
			case 'technical':
				return 'Technisches Problem/Bug';
		}
	}
	function getCategoryColor(category: string) {
		switch (category) {
			default:
				return 'var(--color-accent)';
			case 'money_problem':
				return 'var(--color-error)';
			case 'harassment':
				return 'var(--color-warning)';
			case 'question':
				return 'var(--color-info)';
			case 'technical':
				return 'var(--color-success)';
		}
	}
	function toPlainText(htmlString: string) {
		const tempDiv = document.createElement('div');
		tempDiv.innerHTML = htmlString;
		return tempDiv.textContent || tempDiv.innerText || '';
	}
	async function setStatus(ticketId: string, status: string) {
		// Update ticket status
		await db.collection('support_tickets').update(ticketId, { status });
		// Refresh tickets
		await fetchTickets();
	}
	async function deleteTicket(ticketId: string) {
		const confirmDelete = confirm(
			'Möchten Sie dieses Ticket wirklich löschen? Diese Aktion kann nicht rückgängig gemacht werden.'
		);
		if (!confirmDelete) {
			return;
		}
		// Delete ticket
		await db.collection('support_tickets').delete(ticketId);
		// Refresh tickets
		await fetchTickets();
	}
</script>

<title>Admin - Support Tickets</title>
<AdminPanel title="Support Tickets" description="Verwaltung aller Support Tickets" {totalItems}>
	<svelte:fragment slot="filter">
		<div class="flex gap-2 items-center w-full justify-between">
			<input
				type="text"
				placeholder="Suchen..."
				class="input input-bordered w-full"
				autocomplete="off"
				bind:value={filter}
				oninput={() => {
					page = 1;
					fetchTickets();
				}}
			/>
			<select
				class="select select-bordered"
				bind:value={perPage}
				onchange={() => {
					page = 1;
					fetchTickets();
				}}
			>
				<option value="15">15 / Seite</option>
				<option value="30">30 / Seite</option>
				<option value="50">50 / Seite</option>
				<option value="100">100 / Seite</option>
				<option value="200">200 / Seite</option>
				<option value="500">500 / Seite</option>
			</select>

			<div class="flex-1"></div>
		</div>
		{#if loading}
			<div class="text-sm opacity-70">Lade Tickets…</div>
		{/if}
	</svelte:fragment>

	{#if !loading}
		<div class="overflow-x-auto rounded-2xl border border-base-300">
			<table class="table table-zebra w-full">
				<!-- head -->
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
							<td>
								<div class="flex items-center gap-3">
									<div>
										<p class="font-mono opacity-70">#{ticket.id}</p>
									</div>
								</div>
							</td>
							<td>
								<div class="flex flex-col">
									{#if ticket.unread}
										{limitText(ticket.title, 30)}
									{:else}
										<span class="opacity-70">{limitText(ticket.title, 30)}</span>
									{/if}
									<span
										class="badge badge-ghost badge-sm"
										style="background-color: {getCategoryColor(ticket.category)}"
										>{getCategoryLabel(ticket.category)}</span
									>
								</div>
							</td>
							<td>{limitText(toPlainText(ticket.content), 50)}</td>
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
							<th>
								<div class="flex flex-row">
									<button
										class="btn btn-ghost btn-xs"
										onclick={() => open('/admin/tickets/' + ticket.id)}
										><ExternalLink></ExternalLink></button
									>
									<button
										class="btn btn-ghost btn-xs btn-success"
										onclick={() => setStatus(ticket.id, 'open')}><LockOpen></LockOpen></button
									>
									<button
										class="btn btn-ghost btn-xs btn-warning"
										onclick={() => setStatus(ticket.id, 'in_progress')}
										><Briefcase></Briefcase></button
									>
									<button
										class="btn btn-ghost btn-xs btn-info"
										onclick={() => setStatus(ticket.id, 'closed')}><Lock></Lock></button
									>
									<button
										class="btn btn-ghost btn-xs btn-error"
										onclick={() => deleteTicket(ticket.id)}><TrashIcon></TrashIcon></button
									>
								</div>
							</th>
						</tr>
					{/each}
				</tbody>
			</table>
		</div>
	{/if}

	<div class="flex justify-center mt-4">
		<div class="join">
			<button class="join-item btn" onclick={() => goToPage(page - 1)} disabled={page <= 1}>
				«
			</button>
			<button class="join-item btn btn-ghost">
				Seite <span class="font-mono mx-1">{page}</span> /
				<span class="font-mono">{totalPages}</span>
			</button>
			<button
				class="join-item btn"
				onclick={() => goToPage(page + 1)}
				disabled={page >= totalPages}
			>
				»
			</button>
		</div>
	</div>
</AdminPanel>

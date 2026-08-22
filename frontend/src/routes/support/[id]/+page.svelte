<script lang="ts">
	import { page } from '$app/state';
	import db from '$lib/connections';
	import Site from '$modules/Site.svelte';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';
	import { on } from 'svelte/events';
	import { Carta } from 'carta-md';
	import { MarkdownEditor } from 'carta-md';
	import { emoji } from '@cartamd/plugin-emoji';
	import { slash } from '@cartamd/plugin-slash';
	import { code } from '@cartamd/plugin-code';
	import '$lib/styles/github.scss';
	import { marked } from 'marked';

	// Id is passed via the URL
	let ticketID: string;
	let ticket: any = null;
	let messages: any[] = [];
	let loading = true;
	let message = '';

	let closeTicketDialog: HTMLDialogElement;

	onMount(async () => {
		if (!browser) return;
		ticketID = page.params.id;
		document.title = `Support Ticket ${ticketID}`;
		ticket = await db.collection('support_tickets').getOne(ticketID);
		await fetchMessages();
		await setAllAsRead();

		// Subscribe to SSE for new messages
		db.collection('support_messages').subscribe('*', async (e) => {
			console.log('SSE Event:', e);
			if (e.record.ticket === ticket.id && e.action === 'create') {
				messages.push(e.record);
				messages.sort((a, b) => new Date(a.created).getTime() - new Date(b.created).getTime()); // Ensure messages are sorted
				messages = [...messages]; // Trigger reactivity
				await setAllAsRead();
				console.log('New message received via SSE:', e.record);
			} else if (e.record.ticket === ticket.id && e.action === 'update') {
				// Update message in place
				const index = messages.findIndex((m) => m.id === e.record.id);
				if (index !== -1) {
					messages[index] = e.record;
					messages = [...messages]; // Trigger reactivity
					console.log('Message updated via SSE:', e.record);
				}
			} else if (e.record.ticket === ticket.id && e.action === 'delete') {
				messages = messages.filter((m) => m.id !== e.record.id);
				messages = [...messages]; // Trigger reactivity
				console.log('Message deleted via SSE:', e.record);
			}
		});
		db.collection('support_tickets').subscribe(ticket.id, async (e) => {
			console.log('Ticket SSE Event:', e);
			if (e.action === 'update') {
				ticket = e.record;
				ticket = { ...ticket }; // Trigger reactivity
				console.log('Ticket updated via SSE:', e.record);
			}
		});
		loading = false;
	});
	async function fetchMessages() {
		if (!ticket) return [];
		const data = await db.collection('support_messages').getFullList({
			sort: 'created',
			filter: `ticket.id = "${ticket.id}"`
		});
		messages = data;
	}
	async function setAllAsRead() {
		await Promise.all(
			messages.map(async (message) => {
				if (message.sender == 'admin' && !message.read) {
					await db.collection('support_messages').update(message.id, { read: true }); // Only send over read field as otherwise the request will be rejected
				}
			})
		);
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
	const carta = new Carta({
		sanitizer: false,
		extensions: [emoji(), slash(), code()]
	});
	function sendMessage() {
		console.log('Sending message:', message);
		if (message.trim() === '') return;
		db.collection('support_messages').create({
			ticket: ticket.id,
			sender: 'user',
			message: message,
			read: false
		});
		message = '';
	}
</script>

<title>Support Ticket</title>
<Site>
	{#if loading}
		<div class="flex justify-center items-center h-32">
			<span class="loading loading-spinner loading-lg"></span>
		</div>
	{:else if !ticket}
		<h1 class="text-2xl font-bold">Support Ticket nicht gefunden</h1>
		<p>
			Das angeforderte Support Ticket existiert nicht oder du hast keine Berechtigung, es anzusehen.
		</p>
	{:else}
		<div class="flex flex-col gap-4">
			<h1 class="text-2xl font-bold">Support Ticket #{ticketID}</h1>
			<div class="flex flex-col gap-4 mt-4 bg-base-200 shadow-lg p-4 rounded-lg">
				<h2 class="text-xl font-semibold">Titel: {ticket.title}</h2>
				<div class="bg-base-300 p-4 rounded-lg">
					<span class="text-lg font-semibold">Beschreibung:</span>
					<div class="divider mt-1 mb-1"></div>
					<div>{@html ticket.content}</div>
				</div>
				<div class="flex flex-row gap-2 items-center">
					<strong>Status:</strong>
					{#if ticket.status === 'open'}
						<span class="badge badge-warning">Offen</span>
					{:else if ticket.status === 'in_progress'}
						<span class="badge badge-info">In Bearbeitung</span>
					{:else if ticket.status === 'closed'}
						<span class="badge badge-success">Geschlossen</span>
					{/if}
				</div>

				<div class="flex flex-row gap-2 items-center">
					<strong>Kategorie:</strong>
					<span class="badge" style="background-color: {getCategoryColor(ticket.category)}"
						>{getCategoryLabel(ticket.category)}</span
					>
				</div>

				<div class="flex flex-row gap-2 items-center">
					<strong>Live Chat:</strong>
					<span class="badge badge-success">Unterstützt</span>
				</div>

				<div class="flex flex-row gap-2 items-center">
					<strong>Erstellt am:</strong>
					{new Date(ticket.created).toLocaleString()}
				</div>

				{#if ticket.status !== 'closed'}
					<div>
						<button
							class="btn btn-error"
							onclick={async () => {
								closeTicketDialog.showModal();
							}}>Ticket schließen</button
						>
					</div>

					<div class="flex flex-row gap-2 items-center">
						<p class="text-sm opacity-50">
							Du brauchst keine Hilfe mehr? Kein Problem! Du kannst dein Ticket jederzeit schließen,
							indem du auf die Schaltfläche "Ticket schließen" klickst.
						</p>
					</div>
				{/if}
			</div>

			{#each messages as msg}
				<div
					class="bg-base-300 rounded-lg p-4 flex flex-col gap-2 {msg.sender === 'admin'
						? 'self-start'
						: 'self-end'} max-w-xl"
				>
					<div class="flex flex-row gap-2 items-center mb-2">
						{#if msg.sender === 'admin'}
							<span class="badge badge-error">Support Team</span>
						{:else}
							<span class="badge badge-primary">Du</span>
						{/if}
						<span class="text-sm opacity-50">{new Date(msg.created).toLocaleString()}</span>
					</div>
					<div class="prose max-w-full">
						{@html marked(msg.message)}
					</div>
				</div>
			{/each}

			{#if ticket.status !== 'closed'}
				<div class="w-full bg-base-300 p-4 rounded-lg flex flex-col gap-4">
					<MarkdownEditor mode="tabs" bind:value={message} {carta} theme="github"></MarkdownEditor>
					<button class="btn btn-success" onclick={sendMessage}>Absenden</button>
					<p class="text-sm opacity-50">Dieser Editor unterstützt Markdown</p>
				</div>
			{:else}
				<div class="alert alert-info mt-4">
					<div>
						<span
							>Dieses Ticket wurde geschlossen. Du kannst keine weiteren Nachrichten senden.</span
						>
					</div>
				</div>
			{/if}
		</div>
	{/if}
</Site>

<dialog id="closeTicketDialog" class="modal" bind:this={closeTicketDialog}>
	<form method="dialog" class="modal-box">
		<h3 class="font-bold text-lg">Ticket schließen</h3>
		<p class="py-4">
			Möchtest du dieses Ticket wirklich schließen? Wenn du das Ticket schließt musst du erst wieder
			ein neues eröffnen, falls du weitere Hilfe benötigst.
		</p>
		<div class="modal-action">
			<button class="btn">Abbrechen</button>
			<button
				class="btn btn-primary"
				onclick={async () => {
					await db.collection('support_tickets').update(ticket.id, { status: 'closed' });
					ticket.status = 'closed';
					closeTicketDialog.close();
				}}>Ticket schließen</button
			>
		</div>
	</form>
	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

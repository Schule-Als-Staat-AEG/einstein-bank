<script lang="ts">
	import { page } from '$app/state';
	import db from '$lib/connections';
	import AdminSite from '$modules/AdminSite.svelte';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';
	import { Carta } from 'carta-md';
	import { MarkdownEditor } from 'carta-md';
	import { emoji } from '@cartamd/plugin-emoji';
	import { slash } from '@cartamd/plugin-slash';
	import { code } from '@cartamd/plugin-code';
	import '$lib/styles/github.scss';
	import Currency from '$modules/Currency.svelte';
	import { marked } from 'marked';
	import DOMPurify from 'dompurify';

	// Id is passed via the URL
	let ticketID: string;
	let ticket: any = null;
	let messages: any[] = [];
	let loading = true;
	let message = '';

	// Editing states
	let editingTitle = false;
	let editTitle = '';
	let editingDescription = false;
	let editDescription = '';
	let editingCategory = false;
	let editCategory = '';
	let editingStatus = false;
	let editStatus = '';

	// Message editing
	let editingMessageId: string | null = null;
	let editMessageContent = '';

	// Dialogs
	let deleteMessageDialog: HTMLDialogElement;
	let messageToDelete: any = null;

	let userAccountID: string = '';

	let moneyInAccount: number = 0;

	const carta = new Carta({
		sanitizer: false,
		extensions: [emoji(), slash(), code()]
	});

	onMount(async () => {
		if (!browser) return;
		ticketID = page.params.id;
		document.title = `Admin - Ticket ${ticketID}`;
		ticket = await db.collection('support_tickets').getOne(ticketID, {
			expand: 'user'
		});
		let userAccount = await db.collection('accounts').getFirstListItem(`user = "${ticket.user}"`);
		userAccountID = userAccount.id;
		moneyInAccount = userAccount.balance || 0;
		await fetchMessages();

		// Subscribe to SSE for real-time updates
		db.collection('support_messages').subscribe('*', async (e) => {
			if (e.record.ticket === ticket.id && e.action === 'create') {
				messages.push(e.record);
				messages.sort((a, b) => new Date(a.created).getTime() - new Date(b.created).getTime());
				messages = [...messages];
			} else if (e.record.ticket === ticket.id && e.action === 'update') {
				const index = messages.findIndex((m) => m.id === e.record.id);
				if (index !== -1) {
					messages[index] = e.record;
					messages = [...messages];
				}
			} else if (e.record.ticket === ticket.id && e.action === 'delete') {
				messages = messages.filter((m) => m.id !== e.record.id);
				messages = [...messages];
			}
		});

		db.collection('support_tickets').subscribe(ticketID, async (e) => {
			if (e.action === 'update') {
				ticket = e.record;
				// Get the user and expand again
				ticket.expand = ticket.expand || {};
				ticket.expand.user = ticket.expand.user || {};
				ticket.expand.user = await db.collection('users').getOne(ticket.user);
				userAccountID = (
					await db.collection('accounts').getFirstListItem(`user = "${ticket.user}"`)
				).id;
				ticket = { ...ticket };
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

	function getCategoryLabel(category: string) {
		switch (category) {
			case 'money_problem':
				return 'Geldproblem/Betrug';
			case 'harassment':
				return 'Belästigung/Beleidigung/Illegales Verhalten';
			case 'question':
				return 'Frage';
			case 'technical':
				return 'Technisches Problem/Bug';
			case 'other':
			default:
				return 'Anderes';
		}
	}

	function getCategoryColor(category: string) {
		switch (category) {
			case 'money_problem':
				return 'error';
			case 'harassment':
				return 'warning';
			case 'question':
				return 'info';
			case 'technical':
				return 'success';
			case 'other':
			default:
				return 'accent';
		}
	}

	function sendMessage() {
		if (message.trim() === '') return;
		db.collection('support_messages').create({
			ticket: ticket.id,
			sender: 'admin',
			message: message,
			read: false
		});
		message = '';
	}

	// --- Title Editing ---
	function startEditTitle() {
		editTitle = ticket.title;
		editingTitle = true;
	}

	async function saveTitle() {
		await db.collection('support_tickets').update(ticket.id, { title: editTitle });
		ticket.title = editTitle;
		editingTitle = false;
	}

	function cancelEditTitle() {
		editingTitle = false;
	}

	// --- Description Editing ---
	function startEditDescription() {
		editDescription = ticket.content;
		editingDescription = true;
	}

	async function saveDescription() {
		await db.collection('support_tickets').update(ticket.id, { content: editDescription });
		ticket.content = editDescription;
		editingDescription = false;
	}

	function cancelEditDescription() {
		editingDescription = false;
	}

	// --- Category Editing ---
	function startEditCategory() {
		editCategory = ticket.category;
		editingCategory = true;
	}

	async function saveCategory() {
		await db.collection('support_tickets').update(ticket.id, { category: editCategory });
		ticket.category = editCategory;
		editingCategory = false;
	}

	function cancelEditCategory() {
		editingCategory = false;
	}

	// --- Status Editing ---
	function startEditStatus() {
		editStatus = ticket.status;
		editingStatus = true;
	}

	async function saveStatus() {
		await db.collection('support_tickets').update(ticket.id, { status: editStatus });
		ticket.status = editStatus;
		editingStatus = false;
	}

	function cancelEditStatus() {
		editingStatus = false;
	}

	// --- Message Editing ---
	function startEditMessage(msg: any) {
		editingMessageId = msg.id;
		editMessageContent = msg.message;
	}

	async function saveMessage(msgId: string) {
		await db.collection('support_messages').update(msgId, { message: editMessageContent });
		const index = messages.findIndex((m) => m.id === msgId);
		if (index !== -1) {
			messages[index].message = editMessageContent;
			messages = [...messages];
		}
		editingMessageId = null;
	}

	function cancelEditMessage() {
		editingMessageId = null;
	}

	// --- Message Deletion ---
	function confirmDeleteMessage(msg: any) {
		messageToDelete = msg;
		deleteMessageDialog.showModal();
	}

	async function deleteMessage() {
		if (messageToDelete) {
			await db.collection('support_messages').delete(messageToDelete.id);
			messages = messages.filter((m) => m.id !== messageToDelete.id);
			messageToDelete = null;
		}
		deleteMessageDialog.close();
	}

	// --- User ID Copy ---
	async function copyUserId() {
		if (ticket.expand?.user?.id) {
			await navigator.clipboard.writeText(ticket.expand.user.id);
			alert('User ID kopiert!');
		}
	}

	function goToUserAdmin() {
		if (ticket.expand?.user?.id) {
			window.location.href = `/admin/users?id=${ticket.expand.user.id}`;
		}
	}
	function sanitize(input: string) {
		return DOMPurify.sanitize(input);
	}
	function toHtml(input: string) {
		return marked.parse(input).toString();
	}
</script>

<AdminSite>
	{#if loading}
		<div class="flex justify-center items-center h-32">
			<span class="loading loading-spinner loading-lg"></span>
		</div>
	{:else if !ticket}
		<h1 class="text-2xl font-bold">Support Ticket nicht gefunden</h1>
		<p>Das angeforderte Support Ticket existiert nicht.</p>
	{:else}
		<div class="flex flex-col gap-4">
			<h1 class="text-2xl font-bold">Admin - Support Ticket #{ticketID}</h1>

			<!-- User Info Card -->
			{#if ticket.expand?.user}
				<div class="card w-full bg-base-200 shadow-lg">
					<div class="card-body">
						<h2 class="card-title">Benutzer-Informationen</h2>
						<div class="grid grid-cols-1 md:grid-cols-2 gap-2">
							<div><strong>Name:</strong> {ticket.expand.user.name || 'N/A'}</div>
							<div><strong>Email:</strong> {ticket.expand.user.email || 'N/A'}</div>
							<div>
								<strong>Firma:</strong>
								{ticket.expand.user.isCompany ? 'Ja' : 'Nein'}{ticket.expand.user.isCompany
									? ' - ' + ticket.expand.user.companyName
									: ''}
							</div>
							<div><strong>User ID:</strong> {ticket.expand.user.id}</div>
							<div><strong>Konto ID:</strong> {userAccountID}</div>
							<div class="flex flex-row gap-1">
								<strong>Kontostand:</strong>
								<div class="flex flex-row">{moneyInAccount.toFixed(2)} <Currency></Currency></div>
							</div>
						</div>
						<div class="flex gap-2 mt-2">
							<button class="btn btn-sm btn-primary" onclick={copyUserId}>User ID kopieren</button>
							<button class="btn btn-sm btn-secondary" onclick={goToUserAdmin}>Zum Nutzer</button>
							<button
								class="btn btn-sm btn-accent"
								onclick={() => open(`/admin/accounts?id=${userAccountID}`)}>Zum Konto</button
							>
						</div>
					</div>
				</div>
			{/if}

			<!-- Ticket Details Card -->
			<div class="card w-full bg-base-200 shadow-lg">
				<div class="card-body">
					<!-- Title -->
					<div class="flex items-center gap-2">
						{#if editingTitle}
							<input
								class="input input-bordered flex-1"
								type="text"
								autocomplete="off"
								bind:value={editTitle}
							/>
							<button class="btn btn-sm btn-success" onclick={saveTitle}>Speichern</button>
							<button class="btn btn-sm btn-ghost" onclick={cancelEditTitle}>Abbrechen</button>
						{:else}
							<h2 class="text-xl font-semibold flex-1">Titel: {ticket.title}</h2>
							<button class="btn btn-sm btn-ghost" onclick={startEditTitle}>Bearbeiten</button>
						{/if}
					</div>

					<!-- Description -->
					<div class="bg-base-300 p-4 rounded-lg mt-2">
						<div class="flex items-center justify-between mb-2">
							<span class="text-lg font-semibold">Beschreibung:</span>
							{#if !editingDescription}
								<button class="btn btn-sm btn-ghost" onclick={startEditDescription}
									>Bearbeiten</button
								>
							{/if}
						</div>
						<div class="divider mt-1 mb-1"></div>
						{#if editingDescription}
							<MarkdownEditor mode="tabs" bind:value={editDescription} {carta} theme="github"
							></MarkdownEditor>
							<div class="flex gap-2 mt-2">
								<button class="btn btn-sm btn-success" onclick={saveDescription}>Speichern</button>
								<button class="btn btn-sm btn-ghost" onclick={cancelEditDescription}
									>Abbrechen</button
								>
							</div>
						{:else}
							<div class="prose max-w-full">{@html sanitize(ticket.content)}</div>
						{/if}
					</div>

					<!-- Status -->
					<div class="flex items-center gap-2 mt-2">
						<strong>Status:</strong>
						{#if editingStatus}
							<select class="select select-bordered select-sm" bind:value={editStatus}>
								<option value="open">Offen</option>
								<option value="in_progress">In Bearbeitung</option>
								<option value="closed">Geschlossen</option>
							</select>
							<button class="btn btn-sm btn-success" onclick={saveStatus}>Speichern</button>
							<button class="btn btn-sm btn-ghost" onclick={cancelEditStatus}>Abbrechen</button>
						{:else}
							{#if ticket.status === 'open'}
								<span class="badge badge-warning">Offen</span>
							{:else if ticket.status === 'in_progress'}
								<span class="badge badge-info">In Bearbeitung</span>
							{:else if ticket.status === 'closed'}
								<span class="badge badge-success">Geschlossen</span>
							{/if}
							<button class="btn btn-sm btn-ghost" onclick={startEditStatus}>Bearbeiten</button>
						{/if}
					</div>

					<!-- Category -->
					<div class="flex items-center gap-2 mt-2">
						<strong>Kategorie:</strong>
						{#if editingCategory}
							<select class="select select-bordered select-sm" bind:value={editCategory}>
								<option value="money_problem">Geldproblem/Betrug</option>
								<option value="harassment">Belästigung/Beleidigung</option>
								<option value="question">Frage</option>
								<option value="technical">Technisches Problem/Bug</option>
								<option value="other">Anderes</option>
							</select>
							<button class="btn btn-sm btn-success" onclick={saveCategory}>Speichern</button>
							<button class="btn btn-sm btn-ghost" onclick={cancelEditCategory}>Abbrechen</button>
						{:else}
							<span class="badge badge-{getCategoryColor(ticket.category)}"
								>{getCategoryLabel(ticket.category)}</span
							>
							<button class="btn btn-sm btn-ghost" onclick={startEditCategory}>Bearbeiten</button>
						{/if}
					</div>

					<div class="flex items-center gap-2 mt-2">
						<strong>Erstellt am:</strong>
						{new Date(ticket.created).toLocaleString()}
					</div>
				</div>
			</div>

			<!-- Messages -->
			<h2 class="text-xl font-bold mt-4">Nachrichten</h2>
			{#each messages as msg}
				<div
					class="bg-base-300 rounded-lg p-4 flex flex-col gap-2 {msg.sender === 'admin'
						? 'self-start'
						: 'self-end'} max-w-2xl w-full"
				>
					<div class="flex flex-row gap-2 items-center justify-between mb-2">
						<div class="flex gap-2 items-center">
							{#if msg.sender === 'admin'}
								<span class="badge badge-error">Support Team</span>
							{:else}
								<span class="badge badge-primary">User</span>
							{/if}
							<span class="text-sm opacity-50">{new Date(msg.created).toLocaleString()}</span>
						</div>
						<div class="flex gap-1">
							{#if editingMessageId !== msg.id}
								<button class="btn btn-xs btn-ghost" onclick={() => startEditMessage(msg)}
									>Bearbeiten</button
								>
								<button class="btn btn-xs btn-error" onclick={() => confirmDeleteMessage(msg)}
									>Löschen</button
								>
							{/if}
						</div>
					</div>

					{#if editingMessageId === msg.id}
						<MarkdownEditor mode="tabs" bind:value={editMessageContent} {carta} theme="github"
						></MarkdownEditor>
						<div class="flex gap-2 mt-2">
							<button class="btn btn-sm btn-success" onclick={() => saveMessage(msg.id)}
								>Speichern</button
							>
							<button class="btn btn-sm btn-ghost" onclick={cancelEditMessage}>Abbrechen</button>
						</div>
					{:else}
						<div class="prose max-w-full">
							{@html sanitize(toHtml(msg.message))}
						</div>
					{/if}
				</div>
			{/each}

			<!-- Send Message -->
			<div class="w-full bg-base-300 p-4 rounded-lg flex flex-col gap-4 mt-4">
				<h3 class="text-lg font-semibold">Nachricht senden (als Admin)</h3>
				<MarkdownEditor mode="tabs" bind:value={message} {carta} theme="github"></MarkdownEditor>
				<button class="btn btn-success" onclick={sendMessage}>Absenden</button>
				<p class="text-sm opacity-50">Dieser Editor unterstützt Markdown</p>
			</div>
		</div>
	{/if}
</AdminSite>

<!-- Delete Message Dialog -->
<dialog id="deleteMessageDialog" class="modal" bind:this={deleteMessageDialog}>
	<form method="dialog" class="modal-box">
		<h3 class="font-bold text-lg">Nachricht löschen</h3>
		<p class="py-4">
			Möchtest du diese Nachricht wirklich löschen? Diese Aktion kann nicht rückgängig gemacht
			werden.
		</p>
		<div class="modal-action">
			<button class="btn">Abbrechen</button>
			<button class="btn btn-error" onclick={deleteMessage}>Löschen</button>
		</div>
	</form>
	<form method="dialog" class="modal-backdrop">
		<button>close</button>
	</form>
</dialog>

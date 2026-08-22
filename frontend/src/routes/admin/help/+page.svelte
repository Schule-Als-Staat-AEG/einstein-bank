<script lang="ts">
	import AdminSite from '$modules/AdminSite.svelte';
	import db from '$lib/connections';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';
	import { Editor } from '@tiptap/core';
	import StarterKit from '@tiptap/starter-kit';
	import Link from '@tiptap/extension-link';
	import Underline from '@tiptap/extension-underline';
	import TextAlign from '@tiptap/extension-text-align';
	import {
		Bold,
		Italic,
		Strikethrough,
		List,
		ListOrdered,
		Code,
		Link as LinkIcon,
		Move,
		Trash2,
		Type,
		TextAlignEnd,
		Heading1,
		Heading2,
		Heading3
	} from '@lucide/svelte';
	import { Baseline, Braces, Pencil, TextAlignCenter, TextAlignStart } from '@lucide/svelte';

	let items: Array<any> = [];
	let loading = false;
	let showModal = false;
	let editing: any = null;

	// form fields
	let title = '';
	let contentHTML = '';
	let editor: Editor | null = null;
	let editorElement: HTMLElement;

	// Drag & drop
	let dragIndex: number | null = null;

	onMount(() => {
		if (!browser) return;
		loadItems();
	});

	async function loadItems() {
		loading = true;
		try {
			const res = await db.collection('help_items').getList(1, 200, { sort: 'position' });
			items = res.items;
		} catch (e) {
			console.error(e);
		}
		loading = false;
	}

	function openCreate() {
		editing = null;
		title = '';
		contentHTML = '';
		showModal = true;
		setTimeout(() => initEditor(), 50);
	}

	function openEdit(item: any) {
		editing = item;
		title = item.title;
		contentHTML = item.content || '';
		showModal = true;
		setTimeout(() => initEditor(), 50);
	}

	function initEditor() {
		if (editor) {
			editor.destroy();
		}
		if (!editorElement) return;

		editor = new Editor({
			element: editorElement,
			extensions: [
				StarterKit,
				Link.configure({ openOnClick: false }),
				Underline,
				TextAlign.configure({ types: ['heading', 'paragraph'] })
			],
			content: contentHTML,
			onUpdate: ({ editor }) => {
				contentHTML = editor.getHTML();
			},
			editorProps: {
				attributes: {
					class: 'prose prose-sm max-w-none focus:outline-none min-h-[300px] p-4'
				}
			}
		});
	}

	async function saveItem() {
		const html = contentHTML;
		try {
			if (editing) {
				await db.collection('help_items').update(editing.id, { title, content: html });
			} else {
				const maxPos = items.length > 0 ? Math.max(...items.map((i) => i.position || 0)) : 0;
				await db.collection('help_items').create({ title, content: html, position: maxPos + 1 });
			}
			await loadItems();
			closeModal();
		} catch (e) {
			console.error(e);
			alert('Fehler beim Speichern');
		}
	}

	function closeModal() {
		if (editor) {
			editor.destroy();
			editor = null;
		}
		showModal = false;
	}

	async function deleteItem(item: any) {
		if (!confirm('Eintrag wirklich löschen?')) return;
		try {
			await db.collection('help_items').delete(item.id);
			await loadItems();
		} catch (e) {
			console.error(e);
			alert('Fehler beim Löschen');
		}
	}

	// Drag handlers
	function onDragStart(e: DragEvent, idx: number) {
		dragIndex = idx;
		e.dataTransfer?.setData('text/plain', String(idx));
		e.dataTransfer!.effectAllowed = 'move';
	}
	function onDragOver(e: DragEvent) {
		e.preventDefault();
		e.dataTransfer!.dropEffect = 'move';
	}
	async function onDrop(e: DragEvent, idx: number) {
		e.preventDefault();
		const from = dragIndex;
		const to = idx;
		if (from == null || from === to) return;
		// reorder locally
		const updated = [...items];
		const [moved] = updated.splice(from, 1);
		updated.splice(to, 0, moved);
		// update positions (lower number = higher)
		for (let i = 0; i < updated.length; i++) {
			updated[i].position = i + 1;
		}
		items = updated;
		dragIndex = null;
		// persist positions to PocketBase
		try {
			await Promise.all(
				items.map((it) => db.collection('help_items').update(it.id, { position: it.position }))
			);
			await loadItems();
		} catch (e) {
			console.error(e);
			alert('Fehler beim Speichern der Reihenfolge');
		}
	}

	// Toolbar actions
	function toggleBold() {
		editor?.chain().focus().toggleBold().run();
	}
	function toggleItalic() {
		editor?.chain().focus().toggleItalic().run();
	}
	function toggleUnderline() {
		editor?.chain().focus().toggleUnderline().run();
	}
	function toggleStrike() {
		editor?.chain().focus().toggleStrike().run();
	}
	function toggleCode() {
		editor?.chain().focus().toggleCode().run();
	}
	function setHeading(level: 1 | 2 | 3) {
		editor?.chain().focus().toggleHeading({ level }).run();
	}
	function toggleBulletList() {
		editor?.chain().focus().toggleBulletList().run();
	}
	function toggleOrderedList() {
		editor?.chain().focus().toggleOrderedList().run();
	}
	function toggleCodeBlock() {
		editor?.chain().focus().toggleCodeBlock().run();
	}
	function setLink() {
		const url = prompt('URL eingeben:');
		if (url) editor?.chain().focus().setLink({ href: url }).run();
	}
	function setAlign(align: 'left' | 'center' | 'right') {
		editor?.chain().focus().setTextAlign(align).run();
	}
</script>

<title>Admin - Hilfecenter</title>

<AdminSite>
	<div class="container mx-auto">
		<div class="flex items-center justify-between mb-4">
			<h1 class="text-2xl font-bold">Hilfecenter verwalten</h1>
			<div>
				<button class="btn btn-primary mr-2" on:click={openCreate}>Neuen Eintrag</button>
				<button class="btn" on:click={loadItems}>Aktualisieren</button>
			</div>
		</div>

		{#if loading}
			<div class="text-sm">Lade...</div>
		{:else}
			<div class="overflow-x-auto">
				<table class="table w-full">
					<thead>
						<tr>
							<th></th>
							<th>ID</th>
							<th>Titel</th>
							<th>Vorschau</th>
							<th>Position</th>
							<th>Aktionen</th>
						</tr>
					</thead>
					<tbody>
						{#each items as item, idx}
							<tr
								draggable="true"
								on:dragstart={(e) => onDragStart(e, idx)}
								on:dragover={onDragOver}
								on:drop={(e) => onDrop(e, idx)}
							>
								<td class="w-8"><Move size="18" class="cursor-move" /></td>
								<td class="font-mono text-sm">{item.id}</td>
								<td>{item.title}</td>
								<td>
									<div class="max-w-xs truncate">{@html item.content}</div>
								</td>
								<td>{item.position}</td>
								<td>
									<div class="btn-group">
										<button
											class="btn btn-sm btn-outline"
											on:click={() => openEdit(item)}
											aria-label="Bearbeiten"><Pencil size="16" /></button
										>
										<button
											class="btn btn-sm btn-error"
											on:click={() => deleteItem(item)}
											aria-label="Löschen"><Trash2 size="16" /></button
										>
									</div>
								</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>
		{/if}

		{#if showModal}
			<dialog open class="modal modal-open">
				<form class="modal-box max-w-4xl w-full" on:submit|preventDefault={saveItem}>
					<h3 class="font-bold text-lg">
						{editing ? 'Eintrag bearbeiten' : 'Neuen Eintrag erstellen'}
					</h3>
					<div class="form-control mt-4 flex flex-col">
						<!-- svelte-ignore a11y_label_has_associated_control -->
						<label class="label"><span class="label-text">Titel</span></label>
						<input
							class="input w-full input-bordered"
							autocomplete="off"
							bind:value={title}
							required
						/>
					</div>
					<div class="form-control mt-4">
						<!-- svelte-ignore a11y_label_has_associated_control -->
						<label class="label"><span class="label-text">Inhalt</span></label>
						<div class="border border-base-300 rounded-lg overflow-hidden bg-base-100 shadow-sm">
							<!-- Toolbar -->
							<div
								class="flex items-center gap-1 p-2 border-b border-base-300 bg-base-200/50 flex-wrap"
							>
								<button
									type="button"
									class="btn btn-xs btn-ghost"
									on:click={() => setHeading(1)}
									title="Überschrift 1"><Heading1 size="14" /></button
								>
								<button
									type="button"
									class="btn btn-xs btn-ghost"
									on:click={() => setHeading(2)}
									title="Überschrift 2"><Heading2 size="14" /></button
								>
								<button
									type="button"
									class="btn btn-xs btn-ghost"
									on:click={() => setHeading(3)}
									title="Überschrift 3"><Heading3 size="14" /></button
								>
								<div class="divider divider-horizontal mx-1"></div>
								<button
									type="button"
									class="btn btn-xs btn-ghost"
									on:click={toggleBold}
									title="Fett"><Bold size="14" /></button
								>
								<button
									type="button"
									class="btn btn-xs btn-ghost"
									on:click={toggleItalic}
									title="Kursiv"><Italic size="14" /></button
								>
								<button
									type="button"
									class="btn btn-xs btn-ghost"
									on:click={toggleUnderline}
									title="Unterstrichen"><Baseline size="14" /></button
								>
								<button
									type="button"
									class="btn btn-xs btn-ghost"
									on:click={toggleStrike}
									title="Durchgestrichen"><Strikethrough size="14" /></button
								>
								<div class="divider divider-horizontal mx-1"></div>
								<button
									type="button"
									class="btn btn-xs btn-ghost"
									on:click={toggleBulletList}
									title="Aufzählungsliste"><List size="14" /></button
								>
								<button
									type="button"
									class="btn btn-xs btn-ghost"
									on:click={toggleOrderedList}
									title="Nummerierte Liste"><ListOrdered size="14" /></button
								>
								<div class="divider divider-horizontal mx-1"></div>
								<button
									type="button"
									class="btn btn-xs btn-ghost"
									on:click={toggleCode}
									title="Inline Code"><Code size="14" /></button
								>
								<button
									type="button"
									class="btn btn-xs btn-ghost"
									on:click={toggleCodeBlock}
									title="Code Block"><Braces size="14" /></button
								>
								<button
									type="button"
									class="btn btn-xs btn-ghost"
									on:click={setLink}
									title="Link einfügen"><LinkIcon size="14" /></button
								>
								<div class="divider divider-horizontal mx-1"></div>
								<button
									type="button"
									class="btn btn-xs btn-ghost"
									on:click={() => setAlign('left')}
									title="Links ausrichten"><TextAlignStart size="14" /></button
								>
								<button
									type="button"
									class="btn btn-xs btn-ghost"
									on:click={() => setAlign('center')}
									title="Zentrieren"><TextAlignCenter size="14" /></button
								>
								<button
									type="button"
									class="btn btn-xs btn-ghost"
									on:click={() => setAlign('right')}
									title="Rechts ausrichten"><TextAlignEnd size="14" /></button
								>
							</div>
							<!-- Editor -->
							<div bind:this={editorElement} class="min-h-75"></div>
						</div>
					</div>
					<div class="modal-action mt-4">
						<button type="button" class="btn" on:click={closeModal}>Abbrechen</button>
						<button type="submit" class="btn btn-primary">Speichern</button>
					</div>
				</form>
			</dialog>
		{/if}
	</div>
</AdminSite>

<style>
	:global(.ProseMirror) {
		outline: none;
	}
	:global(.ProseMirror p.is-editor-empty:first-child::before) {
		content: 'Schreibe etwas...';
		float: left;
		color: #adb5bd;
		pointer-events: none;
		height: 0;
	}
</style>

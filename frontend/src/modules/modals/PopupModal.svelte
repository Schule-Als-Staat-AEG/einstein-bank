<script lang="ts">
	import { createEventDispatcher } from 'svelte';

	type ModalResult = 'confirm' | 'cancel' | 'continue' | 'close';

	let modal: HTMLDialogElement;

	let title = '';
	let description = '';

	let showConfirmButton = false;
	let showCancelButton = false;
	let showCloseButton = false;
	let showContinueButton = false;

	let dispatch = createEventDispatcher();

	let resolvePromise: ((result: ModalResult) => void) | null = null;

	function resolveAndClose(result: ModalResult) {
		resolvePromise?.(result);
		resolvePromise = null;
		modal.close();
	}

	export function showDialog(
		newTitle: string,
		newDescription: string,
		options: {
			confirm?: boolean;
			cancel?: boolean;
			close?: boolean;
			continue?: boolean;
		} = {}
	): Promise<ModalResult> {
		title = newTitle;
		description = newDescription;

		showConfirmButton = options.confirm ?? false;
		showCancelButton = options.cancel ?? false;
		showCloseButton = options.close ?? false;
		showContinueButton = options.continue ?? false;

		modal.showModal();

		return new Promise((resolve) => {
			resolvePromise = resolve;
		});
	}

	export async function alertDialog(
		newTitle: string,
		newDescription: string
	): Promise<void> {
		await showDialog(newTitle, newDescription, {
			close: true
		});
	}

	export function closeModal() {
		resolveAndClose('close');
	}

	export function showModal() {
		modal.showModal();
	}

	export function close() {
		resolveAndClose('close');
	}

	export function show() {
		showModal();
	}

	function confirm() {
		dispatch('confirm');
		resolveAndClose('confirm');
	}

	function cancel() {
		dispatch('cancel');
		resolveAndClose('cancel');
	}

	function continueEvent() {
		dispatch('continue');
		resolveAndClose('continue');
	}

	function closeWithX() {
		dispatch('close');
		resolveAndClose('close');
	}
</script>

<dialog id="first-run-modal" class="modal" bind:this={modal}>
	<form method="dialog" class="modal-box max-w-3xl w-fit">
		<div class="w-full flex items-center justify-between">
			<h3 class="text-lg font-bold">{title}</h3>

			<button class="ml-4 btn btn-ghost" type="button" on:click={closeWithX}>
				X
			</button>
		</div>

		<p class="py-2 whitespace-pre-line">{description}</p>

		<div class="flex flex-row w-full justify-end gap-2 mt-2">
			{#if showConfirmButton}
				<button class="btn btn-primary" type="button" on:click={confirm}>
					Bestätigen
				</button>
			{/if}

			{#if showContinueButton}
				<button class="btn btn-success" type="button" on:click={continueEvent}>
					Weiter
				</button>
			{/if}

			{#if showCancelButton}
				<button class="btn btn-error" type="button" on:click={cancel}>
					Abbrechen
				</button>
			{/if}

			{#if showCloseButton}
				<button class="btn btn-error" type="button" on:click={close}>
					Schließen
				</button>
			{/if}
		</div>
	</form>
</dialog>
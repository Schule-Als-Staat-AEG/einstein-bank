<script lang="ts">
	import { browser } from '$app/environment';
	import { onMount } from 'svelte';
	import { createEventDispatcher } from 'svelte';

	let modal: HTMLDialogElement;
	let pinInputs: HTMLInputElement[] = [];
	let pinValues = ['', '', '', ''];
	let isSubmitting = false;

	let dispatch = createEventDispatcher();

	onMount(async () => {
		if (!browser) return;
	});

	function showModal() {
		modal.showModal();
		// Focus first input when modal opens
		setTimeout(() => {
			pinInputs[0]?.focus();
		}, 0);
	}

	function closeModal() {
		modal.close();
		resetPin();
	}

	function resetPin() {
		pinValues = ['', '', '', ''];
		pinInputs.forEach((input) => (input.value = ''));
	}

	function handleInput(index: number, event: Event) {
		const input = event.target as HTMLInputElement;
		let value = input.value;

		// Only allow digits
		value = value.replace(/[^0-9]/g, '');

		// Only keep the last digit if multiple were pasted
		if (value.length > 1) {
			value = value.slice(-1);
		}

		pinValues[index] = value;
		input.value = value;

		// Move to next field if digit entered
		if (value && index < 3) {
			pinInputs[index + 1]?.focus();
		}
	}

	function handleKeydown(index: number, event: KeyboardEvent) {
		if (event.key === 'Backspace') {
			event.preventDefault();

			// Clear current field
			pinValues[index] = '';
			pinInputs[index].value = '';

			// Move to previous field if current is empty
			if (index > 0) {
				pinInputs[index - 1]?.focus();
			}
		}

		// Allow arrow navigation
		if (event.key === 'ArrowLeft' && index > 0) {
			pinInputs[index - 1]?.focus();
		}
		if (event.key === 'ArrowRight' && index < 3) {
			pinInputs[index + 1]?.focus();
		}

		// Submit on Enter
		if (event.key === 'Enter') {
			submitPin();
		}
	}

	async function submitPin() {
		const pin = pinValues.join('');

		// Validate all fields are filled
		if (pin.length !== 4) {
			alert('Bitte geben Sie einen 4-stelligen PIN ein');
			return;
		}

		isSubmitting = true;
		try {
			dispatch('submit', { pin });
		} finally {
			isSubmitting = false;
		}
	}

	export { showModal, closeModal, resetPin };
</script>

<dialog bind:this={modal} class="modal">
	<form method="dialog" class="modal-box w-full max-w-sm">
		<h3 class="text-lg font-bold mb-6">PIN eingeben</h3>

		<!-- PIN Input Fields -->
		<div class="flex justify-center gap-4 mb-8">
			{#each { length: 4 } as _, i}
				<input
					type="password"
					inputmode="numeric"
					maxlength="1"
					class="input input-bordered w-16 h-16 text-center text-2xl font-bold bg-base-200 focus:bg-base-100"
					bind:this={pinInputs[i]}
					on:input={(e) => handleInput(i, e)}
					on:keydown={(e) => handleKeydown(i, e)}
					placeholder="•"
					autocomplete="off"
					disabled={isSubmitting}
				/>
			{/each}
		</div>

		<!-- Buttons -->
		<div class="flex gap-3 justify-center">
			<button type="button" class="btn btn-ghost" on:click={closeModal} disabled={isSubmitting}>
				Abbrechen
			</button>
			<button
				type="button"
				class="btn btn-primary"
				on:click={submitPin}
				disabled={isSubmitting || pinValues.some((v) => !v)}
			>
				{#if isSubmitting}
					<span class="loading loading-spinner loading-sm"></span>
				{/if}
				Bestätigen
			</button>
		</div>
	</form>

	<form method="dialog" class="modal-backdrop">
		<!-- svelte-ignore a11y_consider_explicit_label -->
		<button on:click={closeModal}></button>
	</form>
</dialog>

<style>
	input[type='password']::-webkit-outer-spin-button,
	input[type='password']::-webkit-inner-spin-button {
		-webkit-appearance: none;
		margin: 0;
	}
</style>

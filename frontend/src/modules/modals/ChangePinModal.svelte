<script lang="ts">
	import { browser } from '$app/environment';
	import { createEventDispatcher } from 'svelte';
	import { EyeIcon, EyeOffIcon } from '@lucide/svelte';
	import { onMount } from 'svelte';

	let modal: HTMLDialogElement;
	let pin1: string = '';
	let pin2: string = '';
	let pin3: string = '';
	let pin4: string = '';

	let input1: HTMLInputElement;
	let input2: HTMLInputElement;
	let input3: HTMLInputElement;
	let input4: HTMLInputElement;

	let showNewPin = false;
	let showCurrentPin = false;
	let isSubmitting = false;
	let currentPinValue: string = '';

	let dispatch = createEventDispatcher();

	onMount(async () => {
		if (!browser) return;
	});

	function setCurrentPin(pin: string) {
		currentPinValue = pin;
	}

	function showModal() {
		modal.showModal();
		// Focus first input when modal opens
		setTimeout(() => {
			input1?.focus();
		}, 0);
	}

	function closeModal() {
		modal.close();
		resetPin();
	}

	function resetPin() {
		pin1 = '';
		pin2 = '';
		pin3 = '';
		pin4 = '';
		showNewPin = false;
		showCurrentPin = false;
	}

	function handleInput(pinVar: string, index: number, event: Event): string {
		const input = event.target as HTMLInputElement;
		let value = input.value;

		// Only allow digits
		value = value.replace(/[^0-9]/g, '');

		// Only keep the last digit if multiple were pasted
		if (value.length > 1) {
			value = value.slice(-1);
		}

		input.value = value;

		// Move to next field if digit entered
		if (value && index < 4) {
			if (index === 1) input2?.focus();
			if (index === 2) input3?.focus();
			if (index === 3) input4?.focus();
		}

		return value;
	}

	function handleKeydown(index: number, event: KeyboardEvent) {
		if (event.key === 'Backspace') {
			event.preventDefault();

			// Clear current field
			if (index === 1) pin1 = '';
			if (index === 2) pin2 = '';
			if (index === 3) pin3 = '';
			if (index === 4) pin4 = '';

			// Move to previous field if current is empty
			if (index > 1) {
				if (index === 2) input1?.focus();
				if (index === 3) input2?.focus();
				if (index === 4) input3?.focus();
			}
		}

		// Allow arrow navigation
		if (event.key === 'ArrowLeft' && index > 1) {
			if (index === 2) input1?.focus();
			if (index === 3) input2?.focus();
			if (index === 4) input3?.focus();
		}
		if (event.key === 'ArrowRight' && index < 4) {
			if (index === 1) input2?.focus();
			if (index === 2) input3?.focus();
			if (index === 3) input4?.focus();
		}

		// Submit on Enter
		if (event.key === 'Enter') {
			submitPin();
		}
	}

	async function submitPin() {
		const newPin = pin1 + pin2 + pin3 + pin4;

		// Validate PIN is filled
		if (newPin.length !== 4) {
			alert('Bitte geben Sie einen 4-stelligen PIN ein');
			return;
		}

		isSubmitting = true;
		try {
			dispatch('submit', { newPin });
		} finally {
			isSubmitting = false;
		}
	}

	export { showModal, closeModal, resetPin, setCurrentPin };
</script>

<dialog bind:this={modal} class="modal">
	<form
		method="dialog"
		class="modal-box w-full max-w-2xl"
		autocomplete="off"
		on:submit|preventDefault={submitPin}
	>
		<h3 class="text-lg font-bold mb-6">PIN ändern</h3>

		<!-- Current PIN Display -->
		<div class="mb-6 p-4 rounded-lg bg-base-100 border border-base-300">
			<div class="flex items-center justify-between mb-3">
				<!-- svelte-ignore a11y_label_has_associated_control -->
				<label class="text-sm font-semibold opacity-80">Aktuell gesetzter PIN</label>
				<button
					type="button"
					class="btn btn-ghost btn-xs gap-1"
					on:click={() => (showCurrentPin = !showCurrentPin)}
					disabled={isSubmitting}
				>
					{#if showCurrentPin}
						<EyeOffIcon size={16} />
						<span>Verbergen</span>
					{:else}
						<EyeIcon size={16} />
						<span>Anzeigen</span>
					{/if}
				</button>
			</div>
			<div class="text-center">
				{#if showCurrentPin}
					<div class="text-3xl font-mono font-bold tracking-widest">
						{currentPinValue || '••••'}
					</div>
				{:else}
					<div class="text-3xl font-mono font-bold tracking-widest">••••</div>
				{/if}
			</div>
		</div>

		<!-- New PIN -->
		<div class="mb-6">
			<div class="flex items-center justify-between mb-3">
				<!-- svelte-ignore a11y_label_has_associated_control -->
				<label class="text-sm font-semibold opacity-80">Neuer PIN</label>
				<button
					type="button"
					class="btn btn-ghost btn-xs gap-1"
					on:click={() => (showNewPin = !showNewPin)}
					disabled={isSubmitting}
				>
					{#if showNewPin}
						<EyeOffIcon size={16} />
						<span>Verbergen</span>
					{:else}
						<EyeIcon size={16} />
						<span>Anzeigen</span>
					{/if}
				</button>
			</div>
			<div class="flex justify-center gap-3">
				<input type="text" style="display:none" autocomplete="username" />
				<input type="password" style="display:none" autocomplete="current-password" />
				<input
					type={showNewPin ? 'text' : 'password'}
					id="pin1"
					name="pin1"
					autocomplete="off"
					inputmode="numeric"
					maxlength="1"
					class="input input-bordered w-14 h-14 text-center text-2xl font-bold bg-base-200 focus:bg-base-100"
					bind:this={input1}
					bind:value={pin1}
					on:input={(e) => (pin1 = handleInput(pin1, 1, e))}
					on:keydown={(e) => handleKeydown(1, e)}
					placeholder="•"
					disabled={isSubmitting}
				/>
				<input
					type={showNewPin ? 'text' : 'password'}
					id="pin2"
					autocomplete="off"
					inputmode="numeric"
					maxlength="1"
					class="input input-bordered w-14 h-14 text-center text-2xl font-bold bg-base-200 focus:bg-base-100"
					bind:this={input2}
					bind:value={pin2}
					on:input={(e) => (pin2 = handleInput(pin2, 2, e))}
					on:keydown={(e) => handleKeydown(2, e)}
					placeholder="•"
					disabled={isSubmitting}
				/>
				<input
					type={showNewPin ? 'text' : 'password'}
					id="pin3"
					autocomplete="off"
					inputmode="numeric"
					maxlength="1"
					class="input input-bordered w-14 h-14 text-center text-2xl font-bold bg-base-200 focus:bg-base-100"
					bind:this={input3}
					bind:value={pin3}
					on:input={(e) => (pin3 = handleInput(pin3, 3, e))}
					on:keydown={(e) => handleKeydown(3, e)}
					placeholder="•"
					disabled={isSubmitting}
				/>
				<input
					type={showNewPin ? 'text' : 'password'}
					id="pin4"
					autocomplete="off"
					inputmode="numeric"
					maxlength="1"
					class="input input-bordered w-14 h-14 text-center text-2xl font-bold bg-base-200 focus:bg-base-100"
					bind:this={input4}
					bind:value={pin4}
					on:input={(e) => (pin4 = handleInput(pin4, 4, e))}
					on:keydown={(e) => handleKeydown(4, e)}
					placeholder="•"
					disabled={isSubmitting}
				/>
			</div>
		</div>

		<!-- Buttons -->
		<div class="flex gap-3 justify-center mt-8">
			<button type="button" class="btn btn-ghost" on:click={closeModal} disabled={isSubmitting}>
				Abbrechen
			</button>
			<button
				type="button"
				class="btn btn-primary"
				on:click={submitPin}
				disabled={isSubmitting || !pin1 || !pin2 || !pin3 || !pin4}
			>
				{#if isSubmitting}
					<span class="loading loading-spinner loading-sm"></span>
				{/if}
				PIN ändern
			</button>
		</div>
	</form>

	<form method="dialog" class="modal-backdrop">
		<!-- svelte-ignore a11y_consider_explicit_label -->
		<button on:click={closeModal}></button>
	</form>
</dialog>

<form id="form" autocomplete="off"></form>

<style>
	input[type='password']::-webkit-outer-spin-button,
	input[type='password']::-webkit-inner-spin-button {
		-webkit-appearance: none;
		margin: 0;
	}
</style>

<script lang="ts">
	import { browser } from '$app/environment';
	import { onMount } from 'svelte';
	import { createEventDispatcher } from 'svelte';
	import AdminSite from './AdminSite.svelte';
	import { Download, Upload } from '@lucide/svelte';

	let title: string = 'Admin Panel';
	let description: string = 'Hier können Sie die Daten verwalten.';

	let showCreateButton: boolean = false;
	let showMassCreateButton: boolean = false;
	let showMassEditButton: boolean = false;
	let showMassDeleteButton: boolean = false;
	let showCSVExportButton: boolean = false;
	let showPDFExportButton: boolean = false;
	let showCSVImportButton: boolean = false;
	let showMassFreezeButton: boolean = false;
	let showMassUnfreezeButton: boolean = false;
	let showMassInvalidateButton: boolean = false;
	let showMassValidateButton: boolean = false;
	let showMassRegenerateButton: boolean = false;

	let createButtonText: string = 'Neu anlegen';
	let massCreateButtonText: string = 'Massenanlage';
	let massEditButtonText: string = 'Massenbearbeitung';
	let massDeleteButtonText: string = 'Massenlöschung';
	let csvExportButtonText: string = 'CSV Export';
	let pdfExportButtonText: string = 'PDF Export';
	let csvImportButtonText: string = 'CSV Import';
	let massFreezeButtonText: string = 'Karten sperren';
	let massUnfreezeButtonText: string = 'Karten entsperren';
	let massInvalidateButtonText: string = 'Karten ungültig machen';
	let massValidateButtonText: string = 'Karten gültig machen';
	let massRegenerateButtonText: string = 'Codes Regenerieren';

	const dispatch = createEventDispatcher();

	let totalItems: number = 0;
	let selectedItems: number = 0;

	export {
		title,
		description,
		showCreateButton,
		showMassCreateButton,
		showMassEditButton,
		showMassDeleteButton,
		showCSVExportButton,
		showPDFExportButton,
		showCSVImportButton,
		createButtonText,
		massCreateButtonText,
		massEditButtonText,
		massDeleteButtonText,
		csvExportButtonText,
		pdfExportButtonText,
		csvImportButtonText,
		totalItems,
		selectedItems,
		showMassFreezeButton,
		showMassUnfreezeButton,
		showMassInvalidateButton,
		showMassValidateButton,
		showMassRegenerateButton,
		massFreezeButtonText,
		massUnfreezeButtonText,
		massInvalidateButtonText,
		massValidateButtonText,
		massRegenerateButtonText
	};

	onMount(async () => {
		if (!browser) return;
	});
</script>

<div>
	<AdminSite>
		<h1 class="text-2xl font-bold">{title}</h1>
		<p class="opacity-70 text-sm mb-4">{description}</p>

		<slot name="filter"></slot>

		<div class="card bg-base-100 border border-base-300 shadow-sm mb-4 mt-4">
			<div class="card-body p-4">
				<div class="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
					<div class="flex items-center gap-2">
						<div class="badge badge-neutral badge-lg">
							{totalItems} Treffer
						</div>
						{#if selectedItems > 0}
							<div class="badge badge-primary badge-lg">
								{selectedItems} ausgewählt
							</div>
						{/if}
					</div>

					<div class="flex flex-wrap gap-2 justify-end">
						{#if showMassFreezeButton}
							<button
								class="btn btn-outline btn-primary"
								on:click={() => dispatch('massFreeze')}
								disabled={selectedItems === 0}
							>
								{massFreezeButtonText}
							</button>
						{/if}
						{#if showMassUnfreezeButton}
							<button
								class="btn btn-outline btn-primary"
								on:click={() => dispatch('massUnfreeze')}
								disabled={selectedItems === 0}
							>
								{massUnfreezeButtonText}
							</button>
						{/if}
						{#if (showCreateButton || showMassCreateButton || showMassEditButton || showMassEditButton || showMassDeleteButton || showCSVExportButton || showCSVImportButton || showPDFExportButton || showMassValidateButton || showMassInvalidateButton) && (showMassFreezeButton || showMassUnfreezeButton)}
							<div class="divider divider-horizontal m-0"></div>
						{/if}
						{#if showMassInvalidateButton}
							<button
								class="btn btn-outline btn-error"
								on:click={() => dispatch('massInvalidate')}
								disabled={selectedItems === 0}
							>
								{massInvalidateButtonText}
							</button>
						{/if}
						{#if showMassValidateButton}
							<button
								class="btn btn-outline btn-error"
								on:click={() => dispatch('massValidate')}
								disabled={selectedItems === 0}
							>
								{massValidateButtonText}
							</button>
						{/if}
						{#if (showCreateButton || showMassCreateButton || showMassEditButton || showMassEditButton || showMassDeleteButton || showCSVExportButton || showCSVImportButton || showPDFExportButton || showMassRegenerateButton) && (showMassFreezeButton || showMassUnfreezeButton || showMassInvalidateButton || showMassValidateButton)}
							<div class="divider divider-horizontal m-0"></div>
						{/if}
						{#if showMassRegenerateButton}
							<button
								class="btn btn-outline btn-warning"
								on:click={() => dispatch('massRegenerate')}
								disabled={selectedItems === 0}
							>
								{massRegenerateButtonText}
							</button>
						{/if}
						{#if (showCreateButton || showMassCreateButton || showMassEditButton || showMassEditButton || showMassDeleteButton || showCSVExportButton || showCSVImportButton || showPDFExportButton) && (showMassFreezeButton || showMassUnfreezeButton || showMassInvalidateButton || showMassValidateButton || showMassRegenerateButton)}
							<div class="divider divider-horizontal m-0"></div>
						{/if}
						{#if showCreateButton}
							<button class="btn btn-primary" on:click={() => dispatch('create')}>
								{createButtonText}
							</button>
						{/if}
						{#if showMassCreateButton}
							<button class="btn btn-outline btn-primary" on:click={() => dispatch('massCreate')}>
								{massCreateButtonText}
							</button>
						{/if}

						{#if showCreateButton || showMassCreateButton}
							{#if showMassEditButton || showMassDeleteButton || showCSVExportButton || showCSVImportButton || showPDFExportButton}
								<div class="divider divider-horizontal m-0"></div>
							{/if}
						{/if}

						{#if showMassEditButton}
							<button
								class="btn btn-warning"
								on:click={() => dispatch('massEdit')}
								disabled={selectedItems === 0}
							>
								{massEditButtonText}
							</button>
						{/if}

						{#if showMassDeleteButton}
							<button
								class="btn btn-outline btn-error"
								on:click={() => dispatch('massDelete')}
								disabled={selectedItems === 0}
							>
								{massDeleteButtonText}
							</button>
						{/if}

						{#if (showMassEditButton || showMassDeleteButton) && (showCSVExportButton || showCSVImportButton || showPDFExportButton)}
							<div class="divider divider-horizontal m-0"></div>
						{/if}

						{#if showCSVExportButton}
							<button
								class="btn btn-success"
								on:click={() => dispatch('csvExport')}
								disabled={selectedItems === 0}
							>
								<Download class="w-4 h-4 mr-2" />
								{csvExportButtonText}
							</button>
						{/if}
						{#if showCSVImportButton}
							<button class="btn btn-outline btn-success" on:click={() => dispatch('csvImport')}>
								<Upload class="w-4 h-4 mr-2" />
								{csvImportButtonText}
							</button>
						{/if}
						{#if showPDFExportButton}
							<button
								class="btn btn-error"
								on:click={() => dispatch('pdfExport')}
								disabled={selectedItems === 0}
							>
								<Download class="w-4 h-4 mr-2" />
								{pdfExportButtonText}
							</button>
						{/if}
					</div>
				</div>
			</div>
		</div>
		<slot></slot>
	</AdminSite>
</div>

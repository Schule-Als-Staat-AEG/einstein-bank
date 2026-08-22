<script lang="ts">
	import db from '$lib/connections';
	import AdminPanel from '$modules/AdminPanel.svelte';
	import Currency from '$modules/Currency.svelte';
	import { Trash } from '@lucide/svelte';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';

	let page = 1;
	let perPage = 20;
	let pages = 1;
	let totalItems = 0;
	let filter = '';
	let loading = false;

	let taxReports: any[] = [];
	let selectedReports: Set<string> = new Set();
	let deleteConfirmModal: HTMLDialogElement;
	let toDeleteId: string | null = null;

	onMount(async () => {
		if (!browser) return;
		// Initialization code can go here
		await fetchTaxReports();
	});
	async function fetchTaxReports() {
		loading = true;
		const results = await db.collection('tax_logs').getList(page, perPage, {
			sort: '-created',
			filter:
				filter && filter.trim()
					? `account.id ~ "${filter}" || account.user.name ~ "${filter}" || id ~ "${filter}"`
					: undefined,
			expand: 'account,account.user,transaction'
		});
		taxReports = results.items;
		pages = results.totalPages || 1;
		totalItems = results.totalItems || 0;
		loading = false;
	}
	function toggleSelectReport(id: string) {
		if (selectedReports.has(id)) {
			selectedReports.delete(id);
		} else {
			selectedReports.add(id);
		}
		selectedReports = new Set(selectedReports); // Trigger reactivity
	}
	function toggleSelectAll() {
		if (selectedReports.size === taxReports.length) {
			selectedReports.clear();
		} else {
			taxReports.forEach((report) => selectedReports.add(report.id));
		}
		selectedReports = new Set(selectedReports); // Trigger reactivity
	}
	function confirmDeleteReport(id: string) {
		toDeleteId = id;
		deleteConfirmModal.showModal();
	}
	async function doDeleteReport() {
		if (!toDeleteId) return;
		await db.collection('tax_logs').delete(toDeleteId);
		deleteConfirmModal.close();
		toDeleteId = null;
		await fetchTaxReports();
	}
	async function deleteSelected() {
		if (selectedReports.size === 0) return;
		await Promise.all(
			Array.from(selectedReports).map((id) => db.collection('tax_logs').delete(id))
		);
		selectedReports.clear();
		selectedReports = new Set();
		await fetchTaxReports();
	}
</script>

<title>Admin - Steuer-Logs</title>
<AdminPanel
	title="Steuer-Logs"
	description="Verwaltung aller Steuer-Logs"
	showMassDeleteButton={true}
	massDeleteButtonText="Massenlöschung"
	{totalItems}
	selectedItems={selectedReports.size}
	on:massDelete={() => deleteSelected()}
>
	<svelte:fragment slot="filter">
		<div class="flex gap-3 flex-row justify-between">
			<div class="flex w-full">
				<input
					class="input input-bordered w-full"
					type="text"
					placeholder="Nach Konto, Name oder ID filtern..."
					bind:value={filter}
					autocomplete="off"
					on:input={() => {
						page = 1;
						fetchTaxReports();
					}}
				/>
			</div>

			<div class="flex gap-2 flex-wrap">
				<select
					class="select select-bordered"
					bind:value={perPage}
					on:change={() => fetchTaxReports()}
				>
					<option value="10">10 / Seite</option>
					<option selected value="20">20 / Seite</option>
					<option value="50">50 / Seite</option>
					<option value="100">100 / Seite</option>
				</select>

				<div class="flex-1"></div>
			</div>
		</div>
		{#if loading}
			<div class="text-sm opacity-70">Lade Steuer-Logs…</div>
		{/if}
	</svelte:fragment>

	<div class="overflow-x-auto rounded-2xl border border-base-300">
		<table class="table table-zebra">
			<!-- head -->
			<thead>
				<tr>
					<th>
						<label>
							<input
								type="checkbox"
								class="checkbox bg-base-100"
								on:change={toggleSelectAll}
								checked={selectedReports.size === taxReports.length}
							/>
						</label>
					</th>
					<th>Konto</th>
					<th>Überweisung</th>
					<th>Zusammenfassung</th>
					<th>VAT</th>
					<th>Einkommenssteuer</th>
					<th>Transaktionssteuer</th>
					<th>Erstellt</th>
					<th>Aktionen</th>
				</tr>
			</thead>
			<tbody>
				{#each taxReports as report}
					<tr>
						<th>
							<label>
								<input
									type="checkbox"
									class="checkbox bg-base-100"
									on:change={() => toggleSelectReport(report.id)}
									checked={selectedReports.has(report.id)}
								/>
							</label>
						</th>
						<td>
							<div class="flex gap-1 flex-col">
								<div class="font-bold">{report.expand.account.expand.user.name}</div>
								<div class="text-sm opacity-50 badge badge-ghost badge-sm">{report.account}</div>
							</div>
						</td>
						<td>
							<p>{report.transaction}</p>
						</td>
						<td>
							<div class="flex flex-col justify-center items-center">
								<div class="flex flex-row justify-center items-center">
									{report.originalAmount}
									<Currency />
								</div>
								<div class="flex flex-row justify-center items-center">
									+ {report.totalTaxAmount}
									<Currency />
								</div>
								<div class="flex flex-row justify-center items-center">
									= {report.originalAmount + report.totalTaxAmount}
									<Currency />
								</div>
							</div>
						</td>
						<td>
							<div class="flex flex-row justify-center items-center">
								{report.vatAmount}
								<Currency />
							</div>
						</td>
						<td>
							<div class="flex flex-row justify-center items-center">
								{report.incomeTaxAmount}
								<Currency />
							</div>
						</td>
						<td>
							<div class="flex flex-col justify-center items-center">
								<div class="flex flex-row justify-center items-center">
									{report.transactionTaxAmount}
									<Currency />
								</div>
								<div class="flex flex-row justify-center items-center">
									Steuer Frei: {report.taxFreePoolUsed}
									<Currency />
								</div>
								<div class="flex flex-row justify-center items-center">
									Post: {report.newTaxFreePool}
									<Currency />
								</div>
							</div>
						</td>
						<td>{new Date(report.created).toLocaleString()}</td>
						<th>
							<button
								class="btn btn-ghost btn-xs btn-error"
								on:click={() => confirmDeleteReport(report.id)}><Trash></Trash></button
							>
						</th>
					</tr>
				{/each}
			</tbody>
		</table>
	</div>

	<div class="flex justify-center mt-4">
		<div class="join">
			<button
				class="join-item btn"
				on:click={() => {
					page -= 1;
					fetchTaxReports();
				}}
				disabled={page <= 1}
			>
				«
			</button>
			<button class="join-item btn btn-ghost">
				Seite <span class="font-mono mx-1">{page}</span> / <span class="font-mono">{pages}</span>
			</button>
			<button
				class="join-item btn"
				on:click={() => {
					page += 1;
					fetchTaxReports();
				}}
				disabled={page >= pages}
			>
				»
			</button>
		</div>
	</div>
</AdminPanel>

<!-- Delete confirmation modal -->
<dialog class="modal" bind:this={deleteConfirmModal}>
	<form method="dialog" class="modal-box">
		<h3 class="font-bold text-lg">Steuer-Log löschen</h3>
		<p class="py-4">
			Möchtest du dieses Steuer-Log wirklich löschen? Diese Aktion kann nicht rückgängig gemacht
			werden.
		</p>
		<div class="modal-action">
			<button class="btn" on:click={() => deleteConfirmModal.close()}>Abbrechen</button>
			<button
				class="btn btn-error"
				on:click={(e) => {
					e.preventDefault();
					doDeleteReport();
				}}>Löschen</button
			>
		</div>
	</form>
	<form method="dialog" class="modal-backdrop"><button>close</button></form>
</dialog>

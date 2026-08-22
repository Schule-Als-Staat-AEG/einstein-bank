<script lang="ts">
	import db, { server } from '$lib/connections';
	import AdminPanel from '$modules/AdminPanel.svelte';
	import {
		CopyIcon,
		LockIcon,
		Pen,
		RotateCcwIcon,
		ShieldCheck,
		SnowflakeIcon
	} from '@lucide/svelte';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';
	import { getAuthStore } from '$lib/auth';
	import { generateCardPdf } from '$lib/cards';
	import { getSetting } from '$lib/settings';

	let contactEmail: string = '';

	// Page vars
	let searchInput: string = '';
	let currentPage: number = 1;
	let totalPages: number = 1;
	let perPage: number = 30;
	let totalItems: number = 0;

	// Data vars
	let codes: any[] = [];
	let selectedCodes: Set<string> = new Set();

	// PDF settings
	let rowsPerPage: number = 5;
	let colsPerPage: number = 2;
	let printBackSide: boolean = true;
	let faqItems: Array<{ q: string; a: string }> = [
		{
			q: 'Karte verloren oder gestohlen?',
			a: 'Sofort zur Bank-Zentrale kommen und Karte sperren lassen.'
		},
		{ q: 'Probleme beim Bezahlen?', a: 'Hilfe und Kontakt auf yourdomain.com' },
		{ q: 'Sonstige Fragen?', a: 'Kontaktiere uns unter: ' + contactEmail }
	];

	// Other
	let exportCSVModal: HTMLDialogElement;
	let csvLink: string;

	// Modal vars
	let editModal: HTMLDialogElement;
	let editCodeID: string = '';
	let editSecurity: string = '';
	let editInvalidate: boolean = false;
	let editFrozen: boolean = false;
	let editCardNumber: string = '';
	let editCodeRequired: boolean = false;
	let editPin: string = '';

	let bulkEditModal: HTMLDialogElement;
	let bulkUpdateInvalidate: boolean = false;
	let bulkEditInvalidate: boolean = false;
	let bulkUpdateFrozen: boolean = false;
	let bulkEditFrozen: boolean = false;
	let bulkUpdateCodeRequired: boolean = false;
	let bulkEditCodeRequired: boolean = false;
	let bulkUpdatePin: boolean = false;
	let bulkEditPin: string = '';

	async function fetchCodes() {
		const records = await db.collection('account_validation').getList(currentPage, perPage, {
			sort: '-created',
			filter: `account.user.name ~ "${searchInput}" || account.user.email ~ "${searchInput}" || id ~ "${searchInput}" || account.id ~ "${searchInput}"`,
			expand: 'account,account.user'
		});
		codes = records.items;
		totalPages = records.totalPages;
		totalItems = records.totalItems;
	}

	onMount(async () => {
		if (!browser) return;
		contactEmail = (await getSetting('contact_email')) || '';
		faqItems[2].a = 'Kontaktiere uns unter: ' + contactEmail;
		await fetchCodes();
	});

	function pageUp() {
		if (currentPage < totalPages) {
			currentPage += 1;
			fetchCodes();
		}
	}
	function pageDown() {
		if (currentPage > 1) {
			currentPage -= 1;
			fetchCodes();
		}
	}

	function formatDateTime(dateStr: string | null) {
		if (!dateStr) return '—';
		const d = new Date(dateStr);
		if (isNaN(d.getTime())) return '—';
		return (
			d.toLocaleDateString() +
			' ' +
			d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
		);
	}

	async function copyToClipboard(text: string) {
		try {
			await navigator.clipboard.writeText(text);
		} catch (err) {
			console.error('Failed to copy:', err);
		}
	}

	function toggleCodeSelection(codeId: string) {
		if (selectedCodes.has(codeId)) selectedCodes.delete(codeId);
		else selectedCodes.add(codeId);
		selectedCodes = new Set(selectedCodes);
	}

	function toggleAllCodesSelection() {
		if (selectedCodes.size === codes.length) selectedCodes.clear();
		else codes.forEach((code) => selectedCodes.add(code.id));
		selectedCodes = new Set(selectedCodes);
	}

	function massInvalidateCodes() {
		if (
			!confirm(
				`${selectedCodes.size} Codes ungültig machen? Diese Aktion kann nicht rückgängig gemacht werden.`
			)
		)
			return;
		Promise.all(
			Array.from(selectedCodes).map((id) =>
				db.collection('account_validation').update(id, { invalidate: true })
			)
		).then(() => {
			fetchCodes();
			selectedCodes = new Set();
		});
	}

	function massValidateCodes() {
		if (!confirm(`${selectedCodes.size} Codes gültig machen?`)) return;
		Promise.all(
			Array.from(selectedCodes).map((id) =>
				db.collection('account_validation').update(id, { invalidate: false })
			)
		).then(() => {
			fetchCodes();
			selectedCodes = new Set();
		});
	}

	async function regenerateCode(code: any) {
		await fetch(server + '/api/admin/regenerate-key', {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
				Authorization: `Bearer ${db.authStore.token}`
			},
			body: JSON.stringify({ accountID: code.expand.account.expand.user.id })
		}).catch((err) => alert('Fehler beim Regenerieren: ' + err.message));
		await fetchCodes();
	}

	async function requestRegenerate(code: any) {
		if (
			!confirm(
				`Code für Konto ${code.expand.account.id} regenerieren? Der alte Code wird ungültig.`
			)
		)
			return;
		await regenerateCode(code);
	}

	async function triggerMassRegenerate() {
		if (!confirm(`${selectedCodes.size} Codes regenerieren?`)) return;
		await Promise.all(
			Array.from(selectedCodes).map(async (id) => {
				const code = codes.find((c) => c.id === id);
				if (code) await regenerateCode(code);
			})
		);
		fetchCodes();
		selectedCodes = new Set();
	}

	async function massFreezeCodes() {
		if (!confirm(`${selectedCodes.size} Codes einfrieren?`)) return;
		await Promise.all(
			Array.from(selectedCodes).map((id) =>
				db.collection('account_validation').update(id, { frozen: true })
			)
		);
		await fetchCodes();
		selectedCodes = new Set();
	}

	async function massUnfreezeCodes() {
		if (!confirm(`${selectedCodes.size} Codes auftauen?`)) return;
		await Promise.all(
			Array.from(selectedCodes).map((id) =>
				db.collection('account_validation').update(id, { frozen: false })
			)
		);
		await fetchCodes();
		selectedCodes = new Set();
	}

	function exportCSV() {
		let csv =
			'data:text/csv;charset=utf-8,Code ID;Account ID;User Name;Security;Checksum;Invalid;Frozen;Created;Updated\n';
		Array.from(selectedCodes).forEach((id) => {
			const code = codes.find((c) => c.id === id);
			if (!code) return;
			csv +=
				[
					code.id,
					code.expand.account.id,
					`"${code.expand.account.expand.user.name.replace(/"/g, '""')}"`,
					code.security,
					code.checksum,
					code.invalidate ? 'Yes' : 'No',
					code.frozen ? 'Yes' : 'No',
					code.created,
					code.updated
				].join(';') + '\n';
		});
		csvLink = encodeURI(csv);
		exportCSVModal.showModal();
	}

	function openEditModal(code: any) {
		editCodeID = code.id;
		editSecurity = code.security;
		editInvalidate = code.invalidate;
		editFrozen = code.frozen;
		editCardNumber = code.number;
		editCodeRequired = code.codeRequired;
		editPin = code.pin || '';
		editModal.showModal();
	}

	function openBulkEditModal() {
		bulkUpdateInvalidate = false;
		bulkEditInvalidate = false;
		bulkUpdateFrozen = false;
		bulkEditFrozen = false;
		bulkUpdateCodeRequired = false;
		bulkEditCodeRequired = false;
		bulkUpdatePin = false;
		bulkEditPin = '';
		bulkEditModal.showModal();
	}
</script>

<title>Admin - Karten</title>

<AdminPanel
	title="Kartenverwaltung"
	description="Validierungscodes und Kartendaten aller Konten"
	showCreateButton={false}
	showMassCreateButton={false}
	showMassEditButton={true}
	showMassDeleteButton={false}
	showCSVExportButton={true}
	showPDFExportButton={true}
	showMassFreezeButton={false}
	showMassUnfreezeButton={false}
	showMassInvalidateButton={false}
	showMassValidateButton={false}
	showMassRegenerateButton={true}
	csvExportButtonText="CSV Exportieren"
	pdfExportButtonText="PDF Drucken"
	{totalItems}
	selectedItems={selectedCodes.size}
	on:csvExport={() => exportCSV()}
	on:pdfExport={() =>
		generateCardPdf(selectedCodes, rowsPerPage, colsPerPage, true, printBackSide, faqItems)}
	on:massFreeze={() => massFreezeCodes()}
	on:massUnfreeze={() => massUnfreezeCodes()}
	on:massInvalidate={() => massInvalidateCodes()}
	on:massValidate={() => massValidateCodes()}
	on:massRegenerate={() => triggerMassRegenerate()}
	on:massEdit={() => openBulkEditModal()}
>
	<svelte:fragment slot="filter">
		<div class="flex gap-2 flex-row flex-wrap items-center">
			<input
				type="text"
				placeholder="Nach Nutzer, Konto-ID oder Code-ID suchen…"
				class="input input-bordered flex-1 min-w-48"
				autocomplete="off"
				bind:value={searchInput}
				on:input={fetchCodes}
			/>
			<select class="select select-bordered" bind:value={perPage} on:change={() => fetchCodes()}>
				<option value={10}>10 / Seite</option>
				<option value={15}>15 / Seite</option>
				<option value={30}>30 / Seite</option>
				<option value={50}>50 / Seite</option>
				<option value={100}>100 / Seite</option>
				<option value={200}>200 / Seite</option>
				<option value={500}>500 / Seite</option>
				<option value={1000}>1000 / Seite</option>
				<option value={2000}>2000 / Seite</option>
				<option value={5000}>5000 / Seite</option>
			</select>
			<label
				class="flex items-center gap-2 cursor-pointer select-none border border-base-300 rounded-lg px-3 py-2 hover:bg-base-200 transition-colors"
			>
				<input type="checkbox" class="checkbox checkbox-sm" bind:checked={printBackSide} />
				<span class="text-sm">Kartenrückseite generieren</span>
			</label>
		</div>

		{#if printBackSide}
			<div class="mt-3 border border-base-300 rounded-xl p-4 bg-base-50">
				<p class="text-sm font-semibold mb-3">
					FAQ auf Kartenrückseite <span class="text-xs font-normal opacity-50"
						>(max. 3 Einträge, je eine Zeile)</span
					>
				</p>
				<div class="flex flex-col gap-2">
					{#each faqItems as item, i}
						<div class="flex gap-2 items-center">
							<span class="text-xs opacity-40 w-4 shrink-0">{i + 1}.</span>
							<input
								class="input input-bordered input-sm flex-1"
								placeholder="Frage, z.B. Karte verloren?"
								bind:value={item.q}
							/>
							<input
								class="input input-bordered input-sm flex-1"
								placeholder="Antwort, z.B. Sofort sperren lassen."
								bind:value={item.a}
							/>
							<button
								class="btn btn-ghost btn-sm btn-square text-error opacity-60 hover:opacity-100"
								title="Eintrag entfernen"
								on:click={() => (faqItems = faqItems.filter((_, j) => j !== i))}>✕</button
							>
						</div>
					{/each}
				</div>
				{#if faqItems.length < 3}
					<button
						class="btn btn-ghost btn-sm mt-2 opacity-70"
						on:click={() => (faqItems = [...faqItems, { q: '', a: '' }])}
						>+ Eintrag hinzufügen</button
					>
				{/if}
			</div>
		{/if}
	</svelte:fragment>

	<div class="overflow-x-auto rounded-2xl border border-base-300">
		<table class="table table-zebra">
			<thead>
				<tr>
					<th class="w-10">
						<input
							type="checkbox"
							class="checkbox bg-base-100"
							autocomplete="off"
							on:change={toggleAllCodesSelection}
							checked={codes.length > 0 && selectedCodes.size === codes.length}
						/>
					</th>
					<th>Kartenhalter</th>
					<th>Kartennummer</th>
					<th>Status</th>
					<th>PIN</th>
					<th>Erstellt</th>
					<th class="text-right">Aktionen</th>
				</tr>
			</thead>
			<tbody>
				{#if codes.length === 0}
					<tr>
						<td colspan="7" class="text-center py-10 opacity-50">Keine Einträge gefunden.</td>
					</tr>
				{/if}
				{#each codes as code}
					<tr>
						<td>
							<input
								type="checkbox"
								class="checkbox bg-base-100"
								autocomplete="off"
								on:change={() => toggleCodeSelection(code.id)}
								checked={selectedCodes.has(code.id)}
							/>
						</td>

						<!-- Kartenhalter -->
						<td>
							{#if code.expand.account.expand.user.id === getAuthStore().record.id}
								<p class="font-semibold text-accent">
									{code.expand.account.expand.user.name}
									<span class="text-xs opacity-60">(Sie)</span>
								</p>
							{:else}
								<p class="font-semibold">{code.expand.account.expand.user.name}</p>
							{/if}
							<div class="flex flex-wrap gap-1 mt-1">
								<span
									class="badge badge-ghost badge-sm font-mono cursor-pointer hover:badge-neutral transition-colors"
									title="Konto-ID kopieren"
									on:click={() => copyToClipboard(code.expand.account.id)}
									on:keydown={(e) => e.key === 'Enter' && copyToClipboard(code.expand.account.id)}
									role="button"
									tabindex="0">{code.expand.account.id}</span
								>
								<span
									class="badge badge-ghost badge-sm font-mono cursor-pointer hover:badge-neutral transition-colors opacity-50"
									title="Code-ID kopieren"
									on:click={() => copyToClipboard(code.id)}
									on:keydown={(e) => e.key === 'Enter' && copyToClipboard(code.id)}
									role="button"
									tabindex="0">{code.id}</span
								>
							</div>
						</td>

						<!-- Kartennummer -->
						<td class="font-mono text-sm">
							{code.number.substring(0, 4)}&nbsp;{code.number.substring(
								4,
								8
							)}&nbsp;{code.number.substring(8, 12)}&nbsp;{code.number.substring(12, 16)}
						</td>

						<!-- Status -->
						<td>
							<div class="flex flex-wrap gap-1">
								{#if code.invalidate}
									<span class="badge badge-error gap-1">
										<LockIcon class="w-3 h-3" /> Ungültig
									</span>
								{:else}
									<span class="badge badge-success gap-1">Aktiv</span>
								{/if}
								{#if code.frozen}
									<span class="badge badge-info gap-1">
										<SnowflakeIcon class="w-3 h-3" /> Eingefroren
									</span>
								{/if}
								{#if code.codeRequired}
									<span class="badge badge-neutral gap-1">
										<ShieldCheck class="w-3 h-3" /> PIN
									</span>
								{/if}
							</div>
						</td>

						<!-- PIN -->
						<td>
							<span
								class="font-mono text-sm cursor-pointer blur-sm hover:blur-none transition-all duration-200 select-none"
								title="Klicken zum Kopieren"
								on:click={() => copyToClipboard(code.pin)}
								on:keydown={(e) => e.key === 'Enter' && copyToClipboard(code.pin)}
								role="button"
								tabindex="0">{code.pin || '—'}</span
							>
						</td>

						<!-- Erstellt -->
						<td class="text-sm whitespace-nowrap opacity-70">
							{formatDateTime(code.created)}
						</td>

						<!-- Aktionen -->
						<td class="text-right">
							<div class="flex gap-1 justify-end">
								<button
									class="btn btn-ghost btn-xs text-warning"
									title="Code regenerieren"
									on:click={() => requestRegenerate(code)}><RotateCcwIcon class="w-4 h-4" /></button
								>
								<button
									class="btn btn-ghost btn-xs"
									title="Bearbeiten"
									on:click={() => openEditModal(code)}><Pen class="w-4 h-4" /></button
								>
							</div>
						</td>
					</tr>
				{/each}
			</tbody>
		</table>
	</div>

	<div class="flex justify-center mt-4">
		<div class="join">
			<button class="join-item btn" on:click={pageDown} disabled={currentPage <= 1}>«</button>
			<button class="join-item btn btn-ghost">Seite {currentPage} / {totalPages}</button>
			<button class="join-item btn" on:click={pageUp} disabled={currentPage >= totalPages}>»</button
			>
		</div>
	</div>
</AdminPanel>

<!-- CSV Export Modal -->
<dialog id="exportCSVModal" class="modal" bind:this={exportCSVModal}>
	<div class="modal-box">
		<h3 class="text-lg font-bold">CSV Exportieren</h3>
		<p class="py-3 text-sm opacity-70">{selectedCodes.size} Codes ausgewählt.</p>
		<div class="alert alert-warning mb-4">
			<span class="text-sm font-semibold">Die Datei enthält sensible Sicherheitsdaten.</span>
		</div>
		<div class="modal-action justify-between">
			<a
				class="btn btn-primary"
				href={csvLink}
				download="codes.csv"
				on:click={() => exportCSVModal.close()}>CSV herunterladen</a
			>
			<button class="btn btn-ghost" on:click={() => exportCSVModal.close()}>Abbrechen</button>
		</div>
	</div>
</dialog>

<!-- Edit Modal -->
<dialog id="editModal" class="modal" bind:this={editModal}>
	<form method="dialog" class="modal-box max-w-lg">
		<h3 class="text-lg font-bold mb-1">Karte bearbeiten</h3>
		<p class="text-xs opacity-50 font-mono mb-4">{editCodeID}</p>

		<div class="flex flex-col gap-4">
			<div class="form-control">
				<span class="label-text text-sm font-medium mb-2">Security-Code</span>
				<div class="flex gap-2 items-center">
					<span
						class="flex-1 font-mono text-sm bg-base-200 border border-base-300 rounded-lg px-3 py-2 blur-sm hover:blur-none transition-all duration-200 cursor-default select-none overflow-hidden text-ellipsis whitespace-nowrap"
						title="Hovern zum Anzeigen">{editSecurity}</span
					>
					<button
						type="button"
						class="btn btn-ghost btn-sm btn-square shrink-0"
						title="Kopieren"
						on:click={() => copyToClipboard(editSecurity)}
					>
						<CopyIcon class="w-4 h-4" />
					</button>
				</div>
			</div>

			<div class="form-control">
				<span class="label-text text-sm font-medium mb-2">Kartennummer</span>
				<div class="flex gap-2 items-center">
					<span
						class="flex-1 font-mono text-sm bg-base-200 border border-base-300 rounded-lg px-3 py-2 tracking-wider"
					>
						{editCardNumber.substring(0, 4)}&nbsp;{editCardNumber.substring(
							4,
							8
						)}&nbsp;{editCardNumber.substring(8, 12)}&nbsp;{editCardNumber.substring(12, 16)}
					</span>
					<button
						type="button"
						class="btn btn-ghost btn-sm btn-square shrink-0"
						title="Kopieren"
						on:click={() => copyToClipboard(editCardNumber)}
					>
						<CopyIcon class="w-4 h-4" />
					</button>
				</div>
			</div>

			<div class="grid grid-cols-2 gap-3">
				<label
					class="flex items-center gap-2 cursor-pointer border border-base-300 rounded-lg p-3 hover:bg-base-200 transition-colors"
				>
					<input type="checkbox" class="checkbox checkbox-error" bind:checked={editInvalidate} />
					<div>
						<p class="text-sm font-medium">Ungültig</p>
						<p class="text-xs opacity-50">Karte sperren</p>
					</div>
				</label>
				<label
					class="flex items-center gap-2 cursor-pointer border border-base-300 rounded-lg p-3 hover:bg-base-200 transition-colors"
				>
					<input type="checkbox" class="checkbox checkbox-info" bind:checked={editFrozen} />
					<div>
						<p class="text-sm font-medium">Eingefroren</p>
						<p class="text-xs opacity-50">Transaktionen sperren</p>
					</div>
				</label>
			</div>

			<label
				class="flex items-center gap-3 cursor-pointer border border-base-300 rounded-lg p-3 hover:bg-base-200 transition-colors"
			>
				<input type="checkbox" class="checkbox" bind:checked={editCodeRequired} />
				<div>
					<p class="text-sm font-medium">PIN erforderlich</p>
					<p class="text-xs opacity-50">Transaktion erfordert PIN-Eingabe</p>
				</div>
			</label>

			{#if editCodeRequired}
				<label class="form-control">
					<span class="label-text text-sm font-medium mb-1">4-stellige PIN</span>
					<input
						type="text"
						class="input input-bordered font-mono"
						placeholder="z.B. 1234"
						bind:value={editPin}
						maxlength="4"
					/>
				</label>
			{/if}
		</div>

		<div class="flex flex-row justify-between mt-6">
			<button
				class="btn btn-outline btn-error btn-sm"
				type="button"
				on:click={async () => {
					if (
						!confirm(
							'Code neu generieren? Der alte Code wird ungültig und ausgestellte Karten funktionieren nicht mehr.'
						)
					)
						return;
					await regenerateCode(codes.find((c) => c.id === editCodeID));
					const data = await db
						.collection('account_validation')
						.getOne(editCodeID, { expand: 'account,account.user' });
					editSecurity = data.security;
					fetchCodes();
				}}
			>
				<RotateCcwIcon class="w-4 h-4" /> Code neu generieren
			</button>
			<div class="flex gap-2">
				<button class="btn btn-ghost btn-sm" on:click={() => editModal.close()}>Abbrechen</button>
				<button
					class="btn btn-primary btn-sm"
					on:click={async () => {
						if (editCodeRequired && (!editPin || !/^\d{4}$/.test(editPin))) {
							alert('Bitte eine gültige 4-stellige PIN eingeben.');
							return;
						}
						await db.collection('account_validation').update(editCodeID, {
							invalidate: editInvalidate,
							frozen: editFrozen,
							codeRequired: editCodeRequired,
							pin: editPin || null
						});
						await fetchCodes();
						editModal.close();
					}}>Speichern</button
				>
			</div>
		</div>
	</form>
</dialog>

<!-- Bulk Edit Modal -->
<dialog id="bulkEditModal" class="modal" bind:this={bulkEditModal}>
	<form method="dialog" class="modal-box max-w-lg">
		<h3 class="text-lg font-bold mb-1">Massenbearbeitung</h3>
		<p class="text-sm opacity-60 mb-4">{selectedCodes.size} Karten ausgewählt</p>

		<div class="flex flex-col gap-3">
			<!-- Ungültig -->
			<div class="border border-base-300 rounded-lg p-3">
				<label class="flex items-center gap-2 cursor-pointer mb-2">
					<input type="checkbox" class="checkbox checkbox-sm" bind:checked={bulkUpdateInvalidate} />
					<span class="text-sm font-medium">Gültigkeit setzen</span>
				</label>
				{#if bulkUpdateInvalidate}
					<select class="select select-bordered select-sm w-full" bind:value={bulkEditInvalidate}>
						<option value={false}>Gültig (Aktiv)</option>
						<option value={true}>Ungültig (Gesperrt)</option>
					</select>
				{/if}
			</div>

			<!-- Eingefroren -->
			<div class="border border-base-300 rounded-lg p-3">
				<label class="flex items-center gap-2 cursor-pointer mb-2">
					<input type="checkbox" class="checkbox checkbox-sm" bind:checked={bulkUpdateFrozen} />
					<span class="text-sm font-medium">Einfrieren setzen</span>
				</label>
				{#if bulkUpdateFrozen}
					<select class="select select-bordered select-sm w-full" bind:value={bulkEditFrozen}>
						<option value={false}>Nicht eingefroren</option>
						<option value={true}>Eingefroren</option>
					</select>
				{/if}
			</div>

			<!-- PIN erforderlich -->
			<div class="border border-base-300 rounded-lg p-3">
				<label class="flex items-center gap-2 cursor-pointer mb-2">
					<input
						type="checkbox"
						class="checkbox checkbox-sm"
						bind:checked={bulkUpdateCodeRequired}
					/>
					<span class="text-sm font-medium">PIN-Pflicht setzen</span>
				</label>
				{#if bulkUpdateCodeRequired}
					<select class="select select-bordered select-sm w-full" bind:value={bulkEditCodeRequired}>
						<option value={false}>PIN nicht erforderlich</option>
						<option value={true}>PIN erforderlich</option>
					</select>
				{/if}
			</div>

			<!-- PIN ändern -->
			<div class="border border-base-300 rounded-lg p-3">
				<label class="flex items-center gap-2 cursor-pointer mb-2">
					<input type="checkbox" class="checkbox checkbox-sm" bind:checked={bulkUpdatePin} />
					<span class="text-sm font-medium">PIN ändern</span>
				</label>
				{#if bulkUpdatePin}
					<input
						type="text"
						class="input input-bordered input-sm w-full font-mono"
						placeholder="4-stellige PIN"
						bind:value={bulkEditPin}
						maxlength="4"
					/>
				{/if}
			</div>
		</div>

		<div class="flex gap-2 justify-end mt-5">
			<button class="btn btn-ghost btn-sm" on:click={() => bulkEditModal.close()}>Abbrechen</button>
			<button
				class="btn btn-primary btn-sm"
				on:click={async () => {
					if (bulkUpdatePin && (!bulkEditPin || !/^\d{4}$/.test(bulkEditPin))) {
						alert('Bitte eine gültige 4-stellige PIN eingeben.');
						return;
					}
					await Promise.all(
						Array.from(selectedCodes).map((id) => {
							const upd: any = {};
							if (bulkUpdateInvalidate) upd.invalidate = bulkEditInvalidate;
							if (bulkUpdateFrozen) upd.frozen = bulkEditFrozen;
							if (bulkUpdateCodeRequired) upd.codeRequired = bulkEditCodeRequired;
							if (bulkUpdatePin) upd.pin = bulkEditPin;
							return db.collection('account_validation').update(id, upd);
						})
					);
					await fetchCodes();
					selectedCodes = new Set();
					bulkEditModal.close();
				}}>Änderungen speichern</button
			>
		</div>
	</form>
</dialog>

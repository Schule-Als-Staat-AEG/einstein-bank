<script lang="ts">
	import { getAuthStore } from '$lib/auth';
	import db, { server } from '$lib/connections';
	import Site from '$modules/Site.svelte';
	import { AlertCircle, Check } from '@lucide/svelte';
	import { HubConnectionBuilder } from '@microsoft/signalr';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';
	import UserSelectModal from '$modules/modals/UserSelectModal.svelte';
	import PopupModal from '$modules/modals/PopupModal.svelte';

	let newCaseAvailable = true;
	let connection: any = null;

	let currentTab = 'home';

	let caseSelected = false;

	let selectedCase: any;
	let signatureData: any = null;
	let archiveCaseSelected = false;
	let selectedArchivedCase: any;
	let archivedSignatureData: any = null;

	let legal: boolean = false;
	let reason: string = '';
	let modalOpen: boolean = false;

	let cases = [];
	let archivedCases = [];
	let archivePage = 1;
	let archivePerPage = 20;
	let archiveTotalPages = 1;

	let selectUserModal: UserSelectModal;

	$: transaction = selectedCase?.expand?.transaction;
	$: sender = transaction?.expand?.sender;
	$: receiver = transaction?.expand?.receiver;
	$: senderUser = sender?.expand?.user;
	$: receiverUser = receiver?.expand?.user;
	$: taxLog = selectedCase?.expand?.taxLog;
	$: archivedTransaction = selectedArchivedCase?.expand?.transaction;
	$: archivedSender = archivedTransaction?.expand?.sender;
	$: archivedReceiver = archivedTransaction?.expand?.receiver;
	$: archivedSenderUser = archivedSender?.expand?.user;
	$: archivedReceiverUser = archivedReceiver?.expand?.user;
	$: archivedTaxLog = selectedArchivedCase?.expand?.taxLog;

	function formatDate(value: any) {
		if (!value) return '-';
		return new Date(value).toLocaleString();
	}

	function formatAmount(value: any) {
		if (value === null || value === undefined) return '-';
		const num = Number(value);
		if (Number.isNaN(num)) return value;
		return num.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
	}

	function openCloseModal() {
		const modal = document.getElementById('close_case_modal') as HTMLDialogElement;
		modal?.showModal();
	}

	function closeModal() {
		const modal = document.getElementById('close_case_modal') as HTMLDialogElement;
		modal?.close();
	}

	onMount(async () => {
		if (!browser) return;
		document.title = 'Finanzbehörde';
		await setupSignalR();
		await fetchCases();
		await fetchArchivedCases();
	});
	async function setupSignalR() {
		const token = db.authStore.token; // your stored token
		if (!token) {
			newCaseAvailable = false;
			return;
		}
		connection = new HubConnectionBuilder()
			.withUrl(`${server}/api/employee/job?access_token=${encodeURIComponent(token)}`)
			.withAutomaticReconnect()
			.build();

		connection.on('newCaseAvailable', (msg) => {
			newCaseAvailable = true;
			fetchCases();
		});
		connection.on('noCasesAvailable', (msg) => {
			newCaseAvailable = false;
			fetchCases();
		});
		connection.onreconnecting(() => {
			console.log('Reconnecting to case updates...');
		});

		connection.onreconnected(() => {
			console.log('Reconnected to case updates.');
		});
		connection.onclose(() => {
			console.log('Connection to case updates closed.');
		});
		connection
			.start()
			.then(() => console.log('Connected to case updates.'))
			.catch((err) => console.error('Error connecting to case updates:', err));
	}
	async function fetchCases() {
		const data = await db.collection('assigned_cases').getFullList(200, {
			sort: '-created',
			filter: `handler = "${getAuthStore().record.id}"`,
			expand: 'transaction'
		});

		cases = data;
	}
	async function fetchArchivedCases() {
		const data = await db.collection('archived_cases').getList(archivePage, archivePerPage, {
			sort: '-created',
			filter: `handler = "${getAuthStore().record.id}"`,
			expand: 'transaction'
		});

		archiveTotalPages = data.totalPages || 1;
		archivedCases = data.items || [];
	}

	async function goToArchivePage(newPage: number) {
		if (newPage < 1 || newPage > archiveTotalPages) return;
		archivePage = newPage;
		await fetchArchivedCases();
	}
	async function requestCase() {
		const response = await fetch(`${server}/api/employee/request-case`, {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
				Authorization: 'Bearer ' + db.authStore.token
			}
		});
		const data = await response.json();
		if (data.success) {
			// Case successfully assigned, you can now fetch the case details using data.caseId
			console.log('Case assigned with ID:', data.caseId);
			currentTab = 'my-cases'; // Switch to "Meine Fälle" tab
			fetchCases(); // Refresh the case list to show the newly assigned case
		} else {
			// No cases available or an error occurred
			console.error('Failed to request case:', data.message);
		}
	}
	async function selectCase(c) {
		caseSelected = true;
		selectedCase = await db.collection('assigned_cases').getOne(c.id, {
			expand:
				'transaction,transaction.sender,transaction.receiver,transaction.sender.user,transaction.receiver.user,taxLog'
		});
		const signatureResponse = await fetch(
			`${server}/api/verify-signature?signature=` +
				encodeURIComponent(selectedCase.expand.transaction.signature),
			{
				method: 'GET',
				headers: {
					'Content-Type': 'application/json',
					Authorization: 'Bearer ' + db.authStore.token
				}
			}
		);
		signatureData = await signatureResponse.json();
		console.log('Signature verification result:', signatureData);
		console.log('Selected case details:', selectedCase);
	}

	async function selectArchivedCase(c) {
		archiveCaseSelected = true;
		selectedArchivedCase = await db.collection('archived_cases').getOne(c.id, {
			expand:
				'transaction,transaction.sender,transaction.receiver,transaction.sender.user,transaction.receiver.user,taxLog'
		});
		const signatureResponse = await fetch(
			`${server}/api/verify-signature?signature=` +
				encodeURIComponent(selectedArchivedCase.expand.transaction.signature),
			{
				method: 'GET',
				headers: {
					'Content-Type': 'application/json',
					Authorization: 'Bearer ' + db.authStore.token
				}
			}
		);
		archivedSignatureData = await signatureResponse.json();
	}
	async function submitCase(isLegal: boolean) {
		const response = await fetch(`${server}/api/employee/submit-case`, {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
				Authorization: 'Bearer ' + db.authStore.token
			},
			body: JSON.stringify({
				id: selectedCase.id,
				illegal: !isLegal,
				reason
			})
		});
		const data = await response.json();
		if (data.success) {
			alert('Fall erfolgreich aktualisiert!');
			caseSelected = false;
			selectedCase = null;
			signatureData = null;
			reason = '';
			legal = false;
			const modal = document.getElementById('close_case_modal') as HTMLDialogElement;
			modal?.close();
			fetchCases(); // Refresh the case list
			fetchArchivedCases(); // Refresh archive list
		} else {
			alert('Fehler beim Aktualisieren des Falls: ' + data.message);
		}
	}

	function firstLoginMenu() {
		selectUserModal.showModal();
	}

	async function resetFirstLogin(user: any) {
		try {
			if (!confirm('Möchten Sie die Registrierung des Nutzers wirklich zurücksetzen?')) return;
			const resetPossible = await db.collection('users').getOne(user.id, {
				expand: 'data'
			});
			console.log(resetPossible);
			if (resetPossible.expand.data.registered) {
				await db.collection('user_data').update(user.id, {
					registered: false
				});
				alert('Die Registrierung wurde erfolgreich zurückgesetzt.');
			} else {
				alert('Der Nutzer hat sich nie registriert!');
			}
		} catch {
			alert('Beim zurücksetzen der Registrierungsdaten ist ein Fehler aufgetreten!');
		}
	}
</script>

<title>Finanztracking</title>
<Site>
	<div class="flex justify-end p-4">
		<button class="btn btn-primary" on:click={firstLoginMenu}>Registrierungsmenü</button>
	</div>
	<div class="tabs tabs-lift">
		<input
			type="radio"
			name="tabs"
			class="tab"
			aria-label="Haupseite"
			checked={currentTab === 'home'}
			on:click={() => {
				currentTab = 'home';
			}}
		/>
		<div class="tab-content bg-base-100 border-base-300 p-6">
			<h1 class="text-2xl font-bold mb-4">Finanztracking</h1>
			<div class="w-full h-full items-center justify-center flex">
				<div class="flex flex-col">
					{#if newCaseAvailable}
						<div class="flex flex-col mb-4 justify-center items-center">
							<AlertCircle class="text-error" size={64} />
							<span class="text-error font-bold">Ein neuer Fall ist verfügbar!</span>
							<button class="btn btn-primary mt-4" on:click={requestCase}>Fall annehmen</button>
						</div>
					{:else}
						<div class="flex flex-col mb-4 justify-center items-center">
							<Check class="text-success" size={64} />
							<span class="text-success font-bold">Keine neuen Fälle verfügbar.</span>
						</div>
					{/if}
				</div>
			</div>
		</div>

		<input
			type="radio"
			name="tabs"
			class="tab"
			aria-label="Meine Fälle"
			checked={currentTab === 'my-cases'}
			on:click={() => {
				currentTab = 'my-cases';
			}}
		/>
		<div class="tab-content bg-base-100 border-base-300 p-6">
			<h1 class="text-2xl font-bold mb-4">Meine Fälle</h1>
			{#if !caseSelected}
				{#if cases.length === 0}
					<p>Du hast derzeit keine Fälle zugewiesen.</p>
				{:else}
					<div class="overflow-x-auto">
						<table class="table">
							<thead>
								<tr>
									<th></th>
									<th>Fall ID</th>
									<th>Transaktionsbetrag</th>
									<th>Transaktionsdatum</th>
									<th>Aktionen</th>
								</tr>
							</thead>
							<tbody>
								{#each cases as c, i}
									<tr>
										<th>{i + 1}</th>
										<td>{c.id}</td>
										<td>{c.expand.transaction.amount} {c.expand.transaction.currency}</td>
										<td>{new Date(c.expand.transaction.created).toLocaleString()}</td>
										<td>
											<button class="btn btn-primary btn-sm" on:click={() => selectCase(c)}
												>Details</button
											>
										</td>
									</tr>
								{/each}
							</tbody>
						</table>
					</div>
				{/if}
			{:else}
				<div class="space-y-6">
					<div class="flex flex-wrap items-center justify-between gap-2">
						<h2 class="text-xl font-bold">Fall {selectedCase?.id}</h2>
						<button
							class="btn btn-outline btn-sm"
							on:click={() => {
								caseSelected = false;
								selectedCase = null;
								signatureData = null;
							}}
						>
							Zurück zur Liste
						</button>
					</div>

					<div class="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
						<div class="stat rounded-box border border-base-300 bg-base-100">
							<div class="stat-title">Betrag</div>
							<div class="stat-value text-lg">{formatAmount(transaction?.amount)}</div>
							<div class="stat-desc">Angekommen: {formatAmount(transaction?.received)}</div>
						</div>
						<div class="stat rounded-box border border-base-300 bg-base-100">
							<div class="stat-title">Erstellt</div>
							<div class="stat-value text-lg">{formatDate(transaction?.created)}</div>
						</div>
						<div class="stat rounded-box border border-base-300 bg-base-100">
							<div class="stat-title">Sender ID</div>
							<div class="stat-value text-lg">{transaction?.sender ?? '-'}</div>
						</div>
						<div class="stat rounded-box border border-base-300 bg-base-100">
							<div class="stat-title">Receiver ID</div>
							<div class="stat-value text-lg">{transaction?.receiver ?? '-'}</div>
						</div>
					</div>

					<div class="grid grid-cols-1 gap-4 xl:grid-cols-2">
						<div class="card border border-base-300 bg-base-100">
							<div class="card-body">
								<h3 class="card-title">Transaktionsdetails</h3>
								<div class="space-y-2 text-sm">
									<p><strong>Transaktions-ID:</strong> {transaction?.id ?? '-'}</p>
									<p><strong>Beschreibung:</strong> {transaction?.description ?? '-'}</p>
									<p><strong>System:</strong></p>
									<pre class="mockup-code text-xs whitespace-pre-wrap break-all p-4"><code
											>{transaction?.system ?? '-'}</code
										></pre>
									<p><strong>Signatur:</strong></p>
									<pre class="mockup-code text-xs whitespace-pre-wrap break-all p-4"><code
											>{transaction?.signature ?? '-'}</code
										></pre>
								</div>
							</div>
						</div>

						<div class="card border border-base-300 bg-base-100">
							<div class="card-body">
								<h3 class="card-title">Steuerdaten</h3>
								<div class="overflow-x-auto">
									<table class="table table-sm">
										<tbody>
											<tr
												><th>Ürsprünglicher Betrag</th><td
													>{formatAmount(taxLog?.originalAmount)}</td
												></tr
											>
											<tr><th>Mehrwertsteuer</th><td>{formatAmount(taxLog?.vatAmount)}</td></tr>
											<tr
												><th>Einkommensteuer</th><td>{formatAmount(taxLog?.incomeTaxAmount)}</td
												></tr
											>
											<tr
												><th>Transaktionssteuer</th><td
													>{formatAmount(taxLog?.transactionTaxAmount)}</td
												></tr
											>
											<tr
												><th>Steuerfreier Pool verwendet</th><td
													>{formatAmount(taxLog?.taxFreePoolUsed)}</td
												></tr
											>
											<tr
												><th>Neuer Steuerfreier Pool</th><td
													>{formatAmount(taxLog?.newTaxFreePool)}</td
												></tr
											>
											<tr><th>Gesamtsteuer</th><td>{formatAmount(taxLog?.totalTaxAmount)}</td></tr>
											<tr
												><th>Vorheriges Einkommen (für Einkommensteuerberechnung)</th><td
													>{formatAmount(taxLog?.previousIncomeReceived)}</td
												></tr
											>
										</tbody>
									</table>
								</div>
							</div>
						</div>
					</div>

					<div class="grid grid-cols-1 gap-4 xl:grid-cols-2">
						<div class="card border border-base-300 bg-base-100">
							<div class="card-body">
								<h3 class="card-title">Sender</h3>
								<div class="space-y-1 text-sm">
									<p><strong>Account ID:</strong> {sender?.id ?? '-'}</p>
									<p><strong>Frozen:</strong> {sender?.frozen ? 'Ja' : 'Nein'}</p>
									<p><strong>Balance:</strong> {formatAmount(sender?.balance)}</p>
									<p><strong>Name:</strong> {senderUser?.name ?? '-'}</p>
									<p>
										<strong>Firma:</strong>
										{senderUser?.isCompany ? senderUser?.companyName : '-'}
									</p>
									<p><strong>Locked:</strong> {senderUser?.locked ? 'Ja' : 'Nein'}</p>
								</div>
							</div>
						</div>

						<div class="card border border-base-300 bg-base-100">
							<div class="card-body">
								<h3 class="card-title">Receiver</h3>
								<div class="space-y-1 text-sm">
									<p><strong>Account ID:</strong> {receiver?.id ?? '-'}</p>
									<p><strong>Frozen:</strong> {receiver?.frozen ? 'Ja' : 'Nein'}</p>
									<p><strong>Balance:</strong> {formatAmount(receiver?.balance)}</p>
									<p><strong>Name:</strong> {receiverUser?.name ?? '-'}</p>
									<p>
										<strong>Firma:</strong>
										{receiverUser?.isCompany ? receiverUser?.companyName : '-'}
									</p>
									<p><strong>Locked:</strong> {receiverUser?.locked ? 'Ja' : 'Nein'}</p>
								</div>
							</div>
						</div>
					</div>

					<div class="card border border-base-300 bg-base-100">
						<div class="card-body">
							<h3 class="card-title">Signatur-Claims & Flagging</h3>
							<div class="flex flex-wrap gap-2">
								<div
									class="badge badge-outline"
									style={`color: ${signatureData?.claims?.vatApplicable ? 'green' : 'red'}`}
								>
									Mehrwertsteuer-pflichtig (Nutzerangabe): {signatureData?.claims?.vat_applicable
										? 'Ja'
										: 'Nein'}
								</div>
								<div
									class="badge badge-outline"
									style={`color: ${signatureData?.claims?.incomeTaxApplicable ? 'green' : 'red'}`}
								>
									Einkommensteuer-pflichtig (Nutzerangabe): {signatureData?.claims
										?.income_tax_applicable
										? 'Ja'
										: 'Nein'}
								</div>
								<div class="badge badge-primary">
									Flagging Score: {signatureData?.claims?.flaggingScore ?? '-'}
								</div>
							</div>
							<p class="mt-3 text-sm">
								<strong>Flagging Reasons:</strong>
								{signatureData?.claims?.flaggingReasons ?? '-'}
							</p>
							<div class="alert mt-3">
								<span
									><strong>Hinweis:</strong> Es gibt VAT-, Income- und Transaction-Tax. Der Tax Free Pool
									ist der Betrag, bis zu dem keine Transaction-Tax auf einzelne Transaktionen anfällt.</span
								>
							</div>
						</div>
					</div>

					<div class="flex justify-end mt-6">
						<button class="btn btn-success" on:click={openCloseModal}>Fall abschließen</button>
					</div>
				</div>

				<dialog id="close_case_modal" class="modal">
					<div class="modal-box w-11/12 max-w-md">
						<h3 class="text-lg font-bold">Fall abschließen</h3>
						<p class="py-4">Wie möchten Sie diesen Fall abschließen?</p>

						<div class="space-y-4">
							<div>
								<label class="label" for="reason">
									<span class="label-text">Grund</span>
								</label>
								<textarea
									id="reason"
									class="textarea textarea-bordered w-full"
									placeholder="Grund für die Entscheidung eingeben..."
									bind:value={reason}
								></textarea>
								<p class="mt-1 text-sm">
									Vorlage:
								</p>
								<select class="select select-bordered w-full mt-1" on:change={(e) => reason = e.currentTarget.value}>
									<option value="" selected>-- Vorlage auswählen --</option>
									<option value="Die Transaktion entspricht den gesetzlichen Vorschriften und ist daher legal.">Legal - Gesetzliche Vorschriften erfüllt</option>
									<option value="Die Transaktion weißt auf einen Kauf von einem Produkt hin. Sie enthält allerdings nicht die Gesetzliche Umsatzsteuen/Mehrwertsteuer.">Illegal - Umsatzsteuer nicht enthalten</option>
									<option value="Die Transaktion weißt auf die Überweisung von Einkommen hin. Sie enthält allerdings nicht die Gesetzliche Einkommensteuer.">Illegal - Einkommensteuer nicht enthalten</option>
								</select>
							</div>

							<div class="flex gap-2 justify-end">
								<button class="btn btn-outline" on:click={closeModal}>Abbrechen</button>
								<button class="btn btn-error" on:click={() => submitCase(false)}>Illegal</button>
								<button class="btn btn-success" on:click={() => submitCase(true)}>Legal</button>
							</div>
						</div>
					</div>
					<form method="dialog" class="modal-backdrop">
						<button aria-label="close modal"></button>
					</form>
				</dialog>
			{/if}
		</div>

		<input
			type="radio"
			name="tabs"
			class="tab"
			aria-label="Archiv"
			checked={currentTab === 'archive'}
			on:click={() => {
				currentTab = 'archive';
			}}
		/>
		<div class="tab-content bg-base-100 border-base-300 p-6">
			<h1 class="text-2xl font-bold mb-4">Archiv</h1>
			{#if !archiveCaseSelected}
				{#if archivedCases.length === 0}
					<p>Keine archivierten Fälle vorhanden.</p>
				{:else}
					<div class="overflow-x-auto">
						<table class="table">
							<thead>
								<tr>
									<th></th>
									<th>Fall ID</th>
									<th>Transaktionsbetrag</th>
									<th>Transaktionsdatum</th>
									<th>Archiviert am</th>
									<th>Aktionen</th>
								</tr>
							</thead>
							<tbody>
								{#each archivedCases as c, i}
									<tr>
										<th>{(archivePage - 1) * archivePerPage + i + 1}</th>
										<td>{c.id}</td>
										<td
											>{formatAmount(c.expand?.transaction?.amount)}
											{c.expand?.transaction?.currency ?? ''}</td
										>
										<td>{formatDate(c.expand?.transaction?.created)}</td>
										<td>{formatDate(c.created)}</td>
										<td>
											<button class="btn btn-primary btn-sm" on:click={() => selectArchivedCase(c)}
												>Details</button
											>
										</td>
									</tr>
								{/each}
							</tbody>
						</table>
					</div>
					<div class="mt-4 flex items-center justify-between">
						<button
							class="btn btn-outline btn-sm"
							on:click={() => goToArchivePage(archivePage - 1)}
							disabled={archivePage <= 1}
						>
							Zurück
						</button>
						<span class="text-sm opacity-70">Seite {archivePage} von {archiveTotalPages}</span>
						<button
							class="btn btn-outline btn-sm"
							on:click={() => goToArchivePage(archivePage + 1)}
							disabled={archivePage >= archiveTotalPages}
						>
							Weiter
						</button>
					</div>
				{/if}
			{:else}
				<div class="space-y-6">
					<div class="flex flex-wrap items-center justify-between gap-2">
						<h2 class="text-xl font-bold">Archivierter Fall {selectedArchivedCase?.id}</h2>
						<button
							class="btn btn-outline btn-sm"
							on:click={() => {
								archiveCaseSelected = false;
								selectedArchivedCase = null;
								archivedSignatureData = null;
							}}
						>
							Zurück zur Liste
						</button>
					</div>

					<div class="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
						<div class="stat rounded-box border border-base-300 bg-base-100">
							<div class="stat-title">Betrag</div>
							<div class="stat-value text-lg">{formatAmount(archivedTransaction?.amount)}</div>
							<div class="stat-desc">Angekommen: {formatAmount(archivedTransaction?.received)}</div>
						</div>
						<div class="stat rounded-box border border-base-300 bg-base-100">
							<div class="stat-title">Abgeschlossen</div>
							<div class="stat-value text-lg">{formatDate(archivedTransaction?.created)}</div>
							{#if selectedArchivedCase?.illegal}
								<div class="badge badge-error stat-desc">Illegal</div>
							{:else}
								<div class="badge badge-success stat-desc">Legal</div>
							{/if}
						</div>
						<div class="stat rounded-box border border-base-300 bg-base-100">
							<div class="stat-title">Sender ID</div>
							<div class="stat-value text-lg">{archivedTransaction?.sender ?? '-'}</div>
						</div>
						<div class="stat rounded-box border border-base-300 bg-base-100">
							<div class="stat-title">Receiver ID</div>
							<div class="stat-value text-lg">{archivedTransaction?.receiver ?? '-'}</div>
						</div>
					</div>

					<div class="grid grid-cols-1 gap-4 xl:grid-cols-2">
						<div class="card border border-base-300 bg-base-100">
							<div class="card-body">
								<h3 class="card-title">Transaktionsdetails</h3>
								<div class="space-y-2 text-sm">
									<p><strong>Transaktions-ID:</strong> {archivedTransaction?.id ?? '-'}</p>
									<p><strong>Beschreibung:</strong> {archivedTransaction?.description ?? '-'}</p>
									<p><strong>System:</strong></p>
									<pre class="mockup-code text-xs whitespace-pre-wrap break-all p-4"><code
											>{archivedTransaction?.system ?? '-'}</code
										></pre>
									<p><strong>Signatur:</strong></p>
									<pre class="mockup-code text-xs whitespace-pre-wrap break-all p-4"><code
											>{archivedTransaction?.signature ?? '-'}</code
										></pre>
								</div>
							</div>
						</div>

						<div class="card border border-base-300 bg-base-100">
							<div class="card-body">
								<h3 class="card-title">Steuerdaten</h3>
								<div class="overflow-x-auto">
									<table class="table table-sm">
										<tbody>
											<tr
												><th>Ürsprünglicher Betrag</th><td
													>{formatAmount(archivedTaxLog?.originalAmount)}</td
												></tr
											>
											<tr
												><th>Mehrwertsteuer</th><td>{formatAmount(archivedTaxLog?.vatAmount)}</td
												></tr
											>
											<tr
												><th>Einkommensteuer</th><td
													>{formatAmount(archivedTaxLog?.incomeTaxAmount)}</td
												></tr
											>
											<tr
												><th>Transaktionssteuer</th><td
													>{formatAmount(archivedTaxLog?.transactionTaxAmount)}</td
												></tr
											>
											<tr
												><th>Steuerfreier Pool verwendet</th><td
													>{formatAmount(archivedTaxLog?.taxFreePoolUsed)}</td
												></tr
											>
											<tr
												><th>Neuer Steuerfreier Pool</th><td
													>{formatAmount(archivedTaxLog?.newTaxFreePool)}</td
												></tr
											>
											<tr
												><th>Gesamtsteuer</th><td>{formatAmount(archivedTaxLog?.totalTaxAmount)}</td
												></tr
											>
											<tr
												><th>Vorheriges Einkommen (für Einkommensteuerberechnung)</th><td
													>{formatAmount(archivedTaxLog?.previousIncomeReceived)}</td
												></tr
											>
										</tbody>
									</table>
								</div>
							</div>
						</div>
					</div>

					<div class="grid grid-cols-1 gap-4 xl:grid-cols-2">
						<div class="card border border-base-300 bg-base-100">
							<div class="card-body">
								<h3 class="card-title">Sender</h3>
								<div class="space-y-1 text-sm">
									<p><strong>Account ID:</strong> {archivedSender?.id ?? '-'}</p>
									<p><strong>Frozen:</strong> {archivedSender?.frozen ? 'Ja' : 'Nein'}</p>
									<p><strong>Balance:</strong> {formatAmount(archivedSender?.balance)}</p>
									<p><strong>Name:</strong> {archivedSenderUser?.name ?? '-'}</p>
									<p>
										<strong>Firma:</strong>
										{archivedSenderUser?.isCompany ? archivedSenderUser?.companyName : '-'}
									</p>
									<p><strong>Locked:</strong> {archivedSenderUser?.locked ? 'Ja' : 'Nein'}</p>
								</div>
							</div>
						</div>

						<div class="card border border-base-300 bg-base-100">
							<div class="card-body">
								<h3 class="card-title">Receiver</h3>
								<div class="space-y-1 text-sm">
									<p><strong>Account ID:</strong> {archivedReceiver?.id ?? '-'}</p>
									<p><strong>Frozen:</strong> {archivedReceiver?.frozen ? 'Ja' : 'Nein'}</p>
									<p><strong>Balance:</strong> {formatAmount(archivedReceiver?.balance)}</p>
									<p><strong>Name:</strong> {archivedReceiverUser?.name ?? '-'}</p>
									<p>
										<strong>Firma:</strong>
										{archivedReceiverUser?.isCompany ? archivedReceiverUser?.companyName : '-'}
									</p>
									<p><strong>Locked:</strong> {archivedReceiverUser?.locked ? 'Ja' : 'Nein'}</p>
								</div>
							</div>
						</div>
					</div>

					<div class="card border border-base-300 bg-base-100">
						<div class="card-body">
							<h3 class="card-title">Signatur-Claims & Flagging</h3>
							<div class="flex flex-wrap gap-2">
								<div
									class="badge badge-outline"
									style={`color: ${archivedSignatureData?.claims?.vatApplicable ? 'green' : 'red'}`}
								>
									Mehrwertsteuer-pflichtig (Nutzerangabe): {archivedSignatureData?.claims
										?.vat_applicable
										? 'Ja'
										: 'Nein'}
								</div>
								<div
									class="badge badge-outline"
									style={`color: ${archivedSignatureData?.claims?.incomeTaxApplicable ? 'green' : 'red'}`}
								>
									Einkommensteuer-pflichtig (Nutzerangabe): {archivedSignatureData?.claims
										?.income_tax_applicable
										? 'Ja'
										: 'Nein'}
								</div>
								<div class="badge badge-primary">
									Flagging Score: {archivedSignatureData?.claims?.flaggingScore ?? '-'}
								</div>
							</div>
							<p class="mt-3 text-sm">
								<strong>Flagging Reasons:</strong>
								{archivedSignatureData?.claims?.flaggingReasons ?? '-'}
							</p>
							<div class="alert mt-3">
								<span
									><strong>Hinweis:</strong> Es gibt VAT-, Income- und Transaction-Tax. Der Tax Free Pool
									ist der Betrag, bis zu dem keine Transaction-Tax auf einzelne Transaktionen anfällt.</span
								>
							</div>
						</div>
					</div>
				</div>
			{/if}
		</div>
	</div>

	<UserSelectModal
		bind:this={selectUserModal}
		on:select={(e) => resetFirstLogin(e.detail)}
		selectButton={'Registrierung zurücksetzen'}
	/>
</Site>

<script lang="ts">
	import db, { server } from '$lib/connections';
	import AdminSite from '$modules/AdminSite.svelte';
	import { BanIcon, CheckIcon } from '@lucide/svelte';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';

	let signatureInput: string = '';
	let show: boolean = false;
	let error: boolean = false;
	let message: string = '';
	let valid: boolean = false;

	// Data from the claims
	let amount: number = 0;
	let received: number = 0;
	let aud: string = '';
	let code: string = '';
	let description: string = '';
	let exp: number = 0;
	let fromAccount: string = '';
	let iss: string = '';
	let nbf: number = 0;
	let timeStamp: number = 0;
	let toAccount: string = '';
	let system: string = '';
	let vatApplicable: string = '';
	let incomeTaxApplicable: string = '';
	let flaggingScore: string = '';
	let flaggingReasons: string = '';

	onMount(async () => {
		if (!browser) return;
	});

	function formatTimestamp(ts: number | string) {
		const n = Number(ts);
		if (!isFinite(n)) return String(ts);
		// if timestamp looks like seconds (<= 10-11 digits) convert to ms, if already ms (13+ digits) leave as is
		const ms = n < 1e12 ? n * 1000 : n;
		return new Date(ms).toLocaleString();
	}
	async function readSignature() {
		// Funktion zum Auslesen und Verifizieren der Signatur
		const rsp: any = await fetch(
			`${server}/api/verify-signature?signature=${encodeURIComponent(signatureInput.trim())}`,
			{
				method: 'GET',
				headers: {
					'Content-Type': 'application/json',
					Authorization: `Bearer ${db.authStore.token}`
				}
			}
		).catch((err) => {
			error = true;
			message = err.message;
			show = false;
			return;
		});
		try {
			show = true;
			error = false;
			let payload: any = await rsp.json();
			console.log(payload);
			valid = payload.valid;
			// Extract claims
			amount = payload.claims.amount;
			received = payload.claims.received;
			aud = payload.claims.aud;
			code = payload.claims.code;
			description = payload.claims.description;
			system = payload.claims.system;
			exp = payload.claims.exp;
			fromAccount = payload.claims.fromAccountID;
			iss = payload.claims.iss;
			nbf = payload.claims.nbf;
			timeStamp = payload.claims.timestamp;
			toAccount = payload.claims.toAccountID;
			vatApplicable = payload.claims.vatApplicable;
			incomeTaxApplicable = payload.claims.incomeTaxApplicable;
			flaggingScore = payload.claims.flaggingScore;
			flaggingReasons = payload.claims.flaggingReasons;
		} catch (err: any) {
			error = true;
			message = err.message;
			show = false;
		}
	}
</script>

<title>Admin - Signatur auslesen</title>
<AdminSite>
	<h1 class="text-2xl font-bold mb-4">Signatur auslesen</h1>
	<p>Hier können Sie Signaturen auslesen und verifizieren.</p>

	<div class="flex flex-row justify-between gap-2 mb-2">
		<input
			type="text"
			placeholder="Signatur eingeben/einfügen"
			autocomplete="off"
			class="input input-bordered w-full mt-4"
			bind:value={signatureInput}
		/>
		<button class="btn btn-primary ml-4 mt-4" on:click={readSignature}>Signatur auslesen</button>
	</div>

	{#if error}
		<div role="alert" class="alert alert-error">
			<BanIcon></BanIcon>
			<span>Error: {message}</span>
		</div>
	{/if}

	{#if show}
		{#if valid}
			<div role="alert" class="alert alert-success mt-2">
				<CheckIcon></CheckIcon>
				<span>Die Signatur ist gültig!</span>
			</div>
			<div class="overflow-x-auto mt-4">
				<h2 class="font-bold text-lg mb-2">Payload:</h2>
				<table class="table w-full">
					<thead>
						<tr>
							<th>Field</th>
							<th>Value</th>
						</tr>
					</thead>
					<tbody>
						<tr><td class="font-semibold">Issuer</td><td>{iss}</td></tr>
						<tr><td class="font-semibold">Audience</td><td>{aud}</td></tr>
						<tr><td class="font-semibold">From Account ID</td><td>{fromAccount}</td></tr>
						<tr><td class="font-semibold">To Account ID</td><td>{toAccount}</td></tr>
						<tr><td class="font-semibold">Amount</td><td>{amount}</td></tr>
						<tr><td class="font-semibold">Received</td><td>{received}</td></tr>
						<tr><td class="font-semibold">Description</td><td>{description}</td></tr>
						<tr><td class="font-semibold">System</td><td>{system}</td></tr>
						<tr><td class="font-semibold">Code</td><td>{code}</td></tr>
						<tr><td class="font-semibold">VAT Applicable</td><td>{vatApplicable}</td></tr>
						<tr
							><td class="font-semibold">Income Tax Applicable</td><td>{incomeTaxApplicable}</td
							></tr
						>
						<tr><td class="font-semibold">Flagging Score</td><td>{flaggingScore}</td></tr>
						<tr><td class="font-semibold">Flagging Reasons</td><td>{flaggingReasons}</td></tr>
						<tr>
							<td class="font-semibold">Not Before (nbf)</td>
							<td>{formatTimestamp(nbf)}</td>
						</tr>
						<tr>
							<td class="font-semibold">Expiration (exp)</td>
							<td>{formatTimestamp(exp)}</td>
						</tr>
						<tr>
							<td class="font-semibold">Timestamp</td>
							<td>{formatTimestamp(timeStamp)}</td>
						</tr>
					</tbody>
				</table>
			</div>
		{:else}
			<div role="alert" class="alert alert-warning">
				<BanIcon></BanIcon>
				<span>Warnung: Signatur ist ungültig!</span>
			</div>
		{/if}
	{/if}
</AdminSite>

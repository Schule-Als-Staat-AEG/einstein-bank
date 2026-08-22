<script lang="ts">
	import Site from '$modules/Site.svelte';
	import { goto } from '$app/navigation';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';
	import Currency from '$modules/Currency.svelte';

	import {
		CheckCircle2,
		ArrowLeft,
		Send,
		Copy,
		CalendarClock,
		UserRound,
		ReceiptText
	} from '@lucide/svelte';

	let amount: number = 0;
	let receiver: string = '';
	let description: string = '';
	let dateLabel: string = '';
	let transactionID: string = '';

	function copyText(txt: string) {
		try {
			navigator.clipboard?.writeText(txt);
		} catch (e) {}
	}

	onMount(() => {
		if (!browser) return;
		const urlParams = new URLSearchParams(window.location.search);
		amount = Number(urlParams.get('amount')) || 0;
		receiver = urlParams.get('receiver') || '';
		description = urlParams.get('description') || '';
		transactionID = urlParams.get('id') || '';

		dateLabel = new Date().toLocaleString('de-DE');
	});
</script>

<title>Transaktion erfoglreich</title>
<Site>
	<div class="min-h-[65vh] flex items-center justify-center px-3 py-6">
		<div class="w-full max-w-4xl rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
			<!-- Header -->
			<div class="flex flex-col md:flex-row md:items-center md:justify-between gap-3">
				<div class="flex items-center gap-3">
					<div
						class="w-12 h-12 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center shadow"
					>
						<CheckCircle2 size={22} class="text-success" />
					</div>
					<div class="flex flex-col">
						<h1 class="text-2xl font-bold leading-tight">Überweisung erfolgreich</h1>
						<p class="text-sm opacity-70 mt-0.5">
							Deine Überweisung wurde erfolgreich durchgeführt.
						</p>
					</div>
				</div>

				<div class="flex items-center gap-2">
					<button class="btn btn-ghost" on:click={() => goto('/dashboard')}>
						<ArrowLeft size={18} />
						Dashboard
					</button>
					<button class="btn btn-primary" on:click={() => goto('/transfer')}>
						<Send size={18} />
						Neue Überweisung
					</button>
				</div>
			</div>

			<div class="divider my-4"></div>

			<!-- Summary cards -->
			<div class="grid grid-cols-1 lg:grid-cols-3 gap-3">
				<!-- Amount -->
				<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
					<div class="text-sm opacity-60 flex items-center gap-2">
						<Send size={16} />
						<span>Betrag</span>
					</div>
					<div class="mt-2 flex items-center gap-2">
						<span class="text-3xl font-bold">{amount.toFixed(2)}</span>
						<Currency size={22} />
					</div>
				</div>

				<!-- Receiver -->
				<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
					<div class="text-sm opacity-60 flex items-center gap-2">
						<UserRound size={16} />
						<span>Empfänger</span>
					</div>

					<div class="mt-2 flex items-start justify-between gap-2">
						<p class="text-lg font-bold break-all">{receiver || '—'}</p>
						<button
							class="btn btn-ghost btn-sm"
							on:click={() => copyText(receiver)}
							aria-label="Empfänger kopieren"
							disabled={!receiver}
						>
							<Copy size={16} />
						</button>
					</div>
				</div>

				<!-- Date -->
				<div class="rounded-2xl bg-base-100 border border-base-300 p-4">
					<div class="text-sm opacity-60 flex items-center gap-2">
						<CalendarClock size={16} />
						<span>Datum</span>
					</div>
					<div class="mt-2 text-lg font-bold">{dateLabel}</div>
				</div>

				<!-- Description -->
				<div class="rounded-2xl bg-base-100 border border-base-300 p-4 lg:col-span-3">
					<div class="text-sm opacity-60 flex items-center gap-2">
						<ReceiptText size={16} />
						<span>Verwendungszweck</span>
					</div>
					<div class="mt-2 whitespace-pre-wrap opacity-90">
						{description ? description : 'Kein Verwendungszweck angegeben'}
					</div>
				</div>
			</div>

			<!-- Footer actions -->
			<div
				class="mt-5 flex flex-col md:flex-row items-stretch md:items-center justify-between gap-2"
			>
				<button
					class="btn btn-outline"
					on:click={() => goto('/transactions?id=' + encodeURIComponent(transactionID))}
				>
					Transaktion ansehen
				</button>
			</div>

			<div class="mt-3 text-xs opacity-60">
				Tipp: Du kannst die Transaktions-ID in der Transaktionsliste kopieren, falls du später einen
				Nachweis brauchst.
			</div>
		</div>
	</div>
</Site>

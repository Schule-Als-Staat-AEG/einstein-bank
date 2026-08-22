<script lang="ts">
	import { browser } from '$app/environment';
	import { getAuthStore } from '$lib/auth';
	import db from '$lib/connections';
	import cronToGerman from '$lib/cron';
	import Currency from '$modules/Currency.svelte';
	import Site from '$modules/Site.svelte';
	import { Info, Signature } from '@lucide/svelte';
	import { onMount } from 'svelte';
	import { getSetting } from '$lib/settings';

	let contracts: any[] = [];
	let page = 1;
	let perPage = 5;
	let totalItems = 0;
	let totalPages = 0;
	let contactEmail = '';

	getSetting('contact_email').then((value) => {
		if (value) contactEmail = value;
	});

	onMount(async () => {
		if (!browser) return;
		await fetchContracts();
	});

	async function fetchContracts() {
		const userId = getAuthStore()?.record?.id;
		if (!userId) {
			contracts = [];
			totalItems = 0;
			totalPages = 0;
			return;
		}

		const data = await db.collection('contracts').getList(page, perPage, {
			filter: `sender.user.id="${userId}" || receiver.user.id="${userId}"`,
			requestKey: null
		});

		totalItems = data.totalItems;
		totalPages = data.totalPages;

		await Promise.all(
			data.items.map(async (contract) => {
				const senderData = await db
					.collection('public_accounts')
					.getOne(contract.sender, { requestKey: null });
				const receiverData = await db
					.collection('public_accounts')
					.getOne(contract.receiver, { requestKey: null });

				contract.senderName = senderData.isCompany ? senderData.companyName : `${senderData.name}`;
				contract.receiverName = receiverData.isCompany
					? receiverData.companyName
					: `${receiverData.name}`;
			})
		);

		contracts = data.items;
	}

	function formatDate(dateValue: string) {
		return new Date(dateValue).toLocaleDateString('de-DE', {
			day: '2-digit',
			month: '2-digit',
			year: 'numeric'
		});
	}

	function getContractState(contract: any) {
		if (contract.completed) {
			return {
				label: 'Abgeschlossen',
				badgeClass: 'badge-success',
				panelClass: 'border-success/25 bg-success/5'
			};
		}

		return {
			label: 'Laufend',
			badgeClass: 'badge-warning',
			panelClass: 'border-warning/20 bg-warning/5'
		};
	}

	$: activeContracts = contracts.filter((contract: any) => !contract.completed).length;
	$: completedContracts = contracts.filter((contract: any) => contract.completed).length;
</script>

<Site>
	<div class="flex flex-col gap-4">
		<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
			<div class="flex flex-col md:flex-row md:items-center md:justify-between gap-3">
				<div class="flex items-center gap-3">
					<div class="w-11 h-11 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center shadow">
						<Signature size={20} />
					</div>
					<div class="flex flex-col">
						<h1 class="font-bold text-2xl leading-tight">Verträge</h1>
						<div class="text-sm opacity-70 flex items-center gap-2">
							<Info size={16} />
							<span>
								Hier siehst du alle mit dir verbundenen Verträge. Bei Rückfragen melde dich per E-Mail
								<a href="mailto:{contactEmail}" class="link link-primary">{contactEmail}</a>.
							</span>
						</div>
					</div>
				</div>
			</div>

			<div class="mt-4 grid grid-cols-1 md:grid-cols-3 gap-3">
				<div class="bg-base-100 border border-base-300 rounded-2xl p-4 shadow-sm">
					<div class="text-sm opacity-60">Gesamt</div>
					<div class="text-2xl font-bold mt-1">{totalItems}</div>
				</div>
				<div class="bg-base-100 border border-base-300 rounded-2xl p-4 shadow-sm">
					<div class="text-sm opacity-60">Laufend</div>
					<div class="mt-1 text-sm leading-snug opacity-80">
						Aktiv sichtbare Verträge im aktuellen Bereich.
					</div>
					<div class="mt-2 text-2xl font-bold text-warning">{activeContracts}</div>
				</div>
				<div class="bg-base-100 border border-base-300 rounded-2xl p-4 shadow-sm">
					<div class="text-sm opacity-60">Abgeschlossen</div>
					<div class="mt-1 text-sm leading-snug opacity-80">
						Bereits erledigte Verträge in deiner Übersicht.
					</div>
					<div class="mt-2 text-2xl font-bold text-success">{completedContracts}</div>
				</div>
			</div>
		</div>

		{#if contracts.length === 0}
			<div class="rounded-2xl bg-base-200 border border-base-300 p-8 text-center shadow">
				<div class="flex justify-center mb-3">
					<div class="w-12 h-12 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center">
						<Signature size={20} />
					</div>
				</div>
				<div class="font-bold text-lg">Noch keine Verträge gefunden</div>
				<div class="opacity-70 mt-1">
					Sobald ein Vertrag mit deinem Konto verknüpft ist, erscheint er hier.
				</div>
			</div>
		{:else}
			<div class="space-y-3">
				{#each contracts as contract}
					{@const state = getContractState(contract)}
					<div class="rounded-2xl bg-base-200 border border-base-300 shadow p-4 md:p-5">
						<div class="flex flex-col md:flex-row md:items-start md:justify-between gap-4">
							<div class="flex-1 min-w-0">
								<div class="flex items-start gap-3">
									<div class="w-10 h-10 rounded-2xl bg-base-100 border border-base-300 flex items-center justify-center shadow shrink-0">
										<Signature size={18} />
									</div>

									<div class="min-w-0">
										<div class="flex flex-wrap items-center gap-2">
											<span class="badge badge-ghost badge-sm">Vertrag</span>
											<span class="font-bold text-lg leading-tight text-base-content">{contract.title}</span>
										</div>

										<div class="mt-1 text-sm opacity-70 leading-relaxed">
											Vertrag zwischen <span class="font-medium text-base-content">{contract.senderName}</span> und
											<span class="font-medium text-base-content">{contract.receiverName}</span>
										</div>

										<div class="mt-2 flex flex-wrap items-center gap-2">
											<span class="badge badge-outline gap-2 py-3 px-3">
												<span class="opacity-70">Betrag</span>
												<span class="font-bold text-base-content">{contract.amount}</span>
												<Currency size={16} />
											</span>

											{#if contract.completed}
												<span class="badge badge-success badge-outline">Abgeschlossen</span>
											{:else}
												<span class="badge badge-warning badge-outline">Laufend</span>
											{/if}
										</div>

										<div class="mt-3 rounded-2xl bg-base-100 border border-base-300/70 p-4 prose max-w-full prose-p:my-0 prose-p:leading-relaxed prose-headings:mb-2 prose-headings:mt-0">
											{@html contract.description}
										</div>
									</div>
								</div>
							</div>

							<div class="flex flex-col gap-2 md:items-end">
								<div class="flex items-center gap-2 flex-wrap justify-start md:justify-end">
									<span class="badge badge-ghost gap-2">
										<Info size={14} />
										<span class="font-mono">{contract.id}</span>
									</span>
									<span class="badge badge-ghost">Von: <b>{contract.senderName}</b></span>
									<span class="badge badge-ghost">An: <b>{contract.receiverName}</b></span>
								</div>

								<div class="flex items-center gap-2 flex-wrap justify-start md:justify-end">
									<span class="badge badge-ghost">Erstellt: {formatDate(contract.created)}</span>
									<span class="badge badge-ghost">
										{contract.executed}/{contract.maximum == -1 ? 'Unbegrenzt' : contract.maximum}
									</span>
									<span class="badge badge-ghost">{cronToGerman(contract.cron)}</span>
								</div>
							</div>
						</div>
					</div>
				{/each}
			</div>
			{/if}
		<div class="flex justify-center pt-2">
			<div class="join shadow-sm">
				<button
					class="join-item btn"
					on:click={() => {
						page > 1 && (page -= 1);
						fetchContracts();
					}}
					disabled={page === 1}
				>
					Vorherige Seite
				</button>
				<button class="join-item btn pointer-events-none" disabled>
					Seite {page} / {totalPages || 1}
				</button>
				<button
					class="join-item btn"
					on:click={() => {
						page < totalPages && (page += 1);
						fetchContracts();
					}}
					disabled={page === totalPages}
				>
					Nächste Seite
				</button>
			</div>
		</div>
	</div>
</Site>
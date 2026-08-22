<script lang="ts">
	import { browser } from '$app/environment';
	import { onMount } from 'svelte';
	import { getAuthStore, getUserData, refreshStores } from '$lib/auth';
	import hello_0 from '$lib/assets/images/hello_0.png';
	import hello_1 from '$lib/assets/images/hello_1.png';
	import hello_2 from '$lib/assets/images/hello_2.png';
	import hello_3 from '$lib/assets/images/hello_3.png';
	import hello_4 from '$lib/assets/images/hello_4.png';
	import hello_5 from '$lib/assets/images/hello_5.png';
	import db from '$lib/connections';

	let firstTime = false;
	let modal: HTMLDialogElement;

	let pages = [
		{
			title: 'Willkommen bei Einstein Bank!',
			description: firstTime
				? 'Hier kannst du dir einen Überblick über alle Funktionen verschaffen.'
				: 'Es sieht so aus, als wäre dies dein erster Besuch. Möchtest du eine kurze Einführung?',
			image: null
		},
		{
			title: 'Das Dashboard',
			description:
				'Im Dashboard findest du eine Übersicht über dein Konto, aktuelle Transaktionen und dein Kontostand.',
			image: hello_0
		},
		{
			title: 'Transaktionen',
			description:
				'Hier kannst du deine alle ein und ausgehende Transaktionen einsehen und verwalten.',
			image: hello_1
		},
		{
			title: 'Geld überweisen',
			description:
				'Mit wenigen Klicks kannst du Geld an andere Konten überweisen. Einfach Empfänger, Betrag und Verwendungszweck eingeben und schon ist die Überweisung unterwegs.',
			image: hello_2
		},
		{
			title: 'Überweisung überprüfen',
			description:
				'Bevor du eine Überweisung abschließt, kannst du alle Details noch einmal überprüfen. So stellst du sicher, dass alles korrekt ist.',
			image: hello_3
		},
		{
			title: 'Dauerauftäge',
			description:
				'Du kannst Daueraufträge einrichten, um regelmäßig wiederkehrende Zahlungen automatisch ausführen zu lassen. So verpasst du keine wichtigen Zahlungen mehr.',
			image: hello_4
		},
		{
			title: 'Deine Bankkarte',
			description:
				'Hier kannst du deine Bankkarte verwalten. Falls du deine Karte verloren hast kannst du sie hier sperren.',
			image: hello_5
		}
	];

	let currentPage = 0;

	function open() {
		currentPage = 0;
		modal.showModal();
	}
	function nextPage() {
		if (currentPage < pages.length - 1) {
			currentPage++;
		}
	}
	export { open };

	onMount(async () => {
		if (!browser) return;
		firstTime = getUserData().firstTimeLogin;
	});
</script>

<dialog id="first-run-modal" class="modal" bind:this={modal}>
	<form method="dialog" class="modal-box max-w-4xl">
		<div class="w-full flex items-center justify-between">
			<h3 class="text-lg font-bold">{pages[currentPage].title}</h3>
			<button class="btn" on:click={() => modal.close()}>Schließen</button>
		</div>
		{#if pages[currentPage].image}
			<img src={pages[currentPage].image} alt="Hello" class="w-full rounded-lg" />
		{/if}
		<p class="py-4">{pages[currentPage].description}</p>
		<div class="w-full justify-center flex gap-1">
			{#each pages as _, index}
				{#if index === currentPage}
					<span class="status status-primary"></span>
				{:else}
					<span class="status"></span>
				{/if}
			{/each}
		</div>
		<div class="modal-action flex justify-between">
			<div>
				{#if currentPage > 0}
					<button type="button" class="btn btn-primary" on:click={() => currentPage--}
						>Vorherige Seite</button
					>
				{/if}
			</div>
			<div>
				{#if currentPage < pages.length - 1}
					<button type="button" class="btn btn-primary" on:click={nextPage}>Nächste Seite</button>
				{:else}
					<button type="button" class="btn btn-primary" on:click={() => modal.close()}
						>Fertig!</button
					>
				{/if}
			</div>
		</div>
	</form>
</dialog>

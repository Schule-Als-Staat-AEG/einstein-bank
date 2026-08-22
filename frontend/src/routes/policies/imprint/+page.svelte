<script>
	// Minimal Svelte page for the Impressum.
	import db from '$lib/connections';
	import { getSetting } from '$lib/settings';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';

	let contactEmail = '';
	getSetting('contact_email').then((value) => {
		if (value) contactEmail = value;
	});

	onMount(async () => {
		if (!browser) return;
	});
</script>

<title>Impressum</title>

<!-- Use the same centered full-height layout as other legal pages -->
<div class="min-h-screen bg-base-200 flex items-center justify-center p-6">
	<div class="card w-full max-w-3xl bg-base-100 shadow-lg">
		<div class="card-body">
			<header class="p-6 border-b dark:border-base-300">
				<h1 class="text-3xl font-semibold">Impressum</h1>
				<p class="mt-1 text-sm text-gray-600">Angaben gemäß § 5 TMG</p>
			</header>

			<div class="p-6 space-y-6">
				<div class="grid grid-cols-1 md:grid-cols-2 gap-6">
					<div>
						<h2 class="text-lg font-medium">Anschrift</h2>
						<address class="not-italic mt-2 text-sm leading-relaxed">
							[Insert your Info here]
						</address>
					</div>

					<div>
						<h2 class="text-lg font-medium">Kontakt</h2>
						<p class="mt-2 text-sm leading-relaxed">
							Telefon: <a
								href="tel:+[your phone number here]
								class="underline"
								style="color:var(--color-primary, #2563eb)">+[Your Phone Number]</a
							><br />
							E‑Mail:
							<a
								href="mailto:{contactEmail}"
								class="underline"
								style="color:var(--color-primary, #2563eb)">{contactEmail}</a
							>
						</p>
					</div>
				</div>

				<section class="prose prose-sm max-w-none">
					<h3>Verantwortlich für den Inhalt nach § 18 Abs. 2 MStV</h3>
					<p>[Your Data Here]</p>

					<h3>Haftung für Inhalte</h3>
					<p>
						Als Diensteanbieter sind wir gemäß § 7 Abs.1 TMG für eigene Inhalte auf diesen Seiten
						nach den allgemeinen Gesetzen verantwortlich. Nach §§ 8 bis 10 TMG sind wir als
						Diensteanbieter jedoch nicht verpflichtet, übermittelte oder gespeicherte fremde
						Informationen zu überwachen oder nach Umständen zu forschen, die auf eine rechtswidrige
						Tätigkeit hinweisen.
					</p>

					<h3>Haftung für Links</h3>
					<p>
						Unser Angebot enthält Links zu externen Websites Dritter, auf deren Inhalte wir keinen
						Einfluss haben. Deshalb können wir für diese fremden Inhalte auch keine Gewähr
						übernehmen. Für die Inhalte der verlinkten Seiten ist stets der jeweilige Anbieter oder
						Betreiber der Seiten verantwortlich.
					</p>

					<h3>Urheberrecht</h3>
					<p>
						Die durch die Seitenbetreiber erstellten Inhalte und Werke auf diesen Seiten unterliegen
						dem deutschen Urheberrecht. Beiträge Dritter sind als solche gekennzeichnet. Die
						Vervielfältigung, Bearbeitung, Verbreitung und jede Art der Verwertung außerhalb der
						Grenzen des Urheberrechtes bedürfen der schriftlichen Zustimmung des jeweiligen Autors
						bzw. Erstellers.
					</p>
					<h3>Information</h3>
					<p>
						Es werden keine echten Bankdienstleistungen angeboten. Das Projekt nutzt eine fiktive
						Währung für Projektzwecke. Alle Transaktionen und Kontostände sind rein virtuell und
						haben keinen realen Wert. Das Impressum dient ausschließlich rechtlichen Anforderungen
						und enthält keine echten Unternehmensinformationen.
					</p>
				</section>

				<div class="divider"></div>

				<!-- added: action buttons like other legal pages -->
				<div class="flex flex-col sm:flex-row gap-3 justify-end">
					{#if db.authStore.isValid}
						<a href="/dashboard" class="btn btn-primary">Zum Dashboard</a>
					{:else}
						<a href="/" class="btn btn-secondary">Zurück zur Startseite</a>
					{/if}
					<a href="mailto:{contactEmail}" class="btn btn-ghost">Kontakt / Support</a>
				</div>

				<footer class="px-6 py-4 border-t text-sm text-gray-500 dark:border-base-300">
					&copy; {new Date().getFullYear()} Einstein Bank Team — Alle Rechte vorbehalten.
				</footer>
			</div>
		</div>
	</div>
</div>

<script>
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

<title>Datenschutzerklärung</title>
<div class="min-h-screen bg-base-200 flex items-center justify-center p-6">
	<div class="card w-full max-w-3xl bg-base-100 shadow-lg">
		<div class="card-body">
			<h1 class="card-title text-2xl">Datenschutzerklärung</h1>
			<p class="opacity-70">
				Diese Seite informiert kurz und klar darüber, welche Daten wir erheben und wie wir diese
				verwenden.
			</p>

			<div class="divider"></div>

			<h2 class="text-lg font-semibold">Keine Tracking-Cookies</h2>
			<p class="mb-4">
				Es werden keine Tracking-Cookies zu Werbe- oder Analysezwecken eingesetzt. Nur technisch
				notwendige Cookies können verwendet werden, um grundlegende Funktionen der Seite zu
				ermöglichen.
			</p>

			<h2 class="text-lg font-semibold">Welche Daten wir speichern</h2>
			<ul class="list-disc list-inside mb-4 space-y-1">
				<li>
					<strong>IP-Adresse:</strong> Wird zur Identifikation, bei Verdacht auf Missbrauch und zur Zuordnung
					einer Transaktion gespeichert. Aufbewahrungsdauer: 1 Woche.
				</li>
				<li><strong>Nutzer-ID:</strong> Zur Zuordnung Ihres Kontos und Ihrer Transaktionen.</li>
				<li>
					<strong>Name:</strong> Wird zur Abwicklung und Darstellung von Transaktionen gespeichert. Sie
					haben die Möglichkeit einen "falschen" Namen zu verwenden.
				</li>
				<li>
					<strong>Passwort:</strong> Ihr Passwort wird verschlüsselt bzw. gehasht gespeichert. Wir speichern
					Passwörter niemals im Klartext und können Ihr Passwort nicht einsehen.
				</li>
				<li>
					<strong>Geolocation:</strong> Wir speichern keine Standortdaten (z. B. GPS-Koordinaten) Ihres
					Geräts oder Browsers.
				</li>
				<li>
					<strong>Einstellungen:</strong> Wir speichern Ihre Einstellungen, um die Nutzung der Platform
					genießbar zu machen.
				</li>
			</ul>

			<h2 class="text-lg font-semibold">Zweck der Verarbeitung</h2>
			<p class="mb-4">
				Die genannten Daten werden ausschließlich verwendet, um Transaktionen korrekt zuzuordnen,
				Betrug zu verhindern und die Sicherheit des Dienstes zu gewährleisten.
			</p>

			<h2 class="text-lg font-semibold">Weitergabe an Dritte</h2>
			<p class="mb-4">
				Wir geben Ihre Daten nicht an Dritte weiter. Es erfolgt keine Weiterleitung Ihrer
				personenbezogenen Daten an externe Unternehmen zu Werbe- oder Analysezwecken.
			</p>

			<h2 class="text-lg font-semibold">Cookies & Serverspeicherung</h2>
			<p class="mb-4">
				Wir speichern nur Authentifizierungs-Cookies (Sitzungs-/Auth-Cookies), die nötig sind, damit
				Sie eingeloggt bleiben und sich sicher im Dienst bewegen können. Dazu hinaus nutzten wir
				'Local Storage' für Caching Funktionen um den Datenverkehr zu reduzieren. Es werden keine
				weiteren Tracking-Cookies gesetzt. Alle anderen personenbezogenen Daten (Name, E-Mail,
				Konto- und Transaktionsdaten etc.) werden in unseren Serverdatenbanken gespeichert sowie im
				Autorisierungstoken selber.
			</p>
			<p class="mb-4">
				Wenn Sie mit dieser Datenverarbeitung nicht einverstanden sind, können Sie Ihr Konto
				löschen. Durch die Löschung werden Ihre personenbezogenen Daten aus den Benutzerprofilen auf
				unseren Servern entfernt. Ausgenommen davon sind:
			</p>
			<ul class="list-disc list-inside mb-4 space-y-1">
				<li>
					<strong>Logs:</strong> Protokolldaten wie IP-Adressen werden zur Sicherheitsanalyse und Missbrauchsbekämpfung
					weiterhin für bis zu 1 Woche gespeichert.
				</li>
				<li>
					<strong>Transaktionen:</strong> Transaktionsaufzeichnungen bleiben erhalten; sie enthalten jedoch
					weiterhin die ursprüngliche Nutzer-ID und dienen der Nachvollziehbarkeit von Zahlungen.
				</li>
			</ul>
			<p class="mb-4">
				Hinweis: Die Löschung Ihres Kontos entfernt nicht automatisch Authentifizierungs-Cookies
				oder andere im Browser gespeicherte Daten. Cookies müssen separat in Ihrem Browser gelöscht
				oder durch Abmeldung entfernt werden.
			</p>

			<h2 class="text-lg font-semibold">Verarbeitung von Daten Minderjähriger</h2>
			<p class="mb-4">
				Unsere Plattform kann auch von Kindern und Jugendlichen genutzt werden. Für Kinder unter 14
				Jahren ist die Nutzung nur mit Zustimmung der Erziehungsberechtigten zulässig. Wir erheben
				ausschließlich die Daten, die für den Betrieb der Plattform technisch notwendig sind (z. B.
				IP-Adresse, Nutzerkennung, Login-Daten). Diese Daten werden nicht an Dritte weitergegeben
				und ausschließlich zur Bereitstellung und Absicherung des Dienstes verwendet.
			</p>
			<p class="mb-4">
				Erziehungsberechtigte haben das Recht, jederzeit Auskunft über die gespeicherten Daten ihrer
				Kinder zu erhalten sowie deren Löschung zu verlangen. Im Falle eines Widerrufs der
				Einwilligung wird das Nutzerkonto des Kindes gelöscht und die personenbezogenen Daten werden
				entfernt, soweit keine gesetzlichen Aufbewahrungspflichten bestehen.
			</p>

			<h2 class="text-lg font-semibold">Ihre Rechte</h2>
			<p class="mb-4">
				Sie haben das Recht auf Auskunft, Berichtigung oder Löschung Ihrer personenbezogenen Daten.
				Bitte kontaktieren Sie hierfür den Support über die im Profil hinterlegten
				Kontaktmöglichkeiten oder über die im Dienst angegebenen Kanäle.
			</p>

			<div class="divider"></div>

			<div class="flex flex-col sm:flex-row gap-3 justify-end">
				{#if db.authStore.isValid}
					<a href="/dashboard" class="btn btn-primary">Zum Dashboard</a>
				{:else}
					<a href="/" class="btn btn-secondary">Zurück zur Startseite</a>
				{/if}
				<a href="mailto:{contactEmail}" class="btn btn-ghost">Kontakt / Support</a>
			</div>
		</div>
	</div>
</div>

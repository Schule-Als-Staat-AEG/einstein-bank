<script>
	import { getSetting } from '$lib/settings';
	import db from '$lib/connections';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';

	let contactEmail = '';
	getSetting('contact_email').then((value) => {
		if (value) contactEmail = value;
	});

	let displayName = 'Einstein Bank';
	getSetting('display_name').then((value) => {
		if (value) displayName = value;
	});

	onMount(async () => {
		if (!browser) return;
	});
</script>

<title>Allgemeine Geschäftsbedingungen</title>
<div class="min-h-screen bg-base-200 flex items-center justify-center p-6">
	<div class="card w-full max-w-3xl bg-base-100 shadow-lg">
		<div class="card-body">
			<h1 class="card-title text-2xl">Allgemeine Geschäftsbedingungen</h1>
			<p>
				<strong>Präambel:</strong> Diese Allgemeinen Geschäftsbedingungen (nachfolgend „AGB") regeln
				die vertraglichen Beziehungen zwischen dem Betreiber der Plattform (nachfolgend „Anbieter"
				oder „Diensteanbieter") und den Nutzern (nachfolgend „Nutzer" oder „Endnutzer") der
				digitalen Plattform {displayName}. Die nachfolgenden Bestimmungen bilden die
				rechtsverbindliche Grundlage für die Inanspruchnahme sämtlicher angebotenen Leistungen,
				Funktionalitäten und Dienste einschließlich aller damit in unmittelbarem Zusammenhang
				stehenden Unterleistungen und Nutzungsrechte.
			</p>
			<p class="text-sm opacity-30 mt-2">
				Derzeitige Version: 2.0.0 - 18. April 2026 | Gültig ab sofort mit bindender Wirkung
			</p>

			<img
				src="https://cdn.7tv.app/emote/01G63HG2Y8000D5QB328BXB7ZR/4x.gif"
				alt="Emote"
				class="my-4 h-24 w-auto rounded-lg"
			/>

			<div class="divider"></div>

			<h2 class="text-lg font-semibold">§1 Allgemeine Definitionen und Anwendungsbereich</h2>
			<ol class="list-decimal ml-6 space-y-2 mb-4 marker:text-base-content marker:opacity-65">
				<li class="text-base-content opacity-100">
					<strong>Geltungsbereich:</strong> Diese AGB finden Anwendung auf sämtliche Rechtsbeziehungen
					zwischen dem Anbieter und dem Nutzer hinsichtlich der Bereitstellung und Nutzung der digitalen
					Dienstleistungsplattform sowie aller hiermit verbundenen Funktionalitäten, Anwendungsdienste
					(Services) und Inhalte (im Folgenden zusammengefasst als „Plattform").
				</li>
				<li class="text-base-content opacity-100">
					<strong>Vertragsparteien:</strong> Vertragsparteien sind einerseits der Betreiber der
					Plattform ({displayName}, nachfolgend „Anbieter") und andererseits jede natürliche oder
					juristische Person, welche die Plattform im Wege des bloßen Zugriffs, der Registrierung
					oder durch konkrete Inanspruchnahme von Leistungen nutzt (nachfolgend „Nutzer"). Für
					minderjährige Nutzer gelten zusätzliche Bedingungen gemäß § 4.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Vertragsabschluss:</strong> Der Vertrag kommt durch die faktische Inanspruchnahme und/oder
					explizite Zustimmung des Nutzers zu diesen AGB zustande. Dies kann ausdrücklich durch ein Bestätigungsverfahren
					oder konkludent durch wiederholte Nutzung erfolgen. Mit dem Zugriff auf die Plattform erklärt
					sich der Nutzer als gebunden erklärend gegenüber allen nachfolgend aufgeführten Bedingungen.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Begriffsdefinitionen:</strong> „Nutzer" bezeichnet natürliche und juristische
					Personen sowie unternehmerische Zusammenschlüsse, die die Plattform aufsuchen oder nutzen.
					„Diensteanbieter" bezieht sich auf den Plattformbetreiber {displayName}. „Plattform"
					umfasst die Web-Applikation, Webschnittstellen, API-Zugriffe, mobile Anwendungen und
					sämtliche damit integrierten digitalen Dienste. „Assets" bezeichnen alle in den Systemen
					generierten und verwalteten digitalen Vermögenswerte, Werte, Transaktionsdatensätze,
					virtuelle Währungseinheiten und jede Form von Nutzerdaten.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Geltendes Recht:</strong> Diese Vereinbarung unterliegt ausschließlich dem deutschen
					Recht, insbesondere dem Bürgerlichen Gesetzbuch (BGB) sowie allen für Diensteanbieter geltenden
					Regulierungen (TMG - Telemediengesetz, NetzDG - Netzwerkdurchsetzungsgesetz, DSGVO - Datenschutz-Grundverordnung).
					Abweichende Rechtsordnungen sind unzulässig.
				</li>
			</ol>

			<h2 class="text-lg font-semibold">§2 Nutzungsrechte und Nutzungspflichten</h2>
			<ol class="list-decimal ml-6 space-y-2 mb-4 marker:text-base-content marker:opacity-65">
				<li class="text-base-content opacity-100">
					<strong>Gewährung der Nutzungslizenz:</strong> Der Anbieter gewährt dem Nutzer ein nicht ausschließliches,
					nicht abtrennbares und nicht unterlizenzierbare Recht zur Nutzung der Plattform für den Zweck
					der vertraglich vorgesehenen Funktionen. Dieses Recht erlischt mit Beendigung des Nutzungsverhältnisses
					oder bei Verstoß gegen diese AGB.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Gebote für die Nutzung:</strong> Der Nutzer verpflichtet sich, die Plattform ausschließlich
					zu den in diesen AGB vorgesehenen Zwecken und im Einklang mit sämtlichen einschlägigen Gesetzen,
					Verordnungen und behördlichen Anordnungen zu nutzen. Jede Nutzung, die dieser Bedingung zuwiderläuft,
					ist streng untersagt und kann zu strafrechtlichen und zivilrechtlichen Konsequenzen führen.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Verbote bei der Inhaltsbereitstellung:</strong> Der Nutzer ist untersagt, ohne Zustimmung
					des Anbieters Inhalte hochzuladen, zu generieren, zu verbreiten, öffentlich zugänglich zu machen
					oder anderweitig zu transmittieren, die folgenden Kriterien erfüllen oder entsprechen: (a) Inhalte,
					die gegen geltende Rechtsvorschriften verstoßen oder deren Verbreitung, Speicherung oder Verarbeitung
					rechtlich unzulässig ist; (b) Inhalte, die die Menschenwürde verletzen, beleidigend, diffamierend,
					ehrverletzend, belästigend oder mobbingähnlich wirken; (c) pornografische, sexuell freizügige
					oder kindesmissbrauchsmaterial-haltige Inhalte; (d) Inhalte, die gegen Urheberrechte, Markenrechte,
					Patente oder sonstige Immaterialgüterrechte verstoßen; (e) Malware, Viren, destruktive Programmcodes
					oder sonstige schädliche Inhalte; (f) Inhalte, die Manipulationen, Betrügereien oder Desinformation
					darstellen.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Durchsetzungs- und Sanktionsmechanismen:</strong> Der Anbieter behält sich das Recht
					vor, in eigenständigem Ermessen und ohne vorherige Ankündigung oder Grundangabe Inhalte zu modifizieren,
					zu filtern, zu sperren oder zu entfernen und den Zugang zur Plattform ganz oder teilweise zu
					unterbinden oder einzuschränken, sofern der Anbieter nach eigenständigem, nicht nachprüfbarem
					Ermessen feststellt oder vermutet, dass ein Verstoß gegen diese AGB oder gegen geltendes Recht
					vorliegt.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Steuersätze und Gebührenstrukturen:</strong> Der Anbieter behält sich das ausdrückliche
					Recht vor, Gebührenstrukturen, Transaktionssteuersätze, Provisionsmodelle und weitere finanzielle
					Parameter der Plattform jederzeit einseitig zu ändern und anzupassen. Solche Änderungen werden
					dem Nutzer durch Mitteilung auf der Überweisungs- oder Transaktionsseite mitgeteilt und gelten
					als akzeptiert, wenn der Nutzer eine Transaktion nach Bekanntgabe durchführt.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Datenverfälschung und Missbrauch:</strong> Der Nutzer erklärt sich einverstanden, dass
					jeder Versuch, Transaktionsdaten zu verfälschen, Datensätze zu manipulieren, Systeme zu kompromittieren
					oder Daten außerhalb der autorisierten Schnittstellen der Plattform zu bearbeiten oder zu verändern,
					Strafrechtliche Konsequenzen mitsich ziehen kann.
				</li>
			</ol>

			<h2 class="text-lg font-semibold">§3 Registrierung, Authentifizierung und Kontoverwaltung</h2>
			<ol class="list-decimal ml-6 space-y-2 mb-4 marker:text-base-content marker:opacity-65">
				<li class="text-base-content opacity-100">
					<strong>Registrierungsvoraussetzungen:</strong> Der Nutzer kann zur Inanspruchnahme spezieller
					Funktionalitäten und Leistungen verpflichtet sein, ein individuelles Nutzerkonto zu erstellen.
					Die Registrierung erfordert die Angabe wahrheitsgemäßer, vollständiger und aktueller personenbezogener
					Daten sowie technischer Authentifizierungsinformationen (Benutzername, Kennwort, Email-Adresse).
				</li>
				<li class="text-base-content opacity-100">
					<strong>Datengültigkeitspflicht:</strong> Der Nutzer verpflichtet sich, die bei der Registrierung
					angegebenen Informationen ständig zu überprüfen, zu aktualisieren und auf dem gegenwärtigen
					Stand zu halten. Dies gilt insbesondere für Email-Adressen, Kontaktinformationen und sonstige
					Identifikationsdaten. Der Nutzer trägt alleinig die Verantwortung für die Genauigkeit dieser
					Angaben.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Kontosicherheit und Authentifizierung:</strong> Der Nutzer ist allein verantwortlich
					für die Sicherung, Vertraulichkeit und den sachgemäßen Umgang mit seinen Authentifizierungsdaten
					(insbesondere Passwort, Benutzernamen, Zugangstoken und sonstige Sicherheitsschlüssel). Der
					Nutzer verpflichtet sich, sein Passwort geheim zu halten, dieses nicht an Dritte weiterzugeben
					und regelmäßig zu wechseln. Der Anbieter wird das Passwort des Nutzers nie anfragen oder erfragen.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Haftung für Kontoaktivitäten:</strong> Der Nutzer erklärt sich einverstanden, dass jegliche
					Aktivitäten, Transaktionen, Zugänge und Handlungen, die mit seinem Konto durchgeführt oder initiiert
					werden, ungeachtet der Frage, ob diese mit Wissen oder Zustimmung des Nutzers erfolgt sind,
					rechtlich und faktisch dem Nutzer zuzurechnen sind und dieser dafür vollständig haftbar gemacht
					werden kann.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Kontosperrung, Modifikation und Löschung:</strong> Der Anbieter behält sich das ausdrückliche
					Recht vor, ein Nutzerkonto ohne vorherige Ankündigung, ohne Angabe von Gründen und ohne Möglichkeit
					einer vorgerichtlichen Beschwerde zu sperren, alle damit verbundenen Daten (inklusive Passwort,
					Email-Adresse, Profilangaben und sonstige persönliche Informationen) zu modifizieren oder das
					Konto in Gänze und endgültig zu löschen, wenn der Anbieter nach eigenständigem Ermessen feststellt
					oder vermutet, dass diese AGB verletzt worden sind, wenn Sicherheitsrisiken festgestellt werden,
					oder aus anderen, dem Nutzer möglicherweise nicht mitgeteilten Gründen.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Speicherung und Archivierung:</strong> Der Nutzer erkennt an, dass mit der Kontoschließung
					nicht automatisch alle damit verbundenen Daten gelöscht werden. Der Anbieter behält sich das
					Recht vor, Daten für Compliance-, Sicherheits- und Archivierungszwecke längerfristig zu speichern
					und aufzubewahren.
				</li>
			</ol>

			<h2 class="text-lg font-semibold">
				§4 Verarbeitung personenbezogener Daten Minderjähriger und besondere Schutzbestimmungen
			</h2>
			<ol class="list-decimal ml-6 space-y-2 mb-4 marker:text-base-content marker:opacity-65">
				<li class="text-base-content opacity-100">
					<strong>Altersbestimmung und Zugangsvoraussetzungen:</strong> Die Nutzung der Plattform durch
					Minderjährige ist zulässig. Allerdings ist die Nutzung durch Kinder und Jugendliche unter dem
					14. Lebensjahr ausschließlich mit vorheriger schriftlicher oder elektronischer Zustimmung der
					sorgeberechtigten Eltern oder Erziehungsberechtigten zulässig. Der Nutzer versichert, dass er
					entweder das 14. Lebensjahr vollendet hat oder dass eine solche Zustimmung vorliegt.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Datenminimierungsprinzip:</strong> Der Anbieter verpflichtet sich, bei der Verarbeitung
					personenbezogener Daten von Minderjährigen das Datenminimierungsprinzip nach Art. 5 Abs. 1 DSGVO
					zu beachten. Dies bedeutet, dass ausschließlich solche personenbezogenen Daten erhoben werden,
					die für den Betrieb und die technische Funktionsfähigkeit der Plattform zwingend notwendig sind.
					Dies umfasst insbesondere: IP-Adressen, eindeutige Nutzeridentifikatoren, Login-Daten und Sessions-Informationen.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Weitergabeverbot:</strong> Der Anbieter erklärt, dass erhobene Daten von Nutzern (einschließlich
					minderjähriger Nutzer) nicht an unabhängige Dritte, kommerzielle Partner, Marketingunternehmen
					oder andere externe Datenverarbeiter weitergegeben, verkauft oder lizenziert werden. Eine Ausnahme
					besteht nur, sofern dies gesetzlich vorgeschrieben ist oder zur Erfüllung dieser Bedingungen
					erforderlich ist.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Verwendungszweck und Zweckbindung:</strong> Die erhobenen personenbezogenen Daten werden
					ausschließlich zur Bereitstellung, Optimierung und technischen Absicherung des Dienstes verwendet.
					Eine Verwendung zu Profiling-, Profilierungszwecken oder zu automatisierten Entscheidungsfindungsprozessen
					ist unzulässig.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Verlinkung zur Datenschutzerklärung:</strong> Weitere ausführliche Informationen
					zur Verarbeitung personenbezogener Daten, insbesondere in Bezug auf Rechtsgrundlagen,
					Speicherdauer, Nutzerrechte und Sicherheitsmaßnahmen, sind in unserer separaten und
					verbindlichen
					<a href="/policies/privacy" class="link link-primary">Datenschutzerklärung</a> aufgeführt. Diese
					Datenschutzerklärung stellt einen integralen Bestandteil dieser AGB dar.
				</li>
			</ol>

			<h2 class="text-lg font-semibold">
				§5 Datenschutz, Datenverarbeitung und Konformität mit DSGVO
			</h2>
			<ol class="list-decimal ml-6 space-y-2 mb-4 marker:text-base-content marker:opacity-65">
				<li class="text-base-content opacity-100">
					<strong>Datenschutzerklärung:</strong> Der Schutz und die Vertraulichkeit
					personenbezogener Daten sind dem Anbieter von besonderem Belange. Die
					Datenschutzpraktiken, Verarbeitungsverfahren und Sicherheitsmaßnahmen werden ausführlich
					und rechtsgültig in der separaten
					<a href="/policies/privacy" class="link link-primary">Datenschutzerklärung</a> dargelegt. Diese
					Datenschutzerklärung wird durch diese AGB vollständig inkorporiert und bildet einen verbindlichen
					Bestandteil des Nutzungsvertrages.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Einwilligung zur Datenverarbeitung:</strong> Durch die fortgesetzte Nutzung und den
					Zugriff auf die Plattform erklärt sich der Nutzer ausdrücklich und uneingeschränkt damit einverstanden,
					dass seine personenbezogenen Daten gemäß der geltenden Datenschutzerklärung und nach Maßgabe
					der Regelungen dieser AGB erhoben, verarbeitet, gespeichert und möglicherweise weitergegeben
					werden können. Dies geschieht unter Beachtung der datenschutzrechtlichen Vorgaben der Europäischen
					Datenschutz-Grundverordnung (DSGVO), des deutschen Bundesdatenschutzgesetzes (BDSG) und aller
					sonstigen einschlägigen Datenschutzgesetze.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Rechtsgrundlagen:</strong> Die Datenverarbeitung stützt sich auf mindestens eine der
					folgenden Rechtsgrundlagen: (a) Erforderlichkeit zur Vertragserfüllung (Art. 6 Abs. 1 lit. b
					DSGVO); (b) Erforderlichkeit zur Erfüllung einer gesetzlichen Verpflichtung (Art. 6 Abs. 1 lit.
					c DSGVO); (c) Schutz berechtigter Interessen (Art. 6 Abs. 1 lit. f DSGVO); (d) Einwilligung
					des Nutzers (Art. 6 Abs. 1 lit. a DSGVO), sofern separat erhoben.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Speicherdauer:</strong> Personenbezogene Daten werden für den zur Vertragsdurchführung
					erforderlichen Zeitraum gespeichert. Nach Vertragsbeendigung oder bei Datenlöschanforderungen
					werden Daten gelöscht, sofern keine gesetzlichen Aufbewahrungspflichten (insbesondere steuerrechtliche
					oder handelsrechtliche Fristen nach HGB oder AStV) entgegenstehen.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Nutzerrechte:</strong> Der Nutzer kann die ihm zustehenden Rechte nach Maßgabe der
					DSGVO geltend machen, insbesondere Auskunftsrecht (Art. 15), Berichtigungsrecht (Art. 16),
					Löschungsrecht (Art. 17), Einschränkungsrecht (Art. 18) und Datenportabilität (Art. 20).
					Anfragen sind schriftlich an
					<a href="mailto:{contactEmail}" class="link link-primary">{contactEmail}</a> zu richten.
				</li>
			</ol>

			<h2 class="text-lg font-semibold">§6 Haftungsbeschränkung und Gewährleistungsausschlüsse</h2>
			<ol class="list-decimal ml-6 space-y-2 mb-4 marker:text-base-content marker:opacity-65">
				<li class="text-base-content opacity-100">
					<strong>Risikoübernahme und Haftungsausschluss allgemein:</strong> Die Nutzung der Plattform
					und aller damit verbundenen Dienstleistungen erfolgt vollständig auf eigenes Risiko des Nutzers.
					Der Anbieter übernimmt keine Haftung für Schäden aller Art (materiell, immateriell, Folgeschäden,
					Vermögensschäden, Gewinne oder Geschäftsmöglichkeiten), die durch die Nutzung oder Nichtnutzung
					der Plattform entstehen, gleich ob direkt oder indirekt, es sei denn, die Haftung folgt zwingend
					aus Gesetz oder diese Schäden beruhen ausschließlich auf vorsätzlichem oder grob fahrlässigem
					Verhalten des Anbieters oder seiner Organe.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Verfügbarkeit und Funktionalität:</strong> Der Anbieter gewährleistet nicht, dass die
					Plattform zu allen Zeiten verfügbar ist, ohne Unterbrechungen läuft oder fehlerfrei funktioniert.
					Die Plattform wird „as-is" und „as-available" bereitgestellt. Der Anbieter ist nicht verpflichtet,
					Wartungsarbeiten voranzukündigen oder Ausfallzeiten zu minimieren. Der Nutzer trägt das Risiko
					unerwarteter Unterbrechungen, technischer Fehler und Systemausfälle.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Sicherheitsmaßnahmen und Datensicherheit:</strong> Der Anbieter setzt angemessene technische
					und organisatorische Sicherheitsmaßnahmen ein, um die Integrität, Vertraulichkeit und Verfügbarkeit
					der gespeicherten Daten zu gewährleisten. Allerdings kann keine absolute oder hundertprozentige
					Sicherheit garantiert werden. Der Anbieter kann insbesondere nicht garantieren oder haften dafür,
					dass die Plattform oder die darin gespeicherten Kontoinformationen vollständig und dauerhaft
					gegen unbefugten Zugriff, Datenverlust, Datenverfälschung, Cyber-Attacken, Hackerangriffe, Malware-Infektionen
					oder sonstige Sicherheitsverletzungen Dritter geschützt sind. Eine Haftung für durch unbefugten
					Zugriff Dritter entstehende Schäden ist ausschließlich auf die gesetzlich zwingend vorgegebenen
					Haftungsgrenzen des BGB beschränkt und ist andernfalls vollständig ausgeschlossen.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Inhalte Dritter und externe Links:</strong> Der Anbieter ist nicht verantwortlich, haftet
					nicht und trägt keine Haftung für Inhalte, die von Dritten stammen oder über die Plattform (einschließlich
					Links, Referenzen, Hyperlinks zu externen Webseiten, Diensten, Anwendungen oder Plattformen)
					zugänglich sind. Dies umfasst insbesondere: (a) jegliche Inhalte, die von anderen Nutzern hochgeladen,
					bereitgestellt oder generiert worden sind; (b) vollständigkeit, Richtigkeit, Genauigkeit, Zuverlässigkeit,
					Aktualität oder Rechtmäßigkeit von nutzergenerierten Inhalten; (c) Datenschutzpraktiken, Nutzungsbedingungen
					oder Sicherheitsstandards externer Webseiten und Dienste. Der Nutzer allein trägt die volle
					Verantwortung für Inhalte, die er über die Plattform bereitstellt oder hochlädt.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Ausschluss der Haftung für leichte Fahrlässigkeit:</strong> Der Anbieter haftet nicht
					für Schäden, die durch leicht fahrlässiges Verhalten verursacht werden, sofern es sich nicht
					um Verletzungen wesentlicher Vertragspflichten (Kardinalpflichten im Sinne des BGB) handelt.
					Dies gilt auch für leicht fahrlässige Pflichtverletzungen bei der Datenverarbeitung, sofern
					diese keine Grundrechtsverletzungen oder schwerwiegenden Datenschutzverstöße darstellen.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Ausnahme: Schadensersatz für Körper, Gesundheit und Leben:</strong> Die vorstehenden
					Haftungsbeschränkungen und Ausschlüsse finden keine Anwendung auf Schäden, die durch Verletzung
					des Lebens, des Körpers oder der Gesundheit entstehen. Diese fallen unter die gesetzliche Haftung
					nach BGB und können vom Anbieter nicht ausgeschlossen werden.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Haftung von Erfüllungsgehilfen:</strong> Soweit die Haftung des Anbieters ausgeschlossen
					oder beschränkt ist, gilt diese Ausnahme auch für die persönliche, strafrechtliche und zivilrechtliche
					Haftung seiner Angestellten, Arbeitnehmer, Vertreter, Bevollmächtigten, Erfüllungsgehilfen und
					sonstigen Zugehörigen.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Meldeverfahren für unangemessene Inhalte:</strong> Der Nutzer wird hiermit in
					Kenntnis gesetzt, dass der Anbieter ein Hilfsmittel (Reporting-Funktion) zur Verfügung
					stellt, mit dessen Hilfe der Nutzer unangemessene, rechtswidrige, verletzende oder
					sonstige gegen diese AGB verstoßende Inhalte dem Anbieter mitteilen und zur Löschung
					anmelden kann. Zusätzlich steht dem Nutzer jederzeit das Recht zu, eine E-Mail mit
					detaillierten Informationen an
					<a href="mailto:{contactEmail}" class="link link-primary">{contactEmail}</a> zu versenden. Der
					Anbieter wird solche Meldungen überprüfen und im Falle einer Bestätigung des Verstoßes entsprechend
					handeln.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Ausschluss der Stellvertretung:</strong> Jede Partei verzichtet unwiderruflich auf das
					Recht, Ansprüche oder Positionen namens, im Namen oder für Rechnung Dritter, von Dritten oder
					im Auftrag von Dritten geltend zu machen. Dies umfasst insbesondere den Verzicht auf das Recht,
					als Vertreter, Bevollmächtigter oder Anwalt anderer Nutzer oder Dritter aufzutreten.
				</li>
			</ol>

			<h2 class="text-lg font-semibold">§7 Änderung, Anpassung und Fortbestand der AGB</h2>
			<ol class="list-decimal ml-6 space-y-2 mb-4 marker:text-base-content marker:opacity-65">
				<li class="text-base-content opacity-100">
					<strong>Änderungsvorbehalt:</strong> Der Anbieter behält sich das ausdrückliche und uneingeschränkte
					Recht vor, diese Allgemeinen Geschäftsbedingungen jederzeit, ohne Angabe von Gründen und ohne
					Notwendigkeit einer Ankündigung oder Fristsetzung zu ändern, anzupassen, zu ergänzen oder zu
					überarbeiten. Dies ist ungeachtet möglicher Auswirkungen auf bereits bestehende Nutzungsverträge
					zulässig.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Veröffentlichung von Änderungen:</strong> Änderungen werden auf der Plattform und insbesondere
					im Bereich „Allgemeine Geschäftsbedingungen" oder an ähnlich prominenter Stelle veröffentlicht
					und werden hierdurch den Nutzern bekannt gemacht. Der Anbieter wird die Nutzer ferner per Email
					(sofern eine gültige Email-Adresse verfügbar ist) oder durch Benachrichtigungen auf der Plattform
					selbst über wesentliche Änderungen informieren.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Inkrafttreten und Zustimmung durch Fortbestand:</strong> Geänderte Bedingungen treten
					sofort nach Veröffentlichung in Kraft und Geltung. Der Nutzer wird aufgefordert, die aktualisierten
					Bedingungen regelmäßig zu überprüfen. Mit der fortgesetzten, wiederholten oder andauernden Nutzung
					der Plattform nach Bekanntgabe, Veröffentlichung oder Mitteilung von Änderungen erklärt sich
					der Nutzer uneingeschränkt und unwiderruflich mit den neuen und geänderten Bedingungen einverstanden.
					Ein explizites Akzeptanzverfahren ist nicht erforderlich.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Bestandteile dieser AGB:</strong> Diese AGB, einschließlich aller Anlage, Unterbestimmungen,
					Referenzen (insbesondere die Datenschutzerklärung, Impressum und sonstige Policies) bilden zusammen
					die gesamte, vollständige und bindende Vereinbarung zwischen dem Anbieter und dem Nutzer in
					Bezug auf die Nutzung der Plattform.
				</li>
			</ol>

			<h2 class="text-lg font-semibold">§8 Verwaltung und Kontrolle digitaler Assets</h2>
			<ol class="list-decimal ml-6 space-y-2 mb-4 marker:text-base-content marker:opacity-65">
				<li class="text-base-content opacity-100">
					<strong>Administrationsvorbehalt:</strong> Der Anbieter behält sich umfassend das Recht vor,
					alle Nutzerdaten zu speichern, zu ändern oder zu löschen ohne zeitliche Begrenzung. Die Speicherung
					unterliegt weiterhin dem Datenschutz.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Digitale Assets und virtuelle Vermögenswerte:</strong> Sämtliche virtuellen Währungen,
					Token, Punkte und digitale Assets verbleiben vollständig im Eigentum und unter der Kontrolle
					des Anbieters. Der Nutzer erwirbt nur ein zeitlich begrenztes, nicht übertragbares Nutzungsrecht.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Definition Assets:</strong> Assets sind alle in der Plattform generierten digitalen
					Vermögenswerte, Daten und immateriellen Gegenstände unabhängig ihrer Bezeichnung oder Form.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Einseitiges Gestaltungsrecht:</strong> Der Anbieter behält das ausdrückliche Recht vor,
					Assets nach eigenständigem Ermessen jederzeit zu generieren, zu modifizieren oder zu entfernen,
					unabhängig von deren Einfluss auf andere Nutzer oder deren Wert.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Wertbeeinflussung und Risikoakzeptanz:</strong> Mit der Plattformnutzung erklärt sich
					der Nutzer einverstanden, dass administrative Änderungen den Wert und die Funktionalität der
					Assets erheblich beeinflussen können. Der Nutzer trägt das vollständige Risiko solcher Wertveränderungen.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Kein Anspruch auf Kompensation:</strong> Der Nutzer hat keinen Anspruch auf Schadensersatz
					oder Kompensation bei Wertveränderungen von Assets.
				</li>
			</ol>

			<h2 class="text-lg font-semibold">
				§9 Gewährleistungsausschluss und Verfügbarkeit der Inhalte
			</h2>
			<ol class="list-decimal ml-6 space-y-2 mb-4 marker:text-base-content marker:opacity-65">
				<li class="text-base-content opacity-100">
					<strong>Bereitstellung as-is und as-available:</strong> Der Anbieter stellt die Inhalte in der
					aktuellen Form zur Verfügung und übernimmt keine Gewähr für Vollständigkeit, Richtigkeit oder
					Verfügbarkeit der Inhalte und Services.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Einseitiges Änderungsrecht:</strong> Der Anbieter behält sich das Recht vor, Inhalte,
					Funktionen und Services jederzeit ohne Ankündigung zu ändern, zu modifizieren oder zu entfernen.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Technische Fehler und Ausfallrisiken:</strong> Der Nutzer trägt das Risiko für technische
					Fehler, Systemausfälle und Netzwerkstörungen, einschließlich HTTP-Fehler wie NGINX 502 - Bad
					Gateway.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Keine Verfügbarkeitsgarantie:</strong> Der Anbieter garantiert nicht, dass die Plattform
					jederzeit verfügbar oder fehlerfrei funktioniert. Wartungen und Updates können jederzeit durchgeführt
					werden.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Haftungsausschluss bei Inhaltsänderungen:</strong> Der Anbieter haftet nicht für Schäden
					durch Änderungen, Ausfälle oder Fehler der Plattform und ihrer Inhalte.
				</li>
			</ol>

			<h2 class="text-lg font-semibold">
				§10 Salvatorische Klausel, Gerichtstand und Schlussbestimmungen
			</h2>
			<ol class="list-decimal ml-6 space-y-2 mb-4 marker:text-base-content marker:opacity-65">
				<li class="text-base-content opacity-100">
					<strong>Salvatorische Klausel:</strong> Sollte eine Bestimmung dieser AGB ganz oder teilweise
					unwirksam sein, bleibt die Wirksamkeit der übrigen Bestimmungen unberührt. Anstelle der unwirksamen
					Bestimmung gilt eine dem wirtschaftlichen Zweck am nächsten kommende rechtlich zulässige Regelung.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Gerichtsstand und Erfüllungsort:</strong> Alle Streitigkeiten werden nach deutschem
					Recht entschieden. Ausschließlich zuständig sind die Gerichte der Bundesrepublik Deutschland.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Gesamte Vereinbarung:</strong> Diese AGB, einschließlich der Datenschutzerklärung und
					aller Policies, bilden die gesamte, vollständige und verbindliche Vereinbarung und ersetzen
					alle vorherigen mündlichen oder schriftlichen Absprachen.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Inkorporation von Dokumenten:</strong> Alle in dieser AGB durch Verweis aufgeführten
					Dokumente werden vollständig inkorporiert und bilden einen verbindlichen Bestandteil dieser
					Vereinbarung.
				</li>
				<li class="text-base-content opacity-100">
					<strong>Kontakt:</strong> Bei Fragen, Beschwerden oder Rechtsverletzungen:
					<a href="mailto:{contactEmail}" class="link link-primary">{contactEmail}</a>
				</li>
			</ol>

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

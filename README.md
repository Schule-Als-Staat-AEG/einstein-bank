# Digitales Bank System
<p>Programmiert für Schule als Staat bei dem AEG in Ulm</p>
<p>Frontend Tech Stack (STD):</p>

- [S]velte Kit
- [T]ailwind CSS
- [D]aisyUI

<p>Backend Tech Stack</p>

- ASP.NET
- PocketBase

---
<h4>Achtung: Dies ist eine Datenschutz-bereinigte Version des Projektes. Es befindet sich in einem nicht-voll-funktionsfähigen Stand.</h4>

<h2>Projekt Bauen</h2>
<p>Das Projekt benötigt Dotnet 10.0 sowie NodeJS 22.0 oder 24.0</p>

```bash
# Server + Backup System bauen
dotnet publish --os linux --sc true
```

```bash
# Webpage bauen
cd frontend/
npm run build
```

<p>Bei download müssen die Packete für die Webpage installiert werden:</p>
<ul>
  <li>NodeJS 24.0</li>
  <li>npm (kommt mit NodeJS)</li>
</ul>

```bash
# Webpage Packete installieren
cd frontend/
npm install
```

<p>Der Server benötigt Einstellungen aus der Settings Collection. Ein Beispiel befindet sich in settings_records_to_insert.json</p>

<p>Das bauen des Projektes ist für Linux ausgelegt. Es befinden sich aber Helper Scripte für den Upload in der Repository</p>
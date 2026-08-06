# Assets und Lizenzen

Das Repository enthält **keine kommerziellen Assets**. Die Szene läuft als Blockout aus
Unity-Primitiven und ist nach dem Klonen vollständig lauffähig.

## Verwendete 3rd-Party-Pakete (nicht im Repository)

| Paket | Verwendung | Quelle / Lizenz |
|---|---|---|
| POLYBOX „Hazelwood Loft" (City & Terraces) | Ferne Stadt-Kulisse | Unity Asset Store, kommerzielle EULA |
| City Builder Urban | Ergänzende Stadtgeometrie | Unity Asset Store, kommerzielle EULA |
| Materialien unter `Mats/` | Marmor, Stoff, Metall, Holz | Sammlung, Lizenz je Quelle |

**Warum nicht im Repository:** Die Unity-Asset-Store-EULA untersagt die Weiterverbreitung in
extrahierbarer Form, und die Pakete überschreiten die GitHub-Größenlimits deutlich.
`Assets/3rd Party Assets/` steht deshalb in `.gitignore`.

**Nachimportieren:** Package Manager → My Assets, unter eigener Lizenz. Es sind keine
UPM-Pakete, sie stehen daher nicht in `Packages/manifest.json` und werden beim Klonen nicht
automatisch aufgelöst.

**Hinweis zur Render-Pipeline:** Das POLYBOX-Paket ist für die Built-in-Pipeline gebaut. Unter
URP rendern seine Materialien magenta, bis sie über *Window → Rendering → Render Pipeline
Converter → „Built-in to URP"* konvertiert werden.

## Polierte Fassung erzeugen

Die Stadt-Kulisse ist **generiert, nicht in der Szene gespeichert** — sonst enthielte die
committete Szene Referenzen auf Dateien, die niemand beim Klonen bekommt.

- `Exposure → Polish → Build City Backdrop` — vor dem polierten Build
- `Exposure → Polish → Clear City Backdrop` — vor dem Blockout-Build **und vor jedem Commit**

# Assets und Lizenzen

Das Repository enthält **keine kommerziellen Assets**. Die Szene läuft als Blockout aus
Unity-Primitiven und ist nach dem Klonen vollständig lauffähig.

## Sounds (im Repository, frei lizenziert)

Unter `Assets/_Exposure/Audio/`, gekürzt und für Standalone-VR nach Ogg Vorbis konvertiert.

| Datei | Quelle | Lizenz |
|---|---|---|
| `ElevatorRide.ogg` | *Elevator Ride Interior* von Filmscore — [freesound.org/s/825478](https://freesound.org/s/825478/) | CC0 |
| `CityAmbience.ogg` | *CityPark AtmosSpring NL Havensingel* von klankbeeld — [freesound.org/s/231839](https://freesound.org/s/231839/) | CC BY 4.0 |
| `CarIdle.ogg` | *Car Engine, VW Golf GLE 1.8, idle, interior* von JoniHeinonen — [freesound.org/s/236903](https://freesound.org/s/236903/) | CC BY 3.0 |

Die beiden CC-BY-Titel erfordern Namensnennung; die Tabelle oben erfüllt das.

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

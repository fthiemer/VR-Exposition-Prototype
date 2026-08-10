# VR-Expositionsprototyp — Höhenangst

Prototyp für App-geführte Höhenangst-Exposition für Meta Quest, die den Fortschritt an der
Erwartungs**änderung** ausrichtet statt am Abklingen der Angst. Die Patient:in arbeitet
selbstgesteuert in gestuften Höhensituationen; die Therapeut:in verordnet und bespricht die
dokumentierten Ergebnisse. Blick in die App im 6 minütigen [Playthrough auf Youtube](https://youtu.be/qVgzjDLyt3I).

Unity 6000.5.7f1 · URP · OpenXR · natives Handtracking
Gebaut für Meta Quest 2/3/3S, **getestet ausschließlich auf Quest 2**.

---
<img width="720" height="720" alt="grafik" src="https://github.com/user-attachments/assets/e76a4236-6cbb-4b4a-a26c-14ba55562a2a" />


## KOL-Auftrag (simuliert)

**Ziel.** Graduelle Höhenexposition in-virtuo. Jede Sitzung ist ein **Verhaltensexperiment**: Befürchtung vorher benennen, prüfen, hinterher auswerten.

**Selbstgesteuert im geführten Rahmen.** Die App liefert Struktur, Anleitung und
Dokumentation — welche Befürchtung geprüft wird, auf welcher Etage, mit welcher Aufgabe und
wie lange, entscheidet die Patient:in. Keine Sperre, keine Empfehlung aus den Messwerten.
Die App ersetzt nicht die Behandlung.

**Wirkmodell.** Inhibitorisches Lernen: Die Angstassoziation wird nicht gelöscht, sondern
bekommt über die Erwartungsverletzung eine konkurrierende Sicherheitsassoziation
([Craske et al. 2014](https://doi.org/10.1016/j.brat.2014.04.006)). Aufbau und
Sitzungsstruktur folgen der automatisierten VR-Höhentherapie von
[Freeman et al. 2018](https://doi.org/10.1016/S2215-0366(18)30226-8) (RCT, d = 2,0).

### Stellschrauben (im Prototyp)

| Parameter | Stufen |
|---|---|
| Höhe | Etage 0 … 10 |
| Randschutz | Geländer → Glasbrüstung → offene Kante |
| Untergrund | fest → Gitterrost → Glasboden → Steg |
| Aufgabe | stehen → an Rand treten → hinabblicken → Steg queren |
| Wind | aus … stark |

### Rahmenbedingungen

Abbruch jederzeit ohne Begründung, harte Grenze bei Herzfrequenz 200 · Start ebenerdig, Selbstwahl bei Stufenwechsel, die Aufzugfahrt wird real durchlebt · Bedienung per Handtracking und Controller möglich.

---

## Messung

Dreiteilig, **einmal pro Sitzung am Boden** — nie in der Höhe, nach
[Freeman et al. 2018](https://doi.org/10.1016/S2215-0366(18)30226-8), Skala 0–10:
**E₁** erwartete Wahrscheinlichkeit vorher · **O** wie stark es eintrat ·
**E₂** erwartete Wahrscheinlichkeit fürs nächste Mal.
Daraus **Erwartungsänderung** = E₁ − E₂ und **Lernrate** = (E₁ − E₂) / (E₁ − O), beides ins
CSV-Protokoll.

Fortschritt hängt nicht an der Erwartungsverletzung:
[Pittig et al. 2023](https://doi.org/10.1177/21677026221101379) fanden, dass *„not expectancy
violation itself, but higher learning rate and expectancy change predicted better treatment
outcome"*. Etagen werden nach erfüllter Aufgabe dauerhaft freigeschaltet und nie entzogen.

## Architektur

**Generischer Kern, zwei Ableitungen.** `ExposureSessionController<TState>` ist abstrakt und
generisch über den Umgebungszustand; Höhenangst (`HeightState`) und Klaustrophobie
(`RoomState`) sind dünne Schließungen desselben Ablaufs. Zwei statt einer sind Absicht: eine
generische Struktur, die nur einmal verwendet wurde, ist nicht als generisch nachgewiesen.

**Inhalte als Daten.** Stufen, Parameter und Befürchtungstexte liegen in ScriptableObjects
aus Editor-Generatoren. Eine Stufe hinzuzufügen ist Dateneingabe. 

**Abhängigkeiten über Interfaces.** `IPredictionPrompt`, `ITaskCompletionSource`,
`IEnvironmentController<TState>`, `IBiosignalSource`, `ISessionLogger`. Ein echter
Herzfrequenzsensor ersetzt eine Klasse, nicht den Ablauf.

**Erkennung geometrisch.** Abstand zur Kantenlinie, Blickwinkel, Verweildauer — funktioniert
mit bloßem Handtracking. Die Bedingung muss *gehalten* werden; ein Streifen an der Kante
zählt nicht als Exposition.

```
Assets/_Exposure/
├── Scripts/    Data · Runtime · Environment · Feedback · IO
├── Editor/     Szenario-Generatoren, Szenen-Setup
├── Scenarios/  generierte ScriptableObjects
├── Content/    Coach-Texte (CSV)
├── Audio/      Umgebungston, Abschlussfanfare
└── Scenes/     Exposure_Acrophobia.unity
```

## Mögliche Erweiterungen
- **Speicherstände** — Sitzungen fortsetzen und die Erwartungsänderung über mehrere Sitzungen
  als Verlauf zeigen. Derzeit setzt sich der Status zwischen den Sitzungen zurück.
- **Aufgabenpool je Etage füllen** — `TaskVariant<TState>` trägt beliebig viele Aufgaben pro
  Stufe, hinterlegt ist bislang eine.
- **Sprachausgabe statt Panels** — bei Freeman per Spracherkennung oder virtueller Armbanduhr.
  Die Coach-Texte liegen vor, die Audio-Schicht fehlt.
- **Feinstufige Progression** — pro Schritt eine Stellschraube, auf gleicher Höhe variieren
  statt nur zu überbieten.
- **Therapeutenzugang** — Stufen vorgeben, eingreifen, Kennzahlen einsehen. CSV liegt vor,
  eine Oberfläche nicht.
- **Sicherheitsverhalten adressieren** — Interaktiv durch Coaching Stimme s. [Freeman et al. 2018](https://doi.org/10.1016/S2215-0366(18)30226-8). Begründung: Festhalten, Wegschauen, Abstandhalten schwächen den
  Effekt ([Blakey & Abramowitz 2019](https://doi.org/10.1016/j.cbpra.2018.03.001)).

## Assets

Keine kommerziellen Assets im Repository; nach dem Klonen als Blockout lauffähig.
Umgebungsklänge von freesound.org (CC0 / CC BY, mit Namensnennung), Abschlussfanfare selbst
erzeugt. Details: [ASSETS.md](ASSETS.md).

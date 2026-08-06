# VR-Expositionsprototyp — Höhenangst

App-geführte Höhenangst-Exposition für Meta Quest, die den Fortschritt an der
Erwartungsverletzung ausrichtet statt am Abklingen der Angst. Die Patient:in arbeitet
eigenständig in gestuften Höhensituationen; die Therapeut:in verordnet, legt die Stufen
fest und bespricht die dokumentierten Ergebnisse.

Unity 6000.5.7f1 · URP · OpenXR · Meta Quest 2/3/3S · natives Handtracking

---

## KOL-Auftrag

*Fachliche Vorgabe, mitgeschrieben im Abstimmungsgespräch — Grundlage für die Umsetzung.*

### Was das Modul leisten soll

- Höhenexposition durchführbar machen, ohne Hochhaus, Brücke oder Auswärtstermin.
- Die Sitzung läuft geführt ab — die App liefert Situation, Anleitung und Dokumentation,
  ersetzt aber nicht die Behandlung.
- Jede Übung ist ein **Verhaltensexperiment**: Befürchtung vorher benennen, prüfen,
  hinterher auswerten.

### Fachlicher Hintergrund

Wirkmodell ist das inhibitorische Lernen: die Angstassoziation wird nicht gelöscht, sondern
bekommt eine konkurrierende Sicherheitsassoziation
([Craske et al. 2014](https://doi.org/10.1016/j.brat.2014.04.006)). Wirksam ist die
**Erwartungsverletzung** — die Lücke zwischen Befürchtung und tatsächlichem Ausgang
([Hamlett et al. 2023](https://doi.org/10.1007/7854_2022_385)).

**Habituation und inhibitorisches Lernen schließen sich nicht aus.** Habituation findet statt
und bleibt klinisch aussagekräftig; strittig ist nur, ob sie eine *notwendige* Bedingung für
den Langzeiterfolg ist. Deshalb: beides dokumentieren, aber nur die Erwartungsverletzung als
Fortschrittskriterium verwenden. Ein Habituations-Gate würde die Übung genau dann beenden,
wenn keine Erwartung mehr besteht, die sich verletzen ließe — also wenn kein Lernen mehr
möglich ist.

Sicherheitssignale und subtile Vermeidung — Festhalten, Wegschauen, Abstandhalten —
schwächen den Effekt und gehören angesprochen
([Blakey & Abramowitz 2019](https://doi.org/10.1016/j.cbpra.2018.03.001)).

*Einordnung:* Die Belege zu einzelnen Optimierungsstrategien sind vorläufig — im Review von
[Plaisted et al. 2021](https://doi.org/10.1007/s10567-021-00347-3) wurde kein Befund
repliziert. Als Designleitlinie brauchbar, nicht als feste Regel.

### Stellschrauben

| Parameter | Stufen | Wirkung |
|---|---|---|
| Höhe | Etage 0 … 10 | Grundintensität |
| Randschutz | Geländer → Glasbrüstung → offene Kante | Sicherheitssignal |
| Untergrund | fest → Gitterrost → Glasboden → Steg | Tiefenwirkung |
| Sicherheitsnetz | sichtbar / entfernt | Sicherheitssignal |
| Aufgabe | stehen → an Rand treten → hinabblicken → Steg queren | Annäherung |
| Wind | aus … stark | zusätzlicher Reiz |

Pro Schritt nur **eine** Stellschraube verändern. Nicht stur höher: auf gleicher Höhe
variieren, Bewältigtes neu kombinieren (Glasboden *und* keine Brüstung). Die Therapeut:in
muss jederzeit eingreifen und die Stufe anpassen können.

### Ablauf einer Sitzung

Gemessen wird **einmal pro Sitzung, am Boden** — nicht pro Aufgabe und nie in der Höhe.

| Phase | Ort | Was passiert |
|---|---|---|
| 1. Einführung | Boden | Prinzip erklären, Befürchtung wählen, **E₁** erheben |
| 2. Etagenauswahl | Boden | Patient:in wählt aus den freigeschalteten Etagen |
| 3. Aufgabe | Höhe | Ausführen, keine Abfrage. Danach Coach-Rückfrage: *„Fühlst du dich sicherer als vorhin?"* |
| 4. Entscheidung | Höhe | Wiederholen · andere Aufgabe · eine Etage höher · Sitzung beenden — wiederholt sich bis zum Ende |
| 5. Abschluss | zurück am Boden | **O** und **E₂** erheben, Fortschritt zeigen |

**Die drei Messwerte** (0–10, nach
[Freeman et al. 2018](https://doi.org/10.1016/S2215-0366(18)30226-8)):
**E₁** erwartete Wahrscheinlichkeit vorher · **O** wie stark es tatsächlich eintrat ·
**E₂** erwartete Wahrscheinlichkeit fürs nächste Mal.

Daraus **Erwartungsänderung** = E₁ − E₂ und **Lernrate** = (E₁ − E₂) / (E₁ − O).

**Warum nicht die Erwartungsverletzung als Fortschrittskriterium?**
[Pittig et al. 2023](https://doi.org/10.1177/21677026221101379) fanden im Volltext:
*„Not expectancy violation itself, but higher learning rate and expectancy change predicted
better treatment outcome."* Die Überraschung allein sagt den Erfolg also **nicht** vorher —
entscheidend ist, ob sich die Erwartung für die Zukunft bewegt. Deshalb schaltet der Prototyp
nicht mehr auf die Verletzung frei.

### Progression

Zwei getrennte Achsen: **Etagen** werden dauerhaft freigeschaltet (jeweils genau eine über der
erreichten, sobald dort eine Aufgabe erfüllt wurde) und nie wieder entzogen. **Aufgaben** liegen
als Pool je Etage vor, leichteste zuerst — „auf der Etage bleiben" heißt eine *andere* Aufgabe,
nicht dieselbe nochmal.

Es gibt **keine Sperre und keine datenbasierte Empfehlung** nach einer Aufgabe: nur die
qualitative Coach-Frage, dann freie Wahl. Das entspricht Freemans Vorgehen; E₁/O/E₂ und Lernrate
sind Sitzungskennzahlen für die Therapeut:in, entkoppelt von der Aufgabe-zu-Aufgabe-Entscheidung.

### Rahmenbedingungen

- Abbruch jederzeit ohne Begründung; harte Grenze bei Herzfrequenz 200.
- Start ebenerdig, kein automatischer Stufenwechsel; die Aufzugfahrt wird immer real durchlebt.
- **Null Datenabfragen in der Höhe** — Dauerabfragen zerstören das Erleben.
- Bedienung per Handtracking, ohne Controller.
- Dokumentiert werden Befürchtung, E₁/O/E₂, Erwartungsänderung, Lernrate und Abstand zur Kante.
- Sicherheitsverhalten wird in der Einführung *erklärt*, nicht algorithmisch bewertet — so
  handhabt es auch Freeman.
- Vor und nach der Behandlung:
  [Acrophobia Questionnaire](https://doi.org/10.1016/S0005-7894(77)80116-0), STAI-State.

---

## Projektaufbau

```
Assets/_Exposure/
├── Scripts/
│   ├── Data/         Zustands- und Definitionstypen (generisch über TState)
│   ├── Runtime/      Ablaufsteuerung, Interfaces für Prompt/Aufgabe/Feedback
│   ├── Environment/  Szenario-spezifische Umgebungssteuerung
│   ├── Feedback/     Zielmarkierung, Ton, Partikel (einsteckbar)
│   └── IO/           Protokollierung (CSV)
├── Editor/           Szenario-Generatoren, Szenen-Setup, Umgebungs-Generator
├── Scenarios/        Generierte ScriptableObject-Assets
├── Content/          Coach-Texte (CSV, IDs für spätere Audio-Zuordnung)
└── Scenes/           Exposure_Acrophobia.unity
```

### Tragende Entscheidungen

**Ein generischer Kern, geschlossen durch zwei Szenarien.**
`ExposureSessionController<TState>` ist generisch über den Umgebungszustand. Höhenangst
(`HeightState`) und Klaustrophobie (`RoomState`) sind zwei dünne Schließungen desselben
Codes. Zwei Schließungen statt einer sind Absicht: eine „generische" Struktur, die nur einmal
verwendet wurde, ist nicht als generisch nachgewiesen. Ein neues Szenario braucht einen
`IEnvironmentController<TState>` und die passenden Definitionstypen — sonst nichts.

**Inhalte als Daten, nicht als Code.** Stufen, Parameter und Befürchtungstexte liegen in
ScriptableObjects, erzeugt aus Editor-Generatoren. Eine Stufe hinzuzufügen ist Dateneingabe.

**Abhängigkeiten über Interfaces.** Abfrage-UI, Aufgabenerkennung, Biosignal und
Protokollierung hängen an `IPredictionPrompt`, `ITaskCompletionSource`, `IBiosignalSource`
und `ISessionLogger`. Die Herzfrequenz kommt derzeit aus einer simulierten Quelle; ein echter
Sensor ersetzt eine Klasse, nicht den Ablauf.

**Aufgaben-Feedback als einsteckbare Komponenten.** `ITaskFeedback` (Start / Fortschritt /
Abschluss / Abbruch) wird von beliebig vielen Implementierungen bedient — Zielmarkierung,
Halteton, Partikel sind unabhängig voneinander an- und abschaltbar. Bewusst **nicht** auf
XRIs Affordance-System gebaut: das ist in XRI 3.x durchgehend `[Obsolete]` und an
Interactor-Events gebunden, während hier eine geometrische Bedingung über Zeit gehalten wird.

**Erkennung ist geometrisch, nicht interaktionsbasiert.** Abstand, Blickwinkel und
Verweildauer — damit funktioniert die Aufgabenerkennung mit bloßem Handtracking, ohne
Greifobjekte. Eine Bedingung muss zudem *gehalten* werden; ein Streifen an der Kante zählt
nicht als Exposition.

**Platzhalter sind in den Daten als solche markiert**, nicht nur im Commit. Der
Befürchtungskatalog trägt den Hinweis, dass er kein validierter Itempool ist — das überlebt
Kopieren und Exportieren.

## Assets

Das Repository enthält keine kommerziellen Assets und ist nach dem Klonen als Blockout
lauffähig. Die verwendeten Sounds stammen von freesound.org (CC0 / CC BY) und liegen mit
Namensnennung bei. Pakete, Lizenzen und die Erzeugung der polierten Fassung: siehe
[ASSETS.md](ASSETS.md).

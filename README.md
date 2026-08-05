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

- Wirkmodell ist das inhibitorische Lernen: die Angstassoziation wird nicht gelöscht,
  sondern bekommt eine konkurrierende Sicherheitsassoziation.
  ([Craske et al. 2014](https://doi.org/10.1016/j.brat.2014.04.006))
- Wirksam ist die **Erwartungsverletzung** — die Lücke zwischen Befürchtung und
  tatsächlichem Ausgang. Sie ist mit inhibitorischem Lernen *und* mit der Emotional
  Processing Theory vereinbar. ([Hamlett et al. 2023](https://doi.org/10.1007/7854_2022_385))
- **Habituation und inhibitorisches Lernen schließen sich nicht aus.** Habituation findet
  statt und bleibt klinisch aussagekräftig; strittig ist nur, ob sie eine *notwendige*
  Bedingung und ein guter Prädiktor für den Langzeiterfolg ist.
  (Craske et al. 2008, referiert in
  [Blakey & Abramowitz 2019](https://doi.org/10.1016/j.cbpra.2018.03.001))
- Deshalb: **beides dokumentieren, nur die Erwartungsverletzung als Fortschrittskriterium
  verwenden.** Ein Habituations-Gate würde die Übung genau dann beenden, wenn keine
  Erwartung mehr besteht, die sich verletzen ließe — also wenn kein Lernen mehr möglich ist.
- Sicherheitssignale und subtile Vermeidung — Festhalten, Wegschauen, Abstandhalten —
  schwächen den Effekt und gehören angesprochen.
  ([Blakey & Abramowitz 2019](https://doi.org/10.1016/j.cbpra.2018.03.001);
  [Plaisted et al. 2021](https://doi.org/10.1007/s10567-021-00347-3))
- **Einordnung:** Die Belege zu einzelnen Optimierungsstrategien sind vorläufig — im Review
  von Plaisted wurde kein Befund repliziert. Als Designleitlinie brauchbar, nicht als feste
  Regel.

### Stellschrauben

| Parameter | Stufen | Wirkung |
|---|---|---|
| Höhe | Etage 0 … 10 | Grundintensität |
| Randschutz | Geländer → Glasbrüstung → offene Kante | Sicherheitssignal |
| Untergrund | fest → Gitterrost → Glasboden → Steg | Tiefenwirkung |
| Sicherheitsnetz | sichtbar / entfernt | Sicherheitssignal |
| Aufgabe | stehen → an Rand treten → hinabblicken → Steg queren | Annäherung |
| Wind | aus … stark | zusätzlicher Reiz |

- Pro Schritt nur **eine** Stellschraube verändern.
- Nicht stur höher: auf gleicher Höhe variieren, Bewältigtes neu kombinieren
  (Glasboden *und* keine Brüstung).
- Die Therapeut:in muss jederzeit eingreifen und die Stufe anpassen können.

### Ablauf einer Übung

1. Stufe auswählen — durch die Therapeut:in oder gemeinsam.
2. **Vorher:** Befürchtung aus einer Liste typischer Höhen-Katastrophengedanken auswählen,
   dazu Überzeugungsgrad in Prozent. Liste aus einem validierten Instrument
   (ACQ-Gedankenskala oder Heights Interpretation Questionnaire), kein Freitext.
3. Aufgabe ausführen. Keine Unterbrechung — außer kurzem Hinweis bei erkennbarer Vermeidung.
4. **Nachher:** „Ist es eingetreten?" plus Angstrating und erneuter Überzeugungsgrad —
   die Differenz ist das eigentliche Ergebnis.
5. Weiter erst nach Bestätigung durch die Patient:in.
6. Am Sitzungsende: Übersicht der getesteten Befürchtungen als Gesprächsgrundlage.

### Rahmenbedingungen

- Abbruch jederzeit ohne Begründung; harte Grenze bei Herzfrequenz 200.
- Start ebenerdig, kein automatischer Stufenwechsel.
- Höchstens zwei Unterbrechungen pro Übung — Dauerabfragen zerstören das Erleben.
- Bedienung per Handtracking, ohne Controller.
- Zu dokumentieren: Befürchtung und Überzeugungsgrad vorher/nachher, Ausgang, Angstverlauf
  (vorher/höchste/nachher, auch über Sitzungen hinweg), Abstand zur Kante.
- Vor Behandlungsbeginn und am Ende:
  [Acrophobia Questionnaire](https://doi.org/10.1016/S0005-7894(77)80116-0),
  STAI-State (deutsche Fassung).

---

## Projektaufbau

```
Assets/_Exposure/
├── Scripts/
│   ├── Data/         Zustands- und Definitionstypen (generisch über TState)
│   ├── Runtime/      Ablaufsteuerung, Interfaces für Prompt/Biosignal
│   ├── Environment/  Szenario-spezifische Umgebungssteuerung
│   └── IO/           Protokollierung (CSV)
├── Editor/           Szenario-Generatoren
├── Scenarios/        Generierte ScriptableObject-Assets
├── Content/          Coach-Texte (CSV, IDs für spätere Audio-Zuordnung)
└── Scenes/           Exposure_Acrophobia.unity
```

Der Kern ist generisch über den Umgebungszustand (`ExposureSessionController<TState>`).
Höhenangst (`HeightState`) und Klaustrophobie (`RoomState`) sind zwei dünne Schließungen
desselben Codes — ein neues Szenario braucht nur einen `IEnvironmentController<TState>`
und die passenden Definitionstypen.

## Assets

Das Projekt läuft **ohne kommerzielle Assets** — die Szene nutzt einen Blockout aus
Primitiven. Visuelle Ausgestaltung ist eine austauschbare Schicht darüber.

Kommerzielle Packs (City Builder Urban, POLYBOX Hazelwood Loft) liegen lokal unter
`Assets/3rd Party Assets/` und sind bewusst **nicht** im Repository: die Unity-Asset-Store-
EULA untersagt die Weiterverbreitung in extrahierbarer Form, und sie überschreiten die
GitHub-Limits deutlich.

## Wissenschaftliche Grundlage

Recherche und Begründung der Diagnosewahl in den Markdown-Dokumenten im übergeordneten
Ordner (`05_Diagnose_Recherche_und_Empfehlung.md`, `06_Akrophobie_Goldstandard.md`).

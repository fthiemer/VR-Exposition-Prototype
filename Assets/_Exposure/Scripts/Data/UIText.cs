using System.Collections.Generic;

namespace Exposure
{
    /// <summary>
    /// Central lookup for user-facing runtime text. Only German is populated today, but
    /// callers reference stable keys instead of literal strings, so adding a second
    /// language later is "add a table," not "hunt down every inline string in the code."
    /// </summary>
    public static class UIText
    {
        private static readonly Dictionary<string, string> De = new Dictionary<string, string>
        {
            ["ready_screen"] = "Als Nächstes: {0}\n\nBereit, nach oben zu gehen?",
            ["ready_confirm"] = "Ich bin bereit",
            ["task_dismiss"] = "Verstanden",
            ["predict_question"] = "Was, denkst du, wird hier oben passieren?",
            ["conviction_before_question"] = "Wie überzeugt bist du, dass das passieren wird?",
            ["outcome_question"] = "Du hast erwartet: „{0}“\n\nIst es passiert?",
            ["outcome_yes"] = "Ja, ist passiert",
            ["outcome_no"] = "Nein, ist nicht passiert",
            ["conviction_after_question"] = "Wie überzeugt bist du jetzt, dass es passieren würde?",
            ["anxiety_question"] = "Wie ängstlich hast du dich dabei gefühlt?",
            ["summary_title"] = "Was du heute getestet hast",
            ["summary_levels"] = "Bearbeitete Stufen: {0}",
            ["summary_disconfirmed"] = "Befürchtungen, die nicht eingetreten sind: {0} von {1}",
            ["summary_conviction_drop"] = "Im Schnitt warst du danach {0} % weniger überzeugt.",
            ["summary_conviction_same"] = "Deine Überzeugung blieb etwa gleich — das lohnt sich zu besprechen.",
            ["summary_empty"] = "Sitzung beendet.",
            ["summary_done"] = "Fertig",
            ["aborted_message"] = "Wir haben hier gestoppt. Das war die richtige Entscheidung, kein Rückschlag.",
            ["aborted_close"] = "Schließen",
            ["repeat_level_coach"] = "Bleiben wir auf dieser Stufe und versuchen es noch einmal.",
            ["avoidance_approach_edge"] = "Lass dir Zeit — vielleicht gehst du noch einen Schritt näher an die Kante.",
            ["avoidance_look_down_away"] = "Versuch, weiter nach unten zu schauen statt wegzusehen.",
            ["avoidance_look_down_not_at_edge"] = "Geh zuerst an die Kante, dann schau nach unten.",
        };

        public static string Get(string key, params object[] args)
        {
            if (!De.TryGetValue(key, out var value)) return key;
            return args != null && args.Length > 0 ? string.Format(value, args) : value;
        }
    }
}

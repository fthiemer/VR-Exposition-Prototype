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
            ["ready_screen"] = "Als Nächstes:\n{0}",
            ["ready_confirm"] = "Hochfahren",
            ["task_briefing"] = "Deine Aufgabe:\n\n{0}",
            ["task_dismiss"] = "Verstanden",
            ["space_hint"] = "Sorg dafür, dass rundherum etwa zwei Meter frei sind.\nDu wirst dich bewegen.",
            ["space_hint_confirm"] = "Alles frei",

            // Intro (E1) and closing (O, E2) -- asked once per session, on the ground.
            ["predict_question"] = "Was, denkst du, wird dir dort oben passieren?",
            ["expectancy_before_question"] = "Wie sicher bist du, dass das passiert?",
            ["outcome_occurred_question"] = "Du hast erwartet: „{0}“\n\nWie stark ist das insgesamt eingetreten?",
            ["expectancy_after_question"] = "Wie sicher wärst du jetzt, dass das beim nächsten Mal passiert?",

            // Scale anchors. Naming both ends and the middle is what makes a rating
            // interpretable -- a bare number leaves people inventing their own scale.
            ["scale_expectancy_low"] = "glaube ich nicht",
            ["scale_expectancy_mid"] = "könnte sein",
            ["scale_expectancy_high"] = "ganz sicher",
            ["scale_occurred_low"] = "gar nicht",
            ["scale_occurred_mid"] = "teilweise",
            ["scale_occurred_high"] = "genau wie erwartet",
            ["rating_confirm"] = "Weiter",

            // Floor selection (once, after the intro) and the post-task menu (after each task).
            ["floor_select_question"] = "Welche Etage möchtest du versuchen?",
            ["floor_locked_hint"] = "noch nicht freigeschaltet",
            ["task_choice_question"] = "Wie geht's weiter?",
            ["choice_repeat"] = "Nochmal versuchen",
            ["choice_other_task"] = "Andere Aufgabe auf dieser Etage",
            ["choice_next_floor"] = "Eine Etage höher",
            ["choice_next_floor_locked"] = "erst nach einer geschafften Aufgabe",
            ["choice_end_session"] = "Sitzung beenden",
            ["safer_than_before_coach"] = "Fühlst du dich sicherer als vorhin?",

            ["summary_title"] = "Was du heute getestet hast",
            ["summary_expectancy_drop"] = "Deine Erwartung ist um {0} Punkte gesunken — das lohnt sich zu besprechen.",
            ["summary_expectancy_same"] = "Deine Erwartung blieb etwa gleich — das lohnt sich zu besprechen.",
            ["summary_done_no_rating"] = "Sitzung beendet.",
            ["summary_empty"] = "Sitzung beendet.",
            ["summary_done"] = "Fertig",
            ["aborted_message"] = "Wir haben hier gestoppt. Das war die richtige Entscheidung, kein Rückschlag.",
            ["aborted_close"] = "Schließen",
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

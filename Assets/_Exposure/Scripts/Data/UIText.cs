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

            // Two different questions, because the two situations are different. After a
            // completed task the screen is a reward and an invitation; after an abandoned one it
            // is just a menu. One neutral wording for both made the completed case read as if
            // something had gone wrong.
            ["task_choice_done"] = "Geschafft!\n\nWie möchtest du weitermachen?",
            ["task_choice_question"] = "Wie möchtest du weitermachen?",
            ["choice_repeat"] = "Diese Aufgabe nochmal",
            ["choice_other_task"] = "Andere Aufgabe auf dieser Etage",
            ["choice_next_floor"] = "Eine Etage höher fahren",
            ["choice_next_floor_locked"] = "frei, sobald du hier eine Aufgabe schaffst",
            ["choice_end_session"] = "Runterfahren und Sitzung beenden",
            ["safer_than_before_coach"] = "Fühlst du dich sicherer als vorhin?",

            ["summary_title"] = "Sitzung abgeschlossen. Gut gemacht!",
            ["summary_expectancy_drop"] = "Deine Erwartung ist um {0} Punkte gesunken — das lohnt sich zu besprechen.",
            ["summary_expectancy_same"] = "Deine Erwartung blieb etwa gleich — das lohnt sich zu besprechen.",
            ["summary_done_no_rating"] = "Bis zum nächsten Mal.",
            ["summary_empty"] = "Sitzung abgeschlossen. Gut gemacht!",
            ["summary_done"] = "Schließen",
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

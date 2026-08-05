using System;

namespace Exposure
{
    /// <summary>
    /// Abstraktion der In-VR-Angstabfrage (visuelle Analogskala 0-100 %).
    /// Konkrete Umsetzung als World-Space-UI, per Handtracking bedienbar.
    /// </summary>
    public interface IAnxietyPrompt
    {
        /// <summary>
        /// Zeigt die VAS-Abfrage und ruft <paramref name="onAnswered"/> mit dem Wert
        /// (0-100) auf, sobald die Person bestätigt hat.
        /// </summary>
        void Ask(string questionLabel, Action<int> onAnswered);
    }
}

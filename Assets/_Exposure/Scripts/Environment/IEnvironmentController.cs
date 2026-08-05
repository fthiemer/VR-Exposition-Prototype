namespace Exposure
{
    /// <summary>
    /// Abstraktion für die Anwendung eines RoomState auf die konkrete Szene.
    /// Entkoppelt Ablaufsteuerung von der Raumdarstellung -> pro Szenario eine
    /// eigene Implementierung (Klaustrophobie-Keller, Höhe, ...), gleiche Steuerung.
    /// Nahtloser Wechsel OHNE Brille-Absetzen (behebt die 30-s-Bruchstelle der Studie).
    /// </summary>
    public interface IEnvironmentController
    {
        /// <summary>
        /// Wendet den Zielzustand an. <paramref name="instant"/> = true springt
        /// hart (z. B. beim Initialisieren), sonst weiche Überblendung.
        /// </summary>
        void Apply(RoomState state, bool instant);
    }
}

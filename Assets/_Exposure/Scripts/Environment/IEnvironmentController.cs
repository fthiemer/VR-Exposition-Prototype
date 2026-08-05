namespace Exposure
{
    /// <summary>
    /// Abstraction for applying a scenario-specific environment state to the scene.
    /// Decouples the generic session flow from the concrete environment representation
    /// -> one implementation per scenario (claustrophobia room, height platform, ...),
    /// same generic session controller. Seamless transitions without removing the
    /// headset (fixes the 30s scene-cut break point from the reference study).
    /// </summary>
    public interface IEnvironmentController<TState>
    {
        /// <summary>
        /// Applies the target state. <paramref name="instant"/> = true jumps immediately
        /// (e.g. on init), otherwise a smooth transition is expected.
        /// </summary>
        void Apply(TState state, bool instant);
    }
}

namespace Exposure
{
    /// <summary>Concrete acrophobia session: closes ExposureSessionController over HeightState.</summary>
    public class HeightExposureSessionController : ExposureSessionController<HeightState>
    {
        protected override HeightState DefaultState => HeightState.Default;
    }
}

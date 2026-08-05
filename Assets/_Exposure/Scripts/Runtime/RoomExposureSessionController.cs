namespace Exposure
{
    /// <summary>Concrete claustrophobia session: closes ExposureSessionController over RoomState.</summary>
    public class RoomExposureSessionController : ExposureSessionController<RoomState>
    {
        protected override RoomState DefaultState => RoomState.Default;
    }
}

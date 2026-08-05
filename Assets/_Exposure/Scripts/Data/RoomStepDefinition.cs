using UnityEngine;

namespace Exposure
{
    /// <summary>Concrete claustrophobia step: closes ExposureStepDefinition over RoomState.</summary>
    [CreateAssetMenu(fileName = "Step_", menuName = "Exposure/Claustrophobia/Room Step")]
    public class RoomStepDefinition : ExposureStepDefinition<RoomState>
    {
    }
}

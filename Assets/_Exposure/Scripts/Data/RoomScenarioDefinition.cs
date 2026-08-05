using UnityEngine;

namespace Exposure
{
    /// <summary>Concrete claustrophobia scenario: closes ExposureScenarioDefinition over RoomState.</summary>
    [CreateAssetMenu(fileName = "Scenario_", menuName = "Exposure/Claustrophobia/Room Scenario")]
    public class RoomScenarioDefinition : ExposureScenarioDefinition<RoomState>
    {
    }
}

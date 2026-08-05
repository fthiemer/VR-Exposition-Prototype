using UnityEngine;

namespace Exposure
{
    /// <summary>Concrete acrophobia scenario: closes ExposureScenarioDefinition over HeightState.</summary>
    [CreateAssetMenu(fileName = "Scenario_", menuName = "Exposure/Acrophobia/Height Scenario")]
    public class HeightScenarioDefinition : ExposureScenarioDefinition<HeightState>
    {
    }
}

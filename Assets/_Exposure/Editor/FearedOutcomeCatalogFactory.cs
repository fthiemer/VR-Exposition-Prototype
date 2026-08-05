#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Exposure.EditorTools
{
    /// <summary>
    /// Generates the placeholder feared-outcome catalog for the acrophobia scenario.
    /// See FearedOutcomeCatalog for why these items are placeholders and what should
    /// replace them.
    /// </summary>
    public static class FearedOutcomeCatalogFactory
    {
        private const string Folder = "Assets/_Exposure/Scenarios/Acrophobia";

        [MenuItem("Exposure/Generate Feared Outcome Catalog (placeholder)")]
        public static void CreateCatalog()
        {
            Directory.CreateDirectory(Folder);

            var catalog = ScriptableObject.CreateInstance<FearedOutcomeCatalog>();
            catalog.sourceInstrument = "PLACEHOLDER - replace with validated items (ACQ thought scale / HIQ)";
            catalog.outcomes.Add(new FearedOutcome { id = "balance", text = "I will lose my balance and fall." });
            catalog.outcomes.Add(new FearedOutcome { id = "dizzy",   text = "I will get so dizzy I cannot hold myself up." });
            catalog.outcomes.Add(new FearedOutcome { id = "support", text = "The railing or floor will not hold." });
            catalog.outcomes.Add(new FearedOutcome { id = "control", text = "I will lose control of myself." });
            catalog.outcomes.Add(new FearedOutcome { id = "panic",   text = "I will panic and not be able to get away." });

            AssetDatabase.CreateAsset(catalog, $"{Folder}/FearedOutcomes_Acrophobia.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);
            Debug.Log("[Exposure] Placeholder feared-outcome catalog generated under " + Folder);
        }
    }
}
#endif

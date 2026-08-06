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
            catalog.sourceInstrument = "PLATZHALTER - durch validierte Items ersetzen (ACQ-Gedankenskala / HIQ)";
            catalog.outcomes.Add(new FearedOutcome { id = "balance", text = "Ich werde das Gleichgewicht verlieren und stürzen." });
            catalog.outcomes.Add(new FearedOutcome { id = "dizzy",   text = "Mir wird so schwindelig, dass ich mich nicht mehr halten kann." });
            catalog.outcomes.Add(new FearedOutcome { id = "support", text = "Das Geländer oder der Boden hält nicht." });
            catalog.outcomes.Add(new FearedOutcome { id = "control", text = "Ich verliere die Kontrolle über mich selbst." });
            catalog.outcomes.Add(new FearedOutcome { id = "panic",   text = "Ich bekomme Panik und komme nicht mehr weg." });

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

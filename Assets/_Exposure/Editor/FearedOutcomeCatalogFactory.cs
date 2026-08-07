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
            catalog.sourceInstrument =
                "Angelehnt an das Heights Interpretation Questionnaire (HIQ), " +
                "Steinman & Teachman 2011, J Anxiety Disord 25:896-902. " +
                "Sinngemäss umformuliert, NICHT die Originalitems und keine validierte Übersetzung.";

            // The eight HIQ interpretations, reworded rather than translated. Two reasons: the
            // instrument is copyrighted and this repository is public, and the original items are
            // written for a paper questionnaire ("You will fall") rather than for a coach speaking
            // to someone standing on a ledge.
            catalog.outcomes.Add(new FearedOutcome { id = "fall",      text = "Ich werde herunterfallen." });
            catalog.outcomes.Add(new FearedOutcome { id = "injury",    text = "Ich werde mich verletzen." });
            catalog.outcomes.Add(new FearedOutcome { id = "unsafe",    text = "Ich bin hier oben nicht sicher." });
            catalog.outcomes.Add(new FearedOutcome { id = "panic",     text = "Ich gerate in Panik und verliere die Kontrolle." });
            catalog.outcomes.Add(new FearedOutcome { id = "endure",    text = "Ich werde die Angst nicht aushalten." });
            catalog.outcomes.Add(new FearedOutcome { id = "faint",     text = "Ich werde ohnmächtig." });
            catalog.outcomes.Add(new FearedOutcome { id = "freeze",    text = "Ich erstarre und komme nicht mehr weg." });
            catalog.outcomes.Add(new FearedOutcome { id = "dizzy",     text = "Ich werde so schwindelig, dass ich mich nicht halten kann." });

            AssetDatabase.CreateAsset(catalog, $"{Folder}/FearedOutcomes_Acrophobia.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);
            Debug.Log("[Exposure] Feared-outcome catalog generated (HIQ-based) under " + Folder);
        }
    }
}
#endif

using System.IO;
using UnityEditor;
using UnityEngine;

public static class TacticsSelectionPanelPrefabGenerator
{
    private const string PrefabDirectory = "Assets/Resources/UI";
    private const string PrefabPath = PrefabDirectory + "/TacticsSelectionPanelView.prefab";

    [MenuItem("Tools/Tactics/Generate Selection Panel Prefab")]
    public static void GeneratePrefab()
    {
        Directory.CreateDirectory(PrefabDirectory);

        GameObject root = new GameObject("Tactics Selection Panel View");
        try
        {
            TacticsSelectionPanelView view = root.AddComponent<TacticsSelectionPanelView>();
            view.EditorBuildPrefabContent();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Generated selection panel prefab at {PrefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}

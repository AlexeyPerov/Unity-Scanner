using System.Linq;
using UnityEditor;
using UnityScanner.UI.Window;
using UnityScanner.Windows;

namespace UnityScanner.ContextMenus
{
    public static class USContextMenus
    {
        [MenuItem("Assets/[US] Find References In Project", false, 20)]
        public static void FindReferencesInProject()
        {
            var selectedPaths = Selection.objects
                .Select(AssetDatabase.GetAssetPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .ToArray();

            if (selectedPaths.Length == 0)
            {
                EditorUtility.DisplayDialog("Unity Scanner", "No valid assets selected.", "OK");
                return;
            }

            FindReferencesWindow.OpenWithSelection();
        }

        [MenuItem("Assets/[US] Find References In Project", true)]
        public static bool ValidateFindReferences()
        {
            return Selection.objects != null && Selection.objects.Length > 0;
        }

        [MenuItem("Tools/[US] Unity Scanner", false, 105)]
        public static void OpenUnityScanner()
        {
            UnityScannerWindow.Launch();
        }
    }
}

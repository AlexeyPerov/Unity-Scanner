using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;

namespace UnityScanner.Categories.Materials
{
    public class MaterialsFixProvider : IUnityScannerFixProvider
    {
        public bool CanFix(UnityScannerIssue issue)
        {
            if (issue?.CategoryId != "materials")
                return false;

            return issue.IssueCode == MaterialsIssueMapper.CodeNullMaterial
                   || issue.IssueCode == MaterialsIssueMapper.CodeNullMaterialSlot
                   || issue.IssueCode == MaterialsIssueMapper.CodeBuiltinMaterial
                   || issue.IssueCode == MaterialsIssueMapper.CodeShaderNull
                   || issue.IssueCode == MaterialsIssueMapper.CodeShaderInternalError;
        }

        public UnityScannerFixPreview Preview(UnityScannerIssue issue, UnityScannerScanContext context)
        {
            switch (issue.IssueCode)
            {
                case MaterialsIssueMapper.CodeNullMaterial:
                    return new UnityScannerFixPreview
                    {
                        Description = $"Remove null material entry on renderer at {issue.AssetPath}",
                        IsSafe = true,
                        AffectedAssets = new List<string> { issue.AssetPath }
                    };
                case MaterialsIssueMapper.CodeNullMaterialSlot:
                    return new UnityScannerFixPreview
                    {
                        Description = $"Remove null material slot on renderer at {issue.AssetPath}",
                        IsSafe = true,
                        AffectedAssets = new List<string> { issue.AssetPath }
                    };
                case MaterialsIssueMapper.CodeBuiltinMaterial:
                    return new UnityScannerFixPreview
                    {
                        Description = $"Replace builtin material reference on renderer at {issue.AssetPath}",
                        IsSafe = false,
                        AffectedAssets = new List<string> { issue.AssetPath }
                    };
                case MaterialsIssueMapper.CodeShaderNull:
                case MaterialsIssueMapper.CodeShaderInternalError:
                    return new UnityScannerFixPreview
                    {
                        Description = $"Assign fallback shader to material at {issue.AssetPath}",
                        IsSafe = false,
                        AffectedAssets = new List<string> { issue.AssetPath }
                    };
                default:
                    return new UnityScannerFixPreview
                    {
                        Description = "No fix available",
                        IsSafe = true
                    };
            }
        }

        public IEnumerator Apply(UnityScannerIssue issue, UnityScannerScanContext context)
        {
            if (string.IsNullOrEmpty(issue.AssetPath)) yield break;

            switch (issue.IssueCode)
            {
                case MaterialsIssueMapper.CodeNullMaterial:
                case MaterialsIssueMapper.CodeNullMaterialSlot:
                    yield return ApplyRemoveNullMaterialSlots(issue);
                    break;
            }

            yield return null;
        }

        private static IEnumerator ApplyRemoveNullMaterialSlots(UnityScannerIssue issue)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(issue.AssetPath);
            if (go == null) yield break;

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var changed = false;

            foreach (var renderer in renderers)
            {
                var shared = renderer.sharedMaterials;
                if (shared == null || shared.Length == 0) continue;

                var filtered = shared.Where(m => m != null).ToArray();
                if (filtered.Length == shared.Length) continue;

                renderer.sharedMaterials = filtered;
                EditorUtility.SetDirty(renderer);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(go);
                AssetDatabase.SaveAssets();
                Debug.Log($"[US] Removed null material slots on {issue.AssetPath}");
            }

            yield return null;
        }
    }
}

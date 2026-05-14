using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityScanner.UI.Controls
{
    public static class USGUIUtilities
    {
        public static bool MonochromeMode { get; set; }

        public static void HorizontalLine(int marginTop = 5, int marginBottom = 5, int height = 2)
        {
            HorizontalLine(marginTop, marginBottom, height, new Color(0.5f, 0.5f, 0.5f, 1));
        }

        public static void HorizontalLine(int marginTop, int marginBottom, int height, Color color)
        {
            EditorGUILayout.BeginHorizontal();
            var rect = EditorGUILayout.GetControlRect(
                false,
                height,
                new GUIStyle { margin = new RectOffset(0, 0, marginTop, marginBottom) });

            EditorGUI.DrawRect(rect, color);
            EditorGUILayout.EndHorizontal();
        }
        
        public static void DrawAssetButtonWithFixedWidth(string assetPath, float fixedWidth = 300f, float height = 18f)
        {
            var selectedObjectType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            var selectedObjectContent = EditorGUIUtility.ObjectContent(null, selectedObjectType);
            selectedObjectContent.text = Path.GetFileName(assetPath);
            selectedObjectContent.tooltip = "Click to select this asset in the Project window";

            var alignment = GUI.skin.button.alignment;
            GUI.skin.button.alignment = TextAnchor.MiddleLeft;

            if (GUILayout.Button(selectedObjectContent, GUILayout.Width(fixedWidth), GUILayout.Height(height)))
            {
                Selection.objects = new[] { AssetDatabase.LoadMainAssetAtPath(assetPath) };
            }

            GUI.skin.button.alignment = alignment;
        }

        public static void DrawAssetButton(string assetPath, float minWidth = 300f, float height = 18f)
        {
            var selectedObjectType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            var selectedObjectContent = EditorGUIUtility.ObjectContent(null, selectedObjectType);
            selectedObjectContent.text = Path.GetFileName(assetPath);
            selectedObjectContent.tooltip = "Click to select this asset in the Project window";

            var alignment = GUI.skin.button.alignment;
            GUI.skin.button.alignment = TextAnchor.MiddleLeft;

            if (GUILayout.Button(selectedObjectContent, GUILayout.MinWidth(minWidth), GUILayout.Height(height)))
            {
                Selection.objects = new[] { AssetDatabase.LoadMainAssetAtPath(assetPath) };
            }

            GUI.skin.button.alignment = alignment;
        }

        public static void DrawColoredLabel(string text, Color color, int? width = null)
        {
            var prevColor = GUI.color;
            GUI.color = color;
            var textStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
            if (width.HasValue)
                GUILayout.Label(text, textStyle, GUILayout.Width(width.Value));
            else
                GUILayout.Label(text, textStyle);

            GUI.color = prevColor;
        }

        public static void DrawColoredLabel(GUIContent content, Color color, int? width = null)
        {
            var prevColor = GUI.color;
            GUI.color = color;
            var textStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
            if (width.HasValue)
                GUILayout.Label(content, textStyle, GUILayout.Width(width.Value));
            else
                GUILayout.Label(content, textStyle);

            GUI.color = prevColor;
        }

        public static void DrawColoredLabelByWarning(string text, int warningLevel, int? width = null)
        {
            var label = MonochromeMode ? $"{GetSeverityTag(warningLevel)} {text}" : text;
            DrawColoredLabel(label, GetColorByWarning(warningLevel), width);
        }

        public static bool DrawColoredFoldout(bool value, string text, Color color)
        {
            var prevColor = GUI.color;
            GUI.color = color;
            var result = EditorGUILayout.Foldout(value, text);
            GUI.color = prevColor;
            return result;
        }

        public static bool DrawColoredFoldoutByWarning(bool value, string text, int warningLevel)
        {
            return DrawColoredFoldout(value, text, GetColorByWarning(warningLevel));
        }

        public static void DrawAtCenterHorizontally(Action toDraw, Color color)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var prevColor = GUI.color;
            GUI.color = color;
            toDraw();
            GUI.color = prevColor;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        public static void DrawLabelAtCenterHorizontally(string text, Color color)
        {
            DrawAtCenterHorizontally(() => { GUILayout.Label(text); }, color);
        }

        public static string GetSeverityTag(int warningLevel)
        {
            if (warningLevel >= 4) return "[CRITICAL]";
            if (warningLevel == 3) return "[HIGH]";
            if (warningLevel == 2) return "[MEDIUM]";
            if (warningLevel == 1) return "[LOW]";
            return "";
        }

        public static Color GetColorByWarning(int warningLevel)
        {
            if (MonochromeMode)
                return warningLevel > 0 ? new Color(0.9f, 0.9f, 0.9f) : Color.white;

            if (warningLevel >= 5)
                return Color.red;
            if (warningLevel == 4)
                return new Color(1f, 0.2f, 0.2f);
            if (warningLevel == 3)
                return new Color(1f, 0.4f, 0.4f);
            if (warningLevel == 2)
                return Color.yellow;
            if (warningLevel == 1)
                return Color.cyan;

            return Color.white;
        }

        public static Color GetHunterColorByWarning(int warningLevel)
        {
            if (warningLevel > 2)
                return Color.red;
            if (warningLevel == 2)
                return Color.yellow;
            if (warningLevel == 1)
                return new Color(0.5f, 0.8f, 1f);

            return Color.white;
        }

        public static void DrawCustomWarnings(USItemDataBase item)
        {
            if (item == null) return;

            var hasNew = item.Errors.Count > 0 || item.Warnings.Count > 0 ||
                         item.Infos.Count > 0 || item.VerboseMessages.Count > 0;

            if (hasNew)
            {
                DrawIssueSection(item.Errors, "Errors:", new Color(1f, 0.4f, 0.4f));
                DrawIssueSection(item.Warnings, "Warnings:", Color.yellow);
                DrawIssueSection(item.Infos, "Info:", Color.cyan);
                DrawIssueSection(item.VerboseMessages, "Details:", Color.white);
            }
            else if (item.CustomWarnings != null && item.CustomWarnings.Count > 0)
            {
                DrawIssueSection(item.CustomWarnings, "Warnings:", Color.yellow);
            }
        }

        private static void DrawIssueSection(List<string> messages, string header, Color color)
        {
            if (messages == null || messages.Count == 0) return;
            GUILayout.Space(3);
            var prev = GUI.color;
            GUI.color = color;
            GUILayout.Label(header, EditorStyles.boldLabel);
            GUI.color = prev;
            EditorGUI.indentLevel++;
            foreach (var w in messages)
                DrawColoredLabel("- " + w, color);
            EditorGUI.indentLevel--;
        }
    }
}

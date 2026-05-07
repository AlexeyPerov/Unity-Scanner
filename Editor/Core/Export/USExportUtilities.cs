using System;
using System.Text;
using UnityEditor;

namespace UnityScanner.Core.Export
{
    public static class USExportUtilities
    {
        public static string EscapeCsvField(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.IndexOfAny(new[] { '"', ',', '\n', '\r' }) >= 0)
                return '"' + value.Replace("\"", "\"\"") + '"';

            return value;
        }

        public static string TsvCell(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            return raw.Replace("\t", " ").Replace("\r", "").Replace("\n", " ");
        }

        public static void CopyToClipboard(string text)
        {
            EditorGUIUtility.systemCopyBuffer = text;
        }

        public static void CopyToClipboard(StringBuilder sb)
        {
            EditorGUIUtility.systemCopyBuffer = sb.ToString();
        }

        public static string GetReadableSize(long bytesSize)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytesSize;
            var order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}

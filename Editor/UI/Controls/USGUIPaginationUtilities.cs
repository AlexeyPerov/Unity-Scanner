using UnityEditor;
using UnityEngine;

namespace UnityScanner.UI.Controls
{
    public static class USGUIPaginationUtilities
    {
        public static void DrawPagesWidget(int itemsCount, USPaginationSettings settings)
        {
            if (itemsCount == 0 || settings.PageSize == 0)
                return;

            var pagesCount = itemsCount / settings.PageSize + (itemsCount % settings.PageSize > 0 ? 1 : 0);

            if (pagesCount <= 1)
            {
                if (settings.PageToShow.HasValue)
                    settings.PageToShow = null;
                return;
            }

            settings.Scroll = EditorGUILayout.BeginScrollView(settings.Scroll);

            EditorGUILayout.BeginHorizontal();

            var prevColor = GUI.color;

            var allowAllButton = itemsCount <= 100;

            if (allowAllButton)
            {
                GUI.color = !settings.PageToShow.HasValue ? Color.yellow : Color.white;

                if (GUILayout.Button(new GUIContent("All", "Show all entries on one page"), GUILayout.Width(30f)))
                {
                    settings.PageToShow = null;
                }

                GUI.color = prevColor;
            }
            else
            {
                settings.PageToShow ??= 0;
            }

            for (var i = 0; i < pagesCount; i++)
            {
                var shouldDraw = true;

                if (pagesCount > 100)
                {
                    var anchorPage = settings.PageToShow ?? 0;

                    if (i != 0 && i != pagesCount - 1 && i % 100 != 0 &&
                        (i < anchorPage - 20 || i > anchorPage + 20))
                        shouldDraw = false;
                }

                if (shouldDraw)
                {
                    prevColor = GUI.color;
                    GUI.color = settings.PageToShow == i ? Color.yellow : Color.white;

                    if (GUILayout.Button(new GUIContent((i + 1).ToString(), $"Go to page {i + 1}"), GUILayout.Width(i <= 998 ? 30f : 60f)))
                    {
                        settings.PageToShow = i;
                    }

                    GUI.color = prevColor;
                }
            }

            if (settings.PageToShow.HasValue && settings.PageToShow > pagesCount - 1)
            {
                settings.PageToShow = Mathf.Max(0, pagesCount - 1);
            }

            if (settings.PageToShow.HasValue && pagesCount == 0)
            {
                settings.PageToShow = null;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        public static bool ShouldDrawItem(int index, USPaginationSettings settings)
        {
            if (settings.PageToShow.HasValue)
            {
                var page = settings.PageToShow.Value;
                if (index < page * settings.PageSize || index >= (page + 1) * settings.PageSize)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

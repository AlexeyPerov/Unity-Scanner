using UnityEditor;
using UnityEngine;

namespace UnityScanner.UI.Controls
{
    public class USPaginationSettings
    {
        public int SortingOption;
        public int? PageToShow = 0;
        public int PageSize = 10;
        public Vector2 Scroll;
    }
}

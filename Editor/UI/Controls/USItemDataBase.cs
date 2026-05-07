using System.Collections.Generic;

namespace UnityScanner.UI.Controls
{
    public abstract class USItemDataBase
    {
        public int WarningLevel { get; private set; }

        public void TrySetWarningLevel(int level)
        {
            if (level <= WarningLevel) return;
            WarningLevel = level;
        }

        public List<string> CustomWarnings { get; private set; }

        public void AddCustomWarning(string warning)
        {
            CustomWarnings ??= new List<string>();
            CustomWarnings.Add(warning);
        }
    }
}

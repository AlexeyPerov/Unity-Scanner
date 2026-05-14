using System.Collections.Generic;
using System.Linq;

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

        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Infos { get; } = new List<string>();
        public List<string> VerboseMessages { get; } = new List<string>();

        public bool HasIssues => Errors.Count > 0 || Warnings.Count > 0 || Infos.Count > 0 || VerboseMessages.Count > 0;

        public void AddError(string msg) { Errors.Add(msg); TrySetWarningLevel(3); }
        public void AddWarning(string msg) { Warnings.Add(msg); TrySetWarningLevel(2); }
        public void AddInfo(string msg) { Infos.Add(msg); TrySetWarningLevel(1); }
        public void AddVerbose(string msg) { VerboseMessages.Add(msg); }

        public List<string> CustomWarnings { get; private set; } = new List<string>();

        public void AddCustomWarning(string warning)
        {
            CustomWarnings ??= new List<string>();
            CustomWarnings.Add(warning);
        }

        public IEnumerable<string> AllMessages =>
            Errors.Concat(Warnings).Concat(Infos).Concat(VerboseMessages)
                .Concat(CustomWarnings ?? Enumerable.Empty<string>());
    }
}

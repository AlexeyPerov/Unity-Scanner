using System.Collections.Generic;

namespace UnityScanner.Utilities.AssetDatabase
{
    public class USIgnorePatternsManager
    {
        public List<string> DefaultIgnorePatterns { get; } = new List<string>
        {
            @"/Resources/",
            @"/Editor/",
            @"/Editor Default Resources/",
            @"/Editor Resources/",
            @"ProjectSettings/",
            @"Packages/",
            @"\.asmdef$",
            @"link\.xml$",
            @"\.csv$",
            @"\.md$",
            @"\.json$",
            @"\.xml$",
            @"\.txt$",
            @"\.cginc",
            @"\.spriteatlas"
        };

        private List<System.Text.RegularExpressions.Regex> _compiledPatterns;

        public List<System.Text.RegularExpressions.Regex> CompiledPatterns
        {
            get
            {
                if (_compiledPatterns == null)
                    _compiledPatterns = USPathFilterUtilities.CompilePatterns(DefaultIgnorePatterns);
                return _compiledPatterns;
            }
        }

        public void InvalidateCompiledPatterns()
        {
            _compiledPatterns = null;
        }

        public bool IsPathValidForOutput(string path)
        {
            return USPathFilterUtilities.IsValidForOutput(path, CompiledPatterns);
        }
    }
}

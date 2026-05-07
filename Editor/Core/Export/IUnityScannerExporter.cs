using UnityScanner.Core.Results;

namespace UnityScanner.Core.Export
{
    public interface IUnityScannerExporter
    {
        string FormatId { get; }
        void Export(UnityScannerResult result, string outputPath);
    }
}

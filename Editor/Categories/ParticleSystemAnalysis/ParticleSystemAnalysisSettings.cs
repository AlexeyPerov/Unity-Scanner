using UnityScanner.Core.Categories;

namespace UnityScanner.Categories.ParticleSystemAnalysis
{
    public class ParticleSystemAnalysisSettings : UnityScannerCategorySettings
    {
        public bool CheckEmission = true;
        public bool CheckCollision = true;
        public bool CheckOverdraw = true;
        public bool CheckSubEmitters = true;
        public bool CheckLOD = true;
        public bool CheckSimulationMismatch = true;
        public bool CheckTextures = true;
        public bool CheckModuleCount = true;
        public string PathFilter = "";
    }
}

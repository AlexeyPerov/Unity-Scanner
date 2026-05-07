using UnityScanner.Core.Categories;

namespace UnityScanner.Categories.PhysicsAnalysis
{
    public class PhysicsAnalysisSettings : UnityScannerCategorySettings
    {
        public bool CheckRigidbodyExceeded = true;
        public bool CheckStaticColliderOnMovingParent = true;
        public bool CheckNoGravityNoConstraints = true;
        public bool CheckTriggerNonKinematic = true;
        public bool CheckInterpolationUnnecessary = true;
        public bool CheckMeshColliderComplex = true;
        public bool CheckConcaveMeshKinematic = true;
        public bool CheckMissingMaterial = true;
        public bool CheckLayerMatrixBloat = true;
        public string PathFilter = "";
    }
}

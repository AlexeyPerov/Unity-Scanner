using System.Collections.Generic;
using UnityScanner.UI.Controls;

namespace UnityScanner.Categories.PhysicsAnalysis
{
    public class ScenePhysicsData : USItemDataBase
    {
        public string ScenePath;
        public int RigidbodyCount;
        public int ColliderCount;
        public int TriggerCount;
        public List<RigidbodyData> Rigidbodies = new List<RigidbodyData>();
        public List<ColliderData> Colliders = new List<ColliderData>();
        public List<LayerCollisionPair> LayerCollisions = new List<LayerCollisionPair>();
        public bool Foldout;
    }

    public class RigidbodyData
    {
        public string ObjectPath;
        public bool IsKinematic;
        public bool UseGravity;
        public int Constraints;
        public int InterpolationMode;
        public bool IsTrigger;
        public string ScenePath;
        public string AssetPath;
    }

    public class ColliderData
    {
        public string ObjectPath;
        public string ColliderType;
        public bool IsTrigger;
        public bool HasPhysicsMaterial;
        public int TriangleCount;
        public bool IsConvex;
        public bool IsStatic;
        public bool HasMovingParent;
        public string ScenePath;
        public string AssetPath;
    }

    public class LayerCollisionPair
    {
        public int LayerA;
        public int LayerB;
        public bool Enabled;
        public int ColliderCountA;
        public int ColliderCountB;
    }
}

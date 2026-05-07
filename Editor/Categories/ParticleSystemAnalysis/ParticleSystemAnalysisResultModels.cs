using System.Collections.Generic;
using UnityScanner.UI.Controls;

namespace UnityScanner.Categories.ParticleSystemAnalysis
{
    public class ParticleSystemData : USItemDataBase
    {
        public string AssetPath;
        public string ScenePath;
        public int EmissionRate;
        public bool IsBurst;
        public int BurstCount;
        public int ActiveModuleCount;
        public string SimulationSpace;
        public int MaxParticles;
        public bool CollisionEnabled;
        public bool CollisionSendMessages;
        public bool TrailEnabled;
        public float TrailLifetime;
        public int SubEmitterCount;
        public int SubEmitterChainDepth;
        public bool HasLOD;
        public string MainTexturePath;
        public int MainTextureSize;
        public List<string> ActiveModules = new List<string>();
        public bool Foldout;
    }
}

using UnityEngine;

namespace UnityScanner.Core.Settings
{
    public class UnityScannerSettings : ScriptableObject
    {
        public bool CacheEnabled = false;
        public bool BinaryCacheEnabled = false;
        public string CachePath = "Library/UnityScanner/UnityScannerAssetCache.bin";
        public string BuildLayoutPath = "";
        public bool CacheAutoValidateOnLoad = true;

        public int YieldAssetThreshold = 5000;
        public int YieldIntervalDivisor = 10;

        public string ActivePlatformProfileId = PlatformProfilePresets.Mobile;
        private PlatformProfile _activeProfile;

        public PlatformProfile ActivePlatformProfile
        {
            get
            {
                if (_activeProfile == null)
                    _activeProfile = PlatformProfilePresets.GetPreset(ActivePlatformProfileId);
                return _activeProfile;
            }
            set
            {
                _activeProfile = value;
                ActivePlatformProfileId = value?.Id ?? PlatformProfilePresets.Desktop;
            }
        }

        public void SetPlatformProfile(string profileId)
        {
            ActivePlatformProfileId = profileId;
            _activeProfile = PlatformProfilePresets.GetPreset(profileId);
        }
    }
}

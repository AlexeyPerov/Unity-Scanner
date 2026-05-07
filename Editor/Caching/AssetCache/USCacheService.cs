using System;
using System.Collections.Generic;
using UnityScanner.Core.Settings;
using UnityEngine;

namespace UnityScanner.Caching
{
    public class USCacheService
    {
        private readonly IUnityScannerCacheProvider _cacheProvider;
        private readonly USCacheInvalidationService _invalidationService;
        private readonly USCacheStatusModel _statusModel;

        public USCacheStatusModel Status => _statusModel;
        public USAssetCacheData Data => _cacheProvider?.Data;
        public bool IsCacheAvailable => _statusModel.CurrentStatus == USCacheStatus.LoadSucceeded;
        public bool IsFallbackMode => _statusModel.IsFallbackMode;

        public USCacheService(
            IUnityScannerCacheProvider cacheProvider,
            USCacheInvalidationService invalidationService)
        {
            _cacheProvider = cacheProvider;
            _invalidationService = invalidationService;
            _statusModel = new USCacheStatusModel();
        }

        public bool TryLoadAndValidate(UnityScannerSettings settings, USCacheValidationContext validationContext)
        {
            if (!settings.CacheEnabled)
            {
                _statusModel.RecordStatus(USCacheStatus.Disabled);
                return false;
            }

            try
            {
                var loadResult = _cacheProvider.Load(settings.CachePath);
                _statusModel.RecordStatus(loadResult, loadResult != USCacheStatus.LoadSucceeded
                    ? "Cache load failed"
                    : null);

                if (loadResult != USCacheStatus.LoadSucceeded)
                    return false;

                if (!settings.BinaryCacheEnabled)
                {
                    return true;
                }

                var validationResult = _cacheProvider.Validate(validationContext);
                if (validationResult != USCacheStatus.LoadSucceeded)
                {
                    _statusModel.RecordStatus(validationResult, "Cache validation failed");
                    _cacheProvider.Invalidate();
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                _statusModel.RecordStatus(USCacheStatus.LoadFailed, e.Message);
                Debug.LogWarning($"[UnityScanner] Cache load failed, falling back to non-cached mode: {e.Message}");
                return false;
            }
        }

        public bool TrySave(UnityScannerSettings settings)
        {
            if (!settings.CacheEnabled)
                return false;

            if (!settings.BinaryCacheEnabled)
                return false;

            try
            {
                var result = _cacheProvider.Save(settings.CachePath);
                _statusModel.RecordStatus(result, result != USCacheStatus.WriteSucceeded
                    ? "Cache save failed"
                    : null);
                return result == USCacheStatus.WriteSucceeded;
            }
            catch (Exception e)
            {
                _statusModel.RecordStatus(USCacheStatus.WriteFailed, e.Message);
                Debug.LogWarning($"[UnityScanner] Cache save failed: {e.Message}");
                return false;
            }
        }

        public void Invalidate()
        {
            _cacheProvider?.Invalidate();
            _statusModel.RecordStatus(USCacheStatus.Invalidated);
        }

        public void Clear()
        {
            _cacheProvider?.Clear();
            _statusModel.Reset();
        }

        public USAssetCacheEntry GetEntryByGuid(string guid)
        {
            if (!IsCacheAvailable || Data == null)
                return null;

            Data.EntriesByGuid.TryGetValue(guid, out var entry);
            return entry;
        }

        public USAssetCacheEntry GetEntryByPath(string path)
        {
            if (!IsCacheAvailable || Data == null)
                return null;

            if (Data.PathToGuid.TryGetValue(path, out var guid))
            {
                Data.EntriesByGuid.TryGetValue(guid, out var entry);
                return entry;
            }

            return null;
        }
    }
}

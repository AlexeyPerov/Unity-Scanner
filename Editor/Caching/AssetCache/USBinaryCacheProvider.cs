using System;
using System.IO;
using UnityEngine;

namespace UnityScanner.Caching
{
    public class USBinaryCacheProvider : IUnityScannerCacheProvider
    {
        private USAssetCacheData _data;
        private USCacheStatus _status = USCacheStatus.NotLoaded;

        public USCacheStatus Status => _status;
        public USAssetCacheData Data => _data;

        public USCacheStatus Load(string cachePath)
        {
            _status = USCacheStatus.NotLoaded;

            try
            {
                if (string.IsNullOrEmpty(cachePath) || !File.Exists(cachePath))
                {
                    _status = USCacheStatus.LoadFailed;
                    return _status;
                }

                using (var stream = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(stream))
                {
                    _data = USBinaryCacheSerializer.Read(reader);
                }

                if (_data == null)
                {
                    _status = USCacheStatus.LoadFailed;
                    return _status;
                }

                _status = USCacheStatus.LoadSucceeded;
                return _status;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityScanner] Binary cache load error: {e.Message}");
                _data = null;
                _status = USCacheStatus.LoadFailed;
                return _status;
            }
        }

        public USCacheStatus Validate(USCacheValidationContext context)
        {
            if (_data == null || _data.Header == null)
            {
                _status = USCacheStatus.ValidationFailed;
                return _status;
            }

            var invalidationService = new USCacheInvalidationService();
            if (invalidationService.ShouldInvalidate(_data, context))
            {
                _status = USCacheStatus.ValidationFailed;
                return _status;
            }

            _status = USCacheStatus.LoadSucceeded;
            return _status;
        }

        public USCacheStatus Save(string cachePath)
        {
            if (_data == null)
            {
                _status = USCacheStatus.WriteFailed;
                return _status;
            }

            try
            {
                var directory = Path.GetDirectoryName(cachePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                _data.Header.LastModifiedTimestamp = DateTime.UtcNow.Ticks;

                using (var stream = new FileStream(cachePath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(stream))
                {
                    USBinaryCacheSerializer.Write(writer, _data);
                }

                _status = USCacheStatus.WriteSucceeded;
                return _status;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityScanner] Binary cache save error: {e.Message}");
                _status = USCacheStatus.WriteFailed;
                return _status;
            }
        }

        public void Invalidate()
        {
            _data = null;
            _status = USCacheStatus.Invalidated;
        }

        public void Clear()
        {
            _data = new USAssetCacheData();
            _status = USCacheStatus.NotLoaded;
        }

        public void SetData(USAssetCacheData data)
        {
            _data = data;
        }
    }
}

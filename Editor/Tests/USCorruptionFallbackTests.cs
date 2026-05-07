using System.IO;
using System.Text;
using NUnit.Framework;
using UnityScanner.Caching;
using UnityScanner.Core.Settings;
using UnityEngine;

namespace UnityScanner.Tests
{
    public class USCorruptionFallbackTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "USCacheCorrupt_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        [Test]
        public void Provider_Load_MissingFile_ReturnsLoadFailed()
        {
            var provider = new USBinaryCacheProvider();
            var result = provider.Load(Path.Combine(_tempDir, "nonexistent.bin"));

            Assert.AreEqual(USCacheStatus.LoadFailed, result);
            Assert.IsNull(provider.Data);
        }

        [Test]
        public void Provider_Load_EmptyFile_ReturnsLoadFailed()
        {
            var path = Path.Combine(_tempDir, "empty.bin");
            File.WriteAllBytes(path, new byte[0]);

            var provider = new USBinaryCacheProvider();
            var result = provider.Load(path);

            Assert.AreEqual(USCacheStatus.LoadFailed, result);
        }

        [Test]
        public void Provider_Load_CorruptedData_ReturnsLoadFailed()
        {
            var path = Path.Combine(_tempDir, "corrupt.bin");
            File.WriteAllBytes(path, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x01, 0x02 });

            var provider = new USBinaryCacheProvider();
            var result = provider.Load(path);

            Assert.AreEqual(USCacheStatus.LoadFailed, result);
        }

        [Test]
        public void Provider_Load_WrongMagicString_ReturnsLoadFailed()
        {
            var path = Path.Combine(_tempDir, "wrong_magic.bin");
            using (var stream = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write("WRONGMAGIC");
            }

            var provider = new USBinaryCacheProvider();
            var result = provider.Load(path);

            Assert.AreEqual(USCacheStatus.LoadFailed, result);
        }

        [Test]
        public void Provider_Load_TruncatedValidHeader_ReturnsLoadFailed()
        {
            var path = Path.Combine(_tempDir, "truncated.bin");
            using (var stream = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write("USCACHE");
                writer.Write(1);
                writer.Write("1.0.0");
            }

            var provider = new USBinaryCacheProvider();
            var result = provider.Load(path);

            Assert.AreEqual(USCacheStatus.LoadFailed, result);
        }

        [Test]
        public void Provider_Save_NullData_ReturnsWriteFailed()
        {
            var provider = new USBinaryCacheProvider();
            var path = Path.Combine(_tempDir, "null_save.bin");

            var result = provider.Save(path);

            Assert.AreEqual(USCacheStatus.WriteFailed, result);
        }

        [Test]
        public void Provider_Invalidate_SetsDataNull()
        {
            var provider = new USBinaryCacheProvider();
            provider.SetData(MakeValidData());

            provider.Invalidate();

            Assert.IsNull(provider.Data);
            Assert.AreEqual(USCacheStatus.Invalidated, provider.Status);
        }

        [Test]
        public void Provider_Clear_SetsEmptyData()
        {
            var provider = new USBinaryCacheProvider();
            provider.SetData(MakeValidData());

            provider.Clear();

            Assert.IsNotNull(provider.Data);
            Assert.AreEqual(0, provider.Data.EntriesByGuid.Count);
            Assert.AreEqual(USCacheStatus.NotLoaded, provider.Status);
        }

        [Test]
        public void Provider_SaveThenLoad_RoundTrip_Succeeds()
        {
            var data = MakeValidData();
            var path = Path.Combine(_tempDir, "roundtrip.bin");

            var saveProvider = new USBinaryCacheProvider();
            saveProvider.SetData(data);
            var saveResult = saveProvider.Save(path);

            Assert.AreEqual(USCacheStatus.WriteSucceeded, saveResult);

            var loadProvider = new USBinaryCacheProvider();
            var loadResult = loadProvider.Load(path);

            Assert.AreEqual(USCacheStatus.LoadSucceeded, loadResult);
            Assert.AreEqual(data.Header.UnityVersion, loadProvider.Data.Header.UnityVersion);
        }

        [Test]
        public void NullProvider_AllOperations_ReturnDisabled()
        {
            var provider = new USNullCacheProvider();

            Assert.AreEqual(USCacheStatus.Disabled, provider.Status);
            Assert.IsNull(provider.Data);
            Assert.AreEqual(USCacheStatus.Disabled, provider.Load("any"));
            Assert.AreEqual(USCacheStatus.Disabled, provider.Validate(null));
            Assert.AreEqual(USCacheStatus.Disabled, provider.Save("any"));
        }

        [Test]
        public void CacheService_DisabledSettings_ReturnsFalse()
        {
            var settings = ScriptableObject.CreateInstance<UnityScannerSettings>();
            settings.CacheEnabled = false;

            var service = new USCacheService(new USBinaryCacheProvider(), new USCacheInvalidationService());
            var result = service.TryLoadAndValidate(settings, new USCacheValidationContext());

            Assert.IsFalse(result);
            Assert.AreEqual(USCacheStatus.Disabled, service.Status.CurrentStatus);

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void CacheService_LoadFailed_SetsFallbackMode()
        {
            var settings = ScriptableObject.CreateInstance<UnityScannerSettings>();
            settings.CacheEnabled = true;
            settings.BinaryCacheEnabled = false;
            settings.CachePath = Path.Combine(_tempDir, "missing.bin");

            var service = new USCacheService(new USBinaryCacheProvider(), new USCacheInvalidationService());
            var result = service.TryLoadAndValidate(settings, new USCacheValidationContext());

            Assert.IsFalse(result);
            Assert.IsTrue(service.IsFallbackMode);

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void CacheService_SaveDisabled_ReturnsFalse()
        {
            var settings = ScriptableObject.CreateInstance<UnityScannerSettings>();
            settings.CacheEnabled = false;

            var service = new USCacheService(new USBinaryCacheProvider(), new USCacheInvalidationService());
            var result = service.TrySave(settings);

            Assert.IsFalse(result);

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void CacheService_Invalidate_RecordsStatus()
        {
            var service = new USCacheService(new USBinaryCacheProvider(), new USCacheInvalidationService());
            service.Invalidate();

            Assert.AreEqual(USCacheStatus.Invalidated, service.Status.CurrentStatus);
            Assert.IsTrue(service.IsFallbackMode);
        }

        [Test]
        public void CacheService_Clear_ResetsStatus()
        {
            var service = new USCacheService(new USBinaryCacheProvider(), new USCacheInvalidationService());
            service.Invalidate();
            service.Clear();

            Assert.AreEqual(USCacheStatus.Disabled, service.Status.CurrentStatus);
        }

        [Test]
        public void StatusModel_RecordsFallbackHistory()
        {
            var model = new USCacheStatusModel();

            model.RecordStatus(USCacheStatus.LoadFailed, "test error 1");
            model.RecordStatus(USCacheStatus.LoadSucceeded);
            model.RecordStatus(USCacheStatus.ValidationFailed, "test error 2");

            Assert.AreEqual(2, model.FallbackHistory.Count);
            Assert.AreEqual(USCacheStatus.LoadFailed, model.FallbackHistory[0].Status);
            Assert.AreEqual("test error 1", model.FallbackHistory[0].Error);
            Assert.AreEqual(USCacheStatus.ValidationFailed, model.FallbackHistory[1].Status);
        }

        [Test]
        public void StatusModel_Reset_ClearsState()
        {
            var model = new USCacheStatusModel();
            model.RecordStatus(USCacheStatus.LoadFailed, "error");
            model.RecordStatus(USCacheStatus.LoadSucceeded);

            model.Reset();

            Assert.AreEqual(USCacheStatus.Disabled, model.CurrentStatus);
            Assert.IsNull(model.LastError);
            Assert.AreEqual(1, model.FallbackHistory.Count);
        }

        [Test]
        public void StatusModel_IsFallbackMode_TrueWhenNotLoaded()
        {
            var model = new USCacheStatusModel();

            model.RecordStatus(USCacheStatus.Disabled);
            Assert.IsTrue(model.IsFallbackMode);

            model.RecordStatus(USCacheStatus.LoadFailed);
            Assert.IsTrue(model.IsFallbackMode);

            model.RecordStatus(USCacheStatus.LoadSucceeded);
            Assert.IsFalse(model.IsFallbackMode);
        }

        private USAssetCacheData MakeValidData()
        {
            return new USAssetCacheData
            {
                Header = new USCacheHeader
                {
                    SchemaVersion = USCacheHeader.CurrentSchemaVersion,
                    ToolVersion = "1.0.0",
                    UnityVersion = "2022.3.0f1",
                    ProjectId = "test"
                }
            };
        }
    }
}

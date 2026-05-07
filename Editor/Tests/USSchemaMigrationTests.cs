using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityScanner.Caching;

namespace UnityScanner.Tests
{
    public class USSchemaMigrationTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "USCacheTests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        [Test]
        public void Serializer_WriteThenRead_PreservesHeaderSchemaVersion()
        {
            var data = CreateTestData();
            var path = Path.Combine(_tempDir, "cache.bin");

            WriteBinary(data, path);
            var loaded = ReadBinary(path);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(USCacheHeader.CurrentSchemaVersion, loaded.Header.SchemaVersion);
        }

        [Test]
        public void Serializer_WriteThenRead_PreservesAllHeaderFields()
        {
            var data = CreateTestData();
            data.Header.ToolVersion = "2.0.0";
            data.Header.UnityVersion = "2022.3.50f1";
            data.Header.ProjectId = "abc123";
            data.Header.CreatedTimestamp = 1000;
            data.Header.LastModifiedTimestamp = 2000;

            var path = Path.Combine(_tempDir, "cache.bin");
            WriteBinary(data, path);
            var loaded = ReadBinary(path);

            Assert.AreEqual("2.0.0", loaded.Header.ToolVersion);
            Assert.AreEqual("2022.3.50f1", loaded.Header.UnityVersion);
            Assert.AreEqual("abc123", loaded.Header.ProjectId);
            Assert.AreEqual(1000, loaded.Header.CreatedTimestamp);
            Assert.AreEqual(2000, loaded.Header.LastModifiedTimestamp);
        }

        [Test]
        public void Invalidation_OldSchemaVersion_TriggersInvalidation()
        {
            var data = CreateTestData();
            data.Header.SchemaVersion = 0;

            var service = new USCacheInvalidationService();
            var context = new USCacheValidationContext
            {
                UnityVersion = data.Header.UnityVersion,
                ProjectId = data.Header.ProjectId
            };

            Assert.IsTrue(service.ShouldInvalidate(data, context));
        }

        [Test]
        public void Invalidation_FutureSchemaVersion_TriggersInvalidation()
        {
            var data = CreateTestData();
            data.Header.SchemaVersion = USCacheHeader.CurrentSchemaVersion + 100;

            var service = new USCacheInvalidationService();
            var context = new USCacheValidationContext();

            Assert.IsTrue(service.ShouldInvalidate(data, context));
        }

        [Test]
        public void Invalidation_CurrentSchemaVersion_DoesNotInvalidate()
        {
            var data = CreateTestData();

            var service = new USCacheInvalidationService();
            var context = new USCacheValidationContext
            {
                UnityVersion = data.Header.UnityVersion,
                ProjectId = data.Header.ProjectId
            };

            Assert.IsFalse(service.ShouldInvalidate(data, context));
        }

        [Test]
        public void Provider_Validate_RejectsOldSchema()
        {
            var data = CreateTestData();
            data.Header.SchemaVersion = 0;

            var provider = new USBinaryCacheProvider();
            provider.SetData(data);

            var result = provider.Validate(new USCacheValidationContext());
            Assert.AreEqual(USCacheStatus.ValidationFailed, result);
        }

        [Test]
        public void Provider_Validate_AcceptsCurrentSchema()
        {
            var data = CreateTestData();

            var provider = new USBinaryCacheProvider();
            provider.SetData(data);

            var context = new USCacheValidationContext
            {
                UnityVersion = data.Header.UnityVersion,
                ProjectId = data.Header.ProjectId
            };

            var result = provider.Validate(context);
            Assert.AreEqual(USCacheStatus.LoadSucceeded, result);
        }

        [Test]
        public void Serializer_RoundTrip_PreservesEmptyEntries()
        {
            var data = new USAssetCacheData();
            data.Header = new USCacheHeader
            {
                SchemaVersion = USCacheHeader.CurrentSchemaVersion,
                UnityVersion = "2022.3",
                ProjectId = "empty"
            };

            var path = Path.Combine(_tempDir, "empty.bin");
            WriteBinary(data, path);
            var loaded = ReadBinary(path);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(0, loaded.EntriesByGuid.Count);
            Assert.AreEqual(0, loaded.PathToGuid.Count);
        }

        [Test]
        public void Serializer_RoundTrip_PreservesEntriesWithDependencies()
        {
            var data = CreateTestData();
            var entry1 = new USAssetCacheEntry
            {
                Guid = "guid1",
                Path = "Assets/a.prefab",
                TypeName = "GameObject",
                FileSize = 1024,
                ImportMarker = 500,
                IsAddressable = true,
                BundleName = "bundle_a",
                DirectDependencies = new HashSet<string> { "guid2", "guid3" },
                ReverseDependencies = new HashSet<string> { "guid4" }
            };
            data.EntriesByGuid["guid1"] = entry1;
            data.PathToGuid["Assets/a.prefab"] = "guid1";

            var path = Path.Combine(_tempDir, "deps.bin");
            WriteBinary(data, path);
            var loaded = ReadBinary(path);

            Assert.IsTrue(loaded.EntriesByGuid.ContainsKey("guid1"));
            var loadedEntry = loaded.EntriesByGuid["guid1"];
            Assert.AreEqual("Assets/a.prefab", loadedEntry.Path);
            Assert.AreEqual(1024, loadedEntry.FileSize);
            Assert.AreEqual(true, loadedEntry.IsAddressable);
            Assert.AreEqual("bundle_a", loadedEntry.BundleName);
            Assert.AreEqual(2, loadedEntry.DirectDependencies.Count);
            Assert.IsTrue(loadedEntry.DirectDependencies.Contains("guid2"));
            Assert.IsTrue(loadedEntry.DirectDependencies.Contains("guid3"));
            Assert.AreEqual(1, loadedEntry.ReverseDependencies.Count);
            Assert.IsTrue(loadedEntry.ReverseDependencies.Contains("guid4"));
        }

        [Test]
        public void Serializer_RoundTrip_PreservesBuildLayoutMetadata()
        {
            var data = CreateTestData();
            data.BuildLayoutPath = "BuildLayout.txt";
            data.BuildLayoutTimestamp = 99999;

            var path = Path.Combine(_tempDir, "bl.bin");
            WriteBinary(data, path);
            var loaded = ReadBinary(path);

            Assert.AreEqual("BuildLayout.txt", loaded.BuildLayoutPath);
            Assert.AreEqual(99999, loaded.BuildLayoutTimestamp);
        }

        [Test]
        public void Serializer_RoundTrip_PreservesPathToGuidMap()
        {
            var data = CreateTestData();
            data.PathToGuid["Assets/textures/tex.png"] = "abc123";
            data.PathToGuid["Assets/materials/mat.mat"] = "def456";

            var path = Path.Combine(_tempDir, "paths.bin");
            WriteBinary(data, path);
            var loaded = ReadBinary(path);

            Assert.AreEqual(2, loaded.PathToGuid.Count);
            Assert.AreEqual("abc123", loaded.PathToGuid["Assets/textures/tex.png"]);
            Assert.AreEqual("def456", loaded.PathToGuid["Assets/materials/mat.mat"]);
        }

        private USAssetCacheData CreateTestData()
        {
            return new USAssetCacheData
            {
                Header = new USCacheHeader
                {
                    SchemaVersion = USCacheHeader.CurrentSchemaVersion,
                    ToolVersion = "1.0.0",
                    UnityVersion = "2022.3.0f1",
                    ProjectId = "test_project",
                    CreatedTimestamp = 100,
                    LastModifiedTimestamp = 200
                }
            };
        }

        private void WriteBinary(USAssetCacheData data, string path)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(stream))
            {
                USBinaryCacheSerializer.Write(writer, data);
            }
        }

        private USAssetCacheData ReadBinary(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(stream))
            {
                return USBinaryCacheSerializer.Read(reader);
            }
        }
    }
}

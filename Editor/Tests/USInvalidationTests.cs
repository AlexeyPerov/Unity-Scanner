using NUnit.Framework;
using UnityScanner.Caching;

namespace UnityScanner.Tests
{
    public class USInvalidationTests
    {
        [Test]
        public void ShouldInvalidate_NullContext_ReturnsTrue()
        {
            var service = new USCacheInvalidationService();
            var data = MakeValidData();

            Assert.IsTrue(service.ShouldInvalidate(data, null));
        }

        [Test]
        public void ShouldInvalidate_NullHeader_ReturnsTrue()
        {
            var service = new USCacheInvalidationService();
            var data = new USAssetCacheData();

            Assert.IsTrue(service.ShouldInvalidate(data, new USCacheValidationContext()));
        }

        [Test]
        public void ShouldInvalidate_ForceRebuild_ReturnsTrue()
        {
            var service = new USCacheInvalidationService();
            var data = MakeValidData();

            var context = new USCacheValidationContext
            {
                ForceRebuild = true,
                UnityVersion = data.Header.UnityVersion,
                ProjectId = data.Header.ProjectId
            };

            Assert.IsTrue(service.ShouldInvalidate(data, context));
        }

        [Test]
        public void ShouldInvalidate_SameVersions_ReturnsFalse()
        {
            var service = new USCacheInvalidationService();
            var data = MakeValidData();

            var context = new USCacheValidationContext
            {
                UnityVersion = data.Header.UnityVersion,
                ProjectId = data.Header.ProjectId
            };

            Assert.IsFalse(service.ShouldInvalidate(data, context));
        }

        [Test]
        public void ShouldInvalidate_DifferentUnityVersion_ReturnsTrue()
        {
            var service = new USCacheInvalidationService();
            var data = MakeValidData();

            var context = new USCacheValidationContext
            {
                UnityVersion = "2099.1.0f1",
                ProjectId = data.Header.ProjectId
            };

            Assert.IsTrue(service.ShouldInvalidate(data, context));
        }

        [Test]
        public void ShouldInvalidate_DifferentProjectId_ReturnsTrue()
        {
            var service = new USCacheInvalidationService();
            var data = MakeValidData();

            var context = new USCacheValidationContext
            {
                UnityVersion = data.Header.UnityVersion,
                ProjectId = "different_project"
            };

            Assert.IsTrue(service.ShouldInvalidate(data, context));
        }

        [Test]
        public void ShouldInvalidate_EmptyContextVersionFields_ReturnsFalse()
        {
            var service = new USCacheInvalidationService();
            var data = MakeValidData();

            var context = new USCacheValidationContext();

            Assert.IsFalse(service.ShouldInvalidate(data, context));
        }

        [Test]
        public void ShouldInvalidate_BuildLayoutPathChanged_ReturnsTrue()
        {
            var service = new USCacheInvalidationService();
            var data = MakeValidData();
            data.BuildLayoutPath = "old_layout.txt";
            data.BuildLayoutTimestamp = 100;

            var context = new USCacheValidationContext
            {
                UnityVersion = data.Header.UnityVersion,
                ProjectId = data.Header.ProjectId,
                BuildLayoutPath = "new_layout.txt",
                BuildLayoutTimestamp = 100
            };

            Assert.IsTrue(service.ShouldInvalidate(data, context));
        }

        [Test]
        public void ShouldInvalidate_BuildLayoutTimestampChanged_ReturnsTrue()
        {
            var service = new USCacheInvalidationService();
            var data = MakeValidData();
            data.BuildLayoutPath = "layout.txt";
            data.BuildLayoutTimestamp = 100;

            var context = new USCacheValidationContext
            {
                UnityVersion = data.Header.UnityVersion,
                ProjectId = data.Header.ProjectId,
                BuildLayoutPath = "layout.txt",
                BuildLayoutTimestamp = 999
            };

            Assert.IsTrue(service.ShouldInvalidate(data, context));
        }

        [Test]
        public void ShouldInvalidate_BuildLayoutSame_ReturnsFalse()
        {
            var service = new USCacheInvalidationService();
            var data = MakeValidData();
            data.BuildLayoutPath = "layout.txt";
            data.BuildLayoutTimestamp = 100;

            var context = new USCacheValidationContext
            {
                UnityVersion = data.Header.UnityVersion,
                ProjectId = data.Header.ProjectId,
                BuildLayoutPath = "layout.txt",
                BuildLayoutTimestamp = 100
            };

            Assert.IsFalse(service.ShouldInvalidate(data, context));
        }

        [Test]
        public void ShouldInvalidate_BuildLayoutPathEmptyInContext_ReturnsFalse()
        {
            var service = new USCacheInvalidationService();
            var data = MakeValidData();
            data.BuildLayoutPath = "layout.txt";

            var context = new USCacheValidationContext
            {
                UnityVersion = data.Header.UnityVersion,
                ProjectId = data.Header.ProjectId,
                BuildLayoutPath = ""
            };

            Assert.IsFalse(service.ShouldInvalidate(data, context));
        }

        [Test]
        public void ShouldInvalidateEntry_NullEntry_ReturnsTrue()
        {
            var service = new USCacheInvalidationService();
            Assert.IsTrue(service.ShouldInvalidateEntry(null, "path", 0));
        }

        [Test]
        public void ShouldInvalidateEntry_PathChanged_ReturnsTrue()
        {
            var service = new USCacheInvalidationService();
            var entry = new USAssetCacheEntry { Path = "old/path.asset", ImportMarker = 100 };

            Assert.IsTrue(service.ShouldInvalidateEntry(entry, "new/path.asset", 100));
        }

        [Test]
        public void ShouldInvalidateEntry_ImportMarkerChanged_ReturnsTrue()
        {
            var service = new USCacheInvalidationService();
            var entry = new USAssetCacheEntry { Path = "path.asset", ImportMarker = 100 };

            Assert.IsTrue(service.ShouldInvalidateEntry(entry, "path.asset", 999));
        }

        [Test]
        public void ShouldInvalidateEntry_SamePathAndMarker_ReturnsFalse()
        {
            var service = new USCacheInvalidationService();
            var entry = new USAssetCacheEntry { Path = "path.asset", ImportMarker = 100 };

            Assert.IsFalse(service.ShouldInvalidateEntry(entry, "path.asset", 100));
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
                    ProjectId = "test_project"
                }
            };
        }
    }
}

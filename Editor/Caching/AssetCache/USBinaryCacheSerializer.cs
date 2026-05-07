using System.IO;

namespace UnityScanner.Caching
{
    public static class USBinaryCacheSerializer
    {
        private const string MagicString = "USCACHE";

        public static void Write(BinaryWriter writer, USAssetCacheData data)
        {
            writer.Write(MagicString);
            WriteHeader(writer, data.Header);
            writer.Write(data.EntriesByGuid.Count);
            foreach (var kvp in data.EntriesByGuid)
            {
                writer.Write(kvp.Key);
                WriteEntry(writer, kvp.Value);
            }

            writer.Write(data.PathToGuid.Count);
            foreach (var kvp in data.PathToGuid)
            {
                writer.Write(kvp.Key);
                writer.Write(kvp.Value);
            }

            writer.Write(data.BuildLayoutPath ?? string.Empty);
            writer.Write(data.BuildLayoutTimestamp);
        }

        public static USAssetCacheData Read(BinaryReader reader)
        {
            var magic = reader.ReadString();
            if (magic != MagicString)
                return null;

            var data = new USAssetCacheData();
            data.Header = ReadHeader(reader);

            var entryCount = reader.ReadInt32();
            for (var i = 0; i < entryCount; i++)
            {
                var guid = reader.ReadString();
                var entry = ReadEntry(reader);
                entry.Guid = guid;
                data.EntriesByGuid[guid] = entry;
            }

            var pathCount = reader.ReadInt32();
            for (var i = 0; i < pathCount; i++)
            {
                var path = reader.ReadString();
                var guid = reader.ReadString();
                data.PathToGuid[path] = guid;
            }

            data.BuildLayoutPath = reader.ReadString();
            data.BuildLayoutTimestamp = reader.ReadInt64();

            return data;
        }

        private static void WriteHeader(BinaryWriter writer, USCacheHeader header)
        {
            writer.Write(header.SchemaVersion);
            writer.Write(header.ToolVersion ?? string.Empty);
            writer.Write(header.UnityVersion ?? string.Empty);
            writer.Write(header.ProjectId ?? string.Empty);
            writer.Write(header.CreatedTimestamp);
            writer.Write(header.LastModifiedTimestamp);
        }

        private static USCacheHeader ReadHeader(BinaryReader reader)
        {
            var header = new USCacheHeader();
            header.SchemaVersion = reader.ReadInt32();
            header.ToolVersion = reader.ReadString();
            header.UnityVersion = reader.ReadString();
            header.ProjectId = reader.ReadString();
            header.CreatedTimestamp = reader.ReadInt64();
            header.LastModifiedTimestamp = reader.ReadInt64();
            return header;
        }

        private static void WriteEntry(BinaryWriter writer, USAssetCacheEntry entry)
        {
            writer.Write(entry.Path ?? string.Empty);
            writer.Write(entry.TypeName ?? string.Empty);
            writer.Write(entry.FileSize);
            writer.Write(entry.ImportMarker);
            writer.Write(entry.IsAddressable);
            writer.Write(entry.BundleName ?? string.Empty);

            writer.Write(entry.DirectDependencies.Count);
            foreach (var dep in entry.DirectDependencies)
                writer.Write(dep ?? string.Empty);

            writer.Write(entry.ReverseDependencies.Count);
            foreach (var dep in entry.ReverseDependencies)
                writer.Write(dep ?? string.Empty);
        }

        private static USAssetCacheEntry ReadEntry(BinaryReader reader)
        {
            var entry = new USAssetCacheEntry();
            entry.Path = reader.ReadString();
            entry.TypeName = reader.ReadString();
            entry.FileSize = reader.ReadInt64();
            entry.ImportMarker = reader.ReadInt64();
            entry.IsAddressable = reader.ReadBoolean();
            entry.BundleName = reader.ReadString();

            var directCount = reader.ReadInt32();
            entry.DirectDependencies = new System.Collections.Generic.HashSet<string>();
            for (var i = 0; i < directCount; i++)
                entry.DirectDependencies.Add(reader.ReadString());

            var reverseCount = reader.ReadInt32();
            entry.ReverseDependencies = new System.Collections.Generic.HashSet<string>();
            for (var i = 0; i < reverseCount; i++)
                entry.ReverseDependencies.Add(reader.ReadString());

            return entry;
        }
    }
}

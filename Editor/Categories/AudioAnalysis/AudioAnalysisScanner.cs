using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityScanner.Core.Export;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;
using UnityEngine;

namespace UnityScanner.Categories.AudioAnalysis
{
    public static class AudioAnalysisScanner
    {
        public static void ScanAll(
            AudioAnalysisSettings settings,
            PlatformProfile profile,
            List<AudioClipData> clips,
            IUnityScannerIssueSink issueSink)
        {
            var audioGuids = AssetDatabase.FindAssets("t:AudioClip");
            var total = audioGuids.Length;

            for (var i = 0; i < audioGuids.Length; i++)
            {
                if (i % 100 == 0)
                    issueSink.ReportProgress((float)i / total * 0.7f, "Scanning audio clips...");

                var clipPath = AssetDatabase.GUIDToAssetPath(audioGuids[i]);
                if (!string.IsNullOrEmpty(settings.PathFilter) && !clipPath.Contains(settings.PathFilter))
                    continue;

                var clip = AssetDatabase.LoadMainAssetAtPath(clipPath) as AudioClip;
                if (clip == null) continue;

                var data = CreateClipData(clip, clipPath);
                clips.Add(data);
            }

            if (settings.DetectDuplicates)
            {
                issueSink.ReportProgress(0.8f, "Detecting duplicates...");
                DetectDuplicateClips(clips);
            }

            GC.Collect();
        }

        private static AudioClipData CreateClipData(AudioClip clip, string path)
        {
            long fileBytes = 0;
            try { if (File.Exists(path)) fileBytes = new FileInfo(path).Length; } catch { }

            var data = new AudioClipData
            {
                Path = path,
                Name = Path.GetFileName(path),
                Clip = clip,
                FileSizeBytes = fileBytes,
                Duration = clip.length,
                SampleRate = clip.samples > 0 && clip.length > 0 ? (int)(clip.samples / clip.length) : 0,
                Channels = clip.channels
            };

            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer != null)
            {
                var defaultSettings = new AudioImporterSampleSettings();
                try { defaultSettings = importer.defaultSampleSettings; } catch { }

                data.LoadType = defaultSettings.loadType switch
                {
                    AudioClipLoadType.DecompressOnLoad => "DecompressOnLoad",
                    AudioClipLoadType.CompressedInMemory => "CompressedInMemory",
                    AudioClipLoadType.Streaming => "Streaming",
                    _ => "Unknown"
                };

                data.CompressionFormat = defaultSettings.compressionFormat switch
                {
                    AudioCompressionFormat.PCM => "PCM",
                    AudioCompressionFormat.Vorbis => "Vorbis",
                    AudioCompressionFormat.ADPCM => "ADPCM",
                    AudioCompressionFormat.MP3 => "MP3",
                    AudioCompressionFormat.VAG => "VAG",
                    AudioCompressionFormat.XMA => "XMA",
                    AudioCompressionFormat.GCADPCM => "GCADPCM",
                    AudioCompressionFormat.AAC => "AAC",
                    _ => "Unknown"
                };
            }

            var maxClipMB = 10;
            if (fileBytes > maxClipMB * 1024L * 1024L)
                data.TrySetWarningLevel(2);

            return data;
        }

        private static void DetectDuplicateClips(List<AudioClipData> clips)
        {
            var bySize = new Dictionary<long, List<AudioClipData>>();
            foreach (var clip in clips)
            {
                if (clip.FileSizeBytes <= 0) continue;
                if (!bySize.TryGetValue(clip.FileSizeBytes, out var list))
                {
                    list = new List<AudioClipData>();
                    bySize[clip.FileSizeBytes] = list;
                }
                list.Add(clip);
            }

            foreach (var group in bySize.Values)
            {
                if (group.Count < 2) continue;
                var key = string.Join("|", group.Select(g => g.FileSizeBytes.ToString()));
                foreach (var c in group)
                {
                    c.IsDuplicate = true;
                    c.DuplicateGroupKey = key;
                    c.DuplicatePaths = group.Where(g => g != c).Select(g => g.Path).ToList();
                    c.TrySetWarningLevel(1);
                }
            }
        }
    }
}

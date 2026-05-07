using System.Collections.Generic;
using UnityScanner.UI.Controls;
using UnityEngine;

namespace UnityScanner.Categories.AudioAnalysis
{
    public class AudioClipData : USItemDataBase
    {
        public string Path;
        public string Name;
        public AudioClip Clip;
        public long FileSizeBytes;
        public float Duration;
        public int SampleRate;
        public int Channels;
        public string LoadType;
        public string CompressionFormat;
        public string MixerGroup;
        public bool IsDuplicate;
        public string DuplicateGroupKey;
        public List<string> DuplicatePaths = new List<string>();
        public bool Foldout;
    }
}

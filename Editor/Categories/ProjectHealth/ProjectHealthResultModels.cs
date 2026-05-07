using UnityScanner.UI.Controls;

namespace UnityScanner.Categories.ProjectHealth
{
    public enum ProjectHealthIssueType
    {
        EmptyFolder,
        MetaOnlyFolder,
        OrphanedMeta,
        BrokenAsset,
        EmptyScene,
        DeepNesting,
        LargeFolder
    }

    public class ProjectHealthEntry : USItemDataBase
    {
        public string Path;
        public string Name;
        public ProjectHealthIssueType IssueType;
        public long FileSizeBytes;
        public string Detail;
    }
}

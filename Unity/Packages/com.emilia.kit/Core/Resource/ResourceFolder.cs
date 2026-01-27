#if UNITY_EDITOR
using System;
using Emilia.Kit.Editor;
using Sirenix.OdinInspector;

namespace Emilia.Kit
{
    [Serializable]
    public class ResourceFolder
    {
        [LabelText("路径过滤")]
        public string pathFilter;

        [LabelText("文件夹")]
        public FolderAsset folderAsset;
    }
}
#endif
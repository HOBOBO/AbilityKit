#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using AbilityKit.BattleFlow;
using UnityEditor;

namespace AbilityKit.BattleFlow.Editor
{
    /// <summary>
    /// 模板库持久化：把复合积木模板（其原子子积木）存成 JSON 文件，编辑器启动时加载并注册回调色板「模板」分类。
    /// 复合积木的 Id/DisplayName/Children 是 init 属性、不经 BattleFlowCodec 往返，故名字用文件名承载，加载时重建复合积木。
    /// </summary>
    public static class BattleFlowTemplateStore
    {
        public const string Category = "模板";
        private static readonly string DirectoryPath = "Assets/BattleFlowTemplates";

        [InitializeOnLoadMethod]
        private static void LoadAll()
        {
            if (!Directory.Exists(DirectoryPath)) return;
            foreach (var file in Directory.GetFiles(DirectoryPath, "*.json"))
            {
                try
                {
                    var doc = BattleFlowCodec.Load(file);
                    var name = Path.GetFileNameWithoutExtension(file);
                    BattleBlockPalette.Register(Category, new BattleCompositeBlock
                    {
                        Id = name,
                        DisplayName = name,
                        Children = doc.Blocks,
                    });
                }
                catch
                {
                    // 损坏的模板文件跳过，不影响其余模板加载。
                }
            }
        }

        /// <summary>把一组积木存成具名模板（写到 <c>Assets/BattleFlowTemplates/&lt;name&gt;.json</c>）。</summary>
        public static void Save(string name, IReadOnlyList<BattleBlock> blocks)
        {
            Directory.CreateDirectory(DirectoryPath);
            var path = Path.Combine(DirectoryPath, name + ".json");
            BattleFlowCodec.Save(path, new BattleFlowDocument { CaseId = name, Blocks = new List<BattleBlock>(blocks) });
        }
    }
}
#endif

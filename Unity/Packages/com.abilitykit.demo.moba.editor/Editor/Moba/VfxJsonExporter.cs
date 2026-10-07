#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    public static class VfxJsonExporter
    {
        [MenuItem("Tools/AbilityKit/Demos/Moba/Vfx/Export Vfx Json")]
        public static void ExportSelected()
        {
            ExportFromFolder(null);
        }

        [MenuItem("Tools/AbilityKit/Demos/Moba/Vfx/Export Vfx Json (Assets)")]
        public static void ExportAll()
        {
            ExportFromFolder(null);
        }

        public static void ExportFromFolder(string assetFolder)
        {
            Debug.LogError("VFX config is authored in LubanConfig/Moba/Production/Datas/T-特效表.xlsx. Run python LubanConfig/Moba/export_pipeline.py --apply to publish.");
        }
    }
}
#endif

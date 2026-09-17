using System;
using System.IO;
using System.Linq;
using AbilityKit.Demo.Moba.Share.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    public static class XiaoQiaoTimelineFlowSync
    {
        private const int SkillId = 10020101;
        private const string SoPath =
            "Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/SkillFlowCO.asset";
        private const string ResourceRoot =
            "Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/";

        [MenuItem("Tools/AbilityKit/Demos/Moba/ActionEditor/Sync XiaoQiao Skill 1 Flow")]
        public static void Sync()
        {
            var source = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(SoPath);
            var flow = source?.dataList?.SingleOrDefault(entry => entry != null && entry.Id == SkillId);
            if (flow == null) throw new InvalidOperationException("XiaoQiao skill flow CO is missing.");
            var dto = flow.ToDto();
            var phases = dto.Phases;
            if (phases == null || phases.Length != 3 || phases[2]?.Timeline == null ||
                phases[0]?.PhaseId != "skill_10020101_release" ||
                phases[1]?.PhaseId != "skill_10020101_commit")
                throw new InvalidDataException("XiaoQiao release/commit/timeline phase layout changed.");
            var timeline = JToken.FromObject(phases[2].Timeline);
            SyncFile(ResourceRoot + "skill_flows/skill_flows_10020101.json", timeline, false);
            SyncFile(ResourceRoot + "skill_flows.json", timeline, true);
            AssetDatabase.Refresh();
            Debug.Log("[ActionEditor] Synced XiaoQiao skill 1 authoritative flow.");
        }

        private static void SyncFile(string assetPath, JToken timeline, bool aggregate)
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            var token = JToken.Parse(File.ReadAllText(path));
            var flow = aggregate
                ? ((JArray)token).OfType<JObject>().SingleOrDefault(entry => (int?)entry["Id"] == SkillId)
                : token as JObject;
            if (flow == null || (int?)flow["Id"] != SkillId)
                throw new InvalidDataException("XiaoQiao skill flow JSON is missing: " + assetPath);
            var phases = flow["Phases"] as JArray;
            if (phases == null || phases.Count != 3 || (int?)phases[2]?["Type"] != 2)
                throw new InvalidDataException("XiaoQiao timeline phase is missing: " + assetPath);
            if (JToken.DeepEquals(phases[2]?["Timeline"], timeline)) return;
            ((JObject)phases[2])["Timeline"] = timeline.DeepClone();
            File.WriteAllText(path, token.ToString(Formatting.Indented));
        }
    }
}

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Editor;
using AbilityKit.Demo.Moba.Share.Config;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Editor.BattleFlow
{
    internal enum MobaBattleFlowConfigKind
    {
        Hero,
        AttributeTemplate,
        Skill,
        Effect,
        Buff,
        ProjectileLauncher,
        Projectile,
        Aoe,
        Summon,
        SearchQuery,
    }

    internal sealed class MobaBattleFlowConfigEntry
    {
        public MobaBattleFlowConfigEntry(
            int id,
            string name,
            MobaBattleFlowConfigKind kind,
            string group = "",
            int heroId = 0,
            int slot = 0,
            int attributeTemplateId = 0)
        {
            Id = id;
            Name = name ?? string.Empty;
            Kind = kind;
            Group = group ?? string.Empty;
            HeroId = heroId;
            Slot = slot;
            AttributeTemplateId = attributeTemplateId;
        }

        public int Id { get; }
        public string Name { get; }
        public MobaBattleFlowConfigKind Kind { get; }
        public string Group { get; }
        public int HeroId { get; }
        public int Slot { get; }
        public int AttributeTemplateId { get; }
        public string Label => string.IsNullOrWhiteSpace(Name) ? $"[{Id}]" : $"{Name} [{Id}]";
    }

    /// <summary>Cached editor index over the MOBA config assets used by BattleFlow authoring controls.</summary>
    internal static class MobaBattleFlowConfigCatalog
    {
        private const string EffectsJsonPath =
            "Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/effects.json";
        private const string SkillFlowsJsonPath =
            "Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/skill_flows.json";
        private const string ProjectileLaunchersJsonPath =
            "Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/projectile_launchers.json";
        private const string ProjectilesJsonPath =
            "Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/projectiles.json";
        private const string AoesJsonPath =
            "Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/aoes.json";
        private const string SummonsJsonPath =
            "Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/summons.json";
        private const string SearchQueryTemplatesJsonPath =
            "Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/search_query_templates.json";

        private static readonly IReadOnlyList<MobaBattleFlowConfigEntry> Empty =
            Array.Empty<MobaBattleFlowConfigEntry>();

        private static Snapshot _snapshot;

        static MobaBattleFlowConfigCatalog()
        {
            EditorApplication.projectChanged += Invalidate;
        }

        public static IReadOnlyList<MobaBattleFlowConfigEntry> Heroes => Current.Heroes;
        public static IReadOnlyList<MobaBattleFlowConfigEntry> AttributeTemplates => Current.AttributeTemplates;
        public static IReadOnlyList<MobaBattleFlowConfigEntry> Skills => Current.Skills;
        public static IReadOnlyList<MobaBattleFlowConfigEntry> Effects => Current.Effects;
        public static IReadOnlyList<MobaBattleFlowConfigEntry> Buffs => Current.Buffs;
        public static IReadOnlyList<MobaBattleFlowConfigEntry> ProjectileLaunchers => Current.ProjectileLaunchers;
        public static IReadOnlyList<MobaBattleFlowConfigEntry> Projectiles => Current.Projectiles;
        public static IReadOnlyList<MobaBattleFlowConfigEntry> Aoes => Current.Aoes;
        public static IReadOnlyList<MobaBattleFlowConfigEntry> Summons => Current.Summons;
        public static IReadOnlyList<MobaBattleFlowConfigEntry> SearchQueries => Current.SearchQueries;

        public static void Refresh()
        {
            _snapshot = BuildSnapshot();
        }

        public static void Invalidate()
        {
            _snapshot = null;
        }

        public static MobaBattleFlowConfigEntry FindHero(int id) =>
            Current.Heroes.FirstOrDefault(entry => entry.Id == id);

        public static MobaBattleFlowConfigEntry FindAttributeTemplate(int id) =>
            Current.AttributeTemplates.FirstOrDefault(entry => entry.Id == id);

        public static MobaBattleFlowConfigEntry FindSkill(int id) =>
            Current.Skills.FirstOrDefault(entry => entry.Id == id);

        public static MobaBattleFlowConfigEntry FindEffect(int id) =>
            Current.Effects.FirstOrDefault(entry => entry.Id == id);

        public static MobaBattleFlowConfigEntry FindBuff(int id) =>
            Current.Buffs.FirstOrDefault(entry => entry.Id == id);

        public static MobaBattleFlowConfigEntry FindProjectileLauncher(int id) =>
            Current.ProjectileLaunchers.FirstOrDefault(entry => entry.Id == id);

        public static MobaBattleFlowConfigEntry FindProjectile(int id) =>
            Current.Projectiles.FirstOrDefault(entry => entry.Id == id);

        public static MobaBattleFlowConfigEntry FindAoe(int id) =>
            Current.Aoes.FirstOrDefault(entry => entry.Id == id);

        public static MobaBattleFlowConfigEntry FindSummon(int id) =>
            Current.Summons.FirstOrDefault(entry => entry.Id == id);

        public static MobaBattleFlowConfigEntry FindSearchQuery(int id) =>
            Current.SearchQueries.FirstOrDefault(entry => entry.Id == id);

        public static IReadOnlyList<MobaBattleFlowConfigEntry> SkillChoices(
            int heroId,
            int attributeTemplateId)
        {
            var choices = Current.SkillChoices;
            if (heroId > 0 && attributeTemplateId > 0)
            {
                var exact = choices
                    .Where(entry => entry.HeroId == heroId &&
                                    entry.AttributeTemplateId == attributeTemplateId)
                    .ToArray();
                if (exact.Length != 0) return exact;
            }

            if (attributeTemplateId > 0)
            {
                var byTemplate = choices
                    .Where(entry => entry.AttributeTemplateId == attributeTemplateId)
                    .ToArray();
                if (byTemplate.Length != 0) return byTemplate;
            }

            if (heroId > 0)
            {
                var byHero = choices.Where(entry => entry.HeroId == heroId).ToArray();
                if (byHero.Length != 0) return byHero;
            }

            return choices;
        }

        public static IReadOnlyList<MobaBattleFlowConfigEntry> TraceConfigChoices(string traceKind)
        {
            if (string.IsNullOrWhiteSpace(traceKind)) return Current.AllTraceConfigs;
            if (traceKind.IndexOf("buff", StringComparison.OrdinalIgnoreCase) >= 0)
                return Current.Buffs;
            if (traceKind.IndexOf("skill", StringComparison.OrdinalIgnoreCase) >= 0 ||
                traceKind.IndexOf("cast", StringComparison.OrdinalIgnoreCase) >= 0)
                return Current.Skills;
            if (traceKind.IndexOf("effect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                traceKind.IndexOf("damage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                traceKind.IndexOf("heal", StringComparison.OrdinalIgnoreCase) >= 0)
                return Current.Effects;
            return Current.AllTraceConfigs;
        }

        public static IReadOnlyList<MobaBattleFlowConfigEntry> TraceConfigChoices(
            string traceKind,
            int skillId)
        {
            if (skillId <= 0) return TraceConfigChoices(traceKind);
            if (IsSkillTrace(traceKind))
            {
                var skill = FindSkill(skillId);
                return skill == null
                    ? TraceConfigChoices(traceKind)
                    : new[] { skill };
            }

            if (!IsEffectTrace(traceKind)) return TraceConfigChoices(traceKind);
            var effects = SkillEffects(skillId);
            return effects.Count == 0 ? TraceConfigChoices(traceKind) : effects;
        }

        public static IReadOnlyList<MobaBattleFlowConfigEntry> SkillEffects(int skillId)
        {
            return Current.SkillEffects.TryGetValue(skillId, out var effects) ? effects : Empty;
        }

        public static IReadOnlyList<int> SkillFlowIds(int skillId)
        {
            return Current.SkillFlowIds.TryGetValue(skillId, out var flowIds)
                ? flowIds
                : Array.Empty<int>();
        }

        public static MobaBattleFlowConfigEntry FindSkillChoice(
            int heroId,
            int attributeTemplateId,
            int slot)
        {
            if (slot <= 0 || (heroId <= 0 && attributeTemplateId <= 0)) return null;
            return SkillChoices(heroId, attributeTemplateId)
                .FirstOrDefault(entry => entry.Slot == slot);
        }

        public static int PreferredTraceConfigId(string traceKind, int skillId)
        {
            if (skillId <= 0) return 0;
            if (IsSkillTrace(traceKind)) return skillId;
            if (!IsEffectTrace(traceKind)) return 0;

            var effects = SkillEffects(skillId);
            var sameId = effects.FirstOrDefault(entry => entry.Id == skillId);
            return sameId?.Id ?? effects.FirstOrDefault()?.Id ?? 0;
        }

        public static bool IsTraceConfigCompatible(string traceKind, int skillId, int configId)
        {
            if (skillId <= 0 || configId <= 0) return true;
            if (IsSkillTrace(traceKind)) return skillId == configId;
            if (!IsEffectTrace(traceKind)) return true;

            var effects = SkillEffects(skillId);
            return effects.Count == 0 || effects.Any(entry => entry.Id == configId);
        }

        private static bool IsSkillTrace(string traceKind) =>
            !string.IsNullOrWhiteSpace(traceKind) &&
            (traceKind.IndexOf("skill", StringComparison.OrdinalIgnoreCase) >= 0 ||
             traceKind.IndexOf("cast", StringComparison.OrdinalIgnoreCase) >= 0);

        private static bool IsEffectTrace(string traceKind) =>
            !string.IsNullOrWhiteSpace(traceKind) &&
            (traceKind.IndexOf("effect", StringComparison.OrdinalIgnoreCase) >= 0 ||
             traceKind.IndexOf("damage", StringComparison.OrdinalIgnoreCase) >= 0 ||
             traceKind.IndexOf("heal", StringComparison.OrdinalIgnoreCase) >= 0);

        private static Snapshot Current => _snapshot ??= BuildSnapshot();

        private static Snapshot BuildSnapshot()
        {
            var characters = LoadAssets<CharacterSO, CharacterDTO>(asset => asset.dataList);
            var skills = LoadAssets<SkillSO, SkillDTO>(asset => asset.dataList);
            var attributes = LoadAssets<BattleAttributeTemplateSO, BattleAttributeTemplateDTO>(asset => asset.dataList);
            var buffs = LoadAssets<BuffSO, BuffDTO>(asset => asset.dataList);
            var flows = LoadSkillFlows();

            var skillNames = skills.ToDictionary(
                skill => skill.Id,
                skill => string.IsNullOrWhiteSpace(skill.Name) ? $"技能 {skill.Id}" : skill.Name);
            var heroNames = characters.ToDictionary(
                hero => hero.Id,
                hero => string.IsNullOrWhiteSpace(hero.Name) ? $"英雄 {hero.Id}" : hero.Name);

            var heroEntries = characters
                .Select(hero => new MobaBattleFlowConfigEntry(
                    hero.Id,
                    hero.Name,
                    MobaBattleFlowConfigKind.Hero,
                    "英雄",
                    hero.Id,
                    attributeTemplateId: hero.AttributeTemplateId))
                .ToArray();

            var attributeEntries = attributes
                .Select(template =>
                {
                    var owner = characters.FirstOrDefault(hero => hero.AttributeTemplateId == template.Id);
                    var name = owner == null
                        ? $"属性模板 {template.Id}"
                        : $"{heroNames[owner.Id]} - 属性模板";
                    return new MobaBattleFlowConfigEntry(
                        template.Id,
                        name,
                        MobaBattleFlowConfigKind.AttributeTemplate,
                        "属性模板",
                        owner?.Id ?? 0,
                        attributeTemplateId: template.Id);
                })
                .ToArray();

            var skillEntries = skills
                .Select(skill => new MobaBattleFlowConfigEntry(
                    skill.Id,
                    skill.Name,
                    MobaBattleFlowConfigKind.Skill,
                    "技能"))
                .ToArray();

            var skillChoices = BuildSkillChoices(
                characters,
                attributes,
                skillNames,
                heroNames);
            var effectEntries = LoadEffects();
            var skillDependencies = BuildSkillDependencies(skills, flows, effectEntries);
            var buffEntries = buffs
                .Select(buff => new MobaBattleFlowConfigEntry(
                    buff.Id,
                    buff.Name,
                    MobaBattleFlowConfigKind.Buff,
                    "Buff"))
                .ToArray();
            var projectileLauncherEntries = LoadNamedEntries(
                ProjectileLaunchersJsonPath,
                MobaBattleFlowConfigKind.ProjectileLauncher,
                "投射物/发射器");
            var projectileEntries = LoadNamedEntries(
                ProjectilesJsonPath,
                MobaBattleFlowConfigKind.Projectile,
                "投射物/模板");
            var aoeEntries = LoadNamedEntries(
                AoesJsonPath,
                MobaBattleFlowConfigKind.Aoe,
                "区域");
            var summonEntries = LoadNamedEntries(
                SummonsJsonPath,
                MobaBattleFlowConfigKind.Summon,
                "召唤物");
            var searchQueryEntries = LoadNamedEntries(
                SearchQueryTemplatesJsonPath,
                MobaBattleFlowConfigKind.SearchQuery,
                "目标查询");
            var allTraceConfigs = skillEntries
                .Concat(effectEntries)
                .Concat(buffEntries)
                .ToArray();

            return new Snapshot(
                heroEntries,
                attributeEntries,
                skillEntries,
                effectEntries,
                buffEntries,
                projectileLauncherEntries,
                projectileEntries,
                aoeEntries,
                summonEntries,
                searchQueryEntries,
                skillChoices,
                allTraceConfigs,
                skillDependencies.Effects,
                skillDependencies.FlowIds);
        }

        private static SkillDependencyIndex BuildSkillDependencies(
            IReadOnlyList<SkillDTO> skills,
            IReadOnlyList<SkillFlowDTO> flows,
            IReadOnlyList<MobaBattleFlowConfigEntry> effects)
        {
            var skillsById = skills.ToDictionary(skill => skill.Id);
            var flowsById = flows.ToDictionary(flow => flow.Id);
            var effectsById = effects.ToDictionary(effect => effect.Id);
            var effectIndex = new Dictionary<int, IReadOnlyList<MobaBattleFlowConfigEntry>>();
            var flowIndex = new Dictionary<int, IReadOnlyList<int>>();

            foreach (var skill in skills)
            {
                var effectIds = new List<int>();
                var flowIds = new List<int>();
                CollectSkillDependencies(
                    skill.Id,
                    skillsById,
                    flowsById,
                    new HashSet<int>(),
                    new HashSet<int>(),
                    effectIds,
                    flowIds);
                effectIndex[skill.Id] = effectIds
                    .Where(effectsById.ContainsKey)
                    .Select(id => effectsById[id])
                    .ToArray();
                flowIndex[skill.Id] = flowIds.ToArray();
            }

            return new SkillDependencyIndex(effectIndex, flowIndex);
        }

        private static void CollectSkillDependencies(
            int skillId,
            IReadOnlyDictionary<int, SkillDTO> skills,
            IReadOnlyDictionary<int, SkillFlowDTO> flows,
            ISet<int> visitedSkills,
            ISet<int> visitedFlows,
            ICollection<int> effectIds,
            ICollection<int> flowIds)
        {
            if (skillId <= 0 || !visitedSkills.Add(skillId) || !skills.TryGetValue(skillId, out var skill))
                return;
            CollectFlowDependencies(skill.PreCastFlowId, skills, flows, visitedSkills, visitedFlows, effectIds, flowIds);
            CollectFlowDependencies(skill.CastFlowId, skills, flows, visitedSkills, visitedFlows, effectIds, flowIds);
        }

        private static void CollectFlowDependencies(
            int flowId,
            IReadOnlyDictionary<int, SkillDTO> skills,
            IReadOnlyDictionary<int, SkillFlowDTO> flows,
            ISet<int> visitedSkills,
            ISet<int> visitedFlows,
            ICollection<int> effectIds,
            ICollection<int> flowIds)
        {
            if (flowId <= 0 || !visitedFlows.Add(flowId) || !flows.TryGetValue(flowId, out var flow)) return;
            AddDistinct(flowIds, flowId);
            CollectPhaseDependencies(flow.Phases, skills, flows, visitedSkills, visitedFlows, effectIds, flowIds);
        }

        private static void CollectPhaseDependencies(
            IReadOnlyList<SkillPhaseDTO> phases,
            IReadOnlyDictionary<int, SkillDTO> skills,
            IReadOnlyDictionary<int, SkillFlowDTO> flows,
            ISet<int> visitedSkills,
            ISet<int> visitedFlows,
            ICollection<int> effectIds,
            ICollection<int> flowIds)
        {
            if (phases == null) return;
            foreach (var phase in phases)
            {
                if (phase == null) continue;
                if (phase.Timeline?.Events != null)
                {
                    foreach (var timelineEvent in phase.Timeline.Events)
                    {
                        if (timelineEvent != null && timelineEvent.EffectId > 0)
                            AddDistinct(effectIds, timelineEvent.EffectId);
                    }
                }

                CollectPhaseDependencies(
                    phase.Children,
                    skills,
                    flows,
                    visitedSkills,
                    visitedFlows,
                    effectIds,
                    flowIds);
                if (phase.Repeat?.Phase != null)
                {
                    CollectPhaseDependencies(
                        new[] { phase.Repeat.Phase },
                        skills,
                        flows,
                        visitedSkills,
                        visitedFlows,
                        effectIds,
                        flowIds);
                }
                if (phase.DerivedSkill?.SkillId > 0)
                {
                    CollectSkillDependencies(
                        phase.DerivedSkill.SkillId,
                        skills,
                        flows,
                        visitedSkills,
                        visitedFlows,
                        effectIds,
                        flowIds);
                }
            }
        }

        private static void AddDistinct(ICollection<int> values, int value)
        {
            if (!values.Contains(value)) values.Add(value);
        }

        private static IReadOnlyList<MobaBattleFlowConfigEntry> BuildSkillChoices(
            IReadOnlyList<CharacterDTO> characters,
            IReadOnlyList<BattleAttributeTemplateDTO> attributes,
            IReadOnlyDictionary<int, string> skillNames,
            IReadOnlyDictionary<int, string> heroNames)
        {
            var result = new List<MobaBattleFlowConfigEntry>();
            var coveredTemplates = new HashSet<int>();
            foreach (var hero in characters)
            {
                var template = attributes.FirstOrDefault(item => item.Id == hero.AttributeTemplateId);
                var ids = template?.ActiveSkills != null && template.ActiveSkills.Length != 0
                    ? template.ActiveSkills
                    : hero.SkillIds ?? Array.Empty<int>();
                if (template != null) coveredTemplates.Add(template.Id);
                AddSkillChoices(result, ids, hero.Id, hero.AttributeTemplateId, heroNames[hero.Id], skillNames);
            }

            foreach (var template in attributes)
            {
                if (coveredTemplates.Contains(template.Id)) continue;
                AddSkillChoices(
                    result,
                    template.ActiveSkills,
                    0,
                    template.Id,
                    $"属性模板 {template.Id}",
                    skillNames);
            }

            return result;
        }

        private static void AddSkillChoices(
            ICollection<MobaBattleFlowConfigEntry> result,
            IReadOnlyList<int> skillIds,
            int heroId,
            int attributeTemplateId,
            string ownerName,
            IReadOnlyDictionary<int, string> skillNames)
        {
            if (skillIds == null) return;
            for (var index = 0; index < skillIds.Count; index++)
            {
                var skillId = skillIds[index];
                skillNames.TryGetValue(skillId, out var skillName);
                result.Add(new MobaBattleFlowConfigEntry(
                    skillId,
                    $"槽位 {index + 1} - {skillName ?? $"技能 {skillId}"}",
                    MobaBattleFlowConfigKind.Skill,
                    ownerName,
                    heroId,
                    index + 1,
                    attributeTemplateId));
            }
        }

        private static TEntry[] LoadAssets<TAsset, TEntry>(Func<TAsset, TEntry[]> entries)
            where TAsset : UnityEngine.Object
            where TEntry : class
        {
            var byId = new Dictionary<int, TEntry>();
            var idField = typeof(TEntry).GetField("Id");
            if (idField == null) return Array.Empty<TEntry>();

            var guids = AssetDatabase.FindAssets($"t:{typeof(TAsset).Name}");
            Array.Sort(guids, StringComparer.Ordinal);
            foreach (var guid in guids)
            {
                var asset = AssetDatabase.LoadAssetAtPath<TAsset>(AssetDatabase.GUIDToAssetPath(guid));
                var values = asset == null ? null : entries(asset);
                if (values == null) continue;
                foreach (var value in values)
                {
                    if (value == null) continue;
                    var id = (int)idField.GetValue(value);
                    if (id > 0 && !byId.ContainsKey(id)) byId.Add(id, value);
                }
            }

            return byId.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToArray();
        }

        private static SkillFlowDTO[] LoadSkillFlows()
        {
            var byId = new Dictionary<int, SkillFlowDTO>();
            var guids = AssetDatabase.FindAssets($"t:{nameof(SkillFlowSO)}");
            Array.Sort(guids, StringComparer.Ordinal);
            foreach (var guid in guids)
            {
                var asset = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                try
                {
                    foreach (var value in asset.GetEntries() ?? Array.Empty<SkillFlowDTO>())
                    {
                        if (!(value is SkillFlowDTO flow) || flow.Id <= 0 || byId.ContainsKey(flow.Id)) continue;
                        byId.Add(flow.Id, flow);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"[BattleFlow] Failed to index SkillFlow asset {asset.name}: {exception.Message}");
                }
            }

            foreach (var flow in LoadJsonArray<SkillFlowDTO>(SkillFlowsJsonPath))
            {
                if (flow != null && flow.Id > 0 && !byId.ContainsKey(flow.Id)) byId.Add(flow.Id, flow);
            }
            return byId.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToArray();
        }

        private static T[] LoadJsonArray<T>(string projectRelativePath)
        {
            try
            {
                var absolutePath = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", projectRelativePath));
                return File.Exists(absolutePath)
                    ? JsonConvert.DeserializeObject<T[]>(File.ReadAllText(absolutePath)) ?? Array.Empty<T>()
                    : Array.Empty<T>();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[BattleFlow] Failed to load {projectRelativePath}: {exception.Message}");
                return Array.Empty<T>();
            }
        }

        private static IReadOnlyList<MobaBattleFlowConfigEntry> LoadEffects()
        {
            try
            {
                var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", EffectsJsonPath));
                if (!File.Exists(absolutePath)) return Empty;
                var effects = JsonConvert.DeserializeObject<EffectRecord[]>(File.ReadAllText(absolutePath));
                return effects == null
                    ? Empty
                    : effects
                        .Where(effect => effect != null && effect.Id > 0)
                        .OrderBy(effect => effect.Id)
                        .Select(effect => new MobaBattleFlowConfigEntry(
                            effect.Id,
                            effect.Name,
                            MobaBattleFlowConfigKind.Effect,
                            "效果/伤害"))
                        .ToArray();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[BattleFlow] Failed to load effects config: {exception.Message}");
                return Empty;
            }
        }

        private static IReadOnlyList<MobaBattleFlowConfigEntry> LoadNamedEntries(
            string projectRelativePath,
            MobaBattleFlowConfigKind kind,
            string group)
        {
            return LoadJsonArray<NamedConfigRecord>(projectRelativePath)
                .Where(entry => entry != null && entry.Id > 0)
                .OrderBy(entry => entry.Id)
                .Select(entry => new MobaBattleFlowConfigEntry(
                    entry.Id,
                    entry.Name,
                    kind,
                    group))
                .ToArray();
        }

        [Serializable]
        private sealed class EffectRecord
        {
            public int Id;
            public string Name;
        }

        [Serializable]
        private sealed class NamedConfigRecord
        {
            public int Id;
            public string Name;
        }

        private sealed class Snapshot
        {
            public Snapshot(
                IReadOnlyList<MobaBattleFlowConfigEntry> heroes,
                IReadOnlyList<MobaBattleFlowConfigEntry> attributeTemplates,
                IReadOnlyList<MobaBattleFlowConfigEntry> skills,
                IReadOnlyList<MobaBattleFlowConfigEntry> effects,
                IReadOnlyList<MobaBattleFlowConfigEntry> buffs,
                IReadOnlyList<MobaBattleFlowConfigEntry> projectileLaunchers,
                IReadOnlyList<MobaBattleFlowConfigEntry> projectiles,
                IReadOnlyList<MobaBattleFlowConfigEntry> aoes,
                IReadOnlyList<MobaBattleFlowConfigEntry> summons,
                IReadOnlyList<MobaBattleFlowConfigEntry> searchQueries,
                IReadOnlyList<MobaBattleFlowConfigEntry> skillChoices,
                IReadOnlyList<MobaBattleFlowConfigEntry> allTraceConfigs,
                IReadOnlyDictionary<int, IReadOnlyList<MobaBattleFlowConfigEntry>> skillEffects,
                IReadOnlyDictionary<int, IReadOnlyList<int>> skillFlowIds)
            {
                Heroes = heroes;
                AttributeTemplates = attributeTemplates;
                Skills = skills;
                Effects = effects;
                Buffs = buffs;
                ProjectileLaunchers = projectileLaunchers;
                Projectiles = projectiles;
                Aoes = aoes;
                Summons = summons;
                SearchQueries = searchQueries;
                SkillChoices = skillChoices;
                AllTraceConfigs = allTraceConfigs;
                SkillEffects = skillEffects;
                SkillFlowIds = skillFlowIds;
            }

            public IReadOnlyList<MobaBattleFlowConfigEntry> Heroes { get; }
            public IReadOnlyList<MobaBattleFlowConfigEntry> AttributeTemplates { get; }
            public IReadOnlyList<MobaBattleFlowConfigEntry> Skills { get; }
            public IReadOnlyList<MobaBattleFlowConfigEntry> Effects { get; }
            public IReadOnlyList<MobaBattleFlowConfigEntry> Buffs { get; }
            public IReadOnlyList<MobaBattleFlowConfigEntry> ProjectileLaunchers { get; }
            public IReadOnlyList<MobaBattleFlowConfigEntry> Projectiles { get; }
            public IReadOnlyList<MobaBattleFlowConfigEntry> Aoes { get; }
            public IReadOnlyList<MobaBattleFlowConfigEntry> Summons { get; }
            public IReadOnlyList<MobaBattleFlowConfigEntry> SearchQueries { get; }
            public IReadOnlyList<MobaBattleFlowConfigEntry> SkillChoices { get; }
            public IReadOnlyList<MobaBattleFlowConfigEntry> AllTraceConfigs { get; }
            public IReadOnlyDictionary<int, IReadOnlyList<MobaBattleFlowConfigEntry>> SkillEffects { get; }
            public IReadOnlyDictionary<int, IReadOnlyList<int>> SkillFlowIds { get; }
        }

        private sealed class SkillDependencyIndex
        {
            public SkillDependencyIndex(
                IReadOnlyDictionary<int, IReadOnlyList<MobaBattleFlowConfigEntry>> effects,
                IReadOnlyDictionary<int, IReadOnlyList<int>> flowIds)
            {
                Effects = effects;
                FlowIds = flowIds;
            }

            public IReadOnlyDictionary<int, IReadOnlyList<MobaBattleFlowConfigEntry>> Effects { get; }
            public IReadOnlyDictionary<int, IReadOnlyList<int>> FlowIds { get; }
        }
    }
}
#endif

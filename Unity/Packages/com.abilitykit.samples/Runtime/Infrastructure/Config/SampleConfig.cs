#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AbilityKit.GameplayTags;

namespace AbilityKit.Samples.Logic.Infrastructure.Config
{
    /// <summary>
    /// 强类型配置访问器
    /// </summary>
    public static class SampleConfig
    {
        private static readonly SampleConfigLoader _loader = SampleConfigLoader.Instance;

        /// <summary>
        /// 加载管线配置
        /// </summary>
        public static List<T> LoadPipelines<T>() where T : class
        {
            try
            {
                var provider = _loader.Load(ConfigPaths.PipelineConfig);
                return provider.GetSectionOrDefault<List<T>>("pipelineConfigs") ?? new List<T>();
            }
            catch (FileNotFoundException)
            {
                return CreateDefaultPipelines<T>();
            }
        }

        /// <summary>
        /// 鍔犺浇鏍囩缁勯厤缃?        /// </summary>
        public static List<TagGroupConfig> LoadTagGroups()
        {
            try
            {
                var provider = _loader.Load(ConfigPaths.TagsConfig);
                return provider.GetSection<List<TagGroupConfig>>(ConfigSections.TagGroups);
            }
            catch (FileNotFoundException)
            {
                return CreateDefaultTagGroups();
            }
        }

        /// <summary>
        /// 鍔犺浇鏍囩缁勫苟娉ㄥ唽鍒?GameplayTagManager
        /// </summary>
        public static List<GameplayTag> LoadAndRegisterTags()
        {
            var groups = LoadTagGroups();
            var registeredTags = new List<GameplayTag>();

            foreach (var group in groups)
            {
                foreach (var tagName in group.Tags)
                {
                    var tag = GameplayTagManager.Instance.RequestTag(tagName);
                    registeredTags.Add(tag);
                }
            }

            return registeredTags;
        }

        /// <summary>
        /// 加载角色标签配置
        /// </summary>
        public static List<T> LoadCharacterTags<T>() where T : class
        {
            var provider = _loader.Load(ConfigPaths.SampleConfigs);
            return provider.GetSectionOrDefault<List<T>>(ConfigSections.CharacterTags) ?? new List<T>();
        }

        /// <summary>
        /// 通用配置加载方法
        /// </summary>
        public static List<T> Load<T>(string filePath, string sectionName) where T : class
        {
            var provider = _loader.Load(filePath);
            return provider.GetSectionOrDefault<List<T>>(sectionName) ?? new List<T>();
        }

        private static List<T> CreateDefaultPipelines<T>() where T : class
        {
            var result = new List<T>();
            var skillPipeline = CreatePipelineDefinition<T>(
                1,
                "技能释放管线",
                "演示技能释放的完整流程",
                new[]
                {
                    ("PreCheck", "Instant", 0f, "预检查：目标是否有效、距离是否满足、冷却是否完成"),
                    ("Validation", "Instant", 0f, "验证：检查资源是否足够、是否被沉默"),
                    ("Casting", "Timed", 1.5f, "引导：播放施法动画，玩家可以取消"),
                    ("Execute", "Timed", 0.2f, "执行：造成伤害、触发效果"),
                    ("Cooldown", "Timed", 0.5f, "收尾：进入冷却并等待下一次释放")
                });

            if (skillPipeline != null)
            {
                result.Add(skillPipeline);
            }

            return result;
        }

        private static T? CreatePipelineDefinition<T>(int id, string name, string description, (string Name, string Type, float Duration, string Description)[] phases) where T : class
        {
            var pipeline = Activator.CreateInstance(typeof(T)) as T;
            if (pipeline == null)
            {
                return null;
            }

            SetProperty(pipeline, "Id", id);
            SetProperty(pipeline, "Name", name);
            SetProperty(pipeline, "Description", description);

            var phasesProperty = typeof(T).GetProperty("Phases");
            var phaseType = phasesProperty?.PropertyType.GetGenericArguments()[0];
            if (phasesProperty != null && phaseType != null)
            {
                var listType = typeof(List<>).MakeGenericType(phaseType);
                var phaseList = Activator.CreateInstance(listType) as IList;
                if (phaseList != null)
                {
                    foreach (var phase in phases)
                    {
                        var phaseDefinition = Activator.CreateInstance(phaseType);
                        if (phaseDefinition == null)
                        {
                            continue;
                        }

                        SetProperty(phaseDefinition, "Name", phase.Name);
                        SetProperty(phaseDefinition, "Type", phase.Type);
                        SetProperty(phaseDefinition, "Duration", phase.Duration);
                        SetProperty(phaseDefinition, "Description", phase.Description);
                        phaseList.Add(phaseDefinition);
                    }

                    phasesProperty.SetValue(pipeline, phaseList);
                }
            }

            return pipeline;
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName);
            if (property?.CanWrite == true)
            {
                property.SetValue(target, value);
            }
        }

        private static List<TagGroupConfig> CreateDefaultTagGroups()
        {
            return new List<TagGroupConfig>
            {
                new TagGroupConfig
                {
                    Id = 1,
                    Name = "Damage",
                    Description = "Damage type tags used by beginner samples.",
                    Tags = new[] { "Damage", "Damage.Fire", "Damage.Fire.Burning", "Damage.Ice" }
                },
                new TagGroupConfig
                {
                    Id = 2,
                    Name = "Buff",
                    Description = "Positive and negative effect tags.",
                    Tags = new[] { "Buff.AttackSpeed", "Buff.MoveSpeed", "Debuff.Stun", "Debuff.Poison", "Debuff.Burning" }
                },
                new TagGroupConfig
                {
                    Id = 3,
                    Name = "Unit",
                    Description = "Unit identity and status tags.",
                    Tags = new[] { "Unit", "Unit.Hero", "Unit.Enemy", "Status.Dead", "Status.Silenced" }
                },
                new TagGroupConfig
                {
                    Id = 4,
                    Name = "Skill",
                    Description = "Skill and cost tags used by requirement samples.",
                    Tags = new[] { "Skill", "Skill.Ultimate", "Cost", "Cost.Mana" }
                }
            };
        }
    }
}

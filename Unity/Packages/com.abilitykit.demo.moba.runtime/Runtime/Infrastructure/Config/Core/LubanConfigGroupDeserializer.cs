using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using Newtonsoft.Json.Linq;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Config.Core
{
    /// <summary>
    /// Luban 配置组反序列化器。
    /// 将 Luban 导出的 JSON 转换为框架的 MO DTO 类型。
    ///
    /// Luban 导出的 JSON 字段名与框架 DTO 不同：
    /// - Luban 使用 "Code"，框架使用 "Id"
    /// - 需要处理字段映射和默认值
    /// </summary>
    public sealed class LubanConfigGroupDeserializer : ConfigGroupDeserializerBase
    {
        public static readonly LubanConfigGroupDeserializer Instance = new LubanConfigGroupDeserializer();

        private LubanConfigGroupDeserializer() { }

        public override Array DeserializeFromBytes(byte[] bytes, Type dtoType)
        {
            throw new NotSupportedException(
                $"[{nameof(LubanConfigGroupDeserializer)}] Bytes deserialization not supported. Use JSON format.");
        }

        public override Array DeserializeFromText(string text, Type dtoType)
        {
            if (string.IsNullOrEmpty(text)) return Array.CreateInstance(dtoType, 0);

            try
            {
                var token = JToken.Parse(text);
                if (token is not JArray array)
                {
                    throw new InvalidOperationException(
                        $"Expected a JSON array root for DTO type '{dtoType.FullName}', but found '{token.Type}'.");
                }

                var list = new List<object>();
                foreach (var item in array)
                {
                    var obj = DeserializeItem(item as JObject, dtoType);
                    if (obj != null) list.Add(obj);
                }

                var result = Array.CreateInstance(dtoType, list.Count);
                for (int i = 0; i < list.Count; i++) result.SetValue(list[i], i);
                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to deserialize {dtoType.Name}: {ex.Message}", ex);
            }
        }

        public override bool CanHandle(Type dtoType)
        {
            return dtoType.Namespace == "AbilityKit.Demo.Moba.Config.BattleDemo.MO";
        }

        private static object DeserializeItem(JObject obj, Type dtoType)
        {
            if (obj == null) return null;

            // 根据类型分发。
            if (dtoType == typeof(CharacterDTO)) return DeserializeCharacter(obj);
            if (dtoType == typeof(BattleAttributeTemplateDTO)) return DeserializeBattleAttributeTemplate(obj);
            if (dtoType == typeof(SkillDTO)) return DeserializeSkill(obj);
            if (dtoType == typeof(PassiveSkillDTO)) return DeserializePassiveSkill(obj);
            if (dtoType == typeof(SkillButtonTemplateDTO)) return DeserializeSkillButtonTemplate(obj);
            if (dtoType == typeof(TagTemplateDTO)) return DeserializeTagTemplate(obj);
            if (dtoType == typeof(ContinuousTagTemplateDTO)) return DeserializeContinuousTagTemplate(obj);
            if (dtoType == typeof(SearchQueryTemplateDTO)) return DeserializeSearchQueryTemplate(obj);
            if (dtoType == typeof(BuffDTO)) return DeserializeBuff(obj);
            if (dtoType == typeof(ContinuousProcessDTO)) return DeserializeContinuousProcess(obj);
            if (dtoType == typeof(ModelDTO)) return DeserializeModel(obj);
            if (dtoType == typeof(ProjectileLauncherDTO)) return DeserializeProjectileLauncher(obj);
            if (dtoType == typeof(ProjectileDTO)) return DeserializeProjectile(obj);
            if (dtoType == typeof(AoeDTO)) return DeserializeAoe(obj);
            if (dtoType == typeof(EmitterDTO)) return DeserializeEmitter(obj);
            if (dtoType == typeof(SummonDTO)) return DeserializeSummon(obj);
            if (dtoType == typeof(SkillFlowDTO)) return DeserializeSkillFlow(obj);
            if (dtoType == typeof(SkillLevelTableDTO)) return DeserializeSkillLevelTable(obj);
            if (dtoType == typeof(ComponentTemplateDTO)) return DeserializeComponentTemplate(obj);
            if (dtoType == typeof(PresentationTemplateDTO)) return DeserializePresentationTemplate(obj);
            if (dtoType == typeof(SpawnSummonActionTemplateDTO)) return DeserializeSpawnSummonActionTemplate(obj);

            // 默认：尝试使用 JObject 反序列化。
            return obj.ToObject(dtoType);
        }

        private static CharacterDTO DeserializeCharacter(JObject obj)
        {
            // Luban: Code, Name, Career, ModelId, AttributeTemplateId
            // 妗嗘灦: Id, Name, SkillIds, PassiveSkillIds, ModelId, AttributeTemplateId
            var dto = new CharacterDTO
            {
                Id = obj["Code"]?.Value<int>() ?? 0,
                Name = obj["Name"]?.Value<string>() ?? string.Empty,
                ModelId = obj["ModelId"]?.Value<int>() ?? 0,
                AttributeTemplateId = obj["AttributeTemplateId"]?.Value<int>() ?? 0,
                SkillIds = obj["ActiveSkills"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                PassiveSkillIds = obj["PassiveSkills"]?.ToObject<int[]>() ?? Array.Empty<int>()
            };
            return dto;
        }

        private static BattleAttributeTemplateDTO DeserializeBattleAttributeTemplate(JObject obj)
        {
            // 框架 DTO: Id, ActiveSkills, PassiveSkills, Hp, MaxHp, ExtraHp, ...
            // 没有 Name 字段。
            var dto = new BattleAttributeTemplateDTO
            {
                Id = obj["Code"]?.Value<int>() ?? obj["Id"]?.Value<int>() ?? 0,
                BasicAttackSkillId = obj["BasicAttackSkillId"]?.Value<int>() ?? 0,
                ActiveSkills = obj["ActiveSkills"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                PassiveSkills = obj["PassiveSkills"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                Hp = obj["Hp"]?.Value<int>() ?? 0,
                MaxHp = obj["MaxHp"]?.Value<int>() ?? 0,
                ExtraHp = obj["ExtraHp"]?.Value<int>() ?? 0,
                PhysicsAttack = obj["PhysicsAttack"]?.Value<int>() ?? 0,
                MagicAttack = obj["MagicAttack"]?.Value<int>() ?? 0,
                ExtraPhysicsAttack = obj["ExtraPhysicsAttack"]?.Value<int>() ?? 0,
                ExtraMagicAttack = obj["ExtraMagicAttack"]?.Value<int>() ?? 0,
                PhysicsDefense = obj["PhysicsDefense"]?.Value<int>() ?? 0,
                MagicDefense = obj["MagicDefense"]?.Value<int>() ?? 0,
                Mana = obj["Mana"]?.Value<int>() ?? 0,
                MaxMana = obj["MaxMana"]?.Value<int>() ?? 0,
                CriticalR = obj["CriticalR"]?.Value<int>() ?? 0,
                AttackSpeedR = obj["AttackSpeedR"]?.Value<int>() ?? 0,
                CooldownReduceR = obj["CooldownReduceR"]?.Value<int>() ?? 0,
                PhysicsPenetrationR = obj["PhysicsPenetrationR"]?.Value<int>() ?? 0,
                MagicPenetrationR = obj["MagicPenetrationR"]?.Value<int>() ?? 0,
                MoveSpeed = obj["MoveSpeed"]?.Value<int>() ?? 0,
                PhysicsBloodsuckingR = obj["PhysicsBloodsuckingR"]?.Value<int>() ?? 0,
                MagicBloodsuckingR = obj["MagicBloodsuckingR"]?.Value<int>() ?? 0,
                AttackRange = obj["AttackRange"]?.Value<int>() ?? 0,
                PerSecondBloodR = obj["PerSecondBloodR"]?.Value<int>() ?? 0,
                PerSecondManaR = obj["PerSecondManaR"]?.Value<int>() ?? 0,
                ResilienceR = obj["ResilienceR"]?.Value<int>() ?? 0
            };
            return dto;
        }

        private static SkillDTO DeserializeSkill(JObject obj)
        {
            var dto = new SkillDTO
            {
                Id = obj["Id"]?.Value<int>() ?? obj["Code"]?.Value<int>() ?? 0,
                Name = obj["Name"]?.Value<string>() ?? string.Empty,
                CooldownMs = obj["CooldownMs"]?.Value<int>() ?? 0,
                Range = obj["Range"]?.Value<int>() ?? 0,
                IconId = obj["IconId"]?.Value<int>() ?? 0,
                Category = obj["Category"]?.Value<int>() ?? 0,
                SkillType = obj["SkillType"]?.Value<int>() ?? 0,
                Tags = obj["Tags"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                SkillButtonTemplateId = obj["SkillButtonTemplateId"]?.Value<int>() ?? 0,
                LevelTableId = obj["LevelTableId"]?.Value<int>() ?? 0,
                PreCastFlowId = obj["PreCastFlowId"]?.Value<int>() ?? 0,
                CastFlowId = obj["CastFlowId"]?.Value<int>() ?? 0
            };
            return dto;
        }

        private static PassiveSkillDTO DeserializePassiveSkill(JObject obj)
        {
            var dto = new PassiveSkillDTO
            {
                Id = obj["Id"]?.Value<int>() ?? obj["Code"]?.Value<int>() ?? 0,
                Name = obj["Name"]?.Value<string>() ?? string.Empty,
                CooldownMs = obj["CooldownMs"]?.Value<int>() ?? 0,
                TriggerIds = obj["TriggerIds"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                ContinuousProcessIds = obj["ContinuousProcessIds"]?.ToObject<int[]>() ?? Array.Empty<int>()
            };
            return dto;
        }

        private static SkillButtonTemplateDTO DeserializeSkillButtonTemplate(JObject obj)
        {
            var dto = new SkillButtonTemplateDTO
            {
                Id = obj["Id"]?.Value<int>() ?? obj["Code"]?.Value<int>() ?? 0,
                Name = obj["Name"]?.Value<string>() ?? string.Empty,
                LongPressSeconds = obj["LongPressSeconds"]?.Value<float>() ?? 0,
                DragThreshold = obj["DragThreshold"]?.Value<float>() ?? 0,
                EnableAim = obj["EnableAim"]?.Value<bool>() ?? false,
                AimMode = obj["AimMode"]?.Value<int>() ?? 0,
                AimMaxRadius = obj["AimMaxRadius"]?.Value<float>() ?? 0,
                IndicatorShape = obj["IndicatorShape"]?.Value<int>() ?? 0,
                IndicatorWorldWidth = obj["IndicatorWorldWidth"]?.Value<float>() ?? 0,
                UsePointMode = obj["UsePointMode"]?.Value<int>() ?? 0,
                SelectRange = obj["SelectRange"]?.Value<float>() ?? 0,
                FaceToAim = obj["FaceToAim"]?.Value<bool>() ?? false
            };
            return dto;
        }

        private static TagTemplateDTO DeserializeTagTemplate(JObject obj)
        {
            var dto = new TagTemplateDTO
            {
                Id = obj["Id"]?.Value<int>() ?? obj["Code"]?.Value<int>() ?? 0,
                Name = obj["Name"]?.Value<string>() ?? string.Empty,
            };

            dto.RequiredTagNames = ReadTagNames(obj["RequiredTags"]);
            dto.BlockedTagNames = ReadTagNames(obj["BlockedTags"]);
            dto.GrantTagNames = ReadTagNames(obj["GrantTags"]);
            dto.RemoveTagNames = ReadTagNames(obj["RemoveTags"]);
            return dto;
        }

        private static ContinuousTagTemplateDTO DeserializeContinuousTagTemplate(JObject obj)
        {
            var dto = new ContinuousTagTemplateDTO
            {
                Id = obj["Id"]?.Value<int>() ?? obj["Code"]?.Value<int>() ?? 0,
                Name = obj["Name"]?.Value<string>() ?? string.Empty,
            };

            dto.ActivationRequiredTagNames = ReadTagNames(obj["ActivationRequiredTags"]);
            dto.ActivationBlockedTagNames = ReadTagNames(obj["ActivationBlockedTags"]);
            dto.ApplicationTagNames = ReadTagNames(obj["ApplicationTags"]);
            dto.RemovalRequiredTagNames = ReadTagNames(obj["RemovalRequiredTags"]);
            dto.RemovalBlockedTagNames = ReadTagNames(obj["RemovalBlockedTags"]);
            dto.OngoingRequiredTagNames = ReadTagNames(obj["OngoingRequiredTags"]);
            dto.OngoingBlockedTagNames = ReadTagNames(obj["OngoingBlockedTags"]);
            dto.RemovalTagNames = ReadTagNames(obj["RemovalTags"]);
            return dto;
        }

        private static string[] ReadTagNames(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return Array.Empty<string>();

            var names = new List<string>();
            IEnumerable<JToken> values = token.Type == JTokenType.Array ? token.Children() : new[] { token };
            foreach (var value in values)
            {
                if (value == null || value.Type == JTokenType.Null) continue;
                var name = value.Value<string>();
                if (!string.IsNullOrWhiteSpace(name)) names.Add(name.Trim());
            }

            return names.Count > 0 ? names.ToArray() : Array.Empty<string>();
        }

        private static SearchQueryTemplateDTO DeserializeSearchQueryTemplate(JObject obj)
        {
            var dto = new SearchQueryTemplateDTO
            {
                Id = obj["Id"]?.Value<int>() ?? obj["Code"]?.Value<int>() ?? 0,
                Name = obj["Name"]?.Value<string>() ?? string.Empty,
                MaxCount = obj["MaxCount"]?.Value<int>() ?? 0,
                ExplicitTargetPolicy = obj["ExplicitTargetPolicy"]?.Value<int>() ?? 0,
                Provider = DeserializeSearchTargetProvider(obj["Provider"]),
                Rules = DeserializeSearchTargetRules(obj["Rules"]),
                Scorer = DeserializeSearchTargetScorer(obj["Scorer"]),
                Selector = DeserializeSearchTargetSelector(obj["Selector"])
            };
            return dto;
        }

        private static SearchTargetProviderDTO DeserializeSearchTargetProvider(JToken token)
        {
            var obj = token as JObject;
            if (obj == null) return null;

            return new SearchTargetProviderDTO
            {
                Id = obj["Id"]?.Value<int>() ?? 0,
                Kind = obj["Kind"]?.Value<int>() ?? 0,
                Param = obj["Param"]?.Value<int>() ?? 0
            };
        }

        private static SearchTargetRuleDTO[] DeserializeSearchTargetRules(JToken token)
        {
            if (token == null || token.Type != JTokenType.Array) return Array.Empty<SearchTargetRuleDTO>();
            var array = token as JArray;
            var result = new List<SearchTargetRuleDTO>();
            foreach (var item in array)
            {
                var obj = item as JObject;
                if (obj == null) continue;
                result.Add(new SearchTargetRuleDTO
                {
                    Id = obj["Id"]?.Value<int>() ?? 0,
                    Kind = obj["Kind"]?.Value<int>() ?? 0,
                    Center = obj["Center"]?.Value<int>() ?? 0,
                    Forward = obj["Forward"]?.Value<int>() ?? 0,
                    Radius = obj["Radius"]?.Value<float>() ?? 0,
                    HalfAngleDeg = obj["HalfAngleDeg"]?.Value<float>() ?? 0,
                    ActorIds = obj["ActorIds"]?.ToObject<int[]>() ?? Array.Empty<int>()
                });
            }
            return result.ToArray();
        }

        private static SearchTargetScorerDTO DeserializeSearchTargetScorer(JToken token)
        {
            var obj = token as JObject;
            if (obj == null) return null;

            return new SearchTargetScorerDTO
            {
                Id = obj["Id"]?.Value<int>() ?? 0,
                Kind = obj["Kind"]?.Value<int>() ?? 0,
                Source = obj["Source"]?.Value<int>() ?? 0,
                RandomSeed = obj["RandomSeed"]?.Value<int>() ?? 0
            };
        }

        private static SearchTargetSelectorDTO DeserializeSearchTargetSelector(JToken token)
        {
            var obj = token as JObject;
            if (obj == null) return null;

            return new SearchTargetSelectorDTO
            {
                Id = obj["Id"]?.Value<int>() ?? 0,
                Kind = obj["Kind"]?.Value<int>() ?? 0
            };
        }

        private static BuffDTO DeserializeBuff(JObject obj)
        {
            var dto = new BuffDTO
            {
                Id = obj["Id"]?.Value<int>() ?? obj["Code"]?.Value<int>() ?? 0,
                Name = obj["Name"]?.Value<string>() ?? string.Empty,
                DurationMs = obj["DurationMs"]?.Value<int>() ?? 0,
                OnAddEffects = obj["OnAddEffects"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                OnRemoveEffects = obj["OnRemoveEffects"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                OnIntervalEffects = obj["OnIntervalEffects"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                IntervalMs = obj["IntervalMs"]?.Value<int>() ?? 0,
                PresentationTemplateId = obj["PresentationTemplateId"]?.Value<int>() ?? 0,
                StackingPolicy = obj["StackingPolicy"]?.Value<int>() ?? 0,
                RefreshPolicy = obj["RefreshPolicy"]?.Value<int>() ?? 0,
                MaxStacks = obj["MaxStacks"]?.Value<int>() ?? 1,
                TriggerIds = obj["TriggerIds"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                ContinuousTagTemplateId = obj["ContinuousTagTemplateId"]?.Value<int>() ?? 0,
                Modifiers = DeserializeContinuousModifiers(obj["Modifiers"])
            };
            dto.TagNames = ReadTagNames(obj["Tags"]);
            return dto;
        }

        private static ContinuousProcessDTO DeserializeContinuousProcess(JObject obj)
        {
            var dto = new ContinuousProcessDTO
            {
                Id = obj["Id"]?.Value<int>() ?? obj["Code"]?.Value<int>() ?? 0,
                Name = obj["Name"]?.Value<string>() ?? string.Empty,
                DurationMs = obj["DurationMs"]?.Value<int>() ?? 0,
                IntervalMs = obj["IntervalMs"]?.Value<int>() ?? 0,
                IntervalTriggerIds = obj["IntervalTriggerIds"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                TriggerIds = obj["TriggerIds"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                ContinuousTagTemplateId = obj["ContinuousTagTemplateId"]?.Value<int>()
                    ?? obj["TagTemplateId"]?.Value<int>()
                    ?? 0,
                Modifiers = DeserializeContinuousModifiers(obj["Modifiers"]),
                RequireOutOfCombat = obj["RequireOutOfCombat"]?.Value<bool>() ?? false,
                OutOfCombatSeconds = obj["OutOfCombatSeconds"]?.Value<int>() ?? 0
            };
            dto.TagNames = ReadTagNames(obj["Tags"]);
            return dto;
        }

        private static ContinuousModifierDTO[] DeserializeContinuousModifiers(JToken token)
        {
            if (token == null) return Array.Empty<ContinuousModifierDTO>();

            var modifiers = token.ToObject<ContinuousModifierDTO[]>() ?? Array.Empty<ContinuousModifierDTO>();
            for (int i = 0; i < modifiers.Length; i++)
            {
                var modifier = modifiers[i];
                if (modifier == null) continue;
                if (modifier.TargetKind == 0 && modifier.AttrTypeId != 0)
                {
                    modifier.TargetKind = 1;
                }

                if (modifier.TargetId == 0 && modifier.AttrTypeId != 0)
                {
                    modifier.TargetId = modifier.AttrTypeId;
                }
            }

            return modifiers;
        }

        private static ModelDTO DeserializeModel(JObject obj)
        {
            // 妗嗘灦 DTO: Id, PrefabPath, Scale (娌℃湁 Name, IconPath)
            var dto = new ModelDTO
            {
                Id = obj["Id"]?.Value<int>() ?? obj["Code"]?.Value<int>() ?? 0,
                PrefabPath = obj["PrefabPath"]?.Value<string>() ?? string.Empty,
                Scale = obj["Scale"]?.Value<float>() ?? 1.0f
            };
            return dto;
        }

        private static ProjectileLauncherDTO DeserializeProjectileLauncher(JObject obj)
        {
            var dto = new ProjectileLauncherDTO
            {
                Id = obj["Id"]?.Value<int>() ?? obj["Code"]?.Value<int>() ?? 0,
                Name = obj["Name"]?.Value<string>() ?? string.Empty,
                EmitterType = obj["EmitterType"]?.Value<int>() ?? 0,
                DurationMs = obj["DurationMs"]?.Value<int>() ?? 0,
                IntervalMs = obj["IntervalMs"]?.Value<int>() ?? 0,
                CountPerShot = obj["CountPerShot"]?.Value<int>() ?? 1,
                FanAngleDeg = obj["FanAngleDeg"]?.Value<float>() ?? 0
            };
            return dto;
        }

        private static ProjectileDTO DeserializeProjectile(JObject obj)
        {
            // 妗嗘灦 DTO: Id, Name, VfxId, Speed, LifetimeMs, MaxDistance, HitPolicyKind, HitsRemaining, HitCooldownMs, TickIntervalMs, OnHitEffectId, OnSpawnVfxId, OnHitVfxId, OnExpireVfxId, ReturnAfterMs, ReturnSpeed, ReturnStopDistance
            var dto = new ProjectileDTO
            {
                Id = obj["Id"]?.Value<int>() ?? obj["Code"]?.Value<int>() ?? 0,
                Name = obj["Name"]?.Value<string>() ?? string.Empty,
                VfxId = obj["VfxId"]?.Value<int>() ?? 0,
                Speed = obj["Speed"]?.Value<float>() ?? 0,
                LifetimeMs = obj["LifetimeMs"]?.Value<int>() ?? 0,
                MaxDistance = obj["MaxDistance"]?.Value<float>() ?? 0,
                CollisionWidth = obj["CollisionWidth"]?.Value<float>() ?? 0,
                CollisionHeight = obj["CollisionHeight"]?.Value<float>() ?? 0,
                CollisionLength = obj["CollisionLength"]?.Value<float>() ?? 0,
                HitPolicyKind = obj["HitPolicyKind"]?.Value<int>() ?? 0,
                HitsRemaining = obj["HitsRemaining"]?.Value<int>() ?? 0,
                HitCooldownMs = obj["HitCooldownMs"]?.Value<int>() ?? 0,
                TickIntervalMs = obj["TickIntervalMs"]?.Value<int>() ?? 0,
                OnHitEffectId = obj["OnHitEffectId"]?.Value<int>() ?? 0,
                OnSpawnTriggerIds = obj["OnSpawnTriggerIds"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                OnHitTriggerIds = obj["OnHitTriggerIds"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                OnTickTriggerIds = obj["OnTickTriggerIds"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                OnExitTriggerIds = obj["OnExitTriggerIds"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                OnSpawnVfxId = obj["OnSpawnVfxId"]?.Value<int>() ?? 0,
                OnHitVfxId = obj["OnHitVfxId"]?.Value<int>() ?? 0,
                OnExpireVfxId = obj["OnExpireVfxId"]?.Value<int>() ?? 0,
                ReturnAfterMs = obj["ReturnAfterMs"]?.Value<int>() ?? 0,
                ReturnSpeed = obj["ReturnSpeed"]?.Value<float>() ?? 0,
                ReturnStopDistance = obj["ReturnStopDistance"]?.Value<float>() ?? 0,
                PrepareMotionMode = obj["PrepareMotionMode"]?.Value<int>() ?? 0,
                PrepareMs = obj["PrepareMs"]?.Value<int>() ?? 0,
                HoldMs = obj["HoldMs"]?.Value<int>() ?? 0,
                PrepareOffsetX = obj["PrepareOffsetX"]?.Value<float>() ?? 0,
                PrepareOffsetY = obj["PrepareOffsetY"]?.Value<float>() ?? 0,
                PrepareOffsetZ = obj["PrepareOffsetZ"]?.Value<float>() ?? 0,
                PrepareSlotSpacing = obj["PrepareSlotSpacing"]?.Value<float>() ?? 0,
                SpawnRandomOffsetX = obj["SpawnRandomOffsetX"]?.Value<float>() ?? 0,
                SpawnRandomOffsetY = obj["SpawnRandomOffsetY"]?.Value<float>() ?? 0,
                SpawnRandomOffsetZ = obj["SpawnRandomOffsetZ"]?.Value<float>() ?? 0,
                PrepareRandomOffsetX = obj["PrepareRandomOffsetX"]?.Value<float>() ?? 0,
                PrepareRandomOffsetY = obj["PrepareRandomOffsetY"]?.Value<float>() ?? 0,
                PrepareRandomOffsetZ = obj["PrepareRandomOffsetZ"]?.Value<float>() ?? 0,
                ConsumeLifetimeBeforeFlying = obj["ConsumeLifetimeBeforeFlying"]?.Value<bool>() ?? true,
                ArmedBeforeFlying = obj["ArmedBeforeFlying"]?.Value<bool>() ?? true
            };
            return dto;
        }

        private static AoeDTO DeserializeAoe(JObject obj)
        {
            // 妗嗘灦 DTO: Id, Name, ModelId, VfxId, AttachMode, OffsetX, OffsetY, OffsetZ, Radius, DelayMs, DurationMs, CollisionLayerMask, MaxTargets, OnDelayTriggerIds, OnEnterTriggerIds, OnExitTriggerIds, OnIntervalTriggerIds, IntervalMs
            var dto = new AoeDTO
            {
                Id = obj["Id"]?.Value<int>() ?? obj["Code"]?.Value<int>() ?? 0,
                Name = obj["Name"]?.Value<string>() ?? string.Empty,
                ModelId = obj["ModelId"]?.Value<int>() ?? 0,
                VfxId = obj["VfxId"]?.Value<int>() ?? 0,
                AttachMode = obj["AttachMode"]?.Value<int>() ?? 0,
                OffsetX = obj["OffsetX"]?.Value<float>() ?? 0,
                OffsetY = obj["OffsetY"]?.Value<float>() ?? 0,
                OffsetZ = obj["OffsetZ"]?.Value<float>() ?? 0,
                Radius = obj["Radius"]?.Value<float>() ?? 0,
                DelayMs = obj["DelayMs"]?.Value<int>() ?? 0,
                DurationMs = obj["DurationMs"]?.Value<int>() ?? 0,
                CollisionLayerMask = obj["CollisionLayerMask"]?.Value<int>() ?? 0,
                MaxTargets = obj["MaxTargets"]?.Value<int>() ?? 0,
                OnDelayTriggerIds = obj["OnDelayTriggerIds"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                OnEnterTriggerIds = obj["OnEnterTriggerIds"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                OnExitTriggerIds = obj["OnExitTriggerIds"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                OnIntervalTriggerIds = obj["OnIntervalTriggerIds"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                IntervalMs = obj["IntervalMs"]?.Value<int>() ?? 0
            };
            return dto;
        }

        private static EmitterDTO DeserializeEmitter(JObject obj)
        {
            // 妗嗘灦 DTO: Id, Name, EmitKind, TemplateId, DelayMs, DurationMs, IntervalMs, TotalCount, CountPerShot, FanAngleDeg, CenterMode, OffsetX, OffsetY, OffsetZ
            var dto = new EmitterDTO
            {
                Id = obj["Id"]?.Value<int>() ?? obj["Code"]?.Value<int>() ?? 0,
                Name = obj["Name"]?.Value<string>() ?? string.Empty,
                EmitKind = obj["EmitKind"]?.Value<int>() ?? 0,
                TemplateId = obj["TemplateId"]?.Value<int>() ?? 0,
                DelayMs = obj["DelayMs"]?.Value<int>() ?? 0,
                DurationMs = obj["DurationMs"]?.Value<int>() ?? 0,
                IntervalMs = obj["IntervalMs"]?.Value<int>() ?? 0,
                TotalCount = obj["TotalCount"]?.Value<int>() ?? 0,
                CountPerShot = obj["CountPerShot"]?.Value<int>() ?? 1,
                FanAngleDeg = obj["FanAngleDeg"]?.Value<float>() ?? 0,
                CenterMode = obj["CenterMode"]?.Value<int>() ?? 0,
                OffsetX = obj["OffsetX"]?.Value<float>() ?? 0,
                OffsetY = obj["OffsetY"]?.Value<float>() ?? 0,
                OffsetZ = obj["OffsetZ"]?.Value<float>() ?? 0
            };
            return dto;
        }

        private static SummonDTO DeserializeSummon(JObject obj)
        {
            // 妗嗘灦 DTO: Id, Name, UnitSubType, ModelId, AttributeTemplateId, LifetimeMs, DespawnOnOwnerDie, MaxAlivePerOwner, OverflowPolicy, StatsMode, AttrScales, SkillIds, PassiveSkillIds, DefaultComponentTemplateIds, Tags
            var dto = new SummonDTO
            {
                Id = obj["Id"]?.Value<int>() ?? obj["Code"]?.Value<int>() ?? 0,
                Name = obj["Name"]?.Value<string>() ?? string.Empty,
                UnitSubType = obj["UnitSubType"]?.Value<int>() ?? 0,
                ModelId = obj["ModelId"]?.Value<int>() ?? 0,
                AttributeTemplateId = obj["AttributeTemplateId"]?.Value<int>() ?? 0,
                LifetimeMs = obj["LifetimeMs"]?.Value<int>() ?? 0,
                DespawnOnOwnerDie = obj["DespawnOnOwnerDie"]?.Value<bool>() ?? false,
                MaxAlivePerOwner = obj["MaxAlivePerOwner"]?.Value<int>() ?? 0,
                OverflowPolicy = obj["OverflowPolicy"]?.Value<int>() ?? 0,
                StatsMode = obj["StatsMode"]?.Value<int>() ?? 0,
                AttrScales = DeserializeSummonAttrScales(obj["AttrScales"]),
                SkillIds = obj["SkillIds"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                PassiveSkillIds = obj["PassiveSkillIds"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                BrainId = obj["BrainId"]?.Value<int>() ?? 0,
                DefaultComponentTemplateIds = obj["DefaultComponentTemplateIds"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                Tags = obj["Tags"]?.ToObject<int[]>() ?? Array.Empty<int>()
            };
            return dto;
        }

        private static SummonAttrScaleDTO[] DeserializeSummonAttrScales(JToken token)
        {
            if (token == null || token.Type != JTokenType.Array) return Array.Empty<SummonAttrScaleDTO>();
            var array = token as JArray;
            var result = new List<SummonAttrScaleDTO>();
            foreach (var item in array)
            {
                if (item is JObject obj)
                {
                    result.Add(new SummonAttrScaleDTO
                    {
                        AttrId = obj["AttrId"]?.Value<int>() ?? 0,
                        Ratio = obj["Ratio"]?.Value<float>() ?? 1.0f,
                        Add = obj["Add"]?.Value<float>() ?? 0
                    });
                }
            }
            return result.ToArray();
        }

        private static SkillFlowDTO DeserializeSkillFlow(JObject obj)
        {
            var dto = new SkillFlowDTO
            {
                Id = obj["Id"]?.Value<int>() ?? obj["Code"]?.Value<int>() ?? 0,
                Name = obj["Name"]?.Value<string>() ?? string.Empty,
                PipelineContinuousTagTemplateId = obj["PipelineContinuousTagTemplateId"]?.Value<int>()
                    ?? obj["ContinuousTagTemplateId"]?.Value<int>()
                    ?? obj["TagTemplateId"]?.Value<int>()
                    ?? 0,
                Phases = DeserializeSkillPhases(obj["Phases"])
            };
            return dto;
        }

        private static SkillPhaseDTO[] DeserializeSkillPhases(JToken token)
        {
            if (token == null || token.Type != JTokenType.Array) return Array.Empty<SkillPhaseDTO>();
            var array = token as JArray;
            var result = new List<SkillPhaseDTO>();
            foreach (var item in array)
            {
                if (item is JObject obj)
                {
                    result.Add(new SkillPhaseDTO
                    {
                        Type = obj["Type"]?.Value<int>() ?? 0,
                        PhaseId = obj["PhaseId"]?.Value<string>() ?? obj["Name"]?.Value<string>() ?? string.Empty,
                        Checks = DeserializeSkillChecks(obj["Checks"]),
                        Timeline = DeserializeSkillTimeline(obj["Timeline"]),
                        Handlers = DeserializeSkillFlowHandlers(obj["Handlers"]),
                        RulePlan = DeserializeSkillRulePlan(obj["RulePlan"]),
                        Children = DeserializeSkillPhases(obj["Children"]),
                        Repeat = DeserializeSkillRepeat(obj["Repeat"]),
                        Delay = DeserializeSkillDelay(obj["Delay"]),
                        WaitUntil = DeserializeSkillWaitUntil(obj["WaitUntil"])
                    });
                }
            }
            return result.ToArray();
        }

        private static SkillRepeatPhaseDTO DeserializeSkillRepeat(JToken token)
        {
            if (token == null) return null;
            var obj = token as JObject;
            if (obj == null) return null;

            return new SkillRepeatPhaseDTO
            {
                RepeatCount = obj["RepeatCount"]?.Value<int>() ?? 0,
                IntervalMs = obj["IntervalMs"]?.Value<int>() ?? 0,
                Phase = DeserializeSkillPhase(obj["Phase"])
            };
        }

        private static SkillDelayPhaseDTO DeserializeSkillDelay(JToken token)
        {
            if (token == null) return null;
            var obj = token as JObject;
            if (obj == null) return null;

            return new SkillDelayPhaseDTO
            {
                DelayMs = obj["DelayMs"]?.Value<int>() ?? 0
            };
        }

        private static SkillWaitUntilPhaseDTO DeserializeSkillWaitUntil(JToken token)
        {
            if (token == null) return null;
            var obj = token as JObject;
            if (obj == null) return null;

            return new SkillWaitUntilPhaseDTO
            {
                Condition = obj["Condition"]?.Value<string>() ?? string.Empty,
                TimeoutMs = obj["TimeoutMs"]?.Value<int>() ?? 0,
                CompleteOnTimeout = obj["CompleteOnTimeout"]?.Value<bool>() ?? true,
                ObservedSlots = obj["ObservedSlots"]?.ToObject<int[]>() ?? Array.Empty<int>()
            };
        }

        private static SkillPhaseDTO DeserializeSkillPhase(JToken token)
        {
            var obj = token as JObject;
            if (obj == null) return null;

            return new SkillPhaseDTO
            {
                Type = obj["Type"]?.Value<int>() ?? 0,
                PhaseId = obj["PhaseId"]?.Value<string>() ?? obj["Name"]?.Value<string>() ?? string.Empty,
                Checks = DeserializeSkillChecks(obj["Checks"]),
                Timeline = DeserializeSkillTimeline(obj["Timeline"]),
                Handlers = DeserializeSkillFlowHandlers(obj["Handlers"]),
                RulePlan = DeserializeSkillRulePlan(obj["RulePlan"]),
                Children = DeserializeSkillPhases(obj["Children"]),
                Repeat = DeserializeSkillRepeat(obj["Repeat"]),
                Delay = DeserializeSkillDelay(obj["Delay"]),
                WaitUntil = DeserializeSkillWaitUntil(obj["WaitUntil"])
            };
        }

        private static SkillRulePlanPhaseDTO DeserializeSkillRulePlan(JToken token)
        {
            if (token == null) return null;
            var obj = token as JObject;
            if (obj == null) return null;

            return new SkillRulePlanPhaseDTO
            {
                TriggerIds = obj["TriggerIds"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                AbortOnFailure = obj["AbortOnFailure"]?.Value<bool>() ?? true,
                FailReason = obj["FailReason"]?.Value<string>()
            };
        }

        private static SkillFlowHandlerConfigDTO DeserializeSkillFlowHandlers(JToken token)
        {
            if (token == null) return null;
            var obj = token as JObject;
            if (obj == null) return null;

            return new SkillFlowHandlerConfigDTO
            {
                PreCastHandlers = DeserializeSkillHandlers(obj["PreCastHandlers"]),
                PostCastHandlers = DeserializeSkillHandlers(obj["PostCastHandlers"]),
                OnFailHandlers = DeserializeSkillHandlers(obj["OnFailHandlers"])
            };
        }

        private static SkillHandlerDTO[] DeserializeSkillHandlers(JToken token)
        {
            if (token == null || token.Type != JTokenType.Array) return Array.Empty<SkillHandlerDTO>();
            var array = token as JArray;
            var result = new List<SkillHandlerDTO>();
            foreach (var item in array)
            {
                if (item is JObject obj)
                {
                    var dto = DeserializeSkillHandler(obj);
                    if (dto != null) result.Add(dto);
                }
            }
            return result.ToArray();
        }

        private static SkillHandlerDTO DeserializeSkillHandler(JObject obj)
        {
            if (obj == null) return null;
            var type = ReadHandlerType(obj["Type"]);
            SkillHandlerDTO dto = type switch
            {
                EHandlerType.CheckCooldown => new CheckCooldownDTO(),
                EHandlerType.CheckResource => new CheckResourceDTO
                {
                    ResourceType = obj["ResourceType"]?.Value<int>() ?? 0,
                    MinAmount = DeserializeNumericRef(obj["MinAmount"])
                },
                EHandlerType.CheckState => new CheckStateDTO
                {
                    RequiredTags = obj["RequiredTags"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                    BlockedTags = obj["BlockedTags"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                    Target = obj["Target"]?.Value<int>() ?? 0
                },
                EHandlerType.CheckTarget => new CheckTargetDTO
                {
                    RequireTarget = obj["RequireTarget"]?.Value<bool>() ?? false,
                    MinDistance = DeserializeNumericRef(obj["MinDistance"]),
                    MaxDistance = DeserializeNumericRef(obj["MaxDistance"]),
                    TargetTags = obj["TargetTags"]?.ToObject<string[]>() ?? Array.Empty<string>()
                },
                EHandlerType.ConsumeResource => new ConsumeResourceDTO
                {
                    ResourceType = obj["ResourceType"]?.Value<int>() ?? 0,
                    Amount = DeserializeNumericRef(obj["Amount"]),
                    FailMessageKey = obj["FailMessageKey"]?.Value<string>()
                },
                EHandlerType.StartCooldown => new StartCooldownDTO
                {
                    CooldownMs = DeserializeNumericRef(obj["CooldownMs"])
                },
                EHandlerType.ApplyBuff => new ApplyBuffDTO
                {
                    BuffId = obj["BuffId"]?.Value<int>() ?? 0,
                    Target = obj["Target"]?.Value<int>() ?? 0,
                    StackPolicy = obj["StackPolicy"]?.Value<int>() ?? 0
                },
                EHandlerType.AddTag => new AddTagDTO
                {
                    Tags = obj["Tags"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                    Target = obj["Target"]?.Value<int>() ?? 0,
                    DurationMs = DeserializeNumericRef(obj["DurationMs"])
                },
                EHandlerType.RemoveTag => new RemoveTagDTO
                {
                    Tags = obj["Tags"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                    Target = obj["Target"]?.Value<int>() ?? 0
                },
                EHandlerType.CustomAction => new CustomActionDTO
                {
                    ActionName = obj["ActionName"]?.Value<string>(),
                    Args = DeserializeNamedArgs(obj["Args"])
                },
                _ => null
            };

            if (dto != null) dto.Type = (int)type;
            return dto;
        }

        private static NamedArgDTO[] DeserializeNamedArgs(JToken token)
        {
            if (token == null || token.Type != JTokenType.Array) return Array.Empty<NamedArgDTO>();
            var array = token as JArray;
            var result = new List<NamedArgDTO>();
            foreach (var item in array)
            {
                if (item is JObject obj)
                {
                    result.Add(new NamedArgDTO
                    {
                        Name = obj["Name"]?.Value<string>(),
                        Value = DeserializeNumericRef(obj["Value"])
                    });
                }
            }
            return result.ToArray();
        }

        private static NumericRefDTO DeserializeNumericRef(JToken token)
        {
            if (token == null) return null;
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                return NumericRefDTO.Const(token.Value<double>());
            }

            var obj = token as JObject;
            if (obj == null) return null;

            return new NumericRefDTO
            {
                Kind = ReadNumericRefKind(obj["Kind"]),
                ConstValue = obj["ConstValue"]?.Value<double>() ?? obj["Value"]?.Value<double>() ?? 0d,
                BoardId = obj["BoardId"]?.Value<int>() ?? 0,
                KeyId = obj["KeyId"]?.Value<int>() ?? 0,
                FieldId = obj["FieldId"]?.Value<int>() ?? 0,
                DomainId = obj["DomainId"]?.Value<string>(),
                Key = obj["Key"]?.Value<string>(),
                ExprText = obj["ExprText"]?.Value<string>(),
                Actor = obj["Actor"]?.Value<int>() ?? 0,
                AttributeType = obj["AttributeType"]?.Value<int>() ?? 0,
                ResourceType = obj["ResourceType"]?.Value<int>() ?? 0,
                Coefficient = obj["Coefficient"]?.Value<double>() ?? 1d,
                Add = obj["Add"]?.Value<double>() ?? 0d
            };
        }

        private static EHandlerType ReadHandlerType(JToken token)
        {
            if (token == null) return 0;
            if (token.Type == JTokenType.Integer) return (EHandlerType)token.Value<int>();
            var raw = token.Value<string>();
            return Enum.TryParse(raw, true, out EHandlerType parsed) ? parsed : 0;
        }

        private static ENumericRefKind ReadNumericRefKind(JToken token)
        {
            if (token == null) return ENumericRefKind.Const;
            if (token.Type == JTokenType.Integer) return (ENumericRefKind)token.Value<int>();
            var raw = token.Value<string>();
            return Enum.TryParse(raw, true, out ENumericRefKind parsed) ? parsed : ENumericRefKind.Const;
        }

        private static SkillChecksPhaseDTO DeserializeSkillChecks(JToken token)
        {
            if (token == null) return null;
            var obj = token as JObject;
            if (obj == null) return null;

            return new SkillChecksPhaseDTO
            {
                CheckCooldown = obj["CheckCooldown"]?.Value<bool>() ?? true,
                CheckCastingState = obj["CheckCastingState"]?.Value<bool>() ?? true,
                RequiredTags = obj["RequiredTags"]?.ToObject<int[]>() ?? Array.Empty<int>(),
                BlockedTags = obj["BlockedTags"]?.ToObject<int[]>() ?? Array.Empty<int>()
            };
        }

        private static SkillTimelinePhaseDTO DeserializeSkillTimeline(JToken token)
        {
            if (token == null) return null;
            var obj = token as JObject;
            if (obj == null) return null;

            return new SkillTimelinePhaseDTO
            {
                DurationMs = obj["DurationMs"]?.Value<int>() ?? 0,
                Events = DeserializeSkillTimelineEvents(obj["Events"])
            };
        }

        private static SkillTimelineEventDTO[] DeserializeSkillTimelineEvents(JToken token)
        {
            if (token == null || token.Type != JTokenType.Array) return Array.Empty<SkillTimelineEventDTO>();
            var array = token as JArray;
            var result = new List<SkillTimelineEventDTO>();
            foreach (var item in array)
            {
                if (item is JObject obj)
                {
                    result.Add(new SkillTimelineEventDTO
                    {
                        AtMs = obj["AtMs"]?.Value<int>() ?? 0,
                        EffectId = obj["EffectId"]?.Value<int>() ?? 0,
                        ExecuteMode = obj["ExecuteMode"]?.Value<int>() ?? 0,
                        EventTag = obj["EventTag"]?.Value<string>() ?? string.Empty
                    });
                }
            }
            return result.ToArray();
        }

        private static SkillLevelTableDTO DeserializeSkillLevelTable(JObject obj)
        {
            // 妗嗘灦 DTO: Id, Levels
            var dto = new SkillLevelTableDTO
            {
                Id = obj["Id"]?.Value<int>() ?? obj["Code"]?.Value<int>() ?? 0,
                Levels = DeserializeSkillLevels(obj["Levels"])
            };
            return dto;
        }

        private static SkillLevelDTO[] DeserializeSkillLevels(JToken token)
        {
            if (token == null || token.Type != JTokenType.Array) return Array.Empty<SkillLevelDTO>();
            var array = token as JArray;
            var result = new List<SkillLevelDTO>();
            foreach (var item in array)
            {
                if (item is JObject obj)
                {
                    // 妗嗘灦 DTO: CooldownMs, Cost, Params (娌℃湁 Level, Damage)
                    result.Add(new SkillLevelDTO
                    {
                        CooldownMs = obj["CooldownMs"]?.Value<int>() ?? 0,
                        Cost = obj["Cost"]?.Value<int>() ?? 0,
                        Params = obj["Params"]?.ToObject<float[]>() ?? Array.Empty<float>()
                    });
                }
            }
            return result.ToArray();
        }

        private static ComponentTemplateDTO DeserializeComponentTemplate(JObject obj)
        {
            // 妗嗘灦 DTO: Id, Name, Ops
            var dto = new ComponentTemplateDTO
            {
                Id = obj["Id"]?.Value<int>() ?? obj["Code"]?.Value<int>() ?? 0,
                Name = obj["Name"]?.Value<string>() ?? string.Empty,
                Ops = DeserializeComponentOps(obj["Ops"])
            };
            return dto;
        }

        private static ComponentOpDTO[] DeserializeComponentOps(JToken token)
        {
            if (token == null || token.Type != JTokenType.Array) return Array.Empty<ComponentOpDTO>();
            var array = token as JArray;
            var result = new List<ComponentOpDTO>();
            foreach (var item in array)
            {
                if (item is JObject op)
                {
                    result.Add(new ComponentOpDTO
                    {
                        Kind = op["Kind"]?.Value<int>() ?? 0,
                        IntValue = op["IntValue"]?.Value<int>() ?? 0,
                        FloatValue = op["FloatValue"]?.Value<float>() ?? 0,
                        BoolValue = op["BoolValue"]?.Value<bool>() ?? false
                    });
                }
            }
            return result.ToArray();
        }

        private static PresentationTemplateDTO DeserializePresentationTemplate(JObject obj)
        {
            // 妗嗘灦 DTO: Id, Name, Kind, AssetId, DefaultDurationMs, AttachMode, Socket, Follow, StackPolicy, StopPolicy, Scale, ColorR, ColorG, ColorB, ColorA, Radius, OffsetX, OffsetY, OffsetZ
            var dto = new PresentationTemplateDTO
            {
                Id = obj["Id"]?.Value<int>() ?? obj["Code"]?.Value<int>() ?? 0,
                Name = obj["Name"]?.Value<string>() ?? string.Empty,
                Kind = obj["Kind"]?.Value<int>() ?? 0,
                AssetId = obj["AssetId"]?.Value<int>() ?? 0,
                DefaultDurationMs = obj["DefaultDurationMs"]?.Value<int>() ?? 0,
                AttachMode = obj["AttachMode"]?.Value<int>() ?? 0,
                Socket = obj["Socket"]?.Value<string>() ?? string.Empty,
                Follow = obj["Follow"]?.Value<bool>() ?? false,
                StackPolicy = obj["StackPolicy"]?.Value<int>() ?? 0,
                StopPolicy = obj["StopPolicy"]?.Value<int>() ?? 0,
                Scale = obj["Scale"]?.Value<float>() ?? 1.0f,
                ColorR = obj["ColorR"]?.Value<float>() ?? 1.0f,
                ColorG = obj["ColorG"]?.Value<float>() ?? 1.0f,
                ColorB = obj["ColorB"]?.Value<float>() ?? 1.0f,
                ColorA = obj["ColorA"]?.Value<float>() ?? 1.0f,
                Radius = obj["Radius"]?.Value<float>() ?? 0,
                OffsetX = obj["OffsetX"]?.Value<float>() ?? 0,
                OffsetY = obj["OffsetY"]?.Value<float>() ?? 0,
                OffsetZ = obj["OffsetZ"]?.Value<float>() ?? 0
            };
            return dto;
        }

        private static SpawnSummonActionTemplateDTO DeserializeSpawnSummonActionTemplate(JObject obj)
        {
            // 妗嗘灦 DTO: Id, Name, SummonId, TargetMode, PositionMode, RotationMode, OwnerKeyMode, PatternMode, PatternCount, Spacing, Radius, StartAngleDeg, ArcAngleDeg, YawOffsetDeg, RandomSeed, RandomRadiusMin, RandomRadiusMax, GridRows, GridCols, GridSpacingX, GridSpacingZ, PerPointRotationMode, PerPointYawOffsetDeg, IntervalMs
            var dto = new SpawnSummonActionTemplateDTO
            {
                Id = obj["Id"]?.Value<int>() ?? obj["Code"]?.Value<int>() ?? 0,
                Name = obj["Name"]?.Value<string>() ?? string.Empty,
                SummonId = obj["SummonId"]?.Value<int>() ?? 0,
                TargetMode = obj["TargetMode"]?.Value<int>() ?? 0,
                PositionMode = obj["PositionMode"]?.Value<int>() ?? 0,
                RotationMode = obj["RotationMode"]?.Value<int>() ?? 0,
                OwnerKeyMode = obj["OwnerKeyMode"]?.Value<int>() ?? 0,
                PatternMode = obj["PatternMode"]?.Value<int>() ?? 0,
                PatternCount = obj["PatternCount"]?.Value<int>() ?? 0,
                Spacing = obj["Spacing"]?.Value<float>() ?? 0,
                Radius = obj["Radius"]?.Value<float>() ?? 0,
                StartAngleDeg = obj["StartAngleDeg"]?.Value<float>() ?? 0,
                ArcAngleDeg = obj["ArcAngleDeg"]?.Value<float>() ?? 0,
                YawOffsetDeg = obj["YawOffsetDeg"]?.Value<float>() ?? 0,
                RandomSeed = obj["RandomSeed"]?.Value<int>() ?? 0,
                RandomRadiusMin = obj["RandomRadiusMin"]?.Value<float>() ?? 0,
                RandomRadiusMax = obj["RandomRadiusMax"]?.Value<float>() ?? 0,
                GridRows = obj["GridRows"]?.Value<int>() ?? 0,
                GridCols = obj["GridCols"]?.Value<int>() ?? 0,
                GridSpacingX = obj["GridSpacingX"]?.Value<float>() ?? 0,
                GridSpacingZ = obj["GridSpacingZ"]?.Value<float>() ?? 0,
                PerPointRotationMode = obj["PerPointRotationMode"]?.Value<int>() ?? 0,
                PerPointYawOffsetDeg = obj["PerPointYawOffsetDeg"]?.Value<float>() ?? 0,
                IntervalMs = obj["IntervalMs"]?.Value<int>() ?? 0
            };
            return dto;
        }
    }
}

using System;
using Newtonsoft.Json;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO
{
    [Serializable]
    public sealed class CharacterDTO
    {
        public int Id;
        public string Name;
        public int ModelId;
        public int AttributeTemplateId;
        public int[] SkillIds;
        public int[] PassiveSkillIds;
    }

    [Serializable]
    public sealed class SkillDTO
    {
        public int Id;
        public string Name;
        public int CooldownMs;
        public int Range;
        public int IconId;
        public int Category;
        public int[] Tags;

        public int SkillButtonTemplateId;

        public int LevelTableId;
        public int PreCastFlowId;
        public int CastFlowId;
    }

    [Serializable]
    public sealed class SkillButtonTemplateDTO
    {
        public int Id;
        public string Name;

        public float LongPressSeconds;
        public float DragThreshold;
        public bool EnableAim;

        public int AimMode;
        public float AimMaxRadius;

        public int UsePointMode;
        public float SelectRange;
        public bool FaceToAim;
    }

    [Serializable]
    public sealed class TagTemplateDTO
    {
        public int Id;
        public string Name;

        [JsonIgnore] public string[] RequiredTagNames;
        [JsonIgnore] public string[] BlockedTagNames;
        [JsonIgnore] public string[] GrantTagNames;
        [JsonIgnore] public string[] RemoveTagNames;

        public int[] RequiredTags;
        public int[] BlockedTags;

        public int[] GrantTags;
        public int[] RemoveTags;
    }

    [Serializable]
    public sealed class SearchQueryTemplateDTO
    {
        public int Id;
        public string Name;

        public int CenterMode;
        public float Radius;
        public int MaxCount;

        public bool ExcludeCaster;
    }

    [Serializable]
    public sealed class PassiveSkillDTO
    {
        public int Id;
        public string Name;
        public int CooldownMs;
        public int[] TriggerIds;
    }

    [Serializable]
    public sealed class SkillFlowDTO
    {
        public int Id;
        public string Name;
        public SkillPhaseDTO[] Phases;
    }

    public enum SkillPhaseType
    {
        Checks = 1,
        Timeline = 2,
    }

    [Serializable]
    public sealed class SkillPhaseDTO
    {
        public int Type;
        public SkillChecksPhaseDTO Checks;
        public SkillTimelinePhaseDTO Timeline;
    }

    [Serializable]
    public sealed class SkillChecksPhaseDTO
    {
        public bool CheckCooldown;
        public bool CheckCastingState;
        public int[] RequiredTags;
        public int[] BlockedTags;
    }

    [Serializable]
    public sealed class SkillTimelinePhaseDTO
    {
        public int DurationMs;
        public SkillTimelineEventDTO[] Events;
    }

    [Serializable]
    public sealed class SkillTimelineEventDTO
    {
        public int AtMs;
        public int EffectId;
        public int ExecuteMode;
        public string EventTag;
    }

    [Serializable]
    public sealed class BattleAttributeTemplateDTO
    {
        public int Id;
        public int[] ActiveSkills;
        public int[] PassiveSkills;
        public int Hp;
        public int MaxHp;
        public int ExtraHp;
        public int PhysicsAttack;
        public int MagicAttack;
        public int ExtraPhysicsAttack;
        public int ExtraMagicAttack;
        public int PhysicsDefense;
        public int MagicDefense;
        public int Mana;
        public int MaxMana;
        public int CriticalR;
        public int AttackSpeedR;
        public int CooldownReduceR;
        public int PhysicsPenetrationR;
        public int MagicPenetrationR;
        public int MoveSpeed;
        public int PhysicsBloodsuckingR;
        public int MagicBloodsuckingR;
        public int AttackRange;
        public int PerSecondBloodR;
        public int PerSecondManaR;
        public int ResilienceR;
    }

    [Serializable]
    public sealed class ModelDTO
    {
        public int Id;
        public string PrefabPath;
        public float Scale;
    }

    [Serializable]
    public sealed class BuffDTO
    {
        public int Id;
        public string Name;
        public int DurationMs;

        public int OngoingEffectId;

        public int[] OnAddEffects;
        public int[] OnRemoveEffects;
        public int[] OnIntervalEffects;
        public int IntervalMs;
        public int StackingPolicy;
        public int RefreshPolicy;
        public int MaxStacks;
        public int[] TriggerIds;
        public int[] Tags;
    }

    [Serializable]
    public sealed class ProjectileLauncherDTO
    {
        public int Id;
        public string Name;
        public int EmitterType;

        public int DurationMs;
        public int IntervalMs;

        public int CountPerShot;
        public float FanAngleDeg;
    }

    [Serializable]
    public sealed class ProjectileDTO
    {
        public int Id;
        public string Name;

        public int VfxId;

        public float Speed;
        public int LifetimeMs;
        public float MaxDistance;

        public int HitPolicyKind;
        public int HitsRemaining;
        public int HitCooldownMs;
        public int TickIntervalMs;

        public int OnHitEffectId;
        public int OnSpawnVfxId;
        public int OnHitVfxId;
        public int OnExpireVfxId;

        public int ReturnAfterMs;
        public float ReturnSpeed;
        public float ReturnStopDistance;
    }

    [Serializable]
    public sealed class AoeDTO
    {
        public int Id;
        public string Name;

        public int ModelId;
        public int VfxId;
        // 0=World, 1=FollowOwner
        public int AttachMode;
        public float OffsetX;
        public float OffsetY;
        public float OffsetZ;

        public float Radius;
        public int DelayMs;
        public int CollisionLayerMask;
        public int MaxTargets;

        public int[] OnDelayTriggerIds;
    }

    [Serializable]
    public sealed class EmitterDTO
    {
        public int Id;
        public string Name;

        // 1=Projectile, 2=AOE
        public int EmitKind;
        // ProjectileId or AoeId
        public int TemplateId;

        public int DelayMs;
        public int DurationMs;
        public int IntervalMs;
        public int TotalCount;

        public int CountPerShot;
        public float FanAngleDeg;

        // 0=AimPos, 1=CasterPos, 2=TargetPos
        public int CenterMode;
        public float OffsetX;
        public float OffsetY;
        public float OffsetZ;
    }

    [Serializable]
    public sealed class SummonDTO
    {
        public int Id;
        public string Name;

        public int UnitSubType;
        public int ModelId;

        public int AttributeTemplateId;

        public int LifetimeMs;
        public bool DespawnOnOwnerDie;

        public int MaxAlivePerOwner;
        public int OverflowPolicy;

        public int StatsMode;
        public SummonAttrScaleDTO[] AttrScales;

        public int[] SkillIds;
        public int[] PassiveSkillIds;

        public int[] DefaultComponentTemplateIds;

        public int[] Tags;
    }

    [Serializable]
    public sealed class SpawnSummonActionTemplateDTO
    {
        public int Id;
        public string Name;

        public int SummonId;

        public int TargetMode;
        public int PositionMode;
        public int RotationMode;
        public int OwnerKeyMode;

        public int PatternMode;
        public int PatternCount;
        public float Spacing;
        public float Radius;
        public float StartAngleDeg;
        public float ArcAngleDeg;
        public float YawOffsetDeg;

        public int RandomSeed;
        public float RandomRadiusMin;
        public float RandomRadiusMax;

        public int GridRows;
        public int GridCols;
        public float GridSpacingX;
        public float GridSpacingZ;

        public int PerPointRotationMode;
        public float PerPointYawOffsetDeg;

        public int IntervalMs;
        public int DurationMs;
        public int TotalCount;

        public string CasterKey;
        public string TargetKey;
        public int QueryTemplateId;

        public string AimPosKey;
        public string FixedPosKey;
        public float FixedPosFallbackX;
        public float FixedPosFallbackY;
        public float FixedPosFallbackZ;
    }

    [Serializable]
    public sealed class ComponentTemplateDTO
    {
        public int Id;
        public string Name;

        public ComponentOpDTO[] Ops;
    }

    [Serializable]
    public sealed class ComponentOpDTO
    {
        public int Kind;

        public int IntValue;
        public float FloatValue;
        public bool BoolValue;
    }

    [Serializable]
    public sealed class OngoingEffectDTO
    {
        public int Id;
        public string Name;

        public int DurationMs;
        public int PeriodMs;

        public int OnApplyEffectId;
        public int OnTickEffectId;
        public int OnRemoveEffectId;
    }

    [Serializable]
    public sealed class SummonAttrScaleDTO
    {
        public int AttrId;
        public float Ratio;
        public float Add;
    }

    [Serializable]
    public sealed class VfxDTO
    {
        public int Id;
        public string Resource;
        public int DurationMs;
    }

    [Serializable]
    public sealed class PresentationTemplateDTO
    {
        public int Id;
        public string Name;

        public int Kind;
        public int AssetId;
        public int DefaultDurationMs;

        public int AttachMode;
        public string Socket;
        public bool Follow;

        public int StackPolicy;
        public int StopPolicy;

        public float Scale;
        public float ColorR;
        public float ColorG;
        public float ColorB;
        public float ColorA;
        public float Radius;
        public float OffsetX;
        public float OffsetY;
        public float OffsetZ;
    }
}

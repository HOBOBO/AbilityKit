using System;

namespace AbilityKit.Demo.Moba.Share.Config
{
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
        public int MaxDistanceResponse;
        public int[] OnArrivalTriggerIds;
        public float CollisionWidth;
        public float CollisionHeight;
        public float CollisionLength;

        public int HitPolicyKind;
        public int HitsRemaining;
        public int HitUnitSubType;
        public int CollisionLayerMask;
        public float BounceSearchRadius;
        public float TrackingArcHeight;
        public float TrackingArcHeightPerUnit;
        public float TrackingArcDurationSeconds;
        public int HitCooldownMs;
        public int HitIdentityPolicy;
        public ProjectileCollisionRuleDTO[] CollisionRules;
        public int TickIntervalMs;

        public int OnHitEffectId;
        public int[] OnSpawnTriggerIds;
        public int[] OnHitTriggerIds;
        public int[] OnTickTriggerIds;
        public int[] OnExitTriggerIds;
        public int OnSpawnVfxId;
        public int OnHitVfxId;
        public int OnExpireVfxId;

        public int ReturnAfterMs;
        public float ReturnSpeed;
        public float ReturnStopDistance;

        public string StateMachineProfileId;
        public float SpawnRandomOffsetX;
        public float SpawnRandomOffsetY;
        public float SpawnRandomOffsetZ;
    }

    public enum ProjectileCollisionRole { TrackingTarget = 1, OtherActor = 2, StaticWorld = 3 }
    public enum ProjectileCollisionRelation { Any = 0, Enemy = 1, Ally = 2, Neutral = 3 }

    [Serializable]
    public sealed class ProjectileCollisionRuleDTO
    {
        public ProjectileCollisionRole Role;
        public int MainType;
        public int UnitSubType;
        public ProjectileCollisionRelation Relation;
        // Existing ProjectileCollisionResponse: 0 Ignore, 1 Hit, 2 Block.
        public int Response;
    }

    [Serializable]
    public sealed class AoeDTO
    {
        public int Id;
        public string Name;

        public int ModelId;
        public int VfxId;
        public int AttachMode;
        public float OffsetX;
        public float OffsetY;
        public float OffsetZ;

        public float Radius;
        public float InnerRadius;
        public int DelayMs;
        public int DurationMs;
        public int CollisionLayerMask;
        public int MaxTargets;

        public int[] OnDelayTriggerIds;
        public int[] OnEnterTriggerIds;
        public int[] OnCrossTriggerIds;
        public int[] OnExitTriggerIds;
        public int[] OnIntervalTriggerIds;
        public int IntervalMs;

        // When armed, consume the first valid enemy enter atomically and remove the area.
        public bool ConsumeOnFirstEnter;
    }

    [Serializable]
    public sealed class EmitterDTO
    {
        public int Id;
        public string Name;

        public int EmitKind;
        public int TemplateId;

        public int DelayMs;
        public int DurationMs;
        public int IntervalMs;
        public int TotalCount;

        public int CountPerShot;
        public float FanAngleDeg;

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

        public int BrainId;

        /// <summary>召唤物出生表现特效 ID；0 表示不播放。</summary>
        public int SpawnVfxId;

        /// <summary>召唤物死亡表现特效 ID；0 表示不播放。</summary>
        public int DeathVfxId;

        /// <summary>召唤物主动销毁表现特效 ID；0 表示不播放。</summary>
        public int DespawnVfxId;

        /// <summary>继承属性配置 id：指向属性继承表（summon_attr_inherits）获取继承属性占比。0 = 不继承。</summary>
        public int InheritAttributeConfigId;

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
    public sealed class SummonAttrScaleDTO
    {
        public int AttrId;
        public float Ratio;
        public float Add;
    }

    /// <summary>
    /// 召唤物继承属性配置表条目：
    /// 召唤物生成时按 施法者属性 × Ratio + Add 写入自身属性基础值。
    /// </summary>
    [Serializable]
    public sealed class SummonAttrInheritDTO
    {
        public int Id;
        public string Name;
        public SummonAttrScaleDTO[] Scales;
    }
}

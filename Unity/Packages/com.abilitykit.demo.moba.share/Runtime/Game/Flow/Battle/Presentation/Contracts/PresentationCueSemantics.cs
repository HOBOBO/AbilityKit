using System;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// 表现 cue 的来源域。取代原先散落在 CueKind/OwnerKind 两个字符串字段里的类型身份。
    /// 表现层消费方可以按域批量过滤（例如 HUD 只关心 Buff|Lifecycle，VFX 只关心 Placement|Visual）。
    /// </summary>
    public enum PresentationCueDomain : byte
    {
        None = 0,
        Trigger = 1,
        Behavior = 2,
        Buff = 3,
        Effect = 4,
        Aoe = 5,
        Projectile = 6,
        Tag = 7,
        Combat = 8,
        Shield = 9,
    }

    /// <summary>
    /// 表现 cue 携带哪一类负载。见 PresentationCuePayload 的分段约定。
    /// 按位组合，表示一个 cue 可以同时属于多个类目（例如 Buff 持续特效 = Placement|Lifecycle）。
    /// </summary>
    [System.Flags]
    public enum PresentationCueClass : byte
    {
        None = 0,
        /// <summary>定位类：在哪、贴谁、多大、多久。绝大多数 cue 都需要。</summary>
        Placement = 1,
        /// <summary>数值类：模板自定义标量（走受控的 param bag）。</summary>
        Numeric = 2,
        /// <summary>战斗数值类：伤害/治疗/护盾吸收/溢出。</summary>
        Combat = 4,
        /// <summary>生命周期类：叠层、剩余时间、结束原因。</summary>
        Lifecycle = 8,
        /// <summary>视觉类：颜色、缩放等纯美术参数。</summary>
        Visual = 16,
    }

    /// <summary>
    /// 一个 stage 到达表现层时应当触发的状态迁移。表现层 reconcile 状态机只认这四种，
    /// 不再各自维护 ShouldStart/ShouldKeepActive/ShouldStop 三张表。
    /// </summary>
    public enum PresentationCueTransition : byte
    {
        None = 0,
        Start = 1,
        KeepActive = 2,
        Stop = 3,
    }

    /// <summary>Controls whether a cue is consumed as an event or reconciled as a live view.</summary>
    public enum PresentationCueLifetime : byte
    {
        Unknown = 0,
        TransientEvent = 1,
        PersistentState = 2,
    }

    public enum PresentationCueSnapshotKind : byte
    {
        EventDelta = 0,
        ActiveBaseline = 1,
        ActiveBaselineEnd = 2,
    }

    /// <summary>
    /// cue 语义的唯一权威判定处。服务端发射端与客户端消费端引用同一份实现，
    /// 因此不可能再出现"三张表不同步导致特效永不出/永不停"。
    ///
    /// 约定：
    /// - 新增 stage 时只需要在这里给出迁移分类，所有端自动生效；
    /// - 新增 stage 必须显式落入四类之一，未覆盖的 stage 编译期可见（见 AllStages 回归测试）。
    /// </summary>
    public static class PresentationCueSemantics
    {
        /// <summary>按 stage 值判定迁移分类。接受 int 以便同时服务三份同值的 stage 枚举镜像。</summary>
        public static PresentationCueTransition Classify(int stage)
        {
            switch ((PresentationCueStage)stage)
            {
                case PresentationCueStage.ConditionPassed:
                case PresentationCueStage.BeforeAction:
                case PresentationCueStage.Executed:
                case PresentationCueStage.Started:
                    return PresentationCueTransition.Start;

                case PresentationCueStage.Ticked:
                case PresentationCueStage.Refreshed:
                case PresentationCueStage.StackChanged:
                    return PresentationCueTransition.KeepActive;

                case PresentationCueStage.ConditionFailed:
                case PresentationCueStage.Interrupted:
                case PresentationCueStage.Skipped:
                case PresentationCueStage.Expired:
                case PresentationCueStage.Removed:
                case PresentationCueStage.Completed:
                    return PresentationCueTransition.Stop;

                default:
                    return PresentationCueTransition.None;
            }
        }

        public static PresentationCueTransition Classify(PresentationCueStage stage)
        {
            return Classify((int)stage);
        }

        public static bool ShouldStart(PresentationCueStage stage)
        {
            return Classify(stage) == PresentationCueTransition.Start;
        }

        public static bool ShouldKeepActive(PresentationCueStage stage)
        {
            return Classify(stage) == PresentationCueTransition.KeepActive;
        }

        public static bool ShouldStop(PresentationCueStage stage)
        {
            return Classify(stage) == PresentationCueTransition.Stop;
        }

        /// <summary>
        /// 该 stage 是否会改变表现对象的存续状态（Start/KeepActive/Stop）。
        /// None 表示这是一个纯观测 stage（例如 Ticked 之外的诊断输出），消费方应忽略。
        /// </summary>
        public static bool AffectsLifetime(PresentationCueStage stage)
        {
            return Classify(stage) != PresentationCueTransition.None;
        }

        public static PresentationCueLifetime ResolveLifetime(
            PresentationCueDomain domain,
            PresentationCueStage stage)
        {
            if (domain == PresentationCueDomain.Buff ||
                domain == PresentationCueDomain.Tag ||
                domain == PresentationCueDomain.Aoe ||
                domain == PresentationCueDomain.Shield)
            {
                return PresentationCueLifetime.PersistentState;
            }

            if (domain == PresentationCueDomain.Projectile &&
                (stage == PresentationCueStage.Started ||
                 stage == PresentationCueStage.Ticked ||
                 stage == PresentationCueStage.Refreshed ||
                 stage == PresentationCueStage.Completed ||
                 stage == PresentationCueStage.Expired ||
                 stage == PresentationCueStage.Removed))
            {
                return PresentationCueLifetime.PersistentState;
            }

            return PresentationCueLifetime.TransientEvent;
        }

        public static PresentationCueDomain ResolveDomain(string legacyKind)
        {
            return ParseDomain(legacyKind);
        }

        /// <summary>已定义且已分类的全部 stage。用于回归测试断言"没有漏分类的 stage"。</summary>
        public static readonly PresentationCueStage[] AllStages =
        {
            PresentationCueStage.ConditionPassed,
            PresentationCueStage.ConditionFailed,
            PresentationCueStage.BeforeAction,
            PresentationCueStage.Executed,
            PresentationCueStage.Interrupted,
            PresentationCueStage.Skipped,
            PresentationCueStage.Started,
            PresentationCueStage.Ticked,
            PresentationCueStage.Refreshed,
            PresentationCueStage.StackChanged,
            PresentationCueStage.Expired,
            PresentationCueStage.Removed,
            PresentationCueStage.Completed,
        };

        /// <summary>解析旧版字符串形式的类型身份，保持 wire 向后兼容一个版本。</summary>
        public static PresentationCueDomain ParseDomain(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return PresentationCueDomain.None;
            var normalized = value.Trim();
            var separator = normalized.IndexOf('.');
            var token = separator > 0 ? normalized.Substring(0, separator) : normalized;
            switch (token.ToLowerInvariant())
            {
                case "buff": return PresentationCueDomain.Buff;
                case "tag": return PresentationCueDomain.Tag;
                case "aoe":
                case "area": return PresentationCueDomain.Aoe;
                case "projectile": return PresentationCueDomain.Projectile;
                case "combat": return PresentationCueDomain.Combat;
                case "shield": return PresentationCueDomain.Shield;
                case "effect": return PresentationCueDomain.Effect;
                case "trigger": return PresentationCueDomain.Trigger;
                case "behavior": return PresentationCueDomain.Behavior;
                default: return PresentationCueDomain.None;
            }
        }

        /// <summary>回退到旧字符串表示，供仍在读 CueKind/OwnerKind 的旧消费方使用。</summary>
        public static string ToLegacyDomainName(PresentationCueDomain domain)
        {
            switch (domain)
            {
                case PresentationCueDomain.Buff: return "Buff";
                case PresentationCueDomain.Tag: return "Tag";
                case PresentationCueDomain.Aoe: return "Aoe";
                case PresentationCueDomain.Projectile: return "Projectile";
                case PresentationCueDomain.Combat: return "Combat";
                case PresentationCueDomain.Shield: return "Shield";
                case PresentationCueDomain.Effect: return "Effect";
                case PresentationCueDomain.Trigger: return "Trigger";
                case PresentationCueDomain.Behavior: return "Behavior";
                default: return null;
            }
        }
    }
}

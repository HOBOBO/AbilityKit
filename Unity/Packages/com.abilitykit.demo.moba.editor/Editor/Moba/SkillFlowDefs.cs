#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share.Config;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [Serializable]
    public sealed class SkillFlowDef
    {
        [MinValue(1)]
        public int Id;

        public string Name;

        [MinValue(0)]
        [Tooltip("技能 Pipeline 整体生命周期绑定的持续标签模板。0 表示不绑定。")]
        public int PipelineContinuousTagTemplateId;

        [SerializeReference]
        [HideReferenceObjectPicker]
        [ListDrawerSettings(
            Expanded = true,
            ListElementLabelName = nameof(SkillPhaseDef.DisplayTitle),
            CustomAddFunction = nameof(AddPhase))]
        public List<SkillPhaseDef> Phases = new List<SkillPhaseDef>();

        private void AddPhase()
        {
            Phases ??= new List<SkillPhaseDef>();
            SkillPhaseDefMenu.Show(phase => Phases.Add(phase), "Add Skill Phase");
        }

        public SkillFlowDTO ToDto()
        {
            return new SkillFlowDTO
            {
                Id = Id,
                Name = Name,
                PipelineContinuousTagTemplateId = PipelineContinuousTagTemplateId,
                Phases = ConvertPhases(Phases),
            };
        }

        /// <summary>
        /// 运行时 DTO（JSON 真相）→ 富作者形态 Def。用于把历史 JSON 配置迁移进编辑器 dataList。
        /// 无 Def 对应的相位类型（如 Handlers）会被跳过。
        /// </summary>
        public static SkillFlowDef FromDto(SkillFlowDTO dto)
        {
            if (dto == null) return null;
            return new SkillFlowDef
            {
                Id = dto.Id,
                Name = dto.Name,
                PipelineContinuousTagTemplateId = dto.PipelineContinuousTagTemplateId,
                Phases = SkillPhaseDef.ConvertPhasesFromDto(dto.Phases),
            };
        }

        internal static SkillPhaseDTO[] ConvertPhases(IReadOnlyList<SkillPhaseDef> phases)
        {
            if (phases == null || phases.Count == 0) return Array.Empty<SkillPhaseDTO>();

            var list = new List<SkillPhaseDTO>(phases.Count);
            for (var i = 0; i < phases.Count; i++)
            {
                var phase = phases[i];
                if (phase == null) continue;
                var dto = phase.ToDto();
                if (dto != null) list.Add(dto);
            }

            return list.Count == 0 ? Array.Empty<SkillPhaseDTO>() : list.ToArray();
        }
    }

    [Serializable]
    public abstract class SkillPhaseDef
    {
        [Tooltip("稳定阶段标识。用于诊断、回放和运行时节点回跳配置。")]
        public string PhaseId;

        public abstract SkillPhaseType PhaseType { get; }

        public string DisplayTitle => string.IsNullOrWhiteSpace(PhaseId)
            ? PhaseType.ToString()
            : $"{PhaseType}  [{PhaseId}]";

#if ODIN_INSPECTOR
        [OnInspectorGUI]
        private void DrawTraceSelectionMarker()
        {
            var selection = SkillFlowInspectorSelectionState.Current;
            if (!selection.IsValid || !ReferenceEquals(selection.Phase, this)) return;

            var previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.35f, 0.75f, 1f, 1f);
            EditorGUILayout.HelpBox(
                $"Trace selected: SkillFlow #{selection.FlowId} / {selection.PhaseId}",
                MessageType.Info);
            GUI.backgroundColor = previous;
        }
#endif

        public abstract SkillPhaseDTO ToDto();

        protected SkillPhaseDTO CreateDto()
        {
            return new SkillPhaseDTO
            {
                Type = (int)PhaseType,
                PhaseId = string.IsNullOrWhiteSpace(PhaseId) ? null : PhaseId.Trim(),
            };
        }

        /// <summary>DTO 相位 → Def 相位（按 Type 分派到对应子类）。无 Def 形态的类型返回 null。</summary>
        public static SkillPhaseDef FromDto(SkillPhaseDTO dto)
        {
            if (dto == null) return null;
            var type = (SkillPhaseType)dto.Type;

            SkillPhaseDef def;
            switch (type)
            {
                case SkillPhaseType.Checks:
                    def = new SkillChecksPhaseDef { Checks = dto.Checks ?? new SkillChecksPhaseDTO() };
                    break;
                case SkillPhaseType.Timeline:
                    def = new SkillTimelinePhaseDef { Timeline = dto.Timeline ?? new SkillTimelinePhaseDTO() };
                    break;
                case SkillPhaseType.RulePlan:
                    var rp = dto.RulePlan ?? new SkillRulePlanPhaseDTO();
                    def = new SkillRulePlanPhaseDef
                    {
                        TriggerIds = rp.TriggerIds ?? Array.Empty<int>(),
                        AbortOnFailure = rp.AbortOnFailure,
                        FailReason = rp.FailReason,
                    };
                    break;
                case SkillPhaseType.Sequence:
                    def = new SkillSequencePhaseDef { Children = ConvertPhasesFromDto(dto.Children) };
                    break;
                case SkillPhaseType.Parallel:
                    def = new SkillParallelPhaseDef { Children = ConvertPhasesFromDto(dto.Children) };
                    break;
                case SkillPhaseType.Race:
                    def = new SkillRacePhaseDef { Children = ConvertPhasesFromDto(dto.Children) };
                    break;
                case SkillPhaseType.Repeat:
                    var rep = dto.Repeat ?? new SkillRepeatPhaseDTO();
                    def = new SkillRepeatPhaseDef
                    {
                        RepeatCount = rep.RepeatCount,
                        IntervalMs = rep.IntervalMs,
                        Phase = FromDto(rep.Phase),
                    };
                    break;
                case SkillPhaseType.Delay:
                    def = new SkillDelayPhaseDef { DelayMs = (dto.Delay ?? new SkillDelayPhaseDTO()).DelayMs };
                    break;
                case SkillPhaseType.WaitUntil:
                    var w = dto.WaitUntil ?? new SkillWaitUntilPhaseDTO();
                    def = new SkillWaitUntilPhaseDef
                    {
                        Condition = w.Condition,
                        TimeoutMs = w.TimeoutMs,
                        CompleteOnTimeout = w.CompleteOnTimeout,
                        ObservedSlots = w.ObservedSlots ?? Array.Empty<int>(),
                        Arguments = w.Arguments ?? Array.Empty<SkillWaitConditionArgumentDTO>(),
                    };
                    break;
                case SkillPhaseType.AwaitEvent:
                    var awaitEvent = dto.AwaitEvent ?? new SkillAwaitEventPhaseDTO();
                    def = new SkillAwaitEventPhaseDef
                    {
                        EventId = awaitEvent.EventId,
                        TimeoutMs = awaitEvent.TimeoutMs,
                        CompleteOnTimeout = awaitEvent.CompleteOnTimeout,
                        Filters = awaitEvent.Filters ?? Array.Empty<SkillEventIntFilterDTO>(),
                    };
                    break;
                case SkillPhaseType.Window:
                    var window = dto.Window ?? new SkillWindowPhaseDTO();
                    def = new SkillWindowPhaseDef
                    {
                        WindowId = window.WindowId,
                        Kind = (SkillWindowKind)window.Kind,
                        DurationMs = window.DurationMs,
                        CompleteOnTimeout = window.CompleteOnTimeout,
                        ChannelIntervalMs = window.ChannelIntervalMs,
                        ChargeTierThresholdMs = window.ChargeTierThresholdMs ?? Array.Empty<int>(),
                        OpenTriggerIds = window.OpenTriggerIds ?? Array.Empty<int>(),
                        TickTriggerIds = window.TickTriggerIds ?? Array.Empty<int>(),
                        CloseTriggerIds = window.CloseTriggerIds ?? Array.Empty<int>(),
                        AbortOnTriggerFailure = window.AbortOnTriggerFailure,
                        FailReason = window.FailReason,
                    };
                    break;
                case SkillPhaseType.CommitPoint:
                    def = new SkillCommitPointPhaseDef
                    {
                        CommitId = (dto.CommitPoint ?? new SkillCommitPointPhaseDTO()).CommitId,
                    };
                    break;
                case SkillPhaseType.Economy:
                    var economy = dto.Economy ?? new SkillEconomyPhaseDTO();
                    def = new SkillEconomyPhaseDef
                    {
                        Operation = (SkillEconomyOperation)economy.Operation,
                        ResourceType = economy.ResourceType,
                        ResourceAmount = economy.ResourceAmount,
                        UseResolvedResourceCost = economy.UseResolvedResourceCost,
                        ChargeCost = economy.ChargeCost,
                        MaxCharges = economy.MaxCharges,
                        ChargeRecoveryMs = economy.ChargeRecoveryMs,
                        StartSkillCooldown = economy.StartSkillCooldown,
                        SkillCooldownMs = economy.SkillCooldownMs,
                        UseResolvedSkillCooldown = economy.UseResolvedSkillCooldown,
                        CooldownGroup = economy.CooldownGroup,
                        SharedCooldownMs = economy.SharedCooldownMs,
                        GlobalCooldownMs = economy.GlobalCooldownMs,
                        IgnoreGlobalCooldown = economy.IgnoreGlobalCooldown,
                        RefundBeforeCommit = economy.RefundBeforeCommit,
                        FailReason = economy.FailReason,
                    };
                    break;
                default:
                    return null; // Handlers 等无 Def 形态，跳过
            }

            def.PhaseId = dto.PhaseId;
            return def;
        }

        internal static List<SkillPhaseDef> ConvertPhasesFromDto(SkillPhaseDTO[] dtos)
        {
            var list = new List<SkillPhaseDef>();
            if (dtos == null) return list;
            for (var i = 0; i < dtos.Length; i++)
            {
                var def = FromDto(dtos[i]);
                if (def != null) list.Add(def);
            }

            return list;
        }
    }

    /// <summary>
    /// Retained only so existing managed-reference assets can still deserialize and migrate.
    /// New flows should use RulePlan conditions.
    /// </summary>
    [Serializable]
    public sealed class SkillChecksPhaseDef : SkillPhaseDef
    {
        [InfoBox("Checks 阶段已废弃。请迁移到 RulePlan 条件。", InfoMessageType.Error)]
        public SkillChecksPhaseDTO Checks = new SkillChecksPhaseDTO();

        public override SkillPhaseType PhaseType => SkillPhaseType.Checks;

        public override SkillPhaseDTO ToDto()
        {
            var dto = CreateDto();
            dto.Checks = Checks;
            return dto;
        }
    }

    [Serializable]
    public sealed class SkillTimelinePhaseDef : SkillPhaseDef
    {
        public SkillTimelinePhaseDTO Timeline = new SkillTimelinePhaseDTO();

        public override SkillPhaseType PhaseType => SkillPhaseType.Timeline;

        public override SkillPhaseDTO ToDto()
        {
            var dto = CreateDto();
            dto.Timeline = Timeline;
            return dto;
        }
    }

    [Serializable]
    public sealed class SkillRulePlanPhaseDef : SkillPhaseDef
    {
        [LabelText("Trigger IDs")]
        public int[] TriggerIds = Array.Empty<int>();

        public bool AbortOnFailure = true;
        public string FailReason;

        public override SkillPhaseType PhaseType => SkillPhaseType.RulePlan;

        public override SkillPhaseDTO ToDto()
        {
            var dto = CreateDto();
            dto.RulePlan = new SkillRulePlanPhaseDTO
            {
                TriggerIds = TriggerIds ?? Array.Empty<int>(),
                AbortOnFailure = AbortOnFailure,
                FailReason = FailReason,
            };
            return dto;
        }
    }

    [Serializable]
    public abstract class SkillCompositePhaseDef : SkillPhaseDef
    {
        [SerializeReference]
        [HideReferenceObjectPicker]
        [ListDrawerSettings(
            Expanded = true,
            ListElementLabelName = nameof(SkillPhaseDef.DisplayTitle),
            CustomAddFunction = nameof(AddChild))]
        public List<SkillPhaseDef> Children = new List<SkillPhaseDef>();

        private void AddChild()
        {
            Children ??= new List<SkillPhaseDef>();
            SkillPhaseDefMenu.Show(phase => Children.Add(phase), $"Add {PhaseType} Child Phase");
        }

        protected SkillPhaseDTO CreateCompositeDto()
        {
            var dto = CreateDto();
            dto.Children = SkillFlowDef.ConvertPhases(Children);
            return dto;
        }
    }

    [Serializable]
    public sealed class SkillSequencePhaseDef : SkillCompositePhaseDef
    {
        public override SkillPhaseType PhaseType => SkillPhaseType.Sequence;
        public override SkillPhaseDTO ToDto() => CreateCompositeDto();
    }

    [Serializable]
    public sealed class SkillParallelPhaseDef : SkillCompositePhaseDef
    {
        public override SkillPhaseType PhaseType => SkillPhaseType.Parallel;
        public override SkillPhaseDTO ToDto() => CreateCompositeDto();
    }

    [Serializable]
    public sealed class SkillRacePhaseDef : SkillCompositePhaseDef
    {
        public override SkillPhaseType PhaseType => SkillPhaseType.Race;
        public override SkillPhaseDTO ToDto() => CreateCompositeDto();
    }

    [Serializable]
    public sealed class SkillRepeatPhaseDef : SkillPhaseDef
    {
        [MinValue(1)]
        public int RepeatCount = 1;

        [MinValue(0)]
        public int IntervalMs;

        [SerializeReference]
        [HideReferenceObjectPicker]
        public SkillPhaseDef Phase;

        public override SkillPhaseType PhaseType => SkillPhaseType.Repeat;

        [Button("选择子阶段")]
        private void SelectPhase()
        {
            SkillPhaseDefMenu.Show(phase => Phase = phase, "Set Repeat Child Phase");
        }

        [Button("清除子阶段")]
        [ShowIf(nameof(HasPhase))]
        private void ClearPhase()
        {
            SkillPhaseDefMenu.RecordChange("Clear Repeat Child Phase", () => Phase = null);
        }

        private bool HasPhase() => Phase != null;

        public override SkillPhaseDTO ToDto()
        {
            var dto = CreateDto();
            dto.Repeat = new SkillRepeatPhaseDTO
            {
                RepeatCount = RepeatCount,
                IntervalMs = IntervalMs,
                Phase = Phase?.ToDto(),
            };
            return dto;
        }
    }

    [Serializable]
    public sealed class SkillDelayPhaseDef : SkillPhaseDef
    {
        [MinValue(0)]
        public int DelayMs;

        public override SkillPhaseType PhaseType => SkillPhaseType.Delay;

        public override SkillPhaseDTO ToDto()
        {
            var dto = CreateDto();
            dto.Delay = new SkillDelayPhaseDTO { DelayMs = DelayMs };
            return dto;
        }
    }

    [Serializable]
    public sealed class SkillWaitUntilPhaseDef : SkillPhaseDef
    {
        [Required]
        public string Condition = "ObservedSlotsIdle";

        [MinValue(0)]
        public int TimeoutMs;

        public bool CompleteOnTimeout = true;
        public int[] ObservedSlots = Array.Empty<int>();
        public SkillWaitConditionArgumentDTO[] Arguments = Array.Empty<SkillWaitConditionArgumentDTO>();

        public override SkillPhaseType PhaseType => SkillPhaseType.WaitUntil;

        public override SkillPhaseDTO ToDto()
        {
            var dto = CreateDto();
            dto.WaitUntil = new SkillWaitUntilPhaseDTO
            {
                Condition = Condition,
                TimeoutMs = TimeoutMs,
                CompleteOnTimeout = CompleteOnTimeout,
                ObservedSlots = ObservedSlots ?? Array.Empty<int>(),
                Arguments = Arguments ?? Array.Empty<SkillWaitConditionArgumentDTO>(),
            };
            return dto;
        }
    }

    [Serializable]
    public sealed class SkillAwaitEventPhaseDef : SkillPhaseDef
    {
        [Required]
        public string EventId;
        [MinValue(0)]
        public int TimeoutMs;
        public bool CompleteOnTimeout = true;
        public SkillEventIntFilterDTO[] Filters = Array.Empty<SkillEventIntFilterDTO>();

        public override SkillPhaseType PhaseType => SkillPhaseType.AwaitEvent;

        public override SkillPhaseDTO ToDto()
        {
            var dto = CreateDto();
            dto.AwaitEvent = new SkillAwaitEventPhaseDTO
            {
                EventId = EventId,
                TimeoutMs = TimeoutMs,
                CompleteOnTimeout = CompleteOnTimeout,
                Filters = Filters ?? Array.Empty<SkillEventIntFilterDTO>(),
            };
            return dto;
        }
    }

    [Serializable]
    public sealed class SkillWindowPhaseDef : SkillPhaseDef
    {
        [Required]
        public string WindowId;
        public SkillWindowKind Kind;
        [MinValue(0)]
        public int DurationMs;
        public bool CompleteOnTimeout = true;
        [MinValue(0)]
        public int ChannelIntervalMs;
        public int[] ChargeTierThresholdMs = Array.Empty<int>();
        public int[] OpenTriggerIds = Array.Empty<int>();
        public int[] TickTriggerIds = Array.Empty<int>();
        public int[] CloseTriggerIds = Array.Empty<int>();
        public bool AbortOnTriggerFailure = true;
        public string FailReason;

        public override SkillPhaseType PhaseType => SkillPhaseType.Window;

        public override SkillPhaseDTO ToDto()
        {
            var dto = CreateDto();
            dto.Window = new SkillWindowPhaseDTO
            {
                WindowId = WindowId,
                Kind = (int)Kind,
                DurationMs = DurationMs,
                CompleteOnTimeout = CompleteOnTimeout,
                ChannelIntervalMs = ChannelIntervalMs,
                ChargeTierThresholdMs = ChargeTierThresholdMs ?? Array.Empty<int>(),
                OpenTriggerIds = OpenTriggerIds ?? Array.Empty<int>(),
                TickTriggerIds = TickTriggerIds ?? Array.Empty<int>(),
                CloseTriggerIds = CloseTriggerIds ?? Array.Empty<int>(),
                AbortOnTriggerFailure = AbortOnTriggerFailure,
                FailReason = FailReason,
            };
            return dto;
        }
    }

    [Serializable]
    public sealed class SkillCommitPointPhaseDef : SkillPhaseDef
    {
        [Required]
        public string CommitId;
        public override SkillPhaseType PhaseType => SkillPhaseType.CommitPoint;

        public override SkillPhaseDTO ToDto()
        {
            var dto = CreateDto();
            dto.CommitPoint = new SkillCommitPointPhaseDTO { CommitId = CommitId };
            return dto;
        }
    }

    [Serializable]
    public sealed class SkillEconomyPhaseDef : SkillPhaseDef
    {
        public SkillEconomyOperation Operation;
        public int ResourceType;
        [MinValue(0)] public float ResourceAmount;
        public bool UseResolvedResourceCost = true;
        [MinValue(0)] public int ChargeCost = 1;
        [MinValue(1)] public int MaxCharges = 1;
        [MinValue(0)] public int ChargeRecoveryMs;
        public bool StartSkillCooldown = true;
        [MinValue(0)] public int SkillCooldownMs;
        public bool UseResolvedSkillCooldown = true;
        public string CooldownGroup;
        [MinValue(0)] public int SharedCooldownMs;
        [MinValue(0)] public int GlobalCooldownMs;
        public bool IgnoreGlobalCooldown;
        public bool RefundBeforeCommit = true;
        public string FailReason;

        public override SkillPhaseType PhaseType => SkillPhaseType.Economy;

        public override SkillPhaseDTO ToDto()
        {
            var dto = CreateDto();
            dto.Economy = new SkillEconomyPhaseDTO
            {
                Operation = (int)Operation,
                ResourceType = ResourceType,
                ResourceAmount = ResourceAmount,
                UseResolvedResourceCost = UseResolvedResourceCost,
                ChargeCost = ChargeCost,
                MaxCharges = MaxCharges,
                ChargeRecoveryMs = ChargeRecoveryMs,
                StartSkillCooldown = StartSkillCooldown,
                SkillCooldownMs = SkillCooldownMs,
                UseResolvedSkillCooldown = UseResolvedSkillCooldown,
                CooldownGroup = CooldownGroup,
                SharedCooldownMs = SharedCooldownMs,
                GlobalCooldownMs = GlobalCooldownMs,
                IgnoreGlobalCooldown = IgnoreGlobalCooldown,
                RefundBeforeCommit = RefundBeforeCommit,
                FailReason = FailReason,
            };
            return dto;
        }
    }

    internal static class SkillPhaseDefMenu
    {
        public static void Show(Action<SkillPhaseDef> apply, string undoLabel)
        {
            if (apply == null) return;

            var owner = Selection.activeObject;
            var menu = new GenericMenu();
            Add(menu, "执行/RulePlan", () => new SkillRulePlanPhaseDef(), apply, owner, undoLabel);
            Add(menu, "执行/Timeline", () => new SkillTimelinePhaseDef(), apply, owner, undoLabel);
            Add(menu, "组合/Sequence", () => new SkillSequencePhaseDef(), apply, owner, undoLabel);
            Add(menu, "组合/Parallel", () => new SkillParallelPhaseDef(), apply, owner, undoLabel);
            Add(menu, "组合/Race", () => new SkillRacePhaseDef(), apply, owner, undoLabel);
            Add(menu, "组合/Repeat", () => new SkillRepeatPhaseDef(), apply, owner, undoLabel);
            Add(menu, "控制/Delay", () => new SkillDelayPhaseDef(), apply, owner, undoLabel);
            Add(menu, "控制/WaitUntil", () => new SkillWaitUntilPhaseDef(), apply, owner, undoLabel);
            Add(menu, "控制/AwaitEvent", () => new SkillAwaitEventPhaseDef(), apply, owner, undoLabel);
            Add(menu, "技能窗口/Window", () => new SkillWindowPhaseDef(), apply, owner, undoLabel);
            Add(menu, "技能窗口/CommitPoint", () => new SkillCommitPointPhaseDef(), apply, owner, undoLabel);
            Add(menu, "技能经济/Economy", () => new SkillEconomyPhaseDef(), apply, owner, undoLabel);
            menu.ShowAsContext();
        }

        public static void RecordChange(string undoLabel, Action change)
        {
            if (change == null) return;

            var owner = Selection.activeObject;
            if (owner != null) Undo.RecordObject(owner, undoLabel);
            change();
            if (owner != null) EditorUtility.SetDirty(owner);
        }

        private static void Add(
            GenericMenu menu,
            string path,
            Func<SkillPhaseDef> factory,
            Action<SkillPhaseDef> apply,
            UnityEngine.Object owner,
            string undoLabel)
        {
            menu.AddItem(new GUIContent(path), false, () =>
            {
                if (owner != null) Undo.RecordObject(owner, undoLabel);
                apply(factory());
                if (owner != null) EditorUtility.SetDirty(owner);
            });
        }
    }
}
#endif

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
            Add(menu, "组合/Repeat", () => new SkillRepeatPhaseDef(), apply, owner, undoLabel);
            Add(menu, "控制/Delay", () => new SkillDelayPhaseDef(), apply, owner, undoLabel);
            Add(menu, "控制/WaitUntil", () => new SkillWaitUntilPhaseDef(), apply, owner, undoLabel);
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

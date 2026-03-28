#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [Serializable]
    public sealed class SkillFlowDef
    {
        public int Id;
        public string Name;

        [SerializeReference]
        [HideReferenceObjectPicker]
        [ListDrawerSettings(Expanded = true, CustomAddFunction = nameof(AddPhase))]
        public List<SkillPhaseDef> Phases = new List<SkillPhaseDef>();

        private void AddPhase()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Checks"), false, () => AddPhaseInternal(new SkillChecksPhaseDef()));
            menu.AddItem(new GUIContent("Timeline"), false, () => AddPhaseInternal(new SkillTimelinePhaseDef()));
            menu.ShowAsContext();
        }

        private void AddPhaseInternal(SkillPhaseDef phase)
        {
            if (phase == null) return;

            var owner = Selection.activeObject as UnityEngine.Object;
            if (owner != null)
            {
                Undo.RecordObject(owner, "Add Skill Phase");
            }

            Phases ??= new List<SkillPhaseDef>();
            Phases.Add(phase);

            if (owner != null)
            {
                EditorUtility.SetDirty(owner);
            }
        }

        public SkillFlowDTO ToDto()
        {
            var dto = new SkillFlowDTO
            {
                Id = Id,
                Name = Name,
                Phases = Phases != null ? ConvertPhases(Phases) : null,
            };
            return dto;
        }

        private static SkillPhaseDTO[] ConvertPhases(List<SkillPhaseDef> phases)
        {
            if (phases == null || phases.Count == 0) return Array.Empty<SkillPhaseDTO>();

            var list = new List<SkillPhaseDTO>(phases.Count);
            for (int i = 0; i < phases.Count; i++)
            {
                var p = phases[i];
                if (p == null) continue;
                var dto = p.ToDto();
                if (dto != null) list.Add(dto);
            }

            return list.Count == 0 ? Array.Empty<SkillPhaseDTO>() : list.ToArray();
        }
    }

    [Serializable]
    public abstract class SkillPhaseDef
    {
        public abstract SkillPhaseDTO ToDto();
    }

    [Serializable]
    public sealed class SkillChecksPhaseDef : SkillPhaseDef
    {
        public SkillChecksPhaseDTO Checks = new SkillChecksPhaseDTO();

        public override SkillPhaseDTO ToDto()
        {
            return new SkillPhaseDTO
            {
                Type = (int)SkillPhaseType.Checks,
                Checks = Checks,
                Timeline = null,
            };
        }
    }

    [Serializable]
    public sealed class SkillTimelinePhaseDef : SkillPhaseDef
    {
        public SkillTimelinePhaseDTO Timeline = new SkillTimelinePhaseDTO();

        public override SkillPhaseDTO ToDto()
        {
            return new SkillPhaseDTO
            {
                Type = (int)SkillPhaseType.Timeline,
                Checks = null,
                Timeline = Timeline,
            };
        }
    }
}
#endif

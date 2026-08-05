using System.Collections.Generic;
using AbilityKit.Game.Battle.View;
using AbilityKit.Game.Battle.View.Lib.Joystick;
using AbilityKit.Game.Battle.View.Lib.Skill;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudInputUi
    {
        public BattleHudInputUi(
            GameObject root,
            BattleHudInputView inputView,
            JoystickAreaView moveJoystick,
            BattleHudMoveInputMapper moveMapper,
            BattleHudSkillAimInputMapper skillAimMapper,
            IReadOnlyList<SkillButtonView> skillViews,
            Button infoButton)
        {
            Root = root;
            InputView = inputView;
            MoveJoystick = moveJoystick;
            MoveMapper = moveMapper;
            SkillAimMapper = skillAimMapper;
            SkillViews = skillViews ?? new SkillButtonView[0];
            Skill1View = SkillViews.Count > 0 ? SkillViews[0] : null;
            Skill2View = SkillViews.Count > 1 ? SkillViews[1] : null;
            Skill3View = SkillViews.Count > 2 ? SkillViews[2] : null;
            InfoButton = infoButton;
        }

        public BattleHudInputUi(
            GameObject root,
            BattleHudInputView inputView,
            JoystickAreaView moveJoystick,
            BattleHudMoveInputMapper moveMapper,
            BattleHudSkillAimInputMapper skillAimMapper,
            SkillButtonView skill1View,
            SkillButtonView skill2View,
            SkillButtonView skill3View,
            Button infoButton)
            : this(
                root,
                inputView,
                moveJoystick,
                moveMapper,
                skillAimMapper,
                new[] { skill1View, skill2View, skill3View },
                infoButton)
        {
        }

        public GameObject Root { get; }
        public BattleHudInputView InputView { get; }
        public JoystickAreaView MoveJoystick { get; }
        public BattleHudMoveInputMapper MoveMapper { get; }
        public BattleHudSkillAimInputMapper SkillAimMapper { get; }
        public SkillButtonView Skill1View { get; }
        public SkillButtonView Skill2View { get; }
        public SkillButtonView Skill3View { get; }
        public IReadOnlyList<SkillButtonView> SkillViews { get; }
        public int SkillButtonCount => SkillViews != null ? SkillViews.Count : 0;
        public Button InfoButton { get; }

        public void Destroy()
        {
            MoveMapper?.Dispose();
            SkillAimMapper?.Dispose();

            if (Root != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(Root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(Root);
                }
            }
        }
    }
}

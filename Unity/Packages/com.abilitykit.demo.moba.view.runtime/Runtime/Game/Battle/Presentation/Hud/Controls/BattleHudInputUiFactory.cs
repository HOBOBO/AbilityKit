using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.View;
using AbilityKit.Game.Battle.View.Lib.Skill;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudInputUiFactory
    {
        private const int DefaultSkillButtonCount = 3;

        private readonly BattleHudInputControlFactory _controls;

        public BattleHudInputUiFactory()
            : this(new BattleHudInputControlFactory())
        {
        }

        public BattleHudInputUiFactory(BattleHudInputControlFactory controls)
        {
            _controls = controls ?? new BattleHudInputControlFactory();
        }

        public BattleHudInputUi Create(RectTransform root, Canvas canvas, Transform cameraTransform, Action infoClicked)
        {
            return Create(root, canvas, cameraTransform, infoClicked, DefaultSkillButtonCount);
        }

        public BattleHudInputUi Create(RectTransform root, Canvas canvas, Transform cameraTransform, Action infoClicked, int skillButtonCount)
        {
            if (skillButtonCount <= 0) skillButtonCount = DefaultSkillButtonCount;

            var inputUiRoot = new GameObject("BattleHudInput", typeof(RectTransform));
            inputUiRoot.transform.SetParent(root, worldPositionStays: false);
            inputUiRoot.SetActive(false);

            BattleHudRectTransformLayout.StretchToParent(inputUiRoot.GetComponent<RectTransform>());

            var inputView = inputUiRoot.AddComponent<BattleHudInputView>();
            var moveJoystick = _controls.CreateMoveJoystick(inputUiRoot.transform, canvas);
            var moveMapper = new BattleHudMoveInputMapper();
            var skillAimMapper = new BattleHudSkillAimInputMapper();

            var skillViews = new List<SkillButtonView>(skillButtonCount);
            for (var slot = 1; slot <= skillButtonCount; slot++)
            {
                skillViews.Add(CreateSkillButton(inputUiRoot.transform, root, canvas, BattleHudInputLayout.GetSkill(slot)));
            }
            var infoButton = _controls.CreateInfoButton(
                inputUiRoot.transform,
                BattleHudInputLayout.InfoButtonPosition,
                infoClicked);

            inputUiRoot.SetActive(true);

            inputView.Initialize(moveJoystick, skillViews);
            moveMapper.Initialize(inputView, cameraTransform);
            skillAimMapper.Initialize(inputView, cameraTransform);

            return new BattleHudInputUi(
                inputUiRoot,
                inputView,
                moveJoystick,
                moveMapper,
                skillAimMapper,
                skillViews,
                infoButton);
        }

        private SkillButtonView CreateSkillButton(
            Transform parent,
            RectTransform root,
            Canvas canvas,
            BattleHudSkillButtonLayoutSpec layout)
        {
            return _controls.CreateSkillButton(parent, root, canvas, layout.Slot, layout.Name, layout.AnchoredPos);
        }
    }
}

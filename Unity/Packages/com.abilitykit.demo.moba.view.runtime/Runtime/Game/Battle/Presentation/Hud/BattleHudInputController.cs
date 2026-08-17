using System;
using System.Collections.Generic;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudInputController : IDisposable
    {
        private readonly IBattleHudInputSink _hudInput;
        private readonly RectTransform _root;
        private readonly Canvas _canvas;
        private readonly Transform _cameraTransform;
        private readonly BattleHudInputEventBridge _inputEvents;
        private readonly BattleHudSkillButtonTemplateBinder _templateBinder;
        private readonly BattleHudInputUiFactory _uiFactory;

        private readonly HashSet<int> _appliedSkillStateSlots = new HashSet<int>();

        private BattleHudInputUi _inputUi;

        public BattleHudInputController(
            IBattleHudInputSink hudInput,
            RectTransform root,
            Canvas canvas,
            Transform cameraTransform,
            BattleViewResourceProvider resources = null,
            BattleHudInputUiFactory uiFactory = null,
            BattleHudInputControllerFactory controllers = null)
        {
            controllers ??= new BattleHudInputControllerFactory();

            _hudInput = hudInput;
            _root = root;
            _canvas = canvas;
            _cameraTransform = cameraTransform;
            _inputEvents = controllers.CreateInputEvents(hudInput);
            _templateBinder = controllers.CreateTemplateBinder(resources);
            _uiFactory = uiFactory ?? new BattleHudInputUiFactory();
        }

        public void Ensure()
        {
            if (_root == null) return;
            if (_hudInput == null) return;
            if (_inputUi != null) return;

            _inputUi = _uiFactory.Create(_root, _canvas, _cameraTransform, OnInfoClick);
            _inputEvents.Bind(_inputUi);
        }

        public IReadOnlyDictionary<int, BattleHudSkillPresentationSpec> SkillSpecs => _templateBinder.SkillSpecs;
        internal BattleHudInputUi InputUi => _inputUi;

        public bool ApplySkillButtonTemplates(EnterMobaGameRes res, string playerId)
        {
            if (!_templateBinder.TryResolveLoadout(res, playerId, out var loadout))
            {
                return false;
            }

            EnsureSkillButtonCount(_templateBinder.ResolveSkillButtonCount(loadout));
            if (!_templateBinder.TryApply(loadout, _inputUi?.SkillViews))
            {
                return false;
            }

            _inputEvents.SetSkillSpecs(_templateBinder.SkillSpecs);
            return true;
        }

        public void ApplySkillStates(MobaSkillStateSnapshotEntry[] entries, int localActorId)
        {
            if (_inputUi == null) return;

            _appliedSkillStateSlots.Clear();
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    if (!ShouldApplySkillState(entry, localActorId)) continue;
                    if (!TryResolveSkillView(entry.Slot, entry.SkillId, out var view)) continue;

                    view.ApplySkillState(entry);
                    _appliedSkillStateSlots.Add(entry.Slot);
                }
            }

            if (_appliedSkillStateSlots.Count > 0)
            {
                ClearMissingSkillStates(_appliedSkillStateSlots);
            }
        }

        public void Dispose()
        {
            _inputEvents.ResetHudAim();
            DestroyInputUi();
        }

        private static bool ShouldApplySkillState(in MobaSkillStateSnapshotEntry entry, int localActorId)
        {
            if (entry.ActorId <= 0) return false;
            return localActorId > 0 && entry.ActorId == localActorId;
        }

        internal bool SkillStateMatchesTemplate(MobaSkillStateSnapshotEntry entry)
        {
            if (entry.Slot <= 0) return false;
            if (!_templateBinder.SkillSpecs.TryGetValue(entry.Slot, out var spec)) return false;
            return spec.SkillId <= 0 || entry.SkillId <= 0 || spec.SkillId == entry.SkillId;
        }

        private bool TryResolveSkillView(int slot, int skillId, out AbilityKit.Game.Battle.View.Lib.Skill.SkillButtonView view)
        {
            view = null;
            if (_inputUi?.SkillViews == null) return false;

            var index = slot - 1;
            if (index < 0 || index >= _inputUi.SkillViews.Count) return false;

            if (_templateBinder.SkillSpecs.TryGetValue(slot, out var spec)
                && skillId > 0
                && spec.SkillId > 0
                && spec.SkillId != skillId)
            {
                AbilityKit.Core.Logging.Log.Warning($"[BattleHudInputController] reject skill state with mismatched presentation. slot={slot}, snapshotSkillId={skillId}, templateSkillId={spec.SkillId}");
                return false;
            }

            view = _inputUi.SkillViews[index];
            return view != null;
        }

        private void ClearMissingSkillStates(HashSet<int> appliedSlots)
        {
            if (_inputUi?.SkillViews == null) return;

            for (int i = 0; i < _inputUi.SkillViews.Count; i++)
            {
                var slot = i + 1;
                if (appliedSlots != null && appliedSlots.Contains(slot)) continue;
                _inputUi.SkillViews[i]?.ClearSkillState();
            }
        }

        private void EnsureSkillButtonCount(int skillButtonCount)
        {
            if (skillButtonCount <= 0) return;
            if (_inputUi != null && _inputUi.SkillButtonCount == skillButtonCount) return;
            if (_root == null || _hudInput == null) return;

            DestroyInputUi();
            _inputUi = _uiFactory.Create(_root, _canvas, _cameraTransform, OnInfoClick, skillButtonCount);
            _inputEvents.Bind(_inputUi);
        }

        private void DestroyInputUi()
        {
            if (_inputUi == null) return;

            _inputEvents.Unbind();
            _inputUi.Destroy();
            _inputUi = null;
        }

        private static void OnInfoClick()
        {
        }
    }

    internal sealed class BattleHudInputControllerFactory
    {
        public BattleHudInputEventBridge CreateInputEvents(IBattleHudInputSink hudInput)
        {
            return new BattleHudInputEventBridge(hudInput);
        }

        public BattleHudSkillButtonTemplateBinder CreateTemplateBinder(BattleViewResourceProvider resources)
        {
            return new BattleHudSkillButtonTemplateBinder(resources);
        }
    }
}

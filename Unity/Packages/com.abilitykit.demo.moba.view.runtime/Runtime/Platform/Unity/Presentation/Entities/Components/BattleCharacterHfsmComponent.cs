using System;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.HFSM.Runtime;
using AbilityKit.HFSM.Definition;
using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Game.Battle.Component
{
    public sealed class BattleCharacterHfsmComponent
    {
        private readonly MobaCharacterHfsmFacts _owner = new MobaCharacterHfsmFacts();
        private readonly StateMachineRuntime<MobaCharacterHfsmFacts> _machine;
        private readonly StateMachineDefinition _definition;
        private readonly CharacterPresentationActionRunner _actions;
        private readonly CharacterPresentationActionCatalog _catalog;
        private int _snapshotFrame;

        public BattleCharacterHfsmComponent(StateMachineDefinition definition = null,
            CharacterPresentationActionCatalog catalog = null, int entityCode = 0)
        {
            _definition = definition ?? MobaCharacterHfsmProfile.CreateGenericDefinition();
            EntityCode = entityCode;
            _catalog = (catalog ?? CharacterPresentationActionCatalog.CreateDefault())
                .CreateForEntityCode(entityCode);
            _catalog.ValidateAgainst(_definition);
            _actions = new CharacterPresentationActionRunner(_catalog);
            _machine = new StateMachineRuntime<MobaCharacterHfsmFacts>(_owner,
                _definition, MobaCharacterHfsmProfile.CreateBindings(_definition));
        }

        public MobaCharacterActionState Action { get; private set; }
        public CastPresentationFrame CastFrame { get; private set; }
        public int SnapshotFrame => _snapshotFrame;
        public long DefinitionHash => _machine.DefinitionHash;
        public int EntityCode { get; private set; }

        public void ReplaceAction(string path, string actionId, CharacterPresentationAction replacement) =>
            _catalog.ReplaceAction(path, actionId, replacement);

        public void ApplySnapshot(MobaCharacterHfsmSnapshot snapshot)
        {
            if (snapshot?.Machine == null) throw new ArgumentNullException(nameof(snapshot));
            // Restore does not replay OnEnter, transition actions or timeline signals.
            var path = MobaCharacterHfsmRuntime.ActivePath(snapshot.Machine, _definition);
            if (snapshot.Machine.DefinitionHash != DefinitionHash || !snapshot.Machine.Initialized ||
                !string.Equals(path, snapshot.Action.Path, StringComparison.Ordinal) ||
                snapshot.Action.LocalFrame < 0 || snapshot.Action.StartFrame < 0 ||
                snapshot.Action.LocalFrame != snapshot.Machine.Frame - snapshot.Action.StartFrame ||
                (snapshot.Mode != null &&
                 (snapshot.Mode.Frame != snapshot.Machine.Frame ||
                  !string.Equals(snapshot.Mode.Current.ModeId, snapshot.Action.ModeId, StringComparison.Ordinal) ||
                  snapshot.Mode.Current.InstanceId != snapshot.Action.ModeInstanceId ||
                  snapshot.Mode.Current.StartFrame != snapshot.Action.ModeStartFrame ||
                  snapshot.Mode.Current.LocalFrame != snapshot.Action.ModeLocalFrame)))
                throw new InvalidOperationException("Character view action and HFSM hierarchy differ.");
            _machine.RestoreSnapshot(snapshot.Machine);
            _snapshotFrame = snapshot.Machine.Frame;
            Action = snapshot.Action;
            CastFrame = default;
        }

        public void ApplyPresentation(in CharacterActionData data)
        {
            if (data.SnapshotFrame < _snapshotFrame) return;
            _snapshotFrame = data.SnapshotFrame;
            CastFrame = data.CastFrame;
            Action = new MobaCharacterActionState(data.Path, data.InstanceId,
                data.StartFrame, data.LocalFrame, data.CastInstanceId, data.SkillId,
                data.ModeId, data.ModeInstanceId, data.ModeStartFrame, data.ModeLocalFrame);
        }

        public CharacterPlaybackIntent? Evaluate(int frame, int tickRate,
            ICharacterPresentationSink sink = null)
        {
            if (frame < SnapshotFrame || string.IsNullOrEmpty(Action.Path)) return null;
            var localFrame = checked(Action.LocalFrame + frame - SnapshotFrame);
            return _actions.Evaluate(Action.Path, Action.InstanceId, localFrame, frame, tickRate, sink, Action.SkillId);
        }
    }
}

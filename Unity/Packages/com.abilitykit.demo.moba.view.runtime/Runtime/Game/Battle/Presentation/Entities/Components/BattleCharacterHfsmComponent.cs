using System;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.HFSM.Runtime;
using AbilityKit.HFSM.Definition;

namespace AbilityKit.Game.Battle.Component
{
    public sealed class BattleCharacterHfsmComponent
    {
        private readonly MobaCharacterHfsmFacts _owner = new MobaCharacterHfsmFacts();
        private readonly StateMachineRuntime<MobaCharacterHfsmFacts> _machine;
        private readonly StateMachineDefinition _definition;
        private readonly CharacterPresentationActionRunner _actions;
        private readonly CharacterPresentationActionCatalog _catalog;

        public BattleCharacterHfsmComponent(StateMachineDefinition definition = null,
            CharacterPresentationActionCatalog catalog = null, int entityCode = 0)
        {
            _definition = definition ?? MobaCharacterHfsmProfile.CreateDefinition();
            EntityCode = entityCode;
            _catalog = (catalog ?? CharacterPresentationActionCatalog.CreateDefault())
                .CreateForEntityCode(entityCode);
            _catalog.ValidateAgainst(_definition);
            _actions = new CharacterPresentationActionRunner(_catalog);
            _machine = new StateMachineRuntime<MobaCharacterHfsmFacts>(_owner,
                _definition, MobaCharacterHfsmProfile.CreateBindings());
        }

        public MobaCharacterActionState Action { get; private set; }
        public int SnapshotFrame => _machine.CurrentFrame;
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
                snapshot.Action.LocalFrame != snapshot.Machine.Frame - snapshot.Action.StartFrame)
                throw new InvalidOperationException("Character view action and HFSM hierarchy differ.");
            _machine.RestoreSnapshot(snapshot.Machine);
            Action = snapshot.Action;
        }

        public CharacterPlaybackIntent? Evaluate(int frame, int tickRate,
            ICharacterPresentationSink sink = null)
        {
            if (frame < SnapshotFrame || string.IsNullOrEmpty(Action.Path)) return null;
            var localFrame = checked(Action.LocalFrame + frame - SnapshotFrame);
            return _actions.Evaluate(Action.Path, Action.InstanceId, localFrame, frame, tickRate, sink);
        }
    }
}

using System;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudInputEventBridge : IDisposable
    {
        private readonly BattleHudInputEventDispatcher _events;
        private readonly BattleHudInputEventBinding _binding;

        public BattleHudInputEventBridge(
            IBattleHudInputSink hudInput,
            BattleHudInputEventBridgeFactory factory = null)
        {
            factory ??= new BattleHudInputEventBridgeFactory();

            _events = factory.CreateDispatcher(hudInput);
            _binding = factory.CreateBinding(_events);
        }

        public void Bind(BattleHudInputUi ui)
        {
            _binding.Bind(ui);
        }

        public void Unbind()
        {
            _binding.Unbind();
        }

        public void SetSkillSpecs(System.Collections.Generic.IReadOnlyDictionary<int, BattleHudSkillPresentationSpec> skillSpecs)
        {
            _events.SetSkillSpecs(skillSpecs);
        }

        public void ResetHudAim()
        {
            _events.ResetHudAim();
        }

        public void Dispose()
        {
            ResetHudAim();
            Unbind();
        }
    }

    internal sealed class BattleHudInputEventBridgeFactory
    {
        public BattleHudInputEventDispatcher CreateDispatcher(IBattleHudInputSink hudInput)
        {
            return new BattleHudInputEventDispatcher(hudInput);
        }

        public BattleHudInputEventBinding CreateBinding(BattleHudInputEventDispatcher events)
        {
            return new BattleHudInputEventBinding(events);
        }
    }
}

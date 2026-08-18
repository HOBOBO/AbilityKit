#nullable enable
using System;

namespace AbilityKit.Ability.HotReload
{
    internal sealed class HotfixSystemProxy : global::Entitas.IExecuteSystem, global::Entitas.ICleanupSystem, global::Entitas.ITearDownSystem
    {
        private readonly Action _onTearDown;
        private global::Entitas.Systems? _current;

        public HotfixSystemProxy(Action onTearDown)
        {
            _onTearDown = onTearDown ?? throw new ArgumentNullException(nameof(onTearDown));
        }

        internal void SetCurrent(global::Entitas.Systems? next)
        {
            _current = next;
        }

        public void Execute()
        {
            _current?.Execute();
        }

        public void Cleanup()
        {
            _current?.Cleanup();
        }

        public void TearDown()
        {
            _onTearDown();
        }
    }
}

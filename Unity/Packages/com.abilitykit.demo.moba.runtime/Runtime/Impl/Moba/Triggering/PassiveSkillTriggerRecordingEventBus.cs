using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Impl.Moba.Rollback;
using AbilityKit.Ability.Share.Impl.Moba.Systems;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Ability.Impl.Moba.Triggering
{
    public sealed class PassiveSkillTriggerRecordingEventBus : IEventBus
    {
        private readonly IEventBus _inner;
        private readonly IFrameTime _frameTime;
        private readonly PassiveSkillTriggerEventRollbackLog _log;

        public PassiveSkillTriggerRecordingEventBus(IEventBus inner, IFrameTime frameTime, PassiveSkillTriggerEventRollbackLog log)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _frameTime = frameTime;
            _log = log;
        }

        public void Publish<TArgs>(EventKey<TArgs> key, in TArgs args)
        {
            RecordIfPassiveSkillTrigger(key, in args);
            _inner.Publish(key, in args);
        }

        public void Publish<TArgs>(EventKey<TArgs> key, in TArgs args, ExecutionControl control)
        {
            RecordIfPassiveSkillTrigger(key, in args);
            _inner.Publish(key, in args, control);
        }

        public IDisposable Subscribe<TArgs>(EventKey<TArgs> key, Action<TArgs> handler)
        {
            return _inner.Subscribe(key, handler);
        }

        public IDisposable Subscribe<TArgs>(EventKey<TArgs> key, Action<TArgs, ExecutionControl> handler)
        {
            return _inner.Subscribe(key, handler);
        }

        public void Flush()
        {
            _inner.Flush();
        }

        private void RecordIfPassiveSkillTrigger<TArgs>(EventKey<TArgs> key, in TArgs args)
        {
            if (_log == null || _frameTime == null) return;

            if (typeof(TArgs) != typeof(PassiveSkillTriggerEventArgs)) return;

            var expectedKey = (EventKey<TArgs>)(object)PassiveSkillTriggerEventArgs.EventKey;
            if (!key.Equals(expectedKey)) return;

            var evt = (PassiveSkillTriggerEventArgs)(object)args;
            _log.Record(_frameTime.Frame, in evt);
        }
    }
}

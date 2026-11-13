using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaCombatActivityService))]
    public sealed class MobaCombatActivityService : IService
    {
        private readonly IWorldClock _clock;
        private Dictionary<int, float> _lastCombatTimeByActorId = new Dictionary<int, float>();

        public MobaCombatActivityService(IWorldClock clock)
        {
            _clock = clock;
        }

        public void RecordCombat(int actorId)
        {
            if (actorId <= 0) return;
            _lastCombatTimeByActorId[actorId] = CurrentTime;
        }

        public bool IsOutOfCombat(int actorId, float thresholdSeconds)
        {
            if (actorId <= 0) return false;
            if (thresholdSeconds <= 0f) return true;
            if (!_lastCombatTimeByActorId.TryGetValue(actorId, out var lastTime)) return true;
            return CurrentTime - lastTime >= thresholdSeconds;
        }

        public bool TryGetLastCombatTime(int actorId, out float time)
        {
            if (actorId <= 0)
            {
                time = default;
                return false;
            }

            return _lastCombatTimeByActorId.TryGetValue(actorId, out time);
        }

        public bool RemoveActor(int actorId)
        {
            return actorId > 0 && _lastCombatTimeByActorId.Remove(actorId);
        }

        internal float CurrentTime => _clock != null ? _clock.Time : 0f;

        internal Dictionary<int, float> CaptureCombatTimes()
            => new Dictionary<int, float>(_lastCombatTimeByActorId);

        // The rollback owner supplies a detached, fully validated map and transfers ownership.
        // No gameplay or continuous reconciliation is run during this synchronous exchange.
        internal void RestoreCombatTimes(Dictionary<int, float> prepared)
        {
            _lastCombatTimeByActorId = prepared ?? throw new System.ArgumentNullException(nameof(prepared));
        }

        public void Dispose()
        {
            _lastCombatTimeByActorId.Clear();
        }
    }
}

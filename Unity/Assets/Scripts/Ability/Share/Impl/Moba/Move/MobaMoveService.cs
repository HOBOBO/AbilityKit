using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Impl.Moba.Move
{
    public sealed class MobaMoveService
    {
        private readonly Dictionary<int, MoveController> _controllers = new Dictionary<int, MoveController>();
        private readonly MovePolicy _policy = new MovePolicy();

        public IMoveCollisionResolver Collision { get; set; } = new NoCollisionResolver();

        public float InputSpeed { get; set; } = 4.5f;

        private MoveController GetOrCreate(int actorId)
        {
            if (actorId <= 0) return null;
            if (_controllers.TryGetValue(actorId, out var c)) return c;
            c = new MoveController(_policy, InputSpeed);
            _controllers[actorId] = c;
            return c;
        }

        public void SetDisableMask(int actorId, MobaMoveDisableMask mask)
        {
            var c = GetOrCreate(actorId);
            c?.SetDisableMask(mask);
        }

        public void AddDisable(int actorId, MobaMoveDisableMask mask)
        {
            var c = GetOrCreate(actorId);
            c?.AddDisable(mask);
        }

        public void RemoveDisable(int actorId, MobaMoveDisableMask mask)
        {
            var c = GetOrCreate(actorId);
            c?.RemoveDisable(mask);
        }

        public bool TryGetController(int actorId, out MoveController controller)
        {
            return _controllers.TryGetValue(actorId, out controller);
        }

        public (int actorId, MoveController.ControllerSnapshot snapshot)[] ExportAll()
        {
            if (_controllers.Count == 0) return Array.Empty<(int, MoveController.ControllerSnapshot)>();

            var tmp = new List<(int, MoveController.ControllerSnapshot)>(_controllers.Count);
            foreach (var kv in _controllers)
            {
                if (kv.Value == null) continue;
                tmp.Add((kv.Key, kv.Value.ExportState()));
            }

            tmp.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            return tmp.ToArray();
        }

        public void ImportAll((int actorId, MoveController.ControllerSnapshot snapshot)[] entries)
        {
            if (entries == null || entries.Length == 0) return;

            for (int i = 0; i < entries.Length; i++)
            {
                var actorId = entries[i].actorId;
                var c = GetOrCreate(actorId);
                c?.ImportState(entries[i].snapshot);
            }
        }

        public void SetInput(int actorId, float dx, float dz)
        {
            var c = GetOrCreate(actorId);
            c?.SetInput(dx, dz);
        }

        public void Dash(int actorId, in Vec3 velocity, float duration, int priority = 10)
        {
            var c = GetOrCreate(actorId);
            c?.Dash(velocity, duration, priority);
        }

        public void Knock(int actorId, in Vec3 velocity, float duration, float gravity = 9.8f, int priority = 100)
        {
            var c = GetOrCreate(actorId);
            c?.Knock(velocity, duration, gravity, priority);
        }

        public Vec3 Tick(int actorId, in Vec3 position, float dt)
        {
            var c = GetOrCreate(actorId);
            if (c == null) return Vec3.Zero;
            var desired = c.Tick(dt);
            return Collision != null ? Collision.ResolveDelta(actorId, position, desired) : desired;
        }

        public void RemoveActor(int actorId)
        {
            _controllers.Remove(actorId);
        }
    }
}

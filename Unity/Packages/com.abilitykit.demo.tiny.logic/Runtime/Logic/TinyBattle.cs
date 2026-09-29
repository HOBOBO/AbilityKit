using System;
using System.Collections.Generic;
using System.Linq;

namespace AbilityKit.Demo.Tiny
{
    public readonly struct TinyActorState
    {
        public TinyActorState(uint playerId, int x, int y, int hp, int cooldownFrames)
        {
            PlayerId = playerId;
            X = x;
            Y = y;
            Hp = hp;
            CooldownFrames = cooldownFrames;
        }

        public uint PlayerId { get; }
        public int X { get; }
        public int Y { get; }
        public int Hp { get; }
        public int CooldownFrames { get; }
    }

    public readonly struct TinyBattleState
    {
        public TinyBattleState(int frame, TinyActorState[] actors)
        {
            Frame = frame;
            Actors = actors;
        }

        public int Frame { get; }
        public TinyActorState[] Actors { get; }
    }

    public sealed class TinyBattle
    {
        public const string AssetKey = "tiny:arena";
        public const string RulesKey = "tiny:rules.v1";
        public const int InputOpCode = 1;
        public const int MaxHp = 100;
        public const int AttackDamage = 10;
        public const int AttackRangeSquared = 9;
        public const int AttackCooldownFrames = 30;

        private readonly SortedDictionary<uint, TinyActorState> _actors = new SortedDictionary<uint, TinyActorState>();
        private readonly Dictionary<uint, TinyInput> _inputs = new Dictionary<uint, TinyInput>();

        public int Frame { get; private set; }
        public IReadOnlyCollection<TinyActorState> Actors => _actors.Values;

        public void AddPlayer(uint playerId, int x, int y)
        {
            if (playerId == 0 || (_actors.Count >= 2 && !_actors.ContainsKey(playerId)))
                throw new ArgumentOutOfRangeException(nameof(playerId));

            if (!_actors.ContainsKey(playerId))
                _actors.Add(playerId, new TinyActorState(playerId, x, y, MaxHp, 0));
        }

        public bool ContainsPlayer(uint playerId) => _actors.ContainsKey(playerId);

        public TinyBattleState CaptureState() => new TinyBattleState(Frame, _actors.Values.ToArray());

        public void RestoreState(TinyBattleState state)
        {
            if (state.Frame < 0 || state.Actors == null || state.Actors.Length < 1 || state.Actors.Length > 2 ||
                state.Actors.Any(actor => actor.PlayerId == 0 || actor.Hp < 0 || actor.Hp > MaxHp ||
                    actor.CooldownFrames < 0 || actor.CooldownFrames > AttackCooldownFrames) ||
                state.Actors.Select(actor => actor.PlayerId).Distinct().Count() != state.Actors.Length)
                throw new ArgumentException("Invalid Tiny battle state.", nameof(state));

            _actors.Clear();
            foreach (var actor in state.Actors) _actors.Add(actor.PlayerId, actor);
            _inputs.Clear();
            Frame = state.Frame;
        }

        public void Submit(uint playerId, TinyInput input)
        {
            if (!input.IsValid || !_actors.ContainsKey(playerId))
                throw new ArgumentException("Unknown player or invalid Tiny input.", nameof(input));

            _inputs[playerId] = input;
        }

        public void Tick()
        {
            var ids = _actors.Keys.ToArray();
            foreach (var id in ids)
            {
                var actor = _actors[id];
                TinyInput input;
                _inputs.TryGetValue(id, out input);
                if (actor.Hp <= 0) continue;

                actor = new TinyActorState(id, actor.X + input.MoveX, actor.Y + input.MoveY,
                    actor.Hp, Math.Max(0, actor.CooldownFrames - 1));
                _actors[id] = actor;
                if (!input.Attack || actor.CooldownFrames != 0) continue;

                foreach (var targetId in ids)
                {
                    if (targetId == id) continue;
                    var target = _actors[targetId];
                    var dx = (long)actor.X - target.X;
                    var dy = (long)actor.Y - target.Y;
                    if (target.Hp <= 0 || dx * dx + dy * dy > AttackRangeSquared) continue;

                    _actors[targetId] = new TinyActorState(targetId, target.X, target.Y,
                        Math.Max(0, target.Hp - AttackDamage), target.CooldownFrames);
                    _actors[id] = new TinyActorState(id, actor.X, actor.Y, actor.Hp, AttackCooldownFrames);
                    break;
                }
            }

            _inputs.Clear();
            Frame++;
        }

        public uint ComputeHash()
        {
            const uint prime = 16777619;
            var hash = 2166136261u;
            unchecked
            {
                hash = (hash ^ (uint)Frame) * prime;
                foreach (var actor in _actors.Values)
                {
                    hash = (hash ^ actor.PlayerId) * prime;
                    hash = (hash ^ (uint)actor.X) * prime;
                    hash = (hash ^ (uint)actor.Y) * prime;
                    hash = (hash ^ (uint)actor.Hp) * prime;
                    hash = (hash ^ (uint)actor.CooldownFrames) * prime;
                }
            }

            return hash;
        }
    }
}

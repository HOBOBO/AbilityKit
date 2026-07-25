#nullable enable

using System;
using AbilityKit.World.Svelto;
using Svelto.DataStructures;
using Svelto.ECS;
using Svelto.ECS.Internal;

namespace AbilityKit.Demo.Shooter.Runtime
{
    internal sealed class ShooterEnemyWaveCombatModule
    {
        private const int MaxEnemyAttackEventsPerFrame = 64;

        private readonly ShooterBattleState _state;
        private readonly IShooterEntityManager _entities;
        private readonly ShooterCombatEventBuffer _events;
        private readonly ISveltoWorldContext _context;
        private readonly int _attackIntervalFrames;
        private readonly int _attackDamage;
        private ShooterSpatialTargetIndex _targetIndex => _state.PlayerTargetIndex;

        public ShooterEnemyWaveCombatModule(ShooterBattleState state, IShooterEntityManager entities)
            : this(state, entities, ShooterEnemyWaveOptions.DefaultEnabled)
        {
        }

        public ShooterEnemyWaveCombatModule(ShooterBattleState state, IShooterEntityManager entities, ShooterEnemyWaveOptions options)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _events = new ShooterCombatEventBuffer(_state);
            _context = _entities.SveltoContext;
            var activeOptions = options ?? ShooterEnemyWaveOptions.DefaultEnabled;
            _attackIntervalFrames = activeOptions.EnemyAttackIntervalFrames;
            _attackDamage = activeOptions.EnemyAttackDamage;
        }

        public void Tick()
        {
            if (_state.CurrentFrame % _attackIntervalFrames != 0 || _entities.PlayerCount == 0 || _entities.EnemyCount == 0)
            {
                return;
            }

            var playerCollection = _context.EntitiesDB.QueryEntities<ShooterSveltoPlayerComponent>(ShooterSveltoGroups.Players);
            playerCollection.Deconstruct(out NB<ShooterSveltoPlayerComponent> players, out _, out var playerCount);
            if (playerCount == 0)
            {
                return;
            }

            var emittedEvents = 0;
            var (transforms, healths, ids, count) = _context.EntitiesDB.QueryEntities<ShooterSveltoTransformComponent, ShooterSveltoHealthComponent>((ExclusiveGroupStruct)ShooterSveltoGroups.GameplayTargets);
            _targetIndex.Rebuild(players, playerCount, _state.CurrentFrame);
            if (_targetIndex.TryGetOnlyLivePlayer(out var onlyPlayerIndex, out _))
            {
                AttackSinglePlayer(players, onlyPlayerIndex, transforms, healths, ids, count, ref emittedEvents);
                return;
            }

            AttackNearestPlayers(players, transforms, healths, ids, count, ref emittedEvents);
        }

        private void AttackSinglePlayer(
            NB<ShooterSveltoPlayerComponent> players,
            int playerIndex,
            NB<ShooterSveltoTransformComponent> transforms,
            NB<ShooterSveltoHealthComponent> healths,
            NativeEntityIDs ids,
            int count,
            ref int emittedEvents)
        {
            if (playerIndex < 0 || !players[playerIndex].Alive || players[playerIndex].Hp <= 0)
            {
                return;
            }

            var damage = 0;
            var playerId = players[playerIndex].PlayerId;
            for (var i = 0; i < count; i++)
            {
                if (healths[i].Alive == 0 || healths[i].Current <= 0)
                {
                    continue;
                }

                damage += _attackDamage;
                AddEnemyAttackEvent(ids[i], playerId, transforms[i].X, transforms[i].Y, ref emittedEvents);
            }

            ApplyDamage(ref players[playerIndex], damage);
        }

        private void AttackNearestPlayers(
            NB<ShooterSveltoPlayerComponent> players,
            NB<ShooterSveltoTransformComponent> transforms,
            NB<ShooterSveltoHealthComponent> healths,
            NativeEntityIDs ids,
            int count,
            ref int emittedEvents)
        {
            for (var i = 0; i < count; i++)
            {
                if (healths[i].Alive == 0 || healths[i].Current <= 0)
                {
                    continue;
                }

                if (!_targetIndex.TryFindNearestPlayer(
                    transforms[i].X,
                    transforms[i].Y,
                    selfPlayerId: 0,
                    out var playerIndex,
                    out var player,
                    out _))
                {
                    continue;
                }

                ApplyDamage(ref players[playerIndex], _attackDamage);
                AddEnemyAttackEvent(ids[i], player.PlayerId, transforms[i].X, transforms[i].Y, ref emittedEvents);
            }
        }

        private void ApplyDamage(ref ShooterSveltoPlayerComponent player, int damage)
        {
            if (damage <= 0 || !player.Alive || player.Hp <= 0)
            {
                return;
            }

            player.Hp = Math.Max(0, player.Hp - damage);
            if (player.Hp == 0)
            {
                player.Alive = false;
            }
        }

        private void AddEnemyAttackEvent(uint enemyId, int playerId, float x, float y, ref int emittedEvents)
        {
            if (emittedEvents >= MaxEnemyAttackEventsPerFrame)
            {
                return;
            }

            emittedEvents++;
            _events.AddEnemyAttack(enemyId, playerId, x, y, _attackDamage);
        }
    }
}

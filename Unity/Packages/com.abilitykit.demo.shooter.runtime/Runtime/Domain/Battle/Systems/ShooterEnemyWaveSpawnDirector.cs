#nullable enable

using System;

namespace AbilityKit.Demo.Shooter.Runtime
{
    internal sealed class ShooterEnemyWaveSpawnDirector
    {
        private const float Pi = 3.14159265358979323846f;
        private const float ClearedWaveAdvanceDelaySeconds = 2f;
        private readonly ShooterEnemyWaveOptions _options;
        private readonly ShooterSveltoGameplayWaveConfig[] _waves;
        private readonly ShooterEnemyWaveProgress _progress;
        private readonly ShooterEnemyIdAllocator _allocator;
        private readonly IShooterEntityManager _entities;
        private readonly ShooterArenaGameplayOptions _arenaOptions;
        private readonly int[] _earlyStartFrames;
        private float _clearedWaveElapsedSeconds;

        public ShooterEnemyWaveSpawnDirector(
            ShooterEnemyWaveOptions options,
            ShooterEnemyWaveProgress progress,
            ShooterEnemyIdAllocator allocator,
            IShooterEntityManager entities)
            : this(options, progress, allocator, entities, ShooterArenaGameplayOptions.Disabled)
        {
        }

        public ShooterEnemyWaveSpawnDirector(
            ShooterEnemyWaveOptions options,
            ShooterEnemyWaveProgress progress,
            ShooterEnemyIdAllocator allocator,
            IShooterEntityManager entities,
            ShooterArenaGameplayOptions arenaOptions)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _arenaOptions = arenaOptions ?? ShooterArenaGameplayOptions.Disabled;
            _waves = _options.Waves;
            _earlyStartFrames = new int[_waves.Length];
            ResetEarlyStartState();
        }

        public void Reset()
        {
            _progress.Reset();
            _allocator.Reset();
            ResetEarlyStartState();
        }

        public void SynchronizeFromImportedTargets(int importedSpawnCount)
        {
            _progress.RestoreFromSpawnCount(importedSpawnCount, _waves);
            ResetEarlyStartState();
        }

        public void Tick(ShooterBattleState state, float deltaTime)
        {
            if (!_options.Enabled || _waves.Length == 0)
            {
                return;
            }

            var activeEnemies = _entities.EnemyCount;
            UpdateClearedWaveAdvance(state, deltaTime, activeEnemies);
            for (var i = 0; i < _waves.Length; i++)
            {
                TickWave(state, i, in _waves[i], ref activeEnemies);
            }
        }

        private void TickWave(ShooterBattleState state, int index, in ShooterSveltoGameplayWaveConfig wave, ref int activeEnemies)
        {
            var effectiveStartFrame = _earlyStartFrames[index] >= 0
                ? _earlyStartFrames[index]
                : wave.StartFrame;
            if (state.CurrentFrame < effectiveStartFrame || _progress.GetSpawned(index) >= wave.EnemyCount || activeEnemies >= _options.MaxActiveEnemies)
            {
                return;
            }

            var framesSinceStart = state.CurrentFrame - effectiveStartFrame;
            if (framesSinceStart % wave.SpawnFrameInterval != 0)
            {
                return;
            }

            SpawnEnemy(wave.WaveId, _progress.GetSpawned(index), wave.EnemyHp, wave.SpawnRadius);
            _progress.Increment(index);
            activeEnemies++;
        }

        private void UpdateClearedWaveAdvance(ShooterBattleState state, float deltaTime, int activeEnemies)
        {
            var nextGroupStartIndex = FindNextLockedGroupStartIndex(state.CurrentFrame);
            if (nextGroupStartIndex < 0 || activeEnemies > 0)
            {
                _clearedWaveElapsedSeconds = 0f;
                return;
            }

            _clearedWaveElapsedSeconds += Math.Max(0f, deltaTime);
            if (_clearedWaveElapsedSeconds < ClearedWaveAdvanceDelaySeconds)
            {
                return;
            }

            var groupStartFrame = _waves[nextGroupStartIndex].StartFrame;
            for (var i = nextGroupStartIndex; i < _waves.Length && _waves[i].StartFrame == groupStartFrame; i++)
            {
                _earlyStartFrames[i] = state.CurrentFrame;
            }

            _clearedWaveElapsedSeconds = 0f;
        }

        private int FindNextLockedGroupStartIndex(int currentFrame)
        {
            var index = 0;
            while (index < _waves.Length)
            {
                var groupStartIndex = index;
                var groupStartFrame = _waves[index].StartFrame;
                var groupComplete = true;
                var groupStarted = currentFrame >= groupStartFrame || _earlyStartFrames[index] >= 0;
                while (index < _waves.Length && _waves[index].StartFrame == groupStartFrame)
                {
                    groupComplete &= _progress.GetSpawned(index) >= _waves[index].EnemyCount;
                    index++;
                }

                if (!groupStarted)
                {
                    return groupStartIndex == 0 ? -1 : groupStartIndex;
                }

                if (!groupComplete)
                {
                    return -1;
                }
            }

            return -1;
        }

        private void ResetEarlyStartState()
        {
            for (var i = 0; i < _earlyStartFrames.Length; i++)
            {
                _earlyStartFrames[i] = -1;
            }

            _clearedWaveElapsedSeconds = 0f;
        }

        private void SpawnEnemy(int waveId, int spawnIndex, int enemyHp, float spawnRadius)
        {
            var enemyId = _allocator.Allocate();
            var angle = (waveId * 97 + spawnIndex * 37) * Pi / 180f;
            var activeSpawnRadius = ShooterCircularArenaMath.ClampSpawnRadius(spawnRadius, _arenaOptions);
            var x = MathF.Cos(angle) * activeSpawnRadius;
            var y = MathF.Sin(angle) * activeSpawnRadius;
            var directionX = -x;
            var directionY = -y;
            Normalize(ref directionX, ref directionY);

            var transform = new ShooterSveltoTransformComponent
            {
                X = x,
                Y = y,
                DirectionX = directionX,
                DirectionY = directionY
            };
            var health = new ShooterSveltoHealthComponent
            {
                Current = enemyHp,
                Max = enemyHp,
                Alive = 1
            };
            _entities.AddEnemy(enemyId, in transform, in health);
        }

        private static void Normalize(ref float x, ref float y)
        {
            var lengthSquared = x * x + y * y;
            if (lengthSquared <= 0.000001f)
            {
                x = 1f;
                y = 0f;
                return;
            }

            var inv = 1f / MathF.Sqrt(lengthSquared);
            x *= inv;
            y *= inv;
        }
    }
}

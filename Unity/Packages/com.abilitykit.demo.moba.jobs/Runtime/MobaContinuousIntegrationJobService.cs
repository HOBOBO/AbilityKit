using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace AbilityKit.Demo.Moba.Jobs
{
    public readonly struct MobaContinuousIntegrationInput
    {
        public MobaContinuousIntegrationInput(int actorId, int value, int ratePerTick, int deltaTicks, int minimum, int maximum)
        {
            ActorId = actorId;
            Value = value;
            RatePerTick = ratePerTick;
            DeltaTicks = deltaTicks;
            Minimum = minimum;
            Maximum = maximum;
        }

        public int ActorId { get; }
        public int Value { get; }
        public int RatePerTick { get; }
        public int DeltaTicks { get; }
        public int Minimum { get; }
        public int Maximum { get; }
    }

    public readonly struct MobaContinuousIntegrationResult
    {
        public MobaContinuousIntegrationResult(int actorId, int value, bool clamped)
        {
            ActorId = actorId;
            Value = value;
            Clamped = clamped;
        }

        public int ActorId { get; }
        public int Value { get; }
        public bool Clamped { get; }
    }

    public static class MobaContinuousIntegrationReference
    {
        public static MobaContinuousIntegrationResult Calculate(in MobaContinuousIntegrationInput input)
        {
            var value = CalculateValue(in input);
            return new MobaContinuousIntegrationResult(input.ActorId, value, value != CalculateUnclampedValue(in input));
        }

        public static ulong CalculateHash(NativeArray<MobaContinuousIntegrationResult> results)
        {
            var hash = 1469598103934665603UL;
            for (var index = 0; index < results.Length; index++)
            {
                var result = results[index];
                hash = Mix(hash, unchecked((uint)result.ActorId));
                hash = Mix(hash, unchecked((uint)result.Value));
                hash = Mix(hash, result.Clamped ? 1u : 0u);
            }

            return hash;
        }

        internal static int CalculateValue(in MobaContinuousIntegrationInput input)
        {
            var unclamped = CalculateUnclampedValue(in input);
            return Clamp(unclamped, input.Minimum, input.Maximum);
        }

        internal static int CalculateUnclampedValue(in MobaContinuousIntegrationInput input)
        {
            var product = (long)input.RatePerTick * input.DeltaTicks;
            var value = (long)input.Value + product;
            if (value > int.MaxValue) return int.MaxValue;
            if (value < int.MinValue) return int.MinValue;
            return (int)value;
        }

        internal static int Clamp(int value, int minimum, int maximum)
        {
            if (minimum > maximum)
            {
                var temporary = minimum;
                minimum = maximum;
                maximum = temporary;
            }

            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }

        private static ulong Mix(ulong hash, uint value)
        {
            hash ^= value;
            return hash * 1099511628211UL;
        }
    }

    public sealed class MobaContinuousIntegrationJobService : IDisposable
    {
        private NativeArray<MobaContinuousIntegrationInput> _inputs;
        private NativeArray<MobaContinuousIntegrationResult> _results;
        private bool _disposed;
        private JobHandle _pending;
        private bool _hasPending;

        public MobaContinuousIntegrationJobService(int capacity = 64)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _inputs = new NativeArray<MobaContinuousIntegrationInput>(capacity, Allocator.Persistent);
            _results = new NativeArray<MobaContinuousIntegrationResult>(capacity, Allocator.Persistent);
        }

        public bool IsAvailable => !_disposed;
        public int Capacity => _inputs.IsCreated ? _inputs.Length : 0;

        public JobHandle Schedule(NativeArray<MobaContinuousIntegrationInput> inputs)
        {
            EnsureAvailable();
            if (!inputs.IsCreated) throw new ArgumentException("Input array must be created.", nameof(inputs));
            if (inputs.Length > Capacity) throw new ArgumentException("Input array exceeds service capacity.", nameof(inputs));

            Complete();
            NativeArray<MobaContinuousIntegrationInput>.Copy(inputs, _inputs, inputs.Length);
            var job = new IntegrateJob
            {
                Inputs = _inputs,
                Results = _results,
                Count = inputs.Length
            };
            _pending = job.Schedule(inputs.Length, 32);
            _hasPending = true;
            return _pending;
        }

        public void Complete()
        {
            if (!_hasPending) return;
            _pending.Complete();
            _hasPending = false;
        }

        public NativeArray<MobaContinuousIntegrationResult> Results
        {
            get
            {
                EnsureAvailable();
                Complete();
                return _results;
            }
        }

        public ulong CalculateResultHash(int count)
        {
            EnsureAvailable();
            Complete();
            if (count < 0 || count > _results.Length) throw new ArgumentOutOfRangeException(nameof(count));
            var hash = 1469598103934665603UL;
            for (var index = 0; index < count; index++)
            {
                var result = _results[index];
                hash ^= unchecked((uint)result.ActorId);
                hash *= 1099511628211UL;
                hash ^= unchecked((uint)result.Value);
                hash *= 1099511628211UL;
                hash ^= result.Clamped ? 1u : 0u;
                hash *= 1099511628211UL;
            }

            return hash;
        }

        public void Dispose()
        {
            if (_disposed) return;
            Complete();
            if (_inputs.IsCreated) _inputs.Dispose();
            if (_results.IsCreated) _results.Dispose();
            _disposed = true;
        }

        private void EnsureAvailable()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MobaContinuousIntegrationJobService));
        }

        [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard)]
        private struct IntegrateJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<MobaContinuousIntegrationInput> Inputs;
            [WriteOnly] public NativeArray<MobaContinuousIntegrationResult> Results;
            public int Count;

            public void Execute(int index)
            {
                if (index >= Count) return;
                var input = Inputs[index];
                var value = MobaContinuousIntegrationReference.CalculateValue(in input);
                var unclamped = MobaContinuousIntegrationReference.CalculateUnclampedValue(in input);
                Results[index] = new MobaContinuousIntegrationResult(
                    input.ActorId,
                    value,
                    value != unclamped);
            }
        }
    }
}

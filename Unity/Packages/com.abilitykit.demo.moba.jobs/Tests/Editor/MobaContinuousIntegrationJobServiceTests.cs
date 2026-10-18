using System;
using System.Diagnostics;
using Unity.Collections;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Jobs.Tests
{
    public sealed class MobaContinuousIntegrationJobServiceTests
    {
        [Test]
        public void JobResultsMatchSingleThreadReferenceAndHash()
        {
            var inputs = new NativeArray<MobaContinuousIntegrationInput>(5, Allocator.TempJob);
            try
            {
                inputs[0] = new MobaContinuousIntegrationInput(10, 100, 3, 4, 0, 1000);
                inputs[1] = new MobaContinuousIntegrationInput(20, 100, -40, 2, 0, 100);
                inputs[2] = new MobaContinuousIntegrationInput(30, 0, 100, 20, -500, 500);
                inputs[3] = new MobaContinuousIntegrationInput(40, int.MaxValue, 1, 10, 0, int.MaxValue);
                inputs[4] = new MobaContinuousIntegrationInput(50, int.MinValue, -1, 10, int.MinValue, 0);

                using (var service = new MobaContinuousIntegrationJobService(capacity: inputs.Length))
                {
                    service.Schedule(inputs);
                    service.Complete();
                    var results = service.Results;
                    var expected = new NativeArray<MobaContinuousIntegrationResult>(inputs.Length, Allocator.Temp);
                    try
                    {
                        for (var index = 0; index < inputs.Length; index++)
                        {
                            var input = inputs[index];
                            expected[index] = MobaContinuousIntegrationReference.Calculate(in input);
                            Assert.AreEqual(expected[index].ActorId, results[index].ActorId);
                            Assert.AreEqual(expected[index].Value, results[index].Value);
                            Assert.AreEqual(expected[index].Clamped, results[index].Clamped);
                        }

                        Assert.AreEqual(
                            MobaContinuousIntegrationReference.CalculateHash(expected),
                            service.CalculateResultHash(inputs.Length));
                    }
                    finally
                    {
                        expected.Dispose();
                    }
                }
            }
            finally
            {
                inputs.Dispose();
            }
        }

        [Test]
        public void ReferenceNormalizesReversedBoundsAndDetectsClamp()
        {
            var input = new MobaContinuousIntegrationInput(7, 5, 10, 1, 20, 0);
            var result = MobaContinuousIntegrationReference.Calculate(in input);

            Assert.AreEqual(15, result.Value);
            Assert.IsFalse(result.Clamped);

            input = new MobaContinuousIntegrationInput(7, 5, -10, 1, 20, 0);
            result = MobaContinuousIntegrationReference.Calculate(in input);
            Assert.AreEqual(0, result.Value);
            Assert.IsTrue(result.Clamped);
        }

        [Test]
        public void ServiceRejectsInvalidCapacityAndInputLength()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MobaContinuousIntegrationJobService(0));

            using (var service = new MobaContinuousIntegrationJobService(1))
            using (var inputs = new NativeArray<MobaContinuousIntegrationInput>(2, Allocator.TempJob))
            {
                Assert.Throws<ArgumentException>(() => service.Schedule(inputs));
            }
        }

        [Test]
        public void ServiceRejectsUseAfterDispose()
        {
            var service = new MobaContinuousIntegrationJobService(1);
            service.Dispose();

            Assert.IsFalse(service.IsAvailable);
            Assert.Throws<ObjectDisposedException>(() => service.CalculateResultHash(0));
        }

        [Test]
        public void BatchBenchmarkReportsReferenceAndJobBaseline()
        {
            var counts = new[] { 2, 10, 20, 128 };
            for (var caseIndex = 0; caseIndex < counts.Length; caseIndex++)
            {
                var count = counts[caseIndex];
                var inputs = new NativeArray<MobaContinuousIntegrationInput>(count, Allocator.TempJob);
                var expected = new NativeArray<MobaContinuousIntegrationResult>(count, Allocator.Temp);
                try
                {
                    using (var service = new MobaContinuousIntegrationJobService(count))
                    {
                        for (var index = 0; index < count; index++)
                        {
                            inputs[index] = new MobaContinuousIntegrationInput(
                                index + 1,
                                1000 + index,
                                (index & 1) == 0 ? 3 : -2,
                                4 + (index % 7),
                                0,
                                2000);
                        }

                        for (var index = 0; index < count; index++)
                        {
                            var input = inputs[index];
                            expected[index] = MobaContinuousIntegrationReference.Calculate(in input);
                        }

                        service.Schedule(inputs);
                        service.Complete();

                        var warmupHash = service.CalculateResultHash(count);
                        var expectedHash = MobaContinuousIntegrationReference.CalculateHash(expected);
                        Assert.AreEqual(expectedHash, warmupHash);

                        var referenceAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                        var referenceStarted = Stopwatch.GetTimestamp();
                        for (var iteration = 0; iteration < 32; iteration++)
                        {
                            for (var index = 0; index < count; index++)
                            {
                                var input = inputs[index];
                                expected[index] = MobaContinuousIntegrationReference.Calculate(in input);
                            }
                        }
                        var referenceElapsed = Stopwatch.GetTimestamp() - referenceStarted;
                        var referenceAllocated = GC.GetAllocatedBytesForCurrentThread() - referenceAllocatedBefore;

                        var jobAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                        var jobStarted = Stopwatch.GetTimestamp();
                        for (var iteration = 0; iteration < 32; iteration++)
                        {
                            service.Schedule(inputs);
                            service.Complete();
                            service.CalculateResultHash(count);
                        }
                        var jobElapsed = Stopwatch.GetTimestamp() - jobStarted;
                        var jobAllocated = GC.GetAllocatedBytesForCurrentThread() - jobAllocatedBefore;

                        Assert.AreEqual(expectedHash, service.CalculateResultHash(count));
                        UnityEngine.Debug.LogFormat(
                            "[MobaContinuousIntegrationBenchmark] count={0}; referenceMs={1:F3}; jobMs={2:F3}; referenceThreadAllocBytes={3}; jobThreadAllocBytes={4}; hash=0x{5:X16}",
                            count,
                            ToMilliseconds(referenceElapsed),
                            ToMilliseconds(jobElapsed),
                            referenceAllocated,
                            jobAllocated,
                            expectedHash);
                    }
                }
                finally
                {
                    expected.Dispose();
                    inputs.Dispose();
                }
            }
        }

        private static double ToMilliseconds(long timestampDelta)
        {
            return timestampDelta * 1000d / Stopwatch.Frequency;
        }
    }
}

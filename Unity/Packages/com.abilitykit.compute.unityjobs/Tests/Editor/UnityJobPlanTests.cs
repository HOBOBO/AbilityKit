#nullable enable

using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace AbilityKit.Compute.UnityJobs.Tests
{
    public sealed class UnityJobPlanTests
    {
        [Test]
        public void Schedule_HonorsFanOutAndFanInDependencies()
        {
            using (var seedValue = new NativeArray<int>(1, Allocator.TempJob))
            using (var leftValue = new NativeArray<int>(1, Allocator.TempJob))
            using (var rightValue = new NativeArray<int>(1, Allocator.TempJob))
            using (var resultValue = new NativeArray<int>(1, Allocator.TempJob))
            {
                var builder = new UnityJobPlanBuilder();
                var seed = builder.Add("seed");
                var left = builder.Add("left", seed);
                var right = builder.Add("right", seed);
                var combine = builder.Add("combine", left, right);
                var plan = builder.Build();

                plan.BeginSchedule();
                plan.Record(seed, new WriteJob { Value = seedValue, Number = 5 }
                    .Schedule(plan.GetDependency(seed)));
                plan.Record(left, new CopyAndAddJob
                {
                    Source = seedValue,
                    Target = leftValue,
                    Add = 2
                }.Schedule(plan.GetDependency(left)));
                plan.Record(right, new CopyAndAddJob
                {
                    Source = seedValue,
                    Target = rightValue,
                    Add = 3
                }.Schedule(plan.GetDependency(right)));
                plan.Record(combine, new CombineJob
                {
                    Left = leftValue,
                    Right = rightValue,
                    Result = resultValue
                }.Schedule(plan.GetDependency(combine)));
                plan.EndSchedule();
                plan.Complete();

                Assert.That(resultValue[0], Is.EqualTo(15));
                Assert.That(plan.IsInFlight, Is.False);
            }
        }

        [Test]
        public void EndSchedule_RejectsMissingNodesAndCompleteResetsPartialExecution()
        {
            using (var value = new NativeArray<int>(1, Allocator.TempJob))
            {
                var builder = new UnityJobPlanBuilder();
                var first = builder.Add("first");
                builder.Add("second", first);
                var plan = builder.Build();

                plan.BeginSchedule();
                plan.Record(first, new WriteJob { Value = value, Number = 7 }
                    .Schedule(plan.GetDependency(first)));

                Assert.Throws<InvalidOperationException>(() => plan.EndSchedule());
                plan.Complete();
                Assert.That(value[0], Is.EqualTo(7));
                Assert.That(plan.IsScheduling, Is.False);
                Assert.That(plan.IsInFlight, Is.False);
            }
        }

        [Test]
        public void BeginSchedule_RejectsScheduleThatHasNotEnded()
        {
            var builder = new UnityJobPlanBuilder();
            builder.Add("node");
            var plan = builder.Build();

            plan.BeginSchedule();

            Assert.Throws<InvalidOperationException>(() => plan.BeginSchedule());
            plan.Complete();
        }

        private struct WriteJob : IJob
        {
            public NativeArray<int> Value;
            public int Number;

            public void Execute()
            {
                Value[0] = Number;
            }
        }

        private struct CopyAndAddJob : IJob
        {
            [ReadOnly] public NativeArray<int> Source;
            [WriteOnly] public NativeArray<int> Target;
            public int Add;

            public void Execute()
            {
                Target[0] = Source[0] + Add;
            }
        }

        private struct CombineJob : IJob
        {
            [ReadOnly] public NativeArray<int> Left;
            [ReadOnly] public NativeArray<int> Right;
            [WriteOnly] public NativeArray<int> Result;

            public void Execute()
            {
                Result[0] = Left[0] + Right[0];
            }
        }
    }

    public sealed class UnityComputeBufferTests
    {
        [Test]
        public void EnsureCapacity_ReusesGrowsAndRejectsAccessAfterDispose()
        {
            var buffer = new UnityComputeBuffer<int>();
            try
            {
                Assert.That(buffer.IsCreated, Is.False);
                var initial = buffer.EnsureCapacity(1);
                initial[0] = 42;
                Assert.That(buffer.Capacity, Is.EqualTo(64));

                var reused = buffer.EnsureCapacity(32);
                Assert.That(buffer.Capacity, Is.EqualTo(64));
                Assert.That(reused[0], Is.EqualTo(42));

                buffer.EnsureCapacity(65);
                Assert.That(buffer.Capacity, Is.EqualTo(128));
            }
            finally
            {
                buffer.Dispose();
            }

            Assert.That(buffer.IsCreated, Is.False);
            Assert.Throws<ObjectDisposedException>(() => buffer.EnsureCapacity(1));
            Assert.Throws<ObjectDisposedException>(() => _ = buffer.Array);
            buffer.Dispose();
        }
    }
}

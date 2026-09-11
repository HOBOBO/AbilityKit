#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbilityKit.Samples.Logic.Ability.Core.Bootstrap;

namespace AbilityKit.Samples.Logic.Ability.Tests
{
    /// <summary>
    /// Bootstrap 框架单元测试。
    /// </summary>
    public static class BootstrapTests
    {
        public static void RunAll()
        {
            TestWorldBlueprint();
            TestBootstrapPipeline();
            TestWorldModule();
            Console.WriteLine("[BootstrapTests] All tests passed!");
        }

        private static void TestWorldBlueprint()
        {
            Console.WriteLine("[Test] WorldBlueprint");

            var blueprint = new WorldBlueprint();

            // Register modules
            var module1 = new TestModule("module1", 1);
            var module2 = new TestModule("module2", 2);

            blueprint.RegisterModule(module1);
            blueprint.RegisterModule(module2);

            // Register services
            blueprint.RegisterService<ITestService>(new TestService());

            // Test module retrieval
            var retrieved = blueprint.GetModule<ITestModule>("module1");
            Assert(retrieved != null, "Module should be retrieved");
            Assert(retrieved.ModuleId == "module1", "Retrieved module should have correct id");

            // Test service retrieval
            var service = blueprint.GetService<ITestService>();
            Assert(service != null, "Service should be retrieved");
            Assert(service.IsValid, "Service should be valid");

            // Test initialization
            blueprint.InitializeModules();
            Assert(module1.IsInitialized, "Module1 should be initialized");
            Assert(module2.IsInitialized, "Module2 should be initialized");

            // Test destruction
            blueprint.DestroyModules();
            Assert(!module1.IsInitialized, "Module1 should be destroyed");
            Assert(!module2.IsInitialized, "Module2 should be destroyed");

            Console.WriteLine("  - WorldBlueprint: OK");
        }

        private static void TestBootstrapPipeline()
        {
            Console.WriteLine("[Test] BootstrapPipeline");

            var pipeline = new BootstrapPipeline();
            var stage1 = new TestStage("stage1", 1);
            var stage2 = new TestStage("stage2", 2);
            var stage3 = new TestStage("stage3", 0);

            pipeline.AddStage(stage1);
            pipeline.AddStage(stage2);
            pipeline.AddStage(stage3);

            // Verify order
            var stages = pipeline.GetStages();
            Assert(stages.Count == 3, "Should have 3 stages");
            Assert(stages[0].StageId == "stage3", "Stage3 should be first (order 0)");
            Assert(stages[1].StageId == "stage1", "Stage1 should be second (order 1)");
            Assert(stages[2].StageId == "stage2", "Stage2 should be third (order 2)");

            // Test execution
            var blueprint = new WorldBlueprint();
            pipeline.ExecuteAsync(blueprint).Wait();

            Assert(stage1.Executed, "Stage1 should be executed");
            Assert(stage2.Executed, "Stage2 should be executed");
            Assert(stage3.Executed, "Stage3 should be executed");
            Assert(stage3.ExecutedOrder < stage1.ExecutedOrder, "Stage3 should execute before stage1");
            Assert(stage1.ExecutedOrder < stage2.ExecutedOrder, "Stage1 should execute before stage2");

            // Test stage removal
            pipeline.RemoveStage("stage2");
            Assert(pipeline.GetStages().Count == 2, "Should have 2 stages after removal");

            Console.WriteLine("  - BootstrapPipeline: OK");
        }

        private static void TestWorldModule()
        {
            Console.WriteLine("[Test] WorldModule");

            var module = new TestModule("test_module", 10);

            Assert(!module.IsInitialized, "Module should not be initialized initially");

            module.Initialize();
            Assert(module.IsInitialized, "Module should be initialized after Initialize()");

            module.Destroy();
            Assert(!module.IsInitialized, "Module should not be initialized after Destroy()");

            Console.WriteLine("  - WorldModule: OK");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception($"Assertion failed: {message}");
        }

        /// <summary>
        /// 测试用模块接口。
        /// </summary>
        public interface ITestModule : IWorldModule
        {
        }

        /// <summary>
        /// 测试用模块。
        /// </summary>
        public class TestModule : ITestModule
        {
            public string ModuleId { get; }
            public string DisplayName => ModuleId;
            public int Priority { get; }
            public bool IsInitialized { get; private set; }
            public bool Executed { get; private set; }
            public int ExecutedOrder { get; private set; }

            private static int _executionCounter;

            public TestModule(string moduleId, int priority)
            {
                ModuleId = moduleId;
                Priority = priority;
            }

            public void Initialize()
            {
                IsInitialized = true;
            }

            public void Destroy()
            {
                IsInitialized = false;
            }

            public IReadOnlyList<string> GetDependencies() => Array.Empty<string>();
        }

        /// <summary>
        /// 测试用阶段。
        /// </summary>
        private class TestStage : IBootstrapStage
        {
            public string StageId { get; }
            public int Order { get; }
            public string DisplayName => StageId;
            public bool Executed { get; private set; }
            public int ExecutedOrder { get; private set; }

            private static int _executionCounter;

            public TestStage(string stageId, int order)
            {
                StageId = stageId;
                Order = order;
            }

            public async Task ExecuteAsync(WorldBlueprint blueprint)
            {
                await Task.Yield();
                Executed = true;
                ExecutedOrder = _executionCounter++;
            }
        }

        /// <summary>
        /// 测试用服务接口。
        /// </summary>
        public interface ITestService
        {
            bool IsValid { get; }
        }

        /// <summary>
        /// 测试用服务实现。
        /// </summary>
        private class TestService : ITestService
        {
            public bool IsValid => true;
        }
    }
}

using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Core.Logging;
using AbilityKit.Samples.Foundation;

namespace AbilityKit.Samples.SkillCore;

internal static class Program
{
    private const string WorldType = "skillcore.sample";
    private const int TotalFrames = 120;
    private const float DeltaTime = 1f / 30f;

    private static void Main()
    {
        Log.SetSink(new ConsoleLogSink());

        Log.Info("=== AbilityKit SkillCore Starter（Foundation + triggering + pipeline + modifiers）===");

        // 世界装配与 Foundation 相同：注册 world 类型 → factory 组模块 → WorldManager 驱动。
        var registry = new WorldTypeRegistry();
        registry.Register(WorldType, options =>
        {
            options.ServiceBuilder ??= new WorldContainerBuilder();
            var plan = WorldModulePlanner.Create(options.Modules.ToArray());
            foreach (var entry in plan.Entries)
            {
                options.ServiceBuilder.AddModule(entry.Module);
            }

            return new FoundationWorld(options.Id, options.WorldType, options.ServiceBuilder.Build());
        });

        var manager = new WorldManager(new RegistryWorldFactory(registry));
        var world = manager.Create(new WorldCreateOptions(new WorldId("skillcore"), WorldType)
        {
            ServiceBuilder = new WorldContainerBuilder(),
            Modules = { new SkillCoreModule() }
        });

        var skills = world.Services.Resolve<ISkillCastService>();

        // 1) 触发规则：伤害 + 黑板攻击力 ≥ 12 时触发反击（RPN 表达式条件）。
        skills.SetupTriggerRule(threshold: 12f);

        // 2) 验证规则的两条路径：7+7=14 命中，3+7=10 不命中。
        skills.PublishDamageProbe(7);
        skills.PublishDamageProbe(3);

        // 3) 技能 1「火球」：前摇 → 三连发伤害 → 后摇（pipeline 阶段编排）。
        skills.CastFireball();

        // 4) 技能 2「虚弱」：移动速度 ×0.5 的 Buff（modifiers 计算）+ 5 跳 DOT。
        skills.CastWeaken();

        // 5) 宿主驱动 Tick：管线阶段推进、DOT 跳数、事件派发都在这一条链上完成。
        for (int frame = 1; frame <= TotalFrames; frame++)
        {
            manager.Tick(DeltaTime);
        }

        manager.Destroy(world.Id);
        Log.Info("=== Starter 完成 ===");
    }

    private sealed class ConsoleLogSink : ILogSink
    {
        public void Info(string message) => Console.WriteLine($"[INFO ] {message}");

        public void Warning(string message) => Console.WriteLine($"[WARN ] {message}");

        public void Error(string message) => Console.WriteLine($"[ERROR] {message}");

        public void Exception(Exception exception, string message = null!)
            => Console.WriteLine($"[EXCPT] {message} {exception}");
    }
}

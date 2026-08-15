using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Core.Logging;
using AbilityKit.Samples.Foundation;

namespace AbilityKit.Samples.BattleRuntime;

internal static class Program
{
    private const string WorldType = "battleruntime.sample";
    private const int TotalFrames = 90;
    private const float DeltaTime = 1f / 30f;

    private static void Main()
    {
        Log.SetSink(new ConsoleLogSink());

        Log.Info("=== AbilityKit BattleRuntime Starter（SkillCore + targeting/projectile/damage）===");

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
        var world = manager.Create(new WorldCreateOptions(new WorldId("battle"), WorldType)
        {
            ServiceBuilder = new WorldContainerBuilder(),
            Modules = { new BattleRuntimeModule() }
        });

        var battle = world.Services.Resolve<IBattleService>();

        // 1) 施放火球齐射：目标选择 → 投射物发射。
        battle.CastFireballVolley();

        // 2) 宿主驱动 Tick：投射物飞行、命中、伤害结算、事件触发都在这一条链上完成。
        for (int frame = 1; frame <= TotalFrames; frame++)
        {
            manager.Tick(DeltaTime);
        }

        // 3) 战报。
        Log.Info("=== 战报 ===");
        foreach (var monster in battle.Monsters)
        {
            Log.Info($"[Report] {monster.Name}({monster.Id})：HP {Math.Max(monster.Hp, 0f):0.#} / {monster.MaxHp:0}");
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

#nullable enable

using System;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Foundation
{
    /// <summary>
    /// ObjectPool - 瀵硅薄姹?    /// </summary>
    [Sample]
    public sealed class ObjectPool : SampleBase
    {
        public override string Title => "Object Pool";
        public override string Description => "?? AbilityKit.Core ??????";
        public override SampleCategory Category => SampleCategory.Foundation;

        protected override void OnRun()
        {
            Log("???(Object Pool)");
            Output.Divider();

            Log("?????????????????????? GC ???");
            Log("");

            Log("????: IPoolable");
            Output.Bullet("OnSpawn(): ????????");
            Output.Bullet("OnDespawn(): ????????");
            Output.Bullet("OnPoolDestroy(): ????????");

            Output.Divider();

            Log("????:");
            Log("  var pool = Pools.Get<T>();");
            Log("  pool.Prewarm(count);        // ??");
            Log("  var obj = pool.Get();       // ??");
            Log("  // ????...");
            Log("  pool.Collect(obj);            // ??");

            Output.Divider();

            Log("????:");
            Output.Bullet("??? (Bullet, Arrow)");
            Output.Bullet("????");
            Output.Bullet("????");
            Output.Bullet("??????");

            Output.Divider();

            Log("API ????:");
            Log("  AbilityKit.Core.Pooling");

            Output.Divider();

            Log("??:");
            Log("  var projectilePool = Pools.Get<Projectile>();");
            Log("  projectilePool.Prewarm(10);");
            Log("  ");
            Log("  var bullet = projectilePool.Get();");
            Log("  bullet.Position = spawnPoint;");
            Log("  bullet.Velocity = direction * speed;");
            Log("  ");
            Log("  // ?????");
            Log("  projectilePool.Collect(bullet);");
        }
    }
}

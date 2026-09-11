#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Demo
{
    /// <summary>
    /// Tower - ???????
    /// </summary>
    public sealed class Tower
    {
        public string Name { get; set; }
        public float Damage { get; set; }
        public float Range { get; set; }
        public float FireRate { get; set; }

        public Tower(string name, float damage, float range, float fireRate)
        {
            Name = name;
            Damage = damage;
            Range = range;
            FireRate = fireRate;
        }
    }

    /// <summary>
    /// Enemy - ??????
    /// </summary>
    public sealed class Enemy
    {
        public string Name { get; set; }
        public float Health { get; set; }
        public float MaxHealth { get; set; }
        public float Speed { get; set; }
        public float Position { get; set; }
    }

    /// <summary>
    /// TimedTowerDefense - 鍥炲悎鍒跺闃?
    /// </summary>
    [Sample]
    public sealed class TimedTowerDefense : SampleBase
    {
        public override string Title => "Timed Tower Defense";
        public override string Description => "???????????????";
        public override SampleCategory Category => SampleCategory.Demo;

        protected override void OnRun()
        {
            Log("=== ?????????? ===");
            Output.Divider();

            // ?????
            var towers = new List<Tower>
            {
                new Tower("???", 50f, 10f, 1.0f),
                new Tower("???", 30f, 8f, 0.8f)
            };

            // ????
            var enemies = new List<Enemy>
            {
                new Enemy { Name = "???", Health = 100f, MaxHealth = 100f, Speed = 2f, Position = 0f },
                new Enemy { Name = "??", Health = 200f, MaxHealth = 200f, Speed = 1f, Position = 0f }
            };

            Log("???:");
            foreach (var t in towers)
            {
                Output.Bullet($"{t.Name}: ??={t.Damage}, ??={t.Range}, ??={t.FireRate}/s");
            }

            Output.Divider();

            Log("??:");
            foreach (var e in enemies)
            {
                Output.Bullet($"{e.Name}: HP={e.Health}, ??={e.Speed}");
            }

            Output.Divider();

            Log("?? 10 ?????...");
            Output.Divider();

            // ?? 10 ???? 0.1 ?
            for (int i = 1; i <= 10; i++)
            {
                SimulateFrames(10, 0.1f);
                Log($"[Time={Time:F1}s] ?????");
            }

            Output.Divider();

            Log("?????:");
            Output.Bullet("?? SimulateFrames ????");
            Output.Bullet("?????????");
            Output.Bullet("?????????");
        }
    }
}

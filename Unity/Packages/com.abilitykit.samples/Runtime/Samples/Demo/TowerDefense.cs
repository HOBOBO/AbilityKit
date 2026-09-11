#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Demo
{
    /// <summary>
    /// TowerDefense - 塔防游戏
    /// </summary>
    [Sample]
    public sealed class TowerDefense : SampleBase
    {
        public override string Title => "Tower Defense";
        public override string Description => "???????????";
        public override SampleCategory Category => SampleCategory.Demo;

        protected override void OnRun()
        {
            Log("=== ?????? ===");
            Output.Divider();

            // ?????
            var towers = new List<(string Name, float Damage, float Range)>
            {
                ("???", 50f, 10f),
                ("???", 30f, 8f)
            };

            // ????
            var enemies = new List<(string Name, float Health, float Speed)>
            {
                ("???", 100f, 2f),
                ("??", 200f, 1f)
            };

            Log("?????:");
            foreach (var tower in towers)
            {
                Output.Bullet($"{tower.Name}: ??={tower.Damage}, ??={tower.Range}");
            }

            Output.Divider();

            Log("????:");
            foreach (var enemy in enemies)
            {
                Output.Bullet($"{enemy.Name}: ??={enemy.Health}, ??={enemy.Speed}");
            }

            Output.Divider();

            Log("????:");
            Output.Bullet("1. ???????");
            Output.Bullet("2. ???????????");
            Output.Bullet("3. ??????????");
            Output.Bullet("4. ??????????");

            Output.Divider();

            Log("?????:");
            Output.Bullet("?????????");
            Output.Bullet("?????????");
            Output.Bullet("?????????");
        }
    }
}

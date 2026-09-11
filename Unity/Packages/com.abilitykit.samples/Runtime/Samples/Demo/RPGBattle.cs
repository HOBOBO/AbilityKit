#nullable enable

using System;
using AbilityKit.Samples.Abstractions;
using AbilityKit.Samples.Logic.Infrastructure.Common.Math;

namespace AbilityKit.Samples.Logic.Samples.Demo
{
    /// <summary>
    /// RPGBattle - RPG战斗系统
    /// </summary>
    [Sample]
    public sealed class RPGBattle : SampleBase
    {
        public override string Title => "RPG Battle";
        public override string Description => "???????? RPG ??";
        public override SampleCategory Category => SampleCategory.Demo;

        private float _heroHealth = 500f;
        private float _heroMaxHealth = 500f;
        private float _heroAttack = 80f;
        private float _heroDefense = 20f;

        private float _enemyHealth = 800f;
        private float _enemyMaxHealth = 800f;
        private float _enemyAttack = 60f;
        private float _enemyDefense = 30f;

        protected override void OnRun()
        {
            Log("=== RPG ????? ===");
            Output.Divider();

            Log("????:");
            Output.Bullet($"??: HP={_heroMaxHealth}, ATK={_heroAttack}, DEF={_heroDefense}");
            Output.Bullet($"??: HP={_enemyMaxHealth}, ATK={_enemyAttack}, DEF={_enemyDefense}");

            Output.Divider();
            Log("????...");

            int turn = 1;
            while (_heroHealth > 0 && _enemyHealth > 0 && turn <= 5)
            {
                Log($"--- ? {turn} ?? ---");

                // ????
                HeroTurn();
                if (_enemyHealth <= 0) break;

                // ????
                EnemyTurn();
                if (_heroHealth <= 0) break;

                turn++;
            }

            Output.Divider();
            Log("=== ???? ===");

            if (_heroHealth > 0)
            {
                Log($"????! ?? HP: {_heroHealth:F0}/{_heroMaxHealth}");
            }
            else
            {
                Log("????!");
            }
        }

        private void HeroTurn()
        {
            Log("?????:");

            // ??????
            var damage = MathUtil.CalculateDamage(_heroAttack, _enemyDefense, 0);
            _enemyHealth -= damage;

            Log("  ????: ????");
            Log($"  ?? {damage:F0} ??!");
            Log($"  ???? HP: {_enemyHealth:F0}/{_enemyMaxHealth}");
        }

        private void EnemyTurn()
        {
            Log("?????:");

            var damage = MathUtil.CalculateDamage(_enemyAttack, _heroDefense, 0);
            _heroHealth -= damage;

            Log("  ????");
            Log($"  ?? {damage:F0} ??!");
            Log($"  ???? HP: {_heroHealth:F0}/{_heroMaxHealth}");
        }
    }
}

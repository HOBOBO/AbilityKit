using AbilityKit.Game.Battle.Entity;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal sealed class ResourceBattleViewShellLoader : IBattleViewShellLoader
    {
        private readonly BattleViewResourceProvider _resources;

        public ResourceBattleViewShellLoader(BattleViewResourceProvider resources = null)
        {
            _resources = BattleViewResourceProvider.OrDefault(resources);
        }

        public GameObject CreateShellGameObject(int actorId, int modelId)
        {
            return _resources.CreateShellGameObject(actorId, modelId);
        }

        public GameObject CreateShellGameObject(int actorId, int modelId, BattleEntityKind kind)
        {
            switch (kind)
            {
                case BattleEntityKind.Character:
                case BattleEntityKind.Unknown:
                    return _resources.CreateShellGameObject(actorId, modelId);

                case BattleEntityKind.Minion:
                    return _resources.CreateMinionShell(actorId, modelId);

                case BattleEntityKind.Summon:
                case BattleEntityKind.Clone:
                    return _resources.CreateSummonShell(actorId, modelId);

                case BattleEntityKind.Turret:
                    return _resources.CreateTurretShell(actorId, modelId);

                case BattleEntityKind.Monster:
                    return _resources.CreateMonsterShell(actorId, modelId);

                case BattleEntityKind.Building:
                    return _resources.CreateBuildingShell(actorId, modelId);

                case BattleEntityKind.Projectile:
                    return _resources.CreateProjectileShell(actorId, modelId);

                default:
                    return _resources.CreateShellGameObject(actorId, modelId);
            }
        }
    }
}

using AbilityKit.Game.Flow.Battle.View;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    internal sealed class BattleDamageFloatingTextSpawner
    {
        private readonly EC.IECWorld _world;
        private readonly EC.IEntity _vfxNode;
        private readonly BattleFloatingTextSystem _floatingTexts;
        private readonly BattleDamageFloatingTextPositionResolver _positions;
        private readonly BattleDamageFloatingTextFormatter _formatter;
        private readonly BattleDamageFloatingTextSpawnGate _spawnGate;

        public BattleDamageFloatingTextSpawner(
            EC.IECWorld world,
            in EC.IEntity vfxNode,
            BattleFloatingTextSystem floatingTexts,
            BattleDamageFloatingTextPositionResolver positions,
            BattleDamageFloatingTextFormatter formatter = null,
            BattleDamageFloatingTextSpawnGate spawnGate = null)
        {
            _world = world;
            _vfxNode = vfxNode;
            _floatingTexts = floatingTexts;
            _positions = positions;
            _formatter = formatter ?? new BattleDamageFloatingTextFormatter();
            _spawnGate = spawnGate ?? new BattleDamageFloatingTextSpawnGate();
        }

        public bool CanSpawn => _spawnGate.CanSpawn(_world, in _vfxNode, _positions);

        public void Spawn(int targetActorId, float value, bool isHeal)
        {
            if (!CanSpawn) return;
            if (targetActorId <= 0) return;
            if (!_formatter.TryFormat(value, isHeal, out var spec)) return;

            var position = _positions.Resolve(targetActorId);
            _floatingTexts?.Spawn(_vfxNode, spec.Text, in position, spec.Color);
        }
    }

    internal sealed class BattleDamageFloatingTextSpawnGate
    {
        public bool CanSpawn(
            EC.IECWorld world,
            in EC.IEntity vfxNode,
            BattleDamageFloatingTextPositionResolver positions)
        {
            if (world == null) return false;
            if (!vfxNode.IsValid) return false;
            if (positions == null) return false;
            return true;
        }
    }
}

using AbilityKit.Game.Battle.Vfx;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattlePresentationContext
    {
        private long _vfxBindingGeneration;
        private long _remoteInterpolationGeneration;

        public bool EnableRemoteInterpolation { get; set; }
        public BattleVfxManager ViewVfxManager { get; set; }
        public EC.IEntity ViewVfxNode { get; set; }

        public long BindVfx(BattleVfxManager manager, EC.IEntity node)
        {
            _vfxBindingGeneration++;
            ViewVfxManager = manager;
            ViewVfxNode = node;
            return _vfxBindingGeneration;
        }

        public bool ClearVfx(long bindingGeneration)
        {
            if (bindingGeneration != _vfxBindingGeneration)
            {
                return false;
            }

            ViewVfxManager = null;
            ViewVfxNode = default;
            _vfxBindingGeneration++;
            return true;
        }

        public long BeginRemoteInterpolation()
        {
            _remoteInterpolationGeneration++;
            EnableRemoteInterpolation = true;
            return _remoteInterpolationGeneration;
        }

        public bool EndRemoteInterpolation(long generation)
        {
            if (generation != _remoteInterpolationGeneration)
            {
                return false;
            }

            EnableRemoteInterpolation = false;
            _remoteInterpolationGeneration++;
            return true;
        }

        public void Reset()
        {
            EnableRemoteInterpolation = false;
            ViewVfxManager = null;
            ViewVfxNode = default;
            _vfxBindingGeneration++;
            _remoteInterpolationGeneration++;
        }
    }
}

using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 管线生命周期注册表 Runtime 实现
    /// 轻量级版本，仅提供基础注册/查询功能
    /// </summary>
    public sealed class PipelineRegistry : IPipelineRegistry
    {
        public static readonly PipelineRegistry Instance = new PipelineRegistry();

        private readonly List<IPipelineLifeOwner> _owners = new List<IPipelineLifeOwner>(64);
        private bool _isInitialized;

        public int ActiveCount => _owners.Count;

        public void Initialize()
        {
            _isInitialized = true;
        }

        public void Shutdown()
        {
            _isInitialized = false;
            _owners.Clear();
        }

        public void Register(IPipelineLifeOwner owner)
        {
            if (!_isInitialized || owner == null) return;
            if (!_owners.Contains(owner))
            {
                _owners.Add(owner);
            }
            PipelineRegistryEvents.OnRunStarted?.Invoke(owner);
        }

        public void Unregister(IPipelineLifeOwner owner)
        {
            if (owner == null) return;
            if (_owners.Remove(owner))
            {
                PipelineRegistryEvents.OnRunEnded?.Invoke(owner, owner.State);
            }
        }

        public IReadOnlyList<IPipelineLifeOwner> GetActiveOwners()
        {
            return _owners;
        }

        public void InterruptAll()
        {
            PipelineRegistryEvents.OnGlobalInterrupt?.Invoke();
            for (int i = 0; i < _owners.Count; i++)
            {
                if (_owners[i] is IPipelineInterruptible interruptible)
                {
                    interruptible.Interrupt();
                }
            }
        }

        public IReadOnlyList<IPipelineLifeOwner> GetOwnersByPhase(AbilityPipelinePhaseId phaseId)
        {
            var result = new List<IPipelineLifeOwner>();
            for (int i = 0; i < _owners.Count; i++)
            {
                if (_owners[i].CurrentPhaseId == phaseId)
                {
                    result.Add(_owners[i]);
                }
            }
            return result;
        }

        public IReadOnlyList<IPipelineLifeOwner> GetOwnersByState(EAbilityPipelineState state)
        {
            var result = new List<IPipelineLifeOwner>();
            for (int i = 0; i < _owners.Count; i++)
            {
                if (_owners[i].State == state)
                {
                    result.Add(_owners[i]);
                }
            }
            return result;
        }
    }
}

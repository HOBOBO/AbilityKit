using System;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 能力管线阶段id
    /// </summary>
    public class AbilityPipelinePhaseId : GenericEnumId<int>
    {
        public AbilityPipelinePhaseId(int id) : base(id)
        {
        }
        /// <summary>
        /// 与
        /// </summary>
        public const string AndName = "And";
        /// <summary>
        /// 或
        /// </summary>
        public const string OrName = "Or";
        /// <summary>
        /// 或
        /// </summary>
        public const string NotName = "Not";
        /// <summary>
        /// 顺序
        /// </summary>
        public const string SequenceName = "Sequence";
        /// <summary>
        /// 并行
        /// </summary>
        public const string ParallelName = "Parallel";
        
        
        public static AbilityPipelinePhaseId And =>
            AbilityPipelinePhaseIdManager.Instance.Register(AndName);
        public static AbilityPipelinePhaseId Or =>
            AbilityPipelinePhaseIdManager.Instance.Register(OrName);
        public static AbilityPipelinePhaseId Not =>
            AbilityPipelinePhaseIdManager.Instance.Register(NotName);
        public static AbilityPipelinePhaseId Sequence =>
            AbilityPipelinePhaseIdManager.Instance.Register(SequenceName);
        public static AbilityPipelinePhaseId Parallel =>
            AbilityPipelinePhaseIdManager.Instance.Register(ParallelName);
    }

    public class AbilityPipelinePhaseIdManager
    {
        public static readonly AbilityPipelinePhaseIdManager Instance = new AbilityPipelinePhaseIdManager();

        private readonly StableStringEnumIdManager<AbilityPipelinePhaseId> _impl =
            new StableStringEnumIdManager<AbilityPipelinePhaseId>(raw => new AbilityPipelinePhaseId(raw));

        public AbilityPipelinePhaseId Register(string name) => _impl.Register(name);

        public bool TryGetId(string name, out AbilityPipelinePhaseId id) => _impl.TryGetId(name, out id);

        public bool TryGetName(AbilityPipelinePhaseId id, out string name) => _impl.TryGetName(id, out name);
    }
}
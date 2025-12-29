
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
        
        
        public static AbilityPipelinePhaseId And =
            AbilityPipelinePhaseIdManager.Instance.Register(AndName);
        public static AbilityPipelinePhaseId Or =
            AbilityPipelinePhaseIdManager.Instance.Register(OrName);
        public static AbilityPipelinePhaseId Not =
            AbilityPipelinePhaseIdManager.Instance.Register(NotName);
        public static AbilityPipelinePhaseId Sequence =
            AbilityPipelinePhaseIdManager.Instance.Register(SequenceName);
        public static AbilityPipelinePhaseId Parallel =
            AbilityPipelinePhaseIdManager.Instance.Register(ParallelName);
    }

    public class AbilityPipelinePhaseIdManager : GenericIdManager<string, AbilityPipelinePhaseId>
    {
        public static AbilityPipelinePhaseIdManager Instance = new AbilityPipelinePhaseIdManager(null);

        public AbilityPipelinePhaseIdManager(Func<AbilityPipelinePhaseId, AbilityPipelinePhaseId> incrementFunc) : base(incrementFunc)
        {
        }
    }
}
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// MOBA 溯源元数据。
    /// 仅保留业务查询与诊断需要的强类型字段，不再重复维护节点整数类型的独立真值。
    /// </summary>
    public sealed class MobaTraceMetadata : TraceMetadata
    {
        /// <summary>MOBA 溯源种类的强类型视图。</summary>
        public MobaTraceKind TraceKind { get; set; }
        public int ConfigId { get; set; }
        public int SkillId { get; set; }
        public int CastFlowId { get; set; }
        public string PhaseId { get; set; }
        public long RootId { get; set; }
        public long ParentId { get; set; }
        public long SourceActorId { get; set; }
        public long TargetActorId { get; set; }
        public long SourceId { get; set; }
        public long TargetId { get; set; }
        public long OriginSourceId { get; set; }
        public long OriginTargetId { get; set; }
        public string OriginSource { get; set; }
        public string OriginTarget { get; set; }
        public int Kind => (int)TraceKind;
 
        /// <summary>生成用于调试与日志输出的简要展示字符串。</summary>
        public string ToDisplayString()
        {
            return $"{TraceKind}(root={RootId}, config={ConfigId}, skill={SkillId}, castFlow={CastFlowId}, phase={PhaseId}, source={SourceActorId}, target={TargetActorId}, origin={OriginSource}, targetOrigin={OriginTarget})";
        }

        public bool IsEmpty => RootId <= 0 && SourceActorId <= 0 && TargetActorId <= 0 &&
                               ConfigId <= 0 && SkillId <= 0 && CastFlowId <= 0 &&
                               string.IsNullOrEmpty(PhaseId) && string.IsNullOrEmpty(OriginSource) &&
                               string.IsNullOrEmpty(OriginTarget);
    }
}

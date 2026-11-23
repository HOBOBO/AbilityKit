using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services.EntityManager;

/// <summary>
/// 文件名称：MobaInputCommandContext.cs
///
/// 功能描述：封装输入命令处理所需的逻辑世界服务。
///
/// 创建日期：2026-05-27
/// 修改日期：2026-05-27
/// </summary>
namespace AbilityKit.Demo.Moba.Services
{
    internal enum MobaInputExecutionKind
    {
        Authority,
        Predicted,
        Received,
        Replay,
        Settled
    }

    /// <summary>
    /// 输入命令处理上下文，避免处理器直接依赖输入 Sink。
    /// </summary>
    public sealed class MobaInputCommandContext
    {
        public MobaLogicWorldRunGateService Phase { get; }
        public MobaPlayerActorMapService PlayerActorMap { get; }
        public MobaEntityManager Entities { get; }
        public SkillCastCoordinator Skills { get; }
        public IWorldResolver Services { get; }
        /// <summary>
        /// 当前批次是否正在重演权威输入。
        /// </summary>
        public bool IsReplay => ExecutionKind == MobaInputExecutionKind.Replay;
        internal MobaInputExecutionKind ExecutionKind { get; }
        internal bool IsAuthority => ExecutionKind == MobaInputExecutionKind.Authority;
        internal bool IsSettled => ExecutionKind == MobaInputExecutionKind.Settled;
        private Dictionary<(int Actor, int Version, int Key), bool> _clientBatchDecisions;
        public long DiagnosticCommandId { get; internal set; }

        /// <summary>
        /// 创建输入命令处理上下文。
        /// </summary>
        public MobaInputCommandContext(MobaLogicWorldRunGateService phase, MobaPlayerActorMapService playerActorMap, MobaEntityManager entities, SkillCastCoordinator skills, IWorldResolver services, bool isReplay = false)
            : this(phase, playerActorMap, entities, skills, services,
                isReplay ? MobaInputExecutionKind.Replay : MobaInputExecutionKind.Authority) { }

        internal MobaInputCommandContext(MobaLogicWorldRunGateService phase, MobaPlayerActorMapService playerActorMap,
            MobaEntityManager entities, SkillCastCoordinator skills, IWorldResolver services, MobaInputExecutionKind executionKind)
        {
            Phase = phase;
            PlayerActorMap = playerActorMap;
            Entities = entities;
            Skills = skills;
            Services = services;
            ExecutionKind = executionKind;
        }

        internal bool TryGetClientBatchDecision(int actorId, int version, int key, out bool accepted)
        {
            accepted = false;
            return _clientBatchDecisions != null && _clientBatchDecisions.TryGetValue((actorId, version, key), out accepted);
        }

        internal void RecordClientBatchDecision(int actorId, int version, int key, bool accepted)
        {
            _clientBatchDecisions ??= new Dictionary<(int, int, int), bool>();
            _clientBatchDecisions[(actorId, version, key)] = accepted;
        }

        /// <summary>
        /// 尝试通过 ActorId 获取实体。
        /// </summary>
        public bool TryGetEntity(int actorId, out ActorEntity entity)
        {
            if (Entities != null && Entities.TryGetActorEntity(actorId, out entity) && entity != null)
            {
                return true;
            }

            entity = null;
            return false;
        }
    }
}

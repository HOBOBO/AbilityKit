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
        public long DiagnosticCommandId { get; internal set; }

        /// <summary>
        /// 创建输入命令处理上下文。
        /// </summary>
        public MobaInputCommandContext(MobaLogicWorldRunGateService phase, MobaPlayerActorMapService playerActorMap, MobaEntityManager entities, SkillCastCoordinator skills, IWorldResolver services)
        {
            Phase = phase;
            PlayerActorMap = playerActorMap;
            Entities = entities;
            Skills = skills;
            Services = services;
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

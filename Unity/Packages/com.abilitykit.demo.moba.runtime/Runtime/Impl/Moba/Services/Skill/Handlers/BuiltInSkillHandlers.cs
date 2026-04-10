using System;
using AbilityKit.Ability.Share.ECS; using AbilityKit.ECS; using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.Ability.Share.Impl.Moba.Components;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    // ========================================================================
    // 检查类处理项实现
    // ========================================================================

    /// <summary>
    /// 冷却检查处理项
    /// </summary>
    public sealed class CheckCooldownHandler : ISkillHandler
    {
        private readonly MobaActorLookupService _actors;
        private readonly IFrameTime _time;

        public int HandlerType => (int)EHandlerType.CheckCooldown;

        public CheckCooldownHandler(MobaActorLookupService actors, IFrameTime time)
        {
            _actors = actors;
            _time = time;
        }

        public HandlerResult Execute(in HandlerContext context)
        {
            var skillId = context.PipelineContext.SkillId;
            var slot = context.PipelineContext.SkillSlot;

            if (_actors == null || !_actors.TryGetActorEntity(context.CasterActorId, out var entity) || entity == null)
                return HandlerResult.Ok;

            if (!entity.hasSkillLoadout)
                return HandlerResult.Ok;

            var skills = entity.skillLoadout.ActiveSkills;
            if (skills == null || slot <= 0 || slot > skills.Length)
                return HandlerResult.Ok;

            var runtime = skills[slot - 1];
            if (runtime == null || runtime.SkillId != skillId)
                return HandlerResult.Ok;

            var currentTimeMs = _time != null ? (long)System.MathF.Round(_time.Time * 1000f) : 0L;

            if (runtime.CooldownEndTimeMs > currentTimeMs)
            {
                var remainingMs = runtime.CooldownEndTimeMs - currentTimeMs;
                var remainingSec = remainingMs / 1000.0;
                return HandlerResult.Fail($"冷却中 ({remainingSec:F1}s)", "cooldown", remainingSec);
            }

            return HandlerResult.Ok;
        }
    }

    /// <summary>
    /// 资源检查处理项（检查资源是否足够，不扣除）
    /// </summary>
    public sealed class CheckResourceHandler : ISkillHandler
    {
        private readonly MobaActorLookupService _actors;

        public int HandlerType => (int)EHandlerType.CheckResource;

        public CheckResourceHandler(MobaActorLookupService actors)
        {
            _actors = actors;
        }

        public HandlerResult Execute(in HandlerContext context)
        {
            var dto = context.CurrentDto as CheckResourceDTO;
            if (dto == null)
                return HandlerResult.Ok;

            if (_actors == null || !_actors.TryGetActorEntity(context.CasterActorId, out var entity) || entity == null)
                return HandlerResult.Ok;

            if (!entity.hasResourceContainer)
                return HandlerResult.Ok;

            var resourceType = (ResourceType)dto.ResourceType;
            var requiredAmount = dto.MinAmount?.ConstValue ?? 0;

            var currentAmount = GetResourceAmount(entity, resourceType);
            if (currentAmount < requiredAmount)
            {
                return HandlerResult.Fail("资源不足", "not_enough_resource", requiredAmount, currentAmount);
            }

            return HandlerResult.Ok;
        }

        private float GetResourceAmount(ActorEntity entity, ResourceType resourceType)
        {
            if (entity.hasResourceContainer)
            {
                var container = entity.resourceContainer.Value;
                if (container.Map != null && container.Map.TryGetValue(resourceType, out var state))
                {
                    return state.Current;
                }
            }
            return 0;
        }
    }

    // ========================================================================
    // 操作类处理项实现
    // ========================================================================

    /// <summary>
    /// 资源消耗处理项
    /// </summary>
    public sealed class ConsumeResourceHandler : ISkillHandler
    {
        private readonly MobaActorLookupService _actors;

        public int HandlerType => (int)EHandlerType.ConsumeResource;

        public ConsumeResourceHandler(MobaActorLookupService actors)
        {
            _actors = actors;
        }

        public HandlerResult Execute(in HandlerContext context)
        {
            var dto = context.CurrentDto as ConsumeResourceDTO;
            if (dto == null)
                return HandlerResult.Ok;

            if (_actors == null || !_actors.TryGetActorEntity(context.CasterActorId, out var entity) || entity == null)
                return HandlerResult.Ok;

            var resourceType = (ResourceType)dto.ResourceType;
            var amount = dto.Amount?.ConstValue ?? 0;

            if (amount <= 0)
                return HandlerResult.Ok;

            // TODO: 实际扣除资源
            // if (!TryConsumeResource(entity, resourceType, amount))
            // {
            //     return HandlerResult.Fail(dto.FailMessageKey ?? "not_enough_resource");
            // }

            return HandlerResult.Ok;
        }
    }

    /// <summary>
    /// 开始冷却处理项
    /// </summary>
    public sealed class StartCooldownHandler : ISkillHandler
    {
        private readonly MobaActorLookupService _actors;
        private readonly IFrameTime _time;

        public int HandlerType => (int)EHandlerType.StartCooldown;

        public StartCooldownHandler(MobaActorLookupService actors, IFrameTime time)
        {
            _actors = actors;
            _time = time;
        }

        public HandlerResult Execute(in HandlerContext context)
        {
            var dto = context.CurrentDto as StartCooldownDTO;
            if (dto == null)
                return HandlerResult.Ok;

            if (_actors == null || !_actors.TryGetActorEntity(context.CasterActorId, out var entity) || entity == null)
                return HandlerResult.Ok;

            var cooldownMs = dto.CooldownMs?.ConstValue ?? context.PipelineContext.SkillCooldownMs;
            if (cooldownMs <= 0)
                return HandlerResult.Ok;

            var endTimeMs = (_time != null ? (long)System.MathF.Round(_time.Time * 1000f) : 0L) + cooldownMs;

            // TODO: 写入冷却数据到 SkillLoadout

            return HandlerResult.Ok;
        }
    }

    /// <summary>
    /// 添加Buff处理项
    /// </summary>
    public sealed class ApplyBuffHandler : ISkillHandler
    {
        private readonly MobaActorLookupService _actors;

        public int HandlerType => (int)EHandlerType.ApplyBuff;

        public ApplyBuffHandler(MobaActorLookupService actors)
        {
            _actors = actors;
        }

        public HandlerResult Execute(in HandlerContext context)
        {
            var dto = context.CurrentDto as ApplyBuffDTO;
            if (dto == null)
                return HandlerResult.Ok;

            // 确定目标
            var targetActorId = dto.Target == 0 ? context.CasterActorId : context.TargetActorId;
            if (targetActorId <= 0)
                return HandlerResult.Ok;

            if (_actors == null || !_actors.TryGetActorEntity(targetActorId, out var entity) || entity == null)
                return HandlerResult.Ok;

            // TODO: 调用Buff系统添加Buff
            // _buffService.ApplyBuff(targetActorId, dto.BuffId, context.CasterActorId);

            return HandlerResult.Ok;
        }
    }

    /// <summary>
    /// 添加标签处理项
    /// </summary>
    public sealed class AddTagHandler : ISkillHandler
    {
        private readonly MobaActorLookupService _actors;

        public int HandlerType => (int)EHandlerType.AddTag;

        public AddTagHandler(MobaActorLookupService actors)
        {
            _actors = actors;
        }

        public HandlerResult Execute(in HandlerContext context)
        {
            var dto = context.CurrentDto as AddTagDTO;
            if (dto == null || dto.Tags == null || dto.Tags.Length == 0)
                return HandlerResult.Ok;

            // 确定目标
            var targetActorId = dto.Target == 0 ? context.CasterActorId : context.TargetActorId;
            if (targetActorId <= 0)
                return HandlerResult.Ok;

            if (_actors == null || !_actors.TryGetActorEntity(targetActorId, out var entity) || entity == null)
                return HandlerResult.Ok;

            // TODO: 调用标签系统添加标签
            // foreach (var tagName in dto.Tags)
            // {
            //     _tagService.AddTag(targetActorId, tagName, durationMs);
            // }

            return HandlerResult.Ok;
        }
    }
}

using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Log;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    using AbilityKit.Ability;
    /// <summary>
    /// 技能处理项执行结果
    /// </summary>
    public readonly struct HandlerResult
    {
        public readonly bool Success;
        public readonly string FailReason;
        public readonly string FailKey;
        public readonly object[] FailParams;

        public static HandlerResult Ok => new(true, null, null, null);

        public static HandlerResult Fail(string reason, string failKey = null, params object[] @params)
            => new(false, reason, failKey, @params);

        private HandlerResult(bool success, string reason, string failKey, object[] @params)
        {
            Success = success;
            FailReason = reason;
            FailKey = failKey;
            FailParams = @params;
        }
    }

    /// <summary>
    /// 技能处理项上下文
    /// </summary>
    public readonly struct HandlerContext
    {
        /// <summary>
        /// 技能管线上下文
        /// </summary>
        public readonly SkillPipelineContext PipelineContext;

        /// <summary>
        /// 施法者 Actor ID
        /// </summary>
        public readonly int CasterActorId;

        /// <summary>
        /// 目标 Actor ID
        /// </summary>
        public readonly int TargetActorId;

        /// <summary>
        /// 当前正在执行的 Handler 配置 DTO
        /// </summary>
        public readonly SkillHandlerDTO CurrentDto;

        public HandlerContext(
            SkillPipelineContext pipelineContext,
            int casterActorId,
            int targetActorId,
            SkillHandlerDTO currentDto)
        {
            PipelineContext = pipelineContext;
            CasterActorId = casterActorId;
            TargetActorId = targetActorId;
            CurrentDto = currentDto;
        }
    }

    /// <summary>
    /// 技能处理项接口
    /// 定义单个处理项的执行逻辑
    /// </summary>
    public interface ISkillHandler
    {
        /// <summary>
        /// 处理项类型
        /// </summary>
        int HandlerType { get; }

        /// <summary>
        /// 执行处理项
        /// </summary>
        HandlerResult Execute(in HandlerContext context);
    }

    /// <summary>
    /// 技能处理项注册表
    /// 管理所有已注册的处理项类型
    /// </summary>
    public sealed class SkillHandlerRegistry
    {
        private readonly Dictionary<int, ISkillHandler> _handlers = new Dictionary<int, ISkillHandler>();
        private readonly Dictionary<string, ISkillHandler> _handlersByName = new Dictionary<string, ISkillHandler>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 注册处理项
        /// </summary>
        public void Register(ISkillHandler handler)
        {
            if (handler == null) return;
            _handlers[handler.HandlerType] = handler;

            var typeName = handler.GetType().Name;
            if (typeName.EndsWith("Handler"))
            {
                typeName = typeName.Substring(0, typeName.Length - 6);
            }
            _handlersByName[typeName] = handler;
        }

        /// <summary>
        /// 根据类型获取处理项
        /// </summary>
        public bool TryGet(int handlerType, out ISkillHandler handler)
        {
            return _handlers.TryGetValue(handlerType, out handler);
        }

        /// <summary>
        /// 根据类型名获取处理项
        /// </summary>
        public bool TryGet(string typeName, out ISkillHandler handler)
        {
            return _handlersByName.TryGetValue(typeName, out handler);
        }

        /// <summary>
        /// 获取所有已注册的处理项类型
        /// </summary>
        public IEnumerable<int> GetRegisteredTypes() => _handlers.Keys;
    }

    /// <summary>
    /// 技能处理项执行器
    /// 负责根据配置执行一系列处理项
    /// </summary>
    public sealed class SkillHandlerExecutor
    {
        private readonly SkillHandlerRegistry _registry;

        public SkillHandlerExecutor(SkillHandlerRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// 执行处理项列表
        /// </summary>
        /// <param name="context">基础执行上下文</param>
        /// <param name="handlers">处理项配置列表</param>
        /// <returns>第一个失败的结果，如果没有失败则返回成功</returns>
        public HandlerResult ExecuteAll(in HandlerContext context, params SkillHandlerDTO[] handlers)
        {
            if (handlers == null || handlers.Length == 0)
                return HandlerResult.Ok;

            foreach (var handlerDto in handlers)
            {
                if (handlerDto == null) continue;

                if (!_registry.TryGet(handlerDto.Type, out var handler))
                {
                    Log.Warning($"[SkillHandlerExecutor] No handler registered for type: {handlerDto.Type}");
                    continue;
                }

                // 创建包含当前 DTO 的上下文
                var handlerCtx = new HandlerContext(
                    context.PipelineContext,
                    context.CasterActorId,
                    context.TargetActorId,
                    handlerDto);

                var result = handler.Execute(in handlerCtx);
                if (!result.Success)
                {
                    return result;
                }
            }

            return HandlerResult.Ok;
        }

        /// <summary>
        /// 回滚处理项（用于失败时的清理）
        /// </summary>
        public void Rollback(in HandlerContext context, params SkillHandlerDTO[] handlers)
        {
            if (handlers == null || handlers.Length == 0)
                return;

            // 逆序执行回滚逻辑
            for (int i = handlers.Length - 1; i >= 0; i--)
            {
                var handlerDto = handlers[i];
                if (handlerDto == null) continue;

                if (_registry.TryGet(handlerDto.Type, out var handler) && handler is ISkillRollbackHandler rollback)
                {
                    var rollbackCtx = new HandlerContext(
                        context.PipelineContext,
                        context.CasterActorId,
                        context.TargetActorId,
                        handlerDto);
                    rollback.Rollback(in rollbackCtx);
                }
            }
        }

        /// <summary>
        /// 支持回滚的处理项接口
        /// </summary>
        public interface ISkillRollbackHandler : ISkillHandler
        {
            void Rollback(in HandlerContext context);
        }
    }
}

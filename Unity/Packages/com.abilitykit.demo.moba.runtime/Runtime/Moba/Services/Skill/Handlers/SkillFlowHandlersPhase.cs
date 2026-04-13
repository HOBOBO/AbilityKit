using System;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Services
{
    using AbilityKit.Ability;
    /// <summary>
    /// 技能流程处理阶段
    /// 用于技能管线中，根据配置执行一系列可选的处理项
    /// </summary>
    public sealed class SkillFlowHandlersPhase : AbilityInstantPhaseBase<SkillPipelineContext>
    {
        private readonly SkillFlowHandlerConfigDTO _def;
        private readonly SkillHandlerRegistry _handlerRegistry;
        private readonly SkillHandlerExecutor _executor;

        public SkillFlowHandlersPhase(
            AbilityPipelinePhaseId phaseId,
            SkillFlowHandlerConfigDTO def,
            SkillHandlerRegistry handlerRegistry)
            : base(phaseId)
        {
            _def = def;
            _handlerRegistry = handlerRegistry;
            _executor = new SkillHandlerExecutor(_handlerRegistry);
        }

        protected override void OnInstantExecute(SkillPipelineContext context)
        {
            if (_def == null) return;

            var handlerCtx = new HandlerContext(
                context,
                context.CasterActorId,
                context.TargetActorId,
                currentDto: null);

            // 执行 PreCast 处理项（检查类应该放在前面）
            if (_def.PreCastHandlers != null && _def.PreCastHandlers.Length > 0)
            {
                var result = _executor.ExecuteAll(handlerCtx, _def.PreCastHandlers);
                if (!result.Success)
                {
                    context.FailReason = result.FailReason ?? "PreCast check failed";
                    context.IsAborted = true;

                    // 执行回滚
                    ExecuteOnFailHandlers(handlerCtx);
                    return;
                }
            }

            // 执行 PostCast 处理项
            if (_def.PostCastHandlers != null && _def.PostCastHandlers.Length > 0)
            {
                var result = _executor.ExecuteAll(handlerCtx, _def.PostCastHandlers);
                if (!result.Success)
                {
                    context.FailReason = result.FailReason ?? "PostCast execution failed";
                    context.IsAborted = true;
                    return;
                }
            }
        }

        private void ExecuteOnFailHandlers(in HandlerContext context)
        {
            if (_def.OnFailHandlers == null || _def.OnFailHandlers.Length == 0)
                return;

            _executor.Rollback(context, _def.OnFailHandlers);
        }
    }
}

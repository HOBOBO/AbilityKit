using AbilityKit.Demo.Moba.Services.StateMachine;

namespace AbilityKit.Demo.Moba.Components
{
    /// <summary>
    /// ActorEntity 的手写 API 扩展：承载生成器模板表达不了的生命周期语义。
    ///
    /// 2026-10-12 从生成目录迁出：CharacterHfsmComponent 的 Remove 需要 Dispose 持有的
    /// MobaCharacterHfsmRuntime，这属于运行时行为而非组件 API 形状，标准生成模板不再特判。
    /// 需要释放语义的调用点应使用本扩展；普通 Remove 走生成的标准 API（不 Dispose）。
    /// </summary>
    public static class ActorEntityExtensions
    {
        /// <summary>移除 CharacterHfsm 组件并释放其 MobaCharacterHfsmRuntime（原生成目录手写版的语义）。</summary>
        public static void RemoveAndDisposeCharacterHfsm(this global::ActorEntity entity)
        {
            var runtime = entity.hasCharacterHfsm ? entity.characterHfsm.Runtime : null;
            entity.RemoveCharacterHfsm();
            runtime?.Dispose();
        }
    }
}

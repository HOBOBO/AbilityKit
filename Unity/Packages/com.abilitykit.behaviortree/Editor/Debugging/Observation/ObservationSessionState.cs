using UnityEngine.Scripting.APIUpdating;
#nullable enable

namespace AbilityKit.BehaviorTree.Editor.Debugging.Observation
{
    /// <summary>
    /// 观察会话的顶层状态。观察窗口与观察图据此渲染状态徽标并门控交互：
    /// Live/Frozen 可采样，Disconnected 提示重新绑定，NoSample 等待首次采样。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationSessionState")]
    public enum ObservationSessionState
    {
        /// <summary>尚无任何采样：未选中实例，或选中实例尚未产生首个样本。</summary>
        NoSample = 0,

        /// <summary>选中实例在线且持续自动采样。</summary>
        Live = 1,

        /// <summary>选中实例在线但已冻结：暂停自动采样，仅允许显式单步。</summary>
        Frozen = 2,

        /// <summary>之前选中的实例已从注册中心消失（弱引用失效/注销）。不静默切换到其它实例。</summary>
        Disconnected = 3,
    }
}

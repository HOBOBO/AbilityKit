using System;

namespace AbilityKit.Demo.Moba.Components
{
    /// <summary>
    /// 声明一个 Entitas 组件（或组件字段的类型）持有托管/运行时 blob 状态。
    ///
    /// 该标记供 EntitasComponentGuardAnalyzer 识别托管字段风险；它本身不选择回滚 Provider，
    /// 也不自动改变生成器覆盖集。组件是否进入逐字段生成序列化仍由 pin coverage 决定，
    /// 实际恢复覆盖还须核对 Provider 注册及字段往返。
    /// 未标记的非平值组件字段会被 EntitasComponentGuardAnalyzer（AKSG9201）标黄。
    /// 按 attribute 名字匹配，其他项目可自带同名特性复用该 analyzer。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class EntitasBlobAttribute : Attribute
    {
    }
}

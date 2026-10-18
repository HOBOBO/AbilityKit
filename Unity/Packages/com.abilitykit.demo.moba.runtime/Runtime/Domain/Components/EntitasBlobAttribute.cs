using System;

namespace AbilityKit.Demo.Moba.Components
{
    /// <summary>
    /// 声明一个 Entitas 组件（或组件字段的类型）持有 blob 状态——显式的架构债标记。
    ///
    /// 语义：被标注的组件不参与生成的逐字段序列化（tools/AbilityKit.Entitas.CodeGen 的
    /// auto 覆盖集会要求它在 pin 里以 coverage 声明豁免，回滚走专属 Provider）。
    /// 这是把"ECS 管数据"从口头约定变成编译期约束的一部分：
    /// 未标注的非平值组件字段会被 EntitasComponentGuardAnalyzer（AKSG9201）标黄。
    /// 按 attribute 名字匹配，其他项目可自带同名特性复用该 analyzer。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class EntitasBlobAttribute : Attribute
    {
    }
}

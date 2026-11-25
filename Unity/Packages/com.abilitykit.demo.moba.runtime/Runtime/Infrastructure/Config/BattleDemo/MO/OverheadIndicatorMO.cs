using System;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    /// <summary>
    /// 已加载的指示器内容，只供投影器读取，不参与玩法变更。
    /// </summary>
    public sealed class IndicatorDefinitionMO
    {
        public int Id { get; }
        public IndicatorDefinitionDTO Definition { get; }
        public IndicatorDefinitionMO(IndicatorDefinitionDTO dto)
        { Definition = dto ?? throw new ArgumentNullException(nameof(dto)); Id = dto.Id; }
    }

    /// <summary>
    /// 平台无关的头顶布局内容。
    /// </summary>
    public sealed class OverheadLayoutMO
    {
        public int Id { get; }
        public OverheadLayoutDTO Definition { get; }
        public OverheadLayoutMO(OverheadLayoutDTO dto)
        { Definition = dto ?? throw new ArgumentNullException(nameof(dto)); Id = dto.Id; }
    }

    /// <summary>
    /// Actor 内容分类与布局的绑定。
    /// </summary>
    public sealed class ActorOverheadBindingMO
    {
        public int Id { get; }
        public ActorOverheadBindingDTO Definition { get; }
        public ActorOverheadBindingMO(ActorOverheadBindingDTO dto)
        { Definition = dto ?? throw new ArgumentNullException(nameof(dto)); Id = dto.Id; }
    }
}

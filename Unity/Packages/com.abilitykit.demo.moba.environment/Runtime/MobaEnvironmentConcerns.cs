namespace AbilityKit.Demo.Moba.EnvironmentModel
{
/// <summary>
/// MOBA 环境的常用组 taxonomy：关注点 id 与取值域。这是「项目给分类」的具体实现——
/// 全部是字符串数据（配合框架的 <see cref="AbilityKit.EnvironmentModel.EnvironmentConcern"/>），不新增任何框架枚举。
/// </summary>
public static class MobaEnvironmentConcerns
{
    /// <summary>单位类别：环境里刷什么怪/兵。</summary>
    public const string UnitClass = "unit-class";

    /// <summary>目标形态：目标怎么排布（单/群/建筑/无目标）。</summary>
    public const string TargetShape = "target-shape";

    /// <summary>场景几何：墙体/障碍/可破坏物。</summary>
    public const string Geometry = "geometry";

    /// <summary>状态挂载：目标/单位的初始状态。</summary>
    public const string State = "state";

    /// <summary>单位类别取值域。</summary>
    public static readonly string[] UnitClassValues = { "hero", "minion", "jungle", "summon", "neutral" };
    /// <summary>目标形态取值域。</summary>
    public static readonly string[] TargetShapeValues = { "single", "group", "structure", "none" };
    /// <summary>场景几何取值域。</summary>
    public static readonly string[] GeometryValues = { "open", "walled", "obstacle", "destructible" };
    /// <summary>状态挂载取值域。</summary>
    public static readonly string[] StateValues = { "full", "wounded", "armored", "cc-immune" };
}
}

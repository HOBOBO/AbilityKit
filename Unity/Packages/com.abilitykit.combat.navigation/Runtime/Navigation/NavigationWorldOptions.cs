namespace AbilityKit.Combat.Navigation
{
    /// <summary>
    /// 导航世界选项。
    /// </summary>
    public sealed class NavigationWorldOptions
    {
        /// <summary>导航网格格距（米）。越小越精细、烘焙与查询越贵。</summary>
        public float CellSize { get; set; } = 0.5f;

        /// <summary>Agent 半径（米），用于可行走性膨胀与终点投影。</summary>
        public float AgentRadius { get; set; } = 0.5f;

        /// <summary>是否允许对角移动（不允许则纯四邻接）。</summary>
        public bool AllowDiagonal { get; set; } = true;

        /// <summary>单次寻路最大展开节点数（防爆栈/限耗）。</summary>
        public int MaxIterations { get; set; } = 16384;

        /// <summary>路径点共线化简与 LOS 拉直开关。</summary>
        public bool SimplifyPath { get; set; } = true;
    }
}

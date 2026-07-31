namespace AbilityKit.Core.Mathematics
{
    /// <summary>
    /// 导航世界：基于烘焙的导航网格提供可行走性查询与寻路。
    /// 镜像 <c>ICollisionWorld</c> 的接口风格（in/out、返回状态）。
    /// </summary>
    public interface INavigationWorld
    {
        /// <summary>从 start 到 target 规划路径，输出世界空间路径点。</summary>
        PathStatus FindPath(in Vec3 start, in Vec3 target, float agentRadius, out NavigationPath path);

        /// <summary>位置（半径膨胀后）是否可行走。</summary>
        bool IsWalkable(in Vec3 position, float radius);

        /// <summary>把位置投影到最近可行走点。</summary>
        bool TryProjectToWalkable(in Vec3 position, float radius, out Vec3 projected);
    }
}

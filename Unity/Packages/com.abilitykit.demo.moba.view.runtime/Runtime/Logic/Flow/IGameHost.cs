using System.Collections;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 游戏宿主抽象接口，解耦 Flow 层对 Unity MonoBehaviour (GameEntry) 的直接依赖。
    /// Unity 运行时由 GameEntry 实现；纯 C# 测试/控制台环境可提供替代实现。
    /// </summary>
    public interface IGameHost
    {
        bool DebugEnabled { get; }

        T Get<T>() where T : class;
        bool TryGet<T>(out T component) where T : class;

        void RunCoroutine(IEnumerator coroutine);
    }
}

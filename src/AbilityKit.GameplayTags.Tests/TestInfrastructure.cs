using System.Collections.Generic;
using AbilityKit.GameplayTags;
using Xunit;

namespace AbilityKit.GameplayTags.Tests;

/// <summary>
/// 标签系统存在程序集级单例（GameplayTagManager / TagTemplateRegistry / GameplayTagConfigLoader）。
/// 所有触碰这些单例的测试类挂到同一集合，关闭类间并行执行，避免共享状态导致的不确定结果。
/// </summary>
[CollectionDefinition(TagTestCollection.Name)]
public sealed class TagTestCollection
{
    public const string Name = "GameplayTagsSingletonState";
}

/// <summary>
/// 测试基类：每个测试开始前重置全部包级单例，保证测试相互独立、与执行顺序无关。
/// </summary>
public abstract class TagTestBase
{
    protected TagTestBase()
    {
        GameplayTagManager.Instance.Reset();
        TagTemplateRegistry.Instance.Clear();
        GameplayTagConfigLoader.SetLoader(null);
    }

    /// <summary>注册并返回标签（RequestTag 的简写，降低测试噪音）。</summary>
    protected static GameplayTag T(string name) => GameplayTagManager.Instance.RequestTag(name);

    /// <summary>注册一组标签并构建容器。</summary>
    protected static GameplayTagContainer C(params string[] names)
    {
        var container = new GameplayTagContainer();
        foreach (var name in names)
        {
            container.Add(T(name));
        }
        return container;
    }

    /// <summary>取出名称并按序排序，消除 HashSet/Dictionary 遍历顺序差异。</summary>
    protected static string[] SortedNames(IEnumerable<GameplayTag> tags)
    {
        var names = new List<string>();
        foreach (var tag in tags)
        {
            names.Add(tag.TagName);
        }
        names.Sort(System.StringComparer.Ordinal);
        return names.ToArray();
    }
}

/// <summary>记录所有标签变更通知的监听器。</summary>
internal sealed class RecordingTagChangeListener : IGameplayTagChangedListener
{
    public List<(GameplayTag Tag, GameplayTagChangeType ChangeType)> Events { get; } = new();

    public void OnGameplayTagChanged(GameplayTagChangedEventArgs args)
    {
        Events.Add((args.Tag, args.ChangeType));
    }
}

/// <summary>回调时抛异常的监听器，用于验证通知方对监听器异常的容错。</summary>
internal sealed class ThrowingTagChangeListener : IGameplayTagChangedListener
{
    public void OnGameplayTagChanged(GameplayTagChangedEventArgs args)
        => throw new System.InvalidOperationException("listener failure");
}

/// <summary>记录收到的配置数据的假加载器。</summary>
internal sealed class RecordingConfigLoader : ITagConfigLoader
{
    public List<ITagConfigData> Received { get; } = new();

    public void LoadFromData(IEnumerable<ITagConfigData> data)
    {
        if (data == null)
        {
            return;
        }
        foreach (var item in data)
        {
            Received.Add(item);
        }
    }
}

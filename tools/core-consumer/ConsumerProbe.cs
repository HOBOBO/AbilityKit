using System;
using System.Linq;
using AbilityKit.Core.Collections;
using AbilityKit.Core.Identifiers;
using AbilityKit.Core.Lifetime;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;

/// <summary>
/// 在仓库外的最小消费工程中核对包恢复后的基础调用。
/// </summary>
public static class ConsumerProbe
{
    /// <summary>
    /// 执行固定输入的最小调用；失败抛出异常，不代替完整契约或跨平台确定性测试。
    /// </summary>
    public static int RunChecks()
    {
        Check(StableHashV1.Fnv1a32Utf16("hello") == 1335831723, "稳定哈希");
        Check(Math.Abs(MathUtil.Sqrt(9f) - 3f) < 0.0001f, "间接定点依赖");
        Check(Math.Abs(new Vec3(3f, 4f, 0f).Magnitude - 5f) < 0.0001f, "向量长度");
        Check((Fixed64.FromInt32(2) * Fixed64.FromInt32(3)).ToInt32() == 6, "定点公开类型");
        var values = new SortedIntSet();
        values.Add(3);
        values.Add(1);
        values.Add(2);
        Check(values[0] == 1 && values[1] == 2 && values[2] == 3, "集合顺序");
        Check(!values.Add(2) && values.Count == 3, "集合去重");
        int released = 0;
        var registration = DisposableRegistration.Create(() => released++);
        registration.Dispose();
        registration.Dispose();
        Check(released == 1, "单次释放");
        /*
         * 隔离消费夹具专门验证旧入口仍可调用，不为正式生产新增弃用 API 使用方。
         */
#pragma warning disable CS0618
        Check(AbilityKit.Core.Configuration.FlatJsonSettings.Empty().Values.Count == 0, "旧设置兼容入口");
#pragma warning restore CS0618
        return 8;
    }

    private static void Check(bool accepted, string name)
    {
        if (!accepted) throw new InvalidOperationException(name);
    }

#if UNITY_EDITOR
    /// <summary>
    /// 从真实 Unity 批处理入口运行，确认包归属并将明确成功标志写入独立工程。
    /// </summary>
    public static void RunUnity()
    {
        int count = RunChecks();
        var core = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Vec3).Assembly);
        var deterministic = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Fixed64).Assembly);
        Check(core != null && core.name == "com.abilitykit.core" && core.version == "0.1.0", "Core 包归属");
        Check(deterministic != null && deterministic.name == "com.abilitykit.deterministic" && deterministic.version == "0.1.0", "定点包归属");
        var abilityKitPackages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
            .Where(package => package.name.StartsWith("com.abilitykit.", StringComparison.Ordinal)).ToArray();
        Check(abilityKitPackages.Length == 2, "独立包闭包");
        System.IO.File.WriteAllLines(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../consumer-packages.txt"),
            new[] { core.resolvedPath, deterministic.resolvedPath });
        System.IO.File.WriteAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../consumer-result.json"),
            "{\"checks\":" + count + ",\"packageChecks\":3,\"unity\":\"" + UnityEngine.Application.unityVersion + "\"}");
        UnityEngine.Debug.Log("ABILITYKIT_CONSUMER_OK checks=" + count);
    }
#else
    /// <summary>
    /// 从仅声明 Core 包引用的独立 .NET 工程运行最小调用。
    /// </summary>
    public static void Main()
    {
        Console.WriteLine("ABILITYKIT_CONSUMER_OK checks=" + RunChecks());
    }
#endif
}

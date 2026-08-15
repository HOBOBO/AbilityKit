using System.IO;
using AbilityKit.Testing.IrSpike.Ir;

namespace AbilityKit.Testing.IrSpike;

// P0 spike 驱动：用真实 .expected.json 验证「IR + 序列化 + 判定」三层在纯 dotnet 跑通。
// 观测数据是合成的（代替尚未去 Unity 化的真实模拟器），spike 的目的是验证可移植层，不是模拟。
internal static class Program
{
    private const string ExpectationRelPath =
        "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10010101_scenario.expected.json";

    private static int Main()
    {
        var repoRoot = LocateRepoRoot();
        var expectationPath = Path.Combine(repoRoot, ExpectationRelPath);
        if (!File.Exists(expectationPath))
        {
            Console.Error.WriteLine($"找不到期望文件：{expectationPath}");
            return 2;
        }

        var json = File.ReadAllText(expectationPath);
        var scenario = ScenarioCodec.Parse(json);

        Console.WriteLine("=== P0 Spike：白盒测试 DSL 规范 IR 可移植性验证 ===");
        Console.WriteLine($"载入真实期望文件：{Path.GetFileName(expectationPath)}");
        Console.WriteLine();
        Console.WriteLine("→ 玩法无关 IR 摘要（去 MOBA 化后）：");
        Console.WriteLine($"   caseId     = {scenario.CaseId}");
        Console.WriteLine($"   actors     = [{string.Join(", ", scenario.Actors.Select(a => $"{a.Alias}(team={a.TeamId})"))}]");
        Console.WriteLine($"   timeline   = {scenario.Timeline.Length} 步 [{string.Join(", ", scenario.Timeline.Select(t => $"{t.Action}@{t.AtMs}ms"))}]");
        Console.WriteLine($"   mustContain= {scenario.Expectations.MustContain.Length} 期望节点");
        Console.WriteLine($"   expectedActions = {scenario.Expectations.ExpectedActions.Length}");
        Console.WriteLine($"   state      = {scenario.Expectations.State.Length} 状态断言");
        Console.WriteLine();

        var outDir = Path.Combine(repoRoot, "local", "Logs", "ir-spike");
        Directory.CreateDirectory(outDir);

        // —— 用例 A：合成「正确实现」观测，应判 passed=true ——
        var happyTrace = HappyPathTrace();
        var happyState = HappyPathState();
        var happySummary = Verifier.Verify(scenario, happyTrace, happyState, observationSource: "synthetic-happy-path");
        WriteSummary(outDir, scenario.CaseId + "__happy", happySummary);

        // —— 用例 B：合成「回归」观测（漏 DamageApply + 目标 hp 漂移），应判 passed=false ——
        var regTrace = happyTrace.Where(r => r is not { Kind: "DamageApply" }).ToList();
        var regState = HappyPathState(targetHp: 500.0); // 漂移
        var regSummary = Verifier.Verify(scenario, regTrace, regState, observationSource: "synthetic-regression");
        WriteSummary(outDir, scenario.CaseId + "__regression", regSummary);

        Console.WriteLine("→ 判定结果：");
        PrintCase("A 正确路径", happySummary);
        PrintCase("B 回归路径", regSummary);
        Console.WriteLine();

        Console.WriteLine("→ 产物：");
        Console.WriteLine($"   {outDir}\\{scenario.CaseId}__happy_summary.json");
        Console.WriteLine($"   {outDir}\\{scenario.CaseId}__regression_summary.json");
        Console.WriteLine();
        PrintQuantification();
        return 0;
    }

    private static void PrintCase(string label, AcceptanceSummary s)
    {
        var mark = s.Result.Passed ? "PASS" : "FAIL";
        var allMark = s.Result.AllPassed ? "PASS" : "FAIL";
        Console.WriteLine($"   [{label}] canonical passed={mark} | 含state allPassed={allMark}");
        if (!s.Result.Passed)
            Console.WriteLine($"      coverage.missingTraceNodes = {s.Coverage.MissingTraceNodes}");
        if (!s.Result.AllPassed)
            Console.WriteLine($"      state 失败项 = {string.Join("; ", s.State.Where(x => !x.Passed).Select(x => $"{x.Alias}.{x.Property}({x.Detail})"))}");
    }

    // 合成「正确实现」的观测 trace：覆盖全部 mustContain + expectedActions。
    // 全部挂在同一 rootId=100（EffectExecution 10010101 即 effectRoot）。
    private static List<TraceRecord> HappyPathTrace() => new()
    {
        new("SkillCast",      10010101,   100),
        new("EffectExecution", 10010101,  100),
        new("EffectAction",   1241142882, 100),
        new("EffectAction",   427896051,  100),
        new("EffectAction",   589451731,  100), // expectedActions 里的 debug_log
        new("EffectAction",   2133799056, 100),
        new("BuffApply",      10010000,   100),
        new("DamageApply",    10010101,   100), // count=1 ≤ maxCount=1
    };

    private static Dictionary<string, IReadOnlyDictionary<string, double>> HappyPathState(double targetHp = 857.5862) => new()
    {
        ["caster"] = new Dictionary<string, double> { ["buff:10010000"] = 1 },
        ["target"] = new Dictionary<string, double> { ["hp"] = targetHp },
    };

    private static void WriteSummary(string dir, string caseId, AcceptanceSummary s)
    {
        var path = Path.Combine(dir, caseId + "_summary.json");
        File.WriteAllText(path, ScenarioCodec.SerializeSummary(s));
    }

    private static void PrintQuantification()
    {
        Console.WriteLine("=== P0 工作量量化（本次 spike 结论）===");
        Console.WriteLine("  [已可移植] DTO 模型本身与 Unity 无关（无 UnityEngine.Vector3，自带 Vec3）。");
        Console.WriteLine("  [已可移植] 序列化：JsonUtility → System.Text.Json，本 spike 已验证（camelCase + 容错）。");
        Console.WriteLine("  [已可移植] 判定核心 BuildCoverage/BuildSummary.passed：纯函数，已忠实移植，无 harness 依赖。");
        Console.WriteLine("  [已可移植] Wire→IR 翻译层（去 MOBA 化）：configId 作为不透明 token，本次已落地。");
        Console.WriteLine("  [暂不可移植·需模拟器] BuildSummary 中约 5 个富化字段依赖 harness：");
        Console.WriteLine("      - result.finalFrame / finalTimeMs  (harness.FrameTime)");
        Console.WriteLine("      - diagnostics.*                    (harness 战斗诊断服务)");
        Console.WriteLine("      - traceDictionary / 各类 label     (harness.Config 反查名称)");
        Console.WriteLine("    这几项属「展示富化」，不参与 passed 判定，可延迟到真实模拟层接入时补。");
        Console.WriteLine("  [真实成本所在] 把 MobaSkillConfigTestHarness / Runner.ExecuteTimeline 的 Entitas 世界");
        Console.WriteLine("      + 技能管线 + 触发引擎搬到纯 dotnet —— 这正是记忆里在做的「session 层去 ScriptableObject 墙」。");
        Console.WriteLine("  结论：IR+序列化+判定这一可移植层，P0 成本『小且已验证』；真正的大头是模拟器去 Unity 化（在途）。");
    }

    private static string LocateRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir, "Unity")) && Directory.Exists(Path.Combine(dir, "src")))
                return dir;
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }
        return Directory.GetCurrentDirectory();
    }
}

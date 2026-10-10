using System.Text;
using AbilityKit.Demo.Moba.Diagnostics.Tests;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Continuous
{
    /// <summary>
    /// 修改器数值解析 golden 位锁（.NET 侧）：与 Unity EditMode 的
    /// MobaModifierNumericGoldenTests 执行同一份用例与期望值。期望值在本侧锁定；
    /// 两侧不一致即为跨运行时漂移，须查明算术/舍入/换算差异后同步重锁。
    /// </summary>
    public sealed class MobaModifierNumericGoldenTests
    {
        [Fact]
        public void Numeric_golden_cases_hold_on_dotnet_runtime()
        {
            var cases = MobaModifierNumericGoldenCases.BuildCases();
            Assert.Equal(cases.Length, MobaModifierNumericGoldenCases.Expected.Length);

            var mismatches = new StringBuilder();
            for (var i = 0; i < cases.Length; i++)
            {
                var actual = MobaModifierNumericGoldenCases.Execute(in cases[i]);
                if (actual != MobaModifierNumericGoldenCases.Expected[i])
                    mismatches.AppendLine(
                        $"{cases[i].Name}: expected {MobaModifierNumericGoldenCases.Describe(MobaModifierNumericGoldenCases.Expected[i], cases[i].Mode)}, actual {MobaModifierNumericGoldenCases.Describe(actual, cases[i].Mode)}");
            }

            Assert.True(mismatches.Length == 0, "numeric golden drift:\n" + mismatches);
        }
    }
}

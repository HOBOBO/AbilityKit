using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    /// <summary>
    /// 修改器数值解析 golden 位锁（Unity 运行时侧）：与 .NET 侧
    /// MobaModifierNumericGoldenTests 执行同一份用例与期望值，锁 Mono/IL2CPP 下
    /// 组合算术、Math.Round 舍入与 float/fixed 换算的位级一致。失败即跨端漂移。
    /// </summary>
    public sealed class MobaModifierNumericGoldenTests
    {
        [Test]
        public void Numeric_golden_cases_hold_on_unity_runtime()
        {
            var cases = MobaModifierNumericGoldenCases.BuildCases();
            Assert.That(cases.Length, Is.EqualTo(MobaModifierNumericGoldenCases.Expected.Length),
                "case/expected count mismatch: rebake both sides together");

            for (var i = 0; i < cases.Length; i++)
            {
                var actual = MobaModifierNumericGoldenCases.Execute(in cases[i]);
                Assert.That(actual, Is.EqualTo(MobaModifierNumericGoldenCases.Expected[i]),
                    $"{cases[i].Name} ({MobaModifierNumericGoldenCases.Describe(actual, cases[i].Mode)})");
            }
        }
    }
}

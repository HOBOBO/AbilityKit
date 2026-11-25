using System.Collections.Generic;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>
    /// 统一验收的反向 Trace 查询，不把未找到的效果作用域退化为全局查询。
    /// </summary>
    internal static class MobaAcceptanceForbiddenTrace
    {
        /// <summary>
        /// 返回禁止节点的命中数。指定效果时，检查该效果出现过的所有根树；
        /// 若找不到效果所属的根树，则无法证明该作用域内不存在禁止节点。
        /// </summary>
        public static int Count(MobaAcceptanceTraceRecord[] records, MobaAcceptanceTraceExpectation expectation, out bool scopeFound)
        {
            HashSet<long> roots = null;
            if (expectation.underEffectId > 0)
            {
                roots = new HashSet<long>();
                foreach (var record in records)
                    if (record.kind == "EffectExecution" && record.configId == expectation.underEffectId && record.rootId > 0)
                        roots.Add(record.rootId);
            }

            scopeFound = roots == null || roots.Count > 0;
            if (!scopeFound) return 0;

            var count = 0;
            foreach (var record in records)
                if (record.kind == expectation.kind && record.configId == expectation.configId && (roots == null || roots.Contains(record.rootId)))
                    count++;
            return count;
        }
    }
}

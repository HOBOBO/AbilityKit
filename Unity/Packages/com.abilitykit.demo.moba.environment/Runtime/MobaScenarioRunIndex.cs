using System;

namespace AbilityKit.Demo.Moba.EnvironmentModel
{
    /// <summary>
    /// Console 场景批次使用的验收 v1 索引形状；不包含单场证据正文。
    /// </summary>
    [Serializable]
    public sealed class MobaScenarioRunIndex
    {
        /// <summary>批次索引文件名。</summary>
        public const string FileName = "batch_summary_v1.json";
        /// <summary>区分 Console Scenario 与其他验收产物生产链。</summary>
        public const string Suite = "moba-battle-scenario";

        public int schemaVersion;
        public string producerVersion = string.Empty;
        public string suiteId = string.Empty;
        public string host = string.Empty;
        public string evidenceMode = string.Empty;
        public string runId = string.Empty;
        public string startedAt = string.Empty;
        public long durationMs;
        public string status = string.Empty;
        public Counts counts = new Counts();
        public Case[] cases = Array.Empty<Case>();

        /// <summary>
        /// 用例统计；执行错误和无断言不计作断言失败或通过。
        /// </summary>
        [Serializable]
        public sealed class Counts
        {
            public int total;
            public int passed;
            public int failed;
            public int errors;
            public int noAssertions;
        }

        /// <summary>
        /// 列表行；文件引用相对于本批次根目录。
        /// </summary>
        [Serializable]
        public sealed class Case
        {
            public string caseId = string.Empty;
            public string scenario = string.Empty;
            public string status = string.Empty;
            public string summary = string.Empty;
            public string summaryUri = string.Empty;
            public string evidenceStatus = string.Empty;
            public string diagnosticUri = string.Empty;
        }
    }
}

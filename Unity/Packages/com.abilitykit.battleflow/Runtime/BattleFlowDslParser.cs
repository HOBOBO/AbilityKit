using System;
using System.Collections.Generic;
using AbilityKit.Scenario;

namespace AbilityKit.BattleFlow
{
    /// <summary>
    /// 战斗流程 DSL：文本语句 → 积木。策划/测试不拖积木时，用一行行命令描述场景，解析结果与拖积木的编译结果一致。
    /// 行语法（# 开头为注释）：
    ///   scene &lt;relative-or-absolute-ref&gt;（仅 <see cref="ParseDocument(string,string)"/>）
    ///   tag &lt;value&gt; [...]（仅 <see cref="ParseDocument(string,string)"/>）
    ///   env &lt;profileId&gt;
    ///   seed &lt;integer&gt;
    ///   execution tick=&lt;hz&gt; max=&lt;ms&gt; settle=&lt;ms&gt; end=timeline|duration duration=&lt;ms&gt;
    ///   spawn &lt;alias&gt; hero=&lt;id&gt; attr=&lt;id&gt; team=&lt;id&gt; player=&lt;id&gt; pos=&lt;x,y,z&gt;
    ///   cast &lt;actor&gt; &lt;target&gt; slot=&lt;n&gt; at=&lt;ms&gt;
    ///   wait &lt;ms&gt; at=&lt;ms&gt;
    ///   obstacle &lt;pos&gt; &lt;size&gt; &lt;id&gt;
    ///   network disconnect|reconnect at=&lt;ms&gt;
    ///   network packet inbound|outbound opcode=&lt;id&gt; seq=&lt;n&gt; at=&lt;ms&gt;
    ///   network phase at=&lt;ms&gt; until=&lt;ms&gt; direction=both|inbound|outbound latency=&lt;ms&gt;
    ///   assert …（以 assert 开头的动词委托给 <see cref="AssertFactory"/>，由项目注册断言积木）
    /// </summary>
    public static class BattleFlowDslParser
    {
        /// <summary>断言动词工厂：解析「assert xxx」这类项目专属断言，返回一个断言积木。项目（如 MOBA）在启动时注册。</summary>
        public static Func<string, string[], BattleBlock?>? AssertFactory { get; set; }

        /// <summary>把 DSL 文本解析成积木列表（未知/空行/注释行跳过）。</summary>
        public static IReadOnlyList<BattleBlock> Parse(string text)
            => Parse(text, null);

        /// <summary>Parses blocks with an optional project-specific verb factory.</summary>
        public static IReadOnlyList<BattleBlock> Parse(
            string text, Func<string, string[], BattleBlock?>? projectFactory)
        {
            var blocks = new List<BattleBlock>();
            if (string.IsNullOrWhiteSpace(text)) return blocks;

            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

                var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0) continue;

                var verb = tokens[0].ToLowerInvariant();
                var block = ParseLine(verb, tokens, line, projectFactory);
                if (block != null) blocks.Add(block);
            }

            return blocks;
        }

        /// <summary>Parses case metadata and blocks into a sectioned case document.</summary>
        public static BattleFlowDocument ParseDocument(string caseId, string text)
            => ParseDocument(caseId, text, null);

        /// <summary>Parses a sectioned case document with optional project-specific block verbs.</summary>
        public static BattleFlowDocument ParseDocument(
            string caseId,
            string text,
            Func<string, string[], BattleBlock?>? projectFactory)
        {
            var document = new BattleFlowDocument { CaseId = caseId };
            if (string.IsNullOrWhiteSpace(text)) return document;

            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0) continue;

                var verb = tokens[0].ToLowerInvariant();
                var args = tokens.Length > 1 ? tokens[1..] : Array.Empty<string>();
                if (verb == "scene")
                {
                    Require(args, 1, line);
                    if (args.Length != 1) throw new ArgumentException($"scene requires exactly one reference: {line}");
                    if (!string.IsNullOrWhiteSpace(document.ScenarioRef))
                        throw new ArgumentException("BattleFlow DSL may declare scene only once.");
                    document.ScenarioRef = args[0];
                    continue;
                }
                if (verb == "tag")
                {
                    Require(args, 1, line);
                    foreach (var tag in args)
                        if (!document.Tags.Contains(tag)) document.Tags.Add(tag);
                    continue;
                }

                var block = ParseLine(verb, tokens, line, projectFactory);
                if (block != null) document.Sections.Add(block);
            }

            return document;
        }

        private static BattleBlock? ParseLine(
            string verb, string[] tokens, string line,
            Func<string, string[], BattleBlock?>? projectFactory)
        {
            var args = tokens.Length > 1 ? tokens[1..] : Array.Empty<string>();
            switch (verb)
            {
                case "env":
                    Require(args, 1, line);
                    return new SetEnvironmentBlock { ProfileId = args[0] };

                case "seed":
                    Require(args, 1, line);
                    return new SetScenarioSeedBlock { Seed = ParseInt(args[0]) };

                case "execution":
                    return ParseExecution(args, line);

                case "spawn":
                    return ParseSpawn(args, line);

                case "cast":
                    return ParseCast(args, line);

                case "wait":
                    Require(args, 1, line);
                    var wait = new WaitBlock { DurationMs = ParseInt(args[0]) };
                    for (var i = 1; i < args.Length; i++)
                    {
                        var kv = SplitKeyValue(args[i]);
                        if (kv.Key == "at") wait.AtMs = ParseInt(kv.Value);
                    }
                    return wait;

                case "obstacle":
                    Require(args, 3, line);
                    return new PlaceObstacleBlock
                    {
                        Id = args[2],
                        Shape = "box",
                        Position = ParseVector(args[0]),
                        Size = ParseVector(args[1]),
                    };

                case "network":
                    return ParseNetwork(args, line);

                default:
                    var projectBlock = projectFactory?.Invoke(verb, args);
                    if (projectBlock != null) return projectBlock;
                    if (AssertFactory != null && verb.StartsWith("assert", StringComparison.Ordinal))
                    {
                        var assertion = AssertFactory(verb, args);
                        if (assertion != null) return assertion;
                    }
                    throw new ArgumentException($"Unknown BattleFlow DSL verb '{verb}': {line}");
            }
        }

        private static BattleBlock ParseExecution(string[] args, string line)
        {
            var block = new ExecutionSettingsBlock();
            foreach (var arg in args)
            {
                var kv = SplitKeyValue(arg);
                if (string.IsNullOrEmpty(kv.Value))
                    throw new ArgumentException($"Execution DSL parameter requires a value: {arg}");
                switch (kv.Key)
                {
                    case "tick":
                    case "tickrate":
                        block.TickRate = ParseInt(kv.Value);
                        break;
                    case "max":
                    case "timeout":
                    case "maxduration":
                        block.MaxDurationMs = ParseInt(kv.Value);
                        break;
                    case "settle":
                    case "settleduration":
                        block.SettleDurationMs = ParseInt(kv.Value);
                        break;
                    case "end":
                        block.EndCondition = NormalizeEndCondition(kv.Value, line);
                        break;
                    case "duration":
                        block.DurationMs = ParseInt(kv.Value);
                        break;
                    default:
                        throw new ArgumentException($"Unknown execution parameter '{kv.Key}': {line}");
                }
            }
            return block;
        }

        private static string NormalizeEndCondition(string value, string line)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "timeline":
                case "timeline_complete":
                    return TestEndConditionKinds.TimelineComplete;
                case "duration":
                    return TestEndConditionKinds.Duration;
                default:
                    throw new ArgumentException($"Unknown execution end condition '{value}': {line}");
            }
        }

        private static BattleBlock ParseSpawn(string[] args, string line)
        {
            Require(args, 1, line);
            var block = new SpawnActorBlock { Alias = args[0] };
            for (var i = 1; i < args.Length; i++)
            {
                var kv = SplitKeyValue(args[i]);
                switch (kv.Key)
                {
                    case "hero": block.HeroId = ParseInt(kv.Value); break;
                    case "attr": block.AttributeTemplateId = ParseInt(kv.Value); break;
                    case "team": block.TeamId = ParseInt(kv.Value); break;
                    case "player": block.PlayerId = kv.Value; break;
                    case "pos": block.Position = ParseVector(kv.Value); break;
                }
            }
            return block;
        }

        private static BattleBlock ParseCast(string[] args, string line)
        {
            Require(args, 2, line);
            var block = new TimelineStepBlock { Action = "cast_skill", ActorAlias = args[0], TargetAlias = args[1] };
            for (var i = 2; i < args.Length; i++)
            {
                var kv = SplitKeyValue(args[i]);
                switch (kv.Key)
                {
                    case "slot": block.Slot = ParseInt(kv.Value); break;
                    case "at": block.AtMs = ParseInt(kv.Value); break;
                }
            }
            return block;
        }

        private static BattleBlock ParseNetwork(string[] args, string line)
        {
            Require(args, 1, line);
            var action = args[0].ToLowerInvariant();
            if (action != "disconnect" && action != "reconnect" && action != "packet" && action != "phase")
                throw new ArgumentException($"Unknown network DSL action '{args[0]}': {line}");

            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            var block = new CommandBlock { Name = "network." + action, Parameters = parameters };
            var position = 1;
            if (action == "packet" && position < args.Length && args[position].IndexOf('=') < 0)
            {
                var direction = args[position++].ToLowerInvariant();
                if (direction != "inbound" && direction != "outbound")
                    throw new ArgumentException($"Invalid network packet direction '{direction}': {line}");
                parameters["direction"] = direction;
            }

            for (var i = position; i < args.Length; i++)
            {
                var kv = SplitKeyValue(args[i]);
                if (string.IsNullOrEmpty(kv.Value))
                    throw new ArgumentException($"Network DSL parameter requires a value: {args[i]}");
                if (kv.Key == "at") block.AtMs = ParseInt(kv.Value);
                else parameters[CanonicalNetworkParameter(kv.Key)] = kv.Value;
            }

            if (block.AtMs < 0) throw new ArgumentOutOfRangeException("at", "Network command time cannot be negative.");
            if (action == "packet")
            {
                if (!parameters.ContainsKey("direction")) parameters["direction"] = "inbound";
                if (!parameters.ContainsKey("opCode") || !parameters.ContainsKey("seq"))
                    throw new ArgumentException($"Network packet requires opcode and seq: {line}");
            }
            else if (action == "phase" && !parameters.ContainsKey("until"))
            {
                throw new ArgumentException($"Network phase requires until: {line}");
            }

            return block;
        }

        private static string CanonicalNetworkParameter(string key)
        {
            return key == "opcode" ? "opCode" : key;
        }

        private static (string Key, string Value) SplitKeyValue(string token)
        {
            var idx = token.IndexOf('=');
            return idx < 0
                ? (token.ToLowerInvariant(), string.Empty)
                : (token.Substring(0, idx).ToLowerInvariant(), token.Substring(idx + 1));
        }

        private static TestVector3 ParseVector(string text)
        {
            var parts = text.Split(',');
            return new TestVector3(
                ParseFloat(parts[0]),
                parts.Length > 1 ? ParseFloat(parts[1]) : 0f,
                parts.Length > 2 ? ParseFloat(parts[2]) : 0f);
        }

        private static int ParseInt(string text) => int.Parse(text);

        private static float ParseFloat(string text) => float.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

        private static void Require(string[] args, int count, string line)
        {
            if (args.Length < count) throw new ArgumentException($"DSL 行参数不足：{line}");
        }
    }
}

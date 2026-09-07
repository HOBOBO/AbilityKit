using System;
using System.Collections.Generic;
using AbilityKit.Scenario;

namespace AbilityKit.BattleFlow
{
    /// <summary>
    /// 战斗流程 DSL：文本语句 → 积木。策划/测试不拖积木时，用一行行命令描述场景，解析结果与拖积木的编译结果一致。
    /// 行语法（# 开头为注释）：
    ///   env &lt;profileId&gt;
    ///   spawn &lt;alias&gt; hero=&lt;id&gt; attr=&lt;id&gt; team=&lt;id&gt; player=&lt;id&gt; pos=&lt;x,y,z&gt;
    ///   cast &lt;actor&gt; &lt;target&gt; slot=&lt;n&gt; at=&lt;ms&gt;
    ///   wait &lt;ms&gt; at=&lt;ms&gt;
    ///   obstacle &lt;pos&gt; &lt;size&gt; &lt;id&gt;
    ///   assert …（以 assert 开头的动词委托给 <see cref="AssertFactory"/>，由项目注册断言积木）
    /// </summary>
    public static class BattleFlowDslParser
    {
        /// <summary>断言动词工厂：解析「assert xxx」这类项目专属断言，返回一个断言积木。项目（如 MOBA）在启动时注册。</summary>
        public static Func<string, string[], BattleBlock?>? AssertFactory { get; set; }

        /// <summary>把 DSL 文本解析成积木列表（未知/空行/注释行跳过）。</summary>
        public static IReadOnlyList<BattleBlock> Parse(string text)
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
                var block = ParseLine(verb, tokens, line);
                if (block != null) blocks.Add(block);
            }

            return blocks;
        }

        private static BattleBlock? ParseLine(string verb, string[] tokens, string line)
        {
            var args = tokens.Length > 1 ? tokens[1..] : Array.Empty<string>();
            switch (verb)
            {
                case "env":
                    Require(args, 1, line);
                    return new SetEnvironmentBlock { ProfileId = args[0] };

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

                default:
                    if (AssertFactory != null && verb.StartsWith("assert", StringComparison.Ordinal))
                        return AssertFactory(verb, args);
                    return null;
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

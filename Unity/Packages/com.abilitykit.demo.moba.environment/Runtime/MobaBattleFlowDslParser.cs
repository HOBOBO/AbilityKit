using System;
using System.Collections.Generic;
using AbilityKit.BattleFlow;

namespace AbilityKit.Demo.Moba.EnvironmentModel
{
    /// <summary>MOBA DSL entry point that adds project-specific assertions to neutral BattleFlow syntax.</summary>
    public static class MobaBattleFlowDslParser
    {
        public static IReadOnlyList<BattleBlock> Parse(string text)
            => BattleFlowDslParser.Parse(text, ParseProjectBlock);

        private static BattleBlock ParseProjectBlock(string verb, string[] args)
        {
            if (verb == "sync-backend")
            {
                if (args.Length != 1)
                    throw new ArgumentException("sync-backend requires: <headless|unity-route>.");
                return new SetPredictionBackendBlock
                {
                    BackendId = MobaPredictionBackendIds.Normalize(args[0]),
                };
            }
            if (!string.Equals(verb, "assert-sync", StringComparison.Ordinal)) return null;
            if (args == null || args.Length != 3)
                throw new ArgumentException(
                    "assert-sync requires: <property> <comparator> <value>.");

            return new AssertPredictionBlock
            {
                Property = args[0],
                Comparator = args[1],
                ExpectedValue = args[2],
            };
        }
    }
}

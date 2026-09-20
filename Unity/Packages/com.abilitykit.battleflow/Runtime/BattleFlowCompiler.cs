using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Scenario;

namespace AbilityKit.BattleFlow
{
    /// <summary>把积木树编译成玩法中立的 <see cref="TestScenario"/>（线性：按序编译，复合积木展平子积木）。</summary>
    public static class BattleFlowCompiler
    {
        /// <summary>按序编译一组积木（复合积木递归展平）成 <see cref="TestScenario"/>。</summary>
        public static TestScenario Compile(string caseId, IReadOnlyList<BattleBlock> blocks)
        {
            var builder = new BattleFlowBuilder { CaseId = caseId };
            foreach (var block in blocks) CompileBlock(block, builder);
            return builder.Build();
        }

        /// <summary>Compiles a self-contained or scene-backed case document.</summary>
        public static TestScenario Compile(
            BattleFlowDocument document,
            Func<string, BattleSceneDocument>? sceneResolver = null)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            BattleFlowDocumentValidator.ThrowIfInvalid(document);

            var blocks = new List<BattleBlock>();
            blocks.Add(BattleExecutionProfileCatalog.Resolve(document.ExecutionProfileId).CreateBlock());
            if (!string.IsNullOrWhiteSpace(document.ScenarioRef))
            {
                if (sceneResolver == null)
                    throw new InvalidOperationException(
                        $"Case '{document.CaseId}' references scene '{document.ScenarioRef}', but no scene resolver was provided.");
                var scene = sceneResolver(document.ScenarioRef)
                    ?? throw new InvalidDataException($"Scene resolver returned null for '{document.ScenarioRef}'.");
                BattleFlowDocumentValidator.ThrowIfInvalid(scene);
                blocks.AddRange(scene.GetOrderedBlocks());
            }

            blocks.AddRange(document.GetOrderedBlocks());
            return Compile(document.CaseId, blocks);
        }

        /// <summary>Loads a .battleflow and resolves its optional .battlescene reference relative to that file.</summary>
        public static TestScenario CompileFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Case path is required.", nameof(path));
            var document = BattleFlowCodec.Load(path);
            return Compile(document, reference =>
                BattleFlowCodec.LoadScene(BattleFlowCodec.ResolveScenePath(path, reference)));
        }

        private static void CompileBlock(BattleBlock? block, BattleFlowBuilder builder)
        {
            switch (block)
            {
                case null:
                    break;
                case BattleAtomicBlock atomic:
                    atomic.Compile(builder);
                    break;
                case BattleCompositeBlock composite:
                    foreach (var child in composite.Children) CompileBlock(child, builder);
                    break;
            }
        }
    }
}

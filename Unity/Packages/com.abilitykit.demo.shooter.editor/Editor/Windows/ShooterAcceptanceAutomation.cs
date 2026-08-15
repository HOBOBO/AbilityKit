#nullable enable

using System;
using System.Text;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Demo.Harness;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Demo.Shooter.Editor.Windows
{
    /// <summary>
    /// Shooter 验收自动化入口。可从 Unity 菜单或 batchmode 直接执行完整验收矩阵。
    /// </summary>
    public static class ShooterAcceptanceAutomation
    {
        private const string MenuRoot = "Tools/AbilityKit/Demos/Shooter/Shooter Demo/Acceptance";

        [MenuItem(MenuRoot + "/Run Catalog Matrix")]
        public static void RunCatalogMatrixMenu()
        {
            RunCatalogMatrixOrThrow();
        }

        /// <summary>
        /// 供 CI / batchmode 调用的无头入口：运行完整验收矩阵并在失败时抛出异常。
        /// </summary>
        public static DemoHarnessBatchResult RunCatalogMatrixOrThrow(int stepCount = ShooterAcceptanceSession.DefaultStepCount, float deltaSeconds = 1f / 30f, int seed = 0)
        {
            var batch = ShooterAcceptanceLab.RunCatalogMatrix(stepCount, deltaSeconds, seed);
            LogBatchResult(batch);

            if (!batch.AllCompleted || batch.FailedCount > 0 || batch.UnsupportedCount > 0)
            {
                throw new InvalidOperationException(
                    $"Shooter acceptance matrix failed: total={batch.ScenarioCount}, completed={batch.CompletedCount}, unsupported={batch.UnsupportedCount}, failed={batch.FailedCount}, degraded={batch.DegradedCount}.");
            }

            return batch;
        }

        private static void LogBatchResult(in DemoHarnessBatchResult batch)
        {
            var summary = new StringBuilder();
            summary.AppendLine($"Shooter acceptance matrix: total={batch.ScenarioCount}, completed={batch.CompletedCount}, unsupported={batch.UnsupportedCount}, failed={batch.FailedCount}, degraded={batch.DegradedCount}, allCompleted={batch.AllCompleted}");

            for (var i = 0; i < batch.Results.Count; i++)
            {
                var result = batch.Results[i];
                summary.AppendLine($"- {result.Scenario.CarrierName} / {result.Scenario.SyncModel}: {result.Status} ({result.Reason})");
            }

            var message = summary.ToString();
            Debug.Log(message);
        }
    }
}

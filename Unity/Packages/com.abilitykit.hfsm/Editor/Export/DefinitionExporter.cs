using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
using AbilityKit.HFSM.Migration;
using AbilityKit.HFSM.Graph;

namespace AbilityKit.HFSM.Editor.Export
{

    /// <summary>Graph -> validated Next Definition -> canonical JSON export pipeline.</summary>
    public static class DefinitionExporter
    {
        public static DefinitionExportResult Export(
            GraphAsset graph,
            BindingCatalog catalog = null)
        {
            var issues = new List<DefinitionExportIssue>();
            var import = LegacyGraphImporter.Import(graph);
            foreach (var issue in import.Issues)
            {
                issues.Add(new DefinitionExportIssue(
                    issue.Code, issue.Severity, issue.SourcePath, GetLegacyIssueMessage(issue.Code)));
            }

            if (import.Definition == null)
                return new DefinitionExportResult(null, string.Empty, issues);

            catalog = catalog ?? EditorBindingCatalog.Catalog;
            foreach (var catalogIssue in catalog.Issues)
            {
                issues.Add(new DefinitionExportIssue(
                    catalogIssue.Code,
                    LegacyImportSeverity.Error,
                    "$.bindings",
                    FormatCatalogIssue(catalogIssue)));
            }
            ValidateBindings(import.Definition, catalog, issues);
            if (issues.Any(issue => issue.Severity == LegacyImportSeverity.Error))
                return new DefinitionExportResult(null, string.Empty, issues);

            return new DefinitionExportResult(
                import.Definition,
                DefinitionJson.Save(import.Definition),
                issues);
        }

        public static DefinitionExportResult ExportUsingCatalogAsset(
            GraphAsset graph,
            BindingCatalogAsset catalogAsset)
        {
            if (catalogAsset == null)
                throw new ArgumentNullException(nameof(catalogAsset));
            return Export(graph, catalogAsset.BuildCatalog());
        }

        private static void ValidateBindings(
            StateMachineDefinition definition,
            BindingCatalog catalog,
            List<DefinitionExportIssue> issues)
        {
            foreach (var machine in definition.Machines)
            {
                foreach (var state in machine.States)
                {
                    ValidateKey(
                        catalog,
                        BindingKind.State,
                        state.BehaviorKey,
                        $"$.machines['{machine.Id}'].states['{state.Id}'].behaviorKey",
                        issues);
                    for (var keyIndex = 0; keyIndex < state.ParallelBehaviorKeys.Count; keyIndex++)
                    {
                        ValidateKey(
                            catalog,
                            BindingKind.State,
                            state.ParallelBehaviorKeys[keyIndex],
                            $"$.machines['{machine.Id}'].states['{state.Id}'].parallelBehaviorKeys[{keyIndex}]",
                            issues);
                    }
                }

                foreach (var transition in machine.Transitions)
                {
                    var path = $"$.machines['{machine.Id}'].transitions['{transition.Id}']";
                    ValidateKey(catalog, BindingKind.Condition, transition.ConditionKey,
                        path + ".conditionKey", issues);
                    ValidateKey(catalog, BindingKind.Action, transition.ActionKey,
                        path + ".actionKey", issues);
                }
            }
        }

        private static void ValidateKey(
            BindingCatalog catalog,
            BindingKind kind,
            string key,
            string path,
            List<DefinitionExportIssue> issues)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (catalog.Contains(kind, key)) return;
            issues.Add(new DefinitionExportIssue(
                "HFSMNEXT001",
                LegacyImportSeverity.Error,
                path,
                $"未知的 {GetBindingKindName(kind)} 绑定键“{key}”。请先注册绑定描述符再导出。"));
        }

        private static string GetBindingKindName(BindingKind kind)
        {
            switch (kind)
            {
                case BindingKind.State: return "状态";
                case BindingKind.Condition: return "条件";
                case BindingKind.Action: return "动作";
                default: return kind.ToString();
            }
        }

        private static string FormatCatalogIssue(BindingCatalogIssue issue)
        {
            var kind = GetBindingKindName(issue.Kind);
            switch (issue.Code)
            {
                case "HFSMBIND001": return $"{kind}绑定键“{issue.Key}”重复。";
                case "HFSMBIND002": return issue.Message;
                case "HFSMBIND003": return issue.Message;
                case "HFSMBIND004": return string.IsNullOrEmpty(issue.Key)
                    ? $"{kind}绑定缺少稳定键。"
                    : $"{kind}绑定键“{issue.Key}”无效。";
                default: return $"{kind}绑定“{issue.Key}”无效。";
            }
        }

        private static string GetLegacyIssueMessage(string code)
        {
            switch (code)
            {
                case "HFSMLEG001": return "旧版状态机图为空。";
                case "HFSMLEG002": return "根节点必须引用状态机节点。";
                case "HFSMLEG003": return "节点为空。";
                case "HFSMLEG004": return "节点 ID 不能为空。";
                case "HFSMLEG005": return "节点 ID 重复。";
                case "HFSMLEG010": return "子节点不存在。";
                case "HFSMLEG011": return "不支持此节点类型。";
                case "HFSMLEG013": return "状态行为或退出语义需要显式的稳定行为绑定键。";
                case "HFSMLEG014": return "未显式设置默认状态，将保留旧版的首个子节点回退规则。";
                case "HFSMLEG015": return "状态机不包含子状态。";
                case "HFSMLEG020": return "转换不存在。";
                case "HFSMLEG021": return "转换存在多个所属状态机。";
                case "HFSMLEG023": return "转换目标必须是所属状态机的直接子节点。";
                case "HFSMLEG024": return "转换来源必须是所属状态机的直接子节点。";
                case "HFSMLEG025": return "旧版多态条件需要显式的稳定条件绑定键。";
                case "HFSMLEG030": return "转换未被状态机的转换列表引用，因此未导入。";
                case "HFSMLEG090": return "Next Runtime 定义校验失败。";
                default: return "旧版状态机图迁移失败。";
            }
        }
    }
}

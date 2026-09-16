#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Samples.Abstractions;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Samples.Editor
{
    /// <summary>
    /// 呈现示例的学习内容：学习契约、自查点与源码走读。
    ///
    /// 与 <see cref="SampleVisualRenderer"/> 分工：那边画"这个示例跑起来长什么样"，
    /// 这边回答"这个示例教我什么、我该从哪里读起"。
    ///
    /// 三块数据各自的覆盖面差别很大，决定了呈现方式：
    ///   * <c>learningCheckpoints</c> 37/37、<c>codeWalkthrough</c> 37/37（每条 2–4 步）—— 主线；
    ///   * <c>learningContract</c> 里 <c>audience</c>/<c>outcomes</c>/<c>pitfalls</c> 是 37/37，
    ///     而 <c>summary</c>/<c>capabilities</c>/<c>apiHighlights</c>/<c>concepts</c> 只有 16/37 ——
    ///     所以"受众/收获/坑"是常规区块，能力清单类字段存在时才画。
    /// 缺数据的区块给一句说明而不是留白，免得看起来像界面坏了。
    ///
    /// 源码走读按需读取文件并缓存：OnGUI 每帧重绘，不能在绘制路径上做文件 I/O。
    /// </summary>
    internal static class SampleLearningRenderer
    {
        /// <summary>代码片段缓存，键为 <c>sourceFile:start-end</c>。展开过就记住，不再读盘。</summary>
        private static readonly Dictionary<string, string> CodeCache = new Dictionary<string, string>();

        /// <summary>代码片段的滚动位置，按同一个键保存，否则每帧都被重置到顶部。</summary>
        private static readonly Dictionary<string, Vector2> CodeScroll = new Dictionary<string, Vector2>();

        private static string? _workspaceRoot;

        // --- 学习契约 ---------------------------------------------------------

        /// <summary>
        /// 画学习契约。<paramref name="onNavigate"/> 用于跳转到前置示例，传 null 则前置只作文本展示。
        /// </summary>
        internal static void DrawLearningContract(SampleCatalogEntry entry, Action<string>? onNavigate)
        {
            var contract = entry.LearningContract;
            if (contract == null)
            {
                return;
            }

            // 注意：这几个字段是数组，取长度必须用 Length——数组上的 Count 会静默绑到
            // LINQ 的 Enumerable.Count 方法组，编译期报 CS0019 而不是给出错误结果。
            var hasIdentity = contract.Outcomes.Length > 0 ||
                              contract.Pitfalls.Length > 0 ||
                              contract.Prerequisites.Length > 0;
            var hasCapability = !string.IsNullOrWhiteSpace(contract.Summary) ||
                                contract.Capabilities.Length > 0 ||
                                contract.ApiHighlights.Length > 0 ||
                                contract.Concepts.Length > 0;

            if (!hasIdentity && !hasCapability)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("学习契约", EditorStyles.boldLabel);

            // 刻意不渲染 audience：37/37 都是"希望理解 {标题} 在 AbilityKit 学习路径中作用的开发者。"
            // 这个模板套标题的句子，看完标题就没有新信息了，摆出来只会把下面的真内容往下挤。
            // 同理 outcomes 的三条里有两条在 37 条示例里完全相同，所以放在末尾而不是开头。

            if (!string.IsNullOrWhiteSpace(contract.Summary))
            {
                EditorGUILayout.LabelField(contract.Summary, EditorStyles.wordWrappedLabel);
            }

            DrawChips("能力", contract.Capabilities);
            DrawChips("概念", contract.Concepts);
            DrawChips("输入提示", contract.InputHints);
            DrawStringList("关键 API", contract.ApiHighlights, mono: true);
            DrawStringList("输出提示", contract.OutputHints, mono: true);
            DrawPrerequisites(contract.Prerequisites, onNavigate);
            DrawStringList("常见坑", contract.Pitfalls);
            DrawStringList("学完能做到", contract.Outcomes);

            if (!string.IsNullOrWhiteSpace(contract.ExecutionHint))
            {
                EditorGUILayout.LabelField("执行方式: " + contract.ExecutionHint, EditorStyles.miniLabel);
            }
        }

        /// <summary>
        /// 前置示例做成可点的按钮——这是 <c>next</c> 链的反向索引，让学习者能补课而不是卡住。
        /// </summary>
        private static void DrawPrerequisites(IReadOnlyList<string> prerequisites, Action<string>? onNavigate)
        {
            if (prerequisites.Count == 0)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("前置", EditorStyles.boldLabel, GUILayout.Width(70f));
            EditorGUILayout.BeginVertical();
            foreach (var id in prerequisites)
            {
                if (onNavigate != null)
                {
                    if (GUILayout.Button("← " + id, EditorStyles.miniButton))
                    {
                        onNavigate(id);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(id, EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        // --- 自查点 -----------------------------------------------------------

        /// <summary>
        /// 画自查点。答案默认折叠——先想再看才有检验作用；
        /// <paramref name="expanded"/> 由窗口持有，跨帧保持展开状态。
        /// </summary>
        internal static void DrawCheckpoints(SampleCatalogEntry entry, HashSet<string> expanded)
        {
            var checkpoints = entry.LearningCheckpoints;
            if (checkpoints.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("自查（先想再看）", EditorStyles.boldLabel);

            foreach (var checkpoint in checkpoints)
            {
                var key = entry.Id + "/" + checkpoint.Id;
                var open = expanded.Contains(key);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                var title = string.IsNullOrWhiteSpace(checkpoint.Title)
                    ? checkpoint.Question
                    : checkpoint.Title;
                var next = EditorGUILayout.Foldout(open, title, true);
                if (next != open)
                {
                    if (next)
                    {
                        expanded.Add(key);
                    }
                    else
                    {
                        expanded.Remove(key);
                    }
                }

                if (next)
                {
                    if (!string.IsNullOrWhiteSpace(checkpoint.Goal))
                    {
                        EditorGUILayout.LabelField(checkpoint.Goal, EditorStyles.wordWrappedLabel);
                    }

                    if (!string.IsNullOrWhiteSpace(checkpoint.Question) &&
                        !string.Equals(checkpoint.Question, title, StringComparison.Ordinal))
                    {
                        EditorGUILayout.LabelField("问：" + checkpoint.Question, EditorStyles.wordWrappedLabel);
                    }

                    if (!string.IsNullOrWhiteSpace(checkpoint.ExpectedAnswer))
                    {
                        EditorGUILayout.LabelField("答：" + checkpoint.ExpectedAnswer, EditorStyles.wordWrappedLabel);
                    }

                    DrawCrossLinks(checkpoint.RelatedOutputHint, checkpoint.RelatedVisualStep, checkpoint.RelatedApis);
                }

                EditorGUILayout.EndVertical();
            }
        }

        // --- 源码走读 ---------------------------------------------------------

        /// <summary>
        /// 画源码走读。每一步把"源码位置 → 解释了什么 → 对应哪段输出/哪个视觉步骤"串起来，
        /// 这是把运行的示例和要读的代码连起来的那一环。
        /// </summary>
        internal static void DrawCodeWalkthrough(SampleCatalogEntry entry, HashSet<string> expandedCodes)
        {
            var steps = entry.CodeWalkthrough;
            if (steps.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("源码走读", EditorStyles.boldLabel);

            foreach (var step in steps)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(step.Title, EditorStyles.boldLabel);
                if (GUILayout.Button("打开", EditorStyles.miniButton, GUILayout.Width(48f)))
                {
                    OpenSource(step);
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField(
                    step.SourceFile + ":" + step.StartLine + "-" + step.EndLine,
                    EditorStyles.miniLabel);

                if (!string.IsNullOrWhiteSpace(step.Explanation))
                {
                    EditorGUILayout.LabelField(step.Explanation, EditorStyles.wordWrappedLabel);
                }

                DrawCrossLinks(step.OutputHint, step.VisualStep, null);

                var key = CodeKey(step);
                var open = expandedCodes.Contains(key);
                var next = EditorGUILayout.Foldout(open, "显示源码", true);
                if (next != open)
                {
                    if (next)
                    {
                        expandedCodes.Add(key);
                    }
                    else
                    {
                        expandedCodes.Remove(key);
                    }
                }

                if (next)
                {
                    DrawCodeExcerpt(step, key);
                }

                EditorGUILayout.EndVertical();
            }
        }

        private static void DrawCodeExcerpt(SampleCodeWalkthroughStep step, string key)
        {
            if (!CodeCache.TryGetValue(key, out var code))
            {
                code = ReadExcerpt(step);
                CodeCache[key] = code;
            }

            if (string.IsNullOrEmpty(code))
            {
                EditorGUILayout.HelpBox(
                    "读不到源码文件：" + step.SourceFile + "（清单里的路径可能已过期）",
                    MessageType.Warning);
                return;
            }

            if (!CodeScroll.TryGetValue(key, out var position))
            {
                position = Vector2.zero;
            }

            // TextArea 看着可编辑，但内容每帧都从缓存重建，改动不会留下——等于只读，还能选中复制。
            using (var scroll = new EditorGUILayout.ScrollViewScope(position, GUILayout.MaxHeight(240f)))
            {
                CodeScroll[key] = scroll.scrollPosition;
                EditorGUILayout.TextArea(code, EditorStyles.textArea);
            }
        }

        /// <summary>
        /// 按清单里的工作区相对路径读源码片段。找不到就返回空串（由调用方给出提示），不抛异常。
        /// </summary>
        private static string ReadExcerpt(SampleCodeWalkthroughStep step)
        {
            if (string.IsNullOrWhiteSpace(step.SourceFile) || step.StartLine <= 0 || step.EndLine < step.StartLine)
            {
                return string.Empty;
            }

            var root = WorkspaceRoot;
            if (string.IsNullOrEmpty(root))
            {
                return string.Empty;
            }

            var full = System.IO.Path.Combine(
                root,
                step.SourceFile.Replace('/', System.IO.Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(full))
            {
                return string.Empty;
            }

            try
            {
                var lines = System.IO.File.ReadAllLines(full);
                var start = Math.Max(0, step.StartLine - 1);
                var end = Math.Min(lines.Length - 1, step.EndLine - 1);
                if (start > end)
                {
                    return string.Empty;
                }

                var builder = new System.Text.StringBuilder();
                for (var i = start; i <= end; i++)
                {
                    builder.Append(i + 1);
                    builder.Append("  ");
                    builder.AppendLine(lines[i]);
                }

                return builder.ToString();
            }
            catch (System.IO.IOException)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 工作区根。清单里的路径是仓库相对（形如 <c>Unity/Packages/...</c>），
        /// 而 <c>Application.dataPath</c> 指向 <c>&lt;仓库&gt;/Unity/Assets</c>，故上溯两层。
        /// 用"能否解析出 Unity/Packages"兜底，而不是硬编码层数。
        /// </summary>
        private static string WorkspaceRoot
        {
            get
            {
                if (_workspaceRoot != null)
                {
                    return _workspaceRoot;
                }

                var unityProject = System.IO.Directory.GetParent(Application.dataPath);
                if (unityProject == null)
                {
                    _workspaceRoot = string.Empty;
                    return _workspaceRoot;
                }

                var workspace = System.IO.Directory.GetParent(unityProject.FullName);
                _workspaceRoot = workspace != null &&
                                 System.IO.Directory.Exists(
                                     System.IO.Path.Combine(workspace.FullName, "Unity", "Packages"))
                    ? workspace.FullName
                    : unityProject.FullName;
                return _workspaceRoot;
            }
        }

        private static void OpenSource(SampleCodeWalkthroughStep step)
        {
            var root = WorkspaceRoot;
            if (string.IsNullOrEmpty(root))
            {
                return;
            }

            var path = System.IO.Path.Combine(
                root,
                step.SourceFile.Replace('/', System.IO.Path.DirectorySeparatorChar));
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script == null)
            {
                Debug.LogWarning("[AbilityKit Samples] 找不到源码文件：" + step.SourceFile);
                return;
            }

            AssetDatabase.OpenAsset(script, Math.Max(1, step.StartLine));
        }

        private static string CodeKey(SampleCodeWalkthroughStep step)
        {
            return step.SourceFile + ":" + step.StartLine + "-" + step.EndLine;
        }

        // --- 小工具 -----------------------------------------------------------

        private static void DrawStringList(string label, IReadOnlyList<string> values, bool mono = false)
        {
            if (values.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            foreach (var value in values)
            {
                EditorGUILayout.LabelField(
                    "•  " + value,
                    mono ? EditorStyles.miniLabel : EditorStyles.wordWrappedLabel);
            }

            EditorGUI.indentLevel--;
        }

        /// <summary>短标签横排，适合能力名、概念这类不会换行的短串。</summary>
        private static void DrawChips(string label, IReadOnlyList<string> values)
        {
            if (values.Count == 0)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel, GUILayout.Width(70f));
            EditorGUILayout.LabelField(string.Join("  ·  ", values), EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>把"这段输出 / 这一步图 / 这些 API"三个横向引用收成一行，缺哪项就不显示哪项。</summary>
        private static void DrawCrossLinks(string outputHint, string visualStep, IReadOnlyList<string>? apis)
        {
            var links = new List<string>();
            if (!string.IsNullOrWhiteSpace(outputHint))
            {
                links.Add("输出 · " + outputHint);
            }

            if (!string.IsNullOrWhiteSpace(visualStep))
            {
                links.Add("图 · " + visualStep);
            }

            if (apis != null && apis.Count > 0)
            {
                links.Add("API · " + string.Join(" / ", apis));
            }

            if (links.Count > 0)
            {
                EditorGUILayout.LabelField(string.Join("    ", links), EditorStyles.miniLabel);
            }
        }
    }
}

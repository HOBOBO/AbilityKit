using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Text;

var outputDir = args.Length > 0 ? args[0] : Path.Combine("Docs", "ppt-assets", "abilitykit");
Directory.CreateDirectory(outputDir);

using var titleFont = Font(42, FontStyle.Bold);
using var subTitleFont = Font(28, FontStyle.Bold);
using var bodyFont = Font(23, FontStyle.Regular);
using var smallFont = Font(19, FontStyle.Regular);
using var checkFont = Font(38, FontStyle.Bold);

var colors = new Palette();

SaveCanvas("01-abilitykit-architecture-layers.png", g =>
{
    Header(g, "六个能力层解决不同问题", "工程构建、运行底座、会话外壳、逻辑模拟、玩法表达和落地验证彼此分层");
    var layers = new[]
    {
        new Layer("Engineering", colors.DarkLine, new[] { "一份源码", "UPM + .NET", "多端构建", "发布与文档" }),
        new Layer("Foundation", colors.Purple, new[] { "Core", "World.DI", "Event / Pool", "Stable ID" }),
        new Layer("Runtime Shell", colors.Blue, new[] { "Host", "Protocol", "Room Flow", "Network SDK" }),
        new Layer("Simulation", colors.Cyan, new[] { "ECS", "FrameSync", "StateSync", "Record / Replay" }),
        new Layer("Gameplay", colors.Green, new[] { "Trigger / Ability", "Attributes", "Targeting", "Projectile / Damage" }),
        new Layer("Example & Server", colors.Amber, new[] { "Console / ET", "MOBA", "Shooter", "Orleans" })
    };

    var y = 205f;
    foreach (var layer in layers)
    {
        RoundRect(g, new RectangleF(150, y, 1620, 104), 14, Brush(layer.Color), null);
        Text(g, layer.Name, new RectangleF(185, y + 26, 290, 48), bodyFont, Brushes.White, StringAlignment.Near);

        var x = 500f;
        foreach (var item in layer.Items)
        {
            RoundRect(g, new RectangleF(x, y + 20, 280, 64), 9, Brushes.White, null);
            Text(g, item, new RectangleF(x + 12, y + 27, 256, 48), smallFont, Brush(colors.Text));
            x += 300;
        }

        y += 124;
    }

    Text(g, "上层验证下层能力；下层不反向依赖具体玩法", new RectangleF(560, 955, 800, 42), bodyFont, Brush(colors.Muted));
});

SaveCanvas("02-abilitykit-capability-map.png", g =>
{
    Header(g, "项目从问题域选择能力组合", "组合名称帮助选型，不代表仓库提供唯一的预制战斗应用层");
    var stages = new[]
    {
        new Step("Foundation", "Core + World.DI\n事件、池化、生命周期", colors.Purple),
        new Step("SkillCore", "Pipeline + Triggering\nAbility + Attributes", colors.Blue),
        new Step("BattleRuntime", "Targeting + Projectile\nDamage + Motion", colors.Green),
        new Step("SyncRuntime", "FrameSync + Snapshot\nStateSync + Record", colors.Cyan),
        new Step("ServerRuntime", "Host + Protocol\nRoom + Orleans Adapter", colors.Amber)
    };
    const float startX = 105f;
    const float y = 310f;
    const float width = 300f;
    const float pitch = 350f;
    for (var i = 0; i < stages.Length; i++)
    {
        var stage = stages[i];
        var x = startX + i * pitch;
        var top = y + i * 62f;
        RoundRect(g, new RectangleF(x, top, width, 190), 14, Brush(stage.Color), null);
        Text(g, stage.Title, new RectangleF(x + 20, top + 25, width - 40, 44), subTitleFont, Brushes.White);
        Text(g, stage.Description, new RectangleF(x + 20, top + 82, width - 40, 76), smallFont, Brushes.White);
        if (i < stages.Length - 1)
            Arrow(g, x + width + 8, top + 95, x + pitch - 10, top + 157, Pen(colors.DarkLine, 3));
    }

    RoundRect(g, new RectangleF(270, 805, 1380, 105), 12, Brush("#EEF2F7"), Pen("#CBD5E1", 2));
    Text(g, "项目可以停在任意组合：框架提供稳定能力，英雄规则、房间流程、结算体验和预算仍由项目决定。", new RectangleF(320, 830, 1280, 55), bodyFont, Brush(colors.Text));
});

SaveCanvas("03-skill-cast-main-flow.png", g =>
{
    Header(g, "技能释放主链路", "从输入到表现事件：技能系统不是一个 Cast 函数");
    var steps = new[]
    {
        new Step("输入", "玩家 / AI / 脚本 / 网络", colors.Blue),
        new Step("校验", "冷却 / 资源 / 目标 / 状态", colors.Cyan),
        new Step("管线编排", "阶段 / 延迟 / 并行 / 中断", colors.Green),
        new Step("效果执行", "伤害 / Buff / 位移 / 投射物", colors.Amber),
        new Step("事件触发", "Hit / Damage / Death / BuffChanged", colors.Purple),
        new Step("输出", "表现事件 / Trace / Snapshot / 断言", colors.Red)
    };

    var x = 105f;
    const float y = 360f;
    for (var i = 0; i < steps.Length; i++)
    {
        var step = steps[i];
        RoundRect(g, new RectangleF(x, y, 260, 150), 14, Brush(step.Color), null);
        Text(g, step.Title, new RectangleF(x + 20, y + 24, 220, 40), subTitleFont, Brushes.White);
        Text(g, step.Description, new RectangleF(x + 20, y + 75, 220, 54), smallFont, Brushes.White);
        if (i < steps.Length - 1)
            Arrow(g, x + 270, y + 75, x + 335, y + 75, Pen(colors.DarkLine, 3));
        x += 305;
    }

});

SaveCanvas("04-moba-runtime-and-dsl-flow.png", g =>
{
    Header(g, "MOBA 示例：运行时启动链与 DSL 场景", "复杂战斗业务的治理方式：启动可验证，技能可追踪，场景可复用");
    var left = new[] { "WorldTypeRegistry", "Blueprint / Module", "WorldInitData", "EntitasWorld", "System Install", "Tick Execute" };
    var right = new[] { "BattleTestScript", "Move / Skill / Wait", "Console Driver", "View Runtime Driver", "Trace / Snapshot", "Smoke Assertion" };
    const float x1 = 260f;
    const float x2 = 1110f;
    var y = 220f;
    Text(g, "运行时启动链", new RectangleF(x1, 185, 420, 45), subTitleFont, Brush(colors.Text));
    Text(g, "DSL / 脚本场景", new RectangleF(x2, 185, 420, 45), subTitleFont, Brush(colors.Text));

    for (var i = 0; i < left.Length; i++)
    {
        RoundRect(g, new RectangleF(x1, y, 420, 70), 10, Brush("#E0F2FE"), Pen("#38BDF8", 2));
        Text(g, left[i], new RectangleF(x1 + 20, y + 15, 380, 40), bodyFont, Brush(colors.Text));
        RoundRect(g, new RectangleF(x2, y, 420, 70), 10, Brush("#ECFDF5"), Pen("#34D399", 2));
        Text(g, right[i], new RectangleF(x2 + 20, y + 15, 380, 40), bodyFont, Brush(colors.Text));
        if (i < left.Length - 1)
        {
            Arrow(g, x1 + 210, y + 75, x1 + 210, y + 118, Pen("#8EA0B8", 3));
            Arrow(g, x2 + 210, y + 75, x2 + 210, y + 118, Pen("#8EA0B8", 3));
        }
        y += 115;
    }

    Arrow(g, 690, 505, 1100, 505, Pen(colors.DarkLine, 3));
    Text(g, "同一脚本意图可驱动不同运行环境", new RectangleF(705, 455, 380, 46), smallFont, Brush(colors.Muted));
});

SaveCanvas("05-shooter-sync-matrix.png", g =>
{
    Header(g, "Shooter 示例：同步能力矩阵", "同步能力必须用矩阵验收，而不是只靠手动体验");
    var rows = new[] { "PredictRollback", "AuthoritativeInterpolation", "BatchStateSync", "MassBattleLodSync", "HybridHeroPrediction" };
    var cols = new[] { "启动", "收敛", "Snapshot", "协议", "回滚", "重连" };
    const float x0 = 260f;
    const float y0 = 250f;
    const float cw = 210f;
    const float ch = 92f;

    RoundRect(g, new RectangleF(x0, y0 - 85, cw * (cols.Length + 1), 72), 12, Brush("#334155"), null);
    Text(g, "Sync Model × 验收维度", new RectangleF(x0 + 25, y0 - 72, 520, 46), subTitleFont, Brushes.White, StringAlignment.Near);
    for (var c = 0; c < cols.Length; c++)
        Text(g, cols[c], new RectangleF(x0 + cw * (c + 1), y0 - 68, cw, 40), smallFont, Brushes.White);

    for (var r = 0; r < rows.Length; r++)
    {
        var y = y0 + r * ch;
        RoundRect(g, new RectangleF(x0, y, cw, ch - 8), 8, Brush("#E2E8F0"), Pen("#CBD5E1", 1));
        Text(g, rows[r], new RectangleF(x0 + 10, y + 12, cw - 20, ch - 32), smallFont, Brush(colors.Text));
        for (var c = 0; c < cols.Length; c++)
        {
            var cellColor = ((r + c) % 4) switch { 0 => "#DBEAFE", 1 => "#D1FAE5", 2 => "#FEF3C7", _ => "#EDE9FE" };
            RoundRect(g, new RectangleF(x0 + cw * (c + 1), y, cw - 8, ch - 8), 8, Brush(cellColor), Pen("#CBD5E1", 1));
            Text(g, "✓", new RectangleF(x0 + cw * (c + 1), y + 8, cw - 8, ch - 24), checkFont, Brush(colors.Text));
        }
    }

    Text(g, "DemoHarness 将 sync model、carrier、network profile、scenario 组合成可自动回归的验收矩阵。", new RectangleF(320, 790, 1280, 64), bodyFont, Brush(colors.Muted));
});

SaveCanvas("06-test-gates-ci-pyramid.png", g =>
{
    Header(g, "P0/P1/P2 表示风险层级，不等于 CI 触发时机", "28 个 gate 配置表达治理意图；workflow 手写覆盖一部分，实际结果还要核对 Artifact");
    const float cx = 730f;
    var levels = new[]
    {
        new GateLevel("P2 Regression Baseline", "批量回归 / 候选发布 / 大范围重构", 1040f, 690f, colors.Purple),
        new GateLevel("P1 Contract Blocker", "runtime contracts / Unity EditMode / 同步专项", 820f, 520f, colors.Cyan),
        new GateLevel("P0 Development Blocker", "precheck / build / test / 主链路 smoke", 600f, 350f, colors.Green)
    };

    foreach (var level in levels)
    {
        var x = cx - level.Width / 2;
        RoundRect(g, new RectangleF(x, level.Y, level.Width, 135), 14, Brush(level.Color), null);
        Text(g, level.Name, new RectangleF(x + 35, level.Y + 25, level.Width - 70, 40), subTitleFont, Brushes.White);
        Text(g, level.Description, new RectangleF(x + 35, level.Y + 75, level.Width - 70, 35), smallFont, Brushes.White);
    }

    RoundRect(g, new RectangleF(1370, 285, 390, 420), 16, Brushes.White, Pen("#CBD5E1", 2));
    RoundRect(g, new RectangleF(1370, 285, 390, 68), 16, Brush(colors.Amber), null);
    Text(g, "证据链", new RectangleF(1390, 300, 350, 38), subTitleFont, Brushes.White);
    Text(g, "配置与执行", new RectangleF(1400, 380, 300, 34), bodyFont, Brush(colors.Green), StringAlignment.Near);
    Text(g, "test-gates.json\nrun_test_gate.ps1", new RectangleF(1400, 420, 330, 90), smallFont, Brush(colors.Text), StringAlignment.Near);
    Text(g, "自动编排与结果", new RectangleF(1400, 545, 300, 34), bodyFont, Brush(colors.Red), StringAlignment.Near);
    Text(g, "workflow 手写部分 job\ncommit + result + artifact", new RectangleF(1400, 590, 330, 82), smallFont, Brush(colors.Text), StringAlignment.Near);
    Arrow(g, 1255, 520, 1360, 520, Pen(colors.DarkLine, 3));
});

SaveCanvas("07-company-reuse-feedback-loop.png", g =>
{
    Header(g, "公司级复用闭环", "框架价值不止是省代码，而是让问题、规范和测试跨项目沉淀");
    var left = new[] { "项目 A", "项目 B", "项目 C" };
    var right = new[] { "模块修复", "规范更新", "测试补充", "文档沉淀" };
    for (var i = 0; i < left.Length; i++)
    {
        var y = 250 + i * 180;
        RoundRect(g, new RectangleF(130, y, 300, 95), 12, Brush("#DBEAFE"), Pen("#60A5FA", 2));
        Text(g, left[i], new RectangleF(150, y + 22, 260, 45), subTitleFont, Brush(colors.Text));
        Arrow(g, 435, y + 48, 690, 500, Pen(colors.DarkLine, 3));
    }

    RoundRect(g, new RectangleF(705, 375, 510, 250), 18, Brush(colors.Green), null);
    Text(g, "AbilityKit\n公共战斗能力", new RectangleF(735, 405, 450, 112), subTitleFont, Brushes.White);
    Text(g, "技能 / 触发 / Buff / 同步 / 测试 / 文档", new RectangleF(755, 530, 410, 48), smallFont, Brushes.White);

    for (var i = 0; i < right.Length; i++)
    {
        var y = 210 + i * 155;
        RoundRect(g, new RectangleF(1410, y, 350, 85), 12, Brush("#FEF3C7"), Pen("#F59E0B", 2));
        Text(g, right[i], new RectangleF(1430, y + 18, 310, 42), bodyFont, Brush(colors.Text));
        Arrow(g, 1225, 500, 1400, y + 42, Pen(colors.DarkLine, 3));
    }

    Text(g, "一个项目发现的问题，转化为框架资产后，后续项目通过升级、测试和文档直接受益。", new RectangleF(250, 860, 1420, 58), bodyFont, Brush(colors.Muted));
});

SaveCanvas("08-graph-component-selection.png", g =>
{
    Header(g, "图式组件选型", "先判断业务主语，再选择 Pipeline / HFSM / Flow / BehaviorTree");
    var cards = new[]
    {
        new Group("一次能力经历哪些阶段", colors.Blue, new[] { "Pipeline", "技能前摇 / 释放 / 后摇", "run 级中断和追踪" }),
        new Group("实体现在是什么状态", colors.Cyan, new[] { "HFSM", "Idle / Move / Attack / Dead", "状态转换和退出条件" }),
        new Group("一串任务如何完成", colors.Green, new[] { "Flow", "加载 / 匹配 / 进战斗", "取消、失败和清理" }),
        new Group("AI 当前选哪个行为", colors.Amber, new[] { "BehaviorTree", "巡逻 / 追击 / 释放技能", "优先级重评估" })
    };
    var positions = new[] { (140f, 250f), (1010f, 250f), (140f, 620f), (1010f, 620f) };
    for (var i = 0; i < cards.Length; i++)
    {
        var (x, y) = positions[i];
        var card = cards[i];
        RoundRect(g, new RectangleF(x, y, 760, 250), 16, Brushes.White, Pen("#CBD5E1", 2));
        RoundRect(g, new RectangleF(x, y, 760, 66), 16, Brush(card.Color), null);
        Text(g, card.Title, new RectangleF(x + 26, y + 11, 708, 44), subTitleFont, Brushes.White, StringAlignment.Near);
        Text(g, card.Items[0], new RectangleF(x + 35, y + 92, 260, 42), subTitleFont, Brush(colors.Text), StringAlignment.Near);
        Text(g, card.Items[1], new RectangleF(x + 320, y + 92, 390, 42), bodyFont, Brush(colors.Text), StringAlignment.Near);
        Text(g, card.Items[2], new RectangleF(x + 320, y + 150, 390, 42), bodyFont, Brush(colors.Muted), StringAlignment.Near);
    }
    Text(g, "核心判断：不是哪个模块也能做，而是谁拥有生命周期、边代表什么、失败/中断由谁收尾。", new RectangleF(260, 900, 1400, 48), bodyFont, Brush(colors.Muted));
});

SaveCanvas("09-moba-skill-runtime-lifecycle.png", g =>
{
    Header(g, "MOBA 技能 Runtime 生命周期", "一次技能释放从输入到收尾都有正式 runtime 承载状态和诊断");
    var steps = new[]
    {
        new Step("输入请求", "玩家 / AI / DSL", colors.Blue),
        new Step("准备阶段", "SkillCastPreparation\n上下文 + trace root", colors.Cyan),
        new Step("创建 Runtime", "MobaSkillCastRuntime\nhandle / blackboard", colors.Green),
        new Step("执行 Pipeline", "PreCast / Cast\nphase runner", colors.Amber),
        new Step("产生子链路", "trigger / projectile\nbuff / damage", colors.Purple),
        new Step("终止清理", "complete / cancel\nchildren / trace", colors.Red)
    };
    var x = 90f;
    const float y = 365f;
    for (var i = 0; i < steps.Length; i++)
    {
        var step = steps[i];
        RoundRect(g, new RectangleF(x, y, 270, 150), 14, Brush(step.Color), null);
        Text(g, step.Title, new RectangleF(x + 15, y + 20, 240, 38), subTitleFont, Brushes.White);
        Text(g, step.Description, new RectangleF(x + 18, y + 70, 234, 58), smallFont, Brushes.White);
        if (i < steps.Length - 1)
            Arrow(g, x + 278, y + 75, x + 325, y + 75, Pen(colors.DarkLine, 3));
        x += 310;
    }
    RoundRect(g, new RectangleF(315, 670, 1290, 105), 14, Brushes.White, Pen("#CBD5E1", 2));
});

SaveCanvas("10-moba-trigger-context-trace-flow.png", g =>
{
    Header(g, "MOBA 触发执行与上下文溯源", "trigger 不只是调用效果，而是统一 payload、lineage、origin、trace 和预算控制");
    var top = new[] { "触发源", "Gateway", "EffectExecution", "ExecutionContext", "Trace Scope", "Plan Executor" };
    var bottom = new[] { "Skill / Buff\nProjectile / Area", "Direct / OwnerBound", "Budget / Condition", "payload + lineage\norigin + snapshot", "root / child\n诊断链路", "Action / Function\nEventBus" };
    var x = 110f;
    for (var i = 0; i < top.Length; i++)
    {
        RoundRect(g, new RectangleF(x, 300, 260, 90), 12, Brush(i % 2 == 0 ? "#E0F2FE" : "#ECFDF5"), Pen("#CBD5E1", 2));
        Text(g, top[i], new RectangleF(x + 15, 318, 230, 40), bodyFont, Brush(colors.Text));
        RoundRect(g, new RectangleF(x, 470, 260, 120), 12, Brushes.White, Pen("#CBD5E1", 2));
        Text(g, bottom[i], new RectangleF(x + 15, 490, 230, 70), smallFont, Brush(colors.Muted));
        Arrow(g, x + 130, 395, x + 130, 465, Pen(colors.DarkLine, 2));
        if (i < top.Length - 1)
            Arrow(g, x + 265, 345, x + 310, 345, Pen(colors.DarkLine, 3));
        x += 310;
    }
    Text(g, "收益：新增触发场景只补 payload/lineage 适配，不复制一套 context + origin + trace 胶水。", new RectangleF(260, 760, 1400, 54), bodyFont, Brush(colors.Muted));
});

SaveCanvas("11-moba-buff-lifecycle.png", g =>
{
    Header(g, "MOBA Buff 生命周期正式化", "Buff 的难点不是加状态，而是 apply、replace、remove、expire 的顺序和扩展点");
    var phases = new[]
    {
        new Step("Apply", "EnqueueApply\n叠层 / 替换", colors.Blue),
        new Step("Runtime", "BuffRuntime\nkey / source / origin", colors.Cyan),
        new Step("Binding", "continuous\ntrigger owner\ntrace context", colors.Green),
        new Step("Notify", "事件 / 表现\nstage effect", colors.Amber),
        new Step("End", "EndRuntime\n六步严格回收", colors.Purple),
        new Step("Reconcile", "帧级对账\n过期 / 标签移除", colors.Red)
    };
    var x = 120f;
    const float y = 320f;
    for (var i = 0; i < phases.Length; i++)
    {
        var phase = phases[i];
        RoundRect(g, new RectangleF(x, y, 250, 145), 14, Brush(phase.Color), null);
        Text(g, phase.Title, new RectangleF(x + 15, y + 18, 220, 38), subTitleFont, Brushes.White);
        Text(g, phase.Description, new RectangleF(x + 18, y + 68, 214, 58), smallFont, Brushes.White);
        if (i < phases.Length - 1)
            Arrow(g, x + 255, y + 73, x + 305, y + 73, Pen(colors.DarkLine, 3));
        x += 300;
    }
    RoundRect(g, new RectangleF(245, 645, 1430, 135), 16, Brushes.White, Pen("#CBD5E1", 2));
    Text(g, "入口薄、生命周期厚：MobaBuffService 只 EnqueueApply / DrainPending + ReconcileActorBuffLifecycles 对账；BuffEndFlow 按 停 continuous → 结束 trace → 清 owner binding → 释放 skill runtime → 回收 顺序清理。", new RectangleF(245, 670, 1430, 80), bodyFont, Brush(colors.Text));
});

SaveCanvas("12-shooter-pure-csharp-projection.png", g =>
{
    Header(g, "Shooter 纯 C# 到 Unity 表现投影", "同步和玩法逻辑先在纯 C# 可测，Unity 只消费投影后的表现状态");
    var nodes = new[]
    {
        new Step("Shooter Runtime", "确定性玩法\nSimulation / World", colors.Blue),
        new Step("Sync Controller", "PredictRollback\nAuthInterpolation", colors.Cyan),
        new Step("Snapshot Payload", "权威样本\n状态批次", colors.Green),
        new Step("View Projection", "batch -> store\n增量合并", colors.Amber),
        new Step("Unity Shell", "Session.Tick\nRender Sink", colors.Purple)
    };
    var x = 170f;
    const float y = 330f;
    for (var i = 0; i < nodes.Length; i++)
    {
        var node = nodes[i];
        RoundRect(g, new RectangleF(x, y, 275, 160), 14, Brush(node.Color), null);
        Text(g, node.Title, new RectangleF(x + 15, y + 22, 245, 42), subTitleFont, Brushes.White);
        Text(g, node.Description, new RectangleF(x + 18, y + 78, 239, 58), smallFont, Brushes.White);
        if (i < nodes.Length - 1)
            Arrow(g, x + 285, y + 80, x + 345, y + 80, Pen(colors.DarkLine, 3));
        x += 350;
    }
    Text(g, "约束：纯 C# 域不得依赖 UnityEngine；Unity 外壳只负责喂 deltaTime 和渲染 projection/store。", new RectangleF(280, 700, 1360, 55), bodyFont, Brush(colors.Muted));
});

SaveCanvas("13-demoharness-three-axis.png", g =>
{
    Header(g, "DemoHarness 三轴正交模型", "同步能力、网络环境、演示载体拆开，才能批量组合、诊断和回归");
    var axes = new[]
    {
        new Group("A 同步能力档案", colors.Blue, new[] { "PredictRollback", "AuthoritativeInterpolation", "HybridHeroPrediction" }),
        new Group("B 网络环境", colors.Green, new[] { "Ideal / LAN", "4G / CrossRegion", "PoorWifi / Loss" }),
        new Group("C 演示载体", colors.Amber, new[] { "Shooter 2D", "Moba 3D", "未来项目 carrier" })
    };
    var xs = new[] { 150f, 620f, 1090f };
    for (var i = 0; i < axes.Length; i++)
    {
        var axis = axes[i];
        RoundRect(g, new RectangleF(xs[i], 250, 390, 330), 16, Brushes.White, Pen("#CBD5E1", 2));
        RoundRect(g, new RectangleF(xs[i], 250, 390, 70), 16, Brush(axis.Color), null);
        Text(g, axis.Title, new RectangleF(xs[i] + 20, 263, 350, 42), subTitleFont, Brushes.White);
        var y = 355f;
        foreach (var item in axis.Items)
        {
            Text(g, "• " + item, new RectangleF(xs[i] + 35, y, 320, 38), bodyFont, Brush(colors.Text), StringAlignment.Near);
            y += 62;
        }
        Arrow(g, xs[i] + 195, 590, 960, 725, Pen(colors.DarkLine, 3));
    }
    RoundRect(g, new RectangleF(700, 720, 520, 115), 16, Brush(colors.Purple), null);
    Text(g, "可运行矩阵\nCompleted / Degraded / Failed / Unsupported", new RectangleF(735, 742, 450, 68), bodyFont, Brushes.White);
});

SaveCanvas("12b-coordinator-adapter-maturity.png", g =>
{
    Header(g, "通用 Coordinator 不再提供现役会话总装器", "当前 Package 保留配置、契约和值对象；world、连接、Tick、预测和恢复由业务 Session 明确拥有");
    RoundRect(g, new RectangleF(660, 210, 600, 135), 16, Brush(colors.Blue), null);
    Text(g, "Business Session", new RectangleF(705, 235, 510, 42), subTitleFont, Brushes.White);
    Text(g, "world / connections / Tick / recovery", new RectangleF(705, 288, 510, 32), smallFont, Brushes.White);

    var paths = new[]
    {
        new Step("Room Flow", "create / join / ready\nloading / restore", colors.Green),
        new Step("Sync Runtime", "profile / prediction\nsnapshot / replay", colors.Cyan),
        new Step("Battle Data Plane", "input request\nstate push / ack", colors.Amber)
    };
    var xs = new[] { 170f, 720f, 1270f };
    for (var i = 0; i < paths.Length; i++)
    {
        var step = paths[i];
        Arrow(g, 960, 355, xs[i] + 240, 460, Pen("#94A3B8", 3));
        RoundRect(g, new RectangleF(xs[i], 460, 480, 170), 14, Brushes.White, Pen(step.Color, 3));
        Text(g, step.Title, new RectangleF(xs[i] + 25, 485, 430, 40), subTitleFont, Brush(colors.Text));
        Text(g, step.Description, new RectangleF(xs[i] + 25, 545, 430, 60), bodyFont, Brush(step.Color));
    }

    using var retiredPen = Pen("#94A3B8", 2);
    retiredPen.DashStyle = DashStyle.Dash;
    RoundRect(g, new RectangleF(270, 735, 1380, 105), 12, Brush("#F1F5F9"), retiredPen);
    Text(g, "历史实现当前不存在：SessionCoordinator / LocalSyncAdapter / RemoteSyncAdapter / HybridSyncAdapter", new RectangleF(320, 760, 1280, 54), bodyFont, Brush(colors.Muted));
});

SaveCanvas("14-client-flow-boundaries.png", g =>
{
    Header(g, "Client Flow 与表现边界", "客户端流程编排只负责 state lifecycle 到 feature assembly，不替代项目框架");
    var lanes = new[]
    {
        new Step("HFSM", "状态规划\ntransition 条件", colors.Blue),
        new Step("AbilityKit.Flow", "可等待动作\n取消 / 失败 / 清理", colors.Cyan),
        new Step("Client Flow", "state enter/exit\nfeature 装配", colors.Green),
        new Step("Modules", "feature 内部\nattach / detach / tick", colors.Amber),
        new Step("Presentation", "snapshot -> batch\nview adapter 消费", colors.Purple)
    };
    var y = 230f;
    foreach (var lane in lanes)
    {
        RoundRect(g, new RectangleF(245, y, 1430, 105), 14, Brushes.White, Pen("#CBD5E1", 2));
        RoundRect(g, new RectangleF(245, y, 285, 105), 14, Brush(lane.Color), null);
        Text(g, lane.Title, new RectangleF(270, y + 25, 235, 45), subTitleFont, Brushes.White);
        Text(g, lane.Description, new RectangleF(570, y + 20, 1040, 60), bodyFont, Brush(colors.Text), StringAlignment.Near);
        y += 130;
    }
});

SaveCanvas("15-targeting-query-chain.png", g =>
{
    Header(g, "Targeting 查询链路", "目标选择从范围搜索到结果缓存，每一步都应该可配置、可测试、可替换");
    var steps = new[]
    {
        new Step("Query Spec", "阵营 / 半径\n形状 / origin", colors.Blue),
        new Step("Spatial Search", "候选收集\ngrid / physics", colors.Cyan),
        new Step("Filter", "阵营 / 状态\n标签 / 可见性", colors.Green),
        new Step("Score & Sort", "距离 / 角度\n威胁 / 权重", colors.Amber),
        new Step("Select", "single / topN\nrandom / nearest", colors.Purple),
        new Step("Result", "cache / trace\nassertion", colors.Red)
    };
    DrawHorizontalSteps(g, steps, 105, 330, 275, 150, 298);
    Text(g, "收益：同一个查询链可以服务技能选敌、AI 选目标、自动索敌和 DSL 断言，不再散落在各个技能脚本里。", new RectangleF(250, 710, 1420, 55), bodyFont, Brush(colors.Muted));
});

SaveCanvas("16-projectile-lifecycle.png", g =>
{
    Header(g, "Projectile 生命周期", "投射物要承载来源、飞行、碰撞、命中触发和回收，不只是一个飞行表现");
    var steps = new[]
    {
        new Step("Launch", "source context\nskill runtime", colors.Blue),
        new Step("Runtime", "速度 / 轨迹\nowner / lifetime", colors.Cyan),
        new Step("Collision", "hit test\n穿透 / 阻挡", colors.Green),
        new Step("Hit Trigger", "ProjectileHitArgs\n触发计划", colors.Amber),
        new Step("Area Effect", "爆炸 / 范围\n二次查询", colors.Purple),
        new Step("Recycle", "release child\npool / trace", colors.Red)
    };
    DrawHorizontalSteps(g, steps, 105, 315, 275, 160, 298);
    RoundRect(g, new RectangleF(320, 650, 1280, 115), 16, Brushes.White, Pen("#CBD5E1", 2));
    Text(g, "关键点：ProjectileSourceContextBuilder 保证命中后仍能知道这颗投射物来自谁、哪个技能、哪次 runtime 和哪条 trace。", new RectangleF(360, 680, 1200, 46), bodyFont, Brush(colors.Text));
});

SaveCanvas("17-damage-pipeline.png", g =>
{
    Header(g, "Damage 两层结算边界", "公共包负责纯计算顺序，玩法应用层负责状态修改、事件、派生触发与溯源");
    Text(g, "通用内核：DamageCalculationPipeline", new RectangleF(130, 205, 760, 44), subTitleFont, Brush(colors.Text));
    Text(g, "MOBA 参考实现：DamagePipelineService", new RectangleF(1030, 205, 760, 44), subTitleFont, Brush(colors.Text));

    var kernel = new[]
    {
        new Step("Validate", "请求合法性", colors.Blue),
        new Step("Critical / Base", "暴击与基础伤害", colors.Cyan),
        new Step("Bonus / Resist", "加成、护甲与魔抗", colors.Green),
        new Step("Final / Overkill", "最终值与溢出", colors.Purple)
    };
    var app = new[]
    {
        new Step("Stage Events", "玩法阶段与免疫", colors.Amber),
        new Step("Apply State", "shield / health", colors.Red),
        new Step("Derived Trigger", "被动 / Buff / 反伤", colors.Purple),
        new Step("Trace Child", "来源与结果溯源", colors.Blue)
    };
    for (var i = 0; i < 4; i++)
    {
        var y = 275 + i * 125;
        RoundRect(g, new RectangleF(130, y, 700, 90), 12, Brushes.White, Pen(kernel[i].Color, 2));
        Text(g, kernel[i].Title, new RectangleF(155, y + 14, 240, 30), bodyFont, Brush(colors.Text), StringAlignment.Near);
        Text(g, kernel[i].Description, new RectangleF(400, y + 15, 390, 28), smallFont, Brush(colors.Muted), StringAlignment.Near);
        Text(g, i == 2 ? "typed DamageSlots" : "processor", new RectangleF(400, y + 48, 390, 24), smallFont, Brush(kernel[i].Color), StringAlignment.Near);

        RoundRect(g, new RectangleF(1090, y, 700, 90), 12, Brushes.White, Pen(app[i].Color, 2));
        Text(g, app[i].Title, new RectangleF(1115, y + 14, 240, 30), bodyFont, Brush(colors.Text), StringAlignment.Near);
        Text(g, app[i].Description, new RectangleF(1360, y + 15, 390, 28), smallFont, Brush(colors.Muted), StringAlignment.Near);
        Text(g, "gameplay orchestration", new RectangleF(1360, y + 48, 390, 24), smallFont, Brush(app[i].Color), StringAlignment.Near);
        if (i < 3)
        {
            Arrow(g, 480, y + 94, 480, y + 119, Pen("#94A3B8", 3));
            Arrow(g, 1440, y + 94, 1440, y + 119, Pen("#94A3B8", 3));
        }
    }
    Arrow(g, 845, 465, 1075, 465, Pen(colors.DarkLine, 4));
    Text(g, "DamageResult", new RectangleF(850, 415, 220, 38), smallFont, Brush(colors.Muted));
    Text(g, "纯计算规则进入 processor / slot；玩法状态与事件顺序留在应用编排，并分别测试。", new RectangleF(260, 835, 1400, 48), bodyFont, Brush(colors.Muted));
});

SaveCanvas("18-attributes-modifier-stack.png", g =>
{
    Header(g, "Attributes 修饰器栈", "属性系统的价值在于把来源、乘区、脏标记和快照输出变成标准协议");
    var columns = new[]
    {
        new Group("Base", colors.Blue, new[] { "等级 / 配置", "初始属性", "成长曲线" }),
        new Group("Add", colors.Cyan, new[] { "装备", "Buff flat", "临时加值" }),
        new Group("Multiply", colors.Green, new[] { "百分比", "乘区策略", "上下限" }),
        new Group("Dirty", colors.Amber, new[] { "Dirty 标记", "Recompute", "依赖传播 + 缓存失效" }),
        new Group("Snapshot", colors.Purple, new[] { "表现", "同步", "测试断言" })
    };
    var x = 150f;
    foreach (var col in columns)
    {
        RoundRect(g, new RectangleF(x, 260, 285, 380), 16, Brushes.White, Pen("#CBD5E1", 2));
        RoundRect(g, new RectangleF(x, 260, 285, 70), 16, Brush(col.Color), null);
        Text(g, col.Title, new RectangleF(x + 20, 275, 245, 38), subTitleFont, Brushes.White);
        var y = 370f;
        foreach (var item in col.Items)
        {
            Text(g, item, new RectangleF(x + 28, y, 230, 38), bodyFont, Brush(colors.Text));
            y += 72;
        }
        if (x < 1350) Arrow(g, x + 292, 450, x + 350, 450, Pen(colors.DarkLine, 3));
        x += 325;
    }
});

SaveCanvas("19-record-replay-debug-flow.png", g =>
{
    Header(g, "FrameRecord：从三轨记录到可执行回归", "记录不是录像文件，而是 input、snapshot、state hash 可按帧读取和重放的验证资产");
    var tracks = new[]
    {
        new Step("Input Track", "玩家命令 / opcode\n输入帧序", colors.Blue),
        new Step("Snapshot Track", "全量 / 增量状态\nround-trip", colors.Cyan),
        new Step("State Hash Track", "确定性摘要\nfirst divergence", colors.Purple)
    };
    for (var i = 0; i < tracks.Length; i++)
    {
        var y = 235 + i * 135;
        RoundRect(g, new RectangleF(120, y, 520, 100), 12, Brush(tracks[i].Color), null);
        Text(g, tracks[i].Title, new RectangleF(145, y + 14, 220, 34), bodyFont, Brushes.White, StringAlignment.Near);
        Text(g, tracks[i].Description, new RectangleF(370, y + 14, 240, 65), smallFont, Brushes.White, StringAlignment.Near);
        Arrow(g, 650, y + 50, 775, 485, Pen("#94A3B8", 3));
    }
    RoundRect(g, new RectangleF(780, 380, 360, 210), 16, Brushes.White, Pen(colors.Green, 3));
    Text(g, "Replay Source", new RectangleF(805, 405, 310, 42), bodyFont, Brush(colors.Text));
    Text(g, "JSON / optimized binary\nreplaceable codec\nframe-indexed read", new RectangleF(810, 460, 300, 90), smallFont, Brush(colors.Muted));

    var loop = new[]
    {
        new Step("Minimize", "完整 / 最小 replay", colors.Amber),
        new Step("Headless", "input-state / input-logic", colors.Green),
        new Step("Compare", "hash / opcode / snapshot", colors.Purple),
        new Step("Regression", "固定用例 / gate", colors.Red)
    };
    for (var i = 0; i < loop.Length; i++)
    {
        var y = 220 + i * 145;
        RoundRect(g, new RectangleF(1280, y, 500, 100), 12, Brushes.White, Pen(loop[i].Color, 2));
        Text(g, loop[i].Title, new RectangleF(1305, y + 12, 190, 34), bodyFont, Brush(colors.Text), StringAlignment.Near);
        Text(g, loop[i].Description, new RectangleF(1500, y + 14, 245, 56), smallFont, Brush(colors.Muted), StringAlignment.Near);
        if (i < loop.Length - 1) Arrow(g, 1530, y + 104, 1530, y + 137, Pen("#94A3B8", 3));
    }
    Arrow(g, 1150, 485, 1265, 485, Pen(colors.DarkLine, 4));
});

SaveCanvas("20-battlehost-lifecycle.png", g =>
{
    Header(g, "Orleans BattleHost 与玩法适配", "Grain 统一生命周期和状态同步，玩法能力通过 runtime adapter 接入并独立声明成熟度");
    var hostSteps = new[] { "Initialize", "Schedule Input", "Server Tick", "Full / Delta Push", "Late Join", "Destroy" };
    var x = 105f;
    for (var i = 0; i < hostSteps.Length; i++)
    {
        RoundRect(g, new RectangleF(x, 220, 260, 90), 12, Brush(colors.Blue), null);
        Text(g, hostSteps[i], new RectangleF(x + 15, 240, 230, 42), bodyFont, Brushes.White);
        if (i < hostSteps.Length - 1) Arrow(g, x + 268, 265, x + 302, 265, Pen(colors.DarkLine, 3));
        x += 298;
    }
    Text(g, "BattleLogicHostGrain：宿主协议，不复制玩法系统", new RectangleF(480, 340, 960, 44), subTitleFont, Brush(colors.Text));

    var cols = new[] { "Adapter Capability", "Shooter", "MOBA" };
    var rows = new[]
    {
        ("Start / Input / Tick / Snapshot", "已实现", "已实现"),
        ("Dynamic Join", "已实现", "Unsupported"),
        ("Bot AI Mount", "已实现", "Unsupported"),
        ("Observer Interest / Pure State", "已实现", "边界未接入"),
        ("World Diagnostics", "已实现", "Unsupported")
    };
    const float x0 = 300f;
    const float y0 = 455f;
    var widths = new[] { 720f, 360f, 360f };
    var xx = x0;
    for (var c = 0; c < cols.Length; c++)
    {
        RoundRect(g, new RectangleF(xx, y0 - 65, widths[c] - 8, 55), 8, Brush("#334155"), null);
        Text(g, cols[c], new RectangleF(xx + 12, y0 - 55, widths[c] - 32, 34), bodyFont, Brushes.White);
        xx += widths[c];
    }
    for (var r = 0; r < rows.Length; r++)
    {
        var values = new[] { rows[r].Item1, rows[r].Item2, rows[r].Item3 };
        xx = x0;
        for (var c = 0; c < 3; c++)
        {
            var fill = c == 0 ? "#F1F5F9" : values[c] == "已实现" ? "#ECFDF5" : "#FFF7ED";
            var ink = c == 0 ? colors.Text : values[c] == "已实现" ? colors.Green : colors.Amber;
            RoundRect(g, new RectangleF(xx, y0 + r * 78, widths[c] - 8, 68), 8, Brush(fill), Pen("#CBD5E1", 1));
            Text(g, values[c], new RectangleF(xx + 12, y0 + r * 78 + 12, widths[c] - 32, 40), smallFont, Brush(ink));
            xx += widths[c];
        }
    }
});

SaveCanvas("21-config-validation-pipeline.png", g =>
{
    Header(g, "配置生产链必须区分资源、显式注册和项目校验", "旧 AutoPlanAction Source Generator 已 Retired，不能再作为活跃的通用生成能力");
    var columns = new[]
    {
        new Group("资源接入", colors.Blue, new[] { "IResourceProvider", "JsonConfigProvider", "Unity / pure C# 可替换" }),
        new Group("显式模块清单", colors.Cyan, new[] { "module manifest", "Action / Schema 显式注册", "生成产物只作历史兼容" }),
        new Group("运行时校验", colors.Green, new[] { "required validator contract", "startup block + history", "MOBA 参考实现" })
    };
    var xs = new[] { 120f, 700f, 1280f };
    for (var i = 0; i < columns.Length; i++)
    {
        var col = columns[i];
        RoundRect(g, new RectangleF(xs[i], 245, 520, 430), 16, Brushes.White, Pen(col.Color, 3));
        RoundRect(g, new RectangleF(xs[i], 245, 520, 70), 16, Brush(col.Color), null);
        Text(g, col.Title, new RectangleF(xs[i] + 22, 260, 476, 40), subTitleFont, Brushes.White);
        var y = 355f;
        foreach (var item in col.Items)
        {
            Text(g, "• " + item, new RectangleF(xs[i] + 35, y, 450, 56), bodyFont, Brush(colors.Text), StringAlignment.Near);
            y += 82;
        }
        if (i < 2) Arrow(g, xs[i] + 528, 460, xs[i] + 568, 460, Pen(colors.DarkLine, 3));
    }
    RoundRect(g, new RectangleF(165, 730, 1590, 100), 14, Brush("#FFF7ED"), Pen("#FDBA74", 2));
    Text(g, "成熟度边界：资源抽象是公共机制；显式 manifest 是当前方向；Runtime Validation 仍是 MOBA 参考实现。", new RectangleF(205, 752, 1510, 56), bodyFont, Brush(colors.Text));
});

SaveCanvas("22-gc-hot-path-governance.png", g =>
{
    Header(g, "GC / 性能热路径治理", "框架复用越多，越要把分配来源、热路径开关和回归验证做成工程纪律");
    var lanes = new[]
    {
        new Step("Find", "Profiler / tests\nallocation sample", colors.Blue),
        new Step("Classify", "log / boxing\narray copy / LINQ", colors.Cyan),
        new Step("Guard", "debug switch\nvalidation mode", colors.Green),
        new Step("Refactor", "pool / span\ncache / struct", colors.Amber),
        new Step("Benchmark", "stress case\nbaseline diff", colors.Purple),
        new Step("Gate", "threshold\nnightly report", colors.Red)
    };
    DrawHorizontalSteps(g, lanes, 105, 330, 275, 150, 298);
});

foreach (var spec in CodeVisualSpecs())
{
    SaveCanvas(spec.FileName, g => DrawCodeVisualSpec(g, spec));
}

WriteMermaidFiles();
WriteIndex();
Console.WriteLine($"Generated AbilityKit PPT assets in {outputDir}");

void SaveCanvas(string fileName, Action<Graphics> draw)
{
    using var bitmap = new Bitmap(1920, 1080);
    using var g = Graphics.FromImage(bitmap);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    g.Clear(ColorTranslator.FromHtml("#F7F9FC"));
    draw(g);
    bitmap.Save(Path.Combine(outputDir, fileName), ImageFormat.Png);
}

void Header(Graphics g, string title, string subtitle)
{
    Text(g, title, new RectangleF(80, 42, 1760, 60), titleFont, Brush(colors.Text), StringAlignment.Near);
    Text(g, subtitle, new RectangleF(82, 105, 1760, 38), smallFont, Brush(colors.Muted), StringAlignment.Near);
    using var pen = Pen("#CBD5E1", 2);
    g.DrawLine(pen, 80, 160, 1840, 160);
}

void RoundRect(Graphics g, RectangleF rect, float radius, Brush fill, Pen? stroke)
{
    using var path = new GraphicsPath();
    var d = radius * 2;
    path.AddArc(rect.X, rect.Y, d, d, 180, 90);
    path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
    path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
    path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
    path.CloseFigure();
    g.FillPath(fill, path);
    if (stroke != null) g.DrawPath(stroke, path);
}

void Text(Graphics g, string value, RectangleF rect, Font font, Brush brush, StringAlignment align = StringAlignment.Center, StringAlignment lineAlign = StringAlignment.Center)
{
    using var format = new StringFormat
    {
        Alignment = align,
        LineAlignment = lineAlign,
        Trimming = StringTrimming.EllipsisCharacter,
        FormatFlags = StringFormatFlags.LineLimit
    };
    g.DrawString(value, font, brush, rect, format);
}

void DrawCodeVisualSpec(Graphics g, CodeVisualSpec spec)
{
    switch (spec.Kind)
    {
        case CodeVisualKind.Title:
            DrawTitleVisual(g, spec);
            return;
        case CodeVisualKind.Cascade:
            DrawCascade(g, spec);
            return;
        case CodeVisualKind.RoleChain:
            DrawRoleChain(g, spec);
            return;
        case CodeVisualKind.FeedbackLoop:
            DrawFeedbackLoop(g, spec);
            return;
        case CodeVisualKind.FlatLifecycle:
            DrawFlatLifecycle(g, spec);
            return;
        case CodeVisualKind.Responsibility:
            DrawResponsibility(g, spec);
            return;
        case CodeVisualKind.SplitEvidence:
            DrawSplitEvidence(g, spec);
            return;
        case CodeVisualKind.ReviewContract:
            DrawReviewContract(g, spec);
            return;
        case CodeVisualKind.Funnel:
            DrawFunnel(g, spec);
            return;
        case CodeVisualKind.DecisionPath:
            DrawDecisionPath(g, spec);
            return;
        case CodeVisualKind.ProjectRole:
            DrawProjectRole(g, spec);
            return;
        case CodeVisualKind.OwnershipBands:
            DrawOwnershipBands(g, spec);
            return;
        case CodeVisualKind.ScenarioPaths:
            DrawScenarioPaths(g, spec);
            return;
        case CodeVisualKind.IssueRouting:
            DrawIssueRouting(g, spec);
            return;
        case CodeVisualKind.AdoptionStages:
            DrawAdoptionStages(g, spec);
            return;
        case CodeVisualKind.AdoptionCoordinates:
            DrawAdoptionCoordinates(g, spec);
            return;
        case CodeVisualKind.SkillExecutionRuntime:
            DrawSkillExecutionRuntime(g, spec);
            return;
        case CodeVisualKind.FinalizationGate:
            DrawFinalizationGate(g, spec);
            return;
        case CodeVisualKind.FactModelBridge:
            DrawFactModelBridge(g, spec);
            return;
        case CodeVisualKind.BuffOwnershipLifecycle:
            DrawBuffOwnershipLifecycle(g, spec);
            return;
        case CodeVisualKind.ProjectileBoundaryLifecycle:
            DrawProjectileBoundaryLifecycle(g, spec);
            return;
        case CodeVisualKind.SyncDecisionQuestions:
            DrawSyncDecisionQuestions(g, spec);
            return;
        case CodeVisualKind.PresentationProjection:
            DrawPresentationProjection(g, spec);
            return;
        case CodeVisualKind.CapabilityOperatingModel:
            DrawCapabilityOperatingModel(g, spec);
            return;
        case CodeVisualKind.ProblemCapabilityPaths:
            DrawProblemCapabilityPaths(g, spec);
            return;
    }

    Header(g, spec.Title, spec.Subtitle);
    switch (spec.Kind)
    {
        case CodeVisualKind.Sequence:
            DrawSequence(g, spec);
            break;
        case CodeVisualKind.Lifecycle:
            DrawLifecycle(g, spec);
            break;
        case CodeVisualKind.SplitFlow:
            DrawSplitFlow(g, spec);
            break;
        case CodeVisualKind.Matrix:
            DrawMatrix(g, spec);
            break;
        case CodeVisualKind.Stack:
            DrawStack(g, spec);
            break;
        default:
            DrawDataFlow(g, spec);
            break;
    }

    if (spec.Kind is not CodeVisualKind.Lifecycle and not CodeVisualKind.Stack)
    {
        var takeawayY = spec.Kind switch
        {
            CodeVisualKind.DataFlow => 720f,
            CodeVisualKind.SplitFlow => 785f,
            CodeVisualKind.Sequence => 815f,
            _ => 835f
        };
        RoundRect(g, new RectangleF(210, takeawayY, 1500, 82), 12, Brush("#EEF2F7"), Pen("#CBD5E1", 2));
        Text(g, spec.Takeaway, new RectangleF(250, takeawayY + 15, 1420, 50), bodyFont, Brush(colors.Text));
        Text(g, spec.Source, new RectangleF(230, takeawayY + 94, 1460, 30), smallFont, Brush(colors.Muted), StringAlignment.Near);
    }
}

void DrawDataFlow(Graphics g, CodeVisualSpec spec)
{
    using var codeFont = Font(17, FontStyle.Regular);
    var width = Math.Min(295f, (1580f - (spec.Items.Length - 1) * 34f) / spec.Items.Length);
    var pitch = width + 34f;
    var startX = (1920f - (spec.Items.Length * width + (spec.Items.Length - 1) * 34f)) / 2f;
    var y = 325f;
    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var x = startX + i * pitch;
        RoundRect(g, new RectangleF(x, y, width, 215), 14, Brushes.White, Pen("#CBD5E1", 2));
        RoundRect(g, new RectangleF(x, y, width, 56), 14, Brush(item.Color), null);
        Text(g, item.Title, new RectangleF(x + 12, y + 11, width - 24, 34), smallFont, Brushes.White);
        Text(g, item.Description, new RectangleF(x + 18, y + 72, width - 36, 78), smallFont, Brush(colors.Text));
        Text(g, item.Code, new RectangleF(x + 18, y + 158, width - 36, 44), codeFont, Brush(colors.Muted));
        if (i < spec.Items.Length - 1)
            Arrow(g, x + width + 6, y + 108, x + pitch - 8, y + 108, Pen(colors.DarkLine, 3));
    }
}

void DrawSequence(Graphics g, CodeVisualSpec spec)
{
    var x = 150f;
    var y = 235f;
    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        RoundRect(g, new RectangleF(x, y, 690, 96), 12, Brushes.White, Pen("#CBD5E1", 2));
        RoundRect(g, new RectangleF(x, y, 96, 96), 12, Brush(item.Color), null);
        Text(g, (i + 1).ToString(), new RectangleF(x, y + 20, 96, 46), checkFont, Brushes.White);
        Text(g, item.Title, new RectangleF(x + 120, y + 14, 520, 32), bodyFont, Brush(colors.Text), StringAlignment.Near);
        Text(g, item.Description, new RectangleF(x + 120, y + 48, 520, 28), smallFont, Brush(colors.Muted), StringAlignment.Near);
        Text(g, item.Code, new RectangleF(x + 120, y + 74, 520, 22), smallFont, Brush("#2563EB"), StringAlignment.Near);
        if (i < spec.Items.Length - 1 && i != 3)
            Arrow(g, x + 345, y + 101, x + 345, y + 132, Pen(colors.DarkLine, 3));
        if (i == 3)
        {
            using var connector = Pen(colors.DarkLine, 3);
            g.DrawLine(connector, x + 696, y + 48, 960, y + 48);
            g.DrawLine(connector, 960, y + 48, 960, 283);
            Arrow(g, 960, 283, 1072, 283, Pen(colors.DarkLine, 3));
            x = 1080f;
            y = 235f;
        }
        else
        {
            y += 132f;
        }
    }
}

void DrawLifecycle(Graphics g, CodeVisualSpec spec)
{
    var center = new PointF(960, 520);
    var radius = 300f;
    var points = new PointF[spec.Items.Length];
    for (var i = 0; i < spec.Items.Length; i++)
    {
        var angle = -MathF.PI / 2f + i * MathF.Tau / spec.Items.Length;
        points[i] = new PointF(center.X + radius * MathF.Cos(angle), center.Y + radius * MathF.Sin(angle));
    }

    for (var i = 0; i < spec.Items.Length; i++)
    {
        var next = points[(i + 1) % points.Length];
        Arrow(g, points[i].X, points[i].Y, next.X, next.Y, Pen("#94A3B8", 3));
    }

    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var p = points[i];
        RoundRect(g, new RectangleF(p.X - 150, p.Y - 70, 300, 140), 16, Brushes.White, Pen(item.Color, 3));
        Text(g, item.Title, new RectangleF(p.X - 130, p.Y - 50, 260, 34), bodyFont, Brush(colors.Text));
        Text(g, item.Description, new RectangleF(p.X - 130, p.Y - 12, 260, 44), smallFont, Brush(colors.Muted));
        Text(g, item.Code, new RectangleF(p.X - 130, p.Y + 36, 260, 24), smallFont, Brush(item.Color));
    }

    RoundRect(g, new RectangleF(810, 455, 300, 130), 18, Brush("#F8FAFC"), Pen("#CBD5E1", 2));
    Text(g, spec.CenterLabel, new RectangleF(830, 474, 260, 92), bodyFont, Brush(colors.Text));
}

void DrawSplitFlow(Graphics g, CodeVisualSpec spec)
{
    var left = spec.Items.Take((spec.Items.Length + 1) / 2).ToArray();
    var right = spec.Items.Skip(left.Length).ToArray();
    Text(g, spec.LeftLabel, new RectangleF(170, 205, 640, 44), subTitleFont, Brush(colors.Text));
    Text(g, spec.RightLabel, new RectangleF(1110, 205, 640, 44), subTitleFont, Brush(colors.Text));
    DrawColumn(g, left, 170, 270, 620);
    DrawColumn(g, right, 1110, 270, 620);
    Arrow(g, 815, 525, 1100, 525, Pen(colors.DarkLine, 4));
    if (!string.IsNullOrWhiteSpace(spec.CenterLabel))
        Text(g, spec.CenterLabel, new RectangleF(820, 470, 275, 42), smallFont, Brush(colors.Muted));
}

void DrawColumn(Graphics g, CodeVisualItem[] items, float x, float y, float width)
{
    for (var i = 0; i < items.Length; i++)
    {
        var item = items[i];
        RoundRect(g, new RectangleF(x, y, width, 112), 12, Brushes.White, Pen(item.Color, 2));
        Text(g, item.Title, new RectangleF(x + 24, y + 12, width - 48, 30), bodyFont, Brush(colors.Text), StringAlignment.Near);
        Text(g, item.Description, new RectangleF(x + 24, y + 43, width - 48, 38), smallFont, Brush(colors.Muted), StringAlignment.Near);
        Text(g, item.Code, new RectangleF(x + 24, y + 83, width - 48, 23), smallFont, Brush(item.Color), StringAlignment.Near);
        if (i < items.Length - 1)
            Arrow(g, x + width / 2, y + 117, x + width / 2, y + 142, Pen("#94A3B8", 3));
        y += 142;
    }
}

void DrawMatrix(Graphics g, CodeVisualSpec spec)
{
    var rows = spec.Items;
    var cols = spec.MatrixHeaders.Length == 4
        ? spec.MatrixHeaders
        : new[] { "对象", "职责", "代码入口", "边界" };
    const float x0 = 210f;
    const float y0 = 250f;
    const float cw = 380f;
    var ch = Math.Clamp(560f / rows.Length, 86f, 140f);
    RoundRect(g, new RectangleF(x0, y0 - 70, cw * cols.Length, 58), 10, Brush("#334155"), null);
    for (var c = 0; c < cols.Length; c++)
        Text(g, cols[c], new RectangleF(x0 + c * cw, y0 - 60, cw, 36), bodyFont, Brushes.White);

    for (var r = 0; r < rows.Length; r++)
    {
        var row = rows[r];
        var y = y0 + r * ch;
        var values = new[] { row.Title, row.Description, row.Code, row.Note };
        for (var c = 0; c < cols.Length; c++)
        {
            var fill = c == 0 ? Brush(row.Color) : Brushes.White;
            var brush = c == 0 ? Brushes.White : Brush(colors.Text);
            RoundRect(g, new RectangleF(x0 + c * cw, y, cw - 10, ch - 8), 8, fill, Pen("#CBD5E1", 1));
            Text(g, values[c], new RectangleF(x0 + c * cw + 14, y + 14, cw - 38, ch - 34), smallFont, brush);
        }
    }
}

void DrawStack(Graphics g, CodeVisualSpec spec)
{
    var x = 350f;
    var y = 245f;
    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var width = 1220f - i * 70f;
        var xx = x + i * 35f;
        RoundRect(g, new RectangleF(xx, y, width, 86), 12, Brush(item.Color), null);
        Text(g, item.Title, new RectangleF(xx + 28, y + 12, 360, 32), bodyFont, Brushes.White, StringAlignment.Near);
        Text(g, item.Description, new RectangleF(xx + 410, y + 16, width - 440, 28), smallFont, Brushes.White, StringAlignment.Near);
        Text(g, item.Code, new RectangleF(xx + 410, y + 48, width - 440, 22), smallFont, Brushes.White, StringAlignment.Near);
        y += 96f;
    }
}

void DrawTitleVisual(Graphics g, CodeVisualSpec spec)
{
    using var deckFont = Font(88, FontStyle.Bold);
    using var leadFont = Font(34, FontStyle.Regular);
    using var metaFont = Font(24, FontStyle.Regular);

    Text(g, spec.Title, new RectangleF(110, 165, 1700, 130), deckFont, Brush(colors.Text), StringAlignment.Near);
    Text(g, spec.Subtitle, new RectangleF(115, 310, 1500, 70), leadFont, Brush(colors.Muted), StringAlignment.Near);
    using (var accent = Pen(colors.Blue, 8))
        g.DrawLine(accent, 115, 415, 760, 415);

    Text(g, spec.Takeaway, new RectangleF(115, 465, 1560, 90), subTitleFont, Brush(colors.Text), StringAlignment.Near);

    var y = 735f;
    var startX = 190f;
    var pitch = 365f;
    using (var rail = Pen("#CBD5E1", 5))
        g.DrawLine(rail, startX, y, startX + pitch * (spec.Items.Length - 1), y);
    for (var i = 0; i < spec.Items.Length; i++)
    {
        var x = startX + i * pitch;
        using var brush = Brush(spec.Items[i].Color);
        g.FillEllipse(brush, x - 18, y - 18, 36, 36);
        Text(g, spec.Items[i].Title, new RectangleF(x - 110, y + 36, 220, 42), bodyFont, Brush(colors.Text));
    }

    Text(g, spec.Source, new RectangleF(115, 965, 1600, 38), metaFont, Brush(colors.Muted), StringAlignment.Near);
}

void DrawCascade(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    var points = new[]
    {
        new PointF(245, 690), new PointF(575, 610), new PointF(915, 510),
        new PointF(1260, 390), new PointF(1600, 245)
    };
    for (var i = 0; i < points.Length - 1; i++)
        Arrow(g, points[i].X + 125, points[i].Y, points[i + 1].X - 135, points[i + 1].Y, Pen("#64748B", 4));

    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var p = points[i];
        var height = 118f + i * 12f;
        RoundRect(g, new RectangleF(p.X - 135, p.Y - height / 2, 270, height), 14, i < 2 ? Brush("#FFF7ED") : Brush("#FEF2F2"), Pen(item.Color, 3));
        Text(g, item.Title, new RectangleF(p.X - 112, p.Y - 40, 224, 34), bodyFont, Brush(colors.Text));
        Text(g, item.Description, new RectangleF(p.X - 112, p.Y + 2, 224, 50), smallFont, Brush(colors.Muted));
    }

    Text(g, "成本与风险持续放大", new RectangleF(1210, 655, 430, 52), subTitleFont, Brush(colors.Red));
    FooterBand(g, spec, 825);
}

void DrawRoleChain(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    var startX = 220f;
    var pitch = 370f;
    var railY = 505f;
    using (var rail = Pen("#94A3B8", 5))
        g.DrawLine(rail, startX, railY, startX + pitch * (spec.Items.Length - 1), railY);

    for (var i = 0; i < spec.Items.Length - 1; i++)
        Arrow(g, startX + i * pitch + 32, railY, startX + (i + 1) * pitch - 34, railY, Pen("#94A3B8", 3));

    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var x = startX + i * pitch;
        using var nodeBrush = Brush(item.Color);
        g.FillEllipse(nodeBrush, x - 34, railY - 34, 68, 68);
        Text(g, (i + 1).ToString(), new RectangleF(x - 34, railY - 25, 68, 48), subTitleFont, Brushes.White);
        var above = i % 2 == 0;
        var titleY = above ? 270f : 590f;
        Text(g, item.Title, new RectangleF(x - 145, titleY, 290, 42), subTitleFont, Brush(colors.Text));
        Text(g, item.Description, new RectangleF(x - 150, titleY + 52, 300, 68), smallFont, Brush(colors.Muted));
        Text(g, item.Code, new RectangleF(x - 150, titleY + 122, 300, 34), smallFont, Brush(item.Color));
    }

    FooterBand(g, spec, 835);
}

void DrawFeedbackLoop(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    var points = new[]
    {
        new PointF(960, 245), new PointF(1450, 420), new PointF(1270, 735),
        new PointF(650, 735), new PointF(470, 420)
    };
    var rects = points
        .Select(point => new RectangleF(point.X - 150, point.Y - 58, 300, 116))
        .ToArray();
    for (var i = 0; i < points.Length; i++)
    {
        var nextIndex = (i + 1) % points.Length;
        var start = RectangleBoundaryPoint(rects[i], points[nextIndex]);
        var end = RectangleBoundaryPoint(rects[nextIndex], points[i]);
        Arrow(g, start.X, start.Y, end.X, end.Y, Pen("#64748B", 4));
    }

    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var p = points[i];
        RoundRect(g, rects[i], 14, Brushes.White, Pen(item.Color, 3));
        Text(g, item.Title, new RectangleF(p.X - 130, p.Y - 40, 260, 34), bodyFont, Brush(colors.Text));
        Text(g, item.Description, new RectangleF(p.X - 130, p.Y + 4, 260, 38), smallFont, Brush(colors.Muted));
    }

    using var centerFont = Font(31, FontStyle.Bold);
    Text(g, spec.CenterLabel, new RectangleF(730, 430, 460, 145), centerFont, Brush(colors.Green));
    Text(g, spec.Takeaway, new RectangleF(300, 895, 1320, 48), bodyFont, Brush(colors.Text));
    Text(g, spec.Source, new RectangleF(230, 958, 1460, 30), smallFont, Brush(colors.Muted), StringAlignment.Near);
}

void DrawFlatLifecycle(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    var xs = new[] { 260f, 720f, 1180f, 1640f };
    var y = 470f;
    for (var i = 0; i < xs.Length - 1; i++)
        Arrow(g, xs[i] + 110, y, xs[i + 1] - 115, y, Pen("#64748B", 4));

    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var x = xs[i];
        using var brush = Brush(item.Color);
        g.FillEllipse(brush, x - 74, y - 74, 148, 148);
        Text(g, (i + 1).ToString(), new RectangleF(x - 74, y - 39, 148, 70), checkFont, Brushes.White);
        Text(g, item.Title, new RectangleF(x - 170, y + 105, 340, 42), subTitleFont, Brush(colors.Text));
        Text(g, item.Description, new RectangleF(x - 175, y + 154, 350, 62), smallFont, Brush(colors.Muted));
    }

    RoundRect(g, new RectangleF(255, 790, 1410, 88), 12, Brush("#EEF6FF"), Pen("#BFDBFE", 2));
    Text(g, spec.Takeaway, new RectangleF(300, 808, 1320, 50), bodyFont, Brush(colors.Text));
    Text(g, spec.Source, new RectangleF(255, 915, 1410, 30), smallFont, Brush(colors.Muted), StringAlignment.Near);
}

void DrawResponsibility(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    var y = 245f;
    for (var i = 0; i < spec.Items.Length - 1; i++)
        Arrow(g, 960, y + i * 190 + 142, 960, y + (i + 1) * 190 - 8, Pen("#64748B", 4));

    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var rowY = y + i * 190;
        RoundRect(g, new RectangleF(260, rowY, 1400, 142), 16, Brushes.White, Pen(item.Color, 3));
        RoundRect(g, new RectangleF(260, rowY, 300, 142), 16, Brush(item.Color), null);
        Text(g, item.Title, new RectangleF(285, rowY + 42, 250, 52), subTitleFont, Brushes.White);
        Text(g, item.Description, new RectangleF(620, rowY + 26, 570, 90), bodyFont, Brush(colors.Text), StringAlignment.Near);
        Text(g, item.Code, new RectangleF(1215, rowY + 35, 390, 72), smallFont, Brush(item.Color));
    }

    FooterBand(g, spec, 850);
}

void DrawSplitEvidence(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    using (var divider = Pen("#CBD5E1", 3))
        g.DrawLine(divider, 960, 220, 960, 810);
    Text(g, spec.LeftLabel, new RectangleF(150, 205, 700, 52), subTitleFont, Brush(colors.Amber));
    Text(g, spec.RightLabel, new RectangleF(1070, 205, 700, 52), subTitleFont, Brush(colors.Blue));

    var left = spec.Items.Take(3).ToArray();
    var right = spec.Items.Skip(3).Take(3).ToArray();
    DrawEvidencePath(g, left, 205, 320, 620);
    DrawEvidencePath(g, right, 1125, 320, 620);
    FooterBand(g, spec, 835);
}

void DrawEvidencePath(Graphics g, CodeVisualItem[] items, float x, float y, float width)
{
    for (var i = 0; i < items.Length - 1; i++)
        Arrow(g, x + width / 2, y + i * 150 + 105, x + width / 2, y + (i + 1) * 150 - 8, Pen("#94A3B8", 3));
    for (var i = 0; i < items.Length; i++)
    {
        var item = items[i];
        var rowY = y + i * 150;
        using var marker = Brush(item.Color);
        g.FillEllipse(marker, x, rowY + 28, 54, 54);
        Text(g, (i + 1).ToString(), new RectangleF(x, rowY + 36, 54, 36), bodyFont, Brushes.White);
        Text(g, item.Title, new RectangleF(x + 85, rowY + 12, width - 95, 38), bodyFont, Brush(colors.Text), StringAlignment.Near);
        Text(g, item.Description, new RectangleF(x + 85, rowY + 50, width - 95, 58), smallFont, Brush(colors.Muted), StringAlignment.Near);
    }
}

void DrawReviewContract(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    var rects = new[]
    {
        new RectangleF(180, 245, 600, 205), new RectangleF(1140, 245, 600, 205),
        new RectangleF(180, 585, 600, 205), new RectangleF(1140, 585, 600, 205)
    };
    var center = new PointF(960, 520);
    foreach (var rect in rects)
        Arrow(g, rect.X + rect.Width / 2, rect.Y + rect.Height / 2, center.X, center.Y, Pen("#94A3B8", 3));

    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var rect = rects[i];
        Text(g, item.Title, new RectangleF(rect.X, rect.Y + 8, rect.Width, 46), subTitleFont, Brush(item.Color));
        Text(g, item.Description, new RectangleF(rect.X + 35, rect.Y + 67, rect.Width - 70, 60), bodyFont, Brush(colors.Text));
        Text(g, item.Code, new RectangleF(rect.X + 35, rect.Y + 137, rect.Width - 70, 42), smallFont, Brush(colors.Muted));
        using var line = Pen(item.Color, 4);
        g.DrawLine(line, rect.X + 70, rect.Bottom - 10, rect.Right - 70, rect.Bottom - 10);
    }

    using var centerBrush = Brush(colors.DarkLine);
    g.FillEllipse(centerBrush, center.X - 118, center.Y - 118, 236, 236);
    Text(g, spec.CenterLabel, new RectangleF(center.X - 95, center.Y - 70, 190, 140), subTitleFont, Brushes.White);
    Text(g, spec.Takeaway, new RectangleF(330, 860, 1260, 48), bodyFont, Brush(colors.Text));
    Text(g, spec.Source, new RectangleF(230, 940, 1460, 30), smallFont, Brush(colors.Muted), StringAlignment.Near);
}

void DrawFunnel(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    var topY = 245f;
    var heights = 125f;
    var widths = new[] { 1420f, 1160f, 900f, 640f };
    for (var i = 0; i < spec.Items.Length; i++)
    {
        var width = widths[i];
        var x = (1920f - width) / 2f;
        var nextWidth = i == spec.Items.Length - 1 ? 410f : widths[i + 1];
        var nextX = (1920f - nextWidth) / 2f;
        var y = topY + i * 135f;
        var polygon = new[]
        {
            new PointF(x, y), new PointF(x + width, y),
            new PointF(nextX + nextWidth, y + heights), new PointF(nextX, y + heights)
        };
        using var brush = Brush(spec.Items[i].Color);
        g.FillPolygon(brush, polygon);
        Text(g, spec.Items[i].Title, new RectangleF(x + 70, y + 20, width * 0.36f, 42), subTitleFont, Brushes.White, StringAlignment.Near);
        Text(g, spec.Items[i].Description, new RectangleF(x + width * 0.40f, y + 20, width * 0.50f, 60), bodyFont, Brushes.White, StringAlignment.Near);
    }

    RoundRect(g, new RectangleF(755, 810, 410, 86), 12, Brush(colors.DarkLine), null);
    Text(g, spec.CenterLabel, new RectangleF(785, 828, 350, 50), bodyFont, Brushes.White);
    Text(g, spec.Source, new RectangleF(230, 940, 1460, 30), smallFont, Brush(colors.Muted), StringAlignment.Near);
}

void DrawDecisionPath(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    var y = 330f;
    var xs = new[] { 225f, 610f, 995f, 1380f };
    for (var i = 0; i < xs.Length - 1; i++)
        Arrow(g, xs[i] + 135, y, xs[i + 1] - 145, y, Pen("#64748B", 4));
    Arrow(g, 1490, y + 70, 1060, 620, Pen("#64748B", 4));

    Arrow(g, 875, 721, 575, 830, Pen("#64748B", 4));
    Arrow(g, 960, 765, 960, 830, Pen("#64748B", 4));
    Arrow(g, 1045, 721, 1345, 830, Pen("#64748B", 4));

    for (var i = 0; i < 4; i++)
    {
        var item = spec.Items[i];
        RoundRect(g, new RectangleF(xs[i] - 135, y - 70, 270, 140), 14, Brushes.White, Pen(item.Color, 3));
        Text(g, item.Title, new RectangleF(xs[i] - 115, y - 48, 230, 36), bodyFont, Brush(colors.Text));
        Text(g, item.Description, new RectangleF(xs[i] - 115, y - 6, 230, 54), smallFont, Brush(colors.Muted));
    }

    var diamond = new[]
    {
        new PointF(960, 585), new PointF(1125, 675),
        new PointF(960, 765), new PointF(795, 675)
    };
    using (var decisionBrush = Brush(colors.DarkLine))
        g.FillPolygon(decisionBrush, diamond);
    Text(g, spec.CenterLabel, new RectangleF(835, 635, 250, 80), subTitleFont, Brushes.White);

    var decisions = new[] { ("继续采用", colors.Green, 450f), ("调整边界", colors.Amber, 835f), ("回滚退出", colors.Red, 1220f) };
    foreach (var decision in decisions)
    {
        RoundRect(g, new RectangleF(decision.Item3, 840, 250, 72), 10, Brush(decision.Item2), null);
        Text(g, decision.Item1, new RectangleF(decision.Item3 + 20, 855, 210, 42), bodyFont, Brushes.White);
    }
    Text(g, spec.Source, new RectangleF(230, 965, 1460, 30), smallFont, Brush(colors.Muted), StringAlignment.Near);
}

void DrawProjectRole(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    Text(g, spec.LeftLabel, new RectangleF(105, 220, 300, 44), subTitleFont, Brush(colors.Amber));
    Text(g, "AbilityKit 稳定工程链", new RectangleF(535, 220, 850, 44), subTitleFont, Brush(colors.Blue));
    Text(g, spec.RightLabel, new RectangleF(1505, 220, 300, 44), subTitleFont, Brush(colors.Green));

    var inputLabels = new[] { "英雄与技能规则", "房间与网络约束", "体验、预算与平台" };
    var outputLabels = new[] { "Unity 客户端", "服务端运行时", "测试与 Artifact" };
    var ys = new[] { 350f, 490f, 630f };
    using (var collector = Pen("#94A3B8", 3))
    {
        g.DrawLine(collector, 445, ys[0], 445, ys[^1]);
        g.DrawLine(collector, 1475, ys[0], 1475, ys[^1]);
        foreach (var y in ys)
        {
            g.DrawLine(collector, 410, y, 445, y);
            g.DrawLine(collector, 1475, y, 1500, y);
        }
    }
    for (var i = 0; i < ys.Length; i++)
    {
        using var inputBrush = Brush(i == 0 ? colors.Amber : colors.DarkLine);
        g.FillEllipse(inputBrush, 120, ys[i] - 12, 24, 24);
        Text(g, inputLabels[i], new RectangleF(165, ys[i] - 28, 260, 56), bodyFont, Brush(colors.Text), StringAlignment.Near);

        using var outputBrush = Brush(i == 2 ? colors.Green : colors.DarkLine);
        g.FillEllipse(outputBrush, 1515, ys[i] - 12, 24, 24);
        Text(g, outputLabels[i], new RectangleF(1560, ys[i] - 28, 260, 56), bodyFont, Brush(colors.Text), StringAlignment.Near);
    }

    Arrow(g, 445, 490, 505, 490, Pen("#64748B", 4));
    Arrow(g, 1415, 490, 1475, 490, Pen("#64748B", 4));

    const float startX = 510f;
    const float stageY = 320f;
    const float stageWidth = 210f;
    const float stageHeight = 220f;
    const float pitch = 235f;
    for (var i = 0; i < spec.Items.Length - 1; i++)
        Arrow(g, startX + i * pitch + stageWidth, stageY + stageHeight / 2, startX + (i + 1) * pitch - 10, stageY + stageHeight / 2, Pen("#64748B", 4));

    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var x = startX + i * pitch;
        RoundRect(g, new RectangleF(x, stageY, stageWidth, stageHeight), 14, Brushes.White, Pen(item.Color, 3));
        using var markerBrush = Brush(item.Color);
        g.FillEllipse(markerBrush, x + 77, stageY + 20, 56, 56);
        Text(g, (i + 1).ToString(), new RectangleF(x + 77, stageY + 27, 56, 38), bodyFont, Brushes.White);
        Text(g, item.Title, new RectangleF(x + 18, stageY + 88, stageWidth - 36, 38), bodyFont, Brush(colors.Text));
        Text(g, item.Description, new RectangleF(x + 16, stageY + 132, stageWidth - 32, 52), smallFont, Brush(colors.Muted));
        using var codeFont = Font(16, FontStyle.Regular);
        Text(g, item.Code, new RectangleF(x + 12, stageY + 188, stageWidth - 24, 25), codeFont, Brush(item.Color));
    }

    RoundRect(g, new RectangleF(520, 610, 870, 112), 14, Brush("#EEF2F7"), Pen("#CBD5E1", 2));
    Text(g, spec.CenterLabel, new RectangleF(555, 630, 800, 72), bodyFont, Brush(colors.Text));
    FooterBand(g, spec, 820);
}

void DrawOwnershipBands(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);

    Arrow(g, 560, 640, 560, 610, Pen("#64748B", 4));
    Arrow(g, 700, 425, 700, 390, Pen("#64748B", 4));
    Arrow(g, 1360, 390, 1360, 425, Pen("#94A3B8", 3));
    Arrow(g, 1500, 610, 1500, 640, Pen("#94A3B8", 3));

    var bands = new[]
    {
        new RectangleF(520, 225, 880, 155),
        new RectangleF(400, 435, 1120, 165),
        new RectangleF(280, 650, 1360, 165)
    };
    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var band = bands[i];
        RoundRect(g, band, 16, Brush(item.Color), null);
        Text(g, item.Title, new RectangleF(band.X + 35, band.Y + 28, 260, 44), subTitleFont, Brushes.White, StringAlignment.Near);
        Text(g, item.Description, new RectangleF(band.X + 315, band.Y + 22, band.Width - 350, 58), bodyFont, Brushes.White, StringAlignment.Near);
        Text(g, item.Code, new RectangleF(band.X + 315, band.Y + 84, band.Width - 350, 48), smallFont, Brushes.White, StringAlignment.Near);
    }

    Text(g, "稳定能力", new RectangleF(500, 392, 180, 32), smallFont, Brush(colors.Muted));
    Text(g, "配置 / Adapter", new RectangleF(1270, 392, 210, 32), smallFont, Brush(colors.Muted));
    Text(g, "稳定契约", new RectangleF(360, 612, 180, 32), smallFont, Brush(colors.Muted));
    Text(g, "缺陷回流", new RectangleF(1390, 612, 180, 32), smallFont, Brush(colors.Muted));
    FooterBand(g, spec, 845);
}

void DrawScenarioPaths(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    Text(g, "项目场景", new RectangleF(105, 205, 300, 42), bodyFont, Brush(colors.Muted));
    Text(g, "能力组合", new RectangleF(480, 205, 400, 42), bodyFont, Brush(colors.Muted));
    Text(g, "解决的主要问题", new RectangleF(930, 205, 430, 42), bodyFont, Brush(colors.Muted));
    Text(g, "最低证据", new RectangleF(1470, 205, 280, 42), bodyFont, Brush(colors.Muted));

    var rowYs = new[] { 315f, 515f, 715f };
    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var y = rowYs[i];
        Arrow(g, 340, y, 455, y, Pen("#64748B", 4));
        Arrow(g, 850, y, 930, y, Pen("#64748B", 4));
        Arrow(g, 1360, y, 1455, y, Pen("#64748B", 4));

        using var scenarioBrush = Brush(item.Color);
        g.FillEllipse(scenarioBrush, 120, y - 70, 140, 140);
        Text(g, item.Title, new RectangleF(135, y - 42, 110, 84), bodyFont, Brushes.White);

        RoundRect(g, new RectangleF(465, y - 62, 375, 124), 14, Brushes.White, Pen(item.Color, 3));
        Text(g, item.Description, new RectangleF(490, y - 34, 325, 68), bodyFont, Brush(colors.Text));

        using (var line = Pen(item.Color, 5))
            g.DrawLine(line, 960, y - 46, 960, y + 46);
        Text(g, item.Code, new RectangleF(995, y - 48, 340, 96), bodyFont, Brush(colors.Text), StringAlignment.Near);

        RoundRect(g, new RectangleF(1470, y - 48, 280, 96), 12, Brush("#EEF2F7"), Pen(item.Color, 2));
        Text(g, item.Note, new RectangleF(1490, y - 24, 240, 48), smallFont, Brush(item.Color));
    }
    FooterBand(g, spec, 850);
}

void DrawIssueRouting(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    Text(g, "项目现象", new RectangleF(130, 205, 300, 42), bodyFont, Brush(colors.Muted));
    Text(g, "能力边界", new RectangleF(570, 205, 300, 42), bodyFont, Brush(colors.Muted));
    Text(g, "责任人", new RectangleF(1025, 205, 280, 42), bodyFont, Brush(colors.Muted));
    Text(g, "可执行证据", new RectangleF(1430, 205, 300, 42), bodyFont, Brush(colors.Muted));

    var rowYs = new[] { 300f, 445f, 590f, 735f };
    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var y = rowYs[i];
        using (var separator = Pen("#E2E8F0", 2))
            g.DrawLine(separator, 120, y + 64, 1780, y + 64);
        Arrow(g, 420, y, 555, y, Pen("#64748B", 4));
        Arrow(g, 840, y, 1000, y, Pen("#64748B", 4));
        Arrow(g, 1290, y, 1410, y, Pen("#64748B", 4));

        Text(g, item.Title, new RectangleF(130, y - 32, 280, 64), bodyFont, Brush(colors.Text), StringAlignment.Near);
        RoundRect(g, new RectangleF(565, y - 48, 265, 96), 48, Brush(item.Color), null);
        Text(g, item.Description, new RectangleF(585, y - 24, 225, 48), bodyFont, Brushes.White);
        Text(g, item.Note, new RectangleF(1020, y - 30, 250, 60), bodyFont, Brush(colors.Text));
        Text(g, item.Code, new RectangleF(1435, y - 34, 300, 68), smallFont, Brush(item.Color));
    }
    FooterBand(g, spec, 850);
}

void DrawAdoptionStages(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    Arrow(g, 105, 800, 105, 250, Pen("#64748B", 4));
    Text(g, "接入风险与证据要求同步提高", new RectangleF(140, 205, 520, 44), bodyFont, Brush(colors.Muted), StringAlignment.Near);

    var blocks = new[]
    {
        new RectangleF(180, 580, 430, 230),
        new RectangleF(735, 420, 430, 230),
        new RectangleF(1290, 260, 430, 230)
    };
    Arrow(g, 620, 650, 725, 535, Pen("#64748B", 4));
    Arrow(g, 1175, 490, 1280, 375, Pen("#64748B", 4));

    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var block = blocks[i];
        RoundRect(g, block, 16, Brushes.White, Pen(item.Color, 3));
        RoundRect(g, new RectangleF(block.X, block.Y, block.Width, 62), 16, Brush(item.Color), null);
        Text(g, item.Title, new RectangleF(block.X + 22, block.Y + 13, block.Width - 44, 38), bodyFont, Brushes.White);
        Text(g, item.Description, new RectangleF(block.X + 28, block.Y + 82, block.Width - 56, 42), bodyFont, Brush(colors.Text));
        Text(g, item.Code, new RectangleF(block.X + 28, block.Y + 132, block.Width - 56, 34), smallFont, Brush(item.Color));
        Text(g, item.Note, new RectangleF(block.X + 28, block.Y + 176, block.Width - 56, 34), smallFont, Brush(colors.Muted));
    }
    FooterBand(g, spec, 850);
}

void DrawAdoptionCoordinates(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    var center = new RectangleF(760, 375, 400, 250);
    var lenses = new[]
    {
        new RectangleF(145, 235, 505, 225),
        new RectangleF(1270, 235, 505, 225),
        new RectangleF(145, 555, 505, 225),
        new RectangleF(1270, 555, 505, 225)
    };
    var centerPoint = new PointF(center.X + center.Width / 2, center.Y + center.Height / 2);

    foreach (var lens in lenses)
    {
        var lensCenter = new PointF(lens.X + lens.Width / 2, lens.Y + lens.Height / 2);
        var start = RectangleBoundaryPoint(lens, centerPoint);
        var end = RectangleBoundaryPoint(center, lensCenter);
        Arrow(g, start.X, start.Y, end.X, end.Y, Pen("#64748B", 4));
    }

    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var rect = lenses[i];
        RoundRect(g, rect, 16, Brushes.White, Pen(item.Color, 3));
        RoundRect(g, new RectangleF(rect.X, rect.Y, rect.Width, 62), 16, Brush(item.Color), null);
        Text(g, item.Title, new RectangleF(rect.X + 22, rect.Y + 13, rect.Width - 44, 38), bodyFont, Brushes.White);
        Text(g, item.Note, new RectangleF(rect.X + 28, rect.Y + 80, rect.Width - 56, 40), bodyFont, Brush(colors.Text));
        Text(g, item.Description, new RectangleF(rect.X + 28, rect.Y + 124, rect.Width - 56, 54), smallFont, Brush(colors.Muted));
        Text(g, item.Code, new RectangleF(rect.X + 28, rect.Y + 184, rect.Width - 56, 26), smallFont, Brush(item.Color));
    }

    RoundRect(g, center, 20, Brush("#172033"), null);
    Text(g, spec.CenterLabel, new RectangleF(center.X + 35, center.Y + 34, center.Width - 70, 88), subTitleFont, Brushes.White);
    Text(g, "四项同时明确\n才形成可执行、可退出的采用结论", new RectangleF(center.X + 40, center.Y + 132, center.Width - 80, 76), smallFont, Brush("#E2E8F0"));
    FooterBand(g, spec, 845);
}

void DrawSkillExecutionRuntime(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    var nodes = Enumerable.Range(0, spec.Items.Length)
        .Select(i => new RectangleF(95 + i * 365, 375, 270, 170))
        .ToArray();
    var pause = new RectangleF(810, 215, 300, 105);
    var failure = new RectangleF(100, 620, 330, 105);

    for (var i = 0; i < nodes.Length - 1; i++)
        Arrow(g, nodes[i].Right + 8, nodes[i].Y + nodes[i].Height / 2, nodes[i + 1].Left - 10, nodes[i + 1].Y + nodes[i + 1].Height / 2, Pen("#64748B", 4));
    Arrow(g, nodes[2].X + nodes[2].Width / 2, nodes[2].Top - 8, pause.X + pause.Width / 2, pause.Bottom + 8, Pen(colors.Amber, 3));
    Arrow(g, nodes[0].X + nodes[0].Width / 2, nodes[0].Bottom + 8, failure.X + failure.Width / 2, failure.Top - 8, Pen(colors.Red, 3));

    using (var tracePen = Pen(colors.Purple, 3))
    {
        tracePen.DashStyle = DashStyle.Dash;
        g.DrawLine(tracePen, 500, 720, 1755, 720);
        foreach (var node in nodes.Skip(1))
            g.DrawLine(tracePen, node.X + node.Width / 2, node.Bottom + 8, node.X + node.Width / 2, 720);
    }

    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var rect = nodes[i];
        RoundRect(g, rect, 16, Brushes.White, Pen(item.Color, 3));
        RoundRect(g, new RectangleF(rect.X, rect.Y, rect.Width, 56), 16, Brush(item.Color), null);
        Text(g, item.Title, new RectangleF(rect.X + 18, rect.Y + 11, rect.Width - 36, 34), bodyFont, Brushes.White);
        Text(g, item.Description, new RectangleF(rect.X + 20, rect.Y + 68, rect.Width - 40, 62), smallFont, Brush(colors.Text));
        Text(g, item.Code, new RectangleF(rect.X + 20, rect.Y + 137, rect.Width - 40, 24), smallFont, Brush(item.Color));
    }

    RoundRect(g, pause, 14, Brush("#FFF7ED"), Pen(colors.Amber, 2));
    Text(g, "暂停 / 恢复", new RectangleF(pause.X + 20, pause.Y + 14, pause.Width - 40, 36), bodyFont, Brush(colors.Text));
    Text(g, "Runner 保留 Runtime 状态", new RectangleF(pause.X + 20, pause.Y + 55, pause.Width - 40, 30), smallFont, Brush(colors.Muted));
    RoundRect(g, failure, 14, Brush("#FEF2F2"), Pen(colors.Red, 2));
    Text(g, "结构化失败出口", new RectangleF(failure.X + 20, failure.Y + 14, failure.Width - 40, 36), bodyFont, Brush(colors.Text));
    Text(g, "校验失败，不创建 Runtime", new RectangleF(failure.X + 20, failure.Y + 55, failure.Width - 40, 30), smallFont, Brush(colors.Muted));
    Text(g, "Trace root -> child lineage -> 输出证据", new RectangleF(760, 690, 760, 38), smallFont, Brush(colors.Purple));
    FooterBand(g, spec, 835);
}

void DrawFinalizationGate(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    var running = new RectangleF(130, 390, 300, 150);
    var ended = new RectangleF(540, 390, 300, 150);
    var decisionCenter = new PointF(1050, 465);
    var waiting = new RectangleF(865, 650, 370, 130);
    var finalized = new RectangleF(1430, 390, 340, 150);

    Arrow(g, running.Right + 8, 465, ended.Left - 10, 465, Pen("#64748B", 4));
    Arrow(g, ended.Right + 8, 465, decisionCenter.X - 112, 465, Pen("#64748B", 4));
    Arrow(g, decisionCenter.X + 112, 465, finalized.Left - 10, 465, Pen(colors.Green, 4));
    Arrow(g, 985, 525, waiting.Left + 95, waiting.Top - 8, Pen(colors.Cyan, 4));
    Arrow(g, waiting.Right - 95, waiting.Top - 8, 1115, 525, Pen(colors.Purple, 4));

    var decisionPath = new GraphicsPath();
    decisionPath.AddPolygon(new[]
    {
        new PointF(decisionCenter.X, decisionCenter.Y - 76),
        new PointF(decisionCenter.X + 112, decisionCenter.Y),
        new PointF(decisionCenter.X, decisionCenter.Y + 76),
        new PointF(decisionCenter.X - 112, decisionCenter.Y)
    });

    var itemRects = new[] { running, ended, waiting, finalized };
    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var rect = itemRects[i];
        RoundRect(g, rect, 16, Brushes.White, Pen(item.Color, 3));
        Text(g, item.Title, new RectangleF(rect.X + 22, rect.Y + 18, rect.Width - 44, 36), bodyFont, Brush(colors.Text));
        Text(g, item.Description, new RectangleF(rect.X + 22, rect.Y + 59, rect.Width - 44, 38), smallFont, Brush(colors.Muted));
        var codeY = i == 2 ? rect.Y + 98 : rect.Y + 105;
        Text(g, item.Code, new RectangleF(rect.X + 22, codeY, rect.Width - 44, 24), smallFont, Brush(item.Color));
    }

    g.FillPath(Brush("#FFF7ED"), decisionPath);
    g.DrawPath(Pen(colors.Amber, 3), decisionPath);
    Text(g, "PendingChildren\n是否为 0？", new RectangleF(948, 420, 204, 88), bodyFont, Brush(colors.Text));
    Text(g, "是：Owner 统一释放", new RectangleF(1190, 408, 240, 36), smallFont, Brush(colors.Green));
    Text(g, "否：继续持有", new RectangleF(785, 555, 220, 34), smallFont, Brush(colors.Cyan));
    Text(g, "子行为释放后再次 TryFinalize", new RectangleF(1070, 595, 360, 34), smallFont, Brush(colors.Purple));
    Text(g, "TryFinalize 是终结闸门", new RectangleF(880, 250, 340, 45), subTitleFont, Brush(colors.Amber));
    decisionPath.Dispose();
    FooterBand(g, spec, 845);
}

void DrawFactModelBridge(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    var hub = new RectangleF(765, 210, 390, 100);
    var nodes = new[]
    {
        new RectangleF(155, 390, 460, 240),
        new RectangleF(730, 390, 460, 240),
        new RectangleF(1305, 390, 460, 240)
    };
    var hubCenter = new PointF(hub.X + hub.Width / 2, hub.Y + hub.Height / 2);
    foreach (var node in nodes)
    {
        var target = new PointF(node.X + node.Width / 2, node.Y + node.Height / 2);
        var start = RectangleBoundaryPoint(hub, target);
        Arrow(g, start.X, start.Y, target.X, node.Top - 10, Pen("#64748B", 4));
    }

    RoundRect(g, hub, 16, Brush("#172033"), null);
    Text(g, "领域服务显式桥接稳定身份", new RectangleF(hub.X + 25, hub.Y + 18, hub.Width - 50, 38), bodyFont, Brushes.White);
    Text(g, "共享 ID，不共享生命周期", new RectangleF(hub.X + 25, hub.Y + 57, hub.Width - 50, 26), smallFont, Brush("#CBD5E1"));

    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var rect = nodes[i];
        RoundRect(g, rect, 18, Brushes.White, Pen(item.Color, 3));
        Text(g, item.Title, new RectangleF(rect.X + 30, rect.Y + 24, rect.Width - 60, 40), subTitleFont, Brush(item.Color));
        Text(g, item.Description, new RectangleF(rect.X + 35, rect.Y + 82, rect.Width - 70, 56), bodyFont, Brush(colors.Text));
        Text(g, item.Code, new RectangleF(rect.X + 35, rect.Y + 148, rect.Width - 70, 32), smallFont, Brush(colors.Muted));
        RoundRect(g, new RectangleF(rect.X + 28, rect.Y + 190, rect.Width - 56, 74), 10, Brush("#FEF2F2"), null);
        Text(g, item.Note, new RectangleF(rect.X + 46, rect.Y + 204, rect.Width - 92, 42), smallFont, Brush(colors.Red));
    }
    FooterBand(g, spec, 825);
}

void DrawBuffOwnershipLifecycle(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    var nodes = new[]
    {
        new RectangleF(120, 275, 300, 160),
        new RectangleF(600, 275, 300, 160),
        new RectangleF(1080, 275, 300, 160),
        new RectangleF(1560, 275, 240, 160)
    };
    for (var i = 0; i < nodes.Length - 1; i++)
        Arrow(g, nodes[i].Right + 8, 355, nodes[i + 1].Left - 10, 355, Pen("#64748B", 4));

    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var rect = nodes[i];
        RoundRect(g, rect, 16, Brushes.White, Pen(item.Color, 3));
        RoundRect(g, new RectangleF(rect.X, rect.Y, rect.Width, 55), 16, Brush(item.Color), null);
        Text(g, item.Title, new RectangleF(rect.X + 16, rect.Y + 10, rect.Width - 32, 34), bodyFont, Brushes.White);
        Text(g, item.Description, new RectangleF(rect.X + 20, rect.Y + 65, rect.Width - 40, 56), smallFont, Brush(colors.Text));
        Text(g, item.Code, new RectangleF(rect.X + 20, rect.Y + 126, rect.Width - 40, 24), smallFont, Brush(item.Color));
    }

    RoundRect(g, new RectangleF(520, 480, 880, 84), 12, Brush("#EEF6FF"), Pen("#BFDBFE", 2));
    Text(g, "跨模块绑定：Continuous · Modifier · Trigger owner\nTrace · skill retain", new RectangleF(555, 490, 810, 64), bodyFont, Brush(colors.Text));

    var cleanup = new[] { "停止 Continuous", "清 owner / skill", "通知与 Cue", "移出 Active", "回收 Runtime" };
    const float cleanupX = 175f;
    const float cleanupPitch = 315f;
    for (var i = 0; i < cleanup.Length - 1; i++)
        Arrow(g, cleanupX + i * cleanupPitch + 235, 670, cleanupX + (i + 1) * cleanupPitch - 20, 670, Pen(colors.Purple, 3));
    for (var i = 0; i < cleanup.Length; i++)
    {
        var x = cleanupX + i * cleanupPitch;
        RoundRect(g, new RectangleF(x - 35, 620, 270, 100), 12, Brushes.White, Pen(colors.Purple, 2));
        Text(g, (i + 1).ToString(), new RectangleF(x - 18, 636, 44, 44), bodyFont, Brush(colors.Purple));
        Text(g, cleanup[i], new RectangleF(x + 24, 633, 190, 56), smallFont, Brush(colors.Text), StringAlignment.Near);
    }
    RoundRect(g, new RectangleF(300, 760, 1320, 64), 10, Brush("#FEF2F2"), Pen("#FCA5A5", 2));
    Text(g, "提交前可局部补偿；写入 Active 后的通知异常和已有实例更新仍不是完整事务回滚。", new RectangleF(345, 772, 1230, 40), bodyFont, Brush(colors.Red));
    FooterBand(g, spec, 855);
}

void DrawProjectileBoundaryLifecycle(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    var nodes = new[]
    {
        new RectangleF(115, 270, 330, 165),
        new RectangleF(570, 270, 330, 165),
        new RectangleF(1025, 270, 330, 165),
        new RectangleF(1480, 270, 330, 165)
    };
    for (var i = 0; i < nodes.Length - 1; i++)
        Arrow(g, nodes[i].Right + 8, 352, nodes[i + 1].Left - 10, 352, Pen("#64748B", 4));
    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var rect = nodes[i];
        RoundRect(g, rect, 16, Brushes.White, Pen(item.Color, 3));
        Text(g, item.Title, new RectangleF(rect.X + 24, rect.Y + 20, rect.Width - 48, 38), bodyFont, Brush(item.Color));
        Text(g, item.Description, new RectangleF(rect.X + 24, rect.Y + 66, rect.Width - 48, 48), smallFont, Brush(colors.Text));
        Text(g, item.Code, new RectangleF(rect.X + 24, rect.Y + 122, rect.Width - 48, 28), smallFont, Brush(colors.Muted));
    }

    Text(g, "框架稳定事件", new RectangleF(210, 505, 250, 36), bodyFont, Brush(colors.Blue));
    RoundRect(g, new RectangleF(420, 480, 420, 105), 12, Brush("#EFF6FF"), Pen("#93C5FD", 2));
    Text(g, "Spawn / Tick / Hit / Exit", new RectangleF(450, 502, 360, 34), bodyFont, Brush(colors.Text));
    Text(g, "结束原因、命中策略、去重与冷却", new RectangleF(450, 541, 360, 26), smallFont, Brush(colors.Muted));
    Arrow(g, 850, 532, 1060, 532, Pen("#64748B", 4));
    RoundRect(g, new RectangleF(1070, 480, 560, 105), 12, Brush("#FFF7ED"), Pen("#FDBA74", 2));
    Text(g, "项目应用层消费事件", new RectangleF(1100, 498, 500, 34), bodyFont, Brush(colors.Text));
    Text(g, "伤害 / Buff / 阵营 / Trigger / 表现 / 权威同步", new RectangleF(1100, 540, 500, 28), smallFont, Brush(colors.Muted));

    RoundRect(g, new RectangleF(250, 655, 660, 115), 12, Brush("#ECFDF5"), Pen("#6EE7B7", 2));
    Text(g, "框架快照可恢复", new RectangleF(285, 675, 590, 34), bodyFont, Brush(colors.Green));
    Text(g, "位置、方向、生命周期、命中状态与调度数据", new RectangleF(285, 718, 590, 28), smallFont, Brush(colors.Text));
    RoundRect(g, new RectangleF(1010, 655, 660, 115), 12, Brush("#FEF2F2"), Pen("#FCA5A5", 2));
    Text(g, "项目副作用需另行恢复", new RectangleF(1045, 675, 590, 34), bodyFont, Brush(colors.Red));
    Text(g, "Actor、伤害结果、Trigger 订阅和表现不会由核心快照自动回滚", new RectangleF(1045, 718, 590, 28), smallFont, Brush(colors.Text));
    FooterBand(g, spec, 835);
}

void DrawSyncDecisionQuestions(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    var questions = new[]
    {
        new RectangleF(100, 275, 405, 300),
        new RectangleF(575, 275, 405, 300),
        new RectangleF(1050, 275, 405, 300)
    };
    var contract = new RectangleF(1540, 310, 290, 230);
    for (var i = 0; i < questions.Length - 1; i++)
        Arrow(g, questions[i].Right + 8, 425, questions[i + 1].Left - 10, 425, Pen("#64748B", 4));
    Arrow(g, questions[^1].Right + 8, 425, contract.Left - 10, 425, Pen(colors.Green, 4));

    for (var i = 0; i < questions.Length; i++)
    {
        var item = spec.Items[i];
        var rect = questions[i];
        RoundRect(g, rect, 16, Brushes.White, Pen(item.Color, 3));
        Text(g, $"0{i + 1}", new RectangleF(rect.X + 24, rect.Y + 20, 70, 42), subTitleFont, Brush(item.Color), StringAlignment.Near);
        Text(g, item.Title, new RectangleF(rect.X + 92, rect.Y + 20, rect.Width - 116, 42), subTitleFont, Brush(colors.Text), StringAlignment.Near);
        Text(g, item.Description, new RectangleF(rect.X + 30, rect.Y + 90, rect.Width - 60, 82), bodyFont, Brush(colors.Text));
        RoundRect(g, new RectangleF(rect.X + 28, rect.Y + 190, rect.Width - 56, 82), 10, Brush("#F8FAFC"), null);
        Text(g, item.Code, new RectangleF(rect.X + 45, rect.Y + 204, rect.Width - 90, 50), smallFont, Brush(colors.Muted));
    }

    var finalItem = spec.Items[3];
    RoundRect(g, contract, 18, Brush("#172033"), null);
    Text(g, finalItem.Title, new RectangleF(contract.X + 24, contract.Y + 25, contract.Width - 48, 48), subTitleFont, Brushes.White);
    Text(g, finalItem.Description, new RectangleF(contract.X + 24, contract.Y + 88, contract.Width - 48, 60), bodyFont, Brush("#E2E8F0"));
    Text(g, finalItem.Code, new RectangleF(contract.X + 24, contract.Y + 160, contract.Width - 48, 44), smallFont, Brush("#86EFAC"));

    RoundRect(g, new RectangleF(235, 650, 1450, 105), 12, Brush("#EEF6FF"), Pen("#BFDBFE", 2));
    Text(g, "可组合能力", new RectangleF(275, 675, 190, 36), bodyFont, Brush(colors.Blue), StringAlignment.Near);
    Text(g, "FrameSync  ·  StateSync  ·  Prediction  ·  Rollback  ·  Replay", new RectangleF(470, 665, 1120, 45), subTitleFont, Brush(colors.Text));
    Text(g, "Profile 是协商与选择入口，不是算法完成度标签；最终以服务端 commit 的模板和能力声明为准。", new RectangleF(405, 712, 1160, 28), smallFont, Brush(colors.Muted));
    FooterBand(g, spec, 820);
}

void DrawPresentationProjection(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    var logic = new RectangleF(125, 355, 360, 250);
    var eventNode = new RectangleF(620, 245, 380, 165);
    var snapshotNode = new RectangleF(620, 550, 380, 165);
    var sink = new RectangleF(1120, 365, 330, 210);
    var view = new RectangleF(1540, 330, 280, 280);

    Arrow(g, logic.Right + 8, 420, eventNode.Left - 10, 330, Pen(colors.Amber, 4));
    Arrow(g, logic.Right + 8, 540, snapshotNode.Left - 10, 632, Pen(colors.Cyan, 4));
    Arrow(g, eventNode.Right + 8, 330, sink.Left - 10, 430, Pen(colors.Amber, 4));
    Arrow(g, snapshotNode.Right + 8, 632, sink.Left - 10, 510, Pen(colors.Cyan, 4));
    Arrow(g, sink.Right + 8, 470, view.Left - 10, 470, Pen(colors.Green, 4));

    var rects = new[] { logic, eventNode, snapshotNode, sink, view };
    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var rect = rects[i];
        RoundRect(g, rect, 16, i == 0 ? Brush("#172033") : Brushes.White, Pen(item.Color, 3));
        var titleBrush = i == 0 ? Brushes.White : Brush(item.Color);
        var textBrush = i == 0 ? Brush("#E2E8F0") : Brush(colors.Text);
        var nodeTitleFont = i == 4 ? bodyFont : subTitleFont;
        Text(g, item.Title, new RectangleF(rect.X + 20, rect.Y + 22, rect.Width - 40, 42), nodeTitleFont, titleBrush);
        if (i is 1 or 2)
        {
            Text(g, item.Description, new RectangleF(rect.X + 24, rect.Y + 70, rect.Width - 48, 52), smallFont, textBrush);
            Text(g, item.Code, new RectangleF(rect.X + 24, rect.Y + 128, rect.Width - 48, 25), smallFont, Brush(colors.Muted));
        }
        else
        {
            Text(g, item.Description, new RectangleF(rect.X + 24, rect.Y + 85, rect.Width - 48, 92), bodyFont, textBrush);
            Text(g, item.Code, new RectangleF(rect.X + 24, rect.Bottom - 58, rect.Width - 48, 32), smallFont, i == 0 ? Brush("#93C5FD") : Brush(colors.Muted));
        }
    }

    using (var denyPen = Pen(colors.Red, 4))
    {
        denyPen.DashStyle = DashStyle.Dash;
        g.DrawLine(denyPen, 310, 735, 1680, 735);
    }
    Text(g, "X  表现层不得反写权威战斗状态", new RectangleF(620, 748, 680, 38), bodyFont, Brush(colors.Red));
    FooterBand(g, spec, 825);
}

void DrawCapabilityOperatingModel(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);

    Text(g, "项目决策", new RectangleF(100, 210, 260, 42), bodyFont, Brush(colors.Amber));
    Text(g, "AbilityKit 六类现役能力", new RectangleF(560, 210, 800, 42), subTitleFont, Brush(colors.Blue));
    Text(g, "项目得到", new RectangleF(1560, 210, 260, 42), bodyFont, Brush(colors.Green));

    var inputYs = new[] { 350f, 485f, 620f };
    var inputLabels = new[] { "英雄 / 技能规则", "权威 / 房间约束", "体验 / 预算 / 平台" };
    var outputLabels = new[] { "规则可运行、可诊断", "事实可同步、可恢复", "多端可构建、可验收" };
    using (var collector = Pen("#94A3B8", 3))
    {
        g.DrawLine(collector, 390, inputYs[0], 390, inputYs[^1]);
        g.DrawLine(collector, 1530, inputYs[0], 1530, inputYs[^1]);
        foreach (var y in inputYs)
        {
            g.DrawLine(collector, 355, y, 390, y);
            g.DrawLine(collector, 1530, y, 1560, y);
        }
    }

    for (var i = 0; i < inputYs.Length; i++)
    {
        using var inputBrush = Brush(i == 0 ? colors.Amber : colors.DarkLine);
        g.FillEllipse(inputBrush, 105, inputYs[i] - 11, 22, 22);
        Text(g, inputLabels[i], new RectangleF(150, inputYs[i] - 30, 220, 60), bodyFont, Brush(colors.Text), StringAlignment.Near);

        using var outputBrush = Brush(i == 2 ? colors.Green : colors.DarkLine);
        g.FillEllipse(outputBrush, 1560, inputYs[i] - 11, 22, 22);
        Text(g, outputLabels[i], new RectangleF(1605, inputYs[i] - 30, 220, 60), bodyFont, Brush(colors.Text), StringAlignment.Near);
    }

    Arrow(g, 390, 485, 455, 485, Pen("#64748B", 4));
    Arrow(g, 1465, 485, 1530, 485, Pen("#64748B", 4));

    var engineering = spec.Items[5];
    var foundation = spec.Items[0];
    RoundRect(g, new RectangleF(465, 260, 990, 90), 14, Brush("#F3E8FF"), Pen(engineering.Color, 3));
    Text(g, engineering.Title, new RectangleF(495, 278, 225, 42), bodyFont, Brush(engineering.Color), StringAlignment.Near);
    Text(g, engineering.Description, new RectangleF(720, 274, 700, 50), smallFont, Brush(colors.Text), StringAlignment.Near);

    const float startX = 465f;
    const float stageY = 385f;
    const float stageWidth = 225f;
    const float stageHeight = 225f;
    const float pitch = 255f;
    for (var i = 0; i < 3; i++)
        Arrow(g, startX + i * pitch + stageWidth, stageY + stageHeight / 2, startX + (i + 1) * pitch - 10, stageY + stageHeight / 2, Pen("#64748B", 4));

    for (var i = 0; i < 4; i++)
    {
        var item = spec.Items[i + 1];
        var x = startX + i * pitch;
        RoundRect(g, new RectangleF(x, stageY, stageWidth, stageHeight), 14, Brushes.White, Pen(item.Color, 3));
        Text(g, item.Title, new RectangleF(x + 16, stageY + 22, stageWidth - 32, 56), bodyFont, Brush(item.Color));
        using var capabilityFont = Font(17, FontStyle.Regular);
        Text(g, item.Description, new RectangleF(x + 14, stageY + 82, stageWidth - 28, 88), capabilityFont, Brush(colors.Text));
        using var codeFont = Font(16, FontStyle.Regular);
        Text(g, item.Code, new RectangleF(x + 15, stageY + 174, stageWidth - 30, 34), codeFont, Brush(colors.Muted));
    }

    RoundRect(g, new RectangleF(465, 650, 990, 90), 14, Brush("#E8EEF7"), Pen(foundation.Color, 3));
    Text(g, foundation.Title, new RectangleF(495, 668, 225, 42), bodyFont, Brush(foundation.Color), StringAlignment.Near);
    Text(g, foundation.Description, new RectangleF(720, 664, 700, 50), smallFont, Brush(colors.Text), StringAlignment.Near);

    Text(g, "全链路支撑", new RectangleF(865, 745, 190, 32), smallFont, Brush(colors.Muted));
    FooterBand(g, spec, 810);
}

void DrawProblemCapabilityPaths(Graphics g, CodeVisualSpec spec)
{
    Header(g, spec.Title, spec.Subtitle);
    Text(g, "当前项目问题", new RectangleF(105, 205, 300, 42), bodyFont, Brush(colors.Muted));
    Text(g, "按需组合的实际能力", new RectangleF(485, 205, 430, 42), bodyFont, Brush(colors.Muted));
    Text(g, "带来的直接作用", new RectangleF(1000, 205, 390, 42), bodyFont, Brush(colors.Muted));
    Text(g, "应建立的证据", new RectangleF(1480, 205, 280, 42), bodyFont, Brush(colors.Muted));

    var rowYs = new[] { 330f, 530f, 730f };
    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var y = rowYs[i];
        Arrow(g, 415, y, 470, y, Pen("#64748B", 4));
        Arrow(g, 925, y, 980, y, Pen("#64748B", 4));
        Arrow(g, 1400, y, 1460, y, Pen("#64748B", 4));

        using (var separator = Pen("#E2E8F0", 2))
            g.DrawLine(separator, 105, y + 82, 1780, y + 82);
        using (var marker = Brush(item.Color))
            g.FillRectangle(marker, 105, y - 66, 8, 132);

        Text(g, item.Title, new RectangleF(135, y - 58, 270, 116), bodyFont, Brush(colors.Text), StringAlignment.Near);
        RoundRect(g, new RectangleF(480, y - 68, 435, 136), 14, Brushes.White, Pen(item.Color, 3));
        Text(g, item.Description, new RectangleF(505, y - 48, 385, 96), smallFont, Brush(colors.Text));

        Text(g, item.Code, new RectangleF(1000, y - 58, 380, 116), bodyFont, Brush(colors.Text), StringAlignment.Near);
        RoundRect(g, new RectangleF(1470, y - 56, 290, 112), 12, Brush("#EEF2F7"), Pen(item.Color, 2));
        Text(g, item.Note, new RectangleF(1490, y - 36, 250, 72), smallFont, Brush(item.Color));
    }

    FooterBand(g, spec, 850);
}

void FooterBand(Graphics g, CodeVisualSpec spec, float y)
{
    RoundRect(g, new RectangleF(210, y, 1500, 82), 12, Brush("#EEF2F7"), Pen("#CBD5E1", 2));
    Text(g, spec.Takeaway, new RectangleF(250, y + 15, 1420, 50), bodyFont, Brush(colors.Text));
    Text(g, spec.Source, new RectangleF(230, y + 98, 1460, 30), smallFont, Brush(colors.Muted), StringAlignment.Near);
}

void DrawHorizontalSteps(Graphics g, Step[] steps, float startX, float y, float width, float height, float pitch)
{
    var x = startX;
    for (var i = 0; i < steps.Length; i++)
    {
        var step = steps[i];
        RoundRect(g, new RectangleF(x, y, width, height), 14, Brush(step.Color), null);
        Text(g, step.Title, new RectangleF(x + 15, y + 18, width - 30, 42), subTitleFont, Brushes.White);
        Text(g, step.Description, new RectangleF(x + 18, y + 72, width - 36, height - 88), smallFont, Brushes.White);
        if (i < steps.Length - 1)
            Arrow(g, x + width + 8, y + height / 2, x + pitch - 10, y + height / 2, Pen(colors.DarkLine, 3));
        x += pitch;
    }
}

void Arrow(Graphics g, float x1, float y1, float x2, float y2, Pen pen)
{
    g.DrawLine(pen, x1, y1, x2, y2);

    var angle = MathF.Atan2(y2 - y1, x2 - x1);
    const float size = 14f;
    var left = new PointF(
        x2 - size * MathF.Cos(angle - MathF.PI / 6f),
        y2 - size * MathF.Sin(angle - MathF.PI / 6f));
    var right = new PointF(
        x2 - size * MathF.Cos(angle + MathF.PI / 6f),
        y2 - size * MathF.Sin(angle + MathF.PI / 6f));

    using var brush = new SolidBrush(pen.Color);
    g.FillPolygon(brush, new[] { new PointF(x2, y2), left, right });
    pen.Dispose();
}

PointF RectangleBoundaryPoint(RectangleF rect, PointF toward)
{
    var center = new PointF(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
    var dx = toward.X - center.X;
    var dy = toward.Y - center.Y;
    if (MathF.Abs(dx) < 0.001f && MathF.Abs(dy) < 0.001f)
        return center;

    var xScale = MathF.Abs(dx) < 0.001f ? float.PositiveInfinity : rect.Width / 2f / MathF.Abs(dx);
    var yScale = MathF.Abs(dy) < 0.001f ? float.PositiveInfinity : rect.Height / 2f / MathF.Abs(dy);
    var scale = MathF.Min(xScale, yScale);
    return new PointF(center.X + dx * scale, center.Y + dy * scale);
}

Font Font(float size, FontStyle style)
{
    foreach (var family in new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI", "Arial" })
    {
        try { return new Font(family, size, style, GraphicsUnit.Pixel); }
        catch { }
    }
    return new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Pixel);
}

SolidBrush Brush(string hex) => new(ColorTranslator.FromHtml(hex));
Pen Pen(string hex, float width) => new(ColorTranslator.FromHtml(hex), width);

CodeVisualSpec[] CodeVisualSpecs()
{
    var palette = new[] { colors.Blue, colors.Cyan, colors.Green, colors.Amber, colors.Purple, colors.Red };
    int StablePaletteIndex(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in value)
                hash = (hash ^ ch) * 16777619;
            return (int)(hash % palette.Length);
        }
    }
    CodeVisualItem I(string title, string desc, string code, string note = "") => new(title, desc, code, note, palette[StablePaletteIndex(title + code)]);
    CodeVisualItem IC(string title, string desc, string code, string color, string note = "") => new(title, desc, code, note, color);
    CodeVisualSpec S(string fileName, CodeVisualKind kind, string title, string subtitle, string takeaway, string source, params CodeVisualItem[] items) =>
        new(fileName, kind, title, subtitle, takeaway, source, string.Empty, string.Empty, string.Empty, Array.Empty<string>(), items);

    return new[]
    {
        S("23-company-framework-title.png", CodeVisualKind.Stack, "AbilityKit 的真实代码资产", "公司级能力不是口号：它由包、服务、示例、测试和工具链共同构成", "这页可以用代码目录证明：AbilityKit 已经具备可跨项目维护的工程形态。", "代码依据：Unity/Packages + src + Docs + tools", I("UPM Packages", "Unity 项目可直接接入", "Unity/Packages/com.abilitykit.*"), I("Pure C# Runtime", "服务端、工具、测试可运行", "src/AbilityKit.*"), I("Demo Runtime", "MOBA / Shooter 承担复杂验收", "com.abilitykit.demo.*"), I("Docs & Gates", "设计、门禁、批量回归沉淀", "Docs/*.md"), I("Asset Generator", "教学图和 Mermaid 可再生成", "tools/AbilityKitPptAssetGenerator")),
        S("24-fragmented-combat-systems.png", CodeVisualKind.SplitFlow, "从单项目实现到统一入口", "同类战斗能力如果分散在项目中，最后会缺少共同调试、追踪和测试入口", "高价值讲法：不是反对项目定制，而是把共性的入口和诊断能力统一下来。", "代码依据：MobaTriggerExecutionGateway / MobaEffectExecutionService", I("分散入口", "技能、Buff、投射物各自触发", "Skill / Buff / Projectile"), I("重复上下文", "来源、payload、trace 各自拼装", "custom context"), I("难以回归", "问题只能在项目内复现", "manual QA"), I("统一网关", "直接触发和 owner-bound 收敛", "ExecuteDirectTrigger"), I("正式上下文", "payload + lineage + snapshot", "CreateCombatExecutionContext"), I("统一计划执行", "同一预算、条件、trace", "ExecuteTriggerPlan")) with { LeftLabel = "单项目自然分裂", RightLabel = "AbilityKit 统一链路" },
        S("25-local-optimum-company-cost.png", CodeVisualKind.Matrix, "代码层面的公司成本差异", "同一个能力在不同项目重复实现，真正变贵的是后续修复、追踪和回归", "把成本说清楚：公共框架多写的是一次结构，少掉的是多项目长期重复验证。", "代码依据：Trigger / Damage / Sync / DemoHarness", I("技能释放", "各项目 Cast 函数膨胀", "SkillCastCoordinator", "统一输入相位"), I("触发反应", "if/else 分散复制", "MobaTriggerPlanExecutor", "统一计划"), I("伤害结算", "公式和事件不一致", "DamagePipelineService", "阶段化"), I("同步验收", "只靠手测弱网", "DemoHarnessRunner", "矩阵化")) with { MatrixHeaders = new[] { "能力域", "重复成本", "统一入口", "框架收益" } },
        S("26-shared-framework-value.png", CodeVisualKind.DataFlow, "一次修复如何跨项目受益", "公共链路的收益来自稳定入口：问题在框架层补测试，后续项目升级即可获得保护", "这页用于解释公司级复利：bug 不只是修掉，还会变成可执行资产。", "代码依据：PlanActionModuleRegistry / tests / CI docs", I("问题暴露", "示例或项目发现链路缺陷", "MobaDamageTrace"), I("框架修复", "服务、模块或注册表修正", "PlanActionModule"), I("补充用例", "Unit / Smoke / DSL / Matrix", "test-gates"), I("资产发布", "UPM / NuGet / Docs 更新", "package.json"), I("项目受益", "升级后共享同一保护", "CI gate")),
        S("27-abilitykit-positioning.png", CodeVisualKind.SplitFlow, "AbilityKit 的边界位置", "核心运行时保持纯 C#，Unity、服务端、工具和示例通过适配层接入", "定位要落到依赖边界：项目可以组合能力，不需要被一个黑盒框架接管。", "代码依据：src/AbilityKit.* 与 Unity/Packages/com.abilitykit.*", I("Core Runtime", "Math / Event / Pipeline / Triggering", "src/AbilityKit.Core"), I("Combat Modules", "Targeting / Projectile / Damage", "src + UPM packages"), I("World Services", "DI / ECS / FrameSync / Snapshot", "World.*"), I("Unity Shell", "表现、编辑器、View Runtime", "Unity/Packages"), I("Server / Tools", "Orleans、Console、Codegen", "Server + tools"), I("Samples", "MOBA / Shooter 验证组合", "demo.*")) with { LeftLabel = "纯逻辑能力", RightLabel = "项目适配层" },
        S("28-abilitykit-non-goals.png", CodeVisualKind.Matrix, "从代码结构排除误解", "AbilityKit 不是全量替代项目业务，而是把可复用的底座和验证路径抽出来", "用边界降低抵触：项目仍然保留业务表达，框架负责稳定公共能力。", "代码依据：服务接口、模块包、示例工程分层", I("不是黑盒", "项目通过服务和模块扩展", "IWorldResolver", "可替换"), I("不是 Demo 代码", "示例承担验收职责", "ShooterAcceptanceLab", "可回归"), I("不是只跑客户端", "纯 C# 可离线执行", "src/*.csproj", "可测试"), I("不是全量迁移", "按模块组合接入", "com.abilitykit.*", "可渐进")) with { MatrixHeaders = new[] { "常见误解", "代码事实", "工程证据", "实际边界" } },
        S("29-company-assets-map.png", CodeVisualKind.Stack, "公司级资产不是只有代码", "真实可复用资产包含运行时包、示例、测试门禁、文档和生成工具", "没有验证的代码只是复制；带门禁和示例的代码才是公司资产。", "代码依据：Docs / tools / Unity Packages", I("Runtime Package", "稳定 API 和模块边界", "com.abilitykit.*"), I("Reference Samples", "MOBA 验证战斗，Shooter 验证同步", "demo.moba / demo.shooter"), I("Test Gates", "P0/P1/P2 分层验证", "AbilityKit测试门禁"), I("Design Docs", "设计意图可追溯", "Docs/*.md"), I("Generators", "Codegen 和 PPT 资产可重复生成", "tools/*")),
        S("30-composable-adoption-model.png", CodeVisualKind.DataFlow, "按能力组合接入，而不是一次性重写", "项目可以从 Core/Pipeline 开始，再按风险接入 Triggering、Combat、Sync 和 Gates", "渐进接入更现实：每个阶段都能用具体代码和用例证明收益。", "代码依据：包拆分和模块边界", I("Core", "基础数学、事件、ID", "com.abilitykit.core"), I("Pipeline", "技能阶段和运行控制", "com.abilitykit.pipeline"), I("Triggering", "规则、条件、Action", "com.abilitykit.triggering"), I("Combat", "Targeting / Projectile / Damage", "combat.*"), I("Sync", "FrameSync / Snapshot / StateSync", "world.*"), I("Gates", "Smoke / DSL / Matrix", "Docs/test-gates")),
        S("31-module-boundary-collaboration.png", CodeVisualKind.Matrix, "模块边界如何帮助协作定位", "统一模块名和服务入口后，跨项目问题可以按链路分派，而不是靠熟人记忆", "边界的价值不是画目录，而是让问题能被定位、复盘和转成回归。", "代码依据：WorldService 和 PlanActionModule", I("Skill", "输入、准备、Pipeline", "SkillCastCoordinator", "技能负责人"), I("Trigger", "事件、条件、计划", "MobaTriggerExecutionGateway", "规则负责人"), I("Damage", "公式、护盾、事件", "DamagePipelineService", "战斗负责人"), I("Sync", "输入帧、快照、矩阵", "FramePacketNetAdapter", "网络负责人")) with { MatrixHeaders = new[] { "问题域", "模块职责", "代码入口", "责任边界" } },
        S("32-maintainability-handover.png", CodeVisualKind.Lifecycle, "换人后仍能维护的代码闭环", "新同事接手不靠口口相传，而是从入口、trace、示例、测试和文档形成闭环", "可维护来自结构和反馈，不只是注释。", "代码依据：Trace / DemoHarness / Docs", I("固定入口", "先找到服务和网关", "WorldService"), I("Trace 链", "能看到来源和子节点", "MobaTraceRegistry"), I("示例复现", "MOBA / Shooter 可运行", "demo.*"), I("测试保护", "改动后快速反馈", "smoke / matrix"), I("文档追溯", "设计意图保留", "Docs/*.md")) with { CenterLabel = "可追溯\n交接闭环" },
        S("33-pipeline-phase-composition.png", CodeVisualKind.Sequence, "真实技能释放调用链", "从输入相位到 runtime 创建，每一步都有独立职责和失败原因", "这张图能替代泛化 Pipeline 说明，直接讲真实代码如何拆解一次 Cast。", "代码依据：SkillCastCoordinator.cs / SkillCastPreparationService.cs", I("输入相位", "Press / Hold / Release / Cancel", "DispatchSkillInputPhase"), I("槽位解析", "slot -> skillId", "TryCastBySlot"), I("准备施法", "配置 / 索敌 / aim / pipeline", "SkillCastPreparationService.Prepare"), I("创建 Trace Root", "技能释放正式根节点", "CreateRootContext"), I("创建 Runtime", "handle / runtimeId / blackboard", "MobaSkillCastRuntimeService.Create"), I("Runner 执行", "PreCast / Cast phases", "SkillRunnerRegistry"), I("标记结束", "PipelineEnded + EndReason", "MarkPipelineEnded"), I("终结判定", "PendingChildren 大于 0 则 WaitingChildren 挂起，否则 Finalized", "TryFinalize")),
        S("34-triggering-rule-system.png", CodeVisualKind.Sequence, "触发计划执行链路", "直接触发和 owner-bound 触发最终进入同一套上下文、预算、条件和计划执行", "Triggering 的价值是统一反应链路，让规则扩展不再散落在业务 if/else。", "代码依据：MobaTriggerExecutionGateway.cs / MobaEffectExecutionService.cs", I("入口收敛", "Direct / OwnerBound", "MobaTriggerExecutionGateway"), I("执行请求", "TriggerId + typed payload", "ExecuteTrigger<TPayload>"), I("上下文创建", "payload / lineage / snapshot", "CreateCombatExecutionContext"), I("预算保护", "depth / frame / same trigger", "TryEnterExecutionBudget"), I("开启会话", "using 保证 EndCurrentTrace", "BeginExecutionSession"), I("条件求值", "EvaluateTriggerConditions", "session 内"), I("计划执行", "TryExecutePlanByTriggerId / ExecuteRulePlan", "MobaTriggerPlanExecutor"), I("会话收尾", "session.Complete 关闭 Trace", "Trace 结束")),
        S("35-sync-risk-framework.png", CodeVisualKind.SplitFlow, "同步风险的代码级收敛点", "FramePacketNetAdapter 把输入帧和快照路由集中处理，DemoHarness 把同步模型放进矩阵验收", "同步框架化不是多写抽象，而是给高风险链路建立固定验证入口。", "代码依据：FramePacketNetAdapter.cs / DemoHarnessRunner.cs", I("FramePacket", "worldId / frame / inputs", "ProcessAndFeed"), I("RemoteDriven", "延迟输入 + jitter buffer", "RemoteDrivenSink.Add"), I("Confirmed", "权威输入 buffer", "ConfirmedSink.Add"), I("Snapshot", "envelope feed", "Snapshots.Feed"), I("Scenario", "sync profile + network + carrier", "DemoHarnessScenario"), I("Status", "Completed / Degraded / Failed", "DemoHarnessRunStatus")) with { LeftLabel = "运行时路由", RightLabel = "自动化验收" },
        S("36-sample-dual-validation.png", CodeVisualKind.Matrix, "MOBA / Shooter 分别验证什么", "两个示例覆盖的不是玩法展示，而是复杂战斗和同步边界两类公司级风险", "示例越接近真实复杂度，越能为框架升级提供信心。", "代码依据：demo.moba.runtime / demo.shooter.view.runtime", I("MOBA", "技能、触发、伤害、Buff", "MobaEffectExecutionService", "战斗治理"), I("Shooter", "预测、快照、网络矩阵", "ShooterAcceptanceLab", "同步治理"), I("Console", "纯逻辑 smoke 和 DSL", "Demo.Host.Console", "CI 友好"), I("Unity", "表现与运行时契约", "View.Runtime", "集成验证")) with { MatrixHeaders = new[] { "验证载体", "覆盖风险", "代码入口", "验证定位" } },
        S("37-shooter-validation-showcase.png", CodeVisualKind.SplitFlow, "Shooter 双层验收证据", "DemoHarness 枚举能力边界，真实 TCP / 多进程 smoke 验证传输、重连和回放闭环", "四态矩阵回答“组合是否支持”，进程级 smoke 回答“真实链路是否跑通”；两者不能互相替代。", "代码依据：DemoHarnessRunner.cs / run_shooter_multiprocess_smoke.ps1 / ShooterSmokeReplayValidation.cs", I("能力组合", "sync profile + network + carrier", "DemoHarnessScenario"), I("四态结果", "Completed / Unsupported / Degraded / Failed", "DemoHarnessRunStatus"), I("可比较指标", "reconcile / jitter / snapshot / health", "metrics + report"), I("真实 TCP", "client process -> gateway -> Orleans", "SmokeTcpGameFrameworkNetworkChannel"), I("重连与迟到加入", "disconnect / reconnect / full snapshot", "multiprocess smoke"), I("回放证据", "完整 + 最小 replay / hash 校验", "ShooterSmokeReplayValidation")) with { LeftLabel = "组合覆盖：DemoHarness", RightLabel = "传输闭环：Process Smoke" },
        S("38-sample-as-best-practice.png", CodeVisualKind.DataFlow, "示例工程如何反向驱动框架", "示例里的失败会暴露框架边界问题，修复后再沉淀为文档和测试", "示例不是复制模板，而是公共能力的验证场和教学入口。", "代码依据：MOBA acceptance / Shooter acceptance / Docs", I("Scenario", "真实链路用例", "skill_10020101"), I("Failure", "trace / log / metrics", "artifacts/*.log"), I("Framework Fix", "runtime service / adapter", "com.abilitykit.*"), I("Regression", "用例进入门禁", "test-gates"), I("Teaching", "图表和讲稿更新", "ppt-assets")),
        S("39-framework-test-necessity.png", CodeVisualKind.Stack, "为什么框架更需要测试", "公共框架的每一次改动都有跨项目影响，所以测试要覆盖 API、主链路、示例和同步矩阵", "复用范围越大，越需要自动化反馈来换取升级信心。", "代码依据：Docs/AbilityKit测试门禁与批量回归规范.md", I("Unit", "规则和纯函数边界", "dotnet test"), I("Contract", "模块接口不破坏", "runtime contracts"), I("Smoke", "主链路快速阻断", "moba-console-smoke"), I("DSL", "战斗剧本复现", "MobaAcceptanceScenario"), I("Matrix", "同步模型批量验收", "DemoHarness")),
        S("40-test-assets-compound.png", CodeVisualKind.Lifecycle, "Bug 如何沉淀成测试资产", "一次问题从日志定位、修复、补用例、进门禁到跨项目复用，形成闭环", "测试资产的复利来自闭环，不来自测试数量本身。", "代码依据：artifacts / test-gates / Docs", I("发现", "日志、trace、metrics", "artifacts"), I("定位", "首个分歧点", "trace chain"), I("修复", "框架或示例代码", "apply fix"), I("补测", "unit / smoke / DSL", "new case"), I("门禁", "PR 或 nightly", "CI gate"), I("复用", "后续项目共享", "package upgrade")) with { CenterLabel = "可复用\n回归资产" },
        S("41-unified-process-handover.png", CodeVisualKind.DataFlow, "统一流程如何降低交接成本", "命名、入口、日志、门禁和文档固定后，新人能沿同一条路径接手问题", "流程统一的收益是可操作，而不是写在规范里的抽象要求。", "代码依据：WorldService / Diagnostics / Docs", I("命名", "同类服务同类入口", "*Service"), I("入口", "WorldInject / Resolve", "IWorldResolver"), I("诊断", "Counter / Gauge / Trace", "Diagnostics"), I("验证", "gate-summary / trx", "artifacts"), I("文档", "设计与讲稿关联", "Docs")),
        S("42-adoption-by-project-scale.png", CodeVisualKind.Matrix, "按项目规模选择代码接入面", "不同项目不需要同样重的框架接入，但可以共享同一套能力边界", "接入策略要围绕风险选择模块，而不是围绕框架覆盖率。", "代码依据：包拆分 + 示例验证", I("原型", "Core + Pipeline", "com.abilitykit.core", "低成本"), I("中型", "Triggering + Combat", "triggering / damage", "规则复用"), I("多人", "FrameSync + Snapshot", "world.framesync", "同步风险"), I("长线", "DSL + CI + Matrix", "test-gates", "运营回归")) with { MatrixHeaders = new[] { "项目阶段", "推荐能力", "接入入口", "主要收益" } },
        S("43-internal-rollout-roadmap.png", CodeVisualKind.Sequence, "公司内部推进可执行路线", "先选稳定入口和可验证场景，再扩到第二项目，最后沉淀门禁和升级策略", "推广要靠可运行资产证明收益，而不是靠一次内训说服所有项目。", "代码依据：packages / demo / gates", I("选试点", "重复且风险高的能力", "pilot module"), I("接入口", "只接稳定服务边界", "IWorldResolver"), I("跑示例", "MOBA / Shooter 对照", "demo.*"), I("补门禁", "smoke + matrix", "test-gates"), I("第二项目", "验证跨项目收益", "package upgrade"), I("版本治理", "文档、报告、发布节奏", "README + artifacts")),
        S("44-reuse-worthiness-filter.png", CodeVisualKind.Matrix, "判断代码是否值得进入框架", "不是所有项目代码都应该上升为公司资产，必须同时满足稳定、通用、可测、可扩展", "克制边界能让框架长期可维护。", "代码依据：包边界和测试能力", I("稳定", "不随业务频繁变", "public API", "必要"), I("通用", "多个项目会遇到", "module package", "必要"), I("可测", "可脱离项目验证", "unit / smoke", "必要"), I("可扩展", "项目差异有插槽", "interfaces", "必要")) with { MatrixHeaders = new[] { "准入条件", "判断标准", "工程证据", "是否必需" } },
        S("45-framework-risk-controls.png", CodeVisualKind.Matrix, "框架落地风险与代码控制点", "风险不靠口头提醒控制，而靠包边界、示例、门禁和降级路径控制", "框架治理的关键是每个风险都有工程抓手。", "代码依据：package split / DemoHarness / gates", I("过早抽象", "缺少第二场景验证", "示例先行", "demo.* / acceptance"), I("接入过重", "小项目流程负担过高", "按能力拆包", "com.abilitykit.*"), I("升级不敢", "缺少自动反馈证据", "分级门禁", "gate-summary / artifacts")) with { MatrixHeaders = new[] { "主要风险", "触发信号", "工程控制", "代码证据" } },
        S("46-company-benefit-summary.png", CodeVisualKind.Stack, "团队收益落到代码资产", "复用、协作、维护、验证和升级都要落到可运行、可检查、可发布的资产上", "最终目标是让战斗系统能力随项目增多而增强。", "代码依据：AbilityKit 全仓库资产", I("复用", "包和服务复用", "UPM / NuGet"), I("协作", "模块边界和命名统一", "WorldService"), I("维护", "Trace + Docs + Samples", "MobaTraceRegistry"), I("验证", "Unit / Smoke / DSL / Matrix", "test-gates"), I("升级", "一次修复多项目受益", "package release")),
        S("47-discussion-decision-map.png", CodeVisualKind.Matrix, "下一步讨论应落到代码决策", "讨论项不再泛泛谈是否采用框架，而是选择试点、入口、门禁和回流机制", "内训最后要收束到可执行决策。", "代码依据：当前可落地入口", I("试点模块", "技能 / 触发 / 同步", "module owner", "试点范围"), I("接入入口", "WorldService / Adapter", "integration owner", "接口清单"), I("验收门禁", "P0 smoke + P1 contract", "quality owner", "门禁清单"), I("回流机制", "项目问题进入框架 backlog", "framework owner", "Docs + tests")) with { MatrixHeaders = new[] { "决策项", "可选范围", "负责人", "输出物" } },
        S("48-framework-reference-project-boundary.png", CodeVisualKind.Matrix, "示例证明组合方式，不定义唯一应用层", "稳定契约进入框架；可复制组织方式留在 Reference；高变化规则由项目拥有", "三层边界避免把 Demo 完成度误写成公共能力成熟度。", "文档依据：玩法能力地图 / 文档治理", I("Framework", "稳定语义、生命周期、失败边界", "public packages", "跨项目契约"), I("Reference", "MOBA / Shooter 的高接入组合", "demo.*", "证明一种接法"), I("Project", "英雄、房间、结算、体验、预算", "game application", "项目最终所有")) with { MatrixHeaders = new[] { "层次", "负责什么", "主要载体", "采用含义" } },
        S("49-adoption-evidence-release-model.png", CodeVisualKind.Matrix, "采用判断需要四个独立坐标", "资产类型、组织成熟度、工程证据和发布状态回答不同问题，不能用一个标签互相替代", "接口存在、测试通过和组织承诺是三份不同证据。", "文档依据：公司级采用与模块治理", I("资产类型", "Foundation / Domain / Adapter", "Reference / Validation", "设计成什么"), I("成熟度", "Experimental / Pilot", "Supported / Recommended", "允许谁采用"), I("工程证据", "E0-E5", "implementation -> gate", "证明到哪里"), I("发布状态", "version / artifact", "owner / rollback", "怎样获取退出")) with { MatrixHeaders = new[] { "坐标", "典型状态", "需要核对", "回答的问题" } },
        S("50-skill-cast-preparation.png", CodeVisualKind.DataFlow, "施法准备只负责形成可执行请求", "把输入解析、配置、目标和 Pipeline 选择收敛在 Runtime 创建之前", "准备阶段失败应有明确原因，不留下半创建 Runtime。", "代码依据：SkillCastPreparationService", I("输入相位", "Press / Hold / Release", "DispatchSkillInputPhase"), I("槽位与配置", "slot -> skill -> config", "TryCastBySlot"), I("施法上下文", "caster / target / aim", "Prepare"), I("Pipeline 请求", "pre-cast + cast", "SkillCastRequest")),
        S("51-skill-runtime-finalization.png", CodeVisualKind.Lifecycle, "Pipeline 结束不等于 Runtime 可以销毁", "子行为、取消原因和所有权必须在 Finalize 前完成收束", "把终结协议单独讲，才能看清泄漏与提前释放风险。", "代码依据：MobaSkillCastRuntimeService", I("Running", "runner 执行阶段", "Tick / Resume"), I("PipelineEnded", "记录结束原因", "MarkPipelineEnded"), I("WaitingChildren", "子行为仍持有", "PendingChildren"), I("Finalized", "统一释放与 Trace 收尾", "TryFinalize")) with { CenterLabel = "Owner 决定\n最终释放" },
        S("52-context-snapshot-trace-bridge.png", CodeVisualKind.Matrix, "Context、Snapshot、Trace 记录三种不同事实", "三者可以桥接，但不能互相代替资源所有权、并发协议或稳定业务序列化", "先分清问题，再选择记录模型。", "文档依据：Context Flow Snapshot Trace Bridge", I("Context", "一次执行携带的输入与来源", "registry / accessor", "执行事实"), I("Snapshot", "实体最新可恢复状态", "snapshot storage", "状态事实"), I("Trace", "父子因果与生命周期", "trace registry", "观测事实")) with { MatrixHeaders = new[] { "模型", "主要记录", "运行入口", "不能替代" } },
        S("53-session-control-data-ownership.png", CodeVisualKind.SplitFlow, "业务 Session 是会话资源的唯一所有者", "Room 控制面负责阶段；Battle 数据面负责每帧通信；业务 Session 串起 world、同步和恢复", "当前 Coordinator Package 不提供现役总装器。", "文档依据：会话协调 v3.0", I("Room Flow", "create / join / ready", "RoomGatewaySessionFlow"), I("Restore", "snapshot + next step", "RestoreAsync"), I("Control RPC", "ack / baseline", "Room connection"), I("World & Sync", "Tick / prediction / replay", "Business Session"), I("Input", "request / response", "Battle Handle"), I("State Push", "queue + main-thread drain", "Battle Data Plane")) with { LeftLabel = "控制面阶段", RightLabel = "业务会话与数据面", CenterLabel = "同一组会话身份" },
        S("54-evidence-levels.png", CodeVisualKind.Stack, "E0-E5 只说明证据覆盖到哪里", "每一级都必须写明对象、环境、日期和未覆盖范围", "证据等级不是成熟度徽章，也不能向上外推。", "文档依据：公司级采用与模块治理", I("E0", "源码或类型存在", "implementation"), I("E1", "存在真实消费者", "consumer"), I("E2", "目标构建或静态校验", "build / lint"), I("E3", "自动化契约或组件测试", "tests"), I("E4", "真实场景、Unity 或网络 Smoke", "scenario artifact"), I("E5", "持续自动门禁与可追溯结果", "workflow + artifact")),
        S("55-gate-config-workflow-evidence.png", CodeVisualKind.DataFlow, "Gate 声明只有跑通证据链才构成自动准入", "28 个配置项、Runner、手写 Workflow 和实际 Artifact 必须逐层对齐", "ciPolicy 或 job 名称存在都不能单独证明 E5。", "文档依据：正式测试流程", I("配置", "level / steps / ciPolicy", "test-gates.json"), I("执行", "命令 / 退出码 / 日志", "run_test_gate.ps1"), I("编排", "event / job / artifact upload", "abilitykit-test-gates.yml"), I("结果", "commit / environment / gaps", "gate-summary + trx")),
        S("56-runtime-artifact-evidence.png", CodeVisualKind.DataFlow, "Artifact 是离线证据容器，不是运行时状态源", "运行事实经过稳定投影、版本化 section 和导出器，才交给分析与门禁消费", "扩展 KeyValue 不是自动获得稳定类型语义。", "文档依据：Analysis Artifact 与运行时证据", I("Runtime Facts", "trace / diagnostics / profiler", "runtime producers"), I("Stable Projection", "schema + section version", "artifact DTO"), I("Export", "JSON / battle diagnostics", "exporter"), I("Consumers", "analysis / compare / gate", "offline tools")),
        S("57-company-problem-map.png", CodeVisualKind.Matrix, "战斗研发的公司成本来自五类问题叠加", "问题会从需求和开发一路传递到联调、上线与长期维护", "先识别问题出现在哪个阶段，才能选择边界、诊断、测试或治理机制。", "文档依据：序章 / 公司级采用与模块治理", I("需求", "规则来源与边界不清", "评估依赖个人经验", "多入口、多套术语"), I("开发", "稳定机制各项目重做", "重复支付设计与实现", "私有模块与补丁增长"), I("联调", "逻辑表现配置网络耦合", "改一处牵动多系统", "跨系统定位与返工"), I("上线", "联机回放和恢复后补", "风险后移且难回退", "Smoke 与回滚缺口"), I("维护", "问题难复现且知识随人", "修复无法跨项目回流", "手工复现、依赖原作者")) with { MatrixHeaders = new[] { "项目阶段", "常见问题", "公司成本", "可观察信号" } },
        S("58-problem-capability-benefit-map.png", CodeVisualKind.Matrix, "每类公司问题都有对应的框架机制", "AbilityKit 的价值要同时说明问题、机制、收益和证据", "没有证据的收益只是试点目标；验证后才能进入采用结论。", "文档依据：序章 / 测试流程 / 公司级采用", I("重复建设", "稳定模块与组合边界", "复用成熟机制", "重复实现数 / 首次可运行"), I("隐性耦合", "Framework / Reference / Project 分层", "变更停在更小边界", "改动面 / 回归面"), I("难以复现", "Context / Trace / Snapshot / Record", "更快复现与定位", "定位时间 / 最小复现"), I("质量后置", "Unit / Contract / Smoke / Gate", "更早拦截风险", "Gate 覆盖 / 失败层级"), I("升级失控", "Owner / Version / Artifact / Rollback", "采用与退出可控", "升级与回滚演练")) with { MatrixHeaders = new[] { "公司问题", "AbilityKit 机制", "直接收益", "验证证据" } },
        S("59-role-benefit-map.png", CodeVisualKind.Matrix, "不同岗位共享同一条运行证据链", "框架价值不只减少编码，也改善协作、测试、维护和采用决策", "同一份 Context、Trace、Gate 和 Artifact 让跨角色讨论基于事实。", "文档依据：测试流程 / 公司级采用与模块治理", I("玩法开发", "效果组合散落在特判", "复用稳定玩法原语", "配置、Pipeline、失败原因"), I("客户端", "逻辑与表现互相牵动", "事件与快照分层", "表现回归与会话边界"), I("服务器", "多端协议语义易漂移", "共享纯 C# 契约", "输入、快照、恢复 Smoke"), I("测试", "问题依赖手工场景", "可执行输入与分层门禁", "用例、Trace、Artifact"), I("技术管理", "成熟度和风险不可见", "Owner、版本与回滚治理", "采用记录与评审结论")) with { MatrixHeaders = new[] { "角色", "当前困难", "实际收益", "共同证据" } },
        S("60-lifecycle-cost-reduction.png", CodeVisualKind.Sequence, "AbilityKit 在项目生命周期减少重复工作", "不是承诺固定 ROI，而是让每个阶段都有可复用入口和可比较证据", "局部快速实现不再把成本推给联调、上线、维护和下一个项目。", "文档依据：序章 / MOBA 与 Shooter 工业化流程", I("需求", "用能力边界拆需求", "减少重复评估"), I("开发", "组合模块与项目 Adapter", "减少基础机制重做"), I("联调", "Context / Trace 定位链路", "缩小排查范围"), I("上线", "Gate / Artifact / Rollback", "控制变更风险"), I("维护", "缺陷转回归并随版本发布", "让后续项目共享保护")),
        S("61-benefit-measurement-loop.png", CodeVisualKind.Sequence, "用试点证据决定继续、调整或退出", "先记录现状基线，再在可旁路场景运行同口径比较", "指标用于形成决策，不用于预设结论或包装未经验证的收益。", "文档依据：公司级采用与模块治理", I("记录基线", "同一场景、环境与规模", "time / count / coverage"), I("选择试点", "边界清晰、可旁路、可回滚", "module + owner + version"), I("收集证据", "运行 Gate 并保存 Artifact", "commit + result + gaps"), I("同口径比较", "交付、定位、质量与升级", "baseline vs pilot"), I("形成决策", "继续采用 / 调整边界 / 回滚", "review conclusion")),
        S("62-abilitykit-training-title.png", CodeVisualKind.Title, "AbilityKit", "把战斗研发问题变成可复用、可验证、可治理的公司能力", "从公司问题出发，用框架机制形成收益，再用工程证据决定采用。", "公司内训 | 事实基线：2026-08-16", IC("问题", "", "", colors.Red), IC("机制", "", "", colors.Blue), IC("收益", "", "", colors.Green), IC("证据", "", "", colors.Cyan), IC("决策", "", "", colors.Amber)),
        S("63-company-problem-cascade.png", CodeVisualKind.Cascade, "战斗研发问题会沿交付链持续放大", "局部实现解决当下需求，却把成本推向联调、上线、维护和下一个项目", "真正需要治理的不是代码数量，而是问题跨阶段重复付费。", "文档依据：序章 / MOBA 与 Shooter 工业化流程", IC("需求变化", "规则持续增加", "", colors.Amber), IC("项目补丁", "先满足当前交付", "", colors.Amber), IC("隐性耦合", "逻辑表现配置网络互相牵动", "", colors.Red), IC("联调返工", "定位跨越多个系统", "", colors.Red), IC("维护依赖", "修复与知识停留在单项目", "", colors.Red)),
        S("64-role-evidence-chain.png", CodeVisualKind.RoleChain, "不同岗位通过同一条证据链协作", "统一术语、运行事实和门禁后，问题不再依赖口头转译", "同一问题从需求、运行、复现、回归到采用决策都引用同一份事实。", "文档依据：测试流程 / 公司级采用与模块治理", IC("玩法研发", "组合稳定玩法原语", "Pipeline / Trigger", colors.Blue), IC("客户端", "从事件与快照消费结果", "Presentation", colors.Cyan), IC("服务器", "复用协议与会话契约", "Session / Sync", colors.Green), IC("测试", "把故障转成可执行输入", "Record / Gate", colors.Purple), IC("技术管理", "用证据决定采用与退出", "Owner / Version", colors.Amber)),
        S("65-company-reuse-loop.png", CodeVisualKind.FeedbackLoop, "公司级复用必须形成可追溯闭环", "一次项目问题只有经过复现、修复、验证和发布，才能保护后续项目", "复制代码不会产生复利；可追溯的修复、证据和版本才会。", "文档依据：公司级采用与模块治理", IC("项目问题", "真实场景暴露风险", "", colors.Red), IC("最小复现", "输入、环境与版本", "", colors.Amber), IC("框架修复", "公共契约或实现", "", colors.Blue), IC("门禁与 Artifact", "回归和未覆盖项", "", colors.Cyan), IC("版本发布", "项目升级获得保护", "", colors.Green)) with { CenterLabel = "公共能力\n可追溯闭环" },
        S("66-continuous-runtime-lifecycle.png", CodeVisualKind.FlatLifecycle, "Continuous 只拥有跨帧执行生命周期", "公共机制负责注册、推进、暂停恢复与释放，具体行为语义仍由项目定义", "DOT、光环、蓄力和区域效果共享生命周期，但不共享唯一业务编排。", "文档依据：Continuous Framework Design", IC("Register", "建立 Owner 与 Tag 规则", "", colors.Blue), IC("Tick", "按统一时间源推进", "", colors.Green), IC("Pause / Resume", "门控、恢复与失败补偿", "", colors.Amber), IC("Unregister", "解绑行为并由 Owner 释放", "", colors.Purple)),
        S("67-prediction-reconcile-recovery.png", CodeVisualKind.Responsibility, "预测、对账和恢复是三层独立责任", "现役能力由同步包与业务 Session 组合，不依赖历史 Hybrid 总开关", "拆开三层责任后，延迟体验、权威一致性和失败恢复才能分别验证。", "文档依据：Prediction Reconciliation / Session Coordination", IC("Prediction", "记录本地输入并生成临时状态，优先解决响应延迟。", "local input + predicted state", colors.Blue), IC("Reconcile", "接收权威快照或 hash，识别首个状态分歧。", "authoritative compare", colors.Amber), IC("Recovery", "恢复基线并重演输入，必要时降级或重建会话。", "rollback / replay / rebuild", colors.Green)),
        S("68-moba-shooter-evidence-split.png", CodeVisualKind.SplitEvidence, "MOBA 与 Shooter 分别暴露两类公司级风险", "一个验证复杂玩法组合，一个验证同步、承载与恢复边界", "当前只陈述可追溯职责；真实画面必须带日期、环境、配置和对应 Artifact。", "文档依据：示例工业化流程 / PPT 图片选择规范", IC("技能与效果链", "Skill / Trigger / Buff / Damage", "", colors.Amber), IC("生命周期所有权", "子行为、取消、Trace 收尾", "", colors.Red), IC("复杂度证据", "Console / Unity / DSL / tests", "", colors.Green), IC("同步策略", "Prediction / Snapshot / StateSync", "", colors.Blue), IC("承载与恢复", "Gateway / Room / Battle / reconnect", "", colors.Cyan), IC("闭环证据", "Harness / multiprocess smoke / replay", "", colors.Purple)) with { LeftLabel = "MOBA：复杂玩法组合", RightLabel = "Shooter：同步与承载" },
        S("69-adoption-review-contract.png", CodeVisualKind.ReviewContract, "采用评审必须同时具备四项契约", "任何一项缺失，都不能把试点结论升级为组织承诺", "评审不是判断“喜不喜欢框架”，而是判断采用是否有责任、有证据、可退出。", "文档依据：公司级采用与模块治理", IC("Owner", "谁维护、审批、接入和替补", "module / project / release owner", colors.Blue), IC("Version", "使用哪个制品、版本和兼容矩阵", "package + changelog", colors.Purple), IC("Evidence", "实际 Gate、commit、结果与未覆盖项", "artifact + environment", colors.Green), IC("Rollback", "开关、旧版本、数据恢复与退出负责人", "rehearsal + exit condition", colors.Amber)) with { CenterLabel = "采用\n评审" },
        S("70-reuse-admission-funnel.png", CodeVisualKind.Funnel, "代码只有通过四层筛选才值得进入公共框架", "相似实现不等于稳定契约，第二场景和独立验证缺一不可", "未通过筛选的代码保留在 Project 或 Reference，比过早上移更可维护。", "文档依据：文档治理路线图 / 公司级采用", IC("稳定语义", "不随单个英雄或房间规则频繁变化", "", colors.Blue), IC("跨项目共性", "至少第二类非同构场景证明价值", "", colors.Cyan), IC("可独立验证", "脱离项目仍能构建、测试和复现", "", colors.Green), IC("可扩展边界", "项目差异通过接口、配置或 Adapter 注入", "", colors.Amber)) with { CenterLabel = "公共框架候选" },
        S("71-pilot-decision-path.png", CodeVisualKind.DecisionPath, "内训最后要形成可执行的试点决策", "把范围、责任、证据和退出方式写清楚，再决定继续、调整或回滚", "没有被记录的决定不会自动变成项目计划。", "文档依据：公司级采用与模块治理", IC("试点范围", "模块与业务场景", "", colors.Blue), IC("责任与版本", "接入边界与目标制品", "", colors.Purple), IC("门禁与证据", "最低证据和未覆盖项", "", colors.Green), IC("退出与回流", "退出条件与公共回流", "", colors.Amber)) with { CenterLabel = "评审\n结论" },
        S("72-project-role-value-chain.png", CodeVisualKind.ProjectRole, "AbilityKit 把项目规则连接到可验证交付", "项目保留业务决策，框架提供从运行机制到多端验证的稳定工程链", "AbilityKit 不替项目决定英雄、房间和体验；它让这些规则沿同一条可运行、可测试、可发布链落地。", "文档依据：AbilityKit 能力地图 / 序章", IC("规则执行", "Pipeline / Trigger", "Skill + Buff", colors.Blue), IC("状态模拟", "ECS / 属性 / 伤害", "Simulation", colors.Cyan), IC("同步承载", "FrameSync / Session", "Sync + Host", colors.Green), IC("验证发布", "Record / Gate", "Artifact + Version", colors.Purple)) with { LeftLabel = "项目输入", RightLabel = "可验证交付", CenterLabel = "Foundation + Engineering：统一生命周期、稳定 ID、UPM + .NET 与多端构建" },
        S("73-framework-reference-project-ownership.png", CodeVisualKind.OwnershipBands, "框架、参考实现和项目各自拥有不同决策", "稳定契约向上支撑业务；项目差异通过配置、Adapter 和缺陷反馈向下回流", "边界清楚后，项目既能复用公共能力，也不会被 Demo 的组织方式反向接管。", "代码依据：AbilityKit.Demo.Common.Unity / Framework packages", IC("Project", "英雄 / 房间 / 结算 / 体验 / 预算", "业务最终所有", colors.Amber), IC("Reference", "Profile → Catalog → Bootstrap", "验证 MOBA / Shooter 组合，不定义唯一应用层", colors.Cyan), IC("Framework", "Pipeline / Trigger / Combat / Sync / Host / Gate", "跨项目稳定契约", colors.Blue)),
        S("74-project-scenario-capability-paths.png", CodeVisualKind.ScenarioPaths, "不同项目只接入解决当前问题的能力", "从项目场景和风险出发选择组合，不以包覆盖率作为目标", "项目可以停在任意组合；Demo Packages 用于学习和验收，不是默认业务应用层。", "文档依据：AbilityKit 能力地图", IC("纯逻辑\n单机", "Foundation + SkillCore", "稳定技能、Buff 与规则执行", colors.Purple, "Unit + Trace"), IC("复杂\n战斗", "+ BattleRuntime", "统一目标、投射物与伤害链", colors.Green, "Contract + Smoke"), IC("多人\n联机", "+ Sync + Server", "同步、回放、重连与权威承载", colors.Cyan, "Replay + Artifact")),
        S("75-project-issue-routing.png", CodeVisualKind.IssueRouting, "项目故障可以沿稳定边界找到责任人和证据", "同一故障不再在玩法、客户端、网络和测试之间反复口头转述", "模块边界的项目作用，是让现象、入口、Owner 和回归证据形成同一条处理路径。", "文档依据：核心概念 / 测试流程 / 会话协调", IC("技能不释放", "Skill / Trigger", "输入相位 + Trace", colors.Blue, "玩法负责人"), IC("表现不同步", "Event / Snapshot", "表现回归 + Snapshot", colors.Purple, "客户端负责人"), IC("状态发生分歧", "Sync / Record", "输入帧 + Replay", colors.Cyan, "网络负责人"), IC("房间或恢复失败", "Session / Host", "Smoke + Artifact", colors.Amber, "服务端负责人")),
        S("76-evidence-led-adoption-stages.png", CodeVisualKind.AdoptionStages, "每扩大一次接入面，都必须增加一层证据", "从可旁路模块开始，逐步扩到战斗链和多人承载，每一阶段都能独立退出", "当前一阶段没有形成可复核证据，就不扩大下一层接入面。", "文档依据：公司级采用与模块治理", IC("1 旁路试点", "Targeting / Pipeline", "Unit + Contract", colors.Green, "保留旧路径与切换开关"), IC("2 核心战斗链", "Trigger / Combat", "Smoke + Artifact", colors.Blue, "明确版本与回滚入口"), IC("3 多人承载", "Sync / Session / Host", "Replay + Multiprocess", colors.Amber, "完成重连、恢复与回滚演练")),
        S("77-adoption-decision-coordinates.png", CodeVisualKind.AdoptionCoordinates, "采用判断不是一个成熟度标签", "四个坐标分别约束资产定位、组织承诺、工程事实和版本退出", "收益：项目不会把接口存在误判成可大规模采用，也能在证据不足时保留退出路径。", "文档依据：公司级采用与模块治理", IC("资产类型", "Foundation / Domain / Adapter\nReference / Validation", "定义复用边界", colors.Blue, "它被设计成什么？"), IC("组织成熟度", "Experimental / Pilot\nSupported / Recommended", "定义采用范围", colors.Purple, "允许谁在什么条件下采用？"), IC("工程证据", "E0-E5 + 对象 / 环境 / 日期 / 缺口", "定义事实覆盖", colors.Green, "当前真正证明到哪里？"), IC("发布状态", "Version / Artifact / Owner / Rollback", "定义获取与退出", colors.Amber, "怎样升级、负责和退出？")) with { CenterLabel = "采用决策\n范围 · Owner · 退出" },
        S("78-skill-execution-runtime-map.png", CodeVisualKind.SkillExecutionRuntime, "一次技能释放是一条可控制的运行链", "主线之外必须同时看见暂停、失败和 Trace 输出，才能解释 Runtime 的项目价值", "收益：失败能定位、长行为能暂停恢复、子行为能追踪，技能不再膨胀成不可观察的 Cast 函数。", "文档依据：Skill System Architecture / MOBA Skill Execution", IC("输入与准备", "slot / config / target / aim", "Prepare / reject", colors.Blue), IC("创建 Runtime", "handle / blackboard / trace root", "MobaSkillCastRuntime", colors.Cyan), IC("Pipeline 执行", "phase / interrupt / pause", "PreCast / Cast", colors.Green), IC("子行为", "Trigger / Buff\nProjectile / Damage", "child retain", colors.Amber), IC("输出", "Event / Snapshot\nTrace / end reason", "observable result", colors.Purple)),
        S("79-skill-finalization-gate.png", CodeVisualKind.FinalizationGate, "Pipeline 结束后还要通过终结闸门", "PendingChildren、结束原因和 Owner 释放共同决定 Runtime 何时真正销毁", "收益：避免异步子行为仍在运行时提前释放，也避免子行为结束后 Runtime、Trace 或绑定长期泄漏。", "代码依据：MobaSkillCastRuntimeService.TryFinalize", IC("Running", "Runner 正在推进阶段", "Tick / Resume", colors.Blue), IC("PipelineEnded", "只记录阶段结束与原因", "MarkPipelineEnded", colors.Amber), IC("WaitingChildren", "子行为仍持有父 Runtime", "PendingChildren", colors.Cyan), IC("Finalized", "统一释放绑定并收尾 Trace", "Owner release", colors.Green)),
        S("80-context-snapshot-trace-responsibilities.png", CodeVisualKind.FactModelBridge, "三种事实模型由领域服务显式桥接", "Context、Snapshot 和 Trace 共享稳定身份，但各自保存不同事实并拥有独立生命周期", "收益：复现、恢复和诊断可以组合，同时避免把记录模型误当成资源所有权或并发协议。", "文档依据：Context Flow Snapshot Trace Bridge / Trace Lifecycle", IC("Context", "执行输入、来源与 provenance", "registry / accessor", colors.Blue, "不能替代 Runtime 资源所有权"), IC("Snapshot", "实体当前可恢复的最新状态", "snapshot storage", colors.Green, "不能替代并发控制或完整历史"), IC("Trace", "父子因果与执行生命周期", "trace registry", colors.Purple, "导出不等于稳定业务序列化")),
        S("81-buff-lifecycle-ownership.png", CodeVisualKind.BuffOwnershipLifecycle, "Buff 的风险集中在提交后的所有权和收尾", "入口只是排队；真正的正确性来自 Runtime 绑定、结束顺序和明确的事务边界", "收益：统一结束顺序可以减少持续行为、Trigger、Modifier 和技能 retain 残留，并让失败边界可测试。", "文档依据：Buff System / MOBA Buff Lifecycle Deep Dive", IC("申请 / 刷新", "校验入队\n刷新 / 叠层 / 新建", "DrainPending", colors.Blue), IC("Active Runtime", "列表已提交\n持有跨模块绑定", "BuffRuntimeKey", colors.Green), IC("Remove / Expire", "显式移除 / 标签中断 / 到期", "BuffEndFlow", colors.Amber), IC("Recycled", "回池后不可再读取", "runtime pool", colors.Purple)),
        S("82-projectile-lifecycle-boundaries.png", CodeVisualKind.ProjectileBoundaryLifecycle, "Projectile 核心管理生命周期，项目管理命中语义", "World Tick、HitPolicy、退出原因和核心快照可复用；伤害、Buff、阵营与表现由项目接管", "收益：项目复用稳定飞行与命中底座，不必把业务效果塞进投射物核心，也能明确回滚需要补哪些项目状态。", "文档依据：Projectile System v3.0", IC("Spawn / Schedule", "生成参数 / 发射模式 / 来源", "ProjectileService", colors.Blue), IC("World Tick", "固定帧 / raycast / 冷却 / 去重", "ProjectileWorld", colors.Cyan), IC("Hit / Exit", "继续 / 穿透 / 返回 / 退出", "HitPolicy / ExitReason", colors.Amber), IC("Recycle", "输出事件后回收核心对象", "pool / lifecycle", colors.Green)),
        S("83-sync-selection-questions.png", CodeVisualKind.SyncDecisionQuestions, "同步选型先形成项目契约，再组合算法", "权威、同步数据和恢复体验是三个前置问题，Profile 只负责协商最终选择", "收益：避免用 PredictRollback 等算法名代替服务端模板、客户端消费和恢复责任，减少联机集成歧义。", "文档依据：Synchronization Capability Map v3.0", IC("谁权威", "客户端预测、服务端权威，还是纯本地逻辑？", "authoritative source / accepted frame", colors.Blue), IC("同步什么", "输入、状态槽、Snapshot、hash 或事件？", "frame envelope / payload", colors.Cyan), IC("怎样恢复", "确认帧、基线、重演、重连和降级体验如何组合？", "baseline / replay / restore", colors.Amber), IC("Sync Contract", "服务端模板 + Profile + 业务 Session", "commit 后生效", colors.Green)),
        S("84-logic-to-presentation-projection.png", CodeVisualKind.PresentationProjection, "表现层消费逻辑事实，但不拥有战斗真相", "一次性反馈走事件，连续状态走 Snapshot；Adapter 与 Sink 把它们投影到平台对象", "收益：逻辑与同步保持可测试、可回放，Unity / Console / ET 可以各自实现表现而不复制战斗规则。", "文档依据：View Event Abstraction / Snapshot Dispatch", IC("Logic State", "权威世界负责规则、状态与确定性 Tick", "Battle Logic World", colors.Blue), IC("Presentation Event", "伤害数字 / Cue / 命中\n一次性反馈", "TriggerEvent", colors.Amber), IC("Snapshot", "位置 / 实体 / 连续状态\n强类型投影", "FrameSnapshotDispatcher", colors.Cyan), IC("Adapter / Sink", "订阅 / 解码 / 分类\n管理订阅生命周期", "project View Runtime", colors.Purple), IC("Platform View", "GameObject / VFX\n浮字 / 区域 / 插值", "Unity / Console / ET", colors.Green)),
        S("85-framework-capability-operating-model.png", CodeVisualKind.CapabilityOperatingModel, "当前能力覆盖规则执行、多人承载与工程验收", "六类能力沿同一条项目运行路径协作，项目仍保留英雄、房间、体验与预算决策", "AbilityKit 的作用不是提供一套固定玩法，而是让项目规则通过稳定生命周期、事实模型、承载契约和工程证据落地。", "文档依据：AbilityKit Capability Map v3.0", IC("公共底座", "Core / Stable ID / Event / Pool / World.DI", "Foundation", colors.DarkLine), IC("运行编排", "Pipeline / Flow / HFSM\n生命周期 / 所有权", "Composition", colors.Blue), IC("玩法原语", "Trigger / Ability / Buff\nTarget / Projectile / Damage", "Gameplay", colors.Green), IC("逻辑与同步", "ECS / Snapshot / Prediction\nRollback / Record / Replay", "Simulation / Sync", colors.Cyan), IC("承载与服务端", "Host / Protocol / Room\nBattle Host / Orleans", "Shell / Server", colors.Amber), IC("工程与证据", "Config / Codegen / Tests / Smoke / Artifact / Gates / Docs", "Engineering", colors.Purple)),
        S("86-project-problem-capability-paths.png", CodeVisualKind.ProblemCapabilityPaths, "项目从实际问题选择能力，不以全量接入为目标", "每条路径都要同时说明使用哪些现役能力、解决什么问题，以及如何形成可复核证据", "能力组合按风险扩大：先把本地战斗链跑通并留证，再进入同步、房间和服务端承载。", "文档依据：AbilityKit / Gameplay / Synchronization / Server Capability Maps", IC("技能规则散在业务 if / else", "Pipeline / Flow / HFSM\nTriggering / Ability / Attributes", "阶段、条件、失败和生命周期统一", colors.Blue, "Unit + Trace"), IC("Buff、目标、投射物和伤害互相耦合", "Targeting / Buff / Continuous\nProjectile / Damage / Motion", "子行为拥有明确边界、顺序与收尾", colors.Green, "Contract + Smoke"), IC("多人状态分歧、恢复和承载难验收", "FrameSync / Snapshot / Prediction\nRollback / Record + Host / Room", "权威事实可恢复、回放并多端消费", colors.Cyan, "Replay + Multiprocess\nArtifact"))
    };
}

void WriteMermaidFiles()
{
    var files = new Dictionary<string, string>
    {
        ["01-abilitykit-architecture-layers.mmd"] = """
flowchart TB
    A[Engineering<br/>一份源码 / UPM + .NET / 多端构建] --> B[Foundation<br/>Core / World.DI / Event / Stable ID]
    B --> C[Runtime Shell<br/>Host / Protocol / Room Flow / Network SDK]
    C --> D[Simulation<br/>ECS / FrameSync / StateSync / Record]
    D --> E[Gameplay<br/>Trigger / Ability / Attributes / Combat]
    E --> F[Example and Server<br/>Console / ET / MOBA / Shooter / Orleans]
""",
        ["02-abilitykit-capability-map.mmd"] = """
flowchart LR
    F[Foundation<br/>Core + World.DI] --> S[SkillCore<br/>Pipeline + Triggering + Ability + Attributes]
    S --> B[BattleRuntime<br/>Targeting + Projectile + Damage + Motion]
    B --> SY[SyncRuntime<br/>FrameSync + Snapshot + StateSync + Record]
    SY --> SR[ServerRuntime<br/>Host + Protocol + Room + Orleans Adapter]
    SR -. 组合名称不是预制应用层 .-> P[项目仍拥有英雄、房间、结算、体验和预算]
""",
        ["03-skill-cast-main-flow.mmd"] = """
flowchart LR
    Input[输入<br/>玩家 / AI / 脚本 / 网络] --> Validate[校验<br/>冷却 / 资源 / 目标 / 状态]
    Validate --> Pipeline[管线编排<br/>阶段 / 延迟 / 并行 / 中断]
    Pipeline --> Effect[效果执行<br/>伤害 / Buff / 位移 / 投射物]
    Effect --> Trigger[事件触发<br/>Hit / Damage / Death / BuffChanged]
    Trigger --> Output[输出<br/>表现事件 / Trace / Snapshot / 断言]
""",
        ["04-moba-runtime-and-dsl-flow.mmd"] = """
flowchart LR
    subgraph Runtime[MOBA 运行时启动链]
      A[WorldTypeRegistry] --> B[Blueprint / Module] --> C[WorldInitData] --> D[EntitasWorld] --> E[System Install] --> F[Tick Execute]
    end
    subgraph DSL[DSL / 脚本场景]
      S[BattleTestScript] --> T[Move / Skill / Wait] --> U[Console Driver]
      T --> V[View Runtime Driver]
      U --> W[Trace / Snapshot]
      V --> W
      W --> X[Smoke Assertion]
    end
    DSL --> Runtime
""",
        ["05-shooter-sync-matrix.mmd"] = """
flowchart TB
    H[DemoHarness Matrix] --> A[PredictRollback]
    H --> B[AuthoritativeInterpolation]
    H --> C[BatchStateSync]
    H --> D[MassBattleLodSync]
    H --> E[HybridHeroPrediction]
    A --> V[启动 / 收敛 / Snapshot / 协议 / 回滚 / 重连]
    B --> V
    C --> V
    D --> V
    E --> V
""",
        ["06-test-gates-ci-pyramid.mmd"] = """
flowchart LR
    subgraph Gate[P0 P1 P2 风险层级]
      P0[P0<br/>precheck / build / test / smoke] --> P1[P1<br/>contracts / Unity EditMode / sync]
      P1 --> P2[P2<br/>batch regression / release candidate]
    end
    Gate --> Config[test-gates.json<br/>28 个配置项]
    Config --> Runner[run_test_gate.ps1]
    Runner --> CI[workflow 手写覆盖部分 job]
    CI --> Result[commit + result + artifact]
""",
        ["12b-coordinator-adapter-maturity.mmd"] = """
flowchart TB
    S[Business Session<br/>world / connections / Tick / recovery] --> R[Room Flow<br/>create / join / ready / restore]
    S --> Y[Sync Runtime<br/>profile / prediction / snapshot / replay]
    S --> B[Battle Data Plane<br/>input / state push / ack]
    H[历史实现当前不存在] -.-> X[SessionCoordinator / Local / Remote / Hybrid adapters]
""",
        ["07-company-reuse-feedback-loop.mmd"] = """
flowchart LR
    A[项目 A] --> K[AbilityKit 公共战斗能力]
    B[项目 B] --> K
    C[项目 C] --> K
    K --> Fix[模块修复]
    K --> Spec[规范更新]
    K --> Test[测试补充]
    K --> Doc[文档沉淀]
    Fix --> A
    Fix --> B
    Fix --> C
""",
        ["08-graph-component-selection.mmd"] = """
flowchart TB
    Q{业务主语是什么}
    Q -->|一次能力经历哪些阶段| P[Pipeline<br/>phase / run / interrupt]
    Q -->|实体现在是什么状态| H[HFSM<br/>state / transition / exit]
    Q -->|一串任务如何完成| F[Flow<br/>task / cancel / cleanup]
    Q -->|AI 当前选哪个行为| BT[BehaviorTree<br/>selector / sequence / tick]
""",
        ["09-moba-skill-runtime-lifecycle.mmd"] = """
flowchart LR
    Input[输入请求] --> Prep[SkillCastPreparation<br/>上下文 + trace root]
    Prep --> Runtime[MobaSkillCastRuntime<br/>handle / blackboard]
    Runtime --> Pipeline[SkillPipelineRunner<br/>PreCast / Cast]
    Pipeline --> Child[trigger / projectile / buff / damage]
    Child --> End[complete / cancel / children cleanup]
""",
        ["10-moba-trigger-context-trace-flow.mmd"] = """
flowchart LR
    Source[触发源<br/>Skill / Buff / Projectile / Area] --> Gateway[MobaTriggerExecutionGateway]
    Gateway --> Service[MobaEffectExecutionService]
    Service --> Context[MobaCombatExecutionContext<br/>payload / lineage / origin / snapshot]
    Context --> Trace[Trace Scope<br/>root / child]
    Trace --> Executor[MobaTriggerPlanExecutor<br/>Action / Function / EventBus]
""",
        ["11-moba-buff-lifecycle.mmd"] = """
flowchart LR
    Apply[Apply<br/>申请 / 刷新 / 叠层 / 替换] --> Runtime[BuffRuntime<br/>key / source]
    Runtime --> Binding[Binding<br/>continuous / trigger owner / trace]
    Binding --> Notify[Notify<br/>事件 / 表现 / stage effect]
    Notify --> End[End<br/>remove / expire / interrupt / replace]
    End --> Verify[Verify<br/>配置校验 / smoke / test]
""",
        ["12-shooter-pure-csharp-projection.mmd"] = """
flowchart LR
    Runtime[Shooter Runtime<br/>确定性玩法] --> Sync[Sync Controller<br/>PredictRollback / AuthInterp]
    Sync --> Snapshot[ShooterStateSnapshotPayload]
    Snapshot --> Projection[ShooterSnapshotViewProjection<br/>batch -> store]
    Projection --> Unity[Unity Shell<br/>Session.Tick / Render Sink]
""",
        ["13-demoharness-three-axis.mmd"] = """
flowchart TB
    A[A 同步能力档案] --> Runner[DemoHarness Runner]
    B[B 网络环境] --> Runner
    C[C 演示载体] --> Runner
    Runner --> Matrix[可运行矩阵<br/>Completed / Degraded / Failed / Unsupported]
""",
        ["14-client-flow-boundaries.mmd"] = """
flowchart TB
    HFSM[HFSM<br/>状态规划 / transition 条件]
    Flow[AbilityKit.Flow<br/>可等待动作 / 取消失败清理]
    Client[Client Flow<br/>state lifecycle -> feature assembly]
    Modules[Modules<br/>feature 内部 attach / detach / tick]
    Presentation[Presentation<br/>snapshot -> batch -> view adapter]
    HFSM --> Client
    Flow --> Client
    Client --> Modules
    Modules --> Presentation
""",
        ["15-targeting-query-chain.mmd"] = """
flowchart LR
    Spec[Query Spec<br/>阵营 / 半径 / 形状 / origin] --> Search[Spatial Search<br/>候选收集]
    Search --> Filter[Filter<br/>阵营 / 状态 / 标签 / 可见性]
    Filter --> Score[Score & Sort<br/>距离 / 角度 / 威胁 / 权重]
    Score --> Select[Select<br/>single / topN / random / nearest]
    Select --> Result[Result<br/>cache / trace / assertion]
""",
        ["16-projectile-lifecycle.mmd"] = """
flowchart LR
    Launch[Launch<br/>source context / skill runtime] --> Runtime[Projectile Runtime<br/>速度 / 轨迹 / owner / lifetime]
    Runtime --> Collision[Collision<br/>hit test / 穿透 / 阻挡]
    Collision --> Hit[Hit Trigger<br/>ProjectileHitArgs / 触发计划]
    Hit --> Area[Area Effect<br/>爆炸 / 范围 / 二次查询]
    Area --> Recycle[Recycle<br/>release child / pool / trace]
""",
        ["17-damage-pipeline.mmd"] = """
flowchart LR
    subgraph Kernel[通用内核 DamageCalculationPipeline]
      Validate --> CriticalBase[Critical / Base] --> BonusResist[Bonus / Resist<br/>typed DamageSlots] --> Final[Final / Overkill]
    end
    subgraph Moba[MOBA 参考实现 DamagePipelineService]
      Stage[Stage Events] --> Apply[Apply shield / health] --> Derived[Derived Trigger] --> Trace[Trace Child]
    end
    Final -->|DamageResult| Stage
""",
        ["18-attributes-modifier-stack.mmd"] = """
flowchart LR
    Base[Base<br/>等级 / 配置 / 成长曲线] --> Add[Add<br/>装备 / Buff flat / 临时加值]
    Add --> Multiply[Multiply<br/>百分比 / 乘区策略 / 上下限]
    Multiply --> Dirty[Dirty<br/>版本号 / 延迟重算 / 依赖传播]
    Dirty --> Snapshot[Snapshot<br/>表现 / 同步 / 测试断言]
""",
        ["19-record-replay-debug-flow.mmd"] = """
flowchart LR
    Input[Input Track] --> Source[FrameRecordReplaySource]
    Snapshot[Snapshot Track] --> Source
    Hash[State Hash Track] --> Source
    Source --> Min[完整 / 最小 replay] --> Headless[input-state / input-logic]
    Headless --> Compare[hash / opcode / snapshot] --> Regression[Regression Gate]
""",
        ["20-battlehost-lifecycle.mmd"] = """
flowchart TB
    Host[BattleLogicHostGrain] --> Life[Initialize / Input Schedule / Tick / Full-Delta Push / Late Join / Destroy]
    Host --> Shooter[Shooter Adapter<br/>动态加入 / AI / Interest / Diagnostics 已实现]
    Host --> Moba[MOBA Adapter<br/>Start / Input / Tick / Snapshot 已实现<br/>其余能力 Unsupported 或未接入]
""",
        ["21-config-validation-pipeline.mmd"] = """
flowchart LR
    Resource[资源接入<br/>IResourceProvider / JsonConfigProvider] --> Manifest[显式模块清单<br/>Action / Schema registration]
    Manifest --> Validation[运行时校验<br/>required contract / startup block / history]
    Retired[AutoPlanAction Source Generator] -. Retired .-> Manifest
    Validation -. MOBA 参考实现 .-> Moba[Gameplay Validation]
""",
        ["22-gc-hot-path-governance.mmd"] = """
flowchart LR
    Find[Find<br/>Profiler / tests / allocation sample] --> Classify[Classify<br/>log / boxing / array copy / LINQ]
    Classify --> Guard[Guard<br/>debug switch / validation mode]
    Guard --> Refactor[Refactor<br/>pool / span / cache / struct]
    Refactor --> Benchmark[Benchmark<br/>stress case / baseline diff]
    Benchmark --> Gate[Gate<br/>threshold / nightly report]
"""
    };

    foreach (var spec in CodeVisualSpecs())
    {
        files[Path.ChangeExtension(spec.FileName, ".mmd")] = ToMermaid(spec);
    }

    foreach (var pair in files)
        File.WriteAllText(Path.Combine(outputDir, pair.Key), pair.Value, new UTF8Encoding(false));
}

void WriteIndex()
{
    var index = """
# AbilityKit PPT 图表资产

本目录由 `tools/AbilityKitPptAssetGenerator` 生成。

## PNG

1. `01-abilitykit-architecture-layers.png`：六层能力架构图。
2. `02-abilitykit-capability-map.png`：按问题域选择能力组合。
3. `03-skill-cast-main-flow.png`：技能释放主链路。
4. `04-moba-runtime-and-dsl-flow.png`：MOBA 启动链与 DSL 场景。
5. `05-shooter-sync-matrix.png`：Shooter 同步能力矩阵。
6. `06-test-gates-ci-pyramid.png`：P0/P1/P2 风险层级与证据链。
7. `07-company-reuse-feedback-loop.png`：公司级复用闭环。
8. `08-graph-component-selection.png`：Pipeline / HFSM / Flow / BehaviorTree 选型。
9. `09-moba-skill-runtime-lifecycle.png`：MOBA 技能 Runtime 生命周期。
10. `10-moba-trigger-context-trace-flow.png`：MOBA 触发执行与上下文溯源。
11. `11-moba-buff-lifecycle.png`：MOBA Buff 生命周期正式化。
12. `12-shooter-pure-csharp-projection.png`：Shooter 纯 C# 到 Unity 表现投影。
12B. `12b-coordinator-adapter-maturity.png`：业务 Session 所有权与历史 Coordinator 实现边界。
13. `13-demoharness-three-axis.png`：DemoHarness 三轴正交模型。
14. `14-client-flow-boundaries.png`：Client Flow 与表现边界。
15. `15-targeting-query-chain.png`：Targeting 查询链路。
16. `16-projectile-lifecycle.png`：Projectile 生命周期。
17. `17-damage-pipeline.png`：Damage 通用计算内核与 MOBA 应用编排边界。
18. `18-attributes-modifier-stack.png`：Attributes 修饰器栈。
19. `19-record-replay-debug-flow.png`：FrameRecord 三轨记录与可执行回归。
20. `20-battlehost-lifecycle.png`：Orleans BattleHost 与玩法适配成熟度。
21. `21-config-validation-pipeline.png`：资源接入、显式模块清单与运行时校验边界。
22. `22-gc-hot-path-governance.png`：GC / 性能热路径治理。
23. `23-company-framework-title.png`：AbilityKit 真实代码资产栈。
24. `24-fragmented-combat-systems.png`：单项目分散实现到统一触发链路。
25. `25-local-optimum-company-cost.png`：代码层面的公司成本矩阵。
26. `26-shared-framework-value.png`：一次修复跨项目受益链路。
27. `27-abilitykit-positioning.png`：纯逻辑能力与项目适配边界。
28. `28-abilitykit-non-goals.png`：从代码结构排除框架误解。
29. `29-company-assets-map.png`：公司级资产分层栈。
30. `30-composable-adoption-model.png`：按模块渐进接入链路。
31. `31-module-boundary-collaboration.png`：模块边界与协作定位矩阵。
32. `32-maintainability-handover.png`：换人维护闭环。
33. `33-pipeline-phase-composition.png`：真实技能释放调用链。
34. `34-triggering-rule-system.png`：触发计划执行链路。
35. `35-sync-risk-framework.png`：同步输入、快照路由与验收链路。
36. `36-sample-dual-validation.png`：MOBA / Shooter 示例验证职责矩阵。
37. `37-shooter-validation-showcase.png`：Shooter DemoHarness 与真实 TCP / 多进程双层验收。
38. `38-sample-as-best-practice.png`：示例工程反向驱动框架链路。
39. `39-framework-test-necessity.png`：框架测试分层栈。
40. `40-test-assets-compound.png`：Bug 沉淀成测试资产闭环。
41. `41-unified-process-handover.png`：统一流程降低交接成本链路。
42. `42-adoption-by-project-scale.png`：按项目规模选择接入面矩阵。
43. `43-internal-rollout-roadmap.png`：公司内部推进代码路线。
44. `44-reuse-worthiness-filter.png`：代码进入框架的筛选矩阵。
45. `45-framework-risk-controls.png`：框架落地风险与工程控制点。
46. `46-company-benefit-summary.png`：团队收益对应代码资产栈。
47. `47-discussion-decision-map.png`：下一步讨论的代码决策矩阵。
48. `48-framework-reference-project-boundary.png`：框架契约、参考实现与项目策略边界。
49. `49-adoption-evidence-release-model.png`：资产类型、成熟度、证据与发布四坐标。
50. `50-skill-cast-preparation.png`：技能施法准备链路。
51. `51-skill-runtime-finalization.png`：技能 Runtime 终结协议。
52. `52-context-snapshot-trace-bridge.png`：Context、Snapshot 与 Trace 职责边界。
53. `53-session-control-data-ownership.png`：会话控制面、数据面与业务所有权。
54. `54-evidence-levels.png`：E0-E5 工程证据层级。
55. `55-gate-config-workflow-evidence.png`：Gate 配置、执行、编排和结果证据链。
56. `56-runtime-artifact-evidence.png`：运行时事实到离线 Artifact 的投影链。
57. `57-company-problem-map.png`：项目生命周期中的公司问题与可观察信号。
58. `58-problem-capability-benefit-map.png`：公司问题、框架机制、直接收益与验证证据映射。
59. `59-role-benefit-map.png`：玩法、客户端、服务器、测试和技术管理的实际收益。
60. `60-lifecycle-cost-reduction.png`：AbilityKit 在需求、开发、联调、上线和维护阶段减少重复工作。
61. `61-benefit-measurement-loop.png`：基线、试点、证据、比较与采用决策闭环。
62. `62-abilitykit-training-title.png`：最小化内训开场与问题到决策主线。
63. `63-company-problem-cascade.png`：需求压力沿交付链放大的因果阶梯。
64. `64-role-evidence-chain.png`：不同岗位共享的运行证据链。
65. `65-company-reuse-loop.png`：项目问题到公共版本收益的闭环。
66. `66-continuous-runtime-lifecycle.png`：Continuous 的注册、Tick、暂停恢复与释放生命周期。
67. `67-prediction-reconcile-recovery.png`：预测、权威对账和恢复的责任分层。
68. `68-moba-shooter-evidence-split.png`：MOBA 与 Shooter 的双风险证据路径。
69. `69-adoption-review-contract.png`：Owner、Version、Evidence、Rollback 四项采用契约。
70. `70-reuse-admission-funnel.png`：代码进入公共框架的四层准入筛选。
71. `71-pilot-decision-path.png`：试点范围到继续、调整或回滚的决策路径。
72. `72-project-role-value-chain.png`：项目输入经 AbilityKit 工程链形成多端可验证交付。
73. `73-framework-reference-project-ownership.png`：Framework、Reference 与 Project 的所有权和反馈边界。
74. `74-project-scenario-capability-paths.png`：纯逻辑、复杂战斗与多人联机场景的能力选型路径。
75. `75-project-issue-routing.png`：项目故障到能力边界、责任人和回归证据的路由。
76. `76-evidence-led-adoption-stages.png`：按证据逐步扩大接入面的三阶段采用路径。
77. `77-adoption-decision-coordinates.png`：资产、成熟度、证据与发布四项采用决策坐标。
78. `78-skill-execution-runtime-map.png`：技能运行主线、暂停、失败与 Trace 输出。
79. `79-skill-finalization-gate.png`：Pipeline 结束后的子行为与 Owner 终结闸门。
80. `80-context-snapshot-trace-responsibilities.png`：Context、Snapshot 与 Trace 的独立事实和桥接边界。
81. `81-buff-lifecycle-ownership.png`：Buff 提交、跨模块绑定、结束顺序与事务边界。
82. `82-projectile-lifecycle-boundaries.png`：Projectile 核心生命周期与项目命中语义边界。
83. `83-sync-selection-questions.png`：从权威、数据和恢复问题形成同步项目契约。
84. `84-logic-to-presentation-projection.png`：逻辑事实经事件和快照投影到平台表现。
85. `85-framework-capability-operating-model.png`：六类现役能力如何把项目规则连接到承载和工程证据。
86. `86-project-problem-capability-paths.png`：项目问题、实际能力、直接作用与应建证据的三条接入路径。

## Mermaid 源码

同名 `.mmd` 文件用于后续在 PPT、Markdown 或 Mermaid Live Editor 中继续调整流程图结构。
""";
    File.WriteAllText(Path.Combine(outputDir, "README.md"), index, new UTF8Encoding(false));
}

string ToMermaid(CodeVisualSpec spec)
{
    var builder = new StringBuilder();
    var direction = spec.Kind is CodeVisualKind.Lifecycle or CodeVisualKind.Responsibility or CodeVisualKind.Funnel or CodeVisualKind.OwnershipBands or CodeVisualKind.AdoptionStages
        ? "flowchart TB"
        : "flowchart LR";
    builder.AppendLine(direction);

    void Node(int index)
    {
        var item = spec.Items[index];
        var id = $"N{index + 1}";
        builder.Append("    ").Append(id).Append("[").Append(item.Title.Replace("\n", " / ")).Append("<br/>").Append(item.Description.Replace("\n", " / ")).Append("<br/>").Append(item.Code.Replace("\n", " / ")).AppendLine("]");
    }

    for (var i = 0; i < spec.Items.Length; i++)
        Node(i);

    switch (spec.Kind)
    {
        case CodeVisualKind.FeedbackLoop:
            for (var i = 1; i < spec.Items.Length; i++)
                builder.Append("    N").Append(i).Append(" --> N").Append(i + 1).AppendLine();
            builder.Append("    N").Append(spec.Items.Length).Append(" --> N1").AppendLine();
            break;
        case CodeVisualKind.ReviewContract:
            builder.AppendLine("    D[采用评审]");
            for (var i = 0; i < spec.Items.Length; i++)
                builder.Append("    N").Append(i + 1).Append(" --> D").AppendLine();
            break;
        case CodeVisualKind.Funnel:
            for (var i = 1; i < spec.Items.Length; i++)
                builder.Append("    N").Append(i).Append(" --> N").Append(i + 1).AppendLine();
            builder.AppendLine("    N4 --> Candidate[Framework Candidate]");
            break;
        case CodeVisualKind.DecisionPath:
            for (var i = 1; i < spec.Items.Length; i++)
                builder.Append("    N").Append(i).Append(" --> N").Append(i + 1).AppendLine();
            builder.AppendLine("    N4 --> Decision{继续 / 调整 / 回滚}");
            break;
        case CodeVisualKind.SplitEvidence:
            builder.AppendLine("    subgraph MOBA[MOBA：复杂玩法组合]");
            builder.AppendLine("      N1 --> N2 --> N3");
            builder.AppendLine("    end");
            builder.AppendLine("    subgraph Shooter[Shooter：同步与承载]");
            builder.AppendLine("      N4 --> N5 --> N6");
            builder.AppendLine("    end");
            break;
        case CodeVisualKind.ProjectRole:
            builder.AppendLine("    Project[项目输入<br/>规则 / 约束 / 体验] --> N1 --> N2 --> N3 --> N4 --> Delivery[可验证交付<br/>Unity / Server / Test / Artifact]");
            builder.AppendLine("    Foundation[Foundation + Engineering] -. 生命周期 / ID / 多端构建 .-> N1");
            builder.AppendLine("    Foundation -. 支撑 .-> N2");
            builder.AppendLine("    Foundation -. 支撑 .-> N3");
            builder.AppendLine("    Foundation -. 支撑 .-> N4");
            break;
        case CodeVisualKind.OwnershipBands:
            builder.AppendLine("    N3 -->|稳定能力| N2 -->|验证组合| N1");
            builder.AppendLine("    N1 -. 配置 / Adapter / 缺陷反馈 .-> N2");
            builder.AppendLine("    N2 -. 公共缺陷 .-> N3");
            break;
        case CodeVisualKind.ScenarioPaths:
            for (var i = 0; i < spec.Items.Length; i++)
                builder.Append("    N").Append(i + 1).Append(" --> E").Append(i + 1).Append("[").Append(spec.Items[i].Note).AppendLine("]");
            break;
        case CodeVisualKind.IssueRouting:
            for (var i = 0; i < spec.Items.Length; i++)
                builder.Append("    N").Append(i + 1).Append(" --> O").Append(i + 1).Append("[").Append(spec.Items[i].Note).AppendLine("]");
            break;
        case CodeVisualKind.AdoptionStages:
            builder.AppendLine("    N1 -->|证据通过| N2 -->|证据通过| N3");
            builder.AppendLine("    N1 -. 证据不足则停止扩面 .-> Stop[保留旧路径 / 调整边界]");
            builder.AppendLine("    N2 -. 证据不足则回滚 .-> Stop");
            break;
        case CodeVisualKind.AdoptionCoordinates:
            builder.AppendLine("    Decision[采用决策<br/>范围 / Owner / 退出]");
            builder.AppendLine("    N1 --> Decision");
            builder.AppendLine("    N2 --> Decision");
            builder.AppendLine("    N3 --> Decision");
            builder.AppendLine("    N4 --> Decision");
            break;
        case CodeVisualKind.SkillExecutionRuntime:
            builder.AppendLine("    N1 --> N2 --> N3 --> N4 --> N5");
            builder.AppendLine("    N1 --> Reject[结构化失败出口]");
            builder.AppendLine("    N3 <--> Pause[暂停 / 恢复]");
            builder.AppendLine("    N2 -. trace root .-> Trace[Trace lineage]");
            builder.AppendLine("    N4 -. child trace .-> Trace");
            builder.AppendLine("    N5 -. output evidence .-> Trace");
            break;
        case CodeVisualKind.FinalizationGate:
            builder.AppendLine("    N1 --> N2 --> Gate{PendingChildren = 0}");
            builder.AppendLine("    Gate -->|否| N3");
            builder.AppendLine("    N3 -->|child released| Gate");
            builder.AppendLine("    Gate -->|是 / Owner release| N4");
            break;
        case CodeVisualKind.FactModelBridge:
            builder.AppendLine("    Domain[领域服务<br/>稳定身份] --> N1");
            builder.AppendLine("    Domain --> N2");
            builder.AppendLine("    Domain --> N3");
            builder.AppendLine("    N1 -. 共享 ID，不共享生命周期 .- N2");
            builder.AppendLine("    N1 -. 共享 ID，不共享生命周期 .- N3");
            break;
        case CodeVisualKind.BuffOwnershipLifecycle:
            builder.AppendLine("    N1 --> N2 --> N3 --> N4");
            builder.AppendLine("    N2 --> Bindings[Continuous / Modifier / Trigger owner / Trace / skill retain]");
            builder.AppendLine("    N3 --> Stop[停止 Continuous] --> Unbind[清 owner / skill] --> Notify[通知与 Cue] --> Remove[移出 Active] --> N4");
            builder.AppendLine("    Commit[提交前局部补偿] -. Active 后非完整事务回滚 .-> N2");
            break;
        case CodeVisualKind.ProjectileBoundaryLifecycle:
            builder.AppendLine("    N1 --> N2 --> N3 --> N4");
            builder.AppendLine("    N3 --> Events[Spawn / Tick / Hit / Exit]");
            builder.AppendLine("    Events --> Project[项目：伤害 / Buff / Trigger / 表现 / 同步]");
            builder.AppendLine("    CoreSnapshot[核心快照] --> N2");
            builder.AppendLine("    Project --> Recovery[项目副作用另行恢复]");
            break;
        case CodeVisualKind.SyncDecisionQuestions:
            builder.AppendLine("    N1 --> N2 --> N3 --> N4");
            builder.AppendLine("    Capabilities[FrameSync / StateSync / Prediction / Rollback / Replay] --> N4");
            builder.AppendLine("    Server[服务端模板与能力声明] --> N4");
            break;
        case CodeVisualKind.PresentationProjection:
            builder.AppendLine("    N1 --> N2 --> N4 --> N5");
            builder.AppendLine("    N1 --> N3 --> N4");
            builder.AppendLine("    N5 -. 禁止反写权威状态 .-> N1");
            break;
        case CodeVisualKind.CapabilityOperatingModel:
            builder.AppendLine("    N1 -. 全链路底座 .-> N2");
            builder.AppendLine("    N1 -. 全链路底座 .-> N3");
            builder.AppendLine("    N1 -. 全链路底座 .-> N4");
            builder.AppendLine("    N1 -. 全链路底座 .-> N5");
            builder.AppendLine("    N2 --> N3 --> N4 --> N5");
            builder.AppendLine("    N6 -. 构建 / 测试 / 发布 .-> N2");
            builder.AppendLine("    N6 -. 构建 / 测试 / 发布 .-> N3");
            builder.AppendLine("    N6 -. 构建 / 测试 / 发布 .-> N4");
            builder.AppendLine("    N6 -. 构建 / 测试 / 发布 .-> N5");
            break;
        case CodeVisualKind.ProblemCapabilityPaths:
            for (var i = 0; i < spec.Items.Length; i++)
            {
                builder.Append("    N").Append(i + 1).Append(" --> B").Append(i + 1).Append("[").Append(spec.Items[i].Code).AppendLine("]");
                builder.Append("    B").Append(i + 1).Append(" --> E").Append(i + 1).Append("[").Append(spec.Items[i].Note.Replace("\n", " / ")).AppendLine("]");
            }
            break;
        default:
            for (var i = 1; i < spec.Items.Length; i++)
                builder.Append("    N").Append(i).Append(" --> N").Append(i + 1).AppendLine();
            break;
    }
    return builder.ToString();
}

record Layer(string Name, string Color, string[] Items);
record Group(string Title, string Color, string[] Items);
record Step(string Title, string Description, string Color);
record GateLevel(string Name, string Description, float Width, float Y, string Color);
record CodeVisualItem(string Title, string Description, string Code, string Note, string Color);
record CodeVisualSpec(string FileName, CodeVisualKind Kind, string Title, string Subtitle, string Takeaway, string Source, string CenterLabel, string LeftLabel, string RightLabel, string[] MatrixHeaders, CodeVisualItem[] Items);
enum CodeVisualKind
{
    DataFlow,
    Sequence,
    Lifecycle,
    SplitFlow,
    Matrix,
    Stack,
    Title,
    Cascade,
    RoleChain,
    FeedbackLoop,
    FlatLifecycle,
    Responsibility,
    SplitEvidence,
    ReviewContract,
    Funnel,
    DecisionPath,
    ProjectRole,
    OwnershipBands,
    ScenarioPaths,
    IssueRouting,
    AdoptionStages,
    AdoptionCoordinates,
    SkillExecutionRuntime,
    FinalizationGate,
    FactModelBridge,
    BuffOwnershipLifecycle,
    ProjectileBoundaryLifecycle,
    SyncDecisionQuestions,
    PresentationProjection,
    CapabilityOperatingModel,
    ProblemCapabilityPaths
}

sealed class Palette
{
    public string Text { get; } = "#172033";
    public string Muted { get; } = "#536070";
    public string Blue { get; } = "#2563EB";
    public string Cyan { get; } = "#0891B2";
    public string Green { get; } = "#059669";
    public string Amber { get; } = "#D97706";
    public string Red { get; } = "#DC2626";
    public string Purple { get; } = "#7C3AED";
    public string DarkLine { get; } = "#334155";
}

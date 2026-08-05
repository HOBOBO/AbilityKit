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
    Header(g, "AbilityKit 四层架构", "从底层能力到游戏示例：依赖方向向下，业务按需组合");
    var layers = new[]
    {
        new Layer("应用与示例层", colors.Blue, new[] { "MOBA 示例", "Shooter 示例", "Unity 表现", "ET / Orleans 集成" }),
        new Layer("技能与战斗层", colors.Cyan, new[] { "Pipeline", "Triggering", "Ability Runtime", "Targeting / Projectile / Damage" }),
        new Layer("世界与同步层", colors.Green, new[] { "World.DI", "ECS", "FrameSync", "Snapshot / StateSync / Record" }),
        new Layer("核心基础层", colors.Purple, new[] { "Core", "Math", "Event", "Attributes / Effects" })
    };

    var y = 210f;
    foreach (var layer in layers)
    {
        RoundRect(g, new RectangleF(150, y, 1620, 150), 18, Brush(layer.Color), Pen("#FFFFFF", 2));
        Text(g, layer.Name, new RectangleF(190, y + 38, 260, 70), subTitleFont, Brushes.White, StringAlignment.Near);

        var x = 500f;
        foreach (var item in layer.Items)
        {
            RoundRect(g, new RectangleF(x, y + 40, 280, 70), 10, Brushes.White, null);
            Text(g, item, new RectangleF(x + 10, y + 48, 260, 54), smallFont, Brush(colors.Text));
            x += 305;
        }

        y += 180;
    }

    Arrow(g, 960, 900, 960, 840, Pen(colors.DarkLine, 3));
    Text(g, "上层组合业务，下层提供稳定能力", new RectangleF(690, 925, 540, 45), bodyFont, Brush(colors.Muted));
});

SaveCanvas("02-abilitykit-capability-map.png", g =>
{
    Header(g, "AbilityKit 能力地图", "按问题域理解模块，而不是按包名机械记忆");
    var groups = new[]
    {
        new Group("技能编排", colors.Blue, new[] { "Pipeline", "Phase", "Sequence / Parallel", "暂停 / 恢复 / 中断" }),
        new Group("事件触发", colors.Cyan, new[] { "Triggering", "强类型事件", "条件表达式", "ExecCtx 注入" }),
        new Group("数值效果", colors.Green, new[] { "Attributes", "Effects", "Buff / Debuff", "Trace" }),
        new Group("战斗查询", colors.Amber, new[] { "Targeting", "Projectile", "Damage", "Entity Index" }),
        new Group("同步回放", colors.Purple, new[] { "FrameSync", "Snapshot", "StateSync", "Record" }),
        new Group("承载验证", colors.Red, new[] { "Host", "DemoHarness", "Smoke", "CI Gate" })
    };
    var positions = new[] { (130f, 230f), (700f, 230f), (1270f, 230f), (130f, 610f), (700f, 610f), (1270f, 610f) };

    for (var i = 0; i < groups.Length; i++)
    {
        var group = groups[i];
        var (x, y) = positions[i];
        RoundRect(g, new RectangleF(x, y, 500, 300), 16, Brushes.White, Pen("#CBD5E1", 2));
        RoundRect(g, new RectangleF(x, y, 500, 68), 16, Brush(group.Color), null);
        Text(g, group.Title, new RectangleF(x + 24, y + 12, 452, 44), subTitleFont, Brushes.White, StringAlignment.Near);
        var yy = y + 95;
        foreach (var item in group.Items)
        {
            Text(g, "• " + item, new RectangleF(x + 40, yy, 420, 34), bodyFont, Brush(colors.Text), StringAlignment.Near);
            yy += 48;
        }
    }
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
    Header(g, "已落地测试门禁与 CI 接入边界", "统一门禁入口已经可执行；PR / schedule 策略已配置，仓库内 workflow 仍待接入");
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
    Text(g, "CI Adapter Boundary", new RectangleF(1390, 300, 350, 38), subTitleFont, Brushes.White);
    Text(g, "已实现", new RectangleF(1400, 385, 120, 34), bodyFont, Brush(colors.Green), StringAlignment.Near);
    Text(g, "run_test_gate.ps1\ntest-gates.json\nlogs / TRX / NUnit XML", new RectangleF(1400, 425, 330, 120), smallFont, Brush(colors.Text), StringAlignment.Near);
    Text(g, "待接入", new RectangleF(1400, 565, 120, 34), bodyFont, Brush(colors.Red), StringAlignment.Near);
    Text(g, ".github/workflows/...\n当前仓库未发现目标 workflow", new RectangleF(1400, 605, 330, 70), smallFont, Brush(colors.Text), StringAlignment.Near);
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
    Text(g, "AbilityKit\n公共战斗能力", new RectangleF(735, 415, 450, 90), titleFont, Brushes.White);
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
    Header(g, "Coordinator：会话编排与适配器成熟度", "同一 Session 协议连接本地、远端和混合同步；预测算法的完成度必须单独声明");
    RoundRect(g, new RectangleF(690, 210, 540, 150), 16, Brush(colors.Blue), null);
    Text(g, "SessionCoordinator", new RectangleF(725, 235, 470, 42), subTitleFont, Brushes.White);
    Text(g, "lifecycle / input / Tick / timeline", new RectangleF(725, 290, 470, 38), smallFont, Brushes.White);

    var adapters = new[]
    {
        new Step("LocalSyncAdapter", "本地帧驱动\n已实现", colors.Green),
        new Step("RemoteSyncAdapter", "远端传输边界\n已实现", colors.Cyan),
        new Step("HybridSyncAdapter", "缓冲与校正边界\n部分实现", colors.Amber)
    };
    var xs = new[] { 180f, 720f, 1260f };
    for (var i = 0; i < adapters.Length; i++)
    {
        var step = adapters[i];
        Arrow(g, 960, 370, xs[i] + 240, 465, Pen("#94A3B8", 3));
        RoundRect(g, new RectangleF(xs[i], 465, 480, 175), 14, Brushes.White, Pen(step.Color, 3));
        Text(g, step.Title, new RectangleF(xs[i] + 25, 490, 430, 40), subTitleFont, Brush(colors.Text));
        Text(g, step.Description, new RectangleF(xs[i] + 25, 545, 430, 70), bodyFont, Brush(step.Color));
    }

    RoundRect(g, new RectangleF(180, 710, 480, 110), 12, Brush("#ECFDF5"), Pen("#86EFAC", 2));
    Text(g, "玩法规则\n归 Logic Runtime", new RectangleF(205, 730, 430, 66), bodyFont, Brush(colors.Text));
    RoundRect(g, new RectangleF(720, 710, 480, 110), 12, Brush("#EFF6FF"), Pen("#93C5FD", 2));
    Text(g, "环境编排\n归 Coordinator", new RectangleF(745, 730, 430, 66), bodyFont, Brush(colors.Text));
    RoundRect(g, new RectangleF(1260, 710, 480, 110), 12, Brush("#FFF7ED"), Pen("#FDBA74", 2));
    Text(g, "预测 / Reconciliation\n归玩法或专用同步实现", new RectangleF(1285, 730, 430, 66), bodyFont, Brush(colors.Text));
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
    Header(g, "生成注册与运行时校验成熟度", "资源加载、Source Generator 和 MOBA Runtime Validation 是三段不同所有权的协议");
    var columns = new[]
    {
        new Group("资源接入", colors.Blue, new[] { "IResourceProvider", "JsonConfigProvider", "Unity / pure C# 可替换" }),
        new Group("生成注册", colors.Cyan, new[] { "扫描 AutoPlanAction", "注册表 + ActionId + Schema", "TryValidateArgs：待补完整校验" }),
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
    Text(g, "成熟度边界：注册与 Schema 生成已实现；参数 Schema 校验未完整实现；Runtime Validation 属于 MOBA 参考实现。", new RectangleF(205, 752, 1510, 56), bodyFont, Brush(colors.Text));
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

    RoundRect(g, new RectangleF(750, 455, 420, 130), 18, Brush("#F8FAFC"), Pen("#CBD5E1", 2));
    Text(g, spec.CenterLabel, new RectangleF(780, 474, 360, 92), bodyFont, Brush(colors.Text));
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
    const float ch = 98f;
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
        var width = 1220f - i * 110f;
        var xx = x + i * 55f;
        RoundRect(g, new RectangleF(xx, y, width, 86), 12, Brush(item.Color), null);
        Text(g, item.Title, new RectangleF(xx + 28, y + 12, 360, 32), bodyFont, Brushes.White, StringAlignment.Near);
        Text(g, item.Description, new RectangleF(xx + 410, y + 16, width - 440, 28), smallFont, Brushes.White, StringAlignment.Near);
        Text(g, item.Code, new RectangleF(xx + 410, y + 48, width - 440, 22), smallFont, Brushes.White, StringAlignment.Near);
        y += 96f;
    }
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
    CodeVisualItem I(string title, string desc, string code, string note = "") => new(title, desc, code, note, palette[Math.Abs((title + code).GetHashCode()) % palette.Length]);
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
        S("47-discussion-decision-map.png", CodeVisualKind.Matrix, "下一步讨论应落到代码决策", "讨论项不再泛泛谈是否采用框架，而是选择试点、入口、门禁和回流机制", "内训最后要收束到可执行决策。", "代码依据：当前可落地入口", I("试点模块", "技能 / 触发 / 同步", "module owner", "试点范围"), I("接入入口", "WorldService / Adapter", "integration owner", "接口清单"), I("验收门禁", "P0 smoke + P1 contract", "quality owner", "门禁清单"), I("回流机制", "项目问题进入框架 backlog", "framework owner", "Docs + tests")) with { MatrixHeaders = new[] { "决策项", "可选范围", "负责人", "输出物" } }
    };
}

void WriteMermaidFiles()
{
    var files = new Dictionary<string, string>
    {
        ["01-abilitykit-architecture-layers.mmd"] = """
flowchart TB
    A[应用与示例层<br/>MOBA / Shooter / Unity 表现 / ET-Orleans] --> B[技能与战斗层<br/>Pipeline / Triggering / Ability / Targeting / Projectile / Damage]
    B --> C[世界与同步层<br/>World.DI / ECS / FrameSync / Snapshot / StateSync / Record]
    C --> D[核心基础层<br/>Core / Math / Event / Attributes / Effects]
""",
        ["02-abilitykit-capability-map.mmd"] = """
mindmap
  root((AbilityKit 能力地图))
    技能编排
      Pipeline
      Phase
      Sequence / Parallel
    事件触发
      Triggering
      强类型事件
      ExecCtx
    数值效果
      Attributes
      Effects
      Buff / Debuff
    战斗查询
      Targeting
      Projectile
      Damage
    同步回放
      FrameSync
      Snapshot
      StateSync
      Record
    承载验证
      Host
      DemoHarness
      Test Gates
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
    subgraph Gate[已实现统一门禁入口]
      P0[P0<br/>precheck / build / test / smoke] --> P1[P1<br/>contracts / Unity EditMode / sync]
      P1 --> P2[P2<br/>batch regression / release candidate]
    end
    Gate --> Adapter[run_test_gate.ps1<br/>test-gates.json<br/>logs / TRX / NUnit XML]
    Adapter -. workflow 待接入 .-> CI[.github/workflows/...]
""",
        ["12b-coordinator-adapter-maturity.mmd"] = """
flowchart TB
    C[SessionCoordinator<br/>lifecycle / input / Tick / timeline] --> L[LocalSyncAdapter<br/>已实现]
    C --> R[RemoteSyncAdapter<br/>已实现]
    C --> H[HybridSyncAdapter<br/>部分实现]
    L --> LR[玩法规则归 Logic Runtime]
    R --> CR[环境编排归 Coordinator]
    H --> PR[预测与 Reconciliation<br/>归玩法或专用同步实现]
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
    Resource[资源接入<br/>IResourceProvider / JsonConfigProvider] --> Generator[生成注册<br/>AutoPlanAction / Registry / ActionId / Schema]
    Generator --> Validation[运行时校验<br/>required contract / startup block / history]
    Generator -. TryValidateArgs 待补 .-> Gap[参数 Schema 校验]
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

1. `01-abilitykit-architecture-layers.png`：四层架构图。
2. `02-abilitykit-capability-map.png`：能力地图。
3. `03-skill-cast-main-flow.png`：技能释放主链路。
4. `04-moba-runtime-and-dsl-flow.png`：MOBA 启动链与 DSL 场景。
5. `05-shooter-sync-matrix.png`：Shooter 同步能力矩阵。
6. `06-test-gates-ci-pyramid.png`：测试门禁与 CI 分层。
7. `07-company-reuse-feedback-loop.png`：公司级复用闭环。
8. `08-graph-component-selection.png`：Pipeline / HFSM / Flow / BehaviorTree 选型。
9. `09-moba-skill-runtime-lifecycle.png`：MOBA 技能 Runtime 生命周期。
10. `10-moba-trigger-context-trace-flow.png`：MOBA 触发执行与上下文溯源。
11. `11-moba-buff-lifecycle.png`：MOBA Buff 生命周期正式化。
12. `12-shooter-pure-csharp-projection.png`：Shooter 纯 C# 到 Unity 表现投影。
12B. `12b-coordinator-adapter-maturity.png`：Coordinator 会话编排与同步适配器成熟度。
13. `13-demoharness-three-axis.png`：DemoHarness 三轴正交模型。
14. `14-client-flow-boundaries.png`：Client Flow 与表现边界。
15. `15-targeting-query-chain.png`：Targeting 查询链路。
16. `16-projectile-lifecycle.png`：Projectile 生命周期。
17. `17-damage-pipeline.png`：Damage 通用计算内核与 MOBA 应用编排边界。
18. `18-attributes-modifier-stack.png`：Attributes 修饰器栈。
19. `19-record-replay-debug-flow.png`：FrameRecord 三轨记录与可执行回归。
20. `20-battlehost-lifecycle.png`：Orleans BattleHost 与玩法适配成熟度。
21. `21-config-validation-pipeline.png`：生成注册与运行时校验成熟度。
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

## Mermaid 源码

同名 `.mmd` 文件用于后续在 PPT、Markdown 或 Mermaid Live Editor 中继续调整流程图结构。
""";
    File.WriteAllText(Path.Combine(outputDir, "README.md"), index, new UTF8Encoding(false));
}

string ToMermaid(CodeVisualSpec spec)
{
    var builder = new StringBuilder();
    builder.AppendLine(spec.Kind == CodeVisualKind.Lifecycle ? "flowchart TB" : "flowchart LR");
    for (var i = 0; i < spec.Items.Length; i++)
    {
        var item = spec.Items[i];
        var id = $"N{i + 1}";
        builder.Append("    ").Append(id).Append("[").Append(item.Title).Append("<br/>").Append(item.Description.Replace("\n", " / ")).Append("<br/>").Append(item.Code).AppendLine("]");
        if (i > 0)
            builder.Append("    N").Append(i).Append(" --> ").Append(id).AppendLine();
    }
    return builder.ToString();
}

record Layer(string Name, string Color, string[] Items);
record Group(string Title, string Color, string[] Items);
record Step(string Title, string Description, string Color);
record GateLevel(string Name, string Description, float Width, float Y, string Color);
record CodeVisualItem(string Title, string Description, string Code, string Note, string Color);
record CodeVisualSpec(string FileName, CodeVisualKind Kind, string Title, string Subtitle, string Takeaway, string Source, string CenterLabel, string LeftLabel, string RightLabel, string[] MatrixHeaders, CodeVisualItem[] Items);
enum CodeVisualKind { DataFlow, Sequence, Lifecycle, SplitFlow, Matrix, Stack }

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

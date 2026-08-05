from __future__ import annotations

import math
import sys
from dataclasses import dataclass, field
from pathlib import Path
from textwrap import wrap

from PIL import Image, ImageDraw, ImageFont

WIDTH = 1920
HEIGHT = 1080
BG = "#F8FAFC"
TEXT = "#0F172A"
MUTED = "#475569"
LINE = "#CBD5E1"
DARK = "#1E293B"
WHITE = "#FFFFFF"
BLUE = "#2563EB"
INDIGO = "#4F46E5"
CYAN = "#0891B2"
GREEN = "#16A34A"
AMBER = "#D97706"
RED = "#DC2626"
PURPLE = "#9333EA"
SLATE = "#64748B"

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUT = ROOT / "Docs" / "ppt-assets" / "abilitykit-deep-dive"


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    candidates = [
        "C:/Windows/Fonts/msyhbd.ttc" if bold else "C:/Windows/Fonts/msyh.ttc",
        "C:/Windows/Fonts/simhei.ttf",
        "C:/Windows/Fonts/arialbd.ttf" if bold else "C:/Windows/Fonts/arial.ttf",
    ]
    for path in candidates:
        if Path(path).exists():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()

TITLE = font(48, True)
SUBTITLE = font(26)
H2 = font(28, True)
BODY = font(22)
SMALL = font(18)
CODE = font(18)
TINY = font(15)


@dataclass
class Node:
    title: str
    desc: str
    code: str = ""
    color: str = BLUE
    note: str = ""


@dataclass
class Diagram:
    file: str
    title: str
    subtitle: str
    kind: str
    takeaway: str
    source: str
    nodes: list[Node] = field(default_factory=list)


def rounded(draw: ImageDraw.ImageDraw, box, radius=22, fill=WHITE, outline=LINE, width=2):
    draw.rounded_rectangle(box, radius=radius, fill=fill, outline=outline, width=width)


def text(draw: ImageDraw.ImageDraw, value: str, box, fnt=BODY, fill=TEXT, spacing=6, max_lines: int | None = None):
    x1, y1, x2, y2 = box
    max_width = max(10, x2 - x1)
    lines: list[str] = []
    for raw in str(value).split("\n"):
        if not raw:
            lines.append("")
            continue
        current = ""
        for ch in raw:
            candidate = current + ch
            if draw.textbbox((0, 0), candidate, font=fnt)[2] <= max_width:
                current = candidate
            else:
                if current:
                    lines.append(current)
                current = ch
        if current:
            lines.append(current)
    if max_lines is not None and len(lines) > max_lines:
        lines = lines[:max_lines]
        lines[-1] = lines[-1].rstrip("…") + "…"
    line_h = fnt.size + spacing
    y = y1
    for line in lines:
        if y + line_h > y2:
            break
        draw.text((x1, y), line, font=fnt, fill=fill)
        y += line_h


def center_text(draw: ImageDraw.ImageDraw, value: str, box, fnt=BODY, fill=TEXT):
    x1, y1, x2, y2 = box
    bbox = draw.textbbox((0, 0), value, font=fnt)
    draw.text((x1 + (x2 - x1 - (bbox[2] - bbox[0])) / 2, y1 + (y2 - y1 - (bbox[3] - bbox[1])) / 2), value, font=fnt, fill=fill)


def arrow(draw: ImageDraw.ImageDraw, start, end, color=SLATE, width=4):
    draw.line([start, end], fill=color, width=width)
    sx, sy = start
    ex, ey = end
    angle = math.atan2(ey - sy, ex - sx)
    size = 14
    left = (ex - size * math.cos(angle - math.pi / 6), ey - size * math.sin(angle - math.pi / 6))
    right = (ex - size * math.cos(angle + math.pi / 6), ey - size * math.sin(angle + math.pi / 6))
    draw.polygon([end, left, right], fill=color)


def canvas(diagram: Diagram) -> tuple[Image.Image, ImageDraw.ImageDraw]:
    img = Image.new("RGB", (WIDTH, HEIGHT), BG)
    draw = ImageDraw.Draw(img)
    draw.rectangle((0, 0, WIDTH, 118), fill="#E0F2FE")
    draw.rectangle((0, 116, WIDTH, 120), fill="#7DD3FC")
    draw.text((70, 30), diagram.title, font=TITLE, fill=TEXT)
    draw.text((72, 88), diagram.subtitle, font=SUBTITLE, fill=MUTED)
    return img, draw


def draw_card(draw, node: Node, box, badge: str | None = None):
    x1, y1, x2, y2 = box
    rounded(draw, box, 22, WHITE, "#D8E2EF", 2)
    draw.rounded_rectangle((x1, y1, x2, y1 + 14), radius=7, fill=node.color)
    if badge:
        draw.ellipse((x1 + 18, y1 + 24, x1 + 58, y1 + 64), fill=node.color)
        center_text(draw, badge, (x1 + 18, y1 + 24, x1 + 58, y1 + 64), SMALL, WHITE)
        tx = x1 + 72
    else:
        tx = x1 + 24
    text(draw, node.title, (tx, y1 + 26, x2 - 22, y1 + 58), H2, TEXT, max_lines=1)
    text(draw, node.desc, (x1 + 24, y1 + 78, x2 - 24, y2 - 52), BODY, MUTED, max_lines=3)
    if node.code:
        draw.rounded_rectangle((x1 + 24, y2 - 44, x2 - 24, y2 - 16), radius=9, fill="#F1F5F9", outline="#E2E8F0")
        text(draw, node.code, (x1 + 36, y2 - 39, x2 - 36, y2 - 18), CODE, "#334155", max_lines=1)


def draw_timeline(diagram: Diagram, out: Path):
    img, draw = canvas(diagram)
    y = 360
    x0 = 110
    step = 260
    for i, node in enumerate(diagram.nodes):
        x = x0 + i * step
        draw_card(draw, node, (x, y, x + 215, y + 255), str(i + 1))
        if i < len(diagram.nodes) - 1:
            arrow(draw, (x + 215, y + 128), (x + step - 18, y + 128), node.color)
    img.save(out / diagram.file)


def orthogonal_arrow(draw: ImageDraw.ImageDraw, points: list[tuple[int, int]], color=SLATE, width=4):
    if len(points) < 2:
        return
    for start, end in zip(points, points[1:]):
        draw.line([start, end], fill=color, width=width)
    start = points[-2]
    end = points[-1]
    sx, sy = start
    ex, ey = end
    angle = math.atan2(ey - sy, ex - sx)
    size = 14
    left = (ex - size * math.cos(angle - math.pi / 6), ey - size * math.sin(angle - math.pi / 6))
    right = (ex - size * math.cos(angle + math.pi / 6), ey - size * math.sin(angle + math.pi / 6))
    draw.polygon([end, left, right], fill=color)


def draw_swimlane(diagram: Diagram, out: Path):
    img, draw = canvas(diagram)
    lanes = [("入口层", 115, "#DBEAFE"), ("运行时层", 455, "#DCFCE7"), ("收尾/保护", 795, "#FEF3C7")]
    for name, x, color in lanes:
        draw.rounded_rectangle((x, 190, x + 300, 930), radius=24, fill=color, outline="#CBD5E1", width=2)
        center_text(draw, name, (x, 205, x + 300, 245), H2, TEXT)
    positions = [(150, 285), (150, 600), (490, 285), (490, 600), (830, 285), (830, 600), (1170, 445), (1510, 445)]
    for i, node in enumerate(diagram.nodes):
        x, y = positions[i]
        draw_card(draw, node, (x, y, x + 250, y + 190), str(i + 1))
        if i < len(diagram.nodes) - 1:
            nx, ny = positions[i + 1]
            arrow(draw, (x + 250, y + 95), (nx - 12, ny + 95), node.color)
    img.save(out / diagram.file)


def draw_owner_protocol(diagram: Diagram, out: Path):
    img, draw = canvas(diagram)
    columns = [
        (70, 190, 390, "计划来源", "#DBEAFE"),
        (430, 190, 750, "对账决策", "#E0F2FE"),
        (790, 190, 1110, "订阅维护", "#DCFCE7"),
        (1150, 190, 1470, "强类型注册", "#FEF3C7"),
        (1510, 190, 1830, "门控执行", "#FCE7F3"),
    ]
    for x1, y1, x2, title, fill in columns:
        draw.rounded_rectangle((x1, y1, x2, 905), radius=24, fill=fill, outline="#CBD5E1", width=2)
        center_text(draw, title, (x1 + 10, y1 + 16, x2 - 10, y1 + 58), H2, TEXT)

    boxes = [
        (95, 300, 365, 460),
        (455, 300, 725, 460),
        (815, 300, 1085, 460),
        (815, 620, 1085, 780),
        (1175, 300, 1445, 460),
        (1535, 300, 1805, 460),
        (1535, 620, 1805, 780),
        (455, 620, 725, 780),
    ]
    for i, (node, box) in enumerate(zip(diagram.nodes, boxes), start=1):
        draw_card(draw, node, box, str(i))

    y = 380
    orthogonal_arrow(draw, [(365, y), (455, y)], BLUE)
    orthogonal_arrow(draw, [(725, y), (815, y)], CYAN)
    orthogonal_arrow(draw, [(1085, y), (1175, y)], GREEN)
    orthogonal_arrow(draw, [(1445, y), (1535, y)], AMBER)

    orthogonal_arrow(draw, [(590, 460), (590, 610)], RED, 3)
    center_text(draw, "stale / empty", (510, 520, 670, 555), SMALL, RED)

    orthogonal_arrow(draw, [(950, 460), (950, 610)], INDIGO, 3)
    center_text(draw, "record + args type", (835, 520, 1065, 555), SMALL, INDIGO)

    orthogonal_arrow(draw, [(1670, 460), (1670, 610)], PURPLE, 3)
    center_text(draw, "success", (1605, 520, 1735, 555), SMALL, PURPLE)
    img.save(out / diagram.file)


def draw_foundation_protocol(diagram: Diagram, out: Path):
    img, draw = canvas(diagram)

    def compact_card(node: Node, box, title_font=H2, desc_font=BODY):
        x1, y1, x2, y2 = box
        rounded(draw, box, 18, WHITE, "#D8E2EF", 2)
        draw.rounded_rectangle((x1, y1, x2, y1 + 12), radius=6, fill=node.color)
        text(draw, node.title, (x1 + 20, y1 + 24, x2 - 20, y1 + 58), title_font, TEXT, max_lines=1)
        text(draw, node.desc, (x1 + 20, y1 + 68, x2 - 20, y2 - 16), desc_font, MUTED, max_lines=2)

    bands = [
        ("战斗能力层", 170, 370, "#DBEAFE"),
        ("运行时协议层", 410, 610, "#DCFCE7"),
        ("工程基础协议层", 650, 960, "#FEF3C7"),
    ]
    for title, y1, y2, fill in bands:
        draw.rounded_rectangle((70, y1, 1850, y2), radius=26, fill=fill, outline="#CBD5E1", width=2)
        text(draw, title, (100, y1 + 28, 310, y1 + 72), H2, TEXT, max_lines=1)

    top_boxes = [(350, 220, 760, 345), (805, 220, 1215, 345), (1260, 220, 1670, 345)]
    mid_boxes = [(420, 460, 870, 585), (1050, 460, 1500, 585)]
    bottom_boxes = [
        (350, 700, 760, 810),
        (805, 700, 1215, 810),
        (1260, 700, 1670, 810),
        (350, 830, 760, 940),
        (805, 830, 1215, 940),
        (1260, 830, 1670, 940),
    ]

    for node, box in zip(diagram.nodes[:3], top_boxes):
        compact_card(node, box, H2, SMALL)
    for node, box in zip(diagram.nodes[3:5], mid_boxes):
        compact_card(node, box, H2, SMALL)
    for node, box in zip(diagram.nodes[5:], bottom_boxes):
        compact_card(node, box, BODY, SMALL)

    support_points = [(555, 645, BLUE), (1010, 645, GREEN), (1465, 645, AMBER), (555, 1275, PURPLE), (1010, 1275, CYAN), (1465, 1275, RED)]
    for start_x, end_x, color in support_points:
        orthogonal_arrow(draw, [(start_x, 700), (start_x, 640), (end_x, 640), (end_x, 585)], color, 3)
    for start_x, end_x, color in [(645, 555, INDIGO), (1275, 1010, CYAN), (1275, 1465, RED)]:
        orthogonal_arrow(draw, [(start_x, 460), (start_x, 395), (end_x, 395), (end_x, 345)], color, 3)

    center_text(draw, "底层协议统一后，上层模块才能跨项目复用、测试和排障", (530, 620, 1390, 650), BODY, DARK)
    img.save(out / diagram.file)


def draw_context_map(diagram: Diagram, out: Path):
    img, draw = canvas(diagram)
    cx, cy = 960, 540
    draw.ellipse((735, 315, 1185, 765), fill="#EEF2FF", outline="#818CF8", width=4)
    center_text(draw, "MobaGameplayOrigin\nLineageContext\nTraceContext", (770, 420, 1150, 555), H2, TEXT)
    center_text(draw, "统一溯源视图", (780, 575, 1140, 625), BODY, INDIGO)
    angles = [-150, -90, -30, 30, 90, 150]
    for i, node in enumerate(diagram.nodes):
        a = math.radians(angles[i])
        x = cx + int(610 * math.cos(a)) - 170
        y = cy + int(335 * math.sin(a)) - 95
        draw_card(draw, node, (x, y, x + 340, y + 190), None)
        arrow(draw, (x + 170, y + 95), (cx + int(235 * math.cos(a)), cy + int(170 * math.sin(a))), node.color, 3)
    img.save(out / diagram.file)


def draw_matrix(diagram: Diagram, out: Path):
    img, draw = canvas(diagram)
    x0, y0 = 160, 205
    col_w = 535
    row_h = 148
    headers = ["结构字段", "运行机制", "代码锚点"]
    for c, h in enumerate(headers):
        rounded(draw, (x0 + c * col_w, y0, x0 + (c + 1) * col_w - 20, y0 + 64), 16, "#DBEAFE", "#93C5FD")
        center_text(draw, h, (x0 + c * col_w, y0, x0 + (c + 1) * col_w - 20, y0 + 64), H2, TEXT)
    for r, node in enumerate(diagram.nodes):
        y = y0 + 86 + r * row_h
        parts = [node.title, node.desc, node.code]
        for c, part in enumerate(parts):
            fill = WHITE if c != 1 else "#F8FAFC"
            rounded(draw, (x0 + c * col_w, y, x0 + (c + 1) * col_w - 20, y + row_h - 18), 14, fill, "#E2E8F0")
            text(draw, part, (x0 + c * col_w + 18, y + 18, x0 + (c + 1) * col_w - 44, y + row_h - 28), BODY if c != 2 else CODE, TEXT if c == 0 else MUTED, max_lines=3)
    img.save(out / diagram.file)


def diagrams() -> list[Diagram]:
    return [
        Diagram(
            "01-lineage-source-trace-map.png", "溯源链路：一次效果从哪里来", "把 Skill / Buff / Trigger / Continuous 都归一到可追踪来源", "context",
            "深讲时重点说明：溯源不是日志，而是运行时上下文、配置、父子 trace 和表现/调试统一入口。",
            "BuffStageEffectExecutor.cs / BuffComponent.cs / SkillCastPreparationService.cs",
            [
                Node("技能根上下文", "Cast 创建 RootContext，记录施法者、目标和配置", "CreateRootContext", BLUE),
                Node("Buff 来源视图", "BuffRuntime 保存 Origin、ContextSource、SourceContextId", "BuffRuntime.ContextSource", GREEN),
                Node("阶段来源快照", "移除阶段 runtime 会清理，因此先冻结 PersistentSource", "CapturePersistentSource", AMBER),
                Node("Trigger Lineage", "payload 提供 LineageContext 与 TraceContext", "TryGetLineageContext", PURPLE),
                Node("技能 Runtime", "Buff 可保留 SkillRuntimeHandle，避免异步效果断链", "SkillRuntimeRetainHandle", INDIGO),
                Node("查询与排查", "按 Kind / RuntimeKind / TraceKind 定位来源", "MobaSourceQuery", CYAN),
            ],
        ),
        Diagram(
            "02-skill-runtime-lifecycle-detail.png", "技能生命周期：从输入到最终释放", "不仅是 Cast 成功，而是 runtime、trace、子效果和清理的完整闭环", "timeline",
            "这页适合解释为什么技能系统要有 runtime handle：它承载异步、子效果、Buff 绑定和最终清理。",
            "SkillCastCoordinator.cs / SkillCastPreparationService.cs / MobaSkillCastRuntimeService.cs",
            [
                Node("输入分发", "Press / Hold / Release / Cancel 统一进入相位分发", "DispatchSkillInputPhase", BLUE, "输入阶段先规范化，避免各项目散写按键逻辑。"),
                Node("准备上下文", "校验配置、目标、等级，构造 SkillCastContext", "Prepare", CYAN, "失败原因集中返回，便于测试和 UI 展示。"),
                Node("创建根 Trace", "施法创建 SkillCast 根上下文", "CreateRootContext", INDIGO, "后续效果都能追到同一次释放。"),
                Node("创建 Runtime", "生成 runtimeId、generation、handle、blackboard", "Create", GREEN, "运行时身份独立于配置和实体。"),
                Node("Pipeline 执行", "PreCast / Cast 等阶段由 runner 推进", "SkillRunnerRegistry", AMBER, "阶段可扩展但入口稳定。"),
                Node("等待子效果", "Pipeline 结束后等待 PendingChildren", "WaitingChildren", PURPLE, "解决投射物、延迟 Buff 等异步收尾。"),
                Node("最终释放", "End trace、RemoveRetains、FlushEnded", "TryFinalize", RED, "清理顺序固定，减少泄漏和断链。"),
            ],
        ),
        Diagram(
            "03-buff-runtime-structure.png", "Buff Runtime：为什么不是一个剩余时间字段", "一个 Buff 同时绑定来源、叠层、持续行为、技能 runtime、标签与修饰器", "matrix",
            "这页适合讲 Buff 的复杂度：真正可复用的是运行时结构和生命周期约束，而不是简单加减属性。",
            "BuffComponent.cs / BuffApplyFlow.cs / BuffEndFlow.cs",
            [
                Node("来源断链", "移除后仍要知道是谁造成的", "SourceContextId / Origin / ContextSource", BLUE, "可追溯、可表现、可回放"),
                Node("异步效果", "技能结束后 Buff 仍可能 tick 或触发", "SkillRuntimeHandle / RetainHandle", INDIGO, "Buff 保留技能运行时身份"),
                Node("生命周期", "到期、标签中断、手动移除都走同一收尾", "Remaining / Continuous", GREEN, "避免不同移除路径漏清理"),
                Node("叠层与属性", "刷新、叠加、替换影响数值和间隔", "StackCount / ModifierBindings", AMBER, "配置策略变成统一规则"),
                Node("标签约束", "Buff 可因标签变化被暂停或移除", "TagRequirements", PURPLE, "控制免疫、沉默、禁疗等规则"),
            ],
        ),
        Diagram(
            "04-buff-apply-refresh-remove-flow.png", "Buff 生命周期：Apply / Refresh / Remove", "入口只排队，生命周期层统一校验、绑定、通知、清理", "swimlane",
            "这页强调 Buff 架构价值：入口薄、生命周期厚，所有项目复用同一套边界和清理顺序。",
            "MobaBuffService.cs / BuffApplyFlow.cs / BuffEndFlow.cs / BuffStageEffectExecutor.cs",
            [
                Node("申请入口", "外部调用 Apply/Remove 只进入命令队列", "EnqueueApply", BLUE),
                Node("防重入 Drain", "效果中再次申请 Buff 不递归执行", "DrainPending", CYAN),
                Node("配置/标签校验", "目标、配置、CanActivate 统一拒绝", "BuffTagLifecycle", INDIGO),
                Node("新建或刷新", "按 key 找 existing，执行叠层策略", "ApplyToExisting", GREEN),
                Node("绑定运行时", "创建 context、continuous、trigger plans", "EnsureContinuousRuntime", AMBER),
                Node("阶段触发", "add/remove/interval 转为 Trigger 请求", "BuffTriggerContext", PURPLE),
                Node("结束清理", "停 continuous、end trace、清 owner binding、释放 skill runtime", "EndRuntime", RED),
                Node("对象回收", "清空 runtime binding 并释放对象", "ReleaseRuntime", SLATE),
            ],
        ),
        Diagram(
            "05-continuous-tick-tag-rule-flow.png", "持续行为：Tick、标签规则与状态同步", "Buff、技能管线、移动等过程统一托管在 ContinuousManager", "timeline",
            "这页适合解释持续行为的复用点：时间推进、间隔触发、标签中断、投影数值和上下文生命周期都集中管理。",
            "MobaContinuousManager.cs / MobaContinuousTagRuleService.cs",
            [
                Node("注册准入", "AdmissionPolicy 先判断能否注册/激活", "CanActivate", BLUE, "启动前就能被标签和配置拦截。"),
                Node("生命周期绑定", "modifier projector 与 context binder 挂入 manager", "AddLifecycleBinder", CYAN, "持续行为不直接散改属性。"),
                Node("逐帧 Tick", "遍历 active continuous，跳过暂停/终止", "Tick", GREEN, "所有持续过程共享时间入口。"),
                Node("间隔处理", "Buff interval 等处理器统一执行", "MobaContinuousTickProcessor", AMBER, "周期触发不散落在 Buff 代码里。"),
                Node("标签对账", "最多 8 轮处理 Pause / Resume / Remove", "ReconcileOwner", PURPLE, "标签变化可驱动持续行为状态迁移。"),
                Node("状态同步", "tick 后把 managed state 同步回 runtime", "SyncManagedState", INDIGO, "调试视图与运行时保持一致。"),
            ],
        ),
        Diagram(
            "06-owner-bound-trigger-subscription.png", "Owner-Bound Trigger：被动与持续触发怎么接入", "ownerKey 把常驻触发计划绑定到来源上下文，并通过 gate 控制执行", "swimlane",
            "这页适合讲触发器使用：Direct Trigger 负责立即执行，Owner-Bound Trigger 负责长期订阅、冷却和清理。",
            "MobaPassiveSkillLifecycleService.cs / MobaTriggerPlanReconcileService.cs / MobaTriggerPlanSubscriptionService.cs",
            [
                Node("同步被动", "SkillLoadout 变化时创建/移除 listener", "SyncActorPassives", BLUE),
                Node("创建 ownerKey", "被动技能创建 root source context", "EnsurePassiveSkillContext", INDIGO),
                Node("写入计划", "PassiveSkill.TriggerIds 写入 OngoingTriggerPlans", "UpsertOngoingTriggerPlansEntry", GREEN),
                Node("计划对账", "hash 变化才 Apply，stale 自动 Stop", "Reconcile", CYAN),
                Node("Typed 注册", "EventName 映射 args type，再注册 runner", "RegisterTypedAs<TArgs>", AMBER),
                Node("Gate 包装", "ownerKey + triggerId 检查冷却/合法性", "GatedOwnerBoundTrigger", PURPLE),
                Node("执行完成", "触发后 Complete 写入冷却结束时间", "Complete", RED),
                Node("卸载清理", "CancelByOwnerKey + EndContext + Stop", "UnregisterActor", SLATE),
            ],
        ),
        Diagram(
            "11-owner-bound-trigger-gate-reconcile-runtime.png", "Owner-Bound Trigger：订阅、门控与对账协议", "ownerKey 下的长期触发不是监听脚本，而是可注册、可门控、可清理的运行时协议", "owner_protocol",
            "这页用于正文：展示 owner-bound trigger 如何从计划对账进入强类型订阅，并通过 gate 完成执行闭环。",
            "MobaTriggerPlanReconcileService.cs / MobaTriggerPlanSubscriptionService.cs / MobaPassiveSkillLifecycleService.cs",
            [
                Node("写入 OngoingPlan", "被动或 Buff 把 ownerKey + triggerIds 写入长期计划", "UpsertOngoingTriggerPlansEntry", BLUE),
                Node("Reconcile 对账", "ComputeHash(triggerIds) 与上次哈希比较，仅变化才 Apply，stale 自动 Stop", "Reconcile", CYAN),
                Node("ApplyTriggers", "为空即 Stop；否则复用 owner regs 字典", "ApplyTriggers", INDIGO),
                Node("Typed 校验", "record、OwnerBound scope、event args type 全部过检", "BuildArgsTypeCache", GREEN),
                Node("RegisterTypedAs", "Plan.AsArgs<TArgs> 后按 phase/priority 注册 runner", "RegisterTypedAs<TArgs>", AMBER),
                Node("Gate 双重防御", "Evaluate 走 CanExecute→Source→Plan 三段 RecordEvaluation，Execute 复查后 Complete", "GatedOwnerBoundTrigger", PURPLE),
                Node("Complete 落账", "执行成功后 Complete 提交冷却/次数状态", "Complete", RED),
                Node("Stale / Deinit 清理", "RemoveStaleRegistrations、Stop、OnDeinit 统一释放订阅", "DisposeRegistration", SLATE),
            ],
        ),
        Diagram(
            "12-foundation-protocol-layer.png", "基础协议层：为什么上层模块能长期维护", "Eventing、StableId、Codegen、Config、Pooling 把战斗能力变成可复用工程资产", "foundation_protocol",
            "这页用于正文：把显性战斗模块、运行时协议和工程基础协议放到同一张图里，说明复用能力来自底层协议托底。",
            "EventDispatcher.cs / StableStringIdRegistry.cs / AutoPlanActionGenerator.cs / SampleConfigLoader.cs / MobaRuntimeValidation.cs",
            [
                Node("Skill / Trigger", "执行入口、触发计划和条件判断统一收敛", "MobaEffectExecutionService", BLUE),
                Node("Buff / Continuous", "跨帧状态、标签规则和生命周期清理统一托管", "MobaBuffService", GREEN),
                Node("Projectile / Damage", "战斗事实、命中事件和结算阶段可测试", "DamagePipelineService", AMBER),
                Node("Context / Trace", "来源、父子链路、诊断和表现能追到同一上下文", "MobaGameplayOrigin", INDIGO),
                Node("Presentation / Snapshot", "表现 Cue、同步快照和预测键使用稳定事实", "MobaPresentationCueKeys", CYAN),
                Node("Eventing", "typed channel、priority、once listener", "EventDispatcher", BLUE),
                Node("StableId", "字符串协议转稳定 int，避免 key 漂移", "StableStringIdRegistry", GREEN),
                Node("Codegen", "Action 注册与 schema（AutoPlanActionGenerator 已 retired，改显式 module manifest）", "module manifest", AMBER),
                Node("Config", "Unity、纯 C#、测试环境共享入口", "SampleConfigLoader", PURPLE),
                Node("Pooling / Keys", "数组池、Cue key、replication id", "MobaPresentationCueEntryPool", CYAN),
                Node("Validation", "启动、编辑器、采样模式输出报告", "MobaRuntimeValidationService", RED),
            ],
        ),
        Diagram(
            "07-attributes-dependency-dirty-graph.png", "Attributes：依赖图、公式与脏传播", "属性不是字典字段，而是 registry、formula、modifier、constraint 组合的数值协议", "matrix",
            "这页面向开发团队说明属性模块的技术核心：依赖关系、公式求值、来源清理和重算边界必须统一。",
            "AttributeRegistry.cs / AttributeContext.cs / AttributeInstance.cs / AttributeExpressionFormula.cs",
            [
                Node("Registry Freeze", "注册属性定义、公式依赖并在冻结时检测环", "AttributeRegistry.Freeze", BLUE),
                Node("Formula Eval", "表达式公式可读取 base、modifier、其他属性", "AttributeExpressionFormula", INDIGO),
                Node("Dirty Propagation", "属性变化标记依赖 dirty 并 Invalidate 计算缓存，按需重算", "OnAttributeValueChanged / MarkDirty", CYAN),
                Node("Modifier Slots", "实例维护修饰器槽位并交给 calculator 聚合", "AttributeInstance.Recompute", GREEN),
                Node("Source Cleanup", "ApplyEffect 返回 sourceId，后续按来源移除", "AttributeContext.ApplyEffect", AMBER),
                Node("Constraints", "重算结果进入约束裁剪，保证输出合法", "RangeAttributeConstraint", PURPLE),
            ],
        ),
        Diagram(
            "08-targeting-query-pipeline.png", "Targeting：SearchQuery 四段式查询", "Provider、Rules、Scorer、Selector 把寻敌从技能脚本中剥离", "timeline",
            "这页面向开发团队说明 Targeting 的扩展边界：项目新增规则、评分或选择器，不需要重写技能链路。",
            "SearchQuery.cs / TargetSearchEngine.cs / SearchPipelineBuilderExtensions.cs / TopKSelectors.cs",
            [
                Node("候选来源", "Provider 负责提供初始候选集合", "ICandidateProvider", BLUE),
                Node("规则过滤", "圆形、扇形、黑白名单、合法性过滤", "ITargetRule", CYAN),
                Node("评分计算", "距离、权重或项目自定义 scoring", "ITargetScorer", INDIGO),
                Node("选择策略", "TopK、随机或业务优先级选择", "ITargetSelector", GREEN),
                Node("流式 TopK", "大候选集可边遍历边维护结果", "IStreamingHitSelector", AMBER),
                Node("注册扩展", "Attribute 标记规则/选择器，便于工具和配置绑定", "TargetRule / TargetSelector", PURPLE),
            ],
        ),
        Diagram(
            "09-projectile-schedule-hit-rollback-flow.png", "Projectile：发射调度、命中策略与回滚", "投射物模块统一处理发射、轨迹、碰撞、事件、穿透和回滚状态", "swimlane",
            "这页面向开发团队说明 Projectile 的工程边界：表现只是消费者，runtime 负责可测试的战斗事实。",
            "ProjectileService.cs / ProjectileWorld.cs / ProjectileSpawnParams.cs / ProjectileRollbackProvider.cs",
            [
                Node("Spawn 参数", "Owner、Template、速度、碰撞体、忽略对象", "ProjectileSpawnParams", BLUE),
                Node("发射调度", "Pattern + Schedule 支持连发、扇形、散射", "ScheduleEmit", INDIGO),
                Node("World Tick", "按 frame 推进位置、寿命和 tick event", "ProjectileWorld.Tick", CYAN),
                Node("Area/Collision", "区域和碰撞命中统一产出事件", "AreaWorld / Collider", GREEN),
                Node("Hit Policy", "ExitOnHit、Pierce、无限穿透等策略", "ProjectileHitPolicyFactory", AMBER),
                Node("事件 Drain", "Spawn/Hit/Exit/Tick 由消费者拉取", "DrainHitEvents", PURPLE),
                Node("回滚导入", "导出/导入 projectile world 状态并清瞬时事件", "ExportRollback / ImportRollback", RED),
                Node("MOBA 溯源", "业务侧把 projectile hit 重新接回 Trigger", "ProjectileHitArgs", SLATE),
            ],
        ),
        Diagram(
            "10-damage-dataflow-slots-pipeline.png", "Damage：Dataflow 处理器与强类型 Slots", "伤害结算通过上下文 slots 和 processor 链固定扩展顺序", "timeline",
            "这页面向开发团队说明 Damage 的扩展方式：新增公式和抗性逻辑应进入 processor，而不是散写在技能脚本。",
            "DamageProcessors.cs / DamageSlots.cs",
            [
                Node("请求校验", "无效来源、目标或数值先失败", "ValidateDamageProcessor", BLUE),
                Node("暴击输入", "CritChance、CritRoll、CritMultiplier 进入上下文", "DamageSlots.CritChance", INDIGO),
                Node("基础伤害", "根据请求和攻击属性计算基础值", "CalculateBaseDamageProcessor", CYAN),
                Node("加成修正", "百分比和固定值加成通过 slot 注入", "DamageBonusPercent", GREEN),
                Node("防御减免", "护甲、魔抗、穿透按类型处理", "ApplyArmorReductionProcessor", AMBER),
                Node("最终结果", "FinalDamage、Overkill、Shield 等输出", "CalculateFinalDamageProcessor", RED),
            ],
        ),
    ]


def mermaid(diagram: Diagram) -> str:
    direction = "TB" if diagram.kind in {"timeline", "swimlane"} else "LR"
    lines = [f"flowchart {direction}"]
    for i, node in enumerate(diagram.nodes, start=1):
        label = f"{node.title}<br/>{node.desc}<br/>{node.code}".replace('"', "'")
        lines.append(f"    N{i}[\"{label}\"]")
        if i > 1:
            lines.append(f"    N{i-1} --> N{i}")
    return "\n".join(lines) + "\n"


def readme(items: list[Diagram]) -> str:
    lines = ["# AbilityKit 深讲图表资产", "", "该目录由 `tools/generate_abilitykit_deep_dive_assets.py` 生成，面向内训 PPT 的专题深讲章节。", "", "## 生成命令", "", "```cmd", "py -3.14 tools\\generate_abilitykit_deep_dive_assets.py", "```", "", "## 图表索引", ""]
    for i, item in enumerate(items, start=1):
        lines.append(f"{i}. `{item.file}`：{item.title}。")
    lines.append("")
    lines.append("## 讲解定位")
    lines.append("")
    lines.append("- 主 PPT 的 47 页负责建立公司级框架价值和模块全貌。")
    lines.append("- 本目录的图用于展开溯源、技能生命周期、Buff、持续行为、触发器，以及 Attributes / Targeting / Projectile / Damage 等基础模块的代码级细节。")
    lines.append("- 每张 PNG 都配有同名 `.mmd`，方便后续改成 Mermaid 或继续手工调整。")
    return "\n".join(lines) + "\n"


def render(output: Path):
    output.mkdir(parents=True, exist_ok=True)
    items = diagrams()
    for item in items:
        if item.kind == "timeline":
            draw_timeline(item, output)
        elif item.kind == "swimlane":
            draw_swimlane(item, output)
        elif item.kind == "owner_protocol":
            draw_owner_protocol(item, output)
        elif item.kind == "foundation_protocol":
            draw_foundation_protocol(item, output)
        elif item.kind == "context":
            draw_context_map(item, output)
        elif item.kind == "matrix":
            draw_matrix(item, output)
        else:
            draw_timeline(item, output)
        (output / item.file).with_suffix(".mmd").write_text(mermaid(item), encoding="utf-8")
    (output / "README.md").write_text(readme(items), encoding="utf-8")
    print(f"Generated {len(items)} deep-dive PNG assets into {output}")


if __name__ == "__main__":
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_OUT
    render(out)

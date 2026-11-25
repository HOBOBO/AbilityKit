# MOBA 实机问题采集包契约（L1）

这份契约规定真实 Unity 运行中排查 HUD、输入命令和技能释放链路时的旁路采集格式。采集器只观察已有边界，不参与技能、HFSM、Pipeline、Trigger、Projectile、伤害或表现决策。

## 目录和生命周期

采集由 `BattleHudFeature.TryStartInputCapture` 显式开启，由 `StopInputCapture` 或 HUD 销毁关闭。默认不开启。每次运行使用唯一的 `caseId/runId`：

在编辑器或 Development Build 中，也可以按 **F8** 切换采集。第一次按键创建默认的 `runtime-input/<runId>`，再次按键停止并关闭 manifest。快捷键只存在于诊断层，不进入正式发布构建，也不占用技能按键。

```text
<root>/<caseId>/<runId>/
  capture-manifest.json
  input.jsonl
  recording.mp4              # 可选，由外部录屏工具生成
  Player.log                 # 可选，复制或记录路径
  trace.jsonl                 # 后续 L2 扩展，不由 L1 伪造
  snapshots.jsonl             # 后续 L2 扩展
  presentation-cues.jsonl     # 后续表现层证据
```

`caseId` 表示可复现问题，例如 `jhin-w-real-input`、`jinx-q-real-input`；`runId` 表示一次具体运行，建议使用时间戳或 CI run id。禁止使用“最终版”“最新视频”等不可追踪名称。

L1 不按英雄或技能硬编码。一次采集可以包含局内所有英雄和技能槽位；通过 `slot`、输入命令 payload、录像标记以及 `caseId` 进行归档。需要只分析某个技能时，在该次运行开始前使用专用 `caseId`，或在操作前后调用 `MarkInputCapture` 标记时间段。

## manifest

`capture-manifest.json` 当前版本为 `moba-battle-capture.v1`：

```json
{
  "schema": "moba-battle-capture.v1",
  "caseId": "jhin-w-real-input",
  "runId": "20261105-153000",
  "source": "unity-hud-l1",
  "status": "active"
}
```

停止采集后 `status` 写为 `closed`。如果写盘失败，采集器停止写入并保持战斗继续运行；这类失败不能传播到战斗 Tick。

## input.jsonl

每行是独立 JSON，包含递增 `seq`、UTC 时间和 `type`。L1 记录两类证据：

### HUD 事件

`type` 为 `hud`，`kind` 取值包括：

- `move.begin`、`move.update`、`move.end`
- `skill.click`
- `skill.aim_start`、`skill.aim_update`、`skill.aim_cancel`、`skill.aim_end`

瞄准事件同时保存原始输入和经过现有投影规则得到的值：

```json
{"type":"hud","kind":"skill.aim_end","slot":2,
 "inputX":0.72,"inputY":0.11,"resolvedX":6.48,"resolvedY":0.99}
```

### 输入命令

`type` 为 `input_command`，保存 frame、player、opcode、是否被现有输入门接受以及原始 payload 的 Base64：

```json
{"type":"input_command","frame":1842,"player":"p1",
 "opCode":12,"accepted":true,"payloadBase64":"..."}
```

命令记录发生在现有 `InputRecordWriter.Append` 之后、`Session.SubmitInput` 之前，因此不会改变命令提交顺序。

### 人工标记

`type` 为 `mark`，用于把录像时间点和输入证据关联起来：

```json
{"type":"mark","label":"w-release-no-projectile","frame":1842}
```

录像是视觉和操作证据，不是权威玩法判定。技能是否命中、是否扣血、是否生成 Projectile，应以后续权威 trace、snapshot 和关系断言为准。

## 排查顺序

对烬 W 这类问题，先在同一 `caseId/runId` 中确认：

1. `skill.aim_start/update/end` 是否存在，`resolved` 方向是否合理；
2. 对应 `input_command` 是否存在且 `accepted=true`；
3. 权威 trace 是否产生 `SkillAimRelease` 和 `SkillCast`；
4. 是否产生 Projectile spawn/launch、命中和伤害；
5. 最后再对照录像、模型动作和 VFX cue。

L1 只覆盖前两步，后续 `trace.jsonl`、`snapshots.jsonl` 和 `presentation-cues.jsonl` 属于 L2 扩展，必须由权威观察点产生，不能为了让验收通过而从 HUD 事件推断或伪造。

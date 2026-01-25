---
trigger: manual
---

# 表现（Presentation）规则

## 1) 架构原则

- **事件驱动表现**：
  - 持续表现：走 Buff/Effect 的 `IGameplayEffectCue` 生命周期
  - 瞬时表现：TriggerAction 发布 `presentation.play` / `presentation.stop`，表现层订阅处理

## 2) 表现模板（Presentation Template）规则

- 表现模板表：`presentation_templates`
- 只存“静态默认值”：
  - 资源 id、默认时长、附着方式、stack/stop policy、默认颜色/scale/offset 等

## 3) Trigger 驱动表现（play_presentation）

### 3.1 输入 args 约定

- `templateId` (int, 必填)
- `targetMode` (int enum, 默认 Target)
- `queryTemplateId` (int, 可选)
- `target` (object, 可选显式目标)
- `requestKey` (string, 可选，用于 stop/replace)
- `durationMs` (int, 可选覆盖)
- `posKey` (string) / `pos` (vec3)（可选，用于位置表现）
- `stop` (bool, 可选；true=stop)
- 动态覆盖：`scale`/`radius`/`color` ...

### 3.2 行为

- 解析目标集合（actorIds 或 positions）
- 发布事件：
  - `presentation.play` 或 `presentation.stop`
- 表现层订阅事件并实例化/停止实际表现资源

### 3.3 事件契约

- EventId：`presentation.play` / `presentation.stop`
- args 必须携带：
  - `templateId`
  - `targets`（actorId list）或 `positions`（vec3 list）等能驱动表现的字段
- `requestKey` 用于 stop/replace 定位（表现层自行实现策略）

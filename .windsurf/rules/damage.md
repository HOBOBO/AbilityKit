---
trigger: manual
---

# 伤害（Damage）规则

## 1) 结构要求（Spec / Parser / Resolver）

Damage 行为遵循 `Spec/Parser/Resolver` 拆分：

- `GiveDamageAction` / `TakeDamageAction`
  - **不允许**在 Action 内直接硬编码大量目标/公式解析逻辑
  - Action 只负责执行与调用服务应用伤害

## 2) 目标选择

目标选择抽到 `DamageTargetSelection`（或等价模块），并支持从以下来源取目标：

- context Source/Target
- 显式 target
- `queryTemplateId` 查询
- payload/vars

## 3) 数值计算

- Resolver 负责读取公式/参数并产出最终数值
- Action 负责调用服务应用伤害

## 4) 目标选择统一原则

伤害与表现统一采用“Option1”思路：

- 统一 `TargetMode`
- 可选 `queryTemplateId`
- 可选显式 target
- 支持从 payload/vars 取目标

目标：减少重复实现与分叉逻辑。

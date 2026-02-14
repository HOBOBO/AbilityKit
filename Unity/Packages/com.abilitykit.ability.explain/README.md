# com.abilitykit.ability.explain

本包提供一套**Editor 下的“可解释化/可视化”框架**：

- 以 Tree/Forest 的方式展示配置驱动的能力/技能逻辑。
- 支持自动发现子树（如 Projectile/Buff/Summon），默认折叠。
- 提供统一的跳转协议，由项目端实现跳转到强化编辑窗口。

本包不负责读表/写表，不实现业务规则；项目端通过扩展点注册 Provider/Resolver/Navigator。

设计文档：

- [Document/AbilityExplainDesign.md](Document/AbilityExplainDesign.md)

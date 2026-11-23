# 状态同步包文档

- [接入与契约](StateSyncDesign.md)：本地检查点、世界快照、预测层与在线会话的职责。
- [稳定性评审](StabilityReview.md)：已验证行为、兼容约束与发布缺口。
- [进程内状态对账方案（P0 已实施）](LocalReconciliationProposal.md)：局部移动、输入终结、重演降级和本地验收范围。
- [离线权威提交结果契约](LocalCommittedStateResult.md)：两处提交的交付顺序、证明边界与本地验收用例。
- [包级验收](../Tests/README.md)、[本地检查点示例](../Samples~/LocalCheckpoints/README.md)。
- [本地快照历史示例](../Samples~/LocalSnapshotHistory/README.md)：用相同接口替换存储后端；提交仍由宿主负责。
- [本地状态提交示例](../Samples~/LocalStateCommit/README.md)：宿主先校验和导入，只发布成功导入的状态与游标；失败不推进。

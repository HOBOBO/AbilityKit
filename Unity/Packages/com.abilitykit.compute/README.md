# AbilityKit 计算

`AbilityKit.Compute` 定义可移植的批量计算内核、后端链、验证、回退和观测契约。
它不安装全局后端，也不替代业务包的托管实现语义。

平台加速由应用装配入口显式选择。后端可以拒绝小批次或执行失败，
随后由后端链尝试下一个后端。接受加速结果前必须执行业务验证。

可选的 Unity Jobs 实现位于 `com.abilitykit.compute.unityjobs`。

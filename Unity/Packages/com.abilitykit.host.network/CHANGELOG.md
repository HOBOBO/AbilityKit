# Changelog — com.abilitykit.host.network

## [Unreleased]

- 按连接桥、传输装配、扩展契约整理目录；公开命名空间和签名不变，显式声明直接使用的 `network.runtime` 依赖。
- 启动先检查生命周期，构造失败时释放已创建的监听器；停止排空完成前不能重启、解绑或释放桥。连接和请求的线协议不变。

// ============================================================================
// AbilityKit.Modifiers - Strategy 模块
// ============================================================================
//
// 策略模块提供通用的修改器扩展能力，支持业务层自定义修改逻辑。
//
// 核心概念：
// - IStrategy: 策略接口，业务层实现
// - IStrategyRegistry: 策略注册表，业务层注册策略
// - StrategyContext: 策略上下文，携带执行数据
// - StrategyData: 策略数据，可序列化
// - StrategyExecutor: 策略执行器
// - IStrategyRepository: 策略仓储，管理实例
//
// 使用方式：
// 1. 业务层定义策略ID字符串（如 "state.set"、"tag.add"）
// 2. 业务层实现 IStrategy 接口
// 3. 业务层注册策略到 IStrategyRegistry
// 4. 使用 StrategyExecutor 或 IStrategyRepository 执行策略
//
// 示例：
// ```csharp
// // 1. 注册策略
// var registry = StrategyExtensions.CreateDefaultRegistry();
// registry.Register(new MyCustomStrategy());
//
// // 2. 创建策略数据
// var data = StrategyData.State("state.set", StrategyOperationKind.SaveAndSet,
//     "MovementMode", "Ghost", ownerKey: 12345);
//
// // 3. 执行策略
// var executor = new StrategyExecutor(registry);
// var result = executor.Execute(target, in data);
//
// // 4. 还原策略
// executor.Revert(target, in data);
// ```
//
// ============================================================================

// 核心接口
// IModifierHandler.cs 中已包含 IModifierContext

// 核心类型
// IStrategy.cs - IStrategy, IStrategyRegistry, StrategyRegistry
// StrategyContext.cs - StrategyContext, StrategyData, StrategyInstance, StrategyOperationKind
// StrategyExecutor.cs - StrategyExecutor, IStrategyRepository, StrategyRepository
// StrategyExtensions.cs - 扩展方法
// BuiltinStrategies.cs - 内置数值策略
// StateTagStrategies.cs - 内置状态/标签策略

// 可选引用
// - ModifierCalculator 支持策略模式
// - NumericModifierHandler 支持策略计算

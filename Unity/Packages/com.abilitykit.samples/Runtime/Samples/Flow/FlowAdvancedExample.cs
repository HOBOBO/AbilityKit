#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Nodes;
using AbilityKit.Ability.Flow.Pooling;
using AbilityKit.Core.Pooling;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Flow
{
    /// <summary>
    /// FlowAdvancedExample - Flow 复杂异步流程进阶示例
    /// 演示 Flow 模块的复杂流程编排：并行、竞态、超时、资源管理等
    /// </summary>
    [Sample]
    public sealed class FlowAdvancedExample : SampleBase
    {
        public override string Title => "Flow Advanced Patterns";
        public override string Description => "演示并行流程、竞态流程、超时处理、资源管理、Flow 节点组合";
        public override SampleCategory Category => SampleCategory.Flow;

        protected override void OnRun()
        {
            FlowPools.RegisterDefaultConfig();
            using var businessPoolOverride = FlowPools.RegisterOverride(
                config => config
                    .Pool<FlowContext>(FlowPools.ScopeName, defaultCapacity: 16, maxSize: 256, prewarmCount: 16, collectionCheck: true, key: FlowPools.ContextKey)
                    .Pool<FlowRunner>(FlowPools.ScopeName, defaultCapacity: 16, maxSize: 256, prewarmCount: 16, collectionCheck: true, key: FlowPools.RunnerKey)
                    .Pool<FlowSession>(FlowPools.ScopeName, defaultCapacity: 16, maxSize: 256, prewarmCount: 16, collectionCheck: true, key: FlowPools.SessionKey)
                    .PoolHost<object>(defaultCapacity: 8, maxSize: 128, prewarmCount: 0, collectionCheck: true)
                    .Pool<Dictionary<Type, object>>(FlowPools.ScopeName, defaultCapacity: 32, maxSize: 512, prewarmCount: 32, collectionCheck: true, key: FlowPools.ContextScopeMapKey)
                    .Pool<List<IFlowNode>>(FlowPools.ScopeName, defaultCapacity: 32, maxSize: 512, prewarmCount: 32, collectionCheck: true, key: FlowPools.StageNodeListKey)
                    .Pool<FlowCompletion>(FlowPools.ScopeName, defaultCapacity: 32, maxSize: 512, prewarmCount: 32, collectionCheck: true, key: FlowPools.CompletionKey),
                moduleName: "Sample.Flow.BusinessPools",
                source: "AbilityKit.Samples.Logic",
                priority: 200);

            Log("=== Flow 复杂异步流程进阶示例 ===");
            Output.Divider();

            // 1. Flow 模块概述
            Log("【1】Flow 模块概述");
            Output.Bullet("IFlowNode - 流程节点接口");
            Output.Bullet("FlowHost<T> - 流程执行宿主");
            Output.Bullet("FlowContext - 流程上下文");
            Output.Bullet("支持 async/await 风格的流程编排");
            Log("");

            // 2. 核心节点类型
            Log("【2】核心节点类型");
            Output.Bullet("Sequence - 顺序执行，所有子节点完成才算完成");
            Output.Bullet("Race - 竞态执行，第一个完成的子节点决定结果");
            Output.Bullet("Parallel - 并行执行，所有子节点同时运行");
            Output.Bullet("If/Else - 条件分支");
            Output.Bullet("While/Until - 循环控制");
            Output.Bullet("Await - 等待异步操作");
            Output.Bullet("Timeout - 超时控制");
            Output.Bullet("Retry - 重试机制");
            Log("");

            // 3. 流程图示
            Log("【3】流程控制模式");
            Log("");
            Log("  Sequence (顺序):");
            Log("    ┌───┐");
            Log("    │ A │──→┌───┐──→┌───┐");
            Log("    └───┘    │ B │    │ C │");
            Log("             └───┘    └───┘");
            Log("    A → B → C 依次执行");
            Log("");
            Log("  Race (竞态):");
            Log("    ┌───┐");
            Log("    │ A │──┐");
            Log("    └───┘  │  → result = first_completed");
            Log("    ┌───┐  │");
            Log("    │ B │──┘");
            Log("    └───┘");
            Log("");
            Log("  Parallel (并行):");
            Log("    ┌───┐");
            Log("    │ A │──→┐");
            Log("    └───┘    │");
            Log("    ┌───┐    │  → 完成所有");
            Log("    │ B │──→┤");
            Log("    └───┘    │");
            Log("    ┌───┐    │");
            Log("    │ C │──→┘");
            Log("    └───┘");
            Log("");

            // 4. 并行执行示例
            Log("【4】并行执行 (Parallel)");
            Log("");
            Log("  // 并行加载多个资源");
            Log("  var flow = new ParallelNode(");
            Log("      new LoadAssetNode(\"player_prefab\"),");
            Log("      new LoadAssetNode(\"enemy_prefab\"),");
            Log("      new LoadAssetNode(\"ui_skin\")");
            Log("  );");
            Log("");
            Log("  await flow.Execute(context);");
            Log("  ");
            Log("  // 所有资源加载完成后继续");
            Log("  var player = context.GetAsset(\"player_prefab\");");
            Log("  var enemy = context.GetAsset(\"enemy_prefab\");");
            Log("");

            // 5. 竞态执行示例
            Log("【5】竞态执行 (Race)");
            Log("");
            Log("  // 等待任意一个完成");
            Log("  var flow = new RaceNode(");
            Log("      new WaitForServerResponse(timeout: 5f),");
            Log("      new WaitForCache(timeout: 2f)");
            Log("  );");
            Log("");
            Log("  var result = await flow.Execute(context);");
            Log("  ");
            Log("  if (result.Source == \"Cache\")");
            Log("  {");
            Log("      // 使用缓存数据");
            Log("  }");
            Log("  else");
            Log("  {");
            Log("      // 使用服务器响应");
            Log("  }");
            Log("");

            // 6. 超时处理
            Log("【6】超时处理 (Timeout)");
            Log("");
            Log("  // 带超时的异步操作");
            Log("  var flow = new TimeoutNode(");
            Log("      inner: new HttpRequestNode(\"api/data\"),");
            Log("      timeoutSeconds: 3f");
            Log("  );");
            Log("");
            Log("  var result = await flow.Execute(context);");
            Log("  ");
            Log("  if (result.IsTimeout)");
            Log("  {");
            Log("      Log(\"请求超时，使用默认数据\");");
            Log("      // 使用缓存或默认值");
            Log("  }");
            Log("");

            // 7. 重试机制
            Log("【7】重试机制 (Retry)");
            Log("");
            Log("  // 带重试的网络请求");
            Log("  var flow = new RetryNode(");
            Log("      inner: new HttpRequestNode(\"api/retry\"),");
            Log("      maxRetries: 3,");
            Log("      retryDelay: 1f,");
            Log("      exponentialBackoff: true");
            Log("  );");
            Log("");
            Log("  try");
            Log("  {");
            Log("      await flow.Execute(context);");
            Log("  }");
            Log("  catch (MaxRetriesExceededException)");
            Log("  {");
            Log("      Log(\"重试次数用尽，显示错误\");");
            Log("  }");
            Log("");

            // 8. 资源管理
            Log("【8】资源管理 (Using/Dispose)");
            Log("");
            Log("  // 使用资源并确保释放");
            Log("  var flow = new UsingResourceNode<DatabaseConnection>(");
            Log("      create: () => OpenDatabase(),");
            Log("      use: async (conn, ctx) => {");
            Log("          var data = await conn.QueryAsync(\"SELECT * FROM users\");");
            Log("          return data;");
            Log("      },");
            Log("      dispose: conn => conn.Close()");
            Log("  );");
            Log("");
            Log("  // 无论成功还是异常，资源都会被正确释放");
            Log("  var result = await flow.Execute(context);");
            Log("");

            // 9. 完整流程示例
            Log("【9】完整流程示例 - 技能施法流程");
            Log("");
            Log("  var skillCastFlow = new SequenceNode(");
            Log("  {");
            Log("      // 1. 检查条件");
            Log("      new ConditionNode(() => CanCastSkill(),");
            Log("          new SequenceNode(");
            Log("          {");
            Log("              // 2. 消耗资源");
            Log("              new ConsumeManaNode(skill.ManaCost),");
            Log("              ");
            Log("              // 3. 播放施法前摇（并行：动画 + 音效）");
            Log("              new ParallelNode(");
            Log("              {");
            Log("                  new PlayAnimationNode(\"cast\"),");
            Log("                  new PlaySoundNode(\"cast_voice\")");
            Log("              }),");
            Log("              ");
            Log("              // 4. 等待前摇完成");
            Log("              new WaitSecondsNode(skill.CastTime),");
            Log("              ");
            Log("              // 5. 释放技能效果（竞态：服务器确认 vs 客户端预测）");
            Log("              new RaceNode(");
            Log("              {");
            Log("                  new ServerConfirmNode(),");
            Log("                  new LocalPredictionNode()");
            Log("              }),");
            Log("              ");
            Log("              // 6. 播放后摇");
            Log("              new PlayAnimationNode(\"idle\")");
            Log("          })");
            Log("      }),");
            Log("      ");
            Log("      // 条件不满足时");
            Log("      new LogNode(\"Cannot cast skill\")");
            Log("  };");
            Log("");

            // 10. 流程组合
            Log("【10】流程组合模式");
            Log("");
            Log("  // 复用子流程");
            Log("  var attackCombo = new SequenceNode(");
            Log("  {");
            Log("      CreateAttackFlow(\"LightAttack\"),");
            Log("      new WaitSecondsNode(0.3f),");
            Log("      CreateAttackFlow(\"HeavyAttack\")");
            Log("  });");
            Log("");
            Log("  // 流程工厂方法");
            Log("  FlowNode CreateAttackFlow(string attackType) =>");
            Log("      new SequenceNode(");
            Log("      {");
            Log("          new PlayAnimationNode(attackType),");
            Log("          new ApplyDamageNode(attackType),");
            Log("          new SpawnEffectNode(attackType)");
            Log("      });");
            Log("");

            // 11. 状态管理
            Log("【11】FlowContext 状态管理");
            Log("");
            Log("  // 在流程中存储和获取数据");
            Log("  context.SetData(\"total_damage\", 0);");
            Log("  ");
            Log("  // 在节点中更新数据");
            Log("  var node = new LambdaNode(async ctx =>");
            Log("  {");
            Log("      var damage = CalculateDamage();");
            Log("      var total = ctx.GetData<int>(\"total_damage\");");
            Log("      ctx.SetData(\"total_damage\", total + damage);");
            Log("  });");
            Log("");

            // 12. 事件和回调
            Log("【12】事件和回调");
            Log("");
            Log("  // 流程状态事件");
            Log("  flow.OnStart += () => Log(\"流程开始\");");
            Log("  flow.OnProgress += (p) => UpdateProgressBar(p);");
            Log("  flow.OnComplete += () => Log(\"流程完成\");");
            Log("  flow.OnError += (e) => Log($\"错误: {e.Message}\");");
            Log("  flow.OnCancel += () => Log(\"流程取消\");");
            Log("");

            // 13. 取消和中止
            Log("【13】取消和中止");
            Log("");
            Log("  var cts = new CancellationTokenSource();");
            Log("  ");
            Log("  // 启动流程");
            Log("  var task = flow.ExecuteAsync(context, cts.Token);");
            Log("  ");
            Log("  // 取消流程");
            Log("  cts.Cancel();");
            Log("  ");
            Log("  // 等待取消完成");
            Log("  try { await task; }");
            Log("  catch (OperationCanceledException) { }");
            Log("");

            // 14. API 参考
            Log("【14】关键 API 参考");
            Output.Bullet("AbilityKit.Ability.Flow");
            Output.Bullet("AbilityKit.Ability.Flow.Nodes");
            Output.Bullet("AbilityKit.Ability.Flow.Blocks");
            Output.Bullet("AbilityKit.Ability.Flow.FlowHost");
            Output.Bullet("AbilityKit.Ability.Flow.FlowContext");
            Output.Bullet("AbilityKit.Ability.Flow.FlowGraph - 推荐的正式节点工厂入口");
            Output.Bullet("IFlowNode.Execute - 一次性同步执行节点图并自动管理 Runner/Context 生命周期");
            Output.Bullet("FlowExecutionOptions / FlowExecutionResult - 统一执行边界、异常和结果契约");
            Output.Bullet("AbilityKit.Ability.Flow.Pooling.FlowPools - Flow 包默认池化配置入口");
            Output.Bullet("FlowPools.RegisterOverride - 业务包高优先级覆盖 Flow 池容量、预热数量与裁剪策略");
            Output.Bullet("FlowPools.RentSession / ReleaseSession - 会级生命周期池化入口，递归管理 Runner 与 Context");
            Output.Bullet("FlowPools.PoolHost<TArgs> - 按实际参数类型配置泛型 Host 池；FlowHost<object> 不会覆盖其他 TArgs");
            Output.Bullet("FlowPools.RentHost<TArgs> / ReleaseHost<TArgs> - 宿主级生命周期池化入口，适合业务侧显式租借释放");
            Output.Bullet("FlowPools.RentCompletion / ReleaseCompletion - 可重置异步完成对象池化入口");
            Output.Bullet("FlowPools.RentEventQueue<TEvent> / ReleaseEventQueue<TEvent> - HFSM 事件队列池化入口");
            Log("");

            Output.Divider();
            Log("【总结】Flow 提供声明式的异步流程编排能力，适合复杂的时间序列和状态管理场景");
        }
    }
}

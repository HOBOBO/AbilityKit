using System;
using AbilityKit.HFSM;
using AbilityKit.Samples.Abstractions;
using AbilityKit.Samples.Logic.Infrastructure.Config;
using Newtonsoft.Json;

namespace AbilityKit.Samples.Logic.Samples.StateMachine
{
    /// <summary>
    /// HFSMJsonConfig - 演示如何从 JSON 配置构建状态机
    /// </summary>
    [Sample]
    public sealed class HFSMJsonConfig : SampleBase
    {
        public override string Title => "HFSM JSON Config";
        public override string Description => "从 JSON 配置构建分层有限状态机";
        public override SampleCategory Category => SampleCategory.StateMachine;

        private HFSMConfig _config;
        private HFSMConfigBuilder _builder;
        private AbilityKit.HFSM.StateMachine _fsm;

        protected override void OnRun()
        {
            Log("=== HFSM JSON 配置示例 ===");
            Output.Divider();

            // 1. 加载配置
            Log("【1】加载 HFSM 配置");
            var configJson = GetEmbeddedConfig();
            _config = JsonConvert.DeserializeObject<HFSMConfig>(configJson);
            Log($"配置名称: {_config.Name}");
            Log($"版本: {_config.Version}");
            Log($"状态数量: {_config.States.Count}");
            Log($"转换数量: {_config.Transitions.Count}");
            Log("");

            // 2. 显示状态列表
            Log("【2】状态定义");
            foreach (var state in _config.States)
            {
                Log($"  - {state.Id}: {state.Description}");
                if (state.NeedsExitTime)
                {
                    Log($"    (需要退出时间)");
                }
            }
            Log("");

            // 3. 显示参数
            Log("【3】参数定义");
            foreach (var param in _config.Parameters)
            {
                Log($"  - {param.Key}: {param.Value.Type} = {param.Value.DefaultValue}");
            }
            Log("");

            // 4. 构建状态机
            Log("【4】构建状态机");
            _builder = new HFSMConfigBuilder(_config, Log);
            _fsm = _builder.Build();
            _fsm.Init();
            Log($"状态机已初始化");
            Log($"初始状态: {_fsm.ActiveStateName}");
            Log("");

            // 5. 模拟运行
            Log("【5】模拟状态机运行");
            Log("--- 帧 1 ---");
            _fsm.OnLogic();
            Log("");

            Log("--- 设置 isMoving = true ---");
            _builder.Parameters.Set("isMoving", true);
            Log("");

            Log("--- 帧 2 ---");
            _fsm.OnLogic();
            Log($"当前状态: {_fsm.ActiveStateName}");
            Log("");

            Log("--- 设置 attackPressed = true ---");
            _builder.Parameters.Set("attackPressed", true);
            Log("");

            Log("--- 帧 3 ---");
            _fsm.OnLogic();
            Log($"当前状态: {_fsm.ActiveStateName}");
            Log("");

            // 6. 测试全局转换
            Log("【6】测试全局转换（任意状态 -> Dead）");
            Log("--- 设置 health = 0 ---");
            _builder.Parameters.Set("health", 0f);
            Log("");

            Log("--- 帧 4 ---");
            _fsm.OnLogic();
            Log($"当前状态: {_fsm.ActiveStateName}");
            Log("");

            // 7. 重置并重新运行
            Log("【7】重置状态机");
            _fsm = _builder.Build();
            _fsm.Init();
            Log($"已重置，初始状态: {_fsm.ActiveStateName}");

            Output.Divider();
        }

        private string GetEmbeddedConfig()
        {
            return @"{
  ""Name"": ""CharacterFSM"",
  ""Version"": ""1.0.0"",
  ""Description"": ""角色状态机配置示例"",
  ""States"": [
    {
      ""Id"": ""Idle"",
      ""Type"": ""State"",
      ""Description"": ""空闲状态"",
      ""OnEnter"": { ""Type"": ""Log"", ""Message"": ""进入 Idle 状态"" },
      ""OnLogic"": { ""Type"": ""Log"", ""Message"": ""Idle: 等待中..."" }
    },
    {
      ""Id"": ""Move"",
      ""Type"": ""State"",
      ""Description"": ""移动状态"",
      ""NeedsExitTime"": false,
      ""OnEnter"": { ""Type"": ""Log"", ""Message"": ""进入 Move 状态"" },
      ""OnLogic"": { ""Type"": ""Log"", ""Message"": ""Move: 移动中..."" }
    },
    {
      ""Id"": ""Attack"",
      ""Type"": ""State"",
      ""Description"": ""攻击状态"",
      ""NeedsExitTime"": true,
      ""OnEnter"": { ""Type"": ""Log"", ""Message"": ""进入 Attack 状态"" },
      ""OnLogic"": { ""Type"": ""Log"", ""Message"": ""Attack: 攻击!"" }
    },
    {
      ""Id"": ""Dead"",
      ""Type"": ""State"",
      ""Description"": ""死亡状态"",
      ""NeedsExitTime"": false,
      ""OnEnter"": { ""Type"": ""Log"", ""Message"": ""进入 Dead 状态"" }
    }
  ],
  ""Transitions"": [
    { ""From"": ""Idle"", ""To"": ""Move"", ""Condition"": { ""Type"": ""Parameter"", ""Name"": ""isMoving"", ""Value"": true } },
    { ""From"": ""Idle"", ""To"": ""Attack"", ""Condition"": { ""Type"": ""Parameter"", ""Name"": ""attackPressed"", ""Value"": true } },
    { ""From"": ""Move"", ""To"": ""Idle"", ""Condition"": { ""Type"": ""Parameter"", ""Name"": ""isMoving"", ""Value"": false } },
    { ""From"": ""Move"", ""To"": ""Attack"", ""Condition"": { ""Type"": ""Parameter"", ""Name"": ""attackPressed"", ""Value"": true } },
    { ""From"": ""Attack"", ""To"": ""Idle"", ""Condition"": null, ""Delay"": 1.0 },
    { ""From"": ""*"", ""To"": ""Dead"", ""Condition"": { ""Type"": ""Parameter"", ""Name"": ""health"", ""Compare"": ""<="", ""Value"": 0 } }
  ],
  ""InitialState"": ""Idle"",
  ""Parameters"": {
    ""isMoving"": { ""Type"": ""Bool"", ""DefaultValue"": false },
    ""attackPressed"": { ""Type"": ""Bool"", ""DefaultValue"": false },
    ""health"": { ""Type"": ""Float"", ""DefaultValue"": 100.0 }
  }
}";
        }
    }
}

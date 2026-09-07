using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AbilityKit.HFSM;
using AbilityKit.Samples.Abstractions;
using AbilityKit.Samples.Logic.Infrastructure.Config;

namespace AbilityKit.Samples.Logic.Samples.Modifiers
{
    /// <summary>
    /// DataDrivenHFSMWithOngoingBehaviors - 数据驱动的 HFSM + 持续行为系统
    /// 
    /// 本示例展示：
    /// 1. 从 JSON 配置加载 HFSM 状态机配置
    /// 2. 从 JSON 配置加载持续行为(Buff/Debuff)配置
    /// 3. 持续行为修改 HFSM 参数
    /// 4. 参数变化触发状态机自动切换
    /// 
    /// 配置文件:
    /// - HFSM/WarriorHFSM.json: 状态机配置
    /// - OngoingBehavior/WarriorOngoingBehaviors.json: 持续行为配置
    /// </summary>
    [Sample]
    public sealed class DataDrivenHFSMWithOngoingBehaviors : SampleBase
    {
        public override string Title => "数据驱动 HFSM + 持续行为";
        public override string Description => "分离的 HFSM 配置和持续行为配置";
        public override SampleCategory Category => SampleCategory.Modifiers;

        private HFSMConfig _fsmConfig;
        private OngoingBehaviorSetConfig _ongoingConfig;
        private WarriorWithDataDrivenFSM _warrior;

        protected override void OnRun()
        {
            Log("================================================================================");
            Log("===         数据驱动 HFSM + 持续行为系统 (分离配置)              ===");
            Log("================================================================================");
            Output.Divider();
            
            // 1. 加载 HFSM 配置
            Log("【1】加载 HFSM 状态机配置");
            if (!LoadFSMConfig())
            {
                Error("HFSM 配置加载失败");
                return;
            }
            Log($"  配置文件: HFSM/WarriorHFSM.json");
            Log($"  版本: {_fsmConfig.Version}");
            Log($"  初始状态: {_fsmConfig.InitialState}");
            Log($"  参数数量: {_fsmConfig.Parameters.Count}");
            Log($"  状态数量: {_fsmConfig.States.Count}");
            Log($"  转换数量: {_fsmConfig.Transitions.Count}");
            Log("");
            
            // 2. 加载持续行为配置
            Log("【2】加载持续行为配置");
            if (!LoadOngoingConfig())
            {
                Error("持续行为配置加载失败");
                return;
            }
            Log($"  配置文件: OngoingBehavior/WarriorOngoingBehaviors.json");
            Log($"  版本: {_ongoingConfig.Version}");
            Log($"  目标 FSM: {_ongoingConfig.TargetFsm}");
            Log($"  行为数量: {_ongoingConfig.Behaviors.Count}");
            foreach (var behavior in _ongoingConfig.Behaviors)
            {
                Log($"    - {behavior.Name} ({behavior.Category}): {behavior.Description}");
            }
            Log("");
            
            Output.Divider();
            
            // 3. 创建状态机
            Log("【3】根据 HFSM 配置创建状态机");
            _warrior = new WarriorWithDataDrivenFSM(this.Log, _fsmConfig, _ongoingConfig);
            _warrior.Initialize();
            
            Output.Divider();
            
            // 4. 模拟游戏循环
            Log("【4】模拟游戏循环");
            Output.Line();
            
            // 初始状态
            Log("  --- 初始状态 ---");
            _warrior.PrintStatus();
            
            // 进入战斗
            Log("");
            Log("  --- 设置 IsCombat=true (HFSM 转换: Idle -> Combat) ---");
            _warrior.SetBool("IsCombat", true);
            _warrior.Tick();
            _warrior.PrintStatus();
            
            // 触发狂暴
            Log("");
            Log("  --- 设置 RageMode=true (HFSM 转换: Combat -> Rage) ---");
            _warrior.SetBool("RageMode", true);
            _warrior.Tick();
            _warrior.PrintStatus();
            
            // 生命值归零
            Log("");
            Log("  --- Health=0 (HFSM 转换: Rage -> Dead) ---");
            _warrior.SetFloat("Health", 0f);
            _warrior.Tick();
            _warrior.PrintStatus();
            
            Output.Divider();
            
            // 5. 使用持续行为
            Log("【5】使用持续行为 (Buff/Debuff)");
            Output.Line();
            
            Log("  --- 应用【战神之力】Buff ---");
            _warrior.ApplyOngoingBehavior("god_of_war_power");
            _warrior.Tick();
            _warrior.PrintStatus();
            
            Log("");
            Log("  --- 应用【狂暴之力】Buff ---");
            _warrior.ApplyOngoingBehavior("rage_power");
            _warrior.Tick();
            _warrior.PrintStatus();
            
            Log("");
            Log("  --- 应用【中毒】Debuff (5秒) ---");
            _warrior.ApplyOngoingBehavior("poison_debuff");
            _warrior.Tick();
            _warrior.PrintStatus();
            
            // 模拟多帧（触发持续效果）
            Log("");
            Log("  --- 模拟 3 秒中毒扣血 ---");
            for (int i = 0; i < 3; i++)
            {
                _warrior.TickWithTime(1.0f);
            }
            _warrior.PrintStatus();
            
            Log("");
            Log("  --- 应用【治疗祝福】Buff (可叠加) ---");
            _warrior.ApplyOngoingBehavior("healing_blessing");
            _warrior.TickWithTime(2.0f);
            _warrior.PrintStatus();
            
            Log("");
            Log("  --- 叠加 2 层治疗祝福 ---");
            _warrior.ApplyOngoingBehavior("healing_blessing");
            _warrior.ApplyOngoingBehavior("healing_blessing");
            _warrior.TickWithTime(2.0f);
            _warrior.PrintStatus();
            
            Output.Divider();
            
            // 6. 移除持续行为
            Log("【6】移除持续行为");
            Output.Line();
            
            Log("  --- 移除【狂暴之力】---");
            _warrior.RemoveOngoingBehavior("rage_power");
            _warrior.Tick();
            _warrior.PrintStatus();
            
            Output.Divider();
            
            // 7. 技术要点
            Log("【7】配置分离架构");
            Output.Bullet("HFSM/WarriorHFSM.json: 定义状态机、状态、转换、参数");
            Output.Bullet("OngoingBehavior/WarriorOngoingBehaviors.json: 定义 Buff/Debuff 行为");
            Output.Bullet("持续行为通过修改 HFSM 参数触发状态切换");
            Log("");
            
            Log("【8】持续行为特性");
            Output.Bullet("Duration: 持续时间 (-1 表示永久)");
            Output.Bullet("TickInterval: 效果触发间隔 (0 表示即时)");
            Output.Bullet("StackPolicy: 叠加策略 (Refresh/Stack/Replace)");
            Output.Bullet("MaxStacks: 最大叠加层数");
            Log("");
            
            Output.Divider();
            Log("【总结】数据驱动的 HFSM + 持续行为系统");
            Log("       配置文件:");
            Log("         - src/AbilityKit.Samples/Configs/HFSM/WarriorHFSM.json");
            Log("         - src/AbilityKit.Samples/Configs/OngoingBehavior/WarriorOngoingBehaviors.json");
            Output.Divider();
        }

        private bool LoadFSMConfig()
        {
            try
            {
                var configPath = FindConfigFile("HFSM/WarriorHFSM.json");
                if (configPath == null)
                {
                    Log($"  使用内联 HFSM 配置");
                    _fsmConfig = CreateInlineFSMConfig();
                    return true;
                }

                Log($"  找到配置: {configPath}");
                var json = File.ReadAllText(configPath);
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                _fsmConfig = JsonSerializer.Deserialize<HFSMConfig>(json, options);
                return true;
            }
            catch (Exception ex)
            {
                Log($"  加载失败: {ex.Message}");
                _fsmConfig = CreateInlineFSMConfig();
                return true;
            }
        }

        private bool LoadOngoingConfig()
        {
            try
            {
                var configPath = FindConfigFile("OngoingBehavior/WarriorOngoingBehaviors.json");
                if (configPath == null)
                {
                    Log($"  使用内联持续行为配置");
                    _ongoingConfig = CreateInlineOngoingConfig();
                    return true;
                }

                Log($"  找到配置: {configPath}");
                var json = File.ReadAllText(configPath);
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                _ongoingConfig = JsonSerializer.Deserialize<OngoingBehaviorSetConfig>(json, options);
                return true;
            }
            catch (Exception ex)
            {
                Log($"  加载失败: {ex.Message}");
                _ongoingConfig = CreateInlineOngoingConfig();
                return true;
            }
        }

        private string FindConfigFile(string relativePath)
        {
            var searchPaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Configs", relativePath),
                Path.Combine(AppContext.BaseDirectory, "..", "Configs", relativePath),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "Configs", relativePath),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Configs", relativePath),
                Path.Combine(AppContext.BaseDirectory, "Configs", "..", relativePath),
                "Configs/" + relativePath,
                "../../Configs/" + relativePath,
                "../../../Configs/" + relativePath
            };

            foreach (var path in searchPaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            return null;
        }

        private HFSMConfig CreateInlineFSMConfig()
        {
            return new HFSMConfig
            {
                Name = "WarriorHFSM",
                Version = "1.0.0",
                Description = "战士 HFSM",
                InitialState = "Idle",
                Parameters = new Dictionary<string, HFSMParameterConfig>
                {
                    ["Health"] = new HFSMParameterConfig { Type = "Float", DefaultValue = 100f },
                    ["IsCombat"] = new HFSMParameterConfig { Type = "Bool", DefaultValue = 0f },
                    ["RageMode"] = new HFSMParameterConfig { Type = "Bool", DefaultValue = 0f }
                },
                States = new List<HFSMStateConfig>
                {
                    new HFSMStateConfig { Id = "Idle", Description = "待机状态" },
                    new HFSMStateConfig { Id = "Combat", Description = "战斗状态" },
                    new HFSMStateConfig { Id = "Rage", Description = "狂暴状态" },
                    new HFSMStateConfig { Id = "Dead", Description = "死亡状态" }
                },
                Transitions = new List<HFSMTransitionConfig>
                {
                    new HFSMTransitionConfig { From = "Idle", To = "Combat", Condition = new HFSMConditionConfig { Type = "ParameterBool", ParameterName = "IsCombat", Value = 1 } },
                    new HFSMTransitionConfig { From = "Combat", To = "Idle", Condition = new HFSMConditionConfig { Type = "ParameterBool", ParameterName = "IsCombat", Value = 0 } },
                    new HFSMTransitionConfig { From = "Combat", To = "Rage", Condition = new HFSMConditionConfig { Type = "ParameterBool", ParameterName = "RageMode", Value = 1 } },
                    new HFSMTransitionConfig { From = "Rage", To = "Combat", Condition = new HFSMConditionConfig { Type = "ParameterBool", ParameterName = "RageMode", Value = 0 } },
                    new HFSMTransitionConfig { From = "Idle", To = "Dead", Condition = new HFSMConditionConfig { Type = "ParameterFloat", ParameterName = "Health", Operator = "LessOrEqual", Value = 0 } },
                    new HFSMTransitionConfig { From = "Combat", To = "Dead", Condition = new HFSMConditionConfig { Type = "ParameterFloat", ParameterName = "Health", Operator = "LessOrEqual", Value = 0 } },
                    new HFSMTransitionConfig { From = "Rage", To = "Dead", Condition = new HFSMConditionConfig { Type = "ParameterFloat", ParameterName = "Health", Operator = "LessOrEqual", Value = 0 } },
                    new HFSMTransitionConfig { From = "Dead", To = "Idle", Condition = new HFSMConditionConfig { Type = "ParameterFloat", ParameterName = "Health", Operator = "GreaterThan", Value = 0 } }
                }
            };
        }

        private OngoingBehaviorSetConfig CreateInlineOngoingConfig()
        {
            return new OngoingBehaviorSetConfig
            {
                Name = "WarriorOngoingBehaviors",
                Version = "1.0.0",
                TargetFsm = "WarriorHFSM",
                Behaviors = new List<OngoingBehaviorConfig>
                {
                    new OngoingBehaviorConfig
                    {
                        Id = "god_of_war_power", Name = "战神之力",
                        Category = "Buff", Duration = -1,
                        Effects = new List<OngoingEffectConfig>
                        {
                            new OngoingEffectConfig { Type = "AddFloat", Target = "Health", Value = 30 },
                            new OngoingEffectConfig { Type = "SetBool", Target = "IsCombat", Value = 1 }
                        }
                    },
                    new OngoingBehaviorConfig
                    {
                        Id = "rage_power", Name = "狂暴之力",
                        Category = "Buff", Duration = -1,
                        Effects = new List<OngoingEffectConfig>
                        {
                            new OngoingEffectConfig { Type = "SetBool", Target = "RageMode", Value = 1 }
                        }
                    }
                }
            };
        }
    }

    // ============================================
    // 数据驱动的状态机实现
    // ============================================

    /// <summary>
    /// 数据驱动的战士状态机
    /// </summary>
    public class WarriorWithDataDrivenFSM
    {
        private readonly Action<string> _log;
        private readonly HFSMConfig _fsmConfig;
        private readonly OngoingBehaviorSetConfig _ongoingConfig;
        
        private StateMachine<string, string> _fsm;
        private HFSMParameters _parameters;
        private readonly Dictionary<string, float> _paramValues = new();
        
        public string CurrentState => _fsm?.ActiveStateName ?? "NotInitialized";
        
        // 活跃的持续行为
        private readonly Dictionary<string, ActiveOngoingBehavior> _activeBehaviors = new();
        
        // 时间追踪
        private float _totalTime = 0f;

        public WarriorWithDataDrivenFSM(Action<string> log, HFSMConfig fsmConfig, OngoingBehaviorSetConfig ongoingConfig)
        {
            _log = log;
            _fsmConfig = fsmConfig;
            _ongoingConfig = ongoingConfig;
        }

        public void Initialize()
        {
            _log("  初始化数据驱动状态机...");
            
            // 初始化参数
            _parameters = new HFSMParameters();
            foreach (var param in _fsmConfig.Parameters)
            {
                switch (param.Value.Type.ToLower())
                {
                    case "float":
                    case "int":
                        _parameters.AddFloat(param.Key, param.Value.DefaultValue);
                        _paramValues[param.Key] = param.Value.DefaultValue;
                        break;
                    case "bool":
                        _parameters.AddBool(param.Key, param.Value.DefaultValue > 0);
                        _paramValues[param.Key] = param.Value.DefaultValue;
                        break;
                    case "trigger":
                        _parameters.AddTrigger(param.Key);
                        break;
                }
            }
            
            // 创建状态机
            _fsm = new StateMachine<string, string>(needsExitTime: true);
            
            // 从配置创建状态
            foreach (var stateConfig in _fsmConfig.States)
            {
                CreateState(stateConfig);
            }
            
            // 从配置创建转换
            foreach (var transitionConfig in _fsmConfig.Transitions)
            {
                CreateTransition(transitionConfig);
            }
            
            // 设置初始状态
            _fsm.SetStartState(_fsmConfig.InitialState);
            _fsm.Init();
            
            _log($"  状态机已创建: {_fsmConfig.States.Count} 状态, {_fsmConfig.Transitions.Count} 转换");
        }

        private void CreateState(HFSMStateConfig config)
        {
            var state = new State<string, string>(
                onEnter: s => 
                {
                    if (config.OnEnter != null && config.OnEnter.Type == "Log")
                        _log($"    {config.OnEnter.Message}");
                    else
                        _log($"    [状态] 进入 {config.Id}");
                },
                onLogic: s =>
                {
                    if (config.OnLogic != null && config.OnLogic.Type == "Log")
                        _log($"    {config.OnLogic.Message}");
                }
            );
            
            _fsm.AddState(config.Id, state);
        }

        private void CreateTransition(HFSMTransitionConfig config)
        {
            var condition = CreateCondition(config.Condition);
            
            _fsm.AddTransition(new Transition<string>(
                from: config.From,
                to: config.To,
                condition: t => condition(),
                onTransition: t => _log($"    [转换] {config.From} -> {config.To}")
            ));
        }

        private Func<bool> CreateCondition(HFSMConditionConfig config)
        {
            return config.Type switch
            {
                "ParameterBool" => () => EvaluateBoolCondition(config),
                "ParameterFloat" => () => EvaluateFloatCondition(config),
                _ => () => false
            };
        }

        private bool EvaluateBoolCondition(HFSMConditionConfig config)
        {
            var value = _paramValues.TryGetValue(config.ParameterName, out var v) ? v > 0 : false;
            var expected = config.Value > 0;
            return config.Operator switch
            {
                "Equal" => value == expected,
                "NotEqual" => value != expected,
                _ => value == expected
            };
        }

        private bool EvaluateFloatCondition(HFSMConditionConfig config)
        {
            var value = _paramValues.TryGetValue(config.ParameterName, out var v) ? v : 0f;
            return config.Operator switch
            {
                "GreaterThan" => value > config.Value,
                "LessThan" => value < config.Value,
                "GreaterOrEqual" => value >= config.Value,
                "LessOrEqual" => value <= config.Value,
                "Equal" => Math.Abs(value - config.Value) < 0.001f,
                "NotEqual" => Math.Abs(value - config.Value) >= 0.001f,
                _ => false
            };
        }

        public void Tick()
        {
            TickWithTime(0.016f);
        }

        public void TickWithTime(float deltaTime)
        {
            if (_fsm == null) return;
            
            _totalTime += deltaTime;
            
            // 更新持续行为
            UpdateOngoingBehaviors(deltaTime);
            
            // 同步参数值
            foreach (var key in _paramValues.Keys)
            {
                _paramValues[key] = _parameters.GetFloat(key);
            }
            
            // 执行状态机逻辑
            _fsm.OnLogic();
            
            // 重置触发器
            _parameters.ResetTriggers();
        }

        private void UpdateOngoingBehaviors(float deltaTime)
        {
            var toRemove = new List<string>();
            
            foreach (var kvp in _activeBehaviors)
            {
                var behavior = kvp.Value;
                behavior.ElapsedTime += deltaTime;
                
                // 检查持续时间
                var config = behavior.Config;
                if (config.Duration > 0 && behavior.ElapsedTime >= config.Duration)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }
                
                // 检查触发间隔
                if (config.TickInterval > 0)
                {
                    if (behavior.ElapsedTime - behavior.LastTickTime >= config.TickInterval)
                    {
                        ApplyEffects(config.Effects);
                        behavior.LastTickTime = behavior.ElapsedTime;
                    }
                }
            }
            
            foreach (var id in toRemove)
            {
                _activeBehaviors.Remove(id);
                _log($"    [持续行为结束] {id}");
            }
        }

        private void ApplyEffects(List<OngoingEffectConfig> effects)
        {
            foreach (var effect in effects)
            {
                switch (effect.Type)
                {
                    case "AddFloat":
                        var current = _paramValues.TryGetValue(effect.Target, out var v) ? v : 0f;
                        var newValue = Math.Max(0, current + effect.Value);
                        _parameters.SetFloat(effect.Target, newValue);
                        _paramValues[effect.Target] = newValue;
                        _log($"      [效果] {effect.Target} += {effect.Value} -> {newValue}");
                        break;
                        
                    case "SetFloat":
                        _parameters.SetFloat(effect.Target, effect.Value);
                        _paramValues[effect.Target] = effect.Value;
                        _log($"      [效果] {effect.Target} = {effect.Value}");
                        break;
                        
                    case "SetBool":
                        var boolValue = effect.Value > 0;
                        _parameters.SetBool(effect.Target, boolValue);
                        _paramValues[effect.Target] = effect.Value;
                        _log($"      [效果] {effect.Target} = {boolValue}");
                        break;
                }
            }
        }

        public void SetFloat(string name, float value)
        {
            _parameters.SetFloat(name, value);
            _paramValues[name] = value;
        }

        public void SetBool(string name, bool value)
        {
            _parameters.SetBool(name, value);
            _paramValues[name] = value ? 1f : 0f;
        }

        /// <summary>
        /// 应用持续行为
        /// </summary>
        public void ApplyOngoingBehavior(string behaviorId)
        {
            var config = _ongoingConfig.Behaviors.Find(b => b.Id == behaviorId);
            if (config == null)
            {
                _log($"    [警告] 未找到持续行为: {behaviorId}");
                return;
            }
            
            // 检查叠加策略
            if (_activeBehaviors.TryGetValue(behaviorId, out var existing))
            {
                switch (config.StackPolicy.ToLower())
                {
                    case "refresh":
                        existing.ElapsedTime = 0;
                        _log($"    [刷新] {config.Name} 持续时间已重置");
                        return;
                        
                    case "stack":
                        if (existing.StackCount < config.MaxStacks)
                        {
                            existing.StackCount++;
                            ApplyEffects(config.Effects);
                            _log($"    [叠加] {config.Name} 层数: {existing.StackCount}/{config.MaxStacks}");
                        }
                        return;
                        
                    case "replace":
                        _activeBehaviors.Remove(behaviorId);
                        _log($"    [替换] {config.Name} 已重新应用");
                        break;
                }
            }
            
            // 立即应用效果
            ApplyEffects(config.Effects);
            
            // 添加新的持续行为
            _activeBehaviors[behaviorId] = new ActiveOngoingBehavior
            {
                Config = config,
                ElapsedTime = 0,
                LastTickTime = 0,
                StackCount = 1
            };
            
            _log($"    [应用] {config.Name} ({config.Category})");
        }

        /// <summary>
        /// 移除持续行为
        /// </summary>
        public void RemoveOngoingBehavior(string behaviorId)
        {
            if (_activeBehaviors.Remove(behaviorId))
            {
                var config = _ongoingConfig.Behaviors.Find(b => b.Id == behaviorId);
                _log($"    [移除] {config?.Name ?? behaviorId}");
            }
        }

        public void PrintStatus()
        {
            _log("");
            _log($"  ╔══════════════════════════════════════════════════════════════════╗");
            _log($"  ║ 状态: {CurrentState,-15}                                            ║");
            _log($"  ╠══════════════════════════════════════════════════════════════════╣");
            _log($"  ║ 参数值:                                                       ║");
            
            foreach (var param in _fsmConfig.Parameters)
            {
                var value = _paramValues.TryGetValue(param.Key, out var v) ? v : 0f;
                var displayValue = param.Value.Type == "Bool" ? (value > 0 ? "true" : "false") : value.ToString("F0");
                _log($"  ║   {param.Key,-12} = {displayValue,-8}                                             ║");
            }
            
            if (_activeBehaviors.Count > 0)
            {
                _log($"  ╠══════════════════════════════════════════════════════════════════╣");
                _log($"  ║ 活跃持续行为 ({_activeBehaviors.Count}):                                             ║");
                foreach (var kvp in _activeBehaviors)
                {
                    var remaining = kvp.Value.Config.Duration > 0 
                        ? $" (剩余 {kvp.Value.Config.Duration - kvp.Value.ElapsedTime:F1}s)" 
                        : "";
                    var stacks = kvp.Value.StackCount > 1 ? $" x{kvp.Value.StackCount}" : "";
                    _log($"  ║   - {kvp.Value.Config.Name,-15}{stacks,-8}{remaining,-30}      ║");
                }
            }
            _log($"  ╚══════════════════════════════════════════════════════════════════╝");
        }
    }

    /// <summary>
    /// 活跃的持续行为实例
    /// </summary>
    public class ActiveOngoingBehavior
    {
        public OngoingBehaviorConfig Config { get; set; }
        public float ElapsedTime { get; set; }
        public float LastTickTime { get; set; }
        public int StackCount { get; set; } = 1;
    }
}

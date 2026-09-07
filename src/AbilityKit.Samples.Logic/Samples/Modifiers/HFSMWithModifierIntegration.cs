using System;
using System.Collections.Generic;
using AbilityKit.HFSM;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Modifiers
{
    /// <summary>
    /// HFSMWithModifierIntegration - 修饰器驱动的 HFSM 参数状态切换
    /// 
    /// 本示例展示：
    /// 1. 使用 AbilityKit.HFSM 框架的 StateMachine 和 Transition
    /// 2. 实现类似 Unity Animator 的参数系统
    /// 3. 修饰器修改参数，参数变化触发条件判断
    /// 4. 条件满足时状态机自动切换状态
    /// 
    /// 使用的框架类型:
    /// - StateMachine<TStateId, TEvent>: 状态机 (AbilityKit.HFSM)
    /// - State<TStateId, TEvent>: 状态 (AbilityKit.HFSM)
    /// - Transition<TStateId>: 转换 (AbilityKit.HFSM)
    /// </summary>
    [Sample]
    public sealed class HFSMWithModifierIntegration : SampleBase
    {
        public override string Title => "修饰器驱动 HFSM 参数切换";
        public override string Description => "使用 AbilityKit.HFSM 框架的参数化状态机";
        public override SampleCategory Category => SampleCategory.Modifiers;

        protected override void OnRun()
        {
            Log("================================================================================");
            Log("===    修饰器驱动 HFSM 参数状态切换 (使用 AbilityKit.HFSM 框架)       ===");
            Log("================================================================================");
            Output.Divider();
            
            // 架构说明
            Log("【1】核心概念 (类似 Unity Animator)");
            Output.Bullet("StateMachine: AbilityKit.HFSM 状态机");
            Output.Bullet("Transition: AbilityKit.HFSM 状态转换");
            Output.Bullet("HFSMParameters: 参数容器，类似 Animator.parameters");
            Output.Bullet("ModifierSystem: 修饰器系统，修改参数触发状态切换");
            Log("");
            
            // 使用的框架类型
            Log("【2】使用的框架类型 (AbilityKit.HFSM)");
            Output.Bullet("StateMachine<string, string>: 状态机");
            Output.Bullet("State<string, string>: 状态 (带 onEnter/onLogic/onExit)");
            Output.Bullet("Transition<string>: 状态转换 (带条件判断)");
            Log("");
            
            // 场景设定
            Log("【3】场景设定");
            Log("  角色: 战士 (Warrior)");
            Log("  参数 (类似 Animator Parameters):");
            Log("    - Health: Float - 生命值 (0-100)");
            Log("    - IsCombat: Bool - 是否在战斗中");
            Log("    - RageMode: Bool - 狂暴模式");
            Log("  状态:");
            Log("    - Idle (待机)");
            Log("    - Combat (战斗)");
            Log("    - Rage (狂暴)");
            Log("    - Dead (死亡)");
            Log("");
            
            Output.Divider();
            
            // 创建状态机
            Log("【4】创建状态机");
            var warrior = new WarriorWithHFSM(this.Log);
            warrior.Initialize();
            
            Output.Divider();
            
            // 模拟游戏循环
            Log("【5】模拟游戏循环");
            Output.Line();
            
            // 初始状态
            Log("  --- 初始状态 (Idle) ---");
            warrior.PrintStatus();
            
            // 进入战斗
            Log("");
            Log("  --- 设置 IsCombat=true (切换到 Combat) ---");
            warrior.SetBool("IsCombat", true);
            warrior.Tick();
            warrior.PrintStatus();
            
            // 触发狂暴
            Log("");
            Log("  --- 设置 RageMode=true (切换到 Rage) ---");
            warrior.SetBool("RageMode", true);
            warrior.Tick();
            warrior.PrintStatus();
            
            // 生命值降低到危险线
            Log("");
            Log("  --- Health=30 (生命危险，但还在 Rage) ---");
            warrior.SetFloat("Health", 30f);
            warrior.Tick();
            warrior.PrintStatus();
            
            // 生命值归零
            Log("");
            Log("  --- Health=0 (触发 Dead) ---");
            warrior.SetFloat("Health", 0f);
            warrior.Tick();
            warrior.PrintStatus();
            
            Output.Divider();
            
            // 使用修饰器修改参数
            Log("【6】使用修饰器修改 HFSM 参数");
            Output.Line();
            
            Log("  --- 添加【战神之力】Buff ---");
            Log("    修改: Health += 30, 触发转换到 Combat");
            warrior.ApplyModifier(new HFSMModifierData
            {
                Name = "战神之力",
                HealthBonus = 30f,
                SetCombat = true
            });
            warrior.Tick();
            warrior.PrintStatus();
            
            Log("");
            Log("  --- 添加【狂暴之力】Buff ---");
            Log("    修改: RageMode = true, 触发转换到 Rage");
            warrior.ApplyModifier(new HFSMModifierData
            {
                Name = "狂暴之力",
                SetRageMode = true
            });
            warrior.Tick();
            warrior.PrintStatus();
            
            Log("");
            Log("  --- 移除【狂暴之力】 ---");
            warrior.RemoveModifier("狂暴之力");
            warrior.Tick();
            warrior.PrintStatus();
            
            Log("");
            Log("  --- 移除【战神之力】 ---");
            warrior.RemoveModifier("战神之力");
            warrior.Tick();
            warrior.PrintStatus();
            
            Output.Divider();
            
            // 技术要点
            Log("【7】技术要点");
            Output.Bullet("StateMachine.OnLogic(): 每帧检查所有 Transition 条件");
            Output.Bullet("Transition.ShouldTransition(): 检查条件是否满足");
            Output.Bullet("参数变化 -> 条件满足 -> 状态自动切换");
            Output.Bullet("修饰器修改参数 -> 触发条件判断 -> 状态转换");
            Log("");
            
            // 与 Unity Animator 对比
            Log("【8】与 Unity Animator 对比");
            Output.Bullet("StateMachine ≈ Animator");
            Output.Bullet("Transition ≈ Animator Transition");
            Output.Bullet("HFSMParameters ≈ Animator.parameters");
            Output.Bullet("Modifier ≈ 代码中调用 Animator.Set...");
            Log("");
            
            Output.Divider();
            Log("【总结】使用 AbilityKit.HFSM 框架实现参数驱动的状态机控制");
            Output.Divider();
        }
    }
    
    // ============================================
    // HFSM 参数系统 (与框架配合使用)
    // ============================================
    
    /// <summary>
    /// HFSM 参数类型
    /// </summary>
    public enum HFSMParamType
    {
        Float,
        Bool,
        Trigger
    }
    
    /// <summary>
    /// HFSM 参数基类
    /// </summary>
    public abstract class HFSMParam
    {
        public string Name { get; protected set; }
        public abstract HFSMParamType Type { get; }
        public abstract float FloatValue { get; set; }
        public abstract bool BoolValue { get; set; }
        public abstract void Reset();
    }
    
    /// <summary>
    /// Float 参数
    /// </summary>
    public class HFSMFloatParam : HFSMParam
    {
        public override HFSMParamType Type => HFSMParamType.Float;
        private float _value;
        public override float FloatValue
        {
            get => _value;
            set => _value = value;
        }
        public override bool BoolValue { get => _value > 0; set => _value = value ? 1 : 0; }
        
        public HFSMFloatParam(string name, float defaultValue = 0f)
        {
            Name = name;
            _value = defaultValue;
        }
        
        public override void Reset() { _value = 0; }
    }
    
    /// <summary>
    /// Bool 参数
    /// </summary>
    public class HFSMBoolParam : HFSMParam
    {
        public override HFSMParamType Type => HFSMParamType.Bool;
        private bool _value;
        public override bool BoolValue
        {
            get => _value;
            set => _value = value;
        }
        public override float FloatValue { get => _value ? 1 : 0; set => _value = value > 0; }
        
        public HFSMBoolParam(string name, bool defaultValue = false)
        {
            Name = name;
            _value = defaultValue;
        }
        
        public override void Reset() { _value = false; }
    }
    
    /// <summary>
    /// Trigger 参数 (一次性触发)
    /// </summary>
    public class HFSMTriggerParam : HFSMParam
    {
        public override HFSMParamType Type => HFSMParamType.Trigger;
        private bool _triggered;
        public override bool BoolValue { get => _triggered; set { if (value) Trigger(); } }
        public override float FloatValue { get => _triggered ? 1 : 0; set { if (value > 0) Trigger(); } }
        
        public HFSMTriggerParam(string name)
        {
            Name = name;
        }
        
        public void Trigger()
        {
            _triggered = true;
        }
        
        public override void Reset()
        {
            _triggered = false;
        }
    }
    
    /// <summary>
    /// HFSM 参数容器
    /// </summary>
    public class HFSMParameters
    {
        private readonly Dictionary<string, HFSMParam> _params = new();
        private readonly List<HFSMTriggerParam> _triggers = new();
        
        public void AddFloat(string name, float defaultValue = 0f)
        {
            _params[name] = new HFSMFloatParam(name, defaultValue);
        }
        
        public void AddBool(string name, bool defaultValue = false)
        {
            _params[name] = new HFSMBoolParam(name, defaultValue);
        }
        
        public void AddTrigger(string name)
        {
            var trigger = new HFSMTriggerParam(name);
            _params[name] = trigger;
            _triggers.Add(trigger);
        }
        
        public HFSMParam GetParam(string name)
        {
            return _params.TryGetValue(name, out var param) ? param : null;
        }
        
        public float GetFloat(string name)
        {
            return _params.TryGetValue(name, out var param) ? param.FloatValue : 0f;
        }
        
        public void SetFloat(string name, float value)
        {
            if (_params.TryGetValue(name, out var param))
            {
                param.FloatValue = value;
            }
        }
        
        public bool GetBool(string name)
        {
            return _params.TryGetValue(name, out var param) && param.BoolValue;
        }
        
        public void SetBool(string name, bool value)
        {
            if (_params.TryGetValue(name, out var param))
            {
                param.BoolValue = value;
            }
        }
        
        public void Trigger(string name)
        {
            if (_params.TryGetValue(name, out var param) && param is HFSMTriggerParam trigger)
            {
                trigger.Trigger();
            }
        }
        
        public void ResetTriggers()
        {
            foreach (var trigger in _triggers)
            {
                trigger.Reset();
            }
        }
    }
    
    // ============================================
    // 修饰器系统
    // ============================================
    
    /// <summary>
    /// 修饰器数据 - 影响 HFSM 参数
    /// </summary>
    public class HFSMModifierData
    {
        public string Name { get; set; }
        
        // 参数修改
        public float HealthBonus { get; set; } = 0f;
        public bool? SetCombat { get; set; }
        public bool? SetRageMode { get; set; }
    }
    
    // ============================================
    // 战士状态机实现 (使用 AbilityKit.HFSM 框架)
    // ============================================
    
    /// <summary>
    /// 战士状态机 - 使用 AbilityKit.HFSM 框架
    /// </summary>
    public class WarriorWithHFSM
    {
        private readonly Action<string> _log;
        
        // AbilityKit.HFSM 核心组件
        private StateMachine<string, string> _fsm;
        private HFSMParameters _parameters;
        
        // 当前状态
        public string CurrentState => _fsm?.ActiveStateName ?? "NotInitialized";
        
        // 活跃修饰器
        private readonly List<HFSMModifierData> _modifiers = new();
        
        // 参数值引用 (用于条件判断)
        private float _health;
        private bool _isCombat;
        private bool _rageMode;
        
        public WarriorWithHFSM(Action<string> log)
        {
            _log = log;
        }
        
        /// <summary>
        /// 初始化状态机
        /// </summary>
        public void Initialize()
        {
            // 创建参数
            _parameters = new HFSMParameters();
            _parameters.AddFloat("Health", 100f);
            _parameters.AddBool("IsCombat", false);
            _parameters.AddBool("RageMode", false);
            
            // 初始化参数值
            _health = 100f;
            _isCombat = false;
            _rageMode = false;
            
            // 创建状态机
            _fsm = new StateMachine<string, string>(needsExitTime: true);
            
            // ===== Idle 状态 =====
            var idleState = new State<string, string>(
                onEnter: s => _log($"    [Idle] 进入待机状态"),
                onLogic: s => { }
            );
            
            // ===== Combat 状态 =====
            var combatState = new State<string, string>(
                onEnter: s => _log($"    [Combat] 进入战斗状态"),
                onLogic: s => { }
            );
            
            // ===== Rage 状态 =====
            var rageState = new State<string, string>(
                onEnter: s => _log($"    [Rage] ★ 进入狂暴状态 ★"),
                onLogic: s => { }
            );
            
            // ===== Dead 状态 =====
            var deadState = new State<string, string>(
                onEnter: s => _log($"    [Dead] ★ 死亡 ★"),
                onLogic: s => { }
            );
            
            // 添加状态
            _fsm.AddState("Idle", idleState);
            _fsm.AddState("Combat", combatState);
            _fsm.AddState("Rage", rageState);
            _fsm.AddState("Dead", deadState);
            
            // ===== 添加转换 (使用 AbilityKit.HFSM Transition) =====
            
            // Idle -> Combat: IsCombat == true
            _fsm.AddTransition(new Transition<string>(
                from: "Idle",
                to: "Combat",
                condition: t => _isCombat && _health > 0,
                onTransition: t => _log($"    [转换] Idle -> Combat (IsCombat=true)")
            ));
            
            // Combat -> Idle: IsCombat == false && Health > 0
            _fsm.AddTransition(new Transition<string>(
                from: "Combat",
                to: "Idle",
                condition: t => !_isCombat && _health > 0,
                onTransition: t => _log($"    [转换] Combat -> Idle (IsCombat=false)")
            ));
            
            // Combat -> Rage: RageMode == true
            _fsm.AddTransition(new Transition<string>(
                from: "Combat",
                to: "Rage",
                condition: t => _rageMode && _health > 0,
                onTransition: t => _log($"    [转换] Combat -> Rage (RageMode=true)")
            ));
            
            // Rage -> Combat: RageMode == false && Health > 0
            _fsm.AddTransition(new Transition<string>(
                from: "Rage",
                to: "Combat",
                condition: t => !_rageMode && _health > 0,
                onTransition: t => _log($"    [转换] Rage -> Combat (RageMode=false)")
            ));
            
            // Any -> Dead: Health <= 0
            _fsm.AddTransition(new Transition<string>(
                from: "Idle",
                to: "Dead",
                condition: t => _health <= 0,
                onTransition: t => _log($"    [转换] Idle -> Dead (Health<=0)")
            ));
            _fsm.AddTransition(new Transition<string>(
                from: "Combat",
                to: "Dead",
                condition: t => _health <= 0,
                onTransition: t => _log($"    [转换] Combat -> Dead (Health<=0)")
            ));
            _fsm.AddTransition(new Transition<string>(
                from: "Rage",
                to: "Dead",
                condition: t => _health <= 0,
                onTransition: t => _log($"    [转换] Rage -> Dead (Health<=0)")
            ));
            
            // Dead -> Idle: Health > 0 (复活)
            _fsm.AddTransition(new Transition<string>(
                from: "Dead",
                to: "Idle",
                condition: t => _health > 0,
                onTransition: t => _log($"    [转换] Dead -> Idle (复活)")
            ));
            
            // 设置初始状态
            _fsm.SetStartState("Idle");
            _fsm.Init();
            
            _log("  状态机已初始化 (AbilityKit.HFSM)");
        }
        
        /// <summary>
        /// 每帧更新
        /// </summary>
        public void Tick()
        {
            if (_fsm == null) return;
            
            // 从参数系统同步值
            _health = _parameters.GetFloat("Health");
            _isCombat = _parameters.GetBool("IsCombat");
            _rageMode = _parameters.GetBool("RageMode");
            
            // 执行状态机逻辑 (会检查所有转换条件)
            _fsm.OnLogic();
            
            // 重置触发器
            _parameters.ResetTriggers();
        }
        
        /// <summary>
        /// 设置 Float 参数
        /// </summary>
        public void SetFloat(string name, float value)
        {
            _log($"    SetFloat({name}, {value})");
            _parameters.SetFloat(name, value);
        }
        
        /// <summary>
        /// 设置 Bool 参数
        /// </summary>
        public void SetBool(string name, bool value)
        {
            _log($"    SetBool({name}, {value})");
            _parameters.SetBool(name, value);
        }
        
        /// <summary>
        /// 添加修饰器
        /// </summary>
        public void ApplyModifier(HFSMModifierData modifier)
        {
            _modifiers.Add(modifier);
            ApplyModifierEffects(modifier);
        }
        
        /// <summary>
        /// 移除修饰器
        /// </summary>
        public void RemoveModifier(string name)
        {
            var removed = _modifiers.RemoveAll(m => m.Name == name);
            if (removed > 0)
            {
                _log($"    [移除修饰器] {name}");
                RecalculateParameters();
            }
        }
        
        private void ApplyModifierEffects(HFSMModifierData modifier)
        {
            _log($"    [添加修饰器] {modifier.Name}");
            
            // 修改 Health 参数
            if (modifier.HealthBonus != 0f)
            {
                var currentHealth = _parameters.GetFloat("Health");
                var newHealth = Math.Max(0, Math.Min(100, currentHealth + modifier.HealthBonus));
                _parameters.SetFloat("Health", newHealth);
                _log($"      Health += {modifier.HealthBonus} -> {newHealth}");
            }
            
            // 设置 IsCombat 参数
            if (modifier.SetCombat.HasValue)
            {
                _parameters.SetBool("IsCombat", modifier.SetCombat.Value);
                _log($"      IsCombat = {modifier.SetCombat.Value}");
            }
            
            // 设置 RageMode 参数
            if (modifier.SetRageMode.HasValue)
            {
                _parameters.SetBool("RageMode", modifier.SetRageMode.Value);
                _log($"      RageMode = {modifier.SetRageMode.Value}");
            }
        }
        
        private void RecalculateParameters()
        {
            // 重新计算基础值
            float baseHealth = 100f;
            bool baseCombat = false;
            bool baseRage = false;
            
            // 应用所有修饰器
            foreach (var modifier in _modifiers)
            {
                if (modifier.HealthBonus != 0f)
                {
                    baseHealth += modifier.HealthBonus;
                }
                if (modifier.SetCombat.HasValue)
                {
                    baseCombat = modifier.SetCombat.Value;
                }
                if (modifier.SetRageMode.HasValue)
                {
                    baseRage = modifier.SetRageMode.Value;
                }
            }
            
            // 限制范围
            baseHealth = Math.Max(0, Math.Min(100, baseHealth));
            
            _parameters.SetFloat("Health", baseHealth);
            _parameters.SetBool("IsCombat", baseCombat);
            _parameters.SetBool("RageMode", baseRage);
        }
        
        /// <summary>
        /// 打印状态
        /// </summary>
        public void PrintStatus()
        {
            var health = _parameters.GetFloat("Health");
            var isCombat = _parameters.GetBool("IsCombat");
            var rageMode = _parameters.GetBool("RageMode");
            
            _log("");
            _log($"  ╔════════════════════════════════════════════════════╗");
            _log($"  ║ 状态: {CurrentState,-10}                              ║");
            _log($"  ╠════════════════════════════════════════════════════╣");
            _log($"  ║ 参数:                                            ║");
            _log($"  ║   Health={health,5:F0}  IsCombat={isCombat,-5}  RageMode={rageMode,-5}  ║");
            
            if (_modifiers.Count > 0)
            {
                _log($"  ╠════════════════════════════════════════════════════╣");
                _log($"  ║ 活跃修饰器 ({_modifiers.Count}):                                ║");
                foreach (var mod in _modifiers)
                {
                    _log($"  ║   - {mod.Name}                                       ║");
                }
            }
            _log($"  ╚════════════════════════════════════════════════════╝");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using ET.AbilityKit.Demo.ET.Share;
using ET.Logic;

namespace ET.AbilityKit.Demo.ET.App
{
    /// <summary>
    /// ET Demo 入口程序
    /// 使用 ET 标准的 Entry 系统
    /// </summary>
    public sealed class Program
    {
        private const int MainFiberId = 10000001;

        public static int Main(string[] args)
        {
            Console.WriteLine("=== AbilityKit ET Demo ===");
            Console.WriteLine("Starting ET Framework with Demo Process Component...");
            Console.WriteLine();

            try
            {
                var options = DemoRunOptions.Parse(args);
                if (options.ValidateConfigOnly)
                {
                    return RunConfigValidation();
                }

                var launchOptions = options.Smoke
                    ? ETDemoProcessLaunchOptions.CreateLocalSmokeDefaults()
                    : ETDemoProcessLaunchOptions.CreateLocalScenarioDefaults();

                DemoEntry.Init(launchOptions);
                DemoEntry.StartAsync().NoContext();

                Console.WriteLine();
                Console.WriteLine("=== ET Framework Started ===");
                Console.WriteLine(options.Smoke ? "Running ET battle smoke flow." : "Press Ctrl+C to exit.");
                Console.WriteLine();

                if (!options.Smoke)
                {
                    return RunInteractive();
                }

                var exitCode = RunSmoke(options);
                if (options.SmokeForceExit)
                {
                    Environment.Exit(exitCode);
                }

                return exitCode;
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine("=== ET Framework Initialization Failed ===");
                Console.WriteLine($"Error: {e.Message}");
                Console.WriteLine(e.StackTrace);
                return 1;
            }
        }

        private static int RunInteractive()
        {
            while (true)
            {
                TickEt();
                Thread.Sleep(16);
            }
        }

        private static int RunConfigValidation()
        {
            var result = EtConfigSmokeValidator.Validate();
            Console.WriteLine(result.Format());
            Console.WriteLine(result.Passed ? "=== ET Config Validation Passed ===" : "=== ET Config Validation Failed ===");
            return result.Passed ? 0 : 4;
        }

        private static int RunSmoke(DemoRunOptions options)
        {
            var smokeCase = EtBattleSmokeCase.Load(options.SmokeCasePath);
            Console.WriteLine($"SmokeCase: {smokeCase.Name}");
            var probe = new EtBattleSmokeProbe(smokeCase);
            var stopwatch = Stopwatch.StartNew();
            var elapsedFrames = 0;

            for (int i = 0; i < options.SmokeFrames; i++)
            {
                elapsedFrames = i + 1;
                probe.Sample();
                TickEt();
                probe.Sample();

                if (probe.IsHealthy(options))
                {
                    DrainEt(options.SmokeDrainFrames, options.SleepMilliseconds);
                    Console.WriteLine(probe.FormatResult(elapsedFrames, stopwatch.ElapsedMilliseconds, options));
                    Console.WriteLine("=== ET Battle Smoke Passed ===");
                    return 0;
                }

                if (options.SmokeTimeoutMilliseconds > 0 && stopwatch.ElapsedMilliseconds >= options.SmokeTimeoutMilliseconds)
                {
                    break;
                }

                Thread.Sleep(options.SleepMilliseconds);
            }

            Console.WriteLine(probe.FormatResult(elapsedFrames, stopwatch.ElapsedMilliseconds, options));
            Console.WriteLine("=== ET Battle Smoke Failed ===");
            return 2;
        }

        private static void DrainEt(int frames, int sleepMilliseconds)
        {
            for (int i = 0; i < frames; i++)
            {
                TickEt();
                if (sleepMilliseconds > 0)
                {
                    Thread.Sleep(sleepMilliseconds);
                }
            }
        }

        private static void TickEt()
        {
            try
            {
                global::ET.FiberManager.Instance.Update();
                global::ET.FiberManager.Instance.LateUpdate();
            }
            catch (Exception ex)
            {
                global::ET.Log.Error($"Main loop error: {ex}");
                throw;
            }
        }

        private sealed class DemoRunOptions
        {
            public bool Smoke { get; private set; }
            public bool ValidateConfigOnly { get; private set; }
            public int SmokeFrames { get; private set; } = 600;
            public int SmokeMinBattleFrames { get; private set; } = 30;
            public int SmokeTimeoutMilliseconds { get; private set; } = 15000;
            public int SmokeDrainFrames { get; private set; } = 5;
            public int SleepMilliseconds { get; private set; } = 16;
            public bool SmokeForceExit { get; private set; } = true;
            public string SmokeCasePath { get; private set; } = string.Empty;

            public static DemoRunOptions Parse(string[] args)
            {
                var options = new DemoRunOptions();
                if (args == null)
                {
                    return options;
                }

                foreach (var arg in args)
                {
                    if (string.Equals(arg, "--smoke", StringComparison.OrdinalIgnoreCase))
                    {
                        options.Smoke = true;
                    }
                    else if (string.Equals(arg, "--validate-config-only", StringComparison.OrdinalIgnoreCase))
                    {
                        options.ValidateConfigOnly = true;
                    }
                    else if (TryReadInt(arg, "--smoke-frames=", out var frames))
                    {
                        options.SmokeFrames = Math.Max(1, frames);
                    }
                    else if (TryReadInt(arg, "--smoke-min-battle-frames=", out var minBattleFrames))
                    {
                        options.SmokeMinBattleFrames = Math.Max(1, minBattleFrames);
                    }
                    else if (TryReadInt(arg, "--smoke-timeout-ms=", out var timeoutMilliseconds))
                    {
                        options.SmokeTimeoutMilliseconds = Math.Max(0, timeoutMilliseconds);
                    }
                    else if (TryReadInt(arg, "--smoke-drain-frames=", out var drainFrames))
                    {
                        options.SmokeDrainFrames = Math.Max(0, drainFrames);
                    }
                    else if (TryReadInt(arg, "--smoke-sleep-ms=", out var sleepMilliseconds))
                    {
                        options.SleepMilliseconds = Math.Max(0, sleepMilliseconds);
                    }
                    else if (TryReadString(arg, "--smoke-case=", out var smokeCasePath))
                    {
                        options.SmokeCasePath = smokeCasePath;
                    }
                    else if (string.Equals(arg, "--smoke-no-force-exit", StringComparison.OrdinalIgnoreCase))
                    {
                        options.SmokeForceExit = false;
                    }
                }

                return options;
            }

            private static bool TryReadInt(string arg, string prefix, out int value)
            {
                value = 0;
                if (string.IsNullOrEmpty(arg) || !arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return int.TryParse(arg.Substring(prefix.Length), out value);
            }

            private static bool TryReadString(string arg, string prefix, out string value)
            {
                value = string.Empty;
                if (string.IsNullOrEmpty(arg) || !arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                value = arg.Substring(prefix.Length).Trim();
                return !string.IsNullOrEmpty(value);
            }
        }

        private sealed class EtConfigSmokeValidator
        {
            private const int SmokeHeroId = 1001;
            private const int SmokeAttributeTemplateId = 1001;
            private const int SmokeBasicAttackSkillId = 10010101;
            private static readonly int[] SmokeActiveSkillIds = { 10010101, 10010201, 10010301 };
            private const int SmokeTriggerId = 10001;
            private const string SmokeTriggerPath = "ability/triggers/skills/trigger_10001.json";

            public static ConfigValidationResult Validate()
            {
                var result = new ConfigValidationResult();
                var configRoot = ResolveConfigRoot();
                result.ConfigRoot = configRoot ?? string.Empty;

                if (string.IsNullOrEmpty(configRoot) || !Directory.Exists(configRoot))
                {
                    result.AddError("ConfigRootMissing: unable to locate ET App Configs directory.");
                    return result;
                }

                using var characters = LoadJson(result, configRoot, "moba/characters.json", expectArray: true);
                using var attributes = LoadJson(result, configRoot, "moba/attribute_templates.json", expectArray: true);
                using var skills = LoadJson(result, configRoot, "moba/skills.json", expectArray: true);
                using var trigger = LoadJson(result, configRoot, SmokeTriggerPath, expectArray: false);
                LoadJson(result, configRoot, "moba/gameplays.json", expectArray: true)?.Dispose();

                if (characters != null)
                {
                    ValidateCharacter(result, characters.RootElement);
                }

                if (attributes != null)
                {
                    ValidateAttributeTemplate(result, attributes.RootElement);
                }

                if (skills != null)
                {
                    ValidateSkills(result, skills.RootElement);
                }

                if (trigger != null)
                {
                    ValidateTrigger(result, trigger.RootElement);
                }

                return result;
            }

            private static void ValidateCharacter(ConfigValidationResult result, JsonElement root)
            {
                if (!TryFindById(root, SmokeHeroId, out var hero))
                {
                    result.AddError($"MissingHero: character id {SmokeHeroId} is required by ET smoke.");
                    return;
                }

                if (ReadInt(hero, "AttributeTemplateId") != SmokeAttributeTemplateId)
                {
                    result.AddError($"HeroAttributeTemplateMismatch: character {SmokeHeroId} must use attribute template {SmokeAttributeTemplateId}.");
                }

                if (!ContainsAll(hero, "SkillIds", SmokeActiveSkillIds))
                {
                    result.AddError($"HeroSkillIdsMismatch: character {SmokeHeroId} must include {string.Join(",", SmokeActiveSkillIds)}.");
                }

                result.CheckedItems++;
            }

            private static void ValidateAttributeTemplate(ConfigValidationResult result, JsonElement root)
            {
                if (!TryFindById(root, SmokeAttributeTemplateId, out var template))
                {
                    result.AddError($"MissingAttributeTemplate: template id {SmokeAttributeTemplateId} is required by ET smoke.");
                    return;
                }

                if (ReadFloat(template, "Hp") <= 0f || ReadFloat(template, "MaxHp") <= 0f)
                {
                    result.AddError($"InvalidAttributeHp: template {SmokeAttributeTemplateId} must define positive Hp and MaxHp.");
                }

                if (ReadFloat(template, "MoveSpeed") <= 0f)
                {
                    result.AddError($"InvalidMoveSpeed: template {SmokeAttributeTemplateId} must define positive MoveSpeed.");
                }

                if (!ContainsAll(template, "ActiveSkills", SmokeActiveSkillIds))
                {
                    result.AddError($"AttributeActiveSkillsMismatch: template {SmokeAttributeTemplateId} must include {string.Join(",", SmokeActiveSkillIds)}.");
                }

                result.CheckedItems++;
            }

            private static void ValidateSkills(ConfigValidationResult result, JsonElement root)
            {
                foreach (var skillId in SmokeActiveSkillIds)
                {
                    if (!TryFindById(root, skillId, out var skill))
                    {
                        result.AddError($"MissingSkill: skill id {skillId} is required by ET smoke.");
                        continue;
                    }

                    if (ReadInt(skill, "CastFlowId") <= 0)
                    {
                        result.AddError($"InvalidSkillCastFlow: skill {skillId} must define a positive CastFlowId.");
                    }

                    if (ReadInt(skill, "CooldownMs") < 0)
                    {
                        result.AddError($"InvalidSkillCooldown: skill {skillId} must not define a negative CooldownMs.");
                    }

                    result.CheckedItems++;
                }

                if (!TryFindById(root, SmokeBasicAttackSkillId, out _))
                {
                    result.AddError($"MissingBasicAttackSkill: skill id {SmokeBasicAttackSkillId} is required as basic attack.");
                }
            }

            private static void ValidateTrigger(ConfigValidationResult result, JsonElement root)
            {
                if (ReadInt(root, "id") != SmokeTriggerId)
                {
                    result.AddError($"TriggerIdMismatch: {SmokeTriggerPath} must define id {SmokeTriggerId}.");
                }

                if (!ReadBool(root, "enabled"))
                {
                    result.AddError($"TriggerDisabled: trigger {SmokeTriggerId} must be enabled.");
                }

                var hasDamageAction = false;
                if (root.TryGetProperty("actions", out var actions) && actions.ValueKind == JsonValueKind.Array)
                {
                    foreach (var action in actions.EnumerateArray())
                    {
                        if (ReadString(action, "type") == "give_damage" && ReadFloat(action, "damage_value") > 0f)
                        {
                            hasDamageAction = true;
                            break;
                        }
                    }
                }

                if (!hasDamageAction)
                {
                    result.AddError($"MissingDamageAction: trigger {SmokeTriggerId} must contain a positive give_damage action.");
                }

                result.CheckedItems++;
            }

            private static JsonDocument? LoadJson(ConfigValidationResult result, string configRoot, string relativePath, bool expectArray)
            {
                var path = Path.Combine(configRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                {
                    result.AddError($"MissingConfigFile: {relativePath}");
                    return null;
                }

                try
                {
                    var document = JsonDocument.Parse(File.ReadAllText(path));
                    var expectedKind = expectArray ? JsonValueKind.Array : JsonValueKind.Object;
                    if (document.RootElement.ValueKind != expectedKind)
                    {
                        result.AddError($"InvalidConfigShape: {relativePath} expected {expectedKind}, actual {document.RootElement.ValueKind}.");
                    }

                    result.LoadedFiles++;
                    return document;
                }
                catch (Exception ex)
                {
                    result.AddError($"InvalidJson: {relativePath}: {ex.Message}");
                    return null;
                }
            }

            private static string? ResolveConfigRoot()
            {
                var candidates = new List<string>
                {
                    Path.Combine(AppContext.BaseDirectory, "Configs"),
                    Path.Combine(Environment.CurrentDirectory, "src", "AbilityKit.Demo.ET.App", "Configs"),
                    Path.Combine(Environment.CurrentDirectory, "Configs")
                };

                var current = new DirectoryInfo(AppContext.BaseDirectory);
                while (current != null)
                {
                    candidates.Add(Path.Combine(current.FullName, "src", "AbilityKit.Demo.ET.App", "Configs"));
                    candidates.Add(Path.Combine(current.FullName, "Configs"));
                    current = current.Parent;
                }

                foreach (var candidate in candidates)
                {
                    if (!string.IsNullOrEmpty(candidate) && Directory.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                return null;
            }

            private static bool TryFindById(JsonElement array, int id, out JsonElement item)
            {
                if (array.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in array.EnumerateArray())
                    {
                        if (ReadInt(entry, "Id") == id || ReadInt(entry, "id") == id)
                        {
                            item = entry;
                            return true;
                        }
                    }
                }

                item = default;
                return false;
            }

            private static bool ContainsAll(JsonElement element, string propertyName, int[] expectedValues)
            {
                if (!element.TryGetProperty(propertyName, out var values) || values.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                var found = new HashSet<int>();
                foreach (var value in values.EnumerateArray())
                {
                    if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
                    {
                        found.Add(intValue);
                    }
                }

                foreach (var expected in expectedValues)
                {
                    if (!found.Contains(expected))
                    {
                        return false;
                    }
                }

                return true;
            }

            private static int ReadInt(JsonElement element, string propertyName)
            {
                return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue)
                    ? intValue
                    : 0;
            }

            private static float ReadFloat(JsonElement element, string propertyName)
            {
                return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out var floatValue)
                    ? floatValue
                    : 0f;
            }

            private static bool ReadBool(JsonElement element, string propertyName)
            {
                return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;
            }

            private static string ReadString(JsonElement element, string propertyName)
            {
                return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                    ? value.GetString() ?? string.Empty
                    : string.Empty;
            }
        }

        private sealed class ConfigValidationResult
        {
            private readonly List<string> _errors = new List<string>();

            public string ConfigRoot { get; set; } = string.Empty;
            public int LoadedFiles { get; set; }
            public int CheckedItems { get; set; }
            public bool Passed => _errors.Count == 0;

            public void AddError(string error)
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    _errors.Add(error);
                }
            }

            public string Format()
            {
                var errors = _errors.Count == 0 ? "none" : string.Join(" | ", _errors);
                return $"[ETConfigValidation] Passed={Passed}, ConfigRoot={ConfigRoot}, LoadedFiles={LoadedFiles}, CheckedItems={CheckedItems}, Errors={errors}";
            }
        }

        private sealed class EtBattleSmokeCase
        {
            public string Name { get; set; } = "skill-damage-basic";
            public bool DisableBuiltinSkillTest { get; set; }
            public List<EtBattleSmokeInputCase> Inputs { get; set; } = new List<EtBattleSmokeInputCase>
            {
                new EtBattleSmokeInputCase { Type = "move", FrameOffset = 1, MoveX = 1f, MoveZ = 0f },
                new EtBattleSmokeInputCase { Type = "skill", FrameOffset = 1, SkillSlot = 1, Target = "current-target" }
            };
            public EtBattleSmokeExpectedCase Expected { get; set; } = new EtBattleSmokeExpectedCase();

            public static EtBattleSmokeCase Load(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return new EtBattleSmokeCase();
                }

                if (!File.Exists(path))
                {
                    throw new FileNotFoundException("Smoke case file not found.", path);
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var smokeCase = JsonSerializer.Deserialize<EtBattleSmokeCase>(File.ReadAllText(path), options) ?? new EtBattleSmokeCase();
                smokeCase.Name = string.IsNullOrWhiteSpace(smokeCase.Name) ? Path.GetFileNameWithoutExtension(path) : smokeCase.Name;
                smokeCase.Inputs ??= new List<EtBattleSmokeInputCase>();
                smokeCase.Expected ??= new EtBattleSmokeExpectedCase();
                return smokeCase;
            }
        }

        private sealed class EtBattleSmokeInputCase
        {
            public string Type { get; set; } = string.Empty;
            public int FrameOffset { get; set; } = 1;
            public float MoveX { get; set; }
            public float MoveZ { get; set; }
            public int SkillSlot { get; set; } = 1;
            public string Target { get; set; } = "current-target";
        }

        private sealed class EtBattleSmokeExpectedCase
        {
            public bool RequireMoveInput { get; set; } = true;
            public bool RequireSkillInput { get; set; } = true;
            public bool RequireSkillTarget { get; set; } = true;
            public bool RequireTransformSnapshot { get; set; } = true;
            public bool RequireStateHashSnapshot { get; set; } = true;
            public bool RequireActorSpawnSnapshot { get; set; } = true;
            public bool RequireActorSpawnCoverage { get; set; } = true;
            public bool RequireEventSnapshot { get; set; } = true;
            public int MinEventSnapshots { get; set; } = 1;
            public int MaxEventSnapshots { get; set; }
            public int MinActorSpawns { get; set; } = 2;
            public float ExpectedTargetHpAtMost { get; set; } = 150f;
            public bool RequireTargetAttributeGroup { get; set; } = true;
            public bool RequireTargetResourceContainer { get; set; } = true;
            public bool RequireTargetSkillLoadout { get; set; } = true;
            public int MinTargetActiveSkillCount { get; set; } = 3;
            public bool RequireLocalSkillCooldown { get; set; }
            public int ExpectedCooldownSkillSlot { get; set; } = 1;
            public long MinLocalSkillCooldownRemainingMs { get; set; } = 1L;
            public bool RequireLocalManaConsumed { get; set; }
            public float MinLocalManaSpent { get; set; } = 1f;
            public float MaxLocalManaAfter { get; set; }
        }

        private sealed class EtBattleSmokeProbe
        {
            private static readonly MethodInfo GetFiberMethod = typeof(global::ET.FiberManager).GetMethod(
                "Get",
                BindingFlags.Instance | BindingFlags.NonPublic);

            private readonly EtBattleSmokeCase _smokeCase;
            private readonly List<LogicWorldEntityState> _entityStates = new List<LogicWorldEntityState>(16);
            private readonly List<SkillPipelineRunner.RunningSnapshot> _runningSkills = new List<SkillPipelineRunner.RunningSnapshot>(8);
            private readonly HashSet<int> _spawnedActorIds = new HashSet<int>();

            private bool _inputsSubmitted;
            private bool _builtinSkillTestDisabled;
            private float _targetX;
            private float _targetZ;
            private FrameSnapshotDispatcher? _subscribedDispatcher;
            private IDisposable? _transformSubscription;
            private IDisposable? _stateHashSubscription;
            private IDisposable? _damageEventSubscription;
            private IDisposable? _projectileEventSubscription;
            private IDisposable? _areaEventSubscription;
            private IDisposable? _actorSpawnSubscription;
            private int _lastSnapshotFrame = -1;
            private int _snapshotsInFrame;

            public EtBattleSmokeProbe(EtBattleSmokeCase smokeCase)
            {
                _smokeCase = smokeCase ?? new EtBattleSmokeCase();
            }

            public bool HasProcessScene { get; private set; }
            public bool HasBattleScene { get; private set; }
            public bool HasBattleComponent { get; private set; }
            public bool HasRuntimePort { get; private set; }
            public bool HasReadyRuntime { get; private set; }
            public bool HasStartedRuntime { get; private set; }
            public bool HasStartedBattle { get; private set; }
            public bool HasRuntimeEntities { get; private set; }
            public bool HasRuntimeSnapshots { get; private set; }
            public bool HasMoveInputSubmitted { get; private set; }
            public bool HasSkillInputSubmitted { get; private set; }
            public bool HasSkillTargetActor { get; private set; }
            public bool HasDecodedTransformSnapshot { get; private set; }
            public bool HasDecodedStateHashSnapshot { get; private set; }
            public bool HasDecodedEventSnapshot { get; private set; }
            public bool HasDecodedActorSpawnSnapshot { get; private set; }
            public int MaxBattleFrame { get; private set; }
            public int MaxEntityCount { get; private set; }
            public int MaxSnapshotCount { get; private set; }
            public int InputTargetFrame { get; private set; }
            public int MaxTransformEntryCount { get; private set; }
            public int MaxStateHashFrame { get; private set; }
            public int DecodedEventSnapshotCount { get; private set; }
            public int DecodedActorSpawnSnapshotCount { get; private set; }
            public int SpawnLocalActorId { get; private set; }
            public int SpawnTargetActorId { get; private set; }
            public uint LastStateHash { get; private set; }
            public bool HasRunningSkillSnapshot { get; private set; }
            public int MaxRunningSkillElapsedMs { get; private set; }
            public int MaxRunningSkillNextEventIndex { get; private set; }
            public bool HasLocalSkillCooldown { get; private set; }
            public int LocalCooldownSkillSlot { get; private set; }
            public int LocalCooldownSkillId { get; private set; }
            public long MaxLocalSkillCooldownRemainingMs { get; private set; }
            public bool HasLocalManaResource { get; private set; }
            public float LocalInitialMana { get; private set; }
            public float LocalMinMana { get; private set; }
            public float LocalMaxManaSpent { get; private set; }
            public int LocalActorId { get; private set; }
            public int LocalTeamId { get; private set; }
            public int TargetActorId { get; private set; }
            public int TargetTeamId { get; private set; }
            public float TargetInitialHp { get; private set; }
            public float TargetMinHp { get; private set; }
            public bool TargetHasAttributeGroup { get; private set; }
            public bool TargetHasResourceContainer { get; private set; }
            public bool TargetHasSkillLoadout { get; private set; }
            public int TargetActiveSkillCount { get; private set; }
            public string RuntimeStatus { get; private set; } = "runtime port missing";
            public string PendingInputFrames { get; private set; } = "input missing";

            public void Sample()
            {
                var root = GetRootScene();
                if (root == null)
                {
                    return;
                }

                var process = root.GetComponent<DemoProcessComponent>();
                HasProcessScene = process != null;

                var battleScene = process?.CurrentScene;
                HasBattleScene = battleScene != null && battleScene.SceneType == global::ET.SceneType.DemoBattle;

                var battle = battleScene?.GetComponent<ETBattleComponent>();
                HasBattleComponent = battle != null;
                DisableBuiltinSkillTestIfRequested(battleScene);

                if (battle == null || battle.BattleHost == null)
                {
                    return;
                }
    
                var driver = battle.BattleHost;
                HasStartedRuntime |= driver.RuntimeGameStarted;
                HasStartedBattle |= battle.State == BattleState.InProgress && driver.IsRunning;
                MaxBattleFrame = Math.Max(MaxBattleFrame, driver.CurrentFrame);
                SubscribeDriverSnapshots(driver);
                SampleCachedDriverSnapshots(driver);

                if (driver.TryResolve<IMobaBattleRuntimePort>(out var runtime) && runtime != null)
                {
                    HasRuntimePort = true;
                    var status = runtime.Status;
                    RuntimeStatus = status.ToString();
                    HasReadyRuntime |= status.IsReadyForGameStart && status.IsReadyForBattleLoop && status.Has(MobaBattleRuntimeCapability.StateReadModel);

                    _entityStates.Clear();
                    var entityCount = runtime.FillAllEntityStates(_entityStates);
                    MaxEntityCount = Math.Max(MaxEntityCount, entityCount);
                    HasRuntimeEntities |= entityCount > 0;
                    SelectSkillTarget(_entityStates);
                    SampleTargetHealth(_entityStates);

                    if (battleScene != null)
                    {
                        SampleInputBuffer(battleScene);
                        TrySubmitProtocolInputs(battleScene, battle, driver);
                    }

                    SampleRunningSkills(driver);
                    SampleLocalSkillCooldown(driver);
                    SampleLocalMana(driver);
                }
            }

            public bool IsHealthy(DemoRunOptions options)
            {
                var expected = _smokeCase.Expected;
                return HasStartedBattle &&
                       HasReadyRuntime &&
                       HasRuntimeEntities &&
                       HasRuntimeSnapshots &&
                       (!expected.RequireMoveInput || HasMoveInputSubmitted) &&
                       (!expected.RequireSkillInput || HasSkillInputSubmitted) &&
                       (!expected.RequireSkillTarget || HasSkillTargetActor) &&
                       (!expected.RequireTransformSnapshot || HasDecodedTransformSnapshot) &&
                       (!expected.RequireStateHashSnapshot || HasDecodedStateHashSnapshot) &&
                       (!expected.RequireActorSpawnSnapshot || HasDecodedActorSpawnSnapshot) &&
                       (!expected.RequireEventSnapshot || HasDecodedEventSnapshot) &&
                       DecodedEventSnapshotCount >= expected.MinEventSnapshots &&
                       (expected.MaxEventSnapshots <= 0 || DecodedEventSnapshotCount <= expected.MaxEventSnapshots) &&
                       DecodedActorSpawnSnapshotCount >= expected.MinActorSpawns &&
                       (!expected.RequireActorSpawnCoverage || (SpawnLocalActorId == LocalActorId && SpawnTargetActorId == TargetActorId)) &&
                       (expected.ExpectedTargetHpAtMost <= 0f || (TargetMinHp >= 0f && TargetMinHp <= expected.ExpectedTargetHpAtMost)) &&
                       (!expected.RequireTargetAttributeGroup || TargetHasAttributeGroup) &&
                       (!expected.RequireTargetResourceContainer || TargetHasResourceContainer) &&
                       (!expected.RequireTargetSkillLoadout || TargetHasSkillLoadout) &&
                       TargetActiveSkillCount >= expected.MinTargetActiveSkillCount &&
                       (!expected.RequireLocalSkillCooldown || (HasLocalSkillCooldown && MaxLocalSkillCooldownRemainingMs >= expected.MinLocalSkillCooldownRemainingMs)) &&
                       (!expected.RequireLocalManaConsumed || (HasLocalManaResource && LocalMaxManaSpent >= expected.MinLocalManaSpent && (expected.MaxLocalManaAfter <= 0f || LocalMinMana <= expected.MaxLocalManaAfter))) &&
                       MaxBattleFrame >= Math.Max(options.SmokeMinBattleFrames, InputTargetFrame + 1);
            }

            private void DisableBuiltinSkillTestIfRequested(global::ET.Scene? battleScene)
            {
                if (!_smokeCase.DisableBuiltinSkillTest || _builtinSkillTestDisabled || battleScene == null)
                {
                    return;
                }

                var skillTest = battleScene.GetComponent<ETBattleSkillTestComponent>();
                if (skillTest == null)
                {
                    return;
                }

                skillTest.IsEnabled = false;
                _builtinSkillTestDisabled = true;
            }

            private void TrySubmitProtocolInputs(global::ET.Scene battleScene, ETBattleComponent battle, ETMobaBattleDriver driver)
            {
                if (_inputsSubmitted || !HasStartedBattle || !HasSkillTargetActor || battleScene == null)
                {
                    return;
                }

                var input = battleScene.GetComponent<ETInputComponent>();
                if (input == null)
                {
                    return;
                }

                var playerId = ResolveRuntimePlayerId(driver, battle);
                if (string.IsNullOrEmpty(playerId))
                {
                    return;
                }

                InputTargetFrame = driver.CurrentFrame + 1;
                foreach (var command in _smokeCase.Inputs)
                {
                    var targetFrame = driver.CurrentFrame + Math.Max(1, command.FrameOffset);
                    if (string.Equals(command.Type, "move", StringComparison.OrdinalIgnoreCase))
                    {
                        input.AddMoveCommand(targetFrame, playerId, command.MoveX, command.MoveZ);
                        HasMoveInputSubmitted = true;
                    }
                    else if (string.Equals(command.Type, "skill", StringComparison.OrdinalIgnoreCase))
                    {
                        var targetActorId = string.Equals(command.Target, "current-target", StringComparison.OrdinalIgnoreCase) ? TargetActorId : 0;
                        input.AddSkillCommand(targetFrame, playerId, Math.Max(1, command.SkillSlot), _targetX, _targetZ, targetActorId);
                        HasSkillInputSubmitted = true;
                    }

                    InputTargetFrame = Math.Max(InputTargetFrame, targetFrame);
                }

                PendingInputFrames = input.FormatPendingFrames();
                _inputsSubmitted = true;
            }

            private static string ResolveRuntimePlayerId(ETMobaBattleDriver driver, ETBattleComponent battle)
            {
                if (driver?.PlayerSpawnData != null && driver.PlayerSpawnData.Count > 0)
                {
                    var playerId = driver.PlayerSpawnData[0]?.PlayerId;
                    if (!string.IsNullOrEmpty(playerId))
                    {
                        return playerId;
                    }
                }

                return battle != null && battle.PlayerId > 0 ? battle.PlayerId.ToString() : string.Empty;
            }

            private void SampleInputBuffer(global::ET.Scene battleScene)
            {
                var input = battleScene.GetComponent<ETInputComponent>();
                PendingInputFrames = input != null ? input.FormatPendingFrames() : "input missing";
            }

            private void SubscribeDriverSnapshots(ETMobaBattleDriver driver)
            {
                var dispatcher = driver?.SnapshotDispatcher;
                if (dispatcher == null || ReferenceEquals(dispatcher, _subscribedDispatcher))
                {
                    return;
                }

                UnsubscribeDriverSnapshots();
                _subscribedDispatcher = dispatcher;
                _transformSubscription = dispatcher.Subscribe<ActorTransformData[]>(MobaOpCodes.Snapshot.ActorTransform, OnActorTransformSnapshot);
                _stateHashSubscription = dispatcher.Subscribe<StateHashData>(MobaOpCodes.Snapshot.StateHash, OnStateHashSnapshot);
                _damageEventSubscription = dispatcher.Subscribe<DamageEventData[]>(MobaOpCodes.Snapshot.DamageEvent, OnDamageEventSnapshot);
                _projectileEventSubscription = dispatcher.Subscribe<ProjectileEventData[]>(MobaOpCodes.Snapshot.ProjectileEvent, OnProjectileEventSnapshot);
                _areaEventSubscription = dispatcher.Subscribe<AreaEventData[]>(MobaOpCodes.Snapshot.AreaEvent, OnAreaEventSnapshot);
                _actorSpawnSubscription = dispatcher.Subscribe<ActorSpawnData[]>(MobaOpCodes.Snapshot.ActorSpawn, OnActorSpawnSnapshot);
            }

            private void UnsubscribeDriverSnapshots()
            {
                if (_subscribedDispatcher == null)
                {
                    _transformSubscription = null;
                    _stateHashSubscription = null;
                    _damageEventSubscription = null;
                    _projectileEventSubscription = null;
                    _areaEventSubscription = null;
                    _actorSpawnSubscription = null;
                    return;
                }

                _subscribedDispatcher.Unsubscribe(_transformSubscription);
                _subscribedDispatcher.Unsubscribe(_stateHashSubscription);
                _subscribedDispatcher.Unsubscribe(_damageEventSubscription);
                _subscribedDispatcher.Unsubscribe(_projectileEventSubscription);
                _subscribedDispatcher.Unsubscribe(_areaEventSubscription);
                _subscribedDispatcher.Unsubscribe(_actorSpawnSubscription);
                _subscribedDispatcher = null;
                _transformSubscription = null;
                _stateHashSubscription = null;
                _damageEventSubscription = null;
                _projectileEventSubscription = null;
                _areaEventSubscription = null;
                _actorSpawnSubscription = null;
            }

            private void SampleCachedDriverSnapshots(ETMobaBattleDriver driver)
            {
                if (driver == null)
                {
                    return;
                }

                if (!HasDecodedTransformSnapshot)
                {
                    var transforms = driver.LastActorTransformSnapshot;
                    if (transforms != null && transforms.Length > 0)
                    {
                        OnActorTransformSnapshot(-1, transforms);
                    }
                }

                if (!HasDecodedStateHashSnapshot && driver.LastStateHashSnapshot.HasValue)
                {
                    OnStateHashSnapshot(-1, driver.LastStateHashSnapshot);
                }

                var spawns = driver.LastActorSpawnSnapshot;
                if (spawns != null && spawns.Length > 0 && DecodedActorSpawnSnapshotCount == 0)
                {
                    RecordActorSpawnSnapshot(-1, spawns);
                }
            }

            private void OnActorTransformSnapshot(int frameIndex, ActorTransformData[] data)
            {
                RecordDispatchedSnapshot(frameIndex);
                var count = data?.Length ?? 0;
                MaxTransformEntryCount = Math.Max(MaxTransformEntryCount, count);
                HasDecodedTransformSnapshot |= count > 0;
            }

            private void OnStateHashSnapshot(int frameIndex, StateHashData data)
            {
                RecordDispatchedSnapshot(frameIndex);
                MaxStateHashFrame = Math.Max(MaxStateHashFrame, data.FrameIndex);
                LastStateHash = data.StateHash;
                HasDecodedStateHashSnapshot |= data.HasValue && data.FrameIndex >= 0 && data.StateHash != 0;
            }

            private void OnActorSpawnSnapshot(int frameIndex, ActorSpawnData[] data)
            {
                RecordActorSpawnSnapshot(frameIndex, data);
            }

            private void RecordActorSpawnSnapshot(int frameIndex, ActorSpawnData[] data)
            {
                if (data == null || data.Length == 0)
                {
                    return;
                }

                RecordDispatchedSnapshot(frameIndex);
                DecodedActorSpawnSnapshotCount += data.Length;
                for (int i = 0; i < data.Length; i++)
                {
                    var actorId = data[i].ActorId;
                    if (actorId > 0)
                    {
                        _spawnedActorIds.Add(actorId);
                    }
                }

                EvaluateActorSpawnCoverage();
            }

            private void EvaluateActorSpawnCoverage()
            {
                if (LocalActorId > 0 && _spawnedActorIds.Contains(LocalActorId))
                {
                    SpawnLocalActorId = LocalActorId;
                }

                if (TargetActorId > 0 && _spawnedActorIds.Contains(TargetActorId))
                {
                    SpawnTargetActorId = TargetActorId;
                }

                HasDecodedActorSpawnSnapshot |= DecodedActorSpawnSnapshotCount >= 2 && SpawnLocalActorId == LocalActorId && SpawnTargetActorId == TargetActorId;
            }

            private void OnDamageEventSnapshot(int frameIndex, DamageEventData[] data)
            {
                RecordDispatchedSnapshot(frameIndex);
                RecordDispatchedEventSnapshot(data?.Length ?? 0);
            }

            private void OnProjectileEventSnapshot(int frameIndex, ProjectileEventData[] data)
            {
                RecordDispatchedSnapshot(frameIndex);
                RecordDispatchedEventSnapshot(data?.Length ?? 0);
            }

            private void OnAreaEventSnapshot(int frameIndex, AreaEventData[] data)
            {
                RecordDispatchedSnapshot(frameIndex);
                RecordDispatchedEventSnapshot(data?.Length ?? 0);
            }

            private void RecordDispatchedSnapshot(int frameIndex)
            {
                HasRuntimeSnapshots = true;
                if (frameIndex < 0)
                {
                    MaxSnapshotCount = Math.Max(MaxSnapshotCount, 1);
                    return;
                }

                if (frameIndex != _lastSnapshotFrame)
                {
                    _lastSnapshotFrame = frameIndex;
                    _snapshotsInFrame = 0;
                }

                _snapshotsInFrame++;
                MaxSnapshotCount = Math.Max(MaxSnapshotCount, _snapshotsInFrame);
            }

            private void RecordDispatchedEventSnapshot(int count)
            {
                if (count <= 0)
                {
                    return;
                }

                DecodedEventSnapshotCount += count;
                HasDecodedEventSnapshot = true;
            }

            private void SelectSkillTarget(IReadOnlyList<LogicWorldEntityState> states)
            {
                if (HasSkillTargetActor || states == null || states.Count < 2)
                {
                    return;
                }

                var localIndex = -1;
                for (int i = 0; i < states.Count; i++)
                {
                    if (states[i].EntityId > 0 && states[i].TeamId == 1)
                    {
                        localIndex = i;
                        break;
                    }
                }

                if (localIndex < 0)
                {
                    return;
                }

                var local = states[localIndex];
                for (int i = 0; i < states.Count; i++)
                {
                    var candidate = states[i];
                    if (candidate.EntityId <= 0 || candidate.EntityId == local.EntityId || candidate.TeamId == local.TeamId)
                    {
                        continue;
                    }

                    LocalActorId = local.EntityId;
                    LocalTeamId = local.TeamId;
                    TargetActorId = candidate.EntityId;
                    TargetTeamId = candidate.TeamId;
                    _targetX = candidate.X;
                    _targetZ = candidate.Z;
                    HasSkillTargetActor = true;
                    EvaluateActorSpawnCoverage();
                    return;
                }
            }

            private void SampleTargetHealth(IReadOnlyList<LogicWorldEntityState> states)
            {
                if (!HasSkillTargetActor || states == null || states.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < states.Count; i++)
                {
                    var state = states[i];
                    if (state.EntityId != TargetActorId)
                    {
                        continue;
                    }

                    TargetHasAttributeGroup = state.HasAttributeGroup;
                    TargetHasResourceContainer = state.HasResourceContainer;
                    TargetHasSkillLoadout = state.HasSkillLoadout;
                    TargetActiveSkillCount = state.ActiveSkillCount;

                    if (TargetInitialHp <= 0f && state.Hp > 0f)
                    {
                        TargetInitialHp = state.Hp;
                        TargetMinHp = state.Hp;
                    }
                    else if (TargetMinHp <= 0f || state.Hp < TargetMinHp)
                    {
                        TargetMinHp = state.Hp;
                    }

                    return;
                }
            }

            private void SampleRunningSkills(ETMobaBattleDriver driver)
            {
                if (driver == null || LocalActorId <= 0)
                {
                    return;
                }

                if (!driver.TryResolve<SkillCastCoordinator>(out var skills) || skills == null)
                {
                    return;
                }

                _runningSkills.Clear();
                skills.FillRunningSnapshots(LocalActorId, _runningSkills);
                if (_runningSkills.Count == 0)
                {
                    return;
                }

                HasRunningSkillSnapshot = true;
                for (int i = 0; i < _runningSkills.Count; i++)
                {
                    var snapshot = _runningSkills[i];
                    MaxRunningSkillElapsedMs = Math.Max(MaxRunningSkillElapsedMs, snapshot.ElapsedMs);
                    MaxRunningSkillNextEventIndex = Math.Max(MaxRunningSkillNextEventIndex, snapshot.NextEventIndex);
                }
            }

            private void SampleLocalSkillCooldown(ETMobaBattleDriver driver)
            {
                if (driver == null || LocalActorId <= 0)
                {
                    return;
                }

                if (!driver.TryResolve<MobaActorLookupService>(out var actors) || actors == null)
                {
                    return;
                }

                if (!actors.TryGetActorEntity(LocalActorId, out var entity) || entity == null || !entity.hasSkillLoadout)
                {
                    return;
                }

                var skills = entity.skillLoadout.ActiveSkills;
                var slot = Math.Max(1, _smokeCase.Expected.ExpectedCooldownSkillSlot);
                var idx = slot - 1;
                if (skills == null || idx < 0 || idx >= skills.Length)
                {
                    return;
                }

                var runtime = skills[idx];
                if (runtime == null || runtime.CooldownEndTimeMs <= 0L)
                {
                    return;
                }

                var currentTimeMs = 0L;
                try
                {
                    currentTimeMs = driver.TryResolve<global::AbilityKit.Ability.FrameSync.IFrameTime>(out var time) && time != null
                        ? (long)MathF.Round(time.Time * 1000f)
                        : 0L;
                }
                catch
                {
                    currentTimeMs = 0L;
                }

                var remainingMs = runtime.CooldownEndTimeMs - currentTimeMs;
                if (remainingMs <= 0L)
                {
                    return;
                }

                HasLocalSkillCooldown = true;
                LocalCooldownSkillSlot = slot;
                LocalCooldownSkillId = runtime.SkillId;
                MaxLocalSkillCooldownRemainingMs = Math.Max(MaxLocalSkillCooldownRemainingMs, remainingMs);
            }

            private void SampleLocalMana(ETMobaBattleDriver driver)
            {
                if (driver == null || LocalActorId <= 0)
                {
                    return;
                }

                if (!driver.TryResolve<MobaActorLookupService>(out var actors) || actors == null)
                {
                    return;
                }

                if (!actors.TryGetActorEntity(LocalActorId, out var entity) || entity == null || !entity.hasResourceContainer)
                {
                    return;
                }

                var resources = entity.resourceContainer.Value?.Map;
                if (resources == null || !resources.TryGetValue(ResourceType.Mana, out var manaState) || manaState == null)
                {
                    return;
                }

                var mana = manaState.Current;
                if (!HasLocalManaResource)
                {
                    HasLocalManaResource = true;
                    LocalInitialMana = (float)mana;
                    LocalMinMana = (float)mana;
                    return;
                }

                LocalMinMana = Math.Min(LocalMinMana, (float)mana);
                LocalMaxManaSpent = Math.Max(LocalMaxManaSpent, LocalInitialMana - LocalMinMana);
            }

            public string FormatResult(int elapsedFrames, long elapsedMilliseconds, DemoRunOptions options)
            {
                return "[ETBattleSmoke] " +
                       $"SmokeCase={_smokeCase.Name}, " +
                       $"ElapsedFrames={elapsedFrames}, " +
                       $"ElapsedMilliseconds={elapsedMilliseconds}, " +
                       $"ExpectedBattleFrames>={options.SmokeMinBattleFrames}, " +
                       $"TimeoutMilliseconds={options.SmokeTimeoutMilliseconds}, " +
                       $"HasProcessScene={HasProcessScene}, " +
                       $"HasBattleScene={HasBattleScene}, " +
                       $"HasBattleComponent={HasBattleComponent}, " +
                       $"HasRuntimePort={HasRuntimePort}, " +
                       $"HasReadyRuntime={HasReadyRuntime}, " +
                       $"HasStartedRuntime={HasStartedRuntime}, " +
                       $"HasStartedBattle={HasStartedBattle}, " +
                       $"HasRuntimeEntities={HasRuntimeEntities}, " +
                       $"HasRuntimeSnapshots={HasRuntimeSnapshots}, " +
                       $"HasMoveInputSubmitted={HasMoveInputSubmitted}, " +
                       $"HasSkillInputSubmitted={HasSkillInputSubmitted}, " +
                       $"HasSkillTargetActor={HasSkillTargetActor}, " +
                       $"HasDecodedTransformSnapshot={HasDecodedTransformSnapshot}, " +
                       $"HasDecodedStateHashSnapshot={HasDecodedStateHashSnapshot}, " +
                       $"HasDecodedActorSpawnSnapshot={HasDecodedActorSpawnSnapshot}, " +
                       $"HasDecodedEventSnapshot={HasDecodedEventSnapshot}, " +
                       $"MaxBattleFrame={MaxBattleFrame}, " +
                       $"InputTargetFrame={InputTargetFrame}, " +
                       $"MaxEntityCount={MaxEntityCount}, " +
                       $"MaxSnapshotCount={MaxSnapshotCount}, " +
                       $"MaxTransformEntryCount={MaxTransformEntryCount}, " +
                       $"MaxStateHashFrame={MaxStateHashFrame}, " +
                       $"LastStateHash={LastStateHash}, " +
                       $"DecodedEventSnapshotCount={DecodedEventSnapshotCount}, " +
                       $"DecodedActorSpawnSnapshotCount={DecodedActorSpawnSnapshotCount}, " +
                       $"SpawnLocalActorId={SpawnLocalActorId}, " +
                       $"SpawnTargetActorId={SpawnTargetActorId}, " +
                       $"HasRunningSkillSnapshot={HasRunningSkillSnapshot}, " +
                       $"MaxRunningSkillElapsedMs={MaxRunningSkillElapsedMs}, " +
                       $"MaxRunningSkillNextEventIndex={MaxRunningSkillNextEventIndex}, " +
                       $"HasLocalSkillCooldown={HasLocalSkillCooldown}, " +
                       $"LocalCooldownSkillSlot={LocalCooldownSkillSlot}, " +
                       $"LocalCooldownSkillId={LocalCooldownSkillId}, " +
                       $"MaxLocalSkillCooldownRemainingMs={MaxLocalSkillCooldownRemainingMs}, " +
                       $"HasLocalManaResource={HasLocalManaResource}, " +
                       $"LocalInitialMana={LocalInitialMana:F1}, " +
                       $"LocalMinMana={LocalMinMana:F1}, " +
                       $"LocalMaxManaSpent={LocalMaxManaSpent:F1}, " +
                       $"PendingInputFrames={PendingInputFrames}, " +
                       $"TargetInitialHp={TargetInitialHp:F1}, " +
                       $"TargetMinHp={TargetMinHp:F1}, " +
                       $"TargetHasAttributeGroup={TargetHasAttributeGroup}, " +
                       $"TargetHasResourceContainer={TargetHasResourceContainer}, " +
                       $"TargetHasSkillLoadout={TargetHasSkillLoadout}, " +
                       $"TargetActiveSkillCount={TargetActiveSkillCount}, " +
                       $"LocalActorId={LocalActorId}, " +
                       $"LocalTeamId={LocalTeamId}, " +
                       $"TargetActorId={TargetActorId}, " +
                       $"TargetTeamId={TargetTeamId}, " +
                       $"RuntimeStatus={RuntimeStatus}, " +
                       $"DeterminismSignature={CreateDeterminismSignature(elapsedFrames)}";
            }

            private string CreateDeterminismSignature(int elapsedFrames)
            {
                return $"Case={_smokeCase.Name};" +
                       $"BattleFrame={MaxBattleFrame};" +
                       $"InputTargetFrame={InputTargetFrame};" +
                       $"Entities={MaxEntityCount};" +
                       $"Snapshots={MaxSnapshotCount};" +
                       $"Transforms={MaxTransformEntryCount};" +
                       $"StateHashFrame={MaxStateHashFrame};" +
                       $"StateHash={LastStateHash};" +
                       $"Events={DecodedEventSnapshotCount};" +
                       $"ActorSpawns={DecodedActorSpawnSnapshotCount};" +
                       $"SpawnLocalActor={SpawnLocalActorId};" +
                       $"SpawnTargetActor={SpawnTargetActorId};" +
                       $"TargetHp={TargetMinHp:0.###};" +
                       $"CooldownSkill={LocalCooldownSkillId};" +
                       $"CooldownRemainingMs={MaxLocalSkillCooldownRemainingMs};" +
                       $"LocalManaSpent={LocalMaxManaSpent:0.###};" +
                       $"PendingInputs={PendingInputFrames};" +
                       $"LocalActor={LocalActorId};" +
                       $"TargetActor={TargetActorId};" +
                       $"ElapsedFrames={elapsedFrames}";
            }

            private static global::ET.Scene GetRootScene()
            {
                if (GetFiberMethod == null)
                {
                    return null;
                }

                var fiber = GetFiberMethod.Invoke(global::ET.FiberManager.Instance, new object[] { MainFiberId });
                return fiber?.GetType().GetProperty("Root", BindingFlags.Instance | BindingFlags.Public)?.GetValue(fiber) as global::ET.Scene;
            }
        }
    }
}

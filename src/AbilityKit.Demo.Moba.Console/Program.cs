using System;
using System.IO;
using System.Threading;
using AbilityKit.Demo.Moba.Console.AutoTest;
using AbilityKit.Demo.Moba.Console.Battle.Context;
using AbilityKit.Demo.Moba.Testing;
using AbilityKit.Demo.Moba.Console.Platform;
using AbilityKit.Demo.Moba.Console.Replay;
using AbilityKit.Demo.Moba.Console.Services;

namespace AbilityKit.Demo.Moba.Console
{
    internal sealed class Program
    {
        private static readonly ManualResetEvent _running = new(true);
        private static ConsoleBattleBootstrapper? _bootstrapper;
        private static ReplayController? _replayController;
        private static RecordConfig _recordConfig = new();
        private static bool _exitImmediately;

        private static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--training-stdio")
            {
                System.Console.InputEncoding = new System.Text.UTF8Encoding(false);
                System.Console.OutputEncoding = new System.Text.UTF8Encoding(false);
                var protocolOutput = System.Console.Out;
                System.Console.SetOut(System.Console.Error);
                Environment.ExitCode = ConsoleMobaTrainingStdio.Run(System.Console.In, protocolOutput, System.Console.Error, args[1..]);
                return;
            }

            System.Console.OutputEncoding = System.Text.Encoding.UTF8;

            // 先解析参数。
            _exitImmediately = ParseArguments(args);

            // 设置日志级别。
            if (Log.MinLevel != Log.LogLevel.Trace)
            {
                Log.SetMinLevel(Log.LogLevel.System);
            }

            // 如果命令已处理，则直接退出。
            if (_exitImmediately)
            {
                return;
            }

            Log.System("========================================");
            Log.System("   AbilityKit MOBA Console Demo");
            Log.System("========================================");

            try
            {
                // Console 入口现在只保留 CLI shell 与平台适配；战斗核心流程由 moba.view/runtime 方向的共享源码承担。
                _bootstrapper = new ConsoleBattleBootstrapper(null, null, null, _recordConfig);
                _bootstrapper.Initialize();
                _bootstrapper.Start();
                _bootstrapper.SetupBattle();

                // 根据模式运行。
                switch (_recordConfig.Mode)
                {
                    case RecordMode.Recording:
                        StartRecordingMode();
                        break;
                    case RecordMode.Replaying:
                        StartReplayMode();
                        break;
                    case RecordMode.SkillTest:
                        StartSkillTestMode();
                        break;
                    default:
                        StartTestMode();
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Fatal error: {ex.Message}");
                Log.Error(ex.StackTrace);
                System.Console.ReadLine();
            }
            finally
            {
                _running.Reset();
                _bootstrapper?.Stop();
                _replayController?.Dispose();
                Log.System("Goodbye!");
            }
        }

        /// <summary>
        /// 技能测试模式：只测试技能释放流程。
        /// </summary>
        private static void StartSkillTestMode()
        {
            Log.EnableTrace();
            Log.SetMinLevel(Log.LogLevel.Debug);

            Log.System("");
            Log.System("========================================");
            Log.System("   SKILL CAST TEST MODE");
            Log.System("========================================");

            using var testRunner = new AutoTestRunner(_bootstrapper);
            testRunner.EnableDebugLogging();
            testRunner.OnTestCompleted += OnTestCompleted;

            // 创建技能测试场景，并交给共享 BattleTestScriptRunner 路径执行。
            var skillScenario = new SkillCastScenario { SkillSlot = 1, Repeats = 5 };

            Log.System("");
            Log.System($"[SKILL-TEST] Testing skill slot {skillScenario.SkillSlot}, {skillScenario.Repeats} times");
            Log.System("[SKILL-TEST] Starting shared runner skill script...");
            Log.System("");

            testRunner.RunScenario(skillScenario);
            testRunner.WaitForCompletion();

            Log.System("");
            Log.System("[SKILL-TEST] Test completed. Press Enter to exit...");
            System.Console.ReadLine();
            _running.Reset();
        }

        private static void StartTestMode()
        {
            using var testRunner = new AutoTestRunner(_bootstrapper);
            testRunner.OnTestCompleted += OnTestCompleted;

            Log.System("");
            Log.System("Starting automated tests...");
            Log.System("");

            // 注册帧输入命令测试。
            Log.System("[TEST] Registering frame input test steps...");
            testRunner.RunScenario(new FullBattleScenario());

            var gameThread = new Thread(GameLoop);
            gameThread.IsBackground = true;
            gameThread.Start();

            testRunner.WaitForCompletion();

            Log.System("");
            Log.System("All tests completed. Press Enter to exit...");
            System.Console.ReadLine();
        }

        private static void StartRecordingMode()
        {
            _replayController = new ReplayController(_recordConfig);
            _replayController.StartRecording();

            Log.System("[Record] Recording mode active. Press Enter to stop recording and save.");
            Log.System("");

            var gameThread = new Thread(RecordingGameLoop);
            gameThread.IsBackground = true;
            gameThread.Start();

            System.Console.ReadLine();

            Log.System("");
            Log.System("Stopping recording...");
        }

        private static void StartReplayMode()
        {
            _replayController = new ReplayController(_recordConfig);
            
            if (!_replayController.StartReplay(_recordConfig.InputFilePath))
            {
                Log.Error("[Replay] Failed to start replay mode");
                return;
            }

            Log.System("[Replay] Replay mode active.");
            Log.System("");

            var gameThread = new Thread(ReplayGameLoop);
            gameThread.IsBackground = true;
            gameThread.Start();

            System.Console.ReadLine();

            Log.System("");
            Log.System("Stopping replay...");
        }

        private static void RecordingGameLoop()
        {
            while (_running.WaitOne(33))
            {
                if (_bootstrapper == null) continue;
                try
                {
                    _bootstrapper.Tick();

                    // 如果正在录制，回放控制器录制命令
                    if (_replayController?.IsRecording == true)
                    {
                        var ctx = _bootstrapper.Context;
                        var frame = ctx.LastFrame;

                        // 记录移动
                        if (Math.Abs(ctx.HudMoveDx) > 0.01f || Math.Abs(ctx.HudMoveDz) > 0.01f)
                        {
                            var payload = SimpleMoveCodec.Serialize(ctx.HudMoveDx, ctx.HudMoveDz);
                            _replayController.RecordCommand(ctx.LocalActorId, frame, 
                                InputCommandType.Move, 1, payload);
                        }

                        // 记录技能。
                        var skillSlot = ctx.HudSkillClickSlot;
                        if (skillSlot > 0)
                        {
                            _replayController.RecordCommand(ctx.LocalActorId, frame,
                                InputCommandType.SkillPress, (byte)skillSlot, Array.Empty<byte>());
                        }

                        // 定期添加快照
                        if (frame % _recordConfig.SnapshotIntervalFrames == 0)
                        {
                            var actorCount = ctx.EcsWorld?.AliveCount ?? 0;
                            _replayController.AddSnapshot(frame, actorCount);
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private static void ReplayGameLoop()
        {
            while (_running.WaitOne(33))
            {
                if (_bootstrapper == null) continue;

                var driver = _replayController?.GetReplayDriver<ConsoleReplayDriver>();
                if (driver == null) continue;

                try
                {
                    if (driver.IsPlaying && !driver.IsPaused)
                    {
                        var frame = driver.CurrentFrame;
                        var commands = driver.GetCommandsAtFrame(frame);

                        foreach (var cmd in commands)
                        {
                            // 根据命令类型应用输入
                            switch (cmd.Type)
                            {
                                case InputCommandType.Move:
                                    var (dx, dz) = SimpleMoveCodec.Deserialize(cmd.Payload);
                                    _bootstrapper.Context.HudMoveDx = dx;
                                    _bootstrapper.Context.HudMoveDz = dz;
                                    break;
                                case InputCommandType.SkillPress:
                                    _bootstrapper.Context.HudSkillClickSlot = cmd.OpCode;
                                    break;
                            }
                        }

                        driver.AdvanceFrame();

                        // 回放结束
                        if (driver.CurrentFrame >= driver.TotalFrames)
                        {
                            Log.System("[Replay] Replay finished");
                            driver.Stop();
                        }
                    }

                    _bootstrapper.Tick();
                }
                catch
                {
                }
            }
        }

        private static bool ParseArguments(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i].ToLowerInvariant();

                switch (arg)
                {
                    case "--record":
                    case "-r":
                        _recordConfig.Mode = RecordMode.Recording;
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            _recordConfig.OutputPath = args[++i];
                        }
                        break;

                    case "--replay":
                    case "--play":
                    case "-p":
                        _recordConfig.Mode = RecordMode.Replaying;
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            _recordConfig.InputFilePath = args[++i];
                        }
                        break;

                    case "--list":
                    case "-l":
                        RecordFileManager.ListRecords("Records");
                        return true;

                    case "--info":
                        if (i + 1 < args.Length)
                        {
                            PrintRecordInfo(args[++i]);
                            return true;
                        }
                        break;

                    case "--test":
                    case "-t":
                        _recordConfig.Mode = RecordMode.None;
                        break;

                    case "--skill":
                        _recordConfig.Mode = RecordMode.SkillTest;
                        break;

                    case "--trace":
                    case "--debug":
                        Log.EnableTrace();
                        break;

                    case "--help":
                    case "-h":
                    case "-?":
                        PrintHelp();
                        return true;

                    default:
                        if (arg.StartsWith("-"))
                        {
                            Log.Warn($"Unknown option: {args[i]}");
                        }
                        break;
                }
            }
            return false;
        }

        private static void PrintHelp()
        {
            Log.System("");
            Log.System("AbilityKit MOBA Console Demo");
            Log.System("");
            Log.System("Usage: AbilityKit.Demo.Moba.Console [options]");
            Log.System("");
            Log.System("Options:");
            Log.System("  -r, --record [path]     Start recording mode (saves to Records/replay_*.akrec)");
            Log.System("  -p, --replay <file>    Start replay mode from specified record file");
            Log.System("  -l, --list             List all available record files");
            Log.System("  --info <file>          Show info about a record file");
            Log.System("  -t, --test             Run in test mode (default)");
            Log.System("  --training-stdio [match.json]  Run the local 1v1 training pipe");
            Log.System("  -h, --help             Show this help message");
            Log.System("");
            Log.System("Examples:");
            Log.System("  AbilityKit.Demo.Moba.Console                  # Run tests");
            Log.System("  AbilityKit.Demo.Moba.Console --record          # Record a session");
            Log.System("  AbilityKit.Demo.Moba.Console --replay Records/replay_xxx.akrec");
            Log.System("  AbilityKit.Demo.Moba.Console --list            # List recordings");
            Log.System("");
        }

        private static void PrintRecordInfo(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                var file = FrameRecordFile.ReadFromStream(stream);
                Log.System($"");
                Log.System($"Record File: {Path.GetFileName(filePath)}");
                Log.System($"  Recorded: {file.Header.RecordTime:yyyy-MM-dd HH:mm:ss}");
                Log.System($"  Game Mode: {file.Header.GameMode}");
                Log.System($"  Map: {file.Header.MapName}");
                Log.System($"  Player: {file.Header.PlayerName}");
                Log.System($"  Frames: {file.Header.StartFrame} - {file.Header.EndFrame}");
                Log.System($"  Total Commands: {file.Header.TotalCommands}");
                Log.System($"  Snapshots: {file.Snapshots.Count}");
                Log.System($"  Duration: {file.Header.EndFrame / 30.0:F1} seconds (at 30 FPS)");
                Log.System("");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to read record file: {ex.Message}");
            }
        }

        private static void OnTestCompleted(AutoTestResult results)
        {
            Log.System("");
            if (results.HasUnexpectedError)
            {
                Log.Error($"Test failed with unexpected error: {results.ErrorMessage}");
            }
            else if (results.PassedCount == results.TotalCount)
            {
                Log.System("ALL TESTS PASSED!");
            }
            else
            {
                Log.System($"SOME TESTS FAILED: {results.PassedCount}/{results.TotalCount} passed");
            }
        }

        private static void GameLoop()
        {
            while (_running.WaitOne(33))
            {
                if (_bootstrapper == null) continue;
                try
                {
                    _bootstrapper.Tick(0.033f);
                }
                catch
                {
                }
            }
        }
    }
}

using System;
using System.Threading;
using AbilityKit.Demo.Moba.Console.Battle.Input;
using AbilityKit.Demo.Moba.Console.Battle.Flow;
using AbilityKit.Demo.Moba.Console.Platform;
using AbilityKit.Demo.Moba.Input;

namespace AbilityKit.Demo.Moba.Console.Battle.Input
{
    public sealed class ConsoleInputHandler : IDisposable
    {
        private readonly ConsoleInputFeature _inputFeature;
        private readonly Features.ConsoleHudFeature _hudFeature;
        private readonly IBattleFlow _flow;
        private readonly IInputSource _inputSource;
        private readonly ManualResetEvent _running = new(false);
        private Thread _inputThread;
        private bool _disposed;

        public ConsoleInputHandler(
            ConsoleInputFeature inputFeature,
            Features.ConsoleHudFeature hudFeature,
            IBattleFlow flow,
            IInputSource inputSource)
        {
            _inputFeature = inputFeature ?? throw new ArgumentNullException(nameof(inputFeature));
            _hudFeature = hudFeature ?? throw new ArgumentNullException(nameof(hudFeature));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _inputSource = inputSource ?? throw new ArgumentNullException(nameof(inputSource));
        }

        public void Start()
        {
            if (_inputThread != null) return;

            Log.Input("[InputHandler] Starting...");
            _running.Set();
            _inputThread = new Thread(InputLoop);
            _inputThread.IsBackground = true;
            _inputThread.Start();
        }

        public void Stop()
        {
            if (_inputThread == null) return;

            Log.Input("[InputHandler] Stopping...");
            _running.Reset();
            _inputThread.Join(1000);
            _inputThread = null;
        }

        private void InputLoop()
        {
            Log.Input("[InputHandler] Ready. Press H for help.");

            while (_running.WaitOne(100))
            {
                if (!_inputSource.HasInputAvailable()) continue;

                if (_inputSource.TryReadKey(out var key))
                {
                    ProcessKey(key);
                }
            }
        }

        private void ProcessKey(InputKey key)
        {
            switch (key)
            {
                case InputKey.Up:
                    _inputFeature.ApplyIntent(MobaActorIntent.MoveDirection(0f, -1f));
                    Log.Input("[W] Move Up");
                    break;

                case InputKey.Down:
                    _inputFeature.ApplyIntent(MobaActorIntent.MoveDirection(0f, 1f));
                    Log.Input("[S] Move Down");
                    break;

                case InputKey.Left:
                    _inputFeature.ApplyIntent(MobaActorIntent.MoveDirection(-1f, 0f));
                    Log.Input("[A] Move Left");
                    break;

                case InputKey.Right:
                    _inputFeature.ApplyIntent(MobaActorIntent.MoveDirection(1f, 0f));
                    Log.Input("[D] Move Right");
                    break;

                case InputKey.Skill1:
                    _inputFeature.ApplyIntent(MobaActorIntent.Cast(1));
                    Log.Input("[J] Skill 1");
                    break;

                case InputKey.Skill2:
                    _inputFeature.ApplyIntent(MobaActorIntent.Cast(2));
                    Log.Input("[K] Skill 2");
                    break;

                case InputKey.Skill3:
                    _inputFeature.ApplyIntent(MobaActorIntent.Cast(3));
                    Log.Input("[L] Skill 3");
                    break;

                case InputKey.Attack:
                    _inputFeature.ApplyIntent(MobaActorIntent.Hold);
                    Log.Input("[SPACE] Stop move");
                    break;

                case InputKey.Help:
                    ShowHelp();
                    break;

                case InputKey.Quit:
                    Log.Input("[Q] Quit requested");
                    _flow.Stop();
                    break;

                case InputKey.Menu:
                    Log.System("[TAB] Return to lobby");
                    _flow.ReturnToLobby();
                    break;

                case InputKey.Pause:
                    Log.System("[P] Pause toggled");
                    break;
            }
        }

        private static void ShowHelp()
        {
            System.Console.WriteLine();
            System.Console.WriteLine("========================================");
            System.Console.WriteLine("        INPUT HELP                      ");
            System.Console.WriteLine("========================================");
            System.Console.WriteLine("  W/S/A/D or Arrows - Move            ");
            System.Console.WriteLine("  J/K/L            - Skills 1/2/3      ");
            System.Console.WriteLine("  Space            - Stop move         ");
            System.Console.WriteLine("  H                - Help              ");
            System.Console.WriteLine("  TAB              - Return to lobby   ");
            System.Console.WriteLine("  Q                - Quit             ");
            System.Console.WriteLine("========================================");
            System.Console.WriteLine();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _running.Dispose();
        }
    }
}

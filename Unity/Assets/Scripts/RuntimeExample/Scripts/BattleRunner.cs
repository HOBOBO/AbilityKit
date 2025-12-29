using System;
using System.Collections.Generic;
using NBC;

namespace NBC.ActionEditorExample
{
    /// <summary>
    /// 战斗任务运行器
    /// </summary>
    public class BattleRunner : Runner
    {
        private static BattleRunner _updateRunner;
        public static BattleRunner Scheduler => _updateRunner ??= new BattleRunner();

        private readonly List<IProcess> _updateRoutines = new List<IProcess>();

        private static bool _pause;

        public event Action OnUpdate;

        /// <summary>
        /// 暂停运行器
        /// </summary>
        public static bool Pause
        {
            get => _pause;
            set => _pause = value;
        }

        public BattleRunner()
        {
            Log.I("BattleRunner-BattleRunner");
            MonoManager.Inst.OnUpdate += Update;
            StartCoroutine(new RunnerProcess("BattleRunner", Coroutines, ReadyTask, FlushingOperation));
        }

        private void StartCoroutine(IProcess process)
        {
            var routines = _updateRoutines;
            if (!routines.Contains(process))
            {
                routines.Add(process);
            }
        }

        private void Update()
        {
            if (Pause) return;
            ExecuteRoutines(_updateRoutines);
            OnUpdate?.Invoke();
        }

        private void ExecuteRoutines(List<IProcess> arr)
        {
            if (arr != null && arr.Count > 0)
            {
                for (var index = 0; index < arr.Count; index++)
                {
                    var task = arr[index];
                    var st = task.Process();
                    if (st == TaskStatus.Success)
                    {
                        arr.RemoveAt(index);
                        index--;
                    }
                }
            }
        }
    }
}
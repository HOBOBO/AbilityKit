using NBC.ActionEditor;

namespace NBC.ActionEditorExample
{
    public abstract class SkillClipBase : NTask
    {
        /// <summary>
        /// 时间进度
        /// </summary>
        private float _processTime = 0;


        /// <summary>
        /// 时长
        /// </summary>
        private float _totalTime = 0;

        private float _time = 0;


        /// <summary>
        /// 时长
        /// </summary>
        public float Time
        {
            get => _time;
            set
            {
                _time = value;
                _argsDic["time"] = value;
            }
        }

        public float TotalTime
        {
            get => _totalTime;
            set => _totalTime = value;
        }

        public override float Progress => _processTime / _totalTime;
        public override bool IsRunning => _processTime < _totalTime;

        protected SkillConfig SkillConfig;

        //owen
        protected RoleBase Player;


        protected Clip ActionClip;

        public void SetActionClip(Clip actionClip)
        {
            ActionClip = actionClip;
        }

        public void SetSkill(SkillConfig skillConfig)
        {
            SkillConfig = skillConfig;
        }

        public void SetPlayer(RoleBase player)
        {
            Player = player;
        }

        protected override void OnStart()
        {
            _processTime = 0;
            Begin();
        }

        protected override TaskStatus OnProcess()
        {
            _processTime += UnityEngine.Time.deltaTime;
            Tick();

            if (_processTime >= _totalTime)
            {
                End();
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }

        protected abstract void Begin();
        protected abstract void End();

        protected virtual void Tick()
        {
        }
    }
}
using System.Collections.Generic;
using NBC.ActionEditor;
using UnityEngine;

namespace NBC.ActionEditorExample
{
    public class SkillPlayAttack : ISkillPlay
    {
        private TimelineTaskCollection _timelineEvents;
        private ParallelTaskCollection _sequence;

        public SkillConfig SkillConfig { get; set; }
        public RoleBase Player { get; set; }

        public void Start()
        {
            _sequence = new ParallelTaskCollection();
            _timelineEvents = GetTimelineTask(SkillConfig.EventConfig);

            //在播放时间轴前可以做一些前置动作。比如播放施法前摇时间轴或者一些其他逻辑
            _sequence.AddTask(new RunFunTask(() => { Debug.Log("技能开始前置逻辑"); }));
            _sequence.AddTask(new TimeStopTask(1f)); //测试，在博时间轴前等待1s
            _sequence.AddTask(new RunFunTask(() => { Debug.Log("技能时间轴开始播放"); }));
            _sequence.AddTask(_timelineEvents);
            _sequence.OnCompleted((_) => { Stop(); });
            _sequence.Run(BattleRunner.Scheduler);
        }


        private TimelineTaskCollection GetTimelineTask(SkillAsset skillAsset)
        {
            var events = new TimelineTaskCollection();
            if (skillAsset != null)
            {
                List<Clip> list = new List<Clip>();
                foreach (var group in skillAsset.groups)
                {
                    foreach (var track in group.Tracks)
                    {
                        list.AddRange(track.Clips);
                    }
                }

                foreach (var clip in list)
                {
                    var t = SkillDirector.GetClip(clip);
                    if (t == null) continue;
                    t.SetPlayer(Player);
                    t.SetSkill(SkillConfig);
                    events.AddTask(t);
                }
            }
            else
            {
                Log.E($"技能事件行为配置为空，skillId={SkillConfig.Id}");
            }

            return events;
        }

        public void Stop()
        {
            Log.I($"技能表现播放完毕");
        }
    }
}
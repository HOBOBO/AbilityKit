using UnityEngine;

namespace NBC.ActionEditorExample
{
    public class TriggerEventClipTask : SkillClipBase 
    {
        private TriggerEvent TriggerEvent => ActionClip as TriggerEvent;

        protected override void Begin()
        {
            //可以在这里调用消息机制，派发消息事件
            Debug.Log($"触发一个事件,eventName:{TriggerEvent.eventName}");
        }

        protected override void End()
        {
        }
    }
}
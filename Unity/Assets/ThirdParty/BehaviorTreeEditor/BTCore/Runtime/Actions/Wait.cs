//------------------------------------------------------------
//        File:  Wait.cs
//       Brief:  等待节点
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-09-29
//============================================================

using System;

namespace BTCore.Runtime.Actions
{
    public class Wait : Action
    {
        public int Duration { get; set; } = 1000;    // 等待时长(单位ms)
        
        private DateTime _startTime;
        
        protected override void OnStart() {
            base.OnStart();
            _startTime = DateTime.UtcNow;
        }

        protected override NodeState OnUpdate() {
            var elapsedTime = (DateTime.UtcNow - _startTime).TotalMilliseconds;
            return elapsedTime > Duration ? NodeState.Success : NodeState.Running;
        }

        protected override void OnStop() {
            
        }
    }
}
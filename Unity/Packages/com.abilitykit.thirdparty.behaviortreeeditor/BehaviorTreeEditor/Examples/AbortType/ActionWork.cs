//------------------------------------------------------------
//        File:  ActionWork.cs
//       Brief:  ActionWork
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2024-06-21
//============================================================

using BTCore.Runtime;
using BTCore.Runtime.Actions;
using UnityEngine;

namespace Examples.AbortType
{
    public class ActionWork : Action
    {
        private float _startTime;
        private float _duration = 5000;
        
        protected override void OnStart() {
            base.OnStart();
            Debug.Log("==============正式Work开始=============");
            _startTime = Time.time;
        }

        protected override NodeState OnUpdate() {
            var elapseTime = (Time.time - _startTime) * 1000;
            return elapseTime > _duration ? NodeState.Success : NodeState.Running;
        }

        protected override void OnStop() {
            Debug.Log("==============正式Work结束=============");
            // State = NodeState.Inactive;
        }
    }
}
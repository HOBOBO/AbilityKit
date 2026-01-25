//------------------------------------------------------------
//        File:  BTNode.cs
//       Brief:  BTNode
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-09-29
//============================================================

using System;
using System.Reflection;
using BTCore.Runtime.Blackboards;

namespace BTCore.Runtime
{
    public abstract class BTNode : INode
    {
        public string Name { get; set; }
        public string Guid { get; set; }
        public string Comment { get; set; }
#if UNITY_EDITOR
        public float PosX { get; set; }
        public float PosY { get; set; }
#endif        
        
        public NodeState State = NodeState.Inactive;

        protected Blackboard Blackboard;

        public void SetBlackboard(Blackboard blackboard) {
            Blackboard = blackboard;

            foreach (var propertyInfo in GetType().GetProperties()) {
                if (propertyInfo.PropertyType.IsSubclassOf(typeof(SharedValue))) {
                    if (propertyInfo.GetValue(this) is not SharedValue sharedValue) {
                        continue;
                    }

                    var property = sharedValue.GetType()
                        .GetProperty("Blackboard", BindingFlags.Instance | BindingFlags.NonPublic);
                    property?.SetValue(sharedValue, blackboard);
                }
            }
        }
        
        public void Init() {
            OnInit();
        }
        
        public void Start() {
            OnStart();
        }

        public NodeState Update() {
            return State = OnUpdate();
        }

        public void Stop() {
            OnStop();
        }

        public void Pause(bool isPause) {
            OnPause(isPause);
        }

        protected virtual void OnInit() { }

        protected virtual void OnStart() { State = NodeState.Running; }
        protected virtual NodeState OnUpdate() { return NodeState.Failure; }
        protected virtual void OnStop() { }
        protected virtual void OnPause(bool isPause) { }

        public override string ToString() {
            return $"Node name: {Name} type: {GetType()}";
        }
    }
}

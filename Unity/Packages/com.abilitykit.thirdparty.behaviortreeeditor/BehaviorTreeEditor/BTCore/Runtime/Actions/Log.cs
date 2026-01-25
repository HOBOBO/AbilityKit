//------------------------------------------------------------
//        File:  Log.cs
//       Brief:  日志节点
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-09-29
//============================================================

namespace BTCore.Runtime.Actions
{
    public class Log : Action
    {
        public SharedValue<string> Message { get; set; } = new();

        protected override void OnStart() {
            base.OnStart();
        }

        protected override NodeState OnUpdate() {
            var mes = $"Log: {Message.Value}";
            
            BTLogger.Debug(mes);
            return NodeState.Success;
        }

        protected override void OnStop() {
            
        }
    }
}

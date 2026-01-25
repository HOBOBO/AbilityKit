//------------------------------------------------------------
//        File:  SharedValue.cs
//       Brief:  SharedValue
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-11
//============================================================

using BTCore.Runtime.Blackboards;

namespace BTCore.Runtime
{
    public abstract class SharedValue
    {
        protected Blackboard Blackboard { get; set; }
    }
    
    public class SharedValue<T> : SharedValue
    {
        public string ValueName { get; set; }
        public T RawValue { get; set; }

        public T Value {
            get {
                if (string.IsNullOrEmpty(ValueName) || Blackboard == null) {
                    return RawValue;
                }
                
                return Blackboard.GetValue<T>(ValueName);
            }
        }
    }
}

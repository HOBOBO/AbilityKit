//------------------------------------------------------------
//        File:  PresetBlackboardValue.cs
//       Brief:  PresetBlackboardValue
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-10
//============================================================

namespace BTCore.Runtime.Blackboards
{
    public class IntValue : BlackboardValue<int>
    {
        public IntValue(string name) : base(name) {
        }
    }

    public class FloatValue : BlackboardValue<float>
    {
        public FloatValue(string name) : base(name) {
        }
    }

    public class DoubleValue : BlackboardValue<double>
    {
        public DoubleValue(string name) : base(name) {
        }
    }

    public class StringValue : BlackboardValue<string>
    {
        public StringValue(string name) : base(name) {
        }
    }
}
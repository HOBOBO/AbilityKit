//------------------------------------------------------------
//        File:  BTDef.cs
//       Brief:  BTDef
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-09-29
//============================================================

using Newtonsoft.Json;

namespace BTCore.Runtime
{
    public static class BTDef
    {
        public static readonly JsonSerializerSettings SerializerSettingsAll = new JsonSerializerSettings()
            {TypeNameHandling = TypeNameHandling.All, Formatting = Formatting.Indented};
        
        public static readonly JsonSerializerSettings SerializerSettingsAuto = new JsonSerializerSettings()
            {TypeNameHandling = TypeNameHandling.Auto};
    }
    
    public enum NodeState
    {
        Inactive,
        Running,
        Success,
        Failure
    }
    
    public enum BTLogType
    {
        Debug,
        Warning,
        Error
    }

    public enum AbortType
    {
        None,
        Self,
        LowerPriority,
        Both
    }
}

//------------------------------------------------------------
//        File:  BlackboardValue.cs
//       Brief:  BlackboardValue
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-10
//============================================================

using System;

namespace BTCore.Runtime.Blackboards
{
    public abstract class BlackboardValue
    {
        /// <summary>
        /// 对应黑板变量的名称(唯一)
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// 对应黑板变量的类型
        /// </summary>
        public Type Type { get; }

        protected BlackboardValue(string name, Type type) {
            Name = name;
            Type = type;
        }

        public static BlackboardValue Create(Type type, string name) {
            return Activator.CreateInstance(type, name) as BlackboardValue;
        }
    }
    
    public class BlackboardValue<T> : BlackboardValue
    {
        public T Value { get; set; }
        
         public BlackboardValue(string name) : base(name, typeof(T)) {
            
        }
    }
}
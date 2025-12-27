//------------------------------------------------------------
//        File:  BTNodeAttribute.cs
//       Brief:  BTNodeAttribute
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-08
//============================================================

using System;

namespace BTCore.Runtime.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class NodeInspectorAttribute : Attribute
    {
        public Type NodeType { get; }
        
        public NodeInspectorAttribute(Type nodeType) {
            NodeType = nodeType;
        }
    }
}
//------------------------------------------------------------
//        File:  IExternalNode.cs
//       Brief:  IExternalNode
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-18
//============================================================

using System.Collections.Generic;

namespace BTCore.Runtime.Externals
{
    public interface IExternalNode
    {
        /// <summary>
        /// 对应外部节点类型名称
        /// </summary>
        string TypeName { get; set; }
        /// <summary>
        /// PropertyValue统一用string存储，外部工程直接使用Convert转换为对应需要类型
        /// 主要用来传递基本类型数据，特定工程的类型实例数据请使用黑板来传递
        /// </summary>
        Dictionary<string, string> Properties { get; set; }
    }
}
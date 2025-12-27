//------------------------------------------------------------
//        File:  INode.cs
//       Brief:  INode
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-09-29
//============================================================

namespace BTCore.Runtime
{
    public interface INode
    {
        string Name { get; set; }
        
        string Guid { get; set; }

        string Comment { get; set; }
        
#if UNITY_EDITOR
        float PosX { get; set; }
        
        float PosY { get; set; }
#endif
    }
}

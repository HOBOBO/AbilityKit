//------------------------------------------------------------
//        File:  GroupNode.cs
//       Brief:  GroupNode
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2024-06-27
//============================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace BTCore.Runtime.OtherNodes
{
    public class GroupNode
    {
        public string Title;
        public float X;
        public float Y;
        public float Width;
        public float Height;
        public GroupColor GroupColor;
        public readonly List<string> NodeGuids = new();
    }

    public class GroupColor
    {
        public float R;
        public float G;
        public float B;
        public float A;

        public GroupColor(Color color) {
            R = color.r;
            G = color.g;
            B = color.b;
            A = color.a;
        }
        
        public static implicit operator Color(GroupColor groupColor) {
            return new Color(groupColor.R, groupColor.G, groupColor.B, groupColor.A);
        }
    }
}
#endif
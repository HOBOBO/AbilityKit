using System;
using NBC.ActionEditor;
using UnityEngine;

namespace NBC.ActionEditorExample
{
    [Name("行为轨道")]
    [Description("这是一个行为轨道")]
    [ShowIcon(typeof(Transform))]
    [Color(70f / 255f, 1, 140f / 255f)]
    public class ActionTrack : Track
    {
        [MenuName("测试1")] public int Test1;

        [MenuName("测试2")] public float Test2;

        [MenuName("测试3")] public string Test3;
    }
}
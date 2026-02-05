using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    [CreateAssetMenu(menuName = "AbilityKit/Game/Battle EnterGame Config", fileName = "BattleEnterGameConfig")]
    public sealed class BattleEnterGameConfigSO : ScriptableObject
    {
        [LabelText("地图ID")]
        public int MapId = 1;

        [LabelText("随机种子")]
        public int RandomSeed = 12345;

        [LabelText("TickRate")]
        public int TickRate = 30;

        [LabelText("输入延迟帧")]
        public int InputDelayFrames = 2;

        [LabelText("CreateWorld OpCode")]
        public int OpCode = 0;

        [LabelText("CreateWorld Payload(Base64)")]
        public string PayloadBase64;
    }
}

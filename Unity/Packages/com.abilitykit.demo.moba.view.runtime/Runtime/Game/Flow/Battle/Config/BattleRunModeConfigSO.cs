using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    [CreateAssetMenu(menuName = "AbilityKit/Game/Battle RunMode Config", fileName = "BattleRunModeConfig")]
    public sealed class BattleRunModeConfigSO : ScriptableObject
    {
        [LabelText("运行模式")]
        public BattleStartConfig.BattleRunMode Mode = BattleStartConfig.BattleRunMode.Normal;

        [LabelText("录制输出路径")]
        public string RecordOutputPath = "battle_record.json";

        [LabelText("回放输入路径")]
        public string ReplayInputPath = "battle_record.json";
    }
}

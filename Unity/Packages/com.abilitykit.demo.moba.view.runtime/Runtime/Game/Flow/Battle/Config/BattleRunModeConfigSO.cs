using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    [CreateAssetMenu(menuName = "AbilityKit/Game/Battle RunMode Config", fileName = "BattleRunModeConfig")]
    public sealed class BattleRunModeConfigSO : ScriptableObject
    {
        [LabelText("运行模式")]
        public BattleStartConfig.BattleRunMode Mode = BattleStartConfig.BattleRunMode.Normal;

        [LabelText("录制输出目录")]
        [FolderPath(AbsolutePath = false, ParentFolder = "$persistentDataPath")]
        public string RecordOutputDirectory = "battle_records";

        [LabelText("回放文件")]
        [FilePath(AbsolutePath = false, ParentFolder = "$persistentDataPath", Extensions = "json")]
        public string ReplayInputFilePath = "";
    }
}

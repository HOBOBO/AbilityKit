using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    [CreateAssetMenu(menuName = "AbilityKit/Game/Battle RunMode Config", fileName = "BattleRunModeConfig")]
    public sealed class BattleRunModeConfigSO : ScriptableObject
    {
        public enum InputRecordFormat
        {
            Json = 0,
            Binary = 1,
        }

        [LabelText("运行模式")]
        public BattleStartConfig.BattleRunMode Mode = BattleStartConfig.BattleRunMode.Normal;

        [LabelText("录制格式")]
        public InputRecordFormat RecordFormat = InputRecordFormat.Json;

        [LabelText("录制输出目录")]
        [FolderPath(AbsolutePath = false, ParentFolder = "@UnityEngine.Application.persistentDataPath")]
        public string RecordOutputDirectory = "battle_records";

        [LabelText("回放文件")]
        [FilePath(AbsolutePath = false, ParentFolder = "@UnityEngine.Application.persistentDataPath", Extensions = "json,bin")]
        public string ReplayInputFilePath = "";
    }
}

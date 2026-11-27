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
        public BattleRunMode Mode = BattleRunMode.Normal;

        /// <summary>
        /// 默认只写输入和哈希；需要快照时显式启用增强录制。
        /// </summary>
        [LabelText("录制范围")]
        public BattleRecordCoverage RecordCoverage = BattleRecordCoverage.InputsAndHashes;

        /// <summary>
        /// 仅用于增强录制；最小录制固定为二进制。
        /// </summary>
        [LabelText("增强录制格式")]
        public InputRecordFormat RecordFormat = InputRecordFormat.Binary;

        [LabelText("录制输出目录")]
        [FolderPath(AbsolutePath = false, ParentFolder = "@UnityEngine.Application.persistentDataPath")]
        public string RecordOutputDirectory = "battle_records";

        [LabelText("回放文件")]
        [FilePath(AbsolutePath = false, ParentFolder = "@UnityEngine.Application.persistentDataPath", Extensions = "json,bin")]
        public string ReplayInputFilePath = "";
    }
}

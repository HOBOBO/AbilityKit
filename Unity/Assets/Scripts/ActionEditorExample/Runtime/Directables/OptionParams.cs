using NBC.ActionEditor;

namespace NBC.ActionEditorExample
{
    public partial class EventNames
    {
        [OptionSort(0)] public const int None = 0;
        [MenuName("测试事件")] public const int Test = 1;
        [MenuName("触发击中")] public const int Hit = 2;
        [MenuName("必杀检测")] public const int Kill = 3;
    }

    public partial class EventHitTypes
    {
        [OptionSort(0)] [MenuName("正常结算")] public const int Def = 0;
        [MenuName("拆分结算")] public const int Gap = 1;
        [MenuName("不结算")] public const int Fake = 2;
    }

    public partial class EventShakeType
    {
        [OptionSort(0)] public const int None = 0;
        [MenuName("屏幕")] public const int Screen = 1;
        [MenuName("手机")] public const int Phone = 2;
        [MenuName("手机和屏幕")] public const int ScreenAndPhone = 3;
    }

    public partial class EventShakeForceType
    {
        /// <summary>
        /// 选中 
        /// </summary>
        [MenuName("Selection")] public const int Selection = 0;

        /// <summary>
        /// 常规震动
        /// </summary>
        [MenuName("Vibrate")] public const int Vibrate = 7;

        /// <summary>
        /// Unity自带
        /// </summary>
        [MenuName("Default")] public const int Default = 8;
    }

    public class TrackLayer
    {
        [MenuName("底层")] public const int Bottom = 0;
        [MenuName("角色层")] public const int Mid = 1;
        [MenuName("最顶层")] public const int TopHighest = 2;
        [MenuName("随便什么层")] public const int Custom_NoSet = 3;
    }


    public partial class MoveToType
    {
        [MenuName("None")] [OptionSort(0)] public const int None = 0;
        [MenuName("目标面前")] [OptionSort(1)] public const int Target = 1;
        [MenuName("初始站位")] [OptionSort(2)] public const int OriginalPosition = 2;
    }
}
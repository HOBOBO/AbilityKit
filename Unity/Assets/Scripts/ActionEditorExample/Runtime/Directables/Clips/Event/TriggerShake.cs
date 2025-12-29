using NBC.ActionEditor;

namespace NBC.ActionEditorExample
{
    [Name("触发震动")]
    [Description("触发一个震动")]
    [Color(1, 0, 0)]
    [Attachable(typeof(SignalTrack))]
    public class TriggerShake : ClipSignal
    {
        [MenuName("类型")] [OptionParam(typeof(EventShakeType))]
        public int shakeType;

        [MenuName("屏幕抖动时长")] [OptionRelateParam("shakeType", EventShakeType.Screen, EventShakeType.ScreenAndPhone)]
        public float duration = 0.5f;

        [MenuName("屏幕抖动幅度")] [OptionRelateParam("shakeType", EventShakeType.Screen, EventShakeType.ScreenAndPhone)]
        public int range = 5;

        [MenuName("设备震动时长")] [OptionRelateParam("shakeType", EventShakeType.Phone, EventShakeType.ScreenAndPhone)]
        public float vibrationDuration = 0.5f;

        [MenuName("设备震动强度")]
        [OptionParam(typeof(EventShakeForceType))]
        [OptionRelateParam("shakeType", EventShakeType.Phone, EventShakeType.ScreenAndPhone)]
        public int vibrationForce = EventShakeForceType.Default;

        public override string Info
        {
            get
            {
                var name = AttributesUtility.GetMenuName(shakeType, typeof(EventShakeType));
                if (shakeType == EventShakeType.Screen || shakeType == EventShakeType.ScreenAndPhone)
                {
                    return "震动\n" + name + duration + "s " + range+"px";
                }

                return "震动\n" + name;
            }
        }

        public override bool IsValid => shakeType > 0;
    }
}
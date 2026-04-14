using System;

namespace AbilityKit.Triggering.Runtime.Config.Cue
{
    /// <summary>
    /// Cue 配置实现（静态配置数据）
    /// </summary>
    [Serializable]
    public struct CueConfig : ICueConfig
    {
        public ECueKind Kind { get; set; }
        public string VfxId { get; set; }
        public string SfxId { get; set; }
        public string ExtraData { get; set; }

        public static CueConfig None => new CueConfig { Kind = ECueKind.None };

        public static CueConfig Vfx(string vfxId) => new CueConfig
        {
            Kind = ECueKind.Vfx,
            VfxId = vfxId
        };

        public static CueConfig Sfx(string sfxId) => new CueConfig
        {
            Kind = ECueKind.Sfx,
            SfxId = sfxId
        };

        public static CueConfig VfxSfx(string vfxId, string sfxId) => new CueConfig
        {
            Kind = ECueKind.VfxSfx,
            VfxId = vfxId,
            SfxId = sfxId
        };

        public static CueConfig Custom(string extraData) => new CueConfig
        {
            Kind = ECueKind.Custom,
            ExtraData = extraData
        };

        public bool IsEmpty => Kind == ECueKind.None && string.IsNullOrEmpty(VfxId) && string.IsNullOrEmpty(SfxId);
    }
}
using NBC.ActionEditor;
using UnityEngine;

namespace NBC.ActionEditorExample
{
    [Name("特效轨道")]
    [Description("播放特效的轨道，粒子特效等")]
    [ShowIcon(typeof(ParticleSystem))]
    [Color(0f, 1f, 1f)]
    public class EffectTrack : Track
    {
        [MenuName("轨道层")] [OptionParam(typeof(TrackLayer))]
        public int Layer;
    }
}
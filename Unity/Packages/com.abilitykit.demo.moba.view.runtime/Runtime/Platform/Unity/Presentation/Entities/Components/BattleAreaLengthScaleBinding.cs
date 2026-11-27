using System.Collections.Generic;
using AbilityKit.Game.Flow;
using UnityEngine;

namespace AbilityKit.Game.Battle.Component
{
    /// <summary>
    /// 在特效预制件根节点上声明矩形区域长度的本地轴及资源原生长度。
    /// 原生长度是在预制件原始缩放下的全长，不含请求缩放或旧半径倍率。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleAreaLengthScaleBinding : MonoBehaviour
    {
        /// <summary>
        /// 表现请求中已校验形状的矩形全长参数名。
        /// </summary>
        public const string RectangleLengthParameter = "rectangleLength";

        private enum LocalAxis { X, Z }

        [SerializeField] private LocalAxis _lengthAxis;
        [SerializeField, Min(0.001f)] private float _referenceLength = 1f;
        [SerializeField] private string _previewLengthParameter;
        [SerializeField, Min(0f)] private float _previewWidth;

        /// <summary>
        /// 瞄准预览从技能等级表读取的长度参数；空值不启用矩形预览。
        /// </summary>
        public string PreviewLengthParameter => _previewLengthParameter;

        /// <summary>
        /// 瞄准矩形的全宽（世界单位）。
        /// </summary>
        public float PreviewWidth => _previewWidth;

        /// <summary>
        /// 用原生全长计算预览缩放，返回长轴在资源本地空间中的方向。
        /// </summary>
        public bool TryResolvePreview(float length, float legacyScale, out Vector3 scale, out Vector3 axis)
        {
            scale = Vector3.one * legacyScale;
            axis = Vector3.zero;
            if (!IsPositiveFinite(_previewWidth) || !TryResolveMultiplier(length, out var multiplier)) return false;
            if (_lengthAxis == LocalAxis.X)
            {
                scale.x = multiplier;
                axis = Vector3.right;
            }
            else
            {
                scale.z = multiplier;
                axis = Vector3.forward;
            }
            return true;
        }

        internal void Resolve(
            IReadOnlyList<PresentationParameter> parameters,
            ref PresentationVector3 requestScale,
            ref PresentationVector3 radiusScale)
        {
            if (parameters == null || !IsPositiveFinite(_referenceLength)) return;

            var length = 0f;
            for (var index = 0; index < parameters.Count; index++)
            {
                var parameter = parameters[index];
                if (parameter.Key != RectangleLengthParameter) continue;
                length = parameter.Value;
                break;
            }

            if (!TryResolveMultiplier(length, out var multiplier)) return;

            if (_lengthAxis == LocalAxis.X)
            {
                requestScale = new PresentationVector3(1f, requestScale.Y, requestScale.Z);
                radiusScale = new PresentationVector3(multiplier, radiusScale.Y, radiusScale.Z);
            }
            else if (_lengthAxis == LocalAxis.Z)
            {
                requestScale = new PresentationVector3(requestScale.X, requestScale.Y, 1f);
                radiusScale = new PresentationVector3(radiusScale.X, radiusScale.Y, multiplier);
            }
        }

        private bool TryResolveMultiplier(float length, out float multiplier)
        {
            multiplier = IsPositiveFinite(_referenceLength) ? length / _referenceLength : 0f;
            return (_lengthAxis == LocalAxis.X || _lengthAxis == LocalAxis.Z) && IsPositiveFinite(multiplier);
        }

        private static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value);
    }
}

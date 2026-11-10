using AbilityKit.Game.Flow;
using UnityEngine;

namespace AbilityKit.Game.Platform.Unity.Presentation
{
    /// <summary>Shared spatial semantics for Unity backends; no rendering order implementation lives here.</summary>
    public static class UnityPresentationTransformUtility
    {
        public static Transform ResolveSocket(Transform root, in PresentationAttachment attachment)
        {
            if (root == null) return null;
            return attachment.Kind == PresentationAttachmentKind.Socket && !string.IsNullOrEmpty(attachment.Socket)
                ? root.Find(attachment.Socket) ?? root : root;
        }

        public static Vector3 ResolvePosition(Transform target, in PresentationAttachment attachment)
        {
            var offset = new Vector3(attachment.Offset.X, attachment.Offset.Y, attachment.Offset.Z);
            return attachment.OffsetSpace == PresentationOffsetSpace.World
                ? target.position + offset : target.TransformPoint(offset);
        }

        public static Vector3 ResolvePosition(Vector3 position, Quaternion rotation, Vector3 scale, in PresentationAttachment attachment)
        {
            var offset = new Vector3(attachment.Offset.X, attachment.Offset.Y, attachment.Offset.Z);
            return position + (attachment.OffsetSpace == PresentationOffsetSpace.World ? offset : rotation * Vector3.Scale(scale, offset));
        }

        public static void ApplyWorldScale(Transform visual, Vector3 contentScale, Vector3 inheritedScale)
        {
            var desired = Vector3.Scale(contentScale, inheritedScale);
            if (visual.parent != null)
            {
                var parent = visual.parent.lossyScale;
                desired = new Vector3(Divide(desired.x, parent.x), Divide(desired.y, parent.y), Divide(desired.z, parent.z));
            }
            visual.localScale = desired;
        }

        private static float Divide(float value, float divisor) => Mathf.Abs(divisor) > 0.00001f ? value / divisor : 0f;
    }
}

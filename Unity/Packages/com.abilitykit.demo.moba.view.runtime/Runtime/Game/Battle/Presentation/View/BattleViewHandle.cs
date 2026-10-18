using AbilityKit.Game.Battle.Entity;
using AbilityKit.World.ECS;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleViewHandle
    {
        public int Version;
        public bool Destroyed;
        public int ActorId;
        public int ModelId;

        /// <summary>The entity kind, set when the shell is first created.</summary>
        public BattleEntityKind Kind = BattleEntityKind.Unknown;

        public GameObject GameObject;
        public MonoViewHandle ViewHandle;
        public int VfxId;
        public IEntityId VfxEntityId;
        public Vector3 PendingPos;
        public bool HasPendingPos;
        public Vector3 PendingForward;
        public bool HasPendingForward;
        public Vector3 LastSampledPos;
        public bool HasLastSampledPos;
        public Vector3 LastSourceForward;
        public bool HasLastSourceForward;
        public BattleViewPositionSampleBuffer Pos;

        /// <summary>
        /// Last position actually written to the GameObject's transform.
        /// Used as the smoothing source so position changes blend with the
        /// previous rendered pose instead of snapping.
        /// </summary>
        public Vector3 LastDisplayedPos;

        /// <summary>
        /// True after the first frame has written to the GameObject.
        /// The applier skips smoothing on the first frame to avoid initial wobble.
        /// </summary>
        public bool HasLastDisplayed;
    }
}

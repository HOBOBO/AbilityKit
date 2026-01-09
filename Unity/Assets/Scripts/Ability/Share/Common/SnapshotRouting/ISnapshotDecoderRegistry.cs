using AbilityKit.Ability.Server;

namespace AbilityKit.Ability.Share.Common.SnapshotRouting
{
    public interface ISnapshotDecoderRegistry
    {
        public delegate bool TryDecode<T>(in WorldStateSnapshot snap, out T value);
        void RegisterDecoder<T>(int opCode, TryDecode<T> decoder);
    }
}

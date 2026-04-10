using AbilityKit.Ability.Host;

namespace AbilityKit.Core.Common.SnapshotRouting
{
    public interface ISnapshotDecoderRegistry
    {
        public delegate bool TryDecode<T>(in WorldStateSnapshot snap, out T value);
        void RegisterDecoder<T>(int opCode, TryDecode<T> decoder);
    }
}

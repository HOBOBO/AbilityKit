namespace AbilityKit.Network.Runtime.Conditioning
{
    /// <summary>Fixed xorshift32 stream for cross-runtime virtual playback.</summary>
    internal sealed class StableNetworkRandom
    {
        private uint _state;

        public StableNetworkRandom(int seed)
        {
            _state = (uint)seed;
            if (_state == 0) _state = 0x9E3779B9u;
        }

        public double NextDouble() => NextUInt32() / 4294967296d;

        public int Next(int minimum, int maximumExclusive)
        {
            return minimum + (int)(NextUInt32() % (uint)(maximumExclusive - minimum));
        }

        private uint NextUInt32()
        {
            var value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value;
            return value;
        }
    }
}

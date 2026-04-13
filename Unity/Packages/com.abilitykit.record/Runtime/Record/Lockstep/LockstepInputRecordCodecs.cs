using System;
using System.IO;

namespace AbilityKit.Core.Common.Record.Lockstep
{
    public static class LockstepInputRecordCodecs
    {
        private static ILockstepInputRecordCodec s_current;

        public static ILockstepInputRecordCodec Current
        {
            get
            {
                if (s_current != null) return s_current;
                s_current = new DefaultLockstepInputRecordCodec();
                return s_current;
            }
            set
            {
                s_current = value;
            }
        }

        private sealed class DefaultLockstepInputRecordCodec : ILockstepInputRecordCodec
        {
            private readonly LockstepBinaryInputRecordCodec _bin = new LockstepBinaryInputRecordCodec();
            private readonly LockstepJsonInputRecordCodec _json = new LockstepJsonInputRecordCodec();

            public ILockstepInputRecordWriter CreateWriter(string outputPath, LockstepInputRecordMeta meta)
            {
                var ext = Path.GetExtension(outputPath);
                if (string.Equals(ext, ".bin", StringComparison.OrdinalIgnoreCase))
                {
                    return _bin.CreateWriter(outputPath, meta);
                }

                return _json.CreateWriter(outputPath, meta);
            }

            public LockstepInputRecordFile Load(string path)
            {
                var ext = Path.GetExtension(path);
                if (string.Equals(ext, ".bin", StringComparison.OrdinalIgnoreCase))
                {
                    return _bin.Load(path);
                }

                return _json.Load(path);
            }
        }
    }
}

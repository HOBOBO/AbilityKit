using System;

namespace Luban
{
    public sealed class ByteBuf
    {
        private readonly Luban.Serialization.ByteBuf _buf;

        public ByteBuf(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            _buf = new Luban.Serialization.ByteBuf(bytes);
        }

        private ByteBuf(Luban.Serialization.ByteBuf buf)
        {
            _buf = buf ?? throw new ArgumentNullException(nameof(buf));
        }

        public static ByteBuf Wrap(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            return new ByteBuf(Luban.Serialization.ByteBuf.Wrap(bytes));
        }

        public int ReadInt() => _buf.ReadInt();
        public long ReadLong() => _buf.ReadLong();
        public float ReadFloat() => _buf.ReadFloat();
        public double ReadDouble() => _buf.ReadDouble();
        public bool ReadBool() => _buf.ReadBool();
        public byte ReadByte() => _buf.ReadByte();
        public short ReadShort() => _buf.ReadShort();
        public string ReadString() => _buf.ReadString();
        public int ReadSize() => _buf.ReadSize();

        public void Replace(byte[] bytes) => _buf.Replace(bytes);

        public int ReaderIndex
        {
            get => _buf.ReaderIndex;
            set => _buf.ReaderIndex = value;
        }

        public int WriterIndex
        {
            get => _buf.WriterIndex;
            set => _buf.WriterIndex = value;
        }

        public int Capacity => _buf.Capacity;
        public int Size => _buf.Size;
        public bool Empty => _buf.Empty;
        public bool NotEmpty => _buf.NotEmpty;

        internal Luban.Serialization.ByteBuf Raw => _buf;
    }
}

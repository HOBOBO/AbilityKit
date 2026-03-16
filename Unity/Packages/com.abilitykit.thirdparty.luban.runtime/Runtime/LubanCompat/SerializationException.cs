using System;

namespace Luban
{
    public class SerializationException : Exception
    {
        public SerializationException() { }
        public SerializationException(string msg) : base(msg) { }
        public SerializationException(string message, Exception innerException) : base(message, innerException) { }
    }
}

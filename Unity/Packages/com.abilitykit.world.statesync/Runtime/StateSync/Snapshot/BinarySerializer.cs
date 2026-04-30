using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AbilityKit.Ability.StateSync.Snapshot
{
    public static class BinarySerializer
    {
        public static byte[] Serialize<T>(T obj) where T : class
        {
            if (obj == null) return Array.Empty<byte>();

            using var stream = new MemoryStream();
            var serializer = new BinarySerializationContext(stream);
            SerializeObject(serializer, obj, 0);
            return stream.ToArray();
        }

        public static byte[] SerializeObject(object obj)
        {
            if (obj == null) return Array.Empty<byte>();

            using var stream = new MemoryStream();
            var serializer = new BinarySerializationContext(stream);
            SerializeObject(serializer, obj, 0);
            return stream.ToArray();
        }

        public static T Deserialize<T>(byte[] data) where T : class
        {
            if (data == null || data.Length == 0) return null;

            using var stream = new MemoryStream(data);
            var context = new BinaryDeserializationContext(stream);
            return (T)DeserializeObject(context, typeof(T), 0);
        }

        public static object DeserializeObject(byte[] data, Type targetType)
        {
            if (data == null || data.Length == 0) return null;

            using var stream = new MemoryStream(data);
            var context = new BinaryDeserializationContext(stream);
            return DeserializeObject(context, targetType, 0);
        }

        private static void SerializeObject(BinarySerializationContext ctx, object value, int depth)
        {
            if (depth > 32) throw new InvalidOperationException("Maximum serialization depth exceeded");

            if (value == null)
            {
                ctx.Writer.Write(false);
                return;
            }
            ctx.Writer.Write(true);

            var type = value.GetType();

            if (type.IsPrimitive || type == typeof(decimal))
            {
                WritePrimitive(ctx.Writer, value);
                return;
            }

            if (value is string str)
            {
                ctx.Writer.Write(str);
                return;
            }

            if (value is Array array)
            {
                ctx.Writer.Write(array.Length);
                var elementType = type.GetElementType();
                foreach (var item in array)
                {
                    SerializeObject(ctx, item, depth + 1);
                }
                return;
            }

            if (value is System.Collections.IDictionary dict)
            {
                ctx.Writer.Write(dict.Count);
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    SerializeObject(ctx, entry.Key, depth + 1);
                    SerializeObject(ctx, entry.Value, depth + 1);
                }
                return;
            }

            if (value is System.Collections.IEnumerable enumerable)
            {
                var list = enumerable.Cast<object>().ToList();
                ctx.Writer.Write(list.Count);
                foreach (var item in list)
                {
                    SerializeObject(ctx, item, depth + 1);
                }
                return;
            }

            SerializeStruct(ctx, value, depth);
        }

        private static void SerializeStruct(BinarySerializationContext ctx, object value, int depth)
        {
            var fields = value.GetType().GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            ctx.Writer.Write(fields.Length);
            foreach (var field in fields)
            {
                var fieldValue = field.GetValue(value);
                SerializeObject(ctx, fieldValue, depth + 1);
            }
        }

        private static object DeserializeObject(BinaryDeserializationContext ctx, Type expectedType, int depth)
        {
            if (depth > 32) throw new InvalidOperationException("Maximum deserialization depth exceeded");

            if (!ctx.Reader.ReadBoolean())
                return null;

            if (expectedType.IsPrimitive || expectedType == typeof(decimal))
            {
                return ReadPrimitive(ctx.Reader, expectedType);
            }

            if (expectedType == typeof(string))
            {
                return ctx.Reader.ReadString();
            }

            if (expectedType.IsArray)
            {
                var length = ctx.Reader.ReadInt32();
                var elementType = expectedType.GetElementType();
                var array = Array.CreateInstance(elementType, length);
                for (int i = 0; i < length; i++)
                {
                    array.SetValue(DeserializeObject(ctx, elementType, depth + 1), i);
                }
                return array;
            }

            if (typeof(System.Collections.IDictionary).IsAssignableFrom(expectedType))
            {
                var count = ctx.Reader.ReadInt32();
                var dictType = expectedType;
                var keyType = dictType.GetGenericArguments()[0];
                var valueType = dictType.GetGenericArguments()[1];
                var dict = (System.Collections.IDictionary)Activator.CreateInstance(dictType);

                for (int i = 0; i < count; i++)
                {
                    var key = DeserializeObject(ctx, keyType, depth + 1);
                    var val = DeserializeObject(ctx, valueType, depth + 1);
                    dict.Add(key, val);
                }
                return dict;
            }

            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(expectedType))
            {
                var count = ctx.Reader.ReadInt32();
                var elementType = expectedType.GetGenericArguments().FirstOrDefault() ?? typeof(object);
                var listType = typeof(List<>).MakeGenericType(elementType);
                var list = (System.Collections.IList)Activator.CreateInstance(listType);

                for (int i = 0; i < count; i++)
                {
                    list.Add(DeserializeObject(ctx, elementType, depth + 1));
                }
                return list;
            }

            return DeserializeStruct(ctx, expectedType, depth);
        }

        private static object DeserializeStruct(BinaryDeserializationContext ctx, Type type, int depth)
        {
            var obj = Activator.CreateInstance(type);
            var fields = type.GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            var fieldCount = ctx.Reader.ReadInt32();
            for (int i = 0; i < fieldCount && i < fields.Length; i++)
            {
                var fieldValue = DeserializeObject(ctx, fields[i].FieldType, depth + 1);
                fields[i].SetValue(obj, fieldValue);
            }

            return obj;
        }

        private static void WritePrimitive(BinaryWriter writer, object value)
        {
            if (value is bool b) writer.Write(b);
            else if (value is byte bt) writer.Write(bt);
            else if (value is sbyte sb) writer.Write(sb);
            else if (value is char c) writer.Write(c);
            else if (value is short s) writer.Write(s);
            else if (value is ushort us) writer.Write(us);
            else if (value is int i) writer.Write(i);
            else if (value is uint ui) writer.Write(ui);
            else if (value is long l) writer.Write(l);
            else if (value is ulong ul) writer.Write(ul);
            else if (value is float f) writer.Write(f);
            else if (value is double d) writer.Write(d);
            else if (value is decimal dec)
            {
                var bits = decimal.GetBits(dec);
                foreach (var bit in bits) writer.Write(bit);
            }
        }

        private static object ReadPrimitive(BinaryReader reader, Type type)
        {
            if (type == typeof(bool)) return reader.ReadBoolean();
            if (type == typeof(byte)) return reader.ReadByte();
            if (type == typeof(sbyte)) return reader.ReadSByte();
            if (type == typeof(char)) return reader.ReadChar();
            if (type == typeof(short)) return reader.ReadInt16();
            if (type == typeof(ushort)) return reader.ReadUInt16();
            if (type == typeof(int)) return reader.ReadInt32();
            if (type == typeof(uint)) return reader.ReadUInt32();
            if (type == typeof(long)) return reader.ReadInt64();
            if (type == typeof(ulong)) return reader.ReadUInt64();
            if (type == typeof(float)) return reader.ReadSingle();
            if (type == typeof(double)) return reader.ReadDouble();
            if (type == typeof(decimal))
            {
                int[] bits = new int[4];
                for (int i = 0; i < 4; i++) bits[i] = reader.ReadInt32();
                return new decimal(bits);
            }
            throw new NotSupportedException($"Type {type} is not primitive");
        }

        private class BinarySerializationContext
        {
            public BinaryWriter Writer { get; }

            public BinarySerializationContext(Stream stream)
            {
                Writer = new BinaryWriter(stream);
            }
        }

        private class BinaryDeserializationContext
        {
            public BinaryReader Reader { get; }

            public BinaryDeserializationContext(Stream stream)
            {
                Reader = new BinaryReader(stream);
            }
        }
    }
}

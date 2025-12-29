using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace AbilityKit.Ability.Share
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class BinaryMemberAttribute : Attribute
    {
        public int Order { get; }
        public BinaryMemberAttribute(int order) => Order = order;
    }

    public static class BinaryObjectCodec
    {
        private static readonly ConcurrentDictionary<Type, TypeModel> s_models = new ConcurrentDictionary<Type, TypeModel>();

        public static byte[] Encode<T>(in T value)
        {
            using var ms = new MemoryStream(256);
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
            WriteValue(bw, typeof(T), value);
            bw.Flush();
            return ms.ToArray();
        }

        public static T Decode<T>(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using var ms = new MemoryStream(bytes);
            using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);
            return (T)ReadValue(br, typeof(T));
        }

        private static void WriteValue(BinaryWriter bw, Type type, object value)
        {
            if (type == typeof(int)) { bw.Write((int)value); return; }
            if (type == typeof(uint)) { bw.Write((uint)value); return; }
            if (type == typeof(long)) { bw.Write((long)value); return; }
            if (type == typeof(ulong)) { bw.Write((ulong)value); return; }
            if (type == typeof(short)) { bw.Write((short)value); return; }
            if (type == typeof(ushort)) { bw.Write((ushort)value); return; }
            if (type == typeof(byte)) { bw.Write((byte)value); return; }
            if (type == typeof(sbyte)) { bw.Write((sbyte)value); return; }
            if (type == typeof(bool)) { bw.Write((bool)value); return; }
            if (type == typeof(float)) { bw.Write((float)value); return; }
            if (type == typeof(double)) { bw.Write((double)value); return; }

            if (type == typeof(string))
            {
                WriteString(bw, (string)value);
                return;
            }

            if (type == typeof(byte[]))
            {
                WriteBytes(bw, (byte[])value);
                return;
            }

            if (type.IsArray)
            {
                var elemType = type.GetElementType();
                var arr = (Array)value;
                if (arr == null || arr.Length == 0)
                {
                    bw.Write(0);
                    return;
                }

                bw.Write(arr.Length);
                for (int i = 0; i < arr.Length; i++)
                {
                    WriteValue(bw, elemType, arr.GetValue(i));
                }

                return;
            }

            if (TryWriteSingleValueWrapper(bw, type, value))
            {
                return;
            }

            var model = GetModel(type);
            for (int i = 0; i < model.Members.Length; i++)
            {
                var m = model.Members[i];
                var v = m.Getter(value);
                WriteValue(bw, m.MemberType, v);
            }
        }

        private static object ReadValue(BinaryReader br, Type type)
        {
            if (type == typeof(int)) return br.ReadInt32();
            if (type == typeof(uint)) return br.ReadUInt32();
            if (type == typeof(long)) return br.ReadInt64();
            if (type == typeof(ulong)) return br.ReadUInt64();
            if (type == typeof(short)) return br.ReadInt16();
            if (type == typeof(ushort)) return br.ReadUInt16();
            if (type == typeof(byte)) return br.ReadByte();
            if (type == typeof(sbyte)) return br.ReadSByte();
            if (type == typeof(bool)) return br.ReadBoolean();
            if (type == typeof(float)) return br.ReadSingle();
            if (type == typeof(double)) return br.ReadDouble();

            if (type == typeof(string)) return ReadString(br);
            if (type == typeof(byte[])) return ReadBytes(br);

            if (type.IsArray)
            {
                var elemType = type.GetElementType();
                var count = br.ReadInt32();
                if (count <= 0) return Array.CreateInstance(elemType, 0);
                var arr = Array.CreateInstance(elemType, count);
                for (int i = 0; i < count; i++)
                {
                    arr.SetValue(ReadValue(br, elemType), i);
                }

                return arr;
            }

            if (TryReadSingleValueWrapper(br, type, out var wrapper))
            {
                return wrapper;
            }

            var model = GetModel(type);
            var valuesByName = new Dictionary<string, object>(model.Members.Length, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < model.Members.Length; i++)
            {
                var m = model.Members[i];
                valuesByName[m.Name] = ReadValue(br, m.MemberType);
            }

            var args = new object[model.CtorParams.Length];
            for (int i = 0; i < model.CtorParams.Length; i++)
            {
                var p = model.CtorParams[i];
                if (!valuesByName.TryGetValue(p.Name, out var v))
                {
                    throw new InvalidOperationException($"Cannot map binary member to ctor param '{type.FullName}.{p.Name}'.");
                }

                args[i] = v;
            }

            return model.Constructor.Invoke(args);
        }

        private static bool TryWriteSingleValueWrapper(BinaryWriter bw, Type type, object value)
        {
            var member = FindSingleValueMember(type);
            if (member == null) return false;

            var wrappedValue = member.Getter(value);
            WriteValue(bw, member.MemberType, wrappedValue);
            return true;
        }

        private static bool TryReadSingleValueWrapper(BinaryReader br, Type type, out object wrapper)
        {
            var member = FindSingleValueMember(type);
            if (member == null)
            {
                wrapper = null;
                return false;
            }

            var v = ReadValue(br, member.MemberType);

            var ctor = type.GetConstructor(new[] { member.MemberType });
            if (ctor == null)
            {
                wrapper = null;
                return false;
            }

            wrapper = ctor.Invoke(new[] { v });
            return true;
        }

        private static MemberModel FindSingleValueMember(Type type)
        {
            if (!(type.IsValueType || type.IsClass) || type == typeof(string)) return null;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            var fields = type.GetFields(flags)
                .Where(f => !f.IsStatic)
                .ToArray();
            var props = type.GetProperties(flags)
                .Where(p => p.GetMethod != null && !p.GetMethod.IsStatic)
                .ToArray();

            if (fields.Length + props.Length != 1) return null;

            if (fields.Length == 1)
            {
                return MemberModel.FromField(fields[0]);
            }

            return MemberModel.FromProperty(props[0]);
        }

        private static TypeModel GetModel(Type type)
        {
            return s_models.GetOrAdd(type, BuildModel);
        }

        private static TypeModel BuildModel(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

            var members = new List<MemberModel>();

            foreach (var f in type.GetFields(flags))
            {
                if (f.IsStatic) continue;
                members.Add(MemberModel.FromField(f));
            }

            foreach (var p in type.GetProperties(flags))
            {
                if (p.GetMethod == null || p.GetMethod.IsStatic) continue;
                members.Add(MemberModel.FromProperty(p));
            }

            var orderedMembers = members
                .Select(m => (m, attr: m.MemberInfo.GetCustomAttribute<BinaryMemberAttribute>()))
                .OrderBy(x => x.attr != null ? 0 : 1)
                .ThenBy(x => x.attr != null ? x.attr.Order : 0)
                .ThenBy(x => x.m.Name, StringComparer.Ordinal)
                .Select(x => x.m)
                .ToArray();

            var ctors = type.GetConstructors(flags);
            var ctor = ctors
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault();

            if (ctor == null)
            {
                throw new InvalidOperationException($"Type '{type.FullName}' must have a public instance constructor for binary decode.");
            }

            var ctorParams = ctor.GetParameters();
            var paramNames = new HashSet<string>(ctorParams.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < ctorParams.Length; i++)
            {
                var p = ctorParams[i];
                if (!orderedMembers.Any(m => string.Equals(m.Name, p.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException($"Type '{type.FullName}' ctor param '{p.Name}' has no matching public field/property.");
                }
            }

            var finalMembers = orderedMembers
                .Where(m => paramNames.Contains(m.Name))
                .ToArray();

            return new TypeModel(type, ctor, ctorParams, finalMembers);
        }

        private static void WriteString(BinaryWriter bw, string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                bw.Write(0);
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(s);
            bw.Write(bytes.Length);
            bw.Write(bytes);
        }

        private static string ReadString(BinaryReader br)
        {
            var len = br.ReadInt32();
            if (len <= 0) return string.Empty;
            var bytes = br.ReadBytes(len);
            return Encoding.UTF8.GetString(bytes);
        }

        private static void WriteBytes(BinaryWriter bw, byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                bw.Write(0);
                return;
            }

            bw.Write(data.Length);
            bw.Write(data);
        }

        private static byte[] ReadBytes(BinaryReader br)
        {
            var len = br.ReadInt32();
            if (len <= 0) return Array.Empty<byte>();
            return br.ReadBytes(len);
        }

        private sealed class TypeModel
        {
            public readonly Type Type;
            public readonly ConstructorInfo Constructor;
            public readonly ParameterInfo[] CtorParams;
            public readonly MemberModel[] Members;

            public TypeModel(Type type, ConstructorInfo constructor, ParameterInfo[] ctorParams, MemberModel[] members)
            {
                Type = type;
                Constructor = constructor;
                CtorParams = ctorParams;
                Members = members;
            }
        }

        private sealed class MemberModel
        {
            public readonly string Name;
            public readonly Type MemberType;
            public readonly MemberInfo MemberInfo;
            public readonly Func<object, object> Getter;

            private MemberModel(string name, Type memberType, MemberInfo memberInfo, Func<object, object> getter)
            {
                Name = name;
                MemberType = memberType;
                MemberInfo = memberInfo;
                Getter = getter;
            }

            public static MemberModel FromField(FieldInfo f)
            {
                return new MemberModel(f.Name, f.FieldType, f, obj => f.GetValue(obj));
            }

            public static MemberModel FromProperty(PropertyInfo p)
            {
                return new MemberModel(p.Name, p.PropertyType, p, obj => p.GetValue(obj));
            }
        }
    }
}

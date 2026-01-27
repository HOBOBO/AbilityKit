using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Sirenix.Serialization;
using Sirenix.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emilia.Kit
{
    public static class TagSerializationUtility
    {
        private static Type _tupleInterface;

        private static Type tupleInterface
        {
            get
            {
                if (_tupleInterface == null)
                {
                    _tupleInterface = typeof(string).Assembly.GetType("System.ITuple");
                    if (_tupleInterface == null) _tupleInterface = typeof(string).Assembly.GetType("System.ITupleInternal");
                }

                return _tupleInterface;
            }
        }

        public static byte[] IgnoreTagSerializeValue<T>(T value, DataFormat dataFormat, out List<Object> references, params string[] ignoreTags)
        {
            SerializationContext context = new SerializationContext();
            context.Config.SerializationPolicy = GetIgnoreTagPolicy(ignoreTags);
            return SerializationUtility.SerializeValue(value, dataFormat, out references, context);
        }

        public static byte[] IgnoreTagSerializeValue<T>(T value, DataFormat dataFormat, params string[] ignoreTags)
        {
            SerializationContext context = new SerializationContext();
            context.Config.SerializationPolicy = GetIgnoreTagPolicy(ignoreTags);
            return SerializationUtility.SerializeValue(value, dataFormat, context);
        }

        public static ISerializationPolicy GetIgnoreTagPolicy(string[] ignoreTags)
        {
            CustomSerializationPolicy ignoreTagPolicy = new CustomSerializationPolicy("IgnoreTagFilter", true, member => {
                if (member is PropertyInfo)
                {
                    PropertyInfo propertyInfo = member as PropertyInfo;
                    if (propertyInfo.GetGetMethod(true) == null || propertyInfo.GetSetMethod(true) == null) return false;
                }
                if (member.IsDefined<NonSerializedAttribute>(true) && ! member.IsDefined<OdinSerializeAttribute>()) return false;

                if (IgnoreTagFilter(ignoreTags, member) == false) return false;

                if ((member is FieldInfo
                     && ((member as FieldInfo).IsPublic
                         || (member.DeclaringType.IsNestedPrivate
                             && member.DeclaringType.IsDefined<CompilerGeneratedAttribute>())
                         || (tupleInterface != null
                             && tupleInterface.IsAssignableFrom(member.DeclaringType))))
                    || member.IsDefined<SerializeField>(false)
                    || member.IsDefined<OdinSerializeAttribute>(false)) return true;

                return UnitySerializationUtility.SerializeReferenceAttributeType != null && member.IsDefined(UnitySerializationUtility.SerializeReferenceAttributeType, false);
            });

            return ignoreTagPolicy;
        }

        private static bool IgnoreTagFilter(string[] ignoreTags, MemberInfo memberInfo)
        {
            SerializeTagAttribute tagAttribute = memberInfo.GetCustomAttribute<SerializeTagAttribute>();
            if (tagAttribute == null) return true;

            int tagLength = tagAttribute.tags.Length;
            for (int i = 0; i < tagLength; i++)
            {
                string tag = tagAttribute.tags[i];
                int ignoreTagLength = ignoreTags.Length;
                for (int j = 0; j < ignoreTagLength; j++)
                {
                    string ignoreTag = ignoreTags[j];
                    if (string.Equals(tag, ignoreTag)) return false;
                }
            }

            return true;
        }

        public static byte[] OnlyTagSerializeValue<T>(T value, DataFormat dataFormat, out List<Object> references, params string[] onlyTags)
        {
            SerializationContext context = new SerializationContext();
            context.Config.SerializationPolicy = GetOnlyTagPolicy(onlyTags);
            return SerializationUtility.SerializeValue(value, dataFormat, out references, context);
        }

        public static ISerializationPolicy GetOnlyTagPolicy(string[] onlyTags)
        {
            CustomSerializationPolicy ignoreTagPolicy = new CustomSerializationPolicy("IgnoreTagFilter", true, member => {
                if (member is PropertyInfo)
                {
                    PropertyInfo propertyInfo = member as PropertyInfo;
                    if (propertyInfo.GetGetMethod(true) == null || propertyInfo.GetSetMethod(true) == null) return false;
                }
                if (member.IsDefined<NonSerializedAttribute>(true) && ! member.IsDefined<OdinSerializeAttribute>()) return false;
                return OnlyTagFilter(onlyTags, member);
            });

            return ignoreTagPolicy;
        }

        private static bool OnlyTagFilter(string[] onlyTags, MemberInfo memberInfo)
        {
            SerializeTagAttribute tagAttribute = memberInfo.GetCustomAttribute<SerializeTagAttribute>();
            if (tagAttribute == null) return false;

            int tagLength = tagAttribute.tags.Length;
            for (int i = 0; i < tagLength; i++)
            {
                string tag = tagAttribute.tags[i];
                int onlyTagLength = onlyTags.Length;
                for (int j = 0; j < onlyTagLength; j++)
                {
                    string onlyTag = onlyTags[j];
                    if (string.Equals(tag, onlyTag)) return true;
                }
            }
            return false;
        }
    }
}
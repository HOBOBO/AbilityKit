using System;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 通用的枚举
    /// </summary>
    public class GenericEnumId<T> : IEquatable<GenericEnumId<T>> where T : IEquatable<T>
    {
        private readonly T _id;

        public GenericEnumId(T id)
        {
            _id = id;
        }

        public bool Equals(GenericEnumId<T> other) => _id.Equals(other._id);
        public override bool Equals(object obj) => obj is GenericEnumId<T> other && Equals(other);
        public override int GetHashCode() => _id.GetHashCode();

        public static bool operator ==(GenericEnumId<T> a, GenericEnumId<T> b) => a.Equals(b);
        public static bool operator !=(GenericEnumId<T> a, GenericEnumId<T> b) => !a.Equals(b);
    }
}
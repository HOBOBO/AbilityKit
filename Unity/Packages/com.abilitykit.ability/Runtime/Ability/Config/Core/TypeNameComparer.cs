using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// Type.FullName-based equality comparer for Dictionary keys.
    /// Works around IL2CPP/AOT issues where two Type objects representing the same type
    /// may not be reference-equal or Equals-equal (they can have identical GetHashCode
    /// but Equals returns false due to AOT type reconstruction).
    /// Using FullName ensures consistent string-based identity across assembly loads.
    /// </summary>
    public sealed class TypeNameComparer : IEqualityComparer<Type>
    {
        public static readonly TypeNameComparer Instance = new TypeNameComparer();

        private TypeNameComparer() { }

        public bool Equals(Type x, Type y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x == null || y == null) return false;
            return x.FullName == y.FullName;
        }

        public int GetHashCode(Type obj) => obj != null ? obj.FullName.GetHashCode() : 0;
    }
}

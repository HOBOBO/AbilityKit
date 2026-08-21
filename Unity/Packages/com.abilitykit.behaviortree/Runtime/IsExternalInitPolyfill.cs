using System;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill for C# 9.0 init accessor in .NET Framework 4.x / .NET Standard 2.1.
    /// 与 com.abilitykit.protocol.room / protocol.moba 内的同名 polyfill 同模式：
    /// internal 保证仅在程序集内部可见，不产生跨程序集冲突。
    /// </summary>
    internal static class IsExternalInit { }
}

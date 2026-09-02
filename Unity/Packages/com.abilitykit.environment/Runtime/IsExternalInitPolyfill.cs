// 仅在 .NET 5 以下的运行时（Unity 的 .NET Standard 2.1）里提供 IsExternalInit。
// .NET 镜像（net10.0）自带该类型，guard 会跳过本文件，避免 CS0436 冲突。
// 与 behaviortree / protocol.room / protocol.moba 内的同名 polyfill 同源，但加了框架版本 guard。
#if !NET5_0_OR_GREATER
using System;

namespace System.Runtime.CompilerServices
{
    /// <summary>C# 9.0 init 访问器在 .NET Standard 2.1 下的 polyfill（internal，仅程序集内可见）。</summary>
    internal static class IsExternalInit { }
}
#endif

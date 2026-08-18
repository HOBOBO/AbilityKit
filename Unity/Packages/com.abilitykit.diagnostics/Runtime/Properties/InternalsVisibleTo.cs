using System.Runtime.CompilerServices;

// 允许 .NET 测试工程直接验证 internal 换算/记账函数（如 TicksToNanoseconds 的大 tick 溢出回归）。
[assembly: InternalsVisibleTo("AbilityKit.Diagnostics.Tests")]

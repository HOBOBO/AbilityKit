#nullable enable

using System;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Foundation
{
    /// <summary>
    /// MarkerRegistry - Marker 注册模式
    /// </summary>
    [Sample]
    public sealed class MarkerRegistry : SampleBase
    {
        public override string Title => "Marker Registry";
        public override string Description => "???? Attribute ????????????";
        public override SampleCategory Category => SampleCategory.Foundation;

        protected override void OnRun()
        {
            Log("Marker Registry (??????)");
            Output.Divider();

            Log("Marker ??????? Attribute ?????????????");
            Log("");

            Log("????:");
            Output.Bullet("MarkerAttribute: ???????");
            Output.Bullet("MarkerScanner<T>: ?????");
            Output.Bullet("IMarkerRegistry: ?????");
            Output.Bullet("MarkerRegistry<T>: ????");

            Output.Divider();

            Log("????:");
            Output.Numbered(1, "????? Attribute (?? MarkerAttribute)");
            Output.Numbered(2, "?? Registry (?? IMarkerRegistry)");
            Output.Numbered(3, "????? Attribute");
            Output.Numbered(4, "????????");

            Output.Divider();

            Log("??:");
            Output.Bullet("???: ?? Attribute ?????????");
            Output.Bullet("??: ???????????");
            Output.Bullet("??: ??????????");

            Output.Divider();

            Log("????:");
            Output.Bullet("ExecutableTypeIdAttribute: ??????");
            Output.Bullet("ConditionTypeIdAttribute: ??????");

            Output.Divider();

            Log("????:");
            Log("  [AttributeUsage(...)]");
            Log("  public sealed class MyTypeIdAttribute : MarkerAttribute");
            Log("  {");
            Log("      public int TypeId { get; }");
            Log("      public override void OnScanned(Type t, IMarkerRegistry r) { ... }");
            Log("  }");
            Log("  ");
            Log("  [MyTypeId(1, \"MyType\")]");
            Log("  public sealed class MyType : IMyInterface { }");
        }
    }
}

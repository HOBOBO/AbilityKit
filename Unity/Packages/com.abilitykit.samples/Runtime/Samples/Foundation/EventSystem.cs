#nullable enable

using System;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Foundation
{
    /// <summary>
    /// EventSystem - 事件系统
    /// </summary>
    [Sample]
    public sealed class EventSystem : SampleBase
    {
        public override string Title => "Event System";
        public override string Description => "?? AbilityKit.Core ???????";
        public override SampleCategory Category => SampleCategory.Foundation;

        protected override void OnRun()
        {
            Log("???? (Event System)");
            Output.Divider();

            Log("AbilityKit ?? GlobalEventDispatcher ?????????");
            Log("");

            Log("????:");
            Output.Bullet("GlobalEventDispatcher: ???????");
            Output.Bullet("EventKey<T>: ????????");
            Output.Bullet("IEventSubscription: ??????");

            Output.Divider();

            Log("????:");
            Log("  var dispatcher = GlobalEventDispatcher.Instance;");
            Log("  var key = new EventKey<DamageEvent>(\"OnDamage\");");
            Log("  var sub = dispatcher.Subscribe(key, OnDamage);");
            Log("  dispatcher.Dispatch(key, new DamageEvent { Damage = 100 });");
            Log("  sub.Dispose();");

            Output.Divider();

            Log("??:");
            Output.Bullet("????: EventKey<T> ??????");
            Output.Bullet("???: IEventSubscription.Dispose()");
            Output.Bullet("????: ?????????");

            Output.Divider();

            Log("API ????:");
            Log("  AbilityKit.Core.Eventing");

            Output.Divider();

            Log("????????:");
            Log("  var eventBus = new GlobalEventDispatcher();");
            Log("  var key = new EventKey<AttackEvent>(\"OnAttack\");");
            Log("  var sub = eventBus.Subscribe(key, handler);");
            Log("  eventBus.Dispatch(key, new AttackEvent { ... });");
            Log("  sub.Dispose();");
        }
    }
}

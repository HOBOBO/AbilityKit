using System.Reflection;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Triggering.Blackboard;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Triggering;

public sealed class MobaTriggerPlanReconcileServiceTests
{
    [Fact]
    public void Reconcile_rechecks_exact_ids_when_hash_collides()
    {
        var subscriptions = new MobaTriggerPlanSubscriptionService();
        typeof(MobaTriggerPlanSubscriptionService)
            .GetField("_ownerBlackboards", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(subscriptions, new OwnerBlackboardStore());

        var gateway = new MobaTriggerExecutionGateway(effects: null, subscriptions, diagnostics: null);
        var reconcile = new MobaTriggerPlanReconcileService();
        typeof(MobaTriggerPlanReconcileService)
            .GetField("_triggers", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(reconcile, gateway);

        const long ownerKey = 9001;
        reconcile.Reconcile(new[]
        {
            new OngoingTriggerPlanEntry { OwnerKey = ownerKey, TriggerIds = new[] { 1, 32 } },
        });
        reconcile.Reconcile(new[]
        {
            new OngoingTriggerPlanEntry { OwnerKey = ownerKey, TriggerIds = new[] { 1, 32 } },
        });

        // 17*31^2 + 1*31 + 32 == 17*31^2 + 2*31 + 1.
        reconcile.Reconcile(new[]
        {
            new OngoingTriggerPlanEntry { OwnerKey = ownerKey, TriggerIds = new[] { 2, 1 } },
        });

        Assert.Equal(2, gateway.Stats.OwnerApplyRequests);
        Assert.Equal(2, gateway.Stats.OwnerApplied);
    }
}

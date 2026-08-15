using AbilityKit.Core.Lifetime;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class DisposableRegistrationTests
{
    [Fact]
    public void Create_RejectsNullReleaseCallbacks()
    {
        Assert.Throws<ArgumentNullException>(() => DisposableRegistration.Create(null!));
        Assert.Throws<ArgumentNullException>(() => DisposableRegistration.Create(42, null!));
    }

    [Fact]
    public void Dispose_InvokesCallbackOnlyOnce()
    {
        var releaseCount = 0;
        var registration = DisposableRegistration.Create(() => releaseCount++);

        registration.Dispose();
        registration.Dispose();

        Assert.Equal(1, releaseCount);
    }

    [Fact]
    public void StateOverload_PassesStateAndInvokesCallbackOnlyOnce()
    {
        var released = new List<string>();
        var registration = DisposableRegistration.Create(
            "subscription",
            state => released.Add(state));

        registration.Dispose();
        registration.Dispose();

        Assert.Equal(new[] { "subscription" }, released);
    }

    [Fact]
    public void Dispose_RemainsReleasedWhenCallbackThrows()
    {
        var releaseCount = 0;
        var registration = DisposableRegistration.Create(() =>
        {
            releaseCount++;
            throw new InvalidOperationException("release failed");
        });

        Assert.Throws<InvalidOperationException>(() => registration.Dispose());
        registration.Dispose();

        Assert.Equal(1, releaseCount);
    }

    [Fact]
    public void ConcurrentDispose_InvokesCallbackAtMostOnce()
    {
        var releaseCount = 0;
        var registration = DisposableRegistration.Create(
            () => Interlocked.Increment(ref releaseCount));

        Parallel.For(0, 128, _ => registration.Dispose());

        Assert.Equal(1, Volatile.Read(ref releaseCount));
    }

    [Fact]
    public void StateOverload_ConcurrentDisposeInvokesCallbackAtMostOnce()
    {
        var releaseCount = 0;
        var registration = DisposableRegistration.Create(
            7,
            state => Interlocked.Add(ref releaseCount, state));

        Parallel.For(0, 128, _ => registration.Dispose());

        Assert.Equal(7, Volatile.Read(ref releaseCount));
    }
}

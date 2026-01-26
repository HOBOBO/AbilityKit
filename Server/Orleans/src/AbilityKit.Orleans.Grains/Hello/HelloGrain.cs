using AbilityKit.Orleans.Contracts.Hello;
using Orleans;

namespace AbilityKit.Orleans.Grains.Hello;

public sealed class HelloGrain : Grain, IHelloGrain
{
    public Task<string> SayHello(string name)
    {
        var key = this.GetPrimaryKeyString();
        return Task.FromResult($"Hello {name}! (GrainKey={key})");
    }
}

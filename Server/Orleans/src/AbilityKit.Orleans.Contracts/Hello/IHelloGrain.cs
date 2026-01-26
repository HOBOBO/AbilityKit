using Orleans;

namespace AbilityKit.Orleans.Contracts.Hello;

public interface IHelloGrain : IGrainWithStringKey
{
    Task<string> SayHello(string name);
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AbilityKit.Demo.Moba.Services;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Demo.Moba.Tests.Config;

public sealed class MobaModifierConsumerContractTests
{
    private readonly ITestOutputHelper _output;

    public MobaModifierConsumerContractTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Every_published_parameter_has_a_staged_runtime_consumer()
    {
        var catalogPath = Path.Combine(AppContext.BaseDirectory, "Configs", "luban",
            "moba", "modifier_parameters.json");
        var rows = JArray.Parse(File.ReadAllText(catalogPath));
        var published = rows.ToDictionary(row => row.Value<int>("Id"));
        var consumers = typeof(MobaSkillParamModifierService).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                BindingFlags.Instance | BindingFlags.Static |
                                                BindingFlags.DeclaredOnly))
            .SelectMany(method => method.GetCustomAttributes<MobaModifierConsumesAttribute>()
                .Select(contract => (Method: method, Contract: contract)))
            .ToArray();

        foreach (var consumer in consumers)
        {
            var id = consumer.Contract.ParameterId;
            Assert.True(published.ContainsKey(id),
                $"{consumer.Method.DeclaringType?.FullName}.{consumer.Method.Name} declares unpublished parameter {id}.");
            Assert.True(MobaSkillParamModifierKeys.TryResolve(id, out var key));
            Assert.True(MobaSkillParamModifierKeys.TryGetResolveStage(key, out var stage));
            Assert.Equal(stage, consumer.Contract.Stage);
            Assert.True(MobaSkillParamModifierKeys.TryGetConsumerTypeName(id, out var typeName));
            Assert.Contains(consumer.Method.DeclaringType?.Name, typeName.Split(','));
            Assert.True(Enum.IsDefined(typeof(MobaModifierSampleTiming), consumer.Contract.Timing));
            Assert.True(MobaSkillParamModifierKeys.TryGetConsumerSampleTiming(id, out var publishedTiming));
            Assert.Equal(publishedTiming, consumer.Contract.Timing);
        }

        foreach (var row in rows)
        {
            var id = row.Value<int>("Id");
            var registrations = consumers.Where(consumer => consumer.Contract.ParameterId == id)
                .OrderBy(consumer => consumer.Method.DeclaringType?.FullName, StringComparer.Ordinal)
                .ThenBy(consumer => consumer.Method.Name, StringComparer.Ordinal)
                .ToArray();
            Assert.True(registrations.Length > 0,
                $"Published modifier parameter {id} ({row.Value<string>("Key")}) has no runtime consumer.");
            Assert.True(MobaSkillParamModifierKeys.TryGetConsumerTypeName(id, out var typeName));
            Assert.Equal(typeName.Split(',').OrderBy(name => name, StringComparer.Ordinal),
                    registrations.Select(registration => registration.Method.DeclaringType?.Name)
                        .Distinct().OrderBy(name => name, StringComparer.Ordinal));
            Assert.True(MobaSkillParamModifierKeys.TryGetConsumerSampleTiming(id, out _));
            foreach (var registration in registrations)
                _output.WriteLine($"{id} {row.Value<string>("Key")} {registration.Contract.Stage} " +
                    $"{registration.Contract.Timing} {registration.Method.DeclaringType?.FullName}." +
                    registration.Method.Name);
        }
    }

    [Fact]
    public void Generated_parameter_schema_enforces_declared_value_bounds()
    {
        Assert.True(MobaSkillParamModifierKeys.TryValidateValue(
            MobaSkillParamModifierKeys.Emitter.Ids.FanAngleDeg, 360f, out _));
        Assert.False(MobaSkillParamModifierKeys.TryValidateValue(
            MobaSkillParamModifierKeys.Emitter.Ids.FanAngleDeg, 360.01f, out _));
        Assert.True(MobaSkillParamModifierKeys.TryValidateValue(
            MobaSkillParamModifierKeys.Emitter.Ids.CountPerShot, 1f, out _));
        Assert.False(MobaSkillParamModifierKeys.TryValidateValue(
            MobaSkillParamModifierKeys.Emitter.Ids.CountPerShot, 0f, out _));
        Assert.False(MobaSkillParamModifierKeys.TryValidateValue(
            MobaSkillParamModifierKeys.Skill.Ids.ResourceCost, float.NaN, out _));
    }
}

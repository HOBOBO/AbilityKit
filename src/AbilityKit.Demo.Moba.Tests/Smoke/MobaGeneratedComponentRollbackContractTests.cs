using System.Reflection;
using System.Collections;
using System.Text.Json;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Mathematics;
using AbilityKit.Attributes.Core;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Rollback;
using Entitas;
using MemoryPack;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaGeneratedComponentRollbackContractTests
{
    public static IEnumerable<object[]> GeneratedComponents() => new[]
    {
        "Lifetime", "MoveInput", "ProjectileEffectSnapshot", "ProjectileLauncher",
        "SkillCastCancelRequest", "SkillCastDestroyRequest", "SkillCastRunningTag",
        "SkillCastTimelineRuntime", "SkillLoadout", "SummonMeta", "Transform",
        "ActorDespawnRequest", "ChampionLevel", "SkillCastRetention", "ResourceContainer"
    }.Select(name => new object[] { name });

    [Fact]
    public void Every_unexempted_pin_component_has_a_runtime_field_contract()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "tools/entitas-codegen.moba.pin.json")))
            root = root.Parent;
        Assert.NotNull(root);
        using var pin = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, "tools/entitas-codegen.moba.pin.json")));
        var generated = pin.RootElement.GetProperty("contexts").GetProperty("Actor").GetProperty("components")
            .EnumerateArray().Where(component => component.GetProperty("coverage").ValueKind == JsonValueKind.Null)
            .Select(component => component.GetProperty("name").GetString()).OrderBy(name => name).ToArray();
        Assert.Equal(generated, GeneratedComponents().Select(row => (string)row[0]).OrderBy(name => name));
    }

    [Theory]
    [MemberData(nameof(GeneratedComponents))]
    public void Generated_codec_restores_all_public_fields_and_presence_in_both_directions(string name)
    {
        var index = Array.IndexOf(ActorComponentsLookup.componentNames, name);
        Assert.True(index >= 0);
        var type = ActorComponentsLookup.componentTypes[index];
        var context = new ActorContext();
        var source = context.CreateEntity();
        var target = context.CreateEntity();
        var expected = (IComponent)Sample(type, 1);
        source.AddComponent(index, expected);
        var present = Capture(source);
        var absent = Capture(target);

        /*
         * 直接检验真实生成 codec 的所有字段；不把 Cast 专属字段伪装成 Actor 载荷。
         * 逐字段比较独立样本，避免 Write/Restore 同时漏字段仍凭字节相等误过。
         */
        Restore(target, present);
        Assert.True(target.HasComponent(index));
        EqualFields(expected, target.GetComponent(index));
        if (!type.IsValueType && type.GetFields().Length > 0)
            Assert.NotSame(expected, target.GetComponent(index));

        target.ReplaceComponent(index, (IComponent)Sample(type, 2));
        Restore(target, present);
        EqualFields(expected, target.GetComponent(index));
        Restore(target, absent);
        Assert.False(target.HasComponent(index));
        Restore(target, present);
        EqualFields(expected, target.GetComponent(index));
        Assert.Equal(present, Capture(target));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Skill_arrays_preserve_null_versus_empty(bool empty)
    {
        var context = new ActorContext();
        var source = context.CreateEntity();
        var target = context.CreateEntity();
        source.AddSkillLoadout(7, empty ? Array.Empty<ActiveSkillRuntime>() : null,
            empty ? Array.Empty<PassiveSkillRuntime>() : null);
        target.AddSkillLoadout(8, new[] { new ActiveSkillRuntime() }, new[] { new PassiveSkillRuntime() });
        Restore(target, Capture(source));
        Assert.Equal(empty, target.skillLoadout.ActiveSkills != null);
        Assert.Equal(empty, target.skillLoadout.PassiveSkills != null);
        if (empty)
        {
            Assert.Empty(target.skillLoadout.ActiveSkills);
            Assert.Empty(target.skillLoadout.PassiveSkills);
        }
    }

    [Fact]
    public void Managed_array_elements_preserve_explicit_null_without_sentinel_values()
    {
        var context = new ActorContext();
        var entity = context.CreateEntity();
        entity.AddSkillLoadout(7, new ActiveSkillRuntime[] { null!, new() { SkillId = 0, Level = 0 } },
            new PassiveSkillRuntime[] { new() { PassiveSkillId = int.MinValue }, null! });
        var saved = Capture(entity);
        entity.ReplaceSkillLoadout(8, Array.Empty<ActiveSkillRuntime>(), Array.Empty<PassiveSkillRuntime>());
        Restore(entity, saved);
        Assert.Null(entity.skillLoadout.ActiveSkills[0]);
        Assert.NotNull(entity.skillLoadout.ActiveSkills[1]);
        Assert.Equal(0, entity.skillLoadout.ActiveSkills[1].SkillId);
        Assert.Equal(int.MinValue, entity.skillLoadout.PassiveSkills[0].PassiveSkillId);
        Assert.Null(entity.skillLoadout.PassiveSkills[1]);
    }

    [Fact]
    public void Corrupt_late_component_entry_is_rejected_before_any_live_write()
    {
        var context = new ActorContext();
        var first = context.CreateEntity();
        first.AddActorId(1);
        first.AddLifetime(11);
        var last = context.CreateEntity();
        last.AddActorId(2);
        last.AddLifetime(22);
        var provider = new MobaEntitasComponentRollbackProvider(context);
        var frame = new FrameIndex(10);
        var target = MemoryPackSerializer.Deserialize<MobaEntitasComponentRollbackPayload>(provider.Export(frame));
        target.Entries[1] = new MobaEntitasComponentRollbackEntry(2, 0, new byte[] { 1 });
        var invalid = MemoryPackSerializer.Serialize(target);
        first.ReplaceLifetime(111);
        last.ReplaceLifetime(222);
        var live = provider.Export(frame);
        Assert.ThrowsAny<Exception>(() => provider.ValidateImport(frame, invalid));
        Assert.Equal(live, provider.Export(frame));
        Assert.ThrowsAny<Exception>(() => provider.Import(frame, invalid));
        Assert.Equal(live, provider.Export(frame));
    }

    private static byte[] Capture(ActorEntity entity)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        ActorComponentSerialization.Write(entity, writer);
        return stream.ToArray();
    }

    private static void Restore(ActorEntity entity, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);
        ActorComponentSerialization.Restore(entity, reader);
        Assert.Equal(stream.Length, stream.Position);
    }

    private static object Sample(Type type, int seed)
    {
        if (type == typeof(int)) return 11 + seed;
        if (type == typeof(long)) return 9876543210L + seed;
        if (type == typeof(float)) return -2.25f * seed;
        if (type == typeof(bool)) return true;
        if (type == typeof(Fixed64)) return Fixed64.FromRaw(1234567890123L + seed);
        if (type == typeof(AttributeId)) return AttributeId.FromRaw(7654321 + seed);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var dictionary = (IDictionary)Activator.CreateInstance(type)!;
            var arguments = type.GetGenericArguments();
            dictionary.Add(Sample(arguments[0], seed), Sample(arguments[1], seed));
            return dictionary;
        }
        if (type.IsEnum) return Enum.GetValues(type).GetValue(1)!;
        if (type == typeof(Transform3)) return new Transform3(new Vec3(1.25f * seed, -2, 3),
            new Quat(.1f, -.2f * seed, .3f, .7f), new Vec3(-2, .5f * seed, 3.5f * seed));
        if (type.IsArray)
        {
            var array = Array.CreateInstance(type.GetElementType()!, 2);
            for (var i = 0; i < array.Length; i++) array.SetValue(Sample(type.GetElementType()!, seed + i), i);
            return array;
        }
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        Assert.True(typeof(IComponent).IsAssignableFrom(type) || fields.Length > 0, $"Missing sample contract: {type}");
        var value = Activator.CreateInstance(type)!;
        for (var i = 0; i < fields.Length; i++)
        {
            Assert.False(fields[i].IsInitOnly, $"Missing readonly sample contract: {type}.{fields[i].Name}");
            fields[i].SetValue(value, Sample(fields[i].FieldType, seed + i));
        }
        return value;
    }

    private static void EqualFields(object expected, object actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType());
        var type = expected.GetType();
        if (type.IsValueType)
        {
            Assert.Equal(expected, actual);
            return;
        }
        Assert.NotSame(expected, actual);
        if (expected is IDictionary leftMap && actual is IDictionary rightMap)
        {
            Assert.Equal(leftMap.Count, rightMap.Count);
            foreach (var key in leftMap.Keys) EqualFields(leftMap[key]!, rightMap[key]!);
            return;
        }
        if (expected is Array left && actual is Array right)
        {
            Assert.Equal(left.Length, right.Length);
            for (var i = 0; i < left.Length; i++) EqualFields(left.GetValue(i)!, right.GetValue(i)!);
            return;
        }
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            EqualFields(field.GetValue(expected)!, field.GetValue(actual)!);
    }
}

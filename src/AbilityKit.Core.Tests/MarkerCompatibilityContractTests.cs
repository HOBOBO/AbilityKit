/*
 * 保留弃用入口的直接契约覆盖，不为兼容 API 新增生产使用方。
 */
#pragma warning disable CS0618
using System.Reflection;
using AbilityKit.Core.Markers;
using Xunit;

namespace AbilityKit.Core.Tests
{
    [CollectionDefinition(Name)]
    public sealed class MarkerSystemCollection
    {
        public const string Name = "Core MarkerSystem 状态隔离";
    }

    [Collection(MarkerSystemCollection.Name)]
    public sealed class MarkerCompatibilityContractTests : IDisposable
    {
        public MarkerCompatibilityContractTests() => MarkerSystem.Reset();
        public void Dispose() => MarkerSystem.Reset();

        [Fact]
        public void Default_attribute_callback_is_a_formal_no_op()
        {
            var registry = new MarkerRegistry<EmptyAttribute>();
            new EmptyAttribute().OnScanned(typeof(First), registry);
            Assert.Empty(registry.Types);
        }

        [Fact]
        public void Plain_registry_filters_only_null_abstract_and_interface_and_preserves_first_order()
        {
            var registry = new MarkerRegistry<EmptyAttribute>();
            foreach (var type in new[] { null, typeof(Abstract), typeof(IDisposable), typeof(First), typeof(int), typeof(List<>), typeof(First) })
                registry.Register(type!);
            Assert.Equal(new[] { typeof(First), typeof(int), typeof(List<>) }, registry.Types);
            Assert.Equal(3, registry.Count);
        }

        [Fact]
        public void Plain_registry_exposes_live_underlying_list_interface()
        {
            var registry = new MarkerRegistry<EmptyAttribute>();
            var view = registry.Types;
            registry.Register(typeof(First));
            Assert.Same(view, registry.Types);
            Assert.Equal(typeof(First), Assert.Single(view));
            Assert.IsType<List<Type>>(view);
        }

        [Fact]
        public void Plain_foreach_includes_callback_append_and_propagates_failure_without_rollback()
        {
            var registry = Plain();
            var seen = new List<Type>();
            var failure = new InvalidOperationException("回调失败");
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => registry.ForEach(type =>
            {
                seen.Add(type);
                registry.Register(typeof(Second));
                if (type == typeof(Second)) throw failure;
            })));
            Assert.Equal(new[] { typeof(First), typeof(Second) }, seen);
            Assert.Equal(seen, registry.Types);
        }

        [Fact]
        public void Where_is_deferred_and_observes_appends_before_and_during_enumeration()
        {
            var registry = Plain();
            var calls = 0;
            var query = registry.Where(type =>
            {
                calls++;
                registry.Register(typeof(int));
                return type != typeof(Second);
            });
            Assert.Equal(0, calls);
            registry.Register(typeof(Second));
            Assert.Equal(new[] { typeof(First), typeof(int) }, query.ToArray());
            Assert.Equal(3, calls);
        }

        [Fact]
        public void Where_failure_occurs_during_enumeration_and_find_uses_live_order()
        {
            var registry = Plain();
            var failure = new InvalidOperationException("谓词失败");
            var query = registry.Where(_ => throw failure);
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => query.ToArray()));
            Assert.Equal(typeof(Second), registry.Find(type =>
            {
                registry.Register(typeof(Second));
                return type == typeof(Second);
            }));
            Assert.Equal(typeof(First), registry.Find(_ => true));
            Assert.Null(registry.Find(_ => false));
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => registry.Find(_ => throw failure)));
        }

        [Fact]
        public void Empty_plain_registry_does_not_prevalidate_callbacks()
        {
            var registry = new MarkerRegistry<EmptyAttribute>();
            registry.ForEach(null!);
            Assert.Empty(registry.Where(null!));
            Assert.Null(registry.Find(null!));
            registry.Register(typeof(First));
            Assert.Throws<NullReferenceException>(() => registry.ForEach(null!));
            Assert.Throws<NullReferenceException>(() => registry.Where(null!).ToArray());
            Assert.Throws<NullReferenceException>(() => registry.Find(null!));
        }

        [Fact]
        public void Keyed_registry_keeps_historical_types_and_type_only_registration_has_no_key()
        {
            var registry = Keyed();
            var types = registry.Types;
            var keys = registry.Keys;
            registry.Register(typeof(int));
            Assert.Equal(0, registry.Count);
            registry.Register("key", typeof(First));
            registry.Register("key", typeof(Second));
            registry.Register("other", typeof(Second));
            Assert.Equal(2, registry.Count);
            Assert.Equal(new[] { typeof(int), typeof(First), typeof(Second) }, types);
            Assert.Equal(typeof(Second), registry.Get("key"));
            Assert.True(registry.Contains("other"));
            Assert.Equal(new[] { "key", "other" }, keys.Order().ToArray());
            Assert.IsType<List<Type>>(types);
        }

        [Fact]
        public void Keyed_registry_filters_types_before_validating_key_and_uses_default_comparison()
        {
            var registry = Keyed();
            foreach (var type in new[] { null, typeof(Abstract), typeof(IDisposable) })
            {
                registry.Register(null!, type!);
                registry.Register(type!);
            }
            Assert.Empty(registry.Types);
            Assert.Equal(0, registry.Count);
            Assert.Throws<ArgumentNullException>(() => registry.Register(null!, typeof(First)));
            registry.Register("a", typeof(First));
            registry.Register("A", typeof(List<>));
            Assert.Equal(2, registry.Count);
            Assert.False(registry.TryGet("missing", out var missing));
            Assert.Null(missing);
            Assert.False(registry.Contains("missing"));
            Assert.Throws<KeyNotFoundException>(() => registry.Get("missing"));
            Assert.Throws<ArgumentNullException>(() => registry.Contains(null!));
        }

        [Fact]
        public void Keyed_foreach_propagates_callback_failure_without_changing_entries()
        {
            var registry = Keyed();
            registry.ForEach(null!);
            registry.Register("a", typeof(First));
            var failure = new InvalidOperationException("键回调失败");
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => registry.ForEach((key, type) =>
            {
                Assert.Equal("a", key);
                Assert.Equal(typeof(First), type);
                throw failure;
            })));
            Assert.Equal(1, registry.Count);
        }

        [Fact]
        public void Instance_helpers_create_each_time_and_skip_missing_or_incompatible_types()
        {
            var registry = Keyed();
            registry.Register("new", typeof(Creatable));
            registry.Register("wrong", typeof(ThrowingConstructor));
            Assert.NotSame(registry.GetOrCreateInstance("new"), registry.GetOrCreateInstance("new"));
            Assert.NotSame(registry.GetOrCreateInstance<Creatable>("new"), registry.GetOrCreateInstance<Creatable>("new"));
            Assert.Null(registry.GetOrCreateInstance("missing"));
            Assert.Null(registry.GetOrCreateInstance<Creatable>("missing"));
            Assert.Null(registry.GetOrCreateInstance<Creatable>("wrong"));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Instance_helpers_preserve_constructor_wrapping(bool generic)
        {
            var registry = Keyed();
            registry.Register("throw", typeof(ThrowingConstructor));
            var error = Assert.Throws<TargetInvocationException>(() =>
                generic ? registry.GetOrCreateInstance<object>("throw") : registry.GetOrCreateInstance("throw"));
            Assert.IsType<InvalidOperationException>(error.InnerException);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Instance_helpers_defer_missing_constructor_validation(bool generic)
        {
            var registry = Keyed();
            registry.Register("no-default", typeof(NoDefaultConstructor));
            Assert.Throws<MissingMethodException>(() => generic
                ? registry.GetOrCreateInstance<object>("no-default") : registry.GetOrCreateInstance("no-default"));
        }

        [Fact]
        public void Instance_helper_can_box_value_types_but_rejects_open_generics_at_creation()
        {
            var registry = Keyed();
            registry.Register("value", typeof(int));
            registry.Register("open", typeof(List<>));
            Assert.Equal(0, registry.GetOrCreateInstance("value"));
            Assert.Throws<ArgumentException>(() => registry.GetOrCreateInstance("open"));
        }

        [Fact]
        public void Scanner_handles_null_inputs_without_reading_assembly()
        {
            var calls = 0;
            var assembly = new TestAssembly(() => { calls++; return []; });
            MarkerScanner<MarkerFixtures.ScanAttribute>.Scan([assembly], null!);
            MarkerScanner<MarkerFixtures.ScanAttribute>.Scan(null!, new Recorder());
            MarkerScanner<MarkerFixtures.ScanAttribute>.Scan([], new Recorder());
            Assert.Equal(0, calls);
        }

        [Fact]
        public void Scanner_keeps_input_order_and_repeats_callbacks_without_implicit_registration()
        {
            var recorder = new Recorder();
            var a = AssemblyOf(typeof(MarkerFixtures.One));
            var b = AssemblyOf(typeof(MarkerFixtures.Two));
            MarkerScanner<MarkerFixtures.ScanAttribute>.Scan([null!, b, a, b], recorder);
            Assert.Equal(new[] { "two", "one", "two" }, recorder.Events.Select(e => e.Token));
            Assert.Empty(recorder.Types);
        }

        [Fact]
        public void Scanner_skips_abstract_interface_null_and_inherited_attributes_but_accepts_open_generic()
        {
            var recorder = new Recorder();
            MarkerScanner<MarkerFixtures.ScanAttribute>.Scan([AssemblyOf(null!, typeof(MarkerFixtures.Abstract),
                typeof(MarkerFixtures.IContract), typeof(MarkerFixtures.InheritedOnly), typeof(MarkerFixtures.Open<>))], recorder);
            var seen = Assert.Single(recorder.Events);
            Assert.Equal(typeof(MarkerFixtures.Open<>), seen.Type);
            Assert.Equal("open", seen.Token);
        }

        [Fact]
        public void Scanner_calls_all_direct_attributes_and_recovers_partial_type_load()
        {
            var recorder = new Recorder();
            var partial = new TestAssembly(() => throw new ReflectionTypeLoadException(
                [typeof(MarkerFixtures.Multiple), null, typeof(MarkerFixtures.Two)], [new TypeLoadException("部分不可用")]));
            MarkerScanner<MarkerFixtures.ScanAttribute>.Scan([partial], recorder);
            Assert.Equal(3, recorder.Events.Count);
            Assert.Equal(new[] { "first", "second" }, recorder.Events.Take(2).Select(e => e.Token).Order().ToArray());
            Assert.Equal("two", recorder.Events[2].Token);
        }

        [Fact]
        public void Scanner_propagates_other_load_errors_and_retains_prior_callbacks()
        {
            var recorder = new Recorder();
            var failure = new InvalidOperationException("无法枚举");
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
                MarkerScanner<MarkerFixtures.ScanAttribute>.Scan([AssemblyOf(typeof(MarkerFixtures.One)),
                    new TestAssembly(() => throw failure), AssemblyOf(typeof(MarkerFixtures.Two))], recorder)));
            Assert.Equal("one", Assert.Single(recorder.Events).Token);
        }

        [Fact]
        public void Scanner_propagates_callback_error_and_stops_after_partial_side_effects()
        {
            var recorder = new Recorder();
            var failure = new InvalidOperationException("扫描回调失败");
            recorder.Callback = (_, _) => throw failure;
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
                MarkerScanner<MarkerFixtures.ScanAttribute>.Scan([AssemblyOf(typeof(MarkerFixtures.One), typeof(MarkerFixtures.Two))], recorder)));
            Assert.Equal("one", Assert.Single(recorder.Events).Token);
        }

        [Fact]
        public void Scanner_does_not_isolate_attribute_construction_errors()
        {
            var recorder = new Recorder();
            var error = Assert.Throws<InvalidOperationException>(() => MarkerScanner<BrokenAttribute>.Scan(
                [AssemblyOf(typeof(BrokenTarget))], recorder));
            Assert.Equal("特性构造失败", error.Message);
            Assert.Empty(recorder.Events);
        }

        [Fact]
        public void Namespace_scanner_ignores_case_uses_text_prefix_and_only_first_attribute()
        {
            var recorder = new Recorder();
            recorder.ScanByNamespace<MarkerFixtures.ScanAttribute>("abilitykit.core.tests.markerfixt");
            Assert.Equal(4, recorder.Events.Count);
            Assert.Single(recorder.Events, e => e.Type == typeof(MarkerFixtures.Multiple));
            Assert.DoesNotContain(recorder.Events, e => e.Type == typeof(MarkerFixtures.InheritedOnly));
            Assert.Contains(recorder.Events, e => e.Type == typeof(MarkerFixtures.Open<>));
        }

        [Fact]
        public void Namespace_scanner_treats_empty_first_prefix_as_whole_operation_no_op()
        {
            var recorder = new Recorder();
            recorder.ScanByNamespace<MarkerFixtures.ScanAttribute>((string[])null!);
            recorder.ScanByNamespace<MarkerFixtures.ScanAttribute>(Array.Empty<string>());
            recorder.ScanByNamespace<MarkerFixtures.ScanAttribute>(["", "AbilityKit.Core.Tests.MarkerFixtures"]);
            recorder.ScanByNamespace<MarkerFixtures.ScanAttribute>((string)null!);
            Assert.Empty(recorder.Events);
        }

        [Fact]
        public void Namespace_scanner_does_not_prevalidate_registry()
        {
            Assert.Throws<NullReferenceException>(() => MarkerScannerExtensions.ScanByNamespace<MarkerFixtures.ScanAttribute>(
                null!, "AbilityKit.Core.Tests.MarkerFixtures"));
        }

        [Fact]
        public void Loaded_assembly_name_helpers_use_case_insensitive_matching_and_exact_exclusions()
        {
            var name = typeof(MarkerCompatibilityContractTests).Assembly.GetName().Name!;
            Assert.Contains(typeof(MarkerCompatibilityContractTests).Assembly, MarkerScannerExtensions.GetAssembliesByName(name.ToLowerInvariant()));
            Assert.Contains(typeof(MarkerCompatibilityContractTests).Assembly, MarkerScannerExtensions.GetAssembliesByName(""));
            Assert.Throws<ArgumentNullException>(() => MarkerScannerExtensions.GetAssembliesByName(null!));
            var recorder = new Recorder();
            recorder.ScanExcluding<MarkerFixtures.ScanAttribute>(name.ToLowerInvariant());
            Assert.Empty(recorder.Events);
            recorder.ScanExcluding<MarkerFixtures.ScanAttribute>(name + ".dll");
            Assert.Equal(5, recorder.Events.Count);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Loaded_assembly_scan_system_filter_preserves_user_assembly(bool excludeSystem)
        {
            var recorder = new Recorder();
            recorder.ScanAllExcludingSystem<MarkerFixtures.ScanAttribute>(excludeSystem);
            Assert.Equal(5, recorder.Events.Count);
        }

        [Fact]
        public void Reference_scanner_null_and_negative_depth_do_not_visit_start()
        {
            var recorder = new Recorder();
            var reads = 0;
            var assembly = new TestAssembly(() => { reads++; return [typeof(MarkerFixtures.One)]; });
            recorder.ScanWithReferences<MarkerFixtures.ScanAttribute>(null!);
            recorder.ScanWithReferences<MarkerFixtures.ScanAttribute>(assembly, -1);
            Assert.Equal(0, reads);
            Assert.Empty(recorder.Events);
        }

        [Fact]
        public void Reference_scanner_isolates_reference_enumeration_and_load_failure_but_not_scan_failure()
        {
            var recorder = new Recorder();
            var assembly = AssemblyOf(typeof(MarkerFixtures.One));
            assembly.References = () => throw new InvalidOperationException("引用不可用");
            recorder.ScanWithReferences<MarkerFixtures.ScanAttribute>(assembly, 0);
            assembly.References = () => [new AssemblyName("AbilityKit.MissingMarkerDependency_490d800d")];
            recorder.ScanWithReferences<MarkerFixtures.ScanAttribute>(assembly, 1);
            Assert.Equal(2, recorder.Events.Count);
            var failure = new InvalidOperationException("扫描失败");
            var broken = new TestAssembly(() => throw failure);
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
                recorder.ScanWithReferences<MarkerFixtures.ScanAttribute>(broken, 0)));
        }

        [Fact]
        public void Reference_scanner_visits_start_before_loaded_reference_and_deduplicates_simple_name()
        {
            var recorder = new Recorder();
            var assembly = AssemblyOf(typeof(MarkerFixtures.One));
            assembly.References = () => [typeof(First).Assembly.GetName(), typeof(First).Assembly.GetName()];
            recorder.ScanWithReferences<MarkerFixtures.ScanAttribute>(assembly, 1);
            Assert.Equal(6, recorder.Events.Count);
            Assert.Equal("one", recorder.Events[0].Token);
            recorder.Events.Clear();
            assembly.Name = typeof(First).Assembly.GetName().Name!;
            recorder.ScanWithReferences<MarkerFixtures.ScanAttribute>(assembly, 1);
            Assert.Single(recorder.Events);
        }

        [Fact]
        public void System_validates_registration_and_deduplicates_by_reference_and_delegate_equality()
        {
            Assert.Throws<ArgumentNullException>(() => MarkerSystem.Register(null!, new Recorder()));
            Assert.Throws<ArgumentNullException>(() => MarkerSystem.Register(typeof(EmptyAttribute), null!));
            Assert.Throws<ArgumentException>(() => MarkerSystem.Register(typeof(string), new Recorder()));
            var recorder = new Recorder();
            var target = new FilterTarget();
            MarkerSystem.Register(typeof(EmptyAttribute), recorder, new Func<Assembly, bool>(target.Accept));
            MarkerSystem.Register(typeof(EmptyAttribute), recorder, new Func<Assembly, bool>(target.Accept));
            Assert.Equal(1, MarkerSystem.RegistrationCount);
            MarkerSystem.Register(typeof(EmptyAttribute), new Recorder(), target.Accept);
            MarkerSystem.Register(typeof(EmptyAttribute), recorder);
            Assert.Equal(3, MarkerSystem.RegistrationCount);
            Assert.Same(recorder, MarkerSystem.GetRegistry<Recorder>());
            Assert.Null(MarkerSystem.GetRegistry<MarkerRegistry<EmptyAttribute>>());
        }

        [Fact]
        public void System_counts_live_registry_counts_per_registration_and_reset_preserves_external_state()
        {
            var registry = Keyed();
            registry.Register("one", typeof(First));
            registry.Register("two", typeof(Second));
            registry.Register(typeof(int));
            MarkerSystem.Register(typeof(EmptyAttribute), registry);
            MarkerSystem.Register(typeof(MarkerFixtures.ScanAttribute), registry);
            var snapshot = MarkerSystem.GetRegistrations();
            Assert.Equal(4, MarkerSystem.TotalScannedTypes);
            registry.Register("third", typeof(First));
            Assert.Equal(6, MarkerSystem.TotalScannedTypes);
            MarkerSystem.Reset();
            Assert.Equal(0, MarkerSystem.TotalScannedTypes);
            Assert.Equal(2, snapshot.Count);
            Assert.Same(registry, snapshot[0].Registry);
            Assert.Equal(3, registry.Count);
        }

        [Fact]
        public void System_nonempty_scan_without_registrations_initializes_but_empty_scan_preserves_state()
        {
            MarkerSystem.ScanAll([]);
            Assert.False(MarkerSystem.IsInitialized);
            MarkerSystem.ScanAll([AssemblyOf()]);
            Assert.True(MarkerSystem.IsInitialized);
            MarkerSystem.ScanAll(null!);
            MarkerSystem.ScanAll([]);
            Assert.True(MarkerSystem.IsInitialized);
        }

        [Fact]
        public void System_filters_before_scanning_and_preserves_filter_error_without_wrapping()
        {
            var recorder = new Recorder();
            var failure = new InvalidOperationException("过滤失败");
            var calls = 0;
            MarkerSystem.Register(typeof(MarkerFixtures.ScanAttribute), recorder, _ =>
            {
                calls++;
                if (calls == 2) throw failure;
                return true;
            });
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => MarkerSystem.ScanAll(
                [AssemblyOf(typeof(MarkerFixtures.One)), AssemblyOf(typeof(MarkerFixtures.Two))])));
            Assert.Equal(2, calls);
            Assert.Empty(recorder.Events);
            Assert.False(MarkerSystem.IsInitialized);
        }

        [Fact]
        public void System_reflection_wraps_callback_failure_and_keeps_prior_registration_effects()
        {
            var recorder = new Recorder();
            var failure = new InvalidOperationException("被包装的扫描失败");
            recorder.Callback = (type, _) => { recorder.Register(type); throw failure; };
            MarkerSystem.Register(typeof(MarkerFixtures.ScanAttribute), recorder);
            var error = Assert.Throws<TargetInvocationException>(() => MarkerSystem.ScanAll([AssemblyOf(typeof(MarkerFixtures.One))]));
            Assert.Same(failure, error.InnerException);
            Assert.Single(recorder.Types);
            Assert.False(MarkerSystem.IsInitialized);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void System_callback_registration_or_reset_changes_version_and_invalidates_outer_completion(bool reset)
        {
            var recorder = new Recorder();
            var next = new Recorder();
            recorder.Callback = (_, _) =>
            {
                if (reset) MarkerSystem.Reset();
                else MarkerSystem.Register(typeof(MarkerFixtures.ScanAttribute), next);
            };
            MarkerSystem.Register(typeof(MarkerFixtures.ScanAttribute), recorder);
            MarkerSystem.ScanAll([AssemblyOf(typeof(MarkerFixtures.One), typeof(MarkerFixtures.Two))]);
            Assert.Equal(2, recorder.Events.Count);
            Assert.Empty(next.Events);
            Assert.False(MarkerSystem.IsInitialized);
            Assert.Equal(reset ? 0 : 2, MarkerSystem.RegistrationCount);
        }

        [Fact]
        public void System_same_thread_reentry_is_allowed_and_inner_status_can_survive_outer_failure()
        {
            var recorder = new Recorder();
            var failure = new InvalidOperationException("外层失败");
            recorder.Callback = (_, _) =>
            {
                MarkerSystem.ScanAll([AssemblyOf()]);
                throw failure;
            };
            MarkerSystem.Register(typeof(MarkerFixtures.ScanAttribute), recorder);
            var error = Assert.Throws<TargetInvocationException>(() => MarkerSystem.ScanAll([AssemblyOf(typeof(MarkerFixtures.One))]));
            Assert.Same(failure, error.InnerException);
            Assert.True(MarkerSystem.IsInitialized);
        }

        [Fact]
        public void Registration_is_independent_and_filter_receives_null_array_elements()
        {
            var recorder = new Recorder();
            var filtered = new List<Assembly?>();
            var registration = new MarkerRegistration(typeof(MarkerFixtures.ScanAttribute), recorder, assembly =>
            {
                filtered.Add(assembly);
                return assembly != null;
            });
            registration.Scan(null!);
            registration.Scan([]);
            var source = AssemblyOf(typeof(MarkerFixtures.One));
            registration.Scan([null!, source]);
            Assert.Equal(new Assembly?[] { null, source }, filtered);
            Assert.Single(recorder.Events);
            Assert.Equal(0, MarkerSystem.RegistrationCount);
            Assert.Same(recorder, registration.Registry);
            Assert.Equal(typeof(MarkerFixtures.ScanAttribute), registration.AttributeType);
        }

        [Fact]
        public void Instance_bootstrappers_register_during_base_construction_without_scanning()
        {
            var registry = new Recorder();
            var bootstrap = new InstanceBootstrap(registry);
            Assert.Same(registry, bootstrap.ExposedRegistry);
            Assert.False(bootstrap.WasConstructedWhenAutoRead);
            Assert.False(bootstrap.WasConstructedWhenFilterRead);
            Assert.Equal(1, MarkerSystem.RegistrationCount);
            Assert.Empty(registry.Events);
            Assert.False(MarkerSystem.IsInitialized);
            Assert.Throws<ArgumentNullException>(() => new InstanceBootstrap(null!));
            _ = new DisabledBootstrap(registry);
            Assert.Equal(1, MarkerSystem.RegistrationCount);
        }

        [Fact]
        public void Keyed_bootstrapper_registers_in_instance_construction_and_supports_opt_out()
        {
            var registry = Keyed();
            var enabled = new KeyedBootstrap(registry);
            Assert.Same(registry, enabled.ExposedRegistry);
            Assert.False(enabled.WasConstructedWhenAutoRead);
            Assert.Equal(1, MarkerSystem.RegistrationCount);
            _ = new DisabledKeyedBootstrap(registry);
            Assert.Equal(1, MarkerSystem.RegistrationCount);
            Assert.Throws<ArgumentNullException>(() => new KeyedBootstrap(null!));
        }

        [Fact]
        public void Static_bootstrapper_runs_once_and_reset_does_not_make_it_register_again()
        {
            StaticBootstrap.EnsureInitialized();
            Assert.Equal(1, MarkerSystem.RegistrationCount);
            Assert.False(MarkerSystem.IsInitialized);
            StaticBootstrap.EnsureInitialized();
            Assert.Equal(1, MarkerSystem.RegistrationCount);
            MarkerSystem.Reset();
            StaticBootstrap.EnsureInitialized();
            Assert.Equal(0, MarkerSystem.RegistrationCount);
        }

        [Fact]
        public void Static_bootstrapper_override_can_skip_global_registration()
        {
            NoRegistrationBootstrap.EnsureInitialized();
            Assert.Equal(0, MarkerSystem.RegistrationCount);
        }

        [Fact]
        public void Static_bootstrapper_failure_is_cached_and_does_not_rollback_side_effects()
        {
            Assert.Throws<TypeInitializationException>(() => FailingStaticBootstrap.EnsureInitialized());
            Assert.Equal(1, MarkerSystem.RegistrationCount);
            MarkerSystem.Reset();
            Assert.Throws<TypeInitializationException>(() => FailingStaticBootstrap.EnsureInitialized());
            Assert.Equal(0, MarkerSystem.RegistrationCount);
        }

        private static MarkerRegistry<EmptyAttribute> Plain()
        {
            var registry = new MarkerRegistry<EmptyAttribute>();
            registry.Register(typeof(First));
            return registry;
        }

        private static KeyedMarkerRegistry<string, EmptyAttribute> Keyed() => new();
        private static TestAssembly AssemblyOf(params Type[] types) => new(() => types);
        private sealed class EmptyAttribute : MarkerAttribute { }
        private abstract class Abstract { }
        private sealed class First { }
        private sealed class Second { }
        public sealed class Creatable { }
        public sealed class NoDefaultConstructor { public NoDefaultConstructor(int _) { } }
        public sealed class ThrowingConstructor
        {
            public ThrowingConstructor() => throw new InvalidOperationException("构造失败");
        }

        private sealed class BrokenAttribute : MarkerAttribute
        {
            public BrokenAttribute() => throw new InvalidOperationException("特性构造失败");
        }
        [Broken]
        private sealed class BrokenTarget { }

        internal sealed class Recorder : IMarkerRegistry
        {
            public List<(Type Type, string Token)> Events { get; } = [];
            public List<Type> Types { get; } = [];
            public Action<Type, string>? Callback { get; set; }
            public int Count => Types.Count;
            public void Register(Type implType) => Types.Add(implType);
        }

        private sealed class TestAssembly(Func<Type[]> getTypes) : Assembly
        {
            public string Name { get; set; } = "CoreMarkerControlledAssembly";
            public Func<AssemblyName[]> References { get; set; } = () => [];
            public override Type[] GetTypes() => getTypes();
            public override AssemblyName GetName() => new(Name);
            public override AssemblyName[] GetReferencedAssemblies() => References();
        }

        private sealed class FilterTarget
        {
            public bool Accept(Assembly _) => true;
        }

        private sealed class InstanceBootstrap : MarkerBootstrapper<EmptyAttribute, Recorder>
        {
            private bool _constructed;
            public bool WasConstructedWhenAutoRead { get; private set; }
            public bool WasConstructedWhenFilterRead { get; private set; }
            public Recorder ExposedRegistry => Registry;
            protected override bool AutoRegister { get { WasConstructedWhenAutoRead = _constructed; return true; } }
            protected override Func<Assembly, bool>? AssemblyFilter
            {
                get { WasConstructedWhenFilterRead = _constructed; return null; }
            }
            public InstanceBootstrap(Recorder registry) : base(registry) => _constructed = true;
        }

        private sealed class DisabledBootstrap(Recorder registry) : MarkerBootstrapper<EmptyAttribute, Recorder>(registry)
        {
            protected override bool AutoRegister => false;
            protected override Func<Assembly, bool>? AssemblyFilter => throw new InvalidOperationException("不应读取");
        }

        private sealed class KeyedBootstrap : KeyedMarkerBootstrapper<string, EmptyAttribute, KeyedMarkerRegistry<string, EmptyAttribute>>
        {
            private bool _constructed;
            public bool WasConstructedWhenAutoRead { get; private set; }
            public KeyedMarkerRegistry<string, EmptyAttribute> ExposedRegistry => Registry;
            protected override bool AutoRegister { get { WasConstructedWhenAutoRead = _constructed; return true; } }
            public KeyedBootstrap(KeyedMarkerRegistry<string, EmptyAttribute> registry) : base(registry) => _constructed = true;
        }

        private sealed class DisabledKeyedBootstrap(KeyedMarkerRegistry<string, EmptyAttribute> registry)
            : KeyedMarkerBootstrapper<string, EmptyAttribute, KeyedMarkerRegistry<string, EmptyAttribute>>(registry)
        {
            protected override bool AutoRegister => false;
            protected override Func<Assembly, bool>? AssemblyFilter => throw new InvalidOperationException("不应读取");
        }

        private sealed class StaticBootstrap : StaticMarkerBootstrapper<StaticBootstrap, EmptyAttribute, Recorder>
        {
            public StaticBootstrap() { }
            protected override Recorder CreateRegistry() => new();
        }

        private sealed class NoRegistrationBootstrap : StaticMarkerBootstrapper<NoRegistrationBootstrap, EmptyAttribute, Recorder>
        {
            public NoRegistrationBootstrap() { }
            protected override Recorder CreateRegistry() => throw new InvalidOperationException("不应执行默认注册");
            protected override void Register() { }
        }

        private sealed class FailingStaticBootstrap : StaticMarkerBootstrapper<FailingStaticBootstrap, EmptyAttribute, Recorder>
        {
            public FailingStaticBootstrap() { }
            protected override Recorder CreateRegistry() => new();
            protected override void Register()
            {
                base.Register();
                throw new InvalidOperationException("静态初始化失败");
            }
        }
    }
}

namespace AbilityKit.Core.Tests.MarkerFixtures
{
    /*
     * 这些类型供真实应用域扫描使用，特性不保存全局状态，记录由本轮注册表持有。
     */
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
    internal sealed class ScanAttribute(string token) : MarkerAttribute
    {
        public override void OnScanned(Type implType, IMarkerRegistry registry)
        {
            var recorder = (MarkerCompatibilityContractTests.Recorder)registry;
            recorder.Events.Add((implType, token));
            recorder.Callback?.Invoke(implType, token);
        }
    }

    [Scan("one")]
    internal class One { }
    [Scan("two")]
    internal sealed class Two { }
    [Scan("first"), Scan("second")]
    internal sealed class Multiple { }
    [Scan("abstract")]
    internal abstract class Abstract { }
    [Scan("interface")]
    internal interface IContract { }
    internal sealed class InheritedOnly : One { }
    [Scan("open")]
    internal sealed class Open<T> { }
}

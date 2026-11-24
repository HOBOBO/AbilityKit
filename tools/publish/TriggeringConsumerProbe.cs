using System;
using System.IO;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Example;
using AbilityKit.Triggering.Runtime.Plan;
using MemoryPack;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
#endif

public static class TriggeringConsumerProbe
{
    private const string Success = "Triggering consumer passed: mainline, Timed, WorldId";

    private static void Run()
    {
        FormalTriggeringMainlineExample.RunOnce();

        var actions = new ActionRegistry();
        var actionId = new ActionId(1);
        var executed = 0;
        actions.Register<NamedAction0<object, object, object>>(actionId, (_, _, _) => executed++, isDeterministic: true);
        var executable = TriggerPlanExecutableDsl.Timed(new ActionCallTriggerPlanExecutable(new ActionCallPlan(actionId)), 10f);
        var scheduler = new TriggerExecutionScheduler(retainFinishedEntries: true);
        var context = new ExecCtx<object>(new object(), new EventBus(), new FunctionRegistry(), actions,
            blackboards: null, payloads: null, idNames: null, numericDomains: null, numericFunctions: null,
            policy: new ExecPolicy(requireDeterministic: true), control: new ExecutionControl(), executionScheduler: scheduler);
        if (!executable.Execute(new object(), in context).IsSuccess || scheduler.ActiveCount != 1 || executed != 0)
            throw new InvalidOperationException("Expected one waiting Timed node.");
        scheduler.Tick(9f);
        if (executed != 0)
            throw new InvalidOperationException("Timed node executed early.");
        scheduler.Tick(1f);
        if (executed != 1 || scheduler.ActiveCount != 0)
            throw new InvalidOperationException("Timed node did not execute at its deadline.");
        scheduler.Tick(10f);
        if (executed != 1)
            throw new InvalidOperationException("Timed node executed more than once.");

        var world = new WorldId("dependency-probe");
        if (!world.Equals(MemoryPackSerializer.Deserialize<WorldId>(MemoryPackSerializer.Serialize(world))))
            throw new InvalidOperationException("WorldId did not round-trip.");
    }

#if UNITY_EDITOR
    public static void RunEditor()
    {
        Run();
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TriggerExecutionScheduler).Assembly);
        if (package == null || package.name != "com.abilitykit.triggering" || package.source != PackageSource.Registry)
            throw new InvalidOperationException("Triggering was not loaded from a UPM registry.");
        File.WriteAllLines("consumer-result.txt", new[] { Success, package.resolvedPath });
    }

    public static void BuildMono() => Build(ScriptingImplementation.Mono2x);

    public static void BuildIL2CPP() => Build(ScriptingImplementation.IL2CPP);

    private static void Build(ScriptingImplementation backend)
    {
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, backend);
        const string scenePath = "Assets/Consumer.unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (!EditorSceneManager.SaveScene(scene, scenePath))
            throw new InvalidOperationException("Could not save the probe scene.");
        var result = BuildPipeline.BuildPlayer(new[] { scenePath }, "Player/TriggeringConsumer.exe",
            BuildTarget.StandaloneWindows64, BuildOptions.None);
        if (result.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException($"Windows Player build failed: {result.summary.result}");
    }
#else
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RunPlayer()
    {
        try
        {
            Run();
            Debug.Log(Success);
            Application.Quit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            Application.Quit(1);
        }
    }
#endif
}

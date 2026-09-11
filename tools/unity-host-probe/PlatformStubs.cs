// Stubs for the editor-platform surface the sample host touches.
//
// The real contracts were already validated the hard way: Unity compiled
// SamplesEditorModule.cs successfully. Pulling the real platform sources in here
// instead would drag their whole Unity dependency surface along (EditorPrefs,
// ScriptableSingleton, ...) to re-prove something already proven.
//
// What this probe is FOR: catching defects that involve types the compiler CAN
// resolve - our own code and the sample package. Those are exactly the ones the
// "compile without Unity" trick silently swallowed (`string[].Count` is CS0019
// and went unreported).
#pragma warning disable CS0067, CS0649

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.Editor.Platform.Core
{
    public sealed class EditorModuleDescriptor
    {
        public EditorModuleDescriptor(string id, string displayNameKey, int order = 0) { }
    }

    public interface IEditorModule
    {
        EditorModuleDescriptor Descriptor { get; }
        void OnRegister(IEditorPlatformContext context);
        void OnUnregister();
    }

    public interface IEditorContribution
    {
        string Id { get; }
        int Order { get; }
    }

    public sealed class EditorPanelContribution : IEditorContribution
    {
        public EditorPanelContribution(
            string id,
            string titleKey,
            Func<VisualElement> createVisualElement = null,
            Action<Rect> drawImGui = null,
            int order = 0)
        { }

        public string Id => string.Empty;
        public int Order => 0;
    }

    public sealed class EditorContributionRegistry<TContribution>
        where TContribution : class, IEditorContribution
    {
        public IDisposable Register(TContribution contribution) => null;
    }

    public interface IEditorPlatformContext
    {
        EditorContributionRegistry<EditorPanelContribution> Panels { get; }
    }

    public sealed class EditorModuleRegistry
    {
        public IDisposable Register(IEditorModule module) => null;
    }

    public static class AbilityKitEditorPlatform
    {
        public static EditorModuleRegistry Modules => null;
        public static Localization.IEditorLocalization Localization => null;
    }
}

namespace AbilityKit.Editor.Platform.Localization
{
    public interface IEditorLocalization
    {
        string Get(string key);
        IDisposable RegisterSource(IEditorLocalizationSource source);
    }

    public interface IEditorLocalizationSource
    {
        string ModuleId { get; }
        bool TryGet(string language, string key, out string value);
    }

    public sealed class DictionaryEditorLocalizationSource : IEditorLocalizationSource
    {
        public DictionaryEditorLocalizationSource(
            string moduleId,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> tables)
        { }

        public string ModuleId => string.Empty;
        public bool TryGet(string language, string key, out string value)
        {
            value = null;
            return false;
        }
    }
}

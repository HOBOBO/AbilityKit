#if UNITY_EDITOR
using System;
using System.IO;
using AbilityKit.Ability.Config.Authoring;
using UnityEditor;

namespace AbilityKit.Ability.Editor.Utilities
{
    /// <summary>
    /// Public package boundary for importing an authored trigger module into a workspace project.
    /// Business packages can own their sources and synchronization commands without depending on internal editor services.
    /// </summary>
    public static class TriggerAuthoringModuleSourceImportApi
    {
        public static bool Import(
            TriggerAuthoringModuleAsset module,
            TriggerAuthoringProjectAsset project,
            string sourcePath,
            TriggerAuthoringModuleSourceImportOptions options,
            out string message)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (string.IsNullOrWhiteSpace(sourcePath)) throw new ArgumentException("Source path is required.", nameof(sourcePath));

            options ??= new TriggerAuthoringModuleSourceImportOptions();
            if (!string.IsNullOrWhiteSpace(options.ExpectedModuleId))
            {
                try
                {
                    var document = TriggerAuthoringSourceCodec.ReadFile(Path.GetFullPath(sourcePath));
                    if (!string.Equals(document.Module?.ModuleId, options.ExpectedModuleId, StringComparison.Ordinal))
                    {
                        message = $"Source module id '{document.Module?.ModuleId ?? string.Empty}' does not match expected id '{options.ExpectedModuleId}'.";
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    message = ex.Message;
                    return false;
                }
            }

            var previousProject = module.Project;
            TriggerAuthoringProjectMembership.Assign(module, project);
            var result = TriggerAuthoringSourceSync.Import(module, sourcePath, options.Force);
            if (!result.Success)
            {
                TriggerAuthoringProjectMembership.Assign(module, previousProject);
                message = result.Message;
                return false;
            }

            if (options.ConfigurePackageMetadata)
            {
                var metadata = new TriggerAuthoringPackageMetadata();
                metadata.SetIdentity(options.DomainId, options.ContentKey);
                metadata.SetOwner(options.Owner);
                metadata.SetTags(options.Tags);
                module.SetPackageMetadata(metadata);
            }

            if (!string.IsNullOrWhiteSpace(options.AssetName)) module.name = options.AssetName.Trim();
            EditorUtility.SetDirty(module);
            EditorUtility.SetDirty(project);

            if (options.ValidateProject)
            {
                var validation = TriggerAuthoringProjectValidator.Validate(project);
                if (!validation.Success)
                {
                    message = validation.BuildMessage();
                    return false;
                }
            }

            message = string.Empty;
            return true;
        }
    }

    public sealed class TriggerAuthoringModuleSourceImportOptions
    {
        public bool Force { get; set; } = true;
        public bool ValidateProject { get; set; } = true;
        public string ExpectedModuleId { get; set; } = string.Empty;
        public bool ConfigurePackageMetadata { get; set; }
        public string DomainId { get; set; } = string.Empty;
        public string ContentKey { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string[] Tags { get; set; } = Array.Empty<string>();
        public string AssetName { get; set; } = string.Empty;
    }
}
#endif

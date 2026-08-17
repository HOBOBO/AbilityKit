#nullable enable

using System;
using AbilityKit.Demo.Common.Gameplay;
using UnityEngine;

namespace AbilityKit.Demo.Common.Composition
{
    [CreateAssetMenu(
        fileName = "DemoGameplayProfile",
        menuName = "AbilityKit/Demo/Gameplay Profile")]
    public sealed class DemoGameplayProfileSO : ScriptableObject
    {
        [SerializeField] private string profileId = string.Empty;
        [SerializeField] private DemoGameplayId gameplay;
        [SerializeField] private DemoLaunchMode mode;
        [SerializeField] private GameObject? rootPrefab;

        public string ProfileId => profileId?.Trim() ?? string.Empty;
        public DemoGameplayId Gameplay => gameplay;
        public DemoLaunchMode Mode => mode;
        public GameObject? RootPrefab => rootPrefab;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(ProfileId))
            {
                error = $"Gameplay profile '{name}' has no profile id.";
                return false;
            }

            if (!Enum.IsDefined(typeof(DemoGameplayId), gameplay)
                || !Enum.IsDefined(typeof(DemoLaunchMode), mode))
            {
                error = $"Gameplay profile '{ProfileId}' has an invalid gameplay or launch mode.";
                return false;
            }

            if (rootPrefab == null)
            {
                error = $"Gameplay profile '{ProfileId}' has no root prefab.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}

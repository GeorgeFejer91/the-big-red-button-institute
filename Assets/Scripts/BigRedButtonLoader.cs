using System;
using System.IO;
using GLTFast;
using GLTFast.Logging;
using UnityEngine;

namespace TheBigRedButtonInstitute
{
    public class BigRedButtonLoader : MonoBehaviour
    {
        [SerializeField] BigRedButtonModelProfile modelProfile = BigRedButtonModelProfile.Classic;
        [SerializeField] bool useModelProfilePath = true;
        [SerializeField] string relativePath = BigRedButtonModelProfiles.ClassicStreamingRelativePath;

        bool _loaded;

        async void Start()
        {
            if (_loaded)
            {
                return;
            }

            try
            {
                var import = new GltfImport(logger: new ConsoleLogger());
                var assetUrl = GetAssetUrl(ResolveRelativePath());
                var success = await import.Load(assetUrl);

                if (!success)
                {
                    Debug.LogError($"Failed to load big red button model from {assetUrl}", this);
                    return;
                }

                ClearChildren();
                success = await import.InstantiateMainSceneAsync(transform);

                if (!success)
                {
                    Debug.LogError("Failed to instantiate big red button scene", this);
                    return;
                }

                _loaded = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        static string GetAssetUrl(string relativePath)
        {
            var combinedPath = Path.Combine(Application.streamingAssetsPath, relativePath);
            var normalizedPath = combinedPath.Replace("\\", "/");
            return normalizedPath.Contains("://", StringComparison.Ordinal)
                ? normalizedPath
                : new Uri(normalizedPath).AbsoluteUri;
        }

        string ResolveRelativePath()
        {
            if (!useModelProfilePath && !string.IsNullOrWhiteSpace(relativePath))
            {
                return relativePath;
            }

            relativePath = BigRedButtonModelProfiles.GetStreamingRelativePath(modelProfile);
            return relativePath;
        }

        public void ConfigureModelProfile(BigRedButtonModelProfile profile)
        {
            modelProfile = BigRedButtonModelProfiles.Normalize(profile);
            useModelProfilePath = true;
            relativePath = BigRedButtonModelProfiles.GetStreamingRelativePath(modelProfile);
        }

        void ClearChildren()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }
    }
}

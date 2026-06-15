using System;

namespace TheBigRedButtonInstitute
{
    public enum BigRedButtonModelProfile
    {
        Classic = 0,
        NativeQuestStudy = 1
    }

    public static class BigRedButtonModelProfiles
    {
        public const string ClassicEditorAssetPath = "Assets/Models/BigRedButton.glb";
        public const string NativeQuestStudyEditorAssetPath = "Assets/Models/NativeQuestStudyBigRedButton.glb";
        public const string ClassicStreamingRelativePath = "Models/BigRedButton.glb";
        public const string NativeQuestStudyStreamingRelativePath = "Models/NativeQuestStudyBigRedButton.glb";

        public static string GetEditorAssetPath(BigRedButtonModelProfile profile)
        {
            return Normalize(profile) == BigRedButtonModelProfile.NativeQuestStudy
                ? NativeQuestStudyEditorAssetPath
                : ClassicEditorAssetPath;
        }

        public static string GetStreamingRelativePath(BigRedButtonModelProfile profile)
        {
            return Normalize(profile) == BigRedButtonModelProfile.NativeQuestStudy
                ? NativeQuestStudyStreamingRelativePath
                : ClassicStreamingRelativePath;
        }

        public static string GetDisplayName(BigRedButtonModelProfile profile)
        {
            return Normalize(profile) == BigRedButtonModelProfile.NativeQuestStudy
                ? "Native Quest study"
                : "classic Unity";
        }

        public static BigRedButtonModelProfile Normalize(BigRedButtonModelProfile profile)
        {
            return Enum.IsDefined(typeof(BigRedButtonModelProfile), profile)
                ? profile
                : BigRedButtonModelProfile.Classic;
        }
    }
}

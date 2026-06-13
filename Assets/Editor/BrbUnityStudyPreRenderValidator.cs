using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheBigRedButtonInstitute.Editor
{
    public static class BrbUnityStudyPreRenderValidator
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const int Width = 1280;
        const int Height = 720;

        [MenuItem("Tools/Big Red Button/Run Unity Study Pre-render Validation")]
        public static void RunFromMenu()
        {
            Run();
        }

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            QuestVrSceneInstaller.InstallIntoSampleScene();
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var outputRoot = Path.GetFullPath(Path.Combine("Builds", "Android", "Validation", $"unity-study-prerenders-{stamp}"));
            Directory.CreateDirectory(outputRoot);

            var results = new List<RenderResult>();
            results.Add(RenderPreview(scene, outputRoot, "demographics-keyboard", "Participant Setup", "Name entry with pop-out spatial keyboard", true));
            results.Add(RenderPreview(scene, outputRoot, "button-session", "Button Session 1", "The 3D Big Red Button is visible and controller-contact pressing is active.", false));
            results.Add(RenderPreview(scene, outputRoot, "pictographic", "After Button Session", "Pictographic presence and redness checks render as a spatial questionnaire panel.", false));
            results.Add(RenderPreview(scene, outputRoot, "presence-ipq", "Presence Questionnaire", "IPQ history narration and questionnaire structure are present without factor/scoring labels.", false));
            results.Add(RenderPreview(scene, outputRoot, "final-extra-presses", "Final Button Interaction", "Final interaction remains a controller-contact press of the 3D Big Red Button.", false));

            var summaryPath = Path.Combine(outputRoot, "unity-study-prerender-summary.json");
            File.WriteAllText(summaryPath, BuildSummary(results), Encoding.UTF8);
            var failures = results.FindAll(result => !result.nonBlank || result.uniqueColorCount < 8);
            if (failures.Count > 0)
            {
                throw new InvalidOperationException($"Unity study pre-render validation failed. Summary: {summaryPath}");
            }

            Debug.Log($"[BRB_UNITY_PRERENDER_PASS] summary={summaryPath}");
        }

        static RenderResult RenderPreview(
            Scene scene,
            string outputRoot,
            string name,
            string title,
            string body,
            bool includeKeyboard)
        {
            var tempRoot = new GameObject($"BRB Unity PreRender {name}");
            SceneManager.MoveGameObjectToScene(tempRoot, scene);
            var cameraObject = new GameObject($"PreRender Camera {name}", typeof(Camera));
            cameraObject.transform.SetParent(tempRoot.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0f, 1.62f, -1.72f);
            camera.transform.rotation = Quaternion.identity;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 8f;
            camera.fieldOfView = 72f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.018f, 0.022f, 1f);

            var lightObject = new GameObject($"PreRender Light {name}", typeof(Light));
            lightObject.transform.SetParent(tempRoot.transform, false);
            lightObject.transform.position = new Vector3(0.2f, 2.4f, -0.9f);
            lightObject.transform.rotation = Quaternion.Euler(58f, -18f, 0f);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;

            var panel = CreatePanel(tempRoot.transform, "Study Panel", new Vector3(0f, 1.6f, -0.55f), new Vector2(900f, 520f));
            SetPanelText(
                panel.text,
                title,
                body,
                includeKeyboard
                    ? "keyboard_panel | presentation=pop_out_spatial_panel | radialReference=headset_center | orientation=faces_headset"
                    : "UNITY_VERSION | brb_unity_first_study_v1");

            if (includeKeyboard)
            {
                var keyboard = CreatePanel(tempRoot.transform, "keyboard_panel", new Vector3(-0.78f, 1.55f, -0.62f), new Vector2(590f, 360f));
                SetPanelText(
                    keyboard.text,
                    "keyboard_panel",
                    "A B C D E F\nG H I J K L\nM N O P Q R\nS T U V W X\nY Z SPACE BACK OK",
                    "nonObstructing=true | fovVisible=true");
            }

            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var renderTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                texture.Apply();

                var pngPath = Path.Combine(outputRoot, $"{name}.png");
                File.WriteAllBytes(pngPath, texture.EncodeToPNG());
                var result = InspectTexture(texture);
                result.name = name;
                result.path = pngPath;
                Debug.Log($"[BRB_UNITY_PRERENDER] name={name} path={pngPath} nonBlank={result.nonBlank} uniqueColors={result.uniqueColorCount}");
                return result;
            }
            finally
            {
                RenderTexture.active = null;
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(tempRoot);
            }
        }

        static PreviewPanel CreatePanel(Transform parent, string name, Vector3 position, Vector2 size)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(parent, false);
            canvasObject.transform.position = position;
            canvasObject.transform.rotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * 0.0012f;
            var rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = null;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.dynamicPixelsPerUnit = 1f;

            var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backgroundObject.transform.SetParent(rect, false);
            var backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            var background = backgroundObject.GetComponent<Image>();
            background.color = new Color(0.02f, 0.03f, 0.04f, 0.96f);

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(backgroundRect, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(38f, 34f);
            textRect.offsetMax = new Vector2(-38f, -34f);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = 34f;
            text.color = new Color(0.93f, 0.97f, 1f, 1f);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            return new PreviewPanel { text = text };
        }

        static void SetPanelText(TextMeshProUGUI text, string title, string body, string footer)
        {
            text.text =
                $"<size=128%><b>{EscapeRichText(title)}</b></size>\n" +
                "<size=72%><color=#9ED8FF>UNITY_VERSION | brb_unity_first_study_v1</color></size>\n\n" +
                $"{EscapeRichText(body)}\n\n" +
                $"<size=72%><color=#BFD1DA>{EscapeRichText(footer)}</color></size>";
        }

        static RenderResult InspectTexture(Texture2D texture)
        {
            var pixels = texture.GetPixels32();
            var unique = new HashSet<int>();
            long luminanceTotal = 0;
            for (var i = 0; i < pixels.Length; i += 17)
            {
                var pixel = pixels[i];
                luminanceTotal += pixel.r + pixel.g + pixel.b;
                unique.Add((pixel.r << 16) | (pixel.g << 8) | pixel.b);
            }

            var sampleCount = Math.Max(1, pixels.Length / 17);
            var averageLuminance = luminanceTotal / (double)(sampleCount * 3 * 255);
            return new RenderResult
            {
                nonBlank = averageLuminance > 0.02d,
                averageLuminance = averageLuminance,
                uniqueColorCount = unique.Count
            };
        }

        static string BuildSummary(IReadOnlyList<RenderResult> results)
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"schema\": \"brb.unity.prerender.validation.v1\",");
            builder.AppendLine("  \"appVariant\": \"UNITY_VERSION\",");
            builder.AppendLine("  \"width\": 1280,");
            builder.AppendLine("  \"height\": 720,");
            builder.AppendLine("  \"renders\": [");
            for (var i = 0; i < results.Count; i++)
            {
                var result = results[i];
                builder.Append("    {");
                builder.Append($"\"name\":\"{EscapeJson(result.name)}\",");
                builder.Append($"\"path\":\"{EscapeJson(result.path)}\",");
                builder.Append($"\"nonBlank\":{(result.nonBlank ? "true" : "false")},");
                builder.Append($"\"uniqueColorCount\":{result.uniqueColorCount},");
                builder.Append($"\"averageLuminance\":{result.averageLuminance.ToString("0.####", CultureInfo.InvariantCulture)}");
                builder.Append(i == results.Count - 1 ? "}" : "},");
                builder.AppendLine();
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        static string EscapeRichText(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        static string EscapeJson(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        struct PreviewPanel
        {
            public TextMeshProUGUI text;
        }

        struct RenderResult
        {
            public string name;
            public string path;
            public bool nonBlank;
            public int uniqueColorCount;
            public double averageLuminance;
        }
    }
}

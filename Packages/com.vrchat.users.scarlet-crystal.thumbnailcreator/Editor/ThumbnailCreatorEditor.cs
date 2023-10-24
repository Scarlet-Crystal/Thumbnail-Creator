using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEditor;

#if UNITY_POST_PROCESSING_STACK_V2
using UnityEngine.Rendering.PostProcessing;
#endif


namespace ThumbnailUtilities
{
    [CustomEditor(typeof(ThumbnailCreator))]
    class ThumbnailCreatorEditor : Editor
    {
        readonly struct QualityParameters
        {
            public readonly int renderScale;
            public readonly string blitterKeyword;
            public readonly bool allowAntialiasing;

            public QualityParameters(int renderScale, string blitterKeyword, bool allowAntialiasing)
            {
                this.renderScale = renderScale;
                this.blitterKeyword = blitterKeyword;
                this.allowAntialiasing = allowAntialiasing;
            }
        }

        private static readonly Dictionary<SSAAQuality, QualityParameters> QualityPresets = new Dictionary<SSAAQuality, QualityParameters>()
        {
            { SSAAQuality.None, new QualityParameters(1, "_SSAAx1", true) },
            { SSAAQuality.Low, new QualityParameters(6, "_SSAAx36", false) },
            { SSAAQuality.Medium, new QualityParameters(8, "_SSAAx64", false) },
            { SSAAQuality.High, new QualityParameters(12, "_SSAAx144", false) }
        };

        [MenuItem("GameObject/Thumbnail Creator", priority = 10)]
        public static void CreateThumbnailCreator(MenuCommand menuCommand)
        {
            var go = new GameObject("ThumbnailCreator", typeof(ThumbnailCreator)) { tag = "EditorOnly" };

            var cam = go.GetComponent<Camera>();
            cam.targetTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(
                "Packages/com.vrchat.users.scarlet-crystal.thumbnailcreator/Editor/ThumbnailPreview.renderTexture"
            );

#if UNITY_POST_PROCESSING_STACK_V2
            var ppl = go.AddComponent<PostProcessLayer>();
            ppl.enabled = false;
            ppl.volumeLayer = -1;
#endif

            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, $"Create {go.name}");
            Selection.activeObject = go;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var thumbnailCreator = target as ThumbnailCreator;

            if (GUILayout.Button("Render"))
            {
                string savePath = EditorUtility.SaveFilePanel("Save Thumbnail", "", "Thumbnail.png", "png");

                if (!string.IsNullOrWhiteSpace(savePath))
                {
                    try
                    {
                        Texture2D thumbnail = RenderThumbnail(thumbnailCreator);

                        File.WriteAllBytes(savePath, thumbnail.EncodeToPNG());

                        AssetDatabase.Refresh();
                    }
                    catch (Exception)
                    {
                        EditorUtility.DisplayDialog("Rendering Error", "Rendering failed, check the console for details.", "OK");
                        throw;
                    }
                }
            }
        }

        private Texture2D RenderThumbnail(ThumbnailCreator thumbnailCreator)
        {
            var selectedParams = QualityPresets[thumbnailCreator.supersampleLevel];

            RenderTexture thumbnail = new RenderTexture(
                new RenderTextureDescriptor(1200, 900, RenderTextureFormat.ARGB32)
                {
                    sRGB = true
                }
            );

            int supersampleWidth = thumbnail.width * selectedParams.renderScale;
            int supersampleHeight = thumbnail.height * selectedParams.renderScale;

            if (supersampleWidth > SystemInfo.maxTextureSize || supersampleHeight > SystemInfo.maxTextureSize)
            {
                throw new Exception("Unable to allocate supersample buffer. Try using a lower supersample level.");
            }

            RenderTexture supersampleBuffer = new RenderTexture(
                new RenderTextureDescriptor(supersampleWidth, supersampleHeight, RenderTextureFormat.ARGBHalf)
                {
                    depthBufferBits = 32,
                    msaaSamples = 8
                }
            )
            {
                filterMode = FilterMode.Point
            };

            // Debug.Log($"SupersampleBuffer size: {supersampleBuffer.width}x{supersampleBuffer.height}");

            var lastRT = RenderTexture.active;
            GameObject renderer = Instantiate(thumbnailCreator.gameObject);

            try
            {
                Camera cam = renderer.GetComponent<Camera>();

                if (!selectedParams.allowAntialiasing)
                {

#if UNITY_POST_PROCESSING_STACK_V2
                    if (renderer.TryGetComponent<PostProcessLayer>(out var postProcessLayer))
                    {
                        postProcessLayer.antialiasingMode = PostProcessLayer.Antialiasing.None;
                    }
#endif

                    cam.allowMSAA = false;
                    supersampleBuffer.antiAliasing = 1;
                }

                cam.allowDynamicResolution = false;
                cam.targetTexture = supersampleBuffer;
                cam.Render();

                var blitMat = new Material(Shader.Find("ThumbnailCreator/DownsamplingBlitter"))
                {
                    shaderKeywords = new string[] { selectedParams.blitterKeyword }
                };

                Graphics.Blit(supersampleBuffer, thumbnail, blitMat);

                Texture2D result = new Texture2D(thumbnail.width, thumbnail.height, TextureFormat.ARGB32, false);
                result.ReadPixels(new Rect(0, 0, thumbnail.width, thumbnail.height), 0, 0, false);

                return result;
            }
            finally
            {
                DestroyImmediate(renderer);
                RenderTexture.active = lastRT;

                thumbnail.Release();
                supersampleBuffer.Release();
            }
        }
    }
}
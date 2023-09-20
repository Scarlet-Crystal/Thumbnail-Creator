using System;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Experimental.Rendering;

using UnityEditor;

namespace ThumbnailUtilities
{
    [CustomEditor(typeof(ThumbnailCreator))]
    class ThumbnailCreatorEditor : Editor
    {
        //Keep the order and size of this array in sync with the SSAAQuality enum
        private static readonly Tuple<int, string, bool>[] QualityParams = new Tuple<int, string, bool>[]
        {
            //Render scale, Shaderkeyword, allow MSAA/postprocessing-based antialiasing
            Tuple.Create(1,    "_SSAAx1", true ),
            Tuple.Create(4,   "_SSAAx16", false),
            Tuple.Create(6,   "_SSAAx36", false),
            Tuple.Create(8,   "_SSAAx64", false),
            Tuple.Create(12, "_SSAAx144", false)
        };

        [MenuItem("GameObject/Thumbnail Creator", priority = 10)]
        public static void CreateThumbnailCreator(MenuCommand menuCommand)
        {
            var go = new GameObject("ThumbnailCreator", typeof(ThumbnailCreator)) { tag = "EditorOnly" };

            var ppl = go.AddComponent<PostProcessLayer>();
            ppl.enabled = false;
            ppl.volumeLayer = -1;

            var cam = go.GetComponent<Camera>();
            cam.targetTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(
                "Packages/com.vrchat.users.scarlet-crystal.thumbnailcreator/Editor/ThumbnailPreview.renderTexture"
            );


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

                        savePath = Path.GetFullPath(savePath);
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
            var selectedParams = QualityParams[(int)thumbnailCreator.supersampleLevel];

            RenderTexture thumbnail = new RenderTexture(
                new RenderTextureDescriptor(1200, 900)
                {
                    graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm,
                    sRGB = true
                }
            );

            int supersampleWidth = thumbnail.width * selectedParams.Item1;
            int supersampleHeight = thumbnail.height * selectedParams.Item1;

            if (supersampleWidth > SystemInfo.maxTextureSize || supersampleHeight > SystemInfo.maxTextureSize)
            {
                throw new Exception("Unable to allocate supersample buffer. Try using a lower supersample level.");
            }

            RenderTexture supersampleBuffer = new RenderTexture(
                new RenderTextureDescriptor(supersampleWidth, supersampleHeight)
                {
                    depthBufferBits = 32,
                    graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat,
                    msaaSamples = selectedParams.Item3 ? 8 : 1
                }
            )
            {
                filterMode = FilterMode.Point
            };

            // Debug.Log($"SupersampleBuffer size: {supersampleBuffer.width}x{supersampleBuffer.height}");

            GameObject renderer = Instantiate(thumbnailCreator.gameObject);
            var lastRT = RenderTexture.active;

            try
            {
                Camera cam = renderer.GetComponent<Camera>();

                if (!selectedParams.Item3)
                {
                    if (renderer.TryGetComponent<PostProcessLayer>(out var postProcessLayer))
                    {
                        postProcessLayer.antialiasingMode = PostProcessLayer.Antialiasing.None;
                    }

                    cam.allowMSAA = false;
                }

                cam.allowDynamicResolution = false;
                cam.targetTexture = supersampleBuffer;
                cam.Render();

                var blitMat = new Material(Shader.Find("ThumbnailCreator/DownsamplingBlitter"))
                {
                    shaderKeywords = new string[] { selectedParams.Item2 }
                };

                Graphics.Blit(supersampleBuffer, thumbnail, blitMat);

                return ReadBackRenderTexture(thumbnail);
            }
            finally
            {
                DestroyImmediate(renderer);
                RenderTexture.active = lastRT;

                thumbnail.Release();
                supersampleBuffer.Release();
            }
        }

        private Texture2D ReadBackRenderTexture(RenderTexture target)
        {
            AsyncGPUReadbackRequest readbackRequest = AsyncGPUReadback.Request(target);
            readbackRequest.WaitForCompletion();

            if (readbackRequest.hasError)
            {
                throw new Exception("GPU Readback failed.");
            }

            Texture2D r = new Texture2D(target.width, target.height, target.graphicsFormat, TextureCreationFlags.None);
            r.SetPixelData(readbackRequest.GetData<byte>(), 0);

            if (!target.sRGB)
            {
                r.SetPixels(r.GetPixels().Select(c => c.gamma).ToArray());
            }
            
            return r;
        }
    }
}
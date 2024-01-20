using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEditor;

#if UNITY_POST_PROCESSING_STACK_V2
using UnityEngine.Rendering.PostProcessing;
#endif

#if UDON
using VRC.SDK3.Components;
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
            { SSAAQuality.Low, new QualityParameters(4, "_SSAAx16", true) },
            { SSAAQuality.Medium, new QualityParameters(8, "_SSAAx64", false) },
            { SSAAQuality.High, new QualityParameters(12, "_SSAAx144", false) }
        };

        [MenuItem("GameObject/Thumbnail Creator", priority = 10)]
        public static void CreateThumbnailCreator(MenuCommand menuCommand)
        {
            var go = new GameObject("ThumbnailCreator", typeof(ThumbnailCreator)) { tag = "EditorOnly" };

            var cam = go.GetComponent<Camera>();
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;

#if UNITY_POST_PROCESSING_STACK_V2
            var ppl = go.AddComponent<PostProcessLayer>();
            ppl.enabled = false;
            ppl.volumeLayer = -1;
#endif

#if UDON
            VRCSceneDescriptor sceneDescriptor = FindObjectOfType<VRCSceneDescriptor>();

            if (sceneDescriptor != null && sceneDescriptor.ReferenceCamera != null)
            {
                if (sceneDescriptor.ReferenceCamera.TryGetComponent<Camera>(out var refCam))
                {
                    cam.CopyFrom(refCam);
                }

#if UNITY_POST_PROCESSING_STACK_V2
                if (sceneDescriptor.ReferenceCamera.TryGetComponent<PostProcessLayer>(out var refPPL))
                {
                    ppl.enabled = true;
                    ppl.volumeLayer = refPPL.volumeLayer;
                }
#endif

            }
#endif

            cam.targetTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(
                "Packages/com.vrchat.users.scarlet-crystal.thumbnailcreator/Editor/ThumbnailPreview.renderTexture"
            );

            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            GameObjectUtility.EnsureUniqueNameForSibling(go);

            Undo.RegisterCreatedObjectUndo(go, $"Create {go.name}");
            Selection.activeObject = go;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (!QualityPresets[(target as ThumbnailCreator).supersampleLevel].allowAntialiasing)
            {
                EditorGUILayout.HelpBox(
                    "MSAA and postprocessing antialiasing will be disabled when rendering at this level.",
                    MessageType.Info
                );
            }

            EditorGUILayout.BeginHorizontal();

            bool mirrorClicked = GUILayout.Button(
                new GUIContent(
                    "Mirror scene view",
                    "Configure the thumbnail camera so that it matches the scene view as closely as possible."
                )
            );

            bool alignClicked = GUILayout.Button(
                new GUIContent(
                    "Align with scene view",
                    "Copies the scene view's position and rotation to the thumbnail camera."
                )
            );

            if (mirrorClicked || alignClicked)
            {
                if (!TryAlignWithSceneView(mirrorClicked))
                {
                    EditorUtility.DisplayDialog(
                        "Missing Scene View",
                        "Can't find the last active scene view. Try opening a new scene window.",
                        "Understood"
                    );
                }
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Render"))
            {
                string savePath = EditorUtility.SaveFilePanel("Save Thumbnail", "", "Thumbnail.png", "png");

                if (!string.IsNullOrWhiteSpace(savePath))
                {
                    try
                    {
                        Texture2D thumbnail = RenderThumbnail(target as ThumbnailCreator);

                        File.WriteAllBytes(savePath, thumbnail.EncodeToPNG());

                        AssetDatabase.Refresh();
                    }
                    catch (Exception)
                    {
                        EditorUtility.DisplayDialog("Rendering Error", "Rendering failed, check the console for details.", "Understood");
                        throw;
                    }
                }
            }
        }

        private bool TryAlignWithSceneView(bool copyCamera)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;

            if (sceneView == null)
            {
                return false;
            }

            Camera sceneCamera = sceneView.camera;
            ThumbnailCreator thumbnailCreator = target as ThumbnailCreator;
            string undoName = copyCamera ? "Mirror scene view" : "Align with scene view";

            Undo.RecordObject(thumbnailCreator.transform, undoName);

            thumbnailCreator.transform.SetPositionAndRotation(
                sceneCamera.transform.position,
                sceneCamera.transform.rotation
            );

            PrefabUtility.RecordPrefabInstancePropertyModifications(thumbnailCreator.transform);

            if (copyCamera)
            {
                Camera thumbnailCamera = thumbnailCreator.GetComponent<Camera>();

                Undo.RecordObject(thumbnailCamera, undoName);

                thumbnailCamera.usePhysicalProperties = false;

                thumbnailCamera.orthographic = sceneCamera.orthographic;

                thumbnailCamera.fieldOfView = sceneCamera.fieldOfView;
                thumbnailCamera.orthographicSize = sceneCamera.orthographicSize;

                thumbnailCamera.farClipPlane = sceneCamera.farClipPlane;
                thumbnailCamera.nearClipPlane = sceneCamera.nearClipPlane;

                PrefabUtility.RecordPrefabInstancePropertyModifications(thumbnailCamera);
            }

            return true;
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

#if UNITY_POST_PROCESSING_STACK_V2
                PostProcessLayer postProcessLayer = renderer.GetComponent<PostProcessLayer>();
#endif

                if (!selectedParams.allowAntialiasing)
                {

#if UNITY_POST_PROCESSING_STACK_V2
                    if (postProcessLayer != null)
                    {
                        postProcessLayer.antialiasingMode = PostProcessLayer.Antialiasing.None;
                    }
#endif

                    cam.allowMSAA = false;
                }

                if (cam.allowMSAA)
                {
                    supersampleBuffer.antiAliasing = 8;
                }

                cam.allowDynamicResolution = false;
                cam.targetTexture = supersampleBuffer;

#if UNITY_POST_PROCESSING_STACK_V2
                int renderCount = 1;

                if (postProcessLayer != null
                    && postProcessLayer.enabled
                    && postProcessLayer.antialiasingMode == PostProcessLayer.Antialiasing.TemporalAntialiasing
                )
                {
                    //Render the scene multiple times so that TAA can properly antialias the thumbnail
                    renderCount = 300;
                }

                while (renderCount > 0)
                {
                    cam.Render();
                    renderCount -= 1;
                }
#else
                cam.Render();
#endif

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
                RenderTexture.active = lastRT;
                DestroyImmediate(renderer);

                thumbnail.Release();
                supersampleBuffer.Release();
            }
        }
    }
}
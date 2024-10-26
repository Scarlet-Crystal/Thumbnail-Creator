using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;

#if POSTPROCESSING_STACK_V2_INSTALLED
using UnityEngine.Rendering.PostProcessing;
#endif

#if WORLDS_VRCSDK_INSTALLED
using VRC.SDK3.Components;
#endif

namespace ThumbnailUtilities
{
    [CustomEditor(typeof(ThumbnailCreator))]
    class ThumbnailCreatorEditor : Editor
    {
        const int ThumbnailWidth = 1200, ThumbnailHeight = 900;

        SerializedProperty supersampleModeProperty,
            resolutionScaleProperty,
            jitteredSamplesProperty;

        AnimBool advancedFoldoutBool, otherAANoticeBool;

        readonly Vector2[] x1SamplePositions =
        {
            Vector2.zero
        };

        //MSAAx4 sample positions
        readonly Vector2[] x4SamplePositions =
        {
            new(0.375f, 0.125f),
            new(-0.125f, 0.375f),
            new(0.125f, -0.375f),
            new(-0.375f, -0.125f),
        };

        readonly Vector2[] x16SamplePositions =
        {
            new(-0.46875f, -0.21875f),
            new(-0.40625f, 0.28125f),
            new(-0.34375f, -0.46875f),
            new(-0.28125f, 0.03125f),
            new(-0.21875f, -0.09375f),
            new(-0.15625f, 0.40625f),
            new(-0.09375f, -0.34375f),
            new(-0.03125f, 0.15625f),
            new(0.03125f, -0.15625f),
            new(0.09375f, 0.34375f),
            new(0.15625f, -0.40625f),
            new(0.21875f, 0.09375f),
            new(0.28125f, -0.03125f),
            new(0.34375f, 0.46875f),
            new(0.40625f, -0.28125f),
            new(0.46875f, 0.21875f),
        };

        //https://en.wikipedia.org/wiki/Low-discrepancy_sequence#Additive_recurrence
        //https://extremelearning.com.au/unreasonable-effectiveness-of-quasirandom-sequences/
        private static IEnumerable<Vector2> AdditiveRecurrenceSequence(int amount)
        {
            double g = 1.32471795724474602596;

            double a1 = 1 / g;
            double a2 = 1 / (g * g);

            double x = 0, y = 0;

            for (int i = 0; i < amount; i += 1)
            {
                x = (x + a1) % 1;
                y = (y + a2) % 1;

                yield return new Vector2((float)(x - 0.5), (float)(y - 0.5));
            }

            yield break;
        }

        [MenuItem("GameObject/Thumbnail Creator", priority = 10)]
        public static void CreateThumbnailCreator(MenuCommand menuCommand)
        {
            var go = new GameObject("ThumbnailCreator", typeof(ThumbnailCreator)) { tag = "EditorOnly" };

            var cam = go.GetComponent<Camera>();
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;

#if POSTPROCESSING_STACK_V2_INSTALLED
            var ppl = go.AddComponent<PostProcessLayer>();
            ppl.enabled = false;
            ppl.volumeLayer = -1;
#endif

#if WORLDS_VRCSDK_INSTALLED
            VRCSceneDescriptor sceneDescriptor = FindObjectOfType<VRCSceneDescriptor>();

            if (sceneDescriptor != null && sceneDescriptor.ReferenceCamera != null)
            {
                if (sceneDescriptor.ReferenceCamera.TryGetComponent<Camera>(out var refCam))
                {
                    cam.CopyFrom(refCam);
                }

#if POSTPROCESSING_STACK_V2_INSTALLED
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

        private bool IsOtherAADisallowed()
        {
            var ssmode = (SupersampleMode)supersampleModeProperty.intValue;

            return ssmode switch
            {
                SupersampleMode.Off => false,
                SupersampleMode.Advanced => jitteredSamplesProperty.intValue > 1,
                _ => true
            };
        }

        private bool IsAdvancedFoldoutVisable()
        {
            return supersampleModeProperty.intValue == (int)SupersampleMode.Advanced;
        }

        private void OnEnable()
        {
            supersampleModeProperty = serializedObject.FindProperty(nameof(ThumbnailCreator.supersampleMode));
            resolutionScaleProperty = serializedObject.FindProperty(nameof(ThumbnailCreator.resolutionScale));
            jitteredSamplesProperty = serializedObject.FindProperty(nameof(ThumbnailCreator.jitteredSamples));

            advancedFoldoutBool = new AnimBool(IsAdvancedFoldoutVisable(), Repaint);
            otherAANoticeBool = new AnimBool(IsOtherAADisallowed(), Repaint);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();


            EditorGUILayout.PropertyField(supersampleModeProperty);


            advancedFoldoutBool.target = IsAdvancedFoldoutVisable();

            if (EditorGUILayout.BeginFadeGroup(advancedFoldoutBool.faded))
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(resolutionScaleProperty);
                EditorGUILayout.PropertyField(jitteredSamplesProperty);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFadeGroup();


            EditorGUILayout.Space();

            otherAANoticeBool.target = IsOtherAADisallowed();

            if (EditorGUILayout.BeginFadeGroup(otherAANoticeBool.faded))
            {
                EditorGUILayout.HelpBox(
                    "MSAA and postprocessing anti-aliasing will be disabled when rendering in this mode.",
                    MessageType.Info
                );
            }

            EditorGUILayout.EndFadeGroup();


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
                        if (TryRenderThumbnail(target as ThumbnailCreator, out Texture2D thumbnail))
                        {
                            File.WriteAllBytes(savePath, thumbnail.EncodeToPNG());
                            AssetDatabase.Refresh();
                        }
                    }
                    catch (Exception)
                    {
                        EditorUtility.DisplayDialog("Rendering Error", "Rendering failed, check the console for details.", "Understood");
                        throw;
                    }
                }
            }


            serializedObject.ApplyModifiedProperties();
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

                var targetTexture = thumbnailCamera.targetTexture;
                thumbnailCamera.CopyFrom(sceneCamera);
                thumbnailCamera.targetTexture = targetTexture;

                PrefabUtility.RecordPrefabInstancePropertyModifications(thumbnailCamera);
            }

            return true;
        }

        private bool TryRenderThumbnail(ThumbnailCreator thumbnailCreator, out Texture2D thumbnail)
        {
            ComputeRenderingParameters(
                out int renderScale,
                out int totalSamples,
                out IEnumerable<Vector2> samplePoints
            );

            CreateRenderTextures(
                renderScale,
                out RenderTexture readPixelsBuffer,
                out RenderTexture renderingBuffer,
                out RenderTexture accumulationBuffer
            );

            var lastRT = RenderTexture.active;
            GameObject renderer = Instantiate(thumbnailCreator.gameObject);

            try
            {
                renderer.hideFlags = HideFlags.DontSave;

                Camera cam = GetAndSetupCamera(
                    renderer,
                    renderingBuffer,
                    out Matrix4x4 preJitteredMatrix
                );

                Material blitMat = new(Shader.Find("ThumbnailCreator/DownsamplingBlitter"))
                {
                    shaderKeywords = new string[]
                    {
                        $"_SIDE_LENGTH_{renderScale.ToString(CultureInfo.InvariantCulture)}"
                    }
                };


                Graphics.Blit(Texture2D.blackTexture, accumulationBuffer);

                int count = 0;
                foreach (var sample in samplePoints)
                {
                    bool shouldAbort = EditorUtility.DisplayCancelableProgressBar(
                        "Rendering Thumbnail",
                        $"Rendering sample {count + 1} of {totalSamples}...",
                        count / (float)totalSamples
                    );

                    if (shouldAbort)
                    {
                        thumbnail = null;
                        return false;
                    }

                    cam.projectionMatrix = Matrix4x4.Translate(sample) * preJitteredMatrix;
                    cam.Render();

                    Graphics.Blit(renderingBuffer, accumulationBuffer, blitMat);

                    count += 1;
                }

                renderingBuffer.Release();


                blitMat = new Material(Shader.Find("ThumbnailCreator/ResolveBlitter"));
                blitMat.SetFloat("_JitteredSampleCount", totalSamples);

                Graphics.Blit(accumulationBuffer, readPixelsBuffer, blitMat);

                thumbnail = new Texture2D(readPixelsBuffer.width, readPixelsBuffer.height, TextureFormat.ARGB32, false);
                thumbnail.ReadPixels(new Rect(0, 0, readPixelsBuffer.width, readPixelsBuffer.height), 0, 0, false);

                return true;
            }
            finally
            {
                RenderTexture.active = lastRT;
                DestroyImmediate(renderer);

                readPixelsBuffer.Release();
                renderingBuffer.Release();
                accumulationBuffer.Release();

                EditorUtility.ClearProgressBar();
            }
        }

        private void ComputeRenderingParameters(
            out int renderScale,
            out int totalSamplePoints,
            out IEnumerable<Vector2> samplePoints
        )
        {
            var mode = (SupersampleMode)supersampleModeProperty.intValue;

            Tuple<ResolutionScale, JitteredSampleCount> scaleAndSampleCount = mode switch
            {
                SupersampleMode.Off => new(ResolutionScale.m_1, JitteredSampleCount.None),
                SupersampleMode.Low => new(ResolutionScale.m_4, JitteredSampleCount.m_4),
                SupersampleMode.High => new(ResolutionScale.m_4, JitteredSampleCount.m_256),
                _ => new(
                        (ResolutionScale)resolutionScaleProperty.intValue,
                        (JitteredSampleCount)jitteredSamplesProperty.intValue
                    )
            };

            renderScale = Mathf.Max(1, (int)scaleAndSampleCount.Item1);
            totalSamplePoints = Mathf.Max(1, (int)scaleAndSampleCount.Item2);

            float halfWidth = (ThumbnailWidth * renderScale) / 2f;
            float halfHeight = (ThumbnailHeight * renderScale) / 2f;

            samplePoints = scaleAndSampleCount.Item2 switch
            {
                JitteredSampleCount.None => x1SamplePositions,
                JitteredSampleCount.m_4 => x4SamplePositions,
                JitteredSampleCount.m_16 => x16SamplePositions,
                _ => AdditiveRecurrenceSequence(totalSamplePoints),
            };

            samplePoints = samplePoints.Select(v => new Vector2(v.x / halfWidth, v.y / halfHeight));
        }

        private void CreateRenderTextures(
            int renderScale,
            out RenderTexture readPixelsBuffer,
            out RenderTexture renderingBuffer,
            out RenderTexture accumulationBuffer
        )
        {
            int renderWidth = ThumbnailWidth * renderScale;
            int renderHeight = ThumbnailHeight * renderScale;

            if (renderWidth > SystemInfo.maxTextureSize || renderHeight > SystemInfo.maxTextureSize)
            {
                throw new Exception("Unable to allocate rendering buffer. Try using a lower resolution scale.");
            }

            readPixelsBuffer = new RenderTexture(
                new RenderTextureDescriptor(ThumbnailWidth, ThumbnailHeight, RenderTextureFormat.ARGB32)
                {
                    sRGB = true
                }
            );

            accumulationBuffer = new RenderTexture(ThumbnailWidth, ThumbnailHeight, 0)
            {
                format = RenderTextureFormat.ARGBFloat,
                filterMode = FilterMode.Point,
            };

            renderingBuffer = new RenderTexture(renderWidth, renderHeight, 32)
            {
                format = RenderTextureFormat.ARGBHalf,
                filterMode = FilterMode.Point,
            };
        }

        private Camera GetAndSetupCamera(GameObject renderer, RenderTexture renderingBuffer, out Matrix4x4 preJitteredProjectionMatrix)
        {
            Camera cam = renderer.GetComponent<Camera>();

            preJitteredProjectionMatrix = cam.projectionMatrix;
            cam.nonJitteredProjectionMatrix = preJitteredProjectionMatrix;
            cam.useJitteredProjectionMatrixForTransparentRendering = true;

            if (IsOtherAADisallowed())
            {

#if POSTPROCESSING_STACK_V2_INSTALLED
                if (cam.TryGetComponent<PostProcessLayer>(out var postProcessLayer))
                {
                    postProcessLayer.antialiasingMode = PostProcessLayer.Antialiasing.None;
                }
#endif

                cam.allowMSAA = false;
            }

            if (cam.clearFlags != CameraClearFlags.Skybox && cam.clearFlags != CameraClearFlags.SolidColor)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
            }

            renderingBuffer.antiAliasing = cam.allowMSAA ? 8 : 1;

            cam.allowDynamicResolution = false;
            cam.targetTexture = renderingBuffer;

            return cam;
        }
    }
}
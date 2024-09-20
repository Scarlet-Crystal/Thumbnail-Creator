using System;
using UnityEngine;
using VRC.SDKBase;

namespace ThumbnailUtilities
{
    public enum SupersampleMode
    {
        Off,
        Low,
        High,
        Advanced,
    }

    public enum ResolutionScale
    {
        m_1,
        m_2 = 2,
        m_4 = 4,
    }

    public enum JitteredSampleCount
    {
        None,
        m_4 = 4,
        m_16 = 16,
        m_64 = 64,
        m_256 = 256,
        m_1024 = 1024,
        m_4096 = 4096,
    }

    [RequireComponent(typeof(Camera))]
    public class ThumbnailCreator : MonoBehaviour, IEditorOnly
    {
        [Tooltip(
            "Supersampling mode\n\nOff - Disables supersampling\n\n" +
            "Low - Provides high quality anti-aliasing for most edges\n\n" +
            "High - Useful for scenes with intense aliasing\n\n" +
            "Advanced - Lets you tailor the supersampling parameters to your scene"
        )]
        public SupersampleMode supersampleMode = SupersampleMode.Low;

        [Tooltip(
            "The scale to render the thumbnail at. " +
            "After rendering the thumbnail will be scaled down to the proper size."
        )]
        public ResolutionScale resolutionScale = ResolutionScale.m_2;

        [Tooltip(
            "The amount of jittered samples to render. For each jittered sample, " +
            "the thumbnail is rendered with a small offset. " +
            "The resulting renders are then combined to produce a single, anti-aliased image. \n" +
            "Setting this to none will allow you to use MSAA and postprocessing anti-aliasing."
        )]
        public JitteredSampleCount jitteredSamples = JitteredSampleCount.m_16;

        void OnValidate()
        {
            ValidateEnum(ref supersampleMode, SupersampleMode.Low);
            ValidateEnum(ref resolutionScale, ResolutionScale.m_2);
            ValidateEnum(ref jitteredSamples, JitteredSampleCount.m_16);
        }

        private void ValidateEnum<T>(ref T value, T defaultValue) where T : Enum
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                value = defaultValue;
            }
        }
    }
}
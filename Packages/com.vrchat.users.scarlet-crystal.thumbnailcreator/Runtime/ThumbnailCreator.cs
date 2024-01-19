using System;
using UnityEngine;
using VRC.SDKBase;

namespace ThumbnailUtilities
{
    public enum SSAAQuality
    {
        None,
        Low,
        Medium,
        High,
    }

    [RequireComponent(typeof(Camera))]
    public class ThumbnailCreator : MonoBehaviour, IEditorOnly
    {
        [Tooltip("Supersample level."
        + "\nHigher levels will use more VRAM."
        + "\nNone will disable supersampling. Use this option if you wish to use MSAA or"
        + " postprocessing-based antialiasing instead.")]
        public SSAAQuality supersampleLevel = SSAAQuality.None;

        void OnValidate()
        {
            if (!Enum.IsDefined(typeof(SSAAQuality), supersampleLevel))
            {
                supersampleLevel = SSAAQuality.None;
            }
        }
    }
}
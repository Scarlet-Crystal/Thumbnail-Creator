using UnityEngine;
using VRC.SDKBase;

namespace ThumbnailUtilities
{
    //keep this enum in sync with the size and order of the QualityParams array in ThumbnailCreatorEditor
    public enum SSAAQuality
    {
        None,
        Low,
        Medium,
        High,
        Ultra
    }
    
    [RequireComponent(typeof(Camera))]
    public class ThumbnailCreator : MonoBehaviour, IEditorOnly
    {
        [Tooltip("Supersample level."
        + "\nHigher levels will use more VRAM, with Ultra potentially requiring several gigabytes of VRAM."
        + "\nNone will disable supersampling. Use this option if you wish to use MSAA or"
        + " postprocessing-based antialiasing instead.")]
        public SSAAQuality supersampleLevel = SSAAQuality.Medium;
    }
}
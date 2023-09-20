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
    }
    
    [RequireComponent(typeof(Camera))]
    public class ThumbnailCreator : MonoBehaviour, IEditorOnly
    {
        [Tooltip("Supersample level."
        + "\nHigher levels will use more VRAM."
        + "\nNone will disable supersampling. Use this option if you wish to use MSAA or"
        + " postprocessing-based antialiasing instead.")]
        public SSAAQuality supersampleLevel = SSAAQuality.Medium;
    }
}
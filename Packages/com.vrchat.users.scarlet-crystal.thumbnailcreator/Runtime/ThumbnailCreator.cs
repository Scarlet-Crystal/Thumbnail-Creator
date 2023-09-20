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
        [Tooltip("Supersample level.\nLow should be sufficient for most scenes."
        + "\nHigher levels will use more VRAM, with Ultra potentially requiring several gigabytes of VRAM. \n"
        +"Use None if you wish to use MSAA or postprocessing-based antialiasing instead.")]
        public SSAAQuality supersampleLevel = SSAAQuality.Low;
    }
}
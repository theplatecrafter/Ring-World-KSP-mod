using UnityEngine;
namespace NivenRingworld
{
    internal static class StockGraphics
    {
        internal static float Scatter {get{return GameSettings.PLANET_SCATTER?Mathf.Clamp01(GameSettings.PLANET_SCATTER_FACTOR):0;}}
        internal static string Description {get{return "KSP graphics: AA "+QualitySettings.antiAliasing+"x, texture mip limit "+QualitySettings.masterTextureLimit+", shadows "+QualitySettings.shadows+". Terrain scatters "+(Scatter>0?(Scatter*100).ToString("F0")+"%":"OFF (enable in KSP graphics settings)")+". Quality follows KSP settings; ring shadows use a local range.";}}
    }
}

using System;
using HarmonyLib;
using Ringworld.Core;
namespace NivenRingworld
{
    // Direct orbital queries during crash debris initialization bypass the
    // PatchedConicSolver guard. Keep the stock helper's endpoint convention,
    // but never iterate billions of revolutions or loop forever on infinity.
    [HarmonyPatch(typeof(UtilMath),nameof(UtilMath.ClampRadians))]
    internal static class RingAngleSafety
    {
        internal static int Reduced;
        private static bool Prefix(double angle,ref double __result)
        {
            if(!double.IsNaN(angle)&&!double.IsInfinity(angle)&&Math.Abs(angle)<=2*Math.PI*1024)return true;
            __result=AngleReduction.RadiansInclusive(angle);Reduced++;return false;
        }
    }
}

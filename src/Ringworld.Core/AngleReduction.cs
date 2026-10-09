using System;
namespace Ringworld.Core
{
    public static class AngleReduction
    {
        // KSP's legacy ClampRadians includes the positive endpoint (2*pi).
        // Remainder avoids a loop proportional to the number of revolutions.
        public static double RadiansInclusive(double angle)
        {
            if(double.IsNaN(angle)||double.IsInfinity(angle))return double.NaN;
            double period=2*Math.PI;
            if(angle>=0&&angle<=period)return angle;
            double result=angle%period;
            if(result<0)result+=period;
            if(result==0&&angle>0)return period;
            return result==0?0:result;
        }
    }
}

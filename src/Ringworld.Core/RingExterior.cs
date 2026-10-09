using System;
namespace Ringworld.Core
{
    public static class RingExterior
    {
        // Ring-local coordinates in physical metres, before any shader float cast.
        public static bool InHullBand(DVec camera,double radius,double halfWidth,double underside)
        {return Math.Abs(camera.Y)<=halfWidth&&radius-Math.Sqrt(camera.X*camera.X+camera.Z*camera.Z)<underside;}
    }
}

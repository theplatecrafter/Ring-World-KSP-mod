using System;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    internal static class RingCloudField
    {
        internal static Vector4 Handoff(Settings s,bool volume)
        {
            float range=volume?(float)s.RenderCloudRange:180000;
            return new Vector4(range*.65f,range*.95f,0,0);
        }
        internal static string Describe(WeatherSample w){return w.Storm>.05?"Thunderstorm":w.Rain>.05?"Rain":w.Cloud>.65?"Overcast":w.Cloud>.15?"Partly cloudy":"Clear";}
        internal static Vector3 Origin(Settings s,double along,double across,double altitude,double time)
        {
            double period=s.Geometry.P.Circumference/Math.Round(s.Geometry.P.Circumference/512000);
            double x=RingGeometry.Wrap((along-time*s.CloudWind)/period,1)*512000;
            double y=RingGeometry.Wrap(across+time*s.CloudWind*.1875,512000);
            return new Vector3((float)(x+s.Terrain.Scatter(1,0,1249)*512000),(float)(y+s.Terrain.Scatter(2,0,1249)*512000),(float)(altitude+s.Terrain.Scatter(3,0,1249)*64000));
        }
        internal const double CoverageScale=4000000;
        internal static Vector4 CoverageOrigin(Settings s,double along,double across,double time)
        {
            double cells=Math.Round(s.Geometry.P.Circumference/CoverageScale);
            double x=RingGeometry.Wrap((along-time*s.CloudWind)/s.Geometry.P.Circumference,1)*cells;
            double y=RingGeometry.Wrap(across+time*s.CloudWind*.1875,CoverageScale*1048576)/CoverageScale;
            return new Vector4((float)Math.Floor(x),(float)Math.Floor(y),(float)(x-Math.Floor(x)),(float)(y-Math.Floor(y)));
        }
        internal static WeatherSample Apply(Material material,Settings s,double along,double across,double altitude,double time)
        {
            var weather=s.Weather(along,across,time);
            material.SetVector("_CoverageOrigin",CoverageOrigin(s,along,across,time));
            // Slowly morph the shared field as well as advecting it. Interpolate
            // seeded epochs so warp never jumps at a weather-period boundary.
            double epoch=Math.Floor(time/s.WeatherPeriod),blend=time/s.WeatherPeriod-epoch;
            blend=blend*blend*(3-2*blend);
            Func<int,float> evolution=salt=>s.DynamicWeather?(float)(.5*(s.Terrain.Scatter((long)epoch,0,salt)*(1-blend)+s.Terrain.Scatter((long)epoch+1,0,salt)*blend-.5)):0;
            material.SetVector("_CoverageEvolution",new Vector4(evolution(1553),evolution(1559),0,0));
            material.SetFloat("_CoveragePeriod",(float)Math.Round(s.Geometry.P.Circumference/CoverageScale));
            material.SetFloat("_CoverageScaleX",(float)(Math.Round(s.Geometry.P.Circumference/CoverageScale)/s.Geometry.P.Circumference));
            uint seed=unchecked((uint)s.Geometry.P.Seed);material.SetVector("_CoverageSeed",new Vector4(seed&65535,seed>>16,0,0));
            material.SetVector("_CloudOrigin",Origin(s,along,across,altitude,time));material.SetFloat("_CloudAmount",(float)weather.Cloud);
            float flash=s.LightningEnabled&&TimeWarp.CurrentRate<=10?(float)RingWeather.Lightning(s.Terrain,time,weather.Storm):0;
            material.SetFloat("_Lightning",flash*.25f);return weather;
        }
    }
}

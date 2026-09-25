using System;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld.Extensions
{
    // Optional presentation only. Mean water level, buoyancy and science belong
    // to the host; disabling this module must never change a saved vessel.
    internal static class RingworldWater
    {
        private static readonly Vector4[] spectrum=new Vector4[12];
        internal static void Update(Settings settings,Material material,Shader fallback,DVec observer,Vector3d star)
        {
            RingPoint p=settings.Geometry.Coordinates(observer);
            var f=RingworldFlight.Instance;int waterQuality=settings.WaterExtension?settings.WaterQuality:0;
            bool refracting=false;
            if(f!=null&&f.visuals!=null)
            {
                var shader=f.visuals.WaterShader(waterQuality>=3);
                if(shader==null||!shader.isSupported)shader=f.visuals.WaterShader();
                if(shader!=null&&shader.isSupported)
                {
                    material.shader=shader;refracting=shader.name=="NivenRingworld/WaterRefraction";
                    material.SetVector("_WaveCamera",(Vector3)(star+ConvertVector.Ksp(observer)));
                    material.SetVector("_WaveAlong",ConvertVector.Unity(settings.Geometry.SpinVelocity(observer).Unit));
                    material.SetVector("_WaveAcross",Vector3.up);material.SetVector("_WaveUp",ConvertVector.Unity(settings.Geometry.Up(observer)));
                    double time=Planetarium.GetUniversalTime();
                    var weather=settings.Weather(p.Along,p.Across,time);
                    int count=waterQuality>=4?12:waterQuality>=3?9:waterQuality>=2?6:3;
                    for(int i=0;i<12;i++)
                    {
                        double wavelength=210*Math.Pow(.59,i),k=2*Math.PI/wavelength;
                        double direction=.5+(settings.Terrain.Scatter(i,0,1907)-.5)*2.6;
                        double kx=k*Math.Cos(direction),ky=k*Math.Sin(direction);
                        double speed=Math.Sqrt(settings.Geometry.P.Gravity*k);
                        double phaseValue=kx*p.Along+ky*p.Across-speed*time+settings.Terrain.Scatter(i,0,1913)*Math.PI*2;
                        spectrum[i]=new Vector4((float)kx,(float)ky,(float)RingGeometry.Wrap(phaseValue,Math.PI*2),(float)(.45*Math.Exp(-i*.42)));
                    }
                    material.SetVectorArray("_SeaWaves",spectrum);material.SetFloat("_SeaCount",count);
                    material.SetFloat("_SeaWind",(float)(.4+weather.Storm*.6));
                    var lightPosition=settings.Body!=null?settings.Body.position:star;
                    material.SetVector("_WaveSun",((Vector3)(lightPosition-star-ConvertVector.Ksp(observer))).normalized);

                    material.SetVector("_Wave",new Vector4(0,0,0,waterQuality>=2?(float)settings.WaveHeight:0));
                    Func<double,float> phase=a=>(float)RingGeometry.Wrap(a,Math.PI*2);
                    material.SetVector("_WavePhase",new Vector4(phase(p.Along*.037+p.Across*.012-time*1.1),phase(-p.Along*.016+p.Across*.029-time*.8),phase(p.Along*.063-p.Across*.054-time*1.7),0));
                    material.SetVector("_RipplePhase",new Vector4(phase(p.Along*1.7+p.Across*.64-time*2),phase(p.Across*1.3-p.Along*.92+time*1.6),phase(p.Along*5+p.Across*3.1+time*2.4),phase(p.Across*4.2-p.Along*3.7-time*2.1)));
                    material.SetVector("_NoiseOffset",new Vector4((float)RingGeometry.Wrap(p.Along,65536),(float)RingGeometry.Wrap(p.Across,65536),(float)RingGeometry.Wrap(time*.15,65536),0));
                    material.SetFloat("_WaterLight",(float)settings.Geometry.Daylight(p.Along,time));material.SetFloat("_WaterQuality",waterQuality);
                }
                else material.shader=fallback;
            }
            else material.shader=fallback;
            WaterScreenCopy.Enable(refracting);
        }
    }
}

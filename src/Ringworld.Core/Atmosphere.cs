using System;
using System.Collections.Generic;

namespace Ringworld.Core
{
    public struct AirSample
    {
        public double Density,PressureKPa,Temperature,SoundSpeed;
    }
    public struct SkySample
    {
        public DVec Radiance;
        public double Opacity;
    }
    // Dry air, a tropospheric lapse rate and a smooth high-altitude density cutoff.
    // The habitat is an inward-facing finite cylindrical shell, never a sphere.
    public sealed class RingAtmosphere
    {
        private readonly RingGeometry geometry;
        private TerrainGenerator cloudNoise;
        public RingAtmosphere(RingGeometry geometry) { this.geometry=geometry; }
        public AirSample Sample(DVec position)
        {
            var p=geometry.Coordinates(position);
            double temperature=Math.Max(216.65,288.15-.0065*Math.Max(0,p.Altitude));
            double density=p.Altitude<-.001 || Math.Abs(p.Across)>geometry.P.Width/2 ? 0 : geometry.Density(p.Altitude);
            return new AirSample{Density=density,Temperature=temperature,PressureKPa=density*287.05*temperature/1000,SoundSpeed=Math.Sqrt(1.4*287.05*temperature)};
        }
        // Numerical single scattering with Rayleigh and Henyey-Greenstein Mie phase
        // functions. Coefficients are per metre. This is not multiple scattering.
        public SkySample Sky(DVec origin,DVec direction,double time,bool unshadowed=false)
        {
            direction=direction.Unit;
            const double limit=4000000;
            var localOrigin=geometry.Basis.ToLocal(origin);var localDirection=geometry.Basis.ToLocal(direction);
            var cuts=new List<double>{0,limit};
            AddCylinderCuts(cuts,localOrigin,localDirection,geometry.P.Radius,limit);
            AddCylinderCuts(cuts,localOrigin,localDirection,geometry.P.Radius-geometry.P.AtmosphereHeight,limit);
            if(Math.Abs(localDirection.Y)>1e-12)
                foreach(double y in new[]{-geometry.P.Width/2,geometry.P.Width/2})
                {double s=(y-localOrigin.Y)/localDirection.Y;if(s>0&&s<limit)cuts.Add(s);}
            cuts.Sort();DVec optical=new DVec(),light=new DVec();
            DVec beta=new DVec(5.8e-6,13.5e-6,33.1e-6);
            for(int span=0;span<cuts.Count-1;span++)
            {
                double start=cuts[span],end=cuts[span+1];
                var middle=geometry.Coordinates(origin+direction*((start+end)*.5));
                if(middle.Altitude<0&&Math.Abs(middle.Across)<=geometry.P.Width/2)break; // opaque scrith floor
                if(middle.Altitude<0||middle.Altitude>geometry.P.AtmosphereHeight||Math.Abs(middle.Across)>geometry.P.Width/2)continue;
                double ds=(end-start)/32;
                for(int i=0;i<32;i++)
                {
                    var point=origin+direction*(start+(i+.5)*ds);var c=geometry.Coordinates(point);
                    double rho=geometry.Density(c.Altitude)/1.225,mie=Math.Exp(-Math.Max(0,c.Altitude)/1200)*2e-5;
                    var sunlight=geometry.LightDirection==null?(-point).Unit:geometry.LightDirection(point,time);
                    double mu=DVec.Dot(direction,sunlight),hg=.76;
                    double phaseR=3*(1+mu*mu)/(16*Math.PI);
                    double phaseM=(1-hg*hg)/(4*Math.PI*Math.Pow(1+hg*hg-2*hg*mu,1.5));
                    var extinction=beta*rho+new DVec(mie,mie,mie);
                    // Approximate the column along the actual illumination direction.
                    double solarColumn=geometry.P.ScaleHeight*rho/Math.Max(.1,DVec.Dot(sunlight,geometry.Up(point)));
                    var sunDepth=beta*solarColumn+new DVec(mie,mie,mie)*1200;
                    var depth=optical+extinction*(ds*.5)+sunDepth;
                    double lit=unshadowed?1:geometry.Daylight(c.Along,time,c.Across,c.Altitude);
                    var source=beta*(rho*phaseR)+new DVec(mie,mie,mie)*phaseM;
                    light+=new DVec(source.X*Math.Exp(-depth.X),source.Y*Math.Exp(-depth.Y),source.Z*Math.Exp(-depth.Z))*(ds*lit*12);
                    optical+=extinction*ds;
                }
            }
            return new SkySample{Radiance=light,Opacity=1-(Math.Exp(-optical.X)+Math.Exp(-optical.Y)+Math.Exp(-optical.Z))/3};
        }
        private static void AddCylinderCuts(List<double> cuts,DVec o,DVec d,double radius,double limit)
        {
            double a=d.X*d.X+d.Z*d.Z,b=o.X*d.X+o.Z*d.Z;
            if(a<1e-16)return;
            // Factored difference retains precision near the cylinder surface.
            double r=Math.Sqrt(o.X*o.X+o.Z*o.Z),c=(r-radius)*(r+radius),disc=b*b-a*c;
            if(disc<0)return;double root=Math.Sqrt(disc);
            double q=-b-(b>=0?root:-root);
            double s1=q/a,s2=Math.Abs(q)>1e-16?c/q:-b/a;
            if(s1>0&&s1<limit)cuts.Add(s1);if(s2>0&&s2<limit)cuts.Add(s2);
        }
        public double CloudCoverage(double along,double across,double time,double amount=.5,bool dynamicWeather=true)
        {
            // Periodic material coordinates, slow wind, soft edges and several scales.
            if(cloudNoise==null)cloudNoise=new TerrainGenerator(geometry);
            double a=along-time*8,b=across+time*1.5;
            double warp=(cloudNoise.Noise(a,b,85000,201)-.5)*38000;
            double weather=dynamicWeather?cloudNoise.Noise(along-time*120,across+time*45,280000,203):.5;
            double n=.56*cloudNoise.Noise(a+warp,b,24000,211)+.29*cloudNoise.Noise(a,b+warp,7000,223)+.15*cloudNoise.Noise(a,b,1800,227);
            double density=Math.Max(0,Math.Min(1,(n-(.79-.50*amount-.13*weather))/.20));
            return density*density*(3-2*density);
        }
    }
}

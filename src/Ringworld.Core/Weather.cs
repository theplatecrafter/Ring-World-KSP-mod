using System;
namespace Ringworld.Core
{
    public struct WeatherSample
    {
        public double Severity,Cloud,Rain,Storm;
    }
    // Seeded smooth weather in material coordinates and universal time, not a looping animation.
    public static class RingWeather
    {
        private static double Clamp(double x){return Math.Max(0,Math.Min(1,x));}
        private static double Smooth(double x){x=Clamp(x);return x*x*(3-2*x);}
        public static WeatherSample Sample(TerrainGenerator terrain,double along,double across,double time,double baseCloud,bool enabled,double period,double variation,double stormChance)
        {
            if(baseCloud<=0)return new WeatherSample();
            double severity=baseCloud;
            if(enabled)
            {
                double tick=Math.Floor(time/period),blend=Smooth(time/period-tick);
                double a=terrain.Scatter((long)tick,0,1201),b=terrain.Scatter((long)tick+1,0,1201);
                double temporal=a+(b-a)*blend;
                double front=terrain.Noise(along-time*30,across+time*7,800000,1213);
                double regional=Clamp(temporal+(front-.5)*.7);
                double wetThreshold=1-stormChance;
                double target=regional<=wetThreshold?.20+.50*regional/Math.Max(.001,wetThreshold):.7+.3*(regional-wetThreshold)/Math.Max(.001,stormChance);
                severity=baseCloud+(target-baseCloud)*variation;
            }
            double cloud=Smooth((severity-.12)/.66);
            return new WeatherSample{Severity=severity,Cloud=cloud,Rain=Smooth((severity-.60)/.30),Storm=Smooth((severity-.78)/.20)};
        }
        // Presentation model, not a solved rotating climate. Cold biomes replace rain
        // with snow; dry storm fronts lift dust; humid rim shadows favour shallow fog.
        public static string SurfaceKind(WeatherSample w,Biome biome,double rimFraction,double daylight)
        {
            if(biome==Biome.Snow&&w.Rain>.05)return w.Storm>.2?"Blizzard":"Snow";
            if(biome==Biome.Desert&&w.Storm>.2)return "Blowing dust";
            if(rimFraction>.96&&daylight<.2&&w.Cloud>.65)return "Rim-shadow fog";
            return w.Storm>.05?"Thunderstorm":w.Rain>.05?"Rain":w.Cloud>.65?"Overcast":w.Cloud>.15?"Partly cloudy":"Clear";
        }
        public static double Lightning(TerrainGenerator terrain,double time,double storm)
        {
            if(storm<=0)return 0;
            long slot=(long)Math.Floor(time/17);double phase=time-slot*17;
            double delay=2+terrain.Scatter(slot,0,1229)*12;
            if(terrain.Scatter(slot,1,1231)>storm*.7)return 0;
            double distance=Math.Abs(phase-delay);
            return Math.Max(0,1-distance/.13)*storm;
        }
    }
}

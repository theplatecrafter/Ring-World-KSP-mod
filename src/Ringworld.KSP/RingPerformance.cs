using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace NivenRingworld
{
    // Opt-in, aggregated main-thread CPU timing. Never emit per-part/per-frame
    // messages, or include coroutine suspension time in a measured CPU slice.
    internal static class RingPerformance
    {
        internal enum Section { FlightUpdate, FlightForces, Surface, NearBuild, Lod, Relevance, Prediction, MapDraw, SurfaceCaches, PackedCoast, Gravity, FlightLate, SurfacePosition, VisualPrepare, Weather, Atmosphere, SkyBake, CloudBake, ScaledPlacement, CloudVolume, WaterSurface, Underwater, ParallaxPopulate, ParallaxRender, ParallaxContacts }
        private static readonly int sectionCount=Enum.GetValues(typeof(Section)).Length;
        private static readonly long[] ticks=new long[sectionCount],maximum=new long[sectionCount],calls=new long[sectionCount];
        private static bool loaded,enabled;
        private static float nextReport;
        private static int frames,collections;
        internal static bool Enabled {get{return enabled;}}
        internal static void Configure()
        {
            if(loaded)return;loaded=true;
            enabled=Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-profile")>=0;
            if(GameDatabase.Instance!=null)
                foreach(var n in GameDatabase.Instance.GetConfigNodes("RINGWORLD_DIAGNOSTICS"))
                {bool value;if(bool.TryParse(n.GetValue("performance"),out value))enabled=value;}
            if(enabled){nextReport=Time.realtimeSinceStartup+10;collections=GC.CollectionCount(0);UnityEngine.Debug.Log("[NivenRingworld] Performance diagnostics enabled; aggregate CPU timings every 10 seconds (sections may nest).");}
        }
        internal static long Begin(){return enabled?Stopwatch.GetTimestamp():0;}
        internal static void End(Section section,long start)
        {
            if(start==0)return;long elapsed=Stopwatch.GetTimestamp()-start;int i=(int)section;
            ticks[i]+=elapsed;calls[i]++;maximum[i]=Math.Max(maximum[i],elapsed);
        }
        internal static void Report(RingworldFlight flight=null)
        {
            if(!enabled)return;frames++;float now=Time.realtimeSinceStartup;if(now<nextReport)return;
            var text=new StringBuilder("[NivenRingworld] PERF frames=").Append(frames).Append(" map=").Append(MapView.MapIsEnabled)
                .Append(" warp=").Append(TimeWarp.CurrentRate.ToString("R",CultureInfo.InvariantCulture))
                .Append(" gc0=").Append(GC.CollectionCount(0)-collections).Append(" managedMB=").Append(GC.GetTotalMemory(false)/1048576);
            int parts=0;foreach(var v in FlightGlobals.VesselsLoaded)if(v!=null&&v.parts!=null)parts+=v.parts.Count;
            text.Append(" loadedParts=").Append(parts);
            if(flight!=null)text.Append(" nearTiles=").Append(flight.NearTileCount).Append(" lod=").Append(flight.LodCount).Append(" queued=").Append(flight.LodPending);
            foreach(Section section in Enum.GetValues(typeof(Section)))
            {
                int i=(int)section;if(calls[i]==0)continue;
                double ms=ticks[i]*1000.0/Stopwatch.Frequency,max=maximum[i]*1000.0/Stopwatch.Frequency;
                text.Append(' ').Append(section).Append("[calls=").Append(calls[i]).Append(",totalMs=").Append(ms.ToString("F2",CultureInfo.InvariantCulture))
                    .Append(",maxMs=").Append(max.ToString("F2",CultureInfo.InvariantCulture)).Append(']');
            }
            UnityEngine.Debug.Log(text.ToString());Array.Clear(ticks,0,ticks.Length);Array.Clear(calls,0,calls.Length);Array.Clear(maximum,0,maximum.Length);
            frames=0;collections=GC.CollectionCount(0);nextReport=now+10;
        }
    }
}

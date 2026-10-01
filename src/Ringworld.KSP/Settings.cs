using System;
using System.Globalization;
using Ringworld.Core;
using UnityEngine;

namespace NivenRingworld
{
    internal static class ConvertVector
    {
        internal static DVec Core(Vector3d v) { return new DVec(v.x,v.y,v.z); }
        internal static Vector3d Ksp(DVec v) { return new Vector3d(v.X,v.Y,v.Z); }
        internal static Vector3 Unity(DVec v) { return new Vector3((float)v.X,(float)v.Y,(float)v.Z); }
        internal static Vector3d Orbit(Vector3d v) { return new Vector3d(v.x,v.z,v.y); }
    }
    internal sealed class Settings
    {
        private static bool warnedMultipleDefinitions;
        internal string RingId="primary", RingName="Ringworld", ReferenceBody="Sun";
        internal bool DesignatedStar=true;
        internal DVec CenterOffset;
        internal CelestialBody Body {get{return FlightGlobals.Bodies.Find(b=>b.name==ReferenceBody);}}
        internal Vector3d Center {get{return (Body==null?Vector3d.zero:Body.position)+ConvertVector.Ksp(CenterOffset);}}
        internal DVec StellarAcceleration(DVec position,double mu,double elapsed=0)
        {
            var relative=Geometry.ToInertialPosition(position,elapsed)+CenterOffset;
            double distance=relative.Length;
            return distance>1?RingGeometry.Rotate(relative*(-mu/(distance*distance*distance)),-Geometry.P.Omega*elapsed):new DVec();
        }
        internal RingGeometry Geometry;
        internal readonly CylaOptions Cyla=new CylaOptions();
        internal TerrainGenerator Terrain;
        internal int TileResolution=32, TileRadius=3;
        internal double TileSize=1024;
        internal double StructuralThickness=100;
        internal double UndersideAltitude { get { return TerrainGenerator.MinimumHeight-StructuralThickness; } }
        internal bool Atmosphere=true;
        internal double LodRange=160000000, Haze=1, CloudAmount=.45, HeightMultiplier=1, ForestDensity=1;
        internal int LodResolution=8, GenerationBudget=1, GenerationVersion=4;
        internal bool DynamicWeather=true,ShowTrajectory=true;
        internal double PredictionSeconds=3600, SurfaceWarpLimit=1000;
        internal double PondAmount=1,DetailDistance=75;
        internal bool AmbientParticles=true;
        internal int ForestQuality=1;
        internal int AtmosphereBackend=1,CylaLightSteps=2,CylaDivisor=4;internal bool CylaDither=false;
        internal int VisualQuality=0,CloudSteps=64,AtmosphereSteps=32,PhotoSamples=16,WaterQuality=0;
        internal double CloudRange=180000,CloudShadow=.85,AtmosphereExposure=1,WaveHeight=.65;
        internal bool FullRingDetail=false;
        internal bool WaterScattering=false;
        internal bool WaterExtension=true,CloudExtension=true,FullRingAtmosphere=true;
        internal int CloudMode=0;internal double CloudDensity=1;
        internal double WeatherPeriod=21600,WeatherVariation=1,StormChance=.25,CloudWind=8,RainDensity=.7;
        internal bool RainEnabled=true,LightningEnabled=true;
        internal WeatherSample Weather(double along,double across,double time){return RingWeather.Sample(Terrain,along,across,time,CloudAmount,DynamicWeather,WeatherPeriod,WeatherVariation,StormChance);}
        internal void Apply(ConfigNode n)
        {
            RingId=n.GetValue("ringId")??"primary";RingName=n.GetValue("ringName")??"Ringworld";
            ReferenceBody=n.GetValue("referenceBody")??"Sun";DesignatedStar=n.GetValue("designatedStar")!="False";
            CenterOffset=new DVec(Read(n,"centerX",0),Read(n,"centerY",0),Read(n,"centerZ",0));
            Cyla.Load(n);
            WeatherPeriod=Math.Max(600,Read(n,"weatherPeriod",21600));
            WeatherVariation=Math.Max(0,Math.Min(1,Read(n,"weatherVariation",1)));StormChance=Math.Max(0,Math.Min(1,Read(n,"stormChance",.25)));
            CloudWind=Math.Max(0,Math.Min(100,Read(n,"cloudWind",8)));RainDensity=Math.Max(0,Math.Min(1,Read(n,"rainDensity",.7)));
            RainEnabled=n.GetValue("rainEnabled")!="False";LightningEnabled=n.GetValue("lightningEnabled")!="False";
            AtmosphereBackend=(int)Math.Max(0,Math.Min(1,Read(n,"atmosphereBackend",1)));CylaDivisor=(int)Read(n,"cylaDivisor",4);CylaDivisor=CylaDivisor>=8?8:CylaDivisor>=4?4:CylaDivisor>=2?2:1;CylaLightSteps=(int)Math.Max(1,Math.Min(50,Read(n,"cylaLightSteps",2)));CylaDither=n.GetValue("cylaDither")=="True";
            VisualQuality=(int)Math.Max(0,Math.Min(2,Read(n,"visualQuality",0)));
            ForestQuality=(int)Math.Max(0,Math.Min(3,Read(n,"forestQuality",VisualQuality+1)));
            FullRingDetail=n.GetValue("fullRingDetail")=="True";
            CloudSteps=(int)Math.Max(32,Math.Min(256,Read(n,"cloudSteps",64)));
            AtmosphereSteps=(int)Math.Max(16,Math.Min(96,Read(n,"atmosphereSteps",32)));
            PhotoSamples=(int)Math.Max(1,Math.Min(64,Read(n,"photoSamples",16)));
            CloudExtension=!string.Equals(n.GetValue("cloudExtension"),"false",StringComparison.OrdinalIgnoreCase);CloudMode=(int)Math.Max(0,Math.Min(3,Read(n,"cloudMode",VisualQuality>0?VisualQuality:0)));CloudDensity=Math.Max(0,Math.Min(3,Read(n,"cloudDensity",1)));
            WaterExtension=!string.Equals(n.GetValue("waterExtension"),"false",StringComparison.OrdinalIgnoreCase);FullRingAtmosphere=!string.Equals(n.GetValue("fullRingAtmosphere"),"false",StringComparison.OrdinalIgnoreCase);
            WaterQuality=(int)Math.Max(0,Math.Min(4,Read(n,"waterQuality",0)));
            WaterScattering=n.HasValue("waterScattering")?n.GetValue("waterScattering")=="True":WaterQuality>=3;
            CloudRange=Math.Max(30000,Read(n,"cloudRange",180000));
            CloudShadow=Math.Max(0,Math.Min(1,Read(n,"cloudShadow",.85)));
            AtmosphereExposure=Math.Max(.25,Math.Min(2,Read(n,"atmosphereExposure",1)));
            WaveHeight=Math.Max(0,Math.Min(2,Read(n,"waveHeight",.65)));
            Geometry.P.Radius=Math.Max(1000000000,Read(n,"radius",Geometry.P.Radius));
            Geometry.P.Width=Math.Max(10000000,Math.Min(Geometry.P.Radius,Read(n,"width",Geometry.P.Width)));
            Geometry.P.Gravity=Math.Max(1,Math.Min(100,Read(n,"gravity",Geometry.P.Gravity)));
            Geometry.P.SurfaceDensity=Math.Max(0,Math.Min(100000000,Read(n,"surfaceDensity",1000000)));
            Geometry.P.WallHeight=Math.Max(60000,Math.Min(1000000,Read(n,"wallHeight",Geometry.P.WallHeight)));
            Geometry.P.Validate();
            foreach(double coordinate in new[]{CenterOffset.X,CenterOffset.Y,CenterOffset.Z})
                if(!RingParameters.Finite(coordinate)||Math.Abs(coordinate)+Geometry.P.Radius+.01==Math.Abs(coordinate)+Geometry.P.Radius)
                    throw new ArgumentException("Ring center exceeds centimetre coordinate precision.");
            PredictionSeconds=Math.Max(60,Math.Min(86400,Read(n,"predictionSeconds",PredictionSeconds)));
            SurfaceWarpLimit=Math.Max(10,Math.Min(10000,Read(n,"surfaceWarpLimit",SurfaceWarpLimit)));
            ShowTrajectory=n.GetValue("showTrajectory")!="False";
            DetailDistance=Math.Max(25,Math.Min(250,Read(n,"detailDistance",DetailDistance)));
            AmbientParticles=n.GetValue("ambientParticles")!="False";
            PondAmount=Math.Max(0,Math.Min(2,Read(n,"pondAmount",PondAmount)));
            Geometry.P.Seed=(int)Read(n,"seed",Geometry.P.Seed);
            LodRange=Math.Max(200000,Read(n,"lodRange",LodRange));
            LodResolution=(int)Read(n,"lodResolution",LodResolution);LodResolution=LodResolution>=32?32:LodResolution>=16?16:8;
            GenerationBudget=(int)Math.Max(1,Math.Min(4,Read(n,"generationBudget",GenerationBudget)));
            Haze=Math.Max(0,Math.Min(2,Read(n,"haze",Haze)));
            CloudAmount=Math.Max(0,Math.Min(1,Read(n,"cloudAmount",CloudAmount)));
            DynamicWeather=n.GetValue("dynamicWeather")!="False";
            HeightMultiplier=Math.Max(.25,Math.Min(3,Read(n,"heightMultiplier",HeightMultiplier)));
            ForestDensity=Math.Max(0,Math.Min(2,Read(n,"forestDensity",ForestDensity)));
            GenerationVersion=(int)Read(n,"generationVersion",GenerationVersion);
            Geometry.P.DaySeconds=Math.Max(60,Read(n,"daySeconds",Geometry.P.DaySeconds));
            Terrain=new TerrainGenerator(Geometry){HeightMultiplier=HeightMultiplier,GenerationVersion=GenerationVersion,PondAmount=PondAmount};
        }
        internal ConfigNode Save()
        {
            var n=new ConfigNode("OPTIONS");Cyla.Save(n);
            n.AddValue("ringId",RingId);n.AddValue("ringName",RingName);n.AddValue("referenceBody",ReferenceBody);n.AddValue("designatedStar",DesignatedStar);
            n.AddValue("centerX",CenterOffset.X.ToString("R",CultureInfo.InvariantCulture));n.AddValue("centerY",CenterOffset.Y.ToString("R",CultureInfo.InvariantCulture));n.AddValue("centerZ",CenterOffset.Z.ToString("R",CultureInfo.InvariantCulture));
            n.AddValue("atmosphereBackend",AtmosphereBackend);n.AddValue("cylaLightSteps",CylaLightSteps);n.AddValue("cylaDivisor",CylaDivisor);n.AddValue("cylaDither",CylaDither);
            n.AddValue("weatherPeriod",WeatherPeriod.ToString("R",CultureInfo.InvariantCulture));n.AddValue("weatherVariation",WeatherVariation.ToString("R",CultureInfo.InvariantCulture));n.AddValue("stormChance",StormChance.ToString("R",CultureInfo.InvariantCulture));n.AddValue("cloudWind",CloudWind.ToString("R",CultureInfo.InvariantCulture));n.AddValue("rainDensity",RainDensity.ToString("R",CultureInfo.InvariantCulture));n.AddValue("rainEnabled",RainEnabled);n.AddValue("lightningEnabled",LightningEnabled);
            n.AddValue("fullRingDetail",FullRingDetail);n.AddValue("forestQuality",ForestQuality);
            n.AddValue("visualQuality",VisualQuality);n.AddValue("cloudSteps",CloudSteps);n.AddValue("atmosphereSteps",AtmosphereSteps);n.AddValue("photoSamples",PhotoSamples);n.AddValue("waterQuality",WaterQuality);n.AddValue("waterExtension",WaterExtension);n.AddValue("waterScattering",WaterScattering);n.AddValue("fullRingAtmosphere",FullRingAtmosphere);n.AddValue("cloudExtension",CloudExtension);n.AddValue("cloudMode",CloudMode);n.AddValue("cloudDensity",CloudDensity.ToString("R",CultureInfo.InvariantCulture));
            n.AddValue("cloudRange",CloudRange.ToString("R",CultureInfo.InvariantCulture));n.AddValue("cloudShadow",CloudShadow.ToString("R",CultureInfo.InvariantCulture));n.AddValue("atmosphereExposure",AtmosphereExposure.ToString("R",CultureInfo.InvariantCulture));n.AddValue("waveHeight",WaveHeight.ToString("R",CultureInfo.InvariantCulture));
            foreach(var pair in new[]{new[]{"radius",Geometry.P.Radius.ToString("R",CultureInfo.InvariantCulture)},new[]{"width",Geometry.P.Width.ToString("R",CultureInfo.InvariantCulture)},new[]{"gravity",Geometry.P.Gravity.ToString("R",CultureInfo.InvariantCulture)},new[]{"wallHeight",Geometry.P.WallHeight.ToString("R",CultureInfo.InvariantCulture)},new[]{"surfaceDensity",Geometry.P.SurfaceDensity.ToString("R",CultureInfo.InvariantCulture)},new[]{"predictionSeconds",PredictionSeconds.ToString("R",CultureInfo.InvariantCulture)},new[]{"surfaceWarpLimit",SurfaceWarpLimit.ToString("R",CultureInfo.InvariantCulture)},new[]{"pondAmount",PondAmount.ToString("R",CultureInfo.InvariantCulture)}})n.AddValue(pair[0],pair[1]);
            n.AddValue("showTrajectory",ShowTrajectory);n.AddValue("detailDistance",DetailDistance.ToString("R",CultureInfo.InvariantCulture));n.AddValue("ambientParticles",AmbientParticles);
            n.AddValue("seed",Geometry.P.Seed);n.AddValue("lodRange",LodRange.ToString("R",CultureInfo.InvariantCulture));
            n.AddValue("lodResolution",LodResolution);n.AddValue("generationBudget",GenerationBudget);
            n.AddValue("haze",Haze.ToString("R",CultureInfo.InvariantCulture));n.AddValue("cloudAmount",CloudAmount.ToString("R",CultureInfo.InvariantCulture));
            n.AddValue("dynamicWeather",DynamicWeather);n.AddValue("heightMultiplier",HeightMultiplier.ToString("R",CultureInfo.InvariantCulture));
            n.AddValue("forestDensity",ForestDensity.ToString("R",CultureInfo.InvariantCulture));n.AddValue("generationVersion",GenerationVersion);
            n.AddValue("daySeconds",Geometry.P.DaySeconds.ToString("R",CultureInfo.InvariantCulture));return n;
        }
        internal static Settings Load()
        {
            var s=new Settings();var p=new RingParameters{SurfaceDensity=1000000};
            var nodes=GameDatabase.Instance.GetConfigNodes("NIVEN_RINGWORLD");
            if(nodes.Length>1&&!warnedMultipleDefinitions){warnedMultipleDefinitions=true;Debug.LogWarning("[NivenRingworld] Multiple NIVEN_RINGWORLD definitions found. The first definition supplies defaults; create additional save-specific habitats with the Sandbox ring manager.");}
            if(nodes.Length>0)
            {
                var n=nodes[0];
                p.Radius=Read(n,"radius",p.Radius);p.Width=Read(n,"width",p.Width);p.WallHeight=Read(n,"wallHeight",p.WallHeight);
                p.Gravity=Read(n,"gravity",p.Gravity);p.DaySeconds=Read(n,"daySeconds",p.DaySeconds);
                p.AtmosphereHeight=Read(n,"atmosphereHeight",p.AtmosphereHeight);p.ScaleHeight=Read(n,"scaleHeight",p.ScaleHeight);
                p.Seed=(int)Read(n,"seed",p.Seed);
                s.TileResolution=(int)Math.Max(16,Math.Min(64,Read(n,"tileResolution",32)));
                s.TileRadius=(int)Math.Max(2,Math.Min(5,Read(n,"tileRadius",3)));
                s.TileSize=Math.Max(256,Math.Min(4096,Read(n,"tileSize",1024)));
                s.StructuralThickness=Math.Max(1,Math.Min(10000,Read(n,"structuralThickness",100)));
                s.Atmosphere=n.GetValue("atmosphere")!="false";
            }
            s.Geometry=new RingGeometry(p);s.Terrain=new TerrainGenerator(s.Geometry);return s;
        }
        // Keep the requested distance in the save. GPU work stops at a conservative ring bounding extent
        // or the finite squared-distance limit; increasing a setting cannot create infinity.
        internal double RenderCloudRange { get { return Math.Min(CloudRange,Math.Min(2*Geometry.P.Radius+Geometry.P.Width,Math.Sqrt(float.MaxValue)/16)); } }
        private static double Read(ConfigNode n,string key,double fallback)
        { double value;return double.TryParse(n.GetValue(key),NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&RingParameters.Finite(value)?value:fallback; }
    }
}

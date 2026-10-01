using System;
using System.Globalization;
using UnityEngine;
namespace NivenRingworld
{
    internal sealed class RingSettingsPanel
    {
        private readonly RingSandboxEditor rings=new RingSandboxEditor();
        private bool cloudsOpen,scatteringOpen,cylaOpen;
        private bool cloudExtension;private int cloudMode;private float cloudDensity;
        private bool fullRingAtmosphere;private bool waterExtension;private bool waterScattering;private bool cylaAdvanced;private string[] cylaFields;private int cylaMode;
        private bool initialized,dynamicWeather,trajectory,particles,fullRingDetail,rainEnabled,lightningEnabled;
        private string seed,range,height,forest,day,haze,cloud,diameter,width,gravity,wall,density,prediction,warp,ponds,detailDistance,message="";
        private int quality,budget,visualQuality,waterQuality,forestQuality;
        private int atmosphereBackend,cylaResolution;private bool cylaDither;private string cylaLightSteps;
        private bool presetOpen;private string presetLabel="Custom";
        private string weatherPeriod,weatherVariation,stormChance,cloudWind,rainDensity;
        private string cloudSteps,airSteps,cloudRange,cloudShadow,exposure,waveHeight,photoSamples;
        private static string N(double n){return n.ToString("0.#########",CultureInfo.InvariantCulture);}
        private static string Field(string title,string value){GUILayout.Label(title);return GUILayout.TextField(value,40);}
        internal void Reset(){initialized=false;}
        internal void Draw(RingworldFlight flight,bool extensions=false)
        {
            var state=RingworldScenario.Instance;if(state==null){GUILayout.Label("Waiting for save settings...");return;}
            var s=flight.Settings;
            if(!initialized)
            {
                cylaFields=new string[CylaOptions.Definitions.Length];for(int i=0;i<cylaFields.Length;i++)cylaFields[i]=s.Save().GetValue(CylaOptions.Definitions[i].Key);cylaMode=s.Cyla.LightingMode;
                seed=s.Geometry.P.Seed.ToString(CultureInfo.InvariantCulture);range=N(s.LodRange/1000);height=N(s.HeightMultiplier);forest=N(s.ForestDensity);
                day=N(s.Geometry.P.DaySeconds/3600);haze=N(s.Haze);cloud=N(s.CloudAmount*100);dynamicWeather=s.DynamicWeather;
                diameter=N(s.Geometry.P.Radius/500);width=N(s.Geometry.P.Width/1000);gravity=N(s.Geometry.P.Gravity);wall=N(s.Geometry.P.WallHeight/1000);density=N(s.Geometry.P.SurfaceDensity);prediction=N(s.PredictionSeconds/60);warp=N(s.SurfaceWarpLimit);ponds=N(s.PondAmount);trajectory=s.ShowTrajectory;
                detailDistance=N(s.DetailDistance);particles=s.AmbientParticles;
                weatherPeriod=N(s.WeatherPeriod/3600);weatherVariation=N(s.WeatherVariation);stormChance=N(s.StormChance);cloudWind=N(s.CloudWind);rainDensity=N(s.RainDensity);rainEnabled=s.RainEnabled;lightningEnabled=s.LightningEnabled;fullRingDetail=s.FullRingDetail;visualQuality=s.VisualQuality;waterQuality=s.WaterQuality;waterExtension=s.WaterExtension;waterScattering=s.WaterScattering;fullRingAtmosphere=s.FullRingAtmosphere;cloudExtension=s.CloudExtension;cloudMode=s.CloudMode;cloudDensity=(float)s.CloudDensity;cloudSteps=N(s.CloudSteps);airSteps=N(s.AtmosphereSteps);cloudRange=N(s.CloudRange/1000);cloudShadow=N(s.CloudShadow);exposure=N(s.AtmosphereExposure);waveHeight=N(s.WaveHeight);photoSamples=N(s.PhotoSamples);
                atmosphereBackend=s.AtmosphereBackend;cylaResolution=s.CylaDivisor==8?0:s.CylaDivisor==4?1:s.CylaDivisor==2?2:3;cylaDither=s.CylaDither;cylaLightSteps=N(s.CylaLightSteps);presetLabel=RingQualityPresets.Match(s);forestQuality=s.ForestQuality;quality=s.LodResolution==8?0:s.LodResolution==16?1:2;budget=s.GenerationBudget-1;initialized=true;
            }
            if(extensions){DrawExtensions(flight,s);return;}
            rings.Draw(flight);
            GUILayout.Label("Settings are stored with this save.");
            if(GUILayout.Button("Quality preset: "+presetLabel+"  v"))presetOpen=!presetOpen;
            if(presetOpen)for(int i=0;i<RingQualityPresets.Names.Length;i++)
                if(GUILayout.Button(RingQualityPresets.Names[i]))
                {var options=s.Save();RingQualityPresets.Apply(options,i);flight.ApplyOptions(options,false);presetOpen=false;message="Preset applied. Save your game to keep it.";return;}
            GUILayout.Label("Selecting a preset applies its rendering settings immediately. Individual edits below use Apply settings.");
            range=Field("Terrain horizon distance (km; minimum 200)",range);
            fullRingDetail=GUILayout.Toggle(fullRingDetail,"Extra full-ring climate detail");
            GUILayout.Label("Coarse land and oceans remain visible around the entire ring at every preset. This option adds climate colour detail. Nearby terrain uses the horizon distance above; clouds have their own controls.");
            GUILayout.Label(StockGraphics.Description);
            GUILayout.Label("Ringworld atmosphere quality (independent of stock planets)");
            int chosen=GUILayout.Toolbar(visualQuality,new[]{"Simple","Half-resolution","Full-resolution"});
            if(chosen!=visualQuality){visualQuality=chosen;airSteps=chosen==2?"64":"32";}
            GUILayout.Label("Simple uses the lightweight atmosphere. Half/full-resolution use volumetrics. Texture, AA and ordinary shadows follow KSP settings.");
            airSteps=Field("Atmosphere integration steps (16 to 96)",airSteps);
            exposure=Field("Atmosphere brightness (0.25 to 2)",exposure);
            photoSamples=Field("Photo accumulation samples (1 to 64)",photoSamples);
            GUILayout.Label("Frame your shot in flight, then enter Photo mode. Choose a temporary photo preset. It freezes time and waits for that preset's terrain and forests before capture. Resume restores your settings. Choose output resolution separately, up to 16K where GPU memory permits; screen aspect ratio is preserved.");
            if(flight.visuals!=null)flight.visuals.DrawPhotoEntry();
            detailDistance=Field("Close ground detail distance (25 to 250 m)",detailDistance);
            particles=GUILayout.Toggle(particles,"Local airborne dust and pollen");
            GUILayout.Label("Distant mesh quality");quality=GUILayout.Toolbar(quality,new[]{"Low (8)","Balanced (16)","High (32)"});
            GUILayout.Label("New mesh blocks per frame");budget=GUILayout.Toolbar(budget,new[]{"1","2","3","4"});
            GUILayout.Label("Near ground retains collision detail. Far terrain/water use scaled space. Small buildings are nearby only; forests have their own distant LOD; the full ring and rim walls have a coarse global model.");
            GUILayout.Label("Biome features: forests");
            forestQuality=GUILayout.Toolbar(forestQuality,new[]{"Economy","Low","High","Ultra"});
            GUILayout.Label("Economy: quarter-count simple nearby crowns; distant canopy surface only. Low/High/Ultra add progressively denser distant crown meshes. Tree contacts and world generation are unchanged.");
            bool worldUnlocked=state.Vessels.Count==0&&state.Discoveries.Count==0&&state.Research.Receipts.Count==0;
            GUI.enabled=worldUnlocked;
            seed=Field("World seed (blank chooses a random seed)",seed);
            height=Field("Natural terrain height multiplier (0.25-3)",height);
            forest=Field("Forest density multiplier (0-2)",forest);
            ponds=Field("Pond coverage multiplier (0 to 2)",ponds);
            diameter=Field("Ring diameter (km; minimum 2,000,000)",diameter);
            width=Field("Ribbon width (10,000 km to the ring radius)",width);
            wall=Field("Rim wall height (60 to 1,000 km)",wall);
            gravity=Field("Spin acceleration (1 to 100 m/s2)",gravity);
            density=Field("Assumed floor mass/area (0 to 100,000,000 kg/m2)",density);
            GUI.enabled=true;
            if(!worldUnlocked)GUILayout.Label("World generation is locked after the first expedition to preserve ground beneath saved vessels. Use a new save for another world.");
            GUILayout.Label("Resolved seed: "+s.Geometry.P.Seed+" | terrain generation "+s.GenerationVersion);
            GUILayout.Label("Mass uses a uniform-ribbon approximation. Spin acceleration is not attraction.");
            day=Field("Shadow-square cycle (hours; minimum 1/60)",day);
            prediction=Field("Map coast duration (1 to 1,440 minutes)",prediction);
            trajectory=GUILayout.Toggle(trajectory,"Numerical map trajectory (vacuum coast)");
            warp=Field("Maximum stock warp on ring surface (10 to 10,000x)",warp);
            haze=Field("Atmospheric visual haze (0 to 2)",haze);
            cloud=Field("Weather baseline/cloudiness (0-100%; zero forces clear skies)",cloud);
            dynamicWeather=GUILayout.Toggle(dynamicWeather,"Evolving weather fronts");
            weatherPeriod=Field("Weather transition timescale (game hours; minimum 1/6)",weatherPeriod);
            weatherVariation=Field("Weather variation (0 fixed to 1 full sunny/stormy range)",weatherVariation);
            stormChance=Field("Storm fraction of weather range (0 to 1)",stormChance);
            cloudWind=Field("Visual cloud drift (0 to 100 m/s)",cloudWind);
            rainEnabled=GUILayout.Toggle(rainEnabled,"Rain visuals");rainDensity=Field("Rain density (0 to 1; scaled by graphics quality)",rainDensity);
            lightningEnabled=GUILayout.Toggle(lightningEnabled,"Storm lightning (individual flashes at 1x to 10x)");
            GUILayout.Label("Weather follows universal time, including stock warp. Zero cloud amount forces clear skies. Rain and lightning are visual; no wind force or lightning damage. High warp uses a rain veil instead of undersampled streaks/flashes.");
            if(GUILayout.Button("Apply settings"))
            {
                double r,h,f,d,a,c,di,wi,gr,wa,de,pr,wr,po,dd,ats,ex,ps,wp,wv,sc,cw,rd;int resolved;
                if(!Number(weatherPeriod,1.0/6,double.MaxValue/3600,out wp)||!Number(weatherVariation,0,1,out wv)||!Number(stormChance,0,1,out sc)||!Number(cloudWind,0,100,out cw)||!Number(rainDensity,0,1,out rd)||!Number(airSteps,16,96,out ats)||!Number(exposure,.25,2,out ex)||!Number(photoSamples,1,64,out ps)||!Number(range,200,double.MaxValue/1000,out r)||!Number(height,.25,3,out h)||!Number(forest,0,2,out f)||!Number(day,1.0/60,double.MaxValue/3600,out d)||!Number(haze,0,2,out a)||!Number(cloud,0,100,out c)||!Number(diameter,2000000,double.MaxValue/500,out di)||!Number(width,10000,di/2,out wi)||!Number(wall,60,1000,out wa)||!Number(gravity,1,100,out gr)||!Number(density,0,100000000,out de)||!Number(prediction,1,1440,out pr)||!Number(warp,10,10000,out wr)||!Number(ponds,0,2,out po)||!Number(detailDistance,25,250,out dd))
                {message="Enter finite numbers within the displayed ranges (use a decimal point).";return;}
                if(string.IsNullOrWhiteSpace(seed))resolved=worldUnlocked?BitConverter.ToInt32(Guid.NewGuid().ToByteArray(),0):s.Geometry.P.Seed;
                else if(!int.TryParse(seed,NumberStyles.Integer,CultureInfo.InvariantCulture,out resolved)){message="Seed must be a signed 32-bit integer or blank.";return;}
                var n=s.Save();
                n.SetValue("weatherPeriod",N(wp*3600),true);n.SetValue("weatherVariation",N(wv),true);n.SetValue("stormChance",N(sc),true);n.SetValue("cloudWind",N(cw),true);n.SetValue("rainDensity",N(rd),true);n.SetValue("rainEnabled",rainEnabled,true);n.SetValue("lightningEnabled",lightningEnabled,true);
                n.SetValue("forestQuality",forestQuality,true);n.SetValue("fullRingDetail",fullRingDetail,true);n.SetValue("visualQuality",visualQuality,true);n.SetValue("atmosphereSteps",(int)ats,true);n.SetValue("atmosphereExposure",N(ex),true);n.SetValue("photoSamples",(int)ps,true);
                n.SetValue("lodRange",N(r*1000));n.SetValue("lodResolution",new[]{8,16,32}[quality]);n.SetValue("generationBudget",budget+1);
                n.SetValue("haze",N(a));n.SetValue("cloudAmount",N(c/100));n.SetValue("dynamicWeather",dynamicWeather);
                n.SetValue("detailDistance",N(dd),true);n.SetValue("ambientParticles",particles,true);
                n.SetValue("daySeconds",N(d*3600));n.SetValue("predictionSeconds",N(pr*60));n.SetValue("surfaceWarpLimit",N(wr));n.SetValue("showTrajectory",trajectory);
                if(worldUnlocked){n.SetValue("radius",N(di*500));n.SetValue("width",N(wi*1000));n.SetValue("gravity",N(gr));n.SetValue("wallHeight",N(wa*1000));n.SetValue("surfaceDensity",N(de));n.SetValue("pondAmount",N(po));n.SetValue("seed",resolved);n.SetValue("heightMultiplier",N(h));n.SetValue("forestDensity",N(f));n.SetValue("generationVersion",4);}
                try{var candidate=Settings.Load();candidate.Apply(n);}catch(ArgumentException invalid){message=invalid.Message;return;}
                flight.ApplyOptions(n,worldUnlocked);seed=s.Geometry.P.Seed.ToString(CultureInfo.InvariantCulture);
                message="Applied. Save your game to persist these settings.";
            }
            GUILayout.Label(message);
        }
        private void DrawExtensions(RingworldFlight flight,Settings s)
        {
            GUILayout.Label("Mod extension settings");
            GUILayout.Label("Installed extensions are enabled by default. Your saved enable/disable choices are retained. Quality presets still control rendering cost; Slow and below use cloud layers only.");
            bool clouds=Extensions.ExtensionProviders.Clouds!=null,scattering=Extensions.ExtensionProviders.Scattering!=null;
            if(GUILayout.Button((cloudsOpen?"v ":"> ")+"Ringworld Clouds"+(clouds?"":" (not installed)")))cloudsOpen=!cloudsOpen;
            if(cloudsOpen){
                GUI.enabled=clouds;
                bool enabled=GUILayout.Toggle(cloudExtension,"Enabled");
                if(enabled!=cloudExtension){var n=s.Save();n.SetValue("cloudExtension",enabled,true);flight.ApplyOptions(n,false);return;}
                GUI.enabled=clouds&&cloudExtension;
                cloudMode=GUILayout.Toolbar(cloudMode,new[]{"Layers only","Economy","Balanced","Detailed"});
                GUILayout.Label("Volume density: "+cloudDensity.ToString("0.00"));cloudDensity=GUILayout.HorizontalSlider(cloudDensity,0,3);
                cloudSteps=Field("Cloud ray steps (32 to 256)",cloudSteps);
                cloudRange=Field("Volumetric cloud distance (km; minimum 30)",cloudRange);
                cloudShadow=Field("Cloud self-shadow strength (0 to 1)",cloudShadow);
                if(GUILayout.Button("Apply cloud settings")){
                    double steps,distance,shadow;
                    if(!Number(cloudSteps,32,256,out steps)||!Number(cloudRange,30,double.MaxValue/1000,out distance)||!Number(cloudShadow,0,1,out shadow))message="Enter cloud values within the displayed ranges.";
                    else{var n=s.Save();n.SetValue("cloudMode",cloudMode,true);n.SetValue("cloudDensity",N(cloudDensity),true);n.SetValue("cloudSteps",(int)steps,true);n.SetValue("cloudRange",N(distance*1000),true);n.SetValue("cloudShadow",N(shadow),true);flight.ApplyOptions(n,false);message="Cloud settings applied. Save your game to keep them.";}
                }
                GUI.enabled=true;
            }
            if(GUILayout.Button((scatteringOpen?"v ":"> ")+"Ringworld Scattering"+(scattering?"":" (not installed)")))scatteringOpen=!scatteringOpen;
            if(scatteringOpen){
                GUI.enabled=scattering;
                bool active=s.WaterExtension||s.FullRingAtmosphere;
                bool enabled=GUILayout.Toggle(active,"Enabled (water and distant atmosphere)");
                if(enabled!=active){var n=s.Save();n.SetValue("waterExtension",enabled,true);n.SetValue("fullRingAtmosphere",enabled,true);flight.ApplyOptions(n,false);return;}
                GUI.enabled=scattering&&active;
                fullRingAtmosphere=GUILayout.Toggle(fullRingAtmosphere,"Full-ring atmosphere (map, space and distant sky)");
                waterExtension=GUILayout.Toggle(waterExtension,"Enhanced water surface");
                waterScattering=GUILayout.Toggle(waterScattering,"Water light shafts (above and below surface)");
                waterQuality=GUILayout.Toolbar(waterQuality,new[]{"Flat","Ripples","Waves","Detailed","Ultra"});
                waveHeight=Field("Visual wave amplitude (0 to 2 m)",waveHeight);
                GUILayout.Label("Detailed/Ultra add refraction and absorption. Waves are visual; buoyancy uses mean water level. Disabling this extension preserves basic water and flight physics.");
                if(GUILayout.Button("Apply scattering settings")){
                    double amplitude;
                    if(!Number(waveHeight,0,2,out amplitude))message="Wave amplitude must be between 0 and 2 metres.";
                    else{var n=s.Save();n.SetValue("waterExtension",waterExtension,true);n.SetValue("fullRingAtmosphere",fullRingAtmosphere,true);n.SetValue("waterScattering",waterScattering,true);n.SetValue("waterQuality",waterQuality,true);n.SetValue("waveHeight",N(amplitude),true);flight.ApplyOptions(n,false);message="Scattering settings applied. Save your game to keep them.";}
                }
                GUI.enabled=true;
            }
            if(GUILayout.Button((cylaOpen?"v ":"> ")+"Cyla atmosphere"))cylaOpen=!cylaOpen;
            if(cylaOpen){
                bool enabled=GUILayout.Toggle(atmosphereBackend==1,"Enabled when installed and supported");
                if(enabled!=(atmosphereBackend==1)){var n=s.Save();n.SetValue("atmosphereBackend",enabled?1:0,true);flight.ApplyOptions(n,false);return;}
                var integrations=OptionalVisualIntegrations.Instance;
                if(integrations!=null){GUILayout.Label(integrations.TufxStatus);GUILayout.Label(integrations.ScattererStatus);}
                GUILayout.Label("Atmosphere follows the quality preset: Mid and above use installed Cyla automatically; Slow and below use Original. Missing or unsupported Cyla uses Original.");
                GUILayout.Label("Cyla render resolution (depth-aware foreground preservation)");cylaResolution=GUILayout.Toolbar(cylaResolution,new[]{"1/8","1/4","1/2","Full"});
                cylaLightSteps=Field("Cyla light integration steps (1 to 50)",cylaLightSteps);
                cylaDither=GUILayout.Toggle(cylaDither,"Cyla temporal dithering (disabled during photo capture)");
                GUILayout.Label("Cyla by Ghassen Lahmar (LGhassen / blackrack). GPLv3 plugin; see GameData/Cyla/License.md. Cyla has separate view steps under Advanced optics. Clouds and photo capture remain Ringworld systems. Missing/unsupported Cyla falls back to Original.");
                cylaAdvanced=GUILayout.Toggle(cylaAdvanced,"Advanced Cyla optics and diagnostics");
                if(cylaAdvanced){GUILayout.Label("Optical geometry only: radius is a precision-limited proxy; width follows the ring. Inner radius = proxy radius minus thickness. These controls do not change flight physics. Nonzero offsets/tilt deliberately misalign the optical cylinder.");for(int i=0;i<cylaFields.Length;i++)cylaFields[i]=Field(CylaOptions.Definitions[i].Label,cylaFields[i]);GUILayout.Label("Lighting boundary");cylaMode=GUILayout.Toolbar(cylaMode,new[]{"Top / side","Floor","Unlit"});}
    
                if(GUILayout.Button("Apply Cyla settings")){
                    double steps;
                    if(!Number(cylaLightSteps,1,50,out steps)){message="Cyla light steps must be between 1 and 50.";return;}
                    var n=s.Save();
                    for(int i=0;i<cylaFields.Length;i++){double value;var def=CylaOptions.Definitions[i];if(!Number(cylaFields[i],def.Min,def.Max,out value)){message="Invalid Cyla value: "+def.Label;return;}n.SetValue(def.Key,value.ToString("R",CultureInfo.InvariantCulture),true);}
                    n.SetValue("cylaLightingMode",cylaMode,true);n.SetValue("atmosphereBackend",atmosphereBackend,true);n.SetValue("cylaLightSteps",(int)steps,true);n.SetValue("cylaDivisor",new[]{8,4,2,1}[cylaResolution],true);n.SetValue("cylaDither",cylaDither,true);flight.ApplyOptions(n,false);message="Cyla settings applied. Save your game to keep them.";
                }
            }
            GUILayout.Label(message);
        }
        private static bool Number(string text,double min,double max,out double value)
        {return double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&!double.IsNaN(value)&&value>=min&&value<=max;}
    }
}

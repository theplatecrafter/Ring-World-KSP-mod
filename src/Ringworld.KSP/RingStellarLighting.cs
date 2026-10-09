using System;
using System.Collections.Generic;
using HarmonyLib;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    // Kopernicus may keep a constant-intensity Kerbol light alongside an added
    // star. Scope physical attenuation/direction to ring camera renders; neither
    // star configuration nor other vessels' lighting is changed permanently.
    [KSPAddon(KSPAddon.Startup.Flight,false)]
    public sealed class RingStellarLighting : MonoBehaviour
    {
        private sealed class Source {internal CelestialBody Body;internal Light Local,Scaled;internal double AU;internal float LocalNominal,ScaledNominal;}
        private struct Pose {internal Light Light;internal float Intensity,Bias,NormalBias;internal int Mask;internal Quaternion Rotation;}
        private sealed class Scope {internal readonly List<Pose> Poses=new List<Pose>();internal float DeferredAmbient,ShadowDistance,Split2;internal Vector3 Split4;internal ShadowProjection Projection;}
        private readonly List<Source> sources=new List<Source>();
        private readonly Dictionary<Camera,Scope> scopes=new Dictionary<Camera,Scope>();
        private readonly Dictionary<Light,float> baseline=new Dictionary<Light,float>();
        private Light habitat;
        private int bodyCount=-1;
        internal static int RenderScopes;
#if RINGWORLD_SMOKE_TEST
        internal static bool NativeShadowComparison;
#else
        private const bool NativeShadowComparison=false;
#endif
        public void Start(){Camera.onPreCull+=Prepare;Camera.onPostRender+=Restore;}
        private void Discover()
        {
            sources.Clear();bodyCount=FlightGlobals.Bodies.Count;
            foreach(var sun in UnityEngine.Object.FindObjectsOfType<Sun>())
            {
                if(sun.sun==null)continue;
                var local=sun.GetComponent<Light>();
                var scaled=AccessTools.Field(typeof(Sun),"scaledSunLight")?.GetValue(sun) as Light;
                double au=13599840256;
                var shifter=AccessTools.Field(sun.GetType(),"shifter")?.GetValue(sun);
                var value=shifter==null?null:AccessTools.Field(shifter.GetType(),"au")?.GetValue(shifter);
                if(value!=null)au=Convert.ToDouble(value);
                if(!RingParameters.Finite(au)||au<=0)au=13599840256;
                var localCurve=shifter==null?null:AccessTools.Field(shifter.GetType(),"intensityCurve")?.GetValue(shifter) as FloatCurve;
                var scaledCurve=shifter==null?null:AccessTools.Field(shifter.GetType(),"scaledIntensityCurve")?.GetValue(shifter) as FloatCurve;
                sources.Add(new Source{Body=sun.sun,Local=local,Scaled=scaled,AU=au,
                    LocalNominal=localCurve!=null?localCurve.Evaluate((float)au):local==null?0:local.intensity,
                    ScaledNominal=scaledCurve!=null?scaledCurve.Evaluate((float)(au/ScaledSpace.ScaleFactor)):scaled==null?0:scaled.intensity});
            }
        }
        private void Prepare(Camera camera)
        {
            var f=RingworldFlight.Instance;
            if(f==null||!f.FrameInUse||MapView.MapIsEnabled||scopes.ContainsKey(camera))return;
            if((camera.cullingMask&((1<<15)|(1<<10)))==0)return;
            if(bodyCount!=FlightGlobals.Bodies.Count)Discover();
            var scope=new Scope{DeferredAmbient=Shader.GetGlobalFloat("deferredAmbientBrightness"),ShadowDistance=QualitySettings.shadowDistance,Split2=QualitySettings.shadowCascade2Split,Split4=QualitySettings.shadowCascade4Split,Projection=QualitySettings.shadowProjection};scopes.Add(camera,scope);
            // Deferred's planetary probe-derived diffuse light samples our
            // emissive scaled hull/sky rather than a planetary environment.
            // Keep the user's stock ambient term and specular reflections, but
            // exclude this inappropriate diffuse contribution during ring views.
            Shader.SetGlobalFloat("deferredAmbientBrightness",0);
            var poses=scope.Poses;
            double now=Planetarium.GetUniversalTime();var p=f.Position(FlightGlobals.ActiveVessel);
            if(!NativeShadowComparison&&(camera.cullingMask&(1<<15))!=0){
                // A planet-scale shadow range wastes atlas precision on this
                // locally flat habitat. Keep the user's resolution/cascade count.
                var cameraPoint=ConvertVector.Core((Vector3d)camera.transform.position-f.Center);
                var chart=f.Settings.Geometry.Coordinates(cameraPoint);double floor=f.Settings.Terrain.Sample(chart.Along,chart.Across).Height;
                double height=Math.Max(0,chart.Altitude-floor);
                QualitySettings.shadowDistance=Math.Min(scope.ShadowDistance,(float)Math.Min(6000,600+height*2));
                QualitySettings.shadowProjection=ShadowProjection.StableFit;
                if(QualitySettings.shadowCascades==2)QualitySettings.shadowCascade2Split=.2f;
                if(QualitySettings.shadowCascades==4)QualitySettings.shadowCascade4Split=new Vector3(.04f,.14f,.4f);
            }
            var illuminator=RingLighting.Illuminator(f.Settings);
            double rotation=f.Settings.Geometry.P.Omega*now-f.Settings.Geometry.OrientationRadians;
            var observer=f.Settings.Geometry.RotateAroundAxis(p,rotation)+f.Settings.AnchorAt(now).Position;
            bool nativeKey=false;
            foreach(var source in sources)
            {
                RingAnchorState star;string error;
                if(!RingAnchorEphemeris.TryRelative(RingAnchorEphemeris.Id(source.Body),f.Star,now,out star,out error))continue;
                var toStar=star.Position-observer;double distance=toStar.Length;
                if(!(distance>0)||!RingParameters.Finite(distance))continue;
                double factor=source.Body==illuminator?RingLighting.Visibility(f.Settings,p,now):Math.Min(1,source.AU*source.AU/(distance*distance));
                var direction=ConvertVector.Unity(f.Settings.Geometry.RotateAroundAxis(-toStar.Unit,-rotation));
                bool primary=source.Body==illuminator;
                nativeKey|=primary&&source.Local!=null&&source.Local.enabled&&(source.Local.cullingMask&(1<<15))!=0;
                Adjust(source.Local,poses,(float)factor,direction,primary,source.LocalNominal);
                Adjust(source.Scaled,poses,(float)factor,direction,primary,source.ScaledNominal);
            }
            // Prefer one native key light across vessel and terrain layers, so
            // vessels/trees retain cross-layer shadows. The habitat light is a
            // fallback, not an additional sun.
            if(habitat==null){var obj=GameObject.Find("Ringworld habitat sunlight");if(obj!=null)habitat=obj.GetComponent<Light>();}
            if(nativeKey&&habitat!=null){Save(habitat,poses);habitat.intensity=0;}
            RenderScopes++;
        }
        private void Adjust(Light light,List<Pose> poses,float factor,Vector3 direction,bool primary,float nominal)
        {
            if(light==null)return;
            // Scaled light can be a child of the local light. Save local rotation
            // so restoring its parent does not apply the old orientation twice.
            Save(light,poses);
            float raw;if(!baseline.TryGetValue(light,out raw)){raw=light.intensity;baseline[light]=raw;}
            // Cap secondary-star contribution; do not attenuate an already dim
            // custom intensity curve a second time.
            // The stock Sun's horizon fade assumes a spherical main body.
            // Ring visibility and the configured nominal stellar intensity are
            // the common source of illumination for all local cameras instead.
            light.intensity=primary?Math.Max(0,nominal)*factor:Math.Min(raw,Math.Max(0,nominal)*factor);
            light.transform.rotation=Quaternion.LookRotation(direction);
            if(primary&&!NativeShadowComparison&&(light.cullingMask&(1<<15))!=0){light.shadowBias=Math.Min(light.shadowBias,.05f);light.shadowNormalBias=Math.Min(light.shadowNormalBias,.2f);}
        }
        private static void Save(Light light,List<Pose> poses){poses.Add(new Pose{Light=light,Intensity=light.intensity,Mask=light.cullingMask,Rotation=light.transform.localRotation,Bias=light.shadowBias,NormalBias=light.shadowNormalBias});}
        private void Restore(Camera camera)
        {
            Scope scope;if(camera==null||!scopes.TryGetValue(camera,out scope))return;var poses=scope.Poses;
            for(int i=poses.Count-1;i>=0;i--){var p=poses[i];if(p.Light!=null){p.Light.intensity=p.Intensity;p.Light.cullingMask=p.Mask;p.Light.transform.localRotation=p.Rotation;p.Light.shadowBias=p.Bias;p.Light.shadowNormalBias=p.NormalBias;}}
            Shader.SetGlobalFloat("deferredAmbientBrightness",scope.DeferredAmbient);
            QualitySettings.shadowDistance=scope.ShadowDistance;QualitySettings.shadowCascade2Split=scope.Split2;QualitySettings.shadowCascade4Split=scope.Split4;QualitySettings.shadowProjection=scope.Projection;
            scopes.Remove(camera);if(scopes.Count==0)baseline.Clear();
        }
        public void OnDisable(){foreach(var camera in new List<Camera>(scopes.Keys))Restore(camera);}
        public void OnDestroy(){Camera.onPreCull-=Prepare;Camera.onPostRender-=Restore;OnDisable();}
    }
}

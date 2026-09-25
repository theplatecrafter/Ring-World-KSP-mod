#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using System.IO;
using HarmonyLib;
using UnityEngine;
namespace NivenRingworld
{
    internal static class IntegrationSmoke
    {
        internal static IEnumerator Run(RingworldFlight f,Action<string> fail)
        {
            f.arrivalHeight=100;f.Visit();while(!f.Ready)yield return null;
            for(int i=0;i<45;i++)yield return null;
            Time.timeScale=0;
            var service=OptionalVisualIntegrations.Instance;
            if(service==null){fail("Integration service missing");yield break;}
            RingworldEnvironmentState environment;
            if(!RingworldSurfaceApi.TryGetEnvironmentState(FlightGlobals.ActiveVessel,out environment)||!environment.HasAtmosphere||environment.PressureKPa<1||environment.Density<=0||double.IsNaN(environment.Mach))
            {fail("Environment API did not provide ring air");yield break;}
            if(RingworldSurfaceApi.TryGetEnvironmentState(null,out environment)){fail("Environment API accepted null vessel");yield break;}
            Debug.Log("[RingworldSmoke] INTEGRATIONS environment API ring air and null scope passed");
            if(OptionalVisualIntegrations.FindType("Deferred.Deferred")!=null)
            {
                var path=FlightCamera.fetch.mainCamera.actualRenderingPath;
                Debug.Log("[RingworldSmoke] INTEGRATIONS Deferred camera="+path);
                if(path!=RenderingPath.DeferredShading){fail("Installed Deferred did not activate on flight camera");yield break;}
            }
            if(OptionalVisualIntegrations.FindType("Waterfall.ModuleWaterfallFX")!=null)CheckWaterfallControllers();
            CheckWarpTables(f);
            var type=OptionalVisualIntegrations.FindType("TUFX.TexturesUnlimitedFXLoader");
            if(type!=null)
            {
                var instance=AccessTools.Field(type,"INSTANCE").GetValue(null);
                var current=AccessTools.Field(type,"currentProfile");
                var name=AccessTools.Property(OptionalVisualIntegrations.FindType("TUFX.TUFXProfile"),"ProfileName");
                Debug.Log("[RingworldSmoke] INTEGRATIONS "+service.TufxStatus+" / "+service.ScattererStatus);
                if((string)name.GetValue(current.GetValue(instance),null)!="NivenRingworld-Economy"){fail("Automatic TUFX Economy profile did not activate");yield break;}
                MapView.EnterMapView();for(int i=0;i<5;i++)yield return null;
                if(((string)name.GetValue(current.GetValue(instance),null)).StartsWith("NivenRingworld-")){fail("Ring TUFX profile leaked into map");yield break;}
                MapView.ExitMapView();for(int i=0;i<5;i++)yield return null;
                if((string)name.GetValue(current.GetValue(instance),null)!="NivenRingworld-Economy"){fail("TUFX profile failed to return after map");yield break;}
                // A cheap optical probe exercises ordering without raising terrain preset.
                f.Settings.AtmosphereBackend=1;f.Settings.CylaDivisor=8;f.Settings.CylaLightSteps=1;f.Settings.CloudAmount=0;
                var optics=f.Settings.Save();optics.SetValue("cylaViewSteps",16,true);f.Settings.Cyla.Load(optics);
                for(int i=0;i<15;i++)yield return null;
                var frame=AccessTools.Field(typeof(RingVisualRenderer),"postProcessedFrame");
                yield return new WaitForEndOfFrame();
                if((int)frame.GetValue(f.visuals)!=Time.frameCount){fail("TUFX did not receive the ring visual pass");yield break;}
                ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"Ringworld-TUFX-integration.png"));
                var available=(IDictionary)AccessTools.Property(type,"Profiles").GetValue(instance,null);
                var scene=Enum.Parse(OptionalVisualIntegrations.FindType("TUFX.TUFXScene"),"Flight");
                AccessTools.Method(type,"ApplyProfile").Invoke(instance,new[]{available["Default-Empty"],scene});
                for(int i=0;i<4;i++)yield return null;
                if((string)name.GetValue(current.GetValue(instance),null)!="Default-Empty"){fail("TUFX adapter overwrote a manual selection");yield break;}
                Debug.Log("[RingworldSmoke] INTEGRATIONS TUFX automatic entry, map isolation, render ordering and manual selection passed");
            }
            else Debug.Log("[RingworldSmoke] INTEGRATIONS optional TUFX absent; no dependency required");
            var flareType=OptionalVisualIntegrations.FindType("Scatterer.SunFlare");
            if(flareType!=null)
            {
                var all=UnityEngine.Object.FindObjectsOfType(flareType);object flare=null;
                foreach(var candidate in all)if((CelestialBody)AccessTools.Field(flareType,"source").GetValue(candidate)==f.Star){flare=candidate;break;}
                if(flare==null){fail("Scatterer host-star flare not found");yield break;}
                var material=(Material)AccessTools.Field(flareType,"sunglareMaterial").GetValue(flare);
                double saved=Planetarium.GetUniversalTime();
                var geometry=f.Settings.Geometry;var camera=FlightCamera.fetch.mainCamera;
                var c=geometry.Coordinates(ConvertVector.Core((Vector3d)camera.transform.position-f.Center));
                float[] values=new float[2];
                for(int j=0;j<2;j++)
                {
                    double phase=j==0?.1:.75;
                    Planetarium.SetUniversalTime((20*c.Along/geometry.P.Circumference-phase)*geometry.P.DaySeconds);
                    material.SetFloat("renderSunFlare",1);
                    AccessTools.Method(typeof(OptionalVisualIntegrations),"ScattererFlare").Invoke(null,new[]{flare});
                    values[j]=material.GetFloat("renderSunFlare");
                }
                Planetarium.SetUniversalTime(saved);
                Debug.Log("[RingworldSmoke] INTEGRATIONS Scatterer panel night/day="+values[0]+"/"+values[1]);
                if(values[0]!=0||values[1]<.99){fail("Scatterer panel occlusion incorrect");yield break;}
                MapView.EnterMapView();for(int i=0;i<2;i++)yield return null;
                material.SetFloat("renderSunFlare",.7f);AccessTools.Method(typeof(OptionalVisualIntegrations),"ScattererFlare").Invoke(null,new[]{flare});
                if(Math.Abs(material.GetFloat("renderSunFlare")-.7f)>.001){fail("Scatterer map flare altered");yield break;}
                MapView.ExitMapView();
            }
            Debug.Log("[RingworldSmoke] PASS optional visual integrations");Time.timeScale=1;
        }
        private static void CheckWaterfallControllers()
        {
            var part=FlightGlobals.ActiveVessel.rootPart;
            var module=part.AddModule("ModuleWaterfallFX");
            try
            {
                foreach(string name in new[]{"AtmosphereDensityController","MachController"})
                {
                    var type=OptionalVisualIntegrations.FindType("Waterfall."+name);var controller=Activator.CreateInstance(type);
                    AccessTools.Method(type,"Initialize").Invoke(controller,new object[]{module});
                    float value=(float)AccessTools.Method(type,"UpdateSingleValue").Invoke(controller,null);
                    double expected=name=="MachController"?part.vessel.mach:Math.Pow(part.atmDensity,Convert.ToDouble(AccessTools.Field(OptionalVisualIntegrations.FindType("Waterfall.Settings"),"AtmosphereDensityExponent").GetValue(null)));
                    if(double.IsNaN(value)||Math.Abs(value-expected)>.001)throw new Exception("Waterfall ring controller mismatch: "+name);
                    Debug.Log("[RingworldSmoke] INTEGRATIONS Waterfall "+name+"="+value+" inputDensity="+part.atmDensity);
                }
            }
            finally{part.RemoveModule(module);}
        }
        private static void CheckWarpTables(RingworldFlight flight)
        {
            if(RingSurfaceWarp.AllowedIndex(new[]{1f,17f,250f,4000f},3,500)!=2||
                RingSurfaceWarp.AllowedIndex(new[]{1f,float.NaN,float.PositiveInfinity},2,500)!=0||
                RingSurfaceWarp.AllowedIndex(new[]{1f,5f},99,1)!=0)
                throw new Exception("Custom warp table limit failed");
            var type=OptionalVisualIntegrations.FindType("BetterTimeWarp.BetterTimeWarp");if(type==null)return;
            var instance=AccessTools.Field(type,"Instance").GetValue(null);
            var selected=AccessTools.Field(type,"CurrentWarp").GetValue(instance);
            var ratesType=OptionalVisualIntegrations.FindType("BetterTimeWarp.TimeWarpRates");
            var rates=new[]{1f,3f,17f,250f,4000f,20000f,100000f,1000000f};
            var custom=Activator.CreateInstance(ratesType,new object[]{"Ringworld validation",rates,false,false,4});
            var method=AccessTools.Method(type,"SetWarpRates");var original=TimeWarp.fetch.warpRates;
            float previousScale=Time.timeScale;
            try
            {
                method.Invoke(instance,new[]{custom,(object)false});
                if(TimeWarp.fetch.warpRates[2]!=17)throw new Exception("BetterTimeWarp custom rates not applied");
                if(RingSurfaceWarp.AllowedIndex(TimeWarp.fetch.warpRates,7,500)!=3)throw new Exception("BetterTimeWarp ceiling ignored");
                Time.timeScale=1;TimeWarp.SetRate(2,true);
                if(TimeWarp.CurrentRateIndex!=0)throw new Exception("BetterTimeWarp allowed airborne ring rails warp");
                Debug.Log("[RingworldSmoke] INTEGRATIONS BetterTimeWarp custom table, rate ceiling and airborne gate passed: "+flight.surfaceWarp.Status);
            }
            finally
            {
                TimeWarp.SetRate(0,true);Time.timeScale=previousScale;
                if(selected!=null)method.Invoke(instance,new[]{selected,(object)false});
                TimeWarp.fetch.warpRates=original;
            }
        }
    }
}
#endif

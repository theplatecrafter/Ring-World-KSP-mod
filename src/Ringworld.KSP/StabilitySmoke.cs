#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using HarmonyLib;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    [HarmonyPatch(typeof(Part),"explode",new Type[]{typeof(float)})]
    internal static class StabilityExplosionTrace
    {
        private static bool logged;
        private static void Prefix(Part __instance)
        {
            if(logged&&(__instance.vessel==null||__instance.vessel.vesselName!="SunkWorks ring water regression"))return;logged=true;
            Debug.Log("[RingworldSmoke] EXPLOSION TRACE temp="+__instance.temperature+" skin="+__instance.skinTemperature+" packed="+(__instance.vessel==null?false:__instance.vessel.packed)+" "+Environment.StackTrace);
        }
    }
    internal static class StabilitySmoke
    {
        internal static IEnumerator Run(RingworldFlight f,Action<string> fail)
        {
            var v=FlightGlobals.ActiveVessel;int parts=v.parts.Count;var id=v.id;
            f.arrivalHeight=40;f.Visit();while(!f.Ready)yield return null;
            CheatOptions.NoCrashDamage=false;CheatOptions.UnbreakableJoints=false;
            float end=Time.realtimeSinceStartup+180;
            while(!v.Landed&&Time.realtimeSinceStartup<end)
            {
                v.SetWorldVelocity(ConvertVector.Ksp(f.Settings.Geometry.Up(f.Position(v))*-.5));yield return new WaitForFixedUpdate();
            }
            end=Time.realtimeSinceStartup+60;
            while(!f.surfaceWarp.CanAdvance(f)&&Time.realtimeSinceStartup<end)yield return new WaitForSecondsRealtime(.25f);
            if(!v.Landed||v.parts.Count!=parts||!f.surfaceWarp.CanAdvance(f)){fail("Damage-enabled landing/settling failed: "+f.surfaceWarp.Status+" parts="+v.parts.Count);yield break;}
            PauseMenu.Display();yield return new WaitForSecondsRealtime(1);
            bool quick=(bool)AccessTools.Method(typeof(QuickSaveLoad),"QuickSaveClearToSave").Invoke(null,null);
            Debug.Log("[RingworldSmoke] PAUSED SAVE timeScale="+Time.timeScale+" menu="+PauseMenu.canSaveAndExit+" quick="+quick);
            if(Time.timeScale!=0||PauseMenu.canSaveAndExit!=ClearToSaveStatus.CLEAR||!quick){PauseMenu.Close();fail("Real pause-menu save denied");yield break;}
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(KSPUtil.ApplicationRootPath,"RingworldPausedSave.png"));yield return new WaitForSecondsRealtime(.3f);PauseMenu.Close();
            foreach(int rate in new[]{10,100,1000,100,1000})
            {
                end=Time.realtimeSinceStartup+30;
                while(!f.surfaceWarp.CanAdvance(f)&&Time.realtimeSinceStartup<end)yield return new WaitForSecondsRealtime(.25f);
                var root=(Vector3d)v.transform.position-f.Star.position;double ut=Planetarium.GetUniversalTime();
                f.surfaceWarp.Rate=rate;yield return new WaitForSecondsRealtime(2);
                if(!v.packed||TimeWarp.CurrentRate<rate){fail("Stock warp failed to engage at "+rate);yield break;}
                // Exercise the exact conic evaluation stock packed update/save uses.
                var pos=v.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());var vel=v.orbit.getOrbitalVelocityAtUT(Planetarium.GetUniversalTime());
                if(!RingParameters.Finite(pos.magnitude)||!RingParameters.Finite(vel.magnitude)||vel.magnitude<100){fail("Packed orbit invalid/zeroed");yield break;}
                f.Capture();GamePersistence.SaveGame(HighLogic.CurrentGame.Updated(),"warp-check",HighLogic.SaveFolder,SaveMode.OVERWRITE);
                f.surfaceWarp.Rate=1;while(v.packed)yield return null;yield return new WaitForSeconds(3);
                double drift=((Vector3d)v.transform.position-f.Star.position-root).magnitude;
                Debug.Log("[RingworldSmoke] DAMAGE WARP rate="+rate+" seconds="+(Planetarium.GetUniversalTime()-ut)+" drift="+drift+" parts="+v.parts.Count+" speed="+f.Velocity(v).Length);
                if(v.state==Vessel.State.DEAD||v.parts.Count!=parts||drift>1||f.Velocity(v).Length>1){fail("Damage-enabled warp lost craft stability");yield break;}
            }
            PauseMenu.Display();yield return new WaitForSecondsRealtime(.3f);
            if(PauseMenu.canSaveAndExit!=ClearToSaveStatus.CLEAR){PauseMenu.Close();fail("Pause save rejected after warp");yield break;}
            var savedRoot=(Vector3d)v.transform.position-f.Star.position;var folder=HighLogic.SaveFolder;
            GamePersistence.SaveGame(HighLogic.CurrentGame.Updated(),"persistent",folder,SaveMode.OVERWRITE);PauseMenu.Close();
            HighLogic.LoadScene(GameScenes.SPACECENTER);while(HighLogic.LoadedScene!=GameScenes.SPACECENTER)yield return null;yield return new WaitForSecondsRealtime(5);
            var reload=GamePersistence.LoadGame("persistent",folder,true,false);int index=reload.flightState.protoVessels.FindIndex(p=>p.vesselID==id);
            if(index<0){fail("Resident missing after paused save");yield break;}
            FlightDriver.StartAndFocusVessel(reload,index);while(!HighLogic.LoadedSceneIsFlight||RingworldFlight.Instance==null||!RingworldFlight.Instance.Ready)yield return null;
            yield return new WaitForSeconds(5);f=RingworldFlight.Instance;v=FlightGlobals.ActiveVessel;
            double error=((Vector3d)v.transform.position-f.Star.position-savedRoot).magnitude;
            Debug.Log("[RingworldSmoke] PAUSED SAVE RELOAD parts="+v.parts.Count+" error="+error+" landed="+v.Landed);
            if(v.id!=id||!v.Landed||v.parts.Count!=parts||error>1){fail("Paused save scene roundtrip lost resident");yield break;}
        }
    }
}
#endif

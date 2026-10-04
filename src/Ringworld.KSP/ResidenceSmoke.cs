#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using HarmonyLib;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    internal static class ResidenceSmoke
    {
        internal static IEnumerator Run(RingworldFlight f,Action<string> fail)
        {
            var v=FlightGlobals.ActiveVessel;
            f.arrivalHeight=400000;f.Visit();while(!f.Ready)yield return null;
            v.SetWorldVelocity(ConvertVector.Ksp(f.Settings.Geometry.Up(f.Position(v))*-1000));f.Leave();
            MapView.EnterMapView();yield return new WaitForSeconds(1);PlanetariumCamera.fetch.SetDistance(25000);yield return new WaitForSeconds(8);
            Debug.Log("[RingworldSmoke] ENCOUNTER count="+f.trajectory.EncounterCount+" "+f.trajectory.Status);
            if(f.trajectory.EncounterCount<1){fail("Ring entry marker missing");yield break;}
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(KSPUtil.ApplicationRootPath,"RingworldEncounter.png"));yield return new WaitForSeconds(1);MapView.ExitMapView();
            // Wall rendering has a separate regression suite; the scaled root
            // no longer owns a renderer after camera-relative precision work.
            f.arrivalHeight=8;f.Visit();while(!f.Ready)yield return null;
            yield return new WaitForSeconds(15);
            float settleEnd=Time.realtimeSinceStartup+45;
            while((!v.Landed||!f.surfaceWarp.CanAdvance(f))&&Time.realtimeSinceStartup<settleEnd)yield return new WaitForSecondsRealtime(.25f);
            if(!v.Landed||FlightGlobals.ClearToSave(false)!=ClearToSaveStatus.CLEAR){fail("Resident is not saveable: "+f.surfaceWarp.Status+" landed="+v.Landed+" speed="+f.Velocity(v).Length);yield break;}
            var module=v.rootPart.FindModuleImplementing<ModuleScienceExperiment>();
            if(module==null)module=(ModuleScienceExperiment)v.rootPart.AddModule("ModuleScienceExperiment");
            module.experimentID="crewReport";module.experiment=ResearchAndDevelopment.GetExperiment("crewReport");
            AccessTools.Field(typeof(ModuleScienceExperiment),"situation").SetValue(module,ScienceUtil.GetExperimentSituation(v));
            var routine=(IEnumerator)AccessTools.Method(typeof(ModuleScienceExperiment),"OnScienceCompleteDelay").Invoke(module,null);
            while(routine.MoveNext())yield return routine.Current;
            var subject=(ScienceSubject)AccessTools.Field(typeof(ModuleScienceExperiment),"subject").GetValue(module);
            if(subject==null||!subject.id.Contains("SrfLandedRingworldV2_")){fail("Stock landed science not separated: "+(subject==null?"null":subject.id));yield break;}
            Debug.Log("[RingworldSmoke] STOCK LANDED SCIENCE "+subject.id);
            foreach(var dialog in UnityEngine.Object.FindObjectsOfType<KSP.UI.Screens.Flight.Dialogs.ExperimentsResultDialog>())UnityEngine.Object.Destroy(dialog.gameObject);
            yield return GroundScienceSmoke.Run(f,fail);
            // Test per-vessel save permission against an unsafe neighbour. Deployed
            // parts are physically anchored, so use the throttle guard rather than
            // injecting a velocity that their rigidbody constraints can discard.
            float saveDeadline=Time.realtimeSinceStartup+30;
            while(FlightGlobals.ClearToSave(false)!=ClearToSaveStatus.CLEAR&&Time.realtimeSinceStartup<saveDeadline)yield return new WaitForFixedUpdate();
            var neighbour=FlightGlobals.FindVessel(GroundScienceSmoke.Ids[0]);
            float previousThrottle=neighbour.ctrlState.mainThrottle;
            bool warpBlocked,saveAllowed;
            try
            {
                neighbour.ctrlState.mainThrottle=1;
                warpBlocked=!f.surfaceWarp.CanAdvance(f);
                saveAllowed=FlightGlobals.ClearToSave(false)==ClearToSaveStatus.CLEAR;
            }
            finally {neighbour.ctrlState.mainThrottle=previousThrottle;}
            if(!warpBlocked||!saveAllowed){fail("Unsafe neighbour save isolation failed: warpBlocked="+warpBlocked+" saveAllowed="+saveAllowed);yield break;}
            Debug.Log("[RingworldSmoke] RESIDENCE unsafe neighbour blocks warp but permits settled active vessel save");
            f.Capture();var id=v.id;var position=f.Position(v);var folder=HighLogic.SaveFolder;
            GamePersistence.SaveGame(HighLogic.CurrentGame.Updated(),"persistent",folder,SaveMode.OVERWRITE);
            HighLogic.LoadScene(GameScenes.SPACECENTER);while(HighLogic.LoadedScene!=GameScenes.SPACECENTER)yield return null;
            yield return new WaitForSecondsRealtime(5);
            var reload=GamePersistence.LoadGame("persistent",folder,true,false);int index=reload.flightState.protoVessels.FindIndex(p=>p.vesselID==id);
            if(index<0){fail("Resident vessel lost from save");yield break;}
            foreach(var partId in GroundScienceSmoke.Ids)if(reload.flightState.protoVessels.FindIndex(p=>p.vesselID==partId)<0){fail("Deployed part missing from save");yield break;}
            FlightDriver.StartAndFocusVessel(reload,index);
            while(!HighLogic.LoadedSceneIsFlight||RingworldFlight.Instance==null||!RingworldFlight.Instance.Ready)yield return null;
            yield return new WaitForSeconds(8);f=RingworldFlight.Instance;v=FlightGlobals.ActiveVessel;
            double error=(f.Position(v)-position).Length;
            if(!v.Landed||error>5){fail("Residence scene reload failed: "+error);yield break;}
            foreach(var partId in GroundScienceSmoke.Ids)
            {
                var partVessel=FlightGlobals.FindVessel(partId);
                if(partVessel==null||!f.Owns(partVessel)||!partVessel.Landed){fail("Deployed part did not restore beside resident");yield break;}
            }
            var restoredControl=FlightGlobals.FindVessel(GroundScienceSmoke.Ids[0]).rootPart.FindModuleImplementing<ModuleGroundExpControl>();
            var restoredCluster=restoredControl.ScienceClusterData;
            if(restoredCluster==null||!restoredCluster.IsPowered||restoredCluster.PowerAvailable<2||restoredCluster.PowerRequired!=2){fail("Deployed power state did not survive reload");yield break;}
            Debug.Log("[RingworldSmoke] RESIDENCE + DEPLOYABLES KSC roundtrip error="+error+" parts="+GroundScienceSmoke.Ids.Count);
        }
    }
}
#endif

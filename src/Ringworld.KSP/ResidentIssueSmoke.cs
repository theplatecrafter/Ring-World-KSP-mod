#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using HarmonyLib;
using Ringworld.Core;
using UnityEngine;
using KSP.UI.Screens.Mapview.MapContextMenuOptions;
namespace NivenRingworld
{
    internal static class ResidentIssueSmoke
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        internal static IEnumerator Run(RingworldFlight f)
        {
            var v=FlightGlobals.ActiveVessel;int count=v.parts.Count;
            CheatOptions.NoCrashDamage=false;CheatOptions.UnbreakableJoints=false;
            f.gentleArrival=true;f.arrivalHeight=1000;f.Visit();while(!f.Ready)yield return null;f.gentleArrival=false;
            float end=Time.realtimeSinceStartup+60;
            while((!v.Landed||f.Velocity(v).Length>.25)&&Time.realtimeSinceStartup<end)yield return new WaitForFixedUpdate();
            Check(v.Landed&&v.parts.Count==count&&f.Velocity(v).Length<=.25,"Gentle placement failed to settle intact: "+f.Velocity(v).Length+" parts="+v.parts.Count+"/"+count);
            Debug.Log("[RingworldSmoke] PASS gentle arrival overrides 1000 m, intact parts="+count+" speed="+f.Velocity(v).Length+" damage enabled");
            yield return Science(f,v);
            var copy=new ConfigNode("VESSEL");v.BackupVessel().Save(copy);
            var parts=copy.GetNodes("PART");var crew=HighLogic.CurrentGame.CrewRoster.GetNewKerbal(ProtoCrewMember.KerbalType.Crew);crew.rosterStatus=ProtoCrewMember.RosterStatus.Assigned;
            bool seated=false;foreach(var p in parts){bool had=p.HasValue("crew");p.RemoveValues("crew");if(had&&!seated){p.AddValue("crew",crew.name);seated=true;}p.SetValue("uid",ShipConstruction.GetUniqueFlightID(HighLogic.CurrentGame.flightState),true);p.SetValue("persistentId",FlightGlobals.GetUniquepersistentId(),true);}
            Check(seated,"Resident clone has no crewed command part");
            var g=f.Settings.Geometry;var chart=g.Coordinates(f.Position(v));
            double height=RingArrivalPlacement.Height(f.Settings,v,chart.Along,chart.Across+180,ConvertVector.Unity(g.Up(f.Position(v))));
            var pos=g.Position(chart.Along,chart.Across+180,height);var orbit=new Orbit();double dt=Planetarium.GetUniversalTime()-f.FrameEpoch;
            orbit.UpdateFromStateVectors(ConvertVector.Orbit(ConvertVector.Ksp(g.ToInertialPosition(pos,dt))),ConvertVector.Orbit(ConvertVector.Ksp(g.ToInertialVelocity(pos,new DVec(),dt))),f.Star,Planetarium.GetUniversalTime());
            var node=ProtoVessel.CreateVesselNode("Multipart resident issue regression",v.vesselType,orbit,0,parts);node.SetValue("sit","SUB_ORBITAL",true);
            node.SetValue("lastUT",Planetarium.GetUniversalTime(),true);
            node.SetValue("rot",KSPUtil.WriteQuaternion(v.transform.rotation),true);
            string id=Guid.Parse(node.GetValue("pid")).ToString();var record=new VesselRecord{Id=id,RingId=f.Settings.RingId,Position=pos,Epoch=f.FrameEpoch,Rotation=v.transform.rotation,Landed=true};RingworldScenario.Instance.Vessels[id]=record;
            var neighbour=HighLogic.CurrentGame.AddVessel(node).vesselRef;end=Time.realtimeSinceStartup+60;
            neighbour.Load();while(!neighbour.loaded&&Time.realtimeSinceStartup<end)yield return null;
            RingVesselPose.Set(neighbour,f.Center+ConvertVector.Ksp(pos),v.transform.rotation);neighbour.SetWorldVelocity(Vector3d.zero);
            record.Restored=true;while(neighbour.packed&&Time.realtimeSinceStartup<end){RingResidence.HoldSaved(neighbour,record);neighbour.GoOffRails();yield return null;}
            record.Landed=false;neighbour.Landed=false;Physics.SyncTransforms();
            pos=RingArrivalPlacement.Refine(f.Settings,neighbour,f.Center,pos);neighbour.SetPosition(f.Center+ConvertVector.Ksp(pos),true);record.Position=pos;
            Physics.SyncTransforms();RingCollisionFrame.Reset(neighbour);
            while((!neighbour.loaded||neighbour.packed||!neighbour.Landed||f.Velocity(neighbour).Length>.25)&&Time.realtimeSinceStartup<end)yield return new WaitForFixedUpdate();
            Check(neighbour.loaded&&!neighbour.packed&&neighbour.Landed&&neighbour.parts.Count==count&&f.Velocity(neighbour).Length<=.25,"Multipart resident did not settle intact: loaded="+neighbour.loaded+" packed="+neighbour.packed+" landed="+neighbour.Landed+" parts="+neighbour.parts.Count+"/"+count+" speed="+f.Velocity(neighbour).Length);
            // Move only the observer, leaving the neighbour to stock pack/unpack
            // and the ring contact solver. No stabilizing force or pose hold.
            var home=ConvertVector.Core((Vector3d)v.transform.position-f.Center);var baseline=f.Position(neighbour);
            Debug.Log("[RingworldSmoke] RANGE native landed unpack="+neighbour.vesselRanges.landed.unpack+" loaded distance="+(neighbour.transform.position-v.transform.position).magnitude);
            int cycles=Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-repo-local-actions-only")>=0?0:2;
            for(int cycle=0;cycle<cycles;cycle++){
                var far=home+g.AlongDirection(home)*20000+g.Up(home)*20000;v.SetPosition(f.Center+ConvertVector.Ksp(far),true);v.SetWorldVelocity(Vector3d.zero);RingCollisionFrame.Reset(v);
                end=Time.realtimeSinceStartup+30;while(neighbour.loaded&&Time.realtimeSinceStartup<end)yield return null;
                Check(!neighbour.loaded,"Observer failed to cross the stock unloading range");
                for(int step=40;step>=0;step--){v.SetPosition(f.Center+ConvertVector.Ksp(home+g.AlongDirection(home)*step*500+g.Up(home)*step*500),true);v.SetWorldVelocity(Vector3d.zero);RingCollisionFrame.Reset(v);yield return new WaitForSecondsRealtime(.2f);}
                end=Time.realtimeSinceStartup+30;while((!neighbour.loaded||neighbour.packed)&&Time.realtimeSinceStartup<end)yield return null;
                for(int i=0;i<100;i++)yield return new WaitForFixedUpdate();
                RingResidentRestore.Trace("range assertion",f,neighbour,record);
                Check(neighbour.loaded&&!neighbour.packed&&neighbour.parts.Count==count&&f.Velocity(neighbour).Length<2&&(f.Position(neighbour)-baseline).Length<3,"Multipart neighbour drift/breakup on cycle "+cycle+" parts="+neighbour.parts.Count+" speed="+f.Velocity(neighbour).Length+" drift="+(f.Position(neighbour)-baseline).Length);
                Debug.Log("[RingworldSmoke] PASS stock physics-range cycle="+cycle+" parts="+count+" neighbour drift="+(f.Position(neighbour)-baseline).Length);
            }
            end=Time.realtimeSinceStartup+30;while(FlightGlobals.ClearToSave(false)!=ClearToSaveStatus.CLEAR&&Time.realtimeSinceStartup<end)yield return new WaitForFixedUpdate();
            Check(FlightGlobals.ClearToSave(false)==ClearToSaveStatus.CLEAR,"Source resident cannot be saved for native map switch");
            HighLogic.CurrentGame.Parameters.Flight.CanSwitchVesselsFar=true;neighbour.DiscoveryInfo.SetLevel(DiscoveryLevels.Owned);
            MapView.EnterMapView();yield return new WaitForSecondsRealtime(1);
            AccessTools.Method(typeof(FocusObject),"OnSelect").Invoke(new FocusObject(neighbour.orbitDriver),null);
            end=Time.realtimeSinceStartup+30;while(FlightGlobals.ActiveVessel!=neighbour&&Time.realtimeSinceStartup<end)yield return null;
            Check(FlightGlobals.ActiveVessel==neighbour&&!MapView.MapIsEnabled,"Native map Focus option failed to switch to resident");
            Debug.Log("[RingworldSmoke] PASS native map FocusObject switches to other landed resident");
            var cabin=neighbour.parts.Find(p=>p.protoModuleCrew.Count>0);var eva=FlightEVA.fetch.spawnEVA(cabin.protoModuleCrew[0],cabin,cabin.airlock,true);
            Check(eva!=null,"EVA hatch blocked");end=Time.realtimeSinceStartup+20;
            while((eva.vessel==null||eva.vessel.packed||FlightGlobals.ActiveVessel!=eva.vessel)&&Time.realtimeSinceStartup<end)yield return null;
            Check(eva.vessel!=null&&f.Owns(eva.vessel),"EVA did not inherit ring frame");
            end=Time.realtimeSinceStartup+30;
            while(eva.fsm.CurrentState==eva.st_ladder_acquire&&Time.realtimeSinceStartup<end)yield return new WaitForFixedUpdate();
            Check(eva.fsm.CurrentState!=eva.st_ladder_acquire,"EVA ladder acquisition never completed");
            if(eva.OnALadder)eva.fsm.RunEvent(eva.On_ladderLetGo);
            bool contact=false;double peak=0;for(int i=0;i<1200;i++){
                Check(eva.part.State!=PartStates.DEAD,"EVA died after hatch release");peak=Math.Max(peak,f.Velocity(eva.vessel).Length);contact|=RingEva.Grounded(eva);yield return new WaitForFixedUpdate();
            }
            Check(contact&&f.Velocity(eva.vessel).Length<2,"EVA fails to settle after natural contact: "+eva.fsm.CurrentState.name+" peak="+peak);
            // Stock active EVA recovery requires movement input (tgtRpos != 0).
            // Supply ordinary W input and let the native FSM recover; never
            // directly run its recovery event or force a walking state.
            SmokeTest.WalkingEva=eva;SmokeTest.WalkInput=Vector2.up;
            for(int i=0;i<100;i++)yield return new WaitForFixedUpdate();
            Check(!eva.isRagdoll,"EVA remains ragdoll despite ordinary movement input: "+eva.fsm.CurrentState.name);
            var start=f.Position(eva.vessel);
            try{for(int i=0;i<200;i++)yield return new WaitForFixedUpdate();}finally{SmokeTest.WalkingEva=null;}
            double walked=(f.Position(eva.vessel)-start).Length;Check(walked>1&&walked<30&&!eva.isRagdoll,"Natural EVA recovery does not permit walking: "+walked);
            Debug.Log("[RingworldSmoke] PASS natural EVA hatch release/contact/walk distance="+walked+" peak="+peak+" no recovery event injected");
        }
        static IEnumerator Science(RingworldFlight f,Vessel v)
        {
            var host=v.rootPart;var biome=(ModuleBiomeScanner)host.AddModule("ModuleBiomeScanner");biome.FixedUpdate();biome.RunAnalysis();Check(biome.Events["RunAnalysis"].active,"Stock biome action unavailable");
            RingworldSurfaceState surface;Check(RingworldSurfaceApi.TryGetSurfaceState(v,out surface),"Stock biome fixture lacks surface context");
            var message=ScreenMessages.Instance.ActiveMessages.FindLast(m=>m.message.Contains(surface.RingName+" / ")&&m.message.Contains("biome: "+surface.Biome));
            Check(message!=null,"Stock biome scanner did not display ring name/biome");
            Debug.Log("[RingworldSmoke] PASS actual stock biome analysis message="+message.message);host.RemoveModule(biome);
            var experiment=(ModuleScienceExperiment)host.AddModule("ModuleScienceExperiment");experiment.experimentID="crewReport";experiment.experiment=ResearchAndDevelopment.GetExperiment("crewReport");
            AccessTools.Field(typeof(ModuleScienceExperiment),"situation").SetValue(experiment,ScienceUtil.GetExperimentSituation(v));
            yield return (IEnumerator)AccessTools.Method(typeof(ModuleScienceExperiment),"OnScienceCompleteDelay").Invoke(experiment,null);
            var subject=(ScienceSubject)AccessTools.Field(typeof(ModuleScienceExperiment),"subject").GetValue(experiment);Check(subject!=null&&subject.id.Contains("RingworldV2_")&&subject.title.Contains("Ringworld /"),"Stock crew report lacks ring/location subject");
            Debug.Log("[RingworldSmoke] PASS stock crew report subject="+subject.id+" title="+subject.title);host.RemoveModule(experiment);
            foreach(var dialog in UnityEngine.Object.FindObjectsOfType<KSP.UI.Screens.Flight.Dialogs.ExperimentsResultDialog>())UnityEngine.Object.Destroy(dialog.gameObject);
            ConfigNode config=null;foreach(var part in PartLoader.LoadedPartsList){if(part==null||part.partConfig==null)continue;foreach(var n in part.partConfig.GetNodes("MODULE"))if(n.GetValue("name")=="SCANresourceDisplay"){config=n.CreateCopy();break;}if(config!=null)break;}
            Check(config!=null,"Installed SCANsat local resource module missing");config.SetValue("ResourceName","Ore",true);var scan=host.AddModule(config);scan.OnStart(PartModule.StartState.Flying);
            AccessTools.Method(scan.GetType(),"EnableModule").Invoke(scan,null);AccessTools.Method(scan.GetType(),"FixedUpdate").Invoke(scan,null);AccessTools.Method(scan.GetType(),"Update").Invoke(scan,null);
            double abundance;Check(RingworldResourceApi.TryGetAbundance(v,"Ore",out abundance),"No ring Ore");var text=(string)scan.Fields["abundance"].GetValue(scan);
            Check(text.Contains(abundance.ToString("P2"))&&text.Contains(f.Settings.Terrain.Sample(f.Settings.Geometry.Coordinates(f.Position(v)).Along,f.Settings.Geometry.Coordinates(f.Position(v)).Across).Biome.ToString()),"SCANsat local display retained the host/biome lock: "+text);
            Debug.Log("[RingworldSmoke] PASS installed SCANsat local Ore/biome display="+text+" (orbital map not supported)");host.RemoveModule(scan);
        }
    }
}
#endif

#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using HarmonyLib;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    internal static class AquaticCraftIssueSmoke
    {
        static string Diagnostics(Vessel v){string info="";foreach(var p in v.parts)info+="; "+p.partInfo.name+" mass="+(p.rb==null?0:p.rb.mass)+" cube="+p.DragCubes.WeightedSize+" scale="+p.transform.lossyScale+" buoyancy="+p.buoyancy+" portion="+p.submergedPortion+" component="+(p.partBuoyancy!=null)+" enabled="+(p.partBuoyancy!=null&&p.partBuoyancy.enabled)+" lift="+(p.partBuoyancy==null?0:p.partBuoyancy.effectiveForce.magnitude)+" ballast="+(p.Resources["IntakeLqd"]==null?0:p.Resources["IntakeLqd"].amount);return info;}
        internal static IEnumerator Run(RingworldFlight f)
        {
            var g=f.Settings.Geometry;var current=g.Coordinates(f.Position(FlightGlobals.ActiveVessel));
            double along=0,across=0,sea=0;bool found=false;
            for(int i=0;i<40000&&!found;i++){
                double a=current.Along+(i%200-100)*500,b=current.Across+(i/200-100)*500;var s=f.Settings.Terrain.Sample(a,b);
                if(s.Wet&&s.WaterHeight-s.Height>50){along=a;across=b;sea=s.WaterHeight;found=true;}
            }
            if(!found)throw new Exception("Representative boat needs deep water");
            var pos=g.Position(along,across,sea+1.6);var up=g.Up(pos);var heading=g.AlongDirection(pos);
            var hull=ProtoVessel.CreatePartNode("wbiProceduralBoatHull",ShipConstruction.GetUniqueFlightID(HighLogic.CurrentGame.flightState));
            var motor=ProtoVessel.CreatePartNode("wbiEbbTide",ShipConstruction.GetUniqueFlightID(HighLogic.CurrentGame.flightState));
            motor.SetValue("parent",0,true);motor.SetValue("position","0,-5,1.8",true);motor.SetValue("rotation","0,0,0,1",true);
            var pilot=HighLogic.CurrentGame.CrewRoster.GetNewKerbal(ProtoCrewMember.KerbalType.Crew);pilot.rosterStatus=ProtoCrewMember.RosterStatus.Assigned;
            var cabin=ProtoVessel.CreatePartNode("mk1pod.v2",ShipConstruction.GetUniqueFlightID(HighLogic.CurrentGame.flightState),new[]{pilot});
            cabin.SetValue("parent",0,true);cabin.SetValue("position","0,0,-1",true);cabin.SetValue("rotation","-0.70710678,0,0,0.70710678",true);
            var orbit=new Orbit();double dt=Planetarium.GetUniversalTime()-f.FrameEpoch;
            orbit.UpdateFromStateVectors(ConvertVector.Orbit(ConvertVector.Ksp(g.ToInertialPosition(pos,dt))),ConvertVector.Orbit(ConvertVector.Ksp(g.ToInertialVelocity(pos,new DVec(),dt))),f.Star,Planetarium.GetUniversalTime());
            var node=ProtoVessel.CreateVesselNode("SunkWorks ring water regression",VesselType.Ship,orbit,0,new[]{hull,motor,cabin});
            node.SetValue("lastUT",Planetarium.GetUniversalTime(),true);
            node.SetValue("sit","SUB_ORBITAL",true);node.SetValue("landed",false,true);node.SetValue("splashed",false,true);
            string id=Guid.Parse(node.GetValue("pid")).ToString();
            var rotation=Quaternion.LookRotation(ConvertVector.Unity(-up),ConvertVector.Unity(heading));
            var record=new VesselRecord{Id=id,RingId=f.Settings.RingId,Position=pos,Epoch=f.FrameEpoch,Rotation=rotation,Landed=true};
            RingworldScenario.Instance.Vessels[id]=record;
            // A synthetic proto starts in stock inertial coordinates. Explicitly
            // load and place this test craft once in the current rotating frame;
            // all subsequent flotation/sinking is ordinary unconstrained physics.
            var observer=FlightGlobals.ActiveVessel;observer.SetPosition(f.Center+ConvertVector.Ksp(pos+heading*100),true);observer.SetWorldVelocity(Vector3d.zero);RingCollisionFrame.Reset(observer);
            RingResidence.UpdateBookkeeping(observer,f.Settings,f.Star,f.Position(observer),new DVec(),f.FrameEpoch);
            var proto=HighLogic.CurrentGame.AddVessel(node);var vessel=proto.vesselRef;float deadline=Time.realtimeSinceStartup+45;
            vessel.Load();while(!vessel.loaded&&Time.realtimeSinceStartup<deadline)yield return null;
            RingVesselPose.Set(vessel,f.Center+ConvertVector.Ksp(pos),rotation);vessel.SetWorldVelocity(Vector3d.zero);
            record.Restored=true;FlightGlobals.ForceSetActiveVessel(vessel);
            while(vessel.packed&&Time.realtimeSinceStartup<deadline){RingResidence.HoldSaved(vessel,record);vessel.GoOffRails();yield return null;}
            record.Landed=false;vessel.Landed=false;vessel.Splashed=false;Physics.SyncTransforms();RingCollisionFrame.Reset(vessel);
            if(!vessel.loaded||vessel.packed||!f.Owns(vessel))throw new Exception("Actual SunkWorks boat failed to load into ring frame: loaded="+vessel.loaded+" packed="+vessel.packed+" body="+vessel.mainBody.bodyName);
            // Procedural ballast capacity is normally saved by the editor.
            // Run that native calculation once for this directly spawned craft.
            foreach(var module in vessel.rootPart.Modules)if(module.GetType().Name=="WBIBallastCalculator")AccessTools.Method(module.GetType(),"UpdateBallastCapacity").Invoke(module,null);
            var power=new ConfigNode("RESOURCE");power.AddValue("name","ElectricCharge");power.AddValue("amount",10000);power.AddValue("maxAmount",10000);vessel.rootPart.AddResource(power);
            FlightGlobals.ForceSetActiveVessel(vessel);CheatOptions.NoCrashDamage=false;CheatOptions.UnbreakableJoints=false;
            int count=vessel.parts.Count;if(count!=3)throw new Exception("Boat fixture lost its actual hull/motor/cabin parts: "+count);
            float end=Time.realtimeSinceStartup+45;double largestSpeed=0;double startHeight=g.Coordinates(f.Position(vessel)).Altitude;
            while(Time.realtimeSinceStartup<end){
                if(vessel==null||vessel.parts.Count!=count)throw new Exception("Balanced SunkWorks boat lost parts while floating");
                double speed=f.Velocity(vessel).Length;largestSpeed=Math.Max(largestSpeed,speed);
                double height=g.Coordinates(f.Position(vessel)).Altitude;if(double.IsNaN(height)||Math.Abs(height-sea)>15)throw new Exception("Balanced boat left the water: "+height+" sea="+sea+Diagnostics(vessel));
                yield return new WaitForFixedUpdate();
            }
            double upright=Vector3.Dot(-vessel.rootPart.transform.forward,ConvertVector.Unity(g.Up(f.Position(vessel))));
            if(!vessel.Splashed||f.Velocity(vessel).Length>2||upright<.866||vessel.rootPart.rb.angularVelocity.magnitude>.15)throw new Exception("Actual boat did not settle upright: speed="+f.Velocity(vessel).Length+" splash="+vessel.Splashed+" upright="+upright+" angular="+vessel.rootPart.rb.angularVelocity.magnitude);
            Debug.Log("[RingworldSmoke] PASS actual uncalibrated crewed SunkWorks hull/motor/cabin float 45 s: speed="+f.Velocity(vessel).Length+" peak="+largestSpeed+" altitude="+startHeight+" -> "+g.Coordinates(f.Position(vessel)).Altitude);
            var ballastType=AccessTools.TypeByName("SunkWorks.Submarine.WBIBallastTank");PartModule ballast=null;
            foreach(var m in vessel.rootPart.Modules)if(m.GetType()==ballastType)ballast=m;
            if(ballast==null)throw new Exception("Actual hull lacks ballast module");
            var amount=vessel.rootPart.Resources["IntakeLqd"];if(amount==null||amount.maxAmount<=0)throw new Exception("Actual hull lacks ballast capacity");
            var state=AccessTools.Field(ballastType,"ventState");state.SetValue(ballast,Enum.Parse(state.FieldType,"FloodingBallast"));
            double initial=amount.amount,mass=vessel.GetTotalMass(),heightBefore=g.Coordinates(f.Position(vessel)).Altitude;
            end=Time.realtimeSinceStartup+40;
            while(Time.realtimeSinceStartup<end&&g.Coordinates(f.Position(vessel)).Altitude>heightBefore-3)yield return new WaitForFixedUpdate();
            double sunk=g.Coordinates(f.Position(vessel)).Altitude;
            if(amount.amount<=initial||vessel.GetTotalMass()<=mass||sunk>heightBefore-3)throw new Exception("Actual ballast failed to submerge boat: resource="+initial+" -> "+amount.amount+" capacity="+amount.maxAmount+" mass="+mass+" -> "+vessel.GetTotalMass()+" height="+heightBefore+" -> "+sunk);
            state.SetValue(ballast,Enum.Parse(state.FieldType,"Closed"));
            Debug.Log("[RingworldSmoke] PASS actual ballast mass-flow sinks hull: resource="+initial+" -> "+amount.amount+" mass="+mass+" -> "+vessel.GetTotalMass()+" height="+heightBefore+" -> "+sunk);
            ModuleEngines engine=null;foreach(var p in vessel.parts)foreach(var m in p.Modules)if(m.GetType().Name=="WBIAquaticEngine")engine=(ModuleEngines)m;
            if(engine==null)throw new Exception("Actual motor lacks aquatic engine");
            var electric=vessel.rootPart.Resources["ElectricCharge"];double energy=electric.amount;
            FlightInputCallback throttle=s=>s.mainThrottle=.25f;vessel.OnFlyByWire+=throttle;engine.Activate();
            try{
                for(int i=0;i<50;i++){vessel.ctrlState.mainThrottle=.25f;FlightInputHandler.state.mainThrottle=.25f;yield return new WaitForFixedUpdate();}
                if(engine.finalThrust<=.01||electric.amount>=energy)throw new Exception("Actual wet motor produced no thrust/flow: "+engine.finalThrust+" EC="+energy+" -> "+electric.amount+" flameout="+engine.flameout);
                Debug.Log("[RingworldSmoke] PASS actual aquatic engine wet thrust="+engine.finalThrust+" kN / EC used="+(energy-electric.amount));
                var wet=f.Position(vessel);vessel.SetPosition(f.Center+ConvertVector.Ksp(wet+g.Up(wet)*100),true);vessel.SetWorldVelocity(Vector3d.zero);RingCollisionFrame.Reset(vessel);
                for(int i=0;i<10;i++)yield return new WaitForFixedUpdate();
                if(engine.finalThrust>.01)throw new Exception("Actual aquatic motor continued thrust with a dry nozzle");
                Debug.Log("[RingworldSmoke] PASS actual aquatic engine dry-nozzle shutdown");
            }finally{vessel.OnFlyByWire-=throttle;engine.Shutdown();FlightInputHandler.state.mainThrottle=0;vessel.ctrlState.mainThrottle=0;}
            Debug.Log("[RingworldSmoke] PASS issue 4 actual craft flotation / ballast sinking / wet and dry motor operation");
        }
    }
}
#endif

#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using System.Linq;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
namespace NivenRingworld
{
    internal static class SolverEngineSmoke
    {
        static PartModule observed;
        static bool captured,oxygen,underwater;
        static double altitude;
        static int solverTicks;
        static void Capture(PartModule __instance,double __1,bool __5,bool __6)
        {if(__instance!=observed)return;solverTicks++;captured=true;altitude=__1;oxygen=__5;underwater=__6;}
        static void Check(bool condition,string text){if(!condition)throw new Exception(text);}
        static double Fuel(Vessel v,string[] names){double amount=0;foreach(var p in v.parts)foreach(var name in names){var r=p.Resources[name];if(r!=null)amount+=r.amount;}return amount;}
        internal static void ConfigureCraftFuel(string path)
        {
            if(AccessTools.TypeByName("RealFuels.Tanks.ModuleFuelTanks")==null)return;
            // Configure the copied stock aircraft as a player would in the RF
            // editor. Use native persisted tank nodes; do not inject an engine
            // or bypass upstream propellant/resource calculations at runtime.
            var craft=ConfigNode.Load(path);int configured=0;
            foreach(var part in craft.GetNodes("PART")){
                string name=part.GetValue("part");int suffix=name.LastIndexOf('_');if(suffix>=0)name=name.Substring(0,suffix);
                var info=PartLoader.getPartInfoByName(name);if(info==null)continue;
                var tank=info.partConfig.GetNodes("MODULE").FirstOrDefault(n=>n.GetValue("name")=="ModuleFuelTanks");
                if(tank==null||tank.GetValue("type")!="Fuselage"||!part.GetNodes("RESOURCE").Any(n=>n.GetValue("name")=="LiquidFuel"))continue;
                double volume=double.Parse(tank.GetValue("volume"),CultureInfo.InvariantCulture);
                foreach(var old in part.GetNodes("MODULE").Where(n=>n.GetValue("name")=="ModuleFuelTanks").ToArray())part.RemoveNode(old);
                foreach(var old in part.GetNodes("RESOURCE").Where(n=>n.GetValue("name")=="LiquidFuel").ToArray())part.RemoveNode(old);
                var saved=part.AddNode("MODULE");saved.AddValue("name","ModuleFuelTanks");saved.AddValue("type","Fuselage");saved.AddValue("volume",volume);
                var fuel=saved.AddNode("TANK");fuel.AddValue("name","Kerosene");fuel.AddValue("amount",volume*.8);fuel.AddValue("maxAmount",volume*.8);configured++;
                // Part resource state is saved separately from the RF tank
                // metadata. Both are present in a normal editor-saved craft.
                var resource=part.AddNode("RESOURCE");resource.AddValue("name","Kerosene");resource.AddValue("amount",volume*.8);resource.AddValue("maxAmount",volume*.8);resource.AddValue("flowState",true);
            }
            Check(configured>0,"RF aircraft fixture has no configurable native fuselage tank");craft.Save(path);
            Debug.Log("[RingworldSmoke] RF stock-aircraft fixture configured native Fuselage/Kerosene tanks="+configured);
        }
        internal static IEnumerator RunPlane(RingworldFlight f)
        {
            f.gentleArrival=true;f.arrivalHeight=1000;f.Visit();float end=Time.realtimeSinceStartup+90;
            while(!f.Ready&&Time.realtimeSinceStartup<end)yield return null;Check(f.Ready,"Native plane could not enter ring");f.gentleArrival=false;
            var v=FlightGlobals.ActiveVessel;var jets=v.FindPartModulesImplementing<ModuleEngines>().FindAll(m=>m.GetType().FullName.Contains("AJE"));Check(jets.Count>0,"Stock Aeris 3A has no installed AJE engines");
            var intakes=v.FindPartModulesImplementing<ModuleResourceIntake>();foreach(var inlet in intakes)inlet.Activate();
            var harmony=new Harmony("NivenRingworld.Test.NativePlaneSolver");observed=jets[0];captured=false;solverTicks=0;
            var method=AccessTools.Method(observed.GetType(),"UpdateSolver");harmony.Patch(AccessTools.DeclaredMethod(method.DeclaringType,method.Name),prefix:new HarmonyMethod(typeof(SolverEngineSmoke),nameof(Capture)));
            var fuelNames=jets.SelectMany(j=>j.propellants).Select(p=>p.name).Where(n=>n!="IntakeAir"&&n!="ElectricCharge"&&PartResourceLibrary.Instance.GetDefinition(n).density>0).Distinct().ToArray();
            Check(fuelNames.Length>0,"Native AJE engines have no configured fuel propellants");
            double before=Fuel(v,fuelNames);float throttle=v.ctrlState.mainThrottle;
            FlightInputCallback pilot=controls=>controls.mainThrottle=1f;v.OnFlyByWire+=pilot;
            try{
                v.ActionGroups.SetGroup(KSPActionGroup.Brakes,true);
                foreach(var jet in jets){jet.thrustPercentage=100;jet.Activate();}v.ctrlState.mainThrottle=1f;FlightInputHandler.state.mainThrottle=1f;
                for(int i=0;i<300;i++){v.ctrlState.mainThrottle=1f;FlightInputHandler.state.mainThrottle=1f;yield return new WaitForFixedUpdate();}
                double thrust=0;foreach(var jet in jets)thrust+=jet.finalThrust;
                var systemType=AccessTools.TypeByName("SolverEngines.SolverFlightSys");var system=v.GetComponent(systemType);
                string diagnostic="";foreach(var jet in jets){var solver=AccessTools.Field(jet.GetType(),"engineSolver").GetValue(jet);diagnostic+=" ["+jet.status+" ignited="+jet.EngineIgnited+" flameout="+jet.flameout+" throttle="+jet.currentThrottle+" requested="+jet.requestedThrottle+" needArea="+AccessTools.Field(jet.GetType(),"Need_Area").GetValue(jet)+" solverN="+AccessTools.Method(solver.GetType(),"GetThrust").Invoke(solver,null)+" flow="+AccessTools.Method(solver.GetType(),"GetFuelFlow").Invoke(solver,null)+"]";}
                var actualSolver=AccessTools.Field(jets[0].GetType(),"engineSolver").GetValue(jets[0]);diagnostic+=" solverStatus="+AccessTools.Method(actualSolver.GetType(),"GetStatus").Invoke(actualSolver,null)+" running="+AccessTools.Field(actualSolver.GetType(),"running").GetValue(actualSolver)+" ff="+AccessTools.Field(actualSolver.GetType(),"ffFraction").GetValue(actualSolver)+" underwater="+underwater;
                foreach(var name in new[]{"ambientTherm","inletTherm"}){var therm=AccessTools.Field(jets[0].GetType(),name).GetValue(jets[0]);diagnostic+=" "+name+"P="+AccessTools.Property(therm.GetType(),"P").GetValue(therm,null)+" T="+AccessTools.Property(therm.GetType(),"T").GetValue(therm,null);}
                diagnostic+=" flowMult="+AccessTools.Field(jets[0].GetType(),"flowMult").GetValue(jets[0])+" multFlow="+jets[0].multFlow+" multIsp="+jets[0].multIsp;
                diagnostic+=" solverTicks="+solverTicks+" enginePower="+v.VesselValues.EnginePower.value+" density="+v.atmDensity+" temp="+v.atmosphericTemperature+" kPa="+v.staticPressurekPa;
                Check(captured&&oxygen&&!underwater&&thrust>0&&Fuel(v,fuelNames)<before,"Native Aeris AJE burn failed: oxygen="+oxygen+" thrust="+thrust+" fuels="+string.Join(",",fuelNames)+" initial="+before+" used="+(before-Fuel(v,fuelNames))+" area="+AccessTools.Property(systemType,"InletArea").GetValue(system,null)+" TPR="+AccessTools.Property(systemType,"OverallTPR").GetValue(system,null)+" cachedParts="+AccessTools.Field(systemType,"partsCount").GetValue(system)+" actualParts="+v.parts.Count+diagnostic);
                RingworldSurfaceState state;Check(RingworldSurfaceApi.TryGetSurfaceState(v,out state)&&Math.Abs(altitude-state.Altitude)<2,"Native plane solver altitude mismatch");
                Check(!v.mainBody.atmosphereContainsOxygen,"Native plane test mutated host oxygen");
                Debug.Log("[RingworldSmoke] PASS native Aeris 3A / AJE / FAR ring burn: engines="+jets.Count+" thrust="+thrust+" fuels="+string.Join(",",fuelNames)+" fuel used="+(before-Fuel(v,fuelNames))+" local altitude="+altitude+" host oxygen=false");
            }finally{v.OnFlyByWire-=pilot;foreach(var jet in jets)jet.Shutdown();v.ctrlState.mainThrottle=throttle;FlightInputHandler.state.mainThrottle=throttle;harmony.UnpatchAll(harmony.Id);observed=null;}
        }
        internal static IEnumerator Run(RingworldFlight f)
        {
            var type=AccessTools.TypeByName("SolverEngines.AJEInlet");if(type==null)yield break;
            var v=FlightGlobals.ActiveVessel;var host=v.rootPart;
            var inlet=new GameObject("RingSolverInlet").transform;inlet.SetParent(host.transform.Find("model")??host.transform,false);
            PartModule intake=null;

            try{
                ConfigNode cfg=null;foreach(var node in PartLoader.getPartInfoByName("airScoop").partConfig.GetNodes("MODULE"))if(node.GetValue("name")=="AJEInlet")cfg=node.CreateCopy();
                Check(cfg!=null,"Real AJE inlet config missing");cfg.SetValue("intakeTransformName",inlet.name,true);
                intake=host.AddModule(cfg);intake.OnStart(PartModule.StartState.Flying);((ModuleResourceIntake)intake).Activate();
                for(int i=0;i<5;i++)yield return new WaitForFixedUpdate();
                AccessTools.DeclaredMethod(type,"FixedUpdate").Invoke(intake,null);
                Check((bool)AccessTools.Method(type,"IntakeActive").Invoke(intake,null),"Native AJE inlet incorrectly rejects ring atmosphere");
                Check((float)AccessTools.Method(type,"UsableArea").Invoke(intake,null)>0,"Native AJE inlet has no usable area");
                bool atmosphere=f.Settings.Atmosphere;
                try{f.Settings.Atmosphere=false;Check(!(bool)AccessTools.Method(type,"IntakeActive").Invoke(intake,null),"AJE inlet ignores disabled atmosphere");}
                finally{f.Settings.Atmosphere=atmosphere;}
                var registration=RingworldScenario.Instance.Vessels[v.id.ToString()];RingworldScenario.Instance.Vessels.Remove(v.id.ToString());
                try{Check(RingSolverEngineCompatibility.Altitude(v)==v.altitude,"Non-ring SolverEngines altitude changed");}
                finally{RingworldScenario.Instance.Vessels[v.id.ToString()]=registration;}
                Debug.Log("[RingworldSmoke] PASS native AJE inlet area/atmosphere gate and non-ring altitude scope; ignited engines use the separate real-aircraft regression");
            }finally{if(intake!=null)host.RemoveModule(intake);UnityEngine.Object.Destroy(inlet.gameObject);}
        }
    }
}
#endif

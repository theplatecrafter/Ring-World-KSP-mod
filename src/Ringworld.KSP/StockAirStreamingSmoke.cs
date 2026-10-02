#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
namespace NivenRingworld
{
 internal static class StockAirStreamingSmoke
 {
  static void Check(bool value,string text){if(!value)throw new Exception(text);}
  internal static IEnumerator Run(RingworldFlight f)
  {
   var v=FlightGlobals.ActiveVessel;var host=v.rootPart;
   bool originalOxygen=v.mainBody.atmosphereContainsOxygen,originalAtmosphere=v.mainBody.atmosphere;
   var resource=new ConfigNode("RESOURCE");resource.AddValue("name","IntakeAir");resource.AddValue("amount",0);resource.AddValue("maxAmount",10);host.AddResource(resource);
   var info=PartLoader.getPartInfoByName("airScoop");Check(info!=null,"Stock airScoop missing");
   ConfigNode config=null;foreach(var node in info.partConfig.GetNodes("MODULE"))if(node.GetValue("name")=="ModuleResourceIntake")config=node.CreateCopy();
   Check(config!=null,"Stock intake module config missing");
   var inlet=new GameObject("RingStockIntakeRegression").transform;inlet.SetParent(host.transform.Find("model")??host.transform,false);
   config.SetValue("intakeTransformName",inlet.name,true);
   var module=(ModuleResourceIntake)host.AddModule(config);module.OnStart(PartModule.StartState.Flying);module.Activate();
   for(int i=0;i<5;i++)yield return new WaitForFixedUpdate();
   module.FixedUpdate();
   double expectedSpeed=System.Math.Max(0,UnityEngine.Vector3.Dot((UnityEngine.Vector3)RingIntakeEnvironment.Direction(v),inlet.forward))*RingIntakeEnvironment.Speed(v)+module.intakeSpeed;
   Check(System.Math.Abs(module.airSpeedGui-expectedSpeed)<System.Math.Max(.1,expectedSpeed*.001),"Stock intake uses host-relative speed");
   Check(v.atmDensity>.1&&v.staticPressurekPa>1,"Ring atmosphere fields missing");
   Check(module.airFlow>0&&host.Resources["IntakeAir"].amount>0,"Stock intake did not produce IntakeAir in ring air");
   bool atmosphere=f.Settings.Atmosphere;
   try{f.Settings.Atmosphere=false;Check(!RingIntakeEnvironment.Oxygen(v.mainBody,module),"Oxygen supplied with ring atmosphere disabled");}
   finally{f.Settings.Atmosphere=atmosphere;}
   Check(v.mainBody.atmosphereContainsOxygen==originalOxygen&&v.mainBody.atmosphere==originalAtmosphere,"Intake adapter changed the host body's atmosphere");
   Debug.Log("[RingworldSmoke] STOCK AIR actual airScoop ModuleResourceIntake produces IntakeAir; disabled air rejects oxygen; host flags unchanged");
   Check(RingIntakeEnvironment.Gravity(v.GetWorldPos3D(),module).magnitude>1,"Gravity instrument lacks ring acceleration");
   Check(double.IsPositiveInfinity(RingIntakeEnvironment.SensorRadius(v.mainBody,module)),"Gravity instrument retains solar-distance cutoff");
   var engine=(ModuleEngines)host.AddModule("ModuleEngines");engine.thrustTransforms=new List<Transform>{inlet};
   Check(!(bool)AccessTools.Method(typeof(ModuleEngines),"CheckTransformsUnderwater").Invoke(engine,null),"Stock engine nozzle considered submerged in ring air");
   host.RemoveModule(engine);
   Debug.Log("[RingworldSmoke] STOCK ENV ring-relative intake speed, local gravity instrument and dry stock engine nozzle passed");
   host.RemoveModule(module);UnityEngine.Object.Destroy(inlet.gameObject);
   // Low-cost terrain-only fixture: initial complete layout, then a tile crossing.
   var options=f.Settings.Save();options.SetValue("lodRange",200000,true);options.SetValue("lodResolution",8,true);options.SetValue("generationBudget",1,true);options.SetValue("forestDensity",0,true);
   var settings=Settings.Load();settings.Apply(options);
   var mat=new Material(Shader.Find("Standard"));var lod=new TerrainLod(settings,mat,mat,mat);
   try{
    var c=settings.Geometry.Coordinates(f.Position(v));
    int frames=0;do{lod.Update(c.Along,c.Across);lod.Reposition(f.Center);if(++frames%8==0)yield return null;Check(frames<5000,"Initial LOD never completes");}while(lod.Pending>0);
    int visible=lod.Count;Check(visible>0,"No visible terrain layout");
    lod.Update(c.Along+settings.TileSize*3,c.Across,false);
    Check(lod.Count==visible&&lod.Pending>0,"Visible LOD retired before replacement readiness");
    frames=0;while(lod.Pending>1){lod.Update(c.Along+settings.TileSize*6,c.Across,false);if(++frames%8==0)yield return null;Check(frames<5000,"Moving observer starved LOD generation");}
    Check(lod.Count==visible,"LOD retired while near tiles were not ready");
    lod.Update(c.Along+settings.TileSize*6,c.Across,true);lod.Reposition(f.Center);
    Check(lod.Pending==0&&lod.Count>0,"Complete LOD generation not published");
    Debug.Log("[RingworldSmoke] TERRAIN previous layout retained until complete replacement and near tiles ready; moving observer does not starve generation");
   }finally{lod.Dispose();UnityEngine.Object.Destroy(mat);}
  }
 }
}
#endif

#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using HarmonyLib;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
 internal static class InstalledIssueSmoke
 {
  static void Check(bool value,string message){if(!value)throw new Exception(message);}
  internal static IEnumerator Run(RingworldFlight f,Action<string> fail)
  {
   var routine=Execute(f,fail);
   while(true)
   {
    bool more=false;object current=null;Exception error=null;
    try{more=routine.MoveNext();if(more)current=routine.Current;}catch(Exception ex){error=ex;}
    if(error!=null){fail("Installed mods: "+error);yield break;}
    if(!more)yield break;yield return current;
   }
  }
  static IEnumerator Execute(RingworldFlight f,Action<string> fail)
  {
   var surveyor=PartLoader.getPartInfoByName("ringworldSurveyor");
   Check(surveyor!=null&&surveyor.partPrefab!=null,"Surveyor prefab absent");
   var model=surveyor.partConfig.GetNode("MODEL").GetValue("model");
   Check(model=="ReStock/Assets/Science/restock-thermometer","Surveyor did not select ReStock: "+model);
   Check(surveyor.partPrefab.GetComponentsInChildren<MeshFilter>(true).Length>0,"Surveyor has no meshes");
   foreach(var r in surveyor.partPrefab.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)Check(m!=null&&m.shader!=null,"Surveyor material missing");
   Debug.Log("[RingworldSmoke] RESTOCK actual Surveyor prefab/model/materials passed: "+model);
   f.arrivalHeight=100;f.Visit();while(!f.Ready)yield return null;
   yield return new WaitForSeconds(2);
   yield return StockAirStreamingSmoke.Run(f);
   var g=f.Settings.Geometry;var c=g.Coordinates(f.Position(FlightGlobals.ActiveVessel));
   DVec water=new DVec();bool found=false;
   for(int i=0;i<40000&&!found;i++)
   {double a=c.Along+(i%200-100)*500,b=c.Across+(i/200-100)*500;var t=f.Settings.Terrain.Sample(a,b);if(t.Wet&&t.WaterHeight-t.Height>40){water=g.Position(a,b,t.WaterHeight-8);found=true;}}
   Check(found,"Deep water fixture absent");
   FlightGlobals.ActiveVessel.SetPosition(f.Center+ConvertVector.Ksp(water),true);FlightGlobals.ActiveVessel.SetWorldVelocity(Vector3d.zero);
   RingCollisionFrame.Reset(FlightGlobals.ActiveVessel);
   var v=FlightGlobals.ActiveVessel;var host=v.rootPart;
   var resourceNode=new ConfigNode("RESOURCE");resourceNode.AddValue("name","IntakeLqd");resourceNode.AddValue("amount",300);resourceNode.AddValue("maxAmount",1000);
   host.AddResource(resourceNode);
   foreach(var name in new[]{"wbiProceduralBoatHull","wbiEbbTide"})
   {
    var info=PartLoader.getPartInfoByName(name);Check(info!=null,"Installed SunkWorks part missing: "+name);
    string moduleName=name=="wbiProceduralBoatHull"?"WBIBallastTank":"WBIAquaticEngine";
    ConfigNode config=null;foreach(var candidate in info.partConfig.GetNodes("MODULE"))if(candidate.GetValue("name")==moduleName)config=candidate.CreateCopy();
    Check(config!=null,"Installed module configuration missing: "+name);
    var transform=new GameObject("RingIssueTestIntake"+moduleName).transform;transform.SetParent(host.transform.Find("model") ?? host.transform,false);
    config.SetValue(moduleName=="WBIBallastTank"?"intakeTransformName":"thrustVectorTransformName",transform.name,true);
    var m=host.AddModule(config);m.OnStart(PartModule.StartState.Flying);
    v.SetPosition(f.Center+ConvertVector.Ksp(water),true);v.SetWorldVelocity(Vector3d.zero);RingCollisionFrame.Reset(v);
    for(int i=0;i<5;i++)yield return new WaitForFixedUpdate();
    Check(v.Splashed&&host.submergedPortion>0,"Module fixture lacks stock immersion");
    if(moduleName=="WBIBallastTank")
    {
     var resource=host.Resources["IntakeLqd"];resource.amount=resource.maxAmount*.3;
     var state=AccessTools.Field(m.GetType(),"ventState");var update=AccessTools.Method(m.GetType(),"updateBallastResource");
     var intakes=(Transform[])AccessTools.Field(m.GetType(),"intakeTransforms").GetValue(m);
     var patches=Harmony.GetPatchInfo(update);
     Debug.Log("[RingworldSmoke] Ballast diagnostics intakes="+(intakes==null?-1:intakes.Length)+" fillRate="+AccessTools.Field(m.GetType(),"fillRate").GetValue(m)+" transfer="+AccessTools.Method(m.GetType(),"GetActiveFluidTransferPercentage").Invoke(m,null)+" ring="+StockIntegration.Applies(v)+" altitude="+RingAquaticCompatibility.Altitude((Vector3d)transform.position,v.mainBody,m)+" patches="+(patches==null?"none":string.Join(",",patches.Owners)));
     Check(intakes!=null&&intakes.Length>0,"Fixture model intake missing");
     state.SetValue(m,Enum.Parse(state.FieldType,"FloodingBallast"));double start=resource.amount;
     update.Invoke(m,null);Check(resource.amount>start,"Installed ballast fails to flood underwater");
     state.SetValue(m,Enum.Parse(state.FieldType,"VentingBallast"));start=resource.amount;
     update.Invoke(m,null);Check(resource.amount<start,"Installed ballast fails to vent");
     v.SetPosition(f.Center+ConvertVector.Ksp(water+g.Up(water)*100),true);v.SetWorldVelocity(Vector3d.zero);
     state.SetValue(m,Enum.Parse(state.FieldType,"FloodingBallast"));start=resource.amount;update.Invoke(m,null);
     Check(resource.amount==start,"Ballast fills from air");state.SetValue(m,Enum.Parse(state.FieldType,"Closed"));
     Debug.Log("[RingworldSmoke] SUNKWORKS installed module ballast wet fill, vent and dry intake rejection passed");
    }
    else
    {
     var query=AccessTools.Method(m.GetType(),"checkUnderwater");
     var engine=(ModuleEngines)m;Check(engine.thrustTransforms.Count>0,"Fixture has no engine nozzle");
     foreach(var nozzle in engine.thrustTransforms)Check(nozzle!=null,"Fixture has stale engine nozzle");
     Check((bool)query.Invoke(m,null),"Aquatic engine rejects ring water");
     v.SetPosition(f.Center+ConvertVector.Ksp(water+g.Up(water)*100),true);v.SetWorldVelocity(Vector3d.zero);
     Check(!(bool)query.Invoke(m,null),"Aquatic engine accepts dry nozzle");
     Debug.Log("[RingworldSmoke] SUNKWORKS installed module aquatic engine wet/dry nozzle checks passed");
    }
    host.RemoveModule(m);UnityEngine.Object.Destroy(transform.gameObject);
   }
   Debug.Log("[RingworldSmoke] PASS installed issue regressions (PR2 and issue4 query paths; excludes issue3)");
  }
 }
}
#endif

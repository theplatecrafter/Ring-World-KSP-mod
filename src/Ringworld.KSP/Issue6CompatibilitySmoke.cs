#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
namespace NivenRingworld
{
    internal static class Issue6CompatibilitySmoke
    {
        internal static bool Enabled {get{return Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-issue6-only")>=0;}}
        internal static bool Degraded {get{return Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-issue6-degraded-startup")>=0;}}
        internal static bool ScenesOnly {get{return Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-issue6-scenes-only")>=0;}}
        internal static bool FreshScenes {get{return Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-issue6-fresh-scenes")>=0;}}
        static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
        internal static void Prepare()
        {
            var original=AccessTools.Method(typeof(ModuleResourceHarvester),"PrepareRecipe");
            var cache=RingCommunityFixesResourceCache.TargetMethod();
            Check(cache!=null,"Real KSP Community Fixes cache helper is not installed");
            var info=Harmony.GetPatchInfo(original);
            Check(info!=null&&info.Transpilers.Any(p=>p.PatchMethod.DeclaringType.FullName=="KSPCommunityFixes.Performance.ResourceConverterPerf"),"KSPCF optimizer is not active");
            var old=new Harmony("NivenRingworld.Test.Issue6OldHarvester");bool reproduced=false;
            try{old.Patch(original,transpiler:new HarmonyMethod(typeof(Issue6CompatibilitySmoke),nameof(OldHarvester)));}
            catch(Exception e){reproduced=e.ToString().Contains("Stock harvester resource query not found");}
            finally{old.UnpatchAll(old.Id);}
            Check(reproduced,"Real KSPCF did not reproduce the released harvester failure");
            Debug.Log("[RingworldSmoke] PASS actual KSPCF 1.41.1 optimizer reproduces released harvester exception; foreign optimizer retained");
            var intake=AccessTools.Method(typeof(ModuleResourceIntake),"FixedUpdate");
            var retry=new Harmony("NivenRingworld.Test.Issue6OldIntakeRetry");bool duplicateFailed=false;
            try{
                retry.Patch(intake,transpiler:new HarmonyMethod(typeof(Issue6CompatibilitySmoke),nameof(OldIntake)));
                try{retry.Patch(intake,transpiler:new HarmonyMethod(typeof(Issue6CompatibilitySmoke),nameof(OldIntake)));}
                catch(Exception e){duplicateFailed=e.ToString().Contains("Stock intake oxygen gate not found");}
            }finally{retry.UnpatchAll(retry.Id);}
            Check(duplicateFailed,"Actual Harmony retry did not reproduce the released intake failure");
            Debug.Log("[RingworldSmoke] PASS actual Harmony 2.2.1 duplicate-registration retry reproduces released intake oxygen exception; test owner removed");
            var repeated=new Harmony("NivenRingworld.Test.Issue6IntakeRepeatedHelper");
            try{
                repeated.Patch(intake,transpiler:new HarmonyMethod(typeof(RingIntakeEnvironment),"Transpiler"));
                repeated.Patch(intake,transpiler:new HarmonyMethod(typeof(RingIntakeEnvironment),"Transpiler"));
            }finally{repeated.UnpatchAll(repeated.Id);}
            Debug.Log("[RingworldSmoke] PASS corrected intake transpiler composes through actual repeated Harmony passes; temporary owner removed");
            if(Degraded)new Harmony("NivenRingworld.Test.UnsupportedIntake").Patch(AccessTools.Method(typeof(ModuleResourceIntake),"FixedUpdate"),transpiler:new HarmonyMethod(typeof(Issue6CompatibilitySmoke),nameof(OtherOxygenRead)));
        }
        static bool ExternalOxygen(CelestialBody body){return body.atmosphereContainsOxygen;}
        static IEnumerable<CodeInstruction> OtherOxygenRead(IEnumerable<CodeInstruction> source)
        {
            foreach(var instruction in source){var copy=new CodeInstruction(instruction);if(copy.LoadsField(AccessTools.Field(typeof(CelestialBody),"atmosphereContainsOxygen"))){copy.opcode=OpCodes.Call;copy.operand=AccessTools.Method(typeof(Issue6CompatibilitySmoke),nameof(ExternalOxygen));}yield return copy;}
        }
        static IEnumerable<CodeInstruction> OldHarvester(IEnumerable<CodeInstruction> instructions)
        {
            var codes=instructions.ToList();
            if(!codes.Any(c=>c.Calls(AccessTools.Method(typeof(ResourceMap),"GetAbundance",new[]{typeof(AbundanceRequest)}))))throw new InvalidOperationException("Stock harvester resource query not found");
            return codes;
        }
        static IEnumerable<CodeInstruction> OldIntake(IEnumerable<CodeInstruction> instructions)
        {
            var codes=instructions.Select(i=>new CodeInstruction(i)).ToList();bool found=false;
            foreach(var code in codes)if(code.LoadsField(AccessTools.Field(typeof(CelestialBody),"atmosphereContainsOxygen"))){found=true;code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(Issue6CompatibilitySmoke),nameof(ExternalOxygen));}
            if(!found)throw new InvalidOperationException("Stock intake oxygen gate not found; ring intake compatibility cannot be installed.");return codes;
        }
        internal static IEnumerator FreshSceneTransitions(string folder)
        {
            float end=Time.realtimeSinceStartup+90;
            while((HighLogic.LoadedScene!=GameScenes.SPACECENTER||RingworldScenario.Instance==null||!UnityEngine.Object.FindObjectsOfType<ScaledRing>().Any(r=>AccessTools.Field(typeof(ScaledRing),"root").GetValue(r)!=null))&&Time.realtimeSinceStartup<end)yield return null;
            Check(HighLogic.LoadedScene==GameScenes.SPACECENTER&&RingworldScenario.Instance!=null,"First Space Center startup failed");
            Check(StockIntegration.ReadyForFlight&&string.IsNullOrEmpty(StockIntegration.Warning),"Exact reporter stack has startup patch failures: "+StockIntegration.Warning);
            var save=new ConfigNode("SCENARIO");RingworldScenario.Instance.OnSave(save);var ring=save.GetNodes("RING");
            Check(ring.Length==1&&ring[0].GetValue("ringId")=="primary"&&ring[0].GetValue("ringName")=="Kerbol Standard Ringworld","Fresh save did not use the reporter's Kerbol Standard config");
            string savedRing=ring[0].ToString();int patches=OwnedPatchCount();
            var intake=Harmony.GetPatchInfo(AccessTools.Method(typeof(ModuleResourceIntake),"FixedUpdate"));
            Check(intake.Transpilers.Count(p=>p.owner==StockIntegration.OwnerFor(typeof(RingIntakeEnvironment)))==1,"Fresh startup registered duplicate intake patches");
            GamePersistence.SaveGame(HighLogic.CurrentGame.Updated(),"persistent",folder,SaveMode.OVERWRITE);
            for(int cycle=0;cycle<2;cycle++){
                HighLogic.LoadScene(GameScenes.TRACKSTATION);end=Time.realtimeSinceStartup+90;
                while((HighLogic.LoadedScene!=GameScenes.TRACKSTATION||TrackingRing.Trajectory==null||RingworldScenario.Instance==null)&&Time.realtimeSinceStartup<end)yield return null;
                yield return new WaitForSecondsRealtime(3);
                Check(HighLogic.LoadedScene==GameScenes.TRACKSTATION&&TrackingRing.Trajectory!=null,"Fresh-save Tracking Station did not initialize");
                Check(UnityEngine.Object.FindObjectsOfType<ScaledRing>().Any(r=>AccessTools.Field(typeof(ScaledRing),"root").GetValue(r)!=null),"Fresh-save Tracking Station lost ring renderer");
                Check(OwnedPatchCount()==patches&&string.IsNullOrEmpty(StockIntegration.Warning),"Tracking retried patches or degraded compatibility");
                HighLogic.LoadScene(GameScenes.SPACECENTER);end=Time.realtimeSinceStartup+90;
                while((HighLogic.LoadedScene!=GameScenes.SPACECENTER||RingworldScenario.Instance==null)&&Time.realtimeSinceStartup<end)yield return null;
                yield return new WaitForSecondsRealtime(3);
                Check(UnityEngine.Object.FindObjectsOfType<ScaledRing>().Any(r=>AccessTools.Field(typeof(ScaledRing),"root").GetValue(r)!=null),"Space Center lost ring renderer");
                save=new ConfigNode("SCENARIO");RingworldScenario.Instance.OnSave(save);
                Check(save.GetNodes("RING").Length==1&&save.GetNodes("RING")[0].ToString()==savedRing&&OwnedPatchCount()==patches,"Saved ring or patch identities changed across scenes");
            }
            var reload=GamePersistence.LoadGame("persistent",folder,true,false);
            Check(reload!=null,"New save cannot reopen");reload.startScene=GameScenes.SPACECENTER;HighLogic.CurrentGame=reload;reload.Start();end=Time.realtimeSinceStartup+90;
            while((HighLogic.LoadedScene!=GameScenes.SPACECENTER||RingworldScenario.Instance==null)&&Time.realtimeSinceStartup<end)yield return null;
            yield return new WaitForSecondsRealtime(5);save=new ConfigNode("SCENARIO");RingworldScenario.Instance.OnSave(save);
            Check(save.GetNodes("RING").Length==1&&save.GetNodes("RING")[0].ToString()==savedRing&&OwnedPatchCount()==patches&&string.IsNullOrEmpty(StockIntegration.Warning),"Existing save reopening changed ring or startup patches");
            Check(UnityEngine.Object.FindObjectsOfType<ScaledRing>().Any(r=>AccessTools.Field(typeof(ScaledRing),"root").GetValue(r)!=null),"Existing save reopening lost ring renderer");
            Debug.Log("[RingworldSmoke] PASS first Space Center -> Tracking -> KSC twice and existing-save reopen: Kerbol Standard ring identity retained; no failed patches; unchanged patch count="+patches);
            Debug.Log("[RingworldSmoke] PASS startup compatibility regression complete");
        }
        // Deliberately fail after installing one method of an optional class.
        // Exercise real Harmony rollback and later scene-like Install calls.
        internal static void InstallerRegression()
        {
            var target=AccessTools.Method(typeof(ModuleResourceIntake),"FixedUpdate");
            var before=Harmony.GetPatchInfo(target).Transpilers.Select(p=>p.owner).ToArray();
            StockIntegration.InstallClass(typeof(RingIntakeEnvironment),h=>{
                h.Patch(AccessTools.Method(typeof(Issue6CompatibilitySmoke),nameof(Dummy)),prefix:new HarmonyMethod(typeof(Issue6CompatibilitySmoke),nameof(DummyPrefix)));
                throw new InvalidOperationException("Intentional optional patch failure regression");
            });
            Check(StockIntegration.ReadyForFlight&&StockIntegration.Warning.Contains(nameof(RingIntakeEnvironment)),"Optional failure blocked physics or lacked diagnostic");
            Check(Harmony.GetPatchInfo(AccessTools.Method(typeof(Issue6CompatibilitySmoke),nameof(Dummy))).Prefixes.Count==0,"Failed class retained partially installed methods");
            // Test removed the real class as part of its rollback. Restore it
            // explicitly; ordinary Install must not retry/duplicate any class.
            StockIntegration.ClearTestFailure(typeof(RingIntakeEnvironment));
            StockIntegration.InstallClass(typeof(RingIntakeEnvironment),h=>h.CreateClassProcessor(typeof(RingIntakeEnvironment)).Patch());
            StockIntegration.Install();StockIntegration.Install();
            Check(before.SequenceEqual(Harmony.GetPatchInfo(target).Transpilers.Select(p=>p.owner)),"Repeated Install changed intake patch owners");
            StockIntegration.InstallClass(typeof(Issue6CompatibilitySmoke),h=>{throw new InvalidOperationException("Intentional essential patch failure regression");});
            Check(!StockIntegration.ReadyForFlight&&!string.IsNullOrEmpty(StockIntegration.Warning),"Essential patch failure not gated");
            StockIntegration.ClearTestFailure(typeof(Issue6CompatibilitySmoke));
            Check(StockIntegration.ReadyForFlight&&string.IsNullOrEmpty(StockIntegration.Warning),"Test diagnostic state not restored");
            Debug.Log("[RingworldSmoke] PASS per-class partial rollback, optional diagnostic, essential flight gate, repeated Install idempotence");
        }
        static void Dummy(){}
        static void DummyPrefix(){}
        static int OwnedPatchCount()
        {
            int count=0;foreach(var method in Harmony.GetAllPatchedMethods()){
                var info=Harmony.GetPatchInfo(method);
                count+=info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers).Count(p=>p.owner.StartsWith("NivenRingworld.surface.",StringComparison.Ordinal));
            }return count;
        }
        internal static IEnumerator SceneTransitions(RingworldFlight f,string resident)
        {
            // A restored legacy anchor starts above the floor. Finish an actual
            // touchdown before testing landed-resident persistence; airborne
            // records intentionally follow stock orbits outside Flight.
            f.gentleArrival=true;f.arrivalHeight=1000;f.Visit();while(!f.Ready)yield return null;f.gentleArrival=false;
            var v=FlightGlobals.ActiveVessel;float settle=Time.realtimeSinceStartup+60;
            while((!v.Landed||f.Velocity(v).Length>.25)&&Time.realtimeSinceStartup<settle)yield return new WaitForFixedUpdate();
            Check(v.Landed&&f.Velocity(v).Length<.25,"Scene fixture has not completed touchdown");
            int patches=OwnedPatchCount();
            MapView.EnterMapView();yield return new WaitForSecondsRealtime(2);
            Check(MapView.MapIsEnabled&&OwnedPatchCount()==patches,"Map changed ring patch registration");
            MapView.ExitMapView();f.Capture();
            GamePersistence.SaveGame(HighLogic.CurrentGame.Updated(),"persistent",HighLogic.SaveFolder,SaveMode.OVERWRITE);
            HighLogic.LoadScene(GameScenes.TRACKSTATION);
            float end=Time.realtimeSinceStartup+90;
            while((HighLogic.LoadedScene!=GameScenes.TRACKSTATION||TrackingRing.Trajectory==null||RingworldScenario.Instance==null)&&Time.realtimeSinceStartup<end)yield return null;
            Check(HighLogic.LoadedScene==GameScenes.TRACKSTATION&&TrackingRing.Trajectory!=null&&RingworldScenario.Instance!=null,"Tracking Station startup failed");
            yield return new WaitForSecondsRealtime(3);
            Check(RingworldScenario.Instance.Vessels.ContainsKey(resident)&&RingworldScenario.Instance.ActiveRingId=="primary","Tracking lost legacy resident/ring");
            Check(UnityEngine.Object.FindObjectsOfType<ScaledRing>().Any(r=>AccessTools.Field(typeof(ScaledRing),"root").GetValue(r)!=null),"Tracking lost scaled renderer");
            Check(OwnedPatchCount()==patches,"Tracking repeated patch registration");
            HighLogic.LoadScene(GameScenes.SPACECENTER);end=Time.realtimeSinceStartup+90;
            while((HighLogic.LoadedScene!=GameScenes.SPACECENTER||RingworldScenario.Instance==null)&&Time.realtimeSinceStartup<end)yield return null;
            yield return new WaitForSecondsRealtime(3);
            Check(HighLogic.LoadedScene==GameScenes.SPACECENTER&&RingworldScenario.Instance!=null&&RingworldScenario.Instance.Vessels.ContainsKey(resident),"Space Center lost legacy resident");
            Check(UnityEngine.Object.FindObjectsOfType<ScaledRing>().Any(r=>AccessTools.Field(typeof(ScaledRing),"root").GetValue(r)!=null)&&OwnedPatchCount()==patches,"Space Center renderer/patch identity changed");
            Debug.Log("[RingworldSmoke] PASS actual Map -> Tracking Station -> Space Center transitions with CF, saved resident, scaled renderer and unchanged patch count="+patches);
        }
        static void RecipeException(){throw new InvalidOperationException("Intentional recipe exception regression");}
        internal static void ResourceQueries(ModuleResourceHarvester drill,double expected)
        {
            var v=drill.vessel;var records=RingworldScenario.Instance.Vessels;var residence=records[v.id.ToString()];
            var request=new AbundanceRequest{ResourceName=drill.ResourceName,ResourceType=(HarvestTypes)drill.HarvesterType,BodyId=v.mainBody.flightGlobalsIndex,Latitude=v.latitude,Longitude=v.longitude,CheckForLock=false};
            var cached=RingCommunityFixesResourceCache.TargetMethod();float stock;
            records.Remove(v.id.ToString());
            try{
                stock=(float)cached.Invoke(null,new object[]{ResourceMap.Instance,request,drill});
                Check(Math.Abs(stock-ResourceMap.Instance.GetAbundance(request))<1e-6,"Non-ring cached resource query changed");
            }finally{records[v.id.ToString()]=residence;}
            RingResourceHarvester.QueryScope previous;RingResourceHarvester.Prefix(drill,out previous);
            try{
                var value=(float)cached.Invoke(null,new object[]{ResourceMap.Instance,request,drill});
                Check(Math.Abs(value-expected)<1e-6&&Math.Abs(value-stock)>1e-6,"Ring query consumed same-frame host cache entry");
                Check(Math.Abs(ResourceMap.Instance.GetAbundance(request)-expected)<1e-6,"Direct query boundary disagrees");
                var foreign=request;foreign.ResourceName="unrelated-resource";float ignored;
                Check(!RingResourceHarvester.TryAbundance(foreign,drill,out ignored),"Foreign resource query captured by harvester scope");
                foreign=request;foreign.BodyId=FlightGlobals.GetHomeBody().flightGlobalsIndex;
                Check(!RingResourceHarvester.TryAbundance(foreign,drill,out ignored),"Another body's resource query captured by harvester scope");
                RingResourceHarvester.Finalizer(default(RingResourceHarvester.QueryScope));
                Check(RingResourceHarvester.Current==drill,"A skipped nested prefix destroyed the outer query scope");
            }finally{RingResourceHarvester.Finalizer(previous);}
            Check(Math.Abs((float)cached.Invoke(null,new object[]{ResourceMap.Instance,request,drill})-stock)<1e-6,"Ring query polluted stock cache");
            var external=new Harmony("NivenRingworld.Test.RecipeException");
            try{
                external.Patch(AccessTools.Method(typeof(ModuleResourceHarvester),"PrepareRecipe"),prefix:new HarmonyMethod(typeof(Issue6CompatibilitySmoke),nameof(RecipeException)){priority=Priority.Last});
                bool caught=false;try{AccessTools.Method(typeof(ModuleResourceHarvester),"PrepareRecipe").Invoke(drill,new object[]{.02d});}catch(Exception e){caught=e.ToString().Contains("Intentional recipe exception regression");}
                Check(caught&&RingResourceHarvester.Current==null,"Native recipe exception leaked query context or was swallowed");
            }finally{external.UnpatchAll(external.Id);}
            Debug.Log("[RingworldSmoke] PASS real CF cache hit isolated: stock="+stock+" ring="+expected+"; direct boundary, unrelated request and exceptional recipe scope");
        }
        internal static IEnumerator Run(RingworldFlight f)
        {
            if(Degraded){
                Check(StockIntegration.ReadyForFlight&&StockIntegration.Warning.Contains(nameof(RingIntakeEnvironment)),"Unsupported intake did not degrade safely");
                Check(UnityEngine.Object.FindObjectsOfType<CompatibilityNotice>().Length==1,"Startup failure lacked visible diagnostic");
                var target=AccessTools.Method(typeof(ModuleResourceIntake),"FixedUpdate");
                Check(Harmony.GetPatchInfo(target).Transpilers.Any(p=>p.owner=="NivenRingworld.Test.UnsupportedIntake"),"Rollback removed the foreign intake rewrite");
                new Harmony("NivenRingworld.Test.UnsupportedIntake").UnpatchAll("NivenRingworld.Test.UnsupportedIntake");
                StockIntegration.ClearTestFailure(typeof(RingIntakeEnvironment));
                StockIntegration.InstallClass(typeof(RingIntakeEnvironment),h=>h.CreateClassProcessor(typeof(RingIntakeEnvironment)).Patch());
                Debug.Log("[RingworldSmoke] PASS unsupported intake startup keeps ring renderer, launcher, legacy resident, remaining physics and one diagnostic; foreign owner preserved");
            }
            var trees=GameDatabase.Instance.GetConfigs("TechTree");
            if(trees.Any(t=>t.config.GetNodes("RDNode").Any(n=>n.GetValue("id")=="colonization"))){
                Check(trees.Any(t=>t.config.GetNodes("RDNode").Any(n=>n.GetValue("id")=="basicRocketry")),"Community Tech Tree lost stock tech nodes");
                Check(PartResourceLibrary.Instance.GetDefinition("Water")!=null,"CRP Water definition missing");
                var v=FlightGlobals.ActiveVessel;RingworldSurfaceState state;Check(RingworldSurfaceApi.TryGetSurfaceState(v,out state),"Resource probe lacks local biome");
                double water;Check(RingworldResourceApi.TryGetAbundance(v,"Water",out water),"Community-authored Water cfg was not consumed");
                Check(Math.Abs(water-(state.Biome=="Grassland"?.2:.5))<1e-6,"Planetary/biome Water override borrowed host or global data: "+water);
                double sample;Check(RingworldResourceApi.Sample(f.Settings,state.Along,state.Across,"Grassland","Water",out sample)&&Math.Abs(sample-.2)<1e-6,"Authored biome Water override ignored");
                var options=f.Settings.Save();options.SetValue("ringId","community-resource-global-probe",true);var otherRing=Settings.Load();otherRing.Apply(options);
                Check(RingworldResourceApi.Sample(otherRing,state.Along,state.Across,"Grassland","Water",out sample)&&Math.Abs(sample-.1)<1e-6,"Authored global Water fallback ignored or ring override leaked to another ring");
                Debug.Log("[RingworldSmoke] PASS loaded CTT custom/stock nodes, real CRP Water definition and authored GLOBAL/PLANETARY/BIOME cfg precedence; local="+water+" biome=0.2 other-ring=0.1");
            }
            InstallerRegression();
            var cache=RingCommunityFixesResourceCache.TargetMethod();
            Check(Harmony.GetPatchInfo(cache).Prefixes.Any(p=>p.owner==StockIntegration.OwnerFor(typeof(RingCommunityFixesResourceCache))),"KSPCF cache boundary missing");
            var test=ResourceIssueSmoke.Run(f);while(test.MoveNext())yield return test.Current;
            test=StockAirStreamingSmoke.Run(f);while(test.MoveNext())yield return test.Current;
            test=SolverEngineSmoke.Run(f);while(test.MoveNext())yield return test.Current;
            Check(RingResourceHarvester.Current==null,"Harvester context leaked across calls");
            test=WaterPhysicsSmoke.Run(f,text=>{throw new Exception(text);});while(test.MoveNext())yield return test.Current;
            Debug.Log("[RingworldSmoke] PASS issue6 runtime: KSPCF retained, actual stock harvesting/intake, legacy resident and query scope");
        }
    }
}
#endif

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Ringworld.Core;
using Expansions.Serenity.DeployedScience.Runtime;
namespace NivenRingworld
{
    internal static class RingScience
    {
        internal static bool Context(Vessel v,out Settings s,out string key,out string title)
        {
            ResearchLocation location;bool found=Resolve(v,out s,out location);
            key=found?"RingworldV2_"+location.Key:null;title=found?"Ringworld / "+location.Name:null;return found;
        }
        private static int cacheFrame=-1;
        private sealed class CachedLocation {internal Settings Settings;internal ResearchLocation Location;}
        private static readonly Dictionary<Guid,CachedLocation> locations=new Dictionary<Guid,CachedLocation>();
        internal static bool Resolve(Vessel v,out Settings s,out ResearchLocation location)
        {
            if(cacheFrame!=UnityEngine.Time.frameCount){cacheFrame=UnityEngine.Time.frameCount;locations.Clear();}
            CachedLocation cached;
            if(v!=null&&locations.TryGetValue(v.id,out cached)){s=cached.Settings;location=cached.Location;return location!=null;}
            bool found=ResolveUncached(v,out s,out location);
            if(v!=null)locations[v.id]=new CachedLocation{Settings=s,Location=location};return found;
        }
        private static bool ResolveUncached(Vessel v,out Settings s,out ResearchLocation location)
        {
            s=null;location=null;var state=RingworldScenario.Instance;
            if(v==null||v.mainBody==null||state==null)return false;
            var f=RingworldFlight.Instance;DVec position;double epoch;VesselRecord record;
            if(f!=null&&f.Owns(v)){s=f.Settings;position=f.Position(v);epoch=f.FrameEpoch;}
            else if(RingResidence.Saved(v,out record)&&record.Landed){s=state.RingSettings(record.RingId);position=record.Position;epoch=record.Epoch;}
            else {string id=RingSelection.Nearest(v);if(id==null)return false;s=state.RingSettings(id);position=ConvertVector.Core(v.GetWorldPos3D()-s.Center);epoch=Planetarium.GetUniversalTime();}
            if(s==null||s.Body!=v.mainBody)return false;
            // Never alter the active terrain chart while resolving background science.
            double rotation=s.Geometry.OrientationRadians;
            position=s.Geometry.RotateAroundAxis(position,rotation-s.Geometry.P.Omega*epoch);
            bool water=v.Splashed;
            if(!v.Landed&&f!=null&&f.Owns(v))
            {
                var at=s.Geometry.Coordinates(position);
                if(Math.Abs(at.Across)<s.Geometry.P.Width/2&&at.Altitude<10000)
                {var sample=s.Terrain.Sample(at.Along,at.Across);water|=sample.Wet&&Math.Abs(at.Altitude-sample.WaterHeight)<5&&f.Velocity(v).Length<5;}
            }
            location=ResearchRegions.Locate(s.Terrain,position,v.Landed,water,s.Atmosphere,Planetarium.GetUniversalTime());
            if(location==null)return false;
            if(location.Category=="biome"||location.Category=="landmark")
                location=ResearchCatalog.CustomSite(s,s.Geometry.Coordinates(position),location.Zone)??ResearchCatalog.Structure(s,s.Geometry.Coordinates(position),location.Zone)??location;
            if(s.RingId!="primary"){location.Id=s.RingId+"-"+location.Id;location.Name=s.RingName+" / "+location.Name;}
            return true;
        }
        internal static ExperimentSituations Situation(ResearchLocation l)
        {
            switch(l.Zone){case "surface":return ExperimentSituations.SrfLanded;case "water":return ExperimentSituations.SrfSplashed;case "lowair":return ExperimentSituations.FlyingLow;case "highair":return ExperimentSituations.FlyingHigh;case "highspace":return ExperimentSituations.InSpaceHigh;default:return ExperimentSituations.InSpaceLow;}
        }
        public static ScienceSubject Subject(ScienceExperiment e,ExperimentSituations situation,CelestialBody body,string biome,string display,Vessel v)
        {
            Settings s;ResearchLocation location;
            if(!Resolve(v,out s,out location))return ResearchAndDevelopment.GetExperimentSubject(e,situation,body,biome,display);
            situation=Situation(location);
            var subject=ResearchAndDevelopment.GetExperimentSubject(e,situation,body,"RingworldV2_"+location.Key,location.Name);
            subject.title=e.experimentTitle+" — Ringworld / "+location.Name+" ("+situation+")";
            subject.subjectValue=ResearchCatalog.Multiplier(location);subject.scienceCap=e.scienceCap*subject.subjectValue;subject.dataScale=e.dataScale;
            subject.scientificValue=ResearchAndDevelopment.GetSubjectValue(subject.science,subject);
            return subject;
        }
        public static bool Available(ScienceExperiment e,ExperimentSituations situation,CelestialBody body,Vessel v)
        {
            Settings s;ResearchLocation location;
            if(!Resolve(v,out s,out location))return e.IsAvailableWhile(situation,body);
            situation=Situation(location);
            bool air=s.Atmosphere&&(situation==ExperimentSituations.SrfLanded||situation==ExperimentSituations.SrfSplashed||situation==ExperimentSituations.FlyingLow||situation==ExperimentSituations.FlyingHigh)&&location.Category!="wall";
            return ((int)e.situationMask&(int)situation)!=0&&(!e.requireAtmosphere||air)&&(!e.requireNoAtmosphere||!air);
        }
    }
    [HarmonyPatch(typeof(ScienceUtil),"GetExperimentSituation")]
    internal static class RingExperimentSituation
    {
        private static bool Prefix(Vessel v,ref ExperimentSituations __result)
        {Settings s;ResearchLocation l;if(!RingScience.Resolve(v,out s,out l))return true;__result=RingScience.Situation(l);return false;}
    }
    // Public, read-only extension point; no dependence on renderers or the active vessel.
    public static class RingworldResearchApi
    {
        public static bool TryGetLocation(Vessel vessel,out ResearchLocation location)
        {Settings s;ResearchLocation found;bool valid=RingScience.Resolve(vessel,out s,out found);location=valid?new ResearchLocation(found.Id,found.Name,found.Category,found.Zone):null;return valid;}
        public static bool TryGetSubject(Vessel vessel,ScienceExperiment experiment,out ScienceSubject subject)
        {
            subject=null;Settings s;ResearchLocation location;
            if(experiment==null||!RingScience.Resolve(vessel,out s,out location))return false;
            subject=RingScience.Subject(experiment,RingScience.Situation(location),vessel.mainBody,"","",vessel);return true;
        }
    }
    [HarmonyPatch(typeof(ScienceSubject),"Load")]
    internal static class RingResearchTitleLoad
    {
        private static void Postfix(ScienceSubject __instance,ConfigNode node)
        {ResearchReceipt r;if(ExpeditionJournal.Decode(__instance.id,out r)&&node.HasValue("title"))__instance.title=node.GetValue("title");}
    }
    [HarmonyPatch(typeof(ResearchAndDevelopment),"GetResults")]
    internal static class RingResearchResults
    {
        private static bool Prefix(string __0,ref string __result)
        {
            ResearchReceipt r;if(!ExpeditionJournal.Decode(__0,out r))return true;
            __result="The observations document another part of the Ringworld's artificial environment. Transmit or return this data to add it to the expedition's research record.";
            int specificity=-1;
            foreach(var n in GameDatabase.Instance.GetConfigNodes("RINGWORLD_SCIENCE_REPORT"))
            {
                int score=(n.HasValue("experiment")?1:0)+(n.HasValue("category")?1:0);
                if(score>=specificity&&(!n.HasValue("experiment")||n.GetValue("experiment")==r.Experiment)&&(!n.HasValue("category")||n.GetValue("category")==r.Category)&&n.HasValue("text")){__result=n.GetValue("text");specificity=score;}
            }
            return false;
        }
    }
    [HarmonyPatch]
    internal static class RingCargoSettle
    {
        private static MethodBase TargetMethod()
        {
            foreach(var type in typeof(ModuleCargoPart).GetNestedTypes(BindingFlags.Public|BindingFlags.NonPublic))if(type.Name.Contains("MakePartSettle"))return AccessTools.Method(type,"MoveNext");
            throw new InvalidOperationException("Cargo settling iterator unavailable");
        }
        public static Vector3d RelativeVelocity(Vessel v){var f=RingworldFlight.Instance;return f!=null&&f.Owns(v)?ConvertVector.Ksp(f.Velocity(v)):v.srf_velocity;}
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code,ILGenerator generator)
        {
            var field=AccessTools.Field(typeof(Vessel),"srf_velocity");
            var velocity=generator.DeclareLocal(typeof(Vector3d));
            foreach(var i in code)
            {
                bool address=i.opcode==OpCodes.Ldflda;
                if((i.opcode==OpCodes.Ldfld||address)&&Equals(i.operand,field))
                {
                    i.opcode=OpCodes.Call;i.operand=AccessTools.Method(typeof(RingCargoSettle),"RelativeVelocity");yield return i;
                    if(address){yield return new CodeInstruction(OpCodes.Stloc,velocity);yield return new CodeInstruction(OpCodes.Ldloca,velocity);}
                }
                else yield return i;
            }
        }
    }
    // Add the actual instrument's vessel to each call, including iterator bodies.
    // This avoids borrowing the active vessel for background deployed experiments.
    [HarmonyPatch]
    internal static class RingScienceCalls
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach(var t in typeof(ModuleScienceExperiment).GetNestedTypes(BindingFlags.Public|BindingFlags.NonPublic))
                if(t.Name.Contains("gatherData")||t.Name.Contains("OnScienceCompleteDelay"))yield return AccessTools.Method(t,"MoveNext");
            yield return AccessTools.Method(typeof(DeployedScienceExperiment),"Start");
            yield return AccessTools.Method(typeof(ModuleScienceExperiment),"updateModuleUI");
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code,MethodBase __originalMethod)
        {
            var subject=AccessTools.Method(typeof(ResearchAndDevelopment),"GetExperimentSubject",new[]{typeof(ScienceExperiment),typeof(ExperimentSituations),typeof(CelestialBody),typeof(string),typeof(string)});
            var available=AccessTools.Method(typeof(ScienceExperiment),"IsAvailableWhile");
            foreach(var instruction in code)
            {
                if(instruction.Calls(subject)||instruction.Calls(available))
                {
                    var load=new CodeInstruction(OpCodes.Ldarg_0);load.labels.AddRange(instruction.labels);instruction.labels.Clear();yield return load;
                    if(__originalMethod.DeclaringType==typeof(DeployedScienceExperiment))yield return new CodeInstruction(OpCodes.Ldfld,AccessTools.Field(typeof(DeployedScienceExperiment),"ExperimentVessel"));
                    else if(__originalMethod.DeclaringType==typeof(ModuleScienceExperiment))yield return new CodeInstruction(OpCodes.Call,AccessTools.PropertyGetter(typeof(PartModule),"vessel"));
                    else
                    {
                        FieldInfo owner=null;foreach(var field in __originalMethod.DeclaringType.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))if(field.FieldType==typeof(ModuleScienceExperiment)){owner=field;break;}
                        if(owner==null)throw new InvalidOperationException("Science iterator owner missing");
                        yield return new CodeInstruction(OpCodes.Ldfld,owner);yield return new CodeInstruction(OpCodes.Call,AccessTools.PropertyGetter(typeof(PartModule),"vessel"));
                    }
                    instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(RingScience),instruction.Calls(subject)?"Subject":"Available");
                }
                yield return instruction;
            }
        }
    }
}

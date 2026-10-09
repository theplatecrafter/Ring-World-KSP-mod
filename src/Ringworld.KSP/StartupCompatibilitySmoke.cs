#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Ringworld.Core;
using UnityEngine;

namespace NivenRingworld
{
    // Synthetic legacy-format save plus a competing transpiler. Neither this
    // fixture nor its Harmony owner exists in normal builds.
    internal static class StartupCompatibilitySmoke
    {
        const string OtherOwner="NivenRingworld.Test.EvaOtherMod",OldOwner="NivenRingworld.Test.EvaOldFailure";
        static readonly string[] Methods={"heading_acquire_OnLeave","bound_fl_OnLeave","land_OnEnter","jump_OnEnter"};
        static string residentId;
        static DVec savedPosition;
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}

        internal static void Prepare(ConfigNode game)
        {
            var other=new Harmony(OtherOwner);
            foreach(string name in Methods)
                other.Patch(AccessTools.Method(typeof(KerbalEVA),name),transpiler:new HarmonyMethod(typeof(StartupCompatibilitySmoke),nameof(OtherModTranspiler)));
            bool failed=false;
            var old=new Harmony(OldOwner);
            try {old.Patch(AccessTools.Method(typeof(KerbalEVA),Methods[0]),transpiler:new HarmonyMethod(typeof(StartupCompatibilitySmoke),nameof(OldStrictTranspiler)));}
            catch(Exception e){failed=e.ToString().Contains("KSP EVA speed read was not found");}
            finally {old.UnpatchAll(OldOwner);}
            Check(failed,"Competing transpiler did not reproduce the reported released-patch failure");
            StockIntegration.Install();StockIntegration.Install();
            foreach(string name in Methods){
                var info=Harmony.GetPatchInfo(AccessTools.Method(typeof(KerbalEVA),name));
                Check(info.Prefixes.Count(p=>p.owner=="NivenRingworld.surface")==1&&info.Transpilers.Any(p=>p.owner==OtherOwner),"Patch composition/idempotence failed: "+name);
            }
            Debug.Log("[RingworldSmoke] PASS reproduced released EVA exception; all stock patches install with competing transpilers retained");

            var defaults=Settings.Load();
            var options=defaults.Save();options.SetValue("referenceBody","Sun",true);
            options.SetValue("radius",15300000000d,true);options.SetValue("width",160500000d,true);
            options.SetValue("seed",-739779896,true);RingQualityPresets.Apply(options,6);
            foreach(string key in new[]{"ringId","ringName","referenceBody","anchorId","centerX","centerY","centerZ","tiltX","tiltY","tiltZ","spinDirection","panelsEnabled","designatedStar"})options.RemoveValues(key);
            defaults.Apply(options);
            savedPosition=defaults.Geometry.Position(0,0,defaults.Terrain.Sample(0,0).Height+10);
            var vessel=game.GetNode("FLIGHTSTATE").GetNodes("VESSEL")[0];residentId=Guid.Parse(vessel.GetValue("pid")).ToString();
            // The old tutorial's Mark1-2Pod hatch/attachments are unsuitable for
            // this required-only EVA probe. Use a current stock single pod,
            // retaining its crew, vessel identity and legacy residence format.
            var parts=vessel.GetNodes("PART");for(int i=1;i<parts.Length;i++)vessel.RemoveNode(parts[i]);
            parts[0].SetValue("name","mk1-3pod",true);parts[0].SetValue("rTrf","mk1-3pod",true);parts[0].RemoveValues("attN");
            var legacy=new ConfigNode("SCENARIO");legacy.AddValue("name",nameof(RingworldScenario));legacy.AddValue("scene","7, 5, 6");legacy.AddValue("expedition",true);legacy.AddNode(options);
            var record=legacy.AddNode("VESSEL");record.AddValue("id",residentId);record.AddValue("landed",true);
            record.AddValue("x",savedPosition.X);record.AddValue("y",savedPosition.Y);record.AddValue("z",savedPosition.Z);
            var rot=Quaternion.FromToRotation(Vector3.up,ConvertVector.Unity(defaults.Geometry.Up(savedPosition)));
            record.AddValue("qx",rot.x);record.AddValue("qy",rot.y);record.AddValue("qz",rot.z);record.AddValue("qw",rot.w);
            game.AddNode(legacy);
        }

        // Simulate another mod moving the field read behind a helper, without
        // changing its result. This removes the instruction our old patch required.
        static IEnumerable<CodeInstruction> OtherModTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var field=AccessTools.Field(typeof(Vessel),nameof(Vessel.horizontalSrfSpeed));
            foreach(var instruction in instructions){
                var copy=new CodeInstruction(instruction);
                if(copy.LoadsField(field)){copy.opcode=OpCodes.Call;copy.operand=AccessTools.Method(typeof(StartupCompatibilitySmoke),nameof(ExternalSpeed));}
                yield return copy;
            }
        }
        static double ExternalSpeed(Vessel v){return v.horizontalSrfSpeed;}
        static IEnumerable<CodeInstruction> OldStrictTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var code=instructions.ToList();
            if(!code.Any(i=>i.LoadsField(AccessTools.Field(typeof(Vessel),nameof(Vessel.horizontalSrfSpeed)))))
                throw new InvalidOperationException("KSP EVA speed read was not found; incompatible game assembly.");
            return code;
        }

        internal static IEnumerator Run(RingworldFlight f)
        {
            CheatOptions.NoCrashDamage=false;CheatOptions.UnbreakableJoints=false;
            var s=RingworldScenario.Instance;VesselRecord record;
            Check(s.Rings.Count==1&&s.ActiveRingId=="primary"&&s.Vessels.TryGetValue(residentId,out record),"Legacy single-ring save/resident not migrated");
            record=s.Vessels[residentId];
            Check(record.RingId=="primary"&&(record.Position-savedPosition).Length<20,"Legacy resident anchor changed");
            Check(f.Star.bodyName=="Sun"&&f.Settings.Geometry.P.Radius==15300000000d&&f.Settings.Geometry.P.Seed==-739779896,"Installed replacement config overwrote legacy habitat");
            var renderer=UnityEngine.Object.FindObjectsOfType<ScaledRing>().FirstOrDefault(r=>r.RingId=="primary");
            Check(renderer!=null&&AccessTools.Field(typeof(ScaledRing),"root").GetValue(renderer)!=null,"Legacy scaled ring failed startup");
            var saved=new ConfigNode("SCENARIO");s.OnSave(saved);
            Check(saved.GetNodes("RING").Length==1&&saved.GetNodes("VESSEL").Any(n=>n.GetValue("id")==residentId&&n.GetValue("ringId")=="primary"),"Migrated records not retained on resave");
            Debug.Log("[RingworldSmoke] PASS legacy OPTIONS/no ringId migration, Sun/seed/geometry retained despite installed replacement config, scaled ring and launcher present, resident resaved");
            var v=FlightGlobals.ActiveVessel;float end=Time.realtimeSinceStartup+90;
            while(!f.Ready&&Time.realtimeSinceStartup<end)yield return null;
            Check(f.Ready&&f.Owns(v),"Legacy resident did not restore into its ring");
            Check((f.Position(v)-savedPosition).Length<30,"Restored legacy resident moved away from its saved anchor");
            // Place safely using the ordinary arrival path, then exercise the
            // native EVA state machine with all four competing transpilers active.
            f.gentleArrival=true;f.arrivalHeight=1000;f.Visit();while(!f.Ready)yield return null;f.gentleArrival=false;
            end=Time.realtimeSinceStartup+60;
            while((!v.Landed||f.Velocity(v).Length>.25)&&Time.realtimeSinceStartup<end)yield return new WaitForFixedUpdate();
            Check(v.Landed&&f.Velocity(v).Length<.25,"EVA fixture did not settle");
            // Unregistered vessels retain their cache. This training save has
            // only one ship, so test the same vessel outside the residence scope
            // synchronously and restore its registration before yielding.
            var registration=s.Vessels[v.id.ToString()];double before=v.horizontalSrfSpeed;
            s.Vessels.Remove(v.id.ToString());
            try {RingSurfaceCaches.Publish(v);Check(v.horizontalSrfSpeed.Equals(before),"Unregistered vessel speed modified");}
            finally {s.Vessels[v.id.ToString()]=registration;}
            var cabin=v.parts.Find(p=>p.protoModuleCrew.Count>0);
            var eva=FlightEVA.fetch.spawnEVA(cabin.protoModuleCrew[0],cabin,cabin.airlock,true);Check(eva!=null,"EVA hatch blocked");
            end=Time.realtimeSinceStartup+60;
            while((eva.vessel==null||eva.vessel.packed||FlightGlobals.ActiveVessel!=eva.vessel)&&Time.realtimeSinceStartup<end)yield return null;
            while(eva.fsm.CurrentState==eva.st_ladder_acquire&&Time.realtimeSinceStartup<end)yield return new WaitForFixedUpdate();
            Check(f.Owns(eva.vessel)&&eva.fsm.CurrentState!=eva.st_ladder_acquire,"Native EVA acquisition failed");
            if(eva.OnALadder)eva.fsm.RunEvent(eva.On_ladderLetGo);
            bool contact=false;
            for(int i=0;i<1200;i++){Check(eva.part.State!=PartStates.DEAD&&f.Velocity(eva.vessel).Length<40,"EVA contact speed/damage failure");contact|=RingEva.Grounded(eva);yield return new WaitForFixedUpdate();}
            Check(contact&&f.Velocity(eva.vessel).Length<2,"EVA failed natural contact");
            SmokeTest.WalkingEva=eva;SmokeTest.WalkInput=Vector2.up;
            try{
                for(int i=0;i<100;i++)yield return new WaitForFixedUpdate();var start=f.Position(eva.vessel);
                for(int i=0;i<200;i++)yield return new WaitForFixedUpdate();double walk=(f.Position(eva.vessel)-start).Length;
                Check(!eva.isRagdoll&&walk>1&&walk<30,"Native recovery/walk failed: "+walk);
                double expected=EvaTransitionSpeedPatch.HorizontalSpeed(eva.vessel);
                eva.vessel.horizontalSrfSpeed=385637;
                AccessTools.Method(typeof(KerbalEVA),Methods[0]).Invoke(eva,new object[]{eva.fsm.CurrentState});
                float seeded=(float)AccessTools.Field(typeof(KerbalEVA),"lastTgtSpeed").GetValue(eva);
                Check(Math.Abs(seeded-expected)<.001&&Math.Abs(eva.vessel.horizontalSrfSpeed-expected)<.001,"EVA boundary consumed stale orbital speed through the other mod's helper");
                Debug.Log("[RingworldSmoke] PASS EVA contact/recovery/walk with competing speed-read transpilers, distance="+walk+"; stale 385637 m/s cache refreshed to "+seeded+"; unregistered vessel cache unchanged");
            }finally{SmokeTest.WalkingEva=null;}
            Debug.Log("[RingworldSmoke] PASS startup compatibility regression complete");
        }
    }
}
#endif

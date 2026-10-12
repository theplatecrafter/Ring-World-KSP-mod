#if RINGWORLD_SMOKE_TEST
using System;
using System.IO;
using System.Linq;
using UnityEngine;
namespace NivenRingworld
{
    internal static class RetainedSaveCompatibilitySmoke
    {
        static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
        internal static void Run()
        {
            var data=Path.Combine(KSPUtil.ApplicationRootPath,"GameData");
            foreach(var directory in Directory.GetDirectories(data,"RingworldCompatibilityProbe-*"))foreach(var file in Directory.GetFiles(directory,"Retained-*.sfs")){
                var node=ConfigNode.Load(file).GetNode("GAME").GetNodes("SCENARIO").First(n=>n.GetValue("name")==nameof(RingworldScenario));
                var current=RingworldScenario.Instance;var flight=RingworldFlight.Instance;
                var go=new GameObject("Retained Ringworld save regression");
                try{
                    // Never capture the active test ship into a historical save.
                    // No frame is yielded while these temporary globals exist.
                    RingworldFlight.Instance=null;var old=go.AddComponent<RingworldScenario>();old.OnLoad(node.CreateCopy());
                    var source=node.GetNodes("RING");if(source.Length==0)source=new[]{node.GetNode("OPTIONS")};
                    Check(source.All(n=>n!=null)&&old.Rings.Count==source.Length,"Historical ring count changed");
                    foreach(var ring in source){
                        string id=ring.GetValue("ringId")??"primary";var loaded=old.RingOptions(id);
                        Check(loaded!=null,"Historical ring ID changed");
                        foreach(ConfigNode.Value value in ring.values)Check(loaded.GetValue(value.name)==value.value,"Historical option changed: "+value.name);
                        int generation;int.TryParse(ring.GetValue("generationVersion")??"1",out generation);
                        Check(old.RingSettings(id).GenerationVersion==generation,"Historical terrain generator default changed");
                    }
                    Check(node.GetValues("discovery").All(d=>old.Discoveries.Contains(d))&&node.GetValues("researchReceived").All(d=>old.Research.Receipts.ContainsKey(d))&&node.GetValues("objectiveCompleted").All(d=>old.Research.Completed.Contains(d)),"Historical discovery/research state lost on initial load");
                    Check(node.GetNodes("VESSEL").Length==old.Vessels.Count,"Historical resident count changed on initial load");
                    var records=old.Vessels.ToDictionary(p=>p.Key,p=>p.Value);var discoveries=old.Discoveries.ToArray();
                    var first=new ConfigNode("SCENARIO");old.OnSave(first);old.OnLoad(first.CreateCopy());
                    var second=new ConfigNode("SCENARIO");old.OnSave(second);
                    // RING is authoritative in modern saves. The redundant
                    // OPTIONS compatibility copy can acquire newly persisted
                    // anchor-fallback fields on the first legacy round trip.
                    foreach(var redundant in first.GetNodes("OPTIONS"))first.RemoveNode(redundant);
                    foreach(var redundant in second.GetNodes("OPTIONS"))second.RemoveNode(redundant);
                    Check(first.ToString()==second.ToString(),"Historical migration is not repeatable");
                    Check(records.Count==old.Vessels.Count&&discoveries.All(d=>old.Discoveries.Contains(d)),"Historical resident/discovery state lost");
                    foreach(var record in records){var after=old.Vessels[record.Key];var before=record.Value;
                        Check(before.Id==after.Id&&before.RingId==after.RingId&&before.Position.Equals(after.Position)&&before.Velocity.Equals(after.Velocity)&&before.Rotation==after.Rotation&&before.Landed==after.Landed,"Historical resident pose changed");
                    }
                    Debug.Log("[RingworldSmoke] PASS retained "+Path.GetFileName(file)+" format="+(node.GetValue("formatVersion")??"legacy")+": rings="+source.Length+" residents="+records.Count+"; all original options/generator/poses/discoveries retained; repeated migration identical");
                }finally{RingworldScenario.Instance=current;RingworldFlight.Instance=flight;UnityEngine.Object.DestroyImmediate(go);}
            }
        }
    }
}
#endif

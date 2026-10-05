using System;
using System.Collections.Generic;
using System.Globalization;
using Ringworld.Core;
using UnityEngine;

namespace NivenRingworld
{
    internal sealed class VesselRecord
    {
        internal string Id;
        internal string RingId="primary";
        internal DVec Position,Velocity;
        internal Quaternion Rotation=Quaternion.identity;
        internal bool Restored,Landed;
        internal double Epoch;
    }

    [KSPScenario(ScenarioCreationOptions.AddToAllGames,GameScenes.FLIGHT,GameScenes.SPACECENTER,GameScenes.TRACKSTATION)]
    public sealed class RingworldScenario : ScenarioModule
    {
        public static RingworldScenario Instance;
        internal readonly Dictionary<string,VesselRecord> Vessels=new Dictionary<string,VesselRecord>();
        internal readonly HashSet<string> Discoveries=new HashSet<string>();
        internal readonly ExpeditionJournal Research=new ExpeditionJournal();
        internal bool Expedition;
        internal readonly List<ConfigNode> Rings=new List<ConfigNode>();
        internal string ActiveRingId="primary";
        private ConfigNode options;
        internal ConfigNode Options {get{return options;}set{options=value;if(value!=null){if(!value.HasValue("ringId"))value.AddValue("ringId","primary");ActiveRingId=value.GetValue("ringId")??"primary";int i=Rings.FindIndex(n=>(n.GetValue("ringId")??"primary")==ActiveRingId);if(i<0)Rings.Add(value);else Rings[i]=value;}}}
        internal ConfigNode RingOptions(string id){GetOptions();return Rings.Find(n=>(n.GetValue("ringId")??"primary")==id);}
        private readonly Dictionary<ConfigNode,Settings> settingsCache=new Dictionary<ConfigNode,Settings>();
        internal Settings RingSettings(string id){var n=RingOptions(id);if(n==null)return null;Settings s;if(!settingsCache.TryGetValue(n,out s)){s=Settings.Load();s.Apply(n);settingsCache[n]=s;}return s;}
        internal void SelectRing(string id){var n=RingOptions(id);if(n!=null){options=n;ActiveRingId=id;}}
        internal bool Occupied(string id)
        {
            // The editor runs in Flight, where the vessel list includes unloaded craft.
            // Recovered/destroyed vessels must not leave an undeletable phantom habitat.
            var stale=new List<string>();bool occupied=false;
            foreach(var pair in Vessels)
            {
                if(pair.Value.RingId!=id)continue;
                if(HighLogic.LoadedSceneIsFlight&&FlightGlobals.ActiveVessel!=null&&!FlightGlobals.Vessels.Exists(v=>v!=null&&v.id.ToString()==pair.Key&&v.state!=Vessel.State.DEAD))stale.Add(pair.Key);
                else occupied=true;
            }
            foreach(var key in stale)Vessels.Remove(key);return occupied;
        }
        internal ConfigNode GetOptions()
        {
            if(Options==null)
            {
                if(Vessels.Count==0&&Discoveries.Count==0&&Research.Receipts.Count==0)
                {
                    var configured=RingConfigPacks.Load();
                    if(configured!=null&&configured.Count>0)
                    {
                        Rings.AddRange(configured);options=Rings[0];ActiveRingId=options.GetValue("ringId");
                        return options;
                    }
                }
                var defaults=Settings.Load();
                if(Vessels.Count>0||Discoveries.Count>0)defaults.GenerationVersion=1;
                else
                {
                    var nodes=GameDatabase.Instance.GetConfigNodes("NIVEN_RINGWORLD");int seed;
                    if(nodes.Length==0||!int.TryParse(nodes[0].GetValue("seed"),out seed))seed=BitConverter.ToInt32(Guid.NewGuid().ToByteArray(),0);
                    defaults.Geometry.P.Seed=seed;
                }
                Options=defaults.Save();RingQualityPresets.Apply(Options,6); // Safe starting point; existing saved choices are untouched.
            }
            return Options;
        }
        private static string Num(double x) { return x.ToString("R",CultureInfo.InvariantCulture); }
        private static double Read(ConfigNode n,string key,double fallback=0)
        { double v;return double.TryParse(n.GetValue(key),NumberStyles.Float,CultureInfo.InvariantCulture,out v)&&RingParameters.Finite(v)?v:fallback; }
        public override void OnAwake() { base.OnAwake();Instance=this; }
        public override void OnLoad(ConfigNode node)
        {
            Rings.Clear();settingsCache.Clear();options=null;ActiveRingId=node.GetValue("activeRing")??"primary";
            foreach(var n in node.GetNodes("RING")){var copy=n.CreateCopy();copy.name="OPTIONS";if(!string.IsNullOrEmpty(copy.GetValue("ringId"))&&!Rings.Exists(r=>r.GetValue("ringId")==copy.GetValue("ringId")))Rings.Add(copy);}
            if(Rings.Count>0){options=Rings.Find(n=>n.GetValue("ringId")==ActiveRingId)??Rings[0];ActiveRingId=options.GetValue("ringId");}
            else Options=node.HasNode("OPTIONS")?node.GetNode("OPTIONS").CreateCopy():null;
            base.OnLoad(node);Instance=this;Vessels.Clear();Discoveries.Clear();Expedition=node.GetValue("expedition")=="True";
            foreach(var n in node.GetNodes("VESSEL"))
            {
                string id=n.GetValue("id");Guid parsed;
                if(!Guid.TryParse(id,out parsed)) continue;
                var r=new VesselRecord{Id=id,RingId=n.GetValue("ringId")??"primary",Landed=n.GetValue("landed")=="True",Epoch=Read(n,"frameEpoch"),Position=new DVec(Read(n,"x"),Read(n,"y"),Read(n,"z")),Velocity=new DVec(Read(n,"vx"),Read(n,"vy"),Read(n,"vz")),
                    Rotation=new Quaternion((float)Read(n,"qx"),(float)Read(n,"qy"),(float)Read(n,"qz"),(float)Read(n,"qw",1))};
                // Outside Flight, old airborne snapshots must not override a stock
                // orbit that has continued advancing since the snapshot was saved.
                if(r.Position.Length>1000&&(r.Landed||HighLogic.LoadedSceneIsFlight)) Vessels[id]=r;
            }
            foreach(string id in node.GetValues("discovery")) Discoveries.Add(id);
            Research.Load(node);
        }
        public override void OnSave(ConfigNode node)
        {
            if(RingworldFlight.Instance!=null) RingworldFlight.Instance.Capture();
            node.AddNode(GetOptions().CreateCopy());node.AddValue("activeRing",ActiveRingId);
            foreach(var ring in Rings){var copy=ring.CreateCopy();var settings=RingSettings(ring.GetValue("ringId")??"primary");if(settings!=null)settings.SaveAnchorFallback(copy);copy.name="RING";node.AddNode(copy);}
            base.OnSave(node);node.AddValue("formatVersion",5);node.AddValue("positionReference","vesselRoot");node.AddValue("expedition",Expedition);
            foreach(var r in Vessels.Values)
            {
                var n=node.AddNode("VESSEL");n.AddValue("id",r.Id);n.AddValue("ringId",r.RingId);n.AddValue("landed",r.Landed);n.AddValue("frameEpoch",Num(r.Epoch));
                n.AddValue("x",Num(r.Position.X));n.AddValue("y",Num(r.Position.Y));n.AddValue("z",Num(r.Position.Z));
                n.AddValue("vx",Num(r.Velocity.X));n.AddValue("vy",Num(r.Velocity.Y));n.AddValue("vz",Num(r.Velocity.Z));
                n.AddValue("qx",Num(r.Rotation.x));n.AddValue("qy",Num(r.Rotation.y));n.AddValue("qz",Num(r.Rotation.z));n.AddValue("qw",Num(r.Rotation.w));
            }
            foreach(string id in Discoveries) node.AddValue("discovery",id);
            Research.Save(node);
        }
        public void OnDestroy() { if(Instance==this){Instance=null;RingLighting.Clear();} }
    }
}

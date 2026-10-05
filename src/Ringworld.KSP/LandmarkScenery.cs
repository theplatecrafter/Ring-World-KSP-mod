using System;
using System.Collections.Generic;
using System.Globalization;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    // Fixed seeded-world coordinates; no moving Rigidbody megastructures.
    // One new prefab per frame, independent of the small near-ground tile radius.
    internal sealed class LandmarkScenery : IDisposable
    {
        private sealed class Entry
        {
            internal string Key,Kind;internal double Along,Across,Base,Range,Phase;
            internal Vector3 Size;internal GameObject Object;internal Collider[] Contacts;
            internal DVec Position;internal Quaternion Rotation;
        }
        private readonly List<ConfigNode> templates=new List<ConfigNode>();private double cellSize=2000000,chance=.15,reach=500000;private long lastCellX=long.MinValue,lastCellY;
        private readonly List<Entry> entries=new List<Entry>();private readonly Settings settings;
        internal int Count {get{int n=0;foreach(var e in entries)if(e.Object!=null)n++;return n;}}
        internal LandmarkScenery(Settings settings)
        {
            this.settings=settings;
            templates.AddRange(GameDatabase.Instance.GetConfigNodes("RINGWORLD_COLOSSUS_ASSET"));
            foreach(var config in GameDatabase.Instance.GetConfigNodes("RINGWORLD_COLOSSUS_DISTRIBUTION"))
            {cellSize=Math.Max(1000000,Number(config,"cellSize",2000000));chance=Math.Max(0,Math.Min(1,Number(config,"occupancy",.15)));}
            foreach(var template in templates)reach=Math.Max(reach,Number(template,"range",250000)+Number(template,"width",0)+Number(template,"height",0)+Number(template,"depth",0));
            foreach(var n in GameDatabase.Instance.GetConfigNodes("RINGWORLD_LANDMARK_ASSET"))
            {
                var site=settings.Terrain.Landmarks.Find(l=>l.Id==n.GetValue("site"));if(site==null)continue;
                double a=site.Along+Number(n,"along",0),b=site.Across+Number(n,"across",0);
                var size=new Vector3((float)Number(n,"width",100),(float)Number(n,"height",100),(float)Number(n,"depth",100));
                if(size.x<=0||size.y<=0||size.z<=0||Math.Abs(b)+size.z/2>=settings.Geometry.P.Width/2)continue;
                var floor=settings.Terrain.Sample(a,b);if(floor.Wet)continue;
                entries.Add(new Entry{Kind=n.GetValue("kind"),Along=a,Across=b,Base=floor.Height+Number(n,"aboveGround",0),Size=size,Range=Math.Max(1000,Number(n,"range",60000))});
            }
        }
        private static double Number(ConfigNode n,string key,double fallback)
        {double x;return double.TryParse(n.GetValue(key),NumberStyles.Float,CultureInfo.InvariantCulture,out x)&&RingParameters.Finite(x)?x:fallback;}
        private void Scatter(DVec observer)
        {
            if(templates.Count==0)return;var at=settings.Geometry.Coordinates(observer);
            long x=(long)Math.Floor(at.Along/(cellSize*.1)),y=(long)Math.Floor(at.Across/(cellSize*.1));
            if(x==lastCellX&&y==lastCellY)return;lastCellX=x;lastCellY=y;
            var keep=new HashSet<string>();
            foreach(var candidate in ColossusDistribution.Nearby(settings.Terrain,at.Along,at.Across,cellSize,chance,reach))
            {
                keep.Add(candidate.Key);if(entries.Exists(e=>e.Key==candidate.Key))continue;
                var n=templates[Math.Min(templates.Count-1,(int)(candidate.Variant*templates.Count))];
                var size=new Vector3((float)Number(n,"width",100),(float)Number(n,"height",100),(float)Number(n,"depth",100));
                if(size.x<=0||size.y<=0||size.z<=0||Math.Abs(candidate.Across)+size.z/2>=settings.Geometry.P.Width/2)continue;
                var floor=settings.Terrain.Sample(candidate.Along,candidate.Across);if(floor.Wet)continue;
                entries.Add(new Entry{Key=candidate.Key,Kind=n.GetValue("kind"),Along=candidate.Along,Across=candidate.Across,Base=floor.Height+Number(n,"aboveGround",0),Size=size,Range=Math.Max(1000,Number(n,"range",250000))});
            }
            for(int i=entries.Count-1;i>=0;i--)if(entries[i].Key!=null&&!keep.Contains(entries[i].Key))
            {if(entries[i].Object!=null)UnityEngine.Object.Destroy(entries[i].Object);entries.RemoveAt(i);}
        }
        internal void Update(DVec observer,Vector3d star,PhysicMaterial friction)
        {
            Scatter(observer);bool built=false;
            foreach(var e in entries)
            {
                var p=settings.Geometry.Position(e.Along,e.Across,e.Base+e.Size.y/2);double distance=(observer-p).Length;
                if(distance>e.Range+e.Size.magnitude)
                {if(e.Object!=null){UnityEngine.Object.Destroy(e.Object);e.Object=null;}continue;}
                if(e.Object==null)
                {
                    if(built)continue;
                    // Older saves can contain craft anywhere. Never materialize a
                    // new contact mesh around an already resident vessel.
                    bool occupied=false;var f=RingworldFlight.Instance;
                    if(f!=null)foreach(var v in FlightGlobals.VesselsLoaded)
                        if(f.Owns(v)&&v.Landed&&(f.Position(v)-p).Length<e.Size.magnitude*.5+100&&settings.Geometry.Coordinates(f.Position(v)).Altitude<e.Base+30){occupied=true;break;}
                    if(occupied)continue;
                    e.Object=SceneryAssets.Create(e.Kind,0);if(e.Object==null)continue;
                    e.Position=p;e.Phase=settings.Geometry.OrientationRadians;
                    e.Rotation=Quaternion.FromToRotation(Vector3.up,ConvertVector.Unity(settings.Geometry.Up(p)));
                    e.Object.name="Ringworld landmark: "+e.Kind;e.Object.transform.localScale=e.Size;
                    e.Contacts=e.Object.GetComponentsInChildren<Collider>();foreach(var c in e.Contacts)c.sharedMaterial=friction;
                    built=true;
                }
                double delta=settings.Geometry.OrientationRadians-e.Phase;
                e.Object.transform.position=star+ConvertVector.Ksp(settings.Geometry.RotateAroundAxis(e.Position,delta));
                e.Object.transform.rotation=settings.AxisRotation(delta)*e.Rotation;
                bool close=distance<e.Size.magnitude*.5+2500;
                foreach(var c in e.Contacts)if(c.enabled!=close)c.enabled=close;
            }
        }
        public void Dispose(){foreach(var e in entries)if(e.Object!=null)UnityEngine.Object.Destroy(e.Object);entries.Clear();}
    }
}

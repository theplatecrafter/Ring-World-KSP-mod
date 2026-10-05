using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Ringworld.Core;
using CoastState = Ringworld.Core.FlightState;
using UnityEngine;
namespace NivenRingworld
{
    internal sealed class RingTrajectory : MonoBehaviour
    {
        private LineRenderer line;private Material material;private bool computing;
        private float nextPrediction;private Guid vesselId;private CoastState packedState;private double packedEpoch=double.NaN;
        private readonly List<int> encounters=new List<int>();
        private readonly List<double> encounterTimes=new List<double>();
        private readonly List<bool> pathInside=new List<bool>(),encounterEntries=new List<bool>();
        internal int EncounterCount {get{return encounters.Count;}}
        internal Orbit PredictedOrbit;
        internal void ResetPrediction(){StopAllCoroutines();computing=false;packedEpoch=double.NaN;nextPrediction=0;PredictedOrbit=null;encounters.Clear();if(line!=null)line.positionCount=0;}
        internal int PredictionCommits;
        internal static Orbit SolarPatch(Vessel v,CelestialBody star)
        {
            if(v!=null&&v.orbit!=null&&star!=null)
            {
                RingAnchorState parent;string error;
                if(RingAnchorEphemeris.TryRelative(RingAnchorEphemeris.Id(v.orbit.referenceBody),star,Planetarium.GetUniversalTime(),out parent,out error))return v.orbit;
            }
            return null;
        }
        private static bool Tracking {get{return HighLogic.LoadedScene==GameScenes.TRACKSTATION;}}
        private static Vessel Selected {get{return Tracking?(PlanetariumCamera.fetch!=null&&PlanetariumCamera.fetch.target!=null?PlanetariumCamera.fetch.target.vessel:null):FlightGlobals.ActiveVessel;}}
        private static Settings CurrentSettings {get{return Tracking?TrackingRing.Settings:RingworldFlight.Instance==null?null:RingworldFlight.Instance.Settings;}}
        private static CelestialBody CurrentStar {get{return Tracking?TrackingRing.Star:RingworldFlight.Instance==null?null:RingworldFlight.Instance.Star;}}
        internal string Status="Vacuum coast prediction";
        internal int PointCount {get{return line==null?0:line.positionCount;}}
        internal static DVec InertialAcceleration(DVec p,Settings s,double mu,double time=double.NaN)
        {return s.InertialGravity(p,double.IsNaN(time)?Planetarium.GetUniversalTime():time)+s.Geometry.RibbonAcceleration(p);}
        internal void PackedUpdate(RingworldFlight f)
        {
            var v=FlightGlobals.ActiveVessel;double now=Planetarium.GetUniversalTime();
            if(v==null||!v.packed||f.Active||v.mainBody!=f.Star){packedEpoch=double.NaN;return;}
            if(double.IsNaN(packedEpoch)||v.id!=vesselId||now<packedEpoch)
            {
                packedState=new CoastState(ConvertVector.Core(ConvertVector.Orbit(v.orbit.getRelativePositionAtUT(now)))-f.Settings.AnchorAt(now).Position,ConvertVector.Core(ConvertVector.Orbit(v.orbit.getOrbitalVelocityAtUT(now)))-f.Settings.AnchorAt(now).Velocity);
                vesselId=v.id;packedEpoch=now;return;
            }
            double remaining=now-packedEpoch;
            if(remaining<=0)return;
            // Bound CPU work at extreme stock warp rates; do not silently discard time.
            if(remaining>86400){TimeWarp.SetRate(0,true);packedEpoch=double.NaN;return;}
            while(remaining>1e-6)
            {
                double dt=Math.Min(300,remaining);
                var c=f.Settings.Geometry.Coordinates(packedState.Position);
                double distance=Math.Max(100,Math.Abs(c.Altitude));
                double radialSpeed=Math.Abs(DVec.Dot(f.Settings.Geometry.Up(packedState.Position),packedState.Velocity));
                dt=Math.Min(dt,Math.Max(.01,distance/(radialSpeed+Math.Abs(DVec.Dot(f.Settings.Geometry.Axis,packedState.Velocity))+1)*.1));
                dt=Math.Min(dt,Math.Max(.01,Math.Sqrt(distance/Math.Max(1,packedState.Velocity.Length*packedState.Velocity.Length/packedState.Position.Length))*.1));
                packedState=NumericalFlight.Step(packedState,dt,(p,u)=>InertialAcceleration(p,f.Settings,f.Star.gravParameter,now-remaining+dt*.5));remaining-=dt;
                if(f.Settings.Geometry.InArrivalRegion(packedState.Position,false))TimeWarp.SetRate(0,true);
            }
            packedEpoch=now;
            v.orbit.UpdateFromStateVectors(ConvertVector.Orbit(ConvertVector.Ksp(packedState.Position+f.Settings.AnchorAt(now).Position)),ConvertVector.Orbit(ConvertVector.Ksp(packedState.Velocity+f.Settings.AnchorAt(now).Velocity)),f.Star,now);
        }
        public void Update()
        {
            var f=RingworldFlight.Instance;var v=Selected;var settings=CurrentSettings;var star=CurrentStar;
            bool show=settings!=null&&settings.ShowTrajectory&&v!=null&&SolarPatch(v,star)!=null&&(Tracking||MapView.MapIsEnabled);
            if(line!=null)line.enabled=false; // map overlay below remains visible through the ribbon
            if(!show)return;
            if(line==null)
            {
                var shader=Shader.Find("Unlit/Color")??Shader.Find("KSP/Unlit");if(shader==null)return;
                material=new Material(shader){color=new Color(.2f,.9f,1f)};
                var obj=new GameObject("Ringworld numerical coast trajectory");obj.transform.SetParent(transform,false);obj.layer=10;
                line=obj.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;line.positionCount=0;
            }
            if(PlanetariumCamera.Camera!=null&&line.positionCount>0)
            {
                float distance=Vector3.Distance(PlanetariumCamera.Camera.transform.position,line.GetPosition(0));
                line.widthMultiplier=Mathf.Max(.002f,distance*.001f);
            }
            if(!Tracking&&line.positionCount>0&&v.mainBody==f.Star)line.SetPosition(0,(Vector3)ScaledSpace.LocalToScaledSpace(f.Center+ConvertVector.Ksp(f.Position(v))));
            if(!computing&&Time.realtimeSinceStartup>=nextPrediction){nextPrediction=Time.realtimeSinceStartup+1f/30;StartCoroutine(Predict(f,v));}
        }
        public void OnGUI()
        {
            var f=RingworldFlight.Instance;var v=Selected;var settings=CurrentSettings;var star=CurrentStar;
            if(Event.current.type!=EventType.Repaint||(!Tracking&&!MapView.MapIsEnabled)||settings==null||!settings.ShowTrajectory||v==null||SolarPatch(v,star)==null||line==null||line.positionCount<2||PlanetariumCamera.Camera==null)return;
            var color=GUI.color;var matrix=GUI.matrix;int depth=GUI.depth;
            GUI.depth=100;GUI.color=new Color(.15f,.95f,1f,.95f);
            try
            {
                var previous=PlanetariumCamera.Camera.WorldToScreenPoint(line.GetPosition(0));
                for(int i=1;i<line.positionCount;i++)
                {
                    var next=PlanetariumCamera.Camera.WorldToScreenPoint(line.GetPosition(i));
                    int encounter=encounters.IndexOf(i);
                    if(encounter>=0&&next.z>0)
                    {
                        var at=new Vector2(next.x,Screen.height-next.y);
                        GUI.color=new Color(.2f,1f,.65f);DrawMarker(at,encounterEntries[encounter],matrix);
                        if(new Rect(at.x-16,at.y-16,32,32).Contains(Event.current.mousePosition))GUI.Label(new Rect(at.x+16,at.y-10,300,24),(encounterEntries[encounter]?"Ringworld encounter +":"Ringworld escape +")+encounterTimes[encounter].ToString("F1")+" s");
                    }
                    GUI.color=i<pathInside.Count&&pathInside[i]?new Color(1f,.65f,.15f,.95f):new Color(.15f,.95f,1f,.95f);
                    if(previous.z>0&&next.z>0)
                    {
                        var a=new Vector2(previous.x,Screen.height-previous.y);var b=new Vector2(next.x,Screen.height-next.y);
                        if(!((a.x<0&&b.x<0)||(a.x>Screen.width&&b.x>Screen.width)||(a.y<0&&b.y<0)||(a.y>Screen.height&&b.y>Screen.height)))
                        {
                            float length=(b-a).magnitude;
                            if(length>.1f&&length<Screen.width*100)
                            {
                                GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,a);
                                GUI.DrawTexture(new Rect(a.x,a.y-1,length,2),Texture2D.whiteTexture);GUI.matrix=matrix;
                            }
                        }
                    }
                    previous=next;
                }
            }
            finally{GUI.color=color;GUI.matrix=matrix;GUI.depth=depth;}
        }
        private static void DrawMarker(Vector2 at,bool entry,Matrix4x4 matrix)
        {
            for(int k=0;k<20;k++)
            {
                float a=k*Mathf.PI/10,b=(k+1)*Mathf.PI/10;
                DrawStroke(at+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*7,at+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*7,matrix);
            }
            float direction=entry?1:-1;
            var tip=at+Vector2.right*direction*13;
            DrawStroke(at-Vector2.right*direction*3,tip,matrix);
            DrawStroke(tip,tip+new Vector2(-direction*4,-4),matrix);DrawStroke(tip,tip+new Vector2(-direction*4,4),matrix);
        }
        private static void DrawStroke(Vector2 a,Vector2 b,Matrix4x4 matrix)
        {
            GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,a);
            GUI.DrawTexture(new Rect(a.x,a.y-.75f,(b-a).magnitude,1.5f),Texture2D.whiteTexture);GUI.matrix=matrix;
        }
        private IEnumerator Predict(RingworldFlight f,Vessel v)
        {
            computing=true;
            try
            {
                var settings=CurrentSettings;var star=CurrentStar;var g=settings.Geometry;bool rotating=f!=null&&f.Owns(v);double start=Planetarium.GetUniversalTime();var patch=SolarPatch(v,star);if(patch==null)yield break;
                double elapsed=rotating?start-f.FrameEpoch:0;
                DVec position=f!=null?f.Position(v):RingAnchorEphemeris.OrbitRelative(patch,star,start).Position-settings.AnchorAt(start).Position,velocity=f!=null?f.Velocity(v):RingAnchorEphemeris.OrbitRelative(patch,star,start).Velocity-settings.AnchorAt(start).Velocity;
                if(v.mainBody!=star){start=Math.Max(start,patch.StartUT);position=RingAnchorEphemeris.OrbitRelative(patch,star,start).Position-settings.AnchorAt(start).Position;velocity=RingAnchorEphemeris.OrbitRelative(patch,star,start).Velocity-settings.AnchorAt(start).Velocity;}
                if(rotating){velocity=g.ToInertialVelocity(position,velocity,elapsed);position=g.ToInertialPosition(position,elapsed);}
                                var state=new CoastState(position,velocity);var points=new List<Vector3>();double time=0;
                var crossings=new List<int>();var times=new List<double>();var insidePoints=new List<bool>();var entries=new List<bool>();
                bool wasInside=g.InArrivalRegion(position,rotating);bool entryFound=false;
                string result="Numerical vacuum coast: celestial gravity + uniform ribbon; no thrust or manoeuvres";
                // At low rendering FPS a fixed 1.5 ms slice stretches short predictions
                // over many wall-clock frames. Spend at most 10% of the last frame,
                // capped at 8 ms, without changing numerical steps or encounter tests.
                double sliceBudget=Math.Max(1.5,Math.Min(8,Time.unscaledDeltaTime*100));
                var slice=System.Diagnostics.Stopwatch.StartNew();
                for(int i=0;i<4096&&time<=settings.PredictionSeconds;i++)
                {
                    if(v==null||v!=Selected||settings!=CurrentSettings||rotating!=(f!=null&&f.Owns(v)))yield break;
                                        var coord=g.Coordinates(state.Position);
                    bool inside=g.InArrivalRegion(state.Position,wasInside);
                    if(inside!=wasInside)
                    {
                        entries.Add(inside);crossings.Add(points.Count);times.Add(start+time-Planetarium.GetUniversalTime());entryFound=true;
                        wasInside=inside;
                    }
                    if(Math.Abs(coord.Across)<g.P.Width/2&&coord.Altitude>=-1300&&coord.Altitude<g.P.AtmosphereHeight+1&&settings.Atmosphere)
                    {result=time==0?"In atmosphere: no reliable vacuum trajectory; aerodynamic prediction pending":"Coast ends at atmosphere entry (+"+time.ToString("F1")+" s)";break;}
                    if((state.Position+settings.AnchorAt(start+time).Position).Length<star.Radius){result="Coast ends at the Sun";break;}
                    double materialAlong=RingGeometry.Wrap(coord.Along-(g.P.Omega*(start+time)-g.OrientationRadians)*g.P.Radius,g.P.Circumference);
                    if(Math.Abs(coord.Across)<g.P.Width/2&&coord.Altitude>=settings.UndersideAltitude&&coord.Altitude<=settings.Terrain.Sample(materialAlong,coord.Across).Height)
                    {result="Coast ends at terrain contact";break;}
                    if(Math.Abs(Math.Abs(coord.Across)-g.P.Width/2)<2&&coord.Altitude>=settings.UndersideAltitude&&coord.Altitude<=g.P.WallHeight)
                    {result="Coast ends at rim wall";break;}
                    // The map uses the same frozen rotating chart as the local scene.
                    var display=rotating?g.RotateAroundAxis(state.Position,-g.P.Omega*(elapsed+time)):state.Position+settings.AnchorAt(start+time).Position-settings.AnchorAt(Planetarium.GetUniversalTime()).Position;
                    points.Add((Vector3)ScaledSpace.LocalToScaledSpace(settings.Center+ConvertVector.Ksp(display)));insidePoints.Add(inside);
                    double gap=Math.Max(1,Math.Abs(coord.Altitude-g.P.AtmosphereHeight));
                    double radial=Math.Abs(DVec.Dot(g.Up(state.Position),state.Velocity));
                    double dt=Math.Min(120,Math.Max(.01,Math.Min(gap/(radial+1)*.2,Math.Sqrt(gap/(state.Velocity.Length*state.Velocity.Length/state.Position.Length+1))*.2)));
                    if(coord.Altitude<g.P.WallHeight&&Math.Abs(DVec.Dot(g.Axis,state.Velocity))>1)dt=Math.Min(dt,Math.Max(.001,Math.Abs(Math.Abs(coord.Across)-g.P.Width/2)/Math.Abs(DVec.Dot(g.Axis,state.Velocity))*.2));
                    // Resolve the same annular handoff boundary used by automatic arrival.
                    // The straight-line estimate also catches crossings between widely spaced coast samples.
                    if(!inside){double entry=g.TimeToArrival(state.Position,state.Velocity,dt);if(!double.IsInfinity(entry))dt=Math.Min(dt,Math.Max(.0001,entry+.0001));}
                    dt=Math.Min(dt,settings.PredictionSeconds-time);if(dt<=0)break;
                    state=NumericalFlight.Step(state,dt,(p,u)=>InertialAcceleration(p,settings,star.gravParameter,start+time+dt*.5));time+=dt;
                    if(slice.Elapsed.TotalMilliseconds>=sliceBudget){yield return null;slice.Restart();}
                }
                if(time<settings.PredictionSeconds&&points.Count>=4096)result="Coast truncated at numerical step budget";
                PredictionCommits++;
                Status=(entryFound?"Ringworld frame encounter marked. ":"")+result;PredictedOrbit=patch;encounters.Clear();encounters.AddRange(crossings);encounterTimes.Clear();encounterTimes.AddRange(times);encounterEntries.Clear();encounterEntries.AddRange(entries);pathInside.Clear();pathInside.AddRange(insidePoints);line.positionCount=points.Count;line.SetPositions(points.ToArray());
            }
            finally{computing=false;}
        }
        public void OnDestroy(){if(material!=null)Destroy(material);}
    }
    [HarmonyPatch(typeof(OrbitRendererBase),"DrawSpline")]
    internal static class RingOrbitSplinePatch
    {
        private static readonly HashSet<OrbitRendererBase> Hidden=new HashSet<OrbitRendererBase>();
        private static readonly System.Reflection.MethodInfo GetOrbit=AccessTools.PropertyGetter(typeof(OrbitRendererBase),"orbit");
        private static bool Prefix(OrbitRendererBase __instance)
        {
            bool hide=GetOrbit!=null&&ShouldHide(GetOrbit.Invoke(__instance,null) as Orbit);
            // Vectrosity retains the previous mesh when a draw call is skipped.
            Hidden.RemoveWhere(renderer=>renderer==null);
            if(hide&&__instance.OrbitLine!=null){__instance.OrbitLine.active=false;Hidden.Add(__instance);}
            else if(Hidden.Remove(__instance)&&__instance.OrbitLine!=null)__instance.OrbitLine.active=true;
            return !hide;
        }
        internal static bool ShouldHide(Orbit orbit)
        {
            if(orbit==null)return false;
            var f=RingworldFlight.Instance;var v=FlightGlobals.ActiveVessel;
            if(HighLogic.LoadedScene==GameScenes.TRACKSTATION&&TrackingRing.Trajectory!=null&&TrackingRing.Settings!=null&&TrackingRing.Settings.ShowTrajectory)
                return TrackingRing.Trajectory.PointCount>=2&&ReferenceEquals(orbit,TrackingRing.Trajectory.PredictedOrbit);
            return f!=null&&v!=null&&((f.Owns(v)&&ReferenceEquals(orbit,v.orbit))||
                (f.Settings!=null&&f.Settings.ShowTrajectory&&f.trajectory!=null&&f.trajectory.PointCount>=2&&ReferenceEquals(orbit,f.trajectory.PredictedOrbit)));
        }
    }
    [HarmonyPatch(typeof(PatchRendering),"UpdatePR")]
    internal static class RingPatchedConicLine
    {
        private static bool Prefix(PatchRendering __instance)
        {
            if(!RingOrbitSplinePatch.ShouldHide(__instance.patch))return true;
            __instance.DestroyVector();return false;
        }
    }
}

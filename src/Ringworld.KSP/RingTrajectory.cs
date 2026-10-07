using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Ringworld.Core;
using CoastState = Ringworld.Core.FlightState;
using UnityEngine;
using Vectrosity;
namespace NivenRingworld
{
    internal sealed class RingTrajectory : MonoBehaviour
    {
        private VectorLine line,markers;private bool computing;
        private readonly List<Vector2> screenPoints=new List<Vector2>(),markerPoints=new List<Vector2>();
        private readonly List<Color32> segmentColors=new List<Color32>();
        private readonly List<TrajectorySample> samples=new List<TrajectorySample>();
        private readonly List<Vector2> markerScreens=new List<Vector2>();
        private Settings displayedSettings;private CelestialBody displayedStar;private Vessel displayedVessel;
        internal int RenderFrames;internal Vector2 RenderedHead;internal int RenderedPointCount;
        private readonly Color32 coastColor=new Color(.15f,.95f,1f,.95f),ringColor=new Color(1f,.65f,.15f,.95f);
        private float nextPrediction,nextRelevance;private bool relevant;private Vessel checkedVessel;private Settings checkedSettings;private Guid vesselId;private CoastState packedState;private double packedEpoch=double.NaN;
        private readonly List<int> encounters=new List<int>();
        private readonly List<double> encounterTimes=new List<double>();
        private readonly List<bool> pathInside=new List<bool>(),encounterEntries=new List<bool>();
        internal int EncounterCount {get{return encounters.Count;}}
        internal Orbit PredictedOrbit;
        internal void ResetPrediction(){StopAllCoroutines();computing=false;packedEpoch=double.NaN;nextPrediction=0;PredictedOrbit=null;encounters.Clear();encounterTimes.Clear();encounterEntries.Clear();pathInside.Clear();samples.Clear();HideLines();}
        private void Start(){Camera.onPreCull+=PrepareCamera;}
        private void HideLines(){if(line!=null)line.active=false;if(markers!=null)markers.active=false;RenderedPointCount=0;}
        internal static bool LandedOnRing(Vessel v)
        {
            VesselRecord record;
            if(v==null)return false;
            var f=RingworldFlight.Instance;
            return (f!=null&&f.Owns(v)&&(v.Landed||v.Splashed))||
                (RingResidence.Saved(v,out record)&&record.Landed);
        }
        internal bool Replaces(Vessel v){return v!=null&&v==displayedVessel&&displayedSettings==CurrentSettings&&displayedSettings!=null&&displayedSettings.ShowTrajectory&&samples.Count>=2;}
        internal int PredictionCommits;
        internal static Orbit SolarPatch(Vessel v,CelestialBody star)
        {
            if(v!=null&&RingAnchorEphemeris.OrbitReady(v.orbit)&&star!=null)
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
        internal int PointCount {get{return samples.Count;}}
        internal static DVec InertialAcceleration(DVec p,Settings s,double mu,double time=double.NaN)
        {return s.InertialGravity(p,double.IsNaN(time)?Planetarium.GetUniversalTime():time)+s.Geometry.RibbonAcceleration(p);}
        internal void PackedUpdate(RingworldFlight f)
        {
            var v=FlightGlobals.ActiveVessel;double now=Planetarium.GetUniversalTime();
            RingAnchorState orbital;
            if(v==null||!v.packed||f.Active||v.mainBody!=f.Star||!RingAnchorEphemeris.TryVesselRelative(v,f.Star,now,out orbital)){packedEpoch=double.NaN;return;}
            if(double.IsNaN(packedEpoch)||v.id!=vesselId||now<packedEpoch)
            {
                packedState=new CoastState(orbital.Position-f.Settings.AnchorAt(now).Position,orbital.Velocity-f.Settings.AnchorAt(now).Velocity);
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
            var v=Selected;var settings=CurrentSettings;var star=CurrentStar;
            bool show=settings!=null&&settings.ShowTrajectory&&v!=null&&!LandedOnRing(v)&&SolarPatch(v,star)!=null&&(Tracking||MapView.MapIsEnabled);
            if(show&&(v!=checkedVessel||settings!=checkedSettings||Time.realtimeSinceStartup>=nextRelevance))
            {
                nextRelevance=Time.realtimeSinceStartup+.25f;checkedVessel=v;checkedSettings=settings;var f=RingworldFlight.Instance;
                relevant=(f!=null&&f.Owns(v))||
                    !double.IsInfinity(TrackingRing.Encounter(v.orbit,settings.Geometry,Planetarium.GetUniversalTime(),settings.PredictionSeconds,default(DVec),t=>settings.AnchorAt(t).Position,star));
            }
            show&=relevant;
            if(!show){if(samples.Count>0||computing)ResetPrediction();else HideLines();return;}
            if(v!=displayedVessel||settings!=displayedSettings||star!=displayedStar)
            {ResetPrediction();displayedVessel=v;displayedSettings=settings;displayedStar=star;}
            if(!computing&&Time.realtimeSinceStartup>=nextPrediction)
            {nextPrediction=Time.realtimeSinceStartup+1f/30;StartCoroutine(Predict(RingworldFlight.Instance,v));}
        }
        // Prediction is independent of the camera. Vectrosity is KSP's own orbit-line
        // backend; only projection runs at camera time, after scaled-space rebasing.
        private void PrepareCamera(Camera camera)
        {
            if(camera!=PlanetariumCamera.Camera)return;
            Draw(camera);
        }
        internal void Draw(Camera camera)
        {
            var v=Selected;double now=Planetarium.GetUniversalTime();
            if(camera==null||(!Tracking&&!MapView.MapIsEnabled)||v==null||v!=displayedVessel||
               CurrentSettings==null||displayedSettings!=CurrentSettings||displayedStar!=CurrentStar||!CurrentSettings.ShowTrajectory||
               LandedOnRing(v)||samples.Count<2||now>=samples[samples.Count-1].Time)
            {HideLines();return;}
            screenPoints.Clear();segmentColors.Clear();markerPoints.Clear();markerScreens.Clear();
            // Subtract in doubles before converting to Unity floats, even at a distant star.
            var observer=ConvertVector.Core(ScaledSpace.ScaledToLocalSpace(camera.transform.position)-displayedStar.position);
            var rotation=Quaternion.Inverse(camera.transform.rotation);var projection=camera.projectionMatrix;
            int first=TrajectorySample.Segment(samples,now);
            var head=TrajectorySample.Interpolate(samples[first],samples[first+1],Math.Max(now,samples[first].Time));
            Vector3d actual;
            if(RingMapFrame.TryPosition(v,out actual))head=ConvertVector.Core(actual-displayedStar.position);
            else if(now>=samples[0].Time)head=RingAnchorEphemeris.OrbitRelative(v.orbit,displayedStar,now).Position;
            var a=Project(camera,head,observer,rotation,projection);RenderedHead=new Vector2(a.x,a.y);
            for(int i=first;i<samples.Count-1&&screenPoints.Count<12000;i++)
            {
                double start=Math.Max(now,samples[i].Time),end=samples[i+1].Time;
                var b=Project(camera,samples[i+1].Position,observer,rotation,projection);
                AppendCurve(camera,i,start,end,a,b,observer,rotation,projection,0);
                a=b;
            }
            if(line==null&&screenPoints.Count>=2)line=new VectorLine("Ringworld numerical coast",screenPoints,2f,LineType.Discrete);
            if(line!=null)line.active=screenPoints.Count>=2;
            if(line!=null&&line.active){line.SetColors(segmentColors);line.Draw();FlushLine(line);}
            for(int k=0;k<encounters.Count;k++)
            {
                int i=encounters[k];
                if(i>=samples.Count||encounterTimes[k]<=now){markerScreens.Add(new Vector2(-10000,-10000));continue;}
                var point=Project(camera,samples[i].Position,observer,rotation,projection);
                var at=new Vector2(point.x,point.y);markerScreens.Add(at);
                if(point.z<=camera.nearClipPlane||!camera.pixelRect.Contains(at))continue;
                for(int j=0;j<20;j++)
                {float x=j*Mathf.PI/10,y=(j+1)*Mathf.PI/10;markerPoints.Add(at+new Vector2(Mathf.Cos(x),Mathf.Sin(x))*7);markerPoints.Add(at+new Vector2(Mathf.Cos(y),Mathf.Sin(y))*7);}
                float direction=encounterEntries[k]?1:-1;var tip=at+Vector2.right*direction*13;
                markerPoints.Add(at-Vector2.right*direction*3);markerPoints.Add(tip);
                markerPoints.Add(tip);markerPoints.Add(tip+new Vector2(-direction*4,-4));
                markerPoints.Add(tip);markerPoints.Add(tip+new Vector2(-direction*4,4));
            }
            if(markers==null&&markerPoints.Count>=2)markers=new VectorLine("Ringworld encounter markers",markerPoints,1.5f,LineType.Discrete);
            if(markers!=null)markers.active=markerPoints.Count>=2;if(markers!=null&&markers.active){markers.color=new Color(.2f,1f,.65f);markers.Draw();FlushLine(markers);}
            RenderedPointCount=screenPoints.Count;RenderFrames++;
        }
        private static void FlushLine(VectorLine nativeLine)
        {
            // Vectrosity's 2D backend marks a UI Graphic dirty. A map camera can
            // render after the normal Canvas update, so flush just our Graphic now
            // rather than forcing a layout rebuild of every KSP window.
            var graphic=nativeLine.rectTransform.GetComponent<UnityEngine.UI.Graphic>();
            if(graphic!=null)graphic.Rebuild(UnityEngine.UI.CanvasUpdate.PreRender);
        }
        private static Vector3 Project(Camera camera,DVec position,DVec observer,Quaternion rotation,Matrix4x4 projection)
        {
            var local=rotation*ConvertVector.Unity((position-observer)*ScaledSpace.InverseScaleFactor);
            // Unity cameras face +Z locally; the projection matrix uses -Z.
            var clip=projection*new Vector4(local.x,local.y,-local.z,1);
            float w=Mathf.Abs(clip.w)<1e-12f?1e-12f:clip.w;
            var rect=camera.pixelRect;
            return new Vector3(rect.x+(clip.x/w*.5f+.5f)*rect.width,rect.y+(clip.y/w*.5f+.5f)*rect.height,local.z);
        }
        private void AppendCurve(Camera camera,int segment,double start,double end,Vector3 a,Vector3 b,DVec observer,Quaternion rotation,Matrix4x4 projection,int depth)
        {
            double middle=(start+end)*.5;
            var m=Project(camera,TrajectorySample.Interpolate(samples[segment],samples[segment+1],middle),observer,rotation,projection);
            if(depth<6&&screenPoints.Count<12000&&(a.z>camera.nearClipPlane||b.z>camera.nearClipPlane||m.z>camera.nearClipPlane)&&
               ((new Vector2(m.x,m.y)-new Vector2((a.x+b.x)*.5f,(a.y+b.y)*.5f)).sqrMagnitude>.16f||
                (a.z>camera.nearClipPlane)!=(b.z>camera.nearClipPlane)))
            {AppendCurve(camera,segment,start,middle,a,m,observer,rotation,projection,depth+1);AppendCurve(camera,segment,middle,end,m,b,observer,rotation,projection,depth+1);return;}
            if(a.z<=camera.nearClipPlane||b.z<=camera.nearClipPlane)return;
            Vector2 from=new Vector2(a.x,a.y),to=new Vector2(b.x,b.y);
            if(!Clip(camera.pixelRect,ref from,ref to))return;
            screenPoints.Add(from);screenPoints.Add(to);segmentColors.Add(pathInside[segment]?ringColor:coastColor);
        }
        // Clip off-screen segments before giving them to the native UI line mesh.
        private static bool Clip(Rect r,ref Vector2 a,ref Vector2 b)
        {
            var d=b-a;float lo=0,hi=1;
            if(!ClipEdge(-d.x,a.x-r.xMin,ref lo,ref hi)||!ClipEdge(d.x,r.xMax-a.x,ref lo,ref hi)||
               !ClipEdge(-d.y,a.y-r.yMin,ref lo,ref hi)||!ClipEdge(d.y,r.yMax-a.y,ref lo,ref hi))return false;
            b=a+d*hi;a+=d*lo;return true;
        }
        private static bool ClipEdge(float p,float q,ref float lo,ref float hi)
        {if(Mathf.Abs(p)<1e-12f)return q>=0;float t=q/p;if(p<0){if(t>hi)return false;lo=Mathf.Max(lo,t);}else{if(t<lo)return false;hi=Mathf.Min(hi,t);}return true;}
        public void OnGUI()
        {
            // Only hover text uses IMGUI; no trajectory or marker geometry is drawn here.
            if(Event.current.type!=EventType.Repaint||RenderedPointCount==0)return;
            for(int k=0;k<markerScreens.Count;k++)
            {
                var at=markerScreens[k];at.y=Screen.height-at.y;
                if(new Rect(at.x-16,at.y-16,32,32).Contains(Event.current.mousePosition))
                    GUI.Label(new Rect(at.x+16,at.y-10,300,24),(encounterEntries[k]?"Ringworld encounter +":"Ringworld escape +")+Math.Max(0,encounterTimes[k]-Planetarium.GetUniversalTime()).ToString("F1")+" s");
            }
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
                                bool analytical=!rotating&&g.P.SurfaceDensity==0;
                var state=new CoastState(position,velocity);var points=new List<TrajectorySample>();double time=0;
                var crossings=new List<int>();var times=new List<double>();var insidePoints=new List<bool>();var entries=new List<bool>();
                bool wasInside=g.InArrivalRegion(position,rotating);bool entryFound=false;
                string result=analytical?"Stock orbit coast, clipped at ring encounters":"Numerical vacuum coast: celestial gravity + uniform ribbon; no thrust or manoeuvres";
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
                        entries.Add(inside);crossings.Add(points.Count);times.Add(start+time);entryFound=true;
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
                    // Plot inertial positions, including the moving anchor, in the stock orbital map.
                    var anchor=settings.AnchorAt(start+time);
                    points.Add(new TrajectorySample(start+time,state.Position+anchor.Position,state.Velocity+anchor.Velocity));insidePoints.Add(inside);
                    double gap=Math.Max(1,Math.Abs(coord.Altitude-g.P.AtmosphereHeight));
                    double radial=Math.Abs(DVec.Dot(g.Up(state.Position),state.Velocity));
                    double dt=Math.Min(120,Math.Max(.01,Math.Min(gap/(radial+1)*.2,Math.Sqrt(gap/(state.Velocity.Length*state.Velocity.Length/state.Position.Length+1))*.2)));
                    if(coord.Altitude<g.P.WallHeight&&Math.Abs(DVec.Dot(g.Axis,state.Velocity))>1)dt=Math.Min(dt,Math.Max(.001,Math.Abs(Math.Abs(coord.Across)-g.P.Width/2)/Math.Abs(DVec.Dot(g.Axis,state.Velocity))*.2));
                    // Resolve the same annular handoff boundary used by automatic arrival.
                    // The straight-line estimate also catches crossings between widely spaced coast samples.
                    if(!inside){double entry=g.TimeToArrival(state.Position,state.Velocity,dt);if(!double.IsInfinity(entry))dt=Math.Min(dt,Math.Max(.0001,entry+.0001));}
                    dt=Math.Min(dt,settings.PredictionSeconds-time);if(dt<=0)break;
                    if(analytical)
                    {
                        // Rails still use patched conics outside the ring frame. Sample
                        // their exact stock ephemeris rather than substitute an N-body coast.
                        var orbitState=RingAnchorEphemeris.OrbitRelative(patch,star,start+time+dt);
                        var futureAnchor=settings.AnchorAt(start+time+dt);
                        state=new CoastState(orbitState.Position-futureAnchor.Position,orbitState.Velocity-futureAnchor.Velocity);
                    }
                    else state=NumericalFlight.Step(state,dt,(p,u)=>InertialAcceleration(p,settings,star.gravParameter,start+time+dt*.5));
                    time+=dt;
                    if(slice.Elapsed.TotalMilliseconds>=sliceBudget){yield return null;slice.Restart();}
                }
                if(time<settings.PredictionSeconds&&points.Count>=4096)result="Coast truncated at numerical step budget";
                PredictionCommits++;
                Status=(entryFound?"Ringworld frame encounter marked. ":"")+result;PredictedOrbit=patch;encounters.Clear();encounters.AddRange(crossings);encounterTimes.Clear();encounterTimes.AddRange(times);encounterEntries.Clear();encounterEntries.AddRange(entries);pathInside.Clear();pathInside.AddRange(insidePoints);samples.Clear();samples.AddRange(points);
            }
            finally{computing=false;}
        }
        public void OnDisable(){ResetPrediction();}
        public void OnDestroy(){Camera.onPreCull-=PrepareCamera;VectorLine.Destroy(ref line);VectorLine.Destroy(ref markers);}
    }
    [HarmonyPatch(typeof(OrbitRendererBase),"DrawSpline")]
    internal static class RingOrbitSplinePatch
    {
        private static readonly HashSet<OrbitRendererBase> Hidden=new HashSet<OrbitRendererBase>();
        private static readonly System.Reflection.MethodInfo GetOrbit=AccessTools.PropertyGetter(typeof(OrbitRendererBase),"orbit");
        private static bool Prefix(OrbitRendererBase __instance){return ApplyVisibility(__instance);}
        internal static bool ApplyVisibility(OrbitRendererBase __instance)
        {
            bool hide=ShouldHideVessel(__instance.vessel)||(GetOrbit!=null&&ShouldHide(GetOrbit.Invoke(__instance,null) as Orbit));
            // Vectrosity retains the previous mesh when a draw call is skipped.
            Hidden.RemoveWhere(renderer=>renderer==null);
            if(hide&&__instance.OrbitLine!=null){__instance.OrbitLine.active=false;Hidden.Add(__instance);}
            else if(Hidden.Remove(__instance)&&__instance.OrbitLine!=null)__instance.OrbitLine.active=true;
            return !hide;
        }
        internal static bool ShouldHideVessel(Vessel vessel)
        {
            if(vessel==null)return false;
            if(RingTrajectory.LandedOnRing(vessel))return true;
            if(HighLogic.LoadedScene==GameScenes.TRACKSTATION)
                return TrackingRing.Trajectory!=null&&TrackingRing.Trajectory.Replaces(vessel);
            var f=RingworldFlight.Instance;
            return f!=null&&(f.Owns(vessel)||(f.trajectory!=null&&f.trajectory.Replaces(vessel)));
        }
        internal static bool ShouldHide(Orbit orbit)
        {
            if(orbit==null)return false;
            foreach(var resident in FlightGlobals.Vessels)
            {if(ReferenceEquals(resident.orbit,orbit)&&RingTrajectory.LandedOnRing(resident))return true;}
            var f=RingworldFlight.Instance;var v=FlightGlobals.ActiveVessel;
            if(HighLogic.LoadedScene==GameScenes.TRACKSTATION&&TrackingRing.Trajectory!=null&&TrackingRing.Settings!=null&&TrackingRing.Settings.ShowTrajectory)
                return TrackingRing.Trajectory.PointCount>=2&&ReferenceEquals(orbit,TrackingRing.Trajectory.PredictedOrbit);
            return f!=null&&v!=null&&((f.Owns(v)&&ReferenceEquals(orbit,v.orbit))||
                (f.Settings!=null&&f.Settings.ShowTrajectory&&f.trajectory!=null&&f.trajectory.PointCount>=2&&ReferenceEquals(orbit,f.trajectory.PredictedOrbit)));
        }
    }
    [HarmonyPatch(typeof(OrbitRendererBase),"DrawOrbit")]
    internal static class RingOrbitDrawPatch
    {
        // OrbitRenderer reactivates a retained line before the base method. Zero
        // opacity exits that method before DrawSpline, so guard both entry points.
        private static bool Prefix(OrbitRendererBase __instance){return RingOrbitSplinePatch.ApplyVisibility(__instance);}
    }
    [HarmonyPatch(typeof(PatchRendering),"UpdatePR")]
    internal static class RingPatchedConicLine
    {
        internal static int Suppressed;
        private static bool Prefix(PatchRendering __instance,PatchedConicRenderer ___pcrCache)
        {
            // PatchedConicRenderer keeps cloned Orbit patches. Match their owner,
            // not only reference equality with vessel.orbit, or a second line survives.
            if(!RingOrbitSplinePatch.ShouldHideVessel(___pcrCache==null?null:___pcrCache.vessel)&&!RingOrbitSplinePatch.ShouldHide(__instance.patch))return true;
            __instance.DestroyVector();__instance.DestroyUINodes();Suppressed++;return false;
        }
    }
}

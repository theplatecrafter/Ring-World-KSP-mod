using System;
using System.Collections.Generic;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    // Rendering follows the same frozen local chart as collision physics. Actual
    // celestial orbits and body transforms are never rewritten.
    internal static class RingSceneFrame
    {
        internal static RingworldFlight Flight {get{var f=RingworldFlight.Instance;return f!=null&&f.Settings!=null&&f.Star!=null&&f.Active?f:null;}}
        internal static DVec Vector(DVec inertial)
        {var f=Flight;return f==null?inertial:f.Settings.Geometry.RotateAroundAxis(inertial,-f.Settings.Geometry.P.Omega*(Planetarium.GetUniversalTime()-f.FrameEpoch));}
        internal static Vector3d Center(Settings s)
        {
            var f=Flight;double now=Planetarium.GetUniversalTime();
            if(f==null)return s.InertialCenter;
            var frozen=f.FrameAnchorPosition;
            if(f.Settings.RingId==s.RingId)return f.Star.position+ConvertVector.Ksp(frozen);
            RingAnchorState reference;string error;
            if(!RingAnchorEphemeris.TryRelative(RingAnchorEphemeris.Id(s.Body),f.Star,now,out reference,out error))throw new InvalidOperationException(error);
            var relative=reference.Position+s.AnchorAt(now).Position-f.Settings.AnchorAt(now).Position;
            return f.Star.position+ConvertVector.Ksp(frozen+Vector(relative));
        }
    }
    [KSPAddon(KSPAddon.Startup.Flight,false)]
    public sealed class RingBodyRenderFrame : MonoBehaviour
    {
        private struct Pose {internal Transform Transform;internal Vector3 Position;internal Quaternion Rotation;}
        private readonly List<Pose> restore=new List<Pose>();
        private Camera rendering;
        public void Start(){Camera.onPreCull+=Prepare;Camera.onPostRender+=Restore;}
        private void Prepare(Camera camera)
        {
            if(rendering!=null)return;var f=RingSceneFrame.Flight;
            if(f==null||RingMapFrame.Active||(camera.cullingMask&(1<<10))==0)return;
            rendering=camera;double now=Planetarium.GetUniversalTime();var anchor=f.Settings.AnchorAt(now);
            var q=f.Settings.AxisRotation(-f.Settings.Geometry.P.Omega*(now-f.FrameEpoch));
            foreach(var body in FlightGlobals.Bodies)
            {
                if(body.scaledBody==null)continue;RingAnchorState state;string error;
                if(!RingAnchorEphemeris.TryRelative(RingAnchorEphemeris.Id(body),f.Star,now,out state,out error))continue;
                var t=body.scaledBody.transform;restore.Add(new Pose{Transform=t,Position=t.position,Rotation=t.rotation});
                t.position=(Vector3)ScaledSpace.LocalToScaledSpace(f.Center+ConvertVector.Ksp(RingSceneFrame.Vector(state.Position-anchor.Position)));
                t.rotation=q*t.rotation;
            }
        }
        private void Restore(Camera camera)
        {
            if(camera!=rendering)return;
            foreach(var pose in restore)if(pose.Transform!=null)pose.Transform.SetPositionAndRotation(pose.Position,pose.Rotation);
            restore.Clear();rendering=null;
        }
        public void OnDisable(){Restore(rendering);}
        public void OnDestroy(){Camera.onPreCull-=Prepare;Camera.onPostRender-=Restore;Restore(rendering);RingSolarTracking.Clear();}
    }
}

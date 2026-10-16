using System;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    // Scaled-space origins and map cameras can move after LateUpdate. Retain a
    // double-precision ring chart and upload only the camera-relative difference.
    internal sealed class CameraRelativeRingMesh : IDisposable
    {
        private readonly GameObject root;private readonly Mesh mesh;private readonly Material material;
        private readonly DVec[] chart;private readonly Vector3[] vertices;private readonly Settings settings;
        private readonly double radius,halfWidth,axialCenter;
        private DVec lastX,lastY,lastZ,lastObserver;private bool uploaded;
        internal int PreparedFrames;internal Vector3 LastCameraPosition;
        internal CameraRelativeRingMesh(GameObject root,Mesh mesh,Material material,DVec[] chart,Settings settings)
        {
            this.root=root;this.mesh=mesh;this.material=material;this.chart=chart;this.settings=settings;
            vertices=new Vector3[chart.Length];root.transform.SetParent(null,false);
            double r=0,low=double.PositiveInfinity,high=double.NegativeInfinity;
            foreach(var p in chart){r=Math.Max(r,Math.Sqrt(p.X*p.X+p.Z*p.Z));low=Math.Min(low,p.Y);high=Math.Max(high,p.Y);}
            radius=r;halfWidth=(high-low)*.5;axialCenter=(low+high)*.5;
            mesh.MarkDynamic();Camera.onPreCull+=Prepare;
        }
        private void Prepare(Camera camera)
        {
            long profile=RingPerformance.Begin();
            try{PrepareMesh(camera);}finally{RingPerformance.End(RingPerformance.Section.ScaledPlacement,profile);}
        }
        private void PrepareMesh(Camera camera)
        {
            if(root==null||!root.activeSelf||settings.Body==null||(camera.cullingMask&(1<<10))==0)return;
            var flight=RingworldFlight.Instance;
            bool local=flight!=null&&flight.Settings!=null&&flight.Settings.RingId==settings.RingId;
            bool map=RingMapFrame.Active;double now=Planetarium.GetUniversalTime();
            double epoch=!map&&local&&flight.Active?flight.FrameEpoch:now;
            double phase=RingGeometry.Wrap(settings.Geometry.P.Omega*epoch,2*Math.PI);
            var center=RingMapFrame.Center(settings);DVec observer;
            if(!map&&ScaledCamera.Instance!=null&&camera==ScaledCamera.Instance.cam&&ScaledCamera.Instance.tgtRef!=null)
                observer=ConvertVector.Core((Vector3d)ScaledCamera.Instance.tgtRef.position-center)*ScaledSpace.InverseScaleFactor;
            else observer=ConvertVector.Core(ScaledSpace.ScaledToLocalSpace(camera.transform.position)-center)*ScaledSpace.InverseScaleFactor;
            var scene=RingSceneFrame.Flight;
            bool frozen=!map&&!local&&scene!=null;
            var inertialObserver=frozen?scene.Settings.Geometry.RotateAroundAxis(observer,scene.Settings.Geometry.P.Omega*(now-scene.FrameEpoch)):observer;
            var cameraChart=RingGeometry.Rotate(settings.Geometry.Basis.ToLocal(inertialObserver),-phase);
            // One rotation per camera; no rebuilding topology or weather textures.
            double c=Math.Cos(phase),sn=Math.Sin(phase);
            var xAxis=settings.Geometry.Basis.ToWorld(new DVec(c,0,-sn));var yAxis=settings.Geometry.Basis.Y;var zAxis=settings.Geometry.Basis.ToWorld(new DVec(sn,0,c));
            if(frozen){xAxis=RingSceneFrame.Vector(xAxis);yAxis=RingSceneFrame.Vector(yAxis);zAxis=RingSceneFrame.Vector(zAxis);}
            // Compose the two rigid rotations once, rather than resolving the
            // flight frame and its trigonometry separately for every vertex.
            if(!uploaded||!Same(lastX,xAxis)||!Same(lastY,yAxis)||!Same(lastZ,zAxis)||!Same(lastObserver,observer))
            {
                for(int i=0;i<chart.Length;i++){var p=chart[i];vertices[i]=ConvertVector.Unity(xAxis*p.X+yAxis*p.Y+zAxis*p.Z-observer);}
                mesh.vertices=vertices;
                // An analytic enclosing cylinder avoids a second traversal in
                // RecalculateBounds. Round outward at the final float boundary.
                var boundsCenter=yAxis*axialCenter-observer;
                var extent=new DVec(radius*Math.Sqrt(xAxis.X*xAxis.X+zAxis.X*zAxis.X)+Math.Abs(yAxis.X)*halfWidth,
                    radius*Math.Sqrt(xAxis.Y*xAxis.Y+zAxis.Y*zAxis.Y)+Math.Abs(yAxis.Y)*halfWidth,
                    radius*Math.Sqrt(xAxis.Z*xAxis.Z+zAxis.Z*zAxis.Z)+Math.Abs(yAxis.Z)*halfWidth);
                extent+=new DVec((Math.Abs(boundsCenter.X)+extent.X)*2e-7+.001,(Math.Abs(boundsCenter.Y)+extent.Y)*2e-7+.001,(Math.Abs(boundsCenter.Z)+extent.Z)*2e-7+.001);
                mesh.bounds=new Bounds(ConvertVector.Unity(boundsCenter),ConvertVector.Unity(extent*2));
                lastX=xAxis;lastY=yAxis;lastZ=zAxis;lastObserver=observer;uploaded=true;
#if RINGWORLD_SMOKE_TEST
                if(FramePerformanceSmoke.Enabled&&!validated)
                {
                    for(int i=0;i<chart.Length;i++)
                    {
                        var p=chart[i];p=new DVec(p.X*c+p.Z*sn,p.Y,-p.X*sn+p.Z*c);p=settings.Geometry.Basis.ToWorld(p);if(frozen)p=RingSceneFrame.Vector(p);
                        var actual=xAxis*chart[i].X+yAxis*chart[i].Y+zAxis*chart[i].Z;
                        if((actual-p).Length>Math.Max(1e-7,p.Length*3e-15)||!mesh.bounds.Contains(vertices[i]))throw new InvalidOperationException("Scaled ring transform/bounds regression");
                    }
                    validated=true;Debug.Log("[RingworldSmoke] PASS scaled placement reference/bounds "+root.name+" vertices="+chart.Length);
                }
#endif
            }
            var rotation=settings.BasisRotation*Quaternion.Euler(0,(float)(phase*Mathf.Rad2Deg),0);
            if(frozen)rotation=scene.Settings.AxisRotation(-scene.Settings.Geometry.P.Omega*(now-scene.FrameEpoch))*rotation;
            root.transform.SetPositionAndRotation(camera.transform.position,Quaternion.identity);
            material.SetFloat("_RingCameraRelative",1);
            // Decide outside/inside before converting this enormous chart to float.
            bool exterior=RingExterior.InHullBand(cameraChart/ScaledSpace.InverseScaleFactor,settings.Geometry.P.Radius,settings.Geometry.P.Width/2,settings.UndersideAltitude);
            material.SetFloat("_RingCameraExterior",exterior?1:0);
            material.SetVector("_RingCameraLocal",ConvertVector.Unity(cameraChart));
            material.SetMatrix("_RingToChart",Matrix4x4.Rotate(Quaternion.Inverse(rotation)));
            LastCameraPosition=camera.transform.position;PreparedFrames++;
        }
        private static bool Same(DVec a,DVec b){return a.X==b.X&&a.Y==b.Y&&a.Z==b.Z;}
#if RINGWORLD_SMOKE_TEST
        private bool validated;
#endif
        public void Dispose(){Camera.onPreCull-=Prepare;}
    }
}

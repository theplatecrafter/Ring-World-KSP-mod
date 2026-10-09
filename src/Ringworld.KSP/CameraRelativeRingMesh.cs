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
        internal int PreparedFrames;internal Vector3 LastCameraPosition;
        internal CameraRelativeRingMesh(GameObject root,Mesh mesh,Material material,DVec[] chart,Settings settings)
        {
            this.root=root;this.mesh=mesh;this.material=material;this.chart=chart;this.settings=settings;
            vertices=new Vector3[chart.Length];root.transform.SetParent(null,false);
            mesh.MarkDynamic();Camera.onPreCull+=Prepare;
        }
        private void Prepare(Camera camera)
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
            for(int i=0;i<chart.Length;i++)
            {
                var p=chart[i];p=new DVec(p.X*c+p.Z*sn,p.Y,-p.X*sn+p.Z*c);
                p=settings.Geometry.Basis.ToWorld(p);if(frozen)p=RingSceneFrame.Vector(p);
                vertices[i]=ConvertVector.Unity(p-observer);
            }
            var rotation=settings.BasisRotation*Quaternion.Euler(0,(float)(phase*Mathf.Rad2Deg),0);
            if(frozen)rotation=scene.Settings.AxisRotation(-scene.Settings.Geometry.P.Omega*(now-scene.FrameEpoch))*rotation;
            root.transform.SetPositionAndRotation(camera.transform.position,Quaternion.identity);
            mesh.vertices=vertices;mesh.RecalculateBounds();
            material.SetFloat("_RingCameraRelative",1);
            // Decide outside/inside before converting this enormous chart to float.
            bool exterior=RingExterior.InHullBand(cameraChart/ScaledSpace.InverseScaleFactor,settings.Geometry.P.Radius,settings.Geometry.P.Width/2,settings.UndersideAltitude);
            material.SetFloat("_RingCameraExterior",exterior?1:0);
            material.SetVector("_RingCameraLocal",ConvertVector.Unity(cameraChart));
            material.SetMatrix("_RingToChart",Matrix4x4.Rotate(Quaternion.Inverse(rotation)));
            LastCameraPosition=camera.transform.position;PreparedFrames++;
        }
        public void Dispose(){Camera.onPreCull-=Prepare;}
    }
}

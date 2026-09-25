using System;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld.Extensions
{
    // Distant optical column approximation. Local Cyla/Original retains the sky integration.
    internal sealed class RingworldScattering : IDisposable
    {
        private GameObject root; private Mesh mesh; private Material material;
        internal RingworldScattering(Transform parent,Settings s,AssetBundle bundle)
        {
            var shader=bundle.LoadAsset<Shader>("Assets/Shaders/FullRingAtmosphere.shader");
            if(shader==null||!shader.isSupported)return;
            material=new Material(shader);
            root=new GameObject("Ringworld full-ring atmosphere");root.layer=10;root.transform.SetParent(parent,false);
            const int count=16384;
            var vertices=new Vector3[(count+1)*2];var uv=new Vector2[vertices.Length];var indices=new int[count*6];
            var geometry=new RingGeometry(s.Geometry.P);
            for(int i=0;i<=count;i++)
            {
                for(int j=0;j<2;j++)
                {
                    vertices[i*2+j]=ConvertVector.Unity(geometry.Position(i*s.Geometry.P.Circumference/count,(j-.5)*s.Geometry.P.Width,30000)*ScaledSpace.InverseScaleFactor);
                    uv[i*2+j]=new Vector2((float)i/count,j);
                }
                if(i==count)continue;
                int k=i*2,t=i*6;indices[t]=k;indices[t+1]=k+1;indices[t+2]=k+2;indices[t+3]=k+1;indices[t+4]=k+3;indices[t+5]=k+2;
            }
            mesh=new Mesh{name="Full-ring atmospheric column",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=indices;mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        internal void Update(Settings s,double time)
        {
            if(root==null)return;
            root.SetActive(s.FullRingAtmosphere&&s.Atmosphere&&s.Haze>0);
            material.SetVector("_RingSize",new Vector4((float)(s.Geometry.P.Radius*ScaledSpace.InverseScaleFactor),(float)(s.Geometry.P.Width*.5*ScaledSpace.InverseScaleFactor),(float)ScaledSpace.InverseScaleFactor,0));
            material.SetFloat("_DayPhase",(float)RingGeometry.Wrap(time/s.Geometry.P.DaySeconds,1));
            material.SetFloat("_Haze",(float)s.Haze);material.SetFloat("_Exposure",(float)s.AtmosphereExposure);
            material.SetFloat("_Detail",s.VisualQuality);
            // Distance is evaluated from the rendering camera, including map/photo cameras.
            // In flight only, the nearby optical column is already integrated by the local sky.
            var flight=RingworldFlight.Instance;var vessel=FlightGlobals.ActiveVessel;
            bool local=HighLogic.LoadedSceneIsFlight&&!MapView.MapIsEnabled&&flight!=null&&flight.Settings!=null&&flight.Settings.RingId==s.RingId&&!flight.AtmosphereTransition&&vessel!=null&&vessel.mainBody==s.Body;
            material.SetFloat("_LocalBlend",local?1:0);
        }
        public void Dispose()
        {
            if(root!=null)UnityEngine.Object.Destroy(root);
            if(mesh!=null)UnityEngine.Object.Destroy(mesh);
            if(material!=null)UnityEngine.Object.Destroy(material);
        }
    }
}

using System;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    // One inexpensive scaled-space ribbon, with independent UV origins per segment.
    // This avoids multiplying a float longitude by the entire circumference near the player.
    internal sealed class GlobalClouds : IDisposable
    {
        private readonly GameObject root;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly CameraRelativeRingMesh placement;
        internal GlobalClouds(Transform parent,Settings s,AssetBundle bundle)
        {
            var shader=bundle.LoadAsset<Shader>("Assets/Shaders/GlobalClouds.shader");
            if(shader==null||!shader.isSupported)return;
            material=new Material(shader);material.SetVector("_CloudShellSize",new Vector4((float)(s.Geometry.P.Radius*ScaledSpace.InverseScaleFactor),(float)(s.Geometry.P.Width*.5*ScaledSpace.InverseScaleFactor),0,0));material.SetTexture("_Noise",bundle.LoadAsset<Texture3D>("Assets/CloudNoise.asset"));
            root=new GameObject("Ringworld global cloud shell");root.layer=10;root.transform.SetParent(parent,false);
            const int n=16384;var precise=new DVec[n*4];var vertices=new Vector3[n*4];var uv=new Vector2[n*4];var field=new Vector2[n*4];var chart=new Vector2[n*4];var macro=new Vector2[n*4];var triangles=new int[n*6];
            var geometry=new RingGeometry(s.Geometry.P); // Parent supplies the scaled-ring rotation.
            double macroPeriod=s.Geometry.P.Circumference/Math.Round(s.Geometry.P.Circumference/RingCloudField.CoverageScale);
            for(int i=0;i<n;i++)
            {
                double a=i*s.Geometry.P.Circumference/n,span=s.Geometry.P.Circumference/n;
                for(int j=0;j<4;j++)
                {
                    double along=a+(j/2)*span,across=(j%2-.5)*s.Geometry.P.Width;
                    precise[i*4+j]=geometry.Position(along,across,5500)*ScaledSpace.InverseScaleFactor;
                    vertices[i*4+j]=ConvertVector.Unity(precise[i*4+j]);
                    uv[i*4+j]=new Vector2((float)(along/s.Geometry.P.Circumference),(float)(across/s.Geometry.P.Width+.5));
                    field[i*4+j]=new Vector2((float)Math.Floor(a/macroPeriod),(float)(across/512000));
                    chart[i*4+j]=new Vector2(i,(float)((j/2)*span));
                    macro[i*4+j]=new Vector2((float)(RingGeometry.Wrap(a/macroPeriod,1)+(j/2)*span/macroPeriod),(float)(across/RingCloudField.CoverageScale));
                }
                int k=i*4,t=i*6;triangles[t]=k;triangles[t+1]=k+1;triangles[t+2]=k+2;triangles[t+3]=k+1;triangles[t+4]=k+3;triangles[t+5]=k+2;
            }
            mesh=new Mesh{name="Continuous full-ring clouds",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=vertices;mesh.uv=uv;mesh.uv2=field;mesh.uv3=chart;mesh.uv4=macro;mesh.triangles=triangles;mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterial=material;
            placement=new CameraRelativeRingMesh(root,mesh,material,precise,s);
        }
        internal void Update(Settings s,RingworldFlight flight,CelestialBody star,double time)
        {
            if(root==null)return;
            root.SetActive(s.Atmosphere&&s.CloudAmount>0);if(!root.activeSelf)return;
            RingCloudField.Apply(material,s,0,0,0,time);
            RingLighting.Apply(material,s,time);
            material.SetFloat("_PanelsDisabled",s.Geometry.P.PanelsEnabled?0:1);
            material.SetFloat("_DayPhase",(float)RingGeometry.Wrap(time/s.Geometry.P.DaySeconds,1));
            material.SetVector("_Size",new Vector4((float)s.Geometry.P.Circumference,(float)s.Geometry.P.Width,0,0));
            uint seed=unchecked((uint)s.Geometry.P.Seed);material.SetFloat("_SeedLow",seed&65535);material.SetFloat("_SeedHigh",seed>>16);
            material.SetFloat("_CircumferenceKm",(float)(s.Geometry.P.Circumference/1000));material.SetFloat("_WidthKm",(float)(s.Geometry.P.Width/1000));material.SetFloat("_Generation",s.GenerationVersion);
            double tick=Math.Floor(time/s.WeatherPeriod),t=time/s.WeatherPeriod-tick;t=t*t*(3-2*t);
            double temporal=s.Terrain.Scatter((long)tick,0,1201)*(1-t)+s.Terrain.Scatter((long)tick+1,0,1201)*t;
            material.SetVector("_WeatherState",new Vector4((float)s.CloudAmount,(float)(s.DynamicWeather?s.WeatherVariation:0),(float)s.StormChance,(float)temporal));
            material.SetVector("_FrontDrift",new Vector4((float)RingGeometry.Wrap(-time*30/s.Geometry.P.Circumference,1),(float)(time*7/s.Geometry.P.Width),0,0));
            material.SetVector("_Local",Vector4.zero);
            var camera=FlightCamera.fetch==null?null:FlightCamera.fetch.mainCamera;
            var vessel=FlightGlobals.ActiveVessel;
            if(!HighLogic.LoadedSceneIsFlight||MapView.MapIsEnabled||flight==null||camera==null||vessel==null||vessel.mainBody!=star)return;
            var c=s.Geometry.Coordinates(ConvertVector.Core((Vector3d)camera.transform.position-s.Center));
            if(c.Altitude<=-1000||c.Altitude>=600000||Math.Abs(c.Across)>s.Geometry.P.Width/2+500000)return;
            if(flight.visuals!=null&&flight.visuals.Rendering&&!flight.visuals.CloudRendering&&!flight.visuals.SimplePhoto)return;
            var handoff=RingCloudField.Handoff(s,flight.visuals!=null&&flight.visuals.CloudRendering);
            material.SetVector("_Local",new Vector4((float)(c.Along/s.Geometry.P.Circumference),(float)(c.Across/s.Geometry.P.Width+.5),(float)c.Altitude,1));
            material.SetVector("_CloudHandoff",handoff);
            double span=s.Geometry.P.Circumference/16384,sector=Math.Floor(c.Along/span);
            material.SetFloat("_SegmentLength",(float)span);material.SetVector("_LocalChart",new Vector4((float)sector,(float)(c.Along-sector*span),(float)c.Across,(float)c.Altitude));
            material.SetFloat("_LocalAmount",(float)s.Weather(c.Along,c.Across,time).Cloud);
        }
        public void Dispose(){if(placement!=null)placement.Dispose();if(root!=null)UnityEngine.Object.Destroy(root);if(mesh!=null)UnityEngine.Object.Destroy(mesh);if(material!=null)UnityEngine.Object.Destroy(material);}
    }
}

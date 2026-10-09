using System;
using System.Collections.Generic;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    // Small, depth-tested visual billboards. No wind force, heating or opaque
    // screen overlay: clear skies and orbital landmarks remain visible.
    internal sealed class AmbientGroundWeather : IDisposable
    {
        private readonly Settings settings;private readonly GameObject root;private readonly Mesh mesh;private readonly Material material;private readonly Texture2D texture;
        private float next;
        internal AmbientGroundWeather(Settings s)
        {
            settings=s;root=new GameObject("Ringworld dust and pollen");root.layer=15;mesh=new Mesh();root.AddComponent<MeshFilter>().sharedMesh=mesh;
            material=new Material(Shader.Find("Sprites/Default"));texture=new Texture2D(32,32,TextureFormat.RGBA32,true);var pixels=new Color[1024];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++){float r=((x-15.5f)*(x-15.5f)+(y-15.5f)*(y-15.5f))/240;pixels[y*32+x]=new Color(1,1,1,Mathf.Pow(Mathf.Max(0,1-r),2));}
            texture.SetPixels(pixels);texture.Apply(true,true);texture.filterMode=FilterMode.Trilinear;texture.wrapMode=TextureWrapMode.Clamp;material.mainTexture=texture;root.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        internal void Hide(){root.SetActive(false);}
        internal void Update(DVec observer,Vector3d star)
        {
            var camera=FlightCamera.fetch==null?null:FlightCamera.fetch.mainCamera;var g=settings.Geometry;var p=g.Coordinates(observer);var ground=settings.Terrain.Sample(p.Along,p.Across);
            bool show=settings.NativeSurfaceScatters&&settings.AmbientParticles&&!MapView.MapIsEnabled&&camera!=null&&p.Altitude-ground.Height<30&&!ground.Wet;
            root.SetActive(show);if(!show||Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+.1f;
            var climate=Ecology.Sample(settings.Terrain,p.Along,p.Across);bool dust=climate.Desert>.35;double strength=dust?climate.Desert:climate.Forest*.4;
            var vertices=new List<Vector3>();var colors=new List<Color>();var uv=new List<Vector2>();var indices=new List<int>();double time=Planetarium.GetUniversalTime();
            root.transform.position=(Vector3)(star+ConvertVector.Ksp(observer));
            float light=(float)(.15+.85*g.Daylight(p.Along,time,p.Across,p.Altitude));
            for(int i=0;i<48*strength;i++)
            {
                double a=p.Along+RingGeometry.Wrap(i*17.37+time*(dust?6:1),80)-40,b=p.Across+RingGeometry.Wrap(i*31.13,80)-40;
                var sample=settings.Terrain.Sample(a,b);if(sample.Wet)continue;
                var point=ConvertVector.Unity(g.Position(a,b,sample.Height+1+RingGeometry.Wrap(i*.71+time*.15,5))-observer);
                float size=dust?2.5f:.08f;var right=camera.transform.right*size;var up=camera.transform.up*(dust?.5f:size);int start=vertices.Count;
                vertices.AddRange(new[]{point-right-up,point+right-up,point+right+up,point-right+up});uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});
                var color=dust?new Color(.75f*light,.62f*light,.42f*light,.055f):new Color(.75f*light,.79f*light,.48f*light,.35f);
                for(int j=0;j<4;j++)colors.Add(color);indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
        }
        public void Dispose(){UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(material);UnityEngine.Object.Destroy(texture);}
    }
}

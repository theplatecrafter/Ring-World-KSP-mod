#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections.Generic;
using System.IO;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    internal static class TerrainTextureSmoke
    {
        internal static void Run()
        {
            var bundle=RingVisualAssets.Acquire();var shader=bundle.LoadAsset<Shader>("Assets/Shaders/TerrainTransition.shader");
            if(shader==null||!shader.isSupported)throw new Exception("Procedural surface shader unavailable");
            if(bundle.Contains("Assets/SurfaceDetail.asset"))throw new Exception("Obsolete tiled texture array still bundled");
            var obj=new GameObject("Surface texture probe");obj.layer=30;var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-8,-8,0),new Vector3(8,-8,0),new Vector3(-8,8,0),new Vector3(8,8,0)};
            mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};mesh.uv2=mesh.uv;mesh.triangles=new[]{0,2,1,1,2,3};mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=obj.AddComponent<MeshRenderer>();var material=new Material(shader);renderer.sharedMaterial=material;material.SetTexture("_MainTex",Texture2D.whiteTexture);material.SetColor("_Color",new Color(.4f,.45f,.3f));material.SetFloat("_AlbedoOnly",1);TerrainSurfaceDetail.Bind(material,bundle);
            var cameraObject=new GameObject("Terrain texture test camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.transform.position=new Vector3(0,0,-20);camera.orthographic=true;camera.orthographicSize=9;camera.nearClipPlane=.1f;camera.farClipPlane=40;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var rt=new RenderTexture(384,384,24);camera.targetTexture=rt;var previous=RenderTexture.active;
            var pixels=new Texture2D(384,384,TextureFormat.RGB24,false);string folder=Path.Combine(KSPUtil.ApplicationRootPath,"../Ring World KSP/artifacts/diagnostics/terrain-textures-20261008");Directory.CreateDirectory(folder);
            try{
                string[] names={"soil","sand","rock","snow","scrith","road"};
                for(int kind=0;kind<6;kind++){
                    var coordinates=new List<Vector4>();var weights=new List<Vector4>();var positions=new List<Vector3>();
                    foreach(var uv in mesh.uv){coordinates.Add(new Vector4(0,0,kind==4?1:0,kind==5?1:0));weights.Add(new Vector4(kind==0?1:0,kind==1?1:0,kind==2?1:0,kind==3?1:0));float x=uv.x*16,z=uv.y*16,s=(x+z)/3;positions.Add(new Vector3(x+s,s,z+s));}
                    mesh.SetUVs(2,coordinates);mesh.SetUVs(3,weights);mesh.SetUVs(4,positions);camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,384,384),0,0);pixels.Apply();
                    var colors=pixels.GetPixels();double sum=0,squares=0;int count=0;
                    for(int y=40;y<344;y++)for(int x=40;x<344;x++){var c=colors[y*384+x];double v=(c.r+c.g+c.b)/3;sum+=v;squares+=v*v;count++;}
                    double variance=squares/count-(sum/count)*(sum/count);if(variance<.0000001)throw new Exception("Flat procedural surface: "+names[kind]);
                    if(variance>.0002)throw new Exception("Surface contrast exceeds soft-ground range: "+names[kind]+" "+variance);
                    File.WriteAllBytes(Path.Combine(folder,names[kind]+".png"),pixels.EncodeToPNG());Debug.Log("[RingworldSmoke] SURFACE "+names[kind]+" variance="+variance);
                    if(kind==0){
                        double nearEnergy=GrainEnergy(colors);
                        var shifted=new List<Vector3>(positions);for(int j=0;j<shifted.Count;j++)shifted[j]+=new Vector3(8+8f/3,8f/3,8f/3);mesh.SetUVs(4,shifted);
                        camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,384,384),0,0);pixels.Apply();
                        double correlation=Correlation(colors,pixels.GetPixels());if(correlation>.999)throw new Exception("Ground repeats at an 8 m offset: "+correlation);
                        for(int j=0;j<shifted.Count;j++)shifted[j]=positions[j]+new Vector3(65536,0,0);mesh.SetUVs(4,shifted);
                        camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,384,384),0,0);pixels.Apply();
                        double oldPeriod=Correlation(colors,pixels.GetPixels());if(oldPeriod>.999)throw new Exception("Procedural field still wraps at the old 64 km tile period: "+oldPeriod);
                        material.SetVector("_SurfaceOriginX",new Vector4(65535,65535,65535,65535));
                        camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,384,384),0,0);pixels.Apply();
                        var rebased=pixels.GetPixels();double rebaseError=0;int compared=0;for(int y=40;y<344;y++)for(int x=40;x<344;x++){rebaseError+=Math.Abs(rebased[y*384+x].g-colors[y*384+x].g);compared++;}rebaseError/=compared;
                        if(rebaseError>.001)throw new Exception("Procedural noise changed at a chunk origin: "+rebaseError);
                        material.SetVector("_SurfaceOriginX",Vector4.zero);
                        mesh.SetUVs(4,positions);camera.transform.position=new Vector3(0,0,-800);camera.farClipPlane=2000;
                        camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,384,384),0,0);pixels.Apply();
                        double farEnergy=GrainEnergy(pixels.GetPixels());if(farEnergy>=nearEnergy*.45)throw new Exception("Distant grain did not soften: "+nearEnergy+" -> "+farEnergy);
                        File.WriteAllBytes(Path.Combine(folder,"soil-distance-lod.png"),pixels.EncodeToPNG());
                        Debug.Log("[RingworldSmoke] PASS non-tiled noise correlation8="+correlation+" correlation64km="+oldPeriod+" rebaseError="+rebaseError+" distance grain="+nearEnergy+" -> "+farEnergy);
                        camera.transform.position=new Vector3(0,0,-20);camera.farClipPlane=40;
                        var vertexProbe=new List<Vector3>{Vector3.zero,Vector3.zero,Vector3.zero,Vector3.zero};mesh.SetUVs(4,vertexProbe);
                        camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,384,384),0,0);pixels.Apply();var exactVertex=pixels.GetPixel(192,192);
                        for(int j=0;j<4;j++)vertexProbe[j]=new Vector3(.0001f,0,0);mesh.SetUVs(4,vertexProbe);
                        camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,384,384),0,0);pixels.Apply();var besideVertex=pixels.GetPixel(192,192);
                        if(Math.Abs(exactVertex.g-besideVertex.g)>.004)throw new Exception("Noise discontinuity at an exact lattice vertex");
                        mesh.SetUVs(4,positions);Debug.Log("[RingworldSmoke] PASS exact-lattice continuity");
                    }
                }
                material.shader=bundle.LoadAsset<Shader>("Assets/Shaders/TerrainNight.shader");material.SetFloat("_RingCameraExterior",0);RequireVisible(camera,rt,pixels,"Terrain night");material.SetFloat("_RingCameraExterior",1);camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,384,384),0,0);pixels.Apply();
                foreach(var c in pixels.GetPixels())if(c.maxColorComponent>.001)throw new Exception("Habitat night overlay visible from exterior");
                material.shader=bundle.LoadAsset<Shader>("Assets/Shaders/DistantSurface.shader");material.SetFloat("_Detail",1);material.SetFloat("_RingCameraExterior",0);RequireVisible(camera,rt,pixels,"Distant habitat");material.SetFloat("_RingCameraExterior",1);camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,384,384),0,0);pixels.Apply();
                foreach(var c in pixels.GetPixels())if(c.maxColorComponent>.001)throw new Exception("Distant habitat visible from exterior");
                material.SetFloat("_Detail",-1);camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,384,384),0,0);pixels.Apply();
                if(pixels.GetPixel(192,192).maxColorComponent<.005)throw new Exception("Scrith hull incorrectly discarded");
                material.shader=bundle.LoadAsset<Shader>("Assets/Shaders/GlobalClouds.shader");RingCloudField.Apply(material,Settings.Load(),0,0,0,0);material.SetTexture("_Noise",bundle.LoadAsset<Texture3D>("Assets/CloudNoise.asset"));material.SetVector("_WeatherState",new Vector4(.9f,0,.5f,.5f));material.SetFloat("_CloudAmount",.9f);material.SetFloat("_PanelsDisabled",1);material.SetFloat("_RingCameraExterior",0);RequireVisible(camera,rt,pixels,"Global cloud");material.SetFloat("_RingCameraExterior",1);camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,384,384),0,0);pixels.Apply();
                foreach(var c in pixels.GetPixels())if(c.maxColorComponent>.001)throw new Exception("Cloud overlay visible from exterior");
                var scattering=Extensions.ExtensionProviders.Scattering?.Assets;
                if(scattering!=null){material.shader=scattering.LoadAsset<Shader>("Assets/Shaders/FullRingAtmosphere.shader");material.SetVector("_RingSize",new Vector4(100000,100000,1,0));material.SetFloat("_Haze",1);material.SetFloat("_Exposure",1);material.SetFloat("_RingCameraExterior",0);RequireVisible(camera,rt,pixels,"Full-ring atmosphere");material.SetFloat("_RingCameraExterior",1);camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,384,384),0,0);pixels.Apply();foreach(var c in pixels.GetPixels())if(c.maxColorComponent>.001)throw new Exception("Scattering atmosphere visible from exterior");}
                Debug.Log("[RingworldSmoke] PASS six textured surfaces / exterior cloud, atmosphere and shadow rejection / scrith retained");
            }finally{RenderTexture.active=previous;UnityEngine.Object.Destroy(pixels);rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(material);UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(obj);UnityEngine.Object.Destroy(cameraObject);RingVisualAssets.Release();}
        }
        private static void RequireVisible(Camera camera,RenderTexture rt,Texture2D pixels,string label){camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,384,384),0,0);pixels.Apply();double sum=0;foreach(var c in pixels.GetPixels())sum+=c.maxColorComponent;if(sum/pixels.width/pixels.height<.001)throw new Exception(label+" positive control invisible");}
        private static double GrainEnergy(Color[] c){double e=0;int n=0;for(int y=40;y<343;y++)for(int x=40;x<343;x++){int i=y*384+x;double dx=c[i].g-c[i+1].g,dy=c[i].g-c[i+384].g;e+=dx*dx+dy*dy;n++;}return e/n;}
        private static double Correlation(Color[] a,Color[] b){double x=0,y=0,xx=0,yy=0,xy=0;int n=0;for(int j=40;j<344;j++)for(int i=40;i<344;i++){double p=a[j*384+i].g,q=b[j*384+i].g;x+=p;y+=q;xx+=p*p;yy+=q*q;xy+=p*q;n++;}return (xy-x*y/n)/Math.Sqrt((xx-x*x/n)*(yy-y*y/n));}
        internal static void CheckChunks(){
            var coords=new List<Vector4>();var weights=new List<Vector4>();var points=new List<Vector3>();int count=0;
            foreach(var chunk in RingworldTerrainApi.GetLoadedChunks()){chunk.Mesh.GetUVs(2,coords);chunk.Mesh.GetUVs(3,weights);chunk.Mesh.GetUVs(4,points);if(coords.Count!=chunk.Mesh.vertexCount||weights.Count!=coords.Count||points.Count!=coords.Count||chunk.Mesh.tangents.Length!=coords.Count)throw new Exception("Streamed terrain missing procedural coordinates/weights/tangents");count++;}
            if(count==0)throw new Exception("No streamed textured ground");Debug.Log("[RingworldSmoke] PASS textured streamed chunks="+count);
        }
        internal static void CaptureExterior(RingworldFlight f){
            var v=FlightGlobals.ActiveVessel;var c=f.Settings.Geometry.Coordinates(f.Position(v));
            var local=f.Settings.Geometry.Position(c.Along,c.Across,f.Settings.UndersideAltitude-1000000);var floor=f.Settings.Geometry.Position(c.Along,c.Across,0);
            var obj=new GameObject("Exterior habitat verification camera");var camera=obj.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<10;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            camera.transform.position=(Vector3)ScaledSpace.LocalToScaledSpace(f.Center+ConvertVector.Ksp(local));camera.transform.LookAt((Vector3)ScaledSpace.LocalToScaledSpace(f.Center+ConvertVector.Ksp(floor)),ConvertVector.Unity(f.Settings.Geometry.Axis));camera.orthographic=true;camera.orthographicSize=(float)(100000*ScaledSpace.InverseScaleFactor);camera.nearClipPlane=.01f;camera.farClipPlane=(float)(f.Settings.Geometry.P.Radius*3*ScaledSpace.InverseScaleFactor);
            var rt=new RenderTexture(640,480,24);camera.targetTexture=rt;var previous=RenderTexture.active;var pixels=new Texture2D(640,480,TextureFormat.RGB24,false);
            try{
                camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,640,480),0,0);pixels.Apply();double mean=0;float maximum=0;
                foreach(var p in pixels.GetPixels()){mean+=p.maxColorComponent;maximum=Math.Max(maximum,p.maxColorComponent);}mean/=640*480;
                if(mean<.003||maximum>.08)throw new Exception("Exterior is missing scrith or has bright habitat layers: mean="+mean+" max="+maximum);
                File.WriteAllBytes(Path.Combine(KSPUtil.ApplicationRootPath,"../Ring World KSP/artifacts/diagnostics/terrain-textures-20261008/exterior-scrith.png"),pixels.EncodeToPNG());
                foreach(var name in new[]{"Ringworld global cloud shell","Ringworld full-ring atmosphere"}){var o=GameObject.Find(name);if(o!=null&&o.GetComponent<Renderer>().sharedMaterial.GetFloat("_RingCameraExterior")<.5)throw new Exception("Actual exterior camera flag missing: "+name);}
                Debug.Log("[RingworldSmoke] PASS actual exterior scrith view mean="+mean+" max="+maximum);
            }finally{RenderTexture.active=previous;UnityEngine.Object.Destroy(pixels);rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(obj);}
        }
    }
}
#endif

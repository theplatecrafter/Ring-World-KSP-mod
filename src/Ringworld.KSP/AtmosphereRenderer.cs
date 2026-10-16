using System;
using System.Collections.Generic;
using Ringworld.Core;
using UnityEngine;

namespace NivenRingworld
{
    // CPU scattering on a low-resolution sky mesh avoids a Unity editor/asset-bundle
    // dependency. Depth-tested cloud sheets share the double-precision terrain chart.
    internal sealed class AtmosphereRenderer : IDisposable
    {
        private readonly Settings settings;
        private readonly RingAtmosphere model;
        private readonly GameObject sky,clouds;
        private readonly Mesh skyMesh,cloudMesh;
        private readonly Material skyMaterial,cloudMaterial;
        private readonly Texture2D cloudTexture;
        private readonly Vector3[] directions;
        private readonly Color[] skyColors;
        private const int CloudCells=48,CloudColumns=CloudCells+1,CloudCount=CloudColumns*CloudColumns*3;
        private readonly Vector3[] cloudVertices=new Vector3[CloudCount];
        private readonly Color[] cloudColors=new Color[CloudCount];
        private readonly double[] cloudBillows=new double[CloudColumns*CloudColumns],cloudFloors=new double[CloudColumns*CloudColumns];
        private readonly float[] cloudLights=new float[CloudColumns];
        private Color32[] cloudPixels;
        private double cloudAlong=double.NaN,cloudAcross,cloudPhase;
#if RINGWORLD_SMOKE_TEST
        private double cloudTime;
#endif
        private DVec cloudAnchor;
        private float nextSky;
        private AssetBundle visualBundle;
        private bool gpuClouds;
        private Vector3d renderStar;
        internal int CloudBuilds {get;private set;}
        internal AtmosphereRenderer(Settings settings)
        {
            this.settings=settings;model=new RingAtmosphere(settings.Geometry);
            var shader=Shader.Find("Sprites/Default");
            if(shader==null)throw new InvalidOperationException("Sprites/Default shader unavailable for ring atmosphere.");
            skyMaterial=new Material(shader){renderQueue=1000};cloudMaterial=new Material(shader){renderQueue=3000};
            cloudTexture=new Texture2D(384,384,TextureFormat.RGBA32,true);cloudTexture.wrapMode=TextureWrapMode.Clamp;cloudTexture.filterMode=FilterMode.Trilinear;
            cloudMaterial.mainTexture=cloudTexture;
            visualBundle=RingVisualAssets.Acquire();
            var cloudShader=visualBundle!=null?visualBundle.LoadAsset<Shader>("Assets/Shaders/CloudDeck.shader"):null;
            if(cloudShader!=null&&cloudShader.isSupported){cloudMaterial.shader=cloudShader;cloudMaterial.SetTexture("_Noise",visualBundle.LoadAsset<Texture3D>("Assets/CloudNoise.asset"));gpuClouds=true;}
            sky=new GameObject("Ringworld single-scattering sky");sky.layer=15;
            clouds=new GameObject("Ringworld procedural cloud layers");clouds.layer=15;
            const int columns=48,rows=24;
            directions=new Vector3[(columns+1)*(rows+1)];skyColors=new Color[directions.Length];
            var triangles=new List<int>();
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
            {
                double a=x*Math.PI*2/columns,b=-Math.PI/2+y*Math.PI/rows;
                directions[y*(columns+1)+x]=new Vector3((float)(Math.Cos(a)*Math.Cos(b)),(float)Math.Sin(b),(float)(Math.Sin(a)*Math.Cos(b)));
                if(x<columns&&y<rows)AddQuad(triangles,y*(columns+1)+x,columns+1);
            }
            skyMesh=new Mesh{name="Integrated Rayleigh-Mie sky"};skyMesh.vertices=directions;skyMesh.SetTriangles(triangles,0);skyMesh.colors=skyColors;skyMesh.RecalculateBounds();
            sky.AddComponent<MeshFilter>().sharedMesh=skyMesh;sky.AddComponent<MeshRenderer>().sharedMaterial=skyMaterial;
            cloudMesh=new Mesh{name="Material-coordinate cloud field"};
            cloudMesh.MarkDynamic();var cloudUv=new Vector2[CloudCount];var cloudIndices=new List<int>(CloudCells*CloudCells*18);
            for(int layer=0;layer<3;layer++)for(int y=0;y<CloudColumns;y++)for(int x=0;x<CloudColumns;x++)
            {
                int i=layer*CloudColumns*CloudColumns+y*CloudColumns+x;cloudUv[i]=new Vector2(x/(float)CloudCells,y/(float)CloudCells);
                if(x<CloudCells&&y<CloudCells)AddQuad(cloudIndices,i,CloudColumns);
            }
            cloudMesh.vertices=cloudVertices;cloudMesh.uv=cloudUv;cloudMesh.SetTriangles(cloudIndices,0);
            clouds.AddComponent<MeshFilter>().sharedMesh=cloudMesh;clouds.AddComponent<MeshRenderer>().sharedMaterial=cloudMaterial;
            sky.SetActive(false);clouds.SetActive(false);
            Camera.onPreCull+=PrepareCamera;
        }
        private void PrepareCamera(Camera camera)
        {
            if(FlightCamera.fetch==null||camera!=FlightCamera.fetch.mainCamera)return;
            if(sky!=null&&sky.activeSelf)sky.transform.position=camera.transform.position;
            if(clouds!=null&&clouds.activeSelf&&!double.IsNaN(cloudAlong))
            {
                var f=RingworldFlight.Instance;var star=f!=null&&f.Star!=null?f.Center:renderStar;
                double phase=settings.Geometry.OrientationRadians-cloudPhase;
                clouds.transform.position=(Vector3)(star+ConvertVector.Ksp(settings.Geometry.RotateAroundAxis(cloudAnchor,phase)));
            }
        }
        internal void Update(bool enabled,Vector3d star,bool suppressSky=false)
        {
            renderStar=star;
            var camera=FlightCamera.fetch==null?null:FlightCamera.fetch.mainCamera;
            if(camera==null)enabled=false;
            DVec observer=camera==null?new DVec():ConvertVector.Core((Vector3d)camera.transform.position-star);
            var c=settings.Geometry.Coordinates(observer);
            enabled=enabled&&settings.Atmosphere&&c.Altitude>-1000&&c.Altitude<600000&&Math.Abs(c.Across)<settings.Geometry.P.Width/2+500000;
            sky.SetActive(enabled&&!suppressSky&&settings.Haze>0);clouds.SetActive(enabled&&settings.CloudAmount>0);if(!enabled)return;
            sky.transform.position=camera.transform.position;
            sky.transform.localScale=Vector3.one*1000;
            double time=Planetarium.GetUniversalTime();
            float sunlight=(float)settings.Geometry.Daylight(c.Along,time,c.Across,c.Altitude);skyMaterial.color=new Color(sunlight,sunlight,sunlight,1);
            if(sky.activeSelf&&Time.realtimeSinceStartup>=nextSky)
            {
                long profile=RingPerformance.Begin();
                nextSky=Time.realtimeSinceStartup+.2f;
                var localClimate=Ecology.Sample(settings.Terrain,c.Along,c.Across);
                var biomeTint=settings.GenerationVersion>=4?localClimate.SkyTint:new DVec(1,1,1);
                double tintFade=Math.Max(0,1-c.Altitude/settings.Geometry.P.AtmosphereHeight);
                biomeTint=new DVec(1,1,1)*(1-tintFade)+biomeTint*tintFade;
                var lightPosition=RingLighting.LightPosition(settings,time);
                for(int i=0;i<directions.Length;i++)
                {
                    var s=model.SkyFromLight(observer,ConvertVector.Core(directions[i]),time,lightPosition);
                    // Store straight alpha; the stock sprite shader premultiplies it.
                    double opacity=1-Math.Pow(1-s.Opacity,settings.Haze);
                    if(opacity<=.000001){skyColors[i]=Color.clear;continue;}
                    double alpha=opacity;
                    skyColors[i]=new Color((float)(1-Math.Exp(-s.Radiance.X))/ (float)alpha,(float)(1-Math.Exp(-s.Radiance.Y))/(float)alpha,(float)(1-Math.Exp(-s.Radiance.Z))/(float)alpha,(float)opacity);
                }
                for(int i=0;i<skyColors.Length;i++){var color=skyColors[i];color.r*=(float)biomeTint.X;color.g*=(float)biomeTint.Y;color.b*=(float)biomeTint.Z;skyColors[i]=color;}
                skyMesh.colors=skyColors;
                RingPerformance.End(RingPerformance.Section.SkyBake,profile);
            }
            if(!clouds.activeSelf)return;
            if(double.IsNaN(cloudAlong)||Math.Abs(settings.Geometry.AlongDistance(c.Along,cloudAlong))>12000||Math.Abs(c.Across-cloudAcross)>12000)
            {
                long profile=RingPerformance.Begin();
                BuildClouds(c,time);
                RingPerformance.End(RingPerformance.Section.CloudBake,profile);
            }
            var localWeather=RingCloudField.Apply(cloudMaterial,settings,cloudAlong,cloudAcross,0,time);
            // Keep mesh-coordinate drift anchored, but share the observer's weather with the global handoff.
            cloudMaterial.SetFloat("_CloudAmount",(float)settings.Weather(c.Along,c.Across,time).Cloud);
            cloudMaterial.SetFloat("_Extent",180000);cloudMaterial.SetFloat("_Daylight",(float)settings.Geometry.Daylight(c.Along,time,c.Across,c.Altitude));
            cloudMaterial.SetVector("_CloudHandoff",RingCloudField.Handoff(settings,false));
            double phase=settings.Geometry.OrientationRadians-cloudPhase;
            clouds.transform.rotation=settings.AxisRotation(phase);
            clouds.transform.position=(Vector3)(star+ConvertVector.Ksp(settings.Geometry.RotateAroundAxis(cloudAnchor,phase)));
        }
        private void BuildClouds(RingPoint observer,double time)
        {
#if RINGWORLD_SMOKE_TEST
            cloudTime=time;
#endif
            CloudBuilds++;cloudAlong=observer.Along;cloudAcross=observer.Across;cloudPhase=settings.Geometry.OrientationRadians;
            cloudAnchor=settings.Geometry.Position(cloudAlong,cloudAcross,0);
            const int n=CloudCells;const double extent=180000;
            var vertices=cloudVertices;var colors=cloudColors;
            if(!gpuClouds){if(cloudPixels==null)cloudPixels=new Color32[384*384];var pixels=cloudPixels;
            for(int y=0;y<384;y++)for(int x=0;x<384;x++)
            {
                double a=cloudAlong+(x/383.0*2-1)*extent,b=cloudAcross+(y/383.0*2-1)*extent;
                double coverage=model.CloudCoverage(a,b,time,settings.CloudAmount,settings.DynamicWeather);
                pixels[y*384+x]=new Color32(255,255,255,(byte)(255*coverage*coverage));
            }
            cloudTexture.SetPixels32(pixels);cloudTexture.Apply(true);}
            // The decks share the same column: sample terrain/noise/illumination
            // once, rather than repeating expensive eclipse rays for each layer.
            var billows=cloudBillows;var floors=cloudFloors;var lights=cloudLights;
            // This fallback's deck illumination uses Daylight(along,time), so
            // every across row has exactly the same input for a given column.
            for(int x=0;x<=n;x++)lights[x]=(float)(.12+.88*settings.Geometry.Daylight(cloudAlong+(x/(double)n*2-1)*extent,time));
            for(int y=0;y<=n;y++)for(int x=0;x<=n;x++)
            {
                int i=y*(n+1)+x;double along=cloudAlong+(x/(double)n*2-1)*extent,across=cloudAcross+(y/(double)n*2-1)*extent;
                billows[i]=settings.Terrain.Noise(along,across,14000,233);floors[i]=Math.Abs(across)>settings.Geometry.P.Width/2?0:settings.Terrain.Sample(along,across).Height;
            }
            // Three translucent decks provide depth while flying through the cloud band.
            for(int layer=0;layer<3;layer++)for(int y=0;y<=n;y++)for(int x=0;x<=n;x++)
            {
                int i=layer*(n+1)*(n+1)+y*(n+1)+x;
                double da=(x/(double)n*2-1)*extent,db=(y/(double)n*2-1)*extent;
                double along=cloudAlong+da,across=cloudAcross+db;
                double billow=billows[y*(n+1)+x];
                double height=4300+layer*850+billow*950;
                vertices[i]=ConvertVector.Unity(settings.Geometry.Position(along,across,height)-cloudAnchor);
                double cover=1;
                // Mountains pierce the cloud deck; fade the finite patch boundary.
                if(Math.Abs(across)>settings.Geometry.P.Width/2||floors[y*(n+1)+x]>height)cover=0;
                double fade=Math.Max(0,Math.Min(1,(extent-Math.Sqrt(da*da+db*db))/65000));
                float light=lights[x];
                float shade=(float)(.68+.13*layer+.06*billow);
                colors[i]=new Color(light*shade*.94f,light*shade*.97f,light*shade,(float)(cover*fade*(layer==1?.4:.3)));
            }
            // UV/topology never change with relocation; upload only live data.
            cloudMesh.vertices=vertices;cloudMesh.colors=colors;cloudMesh.RecalculateBounds();
        }
#if RINGWORLD_SMOKE_TEST
        internal void TestCloudData()
        {
            if(CloudBuilds==0)throw new InvalidOperationException("Fallback cloud fixture has not built a deck.");
            const int n=48;const double extent=180000;
            var vertices=cloudMesh.vertices;var colors=cloudMesh.colors;var uv=cloudMesh.uv;
            var g=new RingGeometry(settings.Geometry.P){Basis=settings.Geometry.Basis,OrientationRadians=cloudPhase};
            for(int layer=0;layer<3;layer++)for(int y=0;y<=n;y++)for(int x=0;x<=n;x++)
            {
                int i=layer*(n+1)*(n+1)+y*(n+1)+x;
                double da=(x/(double)n*2-1)*extent,db=(y/(double)n*2-1)*extent;
                double a=cloudAlong+da,b=cloudAcross+db,billow=settings.Terrain.Noise(a,b,14000,233),height=4300+layer*850+billow*950;
                var expected=ConvertVector.Unity(g.Position(a,b,height)-cloudAnchor);
                double cover=Math.Abs(b)>g.P.Width/2||settings.Terrain.Sample(a,b).Height>height?0:1;
                double fade=Math.Max(0,Math.Min(1,(extent-Math.Sqrt(da*da+db*db))/65000));
                float light=(float)(.12+.88*settings.Geometry.Daylight(a,cloudTime)),shade=(float)(.68+.13*layer+.06*billow);
                var colour=new Color(light*shade*.94f,light*shade*.97f,light*shade,(float)(cover*fade*(layer==1?.4:.3)));
                if(vertices[i]!=expected||colors[i]!=colour||uv[i]!=new Vector2(x/(float)n,y/(float)n))throw new InvalidOperationException("Optimized cloud deck changed geometry, colour or coverage.");
            }
        }
#endif
        private static void AddQuad(List<int> list,int a,int stride)
        {int b=a+1,c=a+stride,d=c+1;list.Add(a);list.Add(b);list.Add(c);list.Add(b);list.Add(d);list.Add(c);}
        public void Dispose()
        {
            Camera.onPreCull-=PrepareCamera;
            UnityEngine.Object.Destroy(sky);UnityEngine.Object.Destroy(clouds);
            UnityEngine.Object.Destroy(skyMesh);UnityEngine.Object.Destroy(cloudMesh);
            UnityEngine.Object.Destroy(skyMaterial);UnityEngine.Object.Destroy(cloudMaterial);UnityEngine.Object.Destroy(cloudTexture);if(visualBundle!=null)RingVisualAssets.Release();
        }
    }
}

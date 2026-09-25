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
        private double cloudAlong=double.NaN,cloudAcross,cloudPhase;
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
                clouds.transform.position=(Vector3)(star+ConvertVector.Ksp(RingGeometry.Rotate(cloudAnchor,phase)));
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
            float sunlight=(float)settings.Geometry.Daylight(c.Along,time);skyMaterial.color=new Color(sunlight,sunlight,sunlight,1);
            if(Time.realtimeSinceStartup>=nextSky)
            {
                nextSky=Time.realtimeSinceStartup+.2f;
                var localClimate=Ecology.Sample(settings.Terrain,c.Along,c.Across);
                var biomeTint=settings.GenerationVersion>=4?localClimate.SkyTint:new DVec(1,1,1);
                double tintFade=Math.Max(0,1-c.Altitude/settings.Geometry.P.AtmosphereHeight);
                biomeTint=new DVec(1,1,1)*(1-tintFade)+biomeTint*tintFade;
                for(int i=0;i<directions.Length;i++)
                {
                    var s=model.Sky(observer,ConvertVector.Core(directions[i]),time,true);
                    // Store straight alpha; the stock sprite shader premultiplies it.
                    double opacity=1-Math.Pow(1-s.Opacity,settings.Haze);
                    if(opacity<=.000001){skyColors[i]=Color.clear;continue;}
                    double alpha=opacity;
                    skyColors[i]=new Color((float)(1-Math.Exp(-s.Radiance.X))/ (float)alpha,(float)(1-Math.Exp(-s.Radiance.Y))/(float)alpha,(float)(1-Math.Exp(-s.Radiance.Z))/(float)alpha,(float)opacity);
                }
                for(int i=0;i<skyColors.Length;i++){var color=skyColors[i];color.r*=(float)biomeTint.X;color.g*=(float)biomeTint.Y;color.b*=(float)biomeTint.Z;skyColors[i]=color;}
                skyMesh.colors=skyColors;
            }
            if(settings.CloudAmount>0&&(double.IsNaN(cloudAlong)||Math.Abs(settings.Geometry.AlongDistance(c.Along,cloudAlong))>12000||Math.Abs(c.Across-cloudAcross)>12000))
            {
                BuildClouds(c,time);
            }
            var localWeather=RingCloudField.Apply(cloudMaterial,settings,cloudAlong,cloudAcross,0,time);
            // Keep mesh-coordinate drift anchored, but share the observer's weather with the global handoff.
            cloudMaterial.SetFloat("_CloudAmount",(float)settings.Weather(c.Along,c.Across,time).Cloud);
            cloudMaterial.SetFloat("_Extent",180000);cloudMaterial.SetFloat("_Daylight",(float)settings.Geometry.Daylight(c.Along,time));
            cloudMaterial.SetVector("_CloudHandoff",RingCloudField.Handoff(settings,false));
            double phase=settings.Geometry.OrientationRadians-cloudPhase;
            clouds.transform.rotation=Quaternion.AngleAxis((float)(phase*180/Math.PI),Vector3.up);
            clouds.transform.position=(Vector3)(star+ConvertVector.Ksp(RingGeometry.Rotate(cloudAnchor,phase)));
        }
        private void BuildClouds(RingPoint observer,double time)
        {
            CloudBuilds++;cloudAlong=observer.Along;cloudAcross=observer.Across;cloudPhase=settings.Geometry.OrientationRadians;
            cloudAnchor=settings.Geometry.Position(cloudAlong,cloudAcross,0);
            const int n=48;const double extent=180000;
            var vertices=new Vector3[(n+1)*(n+1)*3];var colors=new Color[vertices.Length];var uv=new Vector2[vertices.Length];var triangles=new List<int>();
            if(!gpuClouds){var pixels=new Color32[384*384];
            for(int y=0;y<384;y++)for(int x=0;x<384;x++)
            {
                double a=cloudAlong+(x/383.0*2-1)*extent,b=cloudAcross+(y/383.0*2-1)*extent;
                double coverage=model.CloudCoverage(a,b,time,settings.CloudAmount,settings.DynamicWeather);
                pixels[y*384+x]=new Color32(255,255,255,(byte)(255*coverage*coverage));
            }
            cloudTexture.SetPixels32(pixels);cloudTexture.Apply(true);}
            // Three translucent decks provide depth while flying through the cloud band.
            for(int layer=0;layer<3;layer++)for(int y=0;y<=n;y++)for(int x=0;x<=n;x++)
            {
                int i=layer*(n+1)*(n+1)+y*(n+1)+x;
                double da=(x/(double)n*2-1)*extent,db=(y/(double)n*2-1)*extent;
                double along=cloudAlong+da,across=cloudAcross+db;
                double billow=settings.Terrain.Noise(along,across,14000,233);
                double height=4300+layer*850+billow*950;
                vertices[i]=ConvertVector.Unity(settings.Geometry.Position(along,across,height)-cloudAnchor);
                double cover=1;uv[i]=new Vector2(x/(float)n,y/(float)n);
                // Mountains pierce the cloud deck; fade the finite patch boundary.
                if(Math.Abs(across)>settings.Geometry.P.Width/2||settings.Terrain.Sample(along,across).Height>height)cover=0;
                double fade=Math.Max(0,Math.Min(1,(extent-Math.Sqrt(da*da+db*db))/65000));
                float light=(float)(.12+.88*settings.Geometry.Daylight(along,time));
                float shade=(float)(.68+.13*layer+.06*billow);
                colors[i]=new Color(light*shade*.94f,light*shade*.97f,light*shade,(float)(cover*fade*(layer==1?.4:.3)));
                if(x<n&&y<n)AddQuad(triangles,i,n+1);
            }
            cloudMesh.Clear();cloudMesh.vertices=vertices;cloudMesh.colors=colors;cloudMesh.uv=uv;cloudMesh.SetTriangles(triangles,0);cloudMesh.RecalculateBounds();
        }
        private static void AddQuad(List<int> list,int a,int stride)
        {int b=a+1,c=a+stride,d=c+1;list.AddRange(new[]{a,b,c,b,d,c});}
        public void Dispose()
        {
            Camera.onPreCull-=PrepareCamera;
            UnityEngine.Object.Destroy(sky);UnityEngine.Object.Destroy(clouds);
            UnityEngine.Object.Destroy(skyMesh);UnityEngine.Object.Destroy(cloudMesh);
            UnityEngine.Object.Destroy(skyMaterial);UnityEngine.Object.Destroy(cloudMaterial);UnityEngine.Object.Destroy(cloudTexture);if(visualBundle!=null)RingVisualAssets.Release();
        }
    }
}

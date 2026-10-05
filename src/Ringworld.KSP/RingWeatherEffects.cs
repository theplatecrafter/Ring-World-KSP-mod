using System;
using Ringworld.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace NivenRingworld
{
    // World-space, bounded precipitation; composited after cloud integration.
    internal sealed class RingWeatherEffects : IDisposable
    {
        internal static RingWeatherEffects Instance;
        private readonly GameObject root,boltObject;private readonly Mesh mesh,boltMesh;
        private readonly Material material,boltMaterial;private readonly LineRenderer bolt;private readonly MeshRenderer renderer;
        private readonly AssetBundle bundle;
        private readonly Vector3[] vertices=new Vector3[384*4];private readonly Color[] colours=new Color[384*4];
        private readonly Vector2[] uv=new Vector2[384*4];private readonly int[] triangles=new int[384*6];
        internal string Description="Clear";
        internal WeatherSample Current;internal int Drops;internal float Flash;internal bool Snow;
        private bool manual;
        internal RingWeatherEffects()
        {
            Instance=this;bundle=RingVisualAssets.Acquire();
            var shader=bundle!=null?bundle.LoadAsset<Shader>("Assets/Shaders/Precipitation.shader"):null;
            material=new Material(shader??Shader.Find("Sprites/Default"));boltMaterial=new Material(material);
            root=new GameObject("Ringworld precipitation");root.layer=15;mesh=new Mesh{name="World-space ring precipitation"};mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh=mesh;renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            for(int i=0;i<384;i++){int v=i*4,t=i*6;triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;triangles[t+3]=v;triangles[t+4]=v+2;triangles[t+5]=v+3;uv[v]=Vector2.zero;uv[v+1]=Vector2.right;uv[v+2]=Vector2.one;uv[v+3]=Vector2.up;}
            boltObject=new GameObject("Ringworld lightning");boltObject.layer=15;bolt=boltObject.AddComponent<LineRenderer>();bolt.sharedMaterial=boltMaterial;bolt.useWorldSpace=false;bolt.positionCount=17;bolt.widthMultiplier=2;bolt.startColor=bolt.endColor=new Color(.8f,.88f,1,1);
            boltMesh=new Mesh{name="Ringworld lightning composite"};
            root.SetActive(false);boltObject.SetActive(false);
        }
        internal void Update(bool allowed,Settings s,Vector3d star)
        {
            var camera=FlightCamera.fetch!=null?FlightCamera.fetch.mainCamera:null;Drops=0;Flash=0;Snow=false;
            if(!allowed||camera==null){root.SetActive(false);boltObject.SetActive(false);return;}
            camera.depthTextureMode|=DepthTextureMode.Depth;
            var observer=ConvertVector.Core((Vector3d)camera.transform.position-star);var c=s.Geometry.Coordinates(observer);double time=Planetarium.GetUniversalTime();
            var ground=s.Terrain.Sample(c.Along,c.Across);
            Current=s.Weather(c.Along,c.Across,time);Snow=ground.Biome==Biome.Snow;
            Description=RingWeather.SurfaceKind(Current,ground.Biome,Math.Abs(c.Across)/(s.Geometry.P.Width*.5),s.Geometry.Daylight(c.Along,time,c.Across,c.Altitude));
            var flight=RingworldFlight.Instance;var visuals=flight==null?null:flight.visuals;
            manual=visuals!=null&&visuals.Rendering&&material.shader.name=="NivenRingworld/Precipitation";
            renderer.enabled=!manual;bolt.enabled=!manual;material.SetFloat("_Saved",0);boltMaterial.SetFloat("_Saved",0);
            bool inside=s.Atmosphere&&c.Altitude>=ground.Height&&c.Altitude<Math.Max(visuals==null?700:visuals.PrecipitationBase,ground.Height+100)&&Math.Abs(c.Across)<s.Geometry.P.Width/2;
            bool rain=inside&&s.RainEnabled&&Current.Rain>.001&&TimeWarp.CurrentRate<=10;root.SetActive(rain);
            var up=ConvertVector.Unity(s.Geometry.Up(observer));var along=ConvertVector.Unity(s.Geometry.AlongDirection(observer));var across=ConvertVector.Unity(s.Geometry.Axis);var right=camera.transform.right;
            if(rain)
            {
                Drops=(int)((s.VisualQuality==0?48:s.VisualQuality==1?144:384)*s.RainDensity*Current.Rain);root.transform.position=camera.transform.position;
                double drift=time*(Snow?1.6+Current.Storm*5:4+Current.Storm*8)+Math.Sin(time*.19)*2;
                float lighting=(float)(.06+.94*s.Geometry.Daylight(c.Along,time,c.Across,c.Altitude));
                for(int i=0;i<384;i++)
                {
                    double x=RingGeometry.Wrap(s.Terrain.Scatter(i,0,1259)*64-c.Along+drift,64)-32;
                    double z=RingGeometry.Wrap(s.Terrain.Scatter(i,1,1259)*64-c.Across+drift*.37,64)-32;
                    double y=RingGeometry.Wrap(s.Terrain.Scatter(i,2,1259)*48-time*(Snow?1.4:24)-c.Altitude,48)-16;
                    float altitude=(float)(c.Altitude+y);float alpha=i<Drops&&altitude>Math.Max(ground.Height,ground.Wet?ground.WaterHeight:ground.Height)&&altitude<Math.Max(visuals==null?700:visuals.PrecipitationBase,ground.Height+100)?.5f:0;
                    var centre=along*(float)x+across*(float)z+up*(float)y;
                    float width=Snow?.045f:.012f;var stretch=Snow?camera.transform.up*.09f:up*1.1f-along*.18f;
                    if(Snow)centre+=along*(float)(Math.Sin(time+i)*.45)+across*(float)(Math.Cos(time*.7+i)*.3);
                    int v=i*4;vertices[v]=centre-right*width;vertices[v+1]=centre+right*width;vertices[v+2]=centre+right*width+stretch;vertices[v+3]=centre-right*width+stretch;
                    float edge=Mathf.Clamp01((32-Math.Max(Math.Abs((float)x),Math.Abs((float)z)))/8);
                    for(int j=0;j<4;j++)colours[v+j]=Snow?new Color(.92f*lighting,.96f*lighting,lighting,alpha*edge):new Color(.62f*lighting,.74f*lighting,.88f*lighting,alpha*edge);
                }
                material.SetFloat("_Flake",Snow?1:0);material.SetFloat("_Manual",manual?1:0);
                mesh.vertices=vertices;mesh.colors=colours;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();
            }
            Flash=inside&&!Snow&&s.LightningEnabled&&TimeWarp.CurrentRate<=10?(float)RingWeather.Lightning(s.Terrain,time,Current.Storm):0;
            boltObject.SetActive(Flash>.001);
            if(Flash>.001)
            {
                long slot=(long)Math.Floor(time/17);
                double a=Math.Floor(c.Along/10000)*10000+2000+s.Terrain.Scatter(slot,0,1277)*6000,b=Math.Floor(c.Across/10000)*10000+2000+s.Terrain.Scatter(slot,1,1277)*6000;
                double floor=s.Terrain.Sample(a,b).Height,top=Math.Max(visuals==null?12000:visuals.StormTop,floor+500);
                boltObject.transform.position=camera.transform.position;
                for(int i=0;i<17;i++)
                {
                    double height=floor+(top-floor)*i/16;
                    var point=s.Geometry.Position(a+(s.Terrain.Scatter(slot,i,1297)-.5)*180,b,height);
                    bolt.SetPosition(i,ConvertVector.Unity(point-observer));
                }
                bolt.startColor=new Color(.8f,.88f,1,Flash);bolt.endColor=new Color(.8f,.88f,1,Flash*.04f);
                boltMaterial.SetFloat("_Flake",0);boltMaterial.SetFloat("_Manual",manual?1:0);
                if(manual)bolt.BakeMesh(boltMesh,camera,false);
            }
        }
        private void Bind(Camera camera,Texture depth)
        {
            foreach(var mat in new[]{material,boltMaterial})
            {mat.SetMatrix("_WeatherVP",GL.GetGPUProjectionMatrix(camera.projectionMatrix,true)*camera.worldToCameraMatrix);mat.SetMatrix("_WeatherView",camera.worldToCameraMatrix);mat.SetFloat("_Saved",depth!=null?1:0);if(depth!=null)mat.SetTexture("_WeatherDepth",depth);}
        }
        internal void Draw(Camera camera,RenderTexture target,Texture depth=null)
        {
            if(!manual||camera==null)return;Bind(camera,depth);
            var old=RenderTexture.active;Graphics.SetRenderTarget(target);
            try {if(root.activeSelf&&material.SetPass(0))Graphics.DrawMeshNow(mesh,root.transform.localToWorldMatrix);if(boltObject.activeSelf&&boltMaterial.SetPass(0))Graphics.DrawMeshNow(boltMesh,boltObject.transform.localToWorldMatrix);}
            finally {RenderTexture.active=old;}
        }
        internal void Append(CommandBuffer commands,Camera camera,RenderTargetIdentifier target)
        {
            if(!manual||camera==null)return;Bind(camera,null);commands.SetRenderTarget(target);
            if(root.activeSelf)commands.DrawMesh(mesh,root.transform.localToWorldMatrix,material);
            if(boltObject.activeSelf)commands.DrawMesh(boltMesh,boltObject.transform.localToWorldMatrix,boltMaterial);
        }
        public void Dispose(){if(Instance==this)Instance=null;UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(boltObject);UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(boltMesh);UnityEngine.Object.Destroy(material);UnityEngine.Object.Destroy(boltMaterial);if(bundle!=null)RingVisualAssets.Release();}
    }
}

using System;
using System.Collections;
using System.Reflection;
using Ringworld.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace NivenRingworld
{
    // Original adapter to the public Cyla/Atmo material interface. No upstream
    // plugin source or shader binary is embedded in NivenRingworld.
    internal sealed class CylaAtmosphereBridge : IDisposable
    {
        private readonly Camera camera;
        private Material material;
        private Mesh quad;
        private CommandBuffer commands;
        private RenderTexture background,black,white,scaledDepth;
        private const float OpticalUnitsPerMetre=.001f;
        private readonly MaterialPropertyBlock backgroundBinding=new MaterialPropertyBlock(),blackBinding=new MaterialPropertyBlock(),whiteBinding=new MaterialPropertyBlock();
        private static RenderTexture Target(int width,int height,string name){var rt=new RenderTexture(width,height,0,RenderTextureFormat.ARGBHalf){name=name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};rt.Create();return rt;}
        private static void Free(ref RenderTexture rt){if(rt==null)return;rt.Release();UnityEngine.Object.Destroy(rt);rt=null;}
        private bool attached;
#if RINGWORLD_SMOKE_TEST
        internal static float ProbeUnits=OpticalUnitsPerMetre;

#endif

        private bool diagnosticQueued;
        private int frame;
        internal bool Active {get;private set;}
        internal string Status="Cyla not initialized";
        internal CylaAtmosphereBridge(Camera target){camera=target;}
        private bool Load()
        {
            if(material!=null)return true;
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var loader=assembly.GetType("Cyla.ShaderLoader",false);if(loader==null)continue;
                var property=loader.GetProperty("Shaders",BindingFlags.Public|BindingFlags.Static);
                var registry=property==null?null:property.GetValue(null,null) as IDictionary;
                var shader=registry==null?null:registry["Cyla/Atmo"] as Shader;
                if(shader==null||!shader.isSupported){Status="Cyla shader not ready or unsupported";return false;}
                material=new Material(shader){name="Ringworld Cyla atmosphere"};
                material.EnableKeyword("TRANSPARENT_TOP_AND_SIDE");material.DisableKeyword("TRANSPARENT_FLOOR");material.DisableKeyword("UNLIT");
                quad=new Mesh{name="Ringworld Cyla camera quad"};
                quad.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
                quad.normals=new[]{Vector3.back,Vector3.back,Vector3.back,Vector3.back};
                quad.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};quad.triangles=new[]{0,2,1,0,3,2};quad.RecalculateBounds();

                commands=new CommandBuffer{name="Niven Ringworld / Cyla scattering"};
                Debug.Log("[NivenRingworld] Cyla compiled shader connected: passes="+material.passCount+" / "+SystemInfo.graphicsDeviceType);
                return true;
            }
            Status="Install Cyla 1.1.0 to enable this backend";return false;
        }
        internal void Prepare(bool allowed,Settings s,Vector3d star,bool photo,bool frozen,Material composite)
        {
            Active=false;
            if(!allowed||s.AtmosphereBackend!=1||!s.Atmosphere||s.Haze<=0||composite==null||!Load()){Detach();return;}
            var observer=ConvertVector.Core((Vector3d)camera.transform.position-star);
            var coord=s.Geometry.Coordinates(observer);
            if(coord.Altitude< -1000||coord.Altitude>600000||Math.Abs(coord.Across)>s.Geometry.P.Width/2+500000){Detach();return;}
            if(photo&&frozen){Detach();Active=true;return;}
            if(composite.passCount<5||!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat))
            {Status="Cyla optical depth conversion unavailable; using Original";Detach();return;}
            camera.depthTextureMode|=DepthTextureMode.Depth;
            var optics=s.Cyla;float thickness=(float)optics["Thickness"];
            // The compiled shader visibly loses precision at the real 15.3e9 m
            // radius. A tangent-aligned optical proxy keeps near-surface floats
            // bounded. It does not alter terrain, atmosphere forces or ring motion.
            double renderRadius=Math.Min(s.Geometry.P.Radius,optics["ProxyRadius"]);
            // Keep the compiled shader's optical quantities small. Terrain and
            // physical atmosphere remain in metres; scene depth is converted below.
            float unit=OpticalUnitsPerMetre;
#if RINGWORLD_SMOKE_TEST
            unit=ProbeUnits;
#endif
            Vector3 renderCentre=ConvertVector.Unity(s.Geometry.Up(observer))*(float)(renderRadius-coord.Altitude);
            renderCentre-=ConvertVector.Unity(s.Geometry.Axis)*(float)coord.Across;
            material.SetFloat("outerRadius",(float)renderRadius*unit);
            material.SetFloat("innerRadius",(float)(renderRadius-thickness)*unit);
            material.SetFloat("transparentRadius",(float)(renderRadius-Math.Max(32,thickness*optics["TransparentDepth"]))*unit);
            material.SetFloat("height",(float)(s.Geometry.P.Width*optics["WidthScale"])*unit);
            renderCentre+=ConvertVector.Unity(s.Geometry.AlongDirection(observer))*(float)optics["OffsetAlong"]+ConvertVector.Unity(s.Geometry.Axis)*(float)optics["OffsetAcross"]+ConvertVector.Unity(s.Geometry.Up(observer))*(float)optics["OffsetUp"];
            renderCentre*=unit;
            renderCentre+=camera.transform.position;
            material.SetVector("centerPosition",renderCentre);material.SetVector("axis",Quaternion.AngleAxis((float)optics["Yaw"],ConvertVector.Unity(s.Geometry.Up(observer)))*Quaternion.AngleAxis((float)optics["Pitch"],ConvertVector.Unity(s.Geometry.AlongDirection(observer)))*ConvertVector.Unity(s.Geometry.Axis));
            string[] modes={"TRANSPARENT_TOP_AND_SIDE","TRANSPARENT_FLOOR","UNLIT"};for(int i=0;i<modes.Length;i++){if(i==optics.LightingMode)material.EnableKeyword(modes[i]);else material.DisableKeyword(modes[i]);}
                        material.SetVector("lightEmitterPosition",camera.transform.position+ConvertVector.Unity(RingLighting.Direction(s,observer,Planetarium.GetUniversalTime()))*(float)renderRadius*unit);
            float sunlight=(float)s.Geometry.Daylight(coord.Along,Planetarium.GetUniversalTime(),coord.Across,coord.Altitude);
            material.SetVector("lightColor",new Vector4(sunlight,sunlight,sunlight,1)*(float)s.AtmosphereExposure);
            material.SetVector("rayleighScattering",optics.Scattering("Rayleigh")*(float)s.Haze/unit);
            material.SetVector("mieScattering",optics.Scattering("Mie")*(float)s.Haze/unit);
            material.SetFloat("rayleighScaleHeight",(float)optics["RayleighHeight"]*unit);material.SetFloat("mieScaleHeight",(float)optics["MieHeight"]*unit);material.SetFloat("miePhaseAsymmetry",(float)optics["Asymmetry"]);
            material.SetInt("raymarchingIterations",(int)optics["ViewSteps"]);
            material.SetInt("transmittanceIterations",s.CylaLightSteps);
            material.SetInt("ditheredRaymarching",photo?0:s.CylaDither?1:0);
            material.SetFloat("frame",photo?0:(frame++%1024));
            int divisor=s.CylaDivisor;
            int width=Math.Max(1,camera.pixelWidth),height=Math.Max(1,camera.pixelHeight),w=Math.Max(1,width/divisor),h=Math.Max(1,height/divisor);
            if(background==null||background.width!=width||background.height!=height){Free(ref background);background=Target(width,height,"Cyla camera background");}
            commands.Clear();commands.Blit(BuiltinRenderTextureType.CameraTarget,background);
            if(scaledDepth==null||scaledDepth.width!=w||scaledDepth.height!=h)
            {
                Free(ref scaledDepth);
                scaledDepth=new RenderTexture(w,h,0,RenderTextureFormat.RFloat,RenderTextureReadWrite.Linear){name="Cyla optical depth",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                if(!scaledDepth.Create()){Status="Cyla depth allocation failed; using Original";Free(ref scaledDepth);Detach();return;}
            }
            composite.SetFloat("_CylaUnitScale",unit);commands.Blit(background,scaledDepth,composite,4);
            BindDepth(backgroundBinding);BindDepth(blackBinding);BindDepth(whiteBinding);
            if(divisor==1)
            {
                Free(ref black);Free(ref white);
                backgroundBinding.SetTexture("_RamaAtmoBackgroundTexture",background);
                commands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                commands.DrawMesh(quad,Matrix4x4.identity,material,0,1,backgroundBinding);

            }
            else
            {
                if(black==null||black.width!=w||black.height!=h){Free(ref black);Free(ref white);black=Target(w,h,"Cyla scattering");white=Target(w,h,"Cyla white response");}
                blackBinding.SetTexture("_RamaAtmoBackgroundTexture",Texture2D.blackTexture);whiteBinding.SetTexture("_RamaAtmoBackgroundTexture",Texture2D.whiteTexture);
                commands.SetRenderTarget(black);commands.ClearRenderTarget(false,true,Color.clear);commands.DrawMesh(quad,Matrix4x4.identity,material,0,1,blackBinding);
                commands.SetRenderTarget(white);commands.ClearRenderTarget(false,true,Color.white);commands.DrawMesh(quad,Matrix4x4.identity,material,0,1,whiteBinding);

                composite.SetTexture("_CylaBlack",black);composite.SetTexture("_CylaWhite",white);composite.SetVector("_CylaTexel",new Vector4(1f/w,1f/h,w,h));
                commands.Blit(background,BuiltinRenderTextureType.CameraTarget,composite,3);
            }
            if(!attached){camera.AddCommandBuffer(CameraEvent.BeforeImageEffects,commands);attached=true;}
            if(!diagnosticQueued&&sunlight>.5f&&frame>16)
            {
                diagnosticQueued=true;var owner=camera.GetComponent<RingVisualRenderer>();
                if(owner!=null)owner.StartCoroutine(CylaRenderDiagnostics.Capture(divisor==1?null:black,divisor==1?null:white,background));
                Debug.Log("[NivenRingworld] CYLA ENV os="+SystemInfo.operatingSystem+" gpu="+SystemInfo.graphicsDeviceName+" driver="+SystemInfo.graphicsDeviceVersion+" api="+SystemInfo.graphicsDeviceType+" unity="+Application.unityVersion+" colour="+QualitySettings.activeColorSpace+" path="+camera.actualRenderingPath+" msaa="+QualitySettings.antiAliasing+" cameraMSAA="+camera.allowMSAA+" hdr="+camera.allowHDR+" depth="+camera.depthTextureMode+" clip="+camera.nearClipPlane+"/"+camera.farClipPlane+" altitude="+coord.Altitude+" across="+coord.Across+" radius="+renderRadius+" opticalUnitsPerMetre="+unit+" view="+optics["ViewSteps"]+" light="+s.CylaLightSteps+" divisor="+divisor+" dither="+s.CylaDither);
            }
            Active=true;Status="Cyla local optical approximation / "+(optics["ViewSteps"])+" view, "+(s.CylaLightSteps)+" light steps";
        }
        private void Detach(){if(attached&&camera!=null)camera.RemoveCommandBuffer(CameraEvent.BeforeImageEffects,commands);attached=false;}
        internal void OrderBefore(CommandBuffer postProcessing)
        {
            if(!attached||postProcessing==null)return;
            var buffers=camera.GetCommandBuffers(CameraEvent.BeforeImageEffects);
            int own=Array.IndexOf(buffers,commands),post=Array.IndexOf(buffers,postProcessing);
            if(own<0||post<0||own<post)return;
            // Keep every other buffer's relative order, inserting only our optical pass.
            foreach(var buffer in buffers)camera.RemoveCommandBuffer(CameraEvent.BeforeImageEffects,buffer);
            foreach(var buffer in buffers)
            {
                if(buffer==commands)continue;
                if(buffer==postProcessing)camera.AddCommandBuffer(CameraEvent.BeforeImageEffects,commands);
                camera.AddCommandBuffer(CameraEvent.BeforeImageEffects,buffer);
            }
        }
        private void BindDepth(MaterialPropertyBlock binding)
        {
            binding.Clear();binding.SetTexture("_CameraDepthTexture",scaledDepth);
        }
        public void Dispose(){Detach();

Free(ref background);Free(ref black);Free(ref white);Free(ref scaledDepth);if(commands!=null)commands.Release();if(material!=null)UnityEngine.Object.Destroy(material);if(quad!=null)UnityEngine.Object.Destroy(quad);Active=false;}
    }
}

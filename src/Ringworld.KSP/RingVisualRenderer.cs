using System;
using System.IO;
using System.Collections;
using System.Linq;
using Ringworld.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace NivenRingworld
{
    // One image effect on the final local camera. No planetary material or global
    // graphics preset is replaced. The bundled shader is original project source.
    internal sealed class RingVisualRenderer : MonoBehaviour
    {
        private Camera cameraComponent;
        private CylaAtmosphereBridge cyla;
        internal bool CylaActive {get{return cyla!=null&&cyla.Active;}}
        private DepthTextureMode previousDepth;
        private AssetBundle bundle;
        private Material material;
        private Extensions.RingworldClouds clouds;
        internal bool CloudRendering {get{return Rendering&&Extensions.RingworldClouds.Requested(Settings);}}
        private RenderTexture volume,photoBase,photoDepth,photoA,photoB,photoResult;
        private float savedTimeScale;
        private int savedFrameRate,photoFrame,photoSamples;
        private bool savedUi,photoStarted,photoFinished,failed;
        private int postProcessedFrame=-1;
        private static readonly int PostSource=Shader.PropertyToID("_NivenRingworldPostSource");
        private static readonly int[] PhotoEdges={0,1280,1920,2560,3840,7680,15360};
        private static readonly string[] PhotoSizes={"Screen","720p class (1280)","1080p class (1920)","1440p class (2560)","4K (3840)","8K (7680)","16K (15360)"};
        private int photoSize,outputWidth,outputHeight,viewportWidth,viewportHeight;
        private bool photoSizeChooser,capturingPhoto,captureQueued;
        private RenderTexture captureTarget;
        private bool photoChooser;private int photoPreset=2;private Vector2 photoScroll;
        internal bool SimplePhoto {get{return PhotoActive&&(!Settings.Atmosphere||(Settings.VisualQuality==0&&Settings.AtmosphereBackend==0&&!Extensions.RingworldClouds.Requested(Settings)));}}
        internal void DrawPhotoEntry()
        {
            if(GUILayout.Button("Photo quality: "+RingQualityPresets.Names[photoPreset]+"  v"))photoChooser=!photoChooser;
            if(photoChooser){photoScroll=GUILayout.BeginScrollView(photoScroll,GUILayout.Height(180));for(int i=0;i<RingQualityPresets.Names.Length;i++)if(GUILayout.Button(RingQualityPresets.Names[i])){photoPreset=i;photoChooser=false;}GUILayout.EndScrollView();}
            if(GUILayout.Button("Photo output: "+PhotoSizes[photoSize]+"  v"))photoSizeChooser=!photoSizeChooser;
            if(photoSizeChooser)for(int i=0;i<PhotoSizes.Length;i++)if(GUILayout.Button(PhotoSizes[i])){photoSize=i;photoSizeChooser=false;}
            GUILayout.Label("Output keeps the screen aspect ratio; sizes specify the longer edge. High resolutions need substantial GPU memory and can take minutes.");
            if(GUILayout.Button("Enter photo mode (freezes flight)"))BeginPhoto(null,photoPreset);
        }
        internal float PrecipitationBase { get { return clouds==null?700:clouds.RainBase; } }
        internal float StormTop { get { return clouds==null?12000:clouds.StormTop; } }
        private Vector4 PhotoQuality {get{return new Vector4(Settings.CloudSteps,CylaActive?16:Settings.AtmosphereSteps,Settings.VisualQuality==2?6:4,(float)Settings.RenderCloudRange);}}
        private Vector3 photoPosition;
        private Quaternion photoRotation;
        private float photoFov;
        private const string PhotoLock="NivenRingworld.Photo";
        internal bool PhotoActive {get;private set;}
        internal bool Rendering {get;private set;}
        internal bool PhotoFinished {get{return photoFinished;}}
        internal string LastPhoto {get;private set;}
        internal string Status="Laptop atmosphere active.";
        internal int RenderedFrames {get;private set;}
        private Settings Settings {get{return RingworldFlight.Instance.Settings;}}
        internal void InvalidateWeather(){} // Parameters are now sampled continuously; there is no timed weather cache.
        public void Awake(){cameraComponent=GetComponent<Camera>();previousDepth=cameraComponent.depthTextureMode;cyla=new CylaAtmosphereBridge(cameraComponent);}
        private bool EnsureAssets()
        {
            if(material!=null)return true;if(failed)return false;
            try
            {
                if(!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)||!SystemInfo.supports3DTextures)throw new InvalidOperationException("GPU does not support the required volume textures.");
                bundle=RingVisualAssets.Acquire();if(bundle==null)throw new InvalidOperationException("Ringworld visual shader bundle is missing or incompatible.");
                var shader=Extensions.ExtensionProviders.Clouds?.AtmosphereShader??bundle.LoadAsset<Shader>("Assets/Shaders/RingAtmosphere.shader");
                if(shader==null||!shader.isSupported)throw new InvalidOperationException("Ringworld volumetric shader is unsupported on this graphics API.");
                material=new Material(shader);material.SetTexture("_Noise",bundle.LoadAsset<Texture3D>("Assets/CloudNoise.asset"));
                clouds=new Extensions.RingworldClouds(material,bundle);
                Debug.Log("[NivenRingworld] GPU atmosphere ready: "+SystemInfo.graphicsDeviceType+" / "+SystemInfo.graphicsDeviceName);
                return true;
            }
            catch(Exception e){if(material!=null)Destroy(material);material=null;clouds=null;failed=true;Status=e.Message+" Using laptop atmosphere.";Debug.LogError("[NivenRingworld] "+Status);return false;}
        }
        internal Shader WaterShader(bool refraction=false){return Extensions.ExtensionProviders.Scattering?.WaterShader(refraction);}
        internal void Prepare(bool allowed,Vector3d star)
        {
            if(PhotoActive&&(!allowed||MapView.MapIsEnabled)){EndPhoto();Status="Photo cancelled after camera/scene change.";}
            if(Settings.AtmosphereBackend==1)EnsureAssets();
            cyla.Prepare(allowed&&!MapView.MapIsEnabled&&!RingworldFlight.Instance.AtmosphereTransition,Settings,star,PhotoActive,photoStarted,material);
            Rendering=allowed&&(Settings.VisualQuality>0||PhotoActive||CylaActive||Extensions.RingworldClouds.Requested(Settings))&&EnsureAssets();
            if(!Rendering){enabled=PhotoActive;return;}
            var observer=ConvertVector.Core((Vector3d)cameraComponent.transform.position-star);var g=Settings.Geometry;var c=g.Coordinates(observer);
            Rendering=PhotoActive||(c.Altitude>-1000&&c.Altitude<600000&&Math.Abs(c.Across)<g.P.Width/2+500000&&Settings.Atmosphere);
            if(!Rendering){enabled=PhotoActive;return;}
            if(!enabled){previousDepth=cameraComponent.depthTextureMode;enabled=true;}
            cameraComponent.depthTextureMode|=DepthTextureMode.Depth;
            if(PhotoActive&&photoStarted)
            {cameraComponent.transform.SetPositionAndRotation(photoPosition,photoRotation);cameraComponent.fieldOfView=photoFov;return;}
            double time=Planetarium.GetUniversalTime();
            double extent=Math.Max(150000,Settings.RenderCloudRange*1.15);
            var currentWeather=RingCloudField.Apply(material,Settings,c.Along,c.Across,c.Altitude,time);
            clouds.Update(Settings,currentWeather,time);
            var surface=Settings.Terrain.Sample(c.Along,c.Across);
            string localWeather=RingWeather.SurfaceKind(currentWeather,surface.Biome,Math.Abs(c.Across)/(g.P.Width*.5),g.Daylight(c.Along,time,c.Across,c.Altitude));
            material.SetVector("_SurfaceWeather",new Vector4(localWeather=="Rim-shadow fog"?(float)currentWeather.Cloud:0,localWeather=="Blowing dust"?(float)currentWeather.Storm:0,(float)surface.Height,surface.Biome==Biome.Snow?1:0));

            material.SetVector("_CloudHandoff",RingCloudField.Handoff(Settings,true));
            DVec up=g.Up(observer),along=g.AlongDirection(observer),across=DVec.Cross(up,along).Unit;
            Func<Vector3,Vector3> local=v=>new Vector3((float)DVec.Dot(ConvertVector.Core(v),along),(float)DVec.Dot(ConvertVector.Core(v),across),(float)DVec.Dot(ConvertVector.Core(v),up));
            // Geometry's positive Across is world +Y. Correct the tangent handedness explicitly.
            across=g.Axis;
            material.SetVector("_RayRight",local(cameraComponent.transform.right)/cameraComponent.projectionMatrix.m00);
            material.SetVector("_RayUp",local(cameraComponent.transform.up)/cameraComponent.projectionMatrix.m11);
            material.SetVector("_RayForward",local(cameraComponent.transform.forward));
            material.SetVector("_Sun",local(ConvertVector.Unity(RingLighting.Direction(Settings,observer,time))));
            material.SetVector("_Habitat",new Vector4((float)c.Altitude,(float)g.P.Radius,(float)c.Across,(float)(g.P.Width/2)));
            material.SetVector("_WeatherMap",new Vector4(0,0,(float)extent,(float)currentWeather.Cloud));
            material.SetVector("_Look",new Vector4(CylaActive?0:(float)Settings.Haze,(float)g.Daylight(c.Along,time,c.Across,c.Altitude),(float)Settings.CloudShadow,(float)Settings.AtmosphereExposure));
            material.SetVector("_Quality",new Vector4(Settings.VisualQuality==0?Math.Min(32,Settings.CloudSteps):Settings.CloudSteps,CylaActive?(currentWeather.Rain>.01||localWeather=="Rim-shadow fog"?16:0):Settings.AtmosphereSteps,Settings.VisualQuality==2?6:4,(float)Settings.RenderCloudRange));
            if(!PhotoActive)Status=(Settings.AtmosphereBackend==1?cyla.Status+" | ":"")+"Weather: "+(currentWeather.Storm>.1?"thunderstorm":currentWeather.Rain>.1?"rain":currentWeather.Cloud>.5?"cloudy":"fair")+" / Ringworld Clouds: "+(CloudRendering?Settings.CloudMode+" / "+Settings.CloudSteps+" view steps":"layers only")+" / "+Settings.AtmosphereSteps+" atmosphere steps.";
        }
        private static RenderTexture Target(int w,int h,RenderTextureFormat format,string name)
        {var rt=new RenderTexture(w,h,0,format,RenderTextureReadWrite.Linear){name=name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};rt.Create();return rt;}
        private static void Free(ref RenderTexture rt){if(rt==null)return;rt.Release();Destroy(rt);rt=null;}
        public void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            if(!PhotoActive&&postProcessedFrame==Time.frameCount){Graphics.Blit(source,destination);return;}
            if(!Rendering||material==null){Graphics.Blit(source,destination);return;}
            try
            {
                if(PhotoActive&&(Screen.width!=viewportWidth||Screen.height!=viewportHeight))
                {EndPhoto();Status="Photo cancelled because the viewport size changed. Frame the shot and try again.";Graphics.Blit(source,destination);return;}
                material.SetVector("_FrameSize",new Vector2(source.width,source.height));
                if(PhotoActive)
                {
                    if(capturingPhoto){RenderPhoto(source,destination);return;}
                    if(photoStarted){material.SetVector("_FrameSize",new Vector2(photoBase.width,photoBase.height));RenderPhoto(photoBase,destination);return;}
                    Graphics.Blit(source,destination);
                    Status="Preparing selected photo terrain: "+RingworldFlight.Instance.LodPending+" chunks remaining. Flight is frozen.";
                    if(!captureQueued&&RingworldFlight.Instance.PhotoTerrainReady){captureQueued=true;StartCoroutine(CaptureWorld());}
                    return;
                }
                int divisor=Settings.VisualQuality<2?2:1,w=Math.Max(1,source.width/divisor),h=Math.Max(1,source.height/divisor);
                if(volume==null||volume.width!=w||volume.height!=h){Free(ref volume);volume=Target(w,h,RenderTextureFormat.ARGBHalf,"Ringworld realtime atmosphere");}
                material.SetVector("_Photo",Vector4.zero);material.SetFloat("_Jitter",0);
                Graphics.Blit(source,volume,material,0);material.SetTexture("_Volume",volume);Graphics.Blit(source,destination,material,1);RingWeatherEffects.Instance?.Draw(cameraComponent,destination);RenderedFrames++;
            }
            catch(Exception e){Debug.LogException(e);Status="Visual render failed: "+e.Message;EndPhoto();Rendering=false;failed=true;Graphics.Blit(source,destination);}
        }
        // Insert clouds before TUFX's colour grading/bloom, rather than overlaying
        // untreated clouds on the already graded world. TUFX owns the command buffer.
        internal void AppendBeforePostProcessing(CommandBuffer commands,RenderTargetIdentifier source)
        {
            if(commands==null)return;
            if(cyla!=null)cyla.OrderBefore(commands);
            if(!Rendering||material==null||PhotoActive)return;
            int width=Math.Max(1,cameraComponent.pixelWidth),height=Math.Max(1,cameraComponent.pixelHeight);
            int divisor=Settings.VisualQuality<2?2:1,w=Math.Max(1,width/divisor),h=Math.Max(1,height/divisor);
            if(volume==null||volume.width!=w||volume.height!=h){Free(ref volume);volume=Target(w,h,RenderTextureFormat.ARGBHalf,"Ringworld realtime atmosphere");}
            material.SetVector("_FrameSize",new Vector2(width,height));material.SetVector("_Photo",Vector4.zero);material.SetFloat("_Jitter",0);
            commands.GetTemporaryRT(PostSource,width,height,0,FilterMode.Bilinear,RenderTextureFormat.ARGBHalf);
            commands.Blit(source,PostSource);
            commands.Blit(PostSource,volume,material,0);material.SetTexture("_Volume",volume);
            commands.Blit(PostSource,source,material,1);RingWeatherEffects.Instance?.Append(commands,cameraComponent,source);commands.ReleaseTemporaryRT(PostSource);
            postProcessedFrame=Time.frameCount;RenderedFrames++;
        }
        internal bool BeginPhoto(int? sampleOverride=null,int? preset=null,int? edgeOverride=null)
        {
            var f=RingworldFlight.Instance;
            if(PhotoActive)return false;
            if(f==null||!f.Ready||MapView.MapIsEnabled||TimeWarp.CurrentRate>1.0001f||Time.timeScale==0){Status="Photo mode needs normal flight view at 1x. Frame the camera first.";return false;}
            if(!EnsureAssets())return false;
            viewportWidth=Screen.width;viewportHeight=Screen.height;
            int edge=edgeOverride??PhotoEdges[photoSize];double scale=edge<=0?1:(double)edge/Math.Max(viewportWidth,viewportHeight);
            outputWidth=Math.Max(1,(int)Math.Round(viewportWidth*scale));outputHeight=Math.Max(1,(int)Math.Round(viewportHeight*scale));
            // Full scene + depth + accumulation + engine intermediates. Refuse an
            // obviously impossible allocation rather than crashing a small GPU.
            long estimated=(long)outputWidth*outputHeight*(80+8*Math.Max(1,QualitySettings.antiAliasing));
            if(Math.Max(outputWidth,outputHeight)>SystemInfo.maxTextureSize||
                (edge>Math.Max(viewportWidth,viewportHeight)&&SystemInfo.graphicsMemorySize>0&&estimated>SystemInfo.graphicsMemorySize*1048576L/2))
            {Status="Photo resolution exceeds this GPU's texture or memory budget. Choose a smaller output.";return false;}
            savedTimeScale=Time.timeScale;savedFrameRate=Application.targetFrameRate;savedUi=KSP.UI.UIMasterController.Instance!=null&&KSP.UI.UIMasterController.Instance.IsUIShowing;
            f.PhotoTerrain(true,preset);
            PhotoActive=true;captureQueued=false;photoStarted=false;photoFinished=false;photoFrame=0;photoSamples=Math.Max(1,Math.Min(64,sampleOverride??Settings.PhotoSamples));LastPhoto=null;
            Time.timeScale=0;Application.targetFrameRate=15;InputLockManager.SetControlLock(ControlTypes.All,PhotoLock);
            if(savedUi)KSP.UI.UIMasterController.Instance.HideUI();
            Status="Preparing frozen photo. No flight time will pass.";return true;
        }
        private IEnumerator CaptureWorld()
        {
            yield return new WaitForEndOfFrame();
            if(!PhotoActive)yield break;
            var active=RenderTexture.active;
            try
            {
                captureTarget=new RenderTexture(outputWidth,outputHeight,24,RenderTextureFormat.ARGB32){name="Ringworld high-resolution scene",antiAliasing=Math.Max(1,QualitySettings.antiAliasing)};
                if(!captureTarget.Create())throw new InvalidOperationException("Photo render target allocation failed.");
                RenderTexture.active=captureTarget;GL.Clear(true,true,Color.black);
                // Stock flight world stack only: never include UI or crew portraits.
                var cameras=Camera.allCameras.Where(c=>c.enabled&&(c==cameraComponent||c.name=="GalaxyCamera"||c.name=="Camera ScaledSpace"||c.name=="Camera 01")).OrderBy(c=>c.depth).ToArray();
                capturingPhoto=true;
                foreach(var camera in cameras)
                {
                    var target=camera.targetTexture;var projection=camera.projectionMatrix;var rect=camera.rect;float aspect=camera.aspect;
                    try{camera.targetTexture=captureTarget;camera.rect=new Rect(0,0,1,1);camera.aspect=(float)viewportWidth/viewportHeight;camera.projectionMatrix=projection;if(camera==cameraComponent)Prepare(true,RingworldFlight.Instance.Center);camera.Render();}
                    finally{camera.targetTexture=target;camera.rect=rect;camera.aspect=aspect;camera.projectionMatrix=projection;}
                }
                if(!photoStarted)throw new InvalidOperationException("Final flight camera did not produce the photo.");
            }
            catch(Exception e){EndPhoto();Status="Photo capture failed: "+e.Message;Debug.LogException(e);}
            finally{capturingPhoto=false;RenderTexture.active=active;Free(ref captureTarget);}
        }
        private void RenderPhoto(RenderTexture source,RenderTexture destination)
        {
            if(!photoStarted&&!RingworldFlight.Instance.PhotoTerrainReady)
            {Status="Preparing selected photo terrain: "+RingworldFlight.Instance.LodPending+" chunks remaining. Flight is frozen.";Graphics.Blit(source,destination);return;}
            if(!photoStarted)
            {
                photoPosition=cameraComponent.transform.position;photoRotation=cameraComponent.transform.rotation;photoFov=cameraComponent.fieldOfView;
                photoBase=Target(source.width,source.height,RenderTextureFormat.ARGB32,"Ringworld frozen scene");
                photoDepth=Target(source.width,source.height,RenderTextureFormat.RFloat,"Ringworld frozen depth");
                photoA=Target(source.width,source.height,RenderTextureFormat.ARGBHalf,"Ringworld photo accumulation A");photoB=Target(source.width,source.height,RenderTextureFormat.ARGBHalf,"Ringworld photo accumulation B");
                photoResult=Target(source.width,source.height,RenderTextureFormat.ARGB32,"Ringworld photo result");
                Graphics.Blit(source,photoBase);Graphics.Blit(source,photoDepth,material,2);Graphics.Blit(Texture2D.blackTexture,photoA);Graphics.Blit(Texture2D.blackTexture,photoB);
                material.SetTexture("_SavedDepth",photoDepth);photoStarted=true;
                // Initialize all pixels cheaply, then refine one of 16 tiles per rendered frame.
                material.SetVector("_Quality",new Vector4(24,16,2,(float)Settings.RenderCloudRange));material.SetVector("_Photo",new Vector4(1,-1,4,0));material.SetFloat("_Jitter",0);
                if(!SimplePhoto)Graphics.Blit(photoBase,photoA,material,0);material.SetVector("_Quality",PhotoQuality);
            }
            if(!photoFinished&&!SimplePhoto)
            {
                int sample=photoFrame/16,tile=photoFrame%16;
                material.SetVector("_Photo",new Vector4(1,tile,4,sample));material.SetFloat("_Jitter",sample+.5f);material.SetTexture("_Previous",photoA);
                Graphics.Blit(photoBase,photoB,material,0);var swap=photoA;photoA=photoB;photoB=swap;
                photoFrame++;Status="Rendering photo: "+(100*photoFrame/(16*photoSamples))+"% â€” Cancel restores flight.";
            }
            if(SimplePhoto){Graphics.Blit(photoBase,photoResult);photoFrame=photoSamples*16;}
            else {material.SetTexture("_Volume",photoA);Graphics.Blit(photoBase,photoResult,material,1);}
            RingWeatherEffects.Instance?.Draw(cameraComponent,photoResult,photoDepth);
            Graphics.Blit(photoResult,destination);
            if(!photoFinished&&photoFrame>=photoSamples*16)
            {
                var previous=RenderTexture.active;Texture2D image=null;
                try
                {
                    RenderTexture.active=photoResult;image=new Texture2D(photoResult.width,photoResult.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,image.width,image.height),0,0);image.Apply(false);
                    string folder=Path.Combine(KSPUtil.ApplicationRootPath,"Screenshots","Ringworld");Directory.CreateDirectory(folder);
                    LastPhoto=Path.Combine(folder,"Ringworld-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".png");File.WriteAllBytes(LastPhoto,image.EncodeToPNG());
                    File.WriteAllText(Path.ChangeExtension(LastPhoto,"txt"),"Ringworld GPU photo (scene rendered at output resolution)\nUTC: "+DateTime.UtcNow.ToString("O")+"\nUT: "+Planetarium.GetUniversalTime().ToString("R")+"\nWorld seed: "+Settings.Geometry.P.Seed+"\nSize: "+image.width+"x"+image.height+"\nCloud/air steps: "+Settings.CloudSteps+"/"+Settings.AtmosphereSteps+"\nAccumulated samples: "+photoSamples+"\n"+StockGraphics.Description+"\n");
                    photoFinished=true;Status="Photo saved. Resume returns to your normal graphics.\n"+LastPhoto;Debug.Log("[NivenRingworld] PHOTO SAVED "+LastPhoto);
                }
                finally{RenderTexture.active=previous;if(image!=null)Destroy(image);}
            }
        }
        internal void EndPhoto()
        {
            if(!PhotoActive)return;
            var flight=RingworldFlight.Instance;if(flight!=null)flight.PhotoTerrain(false);
            PhotoActive=false;Time.timeScale=savedTimeScale;Application.targetFrameRate=savedFrameRate;InputLockManager.RemoveControlLock(PhotoLock);
            if(savedUi&&KSP.UI.UIMasterController.Instance!=null)KSP.UI.UIMasterController.Instance.ShowUI();
            Free(ref captureTarget);Free(ref photoBase);Free(ref photoDepth);Free(ref photoA);Free(ref photoB);Free(ref photoResult);
            photoStarted=false;Rendering=false;
        }
        public void OnGUI()
        {
            if(!PhotoActive)return;
            GUILayout.BeginArea(new Rect(20,Screen.height-145,Mathf.Min(700,Screen.width-40),125),GUI.skin.box);
            GUILayout.Label(Status);if(GUILayout.Button(photoFinished?"Resume flight":"Cancel photo"))EndPhoto();GUILayout.EndArea();
        }
        public void OnDisable(){EndPhoto();if(cameraComponent!=null)cameraComponent.depthTextureMode=previousDepth;}
        public void OnDestroy()
        {
            EndPhoto();if(cyla!=null)cyla.Dispose();Free(ref volume);if(cameraComponent!=null)cameraComponent.depthTextureMode=previousDepth;
            if(material!=null)Destroy(material);if(bundle!=null)RingVisualAssets.Release();
        }
    }
}

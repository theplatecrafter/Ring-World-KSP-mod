using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace NivenRingworld
{
    // No compile-time dependency on optional mods. Resolve and validate their
    // actual interfaces once; a missing/changed interface must not break flight.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class OptionalVisualIntegrations : MonoBehaviour
    {
        internal static OptionalVisualIntegrations Instance;
        internal string TufxStatus="TUFX not installed", ScattererStatus="Scatterer not installed";
        private Type loaderType;
        private FieldInfo loaderInstance, currentProfile, flareSource, flareMaterial;
        private PropertyInfo profiles, profileName, contextCamera, contextCommand, contextSource;
        private MethodInfo applyProfile;
        private object flightScene, previousProfile, ownedProfile;
        private bool relinquished, wasEligible, disabled;
        private static bool hooksInstalled;
        private static bool tufxRenderFailed, scattererFailed;
        private static FieldInfo scatterSource, scatterMaterial;
        private static PropertyInfo renderCamera, renderCommand, renderSource;

        internal static Type FindType(string name)
        {
            foreach(var assembly in AssemblyLoader.loadedAssemblies)
            {
                var type=assembly.assembly.GetType(name,false);
                if(type!=null)return type;
            }
            return null;
        }
        public void Awake()
        {
            Instance=this;
            try
            {
                loaderType=FindType("TUFX.TexturesUnlimitedFXLoader");
                if(loaderType!=null)
                {
                    loaderInstance=AccessTools.Field(loaderType,"INSTANCE");
                    currentProfile=AccessTools.Field(loaderType,"currentProfile");
                    profiles=AccessTools.Property(loaderType,"Profiles");
                    var profileType=FindType("TUFX.TUFXProfile");
                    var sceneType=FindType("TUFX.TUFXScene");
                    profileName=AccessTools.Property(profileType,"ProfileName");
                    applyProfile=AccessTools.Method(loaderType,"ApplyProfile",new[]{profileType,sceneType});
                    if(loaderInstance==null||currentProfile==null||profiles==null||profileName==null||applyProfile==null)
                        throw new NotSupportedException("TUFX profile interface changed");
                    flightScene=Enum.Parse(sceneType,"Flight");TufxStatus="TUFX detected; waiting for ring flight";
                }
            }
            catch(Exception e){disabled=true;TufxStatus="TUFX automatic profile unavailable (see KSP.log)";Debug.LogWarning("[NivenRingworld] Optional integration: "+e.Message);}
            try{InstallHooks();}
            catch(Exception e){Debug.LogWarning("[NivenRingworld] Optional render hooks unavailable: "+e.Message);}
        }
        private void InstallHooks()
        {
            var flare=FindType("Scatterer.SunFlare");
            if(flare!=null)ScattererStatus="Scatterer detected; panel-flare adapter unavailable";
            if(hooksInstalled)
            {
                if(scatterMaterial!=null)ScattererStatus="Scatterer: ring panel-flare occlusion active";
                return;
            }
            var harmony=new Harmony("NivenRingworld.optionalVisuals");
            if(flare!=null)
            {
                flareSource=AccessTools.Field(flare,"source");flareMaterial=AccessTools.Field(flare,"sunglareMaterial");
                var update=AccessTools.Method(flare,"updateProperties",Type.EmptyTypes);
                if(flareSource!=null&&flareMaterial!=null&&update!=null)
                {
                    scatterSource=flareSource;scatterMaterial=flareMaterial;
                    harmony.Patch(update,postfix:new HarmonyMethod(typeof(OptionalVisualIntegrations),nameof(ScattererFlare)));
                    ScattererStatus="Scatterer: ring panel-flare occlusion active";
                }
            }
            var layer=FindType("UnityEngine.Rendering.PostProcessing.PostProcessLayer");
            var context=FindType("UnityEngine.Rendering.PostProcessing.PostProcessRenderContext");
            if(loaderType!=null&&layer!=null&&context!=null)
            {
                contextCamera=AccessTools.Property(context,"camera");contextCommand=AccessTools.Property(context,"command");contextSource=AccessTools.Property(context,"source");
                var render=AccessTools.Method(layer,"Render",new[]{context});
                if(contextCamera!=null&&contextCommand!=null&&contextSource!=null&&render!=null)
                {
                    renderCamera=contextCamera;renderCommand=contextCommand;renderSource=contextSource;
                    harmony.Patch(render,prefix:new HarmonyMethod(typeof(OptionalVisualIntegrations),nameof(BeforeTufx)));
                }
            }
            hooksInstalled=true;
        }
        private static void BeforeTufx(object __0)
        {
            if(tufxRenderFailed)return;
            try
            {
            var camera=renderCamera.GetValue(__0,null) as Camera;
            if(camera==null)return;
            var ring=camera.GetComponent<RingVisualRenderer>();
            if(ring==null)return;
            ring.AppendBeforePostProcessing(renderCommand.GetValue(__0,null) as CommandBuffer,(RenderTargetIdentifier)renderSource.GetValue(__0,null));
            }
            catch(Exception e){tufxRenderFailed=true;Debug.LogWarning("[NivenRingworld] TUFX render adapter disabled: "+e);}
        }
        private static void ScattererFlare(object __instance)
        {
            if(scattererFailed)return;
            try
            {
            float daylight;
            if(!RingPanelSunOcclusion.TryDaylight(scatterSource.GetValue(__instance) as CelestialBody,out daylight))return;
            var material=scatterMaterial.GetValue(__instance) as Material;
            if(material!=null)material.SetFloat("renderSunFlare",material.GetFloat("renderSunFlare")*daylight);
            }
            catch(Exception e){scattererFailed=true;Debug.LogWarning("[NivenRingworld] Scatterer flare adapter disabled: "+e);}
        }
        public void LateUpdate(){RefreshProfile();}
        internal void RefreshProfile()
        {
            if(disabled||loaderType==null)return;
            try
            {
                var loader=loaderInstance.GetValue(null);if(loader==null)return;
                var f=RingworldFlight.Instance;
                bool eligible=f!=null&&f.FrameInUse&&!MapView.MapIsEnabled&&CameraManager.Instance!=null&&CameraManager.Instance.currentCameraMode!=CameraManager.CameraMode.IVA&&CameraManager.Instance.currentCameraMode!=CameraManager.CameraMode.Internal;
                var current=currentProfile.GetValue(loader);
                if(!eligible)
                {
                    Restore(loader,current);relinquished=false;wasEligible=false;return;
                }
                if(ownedProfile!=null&&!ReferenceEquals(current,ownedProfile))
                {ownedProfile=null;previousProfile=null;relinquished=true;TufxStatus="TUFX: keeping selected profile";}
                if(!wasEligible){wasEligible=true;relinquished=false;}
                if(relinquished||current==null)return;
                string name=profileName.GetValue(current,null) as string;
                if(ownedProfile==null&&name!="Default-Flight")
                {TufxStatus="TUFX: keeping selected profile "+name;return;}
                string wanted=f.Settings.VisualQuality==0?"NivenRingworld-Economy":f.Settings.VisualQuality==1?"NivenRingworld-Balanced":"NivenRingworld-Cinematic";
                var available=profiles.GetValue(loader,null) as IDictionary;
                if(available==null||!available.Contains(wanted)){TufxStatus="TUFX: Ringworld profiles not loaded";return;}
                var next=available[wanted];if(ReferenceEquals(next,ownedProfile))return;
                if(ownedProfile==null)previousProfile=current;
                applyProfile.Invoke(loader,new[]{next,flightScene});ownedProfile=next;
                TufxStatus="TUFX: "+wanted;Debug.Log("[NivenRingworld] "+TufxStatus);
            }
            catch(Exception e){disabled=true;Debug.LogWarning("[NivenRingworld] TUFX adapter disabled: "+e);}
        }
        private void Restore(object loader,object current)
        {
            if(ownedProfile!=null&&ReferenceEquals(current,ownedProfile)&&previousProfile!=null)
                applyProfile.Invoke(loader,new[]{previousProfile,flightScene});
            ownedProfile=null;previousProfile=null;TufxStatus="TUFX: using player's scene profile";
        }
        public void OnDestroy()
        {
            try{if(loaderInstance!=null){var loader=loaderInstance.GetValue(null);if(loader!=null)Restore(loader,currentProfile.GetValue(loader));}}
            catch(Exception e){Debug.LogWarning("[NivenRingworld] TUFX profile restoration: "+e.Message);}
            if(Instance==this)Instance=null;
        }
    }
}

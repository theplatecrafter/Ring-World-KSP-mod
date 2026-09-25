#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using System.IO;
using Ringworld.Core;
using UnityEngine;
using HarmonyLib;

namespace NivenRingworld
{
    // Compiled only with -p:SmokeTest=true. Release packages do not contain this harness.
    [KSPAddon(KSPAddon.Startup.MainMenu,true)]
    public sealed class SmokeTest : MonoBehaviour
    {
        internal static KerbalEVA WalkingEva;
        internal static Vector2 WalkInput=Vector2.up;
        private string folder;
        private bool running;
        private float deadline;
        public IEnumerator Start()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-smoketest")<0)yield break;
            DontDestroyOnLoad(gameObject);deadline=Time.realtimeSinceStartup+1200;running=true;
            Debug.Log("[RingworldSmoke] MAIN MENU READY");
            yield return new WaitForSeconds(3);
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-integrations-only")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-visual-options-only")>=0)
            {try{GlobalCloudSmoke.Run();}catch(Exception ex){Fail("Global visual integrations: "+ex);yield break;}}
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-global-clouds-only")>=0)
            {
                try{GlobalCloudSmoke.Run();if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-scenery-only")>=0){ScenerySmoke.Run();LibrarySmoke.Run();}}catch(Exception ex){Fail("Global clouds/assets: "+ex);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-map-only")<0){running=false;Application.Quit();yield break;}
            }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-scenery-only")>=0)
            {
                try{ScenerySmoke.Run();LibrarySmoke.Run();}catch(Exception ex){Fail("Scenery: "+ex);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-landmarks-only")<0&&Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-gear-only")<0){running=false;Application.Quit();yield break;}
            }
            foreach(var dialog in UnityEngine.Object.FindObjectsOfType<WhatsNewDialog>())HarmonyLib.AccessTools.Method(typeof(WhatsNewDialog),"Dismiss").Invoke(dialog,null);
            folder="RingworldSmoke-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(Path.Combine(KSPUtil.ApplicationRootPath,"saves",folder));
            bool cylaSaveProbe=Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-cyla-save-probe")>=0;
            if(cylaSaveProbe&&File.Exists(Path.Combine(KSPUtil.ApplicationRootPath,"CylaNoNativeRenderer.flag")))
                new Harmony("NivenRingworld.CylaIsolationTest").Patch(AccessTools.Method("Cyla.CylindricalAtmosphereModule:OnStart"),prefix:new HarmonyMethod(typeof(SmokeTest),"SkipNativeCylaStart"));
            var root=ConfigNode.Load(cylaSaveProbe?Path.Combine(KSPUtil.ApplicationRootPath,"CylaFriend.sfs"):Path.Combine(KSPUtil.ApplicationRootPath,"saves","training","C_Orbit101.sfs"));
            var node=root.GetNode("GAME");node.SetValue("Mode","0");node.SetValue("Title",folder);node.SetValue("scene","7");
            foreach(var scenario in node.GetNodes("SCENARIO"))if((scenario.GetValue("name")??"").StartsWith("Tutorial"))node.RemoveNode(scenario);
            var state=node.GetNode("FLIGHTSTATE");
            if(cylaSaveProbe&&File.Exists(Path.Combine(KSPUtil.ApplicationRootPath,"CylaNoNativeRenderer.flag")))
                foreach(var vessel in state.GetNodes("VESSEL"))foreach(var part in vessel.GetNodes("PART"))foreach(var module in part.GetNodes("MODULE"))if(module.GetValue("name")=="CylindricalAtmosphereModule")part.RemoveNode(module);
            foreach(var vessel in state.GetNodes("VESSEL"))if(vessel.GetValue("type")=="SpaceObject")state.RemoveNode(vessel);
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-multi-ring-only")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-weather-only")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-residence-only")>=0)
            {
                // A single flat-bottomed command pod isolates rendering tests from articulated crash debris.
                foreach(var vessel in state.GetNodes("VESSEL"))
                {
                    var parts=vessel.GetNodes("PART");for(int i=1;i<parts.Length;i++)vessel.RemoveNode(parts[i]);
                    if(parts.Length>0)parts[0].RemoveValues("attN");
                }
            }
            int probeVessel=0;
            if(cylaSaveProbe){var vessels=state.GetNodes("VESSEL");for(int i=0;i<vessels.Length;i++)foreach(var part in vessels[i].GetNodes("PART"))if(part.GetValue("name")=="cylindricalAtmo")probeVessel=i;}
            state.SetValue("activeVessel",probeVessel.ToString(),true);
            var game=GamePersistence.LoadGameCfg(root,folder,true,false);
            if(game==null){Fail("Unable to load test fixture");yield break;}
            game.Mode=Game.Modes.SANDBOX;game.startScene=GameScenes.FLIGHT;HighLogic.SaveFolder=folder;HighLogic.CurrentGame=game;
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-science-only")>=0)
            {
                game.Mode=Game.Modes.CAREER;
                foreach(var type in new[]{typeof(ResearchAndDevelopment),typeof(Funding),typeof(Reputation)})
                    if(!game.scenarios.Exists(s=>s.moduleName==type.Name))game.AddProtoScenarioModule(type,GameScenes.FLIGHT,GameScenes.SPACECENTER,GameScenes.TRACKSTATION);
            }
            game.Parameters.Flight.CanEVA=true;
            if(!cylaSaveProbe)game.AddProtoScenarioModule(typeof(RingworldScenario),GameScenes.FLIGHT,GameScenes.SPACECENTER,GameScenes.TRACKSTATION);
            game.AddProtoScenarioModule(typeof(Expansions.Serenity.DeployedScience.Runtime.DeployedScience),GameScenes.FLIGHT,GameScenes.SPACECENTER,GameScenes.TRACKSTATION,GameScenes.EDITOR);
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-gear-only")>=0)
            {
                var craft=Path.Combine(KSPUtil.ApplicationRootPath,"saves","default","Ships","VAB","Auto-Saved Ship.craft");
                var craftCopy=Path.Combine(KSPUtil.ApplicationRootPath,"saves",folder,"GearRegression.craft");
                File.Copy(craft,craftCopy);
                game.flightState.protoVessels.Clear();game.startScene=GameScenes.SPACECENTER;game.Start();
                while(HighLogic.LoadedScene!=GameScenes.SPACECENTER)yield return null;yield return new WaitForSecondsRealtime(8);
                FlightDriver.StartWithNewLaunch(craftCopy,"Squad/Flags/default","LaunchPad",VesselCrewManifest.FromConfigNode(ConfigNode.Load(craftCopy)));
            }
            else game.Start();
            while(!HighLogic.LoadedSceneIsFlight||RingworldFlight.Instance==null||FlightGlobals.ActiveVessel==null||FlightGlobals.ActiveVessel.packed)yield return null;
            var v=FlightGlobals.ActiveVessel;
            Debug.Log("[RingworldSmoke] FLIGHT READY "+v.vesselName);
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-gear-only")>=0&&RingworldScenario.Instance==null)
            {
                var gearScenario=HighLogic.CurrentGame.AddProtoScenarioModule(typeof(RingworldScenario),GameScenes.FLIGHT,GameScenes.SPACECENTER,GameScenes.TRACKSTATION);
                AccessTools.Method(typeof(ScenarioRunner),"LoadModules",new[]{typeof(ProtoScenarioModule)}).Invoke(ScenarioRunner.Instance,new object[]{gearScenario});
            }
            while(RingworldScenario.Instance==null)yield return null;
            Debug.Log("[RingworldSmoke] SCENARIO READY");
            if(cylaSaveProbe){yield return CylaBlackSkySmoke.RunSave(RingworldFlight.Instance,Fail);running=false;Application.Quit();yield break;}
            // This harness tests an unpowered impact with a damage-immune fixture.
            CheatOptions.NoCrashDamage=true;CheatOptions.UnbreakableJoints=true;
            deadline=Time.realtimeSinceStartup+1200;
            FlightInputHandler.state.mainThrottle=0;v.ctrlState.mainThrottle=0;
            v.ActionGroups.SetGroup(KSPActionGroup.SAS,false);v.ActionGroups.SetGroup(KSPActionGroup.RCS,false);
            foreach(var engine in v.FindPartModulesImplementing<ModuleEngines>())engine.Shutdown();
            // Fixture setup only: establish a spin-matched approach outside the capture shell.
            var flight=RingworldFlight.Instance;
            try
            {
                var mode=HighLogic.CurrentGame.Mode;
                try
                {
                    foreach(var testMode in new[]{Game.Modes.SANDBOX,Game.Modes.CAREER,Game.Modes.SCIENCE_SANDBOX})
                    {
                        HighLogic.CurrentGame.Mode=testMode;
                        if(RingworldFlight.SandboxControls!=(testMode==Game.Modes.SANDBOX))throw new Exception("Sandbox control gate failed");
                    }
                }
                finally{HighLogic.CurrentGame.Mode=mode;}
                var launcher=AccessTools.Field(typeof(RingworldFlight),"toolbar").GetValue(flight);
                if(launcher==null)throw new Exception("Ring toolbar missing");
                var button=AccessTools.Field(typeof(RingToolbar),"button").GetValue(launcher);
                if(button==null)throw new Exception("Stock launcher button missing");
                GameEvents.onHideUI.Fire();if(((RingToolbar)launcher).UiVisible)throw new Exception("F2 hide ignored");
                GameEvents.onShowUI.Fire();if(!((RingToolbar)launcher).UiVisible)throw new Exception("F2 show ignored");
                RingworldSurfaceState outside;
                if(RingworldSurfaceApi.TryGetSurfaceState(v,out outside))throw new Exception("Surface API captured an orbital vessel");
                Debug.Log("[RingworldSmoke] TOOLBAR stock button present; Sandbox/Career/Science and F2 gates passed; API rejects orbital vessel");
            }
            catch(Exception ex){Fail("Toolbar/API: "+ex);yield break;}
            var smokeOptions=RingworldScenario.Instance.GetOptions().CreateCopy();smokeOptions.SetValue("seed",-739779896,true);RingQualityPresets.Apply(smokeOptions,6);
            flight.ApplyOptions(smokeOptions,true);
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-multi-ring-only")>=0){yield return MultiRingSmoke.Run(flight,Fail);running=false;Application.Quit();yield break;}
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-integrations-only")>=0){yield return IntegrationSmoke.Run(flight,Fail);running=false;Application.Quit();yield break;}
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-science-only")>=0){yield return ResearchSmoke.Run(flight,Fail);running=false;Application.Quit();yield break;}
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-wall-only")>=0){yield return WallSmoke.Run(flight,Fail);running=false;Application.Quit();yield break;}
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-visual-options-only")>=0){yield return VisualOptionsSmoke.Run(flight,Fail);running=false;Application.Quit();yield break;}
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-reentry-only")>=0)
            {
                yield return ReentrySmoke.Run(flight,Fail);running=false;Application.Quit();yield break;
            }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-tracking-only")>=0)
            {
                yield return TrackingSmoke.Run(flight,Fail);running=false;Application.Quit();yield break;
            }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-gear-only")>=0)
            {
                yield return GearSmoke.Run(flight,Fail);
                if(running)Debug.Log("[RingworldSmoke] PASS gear-only");
                if(running&&Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-guidance-only")>=0)
                {yield return GuidanceSmoke.Run(RingworldFlight.Instance,Fail);if(running)Debug.Log("[RingworldSmoke] PASS guidance-only");}
                if(running&&Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-landmarks-only")>=0)
                {CheatOptions.NoCrashDamage=true;CheatOptions.UnbreakableJoints=true;yield return LandmarkSmoke.Run(RingworldFlight.Instance,Fail);if(running)Debug.Log("[RingworldSmoke] PASS landmarks-only");}
                if(running){Debug.Log("[RingworldSmoke] PASS combined gear regression");running=false;Application.Quit();}yield break;
            }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-cyla-diagnostic")>=0){yield return CylaBlackSkySmoke.Run(flight,Fail);running=false;Application.Quit();yield break;}
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-cyla-only")>=0){yield return CylaSmoke.Run(flight,Fail);if(running){Debug.Log("[RingworldSmoke] PASS cyla-only");running=false;Application.Quit();}yield break;}
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-landmarks-only")>=0&&Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-guidance-only")>=0)
            {
                yield return GuidanceSmoke.Run(flight,Fail);
                if(running){Debug.Log("[RingworldSmoke] PASS guidance-only");yield return LandmarkSmoke.Run(RingworldFlight.Instance,Fail);}
                if(running)Debug.Log("[RingworldSmoke] PASS landmarks-only");
                running=false;Application.Quit();yield break;
            }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-landmarks-only")>=0){yield return LandmarkSmoke.Run(flight,Fail);if(running){Debug.Log("[RingworldSmoke] PASS landmarks-only");running=false;Application.Quit();}yield break;}
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-stability-only")>=0){yield return StabilitySmoke.Run(flight,Fail);if(running){Debug.Log("[RingworldSmoke] PASS stability-only");running=false;Application.Quit();}yield break;}
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-guidance-only")>=0){yield return GuidanceSmoke.Run(flight,Fail);if(running){Debug.Log("[RingworldSmoke] PASS guidance-only");running=false;Application.Quit();}yield break;}
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-residence-only")>=0){yield return ResidenceSmoke.Run(flight,Fail);if(running){Debug.Log("[RingworldSmoke] PASS residence-only");running=false;Application.Quit();}yield break;}
            int worldSeed=flight.Settings.Geometry.P.Seed;
            var optionCopy=flight.Settings.Save();var optionCheck=Settings.Load();optionCheck.Apply(optionCopy);
            if(optionCheck.Geometry.P.Seed!=worldSeed||optionCheck.LodRange!=160000000||optionCheck.LodResolution!=8){Fail("Save settings roundtrip failed");yield break;}
            var advanced=optionCopy.CreateCopy();advanced.SetValue("radius",2000000000.0);advanced.SetValue("width",30000000.0);advanced.SetValue("lodRange",2000000000.0);advanced.SetValue("daySeconds",60);advanced.SetValue("surfaceDensity",2500000.0);
            optionCheck.Apply(advanced);
            if(optionCheck.Geometry.P.Radius!=2000000000||optionCheck.Geometry.P.Width!=30000000||optionCheck.LodRange!=2000000000||optionCheck.Geometry.P.DaySeconds!=60||optionCheck.Geometry.P.SurfaceDensity!=2500000){Fail("Advanced dimensions/options failed");yield break;}
            optionCheck.Apply(optionCopy);
            Debug.Log("[RingworldSmoke] OPTIONS seed="+worldSeed+" range="+optionCheck.LodRange+" quality="+optionCheck.LodResolution);
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-photo-only")>=0)
            {
                var visualOptions=flight.Settings.Save();visualOptions.SetValue("seed",1970);visualOptions.SetValue("fullRingDetail",true,true);visualOptions.SetValue("cloudAmount",.75);visualOptions.SetValue("dynamicWeather",false);visualOptions.SetValue("visualQuality",1);visualOptions.SetValue("waterQuality",2);visualOptions.SetValue("cloudSteps",64);visualOptions.SetValue("atmosphereSteps",32);
                flight.ApplyOptions(visualOptions,true);AccessTools.Field(typeof(RingworldFlight),"destination").SetValue(flight,5);flight.arrivalHeight=500;flight.Visit();while(!flight.Ready)yield return null;
                var viewCoord=flight.Settings.Geometry.Coordinates(flight.Position(v));double photoTime=Planetarium.GetUniversalTime();double photoPhase=RingGeometry.Wrap(20*viewCoord.Along/flight.Settings.Geometry.P.Circumference-photoTime/flight.Settings.Geometry.P.DaySeconds,1);Planetarium.SetUniversalTime(photoTime+(photoPhase+.5)*flight.Settings.Geometry.P.DaySeconds);
                FlightCamera.fetch.SetDistanceImmediate(30);FlightCamera.CamPitch=.05f;
                yield return new WaitForSecondsRealtime(3);
                foreach(var cam in FlightCamera.fetch.cameras)Debug.Log("[RingworldSmoke] VISUAL CAMERA "+cam.name+" active="+cam.isActiveAndEnabled+" depth="+cam.depth+" far="+cam.farClipPlane+" path="+cam.actualRenderingPath);
                if(flight.visuals==null||!flight.visuals.Rendering||flight.visuals.RenderedFrames<2){Fail("GPU high renderer did not run: "+(flight.visuals==null?"missing":flight.visuals.Status));yield break;}
                bool waterReady=false;foreach(var mr in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())if(mr.gameObject.name=="Water"&&mr.sharedMaterial.shader.name=="NivenRingworld/Waves")waterReady=true;
                if(!waterReady){Fail("Wave water shader not active");yield break;}
                var saveRoundtrip=Settings.Load();saveRoundtrip.Apply(flight.Settings.Save());if(saveRoundtrip.VisualQuality!=1||saveRoundtrip.WaterQuality!=2||saveRoundtrip.CloudSteps!=64||!saveRoundtrip.FullRingDetail){Fail("Visual settings roundtrip failed");yield break;}
                var globalRing=UnityEngine.Object.FindObjectOfType<ScaledRing>();
                if(globalRing==null||!globalRing.DetailActive){Fail("Full-ring surface shader not active");yield break;}
                var globalMesh=GameObject.Find("Niven Ringworld scaled habitat").GetComponent<MeshFilter>().sharedMesh;
                if(globalMesh.subMeshCount!=2||globalMesh.uv.Length!=globalMesh.vertexCount){Fail("Full-ring floor UV/submesh layout failed");yield break;}
                Debug.Log("[RingworldSmoke] FULL RING detail=True vertices="+globalMesh.vertexCount+" submeshes="+globalMesh.subMeshCount);
                ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldHighVisuals.png"));yield return new WaitForSecondsRealtime(1);
                flight.Settings.VisualQuality=2;yield return new WaitForSecondsRealtime(1);Debug.Log("[RingworldSmoke] ULTRA frames="+flight.visuals.RenderedFrames+" status="+flight.visuals.Status);
                // Same clear daytime view in every quality tier; cloud opacity must not hide the ring.
                flight.Settings.CloudAmount=0;flight.visuals.InvalidateWeather();KSP.UI.UIMasterController.Instance.HideUI();AccessTools.Field(typeof(RingworldFlight),"visible").SetValue(flight,false);
                for(int skyQuality=0;skyQuality<=2;skyQuality++)
                {
                    flight.Settings.VisualQuality=skyQuality;yield return new WaitForSecondsRealtime(.5f);
                    ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldClear-"+skyQuality+".png"));yield return new WaitForSecondsRealtime(.5f);
                    Debug.Log("[RingworldSmoke] CLEAR SKY quality="+skyQuality+" clouds="+flight.Settings.CloudAmount+" light="+flight.Settings.Geometry.Daylight(flight.Settings.Geometry.Coordinates(flight.Position(v)).Along,Planetarium.GetUniversalTime()));
                }
                // A diagnostic scaled-space camera shows the macro ocean and surface layer clearly.
                // It renders to a file only; it never changes the playable camera or world geometry.
                var diagnostic=new GameObject("Ringworld far surface diagnostic").AddComponent<Camera>();diagnostic.enabled=false;
                var ribbon=GameObject.Find("Niven Ringworld scaled habitat").transform;
                double diagnosticAlong=flight.Settings.Geometry.P.Circumference*.30;
                var diagnosticGeometry=new RingGeometry(flight.Settings.Geometry.P);
                var eye=ConvertVector.Unity(diagnosticGeometry.Position(diagnosticAlong,0,flight.Settings.Geometry.P.Width*.8)*ScaledSpace.InverseScaleFactor);
                var aim=ConvertVector.Unity(diagnosticGeometry.Position(diagnosticAlong,0,0)*ScaledSpace.InverseScaleFactor);
                diagnostic.transform.position=ribbon.TransformPoint(eye);diagnostic.transform.LookAt(ribbon.TransformPoint(aim),ribbon.TransformDirection(Vector3.up));
                diagnostic.cullingMask=1<<10;diagnostic.nearClipPlane=1;diagnostic.farClipPlane=(float)(flight.Settings.Geometry.P.Radius*3*ScaledSpace.InverseScaleFactor);diagnostic.fieldOfView=70;
                diagnostic.clearFlags=CameraClearFlags.SolidColor;diagnostic.backgroundColor=Color.black;
                var farTarget=new RenderTexture(1280,720,24);diagnostic.targetTexture=farTarget;diagnostic.Render();
                var previousTarget=RenderTexture.active;RenderTexture.active=farTarget;var farImage=new Texture2D(1280,720,TextureFormat.RGB24,false);farImage.ReadPixels(new Rect(0,0,1280,720),0,0);farImage.Apply();RenderTexture.active=previousTarget;
                File.WriteAllBytes(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldDistantSurface.png"),farImage.EncodeToPNG());
                int oceanPixels=0;foreach(var pixel in farImage.GetPixels32())if(pixel.b>pixel.r*1.8&&pixel.g>pixel.r*1.3&&pixel.b>30)oceanPixels++;
                Debug.Log("[RingworldSmoke] DISTANT OCEAN pixels="+oceanPixels);
                if(oceanPixels<10000){Fail("Distant Great Ocean colour absent from diagnostic view");yield break;}
                Destroy(farImage);diagnostic.targetTexture=null;farTarget.Release();Destroy(farTarget);Destroy(diagnostic.gameObject);
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-distant-only")>=0)
                {Debug.Log("[RingworldSmoke] PASS distant-only");running=false;Application.Quit();yield break;}
                flight.Settings.FullRingDetail=false;yield return null;yield return null;
                if(globalRing.DetailActive){Fail("Full-ring detail switch off failed");yield break;}
                flight.Settings.CloudAmount=.45;flight.visuals.InvalidateWeather();KSP.UI.UIMasterController.Instance.ShowUI();
                flight.Settings.VisualQuality=0;flight.Settings.WaterQuality=0;int originalLod=flight.Settings.LodResolution,originalBudget=flight.Settings.GenerationBudget,originalFps=Application.targetFrameRate;double frozenUt=Planetarium.GetUniversalTime();var frozenPosition=flight.Position(v);
                if(!flight.visuals.BeginPhoto()){Fail("Photo entry failed: "+flight.visuals.Status);yield break;}
                while(flight.visuals.PhotoActive&&!flight.visuals.PhotoFinished)yield return null;
                if(!flight.visuals.PhotoFinished||string.IsNullOrEmpty(flight.visuals.LastPhoto)||!File.Exists(flight.visuals.LastPhoto)){Fail("Photo did not finish");yield break;}
                double photoUtDrift=Planetarium.GetUniversalTime()-frozenUt,photoPositionDrift=(flight.Position(v)-frozenPosition).Length;
                Debug.Log("[RingworldSmoke] PHOTO COMPLETE utDrift="+photoUtDrift+" positionDrift="+photoPositionDrift+" lod="+flight.Settings.LodResolution+" pending="+flight.LodPending+" path="+flight.visuals.LastPhoto);
                File.Copy(flight.visuals.LastPhoto,Path.Combine(KSPUtil.ApplicationRootPath,"RingworldPhoto.png"),true);
                if(!globalRing.DetailActive||Math.Abs(photoUtDrift)>.021||photoPositionDrift>.05||flight.Settings.LodResolution!=32||flight.LodPending!=0){Fail("Photo freeze or LOD readiness failed");yield break;}
                flight.visuals.EndPhoto();yield return null;
                if(globalRing.DetailActive||flight.Settings.FullRingDetail||Time.timeScale!=1||Application.targetFrameRate!=originalFps||flight.Settings.LodResolution!=originalLod||flight.Settings.GenerationBudget!=originalBudget||flight.Settings.VisualQuality!=0||flight.Settings.WaterQuality!=0){Fail("Photo restoration failed");yield break;}
                if(!flight.visuals.BeginPhoto(2)){Fail("Second photo entry failed");yield break;}
                yield return null;flight.visuals.EndPhoto();yield return null;
                if(Time.timeScale!=1||flight.visuals.PhotoActive||flight.Settings.LodResolution!=originalLod||InputLockManager.GetControlLock("NivenRingworld.Photo")!=ControlTypes.None||!KSP.UI.UIMasterController.Instance.IsUIShowing){Fail("Photo cancellation did not restore flight");yield break;}
                Debug.Log("[RingworldSmoke] PHOTO RESTORE quality="+flight.Settings.VisualQuality+" water="+flight.Settings.WaterQuality+" lod="+flight.Settings.LodResolution+" timeScale="+Time.timeScale);
                Debug.Log("[RingworldSmoke] PASS photo-only");running=false;Application.Quit();yield break;
            }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-unmatched-only")>=0)
            {
                CheatOptions.NoCrashDamage=false;CheatOptions.UnbreakableJoints=false;CheatOptions.IgnoreMaxTemperature=false;
                flight.arrivalHeight=61000;flight.Visit();while(!flight.Ready||v.packed)yield return null;
                var entryPosition=flight.Position(v);var entryUp=flight.Settings.Geometry.Up(entryPosition);var spin=flight.Settings.Geometry.SpinVelocity(entryPosition);
                double solarSpeed=Math.Sqrt(flight.Star.gravParameter/entryPosition.Length);
                var approach=spin*(solarSpeed/spin.Length-1)-entryUp*1000;
                v.SetWorldVelocity(ConvertVector.Ksp(approach));FlightCamera.fetch.SetDistanceImmediate(45);FlightCamera.CamPitch=.25f;
                int initialParts=v.parts.Count;double entryStart=Planetarium.GetUniversalTime(),firstAir=-1,firstLoss=-1,maxTemperature=0,maxPressure=0;bool sideways=false,captured=false;
                float entryDeadline=Time.realtimeSinceStartup+90;double lastLog=-1;
                Debug.Log("[RingworldSmoke] UNMATCHED SETUP parts="+initialParts+" spin="+spin.Length+" solarSpeed="+solarSpeed+" airRelative="+approach.Length+" crashCheat="+CheatOptions.NoCrashDamage+" heatCheat="+CheatOptions.IgnoreMaxTemperature+" jointCheat="+CheatOptions.UnbreakableJoints);
                while(v!=null&&v.parts.Count>0&&Time.realtimeSinceStartup<entryDeadline)
                {
                    double entryElapsed=Planetarium.GetUniversalTime()-entryStart;var coord=flight.Settings.Geometry.Coordinates(flight.Position(v));var air=RingAir.Sample(v);var velocity=flight.Velocity(v);
                    if(air.Density>0&&firstAir<0)firstAir=entryElapsed;
                    foreach(var part in v.parts)maxTemperature=Math.Max(maxTemperature,Math.Max(part.temperature,part.skinTemperature));
                    maxPressure=Math.Max(maxPressure,v.dynamicPressurekPa);
                    if(v.parts.Count<initialParts&&firstLoss<0)firstLoss=entryElapsed;
                    var fx=UnityEngine.Object.FindObjectOfType<AerodynamicsFX>();
                    double lateral=0,dot=0;if(fx!=null&&fx.velocity.sqrMagnitude>0){dot=Vector3.Dot(fx.velocity.normalized,ConvertVector.Unity(velocity).normalized);lateral=Math.Sqrt(Math.Max(0,1-Math.Pow(Vector3.Dot(fx.velocity.normalized,ConvertVector.Unity(flight.Settings.Geometry.Up(flight.Position(v)))),2)));if(air.Density>0&&dot>.99&&lateral>.99)sideways=true;}
                    if(entryElapsed-lastLog>=.1||v.parts.Count<initialParts){Debug.Log("[RingworldSmoke] UNMATCHED SAMPLE t="+entryElapsed+" altitude="+coord.Altitude+" density="+air.Density+" speed="+velocity.Length+" parts="+v.parts.Count+" maxTemp="+maxTemperature+" externalTemp="+v.externalTemperature+" pressureKPa="+v.dynamicPressurekPa+" fxDot="+dot+" fxLateral="+lateral);lastLog=entryElapsed;}
                    if(sideways&&!captured){ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldUnmatchedEntry.png"));captured=true;}
                    if(firstLoss>=0&&entryElapsed-firstLoss>3)break;
                    if(entryElapsed>35)break;
                    yield return null;
                }
                int remaining=v==null?0:v.parts.Count;if(remaining<initialParts&&firstLoss<0)firstLoss=Planetarium.GetUniversalTime()-entryStart;
                Debug.Log("[RingworldSmoke] UNMATCHED RESULT firstAir="+firstAir+" firstLoss="+firstLoss+" remaining="+remaining+" initial="+initialParts+" maxTemp="+maxTemperature+" maxPressureKPa="+maxPressure+" sidewaysObserved="+sideways);
                if(firstAir<0||firstLoss<0){Fail("Unmatched entry did not reach air and break up within the observation window");yield break;}
                yield return new WaitForSecondsRealtime(1);Debug.Log("[RingworldSmoke] PASS unmatched-only");running=false;Application.Quit();yield break;
            }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-terrain-only")>=0)
            {
                var laptop=flight.Settings.Save();worldSeed=1835517880;laptop.SetValue("seed",worldSeed);laptop.SetValue("lodResolution",8);laptop.SetValue("generationBudget",1);laptop.SetValue("detailDistance",75);
                flight.ApplyOptions(laptop,true);
                flight.arrivalHeight=60;if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-weather-only")>=0)flight.Visit();else flight.VisitRandomTerrain(812794611);
                while(!flight.Ready||v.mainBody!=flight.Star)yield return null;
                int visibleChunks=0;foreach(var mf in UnityEngine.Object.FindObjectsOfType<MeshFilter>())if(mf.sharedMesh!=null&&mf.sharedMesh.name=="Adaptive ring terrain block"&&mf.gameObject.activeInHierarchy)visibleChunks++;
                Debug.Log("[RingworldSmoke] PROGRESSIVE LOD visible="+visibleChunks+" pending="+flight.LodPending);
                if(visibleChunks==0||flight.LodPending==0){Fail("Progressive LOD publication was not observed");yield break;}
                double matchedSpeed=flight.Velocity(v).Length;
                Debug.Log("[RingworldSmoke] RANDOM ARRIVAL speed="+matchedSpeed+" seed="+flight.Settings.Geometry.P.Seed);
                if(matchedSpeed>3||flight.Settings.Geometry.P.Seed!=worldSeed){Fail("Random visit did not match surface velocity or changed seed");yield break;}
                FlightCamera.fetch.SetDistanceImmediate(15);FlightCamera.CamPitch=.15f;
                yield return new WaitForSecondsRealtime(20);
                float restDeadline=Time.realtimeSinceStartup+60;
                while(!flight.surfaceWarp.CanAdvance(flight)&&Time.realtimeSinceStartup<restDeadline)yield return null;
                Debug.Log("[RingworldSmoke] RANDOM CONTACT status="+flight.surfaceWarp.Status+" speed="+flight.Velocity(v).Length+" clearance="+flight.SurfaceClearance(v));
                if(!flight.surfaceWarp.CanAdvance(flight)){ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldRestFailure.png"));yield return new WaitForSecondsRealtime(1);Fail("Random crashed craft cannot settle: "+flight.surfaceWarp.Status);yield break;}
                RingworldSurfaceState surfaceState;
                if(!RingworldSurfaceApi.TryGetSurfaceState(v,out surfaceState)||surfaceState.SurfaceRelativeVelocity.magnitude>.1||Math.Abs(surfaceState.SurfaceUp.magnitude-1)>.00001||surfaceState.TangentialSpeed<100000)
                {Fail("Surface API did not report a resting ring-relative state");yield break;}
                Debug.Log("[RingworldSmoke] SURFACE API speed="+surfaceState.SurfaceRelativeVelocity.magnitude+" tangential="+surfaceState.TangentialSpeed+" biome="+surfaceState.Biome);
                var nativeWarpPosition=flight.Position(v);var nativeHome=FlightGlobals.GetHomeBody().position-flight.Star.position;double nativeStart=Planetarium.GetUniversalTime();
                flight.Settings.SurfaceWarpLimit=1000;
                AccessTools.Method(typeof(TimeWarp),"btnSetHighRate").Invoke(TimeWarp.fetch,new object[]{5});
                float warpDeadline=Time.realtimeSinceStartup+20;
                while((!v.packed||TimeWarp.CurrentRateIndex!=5)&&Time.realtimeSinceStartup<warpDeadline)yield return null;
                if(!v.packed||TimeWarp.CurrentRateIndex!=5){Fail("Native warp UI rate did not engage: index="+TimeWarp.CurrentRateIndex+" packed="+v.packed+" status="+flight.surfaceWarp.Status);yield break;}
                yield return new WaitForEndOfFrame();
                var habitatLight=GameObject.Find("Ringworld habitat sunlight").GetComponent<Light>();var warpCoord=flight.Settings.Geometry.Coordinates(flight.Position(v));
                double expectedLight=flight.Settings.Geometry.Daylight(warpCoord.Along,Planetarium.GetUniversalTime())*.5;
                Debug.Log("[RingworldSmoke] PACKED LIGHT actual="+habitatLight.intensity+" expected="+expectedLight);
                if(Math.Abs(habitatLight.intensity-expectedLight)>.001){Fail("Lighting did not update during native warp");yield break;}
                AccessTools.Method(typeof(TimeWarp),"btnSetHighRate").Invoke(TimeWarp.fetch,new object[]{0});while(v.packed)yield return null;yield return new WaitForSecondsRealtime(1);
                double nativeMotion=((FlightGlobals.GetHomeBody().position-flight.Star.position)-nativeHome).magnitude;
                Debug.Log("[RingworldSmoke] NATIVE GLOBAL WARP seconds="+(Planetarium.GetUniversalTime()-nativeStart)+" planetMotion="+nativeMotion+" drift="+(flight.Position(v)-nativeWarpPosition).Length);
                if(nativeMotion<1000||(flight.Position(v)-nativeWarpPosition).Length>1){Fail("Native warp planet advance or anchor drift failed");yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-weather-only")>=0)
                {
                    yield return WeatherChecks(flight,v);if(!running)yield break;
                    Debug.Log("[RingworldSmoke] PASS weather-only");running=false;Application.Quit();yield break;
                }
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-warp-only")>=0){Debug.Log("[RingworldSmoke] PASS stock-warp-only");running=false;Application.Quit();yield break;}
                // Fixture-only daylight jump for inspection. Ordinary random visits preserve time.
                var visualCoord=flight.Settings.Geometry.Coordinates(flight.Position(v));double visualTime=Planetarium.GetUniversalTime();
                double visualPhase=RingGeometry.Wrap(20*visualCoord.Along/flight.Settings.Geometry.P.Circumference-visualTime/flight.Settings.Geometry.P.DaySeconds,1);
                Planetarium.SetUniversalTime(visualTime+(visualPhase+.5)*flight.Settings.Geometry.P.DaySeconds);
                yield return new WaitForSecondsRealtime(3);
                var frames=new System.Collections.Generic.List<float>();float end=Time.realtimeSinceStartup+20;
                while(Time.realtimeSinceStartup<end){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}
                frames.Sort();double sum=0;foreach(float ms in frames)sum+=ms;
                Debug.Log("[RingworldSmoke] LAPTOP resolution="+Screen.width+"x"+Screen.height+" "+StockGraphics.Description+" details="+flight.groundDetails.Count+" frames="+frames.Count+" meanMs="+(sum/frames.Count)+" p95Ms="+frames[(int)(frames.Count*.95)]+" pendingLod="+flight.LodPending);
                if(flight.groundDetails.Count==0){Fail("Random terrain has no ground details");yield break;}
                AccessTools.Field(typeof(RingworldFlight),"visible").SetValue(flight,false);KSP.UI.UIMasterController.Instance.HideUI();
                FlightCamera.fetch.SetDistanceImmediate(30);FlightCamera.CamPitch=.35f;
                yield return new WaitForSecondsRealtime(1);
                ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldRandomTerrain.png"));
                yield return new WaitForSecondsRealtime(1);
                foreach(float zoom in new[]{5f,30f,150f,1000f})
                {
                    FlightCamera.fetch.SetDistanceImmediate(zoom);FlightCamera.CamPitch=-.15f;
                    yield return new WaitForSecondsRealtime(1);
                    ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldZoom-"+zoom+".png"));yield return new WaitForSecondsRealtime(.5f);
                }
                var coarse=GameObject.Find("Niven Ringworld scaled habitat").GetComponent<MeshFilter>();var cv=coarse.sharedMesh.vertices;double maxCoarseAltitude=double.NegativeInfinity;
                for(int index=3;index+8<cv.Length;index+=8*17)
                {
                    var vertex=coarse.transform.TransformPoint((cv[index]+cv[index+8])*.5f);
                    var world=ScaledSpace.ScaledToLocalSpace(vertex)-flight.Star.position;
                    maxCoarseAltitude=Math.Max(maxCoarseAltitude,flight.Settings.Geometry.Coordinates(ConvertVector.Core(world)).Altitude);
                }
                Debug.Log("[RingworldSmoke] COARSE FLOOR maximumAltitude="+maxCoarseAltitude);
                if(maxCoarseAltitude>TerrainGenerator.MinimumHeight){Fail("Coarse fallback intrudes through terrain");yield break;}
                var randomBefore=flight.Position(v);flight.arrivalHeight=300;flight.VisitRandomTerrain();
                while(!flight.Ready)yield return null;
                double distance=(flight.Position(v)-randomBefore).Length;
                Debug.Log("[RingworldSmoke] RANDOM REVISIT distance="+distance+" speed="+flight.Velocity(v).Length);
                if(distance<1000||flight.Velocity(v).Length>3){Fail("Repeated random relocation failed");yield break;}
                flight.arrivalHeight=30000;flight.Visit();while(!flight.Ready)yield return null;
                v.SetWorldVelocity(ConvertVector.Ksp(flight.Settings.Geometry.Up(flight.Position(v))*-1800));yield return new WaitForSecondsRealtime(2);
                var flame=UnityEngine.Object.FindObjectOfType<AerodynamicsFX>();var flameVelocity=flight.Velocity(v);
                Debug.Log("[RingworldSmoke] FLAMES speed="+flame.airSpeed+" actual="+flameVelocity.Length+" directionDot="+Vector3.Dot(flame.velocity.normalized,ConvertVector.Unity(flameVelocity).normalized));
                if(Math.Abs(flame.airSpeed-flameVelocity.Length)>30||Vector3.Dot(flame.velocity.normalized,ConvertVector.Unity(flameVelocity).normalized)<.99){Fail("High-speed flame direction/speed incorrect");yield break;}
                FlightCamera.fetch.SetDistanceImmediate(35);FlightCamera.CamPitch=.3f;ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldFlames.png"));yield return new WaitForSecondsRealtime(1);
                AccessTools.Field(typeof(RingworldFlight),"destination").SetValue(flight,8);flight.arrivalHeight=300;flight.Visit();
                while(!flight.Ready)yield return null;
                FlightCamera.fetch.SetDistanceImmediate(150);FlightCamera.CamPitch=-.15f;yield return new WaitForSecondsRealtime(3);
                bool wallFound=false;foreach(var filter in UnityEngine.Object.FindObjectsOfType<MeshFilter>())if(filter.sharedMesh!=null&&filter.sharedMesh.name=="Solid atmosphere retaining rim wall")
                {wallFound=true;Debug.Log("[RingworldSmoke] WALL vertices="+filter.sharedMesh.vertexCount);if(filter.sharedMesh.vertexCount!=24){Fail("Wall thickness mesh missing");yield break;}break;}
                if(!wallFound){Fail("No wall collision mesh at rim terminal");yield break;}
                ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldSolidWall.png"));yield return new WaitForSecondsRealtime(1);
                // Inspection camera frames actual near-wall top/outer geometry; no substitute mesh.
                var rim=flight.Settings.Terrain.Landmarks[8];double edge=flight.Settings.Geometry.P.Width/2,top=flight.Settings.Geometry.P.WallHeight;
                double rimTime=Planetarium.GetUniversalTime(),rimPhase=RingGeometry.Wrap(20*rim.Along/flight.Settings.Geometry.P.Circumference-rimTime/flight.Settings.Geometry.P.DaySeconds,1);
                Planetarium.SetUniversalTime(rimTime+(rimPhase+.5)*flight.Settings.Geometry.P.DaySeconds);yield return new WaitForSecondsRealtime(1);
                double maxRimFloor=double.NegativeInfinity,maxRimAcross=0;
                foreach(var mf in UnityEngine.Object.FindObjectsOfType<MeshFilter>())if(mf.sharedMesh!=null&&mf.sharedMesh.name=="Ringworld ground")
                foreach(var vertex in mf.sharedMesh.vertices)
                {
                    var point=flight.Settings.Geometry.Coordinates(ConvertVector.Core((Vector3d)mf.transform.TransformPoint(vertex)-flight.Star.position));
                    maxRimFloor=Math.Max(maxRimFloor,point.Altitude);maxRimAcross=Math.Max(maxRimAcross,Math.Abs(point.Across));
                }
                Debug.Log("[RingworldSmoke] RIM BOUNDARY maxFloor="+maxRimFloor+" maxAcross="+maxRimAcross+" halfWidth="+edge);
                if(maxRimFloor>1000||maxRimAcross>edge+.2){Fail("Phantom rim plateau or out-of-bounds terrain remains");yield break;}
                var eye=flight.Star.position+ConvertVector.Ksp(flight.Settings.Geometry.Position(rim.Along,edge-1500,top+1500));
                var look=flight.Star.position+ConvertVector.Ksp(flight.Settings.Geometry.Position(rim.Along+500,edge+50,top-500));
                var inspection=new GameObject("Wall inspection camera");var inspectionCamera=inspection.AddComponent<Camera>();inspectionCamera.cullingMask=1<<15;inspectionCamera.depth=100;inspectionCamera.farClipPlane=1000000;inspectionCamera.clearFlags=CameraClearFlags.SolidColor;inspectionCamera.backgroundColor=new Color(.08f,.12f,.18f);
                inspection.transform.position=(Vector3)eye;inspection.transform.rotation=Quaternion.LookRotation((Vector3)(look-eye),ConvertVector.Unity(flight.Settings.Geometry.Up(ConvertVector.Core(eye-flight.Star.position))));
                var fill=inspection.AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.8f;fill.cullingMask=1<<15;
                yield return new WaitForSecondsRealtime(1);ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldWallInspection.png"));yield return new WaitForSecondsRealtime(1);UnityEngine.Object.Destroy(inspection);
                MapView.EnterMapView();yield return new WaitForSecondsRealtime(3);PlanetariumCamera.fetch.SetDistance(250);yield return new WaitForSecondsRealtime(2);
                ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldMapHull.png"));yield return new WaitForSecondsRealtime(1);
                Debug.Log("[RingworldSmoke] PASS terrain-only");running=false;Application.Quit();yield break;
            }
            // Isolated fixture: exercise packed solar propagation well outside the
            // ribbon before starting the ordinary arrival/landing regression.
            FlightGlobals.fetch.SetShipOrbit(flight.Star.flightGlobalsIndex,0,flight.Settings.Geometry.P.Radius+2000000,0,0,0,0,Planetarium.GetUniversalTime());
            while(v.mainBody!=flight.Star||v.packed)yield return null;
            yield return new WaitForSeconds(2);
            TimeWarp.SetRate(3,true);yield return new WaitForSecondsRealtime(2);
            if(!v.packed){Fail("Packed orbit fixture did not enter rails warp");yield break;}
            double coastStart=Planetarium.GetUniversalTime();
            var coastState=new Ringworld.Core.FlightState(ConvertVector.Core(ConvertVector.Orbit(v.orbit.getRelativePositionAtUT(coastStart))),ConvertVector.Core(ConvertVector.Orbit(v.orbit.getOrbitalVelocityAtUT(coastStart))));
            yield return new WaitForSecondsRealtime(8);
            double coastDuration=Planetarium.GetUniversalTime()-coastStart;
            var actualCoast=ConvertVector.Core(ConvertVector.Orbit(v.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime())));
            TimeWarp.SetRate(0,true);
            double left=coastDuration;
            while(left>1e-6){double dt=Math.Min(.5,left);coastState=NumericalFlight.Step(coastState,dt,(pos,vel)=>RingTrajectory.InertialAcceleration(pos,flight.Settings,flight.Star.gravParameter));left-=dt;}
            double coastError=(coastState.Position-actualCoast).Length;
            Debug.Log("[RingworldSmoke] PACKED COAST seconds="+coastDuration+" positionError="+coastError);
            if(coastDuration<30||coastError>20){Fail("Packed coast differs from fine-step numerical reference");yield break;}
            while(v.packed)yield return null;
            flight.arrivalHeight=400000;flight.Visit();while(!flight.Ready)yield return null;
            v.SetWorldVelocity(ConvertVector.Ksp(flight.Settings.Geometry.Up(flight.Position(v))*-1000));flight.Leave();
            MapView.EnterMapView();yield return new WaitForSeconds(1);PlanetariumCamera.fetch.SetDistance(25000);yield return new WaitForSeconds(8);
            if(flight.trajectory.EncounterCount<1){Fail("Ring entry marker missing");yield break;}
            int hiddenOrbitLines=0;var orbitGetter=AccessTools.PropertyGetter(typeof(OrbitRendererBase),"orbit");
            foreach(var renderer in UnityEngine.Object.FindObjectsOfType<OrbitRendererBase>())
                if(ReferenceEquals(orbitGetter.Invoke(renderer,null),flight.trajectory.PredictedOrbit)&&renderer.OrbitLine!=null)
                {if(renderer.OrbitLine.active){Fail("Replaced solar orbit mesh remains visible");yield break;}hiddenOrbitLines++;}
            Debug.Log("[RingworldSmoke] ENCOUNTER count="+flight.trajectory.EncounterCount+" hiddenOrbitLines="+hiddenOrbitLines);
            ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldEncounter.png"));yield return new WaitForSeconds(1);MapView.ExitMapView();
            flight.arrivalHeight=230000;flight.Visit();
            while(!flight.Ready||v.mainBody!=flight.Star)yield return null;
            var geom=flight.Settings.Geometry;
            MapView.EnterMapView();
            yield return new WaitForSeconds(1);PlanetariumCamera.fetch.SetDistance(250);
            yield return new WaitForSeconds(8);
            Debug.Log("[RingworldSmoke] MAP cameraAltitude="+geom.Coordinates(ConvertVector.Core(ScaledSpace.ScaledToLocalSpace(PlanetariumCamera.Camera.transform.position)-flight.Star.position)).Altitude);
            Debug.Log("[RingworldSmoke] MAP points="+flight.trajectory.PointCount+" status="+flight.trajectory.Status);
            if(flight.trajectory.PointCount<10){Fail("Numerical map trajectory did not render");yield break;}
            ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldTrajectory.png"));
            yield return new WaitForSeconds(1);
            int previousBudget=flight.Settings.GenerationBudget;flight.Settings.GenerationBudget=4;
            float mapChunksDeadline=Time.realtimeSinceStartup+60;
            while(flight.BuiltScaledLodCount<4&&Time.realtimeSinceStartup<mapChunksDeadline)yield return new WaitForSecondsRealtime(.25f);
            flight.Settings.GenerationBudget=previousBudget;
            try
            {
                var options=flight.Settings.Save();options.SetValue("lodRange","5000000000000");var roundtrip=Settings.Load();roundtrip.Apply(options);
                if(roundtrip.LodRange!=5000000000000)throw new Exception("Render distance still capped");
                int scaled=0;foreach(var meshFilter in Resources.FindObjectsOfTypeAll<MeshFilter>())
                    if(meshFilter.gameObject.layer==10&&meshFilter.sharedMesh!=null&&meshFilter.sharedMesh.name=="Adaptive ring terrain block")
                    {scaled++;meshFilter.gameObject.SetActive(true);if(meshFilter.sharedMesh.vertexCount!=(flight.Settings.LodResolution+1)*(flight.Settings.LodResolution+1))throw new Exception("Scaled terrain still has deep skirts");}
                if(scaled==0)throw new Exception("No scaled terrain to validate");
                foreach(int side in new[]{-1,1})
                {
                    var target=(Vector3)(flight.Star.position+ConvertVector.Ksp(geom.Position(0,side*(geom.P.Width/2-20),100)));
                    var desired=(Vector3)(flight.Star.position+ConvertVector.Ksp(geom.Position(0,side*(geom.P.Width/2+100),100)));
                    var constrained=flight.ConstrainCamera(desired,target,3);
                    double across=geom.Coordinates(ConvertVector.Core((Vector3d)constrained-flight.Star.position)).Across;
                    if(side*across>geom.P.Width/2+8)throw new Exception("Camera crossed rim wall");
                }
                var testCameraObject=new GameObject("Map depth regression camera");var testCamera=testCameraObject.AddComponent<Camera>();testCamera.enabled=false;testCamera.cullingMask=1<<10;
                testCamera.clearFlags=CameraClearFlags.SolidColor;testCamera.backgroundColor=new Color(.02f,.025f,.04f);testCamera.nearClipPlane=.01f;testCamera.farClipPlane=10000000;
                var render=new RenderTexture(1280,720,24);testCamera.targetTexture=render;
                var location=geom.Coordinates(flight.Position(v));
                foreach(double altitude in new[]{2000000.0,-2000000.0,geom.P.Radius*.5})
                {
                    testCamera.transform.position=(Vector3)ScaledSpace.LocalToScaledSpace(flight.Star.position+ConvertVector.Ksp(geom.Position(location.Along,0,altitude)));
                    testCamera.transform.LookAt((Vector3)ScaledSpace.LocalToScaledSpace(flight.Star.position+ConvertVector.Ksp(geom.Position(location.Along+4000000,0,0))),Vector3.up);
                    testCamera.Render();var old=RenderTexture.active;RenderTexture.active=render;var picture=new Texture2D(1280,720,TextureFormat.RGB24,false);
                    picture.ReadPixels(new Rect(0,0,1280,720),0,0);picture.Apply();RenderTexture.active=old;
                    File.WriteAllBytes(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldMapDepth-"+altitude.ToString("F0")+".png"),picture.EncodeToPNG());UnityEngine.Object.Destroy(picture);
                }
                testCamera.targetTexture=null;render.Release();UnityEngine.Object.Destroy(render);UnityEngine.Object.Destroy(testCameraObject);
                Debug.Log("[RingworldSmoke] MAP DEPTH skirt-free scaled chunks="+scaled+"; rim camera constraints and uncapped setting passed");
            }
            catch(Exception ex){Fail("Map depth/walls: "+ex);yield break;}
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-map-only")>=0)
            {Debug.Log("[RingworldSmoke] PASS map-only");yield return new WaitForSeconds(1);running=false;Application.Quit();yield break;}
            MapView.ExitMapView();
            v.SetWorldVelocity(ConvertVector.Ksp(geom.Up(flight.Position(v))*-2000));
            var departurePos=flight.Position(v);var departureVel=flight.Velocity(v);
            double elapsed=Planetarium.GetUniversalTime()-flight.FrameEpoch;
            var expectedPos=geom.ToInertialPosition(departurePos,elapsed);
            var expectedVel=geom.ToInertialVelocity(departurePos,departureVel,elapsed);
            flight.Leave();
            double dp=(flight.Position(v)-expectedPos).Length,dv=(flight.Velocity(v)-expectedVel).Length;
            Debug.Log("[RingworldSmoke] DEPARTURE positionError="+dp+" velocityError="+dv);
            if(dp>2||dv>1){Fail("Departure did not preserve inertial state");yield break;}
            while(!flight.Owns(v))yield return null;
            double arrivalSpeed=flight.Velocity(v).Length;
            Debug.Log("[RingworldSmoke] AUTO ARRIVAL altitude="+geom.Coordinates(flight.Position(v)).Altitude+" speed="+arrivalSpeed);
            if(arrivalSpeed<1800||arrivalSpeed>2300){Fail("Arrival erased or corrupted relative velocity");yield break;}
            // Sample the real flight integrator inside the atmosphere.
            flight.arrivalHeight=30000;flight.Visit();
            while(!flight.Ready)yield return null;
            v.SetWorldVelocity(ConvertVector.Ksp(geom.Up(flight.Position(v))*-100));
            yield return new WaitForSeconds(2);
            Debug.Log("[RingworldSmoke] AIR density="+v.atmDensity+" pressure="+v.staticPressurekPa+" mach="+v.mach+" q="+v.dynamicPressurekPa);
            if(v.atmDensity<=0||v.staticPressurekPa<=0||v.mach<=0||v.dynamicPressurekPa<=0){Fail("Native air integration missing");yield break;}
            var aeroFx=UnityEngine.Object.FindObjectOfType<AerodynamicsFX>();
            double fxDot=aeroFx==null?-1:Vector3.Dot(aeroFx.velocity.normalized,ConvertVector.Unity(flight.Velocity(v)).normalized);
            Debug.Log("[RingworldSmoke] AERO FX directionDot="+fxDot);
            if(fxDot<.99){Fail("Atmospheric visual direction does not match ring airflow");yield break;}
            FlightCamera.fetch.SetDistanceImmediate(40);FlightCamera.CamPitch=.6f;
            ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldArrival.png"));
            yield return new WaitForSeconds(1);
            flight.arrivalHeight=8000;flight.Visit();
            while(!flight.Ready)yield return null;
            yield return new WaitForSeconds(2);
            ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldClouds.png"));
            yield return new WaitForSeconds(1);
            RingworldFlight.Instance.arrivalHeight=150;
            RingworldFlight.Instance.Visit();
            while(!RingworldFlight.Instance.Ready||v.mainBody!=RingworldFlight.Instance.Star)yield return null;
            FlightCamera.fetch.SetDistanceImmediate(35);FlightCamera.CamPitch=.2f;
            yield return new WaitForSeconds(2);
            var p=flight.Settings.Geometry.Coordinates(flight.Position(v));
            double first=p.Altitude;Debug.Log("[RingworldSmoke] ENTRY h="+first+" v="+v.obt_velocity.magnitude);
            yield return new WaitForSeconds(1);
            p=flight.Settings.Geometry.Coordinates(flight.Position(v));
            Debug.Log("[RingworldSmoke] DROP dh="+(p.Altitude-first)+" speed="+v.obt_velocity.magnitude);
            // A fixture-only descent controller isolates contact stability from a destructive free fall.
            for(int step=0;step<6000;step++)
            {
                bool contact=false;foreach(var part in v.parts)if(part.GroundContact){contact=true;break;}
                if(contact){Debug.Log("[RingworldSmoke] CONTACT");break;}
                v.SetWorldVelocity(ConvertVector.Ksp(flight.Settings.Geometry.Up(flight.Position(v))*-2));
                yield return new WaitForFixedUpdate();
            }
            yield return new WaitForSeconds(12);
            p=flight.Settings.Geometry.Coordinates(flight.Position(v));var terrain=flight.Settings.Terrain.Sample(p.Along,p.Across);
            double agl=p.Altitude-terrain.Height;
            Debug.Log("[RingworldSmoke] SETTLED agl="+agl+" speed="+v.obt_velocity.magnitude+" parts="+v.parts.Count);
            float settleDeadline=Time.realtimeSinceStartup+90,settleReport=0;
            while(!flight.surfaceWarp.CanAdvance(flight)&&Time.realtimeSinceStartup<settleDeadline)
            {
                if(Time.realtimeSinceStartup>=settleReport)
                {
                    settleReport=Time.realtimeSinceStartup+5;
                    Debug.Log("[RingworldSmoke] SETTLING rootAngular="+v.rootPart.rb.angularVelocity.magnitude+" surfaceSpeed="+flight.Velocity(v).Length+" "+flight.surfaceWarp.Status);
                }
                yield return new WaitForSecondsRealtime(.25f);
            }
            double warpStart=Planetarium.GetUniversalTime();var warpPosition=flight.Position(v);var homeBefore=FlightGlobals.GetHomeBody().position-flight.Star.position;
            if(!flight.surfaceWarp.CanAdvance(flight)){Fail("Resting expedition rejected surface warp: "+flight.surfaceWarp.Status+" localSpeed="+flight.Velocity(v).Length);yield break;}
            var poseStates=(System.Collections.Generic.Dictionary<Vessel,RingRestPose>)AccessTools.Field(typeof(RingSurfaceWarp),"restPoses").GetValue(flight.surfaceWarp);
            RingRestPose restPose;if(poseStates.TryGetValue(v,out restPose))Debug.Log("[RingworldSmoke] REST POSE translation="+restPose.PositionError+" angleDegrees="+restPose.AngleError);
            var originalAngular=v.rootPart.rb.angularVelocity;v.rootPart.rb.angularVelocity=Vector3.up*.2f;
            if(flight.surfaceWarp.CanAdvance(flight)){Fail("Rest gate accepted a rotating root");yield break;}
            v.rootPart.rb.angularVelocity=originalAngular;
            flight.surfaceWarp.Rate=1000;
            yield return new WaitForSeconds(2);
            if(!v.packed||TimeWarp.CurrentRate<100){Fail("Stock rails warp did not engage");yield break;}
            flight.surfaceWarp.Rate=1;
            while(v.packed)yield return null;
            double warpElapsed=Planetarium.GetUniversalTime()-warpStart,warpDrift=(flight.Position(v)-warpPosition).Length;
            double homeMotion=((FlightGlobals.GetHomeBody().position-flight.Star.position)-homeBefore).magnitude;
            Debug.Log("[RingworldSmoke] SURFACE WARP elapsed="+warpElapsed+" drift="+warpDrift+" homeBodyMotion="+homeMotion);
            if(homeMotion<1000){Fail("Stock warp failed to advance the home planet");yield break;}
            if(warpElapsed<100||warpDrift>1){Fail("Surface warp did not preserve resting expedition");yield break;}
            v.SetWorldVelocity(ConvertVector.Ksp(geom.Up(flight.Position(v))*.5));
            flight.surfaceWarp.Rate=100;flight.surfaceWarp.Update(flight);
            if(flight.surfaceWarp.Rate!=1){Fail("Surface warp failed motion guard");yield break;}
            v.SetWorldVelocity(Vector3d.zero);yield return new WaitForSeconds(2);
            Debug.Log("[RingworldSmoke] CRASH CAMERA wobble="+RingCameraTerrainPatch.Wobble(FlightCamera.fetch)+" effects="+RingCameraTerrainPatch.Effects());
            if(RingCameraTerrainPatch.Wobble(FlightCamera.fetch)!=0||RingCameraTerrainPatch.Effects()!=0){Fail("Grounded craft camera shake still enabled");yield break;}
            Debug.Log("[RingworldSmoke] FRAME velocity="+Krakensbane.GetFrameVelocity().magnitude);
            var navball=UnityEngine.Object.FindObjectOfType<KSP.UI.Screens.Flight.NavBall>();
            foreach(var mode in new[]{FlightGlobals.SpeedDisplayModes.Orbit,FlightGlobals.SpeedDisplayModes.Surface})
            {
                FlightGlobals.SetSpeedMode(mode);
                yield return null;yield return new WaitForEndOfFrame();
                if(navball==null||navball.progradeVector.gameObject.activeSelf||navball.retrogradeVector.gameObject.activeSelf||navball.normalVector.gameObject.activeSelf||navball.radialOutVector.gameObject.activeSelf)
                {Fail("Stationary navball cues were visible in "+mode);yield break;}
            }
            Debug.Log("[RingworldSmoke] NAVBALL stationary cues hidden in both modes; speed="+KSP.UI.Screens.Flight.SpeedDisplay.Instance.textSpeed.text+" CanEVA="+HighLogic.CurrentGame.Parameters.Flight.CanEVA);
            if(!HighLogic.CurrentGame.Parameters.Flight.CanEVA||FlightCamera.GetAutoModeForVessel(v)!=FlightCamera.Modes.FREE){Fail("Scenario EVA permission or camera surface mode incorrect");yield break;}
            ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldSmoke.png"));
            // ScreenCapture runs at end-of-frame. Do not start scenario restoration
            // (which temporarily returns the camera to the solar frame) in that frame.
            yield return new WaitForSeconds(1);
            flight.Capture();var saved=new ConfigNode("SCENARIO");RingworldScenario.Instance.OnSave(saved);
            Debug.Log("[RingworldSmoke] SAVE vessel records="+saved.GetNodes("VESSEL").Length);
            if(!v.Landed||v.situation!=Vessel.Situations.LANDED||FlightGlobals.ClearToSave(false)!=ClearToSaveStatus.CLEAR){Fail("Ring residence is not saveable as landed");yield break;}
            var stockExperiment=v.rootPart.FindModuleImplementing<ModuleScienceExperiment>();
            if(stockExperiment==null)stockExperiment=(ModuleScienceExperiment)v.rootPart.AddModule("ModuleScienceExperiment");
            AccessTools.Field(typeof(ModuleScienceExperiment),"situation").SetValue(stockExperiment,ScienceUtil.GetExperimentSituation(v));stockExperiment.experimentID="crewReport";stockExperiment.experiment=ResearchAndDevelopment.GetExperiment("crewReport");
            var scienceRoutine=(IEnumerator)AccessTools.Method(typeof(ModuleScienceExperiment),"OnScienceCompleteDelay").Invoke(stockExperiment,null);
            while(scienceRoutine.MoveNext())yield return scienceRoutine.Current;
            var stockSubject=(ScienceSubject)AccessTools.Field(typeof(ModuleScienceExperiment),"subject").GetValue(stockExperiment);
            if(stockSubject==null||!stockSubject.id.Contains("RingworldV2_")||stockSubject.title.Contains("over the Sun")){Fail("Stock instrument did not get ring science subject");yield break;}
            Debug.Log("[RingworldSmoke] STOCK SCIENCE "+stockSubject.id+" title="+stockSubject.title);
            foreach(var dialog in UnityEngine.Object.FindObjectsOfType<KSP.UI.Screens.Flight.Dialogs.ExperimentsResultDialog>())UnityEngine.Object.Destroy(dialog.gameObject);
            saved.Save(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldSmoke-scenario.cfg"));
            if(agl< -10||agl>100||flight.Velocity(v).Length>10){Fail("Surface did not settle");yield break;}
            // Validate KSP's science container and subject registration using a temporary survey module.
            var scanner=(ModuleRingSurvey)v.rootPart.AddModule("ModuleRingSurvey");scanner.Survey();
            Debug.Log("[RingworldSmoke] SCIENCE stored="+scanner.GetScienceCount());
            if(scanner.GetScienceCount()!=1){Fail("Survey was not collected");yield break;}
            var copy=new ConfigNode();scanner.OnSave(copy);scanner.OnLoad(copy);
            if(scanner.GetScienceCount()!=1){Fail("Survey failed persistence roundtrip");yield break;}
            var before=flight.Position(v);
            RingworldScenario.Instance.OnLoad(saved);
            while(!flight.Owns(v))yield return null;
            yield return new WaitForSeconds(3);
            double restoredDrift=(flight.Position(v)-before).Length;
            Debug.Log("[RingworldSmoke] RESTORE displacement="+restoredDrift);
            if(restoredDrift>15){Fail("Saved ring coordinates did not restore");yield break;}
            // Check underside collision and camera recovery from below the actual mesh.
            var centre=geom.Coordinates(flight.Position(v));
            var under=geom.Position(centre.Along+300,centre.Across,flight.Settings.UndersideAltitude-10);
            RaycastHit undersideHit;
            bool underside=Physics.Raycast((Vector3)(flight.Star.position+ConvertVector.Ksp(under)),ConvertVector.Unity(geom.Up(under)),out undersideHit,30,1<<15);
            Debug.Log("[RingworldSmoke] UNDERSIDE hit="+underside+" distance="+undersideHit.distance);
            if(!underside||Math.Abs(undersideHit.distance-10)>.1){Fail("Underside is not collidable");yield break;}
            double floor=flight.Settings.Terrain.Sample(centre.Along,centre.Across).Height;
            var below=(Vector3)(flight.Star.position+ConvertVector.Ksp(geom.Position(centre.Along,centre.Across,floor-20)));
            var safe=flight.ConstrainCamera(below,(Vector3)(flight.Star.position+ConvertVector.Ksp(flight.Position(v))),.6f);
            double clearance=geom.Coordinates(ConvertVector.Core((Vector3d)safe-flight.Star.position)).Altitude-floor;
            Debug.Log("[RingworldSmoke] CAMERA clearance="+clearance);
            if(clearance<.59){Fail("Camera stayed below floor");yield break;}
            var lake=flight.Settings.Terrain.Landmarks.Find(l=>l.Id=="waterway");
            var lakeSample=flight.Settings.Terrain.Sample(lake.Along,lake.Across);
            var wetCamera=(Vector3)(flight.Star.position+ConvertVector.Ksp(geom.Position(lake.Along,lake.Across,lakeSample.WaterHeight-2)));
            var wetTarget=(Vector3)(flight.Star.position+ConvertVector.Ksp(geom.Position(lake.Along,lake.Across,lakeSample.WaterHeight+10)));
            var waterSafe=flight.ConstrainCamera(wetCamera,wetTarget,.6f);
            double waterClearance=geom.Coordinates(ConvertVector.Core((Vector3d)waterSafe-flight.Star.position)).Altitude-lakeSample.WaterHeight;
            Debug.Log("[RingworldSmoke] WATER CAMERA clearance="+waterClearance+" ALTITUDE="+flight.SurfaceClearance(v)+" LOD blocks="+flight.LodCount);
            if(waterClearance<.55||flight.SurfaceClearance(v)>10||flight.LodCount<50){Fail("Water camera, ring altitude or terrain LOD failed");yield break;}
            Debug.Log("[RingworldSmoke] GRAPHICS "+StockGraphics.Description+" KSP_AA="+GameSettings.ANTI_ALIASING+" KSP_TEXTURE="+GameSettings.TEXTURE_QUALITY);
            var detailFixture=new GroundDetails(flight.Settings);bool originalScatter=GameSettings.PLANET_SCATTER;float originalDensity=GameSettings.PLANET_SCATTER_FACTOR;
            try
            {
                GameSettings.PLANET_SCATTER=true;GameSettings.PLANET_SCATTER_FACTOR=1;
                var location=flight.Settings.Terrain.Landmarks[0];double da=location.Along+1100,db=location.Across+300;
                var dh=flight.Settings.Terrain.Sample(da,db).Height;var observer=geom.Position(da,db,dh+2);
                detailFixture.Update(observer,flight.Star.position,true);int count=detailFixture.Count;
                detailFixture.Update(observer,flight.Star.position,true);
                Debug.Log("[RingworldSmoke] GROUND DETAILS count="+count+" repeated="+detailFixture.Count);
                if(count<20||detailFixture.Count!=count){Fail("Ground details absent or nondeterministic");yield break;}
                GameSettings.PLANET_SCATTER=false;detailFixture.Update(observer,flight.Star.position);
                if(detailFixture.Count!=0){Fail("Ground details ignored native scatter disable");yield break;}
                var testTexture=TerrainTint.Texture(32,new Color[1024]);
                if(testTexture.mipmapCount<2){Fail("Terrain texture has no mip chain");yield break;}UnityEngine.Object.Destroy(testTexture);
            }
            finally{GameSettings.PLANET_SCATTER=originalScatter;GameSettings.PLANET_SCATTER_FACTOR=originalDensity;detailFixture.Dispose();}
            while(flight.LodPending>0)yield return null;
            Debug.Log("[RingworldSmoke] HORIZON complete blocks="+flight.LodCount+" scaled="+flight.ScaledLodCount);
            if(flight.ScaledLodCount<10||flight.Settings.Geometry.P.Seed!=worldSeed){Fail("Far horizon or saved seed failed");yield break;}
            AccessTools.Field(typeof(RingworldFlight),"panelTab").SetValue(flight,1);
            yield return new WaitForSeconds(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldSettings.png"));
            yield return new WaitForSeconds(1);AccessTools.Field(typeof(RingworldFlight),"panelTab").SetValue(flight,0);
            FlightCamera.CamPitch=-1.3f;
            yield return new WaitForSeconds(2);
            ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldSky.png"));
            yield return new WaitForSeconds(1);FlightCamera.CamPitch=.2f;
            CheatOptions.NoCrashDamage=false;CheatOptions.UnbreakableJoints=false;
            var cabin=v.parts.Find(part=>part.protoModuleCrew.Count>0);
            double epoch=flight.FrameEpoch;var parentBefore=flight.Position(v);
            var eva=FlightEVA.fetch.spawnEVA(cabin.protoModuleCrew[0],cabin,cabin.airlock,true);
            if(eva==null){Fail("EVA hatch blocked");yield break;}
            while(eva.vessel==null||eva.vessel.packed||FlightGlobals.ActiveVessel!=eva.vessel)yield return null;
            if(!flight.Owns(eva.vessel)){Fail("EVA did not inherit rotating frame");yield break;}
            yield return new WaitForSeconds(2);
            if(eva.OnALadder)eva.fsm.RunEvent(eva.On_ladderLetGo);
            bool touched=false;double fastest=0;
            for(int step=0;step<1200;step++)
            {
                if(eva==null||eva.vessel==null||eva.part.State==PartStates.DEAD){Fail("EVA died on contact");yield break;}
                double speed=flight.Velocity(eva.vessel).Length;fastest=Math.Max(fastest,speed);
                if(speed>40||flight.FrameEpoch!=epoch||!flight.Owns(eva.vessel)){Fail("EVA frame discontinuity speed="+speed);yield break;}
                touched|=RingEva.Grounded(eva);
                yield return new WaitForFixedUpdate();
            }
            Debug.Log("[RingworldSmoke] EVA contact="+touched+" peakSpeed="+fastest+" settled="+flight.Velocity(eva.vessel).Length+" state="+eva.fsm.CurrentState.name+" parentDrift="+(flight.Position(v)-parentBefore).Length);
            if(!touched||flight.Velocity(eva.vessel).Length>2||(flight.Position(v)-parentBefore).Length>2){Fail("EVA or parent did not settle");yield break;}
            if(eva.isRagdoll)eva.fsm.RunEvent(eva.On_recover_start);
            yield return new WaitForSeconds(4);
            Debug.Log("[RingworldSmoke] EVA RECOVER state="+eva.fsm.CurrentState.name+" ragdoll="+eva.isRagdoll);
            Debug.Log("[RingworldSmoke] EVA SPEED source="+EvaTransitionSpeedPatch.HorizontalSpeed(eva.vessel)+" stockSolar="+eva.vessel.horizontalSrfSpeed);
            if(eva.isRagdoll){Fail("EVA could not recover from ragdoll");yield break;}
            var walkStart=flight.Position(eva.vessel);WalkingEva=eva;
            for(int tick=0;tick<250;tick++)
            {
                if(eva==null||eva.vessel==null||eva.part.State==PartStates.DEAD){Fail("EVA died on first walk");yield break;}
                if(flight.Velocity(eva.vessel).Length>40){Fail("First EVA walk velocity discontinuity");yield break;}
                yield return new WaitForFixedUpdate();
            }
            bool walking=eva.fsm.CurrentState==eva.st_walk_acd||eva.fsm.CurrentState==eva.st_walk_fps;
            Debug.Log("[RingworldSmoke] EVA WALK state="+eva.fsm.CurrentState.name);
            WalkingEva=null;
            double walkDistance=(flight.Position(eva.vessel)-walkStart).Length;
            Debug.Log("[RingworldSmoke] EVA WALK distance="+walkDistance);
            if(!walking||walkDistance<1||walkDistance>40){Fail("EVA walking did not translate across floor");yield break;}
            WalkingEva=eva;
            foreach(var direction in new[]{new Vector2(1,1),Vector2.right,Vector2.down,new Vector2(-1,-1),Vector2.left,Vector2.up})
            {
                WalkInput=direction;var start=flight.Position(eva.vessel);double peak=0;
                for(int tick=0;tick<200;tick++)
                {
                    if(eva==null||eva.part.State==PartStates.DEAD){Fail("EVA died while turning "+direction);yield break;}
                    double speed=flight.Velocity(eva.vessel).Length;peak=Math.Max(peak,speed);
                    if(speed>40||double.IsNaN(speed)){Fail("EVA turning speed="+speed+" direction="+direction+" state="+eva.fsm.CurrentState.name);yield break;}
                    yield return new WaitForFixedUpdate();
                }
                Debug.Log("[RingworldSmoke] EVA DIRECTION "+direction+" peak="+peak+" distance="+(flight.Position(eva.vessel)-start).Length+" state="+eva.fsm.CurrentState.name);
            }
            // Exercise rapid reversals through the game's key-binding path, with damage on.
            for(int tick=0;tick<600;tick++)
            {
                WalkInput=new Vector2((tick/3)%2==0?1:-1,(tick/5)%2==0?1:-1);
                if(eva==null||eva.part.State==PartStates.DEAD||flight.Velocity(eva.vessel).Length>40){Fail("Rapid EVA direction changes failed");yield break;}
                yield return new WaitForFixedUpdate();
            }
            Debug.Log("[RingworldSmoke] EVA RAPID REVERSALS alive speed="+flight.Velocity(eva.vessel).Length+" temperature="+eva.part.temperature);
            WalkingEva=null;
            yield return new WaitForSeconds(2);
            InputLockManager.SetControlLock(ControlTypes.CAMERACONTROLS,"RingworldSmoke.Camera");
            foreach(var mode in new[]{FlightCamera.Modes.FREE,FlightCamera.Modes.CHASE})
            {
                FlightCamera.SetMode(mode);FlightCamera.fetch.SetDistanceImmediate(5);FlightCamera.CamPitch=.3f;
                yield return new WaitForSeconds(2);
                // Compare rendered poses; Update can still contain the preceding
                // camera pose transformed by this tick's EVA body rotation.
                yield return new WaitForEndOfFrame();
                var previous=FlightCamera.fetch.mainCamera.transform.rotation;float peakAngle=0;
                for(int tick=0;tick<120;tick++)
                {
                    yield return new WaitForEndOfFrame();
                    var current=FlightCamera.fetch.mainCamera.transform.rotation;
                    float angle=Quaternion.Angle(previous,current);
                    if(angle>.2f)
                    {
                        var camera=FlightCamera.fetch;
                        Debug.Log("[RingworldSmoke] CAMERA TRACE angle="+angle+" pitch="+camera.camPitch+" pivot="+camera.GetPivot().rotation.eulerAngles+" frame="+camera.getReferenceFrame().eulerAngles+" localPos="+camera.transform.localPosition+" localRot="+camera.transform.localRotation.eulerAngles+" velocity="+flight.Velocity(eva.vessel).Length+" state="+eva.fsm.CurrentState.name);
                    }
                    peakAngle=Mathf.Max(peakAngle,angle);previous=current;
                }
                Debug.Log("[RingworldSmoke] EVA CAMERA mode="+mode+" peakFrameRotation="+peakAngle+" wobble="+RingCameraTerrainPatch.Wobble(FlightCamera.fetch)+" effects="+RingCameraTerrainPatch.Effects());
                if(peakAngle>.2f||RingCameraTerrainPatch.Wobble(FlightCamera.fetch)!=0||RingCameraTerrainPatch.Effects()!=0){Fail("EVA camera orientation unstable");yield break;}
            }
            InputLockManager.RemoveControlLock("RingworldSmoke.Camera");
            ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldEva.png"));
            yield return new WaitForSeconds(1);
            deadline=Time.realtimeSinceStartup+240;
            yield return GroundScienceSmoke.Run(flight,Fail);if(!running)yield break;
            // Exercise actual game serialization and a scene roundtrip, not only scenario OnLoad.
            flight.Capture();var residentId=eva.vessel.id;var residentPosition=flight.Position(eva.vessel);
            var residentGame=HighLogic.CurrentGame.Updated();
            GamePersistence.SaveGame(residentGame,"persistent",folder,SaveMode.OVERWRITE);
            HighLogic.LoadScene(GameScenes.SPACECENTER);
            while(HighLogic.LoadedScene!=GameScenes.SPACECENTER)yield return null;
            yield return new WaitForSecondsRealtime(5);
            var reload=GamePersistence.LoadGame("persistent",folder,true,false);int residentIndex=reload.flightState.protoVessels.FindIndex(pv=>pv.vesselID==residentId);
            if(residentIndex<0){Fail("Resident missing from saved game");yield break;}
            FlightDriver.StartAndFocusVessel(reload,residentIndex);
            while(!HighLogic.LoadedSceneIsFlight||RingworldFlight.Instance==null||!RingworldFlight.Instance.Ready)yield return null;
            yield return new WaitForSeconds(5);
            flight=RingworldFlight.Instance;var resident=FlightGlobals.ActiveVessel;
            double residenceError=(flight.Position(resident)-residentPosition).Length;
            Debug.Log("[RingworldSmoke] RESIDENCE KSC roundtrip error="+residenceError+" landed="+resident.Landed+" id="+resident.id);
            if(resident.id!=residentId||residenceError>5||!resident.Landed){Fail("Residence scene roundtrip failed");yield break;}
            Debug.Log("[RingworldSmoke] PASS");
            yield return new WaitForSeconds(2);running=false;Application.Quit();
        }
        public void Update()
        {
            if(!running)return;
            if(WalkingEva!=null&&(WalkingEva.part.State==PartStates.DEAD||WalkingEva.vessel==null)){Fail("EVA lost during walking");return;}
            if(Time.realtimeSinceStartup>deadline)Fail("Timeout");
        }
        public void OnGUI()
        {
            if(running)GUI.Box(new Rect(Screen.width/2-350,145,700,65),"AUTOMATED MOD TEST — scripted controls\nNormal build is restored when this test finishes.",new GUIStyle(GUI.skin.box){fontSize=18});
        }
        private IEnumerator WeatherChecks(RingworldFlight f,Vessel v)
        {
            var s=f.Settings;var c=s.Geometry.Coordinates(f.Position(v));
            s.WeatherPeriod=600;s.WeatherVariation=1;s.StormChance=.5;s.DynamicWeather=true;s.CloudAmount=.65;s.FullRingDetail=false;
            var saved=Settings.Load();saved.Apply(s.Save());if(saved.WeatherPeriod!=600||saved.StormChance!=.5||!saved.RainEnabled){Fail("Weather settings roundtrip failed");yield break;}
            var atmosphere=(AtmosphereRenderer)AccessTools.Field(typeof(RingworldFlight),"atmosphere").GetValue(f);int builds=atmosphere.CloudBuilds,gc=GC.CollectionCount(0);long heap=GC.GetTotalMemory(false);
            double minimum=1,maximum=0,start=Planetarium.GetUniversalTime();
            var samples=new System.Collections.Generic.List<float>();
            AccessTools.Method(typeof(TimeWarp),"btnSetHighRate").Invoke(TimeWarp.fetch,new object[]{5});yield return new WaitForSecondsRealtime(2);
            if(!v.packed){Fail("Weather fixture did not enter native warp");yield break;}
            for(int quality=0;quality<3;quality++)
            {
                s.VisualQuality=quality;float until=Time.realtimeSinceStartup+3;
                while(Time.realtimeSinceStartup<until)
                {
                    yield return null;var w=s.Weather(c.Along,c.Across,Planetarium.GetUniversalTime());minimum=Math.Min(minimum,w.Severity);maximum=Math.Max(maximum,w.Severity);samples.Add(Time.unscaledDeltaTime*1000);
                }
                Debug.Log("[RingworldSmoke] WEATHER WARP tier="+quality+" rate="+TimeWarp.CurrentRate+" cloud="+f.weatherEffects.Current.Cloud+" rain="+f.weatherEffects.Current.Rain+" flash="+f.weatherEffects.Flash);
                if(f.weatherEffects.Flash!=0){Fail("Lightning aliases at high warp");yield break;}
            }
            AccessTools.Method(typeof(TimeWarp),"btnSetHighRate").Invoke(TimeWarp.fetch,new object[]{0});while(v.packed)yield return null;
            samples.Sort();Debug.Log("[RingworldSmoke] WEATHER TIMING resolution="+Screen.width+"x"+Screen.height+" ut="+(Planetarium.GetUniversalTime()-start)+" severityRange="+minimum+".."+maximum+" cloudBuilds="+(atmosphere.CloudBuilds-builds)+" gc0="+(GC.CollectionCount(0)-gc)+" heapDelta="+(GC.GetTotalMemory(false)-heap)+" p95ms="+samples[(int)(samples.Count*.95)]);
            if(maximum-minimum<.15||atmosphere.CloudBuilds-builds>1){Fail("Weather did not evolve or cloud mesh still rebuilds on a timer");yield break;}
            AccessTools.Field(typeof(RingworldFlight),"visible").SetValue(f,false);KSP.UI.UIMasterController.Instance.HideUI();FlightCamera.fetch.SetDistanceImmediate(30);FlightCamera.CamPitch=.1f;
            s.DynamicWeather=false;s.CloudAmount=0;
            double now=Planetarium.GetUniversalTime(),phase=RingGeometry.Wrap(20*c.Along/s.Geometry.P.Circumference-now/s.Geometry.P.DaySeconds,1);Planetarium.SetUniversalTime(now+(phase+.5)*s.Geometry.P.DaySeconds);
            for(int quality=0;quality<3;quality++)
            {
                s.VisualQuality=quality;yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();
                Debug.Log("[RingworldSmoke] DAY STARS tier="+quality+" visibility="+RingStarfield.Visibility);
                if(RingStarfield.Visibility>.01){Fail("Daytime galaxy was not suppressed");yield break;}
                ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldDayNoStars-"+quality+".png"));yield return new WaitForSecondsRealtime(.5f);
            }
            now=Planetarium.GetUniversalTime();phase=RingGeometry.Wrap(20*c.Along/s.Geometry.P.Circumference-now/s.Geometry.P.DaySeconds,1);Planetarium.SetUniversalTime(now+phase*s.Geometry.P.DaySeconds);
            yield return new WaitForSecondsRealtime(.5f);if(RingStarfield.Visibility<.99){Fail("Night galaxy did not return");yield break;}
            ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldNightStars.png"));yield return new WaitForSecondsRealtime(.5f);
            // Force a reproducible storm for visual inspection; no save preferences are changed.
            s.CloudAmount=1;s.RainDensity=1;
            for(int quality=0;quality<3;quality++)
            {
                s.VisualQuality=quality;yield return new WaitForSecondsRealtime(.5f);
                Debug.Log("[RingworldSmoke] RAIN tier="+quality+" drops="+f.weatherEffects.Drops+" strength="+f.weatherEffects.Current.Rain);
                if(f.weatherEffects.Drops<(quality==0?48:quality==1?144:384)){Fail("Quality-scaled rain missing");yield break;}
                ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldStorm-"+quality+".png"));yield return new WaitForSecondsRealtime(.5f);
            }
            double flashTime=Planetarium.GetUniversalTime();bool found=false;
            for(long slot=(long)(flashTime/17);slot<(long)(flashTime/17)+100;slot++)
            {double candidate=slot*17+2+s.Terrain.Scatter(slot,0,1229)*12;if(RingWeather.Lightning(s.Terrain,candidate,1)>.5){flashTime=candidate;found=true;break;}}
            if(!found){Fail("No seeded lightning event");yield break;}
            Planetarium.SetUniversalTime(flashTime);float oldScale=Time.timeScale;Time.timeScale=0;yield return null;yield return new WaitForEndOfFrame();
            Debug.Log("[RingworldSmoke] LIGHTNING strength="+f.weatherEffects.Flash);
            if(f.weatherEffects.Flash<.5){Time.timeScale=oldScale;Fail("Lightning event missing");yield break;}
            ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldLightning.png"));yield return new WaitForSecondsRealtime(.5f);Time.timeScale=oldScale;
            var ribbon=GameObject.Find("Niven Ringworld scaled habitat").GetComponent<MeshRenderer>();var mat=ribbon.sharedMaterials[1];
            if(mat.shader.name!="NivenRingworld/DistantSurface"||mat.GetFloat("_Detail")!=0){Fail("Night surface disabled by laptop detail setting");yield break;}
            var target=new RenderTexture(800,40,0);var pixels=new Texture2D(800,40,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            Graphics.Blit(Texture2D.whiteTexture,target,mat);RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,800,40),0,0);pixels.Apply();RenderTexture.active=previous;
            int dark=0,lit=0;for(int x=0;x<800;x++){var colour=pixels.GetPixel(x,20);if(colour.g<.08)dark++;if(colour.g>.35)lit++;}
            File.WriteAllBytes(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldNightBands.png"),pixels.EncodeToPNG());Destroy(pixels);target.Release();Destroy(target);
            Debug.Log("[RingworldSmoke] GLOBAL BANDS dark="+dark+" lit="+lit+" phase="+mat.GetFloat("_DayPhase"));
            if(dark<100||lit<300){Fail("Whole-ring night bands absent");yield break;}
            MapView.EnterMapView();yield return new WaitForSecondsRealtime(1);if(RingStarfield.Visibility<.99){Fail("Map starfield not restored");yield break;}
            ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldWeatherMap.png"));yield return new WaitForSecondsRealtime(.5f);MapView.ExitMapView();
            GamePersistence.SaveGame("persistent",HighLogic.SaveFolder,SaveMode.OVERWRITE);
            HighLogic.LoadScene(GameScenes.TRACKSTATION);float trackingDeadline=Time.realtimeSinceStartup+90;
            while(Time.realtimeSinceStartup<trackingDeadline)
            {
                var candidate=GameObject.Find("Niven Ringworld scaled habitat");
                if(HighLogic.LoadedScene==GameScenes.TRACKSTATION&&candidate!=null&&candidate.GetComponent<MeshRenderer>().sharedMaterials[1].shader.name=="NivenRingworld/DistantSurface")break;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(2);
            var trackingRibbon=GameObject.Find("Niven Ringworld scaled habitat");
            if(trackingRibbon==null||trackingRibbon.GetComponent<MeshRenderer>().sharedMaterials[1].shader.name!="NivenRingworld/DistantSurface"){Fail("Tracking-station ring night surface missing");yield break;}
            int ringCount=0;foreach(var mr in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())if(mr.name=="Niven Ringworld scaled habitat")ringCount++;
            if(ringCount!=1){Fail("Duplicate tracking-station ring: "+ringCount);yield break;}
            Debug.Log("[RingworldSmoke] TRACKING STATION night shader active; ringCount="+ringCount);
        }
        private static bool SkipNativeCylaStart(){return false;}
        private void Fail(string message){EvaTrace.Dump();Debug.LogError("[RingworldSmoke] FAIL: "+message);running=false;Application.Quit();}
    }
    [HarmonyPatch]
    internal static class EvaTrace
    {
        private static readonly System.Collections.Generic.Queue<string> trace=new System.Collections.Generic.Queue<string>();
        private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            foreach(var name in new[]{"HandleMovementInput","correctGroundedRotation","UpdateHeading","UpdateMovement","updateRagdollVelocities"})yield return AccessTools.Method(typeof(KerbalEVA),name);
        }
        private static void Prefix(KerbalEVA __instance,System.Reflection.MethodBase __originalMethod){Record(__instance,"before "+__originalMethod.Name);}
        private static void Postfix(KerbalEVA __instance,System.Reflection.MethodBase __originalMethod){Record(__instance,"after "+__originalMethod.Name);}
        private static void Record(KerbalEVA eva,string step)
        {
            if(eva!=SmokeTest.WalkingEva||eva.part.rb==null)return;
            var rb=eva.part.rb;var pivot=AccessTools.Field(typeof(KerbalEVA),"footPivot").GetValue(eva) as Transform;
            string line=step+" frame="+Time.frameCount+" pos="+rb.position+" v="+rb.velocity+" w="+rb.angularVelocity+" com="+rb.centerOfMass+" foot="+(pivot==null?Vector3.zero:pivot.position-eva.transform.position)+" up="+eva.fUp+" tgt="+AccessTools.Field(typeof(KerbalEVA),"tgtRpos").GetValue(eva)+" speed="+AccessTools.Field(typeof(KerbalEVA),"currentSpd").GetValue(eva);
            trace.Enqueue(line);while(trace.Count>60)trace.Dequeue();
        }
        internal static void Dump(){foreach(var line in trace)Debug.Log("[RingworldSmoke] TRACE "+line);}
    }
    [HarmonyPatch(typeof(KeyBinding),"GetKey")]
    internal static class EvaSmokeInput
    {
        private static bool Prefix(KeyBinding __instance,ref bool __result)
        {
            if(SmokeTest.WalkingEva==null)return true;
            if(__instance==GameSettings.EVA_forward)__result=SmokeTest.WalkInput.y>0;
            else if(__instance==GameSettings.EVA_back)__result=SmokeTest.WalkInput.y<0;
            else if(__instance==GameSettings.EVA_left)__result=SmokeTest.WalkInput.x<0;
            else if(__instance==GameSettings.EVA_right)__result=SmokeTest.WalkInput.x>0;
            else return true;
            return false;
        }
    }
}
#endif






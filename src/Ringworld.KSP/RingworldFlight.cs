using System;
using System.Collections;
using System.Collections.Generic;
using Ringworld.Core;
using UnityEngine;

namespace NivenRingworld
{
    [KSPAddon(KSPAddon.Startup.Flight,false)]
    [DefaultExecutionOrder(10000)]
    public sealed class RingworldFlight : MonoBehaviour
    {
        public static RingworldFlight Instance;
        internal Settings Settings;
        internal CelestialBody Star;
        internal Vector3d Center {get{return Settings.Center;}}
        internal DVec Acceleration(DVec p,DVec velocity,bool ribbon=true){return Settings.Geometry.Acceleration(p,velocity,0,ribbon)+Settings.StellarAcceleration(p,Star.gravParameter,Planetarium.GetUniversalTime()-FrameEpoch);}
        private SurfaceStreamer surface;
        internal double ScatterFloor(double along,double across){return surface!=null?surface.CollisionHeight(along,across):Settings.Terrain.Sample(along,across).Height;}
        internal GroundDetails groundDetails;
        private AmbientGroundWeather groundWeather;
        internal RingTrajectory trajectory;
        internal readonly RingSurfaceWarp surfaceWarp=new RingSurfaceWarp();
        private AtmosphereRenderer atmosphere;
        internal RingVisualRenderer visuals;
        internal RingWeatherEffects weatherEffects;
        private ConfigNode photoOptions;
        private bool photoTerrainUpdated;
        internal bool PhotoTerrainReady {get{return photoTerrainUpdated&&LodPending==0&&surface.SceneryPending==0&&surface.PhotoScattersReady;}}
        internal void PhotoTerrain(bool enabled,int? preset=null)
        {
            photoTerrainUpdated=false;
            if(enabled){photoOptions=Settings.Save();var temporary=Settings.Save();RingQualityPresets.Apply(temporary,preset??2);Settings.Apply(temporary);Settings.GenerationBudget=1;}
            else if(photoOptions!=null){Settings.Apply(photoOptions);photoOptions=null;}
            surface.RebuildLod();
        }
        internal double FrameEpoch;
        internal DVec FrameAnchorPosition;
        private double arrivalCooldown;
        private Rect window=new Rect(30,80,370,570);
        private Vector2 scroll;
        private Vector2 panelScroll;
        internal float arrivalHeight=300;
        internal bool gentleArrival;
        private bool visible,transferring;
        private RingToolbar toolbar;
        internal static bool SandboxControls { get { return HighLogic.CurrentGame!=null&&HighLogic.CurrentGame.Mode==Game.Modes.SANDBOX; } }
        private int destination;
        private int panelTab;
        private readonly RingSettingsPanel settingsPanel=new RingSettingsPanel();
        private readonly RingSandboxEditor ringEditor=new RingSandboxEditor();
        private ConfigNode boundOptions;
        private string status="Fly toward the ring for automatic arrival. Alt+R or the ring toolbar button opens this panel.";
        private float nextCapture;
        private const string WarpLock="NivenRingworld.Warp";
        private RingworldScenario State { get { return RingworldScenario.Instance; } }
        internal bool Active
        {
            get
            {
                var v=FlightGlobals.ActiveVessel;
                return State!=null&&State.Expedition&&(transferring||FrameInUse||(v!=null&&State.Vessels.ContainsKey(v.id.ToString())&&State.Vessels[v.id.ToString()].RingId==Settings.RingId));
            }
        }
        // A Unity physics scene has one velocity frame even while its active vessel
        // changes. Never infer that frame solely from the newly selected vessel ID.
        internal bool FrameInUse
        {
            get
            {
                if(State==null||Star==null)return false;
                foreach(var v in FlightGlobals.VesselsLoaded)
                {
                    VesselRecord r;
                    if(v!=null&&v.mainBody==Star&&State.Vessels.TryGetValue(v.id.ToString(),out r)&&r.RingId==Settings.RingId&&r.Restored)return true;
                }
                return false;
            }
        }
        internal bool AtmosphereTransition {get{return transferring;}}
        internal bool Ready
        {
            get
            {
                var v=FlightGlobals.ActiveVessel;VesselRecord r;
                return Active&&!transferring&&v!=null&&!v.packed&&v.mainBody==Star&&State.Vessels.TryGetValue(v.id.ToString(),out r)&&r.RingId==Settings.RingId&&r.Restored;
            }
        }
        internal bool IsParticipant(Vessel v) { return State!=null&&v!=null&&v.mainBody==Star&&State.Vessels.ContainsKey(v.id.ToString())&&State.Vessels[v.id.ToString()].RingId==Settings.RingId; }
        internal bool Owns(Vessel v)
        {
            VesselRecord r;
            return !transferring&&State!=null&&v!=null&&v.mainBody==Star&&State.Vessels.TryGetValue(v.id.ToString(),out r)&&r.RingId==Settings.RingId&&r.Restored;
        }
        internal DVec Position(Vessel v)
        {
            if(surfaceWarp.Anchored(v))return RootPosition(v);
            if(v.packed){
                VesselRecord record;
                if(State!=null&&State.Vessels.TryGetValue(v.id.ToString(),out record)&&record.RingId==Settings.RingId&&record.Landed)
                    return Settings.Geometry.RotateAroundAxis(record.Position,Settings.Geometry.P.Omega*(FrameEpoch-record.Epoch));
                return ConvertVector.Core(v.GetWorldPos3D()-Center);
            }
            double mass=0;DVec offset=new DVec();var seen=new HashSet<Rigidbody>();
            foreach(var part in v.parts)
            {
                var rb=part.rb;if(rb==null||rb.isKinematic||!seen.Add(rb))continue;
                offset+=ConvertVector.Core(rb.worldCenterOfMass-v.transform.position)*rb.mass;mass+=rb.mass;
            }
            return RootPosition(v)+(mass>0?offset/mass:new DVec());
        }
        private DVec RootPosition(Vessel v){return ConvertVector.Core((Vector3d)v.transform.position-Center);}
        internal DVec Velocity(Vessel v)
        {
            // The packed osculating orbit is inertial bookkeeping, not air speed.
            // Rails anchors are stationary in the rotating atmosphere, including
            // the packing tick after rigidbodies have become kinematic.
            if(surfaceWarp.Anchored(v))return new DVec();
            if(v.packed)
            {
                // Packed residents retain surface-frame velocity in their
                // record. The stock bookkeeping orbit includes hundreds of
                // km/s of ring spin; treating that as airspeed causes enormous
                // shock heating during packing/unpacking and nearby loading.
                VesselRecord record;
                if(State!=null&&State.Vessels.TryGetValue(v.id.ToString(),out record)&&record.RingId==Settings.RingId)
                    return record.Landed?new DVec():Settings.Geometry.RotateAroundAxis(record.Velocity,Settings.Geometry.P.Omega*(FrameEpoch-record.Epoch));
                RingAnchorState state;
                if(RingAnchorEphemeris.TryVesselRelative(v,Star,Planetarium.GetUniversalTime(),out state))return state.Velocity-Settings.AnchorAt(Planetarium.GetUniversalTime()).Velocity;
                return ConvertVector.Core(v.obt_velocity)-Settings.AnchorAt(Planetarium.GetUniversalTime()).Velocity;
            }
            double mass=0;DVec velocity=new DVec();var seen=new HashSet<Rigidbody>();
            foreach(var part in v.parts)
            {
                var rb=part.rb;if(rb==null||rb.isKinematic||!seen.Add(rb))continue;
                velocity+=ConvertVector.Core(rb.velocity)*rb.mass;mass+=rb.mass;
            }
            var result=mass>0?velocity/mass+ConvertVector.Core(Krakensbane.GetFrameVelocity()):ConvertVector.Core(v.obt_velocity);
            if(FrameInUse)return result;
            RingAnchorState reference;string error;
            if(!RingAnchorEphemeris.TryRelative(RingAnchorEphemeris.Id(v.mainBody),Star,Planetarium.GetUniversalTime(),out reference,out error))return result;
            return result+reference.Velocity-Settings.AnchorAt(Planetarium.GetUniversalTime()).Velocity;
        }
        public void Start()
        {
            Instance=this;
            try
            {
                Settings=Settings.Load();Star=FlightGlobals.Bodies.Find(b=>b.name=="Sun");
                if(Star==null)throw new InvalidOperationException("This release requires the stock Sun.");
                surface=new SurfaceStreamer(Settings);
                groundDetails=new GroundDetails(Settings);groundWeather=new AmbientGroundWeather(Settings);
                atmosphere=new AtmosphereRenderer(Settings);
                StockIntegration.Install();
                window.x=Mathf.Max(10,Screen.width-window.width-70);
                toolbar=new RingToolbar(value=>visible=value);
                trajectory=gameObject.AddComponent<RingTrajectory>();
                GameEvents.onCrewOnEva.Add(OnCrewOnEva);
                Debug.Log("[NivenRingworld] Flight controller ready; R="+Settings.Geometry.P.Radius);
            }
            catch(Exception e){Debug.LogException(e);status=e.Message;enabled=false;}
        }
        internal void ApplyOptions(ConfigNode options,bool rebuildWorld)
        {
            if(groundDetails!=null)groundDetails.Hide();if(groundWeather!=null)groundWeather.Hide();
            Settings.Apply(options);Star=Settings.Body;State.Options=options;boundOptions=options;settingsPanel.Reset();
            if(rebuildWorld&&trajectory!=null)trajectory.ResetPrediction();
            if(rebuildWorld){surface.Dispose();surface=new SurfaceStreamer(Settings);}
            else surface.RebuildLod();
            atmosphere.Dispose();atmosphere=new AtmosphereRenderer(Settings);
            if(visuals!=null)visuals.InvalidateWeather();
        }
        private float nextRingSelection;
        private void SelectNearbyRing()
        {
            if(State==null||Settings==null||transferring||FrameInUse)return;
            var v=FlightGlobals.ActiveVessel;if(v==null)return;
            VesselRecord resident;
            if(State.Vessels.TryGetValue(v.id.ToString(),out resident)){State.SelectRing(resident.RingId);return;}
            if(Time.realtimeSinceStartup<nextRingSelection)return;nextRingSelection=Time.realtimeSinceStartup+.5f;
            string nearest=RingSelection.Nearest(v);if(nearest!=null)State.SelectRing(nearest);
        }
        internal bool VisitRing(string id,out string reason)
        {
            reason="";if(!SandboxControls||State==null||transferring||(visuals!=null&&visuals.PhotoActive)){reason="Finish the current transition/photo first.";return false;}
            if(!LiveCraft(FlightGlobals.ActiveVessel)){reason="The controlled craft was destroyed. Select or launch a surviving craft before relocating.";return false;}
            var s=State.RingSettings(id);if(s==null||s.Body==null){reason="Reference body is not installed.";return false;}
            if(Active)foreach(var other in FlightGlobals.VesselsLoaded)if(other!=FlightGlobals.ActiveVessel&&Owns(other)){reason="Nearby vessels share this rotating frame. Switch to a distant vessel before transferring between rings.";return false;}
            if(Active){Leave();if(Active){reason="Unpack the vessel and leave time warp first.";return false;}}
            State.SelectRing(id);ApplyOptions(State.GetOptions(),true);destination=0;nextRingSelection=Time.realtimeSinceStartup+10;Visit();return true;
        }
        public void Update()
        {
            SelectNearbyRing();
            if(State!=null&&Settings!=null&&boundOptions!=State.GetOptions())ApplyOptions(State.GetOptions(),true);
            if(Input.GetKey(KeyCode.LeftAlt)&&Input.GetKeyDown(KeyCode.R)){visible=!visible;if(toolbar!=null)toolbar.SetSelected(visible);}
            AdoptNearbyActiveVessel();
            if(Settings!=null&&Star!=null&&!Active) Settings.Geometry.OrientationRadians=Settings.Geometry.P.Omega*Planetarium.GetUniversalTime();
            if(!Active||Settings==null){
                if(groundDetails!=null)groundDetails.Hide();if(groundWeather!=null)groundWeather.Hide();
                if(trajectory!=null&&Settings!=null)trajectory.PackedUpdate(this);
                PrepareArrival();
                return;
            }
            InputLockManager.RemoveControlLock(WarpLock);
            surfaceWarp.Update(this);
            var v=FlightGlobals.ActiveVessel;
            if(v==null||transferring)return;
            VesselRecord record;
            if(State.Vessels.TryGetValue(v.id.ToString(),out record)&&record.RingId==Settings.RingId&&!record.Restored)
            {SetFrameEpoch(record.Epoch);StartCoroutine(Transfer(v,record.Position,record.Velocity,record.Rotation,false));return;}
            if(v.mainBody!=Star||(v.packed&&!surfaceWarp.Anchored(v))||(!State.Vessels.ContainsKey(v.id.ToString())||State.Vessels[v.id.ToString()].RingId!=Settings.RingId))return;
            RestoreNearbyResidents(v);
            surface.Update(Position(v),Center);
            if(visuals!=null&&visuals.PhotoActive)photoTerrainUpdated=true;
            groundDetails.Update(Position(v),Center);groundWeather.Update(Position(v),Center);
            var coord=Settings.Geometry.Coordinates(Position(v));
            surface.Light(Settings.Geometry.Daylight(coord.Along,Planetarium.GetUniversalTime(),coord.Across,coord.Altitude));
            if(Time.realtimeSinceStartup>nextCapture){Capture();nextCapture=Time.realtimeSinceStartup+1;}
        }
        private void OnCrewOnEva(GameEvents.FromToAction<Part,Part> ev)
        {
            if(ev.from==null||ev.to==null||!Owns(ev.from.vessel))return;
            RegisterParticipant(ev.to.vessel);
        }
        private void RegisterParticipant(Vessel v)
        {
            if(v==null||State==null||Settings.IsAnchor(v))return;
            string id=v.id.ToString();if(State.Vessels.ContainsKey(id))return;
            State.Vessels[id]=new VesselRecord{Id=id,RingId=Settings.RingId,Position=RootPosition(v),Velocity=Velocity(v),Rotation=v.transform.rotation,Restored=true,Epoch=FrameEpoch};
            // Stock modules can query the debris orbit in Initialize/Start,
            // before our next FixedUpdate. Publish inertial bookkeeping now.
            var record=State.Vessels[id];
            if(v.orbit!=null)RingResidence.UpdateBookkeeping(v,Settings,Star,record.Position,record.Velocity,FrameEpoch);
            Debug.Log("[NivenRingworld] Inherited rotating frame: "+v.vesselName);
        }
        internal bool AdoptParticipant(Vessel v)
        {
            if(v==null||State==null||!State.Expedition||v.mainBody!=Star)return false;
            if(Owns(v))return true;
            if(State.Vessels.ContainsKey(v.id.ToString())&&State.Vessels[v.id.ToString()].RingId==Settings.RingId)return false;
            foreach(var other in FlightGlobals.VesselsLoaded)
            {
                if(other==null||other==v||!Owns(other))continue;
                if((v.transform.position-other.transform.position).sqrMagnitude>250*250)continue;
                RegisterParticipant(v);return true;
            }
            return false;
        }
        private void AdoptNearbyActiveVessel(){foreach(var candidate in FlightGlobals.VesselsLoaded)AdoptParticipant(candidate);}
        private void RestoreNearbyResidents(Vessel active)
        {
            foreach(var v in FlightGlobals.VesselsLoaded)
            {
                VesselRecord r;if(v==active||v==null||v.packed||!State.Vessels.TryGetValue(v.id.ToString(),out r)||r.RingId!=Settings.RingId||r.Restored)continue;
                double angle=Settings.Geometry.P.Omega*(FrameEpoch-r.Epoch);var target=Settings.Geometry.RotateAroundAxis(r.Position,angle);
                if((target-RootPosition(active)).Length>2200)continue;
                RingVesselPose.Set(v,Center+ConvertVector.Ksp(target),Settings.AxisRotation(angle)*r.Rotation);
                RingCollisionFrame.Reset(v);
                v.SetWorldVelocity(ConvertVector.Ksp(Settings.Geometry.RotateAroundAxis(r.Velocity,angle)));
                r.Position=target;r.Rotation=v.transform.rotation;r.Epoch=FrameEpoch;r.Restored=true;v.IgnoreGForces(5);v.IgnoreSpeed(5);
            }
        }
        public void FixedUpdate()
        {
            if(Settings==null||Star==null)return;
            AdoptNearbyActiveVessel();
            TryArrival();
            if(!Active)
            {
                foreach(var vessel in FlightGlobals.VesselsLoaded)
                {
                    if(vessel==null||vessel.packed||vessel.mainBody!=Star)continue;
                    var pull=Settings.Geometry.RibbonAcceleration(Position(vessel));
                    var seen=new HashSet<Rigidbody>();
                    foreach(var part in vessel.parts)if(part.rb!=null&&!part.rb.isKinematic&&seen.Add(part.rb))part.rb.AddForce(ConvertVector.Unity(pull),ForceMode.Acceleration);
                }
                return;
            }
            if(transferring)return;
            var active=FlightGlobals.ActiveVessel;
            if(active==null||active.mainBody!=Star||active.packed)return;
            VesselRecord activeRecord;
            if(!State.Vessels.TryGetValue(active.id.ToString(),out activeRecord)||!activeRecord.Restored)return;
            if(!Settings.Geometry.InArrivalRegion(Position(active),true)){Leave();return;}
            surface.Reposition(Center);
            var bodies=new HashSet<Rigidbody>();
            foreach(var v in FlightGlobals.VesselsLoaded)
            {
                if(v==null||v.packed||v.mainBody!=Star)continue;
                string id=v.id.ToString();
                if(!State.Vessels.ContainsKey(id))
                {
                    // Pick up newly separated stages and EVAs close to the expedition.
                    if((v.GetWorldPos3D()-active.GetWorldPos3D()).magnitude>250)continue;
                    RegisterParticipant(v);
                }
                if(State.Vessels[id].RingId!=Settings.RingId||!State.Vessels[id].Restored)continue;
                DVec vesselPos=Position(v);
                var ribbonPull=Settings.Geometry.RibbonAcceleration(vesselPos);
                foreach(var part in v.parts)
                {
                    Rigidbody rb=part.rb;
                    if(rb==null||rb.isKinematic||!bodies.Add(rb))continue;
                    DVec pos=RootPosition(v)+ConvertVector.Core(rb.worldCenterOfMass-v.transform.position);
                    DVec vel=ConvertVector.Core((Vector3d)rb.velocity+Krakensbane.GetFrameVelocity());
                    var coord=Settings.Geometry.Coordinates(pos);
                    // This acceleration belongs to the opted-in rotating frame, not to a spherical SOI.
                    DVec stock=ConvertVector.Core(FlightGlobals.getGeeForceAtPosition(rb.worldCenterOfMass,v.mainBody));
                    DVec acceleration=Acceleration(pos,vel,false)+ribbonPull-stock;
                    // PartBuoyancy now supplies mass-independent displacement forces.
                    // Retain bounded water damping, but never the old 2.5g lift which
                    // made even arbitrarily heavy ballast tanks float.
                    double drag=Math.Max(0,part.submergedPortion)*.7,speed=vel.Length;
                    if(speed>1e-6)acceleration-=vel*((1-Math.Exp(-drag*speed*Time.fixedDeltaTime))/Time.fixedDeltaTime);
                    rb.AddForce(ConvertVector.Unity(acceleration),ForceMode.Acceleration);
                }
            }
        }
        public void LateUpdate()
        {
            if(Settings==null||Star==null)return;
            if(!transferring&&surface!=null)surface.Reposition(Center);
            var v=FlightGlobals.ActiveVessel;
            if(Owns(v)&&FlightCamera.fetch!=null&&!MapView.MapIsEnabled)
            {
                var camera=FlightCamera.fetch.mainCamera;
                if(camera!=null)
                {
                    var target=(Vector3)(Center+ConvertVector.Ksp(Position(v)));
                    float clipRadius=Mathf.Max(.6f,camera.nearClipPlane*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad)*Mathf.Sqrt(1+camera.aspect*camera.aspect)+.2f);
                    RingCameraTerrainPatch.ApplyClearance(FlightCamera.fetch,ConstrainCamera(camera.transform.position,target,clipRadius)-camera.transform.position);
                }
            }
            bool airView=!transferring&&v!=null&&v.mainBody==Star&&!MapView.MapIsEnabled;
            if(FlightCamera.fetch!=null&&FlightCamera.fetch.mainCamera!=null&&visuals==null)
                visuals=FlightCamera.fetch.mainCamera.gameObject.AddComponent<RingVisualRenderer>();
            if(visuals!=null)visuals.Prepare(airView,Center);
            if(weatherEffects==null)weatherEffects=new RingWeatherEffects();weatherEffects.Update(airView,Settings,Center);
            if(atmosphere!=null)atmosphere.Update(airView&&(visuals==null||!visuals.Rendering||visuals.SimplePhoto),Center,visuals!=null&&visuals.CylaActive);
        }
        internal Vector3 ConstrainCamera(Vector3 desired,Vector3 target,float clearance)
        {
            var delta=desired-target;RaycastHit hit;
            if(delta.magnitude>clearance&&Physics.SphereCast(target,clearance,delta.normalized,out hit,delta.magnitude,1<<15,QueryTriggerInteraction.Ignore))
                desired=target+delta.normalized*Mathf.Max(clearance,hit.distance-.2f);
            desired=(Vector3)(Center+ConvertVector.Ksp(RingCameraBounds.ConstrainWalls(Settings.Geometry,ConvertVector.Core((Vector3d)target-Center),ConvertVector.Core((Vector3d)desired-Center),clearance,Settings.StructuralThickness)));
            var p=Settings.Geometry.Coordinates(ConvertVector.Core((Vector3d)desired-Center));
            if(Math.Abs(p.Across)<=Settings.Geometry.P.Width/2)
            {
                double floor=surface.CameraFloor(p.Along,p.Across)+clearance;
                // A cast cannot find a starting overlap, or a one-sided face behind it.
                // This final constraint also protects the near clipping plane.
                if(p.Altitude<floor)desired=(Vector3)(Center+ConvertVector.Ksp(Settings.Geometry.Position(p.Along,p.Across,floor)));
            }
            return desired;
        }
        internal int LodCount {get{return surface==null?0:surface.LodCount;}}
        internal int LodPending {get{return surface==null?0:surface.LodPending+surface.CanopyPending+surface.SceneryPending;}}
        internal int ScaledLodCount {get{return surface==null?0:surface.ScaledLodCount;}}
        internal int BuiltScaledLodCount {get{return surface==null?0:surface.BuiltScaledLodCount;}}
        internal double SurfaceClearance(Vessel v)
        {
            var p=Settings.Geometry.Coordinates(Position(v));
            return Math.Max(0,p.Altitude-surface.GroundOrWaterFloor(p.Along,p.Across));
        }
        internal void Capture()
        {
            if(!Active||transferring||Star==null)return;
            foreach(var v in FlightGlobals.VesselsLoaded)
            {
                VesselRecord r;if(v==null||v.packed||v.mainBody!=Star||!State.Vessels.TryGetValue(v.id.ToString(),out r)||r.RingId!=Settings.RingId||!r.Restored)continue;
                r.Position=RootPosition(v);r.Velocity=Velocity(v);r.Rotation=v.transform.rotation;r.Epoch=FrameEpoch;r.Landed=v.Landed;
            }
        }
        internal void Visit()
        {
            var v=FlightGlobals.ActiveVessel;if(v==null||State==null||transferring)return;
            if(!CanRelocate(v))return;
            var l=Settings.Terrain.Landmarks[destination];
            VisitCoordinates(l.Along,l.Across);
        }
        private readonly System.Random explorationRandom=new System.Random();
        internal void VisitRandomTerrain(int? suppliedSelectionSeed=null)
        {
            if(FlightGlobals.ActiveVessel==null||State==null||transferring)return;
            if(!CanRelocate(FlightGlobals.ActiveVessel))return;
            RingPoint site;int selectionSeed=suppliedSelectionSeed??explorationRandom.Next();
            if(!TerrainExploration.TryChoose(Settings.Terrain,selectionSeed,out site))
            {status="No suitable dry terrain found. Try another random location.";return;}
            Debug.Log("[NivenRingworld] Random terrain worldSeed="+Settings.Geometry.P.Seed+" selectionSeed="+selectionSeed+" along="+site.Along+" across="+site.Across+" ground="+site.Altitude+" biome="+Settings.Terrain.Sample(site.Along,site.Across).Biome);
            VisitCoordinates(site.Along,site.Across);
        }
        private void VisitCoordinates(double along,double across)
        {
            var v=FlightGlobals.ActiveVessel;
            if(!CanRelocate(v))return;
            Capture();
            SetFrameEpoch(Planetarium.GetUniversalTime());
            // Arrive above the analytic surface; scenery clearance still requires piloting.
            var t=Settings.Terrain.Sample(along,across);
            double height=Math.Max(t.Height,double.IsNegativeInfinity(t.WaterHeight)?t.Height:t.WaterHeight)+arrivalHeight;
            DVec position=Settings.Geometry.Position(along,across,height);
            // Preserve pitch/roll relative to the old surface. A plane's nose axis
            // is not its surface-up axis; aligning the nose stood stock planes upright.
            var previousUp=ConvertVector.Unity(ConvertVector.Core(v.upAxis));
            if(previousUp.sqrMagnitude<.5f)previousUp=v.transform.up;
            Quaternion rotation=Quaternion.FromToRotation(previousUp,ConvertVector.Unity(Settings.Geometry.Up(position)))*v.transform.rotation;
            if(gentleArrival)position=Settings.Geometry.Position(along,across,RingArrivalPlacement.Height(Settings,v,along,across,previousUp));
            StartCoroutine(Transfer(v,position,new DVec(),rotation,true,gentleArrival));
        }
        private IEnumerator TrainingApproach()
        {
            float previous=arrivalHeight;bool previousGentle=gentleArrival;arrivalHeight=250000;gentleArrival=false;Visit();arrivalHeight=previous;gentleArrival=previousGentle;
            while(transferring)yield return null;
            var v=FlightGlobals.ActiveVessel;if(!Owns(v))yield break;
            v.SetWorldVelocity(ConvertVector.Ksp(Settings.Geometry.Up(Position(v))*-1000));
            Leave();status="Training setup: spin-matched craft descending at 1 km/s. Automatic handoff occurs near 210 km; air begins at 60 km.";
        }
        private IEnumerator Transfer(Vessel v,DVec position,DVec velocity,Quaternion rotation,bool newVisit,bool gentle=false)
        {
            if(!CanRelocate(v))yield break;
            RingCameraBlend.Cancel();
            surfaceWarp.Rate=1;transferring=true;status="Preparing surface colliders...";
            TimeWarp.SetRate(0,true);
            if(newVisit)CaptureBeforeTransfer(v);
            State.Expedition=true;
            string id=v.id.ToString();
            State.Vessels[id]=new VesselRecord{Id=id,RingId=Settings.RingId,Position=position,Velocity=velocity,Rotation=rotation,Epoch=FrameEpoch};
            // Use KSP's own orbit placement to handle SOI bookkeeping first.
            double stagingRadius=Math.Max(Star.Radius+100000000,(Settings.AnchorAt(Planetarium.GetUniversalTime()).Position+position).Length);
            FlightGlobals.fetch.SetShipOrbit(Star.flightGlobalsIndex,0,stagingRadius,0,0,0,0,Planetarium.GetUniversalTime());
            yield return null;
            double deadline=Time.realtimeSinceStartup+45;
            while((v.packed||v.mainBody!=Star)&&Time.realtimeSinceStartup<deadline)yield return null;
            if(v.packed||v.mainBody!=Star)
            {
                status="KSP did not finish switching the orbital reference. Retry when unpacked.";transferring=false;yield break;
            }
            if(!CanRelocate(v)){transferring=false;yield break;}
            v.Landed=false;v.Splashed=false;v.landedAt="";
            // Shift first: putting a float Transform billions of metres away loses hundreds of metres.
            FloatingOrigin.SetOffset(Center+ConvertVector.Ksp(position));
            Krakensbane.ResetVelocityFrame(true);
            RingVesselPose.Set(v,Center+ConvertVector.Ksp(position),rotation);
            v.SetWorldVelocity(ConvertVector.Ksp(velocity));
            // SetWorldVelocity reports the rotating-frame speed (often zero).
            // Publish its valid inertial orbit in the same frame so stock readers
            // never see a degenerate zero-angular-momentum stellar orbit.
            RingResidence.UpdateBookkeeping(v,Settings,Star,position,velocity,FrameEpoch);
            v.orbitDriver.pos=ConvertVector.Ksp(position+Settings.AnchorAt(Planetarium.GetUniversalTime()).Position);
            v.orbitDriver.vel=ConvertVector.Ksp(velocity);
            RingCollisionFrame.Reset(v);
            v.DetachPatchedConicsSolver();
            v.IgnoreGForces(30);v.IgnoreSpeed(30);
            surface.Update(position,Center,true);
            Physics.SyncTransforms();
            if(gentle){
                position=RingArrivalPlacement.Refine(Settings,v,Center,position);
                v.SetPosition(Center+ConvertVector.Ksp(position),true);v.SetWorldVelocity(ConvertVector.Ksp(velocity));
                State.Vessels[id].Position=position;
                RingResidence.UpdateBookkeeping(v,Settings,Star,position,velocity,FrameEpoch);
                v.orbitDriver.pos=ConvertVector.Ksp(position+Settings.AnchorAt(Planetarium.GetUniversalTime()).Position);
                Physics.SyncTransforms();RingCollisionFrame.Reset(v);
            }
            State.Vessels[id].Restored=true;
            transferring=false;status="Ring frame active. Alt+R opens the ring information panel.";
            FlightCamera.SetMode(FlightCamera.Modes.FREE);
            Debug.Log("[NivenRingworld] Expedition entered at "+Settings.Geometry.Coordinates(position).Along);
        }
        internal static bool LiveCraft(Vessel v)
        {
            return v!=null&&v.state!=Vessel.State.DEAD&&v.rootPart!=null&&v.parts!=null&&v.parts.Exists(part=>part!=null);
        }
        private bool CanRelocate(Vessel v)
        {
            if(LiveCraft(v))return true;
            status="The controlled craft was destroyed. Select or launch a surviving craft before relocating.";
            Debug.LogWarning("[NivenRingworld] Relocation refused: active craft is dead or has no surviving root part.");return false;
        }
        private void CaptureBeforeTransfer(Vessel v)
        {
            // New visits deliberately move only the current vessel; other saved expeditions remain frozen.
            if(!Active)return;
            foreach(var other in FlightGlobals.VesselsLoaded)
            {
                VesselRecord r;if(other==null||other==v||!State.Vessels.TryGetValue(other.id.ToString(),out r))continue;
                if(other.mainBody==Star&&!other.packed){r.Position=RootPosition(other);r.Velocity=Velocity(other);r.Rotation=other.transform.rotation;r.Restored=false;}
            }
        }
        private void SetFrameEpoch(double epoch)
        {
            FrameEpoch=epoch;FrameAnchorPosition=Settings.AnchorAt(Planetarium.GetUniversalTime()).Position;Settings.Geometry.OrientationRadians=Settings.Geometry.P.Omega*epoch;
        }
        private void PrepareArrival()
        {
            var v=FlightGlobals.ActiveVessel;
            if(transferring||!FlightGlobals.ready||v==null||State==null||Settings.IsAnchor(v)||!RingAnchorEphemeris.OrbitReady(v.orbit))
            {InputLockManager.RemoveControlLock(WarpLock);return;}
            var g=Settings.Geometry;var pos=Position(v);
            var coord=g.Coordinates(pos);
            if(coord.Altitude>-1000&&coord.Altitude<600000&&Math.Abs(coord.Across)<g.P.Width/2+500000)
            {
                surface.Update(pos,Center);surface.Light(g.Daylight(coord.Along,Planetarium.GetUniversalTime(),coord.Across,coord.Altitude));
            }
            // Drop warp before the boundary, including high speed radial approaches.
            double lead=Math.Max(10,TimeWarp.CurrentRate*Time.fixedDeltaTime*4);
            double eta=g.TimeToArrival(pos,Velocity(v),lead);
            if(eta<=lead)
            {
                InputLockManager.SetControlLock(ControlTypes.TIMEWARP,WarpLock);
                if(TimeWarp.CurrentRateIndex!=0)TimeWarp.SetRate(0,true);
                status="Approaching ring: matching the rotating atmosphere requires "+(g.SpinVelocity(pos).Length/1000).ToString("F1")+" km/s tangential velocity.";
            }
            else InputLockManager.RemoveControlLock(WarpLock);
        }
        internal void TryArrival()
        {
            var v=FlightGlobals.ActiveVessel;
            if(FrameInUse||Active||transferring||State==null||v==null||v.packed||Settings.IsAnchor(v)||Planetarium.GetUniversalTime()<arrivalCooldown)return;
            if(!Settings.Geometry.InArrivalRegion(Position(v),false))return;
            SetFrameEpoch(Planetarium.GetUniversalTime());
            State.Expedition=true;
            ChangeFrame(false);
            surface.Update(Position(v),Center,true);Physics.SyncTransforms();
            status="Automatic ring arrival. Velocity relative to the rotating atmosphere preserved.";
            Debug.Log("[NivenRingworld] Automatic arrival: air-relative speed="+Velocity(v).Length+" epoch="+FrameEpoch);
        }
        // One chart change for every loaded vessel: there cannot be two different
        // coordinate frames in the same Unity physics scene. Preserve rigidbody spin
        // and per-part velocities, including flexible craft, rather than braking them.
        private void ChangeFrame(bool leaving)
        {
            var g=Settings.Geometry;double elapsed=leaving?Planetarium.GetUniversalTime()-FrameEpoch:0;
            double angle=g.P.Omega*elapsed;
            Quaternion q=Settings.AxisRotation(angle);
            RingCameraBlend.Begin(q);
            var snapshots=new List<FrameVessel>();
            foreach(var v in FlightGlobals.VesselsLoaded)
            {
                if(v==null||v.packed||Settings.IsAnchor(v))continue;
                if(leaving&&(!State.Vessels.ContainsKey(v.id.ToString())||State.Vessels[v.id.ToString()].RingId!=Settings.RingId))continue;
                snapshots.Add(new FrameVessel(v,this,leaving,elapsed,q));
            }
            Krakensbane.ResetVelocityFrame(true);
            var active=FlightGlobals.ActiveVessel;
            foreach(var s in snapshots)if(s.Vessel==active)
                FloatingOrigin.SetOffset((leaving?Settings.InertialCenter:Center)+ConvertVector.Ksp(s.Position));
            foreach(var s in snapshots)
            {
                var v=s.Vessel;
                RingVesselPose.Set(v,(leaving?Settings.InertialCenter:Center)+ConvertVector.Ksp(s.Position),s.Rotation);
                v.SetWorldVelocity(ConvertVector.Ksp(s.Velocity));
                foreach(var body in s.Bodies)
                {
                    body.Body.position=(Vector3)((leaving?Settings.InertialCenter:Center)+ConvertVector.Ksp(s.Position))+body.Offset;
                    body.Body.rotation=body.Rotation;body.Body.velocity=body.Velocity;body.Body.angularVelocity=body.Angular;
                }
                v.ResetGroundContact();v.KillPermanentGroundContact();v.Landed=false;
                RingCollisionFrame.Reset(v);
                if(leaving)
                {
                    v.orbit.UpdateFromStateVectors(ConvertVector.Orbit(ConvertVector.Ksp(g.ToInertialPosition(s.OriginalCOM,elapsed)+Settings.AnchorAt(Planetarium.GetUniversalTime()).Position)),ConvertVector.Orbit(ConvertVector.Ksp(s.Velocity)),Star,Planetarium.GetUniversalTime());
                    v.AttachPatchedConicsSolver();State.Vessels.Remove(v.id.ToString());
                }
                else
                {
                    v.DetachPatchedConicsSolver();v.orbitDriver.referenceBody=Star;string id=v.id.ToString();
                    State.Vessels[id]=new VesselRecord{Id=id,RingId=Settings.RingId,Position=s.Position,Velocity=s.Velocity,Rotation=s.Rotation,Restored=true,Epoch=FrameEpoch};
                }
            }
            Physics.SyncTransforms();
        }
        private sealed class FrameBody
        {
            internal Rigidbody Body;internal Vector3 Offset,Velocity,Angular;internal Quaternion Rotation;
        }
        private sealed class FrameVessel
        {
            internal Vessel Vessel;internal DVec Position,Velocity,OriginalCOM;internal Quaternion Rotation;
            internal readonly List<FrameBody> Bodies=new List<FrameBody>();
            internal FrameVessel(Vessel v,RingworldFlight f,bool leaving,double elapsed,Quaternion q)
            {
                Vessel=v;var g=f.Settings.Geometry;OriginalCOM=f.Position(v);
                var root=f.RootPosition(v);Position=leaving?g.ToInertialPosition(root,elapsed):root;
                Velocity=leaving?g.ToInertialVelocity(OriginalCOM,f.Velocity(v),elapsed)+f.Settings.AnchorAt(Planetarium.GetUniversalTime()).Velocity:g.RotatingVelocity(OriginalCOM,f.Velocity(v));
                Rotation=leaving?q*v.transform.rotation:v.transform.rotation;
                var seen=new HashSet<Rigidbody>();
                foreach(var part in v.parts)
                {
                    var rb=part.rb;if(rb==null||rb.isKinematic||!seen.Add(rb))continue;
                    var p=root+ConvertVector.Core(rb.worldCenterOfMass-v.transform.position);
                    var velocity=ConvertVector.Core((Vector3d)rb.velocity+Krakensbane.GetFrameVelocity());
                    if(!leaving){RingAnchorState reference;string error;if(RingAnchorEphemeris.TryRelative(RingAnchorEphemeris.Id(v.mainBody),f.Star,Planetarium.GetUniversalTime(),out reference,out error))velocity+=reference.Velocity;}
                    var offset=rb.position-v.transform.position;
                    Bodies.Add(new FrameBody{Body=rb,Offset=leaving?q*offset:offset,Rotation=leaving?q*rb.rotation:rb.rotation,
                        Velocity=ConvertVector.Unity(leaving?g.ToInertialVelocity(p,velocity,elapsed)+f.Settings.AnchorAt(Planetarium.GetUniversalTime()).Velocity:g.RotatingVelocity(p,velocity-f.Settings.AnchorAt(Planetarium.GetUniversalTime()).Velocity)),
                        Angular=leaving?q*(rb.angularVelocity+ConvertVector.Unity(g.Axis)*(float)g.P.Omega):rb.angularVelocity-ConvertVector.Unity(g.Axis)*(float)g.P.Omega});
                }
            }
        }
        internal void Leave()
        {
            if(transferring||State==null||!Ready)return;
            Capture();ChangeFrame(true);
            State.Expedition=State.Vessels.Count>0;InputLockManager.RemoveControlLock(WarpLock);
            surface.Dispose();surface=new SurfaceStreamer(Settings);
            arrivalCooldown=Planetarium.GetUniversalTime()+2;
            Settings.Geometry.OrientationRadians=Settings.Geometry.P.Omega*Planetarium.GetUniversalTime();
            status="Inertial solar flight restored with the ring's rotation and elapsed phase.";
            Debug.Log("[NivenRingworld] Automatic departure: inertial frame restored.");
        }
        internal bool ScienceContext(Vessel v,out string key,out string title,out string report)
        {
            key=title=report="";
            if(!Active||v==null||v.mainBody!=Star||(!State.Vessels.ContainsKey(v.id.ToString())||State.Vessels[v.id.ToString()].RingId!=Settings.RingId))return false;
            var p=Settings.Geometry.Coordinates(Position(v));var t=Settings.Terrain.Sample(p.Along,p.Across);
            if(p.Altitude-t.Height>120||p.Altitude-t.Height< -5||Math.Abs(p.Across)>Settings.Geometry.P.Width/2)return false;
            double distance;var l=Settings.Terrain.Nearest(p.Along,p.Across,out distance);
            key=distance<Math.Min(l.Radius,1500)?"site_"+l.Id:"biome_"+t.Biome;
            title=distance<Math.Min(l.Radius,1500)?l.Name:t.Biome.ToString();
            report=distance<Math.Min(l.Radius,1500)?l.Description:"A sample of the artificial habitat's "+t.Biome.ToString().ToLowerInvariant()+" environment.";
            return true;
        }
        public void OnGUI()
        {
            if(!visible||(toolbar!=null&&!toolbar.UiVisible)||Settings==null||(visuals!=null&&visuals.PhotoActive))return;
            window.x=Mathf.Clamp(window.x,0,Mathf.Max(0,Screen.width-window.width));
            window.y=Mathf.Clamp(window.y,0,Mathf.Max(0,Screen.height-100));
            window=GUILayout.Window(19700114,window,DrawWindow,"Niven Ringworld");
        }
        private void DrawWindow(int id)
        {
            int previousTab=panelTab;
            panelTab=GUILayout.Toolbar(panelTab,SandboxControls?new[]{"Expedition","Settings","Extensions","Research","Rings"}:new[]{"Expedition","Extensions","Research"});
            if(panelTab!=previousTab)panelScroll=Vector2.zero;
            panelScroll=GUILayout.BeginScrollView(panelScroll,GUILayout.Height(Mathf.Max(240,Mathf.Min(610,Screen.height-150))));
            if(weatherEffects!=null&&Owns(FlightGlobals.ActiveVessel))GUILayout.Label("Weather: "+weatherEffects.Description);
            if(SandboxControls&&panelTab==4){ringEditor.Draw(this);GUILayout.EndScrollView();GUI.DragWindow(new Rect(0,0,10000,25));return;}
            if(panelTab==(SandboxControls?3:2)){State.Research.Draw(FlightGlobals.ActiveVessel);GUILayout.EndScrollView();GUI.DragWindow(new Rect(0,0,10000,25));return;}
            if(panelTab==(SandboxControls?2:1)){settingsPanel.Draw(this,true);GUILayout.EndScrollView();GUI.DragWindow(new Rect(0,0,10000,25));return;}
            if(SandboxControls&&panelTab==1){settingsPanel.Draw(this);GUILayout.EndScrollView();GUI.DragWindow(new Rect(0,0,10000,25));return;}
            GUILayout.Label(Settings.RingName.ToUpperInvariant());
            GUILayout.Label("Tangential speed: "+(Math.Abs(Settings.Geometry.P.Omega)*Settings.Geometry.P.Radius/1000).ToString("N2")+" km/s");
            GUILayout.Label("Radius "+(Settings.Geometry.P.Radius/1000).ToString("N0")+" km   |   Width "+(Settings.Geometry.P.Width/1000).ToString("N0")+" km");
            var v=FlightGlobals.ActiveVessel;
            if(Active&&v!=null&&v.mainBody==Star)
            {
                var p=Settings.Geometry.Coordinates(Position(v));var terrain=Settings.Terrain.Sample(p.Along,p.Across);
                GUILayout.Label("Above ground: "+(p.Altitude-terrain.Height).ToString("N1")+" m   |   "+terrain.Biome);
                GUILayout.Label("Surface-relative speed: "+Velocity(v).Length.ToString("N1")+" m/s   |   g: "+Acceleration(Position(v),new DVec()).Length.ToString("F3"));
                GUILayout.Label("Spinward: "+(p.Along/1000).ToString("N1")+" km\nAcross: "+(p.Across/1000).ToString("N1")+" km"+(SandboxControls?"   |   Tiles: "+surface.TileCount+" | LOD: "+surface.LodCount+" (queued "+(surface.LodPending+surface.CanopyPending+surface.SceneryPending)+")":""));
                GUILayout.Label("Air: "+v.atmDensity.ToString("F4")+" kg/m³  |  "+v.staticPressurekPa.ToString("F2")+" kPa  |  Mach "+v.mach.ToString("F2"));
                GUILayout.Label("Local light: "+(100*Settings.Geometry.Daylight(p.Along,Planetarium.GetUniversalTime(),p.Across,p.Altitude)).ToString("F0")+"%");
            }
            if(Active)surfaceWarp.Draw(this);
            if(visuals!=null)
            {
                visuals.DrawPhotoEntry();
                GUILayout.Label(visuals.Status);
            }
            if(trajectory!=null)GUILayout.Label(trajectory.Status);
            if(SandboxControls)
            {
                scroll=GUILayout.BeginScrollView(scroll,GUILayout.Height(190));
                for(int i=0;i<Settings.Terrain.Landmarks.Count;i++)
                    if(GUILayout.Toggle(destination==i,Settings.Terrain.Landmarks[i].Name))destination=i;
                GUILayout.EndScrollView();
                GUILayout.Label(Settings.Terrain.Landmarks[destination].Description);
                GUILayout.Label("Arrival height: "+arrivalHeight.ToString("F0")+" m above ground / water");
                arrivalHeight=GUILayout.HorizontalSlider(arrivalHeight,60,2000);
                gentleArrival=GUILayout.Toggle(gentleArrival,"Gentle placement near ground / water");
                if(gentleArrival)GUILayout.Label("Places the craft just above the surface using its collider clearance. Overrides arrival height; choose a clear, level site.");
                bool live=LiveCraft(v);
                if(!live)GUILayout.Label("Craft destroyed: select or launch a surviving craft to relocate. The camera cannot be teleported with an empty vessel.");
                GUI.enabled=!transferring&&live&&State!=null;
                if(GUILayout.Button(Active?"Relocate above selected site":"Begin expedition at selected site"))Visit();
                if(GUILayout.Button("Random terrain test location (spin-matched)"))VisitRandomTerrain();
                GUILayout.Label(gentleArrival?"Random visit: gentle placement above dry procedural terrain.":"Random visit: dry procedural terrain, at the arrival height above ground. Fly the descent; this is not an automatic landing.");
                if(GUILayout.Button("Training: set up a spin-matched approach"))StartCoroutine(TrainingApproach());
                if(Active&&GUILayout.Button("Leave ring frame for spaceflight"))Leave();
                GUI.enabled=true;
            }
            GUILayout.Label("Science: use stock experiments and EVA reports. See Research for locations and expedition objectives.");
            GUILayout.Label(status);
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0,0,10000,25));
        }
        public void OnDestroy()
        {
            RingCameraBlend.Cancel();
            if(toolbar!=null){toolbar.Dispose();toolbar=null;}
            if(visuals!=null){visuals.EndPhoto();Destroy(visuals);}
            if(weatherEffects!=null)weatherEffects.Dispose();
            if(FlightGlobals.fetch!=null)Capture();GameEvents.onCrewOnEva.Remove(OnCrewOnEva);InputLockManager.RemoveControlLock(WarpLock);
            if(groundDetails!=null)groundDetails.Dispose();if(groundWeather!=null)groundWeather.Dispose();if(surface!=null)surface.Dispose();if(atmosphere!=null)atmosphere.Dispose();if(Instance==this)Instance=null;
        }
    }
}

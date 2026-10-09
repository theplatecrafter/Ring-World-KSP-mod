using System;
using HarmonyLib;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    internal static class RingResidence
    {
        internal static bool UpdateContact(RingworldFlight f,Vessel v)
        {
            if(!f.Owns(v))return false;
            if(v.packed)return v.Landed;
            bool contact=f.surfaceWarp.Anchored(v);
            if(v.parts!=null)foreach(var part in v.parts)if(part!=null)contact|=part.GroundContact||part.PermanentGroundContact;
            v.Landed=contact;v.Splashed=v.parts!=null&&v.parts.Exists(p=>p!=null&&p.submergedPortion>0);
            if(!contact&&v.Splashed)v.situation=Vessel.Situations.SPLASHED;
            if(contact)
            {
                v.situation=Vessel.Situations.LANDED;
                v.landedAt="Ringworld";v.displaylandedAt="Ringworld";
            }
            else if(v.landedAt=="Ringworld") {v.landedAt="";v.displaylandedAt="";}
            return contact;
        }
        internal static bool Saved(Vessel v,out VesselRecord record)
        {
            record=null;var s=RingworldScenario.Instance;
            return v!=null&&s!=null&&s.Vessels.TryGetValue(v.id.ToString(),out record);
        }
        internal static void UpdateBookkeeping(Vessel v,Settings settings,CelestialBody star,DVec position,DVec velocity,double epoch)
        {
            double now=Planetarium.GetUniversalTime(),elapsed=now-epoch;var g=settings.Geometry;
            v.orbit.UpdateFromStateVectors(ConvertVector.Orbit(ConvertVector.Ksp(g.ToInertialPosition(position,elapsed)+settings.AnchorAt(now).Position)),ConvertVector.Orbit(ConvertVector.Ksp(g.ToInertialVelocity(position,velocity,elapsed)+settings.AnchorAt(now).Velocity)),star,now);
        }
        internal static void HoldSaved(Vessel v,VesselRecord r)
        {
            if(!r.Landed||v.orbitDriver==null)return;
            var s=RingworldScenario.Instance;var f=RingworldFlight.Instance;
            var settings=s.RingSettings(r.RingId);if(settings==null||settings.Body==null)return;
            var star=settings.Body;
            double epoch=f!=null&&f.Settings.RingId==r.RingId&&f.FrameInUse?f.FrameEpoch:Planetarium.GetUniversalTime();
            double angle=settings.Geometry.P.Omega*(epoch-r.Epoch);
            var p=settings.Geometry.RotateAroundAxis(r.Position,angle);
            UpdateBookkeeping(v,settings,star,p,new DVec(),epoch);
            v.orbitDriver.pos=ConvertVector.Ksp(p+settings.AnchorAt(Planetarium.GetUniversalTime()).Position);v.orbitDriver.vel=Vector3d.zero;
            var renderPosition=f!=null&&f.FrameInUse&&f.Settings.RingId!=r.RingId?RingSceneFrame.Vector(p):p;
            RingVesselPose.Set(v,settings.Center+ConvertVector.Ksp(renderPosition),settings.AxisRotation(angle)*r.Rotation);
            v.Landed=true;v.situation=Vessel.Situations.LANDED;v.landedAt="Ringworld";v.displaylandedAt="Ringworld";
        }
    }
    // A ring participant's local velocity is not a Keplerian state vector. KSP
    // can recreate a solver while unpacking/initializing debris, before our
    // explicit Detach call. Never solve those temporary zero-angular-momentum
    // orbits; RingTrajectory owns their prediction until they leave the frame.
    [HarmonyPatch(typeof(PatchedConicSolver),"Update")]
    internal static class RingParticipantConics
    {
        internal static int Suppressed;
        private static bool Prefix(PatchedConicSolver __instance)
        {
            var v=__instance.obtDriver==null?null:__instance.obtDriver.vessel;
            var f=RingworldFlight.Instance;
            if(f==null||!f.IsParticipant(v))return true;
            Suppressed++;return false;
        }
    }
    [HarmonyPatch(typeof(Vessel),"GoOnRails")]
    internal static class RingResidentPack
    {
        private static void Prefix(Vessel __instance)
        {Capture(__instance);}
        internal static void Capture(Vessel __instance)
        {
            var f=RingworldFlight.Instance;VesselRecord r;
            if(f==null||__instance.packed||__instance.parts==null||__instance.parts.Count==0||!f.Owns(__instance)||!RingResidence.Saved(__instance,out r))return;
            r.Position=ConvertVector.Core((Vector3d)__instance.transform.position-f.Center);r.Velocity=f.Velocity(__instance);r.Rotation=__instance.transform.rotation;r.Epoch=f.FrameEpoch;r.Landed=__instance.Landed;
#if RINGWORLD_SMOKE_TEST
            RingResidentRestore.Trace("capture",f,__instance,r);
#endif
        }
    }
    [HarmonyPatch(typeof(Vessel),"Unload")]
    internal static class RingResidentUnload
    {
        // Stock Unload destroys/clears the parts before calling GoOnRails.
        // Capture while the physical surface pose and velocity still exist.
        private static void Prefix(Vessel __instance){RingResidentPack.Capture(__instance);}
        private static void Postfix(Vessel __instance){VesselRecord r;if(!__instance.loaded&&RingResidence.Saved(__instance,out r))r.Restored=false;}
    }
    internal static class RingResidentRestore
    {
#if RINGWORLD_SMOKE_TEST
        internal static void Trace(string step,RingworldFlight f,Vessel v,VesselRecord r){
            if(v.vesselName!="Multipart resident issue regression")return;
            var stored=f.Settings.Geometry.Coordinates(r.Position);var actual=f.Settings.Geometry.Coordinates(ConvertVector.Core((Vector3d)v.transform.position-f.Center));
            int bodies=0,kinematic=0;foreach(var p in v.parts)if(p!=null&&p.rb!=null){bodies++;if(p.rb.isKinematic)kinematic++;}
            UnityEngine.Debug.Log("[RingworldSmoke] RESIDENT TRACE "+step+" frame="+UnityEngine.Time.frameCount+" UT="+Planetarium.GetUniversalTime()+" frameEpoch="+f.FrameEpoch+" recordEpoch="+r.Epoch+" stored="+stored.Along+","+stored.Across+","+stored.Altitude+" actual="+actual.Along+","+actual.Across+","+actual.Altitude+" flags="+v.loaded+","+v.packed+","+v.Landed+" record="+r.Landed+","+r.Restored+" rb="+bodies+" kinematic="+kinematic);
        }
#endif
        internal static void Pose(RingworldFlight f,Vessel v,VesselRecord r)
        {
#if RINGWORLD_SMOKE_TEST
            Trace("restore before",f,v,r);
#endif
            double angle=f.Settings.Geometry.P.Omega*(f.FrameEpoch-r.Epoch);
            var velocity=f.Settings.Geometry.RotateAroundAxis(r.Velocity,angle);
            RingResidence.HoldSaved(v,r);
            r.Position=ConvertVector.Core((Vector3d)v.transform.position-f.Center);
            r.Rotation=v.transform.rotation;r.Velocity=velocity;r.Epoch=f.FrameEpoch;r.Restored=true;
            if(!v.packed){v.SetWorldVelocity(ConvertVector.Ksp(velocity));RingCollisionFrame.Reset(v);}
#if RINGWORLD_SMOKE_TEST
            Trace("restore after",f,v,r);
#endif
        }
    }
    [HarmonyPatch(typeof(Vessel),"Load")]
    internal static class RingResidentLoad
    {
        // LoadObjects can initialize a recreated vessel directly as unpacked,
        // so the GoOffRails hook is not the only native placement boundary.
        private static void Postfix(Vessel __instance)
        {
            var f=RingworldFlight.Instance;VesselRecord r;
#if RINGWORLD_SMOKE_TEST
            if(f!=null&&RingResidence.Saved(__instance,out r))RingResidentRestore.Trace("load returned",f,__instance,r);
#endif
            if(f!=null&&f.FrameInUse&&__instance.loaded&&RingResidence.Saved(__instance,out r)&&r.Landed&&r.RingId==f.Settings.RingId)
                RingResidentRestore.Pose(f,__instance,r);
        }
    }
    [HarmonyPatch(typeof(Vessel),"getCorrectedLandedAltitude")]
    internal static class RingResidentAltitude
    {
        private static bool Prefix(Vessel __instance,double alt,ref double __result)
        {VesselRecord r;if(!RingResidence.Saved(__instance,out r))return true;__result=alt;return false;}
    }
    // Stock unpacking would orient a LANDED vessel against the Sun's sphere.
    // Preserve the truthful landed status, but bypass that one spherical branch.
    [HarmonyPatch(typeof(Vessel),"GoOffRails")]
    internal static class RingResidentUnpack
    {
        private static void Prefix(Vessel __instance,out bool __state)
        {
            VesselRecord r=null;__state=__instance.Landed&&RingResidence.Saved(__instance,out r);
            if(__state){var f=RingworldFlight.Instance;if(f!=null&&f.FrameInUse&&r.RingId==f.Settings.RingId)RingResidence.HoldSaved(__instance,r);__instance.Landed=false;}
        }
        private static void Postfix(Vessel __instance,bool __state)
        {
            if(__state)__instance.Landed=true;
            VesselRecord r;var f=RingworldFlight.Instance;
            if(!__instance.packed&&f!=null&&RingResidence.Saved(__instance,out r)&&r.RingId==f.Settings.RingId)
            {
                // Stock unpacking places parts from the current inertial
                // ephemeris, overwriting the surface pose seeded by Prefix.
                // A resident must rejoin the existing rotating chart, not move
                // by hundreds of kilometres of spin since the frame epoch.
                if(r.Landed&&f.FrameInUse)RingResidentRestore.Pose(f,__instance,r);
                if(__instance==FlightGlobals.ActiveVessel)Krakensbane.ResetVelocityFrame(true);
                RingCollisionFrame.Reset(__instance);
                __instance.SetWorldVelocity(ConvertVector.Ksp(f.Settings.Geometry.RotateAroundAxis(r.Velocity,f.Settings.Geometry.P.Omega*(f.FrameEpoch-r.Epoch))));
            }
        }
    }
    [HarmonyPatch(typeof(FlightGlobals),"ClearToSave",new[]{typeof(bool)})]
    internal static class RingSavePermission
    {
        private static bool Prefix(ref ClearToSaveStatus __result)
        {
            var f=RingworldFlight.Instance;var v=FlightGlobals.ActiveVessel;
            if(f==null)return true;
            if(f.AtmosphereTransition){__result=ClearToSaveStatus.NOT_UNDER_ACCELERATION;return false;}
            if(!f.Owns(v))return true;
            // The Sun's stock atmosphere test cannot recognize this habitat. Never
            // fall through to its CLEAR result for an unsupported ring resident.
            if(!v.Landed){__result=ClearToSaveStatus.NOT_IN_ATMOSPHERE;return false;}
            if(v.isEVA&&v.evaController!=null&&v.evaController.OnALadder){__result=ClearToSaveStatus.NOT_WHILE_ON_A_LADDER;return false;}
            if(!f.surfaceWarp.CanSave(f,v)){__result=ClearToSaveStatus.NOT_WHILE_MOVING_OVER_SURFACE;return false;}
            f.Capture();__result=ClearToSaveStatus.CLEAR;return false;
        }
    }
    [HarmonyPatch(typeof(OrbitDriver),"UpdateOrbit")]
    internal static class RingResidentOrbit
    {
        private static bool Prefix(OrbitDriver __instance)
        {
            var v=__instance.vessel;VesselRecord r;
            if(v==null||!RingResidence.Saved(v,out r))return true;
            var f=RingworldFlight.Instance;
            if(!v.packed)
            {
                if(f==null||!f.Owns(v))return true;
                var p=f.Position(v);var speed=f.Velocity(v);
                // Keep a valid inertial osculating orbit for stock persistence and spawned parts.
                // A stationary rotating-frame velocity otherwise produces a degenerate solar orbit.
                RingResidence.UpdateBookkeeping(v,f.Settings,f.Star,p,speed,f.FrameEpoch);
                __instance.pos=ConvertVector.Ksp(p+f.Settings.AnchorAt(Planetarium.GetUniversalTime()).Position);__instance.vel=ConvertVector.Ksp(speed);return false;
            }
            if(!r.Landed)return true;
            if(f!=null&&f.surfaceWarp.Anchored(v))return true;
            RingResidence.HoldSaved(v,r);return false;
        }
    }
}

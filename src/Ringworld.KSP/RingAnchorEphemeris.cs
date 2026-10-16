using System;
using System.Collections.Generic;
using Ringworld.Core;

namespace NivenRingworld
{
    // Shared orbit sampling for placement and future encounter prediction. This
    // does not modify any stock orbit, body list, or vessel's reference body.
    internal static class RingAnchorEphemeris
    {
        [ThreadStatic] private static List<CelestialBody> targetPath,referencePath;
        internal static string Id(CelestialBody body)
        {if(body==null)throw new ArgumentException("The orbital body is missing.",nameof(body));return "body:"+body.name;}
        internal static string Id(Vessel vessel){return "vessel:"+vessel.id.ToString("D");}

        internal static bool TryRelative(string anchor,CelestialBody reference,double time,
            out RingAnchorState state,out string error)
        {
            state=new RingAnchorState();error=null;
            if(reference==null){error="The orbital reference body is missing.";return false;}
            if(anchor!=null&&anchor.StartsWith("body:",StringComparison.Ordinal))
            {
                var target=FindBody(anchor);
                if(target==null){error="The ring's anchor body is not installed: "+anchor;return false;}
                return TryBodyRelative(target,reference,time,out state,out error);
            }
            try{state=AnchorEphemeris.Relative(anchor,Id(reference),time,Sample);return true;}
            catch(ArgumentException e){error=e.Message;return false;}
            catch(InvalidOperationException e){error=e.Message;return false;}
        }
        private static CelestialBody FindBody(string id)
        {
            foreach(var body in FlightGlobals.Bodies)
                if(body!=null&&id.Length==body.name.Length+5&&string.CompareOrdinal(id,5,body.name,0,body.name.Length)==0)return body;
            return null;
        }
        private static bool BodyPath(CelestialBody body,List<CelestialBody> path)
        {
            path.Clear();
            while(body!=null)
            {
                if(path.Contains(body))return false;path.Add(body);
                var orbit=body.orbit;
                if(orbit==null||orbit.referenceBody==null||orbit.referenceBody==body)return true;
                body=orbit.referenceBody;
            }
            return true;
        }
        internal static bool TryBodyRelative(CelestialBody target,CelestialBody reference,double time,out RingAnchorState state,out string error)
        {
            state=new RingAnchorState();error=null;
            if(target==null||reference==null||!RingParameters.Finite(time)){error="Bodies and a finite universal time are required.";return false;}
            if(target==reference)return true;
            if(targetPath==null){targetPath=new List<CelestialBody>(16);referencePath=new List<CelestialBody>(16);}
            if(!BodyPath(target,targetPath)||!BodyPath(reference,referencePath)){error="Cycle in anchor orbit hierarchy.";return false;}
            CelestialBody common=null;
            foreach(var body in referencePath)if(targetPath.Contains(body)){common=body;break;}
            if(common==null){error="Anchor and reference belong to disconnected orbital systems.";return false;}
            try
            {
                // Reuse paths and stop before the common ancestor: no per-sample
                // dictionaries/sets/ID strings, no loss of local precision at a distant star.
                var a=SumBodies(targetPath,common,time);var b=SumBodies(referencePath,common,time);
                state=new RingAnchorState(a.Position-b.Position,a.Velocity-b.Velocity,a.Acceleration-b.Acceleration);return true;
            }
            catch(InvalidOperationException e){error=e.Message;return false;}
            catch(ArgumentException e){error=e.Message;return false;}
        }
        private static RingAnchorState SumBodies(List<CelestialBody> path,CelestialBody common,double time)
        {
            var p=new DVec();var v=new DVec();var a=new DVec();
            foreach(var body in path)
            {
                if(body==common)break;var local=OrbitState(body.orbit,time);
                p+=local.Position;v+=local.Velocity;a+=local.Acceleration;
            }
            return new RingAnchorState(p,v,a);
        }

        internal static bool OrbitReady(Orbit orbit)
        {
            // A finite SMA/eccentricity alone is insufficient: a radial/zero-speed
            // state can have NaN orientation or anomaly. Do not pass those values
            // into KSP's iterative Kepler solvers (their convergence tests can hang).
            return orbit!=null&&orbit.referenceBody!=null&&orbit.referenceBody.gravParameter>0&&
                RingParameters.Finite(orbit.semiMajorAxis)&&
                RingParameters.Finite(orbit.eccentricity)&&orbit.eccentricity>=0&&orbit.eccentricity!=1&&
                ((orbit.eccentricity<1&&orbit.semiMajorAxis>0)||(orbit.eccentricity>1&&orbit.semiMajorAxis<0))&&
                RingParameters.Finite(orbit.inclination)&&RingParameters.Finite(orbit.LAN)&&
                RingParameters.Finite(orbit.argumentOfPeriapsis)&&RingParameters.Finite(orbit.epoch)&&
                RingParameters.Finite(orbit.meanAnomalyAtEpoch)&&RingParameters.Finite(orbit.meanMotion)&&orbit.meanMotion>0;
        }
        internal static bool TryVesselRelative(Vessel vessel,CelestialBody reference,double time,out RingAnchorState state)
        {
            state=new RingAnchorState();if(vessel==null||!OrbitReady(vessel.orbit))return false;
            return TryOrbitRelative(vessel.orbit,reference,time,out state);
        }
        internal static RingAnchorState VesselRelative(Vessel vessel,CelestialBody reference,double time)
        {
            return OrbitRelative(vessel.orbit,reference,time);
        }
        internal static RingAnchorState OrbitRelative(Orbit orbit,CelestialBody reference,double time)
        {
            RingAnchorState state;
            if(!TryOrbitRelative(orbit,reference,time,out state))
                throw new InvalidOperationException("Orbit state or future SOI patch is not initialized.");
            return state;
        }
        // A final conic has a reusable nextPatch slot too. It is NOT a future
        // orbit, and commonly has no reference body. Follow actual transitions
        // only; pending transitions return unavailable without throwing per frame.
        internal static bool TrySelectPatch(Orbit orbit,double time,out Orbit selected)
        {
            selected=null;if(!RingParameters.Finite(time))return false;
            int hops=0;
            while(OrbitReady(orbit))
            {
                if(orbit.patchEndTransition==Orbit.PatchTransitionType.FINAL||orbit.patchEndTransition==Orbit.PatchTransitionType.INITIAL||
                    !(orbit.EndUT>orbit.StartUT)||time<orbit.EndUT){selected=orbit;return true;}
                // Bounds also protect corrupt cyclic patch lists without allocating
                // a HashSet for every single future trajectory sample.
                if(++hops>128)return false;
                orbit=orbit.nextPatch;
            }
            return false;
        }
        internal static bool TryOrbitRelative(Orbit orbit,CelestialBody reference,double time,out RingAnchorState state)
        {
            state=new RingAnchorState();Orbit selected;
            if(reference==null||!TrySelectPatch(orbit,time,out selected))return false;
            RingAnchorState parent;string error;
            if(!TryBodyRelative(selected.referenceBody,reference,time,out parent,out error))return false;
            try
            {
                var local=OrbitState(selected,time);
                state=new RingAnchorState(parent.Position+local.Position,parent.Velocity+local.Velocity,parent.Acceleration+local.Acceleration);return true;
            }
            catch(InvalidOperationException){return false;}
            catch(ArgumentException){return false;}
        }
        private static AnchorOrbitSample Sample(string id,double time)
        {
            if(id.StartsWith("body:",StringComparison.Ordinal))
            {
                var body=FindBody(id);
                if(body==null)throw new InvalidOperationException("The ring's anchor body is not installed: "+id);
                var orbit=body.orbit;
                if(orbit==null||orbit.referenceBody==null||orbit.referenceBody==body)
                    return new AnchorOrbitSample(null,new RingAnchorState());
                return OrbitSample(orbit,time);
            }
            if(id.StartsWith("vessel:",StringComparison.Ordinal))
            {
                var vessel=FlightGlobals.Vessels.Find(v=>Id(v)==id);
                if(vessel==null)throw new InvalidOperationException("The ring's asteroid/comet anchor is missing: "+id);
                // Never follow a newly selected active vessel when an old anchor
                // disappears. Persistent GUID identity is required across reloads.
                if(vessel.vesselType!=VesselType.SpaceObject||vessel.LandedOrSplashed)
                    throw new InvalidOperationException("A moving ring anchor must be an orbiting asteroid or comet.");
                Orbit orbit;
                if(!TrySelectPatch(vessel.orbit,time,out orbit))throw new InvalidOperationException("The asteroid/comet has no initialized orbit at that time.");
                return OrbitSample(orbit,time);
            }
            throw new ArgumentException("Unrecognized ring anchor ID: "+id);
        }
        private static AnchorOrbitSample OrbitSample(Orbit orbit,double time)
        {return new AnchorOrbitSample(Id(orbit.referenceBody),OrbitState(orbit,time));}
        private static RingAnchorState OrbitState(Orbit orbit,double time)
        {
            if(!OrbitReady(orbit))throw new InvalidOperationException("Orbit state is not initialized; wait for KSP's orbital reference transition.");
            // KSP's orbit API uses XZY, while local-space render/physics use XYZ.
            var p=ConvertVector.Core(ConvertVector.Orbit(orbit.getRelativePositionAtUT(time)));
            var v=ConvertVector.Core(ConvertVector.Orbit(orbit.getOrbitalVelocityAtUT(time)));
            double r=p.Length;
            if(!(r>0)||!RingParameters.Finite(r)||!RingParameters.Finite(v.X)||!RingParameters.Finite(v.Y)||!RingParameters.Finite(v.Z))throw new InvalidOperationException("Orbit state is not finite or has a zero radius.");
            var a=p*(-orbit.referenceBody.gravParameter/(r*r*r));
            return new RingAnchorState(p,v,a);
        }
    }
}

using System;
using System.Collections.Generic;
using Ringworld.Core;

namespace NivenRingworld
{
    // Shared orbit sampling for placement and future encounter prediction. This
    // does not modify any stock orbit, body list, or vessel's reference body.
    internal static class RingAnchorEphemeris
    {
        internal static string Id(CelestialBody body){return "body:"+body.name;}
        internal static string Id(Vessel vessel){return "vessel:"+vessel.id.ToString("D");}

        internal static bool TryRelative(string anchor,CelestialBody reference,double time,
            out RingAnchorState state,out string error)
        {
            state=new RingAnchorState();error=null;
            if(reference==null){error="The orbital reference body is missing.";return false;}
            try{state=AnchorEphemeris.Relative(anchor,Id(reference),time,Sample);return true;}
            catch(ArgumentException e){error=e.Message;return false;}
            catch(InvalidOperationException e){error=e.Message;return false;}
        }

        internal static RingAnchorState VesselRelative(Vessel vessel,CelestialBody reference,double time)
        {
            return OrbitRelative(vessel.orbit,reference,time);
        }
        internal static RingAnchorState OrbitRelative(Orbit orbit,CelestialBody reference,double time)
        {
            var visited=new HashSet<Orbit>();
            while(orbit!=null&&orbit.nextPatch!=null&&orbit.EndUT>orbit.StartUT&&time>=orbit.EndUT)
            {
                if(!visited.Add(orbit))throw new InvalidOperationException("Cycle in vessel orbit patches.");
                orbit=orbit.nextPatch;
            }
            if(orbit==null)throw new InvalidOperationException("Vessel has no orbit.");
            RingAnchorState parent;string error;
            if(!TryRelative(Id(orbit.referenceBody),reference,time,out parent,out error))throw new InvalidOperationException(error);
            var local=OrbitSample(orbit,time).State;
            return new RingAnchorState(parent.Position+local.Position,parent.Velocity+local.Velocity,parent.Acceleration+local.Acceleration);
        }
        private static AnchorOrbitSample Sample(string id,double time)
        {
            if(id.StartsWith("body:",StringComparison.Ordinal))
            {
                var body=FlightGlobals.Bodies.Find(b=>Id(b)==id);
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
                var orbit=vessel.orbit;var visited=new HashSet<Orbit>();
                while(orbit!=null&&orbit.nextPatch!=null&&orbit.EndUT>orbit.StartUT&&time>=orbit.EndUT)
                {
                    if(!visited.Add(orbit))throw new InvalidOperationException("Cycle in anchor orbit patches.");
                    orbit=orbit.nextPatch;
                }
                if(orbit==null)throw new InvalidOperationException("The asteroid/comet has no valid orbit.");
                return OrbitSample(orbit,time);
            }
            throw new ArgumentException("Unrecognized ring anchor ID: "+id);
        }
        private static AnchorOrbitSample OrbitSample(Orbit orbit,double time)
        {
            if(orbit.referenceBody==null)throw new InvalidOperationException("Anchor orbit has no reference body.");
            // KSP's orbit API uses XZY, while local-space render/physics use XYZ.
            var p=ConvertVector.Core(ConvertVector.Orbit(orbit.getRelativePositionAtUT(time)));
            var v=ConvertVector.Core(ConvertVector.Orbit(orbit.getOrbitalVelocityAtUT(time)));
            double r=p.Length;
            if(!(r>0))throw new InvalidOperationException("Anchor orbit has a zero radius.");
            var a=p*(-orbit.referenceBody.gravParameter/(r*r*r));
            return new AnchorOrbitSample(Id(orbit.referenceBody),new RingAnchorState(p,v,a));
        }
    }
}

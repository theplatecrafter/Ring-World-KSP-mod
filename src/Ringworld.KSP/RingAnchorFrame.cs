using System;
using System.Collections.Generic;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    internal sealed partial class Settings
    {
        internal string AnchorId;
        private double anchorTime=double.NaN;
        private RingAnchorState anchorState;
        private DVec lastAnchorPosition;
        private bool hasAnchorPosition,warnedMissingAnchor;
        internal string AnchorWarning;
        private void LoadAnchorFallback(ConfigNode n)
        {
            hasAnchorPosition=n.HasValue("lastAnchorX");warnedMissingAnchor=false;AnchorWarning=null;
            lastAnchorPosition=new DVec(Read(n,"lastAnchorX",0),Read(n,"lastAnchorY",0),Read(n,"lastAnchorZ",0));
        }
        internal void SaveAnchorFallback(ConfigNode n)
        {
            // Persist a current sample, never a speculative trajectory sample.
            RingAnchorState sample;string error;
            if(RingAnchorEphemeris.TryRelative(AnchorId??("body:"+ReferenceBody),Body,Planetarium.GetUniversalTime(),out sample,out error))
            {lastAnchorPosition=sample.Position;hasAnchorPosition=true;}
            if(!hasAnchorPosition)return;
            n.SetValue("lastAnchorX",lastAnchorPosition.X.ToString("R",System.Globalization.CultureInfo.InvariantCulture),true);
            n.SetValue("lastAnchorY",lastAnchorPosition.Y.ToString("R",System.Globalization.CultureInfo.InvariantCulture),true);
            n.SetValue("lastAnchorZ",lastAnchorPosition.Z.ToString("R",System.Globalization.CultureInfo.InvariantCulture),true);
        }
        internal bool IsAnchor(Vessel v){return v!=null&&AnchorId==RingAnchorEphemeris.Id(v);}
        internal RingAnchorState AnchorAt(double time)
        {
            if(anchorTime==time)return anchorState;
            RingAnchorState sample;string error;
            if(!RingAnchorEphemeris.TryRelative(AnchorId??("body:"+ReferenceBody),Body,time,out sample,out error))
            {
                if(!hasAnchorPosition)throw new InvalidOperationException(error);
                AnchorWarning="Anchor unavailable; ring held at its last saved position relative to "+ReferenceBody+". Select another anchor in the ring editor.";
                if(!warnedMissingAnchor){warnedMissingAnchor=true;Debug.LogWarning("[NivenRingworld] "+RingName+": "+AnchorWarning+" "+error);}
                sample=new RingAnchorState(lastAnchorPosition,new DVec(),new DVec());
            }
            else
            {
                AnchorWarning=null;warnedMissingAnchor=false;
                if(Math.Abs(time-Planetarium.GetUniversalTime())<.001){lastAnchorPosition=sample.Position;hasAnchorPosition=true;}
            }
            anchorState=new RingAnchorState(sample.Position+CenterOffset,sample.Velocity,sample.Acceleration);anchorTime=time;
            return anchorState;
        }
        private double gravityTime=double.NaN;
        private DVec referenceAcceleration;
        private readonly List<Tuple<DVec,double,double>> gravitySources=new List<Tuple<DVec,double,double>>();
        internal DVec InertialGravity(DVec ringPosition,double time)
        {
            var anchor=AnchorAt(time);var position=ringPosition+anchor.Position;
            var acceleration=-anchor.Acceleration;
            if(gravityTime!=time)
            {
                gravitySources.Clear();gravityTime=time;referenceAcceleration=new DVec();
                // An orbiting star can itself be the saved reference body. Subtract
                // that origin's acceleration as well as the anchor's relative motion.
                var root=Body;var chain=new HashSet<CelestialBody>();
                while(root!=null&&root.orbit!=null&&root.orbit.referenceBody!=null&&root.orbit.referenceBody!=root&&chain.Add(root))root=root.orbit.referenceBody;
                if(root!=null&&root!=Body)
                {
                    RingAnchorState origin;string error;
                    if(RingAnchorEphemeris.TryRelative(RingAnchorEphemeris.Id(Body),root,time,out origin,out error))referenceAcceleration=origin.Acceleration;
                }

                foreach(var body in FlightGlobals.Bodies)
                {
                    RingAnchorState bodyState;string error;
                    if(body.gravParameter<=0||!RingAnchorEphemeris.TryRelative(RingAnchorEphemeris.Id(body),Body,time,out bodyState,out error))continue;
                    gravitySources.Add(Tuple.Create(bodyState.Position,body.Radius,body.gravParameter));
                }
            }
            acceleration-=referenceAcceleration;
            foreach(var source in gravitySources)
            {
                var delta=source.Item1-position;double radius=Math.Max(1,Math.Max(delta.Length,source.Item2));
                acceleration+=delta*(source.Item3/(radius*radius*radius));
            }
            return acceleration;
        }
        internal static CelestialBody OrbitalHost(string anchorId)
        {
            CelestialBody body;
            if(anchorId.StartsWith("body:",StringComparison.Ordinal))body=FlightGlobals.Bodies.Find(b=>RingAnchorEphemeris.Id(b)==anchorId);
            else {var v=FlightGlobals.Vessels.Find(x=>RingAnchorEphemeris.Id(x)==anchorId);body=v==null?null:v.mainBody;}
            if(body==null)return null;
            if(body.isStar)return body;
            var seen=new HashSet<CelestialBody>();
            while(body.orbit!=null&&body.orbit.referenceBody!=null&&body.orbit.referenceBody!=body&&seen.Add(body))body=body.orbit.referenceBody;
            return body;
        }
    }
}

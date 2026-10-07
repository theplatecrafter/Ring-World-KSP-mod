using System;
using System.Collections.Generic;
namespace Ringworld.Core
{
    // Translation-invariant Hermite interpolation of an already integrated coast.
    // This changes display sampling, not the physical prediction or its time steps.
    public struct TrajectorySample
    {
        public readonly double Time;
        public readonly DVec Position,Velocity;
        public TrajectorySample(double time,DVec position,DVec velocity){Time=time;Position=position;Velocity=velocity;}
        public static DVec Interpolate(TrajectorySample a,TrajectorySample b,double time)
        {
            double span=b.Time-a.Time;if(span<=0)return a.Position;
            double u=Math.Max(0,Math.Min(1,(time-a.Time)/span)),u2=u*u,u3=u2*u;
            return a.Position+(b.Position-a.Position)*(3*u2-2*u3)+a.Velocity*(span*(u3-2*u2+u))+b.Velocity*(span*(u3-u2));
        }
        public static int Segment(IList<TrajectorySample> points,double time)
        {
            if(points.Count<2)throw new ArgumentException("A coast needs at least two samples.");
            int lo=0,hi=points.Count-1;
            while(hi-lo>1){int mid=(lo+hi)/2;if(points[mid].Time<=time)lo=mid;else hi=mid;}
            return Math.Min(lo,points.Count-2);
        }
    }
}

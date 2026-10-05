using System;
using System.Collections.Generic;

namespace Ringworld.Core
{
    // A sample relative to its parent, in the same non-rotating Cartesian axes.
    // Null Parent identifies a root. All samples in a query use the same UT.
    public struct AnchorOrbitSample
    {
        public readonly string Parent;
        public readonly RingAnchorState State;
        public AnchorOrbitSample(string parent,RingAnchorState state){Parent=parent;State=state;}
    }

    public static class AnchorEphemeris
    {
        // Stop at the common ancestor BEFORE adding coordinates. Subtracting two
        // positions accumulated all the way to a distant star loses local detail.
        // The callback must use persistent IDs, never display names/list indices.
        public static RingAnchorState Relative(string target,string reference,double time,
            Func<string,double,AnchorOrbitSample> sample)
        {
            if(sample==null)throw new ArgumentNullException(nameof(sample));
            if(string.IsNullOrEmpty(target)||string.IsNullOrEmpty(reference)||!RingParameters.Finite(time))
                throw new ArgumentException("Anchor IDs and a finite universal time are required.");
            if(target==reference)return new RingAnchorState();
            var cache=new Dictionary<string,AnchorOrbitSample>(StringComparer.Ordinal);
            var targetPath=Path(target,time,sample,cache);
            var referencePath=Path(reference,time,sample,cache);
            var targetIds=new HashSet<string>(targetPath,StringComparer.Ordinal);
            string common=null;
            foreach(var id in referencePath)if(targetIds.Contains(id)){common=id;break;}
            if(common==null)throw new InvalidOperationException("Anchor and reference belong to disconnected orbital systems.");
            var a=Sum(targetPath,common,cache);var b=Sum(referencePath,common,cache);
            return new RingAnchorState(a.Position-b.Position,a.Velocity-b.Velocity,a.Acceleration-b.Acceleration);
        }
        private static List<string> Path(string id,double time,Func<string,double,AnchorOrbitSample> sample,
            Dictionary<string,AnchorOrbitSample> cache)
        {
            var path=new List<string>();var visited=new HashSet<string>(StringComparer.Ordinal);
            while(id!=null)
            {
                if(!visited.Add(id))throw new InvalidOperationException("Cycle in anchor orbit hierarchy.");
                path.Add(id);AnchorOrbitSample value;
                if(!cache.TryGetValue(id,out value))
                {
                    value=sample(id,time);
                    if(!Finite(value.State.Position)||!Finite(value.State.Velocity)||!Finite(value.State.Acceleration))
                        throw new InvalidOperationException("Anchor orbit returned a non-finite state.");
                    cache.Add(id,value);
                }
                id=value.Parent;
            }
            return path;
        }
        private static bool Finite(DVec p)
        {return RingParameters.Finite(p.X)&&RingParameters.Finite(p.Y)&&RingParameters.Finite(p.Z);}
        private static RingAnchorState Sum(List<string> path,string common,Dictionary<string,AnchorOrbitSample> cache)
        {
            var p=new DVec();var v=new DVec();var a=new DVec();
            foreach(var id in path)
            {
                if(id==common)break;
                var state=cache[id].State;p+=state.Position;v+=state.Velocity;a+=state.Acceleration;
            }
            return new RingAnchorState(p,v,a);
        }
    }
}

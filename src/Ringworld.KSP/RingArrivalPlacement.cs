using System;
using UnityEngine;
namespace NivenRingworld
{
    // Sandbox placement only. No held position, altered gravity or landing flag:
    // normal contact physics finishes the short drop after collider publication.
    internal static class RingArrivalPlacement
    {
        internal static Ringworld.Core.DVec Refine(Settings settings,Vessel vessel,Vector3d center,Ringworld.Core.DVec position)
        {
            var up=ConvertVector.Unity(settings.Geometry.Up(position));var along=ConvertVector.Unity(settings.Geometry.AlongDirection(position));
            var across=ConvertVector.Unity(settings.Geometry.Axis);var origin=vessel.transform.position;
            float bottom=0,radius=0;
            foreach(var part in vessel.parts)foreach(var collider in part.GetComponentsInChildren<Collider>()){
                if(!collider.enabled||collider.isTrigger)continue;var b=collider.bounds;
                for(int i=0;i<8;i++){var offset=b.center-origin+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));bottom=Mathf.Min(bottom,Vector3.Dot(offset,up));radius=Mathf.Max(radius,Vector3.ProjectOnPlane(offset,up).magnitude);}
            }
            float lift=0;
            for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++){
                var point=origin+along*(x*radius*.5f)+across*(y*radius*.5f);RaycastHit hit;
                if(Physics.Raycast(point+up*1000,-up,out hit,2000,1<<15,QueryTriggerInteraction.Ignore))lift=Mathf.Max(lift,Vector3.Dot(hit.point-origin,up)-bottom+.2f);
            }
            Debug.Log("[NivenRingworld] Gentle arrival loaded-collider clearance lift="+lift);
            return position+settings.Geometry.Up(position)*lift;
        }
        internal static double Height(Settings settings,Vessel vessel,double along,double across,Vector3 oldUp)
        {
            // Transfer places the vessel's root transform, not its mass centre.
            oldUp.Normalize();var origin=vessel.transform.position;
            double bottom=0,radius=0;
            foreach(var part in vessel.parts)if(part!=null)
                foreach(var collider in part.GetComponentsInChildren<Collider>())
                {
                    if(!collider.enabled||collider.isTrigger)continue;
                    var b=collider.bounds;
                    for(int i=0;i<8;i++){
                        var offset=b.center-origin+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                        bottom=Math.Min(bottom,Vector3.Dot(offset,oldUp));
                        radius=Math.Max(radius,Vector3.ProjectOnPlane(offset,oldUp).magnitude);
                    }
                }
            double floor=double.NegativeInfinity;
            for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++){
                var s=settings.Terrain.Sample(along+x*radius*.5,across+y*radius*.5);
                floor=Math.Max(floor,Math.Max(s.Height,s.WaterHeight));
            }
            double height=floor-bottom+.2;
            Debug.Log("[NivenRingworld] Gentle arrival root height="+height+" floor="+floor+" underside offset="+bottom+" footprint="+radius);
            return height;
        }
    }
}

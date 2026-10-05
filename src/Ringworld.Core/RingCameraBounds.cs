using System;
namespace Ringworld.Core
{
    public static class RingCameraBounds
    {
        // Analytic wall clipping works even when a collider tile has not streamed yet.
        public static DVec ConstrainWalls(RingGeometry geometry,DVec target,DVec desired,double clearance,double thickness)
        {
            target=geometry.Basis.ToLocal(target);desired=geometry.Basis.ToLocal(desired);
            double half=geometry.P.Width/2;
            var direction=desired-target;double stop=1;
            for(int side=-1;side<=1;side+=2)
            {
                double tSide=side*target.Y,dSide=side*desired.Y;
                bool inside=tSide<half+thickness/2;
                double boundary=inside?half-clearance:half+thickness+clearance;
                if(inside?dSide<=boundary:dSide>=boundary)continue;
                double denominator=dSide-tSide;
                double fraction=Math.Abs(denominator)>1e-12?(boundary-tSide)/denominator:0;
                fraction=Math.Max(0,Math.Min(1,fraction));
                var at=target+direction*fraction;
                double altitude=geometry.P.Radius-Math.Sqrt(at.X*at.X+at.Z*at.Z);
                if(altitude>=TerrainGenerator.MinimumHeight-thickness-clearance&&altitude<=geometry.P.WallHeight+clearance)
                    stop=Math.Min(stop,fraction);
            }
            var result=target+direction*stop;
            double height=geometry.P.Radius-Math.Sqrt(result.X*result.X+result.Z*result.Z);
            if(height>=TerrainGenerator.MinimumHeight-thickness-clearance&&height<=geometry.P.WallHeight+clearance)
                for(int side=-1;side<=1;side+=2)
                {
                    double projection=side*result.Y;
                    if(projection>half-clearance&&projection<half+thickness+clearance)
                    {
                        double boundary=side*target.Y<half+thickness/2?half-clearance:half+thickness+clearance;
                        result=new DVec(result.X,side*boundary,result.Z);
                    }
                }
            return geometry.Basis.ToWorld(result);
        }
    }
}

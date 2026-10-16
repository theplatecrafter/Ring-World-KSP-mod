using System;
using System.Collections.Generic;
using Ringworld.Core;
static class TrajectoryTests
{
    internal static void Run(Action<bool,string> check)
    {
        var offset=new DVec(150000000000,1605000000,-150000000000);
        var velocity=new DVec(300000,-400,1100);var acceleration=new DVec(-9.72,2,.5);
        var a=new TrajectorySample(12345,offset,velocity);
        var b=new TrajectorySample(12365,offset+velocity*20+acceleration*200,velocity+acceleration*20);
        for(int i=0;i<=200;i++)
        {
            double dt=i*.1;var expected=offset+velocity*dt+acceleration*(dt*dt*.5);
            check((TrajectorySample.Interpolate(a,b,12345+dt)-expected).Length<.0002,"coast interpolation preserves constant-acceleration curve at enlarged ring coordinates");
        }
        check((TrajectorySample.Interpolate(a,b,12000)-a.Position).Length==0,"coast interpolation clamps past endpoint");
        check((TrajectorySample.Interpolate(a,b,13000)-b.Position).Length==0,"coast interpolation clamps future endpoint");
        var points=new List<TrajectorySample>{a,b,new TrajectorySample(12385,b.Position,b.Velocity)};
        check(TrajectorySample.Segment(points,12344)==0,"coast keeps future start");
        check(TrajectorySample.Segment(points,12364)==0,"coast trims inside first interval");
        check(TrajectorySample.Segment(points,12365)==1,"coast drops consumed segment at exact timestamp");
        check(TrajectorySample.Segment(points,12386)==1,"coast handles expired endpoint");
        var still=new TrajectorySample(5,offset,new DVec());
        check((TrajectorySample.Interpolate(still,still,5)-offset).Length==0,"zero-length interval is finite");
        var random=new Random(71013);var g=new RingGeometry(new RingParameters());g.Basis=new RingBasis(17,26,39);
        for(int i=0;i<10000;i++)
        {
            var p=g.Position(random.NextDouble()*g.P.Circumference,(random.NextDouble()-.5)*g.P.Width*1.1,(random.NextDouble()-.5)*1000000);
            var v=new DVec((random.NextDouble()-.5)*800000,(random.NextDouble()-.5)*800000,(random.NextDouble()-.5)*800000);
            double horizon=random.NextDouble()*10000;
            check(g.TimeToArrival(p,v,horizon)==LegacyArrival(g,p,v,horizon),"Allocation-free arrival preserves exact roots/hits for inclined high-speed paths");
        }
        var origin=g.Position(1000000,0,300000);var fall=g.Up(origin)*-1000;
        for(int i=0;i<100;i++)g.TimeToArrival(origin,fall,1000);
        long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)g.TimeToArrival(origin,fall,1000);
        check(GC.GetAllocatedBytesForCurrentThread()==before,"Arrival hot loop allocates no boundary/root arrays");
    }
    private static double LegacyArrival(RingGeometry g,DVec p,DVec velocity,double horizon)
    {
        if(g.InArrivalRegion(p,false))return 0;
        p=g.Basis.ToLocal(p);velocity=g.Basis.ToLocal(velocity);double best=double.PositiveInfinity;
        double a=velocity.X*velocity.X+velocity.Z*velocity.Z,b=p.X*velocity.X+p.Z*velocity.Z,r=Math.Sqrt(p.X*p.X+p.Z*p.Z);
        foreach(double boundary in new[]{g.P.Radius+50000,g.P.Radius-g.P.WallHeight-50000})
        {
            double c=(r-boundary)*(r+boundary),disc=b*b-a*c;if(a<1e-20||disc<0)continue;
            double q=-b-(b>=0?1:-1)*Math.Sqrt(disc);
            foreach(double t in new[]{q/a,Math.Abs(q)>1e-20?c/q:-b/a})
                if(t>=0&&t<=horizon&&g.InArrivalRegion(g.Basis.ToWorld(p+velocity*(t+1e-6)),false))best=Math.Min(best,t);
        }
        if(Math.Abs(velocity.Y)>1e-12)foreach(double y in new[]{-g.P.Width/2-50000,g.P.Width/2+50000})
        {double t=(y-p.Y)/velocity.Y;if(t>=0&&t<=horizon&&g.InArrivalRegion(g.Basis.ToWorld(p+velocity*(t+1e-6)),false))best=Math.Min(best,t);}
        return best;
    }
}

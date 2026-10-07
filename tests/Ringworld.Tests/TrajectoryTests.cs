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
    }
}

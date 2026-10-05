using System;
using Ringworld.Core;
static class PlacementTests
{
    internal static void Run(Action<bool,string> check)
    {
        Ephemeris(check);
        bool invalidGravity=false;try{new RingParameters{Gravity=double.Epsilon}.Validate();}catch(ArgumentException){invalidGravity=true;}
        check(invalidGravity,"underflowed rotation rate is rejected");
        var identity=new RingBasis(0,0,0);var basis=new RingBasis(31,67,-112);
        foreach(var p in new[]{new DVec(1,2,3),new DVec(15300000000,160500000,2000)})
        {
            check((basis.ToLocal(basis.ToWorld(p))-p).Length<.00002,"orientation double-precision roundtrip");
            check((identity.ToWorld(p)-p).Length==0,"legacy orientation identity");
        }
        check(Math.Abs(DVec.Dot(basis.X,basis.Y))<1e-14&&Math.Abs(DVec.Dot(basis.Z,basis.Y))<1e-14,"orthogonal ring basis");
        check((DVec.Cross(basis.X,basis.Y)-basis.Z).Length<1e-14,"right handed ring basis");
        foreach(int sign in new[]{-1,1})
        {
            var parameters=new RingParameters{SpinDirection=sign};var ring=new RingGeometry(parameters){Basis=basis};
            var p=ring.Position(1234567,5000,300);var coordinates=ring.Coordinates(p);
            check(Math.Abs(coordinates.Along-1234567)<.0001&&Math.Abs(coordinates.Across-5000)<.0001&&Math.Abs(coordinates.Altitude-300)<.0001,"tilted ring coordinate roundtrip");
            check(Math.Abs(ring.SpinVelocity(p).Length-Math.Sqrt(parameters.Gravity/parameters.Radius)*(parameters.Radius-300))<1e-8,"spin speed retains artificial gravity");
            check(parameters.RotationSeconds>0,"retrograde period remains positive");
            var ahead=ring.Position(1234568,5000,300)-p;
            check(DVec.Dot(ahead.Unit,ring.AlongDirection(p))>.999999,"terrain longitude tangent is unchanged by reverse spin");
            var localVelocity=new DVec(23,-11,7);
            check((ring.RotatingVelocity(p,ring.InertialVelocity(p,localVelocity))-localVelocity).Length<1e-8,"retrograde flight velocity roundtrip");
            var start=ring.Position(1234567,0,parameters.WallHeight+100000);
            check(!double.IsInfinity(ring.TimeToArrival(start,ring.Up(start)*-1000,200)),"tilted arrival intersection");
            parameters.PanelsEnabled=false;check(ring.Daylight(0,0)==1,"panel-free legacy light has no synthetic night");
        }
        var unrotated=new RingGeometry(new RingParameters());
        var inclined=new RingGeometry(unrotated.P){Basis=basis};
        var eye=unrotated.Position(120000,1000,4000);var ray=(unrotated.Up(eye)+new DVec(0,.3,0)).Unit;
        var sky0=new RingAtmosphere(unrotated).Sky(eye,ray,10,true);
        var sky1=new RingAtmosphere(inclined).Sky(basis.ToWorld(eye),basis.ToWorld(ray),10,true);
        check((sky0.Radiance-sky1.Radiance).Length<1e-5&&Math.Abs(sky0.Opacity-sky1.Opacity)<1e-5,"inclined atmosphere ray intervals match canonical atmosphere");
        var target=unrotated.Position(120000,unrotated.P.Width/2-20,1000);
        var desired=target+new DVec(0,80,0);
        var clip0=RingCameraBounds.ConstrainWalls(unrotated,target,desired,2,100);
        var clip1=RingCameraBounds.ConstrainWalls(inclined,basis.ToWorld(target),basis.ToWorld(desired),2,100);
        check((basis.ToWorld(clip0)-clip1).Length<.00002,"inclined camera wall clearance matches canonical wall");
        var speed=new DVec(12,34,56);
        check((basis.ToWorld(unrotated.Acceleration(eye,speed,1e18))-inclined.Acceleration(basis.ToWorld(eye),basis.ToWorld(speed),1e18)).Length<1e-8,"inclined physical acceleration is rotation invariant");
        var anchor=new RingAnchorState(new DVec(1e10,200,300),new DVec(1000,-2000,30),new DVec(.2,.3,0));
        var point=anchor.Position+basis.X*1500000;var relative=new DVec(12,45,6);
        foreach(double spin in new[]{.003,-.003})
        {
            var inertial=anchor.WorldVelocity(point,relative,basis.Y,spin);
            check((anchor.LocalVelocity(point,inertial,basis.Y,spin)-relative).Length<1e-9,"moving anchor velocity roundtrip both spin directions");
            var acceleration=anchor.RelativeAcceleration(point,new DVec(),anchor.Acceleration,basis.Y,spin);
            check((acceleration-basis.X*(spin*spin*1500000)).Length<1e-8,"anchor acceleration removed; centrifugal magnitude independent of spin sign");
        }
        var origin=new DVec();var sun=new DVec(100,0,0);
        check(RingOcclusion.Sphere(origin,sun,new DVec(50,0,0),5),"body eclipses star");
        check(!RingOcclusion.Sphere(origin,sun,new DVec(120,0,0),5),"body beyond star does not eclipse");
        check(!RingOcclusion.Sphere(origin,sun,new DVec(-20,0,0),5),"body behind observer does not eclipse");
        check(!RingOcclusion.Sphere(origin,sun,new DVec(50,6,0),5),"ray misses body");
        check(RingOcclusion.Box(origin,sun,new DVec(50,0,0),basis,new DVec(1,2,3)),"inclined panel casts shadow");
        check(!RingOcclusion.RingShell(new DVec(0,-100,0),new DVec(0,100,0),origin,identity,20,22,5),"ring hole transmits light");
        check(RingOcclusion.RingShell(origin,sun,origin,identity,20,22,5),"ring hull blocks radial light");
        check(!RingOcclusion.RingShell(new DVec(0,10,0),new DVec(100,10,0),origin,identity,20,22,5),"ray above ring misses finite width");
        check(RingOcclusion.RingShell(basis.ToWorld(origin),basis.ToWorld(sun),origin,basis,20,22,5),"tilted ring occlusion invariant");
        double partial=RingOcclusion.Visibility(origin,sun,10,64,p=>RingOcclusion.Sphere(origin,p,new DVec(50,0,0),2));
        check(partial>0&&partial<1,"finite star partial eclipse");
        check(RingOcclusion.Visibility(origin,sun,10,64,p=>false)==1,"unblocked star fully visible");
        check(RingOcclusion.Visibility(origin,sun,10,64,p=>true)==0,"complete eclipse dark");
    }
    private static void Ephemeris(Action<bool,string> check)
    {
        int calls=0;
        Func<string,double,AnchorOrbitSample> sample=(id,time)=>
        {
            calls++;
            switch(id)
            {
                case "root":return new AnchorOrbitSample(null,new RingAnchorState());
                case "star":return new AnchorOrbitSample("root",new RingAnchorState(new DVec(1e20,0,0),new DVec(1e10,0,0),new DVec(1e9,0,0)));
                case "planet":return new AnchorOrbitSample("star",new RingAnchorState(new DVec(1e11,time*1000,0),new DVec(0,1000,0),new DVec(.2,0,0)));
                case "moon":return new AnchorOrbitSample("planet",new RingAnchorState(new DVec(1000.125,0,0),new DVec(0,2,0),new DVec(0,.03,0)));
                case "asteroid":return new AnchorOrbitSample(time<10?"planet":"moon",new RingAnchorState(new DVec(time*3+.25,0,0),new DVec(3,0,0),new DVec(.01,0,0)));
                default:throw new InvalidOperationException("Missing anchor");
            }
        };
        var at=AnchorEphemeris.Relative("moon","planet",123,sample);
        check(at.Position.X==1000.125&&at.Velocity.Y==2&&at.Acceleration.Y==.03,"common ancestor preserves local precision near distant system");
        check(calls==4,"shared ephemeris ancestors sampled once per query");
        var star=AnchorEphemeris.Relative("moon","star",123,sample);
        check(star.Position.Y==123000&&star.Velocity.Y==1002&&star.Acceleration.X==.2,"hierarchical position velocity and acceleration compose at requested time");
        var reverse=AnchorEphemeris.Relative("planet","moon",123,sample);
        check((at.Position+reverse.Position).Length==0&&(at.Velocity+reverse.Velocity).Length==0,"reference transformation is reversible");
        check(AnchorEphemeris.Relative("asteroid","planet",2,sample).Position.X==6.25,"moving anchor sampled before SOI transition");
        check(AnchorEphemeris.Relative("asteroid","planet",20,sample).Position.X==1060.375,"moving anchor samples changed orbital parent");
        check(AnchorEphemeris.Relative("moon","moon",0,sample).Position.Length==0,"body relative to itself is stationary");
        bool rejected=false;
        try{AnchorEphemeris.Relative("a","b",0,(id,time)=>new AnchorOrbitSample(id,new RingAnchorState()));}catch(InvalidOperationException){rejected=true;}
        check(rejected,"cyclic ephemeris is rejected");rejected=false;
        try{AnchorEphemeris.Relative("a","b",0,(id,time)=>new AnchorOrbitSample(null,new RingAnchorState()));}catch(InvalidOperationException){rejected=true;}
        check(rejected,"disconnected ephemeris is rejected");rejected=false;
        try{AnchorEphemeris.Relative("a","b",0,(id,time)=>new AnchorOrbitSample(null,new RingAnchorState(new DVec(double.NaN,0,0),new DVec(),new DVec())));}catch(InvalidOperationException){rejected=true;}
        check(rejected,"non-finite ephemeris cannot enter physics");
    }
}

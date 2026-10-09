using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Ringworld.Core;

static class Program
{
    static int checks;
    static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
    static void Near(double actual,double expected,double tolerance,string text){Check(Math.Abs(actual-expected)<=tolerance,text+": "+actual+" != "+expected);}
    static void Main(string[] args)
    {
        ResearchTests.Run(Check);
        TrajectoryTests.Run(Check);
        PlacementTests.Run(Check);
        foreach(Biome biome in Enum.GetValues(typeof(Biome)))foreach(bool wet in new[]{false,true}){
            int kind=TerrainSurface.Kind(biome,wet);Check(kind>=0&&kind<TerrainSurface.Count,"Every biome has a surface texture");
        }
        foreach(double radius in new[]{15300000000.0,150000000000.0,1e12}){
            var seamStart=TerrainSurface.NoisePoint(0,100,0,radius);var seamEnd=TerrainSurface.NoisePoint(2*Math.PI*radius,100,0,radius);
            Near((seamEnd-seamStart).Length,0,.01,"Procedural field ring seam");
            var point=TerrainSurface.NoisePoint(1000,-2350,30,radius);
            var originA=TerrainSurface.NoiseOrigin(999,-2350,radius);var originB=TerrainSurface.NoiseOrigin(1001,-2350,radius);
            Near(((point-originA)+originA-(point-originB)-originB).Length,0,.001,"Procedural field chunk-independent position");
            Check((TerrainSurface.NoisePoint(1000+65536,-2350,30,radius)-point).Length>1000,"No 64 km texture-coordinate reset");
        }
        foreach(double radius in new[]{15300000000.0,150000000000.0,1e12}){
            Check(RingExterior.InHullBand(new DVec(radius+1301,0,0),radius,100000,-1300),"Metre-precision exterior classification");
            Check(!RingExterior.InHullBand(new DVec(radius+1299,0,0),radius,100000,-1300),"Interior below sea level preserved");
            Check(!RingExterior.InHullBand(new DVec(radius+1301,100001,0),radius,100000,-1300),"Open edge view preserved");
        }
        foreach(double angle in new[]{0.0,-0.0,Math.PI,2*Math.PI,4*Math.PI,-4*Math.PI,-616194345473.6643,1e100,-1e100,double.MaxValue,-double.MaxValue})
        {
            double reduced=AngleReduction.RadiansInclusive(angle);
            Check(reduced>=0&&reduced<=2*Math.PI,"Bounded orbital angle reduction");
            if(angle>=0&&angle<=2*Math.PI)Near(reduced,angle,0,"Ordinary angle unchanged");
        }
        Near(AngleReduction.RadiansInclusive(4*Math.PI),2*Math.PI,0,"Positive endpoint preserved");
        Near(AngleReduction.RadiansInclusive(-4*Math.PI),0,0,"Negative endpoint preserved");
        Check(double.IsNaN(AngleReduction.RadiansInclusive(double.PositiveInfinity)),"Infinite angle terminates as invalid");
        Check(double.IsNaN(AngleReduction.RadiansInclusive(double.NaN)),"NaN angle remains invalid");
        var p=new RingParameters();var g=new RingGeometry(p);var t=new TerrainGenerator(g);
        foreach(double diameterKm in new[]{200000000.0,300000000.0,2000000000.0})
        {
            var large=new RingParameters{Radius=diameterKm*500,Width=50000000000};large.Validate();
            var chart=new RingGeometry(large);var at=chart.Position(123456789,large.Width/2-200,300);
            Near(chart.Coordinates(at).Altitude,300,.001,"large ring altitude precision");
            Check(RingParameters.Finite(new TerrainGenerator(chart).Sample(123456789,large.Width/2-200).Height),"large ring finite terrain");
        }
        bool rejected=false;try{new RingParameters{Radius=1e25}.Validate();}catch(ArgumentException){rejected=true;}
        Check(rejected,"reject dimensions unable to retain contact precision");
        var candidateKeys=new HashSet<string>();
        for(int j=0;j<1000;j++)foreach(var candidate in ColossusDistribution.Nearby(t,j*2000000,0))
        {
            candidateKeys.Add(candidate.Key);
            foreach(var landmarkSite in t.Landmarks)
            {
                double da=RingGeometry.Wrap(candidate.Along-landmarkSite.Along+p.Circumference/2,p.Circumference)-p.Circumference/2,db=candidate.Across-landmarkSite.Across;
                Check(da*da+db*db>=600000.0*600000,"rare colossi exclude landmark cluster");
            }
        }
        Check(candidateKeys.Count>250&&candidateKeys.Count<650,"rare seeded occupancy across 3000 sampled cells");
        var first=new List<ColossusCandidate>(ColossusDistribution.Nearby(t,12345678,0,2000000,1));
        var wrapped=new List<ColossusCandidate>(ColossusDistribution.Nearby(t,12345678+p.Circumference,0,2000000,1));
        Check(first.Count==wrapped.Count,"colossus wrap count");
        for(int j=0;j<first.Count;j++){Check(first[j].Key==wrapped[j].Key,"colossus wrap identity");Near(first[j].Along,wrapped[j].Along,0,"colossus repeatable position");}
        int forestSamples=0;
        for(int j=0;j<1000;j++)
        {
            double a=j*17931,b=(j%23-11)*17000;var ground=t.Sample(a,b);
            var look=BiomePresentation.Sample(t,a,b,ground,64,1);
            Check(look.CanopyCover>=0&&look.CanopyCover<=1&&look.CanopyHeight>=0&&look.CanopyHeight<=48,"bounded biome representation");
            if(look.CanopyCover>.9){forestSamples++;Check(BiomePresentation.ForestMargin(t,a,b)>0,"near and distant forest share stand mask");}
            Near(BiomePresentation.Sample(t,a,b,ground,64,0).CanopyHeight,0,0,"disabled forest has no canopy relief");
            Near(BiomePresentation.Sample(t,a,b,ground,380.001,1).CanopyCover,BiomePresentation.Sample(t,a,b,ground,379.999,1).CanopyCover,1e-8,"forest colour continuous across filter threshold");
            ground.Biome=Biome.Road;Near(BiomePresentation.Sample(t,a,b,ground,64,1).CanopyCover,0,0,"no forest canopy on roads");
        }
        Check(forestSamples>0,"forest coverage fixture exists");
        foreach(int side in new[]{-1,1})
        {
            var target=g.Position(234567,side*(p.Width/2-10),5);
            var desired=g.Position(234567,side*(p.Width/2+50),5);
            var clipped=RingCameraBounds.ConstrainWalls(g,target,desired,2,100);
            Near(side*clipped.Y,p.Width/2-2,1e-5,"camera stops inside rim wall including clip radius");
            var overlap=g.Position(234567,side*(p.Width/2-.2),5);
            Near(side*RingCameraBounds.ConstrainWalls(g,overlap,desired,2,100).Y,p.Width/2-2,1e-5,"camera resolves initial wall overlap");
            var highTarget=g.Position(234567,side*(p.Width/2-10),p.WallHeight+100);
            var highDesired=g.Position(234567,side*(p.Width/2+50),p.WallHeight+100);
            Near((RingCameraBounds.ConstrainWalls(g,highTarget,highDesired,2,100)-highDesired).Length,0,.00001,"camera can pass over wall");
        }
        var unbounded=TerrainLodPlan.Create(0,0,1024,3,double.MaxValue,p.Width/2,p.Circumference/2,Math.Sqrt(8*p.Radius*250)*8);
        Check(unbounded.Count>0&&unbounded.Count<100000,"unlimited preference generates finite unique ring domain");
        foreach(var b in unbounded)Check(b.Y<p.Width/2&&b.Y+b.Size>-p.Width/2,"LOD plan culls beyond walls before subdivision");

        // Weather has deterministic UT evolution, continuous interval boundaries and explicit clear overrides.
        double weatherMin=1,weatherMax=0;
        for(int i=0;i<1000;i++)
        {
            double ut=i*113.7;var w=RingWeather.Sample(t,123400,5600,ut,.45,true,600,1,.4);
            var again=RingWeather.Sample(t,123400,5600,ut,.45,true,600,1,.4);
            Near(w.Severity,again.Severity,0,"weather deterministic");
            var next=RingWeather.Sample(t,123400,5600,ut+.001,.45,true,600,1,.4);
            Check(Math.Abs(w.Severity-next.Severity)<.0001,"weather continuous in UT");
            weatherMin=Math.Min(weatherMin,w.Severity);weatherMax=Math.Max(weatherMax,w.Severity);
            Check(w.Cloud>=0&&w.Cloud<=1&&w.Rain>=0&&w.Rain<=1&&w.Storm>=0&&w.Storm<=1,"weather bounded");
            if(w.Rain>0)Check(w.Cloud>.81,"precipitation begins after overcast coverage threshold");
            var noStorm=RingWeather.Sample(t,123400,5600,ut,.45,true,600,1,0);Near(noStorm.Storm,0,0,"zero storm fraction");
            var clear=RingWeather.Sample(t,123400,5600,ut,0,true,600,1,1);Near(clear.Cloud+clear.Rain+clear.Storm,0,0,"forced clear weather");
        }
        Check(weatherMin<.30&&weatherMax>.85,"weather spans fair skies and storms");
        var wetWeather=new WeatherSample{Cloud=1,Rain=1,Storm=1};
        Check(RingWeather.SurfaceKind(wetWeather,Biome.Snow,0,1)=="Blizzard","snow biome storm phase");
        Check(RingWeather.SurfaceKind(wetWeather,Biome.Desert,0,1)=="Blowing dust","dry storm presentation");
        Check(RingWeather.SurfaceKind(new WeatherSample{Cloud=.8},Biome.Forest,.99,0)=="Rim-shadow fog","humid shadow rim fog");
        Check(RingWeather.SurfaceKind(new WeatherSample(),Biome.Snow,0,1)=="Clear","cold alone does not precipitate");
        var storm=RingWeather.Sample(t,123400,5600,0,1,false,600,1,.4);Check(storm.Rain>.99&&storm.Storm>.99,"fixed storm fixture");
        for(int i=1;i<30;i++)Near(RingWeather.Sample(t,123400,5600,i*600-.00001,.45,true,600,1,.4).Severity,RingWeather.Sample(t,123400,5600,i*600+.00001,.45,true,600,1,.4).Severity,.00001,"weather interval seam");
        // Physical reference cases independent of the flight implementation.
        var zero=RibbonGravity.Acceleration(new DVec(),p,1e6);
        Near(zero.Length,0,1e-12,"ribbon centre symmetry");
        double axisY=p.Radius*.4,lo=-p.Width/2-axisY,hi=p.Width/2-axisY;
        double expectedAxis=2*Math.PI*RibbonGravity.G*1e6*p.Radius*(1/Math.Sqrt(p.Radius*p.Radius+lo*lo)-1/Math.Sqrt(p.Radius*p.Radius+hi*hi));
        Near(RibbonGravity.Acceleration(new DVec(0,axisY,0),p,1e6).Y,expectedAxis,1e-12,"analytic finite-width axial field");
        var nearFloor=new DVec(p.Radius-10000,0,0);
        var sheet=RibbonGravity.Acceleration(nearFloor,p,1e6);
        Near(sheet.X,2*Math.PI*RibbonGravity.G*1e6,8e-6,"near floor infinite-sheet limit (curvature correction below 2%)");
        foreach(var point in new[]{nearFloor,new DVec(p.Radius+100,0,0),new DVec(p.Radius-50,p.Width/2,0),new DVec(p.Radius*.5,p.Width,0)})
        {
            var baseField=RibbonGravity.Acceleration(point,p,1e6);var refined=RibbonGravity.Acceleration(point,p,1e6,50,4);
            Check((baseField-refined).Length<Math.Max(1e-10,refined.Length*2e-4),"ribbon quadrature convergence");
            var mirrored=RibbonGravity.Acceleration(new DVec(point.X,-point.Y,0),p,1e6);
            Near(mirrored.Y,-baseField.Y,1e-12,"ribbon axial reflection");
        }
        var distant=new DVec(p.Radius*100,0,0);double totalMass=2*Math.PI*p.Radius*p.Width*1e6;
        Near(RibbonGravity.Acceleration(distant,p,1e6).X,-RibbonGravity.G*totalMass/(distant.Length*distant.Length),1e-12,"far field point-mass limit");
        Func<DVec,DVec,DVec> kepler=(pos,vel)=>pos*(-1/Math.Pow(pos.Length,3));
        var orbit=new FlightState(new DVec(1,0,0),new DVec(0,0,1));
        for(int i=0;i<1000;i++)orbit=NumericalFlight.Step(orbit,2*Math.PI/1000,kepler);
        Check((orbit.Position-new DVec(1,0,0)).Length<1e-8,"RK4 closed Kepler orbit reference");
        p.SurfaceDensity=1e6;
        var rotatingState=new FlightState(nearFloor,new DVec(0,1,0));
        var inertialState=new FlightState(nearFloor,g.InertialVelocity(nearFloor,rotatingState.Velocity));
        double initialMomentum=DVec.Cross(inertialState.Position,inertialState.Velocity).Y;
        for(int i=0;i<100;i++)
        {
            rotatingState=NumericalFlight.Step(rotatingState,.1,(pos,vel)=>g.Acceleration(pos,vel,1e18));
            inertialState=NumericalFlight.Step(inertialState,.1,(pos,vel)=>pos*(-1e18/Math.Pow(pos.Length,3))+RibbonGravity.Acceleration(pos,p,p.SurfaceDensity));
        }
        Check((g.ToInertialPosition(rotatingState.Position,10)-inertialState.Position).Length<.01,"rotating and inertial numerical predictions agree");
        Near(DVec.Cross(inertialState.Position,inertialState.Velocity).Y/initialMomentum,1,1e-10,"axisymmetric field conserves angular momentum");
        p.SurfaceDensity=0;
        Check(TerrainLodPlan.Create(0,0,1024,3,2000000000).Count<2200,"two-million-km bounded LOD plan");
        var ecologyTerrain=new TerrainGenerator(g){GenerationVersion=4};var climateKinds=new HashSet<Biome>();
        for(int i=0;i<1000;i++)
        {
            double a=i*179381.71,b=Math.Sin(i*.73)*p.Width*.48;
            var climate=Ecology.Sample(ecologyTerrain,a,b);var closeClimate=Ecology.Sample(ecologyTerrain,a+1,b);
            Near(climate.Desert+climate.Meadow+climate.Forest+climate.Cold+climate.Highland,1,1e-12,"normalized climate weights");
            Check((climate.Ground-closeClimate.Ground).Length<.001,"smooth biome color at one metre");
            Check(Math.Abs(climate.Relief-closeClimate.Relief)<.001,"smooth terrain relief blend");
            Near(climate.TreeCover,Ecology.Sample(ecologyTerrain,a+p.Circumference,b).TreeCover,1e-8,"periodic climate map");
            climateKinds.Add(climate.Dominant);
            var ground=ecologyTerrain.Sample(a,b);
            Check(RingParameters.Finite(ground.Height)&&ground.Height<=200,"gentle ordinary v4 relief away from landmarks");
        }
        Check(climateKinds.Count==5,"all five climate families distributed by seed");
        var newTerrain=new TerrainGenerator(g){GenerationVersion=3};int pondCount=0;
        long pondPeriod=Math.Max(2,(long)Math.Round(p.Circumference/4096));
        for(int i=0;i<400;i++)
        {
            double along=(i+.2+.6*newTerrain.Scatter(i,0,523))*p.Circumference/pondPeriod;
            double across=(.2+.6*newTerrain.Scatter(i,0,527))*4096;
            var sample=newTerrain.Sample(along,across);if(sample.Wet&&sample.Biome==Biome.Lake)pondCount++;
            Near(sample.Height,newTerrain.Sample(along+p.Circumference,across).Height,.002,"v3 global seam precision within two millimetres");
            Check(RingParameters.Finite(sample.Height),"v3 finite height");
        }
        Check(pondCount>2,"v3 small ponds exist");
        Near(g.Position(0,0,0).Length,p.Radius,.00001,"radius");
        Near(p.Omega*p.Omega*p.Radius,p.Gravity,1e-10,"centrifugal acceleration");
        Near(p.RotationSeconds,2*Math.PI*Math.Sqrt(p.Radius/p.Gravity),1e-7,"spin period");
        foreach(int n in new[]{1,2,16,32})
        {
            var indices=GroundShell.Triangles(n);var edges=new Dictionary<string,int>();
            Check(indices.Length==12*n*n+24*n,"closed shell triangle count");
            for(int i=0;i<indices.Length;i+=3)for(int j=0;j<3;j++)
            {
                int a=indices[i+j],b=indices[i+(j+1)%3];
                Check(a>=0&&a<2*(n+1)*(n+1)&&a!=b,"valid shell edge");
                string key=a+":"+b;edges[key]=edges.ContainsKey(key)?edges[key]+1:1;
            }
            foreach(var edge in edges)
            {var pair=edge.Key.Split(':');string reverse=pair[1]+":"+pair[0];Check(edge.Value==1&&edges.ContainsKey(reverse)&&edges[reverse]==1,"watertight consistently wound shell");}
        }
        // LOD partitions must cover every requested point exactly once outside fine
        // collision tiles, including negative coordinates and a tile-boundary crossing.
        foreach(double center in new[]{0.0,-1735.0,p.Circumference*.03125,1023.99,1024.01})
        {
            var blocks=TerrainLodPlan.Create(center,1700,1024,3);var keys=new HashSet<string>();
            Check(blocks.Count>30&&blocks.Count<800,"bounded terrain LOD budget");
            foreach(var block in blocks)Check(keys.Add(block.Key),"unique LOD blocks");
            for(int yy=-15;yy<=15;yy++)for(int xx=-15;xx<=15;xx++)
            {
                double a=center+xx*8201.3+37,c=1700+yy*7103.7+19;
                double nx=Math.Floor(center/1024)*1024,ny=1024;
                bool fine=a>=nx-3072&&a<nx+4096&&c>=ny-3072&&c<ny+4096;
                int hits=0;foreach(var block in blocks)if(a>=block.X&&a<block.X+block.Size&&c>=block.Y&&c<block.Y+block.Size)hits++;
                Check(hits==(fine?0:1),"LOD cover without overlap or gaps");
            }
        }
        var random=new Random(1970);
        var air=new RingAtmosphere(g);
        Near(air.Sample(g.Position(1000,0,0)).PressureKPa,101.325,.1,"sea-level dry-air pressure");
        Near(air.Sample(g.Position(1000,p.Width,1000)).Density,0,0,"no air outside rim");
        Near(air.Sample(g.Position(1000,0,-10)).Density,0,0,"no air behind scrith");
        Check(g.InArrivalRegion(g.Position(0,0,200000),false),"arrival before atmosphere and rim");
        Check(!g.InArrivalRegion(g.Position(0,0,240000),false)&&g.InArrivalRegion(g.Position(0,0,240000),true),"entry-exit hysteresis");
        Near(g.TimeToArrival(g.Position(0,0,300000),new DVec(10000,0,0),20),9,.00001,"swept fast approach");
        Check(double.IsPositiveInfinity(g.TimeToArrival(g.Position(0,0,300000),new DVec(-10000,0,0),20)),"receding vessel is not arriving");
        var ordinary=g.RotatingVelocity(g.Position(0,0,100000),new DVec(0,0,-10000));
        Check(ordinary.Length>370000,"arrival must not erase unmatched orbital speed");
        for(int i=0;i<100;i++)
        {
            double epoch=random.NextDouble()*1e8,elapsed=random.NextDouble()*100000;
            g.OrientationRadians=p.Omega*epoch;
            var x=g.Position(1234567,421,12345);var v=new DVec(47,18,-300);
            var inertial=g.ToInertialPosition(x,elapsed);
            Near((RingGeometry.Rotate(inertial,-p.Omega*elapsed)-x).Length,0,.00002,"position chart inverse");
            var iv=g.ToInertialVelocity(x,v,elapsed);
            Near((g.RotatingVelocity(x,RingGeometry.Rotate(iv,-p.Omega*elapsed))-v).Length,0,1e-8,"velocity phase inverse");
            Near(g.AlongDistance(g.Coordinates(x).Along,1234567),0,.01,"material longitude phase");
            Near(air.CloudCoverage(1234567,4500,epoch),air.CloudCoverage(1234567+p.Circumference,4500,epoch),.000001,"cloud seam periodicity");
        }
        g.OrientationRadians=0;
        var sky=air.Sky(g.Position(p.Circumference*.025,0,100),g.Up(g.Position(p.Circumference*.025,0,100)),0);
        Check(sky.Opacity>0&&sky.Opacity<1,"finite zenith optical depth");
        Check(sky.Radiance.Z>sky.Radiance.X,"Rayleigh blue sky");
        var vacuum=air.Sky(g.Position(0,0,300000),g.Up(g.Position(0,0,300000)),0);
        Near(vacuum.Opacity,0,0,"vacuum ray toward central star");
        var fromSpace=air.Sky(g.Position(p.Circumference*.025,0,300000),-g.Up(g.Position(p.Circumference*.025,0,300000)),0);
        Check(fromSpace.Opacity>0,"atmosphere visible from orbital approach");
        for(int i=0;i<2000;i++)
        {
            double a=random.NextDouble()*p.Circumference,b=(random.NextDouble()-.5)*p.Width,h=random.NextDouble()*60000;
            var pos=g.Position(a,b,h);var c=g.Coordinates(pos);
            Near(g.AlongDistance(c.Along,a),0,.00004,"double precision along roundtrip");Near(c.Across,b,1e-8,"across roundtrip");Near(c.Altitude,h,.00001,"altitude roundtrip");
            var force=g.Acceleration(pos,new DVec(),0);
            Check(DVec.Dot(force,g.Up(pos))<0,"gravity must push toward inner floor");
            Near(force.Length,p.Gravity*(1-h/p.Radius),1e-10,"height-dependent g");
            var v=new DVec(random.NextDouble()*100,17,-34);
            Check((g.RotatingVelocity(pos,g.InertialVelocity(pos,v))-v).Length<1e-8,"frame velocity roundtrip");
            var coriolis=g.Acceleration(pos,v,0)-g.Acceleration(pos,new DVec(),0);
            Near(DVec.Dot(coriolis,v),0,1e-10,"Coriolis must do no work");
            var sample=t.Sample(a,b);var again=t.Sample(a,b);
            Near(sample.Height,again.Height,0,"terrain determinism");
            Check(RingParameters.Finite(sample.Height),"finite terrain");
            Near(t.Sample(a+p.Circumference,b).Height,sample.Height,.0001,"periodic terrain");
        }
        for(int i=0;i<100;i++)
        {
            double across=(random.NextDouble()-.5)*p.Width;
            Near(t.Sample(-.001,across).Height,t.Sample(p.Circumference-.001,across).Height,.0001,"circumference seam");
        }
        long catchments=(long)Math.Round(p.Circumference/131072);
        double riverAlong=(12345.5)*p.Circumference/catchments;
        for(int i=-50;i<50;i++)
        {
            double boundary=(i+.5)*131072;
            var left=t.Sample(riverAlong,boundary-.001);var right=t.Sample(riverAlong,boundary+.001);
            Near(left.Height,right.Height,.001,"catchment boundary continuity");
            Near(left.WaterHeight,right.WaterHeight,.001,"catchment water continuity");
        }
        var waterSite=t.Landmarks.Find(l=>l.Id=="waterway");
        Check(t.Sample(waterSite.Along,waterSite.Across).Wet,"waterway destination reaches a lake");
        // A long horizon grows by LOD levels, not by tiling the whole area finely.
        var horizon=TerrainLodPlan.Create(0,0,1024,3,160000000);
        Check(horizon.Count<1600,"160000 km horizon has bounded patch count");
        Check(horizon.Exists(b=>b.Size>16000000),"distant horizon coarsens to continental blocks");
        foreach(double distance in new[]{300000.0,1000000,20000000,159000000})
            Check(horizon.Exists(b=>b.X<=distance&&b.X+b.Size>distance&&b.Y<=0&&b.Y+b.Size>0),"long horizon coverage");
        var clearAir=new RingAtmosphere(g).Sky(g.Position(0,0,100),g.Up(g.Position(0,0,100)),0);
        Check(clearAir.Opacity<.5,"clear overhead air transmits the distant ring and Sun");
        // A stationary object becomes a freely falling trajectory inward in the inertial frame.
        var initial=g.Position(0,0,1000);var position=initial;var velocity=new DVec();double dt=.002;
        for(int i=0;i<5000;i++)
        {
            // Midpoint integration captures the Coriolis deflection without explicit Euler energy drift.
            var acceleration=g.Acceleration(position,velocity,0);
            var middleP=position+velocity*(dt/2);var middleV=velocity+acceleration*(dt/2);
            position+=middleV*dt;velocity+=g.Acceleration(middleP,middleV,0)*dt;
        }
        Near(g.Coordinates(position).Altitude,1000-.5*p.Gravity*100,0.01,"ten second drop");
        Check(g.AlongDistance(g.Coordinates(position).Along,0)<0,"drop deflects antispinward");
        Near(g.Density(p.AtmosphereHeight),0,0,"atmosphere cutoff");Check(g.Density(0)>g.Density(20000),"density falls with height");
        Near(g.Daylight(0,0),g.Daylight(0,p.DaySeconds),1e-12,"day periodicity");Near(g.Daylight(0,0),0,1e-12,"square centered on observer causes night");
        Near(g.Daylight(0,p.DaySeconds/2),1,1e-12,"gap between squares causes day");
        for(int i=0;i<20;i++)Near(g.Daylight(i*p.Circumference/20,0),0,1e-10,"all twenty shadow centers");
        foreach(var l in t.Landmarks)
        {
            if(l.Kind=="city"||l.Kind=="outpost"||l.Kind=="terminal"||l.Kind=="scrith")
            {var s=t.Sample(l.Along,l.Across);Near(s.Height,l.Height,1e-6,"level landmark pad "+l.Id);Check(!s.Wet,"dry landmark "+l.Id);}
        }
        for(int selection=0;selection<100;selection++)
        {
            RingPoint randomSite,repeatSite;
            Check(TerrainExploration.TryChoose(ecologyTerrain,selection,out randomSite),"random exploration site found");
            Check(TerrainExploration.TryChoose(ecologyTerrain,selection,out repeatSite),"repeat exploration search");
            Check(!ecologyTerrain.Sample(randomSite.Along,randomSite.Across).Wet,"random exploration dry");
            Near(randomSite.Along,repeatSite.Along,0,"repeatable exploration selection");
            Check(Math.Abs(randomSite.Across)<p.Width/2-4999,"exploration avoids wall");
        }
        // The resolved terrain must be continuous across tile borders, independent of generation order.
        var site=t.Landmarks[0];var timer=Stopwatch.StartNew();
        for(int y=0;y<224;y++)for(int x=0;x<224;x++)t.Sample(site.Along+x*32,site.Across+y*32);
        timer.Stop();
        Console.WriteLine("PASS: "+checks+" checks; 50,176 terrain samples in "+timer.ElapsedMilliseconds+" ms.");
        Console.WriteLine("Radius: "+p.Radius+" m; width: "+p.Width+" m; spin: "+p.RotationSeconds.ToString("F2")+" s; rim speed: "+(p.Omega*p.Radius).ToString("F2")+" m/s.");
        if(args.Length>0)Export(t,args[0]);
    }
    static void Export(TerrainGenerator terrain,string dir)
    {
        Directory.CreateDirectory(dir);var origin=terrain.Landmarks[0];int n=240;double size=32000;
        string[] colors={"#074159","#186478","#267f92","#5d7046","#849453","#385d37","#baa373","#79746c","#e5ecec","#586a76","#ad9f83","#6d7885","#615e53"};
        using(var w=new StreamWriter(Path.Combine(dir,"terrain-preview.svg")))
        {
            w.WriteLine("<svg xmlns='http://www.w3.org/2000/svg' width='1200' height='1320' viewBox='0 0 1200 1320'><rect width='1200' height='1320' fill='#111c25'/><text x='40' y='52' fill='white' font-family='sans-serif' font-size='26'>Ringworld | generated terrain near the expedition outpost</text><text x='40' y='85' fill='#bac8cf' font-family='sans-serif' font-size='18'>32 km square â€¢ same C# terrain sampler as the KSP plugin â€¢ plan view</text>");
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                var sample=terrain.Sample(origin.Along+(x/(double)n-.5)*size,origin.Across+(y/(double)n-.5)*size);
                double brightness=.58+Math.Min(.42,sample.Height/1800);
                string color=colors[(int)(sample.Wet?Biome.River:sample.Biome)];
                w.WriteLine(string.Format(CultureInfo.InvariantCulture,"<rect x='{0}' y='{1}' width='5' height='5' fill='{2}'/><rect x='{0}' y='{1}' width='5' height='5' fill='black' opacity='{3:F2}'/>",x*5,110+y*5,color,1-brightness));
            }
            w.WriteLine("<circle cx='600' cy='710' r='12' fill='none' stroke='white' stroke-width='3'/><text x='620' y='715' fill='white' font-family='sans-serif' font-size='18'>Landing field</text></svg>");
        }
    }
}

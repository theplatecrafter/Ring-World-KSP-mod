namespace NivenRingworld
{
    /// <summary>Read-only integration point for flight instruments and other mods.</summary>
    public static class RingworldSurfaceApi
    {
        public const int Version=5;

        /// <summary>Sample the loaded ring at a Unity world position. Context disambiguates
        /// the current rotating frame; unavailable for packed vessels or other rings.
        /// Gravity is effective stationary-frame acceleration, not central-body attraction.</summary>
        public static bool TryGetEnvironmentAtPosition(Vessel context,Vector3d worldPosition,out RingworldPointEnvironment state)
        {
            state=default(RingworldPointEnvironment);
            var f=RingworldFlight.Instance;
            if(f==null||!f.enabled||context==null||context.packed||!f.Owns(context))return false;
            var p=ConvertVector.Core(worldPosition-f.Center);
            if(double.IsNaN(p.Length)||double.IsInfinity(p.Length)||!f.Settings.Geometry.InArrivalRegion(p,true))return false;
            var s=f.Settings;var c=s.Geometry.Coordinates(p);var terrain=s.Terrain.Sample(c.Along,c.Across);
            var air=new Ringworld.Core.RingAtmosphere(s.Geometry).Sample(p);
            state=new RingworldPointEnvironment {
                RingId=s.RingId,Along=c.Along,Across=c.Across,Altitude=c.Altitude,
                HeightAboveTerrain=c.Altitude-terrain.Height,Biome=terrain.Biome.ToString(),
                EffectiveGravity=ConvertVector.Ksp(f.Acceleration(p,new Ringworld.Core.DVec())),
                SurfaceUp=ConvertVector.Ksp(s.Geometry.Up(p)),
                AirDensity=s.Atmosphere?air.Density:0,PressureKPa=s.Atmosphere?air.PressureKPa:0,
                TemperatureKelvin=air.Temperature,WaterDepth=terrain.Wet?System.Math.Max(0,terrain.WaterHeight-c.Altitude):0,
                // Fresh-water reference density; visual waves do not change hydrostatics.
                WaterDensity=terrain.Wet&&c.Altitude<terrain.WaterHeight?1000:0
            };
            return true;
        }

        /// <summary>Ring air/weather for optional effects and instruments. Same scope and units as the surface API.
        /// Pressure is kPa, temperature kelvin, density kg/m3; cloud/rain/storm/light are 0..1.
        /// These are local ring fields, not properties of HostBody (which may be a star).</summary>
        public static bool TryGetEnvironmentState(Vessel vessel,out RingworldEnvironmentState state)
        {
            state=default(RingworldEnvironmentState);
            RingworldSurfaceState surface;if(!TryGetSurfaceState(vessel,out surface))return false;
            var f=RingworldFlight.Instance;var s=f.Settings;
            var air=new Ringworld.Core.RingAtmosphere(s.Geometry).Sample(f.Position(vessel));
            var weather=s.Weather(surface.Along,surface.Across,Planetarium.GetUniversalTime());
            bool atmosphere=s.Atmosphere&&air.Density>0;
            state=new RingworldEnvironmentState
            {
                RingId=surface.RingId,HasAtmosphere=atmosphere,
                Density=atmosphere?air.Density:0,PressureKPa=atmosphere?air.PressureKPa:0,
                TemperatureKelvin=air.Temperature,SpeedOfSound=atmosphere?air.SoundSpeed:0,
                Mach=atmosphere?surface.SurfaceRelativeVelocity.magnitude/air.SoundSpeed:0,
                Daylight=s.Geometry.Daylight(surface.Along,Planetarium.GetUniversalTime()),
                CloudCover=atmosphere?weather.Cloud:0,Rain=atmosphere&&s.RainEnabled?weather.Rain:0,
                Storm=atmosphere?weather.Storm:0
            };
            return true;
        }

        /// <summary>
        /// Returns false outside a restored, unpacked ring frame, including transfers.
        /// Vectors use the current Unity world axes, not stock orbital axes.
        /// Call on Unity's main thread and refresh every physics tick; never cache a frame.
        /// </summary>
        public static bool TryGetSurfaceState(Vessel vessel,out RingworldSurfaceState state)
        {
            state=default(RingworldSurfaceState);
            var flight=RingworldFlight.Instance;
            if(flight==null||!flight.enabled||flight.Settings==null||vessel==null||vessel.packed||!flight.Owns(vessel))return false;
            var geometry=flight.Settings.Geometry;
            var position=flight.Position(vessel);var coordinates=geometry.Coordinates(position);
            var terrain=flight.Settings.Terrain.Sample(coordinates.Along,coordinates.Across);
            state=new RingworldSurfaceState
            {
                HostBody=flight.Star,RingId=flight.Settings.RingId,RingName=flight.Settings.RingName,Center=flight.Center,
                Along=coordinates.Along,Across=coordinates.Across,
                Altitude=coordinates.Altitude,
                TerrainElevation=terrain.Height,WaterElevation=terrain.WaterHeight,OverWater=terrain.Wet,
                SurfaceRelativeVelocity=ConvertVector.Ksp(flight.Velocity(vessel)),
                FrameAcceleration=ConvertVector.Ksp(flight.Acceleration(position,flight.Velocity(vessel))),
                StationaryFrameAcceleration=ConvertVector.Ksp(flight.Acceleration(position,new Ringworld.Core.DVec())),
                SurfaceUp=ConvertVector.Ksp(geometry.Up(position)),
                FrameEpoch=flight.FrameEpoch,
                TangentialSpeed=geometry.P.Omega*geometry.P.Radius,
                Biome=terrain.Biome.ToString()
            };
            return true;
        }
    }

    /// <summary>SI units except pressure (kPa). Buoyancy force per displaced cubic metre
    /// is -EffectiveGravity*density in newtons; subtract the gas/payload weight separately.</summary>
    public struct RingworldPointEnvironment
    {
        public string RingId,Biome;
        public double Along,Across,Altitude,HeightAboveTerrain,AirDensity,PressureKPa,TemperatureKelvin,WaterDepth,WaterDensity;
        public Vector3d EffectiveGravity,SurfaceUp;
        public Vector3d AirBuoyancyPerCubicMetre {get{return -EffectiveGravity*AirDensity;}}
        public Vector3d WaterBuoyancyPerCubicMetre {get{return -EffectiveGravity*WaterDensity;}}
    }

    public struct RingworldEnvironmentState
    {
        public string RingId {get;internal set;}
        public bool HasAtmosphere {get;internal set;}
        public double Density {get;internal set;}
        public double PressureKPa {get;internal set;}
        public double TemperatureKelvin {get;internal set;}
        public double SpeedOfSound {get;internal set;}
        public double Mach {get;internal set;}
        public double Daylight {get;internal set;}
        public double CloudCover {get;internal set;}
        public double Rain {get;internal set;}
        public double Storm {get;internal set;}
    }

    /// <summary>Metres, seconds and metres/second. Altitude is above the ring datum.</summary>
    public struct RingworldSurfaceState
    {
        public CelestialBody HostBody { get; internal set; }
        public string RingId { get; internal set; }
        public string RingName { get; internal set; }
        public Vector3d Center { get; internal set; }
        public double Along { get; internal set; }
        public double Across { get; internal set; }
        public double Altitude { get; internal set; }
        public double TerrainElevation { get; internal set; }
        public double WaterElevation { get; internal set; }
        public bool OverWater { get; internal set; }
        public Vector3d SurfaceRelativeVelocity { get; internal set; }
        /// <summary>m/s² in Unity world axes: ring-frame inertial/gravity terms at current velocity.
        /// Excludes thrust, drag, buoyancy and contact. Not a spherical body's gravity.</summary>
        public Vector3d FrameAcceleration { get; internal set; }
        /// <summary>Same acceleration for a stationary point in the ring frame.
        /// A hover controller can oppose this, then regulate ring-relative velocity.</summary>
        public Vector3d StationaryFrameAcceleration { get; internal set; }
        public Vector3d SurfaceUp { get; internal set; }
        public double FrameEpoch { get; internal set; }
        public double TangentialSpeed { get; internal set; }
        public string Biome { get; internal set; }
    }
}

using System;
using HarmonyLib;
using Ringworld.Core;

namespace NivenRingworld
{
    internal static class RingAir
    {
        internal static bool Applies(Vessel v)
        {return StockIntegration.Applies(v)&&RingworldFlight.Instance.Settings.Atmosphere;}
        internal static bool ContainsOxygen(Vessel v)
        {return Applies(v)&&Sample(v).Density>0;}
        internal static AirSample Sample(Vessel v)
        {
            var f=RingworldFlight.Instance;
            return new RingAtmosphere(f.Settings.Geometry).Sample(f.Position(v));
        }
    }
    [HarmonyPatch(typeof(FlightIntegrator),"CalculatePressure")]
    internal static class RingPressurePatch
    {
        private static bool Prefix(FlightIntegrator __instance)
        {
            var v=__instance.GetComponent<Vessel>();if(!RingAir.Applies(v))return true;
            var f=RingworldFlight.Instance;var air=RingAir.Sample(v);
            __instance.altitude=f.Settings.Geometry.Coordinates(f.Position(v)).Altitude;
            __instance.staticPressurekPa=air.PressureKPa;__instance.staticPressureAtm=air.PressureKPa/101.325;
            return false;
        }
    }
    [HarmonyPatch(typeof(FlightIntegrator),"CalculateConstantsAtmosphere")]
    internal static class RingAirConstantsPatch
    {
        private static bool Prefix(FlightIntegrator __instance,ref CelestialBody ___currentMainBody)
        {
            var v=__instance.GetComponent<Vessel>();if(!RingAir.Applies(v))return true;
            var f=RingworldFlight.Instance;var air=RingAir.Sample(v);var fi=__instance;
            fi.spd=f.Velocity(v).Length;
            v.atmDensity=fi.density=air.Density;
            v.atmosphericTemperature=fi.atmosphericTemperature=air.Temperature;
            v.dynamicPressurekPa=fi.dynamicPressurekPa=.0005*air.Density*fi.spd*fi.spd;
            v.speedOfSound=air.SoundSpeed;v.mach=fi.mach=fi.spd/air.SoundSpeed;
            fi.convectiveMachLerp=Math.Pow(Math.Max(0,Math.Min(1,(fi.mach-PhysicsGlobals.NewtonianMachTempLerpStartMach)/(PhysicsGlobals.NewtonianMachTempLerpEndMach-PhysicsGlobals.NewtonianMachTempLerpStartMach))),PhysicsGlobals.NewtonianMachTempLerpExponent);
            // Use stock dry-air shock/convection constants, scoped to this integrator
            // for these synchronous calculations. No CelestialBody is modified.
            var original=___currentMainBody;
            try
            {
                ___currentMainBody=FlightGlobals.GetHomeBody();
                v.externalTemperature=fi.externalTemperature=Math.Max(air.Temperature,fi.CalculateShockTemperature());
                v.convectiveCoefficient=fi.convectiveCoefficient=fi.CalcConvectiveCoefficient(v.situation);
            }
            finally {___currentMainBody=original;}
            fi.pseudoReynolds=air.Density*fi.spd;
            fi.pseudoReLerpTimeMult=1/(PhysicsGlobals.TurbulentConvectionEnd-PhysicsGlobals.TurbulentConvectionStart);
            fi.pseudoReDragMult=PhysicsGlobals.DragCurvePseudoReynolds.Evaluate((float)fi.pseudoReynolds);
            RingSolarFlux.Apply(fi,v,Math.Exp(-air.Density*.04));
            return false;
        }
    }
    [HarmonyPatch(typeof(FlightIntegrator),"DragCubeSetupAndPartAeroStats")]
    internal static class RingPartAirPatch
    {
        private static void Postfix(Vessel v)
        {
            if(!RingAir.Applies(v))return;
            var f=RingworldFlight.Instance;var atmosphere=new RingAtmosphere(f.Settings.Geometry);
            foreach(var part in v.parts)
            {
                var position=ConvertVector.Core((Vector3d)part.transform.position-f.Center);
                var air=atmosphere.Sample(position);
                part.atmDensity=air.Density;part.staticPressureAtm=air.PressureKPa/101.325;
            }
        }
    }
}

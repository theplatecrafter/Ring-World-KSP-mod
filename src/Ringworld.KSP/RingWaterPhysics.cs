using System;
using HarmonyLib;
using UnityEngine;
namespace NivenRingworld
{
    // Scoped cylindrical water state. Never mark the host star as an ocean body.
    [HarmonyPatch(typeof(PartBuoyancy),"FixedUpdate")]
    internal static class RingWaterPhysics
    {
        internal static double Fraction(double depth,double halfHeight)
        {return Math.Max(0,Math.Min(1,(depth+halfHeight)/(2*Math.Max(.05,halfHeight))));}
        internal static double Lift(double volume,double fraction,double coefficient,double gravity)
        {return Math.Max(0,volume)*fraction*Math.Max(0,coefficient)*gravity;}
        private static bool Prefix(PartBuoyancy __instance,Part ___part)
        {
            var p=___part;var f=RingworldFlight.Instance;
            if(p==null||f==null||!f.Owns(p.vessel))return true;
            if(p.vessel.packed||p.rb==null||p.rb.isKinematic)return false;
            var rb=p.rb;var pos=ConvertVector.Core((Vector3d)p.transform.position-f.Center);
            var coord=f.Settings.Geometry.Coordinates(pos);var terrain=f.Settings.Terrain.Sample(coord.Along,coord.Across);
            var up=ConvertVector.Unity(f.Settings.Geometry.Up(pos));
            var size=p.DragCubes.WeightedSize;var center=p.DragCubes.WeightedCenter;
            if(size.sqrMagnitude<.0001f){size=Vector3.one;center=Vector3.zero;}
            var half=size*.5f;float extent=Mathf.Abs(Vector3.Dot(p.transform.TransformVector(Vector3.right*half.x),up))+Mathf.Abs(Vector3.Dot(p.transform.TransformVector(Vector3.up*half.y),up))+Mathf.Abs(Vector3.Dot(p.transform.TransformVector(Vector3.forward*half.z),up));
            var worldCenter=p.transform.TransformPoint(center);
            // Dry terrain uses -infinity as its water sentinel. Do not expose
            // that sentinel through stock physics fields consumed by modules.
            double waterLevel=terrain.Wet?terrain.WaterHeight:coord.Altitude-extent-1;
            double depth=waterLevel-coord.Altitude-Vector3.Dot(worldCenter-p.transform.position,up);
            double fraction=terrain.Wet?Fraction(depth,extent):0;
            if(p.buoyancyUseSine)fraction=.5-.5*Math.Cos(fraction*Math.PI);
            p.submergedPortion=__instance.submergedPortion=fraction;
            p.WaterContact=fraction>0;
            __instance.wasSplashed=__instance.splashed;__instance.splashed=fraction>0;
            // Stock updates the vessel immediately on immersion; contact polling
            // alone may not run again until the next collision or scene event.
            if(fraction>0)p.vessel.Splashed=true;
            else if(__instance.wasSplashed)p.vessel.checkSplashed();
            __instance.waterLevel=waterLevel;__instance.depth=depth;__instance.maxDepth=depth+extent;__instance.minDepth=depth-extent;
            __instance.centerOfBuoyancy=worldCenter;__instance.centerOfDisplacement=worldCenter;
            // KSP rigidbody mass and ocean density use tonnes. Fresh water is 1 t/m^3.
            var scale=p.transform.lossyScale;double volume=Math.Abs(size.x*size.y*size.z*scale.x*scale.y*scale.z);
            __instance.displacement=volume;
            double displaced=volume*fraction*Math.Max(0,p.buoyancy)*PhysicsGlobals.BuoyancyScalar;
            __instance.buoyantGeeForce=displaced;
            var force=up*(float)(Lift(volume,fraction,p.buoyancy,f.Settings.Geometry.P.Gravity)*PhysicsGlobals.BuoyancyScalar*p.vessel.gravityMultiplier*PhysicsGlobals.GraviticForceMultiplier);
            __instance.effectiveForce=force;__instance.lastBuoyantForce=force;__instance.lastForcePosition=worldCenter;
            if(fraction>0)rb.AddForceAtPosition(force,worldCenter,ForceMode.Force);
            return false;
        }
    }
}

#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using System.Collections.Generic;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
 internal static class WaterPhysicsSmoke
 {
  internal static IEnumerator Run(RingworldFlight f,Action<string> fail)
  {
   var v=FlightGlobals.ActiveVessel;var origin=ConvertVector.Core((Vector3d)v.transform.position-f.Center);var velocity=f.Velocity(v);var rotation=v.transform.rotation;
   var c=f.Settings.Geometry.Coordinates(origin);DVec target=new DVec();bool found=false;
   for(int i=0;i<40000&&!found;i++)
   {
    double a=c.Along+(i%200-100)*500,b=c.Across+(i/200-100)*500;var t=f.Settings.Terrain.Sample(a,b);
    if(t.Wet&&t.WaterHeight-t.Height>12){target=f.Settings.Geometry.Position(a,b,t.WaterHeight-5);found=true;}
   }
   if(!found){fail("Water physics fixture missing");yield break;}
   var coefficients=new Dictionary<Part,float>();foreach(var p in v.parts){coefficients[p]=p.buoyancy;p.buoyancy=0;}
   try
   {
    v.SetPosition(f.Center+ConvertVector.Ksp(target),true);v.SetWorldVelocity(Vector3d.zero);RingCollisionFrame.Reset(v);
    for(int i=0;i<10;i++)yield return new WaitForFixedUpdate();
    bool wet=false;double force=0;foreach(var p in v.parts){wet|=p.submergedPortion>0;if(p.partBuoyancy!=null)force+=p.partBuoyancy.effectiveForce.magnitude;}
    double vertical=DVec.Dot(f.Velocity(v),f.Settings.Geometry.Up(f.Position(v)));
    if(!wet||!v.Splashed||force>.01||vertical>=-.1){fail("Water immersion/sinking failed wet="+wet+" splashed="+v.Splashed+" force="+force+" vertical="+vertical);yield break;}
    foreach(var p in v.parts)p.buoyancy=2;
    for(int i=0;i<5;i++)yield return new WaitForFixedUpdate();
    force=0;foreach(var p in v.parts)if(p.partBuoyancy!=null)force+=p.partBuoyancy.effectiveForce.magnitude;
    if(force<=0){fail("Water displacement force absent");yield break;}
    // A displacement-calibrated test hull checks sustained surface settling.
    // All forces remain active; no gravity reduction or pose holding is used.
    foreach(var part in v.parts)
    {
     if(part.rb==null)continue;
     var size=part.DragCubes.WeightedSize;var scale=part.transform.lossyScale;
     double volume=Math.Abs(size.x*size.y*size.z*scale.x*scale.y*scale.z);
     if(volume>.001)part.buoyancy=(float)(2*part.rb.mass/(volume*PhysicsGlobals.BuoyancyScalar));
    }
    var sea=f.Settings.Geometry.Coordinates(target);
    var water=f.Settings.Terrain.Sample(sea.Along,sea.Across).WaterHeight;
    v.SetPosition(f.Center+ConvertVector.Ksp(f.Settings.Geometry.Position(sea.Along,sea.Across,water)),true);
    v.SetWorldVelocity(Vector3d.zero);RingCollisionFrame.Reset(v);
    int partCount=v.parts.Count;
    for(int i=0;i<1500;i++)
    {
     yield return new WaitForFixedUpdate();
     if(v==null||v.parts.Count!=partCount){fail("Water settling lost parts");yield break;}
     var pos=f.Settings.Geometry.Coordinates(f.Position(v));
     if(double.IsNaN(pos.Altitude)||Math.Abs(pos.Altitude-water)>30){fail("Water settling left surface: "+pos.Altitude+" water="+water);yield break;}
    }
    var settledSpeed=f.Velocity(v).Length;
    if(settledSpeed>5){fail("Water settling speed remains excessive: "+settledSpeed);yield break;}
    Debug.Log("[RingworldSmoke] WATER sustained 1500-tick displacement-calibrated hull float passed; speed="+settledSpeed+" parts="+partCount);
    Debug.Log("[RingworldSmoke] WATER PHYSICS immersion fields, SPLASHED, zero-buoyancy sinking and displacement force passed; sink="+vertical+" lift="+force);
   }
   finally
   {
    foreach(var entry in coefficients)if(entry.Key!=null)entry.Key.buoyancy=entry.Value;
    if(v!=null){v.SetPosition(f.Center+ConvertVector.Ksp(origin),true);v.SetRotation(rotation,false);v.SetWorldVelocity(ConvertVector.Ksp(velocity));RingCollisionFrame.Reset(v);}
   }
  }
 }
}
#endif

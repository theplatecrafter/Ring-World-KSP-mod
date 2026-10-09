#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;
namespace NivenRingworld
{
    internal static class AirshipIssueSmoke
    {
        internal static IEnumerator Run(RingworldFlight f)
        {
            var vessel=FlightGlobals.ActiveVessel;var host=vessel.rootPart;
            RingworldPointEnvironment env;
            if(!RingworldSurfaceApi.TryGetEnvironmentAtPosition(vessel,vessel.GetWorldPos3D(),out env))throw new Exception("Airship fixture lacks local atmosphere");
            var hlType=AccessTools.TypeByName("HLAirships.HLEnvelopePartModule");
            var heiType=AccessTools.TypeByName("WildBlueIndustries.WBIModuleStaticLift");
            if(hlType==null||heiType==null)throw new Exception("Install HL Airships and Heisenberg for the issue audit");
            foreach(var partName in new[]{"HL.AirshipEnvelope.Octo","hl10Medium2"}){
                var info=PartLoader.getPartInfoByName(partName)??PartLoader.getPartInfoByName(partName.Replace('.','_'));if(info==null)throw new Exception("Airship part missing: "+partName);
                ConfigNode config=null;foreach(var n in info.partConfig.GetNodes("MODULE"))if(n.GetValue("name")=="HLEnvelopePartModule")config=n.CreateCopy();
                if(config==null)throw new Exception("Airship envelope config missing: "+partName);
                config.SetValue("envelopeHasAnimation",false,true);config.SetValue("dragDeployed",0,true);config.SetValue("envelopeVolume",250,true);config.SetValue("envelopeVolumeScale",1,true);config.SetValue("specificVolumeFractionEnvelope",.5,true);
                var module=host.AddModule(config);module.OnStart(PartModule.StartState.Flying);
                try{
                    if(!RingworldSurfaceApi.TryGetEnvironmentAtPosition(vessel,vessel.GetWorldPos3D(),out env))throw new Exception("Airship sample unavailable");
                    AccessTools.Method(hlType,"envelopeUpdate").Invoke(module,null);
                    float density=Convert.ToSingle(AccessTools.Field(hlType,"atmosDensity").GetValue(module));
                    var lift=(Vector3d)(Vector3)AccessTools.Field(hlType,"maxBuoyancy").GetValue(module);
                    double expected=env.AirDensity*env.EffectiveGravity.magnitude*250/1000;
                    if(density<=0||Math.Abs(density-env.AirDensity)>env.AirDensity*.03||Vector3d.Dot(lift.normalized,env.SurfaceUp)<.99||Math.Abs(lift.magnitude-expected)>expected*.05)
                        throw new Exception("Airship lift mismatch: "+partName+" density="+density+" lift="+lift+" expected="+expected);
                    Debug.Log("[RingworldSmoke] AIRSHIP installed "+partName+" actual envelopeUpdate density="+density+" upward lift="+lift.magnitude+" kN");
                    // Cache a ring chart, not Unity coordinates: KSP can shift
                    // the floating origin when this active craft moves 10 km.
                    var root=ConvertVector.Core((Vector3d)vessel.transform.position-f.Center);
                    double epoch=f.FrameEpoch;var rotation=vessel.transform.rotation;
                    try{
                        RingVesselPose.Set(vessel,f.Center+ConvertVector.Ksp(root+f.Settings.Geometry.Up(root)*10000),rotation);vessel.SetWorldVelocity(Vector3d.zero);Physics.SyncTransforms();
                        // KSP caches CoMD and part WCoM until precalculation.
                        // Let native physics refresh both before asking a
                        // module to sample the moved craft at its new altitude.
                        for(int i=0;i<5;i++)yield return new WaitForFixedUpdate();
                        RingworldPointEnvironment high;if(!RingworldSurfaceApi.TryGetEnvironmentAtPosition(vessel,vessel.GetWorldPos3D(),out high))throw new Exception("High airship sample unavailable");
                        AccessTools.Method(hlType,"envelopeUpdate").Invoke(module,null);
                        float highDensity=Convert.ToSingle(AccessTools.Field(hlType,"atmosDensity").GetValue(module));
                        var highLift=(Vector3d)(Vector3)AccessTools.Field(hlType,"maxBuoyancy").GetValue(module);
                        double highExpected=high.AirDensity*high.EffectiveGravity.magnitude*250/1000;
                        if(highDensity>=density||Math.Abs(highDensity-high.AirDensity)>high.AirDensity*.03||Math.Abs(highLift.magnitude-highExpected)>highExpected*.05||Vector3d.Dot(highLift.normalized,high.SurfaceUp)<.99)
                            throw new Exception("Airship did not follow local altitude density/gravity: altitude="+high.Altitude+" density="+highDensity+" expectedDensity="+high.AirDensity+" lowDensity="+density+" lift="+highLift.magnitude+" expectedLift="+highExpected);
                        Debug.Log("[RingworldSmoke] AIRSHIP 10 km altitude "+partName+" density="+highDensity+" upward lift="+highLift.magnitude+" kN gravity="+high.EffectiveGravity.magnitude);
                    }finally{
                        double angle=f.Settings.Geometry.P.Omega*(f.FrameEpoch-epoch);
                        RingVesselPose.Set(vessel,f.Center+ConvertVector.Ksp(f.Settings.Geometry.RotateAroundAxis(root,angle)),f.Settings.AxisRotation(angle)*rotation);
                        vessel.SetWorldVelocity(Vector3d.zero);Physics.SyncTransforms();RingCollisionFrame.Reset(vessel);
                    }
                }finally{host.RemoveModule(module);}
                for(int i=0;i<5;i++)yield return new WaitForFixedUpdate();
            }
            var native=host.AddModule("WBIModuleStaticLift");native.OnStart(PartModule.StartState.Flying);
            try{
                if(!RingworldSurfaceApi.TryGetEnvironmentAtPosition(vessel,vessel.GetWorldPos3D(),out env))throw new Exception("Heisenberg sample unavailable");
                AccessTools.Field(heiType,"currentVolume").SetValue(native,375000d);
                double force=(double)AccessTools.Method(heiType,"CalculateLiftForce",Type.EmptyTypes).Invoke(native,null);
                double gas=Convert.ToDouble(AccessTools.Field(heiType,"liftGasDensity").GetValue(native));
                double expected=(env.AirDensity-gas)*375000*env.EffectiveGravity.magnitude/1000000;
                if(force<=0||Math.Abs(force-expected)>expected*.05)throw new Exception("Heisenberg native lift rejected ring atmosphere: "+force+" expected="+expected);
                Debug.Log("[RingworldSmoke] AIRSHIP native Heisenberg CalculateLiftForce="+force+" kN");
            }finally{host.RemoveModule(native);}
            Debug.Log("[RingworldSmoke] PASS installed HL/Heisenberg atmospheric lift outputs");
        }
    }
}
#endif

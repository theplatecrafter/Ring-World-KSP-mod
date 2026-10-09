using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
namespace NivenRingworld
{
    // Optional producer bridge: use the procedural part's actual generated
    // cavity volume rather than its small template drag cube. No mass or
    // buoyancy coefficient is calibrated here.
    internal static class RingProceduralHullCompatibility
    {
        static readonly Type type=AccessTools.TypeByName("SunkWorks.Structural.WBIModuleProceduralHull");
        static readonly FieldInfo generated=type==null?null:AccessTools.Field(type,"generatedVolume");
        static readonly FieldInfo beamField=type==null?null:AccessTools.Field(type,"beam"),lengthField=type==null?null:AccessTools.Field(type,"hullLength"),depthField=type==null?null:AccessTools.Field(type,"hullDepth");
        static readonly MethodInfo point=type==null?null:AccessTools.Method(type,"ToPartLocal",new[]{typeof(float),typeof(float),typeof(float)});
        static readonly bool available=generated!=null&&point!=null&&beamField!=null&&lengthField!=null&&depthField!=null&&RingAdapterOptions.Enabled("aquaticModules");
        sealed class Geometry{internal double Volume;internal float Beam,Length,Depth;internal Vector3 Size,Center;}
        static readonly ConditionalWeakTable<PartModule,Geometry> geometry=new ConditionalWeakTable<PartModule,Geometry>();
        internal static bool TryBounds(Part part,out Vector3 size,out Vector3 center,out double volume)
        {
            size=center=Vector3.zero;volume=0;
            if(!available)return false;
            foreach(var module in part.Modules)if(type.IsInstanceOfType(module)){
                volume=Convert.ToDouble(generated.GetValue(module));if(!(volume>0)||double.IsInfinity(volume))return false;
                float beam=Convert.ToSingle(beamField.GetValue(module));
                float length=Convert.ToSingle(lengthField.GetValue(module));
                float depth=Convert.ToSingle(depthField.GetValue(module));
                var cached=geometry.GetValue(module,key=>new Geometry());
                if(cached.Volume==volume&&cached.Beam==beam&&cached.Length==length&&cached.Depth==depth){size=cached.Size;center=cached.Center;return true;}
                var first=(Vector3)point.Invoke(module,new object[]{0f,0f,0f});var bounds=new Bounds(first,Vector3.zero);
                for(int i=0;i<8;i++)bounds.Encapsulate((Vector3)point.Invoke(module,new object[]{(i&1)==0?-beam*.5f:beam*.5f,(i&2)==0?-depth:0f,(i&4)==0?-length*.5f:length*.5f}));
                size=bounds.size;center=bounds.center;
                if(!(size.sqrMagnitude>.001f)||float.IsInfinity(size.sqrMagnitude))return false;
                cached.Volume=volume;cached.Beam=beam;cached.Length=length;cached.Depth=depth;cached.Size=size;cached.Center=center;return true;
            }
            return false;
        }
    }
}

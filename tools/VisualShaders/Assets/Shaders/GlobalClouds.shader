Shader "NivenRingworld/GlobalClouds"
{
 SubShader { Tags { "Queue"="Transparent-20" "RenderType"="Transparent" }
 // Seen both from orbit and from the floor. The analytical hull test below
 // hides the shell through the exterior; back-face culling hid it when landed.
 Pass { Cull Off ZWrite Off Offset -2, -2 Blend SrcAlpha OneMinusSrcAlpha
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 5.0
 #include "UnityCG.cginc"
#include "RingEclipse.cginc"
 #include "CloudField.cginc"
 #include "RingHullOcclusion.cginc"
 float2 _CloudShellSize;float _RingCameraRelative;float3 _RingCameraLocal;float4x4 _RingToChart;
 float _CircumferenceKm,_WidthKm,_SeedLow,_SeedHigh,_Generation;
 #include "RingTerrainNoise.cginc"
 float4 _Size,_Local,_CloudHandoff,_WeatherState,_FrontDrift;
 float _PanelsDisabled;
 float _DayPhase,_LocalAmount,_SegmentLength;float4 _LocalChart;
 struct a {float4 vertex:POSITION;float2 uv:TEXCOORD0;float2 field:TEXCOORD1;float2 chart:TEXCOORD2;float2 macro:TEXCOORD3;};
 struct v {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float2 field:TEXCOORD1;float2 chart:TEXCOORD2;float2 macro:TEXCOORD3;float3 local:TEXCOORD4;};
 v vert(a i){v o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.field=i.field;o.chart=i.chart;o.macro=i.macro;o.local=_RingCameraRelative>.5?mul((float3x3)_RingToChart,i.vertex.xyz)+_RingCameraLocal:i.vertex.xyz;return o;}
 float4 frag(v i):SV_Target
 {
  float3 cameraLocal=_RingCameraRelative>.5?_RingCameraLocal:mul(unity_WorldToObject,float4(_WorldSpaceCameraPos,1)).xyz;
  if(ringHullOccludes(cameraLocal,i.local,_CloudShellSize.x,_CloudShellSize.y))discard;
  float front=noise(i.uv+_FrontDrift.xy,800,1213);
  float regional=saturate(_WeatherState.w+(front-.5)*.7),wet=1-_WeatherState.z;
  float target=regional<=wet?.7*regional/max(.001,wet):.7+.3*(regional-wet)/max(.001,_WeatherState.z);
  float severity=lerp(_WeatherState.x,target,_WeatherState.y);
  // Approximately half of the distant surface has cloud cover; local weather remains unrestricted.
  float amount=.5+(severity-.5)*.1;
  float sector=round(i.chart.x)-_LocalChart.x;sector-=round(sector/16384)*16384;
  float2 delta=float2(sector*_SegmentLength+i.chart.y-_LocalChart.y,i.field.y*512000-_LocalChart.z);
  float distanceToLocal=length(float3(delta,5500-_LocalChart.w));
  float near=1-smoothstep(_CloudHandoff.x,max(_CloudHandoff.x+1,_CloudHandoff.y),distanceToLocal);
  near*=_Local.w;
  // The local renderer uses the observer's weather sample. Match that sample across
  // the transition, then return gradually to each distant region's own weather.
  amount=lerp(amount,_LocalAmount,_Local.w*(1-smoothstep(_CloudHandoff.y,_CloudHandoff.y*2+1,distanceToLocal)));
  float footprint=max(length(ddx(i.macro)),length(ddy(i.macro)));
  float cover=cloudCoverageAt(float2(i.field.x,0)+_CoverageOrigin.xy,i.macro+_CoverageOrigin.zw,amount,footprint);
  float c=cover*cover;
  float opacity=1-(1-.3*c)*(1-.4*c)*(1-.3*c);
  opacity=1-pow(max(.001,1-opacity),1-near);
  float phase=frac(20*i.uv.x-_DayPhase),edge=min(phase,1-phase);
  float daylight=ringEclipse(i.uv);
  float3 color=float3(.94,.97,1)*(.60+.35*cover)*(.07+.93*daylight);
  return float4(color,opacity);
 }
 ENDCG
 }}
}

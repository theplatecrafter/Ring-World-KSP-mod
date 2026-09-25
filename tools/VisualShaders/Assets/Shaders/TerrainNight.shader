Shader "NivenRingworld/TerrainNight"
{
 Properties { _MainTex("Terrain colour",2D)="white"{} }
 SubShader { Tags { "RenderType"="Opaque" }
 Pass {
 Cull Back ZWrite On Offset -1, -1
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 #include "RingHullOcclusion.cginc"
 float3 _RingScaledCenter;float2 _RingScaledSize;
 sampler2D _MainTex;float _RingNightPhase;
 struct a {float4 vertex:POSITION;float2 uv:TEXCOORD0;float2 longitude:TEXCOORD1;};
 struct v {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float longitude:TEXCOORD1;float3 world:TEXCOORD2;};
 v vert(a i){v o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.longitude=i.longitude.x;o.world=mul(unity_ObjectToWorld,i.vertex).xyz;return o;}
 float4 frag(v i):SV_Target
 {
  if(ringHullOccludes(_WorldSpaceCameraPos-_RingScaledCenter,i.world-_RingScaledCenter,_RingScaledSize.x,_RingScaledSize.y))discard;
  float phase=frac(20*i.longitude-_RingNightPhase),edge=min(phase,1-phase);
  return float4(tex2D(_MainTex,i.uv).rgb*lerp(.08,1,saturate((edge-.138307)/.02)),1);
 }
 ENDCG
 }}
}

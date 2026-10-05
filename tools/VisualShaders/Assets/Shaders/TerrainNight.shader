Shader "NivenRingworld/TerrainNight"
{
 Properties { _MainTex("Terrain colour",2D)="white"{} }
 SubShader { Tags { "RenderType"="Opaque" }
 Pass {
 Cull Back ZWrite On Offset -1, -1
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 5.0
 #include "UnityCG.cginc"
 #include "RingEclipse.cginc"
 #include "RingHullOcclusion.cginc"
 float3 _RingBasisX,_RingBasisY,_RingBasisZ;
 float3 ringLocal(float3 p){return float3(dot(p,_RingBasisX),dot(p,_RingBasisY),dot(p,_RingBasisZ));}
 float3 _RingScaledCenter;float2 _RingScaledSize;
 sampler2D _MainTex;float _RingNightPhase,_RingPanelsDisabled;
 struct a {float4 vertex:POSITION;float2 uv:TEXCOORD0;float2 longitude:TEXCOORD1;};
 struct v {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float2 longitude:TEXCOORD1;float3 world:TEXCOORD2;};
 v vert(a i){v o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.longitude=i.longitude;o.world=mul(unity_ObjectToWorld,i.vertex).xyz;return o;}
 float4 frag(v i):SV_Target
 {
  if(ringHullOccludes(ringLocal(_WorldSpaceCameraPos-_RingScaledCenter),ringLocal(i.world-_RingScaledCenter),_RingScaledSize.x,_RingScaledSize.y))discard;

  return float4(tex2D(_MainTex,i.uv).rgb*lerp(.08,1,ringEclipse(i.longitude)),1);
 }
 ENDCG
 }}
}

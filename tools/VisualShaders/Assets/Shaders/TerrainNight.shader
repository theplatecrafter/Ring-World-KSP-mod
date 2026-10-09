Shader "NivenRingworld/TerrainNight"
{
 Properties { _MainTex("Terrain colour",2D)="white"{} _SurfaceDetailEnabled("Surface textures enabled",Float)=0 _SurfaceMetresPerUnit("Physical metres per scene unit",Float)=6000 }
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
 #include "TerrainDetail.cginc"
 float3 _RingBasisX,_RingBasisY,_RingBasisZ;
 float3 ringLocal(float3 p){return float3(dot(p,_RingBasisX),dot(p,_RingBasisY),dot(p,_RingBasisZ));}
 float3 _RingScaledCenter;float2 _RingScaledSize;
 sampler2D _MainTex;float _RingNightPhase,_RingPanelsDisabled;
 float _RingCameraExterior;
 float _SurfaceMetresPerUnit;
 struct a {float4 vertex:POSITION;float2 uv:TEXCOORD0;float2 longitude:TEXCOORD1;float4 detail:TEXCOORD2;float4 weights:TEXCOORD3;float3 noise:TEXCOORD4;};
 struct v {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float2 longitude:TEXCOORD1;float3 world:TEXCOORD2;float4 detail:TEXCOORD3;float4 weights:TEXCOORD4;float3 noise:TEXCOORD5;};
 v vert(a i){v o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.longitude=i.longitude;o.world=mul(unity_ObjectToWorld,i.vertex).xyz;o.detail=i.detail;o.weights=i.weights;o.noise=i.noise;return o;}
 float4 frag(v i):SV_Target
 {
  if(_RingCameraExterior>.5||ringHullOccludes(ringLocal(_WorldSpaceCameraPos-_RingScaledCenter),ringLocal(i.world-_RingScaledCenter),_RingScaledSize.x,_RingScaledSize.y))discard;

  return float4(tex2D(_MainTex,i.uv).rgb*(terrainDetail(i.detail,i.weights,i.noise,distance(i.world,_WorldSpaceCameraPos)*_SurfaceMetresPerUnit).b*2)*lerp(.08,1,ringEclipse(i.longitude)),1);
 }
 ENDCG
 }}
}

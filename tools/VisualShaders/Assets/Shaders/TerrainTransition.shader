Shader "NivenRingworld/TerrainTransition"
{
 Properties
 {
  _MainTex("Terrain colour",2D)="white"{}
  _Color("Daylight tint",Color)=(1,1,1,1)
  _Glossiness("Smoothness",Range(0,1))=0.08
  _LightingFade("Lighting transition metres",Vector)=(60000,200000,0,0)
  _AlbedoOnly("Diagnostic albedo",Float)=0
  _SurfaceDetailEnabled("Surface textures enabled",Float)=0
 }
 SubShader
 {
  Tags { "RenderType"="Opaque" }
  // Surface shader generates both forward and deferred passes, so installed
  // Deferred can light close terrain without changing the mesh generator.
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows vertex:detailVertex
  #pragma target 3.5
  #include "TerrainDetail.cginc"
  sampler2D _MainTex;fixed4 _Color;half _Glossiness;float4 _LightingFade;float _AlbedoOnly;
  struct surfaceVertex {float4 vertex:POSITION;float3 normal:NORMAL;float4 tangent:TANGENT;float4 texcoord:TEXCOORD0;float4 texcoord1:TEXCOORD1;float4 texcoord2:TEXCOORD2;float4 texcoord3:TEXCOORD3;float3 noise:TEXCOORD4;};
  struct Input { float2 uv_MainTex;float3 worldPos;float4 detailCoordinates;float4 surfaceWeights;float3 noisePosition; };
  void detailVertex(inout surfaceVertex v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.detailCoordinates=v.texcoord2;o.surfaceWeights=v.texcoord3;o.noisePosition=v.noise;}
  void surf(Input i,inout SurfaceOutputStandard o)
  {
   float fade=smoothstep(_LightingFade.x,_LightingFade.y,distance(i.worldPos,_WorldSpaceCameraPos));
   fade=max(fade,_AlbedoOnly);
   float4 detail=terrainDetail(i.detailCoordinates,i.surfaceWeights,i.noisePosition,distance(i.worldPos,_WorldSpaceCameraPos));
   fixed3 colour=tex2D(_MainTex,i.uv_MainTex).rgb*_Color.rgb*(detail.b*2);
   float2 normal=(detail.rg*2-1)*(1-fade);o.Normal=normalize(float3(normal,sqrt(saturate(1-dot(normal,normal)))));
   o.Albedo=colour*(1-fade);o.Emission=colour*fade;
   o.Metallic=0;o.Smoothness=_Glossiness*(1-fade);o.Alpha=1;
  }
  ENDCG
 }
 Fallback "Standard"
}

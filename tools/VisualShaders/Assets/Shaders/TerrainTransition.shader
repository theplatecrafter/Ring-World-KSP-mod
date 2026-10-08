Shader "NivenRingworld/TerrainTransition"
{
 Properties
 {
  _MainTex("Terrain colour",2D)="white"{}
  _Color("Daylight tint",Color)=(1,1,1,1)
  _Glossiness("Smoothness",Range(0,1))=0.08
  _LightingFade("Lighting transition metres",Vector)=(60000,200000,0,0)
  _AlbedoOnly("Diagnostic albedo",Float)=0
 }
 SubShader
 {
  Tags { "RenderType"="Opaque" }
  // Surface shader generates both forward and deferred passes, so installed
  // Deferred can light close terrain without changing the mesh generator.
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows
  #pragma target 3.0
  sampler2D _MainTex;fixed4 _Color;half _Glossiness;float4 _LightingFade;float _AlbedoOnly;
  struct Input { float2 uv_MainTex;float3 worldPos; };
  void surf(Input i,inout SurfaceOutputStandard o)
  {
   float fade=smoothstep(_LightingFade.x,_LightingFade.y,distance(i.worldPos,_WorldSpaceCameraPos));
   fade=max(fade,_AlbedoOnly);
   fixed3 colour=tex2D(_MainTex,i.uv_MainTex).rgb*_Color.rgb;
   o.Albedo=colour*(1-fade);o.Emission=colour*fade;
   o.Metallic=0;o.Smoothness=_Glossiness*(1-fade);o.Alpha=1;
  }
  ENDCG
 }
 Fallback "Standard"
}

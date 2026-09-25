Shader "NivenRingworld/CloudDeck"
{
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" }
 Pass { Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 5.0
 #include "UnityCG.cginc"
 #include "CloudField.cginc"
 float _Extent,_Daylight;float4 _CloudHandoff;
 struct a {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 struct v {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;float3 world:TEXCOORD1;};
 v vert(a i){v o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.color=i.color;o.world=mul(unity_ObjectToWorld,i.vertex).xyz;return o;}
 float4 frag(v i):SV_Target
 {
  float cover=cloudCoverage((i.uv*2-1)*_Extent);
  float shade=lerp(.98,.69,smoothstep(.65,.81,_CloudAmount))+.02*cover;
   // Circular fragment fade removes the square sheet silhouette at every mesh LOD.
   float edge=1-smoothstep(.60,.94,length(i.uv*2-1));
  float local=1-smoothstep(_CloudHandoff.x,max(_CloudHandoff.x+1,_CloudHandoff.y),distance(i.world,_WorldSpaceCameraPos));
  float opacity=1-pow(max(.001,1-i.color.a*cover*edge),local);
  return float4(float3(.94,.97,1)*shade*(.10+.90*_Daylight)+_Lightning,opacity);
 }
 ENDCG
 }}
}

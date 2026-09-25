Shader "NivenRingworld/Precipitation"
{
 SubShader {Tags {"Queue"="Transparent+20" "RenderType"="Transparent"}
 Pass {Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.0
 #include "UnityCG.cginc"
 UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
 sampler2D _WeatherDepth;float _Manual,_Saved,_Flake;float4x4 _WeatherVP,_WeatherView;
 struct a {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
 struct v {float4 pos:SV_POSITION;float4 screen:TEXCOORD0;float2 uv:TEXCOORD1;float4 color:COLOR;float depth:TEXCOORD2;};
 v vert(a i){v o;float4 w=mul(unity_ObjectToWorld,i.vertex);o.pos=_Manual>.5?mul(_WeatherVP,w):UnityObjectToClipPos(i.vertex);o.screen=ComputeScreenPos(o.pos);o.uv=i.uv;o.color=i.color;o.depth=_Manual>.5?-mul(_WeatherView,w).z:-UnityObjectToViewPos(i.vertex).z;return o;}
 float4 frag(v i):SV_Target
 {
  float2 uv=i.screen.xy/i.screen.w;
  float depth=_Saved>.5?tex2D(_WeatherDepth,uv).r:LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv));
  clip(depth-i.depth);
  float shape=_Flake>.5?1-smoothstep(.25,.5,length(i.uv-.5)):1;
  return float4(i.color.rgb,i.color.a*shape*saturate((depth-i.depth)*.5));
 }
 ENDCG
 }}
}

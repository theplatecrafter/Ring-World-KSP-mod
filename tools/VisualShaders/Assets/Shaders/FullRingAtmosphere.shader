Shader "NivenRingworld/FullRingAtmosphere"
{
 SubShader { Tags { "Queue"="Transparent-10" "RenderType"="Transparent" }
 Pass { Cull Off ZWrite Off Blend One OneMinusSrcAlpha
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.0
 #include "UnityCG.cginc"
 #include "RingHullOcclusion.cginc"
 float4 _RingSize;
 float _DayPhase,_Haze,_Exposure,_LocalBlend,_Detail;
 struct a {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
 struct v {float4 vertex:SV_POSITION;float3 local:TEXCOORD0;float2 uv:TEXCOORD1;};
 v vert(a i){v o;o.vertex=UnityObjectToClipPos(i.vertex);o.local=i.vertex.xyz;o.uv=i.uv;return o;}
 float4 frag(v i):SV_Target
 {
   float3 camera=mul(unity_WorldToObject,float4(_WorldSpaceCameraPos,1)).xyz;
   if(ringHullOccludes(camera,i.local,_RingSize.x,_RingSize.y))discard;
   float3 delta=camera-i.local;
   float distanceMetres=length(delta)/max(_RingSize.z,1e-12);
   float handoff=lerp(1,smoothstep(250000,600000,distanceMetres),_LocalBlend);
   float3 normal=normalize(float3(-i.local.x,0,-i.local.z));
   float cosine=abs(dot(normal,normalize(delta)));
   // Curvature caps a grazing optical column. No subtraction of enormous radii.
   float grazing=sqrt(max(.00001,16000*_RingSize.z/max(_RingSize.x,1)));
   float column=1/max(grazing,max(.035,cosine));
   float phase=frac(20*i.uv.x-_DayPhase),edge=min(phase,1-phase);
   float light=smoothstep(.138307,.158307,edge);
   float rim=smoothstep(0,.002,min(i.uv.y,1-i.uv.y));
   float optical=min(5,column*.12*max(0,_Haze));
   float alpha=(1-exp(-optical))*handoff*rim*light;
   float mu=dot(normal,normalize(delta));
   float rayleigh=.75*(1+mu*mu);
   float3 colour=float3(.24,.48,.82)*rayleigh;
   if(_Detail>.5)colour+=float3(.13,.11,.08)*pow(saturate(mu),8)*saturate(_Haze);
   colour*=max(0,_Exposure);
   #ifndef UNITY_COLORSPACE_GAMMA
     colour=GammaToLinearSpace(colour);
   #endif
   return float4(colour*alpha,alpha);
 }
 ENDCG
 }}
}

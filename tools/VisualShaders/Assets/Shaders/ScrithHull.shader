Shader "NivenRingworld/ScrithHull"
{
 Properties { _SurfaceDetailEnabled("Surface textures enabled",Float)=0 }
 SubShader { Tags { "RenderType"="Opaque" }
 Pass { Cull Back ZWrite On
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.5
 #include "UnityCG.cginc"
 #include "TerrainDetail.cginc"
 struct a {float4 vertex:POSITION;float4 detail:TEXCOORD2;float3 noise:TEXCOORD4;};
 struct v {float4 vertex:SV_POSITION;float4 detail:TEXCOORD0;float3 world:TEXCOORD1;float3 noise:TEXCOORD2;};
 v vert(a i){v o;o.vertex=UnityObjectToClipPos(i.vertex);o.detail=float4(0,0,1,0);o.world=mul(unity_ObjectToWorld,i.vertex).xyz;o.noise=i.noise;return o;}
 float4 frag(v i):SV_Target {return float4(float3(.012,.015,.019)*(terrainDetail(i.detail,0,i.noise,distance(i.world,_WorldSpaceCameraPos)).b*2),1);}
 ENDCG
 }}
}

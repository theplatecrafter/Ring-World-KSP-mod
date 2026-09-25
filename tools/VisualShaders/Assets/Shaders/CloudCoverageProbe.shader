Shader "NivenRingworld/CloudCoverageProbe"
{
 SubShader { Pass { Cull Off ZWrite Off ZTest Always
 CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #pragma target 5.0
 #include "UnityCG.cginc"
 #include "CloudField.cginc"
 float _ProbeOffset;
 float4 frag(v2f_img i):SV_Target
 {
   float2 p=i.uv*128000000+float2(_ProbeOffset,0);
   float c=cloudCoverageAt(_CoverageOrigin.xy,_CoverageOrigin.zw+p*float2(_CoverageScaleX,1.0/4000000),_CloudAmount,0);
   return float4(c,c,c,1);
 }
 ENDCG
 }}
}

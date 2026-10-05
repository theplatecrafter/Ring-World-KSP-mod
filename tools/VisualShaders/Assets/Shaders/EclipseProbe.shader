Shader "NivenRingworld/EclipseProbe" { SubShader { Pass { ZTest Always ZWrite Off Cull Off
CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 5.0
#include "UnityCG.cginc"
#include "RingEclipse.cginc"
float3 _Observer;
float4 frag(v2f_img i):SV_Target {float light=ringEclipseAt(_Observer);return float4(light,light,light,1);}
ENDCG
}}}
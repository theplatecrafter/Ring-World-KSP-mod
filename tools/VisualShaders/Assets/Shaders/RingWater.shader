Shader "NivenRingworld/Waves"
{
 Properties { _Color("Water tint",Color)=(.04,.25,.32,1) }
 SubShader
 {
  Tags { "RenderType"="Transparent" "Queue"="Transparent-20" }
  Pass
  {
   Cull Off ZWrite Off
   Blend SrcAlpha OneMinusSrcAlpha
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "RingWaterSurface.cginc"
   ENDCG
  }
 }
}

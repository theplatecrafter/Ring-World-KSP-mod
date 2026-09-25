   #include "UnityCG.cginc"
   float3 _WaveCamera,_WaveAlong,_WaveAcross,_WaveUp;
   
   #ifdef RING_WATER_REFRACTION
   sampler2D _RingWaterBackground;
   #endif
   float4 _Wave; // wrapped along, wrapped across, frozen simulation time, amplitude
   float4 _WavePhase,_RipplePhase;
   float _WaterLight,_WaterQuality;float4 _NoiseOffset;
   struct data {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
   struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float2 p:TEXCOORD1;float depth:TEXCOORD2;float4 screen:TEXCOORD3;};
   float4 _SeaWaves[12];float _SeaCount,_SeaWind;float3 _WaveSun;
   float wave(float2 p)
   {
    float h=0;
    [unroll]for(int j=0;j<4;j++){if(j>=_SeaCount)break;float4 w=_SeaWaves[j];h+=sin(dot(p,w.xy)+w.z)*w.w;}
    return h;
   }
   float2 waveSlope(float2 p)
   {
    float2 slope=0;
    [loop]for(int j=0;j<12;j++)
    {if(j>=_SeaCount)break;float4 w=_SeaWaves[j];float theta=dot(p,w.xy)+w.z;float filter=1-smoothstep(.5,3.14159,fwidth(theta));slope+=w.xy*cos(theta)*w.w*filter;}
    return slope;
   }
   float geometryWave(float2 p,float spacing)
   {
    float h=0;
    [unroll]for(int j=0;j<4;j++){if(j>=_SeaCount)break;float4 w=_SeaWaves[j];float resolved=1-smoothstep(.5,1.5,length(w.xy)*spacing);h+=sin(dot(p,w.xy)+w.z)*w.w*resolved;}
    return h;
   }
   v2f vert(data v)
   {
    v2f o;float3 w=mul(unity_ObjectToWorld,v.vertex).xyz;float3 delta=w-_WaveCamera;
    o.p=float2(dot(delta,_WaveAlong),dot(delta,_WaveAcross))+_Wave.xy;
    float amplitude=_Wave.w*saturate(v.uv.x/5)*(1-smoothstep(2000,20000,length(delta)));
    w+=_WaveUp*geometryWave(o.p,v.uv.y)*amplitude;o.world=w;o.depth=v.uv.x;o.pos=mul(UNITY_MATRIX_VP,float4(w,1));o.screen=ComputeGrabScreenPos(o.pos);return o;
   }
   float random(float2 p){p=fmod(p+65536,65536);return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(random(a),random(a+float2(1,0)),f.x),lerp(random(a+float2(0,1)),random(a+1),f.x),f.y);}
   float relief(float2 p){float v=noise(p);if(_WaterQuality>=3)v+=.5*noise(p*2.03+17);if(_WaterQuality>=4)v+=.25*noise(p*4.17-31);return v;}
   float ripple(float theta){return sin(theta)*(1-smoothstep(.5,3.14159,fwidth(theta)));}
   float4 frag(v2f i):SV_Target
   {
    float2 p=i.p;
    if(_WaterQuality<.5)return float4(float3(.04,.23,.29)*(.04+.96*_WaterLight),lerp(.28,.96,saturate(i.depth/18)));
    float amplitude=_Wave.w*saturate(i.depth/5);
    float2 slope=waveSlope(p)*max(.35,amplitude);
    float dx=slope.x,dy=slope.y;
    float2 np=(p+_NoiseOffset.xy)*.12+float2(_NoiseOffset.z,-_NoiseOffset.z*.71);
    dx+=(relief(np+float2(.15,0))-relief(np-float2(.15,0)))*.18;dy+=(relief(np+float2(0,.15))-relief(np-float2(0,.15)))*.18;
    float3 n=normalize(_WaveUp-_WaveAlong*dx-_WaveAcross*dy),v=normalize(_WorldSpaceCameraPos-i.world);
    if(dot(n,v)<0)n=-n;
    float fresnel=.025+.975*pow(1-saturate(dot(n,v)),5);
    float3 reflection=reflect(-v,n);float sky=saturate(dot(reflection,_WaveUp));
    float3 skyColor=lerp(float3(.32,.44,.51),float3(.12,.29,.52),sqrt(sky));
    float3 sun=normalize(_WaveSun+_WaveUp*.0001),halfway=normalize(sun+v);
    float ndh=saturate(dot(n,halfway)),ndl=saturate(dot(n,sun)),ndv=max(.05,saturate(dot(n,v)));
    float roughness=.045+.055*_SeaWind;
    float variance=dot(ddx(n),ddx(n))+dot(ddy(n),ddy(n));
    float a2=clamp(roughness*roughness+variance*.3,.001,.35);
    float denom=ndh*ndh*(a2-1)+1;
    float distribution=a2/(3.14159*denom*denom);
    float glint=min(12,distribution*.025*ndl/(4*ndv));
    float3 water=lerp(float3(.065,.31,.29),float3(.012,.095,.16),saturate(i.depth/25));
    float foam=(1-saturate(i.depth/2))*smoothstep(.25,.8,wave(p))*lerp(.4,1,noise(np*.4));
    float3 color=(lerp(water,skyColor,fresnel)+glint*float3(1,.95,.8)+foam*.3)*(.04+.96*_WaterLight);
    #ifdef UNITY_COLORSPACE_GAMMA
     color=LinearToGammaSpace(color);
    #endif
    float opacity=saturate(lerp(.24,.97,saturate(i.depth/20))+fresnel*.5+foam*.15);
    #ifdef RING_WATER_REFRACTION
     // One per-camera screen copy shared by all visible water tiles. Distortion fades
     // at shorelines and in the distance to avoid swimming tile boundaries.
     float2 uv=i.screen.xy/i.screen.w;
     float strength=.003*saturate(i.depth/3)*(1-smoothstep(1000,12000,distance(i.world,_WorldSpaceCameraPos)));
     float3 background=tex2D(_RingWaterBackground,saturate(uv+float2(dx,dy)*strength)).rgb;
     float3 transmission=exp(-float3(.16,.055,.025)*min(i.depth,80));
     float3 transmitted=background*transmission;
     // Absorption is wavelength dependent; the visible scene is progressively
     // replaced by scattered water colour rather than a constant blue opacity.
     color=lerp(transmitted+color*(1-transmission),color,saturate(fresnel+foam*.3));
     return float4(color,1);
    #else
     return float4(color,opacity);
    #endif
   }

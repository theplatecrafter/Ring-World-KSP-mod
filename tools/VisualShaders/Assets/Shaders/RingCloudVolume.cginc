// Original Ringworld cloud volume. Distances are in metres in the ring tangent chart.
// Feature reference: EVE's public raymarched-cloud documentation; no upstream shader code.
sampler3D _CloudShape;
float4 _CloudBands[10],_CloudShapes[10],_CloudOptics[10];
float4 _CloudControls,_CloudLimits; // mode, density multiplier, storm, rain
float _CloudRise,_CloudProbe,_PrecipitationBase;
float cloudCurve(float4 curve,float h)
{
 float x=saturate(h)*3;int k=min(2,(int)x);float f=frac(x);if(h>=1)f=1;
 f=f*f*(3-2*f);return lerp(curve[k],curve[k+1],f);
}
float cloudHeight(float3 p)
{
 float r=max(1,_Habitat.y-_Habitat.x);
 // Rationalized radial difference avoids subtracting two enormous ring radii.
 float radial=sqrt((r-p.z)*(r-p.z)+p.x*p.x);
 return _Habitat.x+(2*r*p.z-p.z*p.z-p.x*p.x)/max(1,r+radial);
}
float2 cloudCurl(float3 q)
{
 float e=.015;
 float dy=tex3Dlod(_CloudShape,float4(q+float3(0,e,0),0)).a-tex3Dlod(_CloudShape,float4(q-float3(0,e,0),0)).a;
 float dx=tex3Dlod(_CloudShape,float4(q+float3(e,0,0),0)).a-tex3Dlod(_CloudShape,float4(q-float3(e,0,0),0)).a;
 return float2(dy,-dx);
}
float cloudTypeDensity(float3 p,float h,float cover,int type)
{
 float4 band=_CloudBands[type],opt=_CloudOptics[type];
 float z=(h-band.x)/max(100,band.y-band.x);if(z<=0||z>=1)return 0;
 float profile=cloudCurve(_CloudShapes[type],z)*smoothstep(0,.06,z)*(1-smoothstep(.94,1,z));
 float3 q=(p+_CloudOrigin+float3(0,0,-_CloudRise))/band.z;
 // A slow, non-repeating coverage warp breaks alignment of repeated volume tiles.
 float2 f=_CoverageOrigin.zw+p.xy*float2(_CoverageScaleX,1.0/4000000);
 float warp=cloudPerlin(_CoverageOrigin.xy,f,2,383);
 q.xy+=float2(warp,-warp*.73)*.25;
 if(type==3||type==4)q.xy*=float2(.45,4); // sheared high ice layers
 if(type==2)q.xy/=1+smoothstep(.65,.95,z)*1.4; // spreading storm anvil
 if(type==5||type==9)q.z*=.35; // small shallow cellular fields
 if(type==4||type==6||type==7||type==8)q.z*=.15; // high, wind-stretched ice wisps
 if(_CloudControls.x>=3&&opt.w>0&&length(p)<150000)q.xy+=cloudCurl(q*.5)*opt.w*.3;
 float lod=clamp(log2(max(1,length(p)/60000)),0,4);
 float4 n=tex3Dlod(_CloudShape,float4(q,lod));
 float billow=saturate(n.r*.65+n.g*.35);
 float sheet=(type==4||type==6||type==7||type==8)?1:0;
 float threshold=lerp(.65,.43,sheet)-saturate(cover*profile)*lerp(.5,.35,sheet);
 float body=saturate((billow-threshold)/max(.08,1-threshold));
 float detail=n.b;
 if(_CloudControls.x>=2&&length(p)<300000)detail=lerp(detail,tex3Dlod(_CloudShape,float4(q*4+float3(7,13,3),0)).g,.6);
 // Erosion is restricted to the edges so clouds retain cohesive interiors.
 float erosion=(1-detail)*opt.x*(1-body)*.55;
 return saturate(body-erosion)*profile*cover*band.w*lerp(.65,1.15,z);
}
float density(float3 p)
{
 if(_CloudControls.x<.5||_WeatherMap.w<=0||abs(_Habitat.z+p.y)>_Habitat.w)return 0;
 float h=cloudHeight(p);if(h<_CloudLimits.x||h>_CloudLimits.y)return 0;
 float cover=cloudCoverage(p.xy);if(cover<.01)return 0;
 float kind=saturate(.5+cloudPerlin(_CoverageOrigin.xy,_CoverageOrigin.zw+p.xy*float2(_CoverageScaleX,1.0/4000000),4,9049));
 // Stable humid air favours sheets; convection favours heaps; strong storms
 // grow deep anvils. This is a weather-conditioned morphology, not a fluid solver.
 float rain=saturate(_CloudControls.w),storm=saturate(_CloudControls.z);
 float rim=smoothstep(.90,.99,abs(_Habitat.z+p.y)/max(1,_Habitat.w));
 float dry=lerp(cloudTypeDensity(p,h,cover,0),cloudTypeDensity(p,h,cover,1),smoothstep(.25,.75,kind));
 float fog=smoothstep(.15,.65,rim*(1-_Look.y)*cover);
 if(fog>.001)dry=lerp(dry,cloudTypeDensity(p,h,cover,7),fog);
 float wet=lerp(cloudTypeDensity(p,h,cover,6),cloudTypeDensity(p,h,cover,2),smoothstep(.05,.65,storm));
 float wetMix=smoothstep(.02,.6,max(rain,storm));
 float den=lerp(dry,wet,wetMix);
 if(_CloudControls.x>=2)
 {
  den+=lerp(cloudTypeDensity(p,h,cover*.7,5),cloudTypeDensity(p,h,cover,8),rain)*(.25+.35*cover)*(1-storm);
  float ice=lerp(cloudTypeDensity(p,h,cover*.65,3),cloudTypeDensity(p,h,cover*.8,4),rain);
  if(_CloudControls.x>=3)ice=lerp(ice,cloudTypeDensity(p,h,cover*.7,9),smoothstep(.65,.9,kind)*(1-rain));
  den+=ice*(.35+.4*kind);
 }
 float local=1-smoothstep(_CloudHandoff.x,max(_CloudHandoff.x+1,_CloudHandoff.y),length(p));
 if(_CloudProbe>.5)den=cloudTypeDensity(p,h,cover,(int)_CloudProbe-1);
 return den*local*_CloudControls.y;
}
float cloudTau(float3 p)
{
 float tau=0,ds=9000/max(1,_Quality.z);
 [loop]for(int j=0;j<8;j++){if(j>=_Quality.z)break;tau+=density(p+_Sun*((j+.5)*ds))*ds*.001;}
 return tau*_Look.z;
}
float hg(float mu,float g){return (1-g*g)/pow(max(.02,1+g*g-2*g*mu),1.5);}
float3 cloudIllumination(float3 p,float den,float mu)
{
 float tau=cloudTau(p),direct=exp(-tau);
 // Low-order multiple-scattering approximation and dual-lobe angular scattering.
 float scatter=direct;
 if(_CloudControls.x>=2)scatter+=.35*exp(-tau*.45);
 if(_CloudControls.x>=3)scatter+=.15*exp(-tau*.2);
 float phase=.8*hg(mu,.55)+.2*hg(mu,-.25);
 float kind=saturate(.5+cloudPerlin(_CoverageOrigin.xy,_CoverageOrigin.zw+p.xy*float2(_CoverageScaleX,1.0/4000000),4,9049));
 float4 dryOpt=lerp(_CloudOptics[0],_CloudOptics[1],smoothstep(.25,.75,kind));
 float4 wetOpt=lerp(_CloudOptics[6],_CloudOptics[2],smoothstep(.05,.65,_CloudControls.z));
 float4 opt=lerp(dryOpt,wetOpt,smoothstep(.02,.6,max(_CloudControls.w,_CloudControls.z)));
 if(_CloudControls.x>=2)opt=lerp(opt,lerp(_CloudOptics[3],_CloudOptics[4],_CloudControls.w),smoothstep(6500,9000,cloudHeight(p))*(1-_CloudControls.z));
 float powder=1-exp(-den*(1+opt.y*4));
 float ambient=lerp(opt.z,opt.z+.24,saturate((cloudHeight(p)-_CloudLimits.x)/max(1,_CloudLimits.y-_CloudLimits.x)));
 float3 light=float3(.72,.83,1)*ambient+float3(1,.965,.9)*scatter*(.4+.28*phase)*lerp(.7,1,powder);
 return light*(.025+.975*_Look.y)+_Lightning*1.7;
}
float2 cloudShellRoots(float3 d,float altitude)
{
 float r=max(1,_Habitat.y-_Habitat.x),a=d.x*d.x+d.z*d.z;
 if(a<1e-10)return float2(-1,-1);
 float b=-2*r*d.z,c=(altitude-_Habitat.x)*(2*_Habitat.y-altitude-_Habitat.x);
 float disc=b*b-4*a*c;if(disc<0)return float2(-1,-1);
 float q=-.5*(b+(b<0?-1:1)*sqrt(disc));
 if(abs(q)<1e-10)return float2(0,0);
 return float2(q/a,c/q);
}
float4 cloudIntervals(float3 d,float limit)
{
 float2 base=cloudShellRoots(d,_CloudLimits.x),top=cloudShellRoots(d,_CloudLimits.y);
 float points[6];points[0]=0;points[1]=limit;
 points[2]=clamp(base.x,0,limit);points[3]=clamp(base.y,0,limit);
 points[4]=clamp(top.x,0,limit);points[5]=clamp(top.y,0,limit);
 [unroll]for(int x=0;x<5;x++)[unroll]for(int y=x+1;y<6;y++){if(points[y]<points[x]){float v=points[x];points[x]=points[y];points[y]=v;}}
 float4 segments=0;int count=0;
 [unroll]for(int k=0;k<5;k++)
 {
  float h=cloudHeight(d*((points[k]+points[k+1])*.5));
  if(h>=_CloudLimits.x&&h<=_CloudLimits.y&&points[k+1]>points[k])
  {
   if(count==0){segments.xy=float2(points[k],points[k+1]);count=1;}
   else if(abs(points[k]-segments.y)<1)segments.y=points[k+1];
   else if(count==1){segments.zw=float2(points[k],points[k+1]);count=2;}
   else segments.w=points[k+1];
  }
 }
 return segments;
}

// Ring-relative coordinates normalized by the viewed ring radius.
float4 _EclipseSun, _EclipseBodies[32], _EclipseCenters[8], _EclipseAxes[8], _EclipseX[8], _EclipseZ[8], _EclipseSizes[8];
int _EclipseBodyCount,_EclipseRingCount;float _EclipseWidth;
sampler2D _EclipseData;float4 _EclipseDataSize;
float4 eclipseRead(int index)
{
 float x=fmod((float)index,_EclipseDataSize.x),y=floor((float)index/_EclipseDataSize.x);
 return tex2Dlod(_EclipseData,float4((float2(x,y)+.5)/_EclipseDataSize.xy,0,0));
}
bool eclipseSlab(float p,float d,float h,inout float lo,inout float hi)
{
 if(abs(d)<1e-12)return abs(p)<=h;
 float2 t=float2(-h-p,h-p)/d;lo=max(lo,min(t.x,t.y));hi=min(hi,max(t.x,t.y));return lo<=hi;
}
bool eclipseBox(float3 a,float3 d,float3 halfSize)
{
 float lo=1e-7,hi=1-1e-7;
 return eclipseSlab(a.x,d.x,halfSize.x,lo,hi)&&eclipseSlab(a.y,d.y,halfSize.y,lo,hi)&&eclipseSlab(a.z,d.z,halfSize.z,lo,hi);
}
bool eclipseCylinder(float3 p,float3 d,float radius,float lo,float hi)
{
 float a=dot(d.xz,d.xz),b=dot(p.xz,d.xz),r=length(p.xz),c=(r-radius)*(r+radius),disc=b*b-a*c;
 if(a<1e-20||disc<0)return false;
 float q=-b-(b>=0?1:-1)*sqrt(disc);float2 t=float2(q/a,abs(q)>1e-20?c/q:-b/a);
 return (t.x>=lo&&t.x<=hi)||(t.y>=lo&&t.y<=hi);
}
bool eclipseShell(float3 p,float3 d,float radius,float thick,float width)
{
 float lo=1e-7,hi=1-1e-7;if(!eclipseSlab(p.y,d.y,width,lo,hi))return false;
 return eclipseCylinder(p,d,radius,lo,hi)||eclipseCylinder(p,d,radius+thick,lo,hi);
}
bool eclipsePanel(float3 a,float3 b,float radius,float width,float thickness,float phase)
{
 float panelRadius=radius*46/153,aa=dot(b.xz,b.xz),bb=dot(a.xz,b.xz),cc=dot(a.xz,a.xz)-panelRadius*panelRadius;
 float disc=bb*bb-aa*cc;if(aa<1e-20||disc<0)return false;
 float root=sqrt(disc);float2 hits=float2(-bb-root,-bb+root)/aa;
 [unroll]for(int h=0;h<2;h++)
 {
  float t=h==0?hits.x:hits.y;if(t<0||t>1)continue;
  float3 hitPosition=a+b*t;float theta=atan2(-hitPosition.z,hitPosition.x);float sector=round((theta-phase)/.314159265359);
  [unroll]for(int k=-1;k<=1;k++)
  {
   float angle=phase+(sector+k)*.314159265359,sn=sin(angle),cs=cos(angle);
   float3 ap=float3(a.x*cs-a.z*sn,a.y,a.x*sn+a.z*cs)-float3(panelRadius,0,0);
   float3 bp=float3(b.x*cs-b.z*sn,b.y,b.x*sn+b.z*cs);
   if(eclipseBox(ap,bp,float3(max(1e-9,thickness),width,radius*2/153)))return true;
  }
 }
 return false;
}
float ringEclipseAt(float3 p)
{
 float3 d=_EclipseSun.xyz-p;float distance=length(d);float3 ray=d/max(1e-12,distance);float visibility=1;
 [loop]for(int i=0;i<_EclipseBodyCount;i++)
 {
  float4 body=_EclipseDataSize.x>0?eclipseRead(i):_EclipseBodies[i];
  float3 relative=body.xyz-p;float along=dot(relative,ray);
  if(along<=0||along>=distance)continue;
  float gap=length(relative-ray*along)-body.w;
  float penumbra=max(1e-8,_EclipseSun.w*along/distance);
  visibility=min(visibility,smoothstep(-penumbra,penumbra,gap));
 }
 [loop]for(int j=0;j<_EclipseRingCount;j++)
 {
  float4 center,axis,xx,zz,size;
  if(_EclipseDataSize.x>0){int index=_EclipseBodyCount+j*5;center=eclipseRead(index);axis=eclipseRead(index+1);xx=eclipseRead(index+2);zz=eclipseRead(index+3);size=eclipseRead(index+4);}
  else{center=_EclipseCenters[j];axis=_EclipseAxes[j];xx=_EclipseX[j];zz=_EclipseZ[j];size=_EclipseSizes[j];}
  float3 q=p-center.xyz;
  float3 a=float3(dot(q,xx.xyz),dot(q,axis.xyz),dot(q,zz.xyz));
  float3 b=float3(dot(d,xx.xyz),dot(d,axis.xyz),dot(d,zz.xyz));
  float r=center.w,w=axis.w,t=xx.w;
  if(eclipseShell(a,b,r+size.y,t,w))return 0;
  if(eclipseShell(a-float3(0,w,0),b,r-size.x,size.x+t,t)||eclipseShell(a+float3(0,w,0),b,r-size.x,size.x+t,t))return 0;
  if(size.z<.5)continue;
  if(eclipsePanel(a,b,r,w,size.w,zz.w))return 0;
 }
 return visibility;
}

float ringEclipse(float2 uv){float angle=uv.x*6.28318530718;return ringEclipseAt(float3(cos(angle),(uv.y-.5)*_EclipseWidth,-sin(angle)));}

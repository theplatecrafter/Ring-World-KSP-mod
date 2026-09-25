// Seeded gradient Perlin fBm. Only the full circumference wraps in longitude.
// Integer cell + fractional origin avoids float precision loss near the observer.
sampler3D _Noise;
float3 _CloudOrigin;
float4 _CoverageOrigin;
float2 _CoverageEvolution;
float _CloudAmount,_Lightning,_CoveragePeriod,_CoverageScaleX;float2 _CoverageSeed;
uint cloudHash(int2 p,int salt)
{
    uint h=(uint)p.x*0x8da6b343u^(uint)p.y*0xd8163841u^((uint)_CoverageSeed.x|((uint)_CoverageSeed.y<<16))^(uint)salt;
    h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;return h^(h>>16);
}
float cloudGradient(uint h,float2 p)
{
    uint k=h&7u;
    if(k==0)return p.x;if(k==1)return -p.x;if(k==2)return p.y;if(k==3)return -p.y;
    return (k==4?p.x+p.y:k==5?p.x-p.y:k==6?-p.x+p.y:-p.x-p.y)*.70710678;
}
float cloudPerlin(float2 cell,float2 fraction,int frequency,int salt)
{
    float2 p=fraction*frequency;int2 c=(int2)round(cell)*frequency+(int2)floor(p);float2 f=frac(p);
    int period=(int)_CoveragePeriod*frequency;c.x=(c.x%period+period)%period;int next=(c.x+1)%period;
    int yp=1048576*frequency;c.y=(c.y%yp+yp)%yp;int yn=(c.y+1)%yp;
    float2 q=f*f*f*(f*(f*6-15)+10);
    return lerp(lerp(cloudGradient(cloudHash(c,salt),f),cloudGradient(cloudHash(int2(next,c.y),salt),f-float2(1,0)),q.x),
        lerp(cloudGradient(cloudHash(int2(c.x,yn),salt),f-float2(0,1)),cloudGradient(cloudHash(int2(next,yn),salt),f-1),q.x),q.y);
}
float cloudCoverageAt(float2 cell,float2 fraction,float amount,float footprint)
{
    float2 warp=float2(cloudPerlin(cell,fraction+_CoverageEvolution,1,8191),cloudPerlin(cell,fraction+_CoverageEvolution,1,13171))*.7;
    float sum=0,weight=1,total=0;
    [unroll] for(int octave=0;octave<5;octave++)
    {
        int frequency=1<<octave;
        float filter=1-smoothstep(.35,1.5,footprint*frequency);
        sum+=weight*filter*cloudPerlin(cell,fraction+warp,frequency,1970+octave*1013);
        total+=weight;weight*=.5;
    }
    float n=saturate(.5+sum/total*1.5);
    return amount<=0?0:smoothstep(.42+(.5-amount)*.24,.58+(.5-amount)*.24,n);
}
float cloudCoverage(float2 p)
{
    float2 f=_CoverageOrigin.zw+p*float2(_CoverageScaleX,1.0/4000000);
    float broad=cloudCoverageAt(_CoverageOrigin.xy,f,_CloudAmount,0);
    // Mesoscale clusters, independent of mesh tessellation and camera movement.
    float2 warp=float2(cloudPerlin(_CoverageOrigin.xy,f,16,5723),cloudPerlin(_CoverageOrigin.xy,f,16,7933))*.012;
    float detail=.5+.7*cloudPerlin(_CoverageOrigin.xy,f+warp,64,3413)+.3*cloudPerlin(_CoverageOrigin.xy,f+warp,128,6917);
    float clusters=smoothstep(.28,.68,detail);
    // Wet weather closes clear holes before precipitation begins (severity .60).
    float overcast=smoothstep(.65,.81,_CloudAmount);
    return _CloudAmount<=0?0:lerp(broad*clusters,.92+.08*clusters,overcast);
}

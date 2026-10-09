// Texture-free 3-D gradient simplex noise, anchored to the canonical ring.
// Simplex corner ordering follows the Ashima Arts / stegu MIT reference.
// Integer hashing and split-world coordinates are Ringworld's implementation.
float _SurfaceDetailEnabled;
float4 _SurfaceOriginX,_SurfaceOriginY,_SurfaceOriginZ;
float2 _SurfaceSeed;
uint surfaceHash(uint h){h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;return h^(h>>16);}
uint2 surfaceCell(float4 packed,int relative,int octave)
{
    uint2 origin=uint2((uint)packed.x|((uint)packed.y<<16),(uint)packed.z|((uint)packed.w<<16));
    uint shift=(uint)(16-octave);
    uint2 cell=uint2(origin.x<<shift,(origin.y<<shift)|(origin.x>>(32u-shift)));
    uint before=cell.x;cell.x+=(uint)relative;
    cell.y+=(relative<0?0xffffffffu:0u)+(cell.x<before?1u:0u);
    return cell;
}
uint surfaceCorner(int3 cell,int octave,uint salt)
{
    uint2 x=surfaceCell(_SurfaceOriginX,cell.x,octave),y=surfaceCell(_SurfaceOriginY,cell.y,octave),z=surfaceCell(_SurfaceOriginZ,cell.z,octave);
    return surfaceHash(surfaceHash(x.x^surfaceHash(x.y+0x9e3779b9u))^
                       surfaceHash(y.x^surfaceHash(y.y+0x85ebca6bu))^
                       surfaceHash(z.x^surfaceHash(z.y+0xc2b2ae35u))^salt);
}
float surfaceGradient(uint hash,float3 d)
{
    uint h=hash&15u;float u=h<8u?d.x:d.y,v=h<4u?d.y:((h==12u||h==14u)?d.x:d.z);
    return ((h&1u)==0u?u:-u)+((h&2u)==0u?v:-v);
}
float surfaceContribution(int3 cell,int octave,uint salt,float3 d)
{
    float t=max(0,.6-dot(d,d));t*=t;
    return t*t*surfaceGradient(surfaceCorner(cell,octave,salt),d);
}
float surfaceNoise(float3 skewed,int octave,uint salt)
{
    float3 p=skewed*exp2(-(float)octave),f=frac(p);int3 cell=(int3)floor(p);
    float3 d=f-dot(f,float3(1.0/6,1.0/6,1.0/6));
    // Stable X/Y/Z priority at exact ties keeps rank cycles from duplicating
    // corners at lattice vertices (a potential regular pinpoint artifact).
    float3 order=float3(d.x>=d.y?1:0,d.y>=d.z?1:0,d.z>d.x?1:0),inverse=1-order;
    int3 first=(int3)min(order,inverse.zxy),second=(int3)max(order,inverse.zxy);
    return 32*(surfaceContribution(cell,octave,salt,d)+
               surfaceContribution(cell+first,octave,salt,d-first+1.0/6)+
               surfaceContribution(cell+second,octave,salt,d-second+1.0/3)+
               surfaceContribution(cell+1,octave,salt,d-.5));
}
float4 terrainDetail(float4 coordinates,float4 natural,float3 position,float metres)
{
    if(_SurfaceDetailEnabled<.5)return float4(.5,.5,.5,1);
    float total=dot(natural,1)+coordinates.z+coordinates.w;
    if(total<.001)return float4(.5,.5,.5,1);
    uint seed=(uint)_SurfaceSeed.x|((uint)_SurfaceSeed.y<<16);
    float footprint=max(length(ddx(position)),length(ddy(position)));
    // Analytically remove octaves smaller than a pixel. Besides avoiding alias
    // patterns, distant huge triangles never evaluate unresolvable fine cells.
    if(footprint>=4096)return float4(.5,.5,.5,1);
    float fineWeight=(1-smoothstep(80,400,metres))*(1-smoothstep(.08,.5,footprint));
    float distant=smoothstep(2000,16000,metres);
    float warp=0;if(footprint<512)warp=surfaceNoise(position,8,seed+37u)*(1-smoothstep(128,512,footprint));
    float3 warped=position+warp*float3(23,-17,31);
    float coarse=0;
    if(distant<.999){
        if(footprint<64)coarse+=.55*surfaceNoise(warped,6,seed+91u)*(1-smoothstep(16,64,footprint));
        if(footprint<512)coarse+=.45*surfaceNoise(warped,9,seed+719u)*(1-smoothstep(128,512,footprint));
        coarse*=1-distant;
    }
    if(distant>.001)coarse+=surfaceNoise(warped,12,seed+1777u)*distant*(1-smoothstep(1024,4096,footprint));
    float field=.7*coarse;
    if(fineWeight>.001)field+=fineWeight*(.18*surfaceNoise(position,0,seed+227u)+.12*surfaceNoise(position,-2,seed+919u));
    // Stock-like restrained albedo variation; geometry supplies the normals.
    // There are no texture tiles, cell-placement blends or embossed bump patterns.
    float amplitude=(dot(natural,float4(.035,.025,.04,.012))+coordinates.z*.008+coordinates.w*.025)/total;
    return float4(.5,.5,.5+.5*amplitude*clamp(field,-1,1),1);
}

using System;
using System.Collections.Generic;
using Ringworld.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace NivenRingworld
{
    // A single collision-free mesh, seeded in material coordinates. Placement is
    // raycast against actual streamed triangles, avoiding floating foliage over
    // the higher-resolution analytic height function.
    internal sealed class GroundDetails : IDisposable
    {
        private readonly Settings settings;private readonly GameObject root;private readonly Mesh mesh;private readonly Material material;
        private readonly RaycastHit[] hits=new RaycastHit[16];
        private readonly BatchedScenery authored;
        private DVec anchor;private double phase;private long cellX=long.MinValue,cellY;private float density=-1;private double range;
        internal int Count {get;private set;}
        internal void Hide(){root.SetActive(false);cellX=long.MinValue;Count=0;}
        internal GroundDetails(Settings s)
        {
            settings=s;root=new GameObject("Ringworld close ground cover");root.layer=15;
            mesh=new Mesh{name="Batched grass, stones and litter"};root.AddComponent<MeshFilter>().sharedMesh=mesh;
            material=new Material(Shader.Find("Sprites/Default"));var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            authored=new BatchedScenery(root.transform);
        }
        internal void Update(DVec observer,Vector3d star,bool force=false)
        {
            var g=settings.Geometry;var p=g.Coordinates(observer);var s=settings.Terrain.Sample(p.Along,p.Across);
            bool visible=!MapView.MapIsEnabled&&StockGraphics.Scatter>0&&p.Altitude-s.Height<80&&p.Altitude>s.Height-5;
            root.SetActive(visible);if(!visible){Count=0;cellX=long.MinValue;return;}
            long x=(long)Math.Floor(p.Along/16),y=(long)Math.Floor(p.Across/16);
            if(force||x!=cellX||y!=cellY||density!=StockGraphics.Scatter||range!=settings.DetailDistance)
            {cellX=x;cellY=y;density=StockGraphics.Scatter;range=settings.DetailDistance;Build(p,star);}
            double delta=g.OrientationRadians-phase;root.transform.position=(Vector3)(star+ConvertVector.Ksp(settings.Geometry.RotateAroundAxis(anchor,delta)));root.transform.rotation=settings.AxisRotation(delta);
            float light=(float)(.12+.88*g.Daylight(p.Along,Planetarium.GetUniversalTime(),p.Across,p.Altitude));material.color=new Color(light,light,light);
        }
        private void Build(RingPoint centre,Vector3d star)
        {
            var g=settings.Geometry;phase=g.OrientationRadians;anchor=g.Position(cellX*16,cellY*16,0);
            var world=star+ConvertVector.Ksp(anchor);var verts=new List<Vector3>();var colors=new List<Color>();var indices=new List<int>();Count=0;
            authored.Reset();
            // Fixed candidate budget: extending the radius never creates an unbounded grass mesh.
            double spacing=Math.Max(2.5,range/24);int radius=(int)Math.Ceiling(range/spacing);long cx=(long)Math.Floor(centre.Along/spacing),cy=(long)Math.Floor(centre.Across/spacing);
            for(int iy=-radius;iy<=radius&&Count<2200&&verts.Count<59000;iy++)for(int ix=-radius;ix<=radius&&Count<2200&&verts.Count<59000;ix++)
            {
                long x=cx+ix,y=cy+iy;double pick=settings.Terrain.Scatter(x,y,811);
                if(pick>density)continue;
                double a=(x+settings.Terrain.Scatter(x,y,821))*spacing,b=(y+settings.Terrain.Scatter(x,y,823))*spacing;
                double da=g.AlongDistance(a,centre.Along),db=b-centre.Across;if(da*da+db*db>range*range)continue;
                var sample=settings.Terrain.Sample(a,b);if(sample.Wet||sample.Biome==Biome.Ruins||sample.Biome==Biome.Scrith||sample.Biome==Biome.Rimwall||sample.Biome==Biome.Road)continue;
                var p=g.Position(a,b,sample.Height);var up=ConvertVector.Unity(g.Up(p));RaycastHit hit=new RaycastHit();bool underTree=false;float nearest=float.MaxValue;
                int count=Physics.RaycastNonAlloc((Vector3)(star+ConvertVector.Ksp(p))+up*100,-up,hits,200,1<<15);
                for(int k=0;k<count;k++)
                {
                    var name=hits[k].collider.name;
                    if(name.IndexOf("canopy",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("foliage",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("tree",StringComparison.OrdinalIgnoreCase)>=0)underTree=true;
                    if(name.StartsWith("Ringworld terrain ")&&hits[k].distance<nearest){nearest=hits[k].distance;hit=hits[k];}
                }
                if(hit.collider==null||Vector3.Dot(hit.normal,up)<.75f)continue;
                var at=hit.point-(Vector3)world+hit.normal*.025f;
                var side=Vector3.Cross(up,ConvertVector.Unity(settings.Geometry.Axis)).normalized;if(side.sqrMagnitude<.1f)side=Vector3.right;var along=Vector3.Cross(side,up).normalized;
                var climate=Ecology.Sample(settings.Terrain,a,b);Color color=TerrainTint.Color(sample);
                double patch=settings.Terrain.Noise(a,b,38,853);
                // Broad patches and openings, rather than an evenly populated jittered grid.
                if(pick>density*(.35+.65*patch))continue;
                bool stone=sample.Shore>.1||sample.Biome==Biome.Mountain||sample.Biome==Biome.Snow;
                bool sunflower=!stone&&climate.Meadow>.35&&settings.Terrain.Noise(a,b,1400,941)>.58;
                bool litter=!stone&&(underTree||climate.Forest>.45&&pick<density*.5);
                float fade=(float)Math.Min(1,(range-Math.Sqrt(da*da+db*db))/16);
                double artPick=settings.Terrain.Scatter(x,y,837);
                if(artPick<.24&&da*da+db*db<Math.Min(range,60)*Math.Min(range,60))
                {
                    string kind=sunflower?"mirror_sunflower":stone?"pebble_cluster":litter?(artPick<density*.007?"mushrooms":artPick<density*.016?"fern_patch":"leaf_litter"):climate.Desert>.55?"desert_scrub":sample.Shore>.03?"reed_patch":climate.Forest>.5?"fern_patch":"grass_patch";
                    Vector3 size=stone?new Vector3(1.1f,.18f,1.1f):litter?new Vector3(.8f,kind=="mushrooms"?.35f:kind=="fern_patch"?.6f:.07f,.8f):sunflower?new Vector3(.65f,.9f,.65f):new Vector3(.8f,.6f,.8f);
                    if(authored.Add(kind,da*da+db*db<625?1:2,at,hit.normal,size*fade,(float)(pick*359),settings.VisualQuality>0?128:64)){Count++;continue;}
                }
                if(sunflower)
                {
                    var head=at+up*.7f*fade;
                    Triangle(verts,colors,indices,at-side*.025f,at+side*.025f,head,new Color(.3f,.34f,.28f));
                    for(int k=0;k<6;k++){double t=k*Math.PI/3,u=(k+1)*Math.PI/3;Triangle(verts,colors,indices,head,head+(side*(float)Math.Cos(t)+along*(float)Math.Sin(t))*.25f*fade,head+(side*(float)Math.Cos(u)+along*(float)Math.Sin(u))*.25f*fade,new Color(.8f,.83f,.84f));}
                }
                else if(stone)
                {
                    color=new Color(.39f,.37f,.32f);float size=(float)(.07+.22*settings.Terrain.Scatter(x,y,827))*fade;
                    for(int k=0;k<4;k++){double t=k*Math.PI/2,u=(k+1)*Math.PI/2;Triangle(verts,colors,indices,at+up*size,at+(side*(float)Math.Cos(t)+along*(float)Math.Sin(t))*size,at+(side*(float)Math.Cos(u)+along*(float)Math.Sin(u))*size,color);}
                }
                else if(litter||climate.Desert>.55)
                {
                    color=litter?new Color(.32f,.22f,.10f):new Color(.69f,.57f,.36f);float size=(litter?.28f:.5f)*fade;
                    Triangle(verts,colors,indices,at-side*size,at+along*size*.7f,at+side*size,color);
                }
                else
                {
                    float height=(float)(.18+.5*settings.Terrain.Scatter(x,y,829))*fade;
                    int blades=settings.VisualQuality>0?18:12;
                    for(int k=0;k<blades;k++)
                    {
                        double angle=settings.Terrain.Scatter(x,y,860+k)*Math.PI*2;
                        float spread=(float)Math.Sqrt(settings.Terrain.Scatter(x,y,890+k))*.9f;
                        var offset=(side*(float)Math.Cos(angle)+along*(float)Math.Sin(angle))*spread;
                        // Project the small clump onto the hit triangle's tangent plane.
                        offset-=hit.normal*Vector3.Dot(offset,hit.normal);
                        var foot=at+offset;var direction=Quaternion.AngleAxis((float)(angle*180/Math.PI),up)*side;
                        float h=height*(.55f+.65f*(float)settings.Terrain.Scatter(x,y,920+k));
                        var tint=Color.Lerp(color,new Color(.32f,.42f,.16f),.2f+.3f*(float)patch);
                        Triangle(verts,colors,indices,foot-direction*.065f,foot+direction*.065f,foot+up*h+along*.12f,tint);
                    }
                }
                Count++;
            }
            mesh.Clear();mesh.SetVertices(verts);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
            authored.Finish();
        }
        internal static void Triangle(List<Vector3> v,List<Color> c,List<int> t,Vector3 a,Vector3 b,Vector3 d,Color color)
        {int n=v.Count;v.Add(a);v.Add(b);v.Add(d);c.Add(color);c.Add(color);c.Add(color);t.AddRange(new[]{n,n+1,n+2,n+2,n+1,n});}
        public void Dispose(){authored.Dispose();UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(material);}
    }
}

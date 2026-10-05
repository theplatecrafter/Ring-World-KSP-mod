using System;
using System.Collections.Generic;
using Ringworld.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace NivenRingworld
{
    // Dense, continuous crowns use merged patch meshes, not one GameObject per tree.
    // Trunk contacts are activated only near loaded vessels; leaves never collide.
    internal sealed class ForestCanopy : MonoBehaviour
    {
        private struct Tree {internal Vector3 Position,Up;internal float Height,Radius,Yaw,Variation;internal bool Conifer;}
        private readonly List<Tree> trees=new List<Tree>();
        private readonly List<Mesh> meshes=new List<Mesh>();
        private readonly Dictionary<int,CapsuleCollider> contacts=new Dictionary<int,CapsuleCollider>();
        private readonly Stack<CapsuleCollider> pool=new Stack<CapsuleCollider>();
        private PhysicMaterial friction;private float nextContacts;
        internal int TreeCount {get{return trees.Count;}}
        internal int ContactCount {get{return contacts.Count;}}
        internal void Build(Settings s,double x0,double y0,double size,DVec anchor,Material foliage,PhysicMaterial ground)
        {
            friction=ground;double density=Math.Min(2,s.ForestDensity*StockGraphics.Scatter);
            if(density<=0)return;
            double spacing=24/Math.Sqrt(Math.Max(.2,density));
            int patches=(int)Math.Ceiling(size/256);double patchSize=size/patches;
            for(int py=0;py<patches;py++)for(int px=0;px<patches;px++)
            {
                var patch=new List<Tree>();double ax=x0+px*patchSize,by=y0+py*patchSize;
                // Global cells make the forest continuous across tile borders.
                for(long y=(long)Math.Ceiling(by/spacing);y*spacing<by+patchSize;y++)
                for(long x=(long)Math.Ceiling(ax/spacing);x*spacing<ax+patchSize;x++)
                {
                    double a=(x+.7*(s.Terrain.Scatter(x,y,2101)-.5))*spacing;
                    double b=(y+.7*(s.Terrain.Scatter(x,y,2103)-.5))*spacing;
                    if(Math.Abs(b)>s.Geometry.P.Width/2-20)continue;
                    var floor=s.Terrain.Sample(a,b);
                    if(!BiomePresentation.ForestAllowed(floor))continue;
                    // A smooth stand mask makes woods and glades, instead of independent clumps.
                    if(BiomePresentation.ForestMargin(s.Terrain,a,b)<0)continue;
                    var point=s.Geometry.Position(a,b,floor.Height);bool occupied=false;var f=RingworldFlight.Instance;
                    if(f!=null)foreach(var v in FlightGlobals.VesselsLoaded)
                        if(f.Owns(v)&&(f.Position(v)-point).Length<18){occupied=true;break;}
                    if(occupied)continue;
                    var tree=new Tree{Position=ConvertVector.Unity(point-anchor),Up=ConvertVector.Unity(s.Geometry.Up(point)),
                        Yaw=(float)(360*s.Terrain.Scatter(x,y,2119)),Variation=(float)s.Terrain.Scatter(x,y,2121),
                        Height=(float)(30+18*s.Terrain.Scatter(x,y,2113)),Radius=(float)(spacing*(.70+.15*s.Terrain.Scatter(x,y,2117))),
                        Conifer=s.Terrain.Noise(a,b,18000,601)>.5};
                    patch.Add(tree);trees.Add(tree);
                }
                if(patch.Count==0)continue;
                var root=new GameObject("Continuous forest patch");root.layer=15;root.transform.SetParent(transform,false);
                bool economy=s.ForestQuality==0;
                var visualPatch=patch;
                if(economy){visualPatch=new List<Tree>();for(int i=0;i<patch.Count;i+=4){var t=patch[i];t.Radius*=2;visualPatch.Add(t);}}
                var lods=new LOD[economy?1:3];
                for(int level=0;level<lods.Length;level++)
                {
                    var obj=new GameObject("Canopy LOD"+level);obj.layer=15;obj.transform.SetParent(root.transform,false);
                    var mesh=MakeMesh(visualPatch,economy?2:level);meshes.Add(mesh);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=foliage;
                    renderer.shadowCastingMode=!economy&&level==0?ShadowCastingMode.On:ShadowCastingMode.Off;
                    lods[level]=new LOD(economy?.0005f:new[]{.38f,.12f,.0005f}[level],new Renderer[]{renderer});
                }
                var group=root.AddComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();
            }
        }
        private static int DistantQuality(Settings s)
        {var f=RingworldFlight.Instance;return s.ForestQuality;}
        internal static double MaximumDistantBlock(Settings s)
        {return DistantQuality(s)==0?0:DistantQuality(s)==1?8192:DistantQuality(s)==2?16384:32768;}
        internal static IEnumerator<Mesh> DistantMesh(Settings s,LodBlock block,DVec anchor,double phase)
        {
            double density=Math.Min(2,s.ForestDensity*StockGraphics.Scatter);if(density<=0||DistantQuality(s)==0)yield break;
            int side=block.Size<=s.TileSize?32:DistantQuality(s)==1?8:DistantQuality(s)==2?16:24;
            double spacing=24/Math.Sqrt(Math.Max(.2,density));while(block.Size/spacing>side)spacing*=2;
            // A relocation can change the live orientation while this iterator is
            // paused. Build every row in the patch's original coordinate frame.
            var geometry=new RingGeometry(s.Geometry.P){OrientationRadians=phase,Basis=s.Geometry.Basis};
            var patch=new List<Tree>();
            for(long y=(long)Math.Ceiling(block.Y/spacing);y*spacing<block.Y+block.Size;y++)
            {
            for(long x=(long)Math.Ceiling(block.X/spacing);x*spacing<block.X+block.Size;x++)
            {
                double a=(x+.7*(s.Terrain.Scatter(x,y,2101)-.5))*spacing,b=(y+.7*(s.Terrain.Scatter(x,y,2103)-.5))*spacing;
                if(Math.Abs(b)>s.Geometry.P.Width/2-20)continue;
                var floor=s.Terrain.Sample(a,b);if(!BiomePresentation.ForestAllowed(floor)||BiomePresentation.ForestMargin(s.Terrain,a,b)<0)continue;
                var point=geometry.Position(a,b,floor.Height);
                patch.Add(new Tree{Position=ConvertVector.Unity(point-anchor),Up=ConvertVector.Unity(geometry.Up(point)),
                    Yaw=(float)(360*s.Terrain.Scatter(x,y,2119)),Variation=(float)s.Terrain.Scatter(x,y,2121),
                    Height=(float)(30+18*s.Terrain.Scatter(x,y,2113)),Radius=(float)(spacing*(.70+.15*s.Terrain.Scatter(x,y,2117))),
                    Conifer=s.Terrain.Noise(a,b,18000,601)>.5});
            }
            yield return null; // one sampled row per step, on the main thread
            }
            if(patch.Count>0)yield return MakeMesh(patch,2);
        }
        private static Mesh MakeMesh(List<Tree> patch,int lod)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var leaves=new List<int>();var trunks=new List<int>();
            foreach(var t in patch)
            {
                int leafStart=vertices.Count;
                var q=Quaternion.FromToRotation(Vector3.up,t.Up)*Quaternion.Euler(0,t.Yaw,0);int sides=lod==0?9:5;
                if(t.Conifer&&lod<2)
                    for(int tier=0;tier<3;tier++)Crown(vertices,leaves,t,q,t.Height*(.30f+tier*.16f),t.Height*(.68f+tier*.16f),t.Radius*(1-tier*.23f),sides,false);
                else Crown(vertices,leaves,t,q,t.Height*.48f,t.Height,t.Radius,sides,!t.Conifer);
                for(int k=leafStart;k<vertices.Count;k++)uv.Add(Atlas(1+(int)(t.Variation*2.99f),k-leafStart));
                int trunkStart=vertices.Count;
                if(lod<2)
                {
                    int first=vertices.Count;float radius=.38f;
                    for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;var p=new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);vertices.Add(t.Position+q*p);vertices.Add(t.Position+q*(p+Vector3.up*t.Height*.72f));}
                    for(int i=0;i<4;i++){int a=first+i*2,b=first+(i+1)%4*2;trunks.AddRange(new[]{a,a+1,b,b,a+1,b+1});}
                }
                for(int k=trunkStart;k<vertices.Count;k++)uv.Add(Atlas(0,k-trunkStart));
            }
            var mesh=new Mesh{name="Merged continuous forest crowns"};if(vertices.Count>65000)mesh.indexFormat=IndexFormat.UInt32;
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);leaves.AddRange(trunks);mesh.SetTriangles(leaves,0);mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
        }
        private static void Crown(List<Vector3> verts,List<int> tris,Tree t,Quaternion q,float bottom,float top,float radius,int n,bool round)
        {
            int first=verts.Count;
            verts.Add(t.Position+q*(Vector3.up*top));verts.Add(t.Position+q*(Vector3.up*bottom));
            int rings=round?3:1;
            for(int row=0;row<rings;row++)for(int i=0;i<n;i++)
            {
                float a=i*Mathf.PI*2/n;
                float uneven=1+.10f*Mathf.Sin(i*2.7f+t.Variation*13)+.06f*Mathf.Cos(i*4.1f+t.Yaw);
                float width=round?(row==1?1:.70f):1;
                float height=round?Mathf.Lerp(bottom,top,.2f+row*.3f):bottom;
                verts.Add(t.Position+q*new Vector3(Mathf.Cos(a)*radius*width*uneven,height+radius*.035f*Mathf.Sin(i*3+t.Yaw),Mathf.Sin(a)*radius*width*uneven*.86f));
            }
            for(int i=0;i<n;i++)
            {
                int a=first+2+i,b=first+2+(i+1)%n,upper=(rings-1)*n;
                tris.AddRange(new[]{first,b+upper,a+upper,first+1,a,b});
                for(int row=0;row<rings-1;row++)
                {int c=a+row*n,d=b+row*n;tris.AddRange(new[]{c,c+n,d,d,c+n,d+n});}
            }
        }
        private static Vector2 Atlas(int tile,int vertex)
        {
            // Stay inside a single atlas swatch, including at lower texture mip levels.
            return new Vector2((tile%4+.15f+(vertex%2)*.7f)/4f,(tile/4+.15f+((vertex/2)%2)*.7f)/4f);
        }
        private void Update()
        {
            if(Time.unscaledTime<nextContacts)return;nextContacts=Time.unscaledTime+.25f;
            var f=RingworldFlight.Instance;if(f==null)return;
            var nearby=new List<Vector3>();foreach(var v in FlightGlobals.VesselsLoaded)if(f.Owns(v))nearby.Add(transform.InverseTransformPoint(v.transform.position));
            var wanted=new HashSet<int>();
            for(int i=0;i<trees.Count;i++)foreach(var p in nearby)if((trees[i].Position-p).sqrMagnitude<180*180){wanted.Add(i);break;}
            var remove=new List<int>();foreach(var pair in contacts)if(!wanted.Contains(pair.Key)){pair.Value.enabled=false;pool.Push(pair.Value);remove.Add(pair.Key);}
            foreach(int i in remove)contacts.Remove(i);
            foreach(int i in wanted)if(!contacts.ContainsKey(i))
            {
                CapsuleCollider c;
                if(pool.Count>0)c=pool.Pop();else{var obj=new GameObject("Nearby forest trunk");obj.layer=15;obj.transform.SetParent(transform,false);c=obj.AddComponent<CapsuleCollider>();c.sharedMaterial=friction;}
                var t=trees[i];c.transform.localPosition=t.Position;c.transform.localRotation=Quaternion.FromToRotation(Vector3.up,t.Up);
                c.height=t.Height*.72f;c.radius=.38f;c.center=Vector3.up*c.height*.5f;c.enabled=true;contacts.Add(i,c);
            }
        }
        private void OnDestroy(){foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);}
    }
}


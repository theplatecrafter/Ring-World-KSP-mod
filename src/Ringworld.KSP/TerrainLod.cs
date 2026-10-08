using System;
using System.Collections.Generic;
using Ringworld.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace NivenRingworld
{
    // CPU quadtree LOD: globally aligned blocks, shared height function, bounded
    // generation per frame, per-block frustum culling and skirts at resolution seams.
    internal sealed class TerrainLod : IDisposable
    {
        private sealed class Patch
        {
            internal string BoundaryKey;internal bool Retired;internal IEnumerator<Mesh> CanopyWork;internal GameObject Root;internal Mesh Mesh,WaterMesh,CanopyMesh;internal DVec Anchor;internal double Phase;internal bool Scaled;internal Texture2D Texture;
        }
        private AssetBundle visualBundle;private bool nightShader;
        private readonly Settings settings;private readonly Material material,farMaterial,waterMaterial,forestMaterial;
        private readonly Dictionary<string,Patch> patches=new Dictionary<string,Patch>();
        private readonly Dictionary<string,Patch> staged=new Dictionary<string,Patch>();
        private bool building;
        private readonly Queue<Patch> canopyPending=new Queue<Patch>();
        internal int CanopyPending {get{return canopyPending.Count;}}
        private HashSet<string> wanted=new HashSet<string>();
        private List<LodBlock> pending=new List<LodBlock>();
        private readonly List<LodBlock> layout=new List<LodBlock>();
        private readonly Dictionary<Tuple<double,double,double>,Color> edgeColours=new Dictionary<Tuple<double,double,double>,Color>();
        private long lastX=long.MinValue,lastY;private double plannedAlong;
        internal int Count {get{return patches.Count;}}
        internal int Pending {get{return pending.Count+(building?1:0);}}
        internal int BuiltScaledCount {get{int count=0;foreach(var p in patches.Values)if(p.Scaled)count++;return count;}}
        internal int ScaledCount {get{int count=0;foreach(var p in patches.Values)if(p.Scaled&&p.Root.activeSelf)count++;return count;}}
        internal TerrainLod(Settings s,Material m,Material water,Material forest){settings=s;material=m;waterMaterial=water;forestMaterial=forest;farMaterial=new Material(m);farMaterial.color=Color.black;farMaterial.EnableKeyword("_EMISSION");farMaterial.SetColor("_EmissionColor",Color.white);farMaterial.SetFloat("_Glossiness",0);
            visualBundle=RingVisualAssets.Acquire();var shader=visualBundle!=null?visualBundle.LoadAsset<Shader>("Assets/Shaders/TerrainNight.shader"):null;
            if(shader!=null&&shader.isSupported){farMaterial.shader=shader;farMaterial.renderQueue=2010;nightShader=true;}
        }
        internal bool NeedsNearTile(long x,long y){return Math.Abs(x-lastX)<=settings.TileRadius&&Math.Abs(y-lastY)<=settings.TileRadius;}
        internal void Update(double along,double across,bool nearReady=true)
        {
            long x=(long)Math.Floor(along/settings.TileSize),y=(long)Math.Floor(across/settings.TileSize);
            if(!building&&(x!=lastX||y!=lastY))
            {
                building=true;lastX=x;lastY=y;plannedAlong=along;wanted.Clear();pending.Clear();layout.Clear();
                foreach(var b in TerrainLodPlan.Create(along,across,settings.TileSize,settings.TileRadius,Math.Min(settings.LodRange,settings.Geometry.P.Circumference/2+settings.Geometry.P.Width),settings.Geometry.P.Width/2,settings.Geometry.P.Circumference/2,Math.Sqrt(8*settings.Geometry.P.Radius*250)*settings.LodResolution))
                {
                    if(b.Y>=settings.Geometry.P.Width/2||b.Y+b.Size<=-settings.Geometry.P.Width/2)continue;
                    layout.Add(b);wanted.Add(b.Key);if(!patches.ContainsKey(b.Key))pending.Add(b);
                }
                // A retained block may now border a different coarse grid. Rebuild
                // its feathered texture with that topology instead of keeping a seam.
                foreach(var b in layout)
                {
                    Patch old;if(!patches.TryGetValue(b.Key,out old)||old.BoundaryKey==BoundaryKey(b))continue;
                    pending.Add(b);
                }
                pending.Sort((a,b)=>a.DistanceSquared(along,across).CompareTo(b.DistanceSquared(along,across)));
            }
            // Publish completed blocks within the frame budget; the coarse hull fills pending areas.
            for(int i=0;i<settings.GenerationBudget&&pending.Count>0;i++)
            {
                var b=pending[0];var replacement=Build(b);replacement.Root.SetActive(false);staged.Add(b.Key,replacement);pending.RemoveAt(0);
            }
            // Publish a complete generation atomically. Retain the visible layout
            // while its replacements build; never expose the coarse hull through holes.
            if(building&&pending.Count==0&&nearReady)
            {
                var remove=new List<string>();
                foreach(var kv in patches)if(!wanted.Contains(kv.Key)||staged.ContainsKey(kv.Key))remove.Add(kv.Key);
                foreach(var key in remove){patches[key].Root.SetActive(false);Destroy(patches[key]);patches.Remove(key);}
                foreach(var kv in staged){patches.Add(kv.Key,kv.Value);kv.Value.Root.SetActive(true);}
                staged.Clear();building=false;
            }
            // Yield between canopy rows; never spend a whole dense forest block
            // sampling terrain in one frame. Mesh publication stays on Unity's thread.
            var clock=System.Diagnostics.Stopwatch.StartNew();int steps=0;
            while(canopyPending.Count>0&&steps++<8*settings.GenerationBudget&&clock.Elapsed.TotalMilliseconds<2*settings.GenerationBudget)
            {
                var p=canopyPending.Peek();
                if(p.Retired){canopyPending.Dequeue();continue;}
                bool more=p.CanopyWork.MoveNext();var mesh=more?p.CanopyWork.Current:null;
                if(!more||mesh!=null)
                {
                    canopyPending.Dequeue();p.CanopyWork.Dispose();p.CanopyWork=null;
                    if(mesh!=null)
                    {
                        p.CanopyMesh=mesh;var canopy=new GameObject("Ring biome LOD canopy");canopy.layer=15;canopy.transform.SetParent(p.Root.transform,false);
                        canopy.AddComponent<MeshFilter>().sharedMesh=mesh;var cr=canopy.AddComponent<MeshRenderer>();cr.sharedMaterial=forestMaterial;cr.shadowCastingMode=ShadowCastingMode.Off;cr.receiveShadows=false;
                    }
                }
            }
        }
        // Blend the fine colour field toward the actual neighbouring coarse grid.
        // The opaque mesh and collision height are unchanged; no overlapping alpha
        // terrain or screen-wide blur is needed. Only a boundary strip is sampled.
        private string BoundaryKey(LodBlock b)
        {
            string key="";foreach(var o in layout)if(o.Size>b.Size&&o.X<=b.X+b.Size&&o.X+o.Size>=b.X&&o.Y<=b.Y+b.Size&&o.Y+o.Size>=b.Y)key+=o.Key+";";
            return key;
        }
        private Color GridColour(double a,double b,double footprint)
        {
            b=Math.Max(-settings.Geometry.P.Width/2+.01,Math.Min(settings.Geometry.P.Width/2-.01,b));
            var key=Tuple.Create(a,b,footprint);Color cached;if(edgeColours.TryGetValue(key,out cached))return cached;
            var ground=settings.Terrain.Sample(a,b);
            cached=TerrainTint.WithCanopy(ground,BiomePresentation.Sample(settings.Terrain,a,b,ground,footprint,Math.Min(2,settings.ForestDensity*StockGraphics.Scatter)));
            edgeColours[key]=cached;return cached;
        }
        private Color FeatherColour(LodBlock block,List<LodBlock> neighbours,double a,double b,Color fine)
        {
            foreach(var other in neighbours)
            {
                double d=Math.Sqrt(other.DistanceSquared(a,b)),width=block.Size*.3;
                if(d>=width)continue;
                double step=other.Size/settings.LodResolution;
                double gx=Math.Max(other.X,Math.Min(other.X+other.Size,a)),gy=Math.Max(other.Y,Math.Min(other.Y+other.Size,b));
                double x=other.X+Math.Min(settings.LodResolution-1,Math.Floor((gx-other.X)/step))*step;
                double y=other.Y+Math.Min(settings.LodResolution-1,Math.Floor((gy-other.Y)/step))*step;
                float tx=(float)((gx-x)/step),ty=(float)((gy-y)/step);
                Color coarse=Color.Lerp(Color.Lerp(GridColour(x,y,step),GridColour(x+step,y,step),tx),Color.Lerp(GridColour(x,y+step,step),GridColour(x+step,y+step,step),tx),ty);
                float t=(float)(d/width);t=t*t*(3-2*t);fine=Color.Lerp(coarse,fine,t);
            }
            return fine;
        }
        private Patch Build(LodBlock b)
        {
            // Near canopy resolves stand edges and low crown relief; far blocks retain
            // an area-filtered biome colour/height without individual tree draws.
            edgeColours.Clear();
            int n=settings.LodResolution;int count=(n+1)*(n+1);
            var neighbours=new List<LodBlock>();
            foreach(var other in layout)if(other.Size>b.Size&&other.X<=b.X+b.Size&&other.X+other.Size>=b.X&&other.Y<=b.Y+b.Size&&other.Y+other.Size>=b.Y)neighbours.Add(other);
            var p=new Patch{BoundaryKey=BoundaryKey(b),Root=new GameObject("Ring terrain LOD "+b.Key),Anchor=settings.Geometry.Position(b.X,b.Y,0),Phase=settings.Geometry.OrientationRadians};
            p.Scaled=b.Size>=65536&&b.DistanceSquared(lastX*settings.TileSize,lastY*settings.TileSize)>250000.0*250000;
            p.Root.layer=p.Scaled?10:15;
            var colors=new Color[count];
            var seabed=new Vector3[count];var wet=new bool[count];var waterUv=new Vector2[count];
            var vertices=new List<Vector3>(count+4*(n+1));var uv=new List<Vector2>(vertices.Capacity);var longitude=new List<Vector2>(vertices.Capacity);var triangles=new List<int>();
            for(int y=0;y<=n;y++)for(int x=0;x<=n;x++)
            {
                double a=Math.Max(plannedAlong-settings.Geometry.P.Circumference/2,Math.Min(plannedAlong+settings.Geometry.P.Circumference/2,b.X+b.Size*x/n)),c=b.Y+b.Size*y/n;double rawAcross=c;c=Math.Max(-settings.Geometry.P.Width/2,Math.Min(settings.Geometry.P.Width/2,c));var s=settings.Terrain.Sample(a,Math.Max(-settings.Geometry.P.Width/2+.01,Math.Min(settings.Geometry.P.Width/2-.01,c)));
                var appearance=BiomePresentation.Sample(settings.Terrain,a,c,s,b.Size/n,Math.Min(2,settings.ForestDensity*StockGraphics.Scatter));
                double h=(s.Wet?s.WaterHeight+.3:s.Height+(settings.NativeSurfaceScatters&&b.Size>ForestCanopy.MaximumDistantBlock(settings)?appearance.CanopyHeight:0))-.2;
                vertices.Add(ConvertVector.Unity(settings.Geometry.Position(a,c,h)-p.Anchor));
                seabed[y*(n+1)+x]=ConvertVector.Unity(settings.Geometry.Position(a,c,s.Height)-p.Anchor);
                wet[y*(n+1)+x]=s.Wet;waterUv[y*(n+1)+x]=new Vector2(s.Wet?(float)Math.Max(0,s.WaterHeight-s.Height):0,(float)(b.Size/n));
                uv.Add(new Vector2((x+.5f)/(n+1),(y+.5f)/(n+1)));longitude.Add(new Vector2((float)(a/settings.Geometry.P.Circumference),(float)(c/settings.Geometry.P.Width+.5)));colors[y*(n+1)+x]=FeatherColour(b,neighbours,a,c,TerrainTint.WithCanopy(s,appearance));
                if(x<n&&y<n&&rawAcross<settings.Geometry.P.Width/2&&rawAcross+b.Size/n>-settings.Geometry.P.Width/2){int i=y*(n+1)+x;triangles.AddRange(new[]{i,i+n+1,i+1,i+1,i+n+1,i+n+2});}
            }
            var f=RingworldFlight.Instance;
            if(!p.Scaled&&(settings.WaterQuality>0))
            {
                var waterIndices=new List<int>();
                for(int i=0;i<triangles.Count;i+=3)
                {
                    if(!(wet[triangles[i]]&&wet[triangles[i+1]]&&wet[triangles[i+2]]))continue;
                    var target=waterIndices;
                    target.Add(triangles[i]);target.Add(triangles[i+1]);target.Add(triangles[i+2]);
                }
                // Keep every ground triangle beneath transparent water.
                if(waterIndices.Count>0)
                {
                    p.WaterMesh=new Mesh{name="Ringworld LOD water"};p.WaterMesh.SetVertices(vertices);p.WaterMesh.uv=waterUv;p.WaterMesh.SetTriangles(waterIndices,0);p.WaterMesh.RecalculateNormals();p.WaterMesh.RecalculateBounds();var bounds=p.WaterMesh.bounds;bounds.Expand(4);p.WaterMesh.bounds=bounds;
                    var water=new GameObject("Ringworld LOD waves");water.layer=15;water.transform.SetParent(p.Root.transform,false);water.AddComponent<MeshFilter>().sharedMesh=p.WaterMesh;
                    var wr=water.AddComponent<MeshRenderer>();wr.sharedMaterial=waterMaterial;wr.shadowCastingMode=ShadowCastingMode.Off;
                    // Retain sampled bathymetry beneath the separate water surface. Old
                    // mean-water ground/skirt tops caused lines between water patches.
                    for(int i=0;i<count;i++)if(wet[i])vertices[i]=seabed[i];
                }
            }
            // Render-only skirts conceal T-junction gaps; physical ground is exclusively
            // the fine streamed collision mesh, never these large-distance triangles.
            for(int edge=0;!p.Scaled&&edge<4;edge++)
            {
                int previous=-1,previousTop=-1;
                for(int k=0;k<=n;k++)
                {
                    int top=edge==0?k:edge==1?k*(n+1)+n:edge==2?n*(n+1)+n-k:(n-k)*(n+1);
                    int bottom=vertices.Count;var point=p.Anchor+ConvertVector.Core(vertices[top]);
                    vertices.Add(vertices[top]-ConvertVector.Unity(settings.Geometry.Up(point))*(float)Math.Max(50,b.Size/n));uv.Add(uv[top]);longitude.Add(longitude[top]);
                    if(k>0)triangles.AddRange(new[]{previousTop,previous,top,top,previous,bottom,top,previous,previousTop,bottom,previous,top});
                    previous=bottom;previousTop=top;
                }
            }
            if(p.Scaled)for(int i=0;i<vertices.Count;i++)vertices[i]*=(float)ScaledSpace.InverseScaleFactor;
            p.Texture=TerrainTint.Texture(n+1,colors);
            p.Mesh=new Mesh{name="Adaptive ring terrain block"};p.Mesh.SetVertices(vertices);p.Mesh.SetUVs(0,uv);p.Mesh.SetUVs(1,longitude);p.Mesh.SetTriangles(triangles,0);p.Mesh.RecalculateNormals();p.Mesh.RecalculateBounds();
            p.Root.AddComponent<MeshFilter>().sharedMesh=p.Mesh;var renderer=p.Root.AddComponent<MeshRenderer>();renderer.sharedMaterial=p.Scaled?farMaterial:material;var block=new MaterialPropertyBlock();block.SetTexture("_MainTex",p.Texture);if(p.Scaled)block.SetTexture("_EmissionMap",p.Texture);renderer.SetPropertyBlock(block);
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            if(settings.NativeSurfaceScatters&&!p.Scaled&&b.Size<=ForestCanopy.MaximumDistantBlock(settings))
            {
                p.CanopyWork=ForestCanopy.DistantMesh(settings,b,p.Anchor,p.Phase);canopyPending.Enqueue(p);
            }
            return p;
        }
        internal void Reposition(Vector3d star)
        {
            // Skirts are crack covers below the terrain, not exposed structure.
            // From outside the hull the coarse closed underside owns the view.
            bool interior=true;
            if(MapView.MapIsEnabled&&PlanetariumCamera.Camera!=null)
            {
                var camera=ScaledSpace.ScaledToLocalSpace(PlanetariumCamera.Camera.transform.position)-settings.InertialCenter;
                var view=settings.Geometry.Coordinates(ConvertVector.Core(camera));
                interior=view.Altitude>=TerrainGenerator.MinimumHeight||Math.Abs(view.Across)>settings.Geometry.P.Width/2;
            }
            foreach(var p in patches.Values)
            {
                p.Root.SetActive(interior);
                double delta=(p.Scaled&&RingMapFrame.Active?settings.Geometry.P.Omega*Planetarium.GetUniversalTime():settings.Geometry.OrientationRadians)-p.Phase;
                var world=(p.Scaled&&RingMapFrame.Active?settings.InertialCenter:star)+ConvertVector.Ksp(settings.Geometry.RotateAroundAxis(p.Anchor,delta));
                p.Root.transform.position=p.Scaled?(Vector3)ScaledSpace.LocalToScaledSpace(world):(Vector3)world;
                p.Root.transform.rotation=settings.AxisRotation(delta);
            }
        }
        private static void Destroy(Patch p){p.Retired=true;if(p.CanopyWork!=null){p.CanopyWork.Dispose();p.CanopyWork=null;}UnityEngine.Object.Destroy(p.Root);UnityEngine.Object.Destroy(p.Mesh);if(p.CanopyMesh!=null)UnityEngine.Object.Destroy(p.CanopyMesh);if(p.WaterMesh!=null)UnityEngine.Object.Destroy(p.WaterMesh);UnityEngine.Object.Destroy(p.Texture);}
        public void Dispose(){foreach(var p in patches.Values)Destroy(p);patches.Clear();foreach(var p in staged.Values)Destroy(p);staged.Clear();canopyPending.Clear();UnityEngine.Object.Destroy(farMaterial);if(visualBundle!=null)RingVisualAssets.Release();}
        internal void Light(float light){if(!nightShader)farMaterial.SetColor("_EmissionColor",new Color(light,light,light));}
    }
}

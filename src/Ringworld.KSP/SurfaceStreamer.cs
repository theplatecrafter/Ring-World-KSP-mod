using System;
using System.Collections.Generic;
using Ringworld.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace NivenRingworld
{
    internal sealed class SurfaceStreamer : IDisposable
    {
        private sealed class Tile
        {
            internal GameObject Root,Scenery;
            internal DVec Anchor;
            internal long X,Y;internal int ForestQuality=-1;internal Texture2D Texture;
            internal double Phase;
            internal readonly List<Mesh> Meshes=new List<Mesh>();
        }
        private readonly Settings settings;
        private readonly Dictionary<string,Tile> tiles=new Dictionary<string,Tile>();
        private readonly List<GameObject> props=new List<GameObject>();
        private readonly List<DVec> propPositions=new List<DVec>();
        private string propSite="";
        private double propPhase;
        private readonly List<Quaternion> propRotations=new List<Quaternion>();
        private readonly Material terrainMaterial,waterMaterial,buildingMaterial,scrithMaterial,leavesMaterial,forestMaterial;
        private readonly Material rimMaterial;
        private readonly AssetBundle rimVisualBundle;
        private readonly Texture2D palette;
        private readonly Shader simpleWaterShader;
        private TerrainLod lod;
        private readonly LandmarkScenery landmarks;
        private float scatter=-1;
        private readonly PhysicMaterial groundFriction=new PhysicMaterial("Ringworld soil"){staticFriction=1f,dynamicFriction=.85f,bounciness=0,frictionCombine=PhysicMaterialCombine.Maximum,bounceCombine=PhysicMaterialCombine.Minimum};
        internal void RebuildLod(){lod.Dispose();lod=new TerrainLod(settings,terrainMaterial,waterMaterial,forestMaterial);}
        private readonly GameObject sunlightObject;private readonly Light sunlight;
        internal int LodCount {get{return lod.Count;}}
        internal int CanopyPending {get{return lod.CanopyPending;}}
        internal int SceneryPending {get{int count=0;foreach(var t in tiles.Values)if(t.ForestQuality!=settings.ForestQuality)count++;return count;}}
        internal int LodPending {get{return lod.Pending;}}
        internal int ScaledLodCount {get{return lod.ScaledCount;}}
        internal int BuiltScaledLodCount {get{return lod.BuiltScaledCount;}}
        internal int TileCount { get { return tiles.Count; } }
        internal SurfaceStreamer(Settings s)
        {
            settings=s;
            SceneryAssets.Acquire();
            landmarks=new LandmarkScenery(s);
            // A one-row palette avoids requiring a proprietary Unity asset bundle.
            palette=new Texture2D(16,1,TextureFormat.RGBA32,false);palette.filterMode=FilterMode.Point;palette.wrapMode=TextureWrapMode.Clamp;
            Color[] colors={new Color(.02f,.18f,.29f),new Color(.07f,.30f,.40f),new Color(.10f,.36f,.43f),new Color(.28f,.37f,.19f),
                new Color(.40f,.49f,.24f),new Color(.16f,.31f,.16f),new Color(.69f,.57f,.35f),new Color(.41f,.39f,.36f),new Color(.84f,.89f,.89f),
                new Color(.23f,.27f,.30f),new Color(.47f,.45f,.36f),new Color(.28f,.32f,.36f),new Color(.31f,.30f,.26f),Color.gray,Color.gray,Color.gray};
            palette.SetPixels(colors);palette.Apply();
            terrainMaterial=Material(new Color(1,1,1));terrainMaterial.mainTexture=palette;terrainMaterial.SetFloat("_Glossiness",.08f);
            sunlightObject=new GameObject("Ringworld habitat sunlight");sunlight=sunlightObject.AddComponent<Light>();
            sunlight.type=LightType.Directional;sunlight.cullingMask=1<<15;sunlight.color=new Color(1,.96f,.88f);sunlight.intensity=0;sunlight.shadows=LightShadows.Soft;sunlight.shadowBias=.05f;sunlight.shadowNormalBias=.4f;
            waterMaterial=Material(new Color(.07f,.31f,.42f));waterMaterial.SetFloat("_Glossiness",.8f);
            simpleWaterShader=waterMaterial.shader;
            buildingMaterial=Material(new Color(.64f,.60f,.48f));scrithMaterial=Material(new Color(.24f,.29f,.32f));
            // The scaled wall is already behind the Original sky (queue 1000).
            // Render the local collision shell there too, with the same dark shader,
            // rather than stamping a separately lit black strip over the atmosphere.
            rimVisualBundle=RingVisualAssets.Acquire();
            var terrainShader=rimVisualBundle!=null?rimVisualBundle.LoadAsset<Shader>("Assets/Shaders/TerrainTransition.shader"):null;
            if(terrainShader!=null&&terrainShader.isSupported)terrainMaterial.shader=terrainShader;
            var rimShader=rimVisualBundle!=null?rimVisualBundle.LoadAsset<Shader>("Assets/Shaders/DistantSurface.shader"):null;
            rimMaterial=new Material(rimShader!=null&&rimShader.isSupported?rimShader:(Shader.Find("Unlit/Color")??scrithMaterial.shader));
            rimMaterial.SetFloat("_Detail",-1);if(rimMaterial.HasProperty("_Color"))rimMaterial.color=new Color(.012f,.015f,.019f);rimMaterial.renderQueue=900;
            leavesMaterial=Material(new Color(.15f,.29f,.12f));leavesMaterial.SetFloat("_Glossiness",0);
            var forestPrefab=SceneryAssets.DetailPrefab("canopy_broadleaf");
            var forestRenderer=forestPrefab==null?null:forestPrefab.GetComponentInChildren<MeshRenderer>(true);
            forestMaterial=forestRenderer==null?new Material(leavesMaterial):new Material(forestRenderer.sharedMaterial);
            forestMaterial.SetFloat("_Glossiness",0);forestMaterial.SetFloat("_Metallic",0);
            forestMaterial.SetFloat("_SpecularHighlights",0);forestMaterial.SetFloat("_GlossyReflections",0);
            forestMaterial.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");forestMaterial.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            lod=new TerrainLod(settings,terrainMaterial,waterMaterial,forestMaterial);
        }
        private static Material Material(Color c)
        {
            Shader shader=Shader.Find("Standard")??Shader.Find("KSP/Diffuse")??Shader.Find("Diffuse");
            if(shader==null) throw new InvalidOperationException("No compatible terrain shader found.");
            return new Material(shader){color=c};
        }
        internal void Update(DVec observer,Vector3d star,bool immediate=false)
        {
            if(scatter!=StockGraphics.Scatter){foreach(var tile in tiles.Values)RebuildScenery(tile);if(scatter>=0)RebuildLod();scatter=StockGraphics.Scatter;}
            // Quality changes rebuild at most one existing scenery tile each frame.
            foreach(var tile in tiles.Values)if(tile.ForestQuality!=settings.ForestQuality){RebuildScenery(tile);break;}
            sunlight.shadows=QualitySettings.shadows==ShadowQuality.Disable?LightShadows.None:QualitySettings.shadows==ShadowQuality.HardOnly?LightShadows.Hard:LightShadows.Soft;
            RingPoint p=settings.Geometry.Coordinates(observer);
            Extensions.RingworldWater.Update(settings,waterMaterial,simpleWaterShader,observer,star);
            sunlight.transform.rotation=Quaternion.LookRotation(-ConvertVector.Unity(RingLighting.Direction(settings,observer,Planetarium.GetUniversalTime())),ConvertVector.Unity(settings.Geometry.Axis));
            long cx=(long)Math.Floor(p.Along/settings.TileSize),cy=(long)Math.Floor(p.Across/settings.TileSize);
            int radius=settings.TileRadius;
            var wanted=new HashSet<string>();
            var pending=new List<Tuple<long,long>>();
            for(int y=-radius;y<=radius;y++)for(int x=-radius;x<=radius;x++)
            {
                long tx=cx+x,ty=cy+y;
                if(ty*settings.TileSize>=settings.Geometry.P.Width/2||(ty+1)*settings.TileSize<=-settings.Geometry.P.Width/2)continue;
                string key=tx+":"+ty;wanted.Add(key);
                if(!tiles.ContainsKey(key))pending.Add(Tuple.Create(tx,ty));
            }
            pending.Sort((a,b)=>((a.Item1-cx)*(a.Item1-cx)+(a.Item2-cy)*(a.Item2-cy)).CompareTo((b.Item1-cx)*(b.Item1-cx)+(b.Item2-cy)*(b.Item2-cy)));
            // Keep old colliders until replacement tiles exist. Entry builds all tiles before release.
            int budget=immediate?pending.Count:2;
            for(int i=0;i<Math.Min(budget,pending.Count);i++)
            {
                var c=pending[i];var t=Build(c.Item1,c.Item2);tiles.Add(c.Item1+":"+c.Item2,t);
            }
            lod.Update(p.Along,p.Across,pending.Count<=budget);
            var remove=new List<string>();
            if(lod.Pending==0&&pending.Count<=budget)foreach(var kv in tiles)
                if(!wanted.Contains(kv.Key)&&!lod.NeedsNearTile(kv.Value.X,kv.Value.Y))remove.Add(kv.Key);
            foreach(string key in remove){Destroy(tiles[key]);tiles.Remove(key);}
            double distance;var site=settings.Terrain.Nearest(p.Along,p.Across,out distance);
            string id=site!=null&&distance<8000?site.Id:"";
            if(id!=propSite){ClearProps();propSite=id;if(id!=""){propPhase=settings.Geometry.OrientationRadians;BuildProps(site);}}
            Reposition(star);
            landmarks.Update(observer,star,groundFriction);
        }
        private double Across(double across){return Math.Max(-settings.Geometry.P.Width/2,Math.Min(settings.Geometry.P.Width/2,across));}
        private TerrainSample FloorSample(double along,double across)
        {double edge=settings.Geometry.P.Width/2-.01;return settings.Terrain.Sample(along,Math.Max(-edge,Math.Min(edge,across)));}
        private double FloorHeight(double along,double across,bool includeWater)
        {
            double half=settings.Geometry.P.Width/2;
            if(Math.Abs(across)>half)return settings.Terrain.Sample(along,across).Height;
            double spacing=settings.TileSize/settings.TileResolution;
            double x=Math.Floor(along/spacing)*spacing,y=Math.Floor(Math.Min(across,half-.0001)/spacing)*spacing;
            double low=Across(y),high=Across(y+spacing);
            double u=(along-x)/spacing,v=high>low?(across-low)/(high-low):0;
            var a=FloorSample(x,low);var b=FloorSample(x+spacing,low);var c=FloorSample(x,high);var d=FloorSample(x+spacing,high);
            double floor=u+v<=1?a.Height+(b.Height-a.Height)*u+(c.Height-a.Height)*v:d.Height+(c.Height-d.Height)*(1-u)+(b.Height-d.Height)*(1-v);
            if(includeWater&&u+v<=1&&(a.Wet||b.Wet||c.Wet))floor=Math.Max(floor,WaterVertex(a)+(WaterVertex(b)-WaterVertex(a))*u+(WaterVertex(c)-WaterVertex(a))*v);
            else if(includeWater&&u+v>1&&(b.Wet||c.Wet||d.Wet))floor=Math.Max(floor,WaterVertex(d)+(WaterVertex(c)-WaterVertex(d))*(1-u)+(WaterVertex(b)-WaterVertex(d))*(1-v));
            return floor;
        }
        internal double CollisionHeight(double along,double across){return FloorHeight(along,across,false);}
        internal double CameraFloor(double along,double across){return FloorHeight(along,across,false);}
        internal double GroundOrWaterFloor(double along,double across){return FloorHeight(along,across,true);}
        private static int[] Nondegenerate(Vector3[] vertices,int[] indices)
        {
            var result=new List<int>(indices.Length);
            for(int i=0;i<indices.Length;i+=3)if(Vector3.Cross(vertices[indices[i+1]]-vertices[indices[i]],vertices[indices[i+2]]-vertices[indices[i]]).sqrMagnitude>1e-8f)
            {result.Add(indices[i]);result.Add(indices[i+1]);result.Add(indices[i+2]);}
            return result.ToArray();
        }
        private double WaterVertex(TerrainSample s)
        {
            var f=RingworldFlight.Instance;bool waves=settings.WaterQuality>1;
            return (double.IsNegativeInfinity(s.WaterHeight)?s.Height-1:s.WaterHeight)+.1+(waves?settings.WaveHeight:0);
        }
        internal void Reposition(Vector3d star)
        {
            foreach(var t in tiles.Values)Place(t.Root,t.Anchor,t.Phase,Quaternion.identity,star);
            for(int i=0;i<props.Count;i++)Place(props[i],propPositions[i],propPhase,propRotations[i],star);
            lod.Reposition(star);
        }
        private void Place(GameObject obj,DVec anchor,double phase,Quaternion rotation,Vector3d star)
        {
            double delta=settings.Geometry.OrientationRadians-phase;
            obj.transform.position=(Vector3)(star+ConvertVector.Ksp(settings.Geometry.RotateAroundAxis(anchor,delta)));
            obj.transform.rotation=settings.AxisRotation(delta)*rotation;
        }
        internal void Light(double daylight)
        {
            float light=(float)(.08+.92*daylight);
            sunlight.intensity=(float)(daylight*.5);lod.Light(light);
            terrainMaterial.color=new Color(light,light,light);buildingMaterial.color=new Color(.64f*light,.60f*light,.48f*light);
            waterMaterial.color=new Color(.07f*light,.31f*light,.42f*light);
            scrithMaterial.color=new Color(.24f*light,.29f*light,.32f*light);leavesMaterial.color=new Color(.15f*light,.29f*light,.12f*light);forestMaterial.color=new Color(light,light,light);
        }
        private Tile Build(long tx,long ty)
        {
            double size=settings.TileSize,x0=tx*size,y0=ty*size;int n=settings.TileResolution;
            var t=new Tile{Root=new GameObject("Ringworld terrain "+tx+","+ty),Anchor=settings.Geometry.Position(x0,y0,0),X=tx,Y=ty,Phase=settings.Geometry.OrientationRadians};
            t.Root.layer=15;
            int count=(n+1)*(n+1);var vertices=new Vector3[count];var uv=new Vector2[count];var heights=new double[count];
            var colors=new Color[count];
            var waterVerts=new Vector3[count];var waterUv=new Vector2[count];var wet=new bool[count];
            for(int y=0;y<=n;y++)for(int x=0;x<=n;x++)
            {
                int i=y*(n+1)+x;double a=x0+size*x/n,b=Across(y0+size*y/n);
                var sample=FloorSample(a,b);heights[i]=sample.Height;
                vertices[i]=ConvertVector.Unity(settings.Geometry.Position(a,b,sample.Height)-t.Anchor);
                uv[i]=new Vector2((x+.5f)/(n+1),(y+.5f)/(n+1));colors[i]=TerrainTint.WithCanopy(sample,BiomePresentation.Sample(settings.Terrain,a,b,sample,size/n,settings.ForestDensity*StockGraphics.Scatter));
                wet[i]=sample.Wet;
                double water=double.IsNegativeInfinity(sample.WaterHeight)?sample.Height-1:sample.WaterHeight;
                waterVerts[i]=ConvertVector.Unity(settings.Geometry.Position(a,b,water+.1)-t.Anchor);
                waterUv[i]=new Vector2((float)Math.Max(0,water-sample.Height),(float)(size/n));
            }
            var indices=new List<int>(n*n*6);var waterIndices=new List<int>();
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                if(y0+size*y/n>=settings.Geometry.P.Width/2||y0+size*(y+1)/n<=-settings.Geometry.P.Width/2)continue;
                int a=y*(n+1)+x,b=a+1,c=a+n+1,d=c+1;
                // Across cross Along points inward with this ring parameterization.
                Add(indices,a,c,b);Add(indices,b,c,d);
                if(wet[a]||wet[b]||wet[c])Add(waterIndices,a,c,b);
                if(wet[b]||wet[d]||wet[c])Add(waterIndices,b,c,d);
            }
            Mesh ground=new Mesh{name="Ringworld ground"};ground.vertices=vertices;ground.uv=uv;ground.SetTriangles(indices,0);ground.RecalculateNormals();ground.RecalculateBounds();t.Meshes.Add(ground);
            RingworldTerrainApi.Add(settings.RingId,t.Root,ground,x0,y0,size);
            t.Texture=TerrainTint.Texture(n+1,colors);
            t.Root.AddComponent<MeshFilter>().sharedMesh=ground;var renderer=t.Root.AddComponent<MeshRenderer>();renderer.sharedMaterial=terrainMaterial;
            var colorBlock=new MaterialPropertyBlock();colorBlock.SetTexture("_MainTex",t.Texture);renderer.SetPropertyBlock(colorBlock);
            var shellVertices=new Vector3[count*2];Array.Copy(vertices,shellVertices,count);
            for(int y=0;y<=n;y++)for(int x=0;x<=n;x++)
                shellVertices[count+y*(n+1)+x]=ConvertVector.Unity(settings.Geometry.Position(x0+size*x/n,Across(y0+size*y/n),settings.UndersideAltitude)-t.Anchor);
            var shellIndices=GroundShell.Triangles(n);
            var shell=new Mesh{name="Closed ring collision shell"};shell.vertices=shellVertices;shell.triangles=Nondegenerate(shellVertices,shellIndices);shell.RecalculateBounds();t.Meshes.Add(shell);
            var floorCollider=t.Root.AddComponent<MeshCollider>();floorCollider.sharedMesh=shell;floorCollider.sharedMaterial=groundFriction;
            // Render only underside and edges here; the terrain renderer owns the top.
            var undersideIndices=new int[shellIndices.Length-n*n*6];Array.Copy(shellIndices,n*n*6,undersideIndices,0,undersideIndices.Length);
            var undersideMesh=new Mesh{name="Scrith underside and edges"};undersideMesh.vertices=shellVertices;undersideMesh.triangles=Nondegenerate(shellVertices,undersideIndices);undersideMesh.RecalculateNormals();undersideMesh.RecalculateBounds();t.Meshes.Add(undersideMesh);
            var underside=new GameObject("Ring structural underside");underside.layer=15;underside.transform.SetParent(t.Root.transform,false);
            underside.AddComponent<MeshFilter>().sharedMesh=undersideMesh;var undersideRenderer=underside.AddComponent<MeshRenderer>();undersideRenderer.sharedMaterial=scrithMaterial;
            undersideRenderer.shadowCastingMode=ShadowCastingMode.Off;
            AddRimWalls(t,x0,y0,size);
            RebuildScenery(t);
            if(waterIndices.Count>0)
            {
                var water=new GameObject("Water");water.layer=15;water.transform.SetParent(t.Root.transform,false);
                var mesh=new Mesh{name="Ringworld water"};mesh.vertices=waterVerts;mesh.uv=waterUv;mesh.SetTriangles(waterIndices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();var bounds=mesh.bounds;bounds.Expand(4);mesh.bounds=bounds;t.Meshes.Add(mesh);
                water.AddComponent<MeshFilter>().sharedMesh=mesh;water.AddComponent<MeshRenderer>().sharedMaterial=waterMaterial;
            }
            return t;
        }
        private void AddRimWalls(Tile t,double x0,double y0,double size)
        {
            foreach(int sign in new[]{-1,1})
            {
                double edge=sign*settings.Geometry.P.Width/2;
                if(sign>0?(edge<=y0||edge>y0+size):(edge<y0||edge>=y0+size))continue;
                var v=new Vector3[8];double outer=edge+sign*settings.StructuralThickness;
                for(int end=0;end<2;end++)for(int j=0;j<4;j++)
                    v[end*4+j]=ConvertVector.Unity(settings.Geometry.Position(x0+end*size,j<2?edge:outer,j==1||j==2?settings.Geometry.P.WallHeight:settings.UndersideAltitude)-t.Anchor);
                var faceVertices=new List<Vector3>();var faces=new List<int>();Vector3 center=Vector3.zero;foreach(var point in v)center+=point/8;
                int[][] quads={new[]{0,1,5,4},new[]{1,2,6,5},new[]{2,3,7,6},new[]{3,0,4,7},new[]{0,3,2,1},new[]{4,5,6,7}};
                foreach(var q in quads)
                {
                    var normal=Vector3.Cross(v[q[1]]-v[q[0]],v[q[2]]-v[q[0]]);
                    var faceCenter=(v[q[0]]+v[q[1]]+v[q[2]]+v[q[3]])/4;
                    if(Vector3.Dot(normal,faceCenter-center)<0){int swap=q[1];q[1]=q[3];q[3]=swap;}
                    int start=faceVertices.Count;foreach(int index in q)faceVertices.Add(v[index]);
                    faces.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
                }
                var mesh=new Mesh{name="Solid atmosphere retaining rim wall"};mesh.SetVertices(faceVertices);mesh.triangles=faces.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();t.Meshes.Add(mesh);
                var wall=new GameObject("Scrith rim wall");wall.layer=15;wall.transform.SetParent(t.Root.transform,false);
                wall.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=wall.AddComponent<MeshRenderer>();renderer.sharedMaterial=rimMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;wall.AddComponent<MeshCollider>().sharedMesh=mesh;
            }
        }
        private void TileProp(Tile t,string name,double a,double b,double altitude,Vector3 scale,Material mat,PrimitiveType shape,string assetKind=null)
        {
            var point=settings.Geometry.Position(a,b,altitude);
            var obj=SceneryAssets.Create(assetKind??(name=="Weathered rural habitation"?"rural_building":name=="Weathered boulder"?"boulder":""),(int)(t.X^t.Y));bool imported=obj!=null;
            if(obj==null)obj=GameObject.CreatePrimitive(shape);obj.name=name;obj.layer=15;obj.transform.SetParent(t.Scenery.transform,false);
            obj.transform.localPosition=ConvertVector.Unity(point-t.Anchor);
            obj.transform.localRotation=Quaternion.FromToRotation(Vector3.up,ConvertVector.Unity(settings.Geometry.Up(point)));
            obj.transform.localScale=scale;if(!imported)obj.GetComponent<Renderer>().sharedMaterial=mat;
            if(imported)foreach(var collider in obj.GetComponentsInChildren<Collider>())collider.sharedMaterial=groundFriction;
        }
        private void RebuildScenery(Tile t)
        {
            t.ForestQuality=settings.ForestQuality;
            if(t.Scenery!=null)UnityEngine.Object.Destroy(t.Scenery);t.Scenery=new GameObject("Ring tile scenery");t.Scenery.transform.SetParent(t.Root.transform,false);
            double old=settings.Geometry.OrientationRadians;settings.Geometry.OrientationRadians=t.Phase;
            try{AddScenery(t,t.X*settings.TileSize,t.Y*settings.TileSize,settings.TileSize);}finally{settings.Geometry.OrientationRadians=old;}
        }
        private void AddScenery(Tile t,double x0,double y0,double size)
        {
            AddForestGroves(t,x0,y0,size);
            for(int y=0;y<8&&StockGraphics.Scatter>0;y++)for(int x=0;x<8;x++)
            {
                long cellX=t.X*8+x,cellY=t.Y*8+y;
                double a=x0+(settings.GenerationVersion>=3?settings.Terrain.Scatter(cellX,cellY,91)*size:(x+.08+.84*settings.Terrain.Scatter(cellX,cellY,91))*size/8);
                double b=y0+(settings.GenerationVersion>=3?settings.Terrain.Scatter(cellX,cellY,93)*size:(y+.08+.84*settings.Terrain.Scatter(cellX,cellY,93))*size/8);
                if(Math.Abs(b)>=settings.Geometry.P.Width/2)continue;
                var sample=settings.Terrain.Sample(a,b);if(sample.Wet||sample.Biome==Biome.Rimwall||sample.Biome==Biome.Ruins)continue;
                double variation=settings.Terrain.Scatter(cellX,cellY,97);
                if(settings.Terrain.Scatter(cellX,cellY,907)>StockGraphics.Scatter)continue;
                double grove=settings.Terrain.Noise(a,b,1100,99);
                if(settings.GenerationVersion>=4?variation<Ecology.Sample(settings.Terrain,a,b).TreeCover*grove*settings.ForestDensity:(sample.Biome==Biome.Forest&&variation<grove*.85*settings.ForestDensity)||(sample.Biome==Biome.Grassland&&variation>1-.08*settings.ForestDensity&&grove>.55))
                {
                    float h=(float)(8+12*variation);
                    bool conifer=settings.GenerationVersion>=3&&settings.Terrain.Noise(a,b,18000,601)>.5;
                    var model=SceneryAssets.Create(conifer?"tree_conifer":"tree_broadleaf",(int)(variation*100000));
                    if(model!=null)
                    {
                        var point=settings.Geometry.Position(a,b,sample.Height);model.transform.SetParent(t.Scenery.transform,false);
                        model.transform.localPosition=ConvertVector.Unity(point-t.Anchor);
                        model.transform.localRotation=Quaternion.FromToRotation(Vector3.up,ConvertVector.Unity(settings.Geometry.Up(point)))*Quaternion.Euler(0,(float)(variation*360),0);
                        model.transform.localScale=Vector3.one*h;
                        foreach(var collider in model.GetComponentsInChildren<Collider>())collider.sharedMaterial=groundFriction;
                        continue;
                    }
                    TileProp(t,"Procedural tree trunk",a,b,sample.Height+h/2,new Vector3(1.5f,h/2,1.5f),scrithMaterial,PrimitiveType.Cylinder);
                    if(conifer)
                        for(int tier=0;tier<3;tier++)TileProp(t,"Conifer foliage tier",a,b,sample.Height+h*(.6+tier*.18),new Vector3(h*(.65f-tier*.16f),h*.35f,h*(.65f-tier*.16f)),leavesMaterial,PrimitiveType.Sphere);
                    else TileProp(t,"Broadleaf canopy",a,b,sample.Height+h,settings.GenerationVersion>=3?new Vector3(h*.85f,h*.55f,h*.7f):new Vector3(h*.7f,h*.7f,h*.7f),leavesMaterial,PrimitiveType.Sphere);
                }
                else if(variation>.9&&sample.Biome!=Biome.Road)
                {
                    if(sample.Shore>.1)
                        TileProp(t,"Shore driftwood",a,b,sample.Height+.4,new Vector3(4,.8f,2),scrithMaterial,PrimitiveType.Sphere,"driftwood");
                    else if(sample.Biome==Biome.Forest&&variation>.94)
                    {
                        string[] kinds={"dead_tree","stump","fallen_log","root_cluster","bush"};int which=(int)(settings.Terrain.Scatter(cellX,cellY,953)*kinds.Length)%kinds.Length;
                        Vector3 propSize=which==0?new Vector3(3,6,3):which==1?new Vector3(2,1.2f,2):which==2?new Vector3(4,1,2):which==3?new Vector3(3,.6f,3):new Vector3(2,1.5f,2);
                        TileProp(t,"Forest "+kinds[which],a,b,sample.Height+propSize.y*.5,propSize,scrithMaterial,PrimitiveType.Sphere,kinds[which]);
                    }
                    else TileProp(t,"Weathered boulder",a,b,sample.Height+1,new Vector3(4,3,5),scrithMaterial,PrimitiveType.Sphere);
                }
            }
            double ca=x0+size*(.2+.6*settings.Terrain.Scatter(t.X,t.Y,103)),cb=y0+size*(.2+.6*settings.Terrain.Scatter(t.X,t.Y,107));var center=settings.Terrain.Sample(ca,cb);
            if(Math.Abs(cb)<settings.Geometry.P.Width/2&&(center.Biome==Biome.Grassland||center.Biome==Biome.Desert)&&(settings.GenerationVersion>=3?settings.Terrain.Scatter(t.X,t.Y,101):settings.Terrain.Noise(ca,cb,50,101))>.97)
            {
                for(int i=-2;i<=2;i++)
                {
                    double a=ca+i*48+(settings.Terrain.Scatter(t.X+i,t.Y,109)-.5)*25;
                    double b=cb+(settings.Terrain.Scatter(t.X+i,t.Y,113)-.5)*120;
                    if(Math.Abs(b)>=settings.Geometry.P.Width/2)continue;
                    var sample=settings.Terrain.Sample(a,b);if(sample.Wet||sample.Biome==Biome.Road)continue;
                    float height=(float)(5+settings.Terrain.Scatter(t.X+i,t.Y,127)*19);
                    TileProp(t,"Weathered rural habitation",a,b,sample.Height+height/2,new Vector3(18,height,24),buildingMaterial,PrimitiveType.Cube);
                }
            }
        }
        private static void Add(List<int> a,int x,int y,int z){a.Add(x);a.Add(y);a.Add(z);}
        private void AddForestGroves(Tile t,double x0,double y0,double size)
        {
            if(StockGraphics.Scatter<=0||settings.ForestDensity<=0)return;
            t.Scenery.AddComponent<ForestCanopy>().Build(settings,x0,y0,size,t.Anchor,forestMaterial,groundFriction);
        }
        private bool Prop(string name,double along,double across,double height,Vector3 size,Material mat,PrimitiveType shape=PrimitiveType.Cube,string assetKind=null)
        {
            var position=settings.Geometry.Position(along,across,height);
            string kind=name=="Research plinth"?"research_station":name=="Exposed scrith plate"?"scrith_outcrop":name=="Habitat block"?"habitat_block":name=="Abandoned tower"?"ruin_tower":name=="Roof machinery"?"roof_machinery":name=="Transport causeway"?"transport_causeway":name=="Rim transport gantry"?"terminal_gantry":"";
            var obj=SceneryAssets.Create(assetKind??kind,0);bool imported=obj!=null;if(obj==null)obj=GameObject.CreatePrimitive(shape);obj.name=name;obj.layer=15;
            obj.transform.rotation=Quaternion.FromToRotation(Vector3.up,ConvertVector.Unity(settings.Geometry.Up(position)));
            obj.transform.localScale=size;if(!imported)obj.GetComponent<Renderer>().sharedMaterial=mat;
            foreach(var collider in obj.GetComponentsInChildren<Collider>())collider.sharedMaterial=groundFriction;
            props.Add(obj);propPositions.Add(position);propRotations.Add(obj.transform.rotation);
            return imported;
        }
        private void Decor(Landmark site,string kind,double da,double db,Vector3 size)
        {
            double a=site.Along+da,b=site.Across+db;if(Math.Abs(b)>=settings.Geometry.P.Width/2)return;
            var sample=settings.Terrain.Sample(a,b);if(sample.Wet)return;
            var f=RingworldFlight.Instance;var v=FlightGlobals.ActiveVessel;
            if(f!=null&&f.Active&&v!=null&&f.Owns(v)&&(f.Position(v)-settings.Geometry.Position(a,b,sample.Height)).Length<Math.Max(size.x,Math.Max(size.y,size.z))*1.5+50)return;
            Prop("Ringworld "+kind,a,b,sample.Height+size.y*.5,size,scrithMaterial,PrimitiveType.Cube,kind);
        }
        private void BuildProps(Landmark l)
        {
            if(l.Id=="spill")
            {
                Decor(l,"flup_outlet",220,0,new Vector3(80,60,100));Decor(l,"sediment_nozzle",220,140,new Vector3(35,30,60));Decor(l,"pump_station",320,0,new Vector3(60,45,65));Decor(l,"maintenance_platform",320,130,new Vector3(55,20,55));
                return;
            }
            if(l.Kind=="ocean"||l.Kind=="mountain"||l.Kind=="puncture"||l.Kind=="waterway")return;
            double h=settings.Terrain.Sample(l.Along,l.Across).Height;
            Prop("Research plinth",l.Along+45,l.Across,h+3,new Vector3(9,6,9),scrithMaterial);
            if(l.Kind=="scrith")
            { Prop("Exposed scrith plate",l.Along,l.Across,h+.3,new Vector3(90,.6f,90),scrithMaterial);Decor(l,"conduit_live",150,0,new Vector3(65,.5f,40));Decor(l,"conduit_dark",150,90,new Vector3(65,.5f,40));return; }
            int n=l.Kind=="city"?7:3;
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                if(x==n/2&&y==n/2)continue;
                double jitterA=(settings.Terrain.Scatter(x,y,151)-.5)*22,jitterB=(settings.Terrain.Scatter(x,y,157)-.5)*22;
                double a=l.Along+(x-n/2)*95+jitterA,b=l.Across+(y-n/2)*95+jitterB;
                float bh=l.Kind=="city"?18+(x*17+y*31)%105:12+(x*7+y*13)%15;
                double floor=settings.Terrain.Sample(a,b).Height;
                bool importedBlock=Prop(l.Kind=="city"?"Abandoned tower":"Habitat block",a,b,floor+bh/2,new Vector3(36,bh,42),buildingMaterial);
                Prop("Roof machinery",a,b,floor+bh+2,new Vector3(24,4,30),scrithMaterial);
                // Dark window bands are shallow physical trim, requiring no imported textures.
                if(!importedBlock)for(int f=1;f<bh/9;f++)Prop("Window belt",a,b,floor+f*9,new Vector3(36.3f,1.8f,42.3f),scrithMaterial);
            }
            Prop("Transport causeway",l.Along,l.Across,h+.5,new Vector3(28,1,650),scrithMaterial);
            if(l.Kind=="terminal")Prop("Rim transport gantry",l.Along+130,l.Across,h+45,new Vector3(20,90,20),scrithMaterial);
            if(l.Kind=="terminal")
            {
                Decor(l,"rim_hatch",260,0,new Vector3(18,8,20));Decor(l,"rim_airlock",320,0,new Vector3(24,15,24));Decor(l,"elevator_base",420,0,new Vector3(35,70,35));Decor(l,"transit_tube",260,-100,new Vector3(25,20,80));Decor(l,"maglev_segment",360,-100,new Vector3(20,2,80));
            }
            else if(l.Kind=="city")
            {
                Decor(l,"fallen_city",650,0,new Vector3(220,59,218));
                Decor(l,"ruined_district",900,100,new Vector3(180,70,134));Decor(l,"city_arcology",850,-160,new Vector3(80,92,80));Decor(l,"city_concourse",400,-150,new Vector3(120,34,34));Decor(l,"city_disk_fragment",650,230,new Vector3(110,30,100));Decor(l,"levitation_grid",450,340,new Vector3(70,3,60));Decor(l,"debris_beam",550,340,new Vector3(55,8,14));
            }
            else
            {
                Decor(l,"stone_enclosure",280,0,new Vector3(20,2,20));Decor(l,"watchtower",320,0,new Vector3(9,16,9));Decor(l,"campfire",280,40,new Vector3(3,.5f,3));Decor(l,"bridge_segment",280,110,new Vector3(35,4,10));
            }
        }
        private void ClearProps(){foreach(var p in props)UnityEngine.Object.Destroy(p);props.Clear();propPositions.Clear();propRotations.Clear();}
        private static void Destroy(Tile t){RingworldTerrainApi.Remove(t.Root);UnityEngine.Object.Destroy(t.Texture);UnityEngine.Object.Destroy(t.Root);foreach(var m in t.Meshes)UnityEngine.Object.Destroy(m);}
        public void Dispose()
        {
            Extensions.WaterScreenCopy.Enable(false);Extensions.RingUnderwater.Enable(false);
            foreach(var t in tiles.Values)Destroy(t);tiles.Clear();ClearProps();
            SceneryAssets.Release();
            landmarks.Dispose();
            lod.Dispose();UnityEngine.Object.Destroy(groundFriction);UnityEngine.Object.Destroy(sunlightObject);
            UnityEngine.Object.Destroy(terrainMaterial);UnityEngine.Object.Destroy(waterMaterial);UnityEngine.Object.Destroy(buildingMaterial);UnityEngine.Object.Destroy(scrithMaterial);UnityEngine.Object.Destroy(leavesMaterial);UnityEngine.Object.Destroy(forestMaterial);UnityEngine.Object.Destroy(palette);
            UnityEngine.Object.Destroy(rimMaterial);if(rimVisualBundle!=null)RingVisualAssets.Release();
        }
    }
}


using System;
using System.Collections.Generic;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    internal static class RingLighting
    {
        private sealed class RingBlocker
        {
            internal Settings Settings;internal DVec Center;
            internal readonly DVec[] Panels=new DVec[20];internal readonly RingBasis[] Basis=new RingBasis[20];
        }
        private sealed class Scene
        {
            internal double Time=double.NaN,GpuTime=double.NaN;internal CelestialBody Star;internal DVec Light;
            internal Texture2D Texture;
            internal readonly List<Tuple<DVec,double>> Bodies=new List<Tuple<DVec,double>>();
            internal readonly List<RingBlocker> Rings=new List<RingBlocker>();
        }
        private static readonly Dictionary<Settings,Scene> scenes=new Dictionary<Settings,Scene>();
        internal static CelestialBody Illuminator(Settings s)
        {
            CelestialBody body=null;
            if(s.AnchorId!=null&&s.AnchorId.StartsWith("body:",StringComparison.Ordinal))body=FlightGlobals.Bodies.Find(b=>RingAnchorEphemeris.Id(b)==s.AnchorId);
            else if(s.AnchorId!=null){var vessel=FlightGlobals.Vessels.Find(v=>RingAnchorEphemeris.Id(v)==s.AnchorId);if(vessel!=null)body=vessel.mainBody;}
            if(body==null)body=s.Body;var visited=new HashSet<CelestialBody>();
            while(body!=null&&visited.Add(body)){if(body.isStar)return body;body=body.referenceBody;}
            return FlightGlobals.Bodies.Find(b=>b.isStar);
        }
        private static Scene GetScene(Settings s,double time)
        {
            Scene scene;if(!scenes.TryGetValue(s,out scene)){scenes[s]=scene=new Scene();}
            if(scene.Time==time)return scene;scene.Time=time;scene.Bodies.Clear();scene.Rings.Clear();scene.Star=Illuminator(s);
            RingAnchorState light;string error;
            if(scene.Star==null||!RingAnchorEphemeris.TryRelative(RingAnchorEphemeris.Id(scene.Star),s.Body,time,out light,out error)){scene.Star=null;return scene;}
            scene.Light=light.Position;
            foreach(var body in FlightGlobals.Bodies)
            {
                if(body==scene.Star||body.Radius<=0)continue;RingAnchorState state;
                if(RingAnchorEphemeris.TryRelative(RingAnchorEphemeris.Id(body),s.Body,time,out state,out error))scene.Bodies.Add(Tuple.Create(state.Position,body.Radius));
            }
            var scenario=RingworldScenario.Instance;if(scenario==null)return scene;
            foreach(var node in scenario.Rings)
            {
                var other=scenario.RingSettings(node.GetValue("ringId")??"primary");RingAnchorState origin;
                if(other==null||other.Body==null||!RingAnchorEphemeris.TryRelative(RingAnchorEphemeris.Id(other.Body),s.Body,time,out origin,out error))continue;
                RingAnchorState anchor;
                try{anchor=other.AnchorAt(time);}catch(InvalidOperationException){continue;}
                var ring=new RingBlocker{Settings=other,Center=origin.Position+anchor.Position};var g=other.Geometry;
                double phase=g.P.Omega*time+2*Math.PI*time/(20*g.P.DaySeconds);
                for(int i=0;i<20;i++){double angle=phase+i*2*Math.PI/20;ring.Basis[i]=new RingBasis(0,angle*180/Math.PI,0);ring.Panels[i]=RingGeometry.Rotate(new DVec(g.P.Radius*46/153,0,0),angle);}
                scene.Rings.Add(ring);
            }
            return scene;
        }
        internal static void Apply(Material material,Settings s,double time,bool global=false)
        {
            var scene=GetScene(s,time);var anchor=s.AnchorAt(time);double radius=s.Geometry.P.Radius;
            Func<DVec,DVec> chart=v=>s.Geometry.Basis.ToLocal(s.Geometry.RotateAroundAxis(v,-s.Geometry.P.Omega*time));
            Func<DVec,float,Vector4> vector=(v,w)=>new Vector4((float)v.X,(float)v.Y,(float)v.Z,w);
            var sun=vector(chart(scene.Light-anchor.Position)/radius,scene.Star==null?0:(float)(scene.Star.Radius/radius));
            int bodyCount=scene.Bodies.Count,ringCount=scene.Rings.Count;
            if(scene.GpuTime!=time)
            {
                int count=Math.Max(1,bodyCount+5*ringCount),width=Math.Min(4096,Mathf.NextPowerOfTwo(count)),height=(count+width-1)/width;
                if(scene.Texture==null||scene.Texture.width!=width||scene.Texture.height!=height)
                {
                    if(scene.Texture!=null)UnityEngine.Object.Destroy(scene.Texture);
                    scene.Texture=new Texture2D(width,height,TextureFormat.RGBAFloat,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
                }
                var data=new Color[width*height];
                for(int i=0;i<bodyCount;i++)data[i]=vector(chart(scene.Bodies[i].Item1-anchor.Position)/radius,(float)(scene.Bodies[i].Item2/radius));
                for(int i=0;i<ringCount;i++)
                {
                    var r=scene.Rings[i];var g=r.Settings.Geometry;int index=bodyCount+5*i;
                    data[index]=vector(chart(r.Center-anchor.Position)/radius,(float)(g.P.Radius/radius));
                    data[index+1]=vector(chart(g.Axis),(float)(g.P.Width*.5/radius));
                    data[index+2]=vector(chart(g.Basis.X),(float)(r.Settings.StructuralThickness/radius));
                    data[index+3]=vector(chart(g.Basis.Z),(float)RingGeometry.Wrap(g.P.Omega*time+2*Math.PI*time/(20*g.P.DaySeconds),2*Math.PI));
                    data[index+4]=new Vector4((float)(g.P.WallHeight/radius),(float)(-TerrainGenerator.MinimumHeight/radius),g.P.PanelsEnabled?1:0,(float)(500/radius));
                }
                scene.Texture.SetPixels(data);scene.Texture.Apply(false,false);scene.GpuTime=time;
            }
            var dimensions=new Vector4(scene.Texture.width,scene.Texture.height,0,0);
            if(global)
            {
                Shader.SetGlobalVector("_EclipseSun",sun);Shader.SetGlobalInt("_EclipseBodyCount",bodyCount);Shader.SetGlobalInt("_EclipseRingCount",ringCount);
                Shader.SetGlobalTexture("_EclipseData",scene.Texture);Shader.SetGlobalVector("_EclipseDataSize",dimensions);
                Shader.SetGlobalFloat("_EclipseWidth",(float)(s.Geometry.P.Width/radius));
            }
            if(material==null)return;
            material.SetVector("_EclipseSun",sun);material.SetInt("_EclipseBodyCount",bodyCount);material.SetInt("_EclipseRingCount",ringCount);
            material.SetTexture("_EclipseData",scene.Texture);material.SetVector("_EclipseDataSize",dimensions);material.SetFloat("_EclipseWidth",(float)(s.Geometry.P.Width/radius));
        }
        internal static void Clear()
        {
            foreach(var scene in scenes.Values)if(scene.Texture!=null)UnityEngine.Object.Destroy(scene.Texture);
            scenes.Clear();
        }
        internal static double LightDistance(Settings s,DVec local,double time)
        {
            var scene=GetScene(s,time);if(scene.Star==null)return double.PositiveInfinity;
            var observer=s.Geometry.RotateAroundAxis(local,s.Geometry.P.Omega*time-s.Geometry.OrientationRadians)+s.AnchorAt(time).Position;
            return (scene.Light-observer).Length;
        }
        internal static DVec Direction(Settings s,DVec local,double time)
        {
            var scene=GetScene(s,time);if(scene.Star==null)return -local.Unit;
            double rotation=s.Geometry.P.Omega*time-s.Geometry.OrientationRadians;
            var observer=s.Geometry.RotateAroundAxis(local,rotation)+s.AnchorAt(time).Position;
            return s.Geometry.RotateAroundAxis((scene.Light-observer).Unit,-rotation);
        }
        internal static double Visibility(Settings s,DVec local,double time,bool habitats=true)
        {
            var scene=GetScene(s,time);if(scene.Star==null)return 0;
            double rotation=s.Geometry.P.Omega*time-s.Geometry.OrientationRadians;
            var observer=s.Geometry.RotateAroundAxis(local,rotation)+s.AnchorAt(time).Position;
            return RingOcclusion.Visibility(observer,scene.Light,scene.Star.Radius,8,end=>
            {
                foreach(var body in scene.Bodies)if(RingOcclusion.Sphere(observer,end,body.Item1,body.Item2))return true;
                if(habitats)foreach(var ring in scene.Rings)
                {
                    var other=ring.Settings;var g=other.Geometry;var center=ring.Center;
                    if(RingOcclusion.RingShell(observer,end,center,g.Basis,g.P.Radius-TerrainGenerator.MinimumHeight,g.P.Radius-TerrainGenerator.MinimumHeight+other.StructuralThickness,g.P.Width*.5))return true;
                    double wallRadius=g.P.Radius-g.P.WallHeight;
                    foreach(int side in new[]{-1,1})if(RingOcclusion.RingShell(observer,end,center+g.Axis*(side*g.P.Width*.5),g.Basis,wallRadius,g.P.Radius+other.StructuralThickness,other.StructuralThickness))return true;
                    if(!g.P.PanelsEnabled)continue;
                    var a=g.Basis.ToLocal(observer-center);var b=g.Basis.ToLocal(end-center);
                    for(int i=0;i<20;i++)if(RingOcclusion.Box(a,b,ring.Panels[i],ring.Basis[i],new DVec(500,g.P.Width*.5,g.P.Radius*2/153)))return true;
                }
                return false;
            });
        }
    }
}

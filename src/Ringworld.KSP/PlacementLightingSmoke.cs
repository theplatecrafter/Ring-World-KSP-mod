#if RINGWORLD_SMOKE_TEST
using System;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    internal static class PlacementLightingSmoke
    {
        internal static bool Run(Settings anchor,Action<string> fail)
        {
            double time=Planetarium.GetUniversalTime();
            var now=anchor.AnchorAt(time);var future=anchor.AnchorAt(time+600);
            if((now.Position-future.Position).Length<1000||now.Velocity.Length<1){fail("Selected anchor does not move with its orbit" );return false;}
            var reload=Settings.Load();reload.Apply(anchor.Save());
            if(reload.AnchorId!=anchor.AnchorId||(reload.AnchorAt(time+600).Position-future.Position).Length>.01){fail("Anchor identity or future state lost on reload" );return false;}
            var host=FlightGlobals.GetBodyByName("Sun");var kerbin=FlightGlobals.GetBodyByName("Kerbin");
            var first=new Orbit(0,0,1e6,0,0,0,time,kerbin);var next=new Orbit(0,0,1.8e10,0,0,0,time,host);
            first.StartUT=time;first.EndUT=time+10;first.nextPatch=next;next.StartUT=time+10;
            var before=RingAnchorEphemeris.OrbitRelative(first,host,time+5);
            RingAnchorState parent;string error;
            RingAnchorEphemeris.TryRelative("body:Kerbin",host,time+5,out parent,out error);
            var expectedBefore=parent.Position+ConvertVector.Core(ConvertVector.Orbit(first.getRelativePositionAtUT(time+5)));
            var after=RingAnchorEphemeris.OrbitRelative(first,host,time+15);
            if((before.Position-expectedBefore).Length>.01||(after.Position-ConvertVector.Core(ConvertVector.Orbit(next.getRelativePositionAtUT(time+15)))).Length>.01)
            {fail("SOI patch sampling used the wrong parent frame");return false;}
            var snapshot=anchor.Save();snapshot.SetValue("anchorId","vessel:"+Guid.NewGuid().ToString("D"),true);
            var missing=Settings.Load();missing.Apply(snapshot);var held=missing.AnchorAt(time);
            if((held.Position-now.Position).Length>.01||held.Velocity.Length!=0||missing.AnchorWarning==null){fail("Missing anchor did not preserve its saved center");return false;}
            var settings=Settings.Load();var node=settings.Save();node.SetValue("anchorId","body:Kerbin",true);node.SetValue("radius",1e7,true);node.SetValue("width",1e6,true);node.SetValue("panelsEnabled",false,true);settings.Apply(node);
            var direction=RingLighting.Direction(settings,new DVec(),time);var day=direction*1e7;var night=-day;
            double cpuDay=RingLighting.Visibility(settings,day,time,false),cpuNight=RingLighting.Visibility(settings,night,time,false);
            if(cpuDay<.95||cpuNight>.05){fail("Physical Kerbin eclipse CPU visibility: day="+cpuDay+" night="+cpuNight );return false;}
            var bundle=RingVisualAssets.Acquire();Material material=null;RenderTexture target=null;Texture2D pixels=null,blockers=null;var previous=RenderTexture.active;
            try
            {
                var shader=bundle.LoadAsset<Shader>("Assets/Shaders/EclipseProbe.shader");if(shader==null||!shader.isSupported)throw new Exception("Eclipse probe shader unavailable");
                material=new Material(shader);target=new RenderTexture(8,8,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);target.Create();pixels=new Texture2D(8,8,TextureFormat.RGBAFloat,false,true);
                Func<DVec,float> sample=point=>
                {
                    material.SetVector("_Observer",ConvertVector.Unity(point));Graphics.Blit(null,target,material);RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,8,8),0,0);pixels.Apply();
                    float value=pixels.GetPixel(4,4).r;if(float.IsNaN(value)||float.IsInfinity(value))throw new Exception("Non-finite eclipse pixel");return value;
                };
                RingLighting.Apply(material,settings,time);material.SetInt("_EclipseRingCount",0);
                Func<DVec,DVec> chart=p=>settings.Geometry.Basis.ToLocal(settings.Geometry.RotateAroundAxis(p,-settings.Geometry.OrientationRadians))/settings.Geometry.P.Radius;
                float gpuDay=sample(chart(day)),gpuNight=sample(chart(night));
                if(gpuDay<.95||gpuNight>.05)throw new Exception("GPU Kerbin eclipse day="+gpuDay+" night="+gpuNight);
                var basis=new RingBasis(0,0,45);var centers=new Vector4[8];var axes=new Vector4[8];var xs=new Vector4[8];var zs=new Vector4[8];var sizes=new Vector4[8];
                centers[0]=new Vector4(0,0,0,.5f);axes[0]=new Vector4((float)basis.Y.X,(float)basis.Y.Y,(float)basis.Y.Z,.1f);xs[0]=new Vector4((float)basis.X.X,(float)basis.X.Y,(float)basis.X.Z,.01f);zs[0]=new Vector4((float)basis.Z.X,(float)basis.Z.Y,(float)basis.Z.Z,0);
                material.SetVector("_EclipseDataSize",Vector4.zero);material.SetInt("_EclipseBodyCount",0);material.SetInt("_EclipseRingCount",1);material.SetVectorArray("_EclipseCenters",centers);material.SetVectorArray("_EclipseAxes",axes);material.SetVectorArray("_EclipseX",xs);material.SetVectorArray("_EclipseZ",zs);material.SetVectorArray("_EclipseSizes",sizes);
                material.SetVector("_EclipseSun",ConvertVector.Unity(-basis.X*100));float hull=sample(basis.X);
                material.SetVector("_EclipseSun",ConvertVector.Unity(-basis.Y*100));float hole=sample(basis.Y);
                if(hull>.05||hole<.95)throw new Exception("GPU inclined ring eclipse hull="+hull+" hole="+hole);
                blockers=new Texture2D(128,1,TextureFormat.RGBAFloat,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                var data=new Color[128];for(int i=0;i<40;i++)data[i]=new Vector4(10000,10000,10000,1);
                for(int i=0;i<9;i++)
                {
                    int offset=40+5*i;data[offset]=i==8?centers[0]:new Vector4(10000,10000,10000,.5f);
                    data[offset+1]=axes[0];data[offset+2]=xs[0];data[offset+3]=zs[0];data[offset+4]=sizes[0];
                }
                blockers.SetPixels(data);blockers.Apply();material.SetTexture("_EclipseData",blockers);material.SetVector("_EclipseDataSize",new Vector4(128,1,0,0));material.SetInt("_EclipseBodyCount",40);material.SetInt("_EclipseRingCount",9);
                material.SetVector("_EclipseSun",ConvertVector.Unity(-basis.X*100));if(sample(basis.X)>.05)throw new Exception("Ninth ring missing from GPU eclipse texture");
                material.SetVector("_EclipseSun",ConvertVector.Unity(-basis.Y*100));if(sample(basis.Y)<.95)throw new Exception("GPU texture closed the ring's open hole");
                material.SetVector("_EclipseSun",Vector4.zero);data[84]=new Vector4(0,0,1,.001f);blockers.SetPixels(data);blockers.Apply();
                if(sample(basis.X*.3)>.05)throw new Exception("GPU shadow panel did not block light");
                data[84]=Vector4.zero;blockers.SetPixels(data);blockers.Apply();if(sample(basis.X*.3)<.95)throw new Exception("Disabled panel still shadows GPU");
                Debug.Log("[RingworldSmoke] PASS moving anchor persistence, CPU/GPU Kerbin eclipse, inclined ring hull shadow and open hole; anchor="+anchor.AnchorId);
            }
            catch(Exception e){fail("Placement lighting: "+e.Message);return false;}
            finally
            {
                RenderTexture.active=previous;if(material!=null)UnityEngine.Object.Destroy(material);if(pixels!=null)UnityEngine.Object.Destroy(pixels);if(blockers!=null)UnityEngine.Object.Destroy(blockers);if(target!=null){target.Release();UnityEngine.Object.Destroy(target);}if(bundle!=null)RingVisualAssets.Release();
            }
            return true;
        }
    }
}
#endif

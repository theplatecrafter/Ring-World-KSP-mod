using System;
using System.Globalization;
using UnityEngine;
using Ringworld.Core;
namespace NivenRingworld.Extensions
{
    // Original cylindrical cloud implementation. The public EVE documentation
    // informs the feature model, not the implementation or its asset data.
    internal sealed class RingworldClouds
    {
        private readonly Vector4[] bands={new Vector4(900,2400,64000,.65f),new Vector4(1200,6500,64000,1),new Vector4(900,12000,128000,1.35f),new Vector4(9000,12500,32000,.16f),new Vector4(6500,11000,128000,.12f),new Vector4(2500,5500,16000,.45f),new Vector4(700,6500,128000,1.1f),new Vector4(350,1700,64000,.55f),new Vector4(2500,7500,128000,.4f),new Vector4(7000,10000,12000,.22f)};
        private readonly Vector4[] shapes={new Vector4(.12f,1,.8f,.05f),new Vector4(.08f,.92f,1,.04f),new Vector4(.12f,.8f,1,.5f),new Vector4(0,.9f,.6f,0),new Vector4(.1f,.8f,.8f,.1f),new Vector4(.05f,1,.8f,.03f),new Vector4(.15f,1,.95f,.1f),new Vector4(.1f,1,.8f,.05f),new Vector4(.1f,.9f,1,.1f),new Vector4(.02f,.9f,.8f,.02f)};
        private readonly Vector4[] optics={new Vector4(.35f,.4f,.25f,0),new Vector4(.55f,.55f,.2f,.25f),new Vector4(.7f,.8f,.18f,.65f),new Vector4(.2f,.25f,.7f,.9f),new Vector4(.1f,.15f,.6f,.5f),new Vector4(.5f,.4f,.4f,.2f),new Vector4(.12f,.6f,.15f,.12f),new Vector4(.1f,.25f,.25f,.05f),new Vector4(.15f,.3f,.35f,.2f),new Vector4(.5f,.3f,.55f,.3f)};
        private static readonly string[] ids={"stratocumulus","cumulus","cumulonimbus","cirrus","cirrostratus","altocumulus","nimbostratus","stratus","altostratus","cirrocumulus"};
        private readonly Material material;
        internal float RainBase { get { return Math.Min(bands[2].x,bands[6].x); } }
        internal float StormTop { get { return bands[2].y; } }
        internal static bool Requested(Settings s){return s.CloudExtension&&s.CloudMode>0&&s.Atmosphere&&s.CloudAmount>0&&s.CloudDensity>0;}
        internal RingworldClouds(Material target,AssetBundle bundle)
        {
            material=target;var texture=bundle.LoadAsset<Texture3D>("Assets/CloudShape.asset");
            if(texture==null)throw new InvalidOperationException("Ringworld Clouds shape texture missing. Reinstall the complete build.");
            material.SetTexture("_CloudShape",texture);
            if(GameDatabase.Instance!=null)foreach(var node in GameDatabase.Instance.GetConfigNodes("RINGWORLD_CLOUD_TYPE"))
            {
                int i=Array.IndexOf(ids,node.GetValue("name"));if(i<0)continue;
                bands[i].x=Read(node,"baseAltitude",bands[i].x,100,20000);
                bands[i].y=Read(node,"topAltitude",bands[i].y,bands[i].x+100,30000);
                bands[i].z=Read(node,"shapeScale",bands[i].z,1000,1000000);
                bands[i].w=Read(node,"density",bands[i].w,0,4);
                optics[i].x=Read(node,"erosion",optics[i].x,0,.95f);
                optics[i].y=Read(node,"powder",optics[i].y,0,1);
                optics[i].z=Read(node,"ambient",optics[i].z,0,1);
                optics[i].w=Read(node,"curl",optics[i].w,0,1);
                for(int k=0;k<4;k++)shapes[i][k]=Read(node,"shape"+k,shapes[i][k],0,1);
            }
            material.SetFloat("_PrecipitationBase",RainBase);
            material.SetVectorArray("_CloudBands",bands);material.SetVectorArray("_CloudShapes",shapes);material.SetVectorArray("_CloudOptics",optics);
            float low=30000,high=0;foreach(var b in bands){low=Math.Min(low,b.x);high=Math.Max(high,b.y);}
            material.SetVector("_CloudLimits",new Vector4(low,high,0,0));
        }
        private static float Read(ConfigNode n,string key,float fallback,float low,float high)
        {float value;return float.TryParse(n.GetValue(key),NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&!float.IsNaN(value)&&!float.IsInfinity(value)?Mathf.Clamp(value,low,high):fallback;}
        internal void Update(Settings settings,WeatherSample weather,double time)
        {
            material.SetVector("_CloudControls",new Vector4(Requested(settings)?settings.CloudMode:0,(float)settings.CloudDensity,(float)weather.Storm,(float)weather.Rain));
            // Wrapping by a multiple of all shape periods avoids jumps under warp.
            material.SetFloat("_CloudRise",(float)RingGeometry.Wrap(time*.6,512000));
        }
    }
}

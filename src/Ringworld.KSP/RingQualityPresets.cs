using System;
using System.Globalization;
namespace NivenRingworld
{
    // Rendering only: never changes seed, ring dimensions, physics or biome density.
    internal static class RingQualityPresets
    {
        internal static readonly string[] Names={"Absolute Cow","Extra Beefy","Beefy","Strong","Good","Mid","Slow","Better Potato","Potato","Aged Potato","Rotten Potato"};
        private static readonly string[] Keys={"lodRange","lodResolution","generationBudget","forestQuality","visualQuality","waterQuality","cloudSteps","atmosphereSteps","cloudRange","detailDistance","photoSamples","rainDensity"};
        private static readonly double[][] Values={
            new double[]{2000000000,32,4,3,2,2,256,96,500000,250,64,1},
            new double[]{1000000000,32,3,3,2,2,192,80,400000,225,48,1},
            new double[]{500000000,32,2,3,2,2,128,64,300000,200,32,.9},
            new double[]{320000000,16,2,2,1,2,112,56,250000,175,24,.8},
            new double[]{240000000,16,2,2,1,1,96,48,200000,150,16,.7},
            new double[]{160000000,16,1,1,1,1,64,32,150000,125,16,.6},
            new double[]{160000000,8,1,0,0,0,64,32,100000,100,12,.5},
            new double[]{160000000,8,1,0,0,0,48,24,75000,75,8,.4},
            new double[]{160000000,8,1,0,0,0,32,16,50000,50,4,.3},
            new double[]{160000000,8,1,0,0,0,32,16,40000,35,2,.15},
            new double[]{200000,8,1,0,0,0,32,16,30000,25,1,0}
        };
        internal static void Apply(ConfigNode n,int index)
        {
            if(index<0||index>=Names.Length)throw new ArgumentOutOfRangeException("index");
            n.SetValue("atmosphereBackend",index<=5?1:0,true);
            n.SetValue("cylaViewSteps",new[]{500,192,128,80,48,32,24,16,8,4,1}[index],true);
            for(int k=0;k<Keys.Length;k++)n.SetValue(Keys[k],Values[index][k].ToString("R",CultureInfo.InvariantCulture),true);
            n.SetValue("cloudRange",new[]{20000000,10000000,5000000,2000000,750000,300000,100000,75000,50000,40000,30000}[index],true);
            n.SetValue("cloudMode",new[]{3,3,3,2,2,1,0,0,0,0,0}[index],true);n.SetValue("cloudDensity",1,true);
            n.SetValue("waterQuality",new[]{4,4,3,3,2,2,1,1,1,0,0}[index],true);
            n.SetValue("waterScattering",index<=3,true);
            n.SetValue("cylaDivisor",new[]{1,1,1,2,2,2,4,4,4,8,8}[index],true);n.SetValue("cylaLightSteps",new[]{50,24,16,8,6,4,3,2,2,1,1}[index],true);n.SetValue("cylaDither",false,true);
            n.SetValue("fullRingDetail",index<=5,true);n.SetValue("ambientParticles",index<=7,true);
            n.SetValue("rainEnabled",index<10,true);n.SetValue("lightningEnabled",index<=8,true);
            n.SetValue("cloudShadow",new[]{"1","0.95","0.9","0.85","0.8","0.7","0.5","0.35","0.2","0.1","0"}[index],true);
            n.SetValue("waveHeight",index==0?"2":index>=6?"0":"0.65",true);n.SetValue("atmosphereExposure","1",true);
            if(index==0)
            {
                double radius,width;
                if(!double.TryParse(n.GetValue("radius"),NumberStyles.Float,CultureInfo.InvariantCulture,out radius))radius=15300000000;
                if(!double.TryParse(n.GetValue("width"),NumberStyles.Float,CultureInfo.InvariantCulture,out width))width=160500000;
                n.SetValue("lodRange",(Math.PI*radius+width).ToString("R",CultureInfo.InvariantCulture),true);
            }
        }
        internal static string Match(Settings s)
        {
            var actual=s.Save();
            for(int i=0;i<Names.Length;i++)
            {
                var expected=s.Save();Apply(expected,i);bool same=true;
                foreach(ConfigNode.Value v in expected.values)
                    if(actual.GetValue(v.name)!=v.value){same=false;break;}
                if(same)return Names[i];
            }
            return "Custom";
        }
    }
}

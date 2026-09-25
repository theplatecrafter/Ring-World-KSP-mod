using System;
namespace Ringworld.Core
{
    // Shared by close scatter and distant biome surfaces. Extend this descriptor
    // when another biome needs a visual silhouette beyond bare terrain.
    public struct BiomeAppearance
    {
        public double CanopyCover,CanopyHeight;
        public DVec CanopyColor;
    }
    public static class BiomePresentation
    {
        public static bool ForestAllowed(TerrainSample s)
        {return !s.Wet&&s.Biome!=Biome.Road&&s.Biome!=Biome.Ruins&&s.Biome!=Biome.Rimwall;}
        public static double ForestMargin(TerrainGenerator t,double a,double b)
        {
            double cover=Ecology.Sample(t,a,b).TreeCover;
            return cover<.32?-1:(cover-.22)*1.9-t.Noise(a,b,380,2111);
        }
        public static BiomeAppearance Sample(TerrainGenerator t,double a,double b,TerrainSample ground,double footprint,double density)
        {
            var result=new BiomeAppearance{CanopyColor=new DVec(.14,.26,.09)};
            if(density<=0||!ForestAllowed(ground))return result;
            double cover=0;
            int count=footprint>380?4:1;
            for(int i=0;i<count;i++)
            {
                double x=a+(count==1?0:((i%2)*2-1)*footprint*.25),y=b+(count==1?0:((i/2)*2-1)*footprint*.25);
                double margin=ForestMargin(t,x,y);
                double w=Math.Max(0,Math.Min(1,.5+margin/.12));cover+=w*w*(3-2*w)/count;
            }
            double blend=Math.Max(0,Math.Min(1,(footprint-380)/380));blend=blend*blend*(3-2*blend);
            if(count>1){double m=ForestMargin(t,a,b);double w=Math.Max(0,Math.Min(1,.5+m/.12));cover=w*w*(3-2*w)*(1-blend)+cover*blend;}
            result.CanopyCover=cover;
            result.CanopyHeight=result.CanopyCover*(24+24*t.Noise(a,b,64,2127));
            return result;
        }
    }
}

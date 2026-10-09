using System;
namespace Ringworld.Core
{
    public static class TerrainSurface
    {
        public const int Count=6;
        public const double OriginStep=65536;
        public static int Kind(Biome biome,bool wet)
        {
            switch(biome){
                case Biome.Scrith:case Biome.Rimwall:return 4;
                case Biome.Road:return 5;
                case Biome.Snow:return 3;
                case Biome.Mountain:case Biome.Ruins:return 2;
                case Biome.Desert:case Biome.Ocean:case Biome.Lake:case Biome.River:return 1;
                default:return wet?1:0;
            }
        }
        // Canonical cylindrical position, skewed into the 3-D simplex lattice.
        // The cylinder closes naturally: no repeating rectangular texture chart.
        public static DVec NoisePoint(double along,double across,double height,double radius){
            double angle=RingGeometry.Wrap(along/radius,2*Math.PI),r=radius-height;
            double x=r*Math.Cos(angle),z=r*Math.Sin(angle),skew=(x+across+z)/3;
            return new DVec(x+skew,across+skew,z+skew);
        }
        public static DVec NoiseOrigin(double along,double across,double radius){
            var p=NoisePoint(along,across,0,radius);
            return new DVec(Math.Floor(p.X/OriginStep)*OriginStep,Math.Floor(p.Y/OriginStep)*OriginStep,Math.Floor(p.Z/OriginStep)*OriginStep);
        }
    }
}

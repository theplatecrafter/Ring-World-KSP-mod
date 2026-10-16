using System;
using System.Collections.Generic;

namespace Ringworld.Core
{
    // The quadtree is globally aligned. Only the cells touching a block at
    // each coarser level can influence its feathered edge colours.
    public sealed class LodNeighbourIndex
    {
        private struct Cell : IEquatable<Cell>
        {
            internal long X,Y;internal double Size;
            internal Cell(long x,long y,double size){X=x;Y=y;Size=size;}
            public bool Equals(Cell other){return X==other.X&&Y==other.Y&&Size==other.Size;}
            public override bool Equals(object other){return other is Cell&&Equals((Cell)other);}
            public override int GetHashCode(){unchecked{return (X.GetHashCode()*397^Y.GetHashCode())*397^Size.GetHashCode();}}
        }
        private readonly IList<LodBlock> blocks;
        private readonly Dictionary<Cell,int> cells=new Dictionary<Cell,int>();
        private readonly List<double> levels=new List<double>();
        public LodNeighbourIndex(IList<LodBlock> layout)
        {
            blocks=layout;var sizes=new HashSet<double>();
            for(int i=0;i<layout.Count;i++){var b=layout[i];cells.Add(new Cell((long)Math.Round(b.X/b.Size),(long)Math.Round(b.Y/b.Size),b.Size),i);sizes.Add(b.Size);}
            levels.AddRange(sizes);levels.Sort();
        }
        public List<LodBlock> CoarserNeighbours(LodBlock b)
        {
            var indices=new List<int>();
            foreach(double size in levels)
            {
                if(size<=b.Size)continue;
                // Include the cell to the left/below a level boundary. Corner
                // neighbours count too, matching the original inclusive test.
                // The extra upper cell covers roundoff with fractional custom
                // tile sizes. Apply the original physical-coordinate test below.
                long x0=(long)Math.Floor(b.X/size)-1,x1=(long)Math.Floor((b.X+b.Size)/size)+1;
                long y0=(long)Math.Floor(b.Y/size)-1,y1=(long)Math.Floor((b.Y+b.Size)/size)+1;
                for(long y=y0;y<=y1;y++)for(long x=x0;x<=x1;x++)
                {
                    int index;if(!cells.TryGetValue(new Cell(x,y,size),out index))continue;
                    var other=blocks[index];
                    if(other.X<=b.X+b.Size&&other.X+other.Size>=b.X&&other.Y<=b.Y+b.Size&&other.Y+other.Size>=b.Y)indices.Add(index);
                }
            }
            // Colour blending is order-dependent. Preserve the original layout
            // order exactly instead of changing the terrain's appearance.
            indices.Sort();var result=new List<LodBlock>(indices.Count);
            foreach(int index in indices)result.Add(blocks[index]);return result;
        }
    }
}

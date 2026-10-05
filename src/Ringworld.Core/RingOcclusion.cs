using System;

namespace Ringworld.Core
{
    public static class RingOcclusion
    {
        // Finite segment tests: an object behind the light cannot eclipse it.
        public static bool Sphere(DVec observer,DVec light,DVec center,double radius)
        {
            if(!(radius>0)||!RingParameters.Finite(radius))return false;
            var ray=light-observer;double length=ray.Length;if(length<=1e-9)return false;
            var d=ray/length;var relative=center-observer;
            double projection=DVec.Dot(relative,d);
            double closest=Math.Max(0,Math.Min(length,projection));
            return (relative-d*closest).Length<radius;
        }
        public static bool Box(DVec observer,DVec light,DVec center,RingBasis basis,DVec halfSize)
        {
            var a=basis.ToLocal(observer-center);var d=basis.ToLocal(light-observer);
            double low=1e-10,high=1-1e-10;
            return Slab(a.X,d.X,halfSize.X,ref low,ref high)&&
                   Slab(a.Y,d.Y,halfSize.Y,ref low,ref high)&&
                   Slab(a.Z,d.Z,halfSize.Z,ref low,ref high);
        }
        private static bool Slab(double a,double d,double half,ref double low,ref double high)
        {
            if(half<=0)return false;
            if(Math.Abs(d)<1e-20)return Math.Abs(a)<=half;
            double x=(-half-a)/d,y=(half-a)/d;if(x>y){double temp=x;x=y;y=temp;}
            low=Math.Max(low,x);high=Math.Min(high,y);return low<=high;
        }
        // Closed cylindrical shell, with finite width and physical thickness.
        // The central hole is empty: a bounding sphere would create false eclipses.
        public static bool RingShell(DVec observer,DVec light,DVec center,RingBasis basis,
            double innerRadius,double outerRadius,double halfWidth)
        {
            if(!(innerRadius>0&&outerRadius>innerRadius&&halfWidth>0))return false;
            var a=basis.ToLocal(observer-center);var d=basis.ToLocal(light-observer);
            double low=1e-10,high=1-1e-10;
            if(!Slab(a.Y,d.Y,halfWidth,ref low,ref high))return false;
            var first=a+d*low;var last=a+d*high;
            if(InShell(first,innerRadius,outerRadius)||InShell(last,innerRadius,outerRadius))return true;
            return Cylinder(a,d,innerRadius,low,high)||Cylinder(a,d,outerRadius,low,high);
        }
        private static bool InShell(DVec p,double inner,double outer)
        {double r=Math.Sqrt(p.X*p.X+p.Z*p.Z);return r>=inner&&r<=outer;}
        private static bool Cylinder(DVec p,DVec d,double radius,double low,double high)
        {
            double a=d.X*d.X+d.Z*d.Z;if(a<1e-30)return false;
            double b=p.X*d.X+p.Z*d.Z;
            double r=Math.Sqrt(p.X*p.X+p.Z*p.Z),c=(r-radius)*(r+radius),disc=b*b-a*c;
            if(disc<0)return false;
            double q=-b-(b>=0?1:-1)*Math.Sqrt(disc);
            double t=q/a;if(t>=low&&t<=high)return true;
            t=Math.Abs(q)>1e-30?c/q:-b/a;return t>=low&&t<=high;
        }
        // Deterministic disk samples allow a finite star to make a soft eclipse.
        // Callback receives a point on the luminous disk and performs all blockers.
        public static double Visibility(DVec observer,DVec light,double lightRadius,int samples,Func<DVec,bool> blocked)
        {
            if(blocked==null)throw new ArgumentNullException(nameof(blocked));
            var direction=(light-observer).Unit;
            if(lightRadius<=0||samples<=1)return blocked(light)?0:1;
            var axis=Math.Abs(direction.Y)<.9?new DVec(0,1,0):new DVec(1,0,0);
            var right=DVec.Cross(direction,axis).Unit;var up=DVec.Cross(right,direction).Unit;
            int visible=0;
            for(int i=0;i<samples;i++)
            {
                double radius=lightRadius*Math.Sqrt((i+.5)/samples),angle=i*2.399963229728653;
                if(!blocked(light+(right*Math.Cos(angle)+up*Math.Sin(angle))*radius))visible++;
            }
            return (double)visible/samples;
        }
    }
}

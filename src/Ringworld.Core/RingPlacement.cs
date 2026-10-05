using System;

namespace Ringworld.Core
{
    // Fixed extrinsic X, then Y, then Z rotations, in degrees. Position arithmetic
    // stays in double precision until the final camera-relative GPU upload.
    public struct RingBasis
    {
        public readonly DVec X,Y,Z;
        public RingBasis(double xDegrees,double yDegrees,double zDegrees)
        {
            if(!RingParameters.Finite(xDegrees)||!RingParameters.Finite(yDegrees)||!RingParameters.Finite(zDegrees))
                throw new ArgumentException("Ring orientation must be finite.");
            double x=RingGeometry.Wrap(xDegrees,360)*Math.PI/180;
            double y=RingGeometry.Wrap(yDegrees,360)*Math.PI/180;
            double z=RingGeometry.Wrap(zDegrees,360)*Math.PI/180;
            X=Euler(new DVec(1,0,0),x,y,z);Y=Euler(new DVec(0,1,0),x,y,z);Z=Euler(new DVec(0,0,1),x,y,z);
        }
        private static DVec Euler(DVec p,double x,double y,double z)
        {
            p=new DVec(p.X,Math.Cos(x)*p.Y-Math.Sin(x)*p.Z,Math.Sin(x)*p.Y+Math.Cos(x)*p.Z);
            p=RingGeometry.Rotate(p,y);
            return new DVec(Math.Cos(z)*p.X-Math.Sin(z)*p.Y,Math.Sin(z)*p.X+Math.Cos(z)*p.Y,p.Z);
        }
        public DVec ToWorld(DVec local){return X*local.X+Y*local.Y+Z*local.Z;}
        public DVec ToLocal(DVec world){return new DVec(DVec.Dot(world,X),DVec.Dot(world,Y),DVec.Dot(world,Z));}
        public DVec Rotate(DVec world,double angle){return ToWorld(RingGeometry.Rotate(ToLocal(world),angle));}
    }

    // An instantaneous anchor state, not a linear extrapolator. The KSP adapter
    // must sample the actual orbit at the requested universal time.
    public struct RingAnchorState
    {
        public readonly DVec Position,Velocity,Acceleration;
        public RingAnchorState(DVec position,DVec velocity,DVec acceleration)
        {Position=position;Velocity=velocity;Acceleration=acceleration;}
        public DVec LocalVelocity(DVec position,DVec velocity,DVec axis,double omega)
        {return velocity-Velocity-DVec.Cross(axis*omega,position-Position);}
        public DVec WorldVelocity(DVec position,DVec relativeVelocity,DVec axis,double omega)
        {return relativeVelocity+Velocity+DVec.Cross(axis*omega,position-Position);}
        public DVec RelativeAcceleration(DVec position,DVec relativeVelocity,DVec worldGravity,DVec axis,double omega)
        {
            var w=axis*omega;var r=position-Position;
            return worldGravity-Acceleration-DVec.Cross(w,DVec.Cross(w,r))-DVec.Cross(w,relativeVelocity)*2;
        }
    }
}

using UnityEngine;
namespace NivenRingworld
{
    internal static class RingVesselPose
    {
        internal static void Set(Vessel vessel,Vector3d position,Quaternion rotation)
        {
            // KSP's loaded SetRotation updates the parts, while SetPosition's
            // pristine-coordinate branch reads vesselTransform.rotation. Keep
            // that assembly frame consistent before placing the part tree.
            vessel.vesselTransform.SetPositionAndRotation((Vector3)position,rotation);
            vessel.SetRotation(rotation,false);vessel.SetPosition(position,true);
        }
    }
}

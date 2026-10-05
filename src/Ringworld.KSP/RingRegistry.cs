using System;
using System.Collections.Generic;
using System.Globalization;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    internal static class RingSelection
    {
        internal static string Nearest(Vessel vessel)
        {
            var state=RingworldScenario.Instance;if(state==null||vessel==null)return null;state.GetOptions();
            VesselRecord record;if(state.Vessels.TryGetValue(vessel.id.ToString(),out record))return record.RingId;
            string result=null;double distance=double.PositiveInfinity;
            foreach(var node in state.Rings)
            {
                var s=state.RingSettings(node.GetValue("ringId")??"primary");if(s.Body==null||RingTrajectory.SolarPatch(vessel,s.Body)==null)continue;
                var c=s.Geometry.Coordinates(ConvertVector.Core(vessel.GetWorldPos3D()-s.Center));
                double across=Math.Max(0,Math.Abs(c.Across)-s.Geometry.P.Width/2);
                double d=Math.Sqrt(c.Altitude*c.Altitude+across*across);
                if(d<distance){distance=d;result=s.RingId;}
            }
            return result;
        }
        internal static string Validate(Settings candidate,string editingId)
        {
            if(candidate.Body==null)return "The orbital reference body is not installed.";
            // Fixed parallel habitats must not share a contact/arrival shell. A conservative
            // enclosing cylinder test also prevents a spawn from covering an existing ring.
            var state=RingworldScenario.Instance;
            foreach(var n in state.Rings)
            {
                var other=Settings.Load();other.Apply(n);if(other.RingId==editingId||other.Body==null)continue;
                var delta=ConvertVector.Core(candidate.Center-other.Center);
                double axial=DVec.Dot(delta,candidate.Geometry.Axis);
                double horizontal=(delta-candidate.Geometry.Axis*axial).Length;
                double separation=Math.Abs(axial)-(candidate.Geometry.P.Width+other.Geometry.P.Width)/2;
                bool parallel=Math.Abs(DVec.Dot(candidate.Geometry.Axis,other.Geometry.Axis))>1-1e-12;
                double r1=candidate.Geometry.P.Radius,w1=candidate.Geometry.P.Width/2,r2=other.Geometry.P.Radius,w2=other.Geometry.P.Width/2;
                bool overlap=parallel?separation<1000000&&horizontal<r1+r2+1000000:delta.Length<Math.Sqrt(r1*r1+w1*w1)+Math.Sqrt(r2*r2+w2*w2)+1000000;
                // Bounding envelopes alone would reject a small planet ring inside
                // the central hole of the original Sun ring. A sphere wholly in
                // empty space cannot intersect its hull, walls or panel belt.
                if(overlap&&(InsideHole(candidate,other,delta)||InsideHole(other,candidate,-delta)))overlap=false;
                if(overlap)
                    return "Habitat envelopes overlap. Separate their centers before applying. Tilted rings use conservative bounding envelopes.";
            }
            return null;
        }
        private static bool InsideHole(Settings container,Settings guest,DVec delta)
        {
            var g=container.Geometry;double bound=Math.Sqrt(guest.Geometry.P.Radius*guest.Geometry.P.Radius+guest.Geometry.P.Width*guest.Geometry.P.Width*.25)+1000000;
            double axial=DVec.Dot(delta,g.Axis),radial=(delta-g.Axis*axial).Length;
            if(radial+bound>=g.P.Radius-g.P.WallHeight)return false;
            if(!g.P.PanelsEnabled||Math.Abs(axial)-bound>g.P.Width*.5)return true;
            double panel=g.P.Radius*46/153,half=g.P.Radius*2/153;
            return radial-bound>Math.Sqrt((panel+500)*(panel+500)+half*half)||radial+bound<panel-500;
        }
    }
    [KSPAddon(KSPAddon.Startup.FlightAndKSC,false)]
    public sealed class RingRenderRegistry : MonoBehaviour
    {
        private readonly Dictionary<string,ScaledRing> renderers=new Dictionary<string,ScaledRing>();
        public void Update()
        {
            var state=RingworldScenario.Instance;if(state==null)return;state.GetOptions();
            foreach(var node in state.Rings)
            {
                string id=node.GetValue("ringId")??"primary";
                if(renderers.ContainsKey(id))continue;
                var obj=new GameObject("Ringworld "+id);obj.transform.SetParent(transform,false);
                var renderer=obj.AddComponent<ScaledRing>();renderer.RingId=id;renderers[id]=renderer;
            }
            var removed=new List<string>();
            foreach(var pair in renderers)if(state.RingOptions(pair.Key)==null){if(pair.Value!=null)Destroy(pair.Value.gameObject);removed.Add(pair.Key);}
            foreach(var id in removed)renderers.Remove(id);
        }
    }
    internal sealed class RingSandboxEditor
    {
        private bool stars,confirmDelete;
        private bool placementOpen=true,terrainOpen=true,rotationOpen=true;
        private string selected,name="",reference="body:Sun",x="0",y="0",z="0",diameter="30600000",width="160500",seed="",message="";
        private bool designated=true,panels=true,reverseSpin;
        private string gravity="9.72",tiltX="0",tiltY="0",tiltZ="0",wallHeight="160",terrainHeight="1";
        private static string N(double v){return v.ToString("R",CultureInfo.InvariantCulture);}
        private static string Field(string label,string value){GUILayout.Label(label);return GUILayout.TextField(value,80);}
        private void Load(ConfigNode node)
        {
            var s=Settings.Load();s.Apply(node);selected=s.RingId;name=s.RingName;reference=s.AnchorId??("body:"+s.ReferenceBody);designated=s.DesignatedStar;
            x=N(s.CenterOffset.X/1000);y=N(s.CenterOffset.Y/1000);z=N(s.CenterOffset.Z/1000);
            diameter=N(s.Geometry.P.Radius/500);width=N(s.Geometry.P.Width/1000);seed=N(s.Geometry.P.Seed);confirmDelete=false;
            tiltX=N(s.OrientationDegrees.X);tiltY=N(s.OrientationDegrees.Y);tiltZ=N(s.OrientationDegrees.Z);
            panels=s.Geometry.P.PanelsEnabled;reverseSpin=s.Geometry.P.SpinDirection<0;gravity=N(s.Geometry.P.Gravity);
            wallHeight=N(s.Geometry.P.WallHeight/1000);terrainHeight=N(s.HeightMultiplier);
        }
        internal void Draw(RingworldFlight flight)
        {
            if(!RingworldFlight.SandboxControls)return;var state=RingworldScenario.Instance;
            if(state==null){GUILayout.Label("Waiting for save settings...");return;}
            GUILayout.Label("Sandbox ring editor");
            if(selected==null||state.RingOptions(selected)==null)Load(state.GetOptions());
            GUILayout.Label("Each ring is saved separately. Move/delete requires no resident vessels. Save your game after editing.");
            foreach(var n in state.Rings)if(GUILayout.Button((n.GetValue("ringId")==selected?"> ":"")+(n.GetValue("ringName")??"Ringworld")))Load(n);
            var selectedSettings=state.RingSettings(selected);if(selectedSettings!=null&&selectedSettings.AnchorWarning!=null)GUILayout.Label(selectedSettings.AnchorWarning);
            name=Field("Name",name);
            placementOpen=GUILayout.Toggle(placementOpen,"Placement and inclination");
            if(placementOpen)
            {
            designated=GUILayout.Toggle(designated,"Follow an existing body or asteroid/comet");
            if(designated)
            {
                if(GUILayout.Button("Anchor: "+reference+" v"))stars=!stars;
                if(stars)
                {
                    foreach(var body in FlightGlobals.Bodies)if(GUILayout.Button(body.displayName)){reference=RingAnchorEphemeris.Id(body);stars=false;}
                    foreach(var v in FlightGlobals.Vessels)if(v.vesselType==VesselType.SpaceObject&&!v.LandedOrSplashed&&GUILayout.Button(v.vesselName)){reference=RingAnchorEphemeris.Id(v);stars=false;}
                }
            }
            else GUILayout.Label("No new star. Center is fixed relative to the stock Sun; normal stellar gravity still applies.");
            GUILayout.Label("Center offset in km, in KSP's non-rotating reference axes; Centers follow the reference body. Inclination rotates the ring about its center; it does not rotate these offsets.");
            x=Field("X (km)",x);y=Field("Y (km)",y);z=Field("Z (km)",z);
            tiltX=Field("Inclination X (degrees)",tiltX);tiltY=Field("Inclination Y (degrees)",tiltY);tiltZ=Field("Inclination Z (degrees)",tiltZ);
            GUILayout.Label("Orientation applies fixed X, then Y, then Z rotations in KSP reference axes.");
            }
            terrainOpen=GUILayout.Toggle(terrainOpen,"Dimensions and terrain");
            if(terrainOpen)
            {
            diameter=Field("Diameter (km)",diameter);width=Field("Width (km)",width);seed=Field("Seed (blank = random)",seed);
            wallHeight=Field("Rim wall height (minimum 60 km; below one tenth of radius)",wallHeight);
            terrainHeight=Field("Terrain height multiplier (0.25 to 3; 1 = normal)",terrainHeight);
            }
            rotationOpen=GUILayout.Toggle(rotationOpen,"Rotation and day/night");
            if(rotationOpen)
            {
            gravity=Field("Artificial gravity (m/s², greater than 0 and at most 100)",gravity);
            reverseSpin=GUILayout.Toggle(reverseSpin,"Reverse rotation direction");
            panels=GUILayout.Toggle(panels,"Day/night shadow panels");
            GUILayout.Label("Spin speed is calculated from gravity and radius. These changes require an unoccupied ring.");
            }
            GUILayout.Space(8);
            GUILayout.Label("Save changes or create another ring");
            if(GUILayout.Button("Spawn a new ring using these fields"))Apply(flight,true);
            if(GUILayout.Button("Apply changes to selected ring"))Apply(flight,false);
            if(GUILayout.Button("Visit selected ring (spin-matched)")){string reason;if(!flight.VisitRing(selected,out reason))message=reason;else message="Transferring to selected ring.";}
            GUILayout.Space(8);
            confirmDelete=GUILayout.Toggle(confirmDelete,"Confirm deletion of selected ring");
            if(GUILayout.Button("Delete selected ring"))
            {
                if(!confirmDelete)message="Select the deletion confirmation first.";
                else if(state.Rings.Count<=1)message="Keep at least one habitat in this save.";
                else if(state.Occupied(selected)||(flight.Active&&flight.Settings.RingId==selected))message="Move or recover all resident vessels first.";
                else {var n=state.RingOptions(selected);state.Rings.Remove(n);if(state.ActiveRingId==selected){state.SelectRing(state.Rings[0].GetValue("ringId")??"primary");flight.ApplyOptions(state.GetOptions(),true);}selected=null;message="Ring deleted. Save to keep this change.";}
            }
            if(message!="")GUILayout.Label(message);
        }
        private void Apply(RingworldFlight flight,bool create)
        {
            var state=RingworldScenario.Instance;
            if(flight.AtmosphereTransition||(flight.visuals!=null&&flight.visuals.PhotoActive)){message="Finish the transition/photo first.";return;}
            if(!create&&(state.Occupied(selected)||(flight.Active&&flight.Settings.RingId==selected))){message="Move or recover resident vessels before moving/changing this ring.";return;}
            double px,py,pz,di,wi,grav,tx,ty,tz,wall,terrain;int parsed;
            if(!double.TryParse(tiltX,NumberStyles.Float,CultureInfo.InvariantCulture,out tx)||!double.TryParse(tiltY,NumberStyles.Float,CultureInfo.InvariantCulture,out ty)||!double.TryParse(tiltZ,NumberStyles.Float,CultureInfo.InvariantCulture,out tz)||!RingParameters.Finite(tx)||!RingParameters.Finite(ty)||!RingParameters.Finite(tz)){message="Enter finite inclination angles in degrees.";return;}
            if(!double.TryParse(gravity,NumberStyles.Float,CultureInfo.InvariantCulture,out grav)||!RingParameters.Finite(grav)||grav<=0||grav>100){message="Enter artificial gravity greater than 0 and at most 100 m/s².";return;}
            if(!double.TryParse(x,NumberStyles.Float,CultureInfo.InvariantCulture,out px)||!double.TryParse(y,NumberStyles.Float,CultureInfo.InvariantCulture,out py)||!double.TryParse(z,NumberStyles.Float,CultureInfo.InvariantCulture,out pz)||!double.TryParse(diameter,NumberStyles.Float,CultureInfo.InvariantCulture,out di)||!double.TryParse(width,NumberStyles.Float,CultureInfo.InvariantCulture,out wi)||!RingParameters.Finite(px*1000)||!RingParameters.Finite(py*1000)||!RingParameters.Finite(pz*1000)||!RingParameters.Finite(di*500)||!RingParameters.Finite(wi*1000)||di<2000||wi<10||wi>di/2){message="Enter finite coordinates, diameter >= 2,000 km, and width from 10 km to the radius.";return;}
            if(!double.TryParse(wallHeight,NumberStyles.Float,CultureInfo.InvariantCulture,out wall)||!RingParameters.Finite(wall*1000)||wall<60||wall>=di/20){message="Enter a wall height of at least 60 km, below one tenth of the ring radius.";return;}
            if(!double.TryParse(terrainHeight,NumberStyles.Float,CultureInfo.InvariantCulture,out terrain)||!RingParameters.Finite(terrain)||terrain<.25||terrain>3){message="Enter a terrain height multiplier from 0.25 to 3.";return;}
            if(string.IsNullOrWhiteSpace(seed))parsed=BitConverter.ToInt32(Guid.NewGuid().ToByteArray(),0);
            else if(!int.TryParse(seed,out parsed)){message="Seed must be a whole 32-bit number or blank.";return;}
            var n=state.RingOptions(selected).CreateCopy();string id=create?Guid.NewGuid().ToString("N"):selected;
            n.SetValue("ringId",id,true);n.SetValue("ringName",string.IsNullOrWhiteSpace(name)?"Ringworld":name.Trim(),true);
            string anchor=designated?reference:"body:Sun";var host=Settings.OrbitalHost(anchor);
            if(host==null){message="The selected anchor is no longer available.";return;}
            n.SetValue("anchorId",anchor,true);n.SetValue("referenceBody",host.name,true);n.SetValue("designatedStar",designated,true);
            n.SetValue("centerX",N(px*1000),true);n.SetValue("centerY",N(py*1000),true);n.SetValue("centerZ",N(pz*1000),true);
            n.SetValue("tiltX",N(tx),true);n.SetValue("tiltY",N(ty),true);n.SetValue("tiltZ",N(tz),true);
            n.SetValue("gravity",N(grav),true);n.SetValue("spinDirection",reverseSpin?-1:1,true);n.SetValue("panelsEnabled",panels,true);
            n.SetValue("radius",N(di*500),true);n.SetValue("width",N(wi*1000),true);n.SetValue("seed",parsed,true);
            n.SetValue("wallHeight",N(wall*1000),true);n.SetValue("heightMultiplier",N(terrain),true);
            var candidate=Settings.Load();try{candidate.Apply(n);}catch(ArgumentException e){message=e.Message;return;}
            string invalid=RingSelection.Validate(candidate,create?null:selected);if(invalid!=null){message=invalid;return;}
            if(create)state.Rings.Add(n);else state.Rings[state.Rings.IndexOf(state.RingOptions(selected))]=n;
            if(!create&&state.ActiveRingId==selected)flight.ApplyOptions(n,true);
            Load(n);message=create?"Ring spawned. Use Visit to fly there; save to keep it.":"Ring updated. Save to keep it.";
        }
    }
}

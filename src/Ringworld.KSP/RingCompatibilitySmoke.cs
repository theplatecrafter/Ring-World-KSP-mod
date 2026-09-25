#if RINGWORLD_SMOKE_TEST
using System;
using UnityEngine;
namespace NivenRingworld
{
    internal static class RingCompatibilitySmoke
    {
        private static void Check(bool value,string message){if(!value)throw new Exception("Ring compatibility: "+message);}
        internal static void Run(Vessel vessel)
        {
            RingworldPointEnvironment env,higher;
            Check(RingworldSurfaceApi.TryGetEnvironmentAtPosition(vessel,vessel.GetWorldPos3D(),out env),"point query");
            Check(env.AirDensity>0&&Vector3d.Dot(env.AirBuoyancyPerCubicMetre,env.SurfaceUp)>0,"air density/lift direction");
            Check(RingworldSurfaceApi.TryGetEnvironmentAtPosition(vessel,vessel.GetWorldPos3D()+env.SurfaceUp*1000,out higher)&&higher.AirDensity<env.AirDensity,"density at altitude");
            Check(!RingworldSurfaceApi.TryGetEnvironmentAtPosition(null,vessel.GetWorldPos3D(),out higher),"context isolation");
            var chunks=RingworldTerrainApi.GetLoadedChunks();Check(chunks.Length>0,"terrain registry populated");
            foreach(var c in chunks)Check(c.Mesh!=null&&c.Transform!=null&&c.Mesh.vertexCount==c.Mesh.normals.Length,"scatter mesh/normal data");
            var s=RingworldFlight.Instance.Settings;var cache=ResourceCache.Instance;
            const string name="RingworldCompatibilityFixture";
            Func<float,DistributionData> distribution=value=>new DistributionData{PresenceChance=100,MinAbundance=value,MaxAbundance=value,Variance=0,Dispersal=1};
            var global=new ResourceData{ResourceName=name,ResourceType=0,Distribution=distribution(1)};
            var planet=new ResourceData{ResourceName=name,ResourceType=0,PlanetName="Ringworld:"+s.RingId,Distribution=distribution(7)};
            var biome=new ResourceData{ResourceName=name,ResourceType=0,PlanetName="Ringworld:"+s.RingId,BiomeName=env.Biome,Distribution=distribution(11)};
            ModuleResourceScanner scanner=null;ModuleResourceHarvester drill=null;
            try
            {
                double abundance;cache.GlobalResources.Add(global);
                Check(RingworldResourceApi.TryGetAbundance(vessel,name,out abundance)&&Math.Abs(abundance-.01)<1e-6,"global configuration");
                cache.PlanetaryResources.Add(planet);
                Check(RingworldResourceApi.TryGetAbundance(vessel,name,out abundance)&&Math.Abs(abundance-.07)<1e-6,"planet override");
                cache.BiomeResources.Add(biome);
                Check(RingworldResourceApi.TryGetAbundance(vessel,name,out abundance)&&Math.Abs(abundance-.11)<1e-6,"biome override");
                scanner=(ModuleResourceScanner)vessel.rootPart.AddModule("ModuleResourceScanner");scanner.ResourceName=name;scanner.Update();
                drill=(ModuleResourceHarvester)vessel.rootPart.AddModule("ModuleResourceHarvester");drill.ResourceName=name;drill.HarvesterType=0;
                Check(Math.Abs(scanner.abundance-.11)<1e-6&&Math.Abs(RingResourceHarvester.Abundance(ResourceMap.Instance,new AbundanceRequest(),drill)-.11)<1e-6,"scanner/harvester agreement");
                Check(Vector3d.Dot(RingAirshipCompatibility.Gravity(vessel.GetWorldPos3D(),scanner),env.SurfaceUp)<0&&RingAirshipCompatibility.Density(0,0,vessel.mainBody,scanner)>0,"airship queries override airless host");
                Check(Vector3d.Dot((vessel.GetWorldPos3D()-RingGraviticHover.LiftReference(vessel.mainBody,scanner)).normalized,env.SurfaceUp)>.99,"hover lift reference");
                biome.Distribution.PresenceChance=0;
                Check(RingworldResourceApi.TryGetAbundance(vessel,name,out abundance)&&abundance==0,"absent biome deposit");
                biome.PlanetName="Ringworld:another-ring";
                Check(RingworldResourceApi.TryGetAbundance(vessel,name,out abundance)&&Math.Abs(abundance-.07)<1e-6,"ring identity isolation");
            }
            finally {cache.GlobalResources.Remove(global);cache.PlanetaryResources.Remove(planet);cache.BiomeResources.Remove(biome);if(scanner!=null)vessel.rootPart.RemoveModule(scanner);if(drill!=null)vessel.rootPart.RemoveModule(drill);}
            Debug.Log("[RingworldSmoke] COMPATIBILITY resource precedence, ring isolation, scanner/harvester agreement, point environment, buoyancy and terrain meshes passed");
        }
    }
}
#endif

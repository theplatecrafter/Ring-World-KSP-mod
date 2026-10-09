#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;
namespace NivenRingworld
{
    internal static class ResourceIssueSmoke
    {
        static double Energy(Vessel v){double amount=0;foreach(var p in v.parts){var ec=p.Resources["ElectricCharge"];if(ec!=null)amount+=ec.amount;}return amount;}
        internal static IEnumerator Run(RingworldFlight f)
        {
            f.arrivalHeight=8;f.Visit();while(!f.Ready)yield return null;
            var v=FlightGlobals.ActiveVessel;float deadline=Time.realtimeSinceStartup+60;
            while((!v.Landed||f.Velocity(v).Length>.5)&&Time.realtimeSinceStartup<deadline)yield return new WaitForFixedUpdate();
            if(!v.Landed||f.Velocity(v).Length>.5)throw new Exception("Resource fixture failed to settle");
            var host=v.rootPart;var cache=ResourceCache.Instance;
            var blocked=new ResourceData{ResourceName="Ore",ResourceType=0,PlanetName=v.mainBody.bodyName,Distribution=new DistributionData{PresenceChance=0,MinAbundance=0,MaxAbundance=0,Variance=0,Dispersal=1}};
            var tip=new GameObject("Ringworld actual drill impact probe").transform;tip.SetParent(host.transform.Find("model")??host.transform,false);
            var up=f.Settings.Geometry.Up(f.Position(v));var chart=f.Settings.Geometry.Coordinates(f.Position(v));
            double floor=f.Settings.Terrain.Sample(chart.Along,chart.Across).Height;
            tip.position=(Vector3)(f.Center+ConvertVector.Ksp(f.Settings.Geometry.Position(chart.Along,chart.Across,floor+1)));tip.rotation=Quaternion.LookRotation(ConvertVector.Unity(-up));
            var info=PartLoader.getPartInfoByName("RadialDrill");ConfigNode config=null;
            foreach(var n in info.partConfig.GetNodes("MODULE"))if(n.GetValue("HarvesterType")=="0"&&n.GetValue("ResourceName")=="Ore")config=n.CreateCopy();
            if(config==null)throw new Exception("Installed RadialDrill has no crustal Ore harvester configuration");
            // Heisenberg's WBIResources dependency replaces this module with
            // WBIGoldStrikeDrill. Exercise the unchanged stock implementation
            // first; the installed derived implementation is a separate case.
            string installedName=config.GetValue("name");config.SetValue("name","ModuleResourceHarvester",true);
            config.SetValue("ImpactTransform",tip.name,true);
            PartResource ore=host.Resources["Ore"];if(ore==null){var n=new ConfigNode("RESOURCE");n.AddValue("name","Ore");n.AddValue("amount",0);n.AddValue("maxAmount",100);host.AddResource(n);ore=host.Resources["Ore"];}
            var electric=host.Resources["ElectricCharge"];if(electric==null){var n=new ConfigNode("RESOURCE");n.AddValue("name","ElectricCharge");n.AddValue("amount",5000);n.AddValue("maxAmount",5000);host.AddResource(n);electric=host.Resources["ElectricCharge"];}
            electric.maxAmount=Math.Max(electric.maxAmount,5000);electric.amount=electric.maxAmount;
            var drill=(ModuleResourceHarvester)host.AddModule(config);drill.OnStart(PartModule.StartState.Flying);
            cache.PlanetaryResources.Add(blocked);
            try{
                double abundance;if(!RingworldResourceApi.TryGetAbundance(v,"Ore",out abundance)||abundance<=0)throw new Exception("Ring Ore was removed by a host-only exclusion");
                bool impact=(bool)AccessTools.Method(typeof(ModuleResourceHarvester),"CheckForImpact").Invoke(drill,null);
                if(!impact)throw new Exception("Stock drill impact transform cannot hit the ring collider");
                bool unlocked=ResourceMap.Instance.IsBiomeUnlocked(v.mainBody.flightGlobalsIndex,v.SituationString);
                double before=ore.amount,power=Energy(v);drill.StartResourceConverter();
                for(int tick=0;tick<200;tick++)yield return new WaitForFixedUpdate();
                if(ore.amount<=before||Energy(v)>=power)throw new Exception("Stock drill failed production: status="+drill.status+" active="+drill.IsActivated+" ore="+(ore.amount-before)+" power="+(power-Energy(v))+" speed="+v.horizontalSrfSpeed);
                if(ResourceMap.Instance.IsBiomeUnlocked(v.mainBody.flightGlobalsIndex,v.SituationString)!=unlocked)throw new Exception("Drilling unlocked the host body's resource biome");
                var scanner=(ModuleResourceScanner)host.AddModule("ModuleResourceScanner");scanner.ResourceName="Ore";scanner.ScannerType=0;scanner.Update();
                if(Math.Abs(scanner.abundance-abundance)>.001)throw new Exception("Stock scanner disagrees with actual harvesting");host.RemoveModule(scanner);
                Debug.Log("[RingworldSmoke] PASS actual stock drill Ore production="+(ore.amount-before)+" EC used="+(power-Energy(v))+" collider impact="+impact+" host survey unchanged="+unlocked+" ring abundance="+abundance);
                if(installedName!="ModuleResourceHarvester"){
                    drill.StopResourceConverter();host.RemoveModule(drill);config.SetValue("name",installedName,true);
                    drill=(ModuleResourceHarvester)host.AddModule(config);drill.OnStart(PartModule.StartState.Flying);
                    before=ore.amount;power=Energy(v);drill.StartResourceConverter();
                    for(int tick=0;tick<200;tick++)yield return new WaitForFixedUpdate();
                    if(ore.amount<=before||Energy(v)>=power)throw new Exception("Installed "+installedName+" failed Ore production: "+drill.status);
                    Debug.Log("[RingworldSmoke] PASS installed "+installedName+" Ore production="+(ore.amount-before)+" EC used="+(power-Energy(v)));
                }
            }finally{drill.StopResourceConverter();host.RemoveModule(drill);cache.PlanetaryResources.Remove(blocked);UnityEngine.Object.Destroy(tip.gameObject);}
        }
    }
}
#endif

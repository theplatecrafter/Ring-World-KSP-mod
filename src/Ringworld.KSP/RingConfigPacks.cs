using System;
using System.Collections.Generic;
using System.Globalization;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    // GameDatabase has already applied ModuleManager patches. Definitions become
    // save-owned snapshots once; pack updates must never move resident vessels.
    internal static class RingConfigPacks
    {
        internal static List<ConfigNode> Load()
        {
            var nodes=GameDatabase.Instance.GetConfigNodes("NIVEN_RINGWORLD_SYSTEM");
            if(nodes.Length==0)return null;
            try{return Parse(nodes);}
            catch(ArgumentException e)
            {
                string message="Ringworld config packs were not loaded: "+e.Message+" Using the default ring. Correct the packs and start a new save.";
                Debug.LogError("[NivenRingworld] "+message);
                ScreenMessages.PostScreenMessage(message,20,ScreenMessageStyle.UPPER_CENTER);
                return null;
            }
        }
        internal static List<ConfigNode> Parse(ConfigNode[] packs)
        {
            var result=new List<ConfigNode>();var names=new HashSet<string>(StringComparer.Ordinal);
            var ids=new HashSet<string>(StringComparer.Ordinal);bool replace=false;
            foreach(var pack in packs)
            {
                var name=pack.GetValue("name");
                if(string.IsNullOrWhiteSpace(name)||!names.Add(name))throw new ArgumentException("Every system pack needs a unique name.");
                var systemKeys=new HashSet<string>(StringComparer.Ordinal);
                foreach(ConfigNode.Value v in pack.values)if(!systemKeys.Add(v.name)||(v.name!="name"&&v.name!="replaceDefault"))throw new ArgumentException(name+": duplicate or unknown system option "+v.name);
                bool replacing=false;
                if(pack.HasValue("replaceDefault")&&!bool.TryParse(pack.GetValue("replaceDefault"),out replacing))throw new ArgumentException(name+": replaceDefault must be true or false.");
                if(replace&&replacing)throw new ArgumentException("Install only one replacement preset.");
                replace|=replacing;
                if(pack.nodes.Count==0)throw new ArgumentException(name+": no RING definitions.");
                foreach(ConfigNode ring in pack.nodes)
                {
                    if(ring.name!="RING")throw new ArgumentException(name+": expected a RING node.");
                    string id=ring.GetValue("ringId");
                    if(string.IsNullOrWhiteSpace(id)||!ids.Add(id))throw new ArgumentException(name+": every ring needs a unique ringId.");
                    result.Add(Resolve(ring,name));
                }
            }
            if(!replace)
            {
                if(ids.Contains("primary"))throw new ArgumentException("ringId primary is reserved for the default ring unless replaceDefault is true.");
                var defaults=Settings.Load().Save();RingQualityPresets.Apply(defaults,6);
                defaults.SetValue("seed",BitConverter.ToInt32(Guid.NewGuid().ToByteArray(),0),true);result.Insert(0,defaults);
            }
            return result;
        }
        private static ConfigNode Resolve(ConfigNode ring,string pack)
        {
            var settings=Settings.Load();var node=settings.Save();RingQualityPresets.Apply(node,6);
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(ConfigNode.Value value in ring.values)
            {
                if(!seen.Add(value.name)||!node.HasValue(value.name)||value.name.StartsWith("lastAnchor",StringComparison.Ordinal))
                    throw new ArgumentException(pack+": duplicate or unknown ring option "+value.name);
                string text=value.value;
                if(value.name!="ringId"&&value.name!="ringName"&&value.name!="referenceBody"&&value.name!="anchorId")
                {
                    bool flag;double number;
                    if(value.name=="seed"&&string.IsNullOrWhiteSpace(text))continue;
                    if(bool.TryParse(node.GetValue(value.name),out flag))
                    {if(!bool.TryParse(text,out flag))throw new ArgumentException(pack+": invalid boolean "+value.name);text=flag.ToString();}
                    else if(!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out number)||!RingParameters.Finite(number))
                        throw new ArgumentException(pack+": invalid finite number "+value.name);
                }
                node.SetValue(value.name,text,true);
            }
            if(ring.nodes.Count!=0)throw new ArgumentException(pack+": RING uses values, not nested nodes.");
            int seed;
            if(!ring.HasValue("seed")||string.IsNullOrWhiteSpace(ring.GetValue("seed")))seed=BitConverter.ToInt32(Guid.NewGuid().ToByteArray(),0);
            else if(!int.TryParse(ring.GetValue("seed"),NumberStyles.Integer,CultureInfo.InvariantCulture,out seed))throw new ArgumentException(pack+": seed must be a 32-bit integer.");
            node.SetValue("seed",seed,true);
            string anchor=ring.GetValue("anchorId")??("body:"+(ring.GetValue("referenceBody")??"Sun"));
            if(!anchor.StartsWith("body:",StringComparison.Ordinal))throw new ArgumentException(pack+": instance presets must anchor to a body; asteroid GUIDs are save-specific.");
            var body=Settings.OrbitalHost(anchor);
            if(body==null)throw new ArgumentException(pack+": missing anchor "+anchor+" (check this pack's dependencies).");
            node.SetValue("anchorId",anchor,true);node.SetValue("referenceBody",body.name,true);
            settings.Apply(node);
            settings.Geometry.P.Validate();
            return settings.Save();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
internal static class EnvironmentAudit
{
 internal static void Run(PEReader pe,MetadataReader r)
 {
  var ops=new Dictionary<short,OpCode>();foreach(var f in typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static))if(f.FieldType==typeof(OpCode)){var op=(OpCode)f.GetValue(null);ops[op.Value]=op;}
  string Type(EntityHandle h){return h.Kind==HandleKind.TypeDefinition?r.GetString(r.GetTypeDefinition((TypeDefinitionHandle)h).Name):h.Kind==HandleKind.TypeReference?r.GetString(r.GetTypeReference((TypeReferenceHandle)h).Name):"";}
  string Target(int token){var h=MetadataTokens.EntityHandle(token);switch(h.Kind){
   case HandleKind.FieldDefinition:var f=r.GetFieldDefinition((FieldDefinitionHandle)h);return Type(f.GetDeclaringType())+"."+r.GetString(f.Name);
   case HandleKind.MethodDefinition:var m=r.GetMethodDefinition((MethodDefinitionHandle)h);return Type(m.GetDeclaringType())+"."+r.GetString(m.Name);
   case HandleKind.MemberReference:var mr=r.GetMemberReference((MemberReferenceHandle)h);return Type(mr.Parent)+"."+r.GetString(mr.Name);
   default:return "";}}
  Console.WriteLine("type,method,opcode,target");
  foreach(var th in r.TypeDefinitions){var t=r.GetTypeDefinition(th);foreach(var mh in t.GetMethods()){
   var m=r.GetMethodDefinition(mh);if(m.RelativeVirtualAddress==0)continue;
   var il=pe.GetMethodBody(m.RelativeVirtualAddress).GetILBytes();var seen=new HashSet<string>();
   for(int i=0;i<il.Length;){short op=il[i++];if(op==0xfe)op=(short)(0xfe00|il[i++]);var code=ops[op];int size;
    switch(code.OperandType){
     case OperandType.InlineNone:size=0;break;
     case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:size=1;break;
     case OperandType.InlineVar:size=2;break;
     case OperandType.InlineI8:case OperandType.InlineR:size=8;break;
     case OperandType.InlineSwitch:size=4+4*BitConverter.ToInt32(il,i);break;
     default:size=4;break;
    }
    if(code.OperandType==OperandType.InlineField||code.OperandType==OperandType.InlineMethod){
     var target=Target(BitConverter.ToInt32(il,i));
     if(target.StartsWith("CelestialBody.")||target.StartsWith("FlightGlobals.get")||target.StartsWith("Vessel.")&&(target.Contains("altitude")||target.Contains("srf")||target.Contains("Pressure")||target.Contains("Density")||target.Contains("Temperature")||target.Contains("mainBody")||target.Contains("geeForce")||target.Contains("mach"))){
      var row=Type(th)+","+r.GetString(m.Name)+","+code.Name+","+target;if(seen.Add(row))Console.WriteLine(row);
     }
    }
    i+=size;
   }
  }}
 }
}

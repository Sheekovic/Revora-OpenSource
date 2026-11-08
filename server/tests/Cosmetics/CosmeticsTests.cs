using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Network;
using Console = System.Console;
using BitConverter = System.BitConverter;

internal static class CosmeticsTests
{
    static int checks;
    static readonly Type Catalog = typeof(Server).Assembly.GetType("Revora.Database.AvaterFaces");
    static readonly Type Wire = typeof(Server).Assembly.GetType("Revora.Network.GamePackets.HairFaceRequest");
    const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;
    static object Field(object row,string name) { return row.GetType().GetField(name).GetValue(row); }
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message); checks++; Console.WriteLine("PASS: "+message); }
    static IList Parse(params string[] rows) { return (IList)Catalog.GetMethod("Parse",Hidden).Invoke(null,new object[]{rows}); }
    static bool Reject(params string[] rows) {
        try { Parse(rows); return false; } catch(TargetInvocationException ex) { return ex.InnerException is FormatException || ex.InnerException is OverflowException; }
    }
    static byte[] Frame(params byte[] payload) {
        var bytes=new byte[payload.Length+12];Buffer.BlockCopy(payload,0,bytes,4,payload.Length);
        Buffer.BlockCopy(BitConverter.GetBytes((ushort)(bytes.Length-8)),0,bytes,0,2);
        Buffer.BlockCopy(BitConverter.GetBytes((ushort)3510),0,bytes,2,2);
        Buffer.BlockCopy(BitConverter.GetBytes(PacketHandler.ClientSeal),0,bytes,bytes.Length-8,8);return bytes;
    }
    static object Read(byte[] bytes) { var args=new object[]{bytes,null};return (bool)Wire.GetMethod("TryRead",Hidden).Invoke(null,args)?args[1]:null; }
    static uint Number(object value,string field) {return Convert.ToUInt32(Wire.GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value));}
    static readonly Type Response = typeof(Server).Assembly.GetType("Revora.Network.GamePackets.HairFaceResponse");
    static readonly Type Owned = typeof(Server).Assembly.GetType("Revora.Game.Features.CosmeticOwnership+Owned");
    static object Respond(string method,params object[] args) { return Response.GetMethod(method,Hidden).Invoke(null,args); }
    static object Entry(uint kind,uint id,bool active,uint colour,uint mask) {
        var result=Activator.CreateInstance(Owned,true);
        string[] names={"Kind","Id","Active","Colour","Colours"};object[] values={kind,id,active,colour,mask};
        for(int i=0;i<names.Length;i++)Owned.GetField(names[i],BindingFlags.Instance|BindingFlags.NonPublic).SetValue(result,values[i]);
        return result;
    }
    static Array Entries(params object[] rows) { var array=Array.CreateInstance(Owned,rows.Length);for(int i=0;i<rows.Length;i++)array.SetValue(rows[i],i);return array; }
    static bool Payload(byte[] actual,params byte[] payload) {
        var expected=Frame(payload);Array.Clear(expected,expected.Length-8,8);return actual.SequenceEqual(expected);
    }
    static bool BadResponse(string method,params object[] args) {
        try{Respond(method,args);return false;}catch(TargetInvocationException ex){return ex.InnerException is ArgumentException;}
    }
    static void Responses() {
        Check(Payload((byte[])Respond("Pair",3u,1u,345u),8,3,18,5,8,1,16,217,2),"face selection response is a singular two-scalar child with full-width ID");
        Check(Payload((byte[])Respond("Pair",5u,2u,6u),8,5,26,4,8,2,16,6),"colour acquisition response uses native action five and field three");
        Check(Payload((byte[])Respond("Pair",6u,2u,0u),8,6,26,4,8,2,16,0),"default colour selection emits explicit zero");
        Check(Payload((byte[])Respond("Rules",9u,4u),8,9,34,10,8,0,16,0,24,0,32,0,40,4),"hair rules retain all five scalar fields with VIP in field five");
        Check(Payload((byte[])Respond("Rules",8u,0u),8,8,34,10,8,0,16,0,24,0,32,0,40,0),"avatar rules use their separate open action and zeroed unsupported restrictions");
        var entry=Entry(0,2,true,6,65);
        var single=(byte[][])Respond("Entries",1u,Entries(entry));
        Check(single.Length==1 && Payload(single[0],8,1,42,12,8,0,16,2,24,1,32,0,40,6,40,65),"owned hair response supplies both unpacked field-five values after permanent field four");
        var face=(byte[][])Respond("Entries",0u,Entries(Entry(1,345,false,0,1)));
        Check(Payload(face[0],8,0,42,13,8,1,16,217,2,24,0,32,0,40,0,40,1),"owned face has a distinct kind and identity");
        var empty=(byte[][])Respond("Entries",0u,Entries());
        Check(empty.Length==1 && Payload(empty[0],8,0),"empty durable ownership has one explicit empty response");
        var pages=(byte[][])Respond("Entries",0u,Entries(Enumerable.Repeat(entry,1024).ToArray()));
        Check(pages.Length==35 && pages.Take(34).All(x=>x.Length==434) && pages[34].Length==70 && pages.All(x=>BitConverter.ToUInt16(x,0)==x.Length-8),"maximum stored entries split into bounded native-safe pages without an oversized frame");
        Check(BadResponse("Entries",1u,Entries()) && BadResponse("Entries",1u,Entries(entry,entry)) && BadResponse("Entries",0u,Entries(Enumerable.Repeat(entry,1025).ToArray())),"unlock cardinality and total response size are bounded");
        foreach(var invalid in new[]{Entry(2,2,true,0,1),Entry(0,0,true,0,1),Entry(0,2,true,6,1),Entry(0,1000,true,0,1),Entry(1,345,true,6,65),Entry(0,2,true,0,128)})
            Check(BadResponse("Entries",0u,Entries(invalid)),"invalid server-owned projection fails before a response is emitted");
        Check(BadResponse("Pair",7u,2u,1u) && BadResponse("Pair",5u,2u,0u) && BadResponse("Rules",7u,4u),"request-only actions cannot masquerade as committed responses");
    }
    static int Main(string[] args) {
        try {
            string basic="0@@2@@0@@fixture@@0@@0@@0@@500@@0@@@@2@@";
            var row=Parse(basic)[0];
            Check((bool)Field(row,"ISHair") && (uint)Field(row,"ID")==2 && (uint)Field(row,"Cost")==500 && (byte)Field(row,"Type")==2 && ((IList)Field(row,"Class")).Count==0,
                "an empty class column preserves the following restriction and price positions");
            var face=Parse("1@@345@@0@@fixture@@5@@4@@0@@500@@0@@,7,@@2@@")[0];
            Check(!(bool)Field(face,"ISHair") && (byte)Field(face,"Type")==2 && ((IList)Field(face,"Class")).Cast<byte>().SequenceEqual(new byte[]{7})
                && (uint)Field(face,"RuleKind")==5 && (uint)Field(face,"RuleValue")==4,"class-specific appearance preserves its independent restriction fields");
            Check(Parse(basic,basic.Replace("0@@2@@0@@fixture","1@@2@@0@@fixture")).Count==2,"face and hair may have the same numeric ID without colliding");
            Check(Reject(basic,basic),"duplicate identities fail before replacing the published catalog");
            Check(Reject(),"an empty catalog fails explicitly");
            foreach(string invalid in new[]{basic.Replace("@@@@2@@","@@2@@"),basic+"@@",basic.Replace("0@@2@@0@@fixture","2@@2@@0@@fixture"),
                basic.Replace("0@@2@@0@@fixture","0@@0@@0@@fixture"),basic.Replace("0@@2@@0@@fixture","0@@65536@@0@@fixture"),
                basic.Replace("@@500@@","@@-1@@"),basic.Replace("@@500@@","@@4294967296@@"),basic.Replace("@@@@2@@","@@,7,7,@@2@@"),
                basic.Replace("@@@@2@@","@@,0,@@2@@"),basic.Replace("@@@@2@@","@@7@@2@@"),basic.Replace("@@@@2@@","@@,7,,8,@@2@@")})
                Check(Reject(invalid),"malformed or shifted appearance catalog record is rejected");
            Check(Reject(Enumerable.Range(1,1025).Select(i=>basic.Replace("0@@2@@0@@fixture","0@@"+i+"@@0@@fixture")).ToArray()),"catalog allocation is bounded");
            var actual=Parse(File.ReadAllLines(args[0]));
            Check(actual.Count==377 && actual.Cast<object>().Count(x=>(bool)Field(x,"ISHair"))==57,"all 377 installed records parse with 57 hair and 320 face identities");
            Check(actual.Cast<object>().Count(x=>((IList)Field(x,"Class")).Count==0)>0
                && actual.Cast<object>().Any(x=>!(bool)Field(x,"ISHair") && (uint)Field(x,"ID")==345 && ((IList)Field(x,"Class")).Cast<byte>().SequenceEqual(new byte[]{7})),
                "the installed catalog retains unrestricted and pirate-specific entries separately");
            string beforeDirectory=Directory.GetCurrentDirectory();
            string fixture=Path.GetFullPath(args[1]);Directory.CreateDirectory(Path.Combine(fixture,"database"));
            try {
                string target=Path.Combine(fixture,"database","hairface_storage_type.txt");
                File.WriteAllText(target,basic);Directory.SetCurrentDirectory(fixture);
                var load=Catalog.GetMethod("Load",BindingFlags.Static|BindingFlags.Public);
                var published=Catalog.GetField("HairFace",BindingFlags.Static|BindingFlags.Public);
                load.Invoke(null,null);load.Invoke(null,null);
                var stable=published.GetValue(null);
                Check(((IList)stable).Count==1,"production loader replaces its catalog on repeated load instead of duplicating entries");
                File.WriteAllLines(target,new[]{basic,"malformed"});bool failed=false;
                try{load.Invoke(null,null);}catch(TargetInvocationException ex){failed=ex.InnerException is FormatException;}
                Check(failed && ReferenceEquals(stable,published.GetValue(null)),"failed production reload retains the complete prior catalog");
            } finally {Directory.SetCurrentDirectory(beforeDirectory);}
            // Golden payloads follow native 008ca4c3 and 008ca719, not an encoder under test.
            var acquire=Read(Frame(8,7,26,4,8,2,16,6));
            Check(acquire!=null && Number(acquire,"Action")==7 && Number(acquire,"ParameterField")==3 && Number(acquire,"First")==2 && Number(acquire,"Second")==6,
                "native colour acquisition shape decodes its distinct style and colour pair");
            var select=Read(Frame(8,6,26,4,8,2,16,6));
            Check(select!=null && Number(select,"Action")==6,"owned-colour selection remains distinct from paid acquisition");
            Check(Read(Frame(8,6,26,4,8,2,16,0))!=null,"native default-colour selection permits zero without granting a paid colour");
            Check(Read(Frame(8,3,18,5,8,1,16,217,2))!=null && Read(Frame(8,4,18,4,8,0,16,2))!=null,"face and style choices use field two with full integer identity");
            Check(Read(Frame(26,4,16,6,8,2,8,7))!=null,"valid protobuf field order does not change a request");
            foreach(byte action in new byte[]{0,8,9})Check(Read(Frame(8,action))!=null,"parameter-free operation has an explicit action: "+action);
            var valid=Frame(8,7,26,4,8,2,16,6);
            Check(Read(null)==null && Enumerable.Range(0,valid.Length).All(n=>Read(valid.Take(n).ToArray())==null),"every truncated prefix is rejected before decoding");
            var wrong=(byte[])valid.Clone();wrong[2]=0;Check(Read(wrong)==null,"wrong message family is rejected");
            wrong=(byte[])valid.Clone();wrong[wrong.Length-1]^=1;Check(Read(wrong)==null,"wrong transport seal is rejected");
            Check(Read(valid.Concat(new byte[]{0}).ToArray())==null && Read(Frame(new byte[117]))==null,"trailing data and oversized envelopes are rejected");
            foreach(byte[] payload in new[]{new byte[]{8,7,8,7,26,4,8,2,16,6},new byte[]{8,7,26,4,8,2,8,6},new byte[]{8,7,26,2,8,2},
                new byte[]{8,7,26,5,8,2,16,6},new byte[]{8,7,26,4,8,2,16,7},new byte[]{8,7,18,4,8,2,16,6},
                new byte[]{8,3,18,4,8,2,16,2},new byte[]{8,4,18,4,8,0,16,0},new byte[]{8,7,26,4,8,0,16,6},
                new byte[]{8,7,26,4,8,2,16,0},new byte[]{8,7,26,7,8,128,128,128,128,16,16,6},new byte[]{8,135,0,26,4,8,2,16,6},
                new byte[]{8,7,42,0},new byte[]{8,7,34,0},new byte[]{8,5},new byte[]{8,255,255,255,255,15},new byte[]{},new byte[]{8,0,18,4,8,0,16,2}})
                Check(Read(Frame(payload))==null,"malformed, unsupported or client-owned-list payload is rejected");
            Responses();Console.WriteLine("Cosmetic catalog/wire checks passed: "+checks);return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex);return 1; }
    }
}

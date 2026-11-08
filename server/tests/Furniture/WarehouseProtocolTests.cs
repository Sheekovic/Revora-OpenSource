using System;
using System.Reflection;
using Revora.Network;
using Revora.Network.GamePackets;

internal static class WarehouseProtocolTests
{
    static int checks;
    static bool Valid(byte[] packet){return (bool)typeof(Warehouse).GetMethod("ValidRequest",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{packet});}
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;}
    static byte[] Request(byte action){var b=new byte[172];Writer.WriteUInt16(164,0,b);Writer.WriteUInt16(1102,2,b);Writer.WriteUInt32(100,4,b);b[12]=action;b[13]=2;Writer.WriteUInt64(PacketHandler.ClientSeal,164,b);return b;}
    static void Main()
    {
        for(byte action=0;action<=2;action++){
            var b=Request(action);Check(Valid(b),"native request action");
            var reply=new Warehouse(false);reply.Deserialize((byte[])b.Clone());reply.Count=1;
            var frame=reply.ToArray();
            Check(frame[12]==action && frame[13]==2 && BitConverter.ToUInt32(frame,4)==100 && BitConverter.ToUInt16(frame,22)==1,"rebuilt reply preserves action kind and key");
            reply.Count=0;reply.Size=60;frame=reply.ToArray();
            Check(frame.Length==32 && BitConverter.ToUInt16(frame,0)==24 && frame[12]==action && BitConverter.ToUInt32(frame,16)==60,"empty reply carries capacity and action");
        }
        for(int length=0;length<172;length++){var b=new byte[length];Buffer.BlockCopy(Request(0),0,b,0,length);Check(!Valid(b),"truncated request");}
        Check(!Valid(null),"null request");Check(!Valid(Request(3)),"unsupported action");
        var wrong=Request(0);wrong[0]--;Check(!Valid(wrong),"wrong declared length");
        wrong=Request(0);wrong[2]++;Check(!Valid(wrong),"wrong packet family");
        wrong=Request(0);wrong[171]^=1;Check(!Valid(wrong),"wrong seal");
        var extra=new byte[173];Request(0).CopyTo(extra,0);Check(!Valid(extra),"trailing byte");
        Warehouse.Process(new byte[12],null);Check(true,"malformed frame returns before client access");
        var listing=new Warehouse(false);listing.Deserialize(Request(0));listing.Count=2;
        var first=new ConquerItem(true){UID=1800000001,ID=721177};
        var second=new ConquerItem(true){UID=1800000002,ID=721178};
        listing.Append(first);listing.Append(second);
        Check(BitConverter.ToUInt32(listing.ToArray(),24)==first.UID && BitConverter.ToUInt32(listing.ToArray(),164)==second.UID,"two item records retain their separate positions");
        var before=(byte[])listing.ToArray().Clone();bool rejected=false;
        try{listing.Append(first);}catch(InvalidOperationException){rejected=true;}
        Check(rejected && System.Linq.Enumerable.SequenceEqual(before,listing.ToArray()),"overfull append rejects before touching reply or seal");
        listing.Count=1;listing.Append(second);
        Check(BitConverter.ToUInt32(listing.ToArray(),24)==second.UID,"count rebuild resets write position");
        var replacement=Request(1);Writer.WriteUInt16(1,22,replacement);listing.Deserialize(replacement);listing.Append(first);
        Check(BitConverter.ToUInt32(listing.ToArray(),24)==first.UID,"deserialize resets write position");
        listing.Count=0;before=(byte[])listing.ToArray().Clone();rejected=false;
        try{listing.Append(first);}catch(InvalidOperationException){rejected=true;}
        Check(rejected && System.Linq.Enumerable.SequenceEqual(before,listing.ToArray()),"empty reply cannot accept an item");
        Console.WriteLine("Warehouse protocol checks passed: "+checks);
    }
}

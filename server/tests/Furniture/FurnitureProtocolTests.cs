using System;
using System.Collections.Generic;
using Revora.Network.GamePackets;

internal static class FurnitureProtocolTests
{
    static int checks;
    static void Check(bool value, string message) { if(!value)throw new Exception(message);checks++; }
    static byte[] Frame(ushort id, params byte[] body)
    {
        var bytes=new byte[12+body.Length];
        Buffer.BlockCopy(BitConverter.GetBytes((ushort)(bytes.Length-8)),0,bytes,0,2);
        Buffer.BlockCopy(BitConverter.GetBytes(id),0,bytes,2,2);
        Buffer.BlockCopy(body,0,bytes,4,body.Length);
        Buffer.BlockCopy(BitConverter.GetBytes(Revora.Network.PacketHandler.ClientSeal),0,bytes,bytes.Length-8,8);
        return bytes;
    }
    static void Main()
    {
        FurnitureRequest.Placement p;uint actor;
        var good=Frame(2030,32,90,40,70,48,10);
        Check(FurnitureRequest.TryPlacement(good,out p) && p.X==90 && p.Y==70 && p.Look==10,"three native placement fields");
        Check(FurnitureRequest.TryPlacement(Frame(2030,48,120,40,70,32,90),out p) && p.Look==120,"protobuf field ordering");
        Check(FurnitureRequest.TryPlacement(Frame(2030,32,255,255,3,40,255,255,3,48,255,255,3),out p) && p.X==65535,"ushort boundary decodes without truncation");
        Check(FurnitureRequest.TryRemoval(Frame(2031,8,6,16,172,2,24,0,32,0),out actor) && actor==300,"native removal action and actor");
        foreach(var bad in new[]{Frame(2030,32,90,40,70),Frame(2030,32,90,40,70,48,0),Frame(2030,32,90,40,70,48,10,16,1),
            Frame(2030,32,90,40,70,48,10,32,91),Frame(2030,34,90,40,70,48,10),Frame(2030,32,128,128,4,40,70,48,10),
            Frame(2030,32,128,0,40,70,48,10),Frame(2030,32,255,255,255,255,16,40,70,48,10),
            Frame(2030,32,128,128,128,128,128,0,40,70,48,10),Frame(2030,32,90,40,70,48,128),Frame(2031,32,90,40,70,48,10),
            Frame(2030,32,90,40,70,48,10,56,26,64,1)})
            Check(!FurnitureRequest.TryPlacement(bad,out p),"reject malformed or other placement family");
        foreach(var bad in new[]{Frame(2031,8,5,16,1,24,0,32,0),Frame(2031,8,6,16,0,24,0,32,0),
            Frame(2031,8,6,16,1,24,1,32,0),Frame(2031,8,6,16,1,24,0,32,1),Frame(2031,8,6,16,1),Frame(2031,8,6,16,1,24,0,32,0,40,0)})
            Check(!FurnitureRequest.TryRemoval(bad,out actor),"reject other NPC action/shape");
        for(int length=0;length<good.Length;length++){
            var truncated=new byte[length];Buffer.BlockCopy(good,0,truncated,0,length);
            Check(!FurnitureRequest.TryPlacement(truncated,out p),"reject every truncated frame");
        }
        var badSeal=(byte[])good.Clone();badSeal[badSeal.Length-1]^=1;
        Check(!FurnitureRequest.TryPlacement(badSeal,out p),"wrong client seal");
        var badLength=(byte[])good.Clone();badLength[0]++;
        Check(!FurnitureRequest.TryPlacement(badLength,out p),"declared length mismatch");
        var coalesced=new byte[good.Length*2];good.CopyTo(coalesced,0);good.CopyTo(coalesced,good.Length);
        Check(!FurnitureRequest.TryPlacement(coalesced,out p),"decoder requires one framed packet");
        Check(!FurnitureRequest.TryPlacement(null,out p) && !FurnitureRequest.TryRemoval(null,out actor),"null frames");
        var click=Frame(2031,8,0,16,255,255,255,255,15,24,0,32,0);
        Check(FurnitureRequest.TryInteraction(click,out actor) && actor==uint.MaxValue,"native click preserves full-width actor identity");
        Check(FurnitureRequest.TryInteraction(Frame(2031,32,0,24,0,16,172,2,8,0),out actor) && actor==300,"click accepts protobuf field ordering");
        Check(!FurnitureRequest.TryRemoval(click,out actor),"click never authorizes removal");
        foreach(var bad in new[]{Frame(2031,8,6,16,1,24,0,32,0),Frame(2031,8,0,16,0,24,0,32,0),
            Frame(2031,8,0,16,1,24,1,32,0),Frame(2031,8,0,16,1,24,0,32,1),Frame(2031,8,0,16,1),
            Frame(2031,8,0,16,1,24,0,32,0,16,2),Frame(2031,8,0,16,1,24,0,32,0,40,0),
            Frame(2031,8,0,16,129,0,24,0,32,0),Frame(2031,8,0,16,255,255,255,255,16,24,0,32,0),
            Frame(2030,8,0,16,1,24,0,32,0)})Check(!FurnitureRequest.TryInteraction(bad,out actor),"reject malformed or different NPC click contract");
        for(int length=0;length<click.Length;length++){
            var truncated=new byte[length];Buffer.BlockCopy(click,0,truncated,0,length);
            Check(!FurnitureRequest.TryInteraction(truncated,out actor),"reject truncated click");
        }
        click[click.Length-1]^=1;Check(!FurnitureRequest.TryInteraction(click,out actor),"click requires client seal");
        Console.WriteLine("Furniture protocol checks passed: "+checks);
    }
}

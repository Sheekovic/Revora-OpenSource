using System;
using Revora.Game.Features;

internal static class RulesTests
{
    private static int checks;
    private const ulong Seal = 0x1122334455667788UL;
    private static void Check(bool valid,string name) { if(!valid) throw new Exception(name); checks++; }
    private static byte[] Frame(byte mode,params uint[] ids)
    {
        var b=new byte[16+4*ids.Length];
        Array.Copy(BitConverter.GetBytes((ushort)(b.Length-8)),0,b,0,2);
        Array.Copy(BitConverter.GetBytes((ushort)2036),0,b,2,2);
        b[4]=mode;b[5]=(byte)ids.Length;
        for(int i=0;i<ids.Length;i++)Array.Copy(BitConverter.GetBytes(ids[i]),0,b,8+4*i,4);
        Array.Copy(BitConverter.GetBytes(Seal),0,b,b.Length-8,8);return b;
    }
    private static int Main()
    {
        uint value;SteedBreedingRules.Request request;
        uint[] thresholds={20,20,90,240,720,2160,6480,19440,58320,2700,5500,9000};
        uint[] credits={1,10,30,120,360,1080,3240,9720,29160};
        for(byte i=0;i<12;i++)Check(SteedBreedingRules.TryThreshold(i,out value)&&value==thresholds[i],"published and native threshold "+i);
        foreach(byte i in new byte[]{12,13,255})Check(!SteedBreedingRules.TryThreshold(i,out value)&&value==0,"unsupported advancement "+i);
        for(byte i=0;i<9;i++)
        {
            Check(SteedBreedingRules.TryMinorCredit(i,0,out value)&&value==credits[i],"minor credit "+i);
            Check(SteedBreedingRules.TryMinorCredit(i,thresholds[i]-1,out value)&&value==credits[i]+thresholds[i]-1,"saved minor progress "+i);
            Check(!SteedBreedingRules.TryMinorCredit(i,thresholds[i],out value)&&value==0,"noncanonical minor progress "+i);
            Check(!SteedBreedingRules.TryMinorCredit(i,uint.MaxValue,out value),"overflow input "+i);
        }
        foreach(byte i in new byte[]{9,10,11,12,255})Check(!SteedBreedingRules.TryMinorCredit(i,0,out value),"native rejected minor "+i);
        Check(SteedBreedingRules.TryMinorCredit(6,100,out value)&&value==3340,"earned +6 mount retains saved progress");
        Check(SteedBreedingRules.TryBlend(0,0,out value)&&value==0,"black stays black without artificial +1");
        Check(SteedBreedingRules.TryBlend(0xffffff,0xffffff,out value)&&value==0xfefefe,"separate integer truncation");
        Check(SteedBreedingRules.TryBlend(0xff0000,0x00ff00,out value)&&value==0xe51900,"channel order preserved");
        Check(SteedBreedingRules.TryBlend(0x0000ff,0x000000,out value)&&value==229,"low channel remains low");
        Check(SteedBreedingRules.TryBlend(0,0xffffff,out value)&&value==0x191919,"minor contributes ten percent");
        Check(!SteedBreedingRules.TryBlend(0x1000000,0,out value)&&!SteedBreedingRules.TryBlend(0,uint.MaxValue,out value),"reject non RGB values");
        // Independent exhaustive channel invariant: each blend is bounded by its
        // parents, allowing one point of loss from the two integer truncations.
        bool bounded=true;
        for(uint a=0;a<256;a++)for(uint b=0;b<256;b++)
        { SteedBreedingRules.TryBlend(a,b,out value);bounded &= value<=Math.Max(a,b) && value+1>=Math.Min(a,b); }
        Check(bounded,"all channel pairs stay bounded");
        foreach(byte mode in new byte[]{2,3})
        {
            var frame=Frame(mode,100,200);
            Check(SteedBreedingRules.TryRequest(frame,Seal,out request)&&request.Mode==mode&&request.Items.Length==2&&request.Items[0]==100&&request.Items[1]==200,"two-parent frame "+mode);
            frame[8]=0;Check(request.Items[0]==100,"decoder owns its UID snapshot");
        }
        var ids=new uint[41];for(int i=0;i<ids.Length;i++)ids[i]=(uint)(i+1);
        Check(SteedBreedingRules.TryRequest(Frame(3,ids),Seal,out request)&&request.Items.Length==41,"equipped parent plus forty bag slots");
        Check(!SteedBreedingRules.TryRequest(null,Seal,out request),"null frame");
        for(int n=0;n<24;n++)Check(!SteedBreedingRules.TryRequest(new byte[n],Seal,out request),"truncated frame "+n);
        foreach(var frame in new[]{Frame(0,1,2),Frame(1,1,2),Frame(4,1,2),Frame(3,1),Frame(3,1,1),Frame(3,1,2,2),Frame(3,0,2),Frame(3,1,0),Frame(3,new uint[42])})
            Check(!SteedBreedingRules.TryRequest(frame,Seal,out request)&&request==null,"invalid mode/count/identity");
        foreach(int offset in new[]{0,2,5,23})
        {var frame=Frame(3,1,2);frame[offset]^=1;Check(!SteedBreedingRules.TryRequest(frame,Seal,out request),"bad header/seal "+offset);}
        var extra=Frame(3,1,2);Array.Resize(ref extra,extra.Length+1);Check(!SteedBreedingRules.TryRequest(extra,Seal,out request),"trailing bytes");
        Console.WriteLine("Steed rule checks passed: "+checks);return 0;
    }
}

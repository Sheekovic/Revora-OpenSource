// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora


// Revora guild ranking responses. Existing packet 2101 layout retained.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using Revora.Client;
using Revora.Game.Features;
using Revora.Network;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.ConquerStructures.Society
{
    public unsafe class GuildDoantion
    {
        public static void Process(byte[] packet,GameState client) {
            var response=BuildResponse(packet,client);
            if(response!=null)client.Send(response);
        }
        internal static byte[] BuildResponse(byte[] packet,GameState client) {
            if(packet==null || packet.Length<16 || BitConverter.ToUInt16(packet,0)!=packet.Length-8
                || BitConverter.ToUInt16(packet,2)!=2101 || BitConverter.ToUInt64(packet,packet.Length-8)!=PacketHandler.ClientSeal)
                return null;
            return BuildOwnedPage(client,packet[4],packet[5]);
        }
        private static bool Page(byte category,byte page) {return category<=9 && page<(category==9?10:2);}
        private static byte[] BuildOwnedPage(GameState client,byte category,byte page) {
            if(!Page(category,page) || !Q.Player(client,false) || !SessionInstall.TryEnter(client.Entity.UID))return null;
            try {lock(client.ItemSyncRoot) {
                var guild=client.Guild;Guild current;Guild.Member member;
                if(guild==null || !Monitor.TryEnter(GuildLeadership.Gate(guild)))return null;
                try {
                    if(!Q.Player(client,false) || client.Entity.GuildID!=guild.ID
                        || !Kernel.Guilds.TryGetValue(guild.ID,out current) || !ReferenceEquals(current,guild)
                        || !guild.Members.TryGetValue(client.Entity.UID,out member) || !ReferenceEquals(member,client.AsMember)
                        || member.GuildID!=guild.ID)return null;
                    if(category>=5 && category<=8) {
                        try {return SerializePage(FlowerDelivery.GuildRanking(guild.ID,client.Entity.UID,category),category,page);}
                        catch(Exception ex) {
                            Console.WriteLine("[guild-flower-ranking] uid="+client.Entity.UID+" error="+ex.GetType().Name);
                            return null;
                        }
                    }
                    List<RankingList> list;
                    switch(category) {
                        case 0:list=member.SilversList;break;case 1:list=member.CPSList;break;
                        case 2:list=member.GuidesList;break;case 3:list=member.PKSList;break;
                        case 4:list=member.ArsenalsList;break;case 5:list=member.RosesList;break;
                        case 6:list=member.OrchidsList;break;case 7:list=member.LiliesList;break;
                        case 8:list=member.TulipsList;break;default:list=member.TotalDonationsList;break;
                    }
                    return SerializePage(list.ToArray(),category,page);
                } finally {Monitor.Exit(GuildLeadership.Gate(guild));}
            }} finally {SessionInstall.Exit(client.Entity.UID);}
        }
        internal static byte[] SerializePage(RankingList[] rows,byte category,byte page) {
            if(rows==null || !Page(category,page))return null;
            int total=Math.Min(rows.Length,category==9?100:20),start=page*10;
            int count=Math.Max(0,Math.Min(10,total-start));
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)) {
                writer.Write((ushort)(12+count*68));writer.Write((ushort)2101);
                writer.Write(category);writer.Write(page);writer.Write((ushort)count);writer.Write((uint)total);
                for(int i=start;i<start+count;i++) {
                    var row=rows[i];writer.Write(row.EntityID);writer.Write((uint)row.postion);writer.Write((uint)i);
                    foreach(ulong value in new[]{row.silverdoantion,row.cpdoantion,row.guidedoantion,row.pkdoantion,row.arsenaldoantion,
                        row.rosedoantion,row.liliesdoantion,row.orchidesdoantion,row.tuplisedoantion,row.totaldoantion})
                        writer.Write((uint)Math.Min(uint.MaxValue,value));
                    string name=row.Name??"";
                    for(int n=0;n<16;n++)writer.Write(n<name.Length?(byte)name[n]:(byte)0);
                }
                writer.Write(Encoding.ASCII.GetBytes("TQServer"));return stream.ToArray();
            }
        }
        // Keep the legacy entry points as delegates to the same owned serializer.
        public static byte[] BuildPacketRedRosseDonaionPage1(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketRedRossePage2(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketLiliseDonaionPage1(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketLiliesPage2(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketTupliseDonaionPage1(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketTuplisePage2(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketOrchadesDonaionPage1(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketOrchadesPage2(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketGuideDonaionPage1(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketGuidePage2(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketCpDonaionPage1(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketCpsDonationPage2(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketArsenalDonationPage1(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketArsenalDonationPage2(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketPkDonationPage1(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketPkDonationPage2(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketSilverDonationPage1(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketSilverDonationPage2(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketTotalDoantionPage1(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketTotalDoantionPage2(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketTotalDoantionPage3(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketTotalDoantionPage4(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketTotalDoantionPage5(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketTotalDoantionPage6(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketTotalDoantionPage7(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketTotalDoantionPage8(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketTotalDoantionPage9(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
        public static byte[] BuildPacketTotalDoantionPage10(GameState client,byte Subtype,byte pagenumber) {return BuildOwnedPage(client,Subtype,pagenumber);}
    }
}

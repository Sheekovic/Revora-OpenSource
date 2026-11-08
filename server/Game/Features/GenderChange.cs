using System.Drawing;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public static class GenderChange
    {
        public static uint Price { get { return Revora.Config.SerethGenderPrice; } }

        public static string PriceText
        {
            get { return Price.ToString("N0") + " Conquer Points"; }
        }

        public static bool TryTarget(ushort body, out ushort newBody, out ushort newFace)
        {
            switch (body)
            {
                case 1003:
                    newBody = 2001;
                    newFace = 201;
                    return true;
                case 1004:
                    newBody = 2002;
                    newFace = 201;
                    return true;
                case 2001:
                    newBody = 1003;
                    newFace = 1;
                    return true;
                case 2002:
                    newBody = 1004;
                    newFace = 1;
                    return true;
                default:
                    newBody = 0;
                    newFace = 0;
                    return false;
            }
        }

        public static bool Apply(GameState client)
        {
            if (!QuestInventory.Player(client) || !SessionInstall.TryEnter(client.Entity.UID)) return false;
            try {
                bool committed;
                lock (client.ItemSyncRoot) {
                    try { committed=ApplyOwned(client); }
                    catch(System.Exception ex) { QuestInventory.Fail(client,"gender-transaction",ex);return false; }
                }
                if(!committed)return false;
                // The service requires reconnect; the committed projection below
                // lets ordinary disconnect saves retain unrelated pending progress.
                client.Send(new Message("Your gender has changed. Reconnect. You will enter Twin City.", Color.Yellow, Message.TopLeft));
                client.Disconnect();return true;
            } finally { SessionInstall.Exit(client.Entity.UID); }
        }
        private static bool ApplyOwned(GameState client)
        {
            ushort newBody,newFace;
            if(!QuestInventory.Player(client) || client.Booth!=null || client.Trade==null || client.Trade.InTrade
                || !TryTarget(client.Entity.Body,out newBody,out newFace))return false;
            uint price=Price,points=client.Entity.ConquerPoints;
            if(points<price)return false;
            var garment=client.Equipment.TryGetItem((byte)9);
            if(garment!=null && client.Inventory.Count>=40){client.Send("Make room in your bag for your garment.");return false;}
            // Flush ordinary pending character progress before the service. This
            // preflight never charges CPs or changes appearance.
            if(!EntityTable.SaveEntity(client))return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                QuestInventory.Execute(c,t,"LOCK TABLE revora_flower_balances IN SHARE ROW EXCLUSIVE MODE");
                SkillTable.LockOwner(c,t,client);
                AppearanceState.AssertSaved(c,t,client);
                // Preserve any reign immediately before and after membership changes,
                // including a successor who is offline throughout that reign.
                FlowerDelivery.RecordCurrentQueens(c,t);
                if(QuestInventory.Execute(c,t,"UPDATE entities SET conquerpoints=@next WHERE uid=@uid AND conquerpoints=@before",
                    "next",(long)(points-price),"uid",(long)client.Entity.UID,"before",PurseBalance.PreimageCp(client))!=1)
                    throw new System.InvalidOperationException("Gender payment changed.");
                AppearanceState.Persist(c,t,client,newBody,newFace,(ushort)((client.Entity.HairStyle/1000)*1000));
                if(garment!=null && QuestInventory.Execute(c,t,"UPDATE items SET position=0 WHERE uid=@item AND entityid=@uid AND id=@type AND position=9 AND warehouse=0",
                    "item",(long)garment.UID,"uid",(long)client.Entity.UID,"type",(int)garment.ID)!=1)
                    throw new System.InvalidOperationException("Gender garment ownership changed.");
                if(QuestInventory.Execute(c,t,"UPDATE entities SET mapid=1002,x=300,y=278,previousmapid=1002,previousx=300,previousy=278 WHERE uid=@uid",
                    "uid",(long)client.Entity.UID)!=1)throw new System.InvalidOperationException("Gender return location was not saved.");
                FlowerDelivery.RecordCurrentQueens(c,t);
                if(!QuestInventory.Player(client))throw new System.InvalidOperationException("Gender session changed.");
                t.Commit();
            }}
            client.Entity.ApplyCommittedConquerPoints(points-price);
            AppearanceState.ApplyCommitted(client,newBody,newFace,(ushort)((client.Entity.HairStyle/1000)*1000));
            if(garment!=null) {
                if(!client.Equipment.Remove((byte)9,true))throw new System.InvalidOperationException("Committed gender garment requires reload.");
                QuestInventory.Install(client,new uint[0],new[]{garment});
            }
            if(client.NobilityInformation!=null)client.NobilityInformation.Gender=(byte)(newBody%10);
            client.Equipment.UpdateEntityPacket();
            client.Entity.MapID=1002;client.Entity.X=300;client.Entity.Y=278;
            client.Entity.PreviousMapID=1002;client.Entity.PrevX=300;client.Entity.PrevY=278;
            // Reject repeated confirmation before disconnect without suppressing
            // normal saves of unrelated skills, items and character progress.
            client.Action=0;
            return true;
        }
    }
}

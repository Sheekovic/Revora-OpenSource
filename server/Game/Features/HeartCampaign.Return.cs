using System;
using Revora.Client;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static partial class HeartCampaign
    {
        internal static bool Return(GameState p)
        {
            if(!Q.Player(p,false))return false;
            lock(p.ItemSyncRoot)
            {
                if(!Q.Player(p,false))return false;
                try
                {
                    using(var c=Database.DataHolder.MySqlConnection)
                    {
                        c.Open();Guid id;
                        using(var q=Q.Command(c,null,"SELECT id,state FROM revora_heart_encounters WHERE uid=@uid AND NOT returned","uid",(long)p.Entity.UID))
                        using(var r=q.ExecuteReader())
                        {
                            if(!r.Read())return true;
                            if(r.GetString(1)=="active")return false;
                            id=r.GetGuid(0);
                        }
                        Map town;
                        if(!Kernel.Maps.TryGetValue(1002,out town))throw new InvalidOperationException("Heart return town unavailable.");
                        ushort x=321,y=299;bool found=town.Floor[x,y,MapObjectType.Player];
                        for(int radius=1;radius<=5 && !found;radius++)
                            for(int dx=-radius;dx<=radius && !found;dx++)
                                for(int dy=-radius;dy<=radius && !found;dy++)
                                {
                                    ushort nx=(ushort)(321+dx),ny=(ushort)(299+dy);
                                    if(!town.Floor[nx,ny,MapObjectType.Player])continue;
                                    x=nx;y=ny;found=true;
                                }
                        if(!found)throw new InvalidOperationException("Heart safe return is blocked.");
                        p.Entity.Teleport(1002,x,y);
                        if(p.Entity.MapID!=1002 || p.Entity.X!=x || p.Entity.Y!=y)
                            throw new InvalidOperationException("Heart safe return did not arrive.");
                        using(var t=c.BeginTransaction())
                        {
                            if(Q.Execute(c,t,"UPDATE entities SET mapid=1002,x=@x,y=@y WHERE uid=@uid","x",(int)x,"y",(int)y,"uid",(long)p.Entity.UID)!=1)
                                throw new InvalidOperationException("Heart return position not saved.");
                            if(Q.Execute(c,t,"UPDATE revora_heart_encounters SET returned=true WHERE id=@id AND uid=@uid AND NOT returned AND state<>'active'",
                                "id",id,"uid",(long)p.Entity.UID)!=1)
                                throw new InvalidOperationException("Heart return receipt not saved.");
                            t.Commit();
                        }
                    }
                    return true;
                }
                catch(Exception ex){Q.Fail(p,"heart-return",ex);return false;}
            }
        }
    }
}

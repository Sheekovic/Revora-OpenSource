using System;
using System.Collections.Generic;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    internal static class SteedRaceCourse {
        private sealed class Rider {internal GameState Player;internal ushort X,Y;internal bool Started,Invalid;internal int Steps,Tiles;}
        private static readonly object Gate=new object();
        private static readonly Dictionary<uint,Rider> riders=new Dictionary<uint,Rider>();
        private static Guid active;private static Map track;private static ushort startX,startY,finishX,finishY;private static int radius;private static bool opened;
        internal static void Configure(Guid run,Map map,ushort sx,ushort sy,ushort fx,ushort fy,int finishRadius) {
            if(run==Guid.Empty || map==null || map.Floor==null || finishRadius<1 || finishRadius>22
                || !map.Floor[sx,sy,MapObjectType.Player,null] || !map.Floor[fx,fy,MapObjectType.Player,null]
                || Kernel.GetDistance(sx,sy,fx,fy)<=finishRadius)throw new InvalidOperationException("Race start and finish are invalid or overlap.");
            lock(Gate){using(var c=DataHolder.MySqlConnection){c.Open();if(Q.Execute(c,null,@"UPDATE revora_steed_runs SET start_x=@x,start_y=@y,finish_radius=@radius
WHERE id=@id AND map=@map AND finish_x=@fx AND finish_y=@fy AND state='active' AND opened_at IS NULL",
                "x",(int)sx,"y",(int)sy,"radius",finishRadius,"id",run,"map",(int)map.ID,"fx",(int)fx,"fy",(int)fy)!=1)throw new InvalidOperationException("Race course was not saved.");}
                active=run;track=map;startX=sx;startY=sy;finishX=fx;finishY=fy;radius=finishRadius;opened=false;riders.Clear();}
        }
        internal static bool Configured(Guid run){lock(Gate)return run!=Guid.Empty && run==active && track!=null;}
        internal static void Admit(Guid run,GameState p){lock(Gate){if(run!=active)throw new InvalidOperationException("Race course changed.");riders.Add(p.Entity.UID,new Rider{Player=p});}}
        internal static void Open(Guid run){lock(Gate){if(run==active)opened=true;}}
        internal static void Reset(){lock(Gate){active=Guid.Empty;track=null;opened=false;riders.Clear();}}
        internal static void Remove(GameState p){if(p==null || p.Entity==null)return;lock(Gate){Rider r;if(riders.TryGetValue(p.Entity.UID,out r) && ReferenceEquals(r.Player,p))riders.Remove(p.Entity.UID);}}
        internal static bool CanMove(GameState p){if(p==null || p.Entity==null)return true;lock(Gate){Rider r;return !riders.TryGetValue(p.Entity.UID,out r) || !ReferenceEquals(r.Player,p) || r.Invalid || opened;}}
        // Observe only destinations already accepted by the server movement path.
        internal static void Arrived(GameState p,Map map,ushort x,ushort y,TravelProgress.Arrival kind) {
            if(p==null || p.Entity==null)return;
            lock(Gate){Rider r;if(!riders.TryGetValue(p.Entity.UID,out r) || !ReferenceEquals(r.Player,p) || r.Invalid)return;
                if(!Q.Player(p) || !ReferenceEquals(map,track) || p.Entity.MapID!=track.ID || p.Entity.X!=x || p.Entity.Y!=y){r.Invalid=true;return;}
                if(!r.Started){if(!opened && kind==TravelProgress.Arrival.Teleport && x==startX && y==startY){r.Started=true;r.X=x;r.Y=y;}else r.Invalid=true;return;}
                if(kind==TravelProgress.Arrival.Teleport && x==r.X && y==r.Y)return;
                int dx=x-r.X,dy=y-r.Y,distance=Math.Max(Math.Abs(dx),Math.Abs(dy));
                if(!opened || !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ride) || kind!=TravelProgress.Arrival.Walk && kind!=TravelProgress.Arrival.Jump
                    || distance==0 || distance>(kind==TravelProgress.Arrival.Walk?2:20)){r.Invalid=true;return;}
                // Ground traversal for every crossed tile prevents a jump/slide across a course wall.
                for(int n=1;n<=distance;n++){
                    int cx=r.X+(int)Math.Round((double)dx*n/distance),cy=r.Y+(int)Math.Round((double)dy*n/distance);
                    if(!track.Floor[cx,cy,MapObjectType.Player,null]){r.Invalid=true;return;}
                }
                r.X=x;r.Y=y;r.Steps=checked(r.Steps+1);r.Tiles=checked(r.Tiles+distance);
            }
        }
        internal static int Completed(Guid run,GameState p,out int tiles) {
            tiles=0;if(p==null || p.Entity==null)return 0;
            lock(Gate){Rider r;if(run!=active || !opened || !riders.TryGetValue(p.Entity.UID,out r) || !ReferenceEquals(r.Player,p) || !r.Started || r.Invalid || r.Steps==0
                || p.Entity.MapID!=track.ID || !ReferenceEquals(p.Map,track) || p.Entity.X!=r.X || p.Entity.Y!=r.Y
                || Kernel.GetDistance(r.X,r.Y,finishX,finishY)>radius)return 0;
                tiles=r.Tiles;return r.Steps;}
        }
    }
}

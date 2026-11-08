using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshVirtueSnapshot(GameState p,GameState trainee){return FreshVirtueSnapshot(p,trainee.Entity.UID);}
    static string FreshVirtueSnapshot(GameState p,uint trainee){return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'trainee',@trainee::json,'training',(SELECT json_agg(r ORDER BY uid) FROM (SELECT uid,level,experience,virtuepoints FROM entities WHERE uid=ANY(@uids)) r))::text","prior",FreshSnakeSnapshot(p),"trainee",FreshStarterSnapshot(trainee),"uids",new long[]{p.Entity.UID,trainee}));}
    static void FreshVirtueProcess(string phase){
        var p=FreshLoadSaved(true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        bool create=(bool)Sql("SELECT to_regclass('revora_fresh_virtue_probe') IS NULL");var trainee=FreshStarterSession();trainee.Account.Username="FreshPupil1";
        if(create){
            Require(phase.EndsWith("earn") && FreshSnakeSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_snake_probe")) && p.VirtuePoints==0 && (long)Sql("SELECT count(*) FROM accounts WHERE lower(username)='freshpupil1'")==0);
            trainee.Account.Password=Guid.NewGuid().ToString("N");trainee.Account.Insert();Require(trainee.Account.Password=="");string message="";Require(EntityTable.CreateEntity(new EnitityCreate{Name="FreshPupil1",Body=1003,Class=2},trainee,ref message) && message=="ANSWER_OK");
            Sql("CREATE TABLE revora_fresh_virtue_probe(trainee bigint,stage text,kills integer,hits integer,virtue bigint,level integer,snapshot text);INSERT INTO revora_fresh_virtue_probe VALUES(@uid,'created',0,0,0,1,NULL)","uid",(long)trainee.Entity.UID);
        }else{
            uint uid=Convert.ToUInt32(Sql("SELECT trainee FROM revora_fresh_virtue_probe"));trainee.Account.EntityID=uid;trainee.Entity=new Entity(EntityFlag.Player,false){Owner=trainee,UID=uid};Require((bool)Call(Feature("SessionInstall"),"TryClaim",uid,trainee));
        }
        trainee.Quests=new Quests(trainee);Require((bool)Call(typeof(EntityTable),"LoadEntityState",trainee));trainee.Action=2;trainee.Entity.FullyLoaded=true;trainee.BindWorldMap(trainee.Entity.MapID);ConquerItemTable.LoadItems(trainee);SkillTable.LoadSpells(trainee);SkillTable.LoadProficiencies(trainee);trainee.LoadItemStats();Require(!trainee.Fake && trainee.Account.State==AccountTable.AccountState.Player && !PirateFenced(trainee));
        if(create){Require(trainee.Entity.Level==1 && trainee.Entity.Reborn==0);Call(Feature("RevoraStarterPack"),"OnLogin",trainee);Require(trainee.Inventory.Count==1 && trainee.Inventory.Objects.Single().ID==727990 && trainee.Inventory.Objects.Single().Bound);Require(EntityTable.SaveEntity(trainee));Sql("UPDATE revora_fresh_virtue_probe SET snapshot=@snapshot","snapshot",FreshVirtueSnapshot(p,trainee));}
        Require(FreshVirtueSnapshot(p,trainee)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_virtue_probe")));Server.GamePool=new[]{p,trainee};int kills=0,hits=0;
        if(phase.EndsWith("earn")){
            Require(Convert.ToString(Sql("SELECT stage FROM revora_fresh_virtue_probe"))=="created");foreach(var who in new[]{p,trainee}){Revora.Database.KnownPersons.LoadPartner(who);Revora.Database.KnownPersons.LoaderFriends(who);Revora.Database.KnownPersons.LoadMentor(who);who.Entity.Teleport(1015,468,205);}
            Action<GameState,uint,uint> team=(who,action,target)=>{var frame=new Revora.Network.GamePackets.Team{Type=action,UID=target}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);Revora.Network.GamePackets.Team.Process(frame,who);};
            team(p,Revora.Network.GamePackets.Team.Create,0);team(p,Revora.Network.GamePackets.Team.InviteRequest,trainee.Entity.UID);team(trainee,Revora.Network.GamePackets.Team.AcceptInvitation,p.Entity.UID);Require(p.Team!=null && ReferenceEquals(p.Team,trainee.Team) && ReferenceEquals(p.Team.Teammates[0],p));uint expected=p.VirtuePoints;
            while(p.VirtuePoints<2000 && kills<1000){
                var mob=p.Map.Entities.Values.FirstOrDefault(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==18 && !e.Dead);if(mob==null){var until=DateTime.UtcNow.AddSeconds(45);while(mob==null && DateTime.UtcNow<until){System.Threading.Thread.Sleep(100);mob=p.Map.Entities.Values.FirstOrDefault(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==18 && !e.Dead);}}Require(mob!=null);
                trainee.Entity.X=mob.X;trainee.Entity.Y=(ushort)(mob.Y+1);byte before=trainee.Entity.Level;FreshClassicKill(p,1015,18,ref kills,ref hits);if(before<70)for(int level=before;level<trainee.Entity.Level;level++)expected+=(uint)(level*3.83F);Require(p.VirtuePoints==expected && !trainee.Entity.Dead);
            }
            Require(p.VirtuePoints>=2000 && trainee.Entity.Level>1);team(trainee,Revora.Network.GamePackets.Team.ExitTeam,0);team(p,Revora.Network.GamePackets.Team.ExitTeam,0);Require(p.Team==null && trainee.Team==null);Require(EntityTable.SaveEntity(p) && EntityTable.SaveEntity(trainee));Sql("UPDATE revora_fresh_virtue_probe SET stage='earned',kills=@kills,hits=@hits,virtue=@virtue,level=@level,snapshot=@snapshot","kills",kills,"hits",hits,"virtue",(long)p.VirtuePoints,"level",(int)trainee.Entity.Level,"snapshot",FreshVirtueSnapshot(p,trainee));
        }else Require(phase.EndsWith("reload") && Convert.ToString(Sql("SELECT stage FROM revora_fresh_virtue_probe"))=="earned");
        Require(p.VirtuePoints==Convert.ToUInt32(Sql("SELECT virtue FROM revora_fresh_virtue_probe")) && p.VirtuePoints>=2000 && trainee.Entity.Level==Convert.ToByte(Sql("SELECT level FROM revora_fresh_virtue_probe")) && FreshVirtueSnapshot(p,trainee)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_virtue_probe")) && !PirateFenced(p) && !PirateFenced(trainee));System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" trainee="+trainee.Entity.UID+" level="+trainee.Entity.Level+" virtue="+p.VirtuePoints+" kills="+Sql("SELECT kills FROM revora_fresh_virtue_probe")+" hits="+Sql("SELECT hits FROM revora_fresh_virtue_probe"));Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Virtue training did not close.");
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class QuizShowEvent
    {
        internal const uint Npc=900000062;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static readonly TimeZoneInfo Zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        private sealed class Prompt {internal GameState Player;internal string Run;internal int Question;internal DateTime End;internal bool Used;}
        private static readonly ConcurrentDictionary<uint,Prompt> prompts=new ConcurrentDictionary<uint,Prompt>();
        private static bool enabled;
        private static int ticking;
        private static DateTime next;
        private static string ensured;
        internal static DateTime? Window(DateTime utc) {
            if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Quiz timestamps require UTC.");
            var local=TimeZoneInfo.ConvertTimeFromUtc(utc,Zone);
            if(local.DayOfWeek!=DayOfWeek.Saturday && local.DayOfWeek!=DayOfWeek.Sunday)return null;
            foreach(int hour in new[]{4,14,21}) {
                var start=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.Date.AddHours(hour),DateTimeKind.Unspecified),Zone);
                if(utc>=start.AddMinutes(-5) && utc<start.AddMinutes(10))return start;
            }
            return null;
        }
        internal static string Identity(DateTime start) {return TimeZoneInfo.ConvertTimeFromUtc(start,Zone).ToString("yyyyMMddHH",CultureInfo.InvariantCulture);}
        internal static void Initialize() {QuizResults.Initialize();enabled=true;next=DateTime.MinValue;ensured=null;prompts.Clear();}
        internal static void PlaceNpc() {
            var map=Kernel.Maps[(ushort)1002];
            if(Kernel.Maps.Values.Any(m=>m.Npcs!=null && m.Npcs.ContainsKey(Npc)))throw new InvalidOperationException("Quiz NPC identity collision.");
            ushort mesh;using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT lookface FROM npcs WHERE id=2000")) {
                var value=q.ExecuteScalar();if(value==null)throw new InvalidOperationException("Quiz teacher appearance missing.");mesh=Convert.ToUInt16(value);
            }}
            ushort x=430,y=278;bool found=false;
            for(int radius=0;radius<=8 && !found;radius++)for(int dx=-radius;dx<=radius && !found;dx++)for(int dy=-radius;dy<=radius && !found;dy++) {
                int tx=430+dx,ty=278+dy;if(!map.Floor[tx,ty,MapObjectType.Player] || map.Npcs.Values.Any(n=>n.X==tx && n.Y==ty))continue;
                x=(ushort)tx;y=(ushort)ty;found=true;
            }
            if(!found)throw new InvalidOperationException("Quiz courtyard unavailable.");
            CityGuardSystem.SpawnTalker(Npc,1002,x,y,mesh,"MasterWen");
        }
        private static string CurrentRun() {
            var window=Window(Clock());if(!window.HasValue)return null;
            string id=Identity(window.Value);QuizResults.Ensure(id,window.Value);return id;
        }
        private static void Show(GameState p,Npcs d,string id) {
            if(id==null){d.Text("My next show awaits the weekend. Ask me about the hours.");return;}
            using(var c=DataHolder.MySqlConnection){c.Open();var run=QuizResults.Read(c,null,id);
                if(run==null || run.Finished || !QuizResults.Entered(c,null,id,p.Entity.UID)){d.Text("Reserve your place before the first question.");return;}
                int question=QuizResults.Question(run,Clock());if(question<0){d.Text("The questions begin at "+TimeZoneInfo.ConvertTimeFromUtc(run.Start,Zone).ToString("HH:mm",CultureInfo.InvariantCulture)+" Pacific. Stay nearby.");return;}
                using(var q=Q.Command(c,null,"SELECT choice FROM revora_quiz_answers WHERE run=@run AND uid=@uid AND question=@question","run",id,"uid",(long)p.Entity.UID,"question",question))
                    if(q.ExecuteScalar()!=null){prompts[p.Entity.UID]=new Prompt{Player=p,Run=id,Question=question,End=run.Start.AddSeconds((question+1)*30),Used=true};d.Text("Your answer is already sealed. The next question will follow shortly.");return;}
                var entry=QuizQuestionsV1.All[run.Deck[question]];
                prompts[p.Entity.UID]=new Prompt{Player=p,Run=id,Question=question,End=run.Start.AddSeconds((question+1)*30)};
                int seconds=Math.Max(1,(int)Math.Ceiling((run.Start.AddSeconds((question+1)*30)-Clock()).TotalSeconds));
                d.Text("Question "+(question+1)+" of 20. "+seconds+" seconds remain. "+entry.Text);
                for(int i=0;i<4;i++)d.Option(entry.Choices[i],(byte)(10+question*4+i));
            }
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(!enabled || p==null || p.Entity==null || request==null || p.ActiveNpc!=Npc)return false;
            if(p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot) {
                if(!Q.Near(p,Npc,1002))return true;
                try {
                    var d=new Npcs(p);byte option=request.OptionID;
                    if(option==5){Prompt ignored;prompts.TryRemove(p.Entity.UID,out ignored);p.ActiveNpc=0;return true;}
                    if(option>=10 && option<90) {
                        Prompt prompt;int question=(option-10)/4,choice=(option-10)%4;
                        if(prompts.TryGetValue(p.Entity.UID,out prompt) && ReferenceEquals(prompt.Player,p) && !prompt.Used
                            && prompt.Question==question && Clock()<prompt.End) {
                            prompt.Used=true;d.Text(QuizResults.Answer(p,prompt.Run,question,choice));
                        } else d.Text("Ask me for the current question before answering.");
                    } else if(option==1)Show(p,d,CurrentRun());
                    else if(option==2) {string id=CurrentRun();d.Text(id==null?"Enrollment opens five minutes before each show.":QuizResults.Join(p,id));}
                    else if(option==3)d.Text(QuizResults.Claim(p));
                    else if(option==4)d.Text("Weekends at 04:00, 14:00 and 21:00 Pacific. Join five minutes early. Twenty questions, thirty seconds each. Five correct earns EXP Balls and a bound P7 soul in new shows. Speed decides the standings.");
                    else {
                        d.Text("A sharp mind travels farther than a sharp blade. Will you test yours?");
                        d.Option("Save me a place.",2);d.Option("Explain the show.",4);
                    }
                    d.Option("The current question, please.",1);d.Option("Claim my prize.",3);d.Option("Leave the quiz table.",5);d.Send();
                }catch(Exception ex){Q.Fail(p,"quiz-dialogue",ex);}
            }
            return true;
        }
        internal static void Tick() {
            if(!enabled || Interlocked.Exchange(ref ticking,1)!=0)return;
            try {
                var now=Clock();if(now<next)return;next=now.AddSeconds(Window(now).HasValue?2:30);
                foreach(var entry in prompts.ToArray())if(entry.Value.End<=now || !Q.Player(entry.Value.Player))((ICollection<KeyValuePair<uint,Prompt>>)prompts).Remove(entry);
                var window=Window(now);string id=window.HasValue?Identity(window.Value):null;
                if(id!=null && ensured!=id){QuizResults.Ensure(id,window.Value);ensured=id;Kernel.SendWorldMessage(new Message(now<window.Value?"Master Wen's Quiz Show: join in Twin City near (430,278) before the first question.":"Master Wen's Quiz Show is underway. Enrolled players can rejoin him in Twin City near (430,278).",System.Drawing.Color.White,Message.Center));}
                var due=new List<string>();using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT id FROM revora_quiz_runs WHERE NOT finished AND starts_at<=@cutoff ORDER BY starts_at LIMIT 8","cutoff",now.AddMinutes(-10)))
                    using(var r=q.ExecuteReader())while(r.Read())due.Add(r.GetString(0));}
                foreach(string closed in due)foreach(uint uid in QuizResults.Finish(closed)) {
                    GameState p;if(Kernel.GamePool.TryGetValue(uid,out p) && Q.Player(p,false)){Q.Refresh(p);p.Send("The Quiz Show has ended. Master Wen holds any prize you earned.");}
                }
                if(id==null || now<window.Value)return;
                var players=new List<uint>();using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT uid FROM revora_quiz_entries WHERE run=@run ORDER BY uid LIMIT 256","run",id))
                    using(var r=q.ExecuteReader())while(r.Read())players.Add(Convert.ToUInt32(r[0]));}
                foreach(uint uid in players) {
                    GameState p;Prompt prompt;if(!Kernel.GamePool.TryGetValue(uid,out p) || !Q.Near(p,Npc,1002) || prompts.TryGetValue(uid,out prompt))continue;
                    lock(p.ItemSyncRoot)if(Q.Near(p,Npc,1002)) {var d=new Npcs(p);Show(p,d,id);d.Option("Leave the quiz table.",5);d.Send();}
                }
            }catch(Exception ex){Console.WriteLine("[quiz] source=tick error="+ex.GetType().Name+" inner="+(ex.InnerException==null?"none":ex.InnerException.GetType().Name));}
            finally{Volatile.Write(ref ticking,0);}
        }
    }
}

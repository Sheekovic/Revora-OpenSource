// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Revora.Client;
using System.Collections.Concurrent;
using Revora.Network.GamePackets;

namespace Revora.Game.ConquerStructures
{
    public unsafe class QuizShow
    {
        public class QuizClient
        {
            public uint Points = 0;
            public ushort Timer = 0;
            public int Rank;
            public bool Answered = false;
            public uint UID = 0;
            public string Name = "";
            public GameState Client;
            public byte RightQuestion = 2;
            public QuizClient(GameState play)
            {
                UID = play.Entity.UID;
                Name = play.Entity.Name;
                Client = play;
            }
        }
        public class Question
        {
            public string[] All = new string[5];
            public byte AnswerRight = 0;
            public bool Used = false;
        }
        public QuizShow()
        {
            Questions = new List<Question>();
            RegisteredUsers = new ConcurrentDictionary<uint, QuizClient>();
            Database.QuizData.Load(this);
        }
        public List<Question> Questions;
        public ConcurrentDictionary<uint, QuizClient> RegisteredUsers;
        public bool Open = false;
        public Time32 LastQuestion;
        public int AllQuestions = 0;
        public Question CurrentQuestion = null;
        public uint NewQuestionTime = 0;
        private IDisposable Subscriber;
        private bool sendStart = false;
        public ushort NoQuestion = 0;
        public readonly ushort QuestionCount = 20;
        public readonly ushort TimeLimit = 30;
        public readonly int RightAnswerReward = 10;
        public void Start()
        {
            if (!Open)
            {
                LastQuestion = Time32.Now;
                Open = true;
                foreach (var obj in Server.GamePool)
                    RegisteredUsers.Add(obj.Entity.UID, new QuizClient(obj));
                foreach (var question in Questions)
                    question.Used = false;

                ranks = new Dictionary<uint, QuizClient>();
                Top3 = new QuizClient[3];

                Subscriber = Thread.Subscribe(QuizTimerCallback, 1000);
            }
        }
        public void AddPlayer(GameState client)
        {
            RegisteredUsers.Add(client.Entity.UID, new QuizClient(client));
            OpenQuiz open = new OpenQuiz();
            open.AllQuestions = (byte)(QuestionCount - NoQuestion);
            open.FullTimeLimit = TimeLimit;
            open.ExpBallFirst = 900;
            open.ExpBall2nd = 600;
            open.ExpBall3rd = 300;
            open.StartInTimeSecouds = (ushort)(TimeLimit - NewQuestionTime);
            open.Type = QuizShowTypes.Open;
            client.Send(open);
        }
        public void RemovePlayer(GameState client)
        {
            RegisteredUsers.Remove(client.Entity.UID);
        }
        public Question GetNextQuest()
        {
            Question quest = null;
            if (Open)
            {
                while (true)
                {
                    int rand = Kernel.Random.Next(Questions.Count);
                    quest = Questions[rand];
                    if (!quest.Used) break;
                }
                quest.Used = true;
            }
            return quest;
        }
        public void Clear()
        {
            sendStart = false;
            Open = false;
            CurrentQuestion = null;
            ranks = null;
            Top3 = null;
            RegisteredUsers.Clear();
            Subscriber.Dispose();
        }
        public bool FirstQuestion = false;
        public void QuizTimerCallback(int time)
        {
            if (!Open)
            {
                Subscriber.Dispose();
                return;
            }

            if (!sendStart)
            {
                sendStart = true;

                OpenQuiz open = new OpenQuiz();
                open.AllQuestions = QuestionCount;
                open.FullTimeLimit = TimeLimit;
                open.ExpBall2nd = 600;
                open.ExpBallFirst = 900;
                open.ExpBall3rd = 300;
                open.StartInTimeSecouds = 30;//30 secounde for start
                open.Type = QuizShowTypes.Open;
                Kernel.SendWorldMessage(open);
                FirstQuestion = true;
            }
            else
            {
                if (Time32.Now > LastQuestion.AddSeconds(TimeLimit))
                {
                    if (NoQuestion == 1)
                        FirstQuestion = false;

                    Question question = GetNextQuest();

                    SortByPoints();


                    if (NoQuestion == QuestionCount)
                    {
                        QuizRank quizRank = new QuizRank();
                        QuizHistory quizHistory = new QuizHistory();

                        quizRank.Type = QuizShowTypes.SendTop;
                        quizHistory.Type = QuizShowTypes.History;

                        int i = 3;
                        foreach (QuizClient topClient in Top3)
                        {
                            if (topClient != null)
                            {
                                quizRank.Aprend(topClient.Name, (ushort)topClient.Points, topClient.Timer);
                                topClient.Client.IncreaseExperience(Revora.Game.Features.ExperienceMath.Scale(topClient.Client.ExpBall,(ulong)i,1), false);
                                i--;
                            }
                        }


                        foreach (QuizClient quizClient in RegisteredUsers.Values)
                        {

                            if (quizClient.Answered == false)
                                quizClient.Timer += TimeLimit;

                            quizRank.MyPoints = (ushort)quizClient.Points;
                            quizRank.MyTime = quizClient.Timer;
                            quizRank.MyRank = (byte)quizClient.Rank;
                            quizClient.Client.Send(quizRank.ToArray());

                            quizHistory.MyPoints = (ushort)quizClient.Points;
                            quizHistory.MyRank = (byte)quizClient.Rank;
                            quizHistory.MyTime = quizClient.Timer;
                            quizHistory.ExpBallsReceives = (ushort)quizClient.Points;
                            quizHistory.Append(quizClient.Name, (ushort)quizClient.Points, quizClient.Timer);
                            quizClient.Client.Send(quizHistory);
                        }

                        Clear();
                        return;
                    }
                    NoQuestion++;
                    CurrentQuestion = question;

                    QuizQuestions quizQuestion = new QuizQuestions(question.All);
                    quizQuestion.AllQuestions = QuestionCount;
                    quizQuestion.FullTimeLimit = TimeLimit;
                    quizQuestion.NoQuestion = NoQuestion;
                    quizQuestion.Type = QuizShowTypes.SendQuestion;
                    foreach (QuizClient quizClient in RegisteredUsers.Values)
                    {
                        if (NoQuestion > 1)
                            if (quizClient.Answered == false)
                                quizClient.Timer += 30;
                        quizClient.Answered = false;
                        quizQuestion.Right = quizClient.RightQuestion;
                        quizQuestion.MyPoints = (ushort)quizClient.Points;
                        quizClient.Client.Send(quizQuestion.ToArray());
                    }

                    QuizRank CurrentRanks = new QuizRank();
                    CurrentRanks.Type = QuizShowTypes.SendTop;
                    foreach (QuizClient topClient in Top3)
                        if (topClient != null)
                            CurrentRanks.Aprend(topClient.Name, (ushort)topClient.Points, topClient.Timer);

                    foreach (QuizClient quizClient in RegisteredUsers.Values)
                    {
                        CurrentRanks.GiveRight = quizClient.RightQuestion;
                        CurrentRanks.MyPoints = (ushort)quizClient.Points;
                        CurrentRanks.MyTime = quizClient.Timer;
                        CurrentRanks.MyRank = (byte)quizClient.Rank;
                        quizClient.Client.Send(CurrentRanks.ToArray());
                    }

                    NewQuestionTime = 0;
                    LastQuestion = Time32.Now;
                }
                else
                {
                    NewQuestionTime++;
                }
            }
        }
        public Dictionary<uint, QuizClient> ranks;
        public QuizClient[] Top3;
        public void SortByPoints()
        {
            ranks.Clear();
            var data = RegisteredUsers.Values.ToArray();
            var sortedArray = data.OrderByDescending(p => p.Points).ToArray();
            for (int i = 0; i < sortedArray.Length; i++)
            {
                var item = sortedArray[i];
                item.Rank = i + 1;
                if (i <= 2) Top3[i] = item;
                if (item.Rank > 255) item.Rank = 0;
                ranks.Add(item.UID, item);
            }
        }
    }
}
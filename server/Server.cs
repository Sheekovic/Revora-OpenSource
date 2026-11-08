using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.AuthPackets;
using Revora.Network.GamePackets;
using Revora.Network.Sockets;
using Revora.Union;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Revora
{
    public unsafe class Server
    {
        #region Definitions
        public static int PlayerCap = 0;
        public static long MaxPlayers = 0;
        public static ServerSocket AuthServer;
        public static ServerSocket GameServer;
        public static Counter EntityUID;
        public static string GameIP;
        public static string CrossIP;
        public static ushort CrossPort;
        public static ushort AntiCheatPort;
        public static DayOfWeek Today;
        public static ushort GamePort;
        public static ushort AuthPort;
        public static bool Vip = false;
        public static DateTime StartDate;
        public static Thread Thread;
        public static VariableVault Vars;
        public static bool PezziPC = false;
        public static string Password;
        public static int RandomSeed = 0;
        public static bool TestingMode = false;
        public static bool AllTest = false; 
        public static Encoding Encoding = ASCIIEncoding.Default;
        public static DateTime LastRandomReset = DateTime.Now;
        public static Client.GameState[] GamePool = new Client.GameState[0];
        public static Client.GameState[] Values = new Client.GameState[0];
        #endregion
        #region MemoryUsage
        //[DllImport("user32.dll")]
        //public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        //[DllImport("user32.dll")]
        //public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        //private const int TimeLimit = 10000;
        //public static bool CpuUsageTimer = true, WriteHeadLoad = true;
        //public static int CpuUse = 0;
        //public static MemoryCompressor MCompressor = new MemoryCompressor();
        //[DllImport("psapi.dll")]
        //static extern int EmptyWorkingSet(IntPtr hwProc);
        //public static void MinimizeFootprint()
        //{
        //    EmptyWorkingSet(Process.GetCurrentProcess().Handle);
        //}
        //public static void minimizeMemory()
        //{
        //    GC.Collect(GC.MaxGeneration);
        //    GC.WaitForPendingFinalizers();
        //    SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle,
        //        (UIntPtr)0xFFFFFFFF, (UIntPtr)0xFFFFFFFF);
        //}
        //[DllImport("kernel32.dll")]
        //[return: MarshalAs(UnmanagedType.Bool)]
        //private static extern bool SetProcessWorkingSetSize(IntPtr process,UIntPtr minimumWorkingSetSize, UIntPtr maximumWorkingSetSize);
        #endregion
        #region ConsoleHandler
        static bool OnclosingEvent(int Type)
        {
            foreach (Client.GameState client in Server.GamePool)
            {
                ClanWar.Save();
                Database.InnerPowerTable.Save();
                Database.NameChange.UpdateNames();
                Database.StatueTable.Save();
                //Database.Flowers.SaveFlowers();
                Database.JiangHu.SaveJiangHu();                      
                Database.MailboxTable.Save(client);
                Database.RewardDataBase.Save(client);
                Database.DialyQuestTable.Save(client);
                Database.ActivenessTable.Save(client);
                Database.SkillTable.SaveSpells(client);
                client.HeartbeatSave();
                Database.SkillTable.SaveProficiencies(client);
                Game.Features.Tournaments.TeamElitePk.TeamTournament.Save();
                Database.ArenaTable.SaveArenaStatistics(client.ArenaStatistic);
                Game.Features.Tournaments.TeamElitePk.SkillTeamTournament.Save();
                Database.TeamArenaTable.SaveArenaStatistics(client.TeamArenaStatistic);
            }
            return true;
        }
        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleCtrlHandler(ClosingHandler handler, bool add);
        private delegate bool ClosingHandler(int type);
        private static ClosingHandler KeepAlive;
        #endregion
        #region GMQuest Event
        public static long Carnaval = 0;
        public static long Carnaval2 = 0;
        public static long Carnaval3 = 0;
        #endregion
        #region GameServer
        public class ClientDHPacket
        {
            public string Client_PubKey;
            public int JunkLength;
            public ClientDHPacket(byte[] Packet)
            {
                if (Packet == null || Packet.Length < 15)
                    throw new EndOfStreamException("dh packet shorter than header");
                MemoryStream MS = new MemoryStream(Packet);
                BinaryReader BR = new BinaryReader(MS);
                BR.ReadBytes(7);
                BR.ReadUInt32();
                JunkLength = BR.ReadInt32();
                if (JunkLength < 0 || JunkLength > Packet.Length || MS.Position + JunkLength + 4 > Packet.Length)
                    throw new EndOfStreamException("dh junk length " + JunkLength);
                BR.ReadBytes(JunkLength);
                int len = BR.ReadInt32();
                if (len < 0 || MS.Position + len > Packet.Length)
                    throw new EndOfStreamException("dh pubkey length " + len);
                Client_PubKey = Encoding.ASCII.GetString(BR.ReadBytes(len));
                BR.Close();
                MS.Close();
            }
            public void Edit(byte[] Packet, string NewKey)
            {
                MemoryStream MS = new MemoryStream(Packet);
                BinaryWriter BW = new BinaryWriter(MS);
                BW.Seek(19 + JunkLength, SeekOrigin.Current);
                BW.Write(Encoding.ASCII.GetBytes(NewKey));
            }
        }
        static void GameServer_OnClientReceive(byte[] buffer, int length, ClientWrapper obj)
        {
            if (obj.Connector == null)
            {
                obj.Disconnect();
                return;
            }

            GameState Client = obj.Connector as GameState;

            if (Client.Exchange)
            {
                try
                {
                    var crypto = new Network.Cryptography.GameCryptography(Encoding.Default.GetBytes(Constants.GameCryptographyKey));
                    byte[] otherData = new byte[length];
                    Array.Copy(buffer, otherData, length);
                    crypto.Decrypt(otherData, length);
                    var ClientDataDHP = new ClientDHPacket(otherData);
                    string PubKey = ClientDataDHP.Client_PubKey ?? "";
                    int pos = 19 + ClientDataDHP.JunkLength + PubKey.Length + 8;
                    Console.WriteLine("[game] dh-in bytes=" + length
                        + " junk=" + ClientDataDHP.JunkLength
                        + " pub-len=" + PubKey.Length
                        + " pos=" + pos);
                    if (ClientDataDHP.JunkLength < 10 || ClientDataDHP.JunkLength > 49
                        || PubKey.Length < 1 || PubKey.Length > 128 || pos > length)
                    {
                        Console.WriteLine("[game] dh-fail layout");
                        obj.Disconnect();
                        return;
                    }
                    Client.Exchange = false;
                    Client.Action = 1;
                    Client.Cryptography = Client.DHKeyExchange.HandleClientKeyPacket(PubKey, Client.Cryptography);
                    int leftover = length - pos;
                    if (leftover > 0)
                    {
                        byte[] data = new byte[leftover];
                        Buffer.BlockCopy(buffer, pos, data, 0, leftover);
                        Console.WriteLine("[game] dh-extra leftover=" + leftover);
                        processData(data, leftover, Client);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("[game] dh-fail " + e.GetType().Name + " " + e.Message);
                    obj.Disconnect();
                }
            }
            else
            {
                processData(buffer, length, Client);
            }
        }
        static void GameServer_OnClientConnect(ClientWrapper obj)
        {
            GameState client = new GameState(obj);
            obj.Connector = client;
            byte[] hello = client.DHKeyExchange.CreateServerKeyPacket();
            Console.WriteLine("[game] connect " + obj.IP + " dh-hello=" + hello.Length);
            client.SendHandshake(hello);
        }
        static void GameServer_OnClientDisconnect(ClientWrapper obj)
        {
            if (obj.Connector != null)
                (obj.Connector as GameState).Disconnect();
            else
                obj.Disconnect();
        }
        private static void processData(byte[] buffer, int length, GameState Client)
        {
            Client.Cryptography.Decrypt(buffer, length);
            Client.Queue.Enqueue(buffer, length);
            if (Client.Queue.CurrentLength > 1224)
            {
                Console.WriteLine("[Disconnect]Reason:The packet size is too big. " + Client.Queue.CurrentLength);
                Client.Disconnect();
                return;
            }
            while (Client.Queue.CanDequeue())
            {
                byte[] data = Client.Queue.Dequeue();
                /* System.Threading.Tasks.Task.Factory.StartNew(() =>
                 {
                     Network.PacketHandler.HandlePacket(data, Client);
                 });*/

                Network.PacketHandler.HandlePacket(data, Client);
            }
        }
        static void AuthServer_OnClientReceive(byte[] buffer, int length, ClientWrapper arg3)
        {
            var player = arg3.Connector as AuthClient;
            AuthClient authClient = arg3.Connector as AuthClient;
            player.Cryptographer.Decrypt(buffer, length);
            player.Queue.Enqueue(buffer, length);
            while (player.Queue.CanDequeue())
            {
                byte[] packet = player.Queue.Dequeue();

                ushort len = BitConverter.ToUInt16(packet, 0);
                ushort id = BitConverter.ToUInt16(packet, 2);

                if (len == 312)
                {
                    player.Srp = null;
                    player.Info = new Authentication();
                    player.Info.Deserialize(packet, player.PasswordSeed);
                    player.Account = new AccountTable(player.Info.Username);
                    bool match = player.Account.exists && (player.Account.HasSrpVerifier || player.Account.Password == player.Info.Password);
                    if (!match && player.Account.exists)
                    {
                        string storedMethod;
                        if (Authentication.MatchesStored(packet, player.PasswordSeed, player.Info.Username,
                            player.Account.Password, out storedMethod))
                        {
                            player.Info.Password = player.Account.Password;
                            player.Info.PasswordMethod = storedMethod;
                            match = true;
                        }
                    }
                    int p73 = 0;
                    if (packet.Length >= 105)
                        for (int i = 73; i < 105; i++)
                            if (packet[i] != 0) p73++;
                    // Genuine 6711 puts the secret in SRP (1214), not field 73.
                    // Field 73 is empty on this client. Do not require the stored
                    // password to equal the username; packet 1214 proves it.
                    if (!match && player.Account.exists && p73 == 0)
                    {
                        player.Info.Password = player.Account.Password;
                        player.Info.PasswordMethod = "empty-73-srp";
                        match = true;
                    }
                    Console.WriteLine("[auth] request type=" + id + " account-found=" + player.Account.exists);
                    player.Info.Password = "";
                    if (!BruteForceProtection.AcceptJoin(arg3.IP))
                    {
                        Console.WriteLine(string.Concat(new string[] { "Client > ", player.Info.Username, " was blocked address ", arg3.IP, "!" }));
                        arg3.Disconnect();
                        break;
                    }
                    if (IPBan.IsBanned(arg3.IP))
                    {
                        Forward banned = new Forward();
                        banned.Type = Forward.ForwardType.Banned;
                        player.Send(banned);
                        return;
                    }
                    if (!match)
                    {
                        BruteForceProtection.ClientRegistred(arg3.IP);
                        Forward reject = new Forward();
                        reject.Type = Forward.ForwardType.InvalidInfo;
                        player.Send(reject);
                    }
                    else
                    {
                        player.Srp = player.Account.BeginSrp(player.Info.Username);
                        byte[] challenge = player.Srp.CreateChallenge();
                        Console.WriteLine("[auth] 1213 user-len=" + player.Info.Username.Length
                            + " b-len=" + player.Srp.PublicB.Length);
                        player.Send(challenge);
                    }
                }
                else if (len == 262 && id == 1214)
                {
                    byte[] publicA;
                    byte[] proof;
                    bool parsed = Srp6711Exchange.TryReadProof(packet, out publicA, out proof);
                    bool valid = parsed && player.Srp != null && player.Srp.TryVerify(publicA, proof);
                    Console.WriteLine("[auth] 1214 parsed=" + parsed
                        + " a-len=" + (publicA == null ? 0 : publicA.Length)
                        + " proof-len=" + (proof == null ? 0 : proof.Length)
                        + " valid=" + valid);
                    Forward Fw = new Forward();
                    if (!valid)
                    {
                        Fw.Type = Forward.ForwardType.InvalidInfo;
                        player.Send(Fw);
                    }
                    else
                    {
                        try { AccountActivity.Authenticated(player.Account); }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[account-activity] error=" + ex.GetType().Name);
                            Fw.Type = Forward.ForwardType.InvalidInfo;
                            player.Send(Fw);
                            return;
                        }
                        Fw.Type = Forward.ForwardType.Ready;
                        Fw.Identifier = player.Account.GenerateKey();
                        Kernel.AwaitingPool[Fw.Identifier] = player.Account;
                        Fw.IP = GameIP;
                        Fw.Port = GamePort;
                        Fw.ClientConfirmation = player.Srp.Confirmation;
                        Console.WriteLine("[auth] forwarding authenticated connection");
                        player.Send(Fw);
                    }
                }
                else
                {
                    Console.WriteLine("[auth] packet id=" + id + " len=" + len);
                }
            }
        }
        static void AuthServer_OnClientDisconnect(ClientWrapper obj)
        {
            obj.Disconnect();
        }
        static void AuthServer_OnClientConnect(ClientWrapper obj)
        {
            AuthClient authState;
            obj.Connector = (authState = new AuthClient(obj));
            authState.Cryptographer = new Network.Cryptography.AuthCryptography();
            PasswordCryptographySeed pcs = new PasswordCryptographySeed();
            pcs.Seed = Kernel.Random.Next();
            authState.PasswordSeed = pcs.Seed;
            authState.Send(pcs);
        }
        #endregion
        #region SaveException|Commands
        private static void WorkConsole()
        {
            while (true)
            {
                try
                {
                    CommandsAI(Console.ReadLine());
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
        public static void CambiarNombreRegistro(string gm, string commandStr)
        {
            String folderN = DateTime.Now.Year + "-" + DateTime.Now.Month, Path =
                "database\\RegistroCambioNombre\\", NewPath = System.IO.Path.Combine(Path, folderN);// folder path; adjust as needed
            if (!File.Exists(NewPath + folderN))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Path, folderN));
            }
            if (!File.Exists(NewPath + "\\" + DateTime.Now.Day + ".txt"))
            {
                using (System.IO.FileStream fs = System.IO.File.Create(NewPath + "\\" + DateTime.Now.Day + ".txt"))
                {
                    fs.Close();
                }
            }
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(NewPath + "\\" + DateTime.Now.Day + ".txt", true))
            {
                file.WriteLine(gm + commandStr);
            }
        }  
        public static void CommandsAI(string command)
        {
            if (command == null) return;
            string[] data = command.Split(' ');
            switch (data[0])
            {
                case "@ban":
                    {
                        Database.IPBan.Load();
                        break;
                    }
                case "@Rayzo":
                    {
                        Controlpanel MohamedElsayed = new Controlpanel();
                        MohamedElsayed.ShowDialog(); break;
                    }
                case "@Rayzo3":
                    {
                        Database.AccountTable account = new AccountTable(data[1]);
                        account.Password = data[2];
                        account.State = AccountTable.AccountState.Player;
                        account.Save();
                    }
                    break;
                case "@Rayzo1":
                    {
                        Ronax.JiangHu MohamedElsayed = new Ronax.JiangHu();
                        MohamedElsayed.ShowDialog(); break;
                    }
               
                case "@Rayzo2":
                    {
                        new Franko.FrankoNpcControl().ShowDialog();
                        break;
                    }
                case "@encendido":
                    {
                        DateTime now = DateTime.Now;
                        TimeSpan t2 = new TimeSpan(StartDate.ToBinary());
                        TimeSpan t1 = new TimeSpan(now.ToBinary());
                        Console.WriteLine("Servidor Prendido " + (int)(t1.TotalHours - t2.TotalHours) + " horas, " + (int)((t1.TotalMinutes - t2.TotalMinutes) % 60) + " minutos.");
                        break;
                    }
                case "@steam":
                    {
                        if (!Game.Features.Tournaments.TeamElitePk.SkillTeamTournament.Open()) break;
                        foreach (var clien in Kernel.GamePool.Values)
                        {
                            if (clien.Team == null)
                                clien.Team = new Game.ConquerStructures.Team(clien);
                            // Fighters enter through the ordinary Skill Registrar themselves.
                        }
                        break;
                    }
                case "@team":
                    {
                        Game.Features.Tournaments.TeamElitePk.TeamTournament.Open();
                        foreach (var clien in Kernel.GamePool.Values)
                        {
                            if (clien.Team == null)
                                clien.Team = new Game.ConquerStructures.Team(clien);
                            Game.Features.Tournaments.TeamElitePk.TeamTournament.Join(clien, 3);
                        }
                        break;
                    }
                case "@conectados":
                    {
                        Console.WriteLine("Connected Players: " + Kernel.GamePool.Count);
                        string line = "";
                        foreach (Client.GameState pClient in Server.GamePool)
                            line += pClient.Entity.Name + ",";
                        if (line != "")
                        {
                            line = line.Remove(line.Length - 1);
                            Console.WriteLine("Players: " + line);
                        }
                        break;
                    }
                case "@memo":
                    {
                        var proc = System.Diagnostics.Process.GetCurrentProcess();
                        Console.WriteLine("Thread count: " + proc.Threads.Count);
                        Console.WriteLine("Memory set(MB): " + ((double)((double)proc.WorkingSet64 / 1024)) / 1024);
                        proc.Close();
                        break;
                    }
                case "@eslam":
                    {
                        foreach (Client.GameState client in Kernel.GamePool.Values)
                        {
                            client.Map.FreezeMonsters = true;
                            var Clone = client.Map.Entities.Values.ToArray();
                            foreach (var mob in Clone)
                                if (!mob.Companion)
                                {
                                    client.Map.Floor[mob.X, mob.Y, Revora.Game.MapObjectType.Monster, mob] = true;
                                    client.Map.Entities.Remove(mob.UID);
                                }
                            Clone = new Game.Entity[0];
                            client.Map.EntityUIDCounter.Now = 400000;
                            client.Map.LoadMonsters();
                            client.Map.FreezeMonsters = false;
                            foreach (Client.GameState Client in Kernel.GamePool.Values)
                            {
                                if (Client.Map.ID == client.Map.ID)
                                {
                                    Client.Entity.Teleport(Client.Entity.MapID, Client.Entity.X, Client.Entity.Y);
                                }

                            }

                        }
                        break;
                    }
                case "@test":
                    {
                        Console.WriteLine("The server will Be Restart After  5 minutes.");
                        Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("The server will be Restart in 5 minutes, please exit the game now..", System.Drawing.Color.Orange, 2011), Server.GamePool);
                        System.Threading.Thread.Sleep(30000);
                        Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("The server will be Restart in 4 minutes 30 seconds, please exit the game now..", System.Drawing.Color.Orange, 2011), Server.GamePool);
                        System.Threading.Thread.Sleep(30000);
                        Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("The server will be Restart in 4 minutes, please exit the game now..", System.Drawing.Color.Orange, 2011), Server.GamePool);
                        System.Threading.Thread.Sleep(30000); Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("The server will be Restart in 3 minutes 30 seconds, please exit the game now.", System.Drawing.Color.Orange, 2011), Server.GamePool);
                        System.Threading.Thread.Sleep(30000);
                        Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("The server will be Restart in 3 minutes, please exit the game now..", System.Drawing.Color.Orange, 2011), Server.GamePool);
                        System.Threading.Thread.Sleep(30000);
                        Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("The server will be serviced in 2 minutes 30 seconds, please exit the game now..", System.Drawing.Color.Orange, 2011), Server.GamePool);
                        System.Threading.Thread.Sleep(30000);
                        Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("The server will be serviced in 2 minutes, please exit the game now..", System.Drawing.Color.Orange, 2011), Server.GamePool);
                        System.Threading.Thread.Sleep(30000);
                        Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("The server will be serviced in 1 minute 30 seconds, please exit the game now..", System.Drawing.Color.Orange, 2011), Server.GamePool);
                        System.Threading.Thread.Sleep(30000);
                        Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("The server will be serviced in 1 minute, please exit the game now..", System.Drawing.Color.Orange, 2011), Server.GamePool);
                        System.Threading.Thread.Sleep(30000);
                        Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("The server will be serviced in 30 seconds, please exit the game now..", System.Drawing.Color.Orange, 2011), Server.GamePool);
                        Console.WriteLine("Server will exit after 1 minute.");
                        CommandsAI("@save");
                        System.Threading.Thread.Sleep(30000);
                        Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("The server restarted, please log in after 2 minutes! ", System.Drawing.Color.Orange, 0x7db), Server.GamePool);
                        try
                        {
                            CommandsAI("@restart");
                        }
                        catch
                        {
                            Console.WriteLine("The server cannot exit.");
                        }
                        break;
                    }
                case "@clean":
                    {
                        Console.Clear();
                        Console.WriteLine3("Console Cleaned");
                        break;
                    }               
                case "@spill":
                    {
                        new Custom.GUI.SpellControl().ShowDialog();
                        break;
                    }
                case "@close":
                    {
                        GameServer.Disable();
                        AuthServer.Disable();
                        if (!Game.Features.ArenaShutdownDrain.TryDrain(5000))
                        {
                            Console.WriteLine("Arena shutdown is pending. Entries are closed; fights/results remain owned. Retry @close after they settle.");
                            break;
                        }
                        var WC = Server.GamePool.ToArray();
                        foreach (Client.GameState client in WC)
                            client.Disconnect();
                      //  new Database.MySqlCommand(Database.MySqlCommandType.UPDATE).Update("configuration").Set("ItemUID", ConquerItem.ItemUID.Now).Where("Server", Constants.ServerName).Execute();
                    //    Database.JiangHu.SaveJiangHu();
                        Database.StatueTable.Save();
                        Database.InnerPowerTable.Save();
                        new Database.MySqlCommand(Database.MySqlCommandType.UPDATE).Update("configuration").Set("ItemUID", ConquerItem.ItemUID.Now).Where("Server", Constants.ServerName).Execute();
                        Database.EntityVariableTable.Save(0, Vars);
                        Environment.Exit(0);
                        Console.WriteLine4("Saved Successful! / Closing Server ");
                        break;
                    }

                case "@restart":
                    {
                        GameServer.Disable();
                        AuthServer.Disable();
                        if (!Game.Features.ArenaShutdownDrain.TryDrain(5000))
                        {
                            Console.WriteLine("Arena shutdown is pending. Entries are closed; fights/results remain owned. Retry @restart after they settle.");
                            break;
                        }
                        var WC = Server.GamePool.ToArray();
                        foreach (Client.GameState client in WC)
                            client.Disconnect();
                        Database.InnerPowerTable.Save();
                        new Database.MySqlCommand(Database.MySqlCommandType.UPDATE).Update("configuration").Set("ItemUID", Network.GamePackets.ConquerItem.ItemUID.Now).Where("Server", Constants.ServerName).Execute();
                       // new Database.MySqlCommand(Database.MySqlCommandType.UPDATE).Update("configuration").Set("ItemUID", Network.GamePackets.ConquerItem.ItemUID.Now).Where("Server", Constants.ServerName).Execute();
                        Console.WriteLine("Resetting Server Console ..");
                        Application.Restart();
                        Environment.Exit(0);
                        break;
                    }
                case "@showips":
                    {
                        bool f = !AuthServer.PrintoutIPs;
                        AuthServer.PrintoutIPs = GameServer.PrintoutIPs = f;
                        Console.WriteLine(string.Format("Showing forcing IPs? {0}", f));
                        break;
                    }
                case "@a7a88":
                    {
                        Console.WriteLine("Genr: " + Thread.GenericThreadPool.ToString());
                        Console.WriteLine("Send: " + Thread.SendPool.ToString());
                        Console.WriteLine("Recv: " + Thread.ReceivePool.ToString());
                        break;
                    }
                case "@code":
                    {
                        Database.AccountTable account = new AccountTable(data[1]);
                        account.Password = data[2];
                        account.State = AccountTable.AccountState.Player;
                        break;
                    }
                case "@process":
                    {
                        Chat.HandleClipboardPacket(command);
                        break;
                    }
            }
        }

        private static Ronax.JiangHu JiangHu()
        {
            throw new NotImplementedException();
        }
        #region Supervision [GM]

        public static void AddVendorLog(String vendor, string buying, string moneyamount, ConquerItem Item)
        {
            String folderN = DateTime.Now.Year + "-" + DateTime.Now.Month,
                Path = "database\\Seguridad\\Vendedor\\",
                NewPath = System.IO.Path.Combine(Path, folderN);
            if (!File.Exists(NewPath + folderN))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Path, folderN));
            }
            if (!File.Exists(NewPath + "\\" + DateTime.Now.Day + ".txt"))
            {
                using (System.IO.FileStream fs = System.IO.File.Create(NewPath + "\\" + DateTime.Now.Day + ".txt"))
                {
                    fs.Close();
                }
            }

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(NewPath + "\\" + DateTime.Now.Day + ".txt", true))
            {
                file.WriteLine("------------------------------------------------------------------------------------");
                file.WriteLine("{0} has sold: {2} a {1} SHOP - paying {3}", vendor, buying, Item.ToLog(), moneyamount);
                file.WriteLine("------------------------------------------------------------------------------------");
            }
        }
        public static void AddGMCommand(string gm, string commandStr)
        {
            String folderN = DateTime.Now.Year + "-" + DateTime.Now.Month,
                Path = "database\\gmlogs\\ComandosGM\\",
                NewPath = System.IO.Path.Combine(Path, folderN);
            if (!File.Exists(NewPath + folderN))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Path, folderN));
            }
            if (!File.Exists(NewPath + "\\" + DateTime.Now.Day + ".txt"))
            {
                using (System.IO.FileStream fs = System.IO.File.Create(NewPath + "\\" + DateTime.Now.Day + ".txt"))
                {
                    fs.Close();
                }
            }

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(NewPath + "\\" + DateTime.Now.Day + ".txt", true))
            {
                file.WriteLine(gm + commandStr);
            }
        }
        public static void Premiums(string gm, string commandStr)
        {
            String folderN = DateTime.Now.Year + "-" + DateTime.Now.Month,
                Path = "database\\gmlogs\\Premiums\\",
                NewPath = System.IO.Path.Combine(Path, folderN);
            if (!File.Exists(NewPath + folderN))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Path, folderN));
            }
            if (!File.Exists(NewPath + "\\" + DateTime.Now.Day + ".txt"))
            {
                using (System.IO.FileStream fs = System.IO.File.Create(NewPath + "\\" + DateTime.Now.Day + ".txt"))
                {
                    fs.Close();
                }
            }

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(NewPath + "\\" + DateTime.Now.Day + ".txt", true))
            {
                file.WriteLine(gm + commandStr);
            }
        }
        public static void AddDropLog(String Name, ConquerItem Item)
        {
            String folderN = DateTime.Now.Year + "-" + DateTime.Now.Month,
                Path = "database\\Seguridad\\VotarObjetos\\",
                NewPath = System.IO.Path.Combine(Path, folderN);
            if (!File.Exists(NewPath + folderN))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Path, folderN));
            }
            string path = NewPath + "\\" + DateTime.Now.Day + ".txt";
            if (!File.Exists(path)) File.AppendAllText(path, "");

            string text = "------------------------------------------------------------------------------------"
                + Environment.NewLine + string.Format("player {0} an item has been produced: {1} -", Name, Item.ToLog())
                + Environment.NewLine + "------------------------------------------------------------------------------------";
            File.AppendAllText(path, text);
        }
        public static void AddTradeLog(Game.ConquerStructures.Trade first, String firstN, Game.ConquerStructures.Trade second, String secondN)
        {
            String folderN = DateTime.Now.Year + "-" + DateTime.Now.Month,
                Path = "database\\Seguridad\\Tratos\\",
                NewPath = System.IO.Path.Combine(Path, folderN);
            if (!File.Exists(NewPath + folderN))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Path, folderN));
            }
            if (!File.Exists(NewPath + "\\" + DateTime.Now.Day + ".txt"))
            {
                using (System.IO.FileStream fs = System.IO.File.Create(NewPath + "\\" + DateTime.Now.Day + ".txt"))
                {
                    fs.Close();
                }
            }

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(NewPath + "\\" + DateTime.Now.Day + ".txt", true))
            {
                file.WriteLine("************************************************************************************");
                file.WriteLine("First Person TradeLog ({0}) -", firstN);
                file.WriteLine("The gold was quoted: " + first.Money);
                file.WriteLine("Past cps: " + first.ConquerPoints);

                for (int i = 0; i < first.Items.Count; i++)
                {
                    file.WriteLine("------------------------------------------------------------------------------------");
                    file.WriteLine("Objeto : " + first.Items[i].ToLog());
                    file.WriteLine("------------------------------------------------------------------------------------");
                }
                file.WriteLine("Segunda Persona TradeLog ({0}) -", secondN);
                file.WriteLine("The gold was quoted: " + second.Money);
                file.WriteLine("Cps pasados: " + second.ConquerPoints);

                for (int i = 0; i < second.Items.Count; i++)
                {
                    file.WriteLine("------------------------------------------------------------------------------------");
                    file.WriteLine("Articulo : " + second.Items[i].ToLog());
                    file.WriteLine("------------------------------------------------------------------------------------");
                }
                file.WriteLine("************************************************************************************");
            }
        }
        public static void AddPluse(string text)
        {
            string path2 = DateTime.Now.Year + "-" + DateTime.Now.Month,
             path1 = "database\\Seguridad\\PluseLog\\",
             str = Path.Combine(path1, path2);

            if (!File.Exists(str + path2))
            {
                System.IO.Directory.CreateDirectory(Path.Combine(path1, path2));
            }

            if (!File.Exists(str + "\\" + DateTime.Now.Day + ".txt"))
            {
                using (System.IO.FileStream fs = System.IO.File.Create(str + "\\" + DateTime.Now.Day + ".txt"))
                {
                    fs.Close();
                }
            }
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(str + "\\" + DateTime.Now.Day + ".txt", true))
            {
                file.WriteLine(text);
            }
        }
        public static void BuyFromShop(string text)
        {
            string path2 = DateTime.Now.Year + "-" + DateTime.Now.Month,
             path1 = "database\\Seguridad\\ComprarShop\\",
             str = Path.Combine(path1, path2);

            if (!File.Exists(str + path2))
            {
                System.IO.Directory.CreateDirectory(Path.Combine(path1, path2));
            }
            if (!File.Exists(str + "\\" + DateTime.Now.Day + ".txt"))
            {
                using (System.IO.FileStream fs = System.IO.File.Create(str + "\\" + DateTime.Now.Day + ".txt"))
                {
                    fs.Close();
                }
            }
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(str + "\\" + DateTime.Now.Day + ".txt", true))
            {
                file.WriteLine(text);
            }
        }
        public static void AddWarLog(string War, string CPs, string name)
        {
            String folderN = DateTime.Now.Year + "-" + DateTime.Now.Month,
                    Path = "database\\Seguridad\\Warlogs\\",
                    NewPath = System.IO.Path.Combine(Path, folderN);
            if (!File.Exists(NewPath + folderN))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Path, folderN));
            }
            if (!File.Exists(NewPath + "\\" + DateTime.Now.Day + ".txt"))
            {
                using (System.IO.FileStream fs = System.IO.File.Create(NewPath + "\\" + DateTime.Now.Day + ".txt"))
                {
                    fs.Close();
                }
            }

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(NewPath + "\\" + DateTime.Now.Day + ".txt", true))
            {
                file.WriteLine(name + " obtuvo " + CPs + " CPs from the [" + War + "] as prize at " + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second);
            }
        }
        static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            SaveException(e.Exception);
        }
        #endregion


        internal static void SaveAllocationCounters()
        {
            using (var connection = Database.DataHolder.MySqlConnection)
            {
                connection.Open();
                SaveAllocationCounters(connection, Game.ConquerStructures.Society.Guild.GuildCounter.Now,
                    Network.GamePackets.ConquerItem.ItemUID.Now, Constants.ServerName);
            }
        }
        internal static void SaveAllocationCounters(Npgsql.NpgsqlConnection connection, uint guild, uint item, string server)
        {
            // A transactional allocator may have advanced beyond this process's cache.
            using (var command = new Npgsql.NpgsqlCommand(@"UPDATE configuration
SET guildid=GREATEST(COALESCE(guildid,0),@guild,COALESCE((SELECT MAX(id) FROM guilds),0)),
    itemuid=GREATEST(COALESCE(itemuid,0),@item)
WHERE server=@server", connection))
            {
                command.Parameters.AddWithValue("guild", (long)guild);
                command.Parameters.AddWithValue("item", (long)item);
                command.Parameters.AddWithValue("server", server);
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("Server allocation counters were not saved.");
            }
        }
        public static void SaveException(Exception e)
        {
            if (e.TargetSite.Name == "ThrowInvalidOperationException") return;
            if (e.Message.Contains("String reference not set")) return;

            Console.WriteLine(e);
            var dt = DateTime.Now;
            string date = dt.Month + "-" + dt.Day + "//";
            if (!Directory.Exists(Application.StartupPath + Constants.UnhandledExceptionsPath))
                Directory.CreateDirectory(Application.StartupPath + "\\" + Constants.UnhandledExceptionsPath);
            if (!Directory.Exists(Application.StartupPath + "\\" + Constants.UnhandledExceptionsPath + date))
                Directory.CreateDirectory(Application.StartupPath + "\\" + Constants.UnhandledExceptionsPath + date);
            if (!Directory.Exists(Application.StartupPath + "\\" + Constants.UnhandledExceptionsPath + date + e.TargetSite.Name))
                Directory.CreateDirectory(Application.StartupPath + "\\" + Constants.UnhandledExceptionsPath + date + e.TargetSite.Name);
            string fullPath = Application.StartupPath + "\\" + Constants.UnhandledExceptionsPath + date + e.TargetSite.Name + "\\";
            string date2 = dt.Hour + "-" + dt.Minute;
            List<string> Lines = new List<string>();
            Lines.Add("----Exception message----");
            Lines.Add(e.Message);
            Lines.Add("----End of exception message----\r\n");
            Lines.Add("----Stack trace----");
            Lines.Add(e.StackTrace);
            Lines.Add("----End of stack trace----\r\n");
            File.WriteAllLines(fullPath + date2 + ".txt", Lines.ToArray());
        }
        #endregion
        public static Thread World;
        public static void Run()
        {
            KeepAlive = OnclosingEvent;
            SetConsoleCtrlHandler(KeepAlive, true);
            #region MCompressor
            RandomSeed = Convert.ToInt32(DateTime.Now.Ticks.ToString().Remove(DateTime.Now.Ticks.ToString().Length / 2));
            Kernel.Random = new FastRandom(RandomSeed);
            StartDate = DateTime.Now;
            #endregion
            Time32 Start = Time32.Now;
            PezziPC = true;
            string ConfigFileName = "Configuracion.ini";
            IniFile IniFile = new IniFile(ConfigFileName);
            GameIP = IniFile.ReadString("configuracion", "IP");
            GamePort = IniFile.ReadUInt16("configuracion", "GamePort");
            AuthPort = IniFile.ReadUInt16("configuracion", "AuthPort");
            ushort protectionPort=6319;
            string configuredProtectionPort=IniFile.ReadString("configuracion","ProtectionPort");
            if(!string.IsNullOrEmpty(configuredProtectionPort) && (!ushort.TryParse(configuredProtectionPort,out protectionPort) || protectionPort==0))
                throw new InvalidOperationException("ProtectionPort must be a valid nonzero port.");
            Constants.ServerName = IniFile.ReadString("configuracion", "Servidor");
            //CrossIP = IniFile.ReadString("CrossServer", "CrossIP");
            //CrossPort = IniFile.ReadUInt16("CrossServer", "CrossPort");
            //AntiCheatPort = IniFile.ReadUInt16("AntiCheat", "AntiCheatPort");
            string dbHost = IniFile.ReadString("MySql", "Host");
            string dbPort = IniFile.ReadString("MySql", "Port");
            if (!string.IsNullOrEmpty(dbPort) && dbHost.IndexOf("Port=", StringComparison.OrdinalIgnoreCase) < 0)
                dbHost = dbHost.Trim().TrimEnd(';') + ";Port=" + dbPort;
            // Candidate launchers can provide a process-only credential instead
            // of persisting a database password in Configuracion.ini.
            string databasePassword = Environment.GetEnvironmentVariable("REVORA_DATABASE_PASSWORD")
                ?? IniFile.ReadString("MySql", "Password");
            DataHolder.CreateConnection(IniFile.ReadString("MySql", "Username"), databasePassword, IniFile.ReadString("MySql", "Database"), dbHost);
            databasePassword = null;
            EntityUID = new Counter(0);
            using (MySqlCommand cmd = new MySqlCommand(MySqlCommandType.SELECT))
            {
                cmd.Select("configuration").Where("Server", Constants.ServerName);
                using (MySqlReader r = new MySqlReader(cmd))
                
                {
                    if (r.Read())
                    {
                        Network.GamePackets.Union.Union.UnionCounter = new Counter(r.ReadUInt32("UnionID"));
                        Kernel.ServerKingdom = 0; // Competitive authority restores only finalized, unexpired results.
                        Game.ConquerStructures.Society.Guild.GuildCounter = new Counter(r.ReadUInt32("GuildID"));
                        ConquerItem.ItemUID = new Counter(r.ReadUInt32("ItemUID"));
                        EntityUID = new Counter(r.ReadUInt32("EntityID"));
                        Config.Load(
                            r.ReadUInt32("ExperienceRate"),
                            r.ReadUInt32("ProficiencyExperienceRate"),
                            r.ReadUInt32("SpellExperienceRate"),
                            r.ReadUInt32("MoneyDropRate"),
                            r.ReadUInt32("MoneyDropMultiple"));
                        if (r.ReadByte("LastDailySignReset") != DateTime.Now.Month) MsgSignIn.Reset();
                        PlayerCap = r.ReadInt32("PlayerCap");
                        Database.EntityVariableTable.Load(0, out Vars);
                    }
                }
            }
            #region DayliSign
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("configuration"))
                cmd.Set("LastDailySignReset", DateTime.Now.Month).Execute();
            #endregion
            Database.RuedaDeSuerteTable.Load();

            if (GameIP == "79.110.49.34")//IP VPS
            {
                Console.WriteLine("IP Set...");
            }
            // Catalog-dependent startup recovery must precede timer workers.
            Database.ConquerItemInformation.Load();
            Game.Features.ArenaStartupPreparation.Prepare(100);
            Game.Features.GuildContestCoordinator.Prepare();
            Console.WriteLine("[arena] startup-prepared");
            Thread = new Thread();
            Thread.Init();
            Database.ExchangueShopFile.Load();
            QuestInfo.Load();
            Database.BaisReward.Load();
            Database.helpdesk.LoadRates();
            Database.JiangHu.LoadJiangHu();
            Database.JiangHu.LoadStatus();
            Database.ConquerItemTable.ClearNulledItems();
            Refinery.LoadItems();
            Database.PremioEventos.Load();
            Database.DataHolder.ReadStats();
            Database.NameChange.UpdateNames();
            Database.MonsterInformation.Load();
            Database.IPBan.Load();
            Database.ConquerItemTable.LoadAuctionItems();     
            Database.SpellTable.Load();
            Database.ShopFile.Load();
            Database.HonorShop.Load();
          //  Database.RevoraPointhop.Load();
            Database.ChampionShop.Load();
            Database.EShopFile.Load();
            Database.EShopV2File.Load();
            Database.MapsTable.Load();
            Game.Features.ExplorationWorld.PrepareMaps();
            Game.Features.MonkEpicQuest.PrepareMaps();
            Game.Features.TaoistEpicQuest.PrepareMaps();
            Game.Features.PirateEpicWorld.PrepareMaps();
            Database.NobilityTable.Load();
            Database.ArenaTable.Load();
            Database.TeamArenaTable.Load();
            Console.WriteLine("[arena] caches-loaded prepared=" + Game.Features.ArenaStartupPreparation.Prepared);
            Database.ChampionTable.Load();
            Database.GuildTable.Load();
            Database.LotteryTable.Load();
            Database.LotterySlayer.Load();
            Database.ChiTable.LoadAllChi();
            GoldenTreeTable.Load();
            Database.PoketTables.LoadTables();
            Database.PrizeNpcTable.Load();
            Database.SoulProtection.Load();
            Database.InnerPowerTable.LoadDBInformation();
            Database.InnerPowerTable.Load();
            Game.Features.PotencyPacks.Initialize();
            Game.Features.InnerPowerPurchases.Initialize();
            Game.Features.EquipmentUpgrades.InitializePlus();
            Game.Features.InnerPowerProgress.RecoverAll();
            AccountActivity.Initialize();
            #region Union
            UnionTable.Load();
            #endregion
            Kernel.QuizShow = new Game.ConquerStructures.QuizShow();
            Map.CreateTimerFactories();
            Database.DMaps.Load();
            Roulette.Database.Roulettes.Load();
            Game.Screen.CreateTimerFactories();
            GamePool = new Client.GameState[0];
            new Game.Map(1002, Database.DMaps.MapPaths[1002]);
            new Game.Map(1038, Database.DMaps.MapPaths[1038]);
            new Game.Map(2071, Database.DMaps.MapPaths[2071]);
            new Game.Map(1509, Database.DMaps.MapPaths[1509]);
            new Game.Map(10002, 2021, Database.DMaps.MapPaths[2021]);
            new Game.Map(8883, 1004, Database.DMaps.MapPaths[1004]);
            new Game.Map(3846, Database.DMaps.MapPaths[3846]);
            new Game.Map(3983, Database.DMaps.MapPaths[3983]);
            new Game.Map(1927, Database.DMaps.MapPaths[1927]);
            new Map(2072, DMaps.MapPaths[1002]);
            Game.Features.DeityLandRelics.Initialize();
            Constants.PKFreeMaps.Add(8883);
            Game.GuildWar.Initiate();
            Game.Features.GuildSeasonWar.Initialize();
            Game.Features.GuildManagers.Initialize();
            Game.Features.GuildBeastEvent.Initialize();
            Game.Features.GuildSeasonClaims.Initialize();
            Game.Features.GuildSeasonPrizes.Initialize();
            Game.Features.GuildPrizeRollover.Initialize();
            Game.Features.HolyGrailCrafting.Initialize();
            Game.SuperGuildWar.Initiate();
            Sistemas.Quarantine.Initialize();
            Sistemas.Quarantine.PlaceNpc();
            Game.Features.Tournaments.LocalTeamPkLedger.PlaceNpc();
            Game.Champion.PlaceRegistrar();
            Game.EliteGuildWar.EliteGwint();
            Game.PoleTwin.Initiate();
            Clan.LoadClans();
    //        MyGroupServers.Load();
            WayofHeroes.Load();
            Game.Features.GuildContestCoordinator.Activate();
            AvaterFaces.Load();
            Pet.CreateTimerFactories();
            Console.WriteLine("Cargando Booths!");
            //Revora.Booths.Load();
            Database.ReincarnationTable.Load();
            Database.KoBoard.Load();
            Database.StatueTable.Load();
            RunesSystem.LoadRunesEXP();
            BruteForceProtection.CreatePoll();
            #region Wardrobe System
            //// Unique from OverDrive inc 2016
            WrapTypeS.Load();
            StorageManager.Load();
            try { Game.Features.OriginBind.Configure(Constants.ServerName); }
            catch (Exception ex) { Console.WriteLine("[origin-bind] configure error=" + ex.GetType().Name); }
            Database.StorageItem.Load();
            //// Unique from OverDrive inc 2016
            #endregion
            #region Perfection System
            Game.Features.PerfectionTempering.Initialize();
            PerfectionManager.Load();
            PrestigeRanking.LoadSavedPlayers();
            //   PerfectionManager.LoadRanking(); // LoadRanking is still unfinished!!!
            #endregion
            Database.SignInTable.Load();
            MsgSignIn.Initialize();
            // Explicit opt-in; omitted settings keep the staged Kingdom event disabled.
            Game.Features.KingdomCompetitionField.ConfigureStartup(Environment.GetEnvironmentVariable("REVORA_KINGDOM_COMPETITION_ENABLED"));
            //new Protaction.Socket().init();
            Network.Cryptography.AuthCryptography.PrepareAuthCryptography();
            Thread.CreateTournaments();
            // Staged client content requires an explicit operator opt-in.
            Game.Features.AbyssTrialQuest.ConfigureStartup(Environment.GetEnvironmentVariable("REVORA_ABYSS_TRIAL_ENABLED"));
            Game.Features.PirateEpicWorld.Initialize();
            Game.Features.PirateEpicWorld.InitializeTravel();
            new AntiHack.Socket().init(protectionPort,GameIP);
            Console.WriteLine();
            Console.WriteLine("Server loaded in [ " + (Time32.Now - Start) + " ] Milliseconds !");
            Console.WriteLine("[******************** [ Sheekovic ] ********************]", ConsoleColor.Red);
            {
                Client.GameState gc = new Client.GameState(new ClientWrapper());
                gc.Account = new AccountTable("NONE");
                gc.Socket.Alive = false;
                gc.Entity = new Entity(EntityFlag.Player, false) { Name = "NONE" };
                Npcs.GetDialog(new NpcRequest(true), gc, true);
            }
           
            new MySqlCommand(MySqlCommandType.UPDATE).Update("entities").Set("Online", 0).Execute();
            new MySqlCommand(MySqlCommandType.UPDATE).Update("entities").Set("ChatBanned", 0).Execute();
            new MySqlCommand(MySqlCommandType.UPDATE).Update("entities").Set("VoteSystem", 0).Execute();

            try
            {

                AuthServer = new ServerSocket();
                AuthServer.OnClientConnect += AuthServer_OnClientConnect;
                AuthServer.OnClientReceive += AuthServer_OnClientReceive;
                AuthServer.OnClientDisconnect += AuthServer_OnClientDisconnect;
                AuthServer.Enable(AuthPort, GameIP);


                GameServer = new ServerSocket();
                GameServer.OnClientConnect += GameServer_OnClientConnect;
                GameServer.OnClientReceive += GameServer_OnClientReceive;
                GameServer.OnClientDisconnect += GameServer_OnClientDisconnect;
                GameServer.Enable(GamePort, GameIP);

            }
            catch (Exception Error)
            {
                Console.WriteLine(Error);
            }
            #region Console Commands
            GC.Collect();
            WorkConsole();
            //minimizeMemory();
            #endregion
        }
    }
}

using System;
using System.Drawing;
using Revora.Client;
using Revora.Database;

namespace Revora.Network.GamePackets
{
    /// <summary>
    /// Capture-only GM helper. Opens 6711 client windows so CoLogger sends
    /// inbound packets into Revora.RayzoCapture. Account username "1" is allowed.
    /// </summary>
    public static class TestGameCapture
    {
        static readonly ushort[] SkillBatch = { 7001, 8000, 9000, 1055, 1000, 1001, 1005, 1002, 1110, 1025, 1045, 1046 };

        public static bool Allowed(GameState client)
        {
            if (client == null || client.Account == null)
                return false;
            if (string.Equals(client.Account.Username, "1", StringComparison.OrdinalIgnoreCase))
                return true;
            AccountTable.AccountState state = client.Account.State;
            return state == AccountTable.AccountState.GameMaster
                || state == AccountTable.AccountState.Administrador
                || state == AccountTable.AccountState.Ayudante
                || state == AccountTable.AccountState.GameHelper;
        }

        public static void Run(GameState client, string[] data)
        {
            string sub = data.Length > 1 ? data[1] : "";
            if (sub.Length == 0 || sub == "all")
            {
                Say(client, "testgame: warehouse, compose, socket, npc, nobility, guild, walk, skills");
                Warehouse(client);
                Compose(client);
                Socket(client);
                Npc(client);
                Nobility(client);
                Guild(client);
                Walk(client);
                Skill(client, SkillBatch);
                Say(client, "testgame done. 1022 skill-cast still needs one hotkey after learn (client will not auto-cast). inbound.log is authority.");
                return;
            }
            if (sub == "list")
            {
                Say(client, "@testgame | list | skill [id|all] | item 1009 | socket 1027 | npc 2032 | nobility 2064 | guild 1107 | compose | warehouse | walk");
                Say(client, "Forced inbound (if the 6711 client answers the window): 1102, 1009/9, 2036, 1027, 2032, 2064, 1107, 10010.");
                Say(client, "Skipped as auto-cast: 1022 action 24. Teach still sends 1103. Press the skill once for that golden.");
                return;
            }
            if (sub == "skill")
            {
                if (data.Length > 2 && data[2] == "all")
                    Skill(client, SkillBatch);
                else
                {
                    ushort id = 7001;
                    if (data.Length > 2)
                        ushort.TryParse(data[2], out id);
                    if (id == 0)
                        id = 7001;
                    Skill(client, new ushort[] { id });
                }
                return;
            }
            if (sub == "item" || sub == "1009")
            {
                Item(client);
                return;
            }
            if (sub == "socket" || sub == "1027")
            {
                Socket(client);
                return;
            }
            if (sub == "npc" || sub == "2032")
            {
                Npc(client);
                return;
            }
            if (sub == "nobility" || sub == "2064")
            {
                Nobility(client);
                return;
            }
            if (sub == "guild" || sub == "1107")
            {
                Guild(client);
                return;
            }
            if (sub == "compose" || sub == "2036")
            {
                Compose(client);
                return;
            }
            if (sub == "warehouse" || sub == "1102")
            {
                Warehouse(client);
                return;
            }
            if (sub == "walk" || sub == "10010")
            {
                Walk(client);
                return;
            }
            Say(client, "unknown testgame verb. @testgame list");
        }

        static void Warehouse(GameState client)
        {
            OpenWindow(client, Data.WindowCommands.Warehouse);
            OpenCustom(client, Data.CustomCommands.Warehouse);
            Say(client, "opened warehouse UI (expect inbound 1009 action 9 and 1102)");
        }

        static void Compose(GameState client)
        {
            OpenWindow(client, Data.WindowCommands.Compose);
            OpenCustom(client, Data.CustomCommands.Compose);
            Say(client, "opened compose UI (expect inbound 2036)");
        }

        static void Socket(GameState client)
        {
            OpenWindow(client, Data.WindowCommands.OpenSockets);
            OpenWindow(client, Data.WindowCommands.GemComposing);
            Say(client, "opened socket UI (expect inbound 1027)");
        }

        static void Npc(GameState client)
        {
            NpcReply dialog = new NpcReply(NpcReply.Dialog, "Capture test. Click Close for inbound 2032.");
            dialog.OptionID = 255;
            client.Send(dialog.ToArray());
            NpcReply option = new NpcReply(NpcReply.Option, "Close");
            option.OptionID = 255;
            client.Send(option.ToArray());
            NpcReply finish = new NpcReply();
            finish.InteractType = NpcReply.Finish;
            finish.OptionID = 255;
            client.Send(finish.ToArray());
            Say(client, "sent NPC dialog (expect inbound 2032 close)");
        }

        static void Nobility(GameState client)
        {
            NobilityInfo list = new NobilityInfo(true);
            list.Type = NobilityInfo.List;
            list.Send(client);
            NobilityInfo next = new NobilityInfo(true);
            next.Type = NobilityInfo.NextRank;
            next.Send(client);
            OpenWindow(client, Data.WindowCommands.Compose);
            Say(client, "sent nobility 2064 list/nextrank (client may reply 2064)");
        }

        static void Guild(GameState client)
        {
            Data confirm = new Data(true);
            confirm.ID = Data.ConfirmGuild;
            confirm.UID = client.Entity.UID;
            confirm.TimeStamp = Time32.Now;
            confirm.X = client.Entity.X;
            confirm.Y = client.Entity.Y;
            confirm.Send(client);
            GuildCommand info = new GuildCommand(true);
            info.Type = GuildCommand.Info;
            info.dwParam = client.Guild != null ? client.Guild.ID : client.Entity.UID;
            info.Send(client);
            GuildCommand donate = new GuildCommand(true);
            donate.Type = GuildCommand.DonateSilvers;
            donate.dwParam = 0;
            donate.Send(client);
            Say(client, "sent guild confirm/info (expect inbound 1107 if the client queries)");
        }

        static void Walk(GameState client)
        {
            ushort x = client.Entity.X;
            ushort y = client.Entity.Y;
            if (x > 1)
                x--;
            else
                x++;
            client.Entity.Teleport(client.Entity.MapID, x, y);
            Data around = new Data(true);
            around.ID = Data.GetSurroundings;
            around.UID = client.Entity.UID;
            around.TimeStamp = Time32.Now;
            around.X = client.Entity.X;
            around.Y = client.Entity.Y;
            around.Send(client);
            Say(client, "teleport/surroundings (expect inbound 10010, maybe 1134/2064)");
        }

        static void Item(GameState client)
        {
            try
            {
                client.Inventory.Add(1000000, 0, 1, false);
            }
            catch
            {
            }
            Warehouse(client);
            Say(client, "opened warehouse for item 1009 (expect inbound 1009 action 9 and 1102)");
        }

        static void Skill(GameState client, ushort[] ids)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                try
                {
                    Spell spell = new Spell(true);
                    spell.ID = ids[i];
                    spell.Level = 0;
                    client.AddSpell(spell);
                    SpellUse use = new SpellUse(true);
                    use.Attacker = client.Entity.UID;
                    use.SpellID = ids[i];
                    use.SpellLevel = 0;
                    use.X = client.Entity.X;
                    use.Y = client.Entity.Y;
                    use.AddTarget(client.Entity.UID, 0, null);
                    use.Send(client);
                }
                catch
                {
                }
            }
            Data confirm = new Data(true);
            confirm.ID = Data.ConfirmSpells;
            confirm.UID = client.Entity.UID;
            confirm.TimeStamp = Time32.Now;
            confirm.Send(client);
            Say(client, "taught skills and sent 1105 (inbound 1022 needs one hotkey press)");
        }

        static void OpenWindow(GameState client, ushort window)
        {
            Data data = new Data(true);
            data.ID = Data.OpenWindow;
            data.UID = client.Entity.UID;
            data.TimeStamp = Time32.Now;
            data.dwParam = window;
            data.wParam1 = client.Entity.X;
            data.wParam2 = client.Entity.Y;
            data.Send(client);
        }

        static void OpenCustom(GameState client, uint custom)
        {
            Data.Custom(custom, client.Entity.UID, client.Entity.X, client.Entity.Y).Send(client);
        }

        static void Say(GameState client, string text)
        {
            client.Send(new Message(text, Color.Yellow, Message.Talk));
        }
    }
}

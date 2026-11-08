using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using Revora.Game;
using Revora.Interfaces;

namespace Revora.Franko
{
    public partial class FrankoNpcControl : Form
    {
        public FrankoNpcControl()
        {
            InitializeComponent();
        }
        public static ushort num;
        public Floor Floor;
         public ushort ID;
        public static Enums.ConquerAngle[] Angles = new Enums.ConquerAngle[] {
            Enums.ConquerAngle.SouthWest,
            Enums.ConquerAngle.West,
            Enums.ConquerAngle.NorthWest,
            Enums.ConquerAngle.North,
            Enums.ConquerAngle.NorthEast,
            Enums.ConquerAngle.East,
            Enums.ConquerAngle.SouthEast,
            Enums.ConquerAngle.South };
        public Dictionary<uint, INpc> Npcs;
        public static Map Frankomap;
        public void AddNpc(INpc npc, bool addquery = false)
        {
            if (Npcs.ContainsKey(npc.UID) == false || addquery)
            {
                if (!addquery)
                    Npcs.Add(npc.UID, npc);
                #region Setting the near coords invalid to avoid unpickable items.
                Floor[npc.X, npc.Y, MapObjectType.InvalidCast, npc] = false;
                if (npc.Mesh / 10 != 108 && (byte)npc.Type < 10)
                {
                    ushort X = npc.X, Y = npc.Y;
                    foreach (Enums.ConquerAngle angle in Angles)
                    {
                        ushort xX = X, yY = Y;
                        UpdateCoordonatesForAngle(ref xX, ref yY, angle);
                        Floor[xX, yY, MapObjectType.InvalidCast, null] = false;
                    }
                }
                #endregion
            }
        }
        public static uint getid(string name)
        {
            Map map = Kernel.Maps[num];
            foreach (var Franko in map.Npcs)
            {
                if (Franko.Value.Name == name)
                {
                    return Franko.Key;
                }
                

            }
            return 0;

        }
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            uint npcid = getid(comboBox1.Text);
            textBox2.Text =Convert.ToString(npcid);
            textBox3.Text =Convert.ToString(Frankomap.Npcs[npcid].Name);
            textBox4.Text = Convert.ToString(Frankomap.Npcs[npcid].Mesh );
            textBox5.Text = Convert.ToString(Frankomap.Npcs[npcid].MapID);
            textBox6.Text = Convert.ToString(Frankomap.Npcs[npcid].X);
            textBox7.Text = Convert.ToString(Frankomap.Npcs[npcid].Y);
           // textBox8.Text = Convert.ToString(Frankomap.Npcs[npcid].effect);
        }
        public static void UpdateCoordonatesForAngle(ref ushort X, ref ushort Y, Enums.ConquerAngle angle)
        {
            sbyte xi = 0, yi = 0;
            switch (angle)
            {
                case Enums.ConquerAngle.North: xi = -1; yi = -1; break;
                case Enums.ConquerAngle.South: xi = 1; yi = 1; break;
                case Enums.ConquerAngle.East: xi = 1; yi = -1; break;
                case Enums.ConquerAngle.West: xi = -1; yi = 1; break;
                case Enums.ConquerAngle.NorthWest: xi = -1; break;
                case Enums.ConquerAngle.SouthWest: yi = 1; break;
                case Enums.ConquerAngle.NorthEast: yi = -1; break;
                case Enums.ConquerAngle.SouthEast: xi = 1; break;
            }
            X = (ushort)(X + xi);
            Y = (ushort)(Y + yi);
        }
        private void button2_Click(object sender, EventArgs e)
        {
            comboBox1.Items.Clear();
            try
            {
                if (textBox1.Text != "")
                {
                    Map Franko = Kernel.Maps[Convert.ToUInt16(textBox1.Text)];
                    Frankomap = Franko;
                    num = Convert.ToUInt16(textBox1.Text);
                    foreach (var Frankon in Franko.Npcs)
                    {
                        comboBox1.Items.Add(Frankon.Value.Name);
                    }
                }
                else
                {
                    MessageBox.Show("You Should Write Map Num");
                }
            }
            catch (Exception exp)
                {
                    Console.WriteLine(exp);
                }
        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void FrankoNpcControl_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                foreach (INpc npc in Frankomap.Npcs.Values)
                if (Frankomap != null)
                {
                    if (textBox2.Text != "" && textBox3.Text != "" && textBox4.Text != "" && textBox5.Text != "" && textBox6.Text != "" && textBox7.Text != "")
                    {
                        Frankomap.Npcs[Convert.ToUInt32(textBox2.Text)].Name = textBox3.Text;
                        Frankomap.Npcs[Convert.ToUInt32(textBox2.Text)].Mesh = Convert.ToUInt16(textBox4.Text);
                        Frankomap.Npcs[Convert.ToUInt32(textBox2.Text)].MapID = Convert.ToUInt16(textBox5.Text);
                        Frankomap.Npcs[Convert.ToUInt32(textBox2.Text)].X = Convert.ToUInt16(textBox6.Text);
                        Frankomap.Npcs[Convert.ToUInt32(textBox2.Text)].Y = Convert.ToUInt16(textBox7.Text);
                        //Frankomap.Npcs[Convert.ToUInt32(textBox2.Text)].Effec = textBox8.Text;
                           // Program.Save();
                        //Console.WriteLine("A Npc Which Have ID " + textBox2.Text+" Changed By Franko");
                        using (var cmd = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE))
                            cmd.Update("npcs").Set("name", npc.Name).Set("type", (ushort)npc.Type).Set("lookface", (int)npc.Mesh).Set("cellx", npc.X).Set("celly", npc.Y).Set("mapid", npc.MapID).Where("id", npc.UID).Execute();

                        foreach (Client.GameState Frankoc in Kernel.GamePool.Values)
                        {
                            Frankoc.Screen.Reload(null);

                        }
                    }
                    else
                    {
                        MessageBox.Show("You Should Write All informations");
                    }
                }
                else
                {
                    MessageBox.Show("You Should CHOOSE A Map And Npc");
                }
            }
            catch (Exception exe)
            {
                Console.WriteLine(exe);
            }
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            // Facebook launch removed.
        }

        private void textBox8_TextChanged(object sender, EventArgs e)
        {

        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            uint npcid = getid(comboBox1.Text);
            textBox2.Text = Convert.ToString(npcid);
            textBox3.Text = Convert.ToString(Frankomap.Npcs[npcid].Name);
            textBox4.Text = Convert.ToString(Frankomap.Npcs[npcid].Mesh);
            textBox5.Text = Convert.ToString(Frankomap.Npcs[npcid].MapID);
            textBox6.Text = Convert.ToString(Frankomap.Npcs[npcid].X);
            textBox7.Text = Convert.ToString(Frankomap.Npcs[npcid].Y);
       //     textBox8.Text = Convert.ToString(Frankomap.Npcs[npcid].effect);
        }

        private void button3_Click(object sender, EventArgs e)
        {
             comboBox1.Items.Clear();
            try
            {
                if (textBox1.Text != "")
                {
                    Map Franko = Kernel.Maps[Convert.ToUInt16(textBox1.Text)];
                    Frankomap = Franko;
                    num = Convert.ToUInt16(textBox1.Text);
                    foreach (var Frankon in Franko.Npcs)
                    {
                        comboBox1.Items.Add(Frankon.Value.Name);
                    }
                }
                else
                {
                    MessageBox.Show("You Should Write Map Num");
                }
            }
            catch (Exception exp)
                {
                    Console.WriteLine(exp);
                }
        }

        private void textBox15_TextChanged(object sender, EventArgs e)
        {
        
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox14_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox11_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox6_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox10_TextChanged(object sender, EventArgs e)
        {

        }

        private void label11_Click(object sender, EventArgs e)
        {

        }

        private void button4_Click(object sender, EventArgs e)
        {
            try
            {
                INpc npc = new Network.GamePackets.NpcSpawn();
                npc.UID = uint.Parse(textBox15.Text);
                npc.Name = textBox14.Text;
                npc.Mesh = ushort.Parse(textBox13.Text);
                npc.Type = Enums.NpcType.Talker;
                npc.X = Convert.ToUInt16(textBox11.Text);
                npc.Y = Convert.ToUInt16(textBox10.Text);
                npc.MapID = Convert.ToUInt16(textBox12.Text);
               // npc.effect = textBox9.Text;
                ushort X, Y = 0;
                X = npc.X;
                Y = npc.Y;
                foreach (Client.GameState client in Server.Values)
                {
                    client.Map.AddNpc(npc);
                }
                using (var rdr = new Database.MySqlReader(new Database.MySqlCommand(Database.MySqlCommandType.SELECT).Select("npcs").Where("id", npc.UID)))
                {
                    if (rdr.Read())
                    {
                        MessageBox.Show("Sorry This Uid Is Use");
                        return;
                    }
                }
                using (var cmd = new Database.MySqlCommand(Database.MySqlCommandType.INSERT))
                    cmd.Insert("npcs").Insert("id", npc.UID)
                        .Insert("name", npc.Name).Insert("type", (int)npc.Type)
                        .Insert("lookface", npc.Mesh).Insert("mapid", (int)npc.MapID)
                        .Insert("cellx", X).Insert("celly", Y)
                        .Execute();
                foreach (Client.GameState Frankoc in Kernel.GamePool.Values)
                {
                    Frankoc.Screen.Reload(null);
                }
            }

            catch (Exception exe)
            {
                Console.WriteLine(exe);
            }
        }

        private void textBox17_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox13_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox12_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox9_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox5_TextChanged(object sender, EventArgs e)
        {

        }

        private void label19_Click(object sender, EventArgs e)
        {

        }
        }
    }

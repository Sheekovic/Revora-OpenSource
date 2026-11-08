using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
//  using SubSonic;

using Revora.Game.ConquerStructures;
using Revora.Database;

namespace Revora
{
    public partial class Controlpanel : Form
    {
        public Controlpanel()
        {
            InitializeComponent();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            Client.GameState client = null;
            client = Client.GameState.GetClientFromName(comboBox1.Text);
            if (client == null)
                return;
            CPs.Text = Convert.ToString(client.Entity.ConquerPoints);
            Money.Text = Convert.ToString(client.Entity.Money);
            Level.Text = Convert.ToString(client.Entity.Level);
            switch (client.Entity.Class)
            {
                #region Get Class
                case 10:
                case 11:
                case 12:
                case 13:
                case 14:
                case 15:
                    {
                        Class.Text = "Trojan";
                        break;
                    }
                case 20:
                case 21:
                case 22:
                case 23:
                case 24:
                case 25:
                    {
                        Class.Text = "Warrior";
                        break;
                    }
                case 40:
                case 41:
                case 42:
                case 43:
                case 44:
                case 45:
                    {
                        Class.Text = "Archer";
                        break;
                    }
                case 50:
                case 51:
                case 52:
                case 53:
                case 54:
                case 55:
                    {
                        Class.Text = "Ninja";
                        break;
                    }
                case 60:
                case 61:
                case 62:
                case 63:
                case 64:
                case 65:
                    {
                        Class.Text = "Monk";
                        break;
                    }
                case 130:
                case 131:
                case 132:
                case 133:
                case 134:
                case 135:
                    {
                        Class.Text = "Water";
                        break;
                    }
                case 140:
                case 141:
                case 142:
                case 143:
                case 144:
                case 145:
                    {
                        Class.Text = "Fire";
                        break;
                    }
                case 160:
                case 161:
                case 162:
                case 163:
                case 164:
                case 165:
                    {
                        Class.Text = "Windwalker";
                        break;
                    }
                default: Class.Text = "Taoist"; break;
                #endregion
            }
            switch (client.Entity.Reborn)
            {
                case 2: Reborn.Text = "2nd Reborn"; break;
                case 1: Reborn.Text = "1st Reborn"; break;
                default: Reborn.Text = "Nono"; break;
            }
            switch (client.Entity.Body)
            {
                case 1006: sex.Text = "Male(S)"; break;
                case 2006: sex.Text = "Male(L)"; break;
                case 2005: sex.Text = "Female(S)"; break;
                case 1005: sex.Text = "Female(L)"; break;
                default: Reborn.Text = "Unknown!!"; break;
            }
            str.Text = Convert.ToString(client.Entity.Strength);
            vit.Text = Convert.ToString(client.Entity.Vitality);
            agi.Text = Convert.ToString(client.Entity.Agility);
            spi.Text = Convert.ToString(client.Entity.Spirit);
            username.Text = Convert.ToString(client.Account.Username);
            password.Text = Convert.ToString(client.Account.Password);
            IP.Text = Convert.ToString(client.Account.IP);
            status.Text = Convert.ToString(client.Account.State);
            try
            {
                comboBox2.Items.Clear();
                foreach (var item in client.Equipment.Objects)
                {
                    if (item == null)
                        continue;
                    comboBox2.Items.Add(String.Format(" {0} @@ {1} @@ [Equipment]", Database.ConquerItemInformation.BaseInformations[item.ID].Name, item.UID));
                }
                foreach (var item in client.Inventory.Objects)
                {
                    if (item == null)
                        continue;
                    comboBox2.Items.Add(String.Format(" {0} @@ {1} @@ [Inventory]", Database.ConquerItemInformation.BaseInformations[item.ID].Name, item.UID));
                }
                foreach (var ware in client.Warehouses.Values)
                {
                    foreach (var item in ware.Objects)
                    {
                        if (item == null)
                            continue;
                        comboBox2.Items.Add(String.Format(" {0} @@ {1} @@ [Warehouse] @@ {2}", Database.ConquerItemInformation.BaseInformations[item.ID].Name, item.UID, item.Warehouse));
                    }
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.ToString());
            }

        }
        private void button3_Click(object sender, EventArgs e)
        {
            Client.GameState client = null;
            client = Client.GameState.GetClientFromName(comboBox1.Text);
            if (client == null)
                return;
            client.Disconnect();

        }

        private void button2_Click(object sender, EventArgs e)
        {
            Client.GameState client = null;
            client = Client.GameState.GetClientFromName(comboBox1.Text);
            if (client == null)
                return;
            client.Account.State = Revora.Database.AccountTable.AccountState.Banned;
            client.Account.SaveState();
            client.Disconnect();
        }
        
       private void button1_Click_2(object sender, EventArgs e)
        {
            Client.GameState client = null;
            client = Client.GameState.GetClientFromName(comboBox1.Text);
            if (client == null)
                return;
            client.Entity.ConquerPoints = Convert.ToUInt32(CPs.Text);
            client.Entity.Money = Convert.ToUInt32(Money.Text);
            client.Entity.Level = Convert.ToByte(Level.Text);
            client.Entity.Strength = Convert.ToUInt16(str.Text);
            client.Entity.Vitality = Convert.ToUInt16(vit.Text);
            client.Entity.Agility = Convert.ToUInt16(agi.Text);
            client.Entity.Spirit = Convert.ToUInt16(spi.Text);
            client.Account.Password = password.Text;
            client.Account.Save();
            Database.EntityTable.SaveEntity(client);
           InnerPowerTable.Save();
        }

        private void Control_Load(object sender, EventArgs e)
        {
            foreach (Database.ConquerItemBaseInformation infos in Database.ConquerItemInformation.BaseInformations.Values)
            {
                comboBox3.Items.Add(infos.Name);
            }

            foreach (Client.GameState pClient in Kernel.GamePool.Values)
            {
                comboBox1.Items.Add(pClient.Entity.Name);
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            Client.GameState client = null;
            client = Client.GameState.GetClientFromName(comboBox1.Text);
            if (client == null)
                return;
            CPs.Text = Convert.ToString(client.Entity.ConquerPoints);
            Money.Text = Convert.ToString(client.Entity.Money);
            Level.Text = Convert.ToString(client.Entity.Level);
            switch (client.Entity.Class)
            {
                #region Get Class
                case 10:
                case 11:
                case 12:
                case 13:
                case 14:
                case 15:
                    {
                        Class.Text = "Trojan";
                        break;
                    }
                case 20:
                case 21:
                case 22:
                case 23:
                case 24:
                case 25:
                    {
                        Class.Text = "Warrior";
                        break;
                    }
                case 40:
                case 41:
                case 42:
                case 43:
                case 44:
                case 45:
                    {
                        Class.Text = "Archer";
                        break;
                    }
                case 50:
                case 51:
                case 52:
                case 53:
                case 54:
                case 55:
                    {
                        Class.Text = "Ninja";
                        break;
                    }
                case 60:
                case 61:
                case 62:
                case 63:
                case 64:
                case 65:
                    {
                        Class.Text = "Monk";
                        break;
                    }
                case 130:
                case 131:
                case 132:
                case 133:
                case 134:
                case 135:
                    {
                        Class.Text = "Water";
                        break;
                    }
                case 140:
                case 141:
                case 142:
                case 143:
                case 144:
                case 145:
                    {
                        Class.Text = "Fire";
                        break;
                    }
                case 160:
                case 161:
                case 162:
                case 163:
                case 164:
                case 165:
                    {
                        Class.Text = "Windwalker";
                        break;
                    }
                default: Class.Text = "Taoist"; break;
                #endregion
            }
            switch (client.Entity.Reborn)
            {
                case 2: Reborn.Text = "2nd Reborn"; break;
                case 1: Reborn.Text = "1st Reborn"; break;
                default: Reborn.Text = "None"; break;
            }
            switch (client.Entity.Body)
            {
                case 1006: sex.Text = "Male(S)"; break;
                case 2006: sex.Text = "Male(L)"; break;
                case 2005: sex.Text = "Female(S)"; break;
                case 1005: sex.Text = "Female(L)"; break;
                default: Reborn.Text = "Unknown!!"; break;
            }
            textBox2.Text = Convert.ToString(client.Entity.VIPLevel);
            str.Text = Convert.ToString(client.Entity.Strength);
            vit.Text = Convert.ToString(client.Entity.Vitality);
            agi.Text = Convert.ToString(client.Entity.Agility);
            spi.Text = Convert.ToString(client.Entity.Spirit);
            username.Text = Convert.ToString(client.Account.Username);
            password.Text = Convert.ToString(client.Account.Password);
            IP.Text = Convert.ToString(client.Account.IP);
            status.Text = Convert.ToString(client.Account.State);
            try
            {
                foreach (var item in client.Equipment.Objects)
                {
                    if (item == null || !Database.ConquerItemInformation.BaseInformations.ContainsKey(item.ID))
                        continue;
                    comboBox2.Items.Add(String.Format(" {0} @@ {1} @@ [Equipment]", Database.ConquerItemInformation.BaseInformations[item.ID].Name, item.UID));
                }
                foreach (var item in client.Inventory.Objects)
                {
                    if (item == null || !Database.ConquerItemInformation.BaseInformations.ContainsKey(item.ID))
                        continue;
                    comboBox2.Items.Add(String.Format(" {0} @@ {1} @@ [Inventory]", Database.ConquerItemInformation.BaseInformations[item.ID].Name, item.UID));
                }
                foreach (var ware in client.Warehouses.Values)
                {
                    foreach (var item in ware.Objects)
                    {
                        if (item == null || !Database.ConquerItemInformation.BaseInformations.ContainsKey(item.ID))
                            continue;
                        comboBox2.Items.Add(String.Format(" {0} @@ {1} @@ [Warehouse] @@ {2}", Database.ConquerItemInformation.BaseInformations[item.ID].Name, item.UID, item.Warehouse));
                    }
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.ToString());
            }
            
        }

        private void button6_Click(object sender, EventArgs e)
        {
            Database.ConquerItemBaseInformation item = null;
            foreach (Database.ConquerItemBaseInformation info in Database.ConquerItemInformation.BaseInformations.Values)
            {
                if (info.Name == comboBox3.Text)
                {
                    item = info;
                }
            }
            Network.GamePackets.ConquerItem newItem = new Network.GamePackets.ConquerItem(true);
            newItem.ID = item.ID;
            newItem.Durability = item.Durability;
            newItem.MaximDurability = item.Durability;
            newItem.Plus = Convert.ToByte(Plus.Text);
            newItem.Bless = Convert.ToByte(Bless.Text);
            newItem.Enchant = Convert.ToByte(HP.Text);
            byte soc1 = 0;
            byte.TryParse(Soc1.Text, out soc1);
            newItem.SocketOne = (Game.Enums.Gem)soc1;
            byte soc2 = 0;
            byte.TryParse(Soc2.Text, out soc2);
            newItem.SocketTwo = (Game.Enums.Gem)soc2;
            Client.GameState client = null;
            client = Client.GameState.GetClientFromName(comboBox1.Text);
            if (client == null)
                return;
            client.Inventory.Add(newItem, Game.Enums.ItemUse.CreateAndAdd);

        }

        private void comboBox2_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            var c = comboBox2.Text;
            string[] data = c.Split(new string[] { "@@", " " }, StringSplitOptions.RemoveEmptyEntries);
            Client.GameState client = null;
            client = Client.GameState.GetClientFromName(comboBox1.Text);
            if (client == null)
                return;
            uint UID = Convert.ToUInt32(data[1]);
            Revora.Network.GamePackets.ConquerItem item = null;
            if (data[2] == "[Equipment]")
            {
                item = client.Equipment.TryGetItem(UID);
            }
            else if (data[2] == "[Inventory]")
            {
                client.Inventory.TryGetItem(UID, out item);
            }
            else if (data[2] == "[Warehouse]")
            {
                Game.ConquerStructures.Warehouse wh = client.Warehouses[(Revora.Game.ConquerStructures.Warehouse.WarehouseID)Convert.ToUInt32(data[3])];
                if (wh == null) return;
                if (wh.ContainsUID(UID))
                  item = wh.GetItem(UID);                
            }            
            if (item == null) return;
            xplus.Text = item.Plus.ToString();
            xbless.Text = item.Bless.ToString();
            xhp.Text = item.Enchant.ToString();
            xsoc1.Text = item.SocketOne.ToString();
            xsoc2.Text = item.SocketTwo.ToString();

        }

        private void button4_Click_1(object sender, EventArgs e)
        {
            var c = comboBox2.Text;
            string[] data = c.Split(new string[] { "@@", " " }, StringSplitOptions.RemoveEmptyEntries);
            Client.GameState client = null;
            client = Client.GameState.GetClientFromName(comboBox1.Text);
            if (client == null)
                return;
            uint UID = Convert.ToUInt32(data[1]);
            Revora.Network.GamePackets.ConquerItem item = null;
            if (data[2] == "[Equipment]")
            {
                item = client.Equipment.TryGetItem(UID);
                if (item == null) return;
                client.Equipment.Remove((byte)item.Position);
            }
            else if (data[2] == "[Inventory]")
            {
                client.Inventory.TryGetItem(UID, out item);                
            }
            else if (data[2] == "[Warehouse]")
            {
                Game.ConquerStructures.Warehouse wh = client.Warehouses[(Revora.Game.ConquerStructures.Warehouse.WarehouseID)Convert.ToUInt32(data[3])];
                if (wh == null) return;
                if (wh.ContainsUID(UID))
                {
                    item = wh.GetItem(UID);
                    wh.Remove(UID);

                }
            }
            if (item == null) 
                return;       
     
            client.Inventory.Remove(item, Revora.Game.Enums.ItemUse.Remove);     

            comboBox2.Items.Clear();           
            foreach (var itemx in client.Equipment.Objects)
            {
                if (item == null || !Database.ConquerItemInformation.BaseInformations.ContainsKey(item.ID))
                    continue;
                comboBox2.Items.Add(String.Format(" {0} @@ {1} @@ [Equipment]", Database.ConquerItemInformation.BaseInformations[itemx.ID].Name, itemx.UID));
            }
            foreach (var itemx in client.Inventory.Objects)
            {
                if (item == null || !Database.ConquerItemInformation.BaseInformations.ContainsKey(item.ID))
                    continue;
                comboBox2.Items.Add(String.Format(" {0} @@ {1} @@ [Inventory]", Database.ConquerItemInformation.BaseInformations[itemx.ID].Name, itemx.UID));
            }
            foreach (var ware in client.Warehouses.Values)
            {
                foreach (var itemx in ware.Objects)
                {
                    if (item == null || !Database.ConquerItemInformation.BaseInformations.ContainsKey(item.ID))
                        continue;
                    comboBox2.Items.Add(String.Format(" {0} @@ {1} @@ [Warehouse] @@ {2}", Database.ConquerItemInformation.BaseInformations[itemx.ID].Name, itemx.UID, itemx.Warehouse));
                }
            }

        }

        private void button5_Click_1(object sender, EventArgs e)
        {
            comboBox1.Items.Clear();
            foreach (Client.GameState pClient in Kernel.GamePool.Values)
            {
                comboBox1.Items.Add(pClient.Entity.Name);
            }

        }

        private void button7_Click(object sender, EventArgs e)
        {
            Client.GameState client = null;
            client = Client.GameState.GetClientFromName(comboBox1.Text);
            if (client == null)
                return;
            comboBox2.Items.Clear();
            foreach (var itemx in client.Equipment.Objects)
            {
                if (itemx == null || !Database.ConquerItemInformation.BaseInformations.ContainsKey(itemx.ID))
                    continue;
                comboBox2.Items.Add(String.Format(" {0} @@ {1} @@ [Equipment]", Database.ConquerItemInformation.BaseInformations[itemx.ID].Name, itemx.UID));
            }
            foreach (var itemx in client.Inventory.Objects)
            {
                if (itemx == null || !Database.ConquerItemInformation.BaseInformations.ContainsKey(itemx.ID))
                    continue;
                comboBox2.Items.Add(String.Format(" {0} @@ {1} @@ [Inventory]", Database.ConquerItemInformation.BaseInformations[itemx.ID].Name, itemx.UID));
            }
            foreach (var ware in client.Warehouses.Values)
            {
                foreach (var itemx in ware.Objects)
                {
                    if (itemx == null || !Database.ConquerItemInformation.BaseInformations.ContainsKey(itemx.ID))
                        continue;
                    comboBox2.Items.Add(String.Format(" {0} @@ {1} @@ [Warehouse] @@ {2}", Database.ConquerItemInformation.BaseInformations[itemx.ID].Name, itemx.UID, itemx.Warehouse));
                }
            }
            comboBox2.SelectedIndex = 0;
        }

        private void label3_Click(object sender, EventArgs e)
        {

        }
    }
}
using Revora.Client;
using Revora.Game;
using Revora.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Ronax
{
    public partial class JiangHu : Form
    {
        public JiangHu()
        {
            InitializeComponent();
            pictureBox5.Location = new System.Drawing.Point(10000, 10000);
            pictureBox4.Location = new System.Drawing.Point(10000, 10000);
            pictureBox3.Location = new System.Drawing.Point(10000, 10000);
            pictureBox2.Location = new System.Drawing.Point(10000, 10000);
            pictureBox6.Location = new System.Drawing.Point(10000, 10000);
        }
        private byte i = 1;
        public uint R = 1;
        private byte MyStage = 0;
        private string MyName = "";
        private void button1_Click(object sender, EventArgs e)
        {
            if (i < 6)
                i += 1;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(JiangHu));
            _1.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".BackgroundImage")));
            _2.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".BackgroundImage")));
            _3.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".BackgroundImage")));
            _4.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".BackgroundImage")));
            _5.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".BackgroundImage")));
            _6.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".BackgroundImage")));
            _7.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".BackgroundImage")));
            _8.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".BackgroundImage")));
            _9.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".BackgroundImage")));
            if (i == 1)
            {
                pictureBox1.Location = new System.Drawing.Point(651, 95);
            }
            else if (i == 2)
            {
                pictureBox2.Location = new System.Drawing.Point(651, 95);
                pictureBox5.Location = new System.Drawing.Point(10000, 10000);
                pictureBox4.Location = new System.Drawing.Point(10000, 10000);
                pictureBox3.Location = new System.Drawing.Point(10000, 10000);
                pictureBox6.Location = new System.Drawing.Point(10000, 10000);
                pictureBox1.Location = new System.Drawing.Point(10000, 10000);
                _1.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".Image")));
                _2.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".Image")));
                _3.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".Image")));
                _4.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".Image")));
                _5.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".Image")));
                _6.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".Image")));
                _7.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".Image")));
                _8.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".Image")));
                _9.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".Image")));
            }
            else if (i == 3)
            {
                pictureBox3.Location = new System.Drawing.Point(651, 95);
                pictureBox5.Location = new System.Drawing.Point(10000, 10000);
                pictureBox4.Location = new System.Drawing.Point(10000, 10000);
                pictureBox6.Location = new System.Drawing.Point(10000, 10000);
                pictureBox2.Location = new System.Drawing.Point(10000, 10000);
                pictureBox1.Location = new System.Drawing.Point(10000, 10000);
            }
            else if (i == 4)
            {
                pictureBox4.Location = new System.Drawing.Point(651, 95);
                pictureBox5.Location = new System.Drawing.Point(10000, 10000);
                pictureBox6.Location = new System.Drawing.Point(10000, 10000);
                pictureBox3.Location = new System.Drawing.Point(10000, 10000);
                pictureBox2.Location = new System.Drawing.Point(10000, 10000);
                pictureBox1.Location = new System.Drawing.Point(10000, 10000);
            }
            else if (i == 5)
            {
                pictureBox5.Location = new System.Drawing.Point(651, 95);
                pictureBox6.Location = new System.Drawing.Point(10000, 10000);
                pictureBox4.Location = new System.Drawing.Point(10000, 10000);
                pictureBox3.Location = new System.Drawing.Point(10000, 10000);
                pictureBox2.Location = new System.Drawing.Point(10000, 10000);
                pictureBox1.Location = new System.Drawing.Point(10000, 10000);
            }
            else if (i == 6)
            {
                pictureBox6.Location = new System.Drawing.Point(651, 95);
                pictureBox5.Location = new System.Drawing.Point(10000, 10000);
                pictureBox4.Location = new System.Drawing.Point(10000, 10000);
                pictureBox3.Location = new System.Drawing.Point(10000, 10000);
                pictureBox2.Location = new System.Drawing.Point(10000, 10000);
                pictureBox1.Location = new System.Drawing.Point(10000, 10000);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (i > 1)
                i -= 1;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(JiangHu));
            _1.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".BackgroundImage")));
            _2.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".BackgroundImage")));
            _3.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".BackgroundImage")));
            _4.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".BackgroundImage")));
            _5.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".BackgroundImage")));
            _6.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".BackgroundImage")));
            _7.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".BackgroundImage")));
            _8.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".BackgroundImage")));
            _9.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".BackgroundImage")));
            if (i == 1)
            {
                pictureBox1.Location = new System.Drawing.Point(651, 95);
                pictureBox5.Location = new System.Drawing.Point(10000, 10000);
                pictureBox4.Location = new System.Drawing.Point(10000, 10000);
                pictureBox3.Location = new System.Drawing.Point(10000, 10000);
                pictureBox2.Location = new System.Drawing.Point(10000, 10000);
                pictureBox6.Location = new System.Drawing.Point(10000, 10000);
            }
            else if (i == 2)
            {
                pictureBox2.Location = new System.Drawing.Point(651, 95);
                pictureBox5.Location = new System.Drawing.Point(10000, 10000);
                pictureBox4.Location = new System.Drawing.Point(10000, 10000);
                pictureBox3.Location = new System.Drawing.Point(10000, 10000);
                pictureBox6.Location = new System.Drawing.Point(10000, 10000);
                pictureBox1.Location = new System.Drawing.Point(10000, 10000);
                _1.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".Image")));
                _2.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".Image")));
                _3.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".Image")));
                _4.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".Image")));
                _5.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".Image")));
                _6.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".Image")));
                _7.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".Image")));
                _8.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".Image")));
                _9.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox" + i + ".Image")));
            }
            else if (i == 3)
            {
                pictureBox3.Location = new System.Drawing.Point(651, 95);
                pictureBox5.Location = new System.Drawing.Point(10000, 10000);
                pictureBox4.Location = new System.Drawing.Point(10000, 10000);
                pictureBox6.Location = new System.Drawing.Point(10000, 10000);
                pictureBox2.Location = new System.Drawing.Point(10000, 10000);
                pictureBox1.Location = new System.Drawing.Point(10000, 10000);
            }
            else if (i == 4)
            {
                pictureBox4.Location = new System.Drawing.Point(651, 95);
                pictureBox5.Location = new System.Drawing.Point(10000, 10000);
                pictureBox6.Location = new System.Drawing.Point(10000, 10000);
                pictureBox3.Location = new System.Drawing.Point(10000, 10000);
                pictureBox2.Location = new System.Drawing.Point(10000, 10000);
                pictureBox1.Location = new System.Drawing.Point(10000, 10000);
            }
            else if (i == 5)
            {
                pictureBox5.Location = new System.Drawing.Point(651, 95);
                pictureBox6.Location = new System.Drawing.Point(10000, 10000);
                pictureBox4.Location = new System.Drawing.Point(10000, 10000);
                pictureBox3.Location = new System.Drawing.Point(10000, 10000);
                pictureBox2.Location = new System.Drawing.Point(10000, 10000);
                pictureBox1.Location = new System.Drawing.Point(10000, 10000);
            }
            else if (i == 6)
            {
                pictureBox6.Location = new System.Drawing.Point(651, 95);
                pictureBox5.Location = new System.Drawing.Point(10000, 10000);
                pictureBox4.Location = new System.Drawing.Point(10000, 10000);
                pictureBox3.Location = new System.Drawing.Point(10000, 10000);
                pictureBox2.Location = new System.Drawing.Point(10000, 10000);
                pictureBox1.Location = new System.Drawing.Point(10000, 10000);
            }
        }
        public object sync = new object();
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            lock (sync)
            {
                Stage1.Enabled = true;
                Stage2.Enabled = true;
                Stage3.Enabled = true;
                Stage4.Enabled = true;
                Stage5.Enabled = true;
                Stage6.Enabled = true;
                Stage7.Enabled = true;
                Stage8.Enabled = true;
                Stage9.Enabled = true;
                MyName = comboBox1.Text;
            }
        }
        private void Star(object sender, EventArgs e)
        {
            GameState clientFromName = GameState.GetClientFromName(MyName);
            if (clientFromName == null)
            {
                List<KeyValuePair<uint, IJiangHu>> list = Revora.Game.JiangHu.JiangHuClients.Where<KeyValuePair<uint, IJiangHu>>((Func<KeyValuePair<uint, IJiangHu>, bool>)(p => p.Value.OwnName == MyName)).ToList<KeyValuePair<uint, IJiangHu>>();
                ComboBox comboBox = sender as ComboBox;
                IJiangHu jiangHu = list[0].Value;
                jiangHu.Stagers[(int)MyStage - 1].Activate = true;
                jiangHu.Stagers[(int)MyStage - 1].Stars[Convert.ToInt32(comboBox.Name.Substring(comboBox.Name.Length - 1, 1)) - 1].Activate = true;
                jiangHu.Stagers[(int)MyStage - 1].Stars[Convert.ToInt32(comboBox.Name.Substring(comboBox.Name.Length - 1, 1)) - 1].Level = i;
                jiangHu.Stagers[(int)MyStage - 1].Stars[Convert.ToInt32(comboBox.Name.Substring(comboBox.Name.Length - 1, 1)) - 1].Typ = (Revora.Game.JiangHu.JiangStages.AtributesType)Enum.Parse(typeof(Revora.Game.JiangHu.JiangStages.AtributesType), comboBox.Text);
                jiangHu.Stagers[(int)MyStage - 1].Stars[Convert.ToInt32(comboBox.Name.Substring(comboBox.Name.Length - 1, 1)) - 1].UID = ValueToRoll(jiangHu.Stagers[(int)MyStage - 1].Stars[Convert.ToInt32(comboBox.Name.Substring(comboBox.Name.Length - 1, 1)) - 1].Typ, i);
                Revora.Game.JiangHu.JiangHuClients.Remove(jiangHu.UID);
                Revora.Game.JiangHu.JiangHuClients.Add(jiangHu.UID, jiangHu);
            }
            else if (clientFromName != null)
            {
                ComboBox comboBox = sender as ComboBox;
                if (!clientFromName.Entity.MyJiang.Stagers[(int)MyStage - 1].Activate)
                    ++clientFromName.Entity.MyJiang.Stage;
                clientFromName.Entity.MyJiang.Stagers[(int)MyStage - 1].Activate = true;
                clientFromName.Entity.MyJiang.SendInfo(clientFromName, (byte)12, clientFromName.Entity.MyJiang.Stage.ToString());
                clientFromName.Entity.MyJiang.Stagers[(int)MyStage - 1].Stars[Convert.ToInt32(comboBox.Name.Substring(comboBox.Name.Length - 1, 1)) - 1].Activate = true;
                clientFromName.Entity.MyJiang.Stagers[(int)MyStage - 1].Stars[Convert.ToInt32(comboBox.Name.Substring(comboBox.Name.Length - 1, 1)) - 1].Level = i;
                clientFromName.Entity.MyJiang.Stagers[(int)MyStage - 1].Stars[Convert.ToInt32(comboBox.Name.Substring(comboBox.Name.Length - 1, 1)) - 1].Typ = (Revora.Game.JiangHu.JiangStages.AtributesType)Enum.Parse(typeof(Revora.Game.JiangHu.JiangStages.AtributesType), comboBox.Text);
                clientFromName.Entity.MyJiang.Stagers[(int)MyStage - 1].Stars[Convert.ToInt32(comboBox.Name.Substring(comboBox.Name.Length - 1, 1)) - 1].UID = ValueToRoll(clientFromName.Entity.MyJiang.Stagers[(int)MyStage - 1].Stars[Convert.ToInt32(comboBox.Name.Substring(comboBox.Name.Length - 1, 1)) - 1].Typ, i);
                if (clientFromName.Entity.MyJiang != null)
                {
                    clientFromName.Entity.MyJiang.SendStatus(clientFromName, clientFromName);
                    Revora.Game.JiangHu.JiangHuRanking.UpdateRank((Revora.Interfaces.IJiangHu)clientFromName.Entity.MyJiang);
                    clientFromName.LoadItemStats();
                }
            }
            img();
        }
        public ushort ValueToRoll(Revora.Game.JiangHu.JiangStages.AtributesType status, byte level)
        {
            return (ushort)((uint)(ushort)status + (uint)level * 256U);
        }
        private void button_Click(object sender, EventArgs e)
        {
            PictureBox button = sender as PictureBox;
            lock (sync)
            {
                MyStage = Convert.ToByte(button.Name.Substring(button.Name.Length - 1, 1));
                Star1.Enabled = true;
                Star2.Enabled = true;
                Star3.Enabled = true;
                Star4.Enabled = true;
                Star5.Enabled = true;
                Star6.Enabled = true;
                Star7.Enabled = true;
                Star8.Enabled = true;
                Star9.Enabled = true;
                Star1.Items.Clear();
                Star2.Items.Clear();
                Star3.Items.Clear();
                Star4.Items.Clear();
                Star5.Items.Clear();
                Star6.Items.Clear();
                Star7.Items.Clear();
                Star8.Items.Clear();
                Star9.Items.Clear();
                Star1.Text = "";
                Star2.Text = "";
                Star3.Text = "";
                Star4.Text = "";
                Star5.Text = "";
                Star6.Text = "";
                Star7.Text = "";
                Star8.Text = "";
                Star9.Text = "";
                Star1.Items.AddRange((object[])((IEnumerable<string>)JiangType).ToArray<string>());
                Star2.Items.AddRange((object[])((IEnumerable<string>)JiangType).ToArray<string>());
                Star3.Items.AddRange((object[])((IEnumerable<string>)JiangType).ToArray<string>());
                Star4.Items.AddRange((object[])((IEnumerable<string>)JiangType).ToArray<string>());
                Star5.Items.AddRange((object[])((IEnumerable<string>)JiangType).ToArray<string>());
                Star6.Items.AddRange((object[])((IEnumerable<string>)JiangType).ToArray<string>());
                Star7.Items.AddRange((object[])((IEnumerable<string>)JiangType).ToArray<string>());
                Star8.Items.AddRange((object[])((IEnumerable<string>)JiangType).ToArray<string>());
                Star9.Items.AddRange((object[])((IEnumerable<string>)JiangType).ToArray<string>());
                GameState clientFromName = GameState.GetClientFromName(MyName);
                if (clientFromName == null)
                {
                    List<KeyValuePair<uint, IJiangHu>> list = Revora.Game.JiangHu.JiangHuClients.Where<KeyValuePair<uint, IJiangHu>>((Func<KeyValuePair<uint, IJiangHu>, bool>)(p => p.Value.OwnName == MyName)).ToList<KeyValuePair<uint, IJiangHu>>();
                    ComboBox comboBox = sender as ComboBox;
                    IJiangHu jiangHu = list[0].Value;
                    if (jiangHu.Stagers[(int)MyStage - 1] == null)
                        return;
                    Star1.SelectedIndexChanged -= new EventHandler(Star);
                    Star2.SelectedIndexChanged -= new EventHandler(Star);
                    Star3.SelectedIndexChanged -= new EventHandler(Star);
                    Star4.SelectedIndexChanged -= new EventHandler(Star);
                    Star5.SelectedIndexChanged -= new EventHandler(Star);
                    Star6.SelectedIndexChanged -= new EventHandler(Star);
                    Star7.SelectedIndexChanged -= new EventHandler(Star);
                    Star8.SelectedIndexChanged -= new EventHandler(Star);
                    Star9.SelectedIndexChanged -= new EventHandler(Star);
                    Star1.Text = jiangHu.Stagers[(int)MyStage - 1].Stars[0].Typ.ToString();
                    Star2.Text = jiangHu.Stagers[(int)MyStage - 1].Stars[1].Typ.ToString();
                    Star3.Text = jiangHu.Stagers[(int)MyStage - 1].Stars[2].Typ.ToString();
                    Star4.Text = jiangHu.Stagers[(int)MyStage - 1].Stars[3].Typ.ToString();
                    Star5.Text = jiangHu.Stagers[(int)MyStage - 1].Stars[4].Typ.ToString();
                    Star6.Text = jiangHu.Stagers[(int)MyStage - 1].Stars[5].Typ.ToString();
                    Star7.Text = jiangHu.Stagers[(int)MyStage - 1].Stars[6].Typ.ToString();
                    Star8.Text = jiangHu.Stagers[(int)MyStage - 1].Stars[7].Typ.ToString();
                    Star9.Text = jiangHu.Stagers[(int)MyStage - 1].Stars[8].Typ.ToString();
                    Star1.SelectedIndexChanged += new EventHandler(Star);
                    Star2.SelectedIndexChanged += new EventHandler(Star);
                    Star3.SelectedIndexChanged += new EventHandler(Star);
                    Star4.SelectedIndexChanged += new EventHandler(Star);
                    Star5.SelectedIndexChanged += new EventHandler(Star);
                    Star6.SelectedIndexChanged += new EventHandler(Star);
                    Star7.SelectedIndexChanged += new EventHandler(Star);
                    Star8.SelectedIndexChanged += new EventHandler(Star);
                    Star9.SelectedIndexChanged += new EventHandler(Star);
                }
                else if (clientFromName.Entity.MyJiang.Stagers[(int)MyStage - 1] != null)
                {
                    Star1.SelectedIndexChanged -= new EventHandler(Star);
                    Star2.SelectedIndexChanged -= new EventHandler(Star);
                    Star3.SelectedIndexChanged -= new EventHandler(Star);
                    Star4.SelectedIndexChanged -= new EventHandler(Star);
                    Star5.SelectedIndexChanged -= new EventHandler(Star);
                    Star6.SelectedIndexChanged -= new EventHandler(Star);
                    Star7.SelectedIndexChanged -= new EventHandler(Star);
                    Star8.SelectedIndexChanged -= new EventHandler(Star);
                    Star9.SelectedIndexChanged -= new EventHandler(Star);
                    Star1.Text = clientFromName.Entity.MyJiang.Stagers[(int)MyStage - 1].Stars[0].Typ.ToString();
                    Star2.Text = clientFromName.Entity.MyJiang.Stagers[(int)MyStage - 1].Stars[1].Typ.ToString();
                    Star3.Text = clientFromName.Entity.MyJiang.Stagers[(int)MyStage - 1].Stars[2].Typ.ToString();
                    Star4.Text = clientFromName.Entity.MyJiang.Stagers[(int)MyStage - 1].Stars[3].Typ.ToString();
                    Star5.Text = clientFromName.Entity.MyJiang.Stagers[(int)MyStage - 1].Stars[4].Typ.ToString();
                    Star6.Text = clientFromName.Entity.MyJiang.Stagers[(int)MyStage - 1].Stars[5].Typ.ToString();
                    Star7.Text = clientFromName.Entity.MyJiang.Stagers[(int)MyStage - 1].Stars[6].Typ.ToString();
                    Star8.Text = clientFromName.Entity.MyJiang.Stagers[(int)MyStage - 1].Stars[7].Typ.ToString();
                    Star9.Text = clientFromName.Entity.MyJiang.Stagers[(int)MyStage - 1].Stars[8].Typ.ToString();
                    Star1.SelectedIndexChanged += new EventHandler(Star);
                    Star2.SelectedIndexChanged += new EventHandler(Star);
                    Star3.SelectedIndexChanged += new EventHandler(Star);
                    Star4.SelectedIndexChanged += new EventHandler(Star);
                    Star5.SelectedIndexChanged += new EventHandler(Star);
                    Star6.SelectedIndexChanged += new EventHandler(Star);
                    Star7.SelectedIndexChanged += new EventHandler(Star);
                    Star8.SelectedIndexChanged += new EventHandler(Star);
                    Star9.SelectedIndexChanged += new EventHandler(Star);
                }
            }
            img();
        }
        public void img()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(JiangHu));
            if (Star1.Text == "MaxLife")
                _11.Image = ((System.Drawing.Image)(resources.GetObject("button3.Image")));
            if (Star2.Text == "MaxLife")
                _22.Image = ((System.Drawing.Image)(resources.GetObject("button3.Image")));
            if (Star3.Text == "MaxLife")
                _33.Image = ((System.Drawing.Image)(resources.GetObject("button3.Image")));
            if (Star4.Text == "MaxLife")
                _44.Image = ((System.Drawing.Image)(resources.GetObject("button3.Image")));
            if (Star5.Text == "MaxLife")
                _55.Image = ((System.Drawing.Image)(resources.GetObject("button3.Image")));
            if (Star6.Text == "MaxLife")
                _66.Image = ((System.Drawing.Image)(resources.GetObject("button3.Image")));
            if (Star7.Text == "MaxLife")
                _77.Image = ((System.Drawing.Image)(resources.GetObject("button3.Image")));
            if (Star8.Text == "MaxLife")
                _88.Image = ((System.Drawing.Image)(resources.GetObject("button3.Image")));
            if (Star9.Text == "MaxLife")
                _99.Image = ((System.Drawing.Image)(resources.GetObject("button3.Image")));
            ///////////////////////
            if (Star1.Text == "PAttack")
                _11.Image = ((System.Drawing.Image)(resources.GetObject("button4.Image")));
            if (Star2.Text == "PAttack")
                _22.Image = ((System.Drawing.Image)(resources.GetObject("button4.Image")));
            if (Star3.Text == "PAttack")
                _33.Image = ((System.Drawing.Image)(resources.GetObject("button4.Image")));
            if (Star4.Text == "PAttack")
                _44.Image = ((System.Drawing.Image)(resources.GetObject("button4.Image")));
            if (Star5.Text == "PAttack")
                _55.Image = ((System.Drawing.Image)(resources.GetObject("button4.Image")));
            if (Star6.Text == "PAttack")
                _66.Image = ((System.Drawing.Image)(resources.GetObject("button4.Image")));
            if (Star7.Text == "PAttack")
                _77.Image = ((System.Drawing.Image)(resources.GetObject("button4.Image")));
            if (Star8.Text == "PAttack")
                _88.Image = ((System.Drawing.Image)(resources.GetObject("button4.Image")));
            if (Star9.Text == "PAttack")
                _99.Image = ((System.Drawing.Image)(resources.GetObject("button4.Image")));
            //////////////////////
            if (Star1.Text == "MAttack")
                _11.Image = ((System.Drawing.Image)(resources.GetObject("button8.Image")));
            if (Star2.Text == "MAttack")
                _22.Image = ((System.Drawing.Image)(resources.GetObject("button8.Image")));
            if (Star3.Text == "MAttack")
                _33.Image = ((System.Drawing.Image)(resources.GetObject("button8.Image")));
            if (Star4.Text == "MAttack")
                _44.Image = ((System.Drawing.Image)(resources.GetObject("button8.Image")));
            if (Star5.Text == "MAttack")
                _55.Image = ((System.Drawing.Image)(resources.GetObject("button8.Image")));
            if (Star6.Text == "MAttack")
                _66.Image = ((System.Drawing.Image)(resources.GetObject("button8.Image")));
            if (Star7.Text == "MAttack")
                _77.Image = ((System.Drawing.Image)(resources.GetObject("button8.Image")));
            if (Star8.Text == "MAttack")
                _88.Image = ((System.Drawing.Image)(resources.GetObject("button8.Image")));
            if (Star9.Text == "MAttack")
                _99.Image = ((System.Drawing.Image)(resources.GetObject("button8.Image")));
            //////////////////////
            if (Star1.Text == "PDefense")
                _11.Image = ((System.Drawing.Image)(resources.GetObject("button7.Image")));
            if (Star2.Text == "PDefense")
                _22.Image = ((System.Drawing.Image)(resources.GetObject("button7.Image")));
            if (Star3.Text == "PDefense")
                _33.Image = ((System.Drawing.Image)(resources.GetObject("button7.Image")));
            if (Star4.Text == "PDefense")
                _44.Image = ((System.Drawing.Image)(resources.GetObject("button7.Image")));
            if (Star5.Text == "PDefense")
                _55.Image = ((System.Drawing.Image)(resources.GetObject("button7.Image")));
            if (Star6.Text == "PDefense")
                _66.Image = ((System.Drawing.Image)(resources.GetObject("button7.Image")));
            if (Star7.Text == "PDefense")
                _77.Image = ((System.Drawing.Image)(resources.GetObject("button7.Image")));
            if (Star8.Text == "PDefense")
                _88.Image = ((System.Drawing.Image)(resources.GetObject("button7.Image")));
            if (Star9.Text == "PDefense")
                _99.Image = ((System.Drawing.Image)(resources.GetObject("button7.Image")));
            ////////////////////////
            if (Star1.Text == "Mdefense")
                _11.Image = ((System.Drawing.Image)(resources.GetObject("button5.Image")));
            if (Star2.Text == "Mdefense")
                _22.Image = ((System.Drawing.Image)(resources.GetObject("button5.Image")));
            if (Star3.Text == "Mdefense")
                _33.Image = ((System.Drawing.Image)(resources.GetObject("button5.Image")));
            if (Star4.Text == "Mdefense")
                _44.Image = ((System.Drawing.Image)(resources.GetObject("button5.Image")));
            if (Star5.Text == "Mdefense")
                _55.Image = ((System.Drawing.Image)(resources.GetObject("button5.Image")));
            if (Star6.Text == "Mdefense")
                _66.Image = ((System.Drawing.Image)(resources.GetObject("button5.Image")));
            if (Star7.Text == "Mdefense")
                _77.Image = ((System.Drawing.Image)(resources.GetObject("button5.Image")));
            if (Star8.Text == "Mdefense")
                _88.Image = ((System.Drawing.Image)(resources.GetObject("button5.Image")));
            if (Star9.Text == "Mdefense")
                _99.Image = ((System.Drawing.Image)(resources.GetObject("button5.Image")));
            //////////////////////////////
            if (Star1.Text == "FinalAttack")
                _11.Image = ((System.Drawing.Image)(resources.GetObject("button6.Image")));
            if (Star2.Text == "FinalAttack")
                _22.Image = ((System.Drawing.Image)(resources.GetObject("button6.Image")));
            if (Star3.Text == "FinalAttack")
                _33.Image = ((System.Drawing.Image)(resources.GetObject("button6.Image")));
            if (Star4.Text == "FinalAttack")
                _44.Image = ((System.Drawing.Image)(resources.GetObject("button6.Image")));
            if (Star5.Text == "FinalAttack")
                _55.Image = ((System.Drawing.Image)(resources.GetObject("button6.Image")));
            if (Star6.Text == "FinalAttack")
                _66.Image = ((System.Drawing.Image)(resources.GetObject("button6.Image")));
            if (Star7.Text == "FinalAttack")
                _77.Image = ((System.Drawing.Image)(resources.GetObject("button6.Image")));
            if (Star8.Text == "FinalAttack")
                _88.Image = ((System.Drawing.Image)(resources.GetObject("button6.Image")));
            if (Star9.Text == "FinalAttack")
                _99.Image = ((System.Drawing.Image)(resources.GetObject("button6.Image")));
            /////////////////////////////////
            if (Star1.Text == "FinalMagicAttack")
                _11.Image = ((System.Drawing.Image)(resources.GetObject("button11.Image")));
            if (Star2.Text == "FinalMagicAttack")
                _22.Image = ((System.Drawing.Image)(resources.GetObject("button11.Image")));
            if (Star3.Text == "FinalMagicAttack")
                _33.Image = ((System.Drawing.Image)(resources.GetObject("button11.Image")));
            if (Star4.Text == "FinalMagicAttack")
                _44.Image = ((System.Drawing.Image)(resources.GetObject("button11.Image")));
            if (Star5.Text == "FinalMagicAttack")
                _55.Image = ((System.Drawing.Image)(resources.GetObject("button11.Image")));
            if (Star6.Text == "FinalMagicAttack")
                _66.Image = ((System.Drawing.Image)(resources.GetObject("button11.Image")));
            if (Star7.Text == "FinalMagicAttack")
                _77.Image = ((System.Drawing.Image)(resources.GetObject("button11.Image")));
            if (Star8.Text == "FinalMagicAttack")
                _88.Image = ((System.Drawing.Image)(resources.GetObject("button11.Image")));
            if (Star9.Text == "FinalMagicAttack")
                _99.Image = ((System.Drawing.Image)(resources.GetObject("button11.Image")));
            /////////////////////////////////
            if (Star1.Text == "FinalDefense")
                _11.Image = ((System.Drawing.Image)(resources.GetObject("button10.Image")));
            if (Star2.Text == "FinalDefense")
                _22.Image = ((System.Drawing.Image)(resources.GetObject("button10.Image")));
            if (Star3.Text == "FinalDefense")
                _33.Image = ((System.Drawing.Image)(resources.GetObject("button10.Image")));
            if (Star4.Text == "FinalDefense")
                _44.Image = ((System.Drawing.Image)(resources.GetObject("button10.Image")));
            if (Star5.Text == "FinalDefense")
                _55.Image = ((System.Drawing.Image)(resources.GetObject("button10.Image")));
            if (Star6.Text == "FinalDefense")
                _66.Image = ((System.Drawing.Image)(resources.GetObject("button10.Image")));
            if (Star7.Text == "FinalDefense")
                _77.Image = ((System.Drawing.Image)(resources.GetObject("button10.Image")));
            if (Star8.Text == "FinalDefense")
                _88.Image = ((System.Drawing.Image)(resources.GetObject("button10.Image")));
            if (Star9.Text == "FinalDefense")
                _99.Image = ((System.Drawing.Image)(resources.GetObject("button10.Image")));
            /////////////////////////////////
            if (Star1.Text == "FinalMagicDefense")
                _11.Image = ((System.Drawing.Image)(resources.GetObject("button9.Image")));
            if (Star2.Text == "FinalMagicDefense")
                _22.Image = ((System.Drawing.Image)(resources.GetObject("button9.Image")));
            if (Star3.Text == "FinalMagicDefense")
                _33.Image = ((System.Drawing.Image)(resources.GetObject("button9.Image")));
            if (Star4.Text == "FinalMagicDefense")
                _44.Image = ((System.Drawing.Image)(resources.GetObject("button9.Image")));
            if (Star5.Text == "FinalMagicDefense")
                _55.Image = ((System.Drawing.Image)(resources.GetObject("button9.Image")));
            if (Star6.Text == "FinalMagicDefense")
                _66.Image = ((System.Drawing.Image)(resources.GetObject("button9.Image")));
            if (Star7.Text == "FinalMagicDefense")
                _77.Image = ((System.Drawing.Image)(resources.GetObject("button9.Image")));
            if (Star8.Text == "FinalMagicDefense")
                _88.Image = ((System.Drawing.Image)(resources.GetObject("button9.Image")));
            if (Star9.Text == "FinalMagicDefense")
                _99.Image = ((System.Drawing.Image)(resources.GetObject("button9.Image")));
            /////////////////////////////////
            if (Star1.Text == "CriticalStrike")
                _11.Image = ((System.Drawing.Image)(resources.GetObject("button14.Image")));
            if (Star2.Text == "CriticalStrike")
                _22.Image = ((System.Drawing.Image)(resources.GetObject("button14.Image")));
            if (Star3.Text == "CriticalStrike")
                _33.Image = ((System.Drawing.Image)(resources.GetObject("button14.Image")));
            if (Star4.Text == "CriticalStrike")
                _44.Image = ((System.Drawing.Image)(resources.GetObject("button14.Image")));
            if (Star5.Text == "CriticalStrike")
                _55.Image = ((System.Drawing.Image)(resources.GetObject("button14.Image")));
            if (Star6.Text == "CriticalStrike")
                _66.Image = ((System.Drawing.Image)(resources.GetObject("button14.Image")));
            if (Star7.Text == "CriticalStrike")
                _77.Image = ((System.Drawing.Image)(resources.GetObject("button14.Image")));
            if (Star8.Text == "CriticalStrike")
                _88.Image = ((System.Drawing.Image)(resources.GetObject("button14.Image")));
            if (Star9.Text == "CriticalStrike")
                _99.Image = ((System.Drawing.Image)(resources.GetObject("button14.Image")));
            /////////////////////////////////
            if (Star1.Text == "SkillCriticalStrike")
                _11.Image = ((System.Drawing.Image)(resources.GetObject("button13.Image")));
            if (Star2.Text == "SkillCriticalStrike")
                _22.Image = ((System.Drawing.Image)(resources.GetObject("button13.Image")));
            if (Star3.Text == "SkillCriticalStrike")
                _33.Image = ((System.Drawing.Image)(resources.GetObject("button13.Image")));
            if (Star4.Text == "SkillCriticalStrike")
                _44.Image = ((System.Drawing.Image)(resources.GetObject("button13.Image")));
            if (Star5.Text == "SkillCriticalStrike")
                _55.Image = ((System.Drawing.Image)(resources.GetObject("button13.Image")));
            if (Star6.Text == "SkillCriticalStrike")
                _66.Image = ((System.Drawing.Image)(resources.GetObject("button13.Image")));
            if (Star7.Text == "SkillCriticalStrike")
                _77.Image = ((System.Drawing.Image)(resources.GetObject("button13.Image")));
            if (Star8.Text == "SkillCriticalStrike")
                _88.Image = ((System.Drawing.Image)(resources.GetObject("button13.Image")));
            if (Star9.Text == "SkillCriticalStrike")
                _99.Image = ((System.Drawing.Image)(resources.GetObject("button13.Image")));
            /////////////////////////////////
            if (Star1.Text == "Immunity")
                _11.Image = ((System.Drawing.Image)(resources.GetObject("button12.Image")));
            if (Star2.Text == "Immunity")
                _22.Image = ((System.Drawing.Image)(resources.GetObject("button12.Image")));
            if (Star3.Text == "Immunity")
                _33.Image = ((System.Drawing.Image)(resources.GetObject("button12.Image")));
            if (Star4.Text == "Immunity")
                _44.Image = ((System.Drawing.Image)(resources.GetObject("button12.Image")));
            if (Star5.Text == "Immunity")
                _55.Image = ((System.Drawing.Image)(resources.GetObject("button12.Image")));
            if (Star6.Text == "Immunity")
                _66.Image = ((System.Drawing.Image)(resources.GetObject("button12.Image")));
            if (Star7.Text == "Immunity")
                _77.Image = ((System.Drawing.Image)(resources.GetObject("button12.Image")));
            if (Star8.Text == "Immunity")
                _88.Image = ((System.Drawing.Image)(resources.GetObject("button12.Image")));
            if (Star9.Text == "Immunity")
                _99.Image = ((System.Drawing.Image)(resources.GetObject("button12.Image")));
            /////////////////////////////////
            if (Star1.Text == "Breakthrough")
                _11.Image = ((System.Drawing.Image)(resources.GetObject("button15.Image")));
            if (Star2.Text == "Breakthrough")
                _22.Image = ((System.Drawing.Image)(resources.GetObject("button15.Image")));
            if (Star3.Text == "Breakthrough")
                _33.Image = ((System.Drawing.Image)(resources.GetObject("button15.Image")));
            if (Star4.Text == "Breakthrough")
                _44.Image = ((System.Drawing.Image)(resources.GetObject("button15.Image")));
            if (Star5.Text == "Breakthrough")
                _55.Image = ((System.Drawing.Image)(resources.GetObject("button15.Image")));
            if (Star6.Text == "Breakthrough")
                _66.Image = ((System.Drawing.Image)(resources.GetObject("button15.Image")));
            if (Star7.Text == "Breakthrough")
                _77.Image = ((System.Drawing.Image)(resources.GetObject("button15.Image")));
            if (Star8.Text == "Breakthrough")
                _88.Image = ((System.Drawing.Image)(resources.GetObject("button15.Image")));
            if (Star9.Text == "Breakthrough")
                _99.Image = ((System.Drawing.Image)(resources.GetObject("button15.Image")));
            /////////////////////////////////
            if (Star1.Text == "Counteraction")
                _11.Image = ((System.Drawing.Image)(resources.GetObject("button16.Image")));
            if (Star2.Text == "Counteraction")
                _22.Image = ((System.Drawing.Image)(resources.GetObject("button16.Image")));
            if (Star3.Text == "Counteraction")
                _33.Image = ((System.Drawing.Image)(resources.GetObject("button16.Image")));
            if (Star4.Text == "Counteraction")
                _44.Image = ((System.Drawing.Image)(resources.GetObject("button16.Image")));
            if (Star5.Text == "Counteraction")
                _55.Image = ((System.Drawing.Image)(resources.GetObject("button16.Image")));
            if (Star6.Text == "Counteraction")
                _66.Image = ((System.Drawing.Image)(resources.GetObject("button16.Image")));
            if (Star7.Text == "Counteraction")
                _77.Image = ((System.Drawing.Image)(resources.GetObject("button16.Image")));
            if (Star8.Text == "Counteraction")
                _88.Image = ((System.Drawing.Image)(resources.GetObject("button16.Image")));
            if (Star9.Text == "Counteraction")
                _99.Image = ((System.Drawing.Image)(resources.GetObject("button16.Image")));
            /////////////////////////////////
            if (Star1.Text == "MaxMana")
                _11.Image = ((System.Drawing.Image)(resources.GetObject("button17.Image")));
            if (Star2.Text == "MaxMana")
                _22.Image = ((System.Drawing.Image)(resources.GetObject("button17.Image")));
            if (Star3.Text == "MaxMana")
                _33.Image = ((System.Drawing.Image)(resources.GetObject("button17.Image")));
            if (Star4.Text == "MaxMana")
                _44.Image = ((System.Drawing.Image)(resources.GetObject("button17.Image")));
            if (Star5.Text == "MaxMana")
                _55.Image = ((System.Drawing.Image)(resources.GetObject("button17.Image")));
            if (Star6.Text == "MaxMana")
                _66.Image = ((System.Drawing.Image)(resources.GetObject("button17.Image")));
            if (Star7.Text == "MaxMana")
                _77.Image = ((System.Drawing.Image)(resources.GetObject("button17.Image")));
            if (Star8.Text == "MaxMana")
                _88.Image = ((System.Drawing.Image)(resources.GetObject("button17.Image")));
            if (Star9.Text == "MaxMana")
                _99.Image = ((System.Drawing.Image)(resources.GetObject("button17.Image")));


        }
        public string[] JiangType = new string[15]
    {
      "MaxLife",
      "PAttack",
      "MAttack",
      "PDefense",
      "Mdefense",
      "FinalAttack",
      "FinalMagicAttack",
      "FinalDefense",
      "FinalMagicDefense",
      "CriticalStrike",
      "SkillCriticalStrike",
      "Immunity",
      "Breakthrough",
      "Counteraction",
      "MaxMana"
    };
        private void Jiang_Load(object sender, EventArgs e)
        {
            foreach (IJiangHu jiangHu in Revora.Game.JiangHu.JiangHuClients.Values.ToArray<IJiangHu>())
                comboBox1.Items.Add((object)jiangHu.OwnName);
        }
        private void button18_Click(object sender, EventArgs e)
        {
            Star1.Text = Star1.Text;
            Star2.Text = Star1.Text;
            Star3.Text = Star1.Text;
            Star4.Text = Star1.Text;
            Star5.Text = Star1.Text;
            Star6.Text = Star1.Text;
            Star7.Text = Star1.Text;
            Star8.Text = Star1.Text;
            Star9.Text = Star1.Text;
            img();
        }
    }
}

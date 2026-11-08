namespace ConquerOnline
{
    using Revora.Database;
    using Npgsql;
    using System;
    using System.ComponentModel;
    using System.Data;
    using System.Drawing;
    using System.Windows.Forms;
    using Revora;

    public class Accounts : Form
    {
        private Button button10;
        private Button button11;
        private Button button12;
        private Button button13;
        private Button button9;
        private ComboBox comboBox1;
        public static Npgsql.NpgsqlConnection conn = new Npgsql.NpgsqlConnection();

        private TextBox boogy1;//level
        private TextBox boogy2;//id
        private TextBox boogy3;//name
        private TextBox boogy4;//body
        private TextBox boogy12;//wepR
        private TextBox boogy13;//wepL
        private TextBox boogy14;//Garment
        private TextBox boogy15;//Msg
        private TextBox boogy16;//map
        private TextBox boogy17;//x
        private TextBox boogy18;//y
        private TextBox boogy19;//itemID
        private TextBox boogy110;//plus
        private TextBox boogy111;//encheant
        private TextBox boogy112;//beless
        private TextBox boogy113;//soc1
        private TextBox boogy114;//soc2
        private TextBox boogy115;//cost
        private TextBox boogy116;//count
        private GroupBox groupBox_1;
        private IContainer icontainer_0 = null;
        private Label ahmed38;
        private Label ahmed39;
        private Label ahmed40;
        private Label ahmed90;
        private Label ahmed41;
        private Label ahmed46;
        private Label ahmed42;
        private Label ahmed48;
        private Label ahmed49;
        private Label ahmed50;
        private Label ahmed51;
        private Label ahmed52;
        private Label ahmed53;
        private Label ahmed54;
        private Label ahmed55;
        private Label ahmed56;
        private Label ahmed57;
        private Label ahmed58;
        private Label ahmed59;
        private Label ahmed60;
        private Label ahmed61;

        private TextBox defNdEaiis;
        private TextBox defNdEaiis2;
        private TextBox defNdEaiis3;
        private TextBox defNdEaiis4;
        private TextBox defNdEaiis5;
        private TextBox defNdEaiis6;
        private TextBox defNdEaiis7;
        private TextBox defNdEaiis8;
        private TextBox defNdEaiis9;
        private TextBox defNdEaiis10;
        private TextBox defNdEaiis11;
        private TextBox defNdEaiis12;
        private TextBox defNdEaiis13;
        private TextBox defNdEaiis14;
        private TextBox defNdEaiis15;
        private TextBox defNdEaiis16;
        private GroupBox groupBox_0;
        private Label label38;
        private Label label39;
        private Label label40;
        private Label label41;
        private Label label46;
        private Label label42;
        private Label label47;
        private Label label48;
        private Label label49;
        private Label label50;
        private Label label51;
        private Label label52;
        private Label label53;
        private Label label54;
        private Label label55;
        private Label label56;
        private Label label57;
        private Label label58;
        private Label label59;
        private Label label60;
        private Label label61;
        private TextBox textBox_0;
        private TextBox textBox_1;
        private TextBox textBox_2;

        public Accounts()
        {
            this.InitializeComponent();
        }



        private void button10_Click(object sender, EventArgs e)
        {
            if (1 != 0)
            {
                using (conn = DataHolder.MySqlConnection)
                {
                    if (conn.State != ConnectionState.Open)
                    {
                        conn.Open();
                    }
                    Revora.Database.MySqlCommand command = new Revora.Database.MySqlCommand(MySqlCommandType.UPDATE);
                    command.Update("bots").Set("BotName", this.textBox_2.Text).Set("BotBody", this.textBox_0.Text).Set("BotLevel", this.defNdEaiis.Text).Set("BotWeaponR", this.defNdEaiis2.Text).Set("BotWeaponL", this.defNdEaiis3.Text).Set("BotMessage", this.defNdEaiis5.Text).Set("BotGarment", this.defNdEaiis4.Text).Set("BotMap", this.defNdEaiis6.Text).Set("BotMapx", this.defNdEaiis7.Text).Set("BotMapy", this.defNdEaiis8.Text).Set("BItemID", this.defNdEaiis9.Text).Set("BItemPlus", this.defNdEaiis10.Text).Set("BItemEnchant", this.defNdEaiis11.Text).Set("BItemBless", this.defNdEaiis12.Text).Set("BItemSoc1", this.defNdEaiis13.Text).Set("BItemSoc2", this.defNdEaiis14.Text).Set("BItemCost", this.defNdEaiis15.Text).Set("BItemCount", this.defNdEaiis16.Text);
                    command.Where("BotID", this.textBox_1.Text).Execute();
                    conn.Close();
                }
                MessageBox.Show("Done New Modify ");
                this.comboBox1.Items.Clear();
                this.defNdEaiis.Clear();//level
                this.defNdEaiis2.Clear();//wepr
                this.defNdEaiis3.Clear();//wepL
                this.defNdEaiis4.Clear();//garm
                this.defNdEaiis5.Clear();//msg
                this.defNdEaiis6.Clear();//map
                this.defNdEaiis7.Clear();//mapx
                this.defNdEaiis8.Clear();//mapy
                this.defNdEaiis9.Clear();//BItemID
                this.defNdEaiis10.Clear();//BItemPlus
                this.defNdEaiis11.Clear();//BItemEnchant
                this.defNdEaiis12.Clear();//BItemBless
                this.defNdEaiis13.Clear();//BItemSoc1
                this.defNdEaiis14.Clear();//BItemSoc2
                this.defNdEaiis15.Clear();//Cost
                this.defNdEaiis16.Clear();//Cost
                this.textBox_2.Clear();//name
                this.textBox_1.Clear();//id
                this.textBox_0.Clear();//body
            }
        }
        private void button11_Click(object sender, EventArgs e)
        {

                this.comboBox1.Items.Clear();
                this.defNdEaiis.Clear();//level
                this.defNdEaiis2.Clear();//wepr
                this.defNdEaiis3.Clear();//wepL
                this.defNdEaiis4.Clear();//garm
                this.defNdEaiis5.Clear();//msg
                this.defNdEaiis6.Clear();//map
                this.defNdEaiis7.Clear();//mapx
                this.defNdEaiis8.Clear();//mapy
                this.defNdEaiis9.Clear();//BItemID
                this.defNdEaiis10.Clear();//BItemPlus
                this.defNdEaiis11.Clear();//BItemEnchant
                this.defNdEaiis12.Clear();//BItemBless
                this.defNdEaiis13.Clear();//BItemSoc1
                this.defNdEaiis14.Clear();//BItemSoc2
                this.defNdEaiis15.Clear();//Cost
                this.defNdEaiis16.Clear();//Cost
                this.textBox_2.Clear();//name
                this.textBox_1.Clear();//id
                this.textBox_0.Clear();//body
            
        }
        private void button13_Click(object sender, EventArgs e)
        {

            using (conn = DataHolder.MySqlConnection)
            {
                if (conn.State != ConnectionState.Open)
                {
                    conn.Open();
                }
                Revora.Database.MySqlCommand command = new Revora.Database.MySqlCommand(MySqlCommandType.INSERT);
                command.Insert("bots").Insert("BotID", this.boogy2.Text).Insert("BotName", this.boogy3.Text)
                    .Insert("BotBody", this.boogy4.Text).Insert("BotLevel", this.defNdEaiis.Text)
                    .Insert("BotWeaponR", this.boogy12.Text).Insert("BotWeaponL", this.boogy12.Text)
                    .Insert("BotMessage", this.boogy15.Text).Insert("BotGarment", this.boogy14.Text)
                    .Insert("BotMap", this.boogy16.Text).Insert("BotMapx", this.boogy17.Text)
                    .Insert("BotMapy", this.boogy18.Text).Insert("BItemID", this.boogy19.Text)
                    .Insert("BItemPlus", this.boogy110.Text)
                    .Insert("BItemEnchant", this.boogy111.Text).Insert("BItemBless", this.boogy112.Text)
                    .Insert("BItemSoc1", this.boogy113.Text).Insert("BItemSoc2", this.boogy114.Text)
                    .Insert("BItemCost", this.boogy115.Text).Insert("BItemCount", this.boogy116.Text);
                command.Execute();
                conn.Close();
            }
            MessageBox.Show("Successfully New Add Booth ");
            

        }
        private void button9_Click(object sender, EventArgs e)
        {
            if (1 != 0)
            {
                using (conn = DataHolder.MySqlConnection)
                {
                    if (conn.State != ConnectionState.Open)
                    {
                        conn.Open();
                    }
                    MySqlReader reader = new MySqlReader(new Revora.Database.MySqlCommand(MySqlCommandType.SELECT).Select("bots"));
                    this.comboBox1.Items.Clear();
                    while (reader.Read())
                    {
                        this.comboBox1.Items.Add(reader.ReadString("BotID"));
                    }
                    reader.Close();
                    conn.Close();
                }
            }
        }
        private void button12_Click(object sender, EventArgs e)
        {
            if (1 != 0)
            {
                using (conn = DataHolder.MySqlConnection)
                {
                    if (conn.State != ConnectionState.Open)
                    {
                        conn.Open();
                    }
                    MySqlReader reader = new MySqlReader(new Revora.Database.MySqlCommand(MySqlCommandType.SELECT).Select("bots"));
                    this.comboBox1.Items.Clear();
                    while (reader.Read())
                    {
                        this.comboBox1.Items.Add(reader.ReadString("BotID"));
                    }
                    reader.Close();
                    conn.Close();
                }
            }
        }
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (0 == 0)
            {
                this.textBox_2.Text = this.comboBox1.Text;
                using (conn = DataHolder.MySqlConnection)
                {
                    if (conn.State != ConnectionState.Open)
                    {
                        conn.Open();
                    }
                    MySqlReader reader = new MySqlReader(new Revora.Database.MySqlCommand(MySqlCommandType.SELECT).Select("bots").Where("BotID", this.comboBox1.Text));
                    while (reader.Read())
                    {
                        this.textBox_1.Text = reader.ReadString("BotID");
                        this.textBox_0.Text = reader.ReadString("BotBody");
                        this.defNdEaiis.Text = reader.ReadString("BotLevel");
                        this.textBox_2.Text = reader.ReadString("BotName");
                        this.defNdEaiis2.Text = reader.ReadString("BotWeaponR");
                        this.defNdEaiis3.Text = reader.ReadString("BotWeaponL");
                        this.defNdEaiis4.Text = reader.ReadString("BotGarment");
                        this.defNdEaiis5.Text = reader.ReadString("BotMessage");
                        this.defNdEaiis6.Text = reader.ReadString("BotMap");
                        this.defNdEaiis7.Text = reader.ReadString("BotMapx");
                        this.defNdEaiis8.Text = reader.ReadString("BotMapy");
                        this.defNdEaiis9.Text = reader.ReadString("BItemID");
                        this.defNdEaiis10.Text = reader.ReadString("BItemPlus");
                        this.defNdEaiis11.Text = reader.ReadString("BItemEnchant");
                        this.defNdEaiis12.Text = reader.ReadString("BItemBless");
                        this.defNdEaiis13.Text = reader.ReadString("BItemSoc1");
                        this.defNdEaiis14.Text = reader.ReadString("BItemSoc2");
                        this.defNdEaiis15.Text = reader.ReadString("BItemCost");
                        this.defNdEaiis16.Text = reader.ReadString("BItemCount");
                    }
                    reader.Close();
                    conn.Close();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (this.icontainer_0 != null))
            {
                this.icontainer_0.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.groupBox_1 = new System.Windows.Forms.GroupBox();
            this.ahmed38 = new System.Windows.Forms.Label();
            this.ahmed39 = new System.Windows.Forms.Label();
            this.ahmed40 = new System.Windows.Forms.Label();
            this.ahmed90 = new System.Windows.Forms.Label();
            this.boogy1 = new System.Windows.Forms.TextBox();
            this.boogy2 = new System.Windows.Forms.TextBox();
            this.boogy3 = new System.Windows.Forms.TextBox();
            this.boogy4 = new System.Windows.Forms.TextBox();
            this.boogy12 = new System.Windows.Forms.TextBox();
            this.boogy13 = new System.Windows.Forms.TextBox();
            this.boogy14 = new System.Windows.Forms.TextBox();
            this.boogy15 = new System.Windows.Forms.TextBox();
            this.boogy16 = new System.Windows.Forms.TextBox();
            this.boogy17 = new System.Windows.Forms.TextBox();
            this.boogy18 = new System.Windows.Forms.TextBox();
            this.button12 = new System.Windows.Forms.Button();
            this.button13 = new System.Windows.Forms.Button();
            this.boogy19 = new System.Windows.Forms.TextBox();
            this.boogy110 = new System.Windows.Forms.TextBox();
            this.boogy111 = new System.Windows.Forms.TextBox();
            this.boogy112 = new System.Windows.Forms.TextBox();
            this.boogy113 = new System.Windows.Forms.TextBox();
            this.boogy114 = new System.Windows.Forms.TextBox();
            this.boogy115 = new System.Windows.Forms.TextBox();
            this.boogy116 = new System.Windows.Forms.TextBox();
            this.ahmed41 = new System.Windows.Forms.Label();
            this.ahmed46 = new System.Windows.Forms.Label();
            this.ahmed48 = new System.Windows.Forms.Label();
            this.ahmed49 = new System.Windows.Forms.Label();
            this.ahmed51 = new System.Windows.Forms.Label();
            this.ahmed52 = new System.Windows.Forms.Label();
            this.ahmed53 = new System.Windows.Forms.Label();
            this.ahmed54 = new System.Windows.Forms.Label();
            this.ahmed55 = new System.Windows.Forms.Label();
            this.ahmed56 = new System.Windows.Forms.Label();
            this.ahmed57 = new System.Windows.Forms.Label();
            this.ahmed58 = new System.Windows.Forms.Label();
            this.ahmed59 = new System.Windows.Forms.Label();
            this.ahmed60 = new System.Windows.Forms.Label();
            this.ahmed61 = new System.Windows.Forms.Label();
            this.ahmed50 = new System.Windows.Forms.Label();
            this.ahmed42 = new System.Windows.Forms.Label();
            this.textBox_0 = new System.Windows.Forms.TextBox();
            this.textBox_1 = new System.Windows.Forms.TextBox();
            this.button9 = new System.Windows.Forms.Button();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.button11 = new System.Windows.Forms.Button();
            this.button10 = new System.Windows.Forms.Button();
            this.textBox_2 = new System.Windows.Forms.TextBox();
            this.groupBox_0 = new System.Windows.Forms.GroupBox();
            this.label38 = new System.Windows.Forms.Label();
            this.label39 = new System.Windows.Forms.Label();
            this.label40 = new System.Windows.Forms.Label();
            this.defNdEaiis = new System.Windows.Forms.TextBox();
            this.defNdEaiis2 = new System.Windows.Forms.TextBox();
            this.defNdEaiis3 = new System.Windows.Forms.TextBox();
            this.defNdEaiis4 = new System.Windows.Forms.TextBox();
            this.defNdEaiis5 = new System.Windows.Forms.TextBox();
            this.defNdEaiis6 = new System.Windows.Forms.TextBox();
            this.defNdEaiis7 = new System.Windows.Forms.TextBox();
            this.defNdEaiis8 = new System.Windows.Forms.TextBox();
            this.defNdEaiis9 = new System.Windows.Forms.TextBox();
            this.defNdEaiis10 = new System.Windows.Forms.TextBox();
            this.defNdEaiis11 = new System.Windows.Forms.TextBox();
            this.defNdEaiis12 = new System.Windows.Forms.TextBox();
            this.defNdEaiis13 = new System.Windows.Forms.TextBox();
            this.defNdEaiis14 = new System.Windows.Forms.TextBox();
            this.defNdEaiis15 = new System.Windows.Forms.TextBox();
            this.defNdEaiis16 = new System.Windows.Forms.TextBox();
            this.label41 = new System.Windows.Forms.Label();
            this.label46 = new System.Windows.Forms.Label();
            this.label47 = new System.Windows.Forms.Label();
            this.label48 = new System.Windows.Forms.Label();
            this.label49 = new System.Windows.Forms.Label();
            this.label51 = new System.Windows.Forms.Label();
            this.label52 = new System.Windows.Forms.Label();
            this.label53 = new System.Windows.Forms.Label();
            this.label54 = new System.Windows.Forms.Label();
            this.label55 = new System.Windows.Forms.Label();
            this.label56 = new System.Windows.Forms.Label();
            this.label57 = new System.Windows.Forms.Label();
            this.label58 = new System.Windows.Forms.Label();
            this.label59 = new System.Windows.Forms.Label();
            this.label60 = new System.Windows.Forms.Label();
            this.label61 = new System.Windows.Forms.Label();
            this.label50 = new System.Windows.Forms.Label();
            this.label42 = new System.Windows.Forms.Label();
            this.groupBox_1.SuspendLayout();
            this.groupBox_0.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox_1
            // 
            this.groupBox_1.Controls.Add(this.ahmed38);
            this.groupBox_1.Controls.Add(this.ahmed39);
            this.groupBox_1.Controls.Add(this.ahmed40);
            this.groupBox_1.Controls.Add(this.ahmed90);
            this.groupBox_1.Controls.Add(this.boogy1);
            this.groupBox_1.Controls.Add(this.boogy2);
            this.groupBox_1.Controls.Add(this.boogy3);
            this.groupBox_1.Controls.Add(this.boogy4);
            this.groupBox_1.Controls.Add(this.boogy12);
            this.groupBox_1.Controls.Add(this.boogy13);
            this.groupBox_1.Controls.Add(this.boogy14);
            this.groupBox_1.Controls.Add(this.boogy15);
            this.groupBox_1.Controls.Add(this.boogy16);
            this.groupBox_1.Controls.Add(this.boogy17);
            this.groupBox_1.Controls.Add(this.boogy18);
            this.groupBox_1.Controls.Add(this.button12);
            this.groupBox_1.Controls.Add(this.button13);
            this.groupBox_1.Controls.Add(this.boogy19);
            this.groupBox_1.Controls.Add(this.boogy110);
            this.groupBox_1.Controls.Add(this.boogy111);
            this.groupBox_1.Controls.Add(this.boogy112);
            this.groupBox_1.Controls.Add(this.boogy113);
            this.groupBox_1.Controls.Add(this.boogy114);
            this.groupBox_1.Controls.Add(this.boogy115);
            this.groupBox_1.Controls.Add(this.boogy116);
            this.groupBox_1.Controls.Add(this.ahmed41);
            this.groupBox_1.Controls.Add(this.ahmed46);
            this.groupBox_1.Controls.Add(this.ahmed48);
            this.groupBox_1.Controls.Add(this.ahmed49);
            this.groupBox_1.Controls.Add(this.ahmed51);
            this.groupBox_1.Controls.Add(this.ahmed52);
            this.groupBox_1.Controls.Add(this.ahmed53);
            this.groupBox_1.Controls.Add(this.ahmed54);
            this.groupBox_1.Controls.Add(this.ahmed55);
            this.groupBox_1.Controls.Add(this.ahmed56);
            this.groupBox_1.Controls.Add(this.ahmed57);
            this.groupBox_1.Controls.Add(this.ahmed58);
            this.groupBox_1.Controls.Add(this.ahmed59);
            this.groupBox_1.Controls.Add(this.ahmed60);
            this.groupBox_1.Controls.Add(this.ahmed61);
            this.groupBox_1.Controls.Add(this.ahmed50);
            this.groupBox_1.Controls.Add(this.ahmed42);
            this.groupBox_1.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox_1.ForeColor = System.Drawing.SystemColors.MenuHighlight;
            this.groupBox_1.Location = new System.Drawing.Point(391, 12);
            this.groupBox_1.Name = "groupBox_1";
            this.groupBox_1.Size = new System.Drawing.Size(375, 569);
            this.groupBox_1.TabIndex = 56;
            this.groupBox_1.TabStop = false;
            this.groupBox_1.Text = "Add Booth ";
            // 
            // ahmed38
            // 
            this.ahmed38.AutoSize = true;
            this.ahmed38.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed38.Location = new System.Drawing.Point(24, 117);
            this.ahmed38.Name = "ahmed38";
            this.ahmed38.Size = new System.Drawing.Size(72, 16);
            this.ahmed38.TabIndex = 11;
            this.ahmed38.Text = "BoothBody:";
            // 
            // ahmed39
            // 
            this.ahmed39.AutoSize = true;
            this.ahmed39.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed39.ForeColor = System.Drawing.Color.Red;
            this.ahmed39.Location = new System.Drawing.Point(209, 35);
            this.ahmed39.Name = "ahmed39";
            this.ahmed39.Size = new System.Drawing.Size(0, 16);
            this.ahmed39.TabIndex = 10;
            // 
            // ahmed40
            // 
            this.ahmed40.AutoSize = true;
            this.ahmed40.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed40.Location = new System.Drawing.Point(40, 60);
            this.ahmed40.Name = "ahmed40";
            this.ahmed40.Size = new System.Drawing.Size(57, 16);
            this.ahmed40.TabIndex = 8;
            this.ahmed40.Text = "BoothID:";
            // 
            // ahmed90
            // 
            this.ahmed90.AutoSize = true;
            this.ahmed90.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed90.Location = new System.Drawing.Point(38, 24);
            this.ahmed90.Name = "ahmed90";
            this.ahmed90.Size = new System.Drawing.Size(266, 16);
            this.ahmed90.TabIndex = 8;
            this.ahmed90.Text = "ID Should be From 70000000 To 999999999:";
            // 
            // boogy1
            // 
            this.boogy1.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boogy1.ForeColor = System.Drawing.Color.Magenta;
            this.boogy1.Location = new System.Drawing.Point(122, 141);
            this.boogy1.MaxLength = 14;
            this.boogy1.Name = "boogy1";
            this.boogy1.Size = new System.Drawing.Size(144, 23);
            this.boogy1.TabIndex = 3;
            // 
            // boogy2
            // 
            this.boogy2.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boogy2.ForeColor = System.Drawing.Color.Magenta;
            this.boogy2.Location = new System.Drawing.Point(122, 58);
            this.boogy2.MaxLength = 14;
            this.boogy2.Name = "boogy2";
            this.boogy2.Size = new System.Drawing.Size(144, 23);
            this.boogy2.TabIndex = 3;
            // 
            // boogy3
            // 
            this.boogy3.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boogy3.ForeColor = System.Drawing.Color.Magenta;
            this.boogy3.Location = new System.Drawing.Point(122, 87);
            this.boogy3.MaxLength = 14;
            this.boogy3.Name = "boogy3";
            this.boogy3.Size = new System.Drawing.Size(144, 23);
            this.boogy3.TabIndex = 3;
            // 
            // boogy4
            // 
            this.boogy4.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boogy4.ForeColor = System.Drawing.Color.Magenta;
            this.boogy4.Location = new System.Drawing.Point(122, 114);
            this.boogy4.MaxLength = 14;
            this.boogy4.Name = "boogy4";
            this.boogy4.Size = new System.Drawing.Size(144, 23);
            this.boogy4.TabIndex = 3;
            // 
            // boogy12
            // 
            this.boogy12.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boogy12.ForeColor = System.Drawing.Color.Magenta;
            this.boogy12.Location = new System.Drawing.Point(122, 169);
            this.boogy12.MaxLength = 14;
            this.boogy12.Name = "boogy12";
            this.boogy12.Size = new System.Drawing.Size(144, 23);
            this.boogy12.TabIndex = 3;
            // 
            // boogy13
            // 
            this.boogy13.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boogy13.ForeColor = System.Drawing.Color.Magenta;
            this.boogy13.Location = new System.Drawing.Point(122, 196);
            this.boogy13.MaxLength = 14;
            this.boogy13.Name = "boogy13";
            this.boogy13.Size = new System.Drawing.Size(144, 23);
            this.boogy13.TabIndex = 3;
            // 
            // boogy14
            // 
            this.boogy14.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boogy14.ForeColor = System.Drawing.Color.Magenta;
            this.boogy14.Location = new System.Drawing.Point(122, 224);
            this.boogy14.MaxLength = 14;
            this.boogy14.Name = "boogy14";
            this.boogy14.Size = new System.Drawing.Size(144, 23);
            this.boogy14.TabIndex = 3;
            // 
            // boogy15
            // 
            this.boogy15.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boogy15.ForeColor = System.Drawing.Color.Magenta;
            this.boogy15.Location = new System.Drawing.Point(122, 251);
            this.boogy15.MaxLength = 50;
            this.boogy15.Name = "boogy15";
            this.boogy15.Size = new System.Drawing.Size(144, 23);
            this.boogy15.TabIndex = 3;
            // 
            // boogy16
            // 
            this.boogy16.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boogy16.ForeColor = System.Drawing.Color.Magenta;
            this.boogy16.Location = new System.Drawing.Point(58, 278);
            this.boogy16.MaxLength = 14;
            this.boogy16.Name = "boogy16";
            this.boogy16.Size = new System.Drawing.Size(81, 23);
            this.boogy16.TabIndex = 3;
            // 
            // boogy17
            // 
            this.boogy17.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boogy17.ForeColor = System.Drawing.Color.Magenta;
            this.boogy17.Location = new System.Drawing.Point(190, 279);
            this.boogy17.MaxLength = 14;
            this.boogy17.Name = "boogy17";
            this.boogy17.Size = new System.Drawing.Size(52, 23);
            this.boogy17.TabIndex = 3;
            // 
            // boogy18
            // 
            this.boogy18.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boogy18.ForeColor = System.Drawing.Color.Magenta;
            this.boogy18.Location = new System.Drawing.Point(301, 278);
            this.boogy18.MaxLength = 14;
            this.boogy18.Name = "boogy18";
            this.boogy18.Size = new System.Drawing.Size(52, 23);
            this.boogy18.TabIndex = 3;
            // 
            // button12
            // 
            this.button12.Location = new System.Drawing.Point(212, 524);
            this.button12.Name = "button12";
            this.button12.Size = new System.Drawing.Size(75, 28);
            this.button12.TabIndex = 4;
            this.button12.Text = "Clear";
            this.button12.UseVisualStyleBackColor = true;
            this.button12.Click += new System.EventHandler(this.button12_Click);
            // 
            // button13
            // 
            this.button13.Location = new System.Drawing.Point(43, 525);
            this.button13.Name = "button13";
            this.button13.Size = new System.Drawing.Size(114, 28);
            this.button13.TabIndex = 4;
            this.button13.Text = "Add Booth";
            this.button13.UseVisualStyleBackColor = true;
            this.button13.Click += new System.EventHandler(this.button13_Click);
            // 
            // boogy19
            // 
            this.boogy19.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boogy19.ForeColor = System.Drawing.Color.Magenta;
            this.boogy19.Location = new System.Drawing.Point(76, 308);
            this.boogy19.MaxLength = 100;
            this.boogy19.Name = "boogy19";
            this.boogy19.Size = new System.Drawing.Size(283, 23);
            this.boogy19.TabIndex = 3;
            // 
            // boogy110
            // 
            this.boogy110.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boogy110.ForeColor = System.Drawing.Color.Magenta;
            this.boogy110.Location = new System.Drawing.Point(76, 335);
            this.boogy110.MaxLength = 100;
            this.boogy110.Name = "boogy110";
            this.boogy110.Size = new System.Drawing.Size(283, 23);
            this.boogy110.TabIndex = 3;
            // 
            // boogy111
            // 
            this.boogy111.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boogy111.ForeColor = System.Drawing.Color.Magenta;
            this.boogy111.Location = new System.Drawing.Point(76, 361);
            this.boogy111.MaxLength = 100;
            this.boogy111.Name = "boogy111";
            this.boogy111.Size = new System.Drawing.Size(283, 23);
            this.boogy111.TabIndex = 3;
            // 
            // boogy112
            // 
            this.boogy112.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boogy112.ForeColor = System.Drawing.Color.Magenta;
            this.boogy112.Location = new System.Drawing.Point(76, 387);
            this.boogy112.MaxLength = 100;
            this.boogy112.Name = "boogy112";
            this.boogy112.Size = new System.Drawing.Size(283, 23);
            this.boogy112.TabIndex = 3;
            // 
            // boogy113
            // 
            this.boogy113.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boogy113.ForeColor = System.Drawing.Color.Magenta;
            this.boogy113.Location = new System.Drawing.Point(76, 413);
            this.boogy113.MaxLength = 100;
            this.boogy113.Name = "boogy113";
            this.boogy113.Size = new System.Drawing.Size(283, 23);
            this.boogy113.TabIndex = 3;
            // 
            // boogy114
            // 
            this.boogy114.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boogy114.ForeColor = System.Drawing.Color.Magenta;
            this.boogy114.Location = new System.Drawing.Point(76, 440);
            this.boogy114.MaxLength = 100;
            this.boogy114.Name = "boogy114";
            this.boogy114.Size = new System.Drawing.Size(283, 23);
            this.boogy114.TabIndex = 3;
            // 
            // boogy115
            // 
            this.boogy115.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boogy115.ForeColor = System.Drawing.Color.Magenta;
            this.boogy115.Location = new System.Drawing.Point(76, 466);
            this.boogy115.MaxLength = 100;
            this.boogy115.Name = "boogy115";
            this.boogy115.Size = new System.Drawing.Size(283, 23);
            this.boogy115.TabIndex = 3;
            // 
            // boogy116
            // 
            this.boogy116.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boogy116.ForeColor = System.Drawing.Color.Magenta;
            this.boogy116.Location = new System.Drawing.Point(76, 493);
            this.boogy116.MaxLength = 100;
            this.boogy116.Name = "boogy116";
            this.boogy116.Size = new System.Drawing.Size(283, 23);
            this.boogy116.TabIndex = 3;
            // 
            // ahmed41
            // 
            this.ahmed41.AutoSize = true;
            this.ahmed41.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed41.Location = new System.Drawing.Point(21, 145);
            this.ahmed41.Name = "ahmed41";
            this.ahmed41.Size = new System.Drawing.Size(74, 16);
            this.ahmed41.TabIndex = 2;
            this.ahmed41.Text = "BoothLevel:";
            // 
            // ahmed46
            // 
            this.ahmed46.AutoSize = true;
            this.ahmed46.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed46.ForeColor = System.Drawing.SystemColors.MenuHighlight;
            this.ahmed46.Location = new System.Drawing.Point(10, 171);
            this.ahmed46.Name = "ahmed46";
            this.ahmed46.Size = new System.Drawing.Size(86, 16);
            this.ahmed46.TabIndex = 2;
            this.ahmed46.Text = "BoothWeapR:";
            // 
            // ahmed48
            // 
            this.ahmed48.AutoSize = true;
            this.ahmed48.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed48.Location = new System.Drawing.Point(12, 196);
            this.ahmed48.Name = "ahmed48";
            this.ahmed48.Size = new System.Drawing.Size(84, 16);
            this.ahmed48.TabIndex = 2;
            this.ahmed48.Text = "BoothWeapL:";
            // 
            // ahmed49
            // 
            this.ahmed49.AutoSize = true;
            this.ahmed49.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed49.Location = new System.Drawing.Point(5, 226);
            this.ahmed49.Name = "ahmed49";
            this.ahmed49.Size = new System.Drawing.Size(94, 16);
            this.ahmed49.TabIndex = 2;
            this.ahmed49.Text = "BoothGarment:";
            // 
            // ahmed51
            // 
            this.ahmed51.AutoSize = true;
            this.ahmed51.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed51.Location = new System.Drawing.Point(7, 282);
            this.ahmed51.Name = "ahmed51";
            this.ahmed51.Size = new System.Drawing.Size(49, 16);
            this.ahmed51.TabIndex = 2;
            this.ahmed51.Text = "MapID:";
            // 
            // ahmed52
            // 
            this.ahmed52.AutoSize = true;
            this.ahmed52.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed52.Location = new System.Drawing.Point(145, 282);
            this.ahmed52.Name = "ahmed52";
            this.ahmed52.Size = new System.Drawing.Size(45, 16);
            this.ahmed52.TabIndex = 2;
            this.ahmed52.Text = "MapX:";
            // 
            // ahmed53
            // 
            this.ahmed53.AutoSize = true;
            this.ahmed53.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed53.Location = new System.Drawing.Point(253, 281);
            this.ahmed53.Name = "ahmed53";
            this.ahmed53.Size = new System.Drawing.Size(44, 16);
            this.ahmed53.TabIndex = 2;
            this.ahmed53.Text = "MapY:";
            // 
            // ahmed54
            // 
            this.ahmed54.AutoSize = true;
            this.ahmed54.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed54.Location = new System.Drawing.Point(11, 312);
            this.ahmed54.Name = "ahmed54";
            this.ahmed54.Size = new System.Drawing.Size(58, 16);
            this.ahmed54.TabIndex = 2;
            this.ahmed54.Text = "BItemID:";
            // 
            // ahmed55
            // 
            this.ahmed55.AutoSize = true;
            this.ahmed55.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed55.Location = new System.Drawing.Point(3, 337);
            this.ahmed55.Name = "ahmed55";
            this.ahmed55.Size = new System.Drawing.Size(69, 16);
            this.ahmed55.TabIndex = 2;
            this.ahmed55.Text = "BItemPlus:";
            // 
            // ahmed56
            // 
            this.ahmed56.AutoSize = true;
            this.ahmed56.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed56.Location = new System.Drawing.Point(6, 363);
            this.ahmed56.Name = "ahmed56";
            this.ahmed56.Size = new System.Drawing.Size(65, 16);
            this.ahmed56.TabIndex = 2;
            this.ahmed56.Text = "BEnchant:";
            // 
            // ahmed57
            // 
            this.ahmed57.AutoSize = true;
            this.ahmed57.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed57.Location = new System.Drawing.Point(14, 388);
            this.ahmed57.Name = "ahmed57";
            this.ahmed57.Size = new System.Drawing.Size(49, 16);
            this.ahmed57.TabIndex = 2;
            this.ahmed57.Text = "BBless:";
            // 
            // ahmed58
            // 
            this.ahmed58.AutoSize = true;
            this.ahmed58.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed58.Location = new System.Drawing.Point(16, 414);
            this.ahmed58.Name = "ahmed58";
            this.ahmed58.Size = new System.Drawing.Size(48, 16);
            this.ahmed58.TabIndex = 2;
            this.ahmed58.Text = "BSoc1:";
            // 
            // ahmed59
            // 
            this.ahmed59.AutoSize = true;
            this.ahmed59.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed59.Location = new System.Drawing.Point(16, 442);
            this.ahmed59.Name = "ahmed59";
            this.ahmed59.Size = new System.Drawing.Size(48, 16);
            this.ahmed59.TabIndex = 2;
            this.ahmed59.Text = "BSoc2:";
            // 
            // ahmed60
            // 
            this.ahmed60.AutoSize = true;
            this.ahmed60.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed60.Location = new System.Drawing.Point(18, 466);
            this.ahmed60.Name = "ahmed60";
            this.ahmed60.Size = new System.Drawing.Size(45, 16);
            this.ahmed60.TabIndex = 2;
            this.ahmed60.Text = "BCost:";
            // 
            // ahmed61
            // 
            this.ahmed61.AutoSize = true;
            this.ahmed61.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed61.Location = new System.Drawing.Point(10, 495);
            this.ahmed61.Name = "ahmed61";
            this.ahmed61.Size = new System.Drawing.Size(53, 16);
            this.ahmed61.TabIndex = 2;
            this.ahmed61.Text = "BCount:";
            // 
            // ahmed50
            // 
            this.ahmed50.AutoSize = true;
            this.ahmed50.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed50.Location = new System.Drawing.Point(5, 255);
            this.ahmed50.Name = "ahmed50";
            this.ahmed50.Size = new System.Drawing.Size(95, 16);
            this.ahmed50.TabIndex = 2;
            this.ahmed50.Text = "BoothMessage:";
            // 
            // ahmed42
            // 
            this.ahmed42.AutoSize = true;
            this.ahmed42.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ahmed42.Location = new System.Drawing.Point(19, 90);
            this.ahmed42.Name = "ahmed42";
            this.ahmed42.Size = new System.Drawing.Size(78, 16);
            this.ahmed42.TabIndex = 0;
            this.ahmed42.Text = "BoothName:";
            // 
            // textBox_0
            // 
            this.textBox_0.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBox_0.ForeColor = System.Drawing.Color.Magenta;
            this.textBox_0.Location = new System.Drawing.Point(122, 114);
            this.textBox_0.Name = "textBox_0";
            this.textBox_0.Size = new System.Drawing.Size(144, 23);
            this.textBox_0.TabIndex = 12;
            // 
            // textBox_1
            // 
            this.textBox_1.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBox_1.ForeColor = System.Drawing.Color.DarkViolet;
            this.textBox_1.Location = new System.Drawing.Point(122, 57);
            this.textBox_1.Name = "textBox_1";
            this.textBox_1.Size = new System.Drawing.Size(144, 23);
            this.textBox_1.TabIndex = 9;
            // 
            // button9
            // 
            this.button9.Location = new System.Drawing.Point(282, 19);
            this.button9.Name = "button9";
            this.button9.Size = new System.Drawing.Size(75, 26);
            this.button9.TabIndex = 7;
            this.button9.Text = "Refresh";
            this.button9.UseVisualStyleBackColor = true;
            this.button9.Click += new System.EventHandler(this.button9_Click);
            // 
            // comboBox1
            // 
            this.comboBox1.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.comboBox1.ForeColor = System.Drawing.Color.Red;
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Location = new System.Drawing.Point(127, 24);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(144, 24);
            this.comboBox1.TabIndex = 6;
            this.comboBox1.SelectedIndexChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged);
            // 
            // button11
            // 
            this.button11.Location = new System.Drawing.Point(194, 526);
            this.button11.Name = "button11";
            this.button11.Size = new System.Drawing.Size(75, 28);
            this.button11.TabIndex = 4;
            this.button11.Text = "Clear";
            this.button11.UseVisualStyleBackColor = true;
            this.button11.Click += new System.EventHandler(this.button11_Click);
            // 
            // button10
            // 
            this.button10.Location = new System.Drawing.Point(76, 527);
            this.button10.Name = "button10";
            this.button10.Size = new System.Drawing.Size(75, 28);
            this.button10.TabIndex = 4;
            this.button10.Text = "Save";
            this.button10.UseVisualStyleBackColor = true;
            this.button10.Click += new System.EventHandler(this.button10_Click);
            // 
            // textBox_2
            // 
            this.textBox_2.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBox_2.ForeColor = System.Drawing.Color.Blue;
            this.textBox_2.Location = new System.Drawing.Point(122, 86);
            this.textBox_2.Name = "textBox_2";
            this.textBox_2.Size = new System.Drawing.Size(144, 23);
            this.textBox_2.TabIndex = 1;
            // 
            // groupBox_0
            // 
            this.groupBox_0.Controls.Add(this.textBox_0);
            this.groupBox_0.Controls.Add(this.label38);
            this.groupBox_0.Controls.Add(this.label39);
            this.groupBox_0.Controls.Add(this.textBox_1);
            this.groupBox_0.Controls.Add(this.label40);
            this.groupBox_0.Controls.Add(this.button9);
            this.groupBox_0.Controls.Add(this.comboBox1);
            this.groupBox_0.Controls.Add(this.button10);
            this.groupBox_0.Controls.Add(this.button11);
            this.groupBox_0.Controls.Add(this.defNdEaiis);
            this.groupBox_0.Controls.Add(this.defNdEaiis2);
            this.groupBox_0.Controls.Add(this.defNdEaiis3);
            this.groupBox_0.Controls.Add(this.defNdEaiis4);
            this.groupBox_0.Controls.Add(this.defNdEaiis5);
            this.groupBox_0.Controls.Add(this.defNdEaiis6);
            this.groupBox_0.Controls.Add(this.defNdEaiis7);
            this.groupBox_0.Controls.Add(this.defNdEaiis8);
            this.groupBox_0.Controls.Add(this.defNdEaiis9);
            this.groupBox_0.Controls.Add(this.defNdEaiis10);
            this.groupBox_0.Controls.Add(this.defNdEaiis11);
            this.groupBox_0.Controls.Add(this.defNdEaiis12);
            this.groupBox_0.Controls.Add(this.defNdEaiis13);
            this.groupBox_0.Controls.Add(this.defNdEaiis14);
            this.groupBox_0.Controls.Add(this.defNdEaiis15);
            this.groupBox_0.Controls.Add(this.defNdEaiis16);
            this.groupBox_0.Controls.Add(this.label41);
            this.groupBox_0.Controls.Add(this.label46);
            this.groupBox_0.Controls.Add(this.label47);
            this.groupBox_0.Controls.Add(this.label48);
            this.groupBox_0.Controls.Add(this.label49);
            this.groupBox_0.Controls.Add(this.label51);
            this.groupBox_0.Controls.Add(this.label52);
            this.groupBox_0.Controls.Add(this.label53);
            this.groupBox_0.Controls.Add(this.label54);
            this.groupBox_0.Controls.Add(this.label55);
            this.groupBox_0.Controls.Add(this.label56);
            this.groupBox_0.Controls.Add(this.label57);
            this.groupBox_0.Controls.Add(this.label58);
            this.groupBox_0.Controls.Add(this.label59);
            this.groupBox_0.Controls.Add(this.label60);
            this.groupBox_0.Controls.Add(this.label61);
            this.groupBox_0.Controls.Add(this.label50);
            this.groupBox_0.Controls.Add(this.textBox_2);
            this.groupBox_0.Controls.Add(this.label42);
            this.groupBox_0.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox_0.ForeColor = System.Drawing.SystemColors.MenuHighlight;
            this.groupBox_0.Location = new System.Drawing.Point(3, 12);
            this.groupBox_0.Name = "groupBox_0";
            this.groupBox_0.Size = new System.Drawing.Size(382, 580);
            this.groupBox_0.TabIndex = 56;
            this.groupBox_0.TabStop = false;
            this.groupBox_0.Text = "Modify Booth ";
            // 
            // label38
            // 
            this.label38.AutoSize = true;
            this.label38.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label38.Location = new System.Drawing.Point(23, 118);
            this.label38.Name = "label38";
            this.label38.Size = new System.Drawing.Size(72, 16);
            this.label38.TabIndex = 11;
            this.label38.Text = "BoothBody:";
            // 
            // label39
            // 
            this.label39.AutoSize = true;
            this.label39.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label39.ForeColor = System.Drawing.Color.Red;
            this.label39.Location = new System.Drawing.Point(209, 35);
            this.label39.Name = "label39";
            this.label39.Size = new System.Drawing.Size(0, 16);
            this.label39.TabIndex = 10;
            // 
            // label40
            // 
            this.label40.AutoSize = true;
            this.label40.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label40.Location = new System.Drawing.Point(40, 60);
            this.label40.Name = "label40";
            this.label40.Size = new System.Drawing.Size(57, 16);
            this.label40.TabIndex = 8;
            this.label40.Text = "BoothID:";
            // 
            // defNdEaiis
            // 
            this.defNdEaiis.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.defNdEaiis.ForeColor = System.Drawing.Color.Magenta;
            this.defNdEaiis.Location = new System.Drawing.Point(122, 141);
            this.defNdEaiis.MaxLength = 14;
            this.defNdEaiis.Name = "defNdEaiis";
            this.defNdEaiis.Size = new System.Drawing.Size(144, 23);
            this.defNdEaiis.TabIndex = 3;
            // 
            // defNdEaiis2
            // 
            this.defNdEaiis2.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.defNdEaiis2.ForeColor = System.Drawing.Color.Magenta;
            this.defNdEaiis2.Location = new System.Drawing.Point(122, 169);
            this.defNdEaiis2.MaxLength = 14;
            this.defNdEaiis2.Name = "defNdEaiis2";
            this.defNdEaiis2.Size = new System.Drawing.Size(144, 23);
            this.defNdEaiis2.TabIndex = 3;
            // 
            // defNdEaiis3
            // 
            this.defNdEaiis3.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.defNdEaiis3.ForeColor = System.Drawing.Color.Magenta;
            this.defNdEaiis3.Location = new System.Drawing.Point(122, 196);
            this.defNdEaiis3.MaxLength = 14;
            this.defNdEaiis3.Name = "defNdEaiis3";
            this.defNdEaiis3.Size = new System.Drawing.Size(144, 23);
            this.defNdEaiis3.TabIndex = 3;
            // 
            // defNdEaiis4
            // 
            this.defNdEaiis4.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.defNdEaiis4.ForeColor = System.Drawing.Color.Magenta;
            this.defNdEaiis4.Location = new System.Drawing.Point(122, 224);
            this.defNdEaiis4.MaxLength = 14;
            this.defNdEaiis4.Name = "defNdEaiis4";
            this.defNdEaiis4.Size = new System.Drawing.Size(144, 23);
            this.defNdEaiis4.TabIndex = 3;
            // 
            // defNdEaiis5
            // 
            this.defNdEaiis5.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.defNdEaiis5.ForeColor = System.Drawing.Color.Magenta;
            this.defNdEaiis5.Location = new System.Drawing.Point(122, 251);
            this.defNdEaiis5.MaxLength = 50;
            this.defNdEaiis5.Name = "defNdEaiis5";
            this.defNdEaiis5.Size = new System.Drawing.Size(144, 23);
            this.defNdEaiis5.TabIndex = 3;
            // 
            // defNdEaiis6
            // 
            this.defNdEaiis6.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.defNdEaiis6.ForeColor = System.Drawing.Color.Magenta;
            this.defNdEaiis6.Location = new System.Drawing.Point(58, 278);
            this.defNdEaiis6.MaxLength = 14;
            this.defNdEaiis6.Name = "defNdEaiis6";
            this.defNdEaiis6.Size = new System.Drawing.Size(81, 23);
            this.defNdEaiis6.TabIndex = 3;
            // 
            // defNdEaiis7
            // 
            this.defNdEaiis7.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.defNdEaiis7.ForeColor = System.Drawing.Color.Magenta;
            this.defNdEaiis7.Location = new System.Drawing.Point(190, 279);
            this.defNdEaiis7.MaxLength = 14;
            this.defNdEaiis7.Name = "defNdEaiis7";
            this.defNdEaiis7.Size = new System.Drawing.Size(52, 23);
            this.defNdEaiis7.TabIndex = 3;
            // 
            // defNdEaiis8
            // 
            this.defNdEaiis8.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.defNdEaiis8.ForeColor = System.Drawing.Color.Magenta;
            this.defNdEaiis8.Location = new System.Drawing.Point(301, 278);
            this.defNdEaiis8.MaxLength = 14;
            this.defNdEaiis8.Name = "defNdEaiis8";
            this.defNdEaiis8.Size = new System.Drawing.Size(52, 23);
            this.defNdEaiis8.TabIndex = 3;
            // 
            // defNdEaiis9
            // 
            this.defNdEaiis9.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.defNdEaiis9.ForeColor = System.Drawing.Color.Magenta;
            this.defNdEaiis9.Location = new System.Drawing.Point(76, 308);
            this.defNdEaiis9.MaxLength = 100;
            this.defNdEaiis9.Name = "defNdEaiis9";
            this.defNdEaiis9.Size = new System.Drawing.Size(283, 23);
            this.defNdEaiis9.TabIndex = 3;
            // 
            // defNdEaiis10
            // 
            this.defNdEaiis10.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.defNdEaiis10.ForeColor = System.Drawing.Color.Magenta;
            this.defNdEaiis10.Location = new System.Drawing.Point(76, 335);
            this.defNdEaiis10.MaxLength = 100;
            this.defNdEaiis10.Name = "defNdEaiis10";
            this.defNdEaiis10.Size = new System.Drawing.Size(283, 23);
            this.defNdEaiis10.TabIndex = 3;
            // 
            // defNdEaiis11
            // 
            this.defNdEaiis11.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.defNdEaiis11.ForeColor = System.Drawing.Color.Magenta;
            this.defNdEaiis11.Location = new System.Drawing.Point(76, 361);
            this.defNdEaiis11.MaxLength = 100;
            this.defNdEaiis11.Name = "defNdEaiis11";
            this.defNdEaiis11.Size = new System.Drawing.Size(283, 23);
            this.defNdEaiis11.TabIndex = 3;
            // 
            // defNdEaiis12
            // 
            this.defNdEaiis12.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.defNdEaiis12.ForeColor = System.Drawing.Color.Magenta;
            this.defNdEaiis12.Location = new System.Drawing.Point(76, 387);
            this.defNdEaiis12.MaxLength = 100;
            this.defNdEaiis12.Name = "defNdEaiis12";
            this.defNdEaiis12.Size = new System.Drawing.Size(283, 23);
            this.defNdEaiis12.TabIndex = 3;
            // 
            // defNdEaiis13
            // 
            this.defNdEaiis13.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.defNdEaiis13.ForeColor = System.Drawing.Color.Magenta;
            this.defNdEaiis13.Location = new System.Drawing.Point(76, 413);
            this.defNdEaiis13.MaxLength = 100;
            this.defNdEaiis13.Name = "defNdEaiis13";
            this.defNdEaiis13.Size = new System.Drawing.Size(283, 23);
            this.defNdEaiis13.TabIndex = 3;
            // 
            // defNdEaiis14
            // 
            this.defNdEaiis14.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.defNdEaiis14.ForeColor = System.Drawing.Color.Magenta;
            this.defNdEaiis14.Location = new System.Drawing.Point(76, 440);
            this.defNdEaiis14.MaxLength = 100;
            this.defNdEaiis14.Name = "defNdEaiis14";
            this.defNdEaiis14.Size = new System.Drawing.Size(283, 23);
            this.defNdEaiis14.TabIndex = 3;
            // 
            // defNdEaiis15
            // 
            this.defNdEaiis15.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.defNdEaiis15.ForeColor = System.Drawing.Color.Magenta;
            this.defNdEaiis15.Location = new System.Drawing.Point(76, 466);
            this.defNdEaiis15.MaxLength = 100;
            this.defNdEaiis15.Name = "defNdEaiis15";
            this.defNdEaiis15.Size = new System.Drawing.Size(283, 23);
            this.defNdEaiis15.TabIndex = 3;
            // 
            // defNdEaiis16
            // 
            this.defNdEaiis16.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.defNdEaiis16.ForeColor = System.Drawing.Color.Magenta;
            this.defNdEaiis16.Location = new System.Drawing.Point(76, 493);
            this.defNdEaiis16.MaxLength = 100;
            this.defNdEaiis16.Name = "defNdEaiis16";
            this.defNdEaiis16.Size = new System.Drawing.Size(283, 23);
            this.defNdEaiis16.TabIndex = 3;
            // 
            // label41
            // 
            this.label41.AutoSize = true;
            this.label41.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label41.Location = new System.Drawing.Point(21, 145);
            this.label41.Name = "label41";
            this.label41.Size = new System.Drawing.Size(74, 16);
            this.label41.TabIndex = 2;
            this.label41.Text = "BoothLevel:";
            // 
            // label46
            // 
            this.label46.AutoSize = true;
            this.label46.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label46.ForeColor = System.Drawing.SystemColors.MenuHighlight;
            this.label46.Location = new System.Drawing.Point(10, 171);
            this.label46.Name = "label46";
            this.label46.Size = new System.Drawing.Size(86, 16);
            this.label46.TabIndex = 2;
            this.label46.Text = "BoothWeapR:";
            // 
            // label47
            // 
            this.label47.AutoSize = true;
            this.label47.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label47.Location = new System.Drawing.Point(1, 26);
            this.label47.Name = "label47";
            this.label47.Size = new System.Drawing.Size(122, 16);
            this.label47.TabIndex = 2;
            this.label47.Text = "SelectBoothFromID:";
            // 
            // label48
            // 
            this.label48.AutoSize = true;
            this.label48.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label48.Location = new System.Drawing.Point(12, 196);
            this.label48.Name = "label48";
            this.label48.Size = new System.Drawing.Size(84, 16);
            this.label48.TabIndex = 2;
            this.label48.Text = "BoothWeapL:";
            // 
            // label49
            // 
            this.label49.AutoSize = true;
            this.label49.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label49.Location = new System.Drawing.Point(5, 226);
            this.label49.Name = "label49";
            this.label49.Size = new System.Drawing.Size(94, 16);
            this.label49.TabIndex = 2;
            this.label49.Text = "BoothGarment:";
            // 
            // label51
            // 
            this.label51.AutoSize = true;
            this.label51.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label51.Location = new System.Drawing.Point(7, 282);
            this.label51.Name = "label51";
            this.label51.Size = new System.Drawing.Size(49, 16);
            this.label51.TabIndex = 2;
            this.label51.Text = "MapID:";
            // 
            // label52
            // 
            this.label52.AutoSize = true;
            this.label52.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label52.Location = new System.Drawing.Point(145, 282);
            this.label52.Name = "label52";
            this.label52.Size = new System.Drawing.Size(45, 16);
            this.label52.TabIndex = 2;
            this.label52.Text = "MapX:";
            // 
            // label53
            // 
            this.label53.AutoSize = true;
            this.label53.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label53.Location = new System.Drawing.Point(253, 281);
            this.label53.Name = "label53";
            this.label53.Size = new System.Drawing.Size(44, 16);
            this.label53.TabIndex = 2;
            this.label53.Text = "MapY:";
            // 
            // label54
            // 
            this.label54.AutoSize = true;
            this.label54.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label54.Location = new System.Drawing.Point(11, 312);
            this.label54.Name = "label54";
            this.label54.Size = new System.Drawing.Size(58, 16);
            this.label54.TabIndex = 2;
            this.label54.Text = "BItemID:";
            // 
            // label55
            // 
            this.label55.AutoSize = true;
            this.label55.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label55.Location = new System.Drawing.Point(3, 337);
            this.label55.Name = "label55";
            this.label55.Size = new System.Drawing.Size(69, 16);
            this.label55.TabIndex = 2;
            this.label55.Text = "BItemPlus:";
            // 
            // label56
            // 
            this.label56.AutoSize = true;
            this.label56.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label56.Location = new System.Drawing.Point(6, 363);
            this.label56.Name = "label56";
            this.label56.Size = new System.Drawing.Size(65, 16);
            this.label56.TabIndex = 2;
            this.label56.Text = "BEnchant:";
            // 
            // label57
            // 
            this.label57.AutoSize = true;
            this.label57.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label57.Location = new System.Drawing.Point(14, 388);
            this.label57.Name = "label57";
            this.label57.Size = new System.Drawing.Size(49, 16);
            this.label57.TabIndex = 2;
            this.label57.Text = "BBless:";
            // 
            // label58
            // 
            this.label58.AutoSize = true;
            this.label58.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label58.Location = new System.Drawing.Point(16, 414);
            this.label58.Name = "label58";
            this.label58.Size = new System.Drawing.Size(48, 16);
            this.label58.TabIndex = 2;
            this.label58.Text = "BSoc1:";
            // 
            // label59
            // 
            this.label59.AutoSize = true;
            this.label59.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label59.Location = new System.Drawing.Point(16, 442);
            this.label59.Name = "label59";
            this.label59.Size = new System.Drawing.Size(48, 16);
            this.label59.TabIndex = 2;
            this.label59.Text = "BSoc2:";
            // 
            // label60
            // 
            this.label60.AutoSize = true;
            this.label60.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label60.Location = new System.Drawing.Point(18, 466);
            this.label60.Name = "label60";
            this.label60.Size = new System.Drawing.Size(45, 16);
            this.label60.TabIndex = 2;
            this.label60.Text = "BCost:";
            // 
            // label61
            // 
            this.label61.AutoSize = true;
            this.label61.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label61.Location = new System.Drawing.Point(10, 495);
            this.label61.Name = "label61";
            this.label61.Size = new System.Drawing.Size(53, 16);
            this.label61.TabIndex = 2;
            this.label61.Text = "BCount:";
            // 
            // label50
            // 
            this.label50.AutoSize = true;
            this.label50.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label50.Location = new System.Drawing.Point(5, 255);
            this.label50.Name = "label50";
            this.label50.Size = new System.Drawing.Size(95, 16);
            this.label50.TabIndex = 2;
            this.label50.Text = "BoothMessage:";
            // 
            // label42
            // 
            this.label42.AutoSize = true;
            this.label42.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label42.Location = new System.Drawing.Point(19, 90);
            this.label42.Name = "label42";
            this.label42.Size = new System.Drawing.Size(78, 16);
            this.label42.TabIndex = 0;
            this.label42.Text = "BoothName:";
            // 
            // Accounts
            // 
            this.ClientSize = new System.Drawing.Size(813, 593);
            this.Controls.Add(this.groupBox_1);
            this.Controls.Add(this.groupBox_0);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Accounts";
            this.Text = "Add Booth And Modify";
            this.groupBox_1.ResumeLayout(false);
            this.groupBox_1.PerformLayout();
            this.groupBox_0.ResumeLayout(false);
            this.groupBox_0.PerformLayout();
            this.ResumeLayout(false);

        }
    }
}


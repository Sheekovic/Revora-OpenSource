// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using SubSonic;
using System.Xml;
using System.Text;
using System.Data;
using System.Data.Common;
using System.Collections;
using SubSonic.Utilities;
using System.Configuration;
using System.Data.SqlClient;
using System.ComponentModel;
using System.Xml.Serialization;
using System.Collections.Generic;

namespace Conquer.Database
{
    [Serializable]
    public partial class ItemCollection : ActiveList<Item, ItemCollection>
    {
        public ItemCollection() { }
        public ItemCollection Filter()
        {
            for (int i = this.Count - 1; i > -1; i--)
            {
                Item o = this[i];
                foreach (SubSonic.Where w in this.wheres)
                {
                    bool remove = false;
                    System.Reflection.PropertyInfo pi = o.GetType().GetProperty(w.ColumnName);
                    if (pi.CanRead)
                    {
                        object val = pi.GetValue(o, null);
                        switch (w.Comparison)
                        {
                            case SubSonic.Comparison.Equals:
                                if (!val.Equals(w.ParameterValue))
                                {
                                    remove = true;
                                }
                                break;
                        }
                    }
                    if (remove)
                    {
                        this.Remove(o);
                        break;
                    }
                }
            }
            return this;
        }
    }
    [Serializable]
    public partial class Item : ActiveRecord<Item>, IActiveRecord
    {
        #region .ctors and Default Settings

        public Item()
        {
            SetSQLProps();
            InitSetDefaults();
            MarkNew();
        }

        private void InitSetDefaults() { SetDefaults(); }

        public Item(bool useDatabaseDefaults)
        {
            SetSQLProps();
            if (useDatabaseDefaults)
                ForceDefaults();
            MarkNew();
        }

        public Item(object keyID)
        {
            SetSQLProps();
            InitSetDefaults();
            LoadByKey(keyID);
        }

        public Item(string columnName, object columnValue)
        {
            SetSQLProps();
            InitSetDefaults();
            LoadByParam(columnName, columnValue);
        }

        protected static void SetSQLProps() { GetTableSchema(); }

        #endregion
        #region Schema and Query Accessor
        public static Query CreateQuery() { return new Query(Schema); }
        public static TableSchema.Table Schema
        {
            get
            {
                if (BaseSchema == null)
                    SetSQLProps();
                return BaseSchema;
            }
        }

        private static void GetTableSchema()
        {
            if (!IsSchemaInitialized)
            {
                //Schema declaration
                TableSchema.Table schema = new TableSchema.Table("items", TableType.Table, DataService.GetInstance("Conquer_Server"));
                schema.Columns = new TableSchema.TableColumnCollection();
                schema.SchemaName = @"";
                //columns

                TableSchema.TableColumn colvarEntityID = new TableSchema.TableColumn(schema);
                colvarEntityID.ColumnName = "EntityID";
                colvarEntityID.DataType = DbType.Int64;
                colvarEntityID.MaxLength = 0;
                colvarEntityID.AutoIncrement = false;
                colvarEntityID.IsNullable = false;
                colvarEntityID.IsPrimaryKey = false;
                colvarEntityID.IsForeignKey = false;
                colvarEntityID.IsReadOnly = false;
                colvarEntityID.DefaultSetting = @"";
                colvarEntityID.ForeignKeyTableName = "";
                schema.Columns.Add(colvarEntityID);

                TableSchema.TableColumn colvarId = new TableSchema.TableColumn(schema);
                colvarId.ColumnName = "ID";
                colvarId.DataType = DbType.String;
                colvarId.MaxLength = 0;
                colvarId.AutoIncrement = false;
                colvarId.IsNullable = false;
                colvarId.IsPrimaryKey = false;
                colvarId.IsForeignKey = false;
                colvarId.IsReadOnly = false;
                colvarId.DefaultSetting = @"";
                colvarId.ForeignKeyTableName = "";
                schema.Columns.Add(colvarId);

                TableSchema.TableColumn colvarUid = new TableSchema.TableColumn(schema);
                colvarUid.ColumnName = "UID";
                colvarUid.DataType = DbType.Int32;
                colvarUid.MaxLength = 0;
                colvarUid.AutoIncrement = false;
                colvarUid.IsNullable = false;
                colvarUid.IsPrimaryKey = true;
                colvarUid.IsForeignKey = false;
                colvarUid.IsReadOnly = false;
                colvarUid.DefaultSetting = @"";
                colvarUid.ForeignKeyTableName = "";
                schema.Columns.Add(colvarUid);

                TableSchema.TableColumn colvarPlus = new TableSchema.TableColumn(schema);
                colvarPlus.ColumnName = "Plus";
                colvarPlus.DataType = DbType.Int16;
                colvarPlus.MaxLength = 0;
                colvarPlus.AutoIncrement = false;
                colvarPlus.IsNullable = true;
                colvarPlus.IsPrimaryKey = false;
                colvarPlus.IsForeignKey = false;
                colvarPlus.IsReadOnly = false;
                colvarPlus.DefaultSetting = @"";
                colvarPlus.ForeignKeyTableName = "";
                schema.Columns.Add(colvarPlus);

                TableSchema.TableColumn colvarBless = new TableSchema.TableColumn(schema);
                colvarBless.ColumnName = "Bless";
                colvarBless.DataType = DbType.Int16;
                colvarBless.MaxLength = 0;
                colvarBless.AutoIncrement = false;
                colvarBless.IsNullable = true;
                colvarBless.IsPrimaryKey = false;
                colvarBless.IsForeignKey = false;
                colvarBless.IsReadOnly = false;
                colvarBless.DefaultSetting = @"";
                colvarBless.ForeignKeyTableName = "";
                schema.Columns.Add(colvarBless);

                TableSchema.TableColumn colvarEnchant = new TableSchema.TableColumn(schema);
                colvarEnchant.ColumnName = "Enchant";
                colvarEnchant.DataType = DbType.Int16;
                colvarEnchant.MaxLength = 0;
                colvarEnchant.AutoIncrement = false;
                colvarEnchant.IsNullable = false;
                colvarEnchant.IsPrimaryKey = false;
                colvarEnchant.IsForeignKey = false;
                colvarEnchant.IsReadOnly = false;
                colvarEnchant.DefaultSetting = @"";
                colvarEnchant.ForeignKeyTableName = "";
                schema.Columns.Add(colvarEnchant);

                TableSchema.TableColumn colvarSocketOne = new TableSchema.TableColumn(schema);
                colvarSocketOne.ColumnName = "SocketOne";
                colvarSocketOne.DataType = DbType.Int16;
                colvarSocketOne.MaxLength = 0;
                colvarSocketOne.AutoIncrement = false;
                colvarSocketOne.IsNullable = true;
                colvarSocketOne.IsPrimaryKey = false;
                colvarSocketOne.IsForeignKey = false;
                colvarSocketOne.IsReadOnly = false;
                colvarSocketOne.DefaultSetting = @"";
                colvarSocketOne.ForeignKeyTableName = "";
                schema.Columns.Add(colvarSocketOne);

                TableSchema.TableColumn colvarSocketTwo = new TableSchema.TableColumn(schema);
                colvarSocketTwo.ColumnName = "SocketTwo";
                colvarSocketTwo.DataType = DbType.Int16;
                colvarSocketTwo.MaxLength = 0;
                colvarSocketTwo.AutoIncrement = false;
                colvarSocketTwo.IsNullable = true;
                colvarSocketTwo.IsPrimaryKey = false;
                colvarSocketTwo.IsForeignKey = false;
                colvarSocketTwo.IsReadOnly = false;
                colvarSocketTwo.DefaultSetting = @"";
                colvarSocketTwo.ForeignKeyTableName = "";
                schema.Columns.Add(colvarSocketTwo);

                TableSchema.TableColumn colvarDurability = new TableSchema.TableColumn(schema);
                colvarDurability.ColumnName = "Durability";
                colvarDurability.DataType = DbType.String;
                colvarDurability.MaxLength = 0;
                colvarDurability.AutoIncrement = false;
                colvarDurability.IsNullable = false;
                colvarDurability.IsPrimaryKey = false;
                colvarDurability.IsForeignKey = false;
                colvarDurability.IsReadOnly = false;
                colvarDurability.DefaultSetting = @"";
                colvarDurability.ForeignKeyTableName = "";
                schema.Columns.Add(colvarDurability);

                TableSchema.TableColumn colvarMaximDurability = new TableSchema.TableColumn(schema);
                colvarMaximDurability.ColumnName = "MaximDurability";
                colvarMaximDurability.DataType = DbType.String;
                colvarMaximDurability.MaxLength = 0;
                colvarMaximDurability.AutoIncrement = false;
                colvarMaximDurability.IsNullable = false;
                colvarMaximDurability.IsPrimaryKey = false;
                colvarMaximDurability.IsForeignKey = false;
                colvarMaximDurability.IsReadOnly = false;
                colvarMaximDurability.DefaultSetting = @"";
                colvarMaximDurability.ForeignKeyTableName = "";
                schema.Columns.Add(colvarMaximDurability);

                TableSchema.TableColumn colvarSocketProgress = new TableSchema.TableColumn(schema);
                colvarSocketProgress.ColumnName = "SocketProgress";
                colvarSocketProgress.DataType = DbType.Int32;
                colvarSocketProgress.MaxLength = 0;
                colvarSocketProgress.AutoIncrement = false;
                colvarSocketProgress.IsNullable = true;
                colvarSocketProgress.IsPrimaryKey = false;
                colvarSocketProgress.IsForeignKey = false;
                colvarSocketProgress.IsReadOnly = false;
                colvarSocketProgress.DefaultSetting = @"";
                colvarSocketProgress.ForeignKeyTableName = "";
                schema.Columns.Add(colvarSocketProgress);

                TableSchema.TableColumn colvarPlusProgress = new TableSchema.TableColumn(schema);
                colvarPlusProgress.ColumnName = "PlusProgress";
                colvarPlusProgress.DataType = DbType.Int32;
                colvarPlusProgress.MaxLength = 0;
                colvarPlusProgress.AutoIncrement = false;
                colvarPlusProgress.IsNullable = true;
                colvarPlusProgress.IsPrimaryKey = false;
                colvarPlusProgress.IsForeignKey = false;
                colvarPlusProgress.IsReadOnly = false;
                colvarPlusProgress.DefaultSetting = @"";
                colvarPlusProgress.ForeignKeyTableName = "";
                schema.Columns.Add(colvarPlusProgress);

                TableSchema.TableColumn colvarEffect = new TableSchema.TableColumn(schema);
                colvarEffect.ColumnName = "Effect";
                colvarEffect.DataType = DbType.Int16;
                colvarEffect.MaxLength = 0;
                colvarEffect.AutoIncrement = false;
                colvarEffect.IsNullable = true;
                colvarEffect.IsPrimaryKey = false;
                colvarEffect.IsForeignKey = false;
                colvarEffect.IsReadOnly = false;
                colvarEffect.DefaultSetting = @"";
                colvarEffect.ForeignKeyTableName = "";
                schema.Columns.Add(colvarEffect);

                TableSchema.TableColumn colvarBound = new TableSchema.TableColumn(schema);
                colvarBound.ColumnName = "Bound";
                colvarBound.DataType = DbType.Int16;
                colvarBound.MaxLength = 0;
                colvarBound.AutoIncrement = false;
                colvarBound.IsNullable = true;
                colvarBound.IsPrimaryKey = false;
                colvarBound.IsForeignKey = false;
                colvarBound.IsReadOnly = false;
                colvarBound.DefaultSetting = @"";
                colvarBound.ForeignKeyTableName = "";
                schema.Columns.Add(colvarBound);

                TableSchema.TableColumn colvarLocked = new TableSchema.TableColumn(schema);
                colvarLocked.ColumnName = "Locked";
                colvarLocked.DataType = DbType.Int16;
                colvarLocked.MaxLength = 0;
                colvarLocked.AutoIncrement = false;
                colvarLocked.IsNullable = true;
                colvarLocked.IsPrimaryKey = false;
                colvarLocked.IsForeignKey = false;
                colvarLocked.IsReadOnly = false;
                colvarLocked.DefaultSetting = @"";
                colvarLocked.ForeignKeyTableName = "";
                schema.Columns.Add(colvarLocked);

                TableSchema.TableColumn colvarUnlockEnd = new TableSchema.TableColumn(schema);
                colvarUnlockEnd.ColumnName = "UnlockEnd";
                colvarUnlockEnd.DataType = DbType.Int64;
                colvarUnlockEnd.MaxLength = 0;
                colvarUnlockEnd.AutoIncrement = false;
                colvarUnlockEnd.IsNullable = true;
                colvarUnlockEnd.IsPrimaryKey = false;
                colvarUnlockEnd.IsForeignKey = false;
                colvarUnlockEnd.IsReadOnly = false;
                colvarUnlockEnd.DefaultSetting = @"";
                colvarUnlockEnd.ForeignKeyTableName = "";
                schema.Columns.Add(colvarUnlockEnd);

                TableSchema.TableColumn colvarSuspicious = new TableSchema.TableColumn(schema);
                colvarSuspicious.ColumnName = "Suspicious";
                colvarSuspicious.DataType = DbType.Int16;
                colvarSuspicious.MaxLength = 0;
                colvarSuspicious.AutoIncrement = false;
                colvarSuspicious.IsNullable = true;
                colvarSuspicious.IsPrimaryKey = false;
                colvarSuspicious.IsForeignKey = false;
                colvarSuspicious.IsReadOnly = false;
                colvarSuspicious.DefaultSetting = @"";
                colvarSuspicious.ForeignKeyTableName = "";
                schema.Columns.Add(colvarSuspicious);

                TableSchema.TableColumn colvarSuspiciousStart = new TableSchema.TableColumn(schema);
                colvarSuspiciousStart.ColumnName = "SuspiciousStart";
                colvarSuspiciousStart.DataType = DbType.Int16;
                colvarSuspiciousStart.MaxLength = 0;
                colvarSuspiciousStart.AutoIncrement = false;
                colvarSuspiciousStart.IsNullable = true;
                colvarSuspiciousStart.IsPrimaryKey = false;
                colvarSuspiciousStart.IsForeignKey = false;
                colvarSuspiciousStart.IsReadOnly = false;
                colvarSuspiciousStart.DefaultSetting = @"";
                colvarSuspiciousStart.ForeignKeyTableName = "";
                schema.Columns.Add(colvarSuspiciousStart);

                TableSchema.TableColumn colvarColor = new TableSchema.TableColumn(schema);
                colvarColor.ColumnName = "Color";
                colvarColor.DataType = DbType.Int16;
                colvarColor.MaxLength = 0;
                colvarColor.AutoIncrement = false;
                colvarColor.IsNullable = true;
                colvarColor.IsPrimaryKey = false;
                colvarColor.IsForeignKey = false;
                colvarColor.IsReadOnly = false;
                colvarColor.DefaultSetting = @"";
                colvarColor.ForeignKeyTableName = "";
                schema.Columns.Add(colvarColor);

                TableSchema.TableColumn colvarPosition = new TableSchema.TableColumn(schema);
                colvarPosition.ColumnName = "Position";
                colvarPosition.DataType = DbType.String;
                colvarPosition.MaxLength = 0;
                colvarPosition.AutoIncrement = false;
                colvarPosition.IsNullable = true;
                colvarPosition.IsPrimaryKey = false;
                colvarPosition.IsForeignKey = false;
                colvarPosition.IsReadOnly = false;
                colvarPosition.DefaultSetting = @"";
                colvarPosition.ForeignKeyTableName = "";
                schema.Columns.Add(colvarPosition);

                TableSchema.TableColumn colvarWarehouse = new TableSchema.TableColumn(schema);
                colvarWarehouse.ColumnName = "Warehouse";
                colvarWarehouse.DataType = DbType.String;
                colvarWarehouse.MaxLength = 0;
                colvarWarehouse.AutoIncrement = false;
                colvarWarehouse.IsNullable = true;
                colvarWarehouse.IsPrimaryKey = false;
                colvarWarehouse.IsForeignKey = false;
                colvarWarehouse.IsReadOnly = false;
                colvarWarehouse.DefaultSetting = @"";
                colvarWarehouse.ForeignKeyTableName = "";
                schema.Columns.Add(colvarWarehouse);

                TableSchema.TableColumn colvarStackSize = new TableSchema.TableColumn(schema);
                colvarStackSize.ColumnName = "StackSize";
                colvarStackSize.DataType = DbType.Int16;
                colvarStackSize.MaxLength = 0;
                colvarStackSize.AutoIncrement = false;
                colvarStackSize.IsNullable = false;
                colvarStackSize.IsPrimaryKey = false;
                colvarStackSize.IsForeignKey = false;
                colvarStackSize.IsReadOnly = false;
                colvarStackSize.DefaultSetting = @"";
                colvarStackSize.ForeignKeyTableName = "";
                schema.Columns.Add(colvarStackSize);

                TableSchema.TableColumn colvarInscribed = new TableSchema.TableColumn(schema);
                colvarInscribed.ColumnName = "Inscribed";
                colvarInscribed.DataType = DbType.Int16;
                colvarInscribed.MaxLength = 0;
                colvarInscribed.AutoIncrement = false;
                colvarInscribed.IsNullable = true;
                colvarInscribed.IsPrimaryKey = false;
                colvarInscribed.IsForeignKey = false;
                colvarInscribed.IsReadOnly = false;
                colvarInscribed.DefaultSetting = @"";
                colvarInscribed.ForeignKeyTableName = "";
                schema.Columns.Add(colvarInscribed);

                TableSchema.TableColumn colvarAgate = new TableSchema.TableColumn(schema);
                colvarAgate.ColumnName = "agate";
                colvarAgate.DataType = DbType.String;
                colvarAgate.MaxLength = 500;
                colvarAgate.AutoIncrement = false;
                colvarAgate.IsNullable = true;
                colvarAgate.IsPrimaryKey = false;
                colvarAgate.IsForeignKey = false;
                colvarAgate.IsReadOnly = false;
                colvarAgate.DefaultSetting = @"";
                colvarAgate.ForeignKeyTableName = "";
                schema.Columns.Add(colvarAgate);

                TableSchema.TableColumn colvarRefineryItem = new TableSchema.TableColumn(schema);
                colvarRefineryItem.ColumnName = "refineryitem";
                colvarRefineryItem.DataType = DbType.UInt32;
                colvarRefineryItem.MaxLength = 500;
                colvarRefineryItem.AutoIncrement = false;
                colvarRefineryItem.IsNullable = true;
                colvarRefineryItem.IsPrimaryKey = false;
                colvarRefineryItem.IsForeignKey = false;
                colvarRefineryItem.IsReadOnly = false;
                colvarRefineryItem.DefaultSetting = @"";
                colvarRefineryItem.ForeignKeyTableName = "";
                schema.Columns.Add(colvarRefineryItem);

                TableSchema.TableColumn colvarRefineryTime = new TableSchema.TableColumn(schema);
                colvarRefineryTime.ColumnName = "refinerytime";
                colvarRefineryTime.DataType = DbType.UInt64;
                colvarRefineryTime.MaxLength = 500;
                colvarRefineryTime.AutoIncrement = false;
                colvarRefineryTime.IsNullable = true;
                colvarRefineryTime.IsPrimaryKey = false;
                colvarRefineryTime.IsForeignKey = false;
                colvarRefineryTime.IsReadOnly = false;
                colvarRefineryTime.DefaultSetting = @"";
                colvarRefineryTime.ForeignKeyTableName = "";
                schema.Columns.Add(colvarRefineryTime);

                BaseSchema = schema;
                //add this schema to the provider
                //so we can query it later
                DataService.Providers["Conquer_Server"].AddSchema("items", schema);
            }
        }
        #endregion
        #region Props

        [XmlAttribute("EntityID")]
        [Bindable(true)]
        public long EntityID
        {
            get { return GetColumnValue<long>(Columns.EntityID); }
            set { SetColumnValue(Columns.EntityID, value); }
        }

        [XmlAttribute("Id")]
        [Bindable(true)]
        public uint Id
        {
            get { return GetColumnValue<uint>(Columns.Id); }
            set { SetColumnValue(Columns.Id, value); }
        }

        [XmlAttribute("Uid")]
        [Bindable(true)]
        public uint Uid
        {
            get { return GetColumnValue<uint>(Columns.Uid); }
            set { SetColumnValue(Columns.Uid, value); }
        }

        [XmlAttribute("Plus")]
        [Bindable(true)]
        public byte Plus
        {
            get { return GetColumnValue<byte>(Columns.Plus); }
            set { SetColumnValue(Columns.Plus, value); }
        }

        [XmlAttribute("Bless")]
        [Bindable(true)]
        public byte Bless
        {
            get { return GetColumnValue<byte>(Columns.Bless); }
            set { SetColumnValue(Columns.Bless, value); }
        }

        [XmlAttribute("Enchant")]
        [Bindable(true)]
        public byte Enchant
        {
            get { return GetColumnValue<byte>(Columns.Enchant); }
            set { SetColumnValue(Columns.Enchant, value); }
        }

        [XmlAttribute("SocketOne")]
        [Bindable(true)]
        public byte SocketOne
        {
            get { return GetColumnValue<byte>(Columns.SocketOne); }
            set { SetColumnValue(Columns.SocketOne, value); }
        }

        [XmlAttribute("SocketTwo")]
        [Bindable(true)]
        public byte SocketTwo
        {
            get { return GetColumnValue<byte>(Columns.SocketTwo); }
            set { SetColumnValue(Columns.SocketTwo, value); }
        }

        [XmlAttribute("Durability")]
        [Bindable(true)]
        public ushort Durability
        {
            get { return GetColumnValue<ushort>(Columns.Durability); }
            set { SetColumnValue(Columns.Durability, value); }
        }

        [XmlAttribute("MaximDurability")]
        [Bindable(true)]
        public ushort MaximDurability
        {
            get { return GetColumnValue<ushort>(Columns.MaximDurability); }
            set { SetColumnValue(Columns.MaximDurability, value); }
        }

        [XmlAttribute("SocketProgress")]
        [Bindable(true)]
        public uint SocketProgress
        {
            get { return GetColumnValue<uint>(Columns.SocketProgress); }
            set { SetColumnValue(Columns.SocketProgress, value); }
        }

        [XmlAttribute("PlusProgress")]
        [Bindable(true)]
        public uint PlusProgress
        {
            get { return GetColumnValue<uint>(Columns.PlusProgress); }
            set { SetColumnValue(Columns.PlusProgress, value); }
        }

        [XmlAttribute("Effect")]
        [Bindable(true)]
        public short? Effect
        {
            get { return GetColumnValue<short?>(Columns.Effect); }
            set { SetColumnValue(Columns.Effect, value); }
        }

        [XmlAttribute("Bound")]
        [Bindable(true)]
        public bool Bound
        {
            get { return GetColumnValue<bool>(Columns.Bound); }
            set { SetColumnValue(Columns.Bound, value); }
        }

        [XmlAttribute("Locked")]
        [Bindable(true)]
        public byte Locked
        {
            get { return GetColumnValue<byte>(Columns.Locked); }
            set { SetColumnValue(Columns.Locked, value); }
        }

        [XmlAttribute("UnlockEnd")]
        [Bindable(true)]
        public ulong UnlockEnd
        {
            get { return GetColumnValue<ulong>(Columns.UnlockEnd); }
            set { SetColumnValue(Columns.UnlockEnd, value); }
        }

        [XmlAttribute("Suspicious")]
        [Bindable(true)]
        public bool Suspicious
        {
            get { return GetColumnValue<bool>(Columns.Suspicious); }
            set { SetColumnValue(Columns.Suspicious, value); }
        }

        [XmlAttribute("SuspiciousStart")]
        [Bindable(true)]
        public ulong SuspiciousStart
        {
            get { return GetColumnValue<ulong>(Columns.SuspiciousStart); }
            set { SetColumnValue(Columns.SuspiciousStart, value); }
        }

        [XmlAttribute("Color")]
        [Bindable(true)]
        public uint Color
        {
            get { return GetColumnValue<uint>(Columns.Color); }
            set { SetColumnValue(Columns.Color, value); }
        }

        [XmlAttribute("Position")]
        [Bindable(true)]
        public ushort Position
        {
            get { return GetColumnValue<ushort>(Columns.Position); }
            set { SetColumnValue(Columns.Position, value); }
        }

        [XmlAttribute("Warehouse")]
        [Bindable(true)]
        public ushort Warehouse
        {
            get { return GetColumnValue<ushort>(Columns.Warehouse); }
            set { SetColumnValue(Columns.Warehouse, value); }
        }

        [XmlAttribute("StackSize")]
        [Bindable(true)]
        public ushort StackSize
        {
            get { return GetColumnValue<ushort>(Columns.StackSize); }
            set { SetColumnValue(Columns.StackSize, value); }
        }

        [XmlAttribute("Inscribed")]
        [Bindable(true)]
        public bool Inscribed
        {
            get { return GetColumnValue<bool>(Columns.Inscribed); }
            set { SetColumnValue(Columns.Inscribed, value); }
        }

        [XmlAttribute("Agate")]
        [Bindable(true)]
        public string Agate
        {
            get { return GetColumnValue<string>(Columns.Agate); }
            set { SetColumnValue(Columns.Agate, value); }
        }
        [XmlAttribute("RefineryItem")]
        [Bindable(true)]
        public UInt32 RefineryItem
        {
            get { return GetColumnValue<UInt32>(Columns.RefineryItem); }
            set { SetColumnValue(Columns.RefineryItem, value); }
        }
        [XmlAttribute("RefineryTime")]
        [Bindable(true)]
        public UInt64 RefineryTime
        {
            get { return GetColumnValue<UInt64>(Columns.RefineryTime); }
            set { SetColumnValue(Columns.RefineryTime, value); }
        }
        #endregion
        #region ObjectDataSource support


        /// <summary>
        /// Inserts a record, can be used with the Object Data Source
        /// </summary>
        public static void Insert(long varEntityID, uint varId, uint varUid, byte varPlus, byte varBless, byte varEnchant, byte varSocketOne, byte varSocketTwo, ushort varDurability, ushort varMaximDurability, uint varSocketProgress, uint varPlusProgress, short? varEffect, bool varBound, byte varLocked, ulong varUnlockEnd, bool varSuspicious, ulong varSuspiciousStart, uint varColor, ushort varPosition, ushort varWarehouse, ushort varStackSize, bool varInscribed, uint varRefineryItem, UInt64 varRefineryTime)
        {
            Item item = new Item();

            item.EntityID = varEntityID;

            item.Id = varId;

            item.Uid = varUid;

            item.Plus = varPlus;

            item.Bless = varBless;

            item.Enchant = varEnchant;

            item.SocketOne = varSocketOne;

            item.SocketTwo = varSocketTwo;

            item.Durability = varDurability;

            item.MaximDurability = varMaximDurability;

            item.SocketProgress = varSocketProgress;

            item.PlusProgress = varPlusProgress;

            item.Effect = varEffect;

            item.Bound = varBound;

            item.Locked = varLocked;

            item.UnlockEnd = varUnlockEnd;

            item.Suspicious = varSuspicious;

            item.SuspiciousStart = varSuspiciousStart;

            item.Color = varColor;

            item.Position = varPosition;

            item.Warehouse = varWarehouse;

            item.StackSize = varStackSize;

            item.Inscribed = varInscribed;

            item.RefineryItem = varRefineryItem;

            item.RefineryTime = varRefineryTime;


            if (System.Web.HttpContext.Current != null)
                item.Save(System.Web.HttpContext.Current.User.Identity.Name);
            else
                item.Save(System.Threading.Thread.CurrentPrincipal.Identity.Name);
        }

        /// <summary>
        /// Updates a record, can be used with the Object Data Source
        /// </summary>
        public static void Update(long varEntityID, uint varId, uint varUid, byte varPlus, byte varBless, byte varEnchant, byte varSocketOne, byte varSocketTwo, ushort varDurability, ushort varMaximDurability, uint varSocketProgress, uint varPlusProgress, short? varEffect, bool varBound, byte varLocked, ulong varUnlockEnd, bool varSuspicious, ulong varSuspiciousStart, uint varColor, ushort varPosition, ushort varWarehouse, ushort varStackSize, bool varInscribed, string varAgate)
        {
            Item item = new Item();

            item.EntityID = varEntityID;

            item.Id = varId;

            item.Uid = varUid;

            item.Plus = varPlus;

            item.Bless = varBless;

            item.Enchant = varEnchant;

            item.SocketOne = varSocketOne;

            item.SocketTwo = varSocketTwo;

            item.Durability = varDurability;

            item.MaximDurability = varMaximDurability;

            item.SocketProgress = varSocketProgress;

            item.PlusProgress = varPlusProgress;

            item.Effect = varEffect;

            item.Bound = varBound;

            item.Locked = varLocked;

            item.UnlockEnd = varUnlockEnd;

            item.Suspicious = varSuspicious;

            item.SuspiciousStart = varSuspiciousStart;

            item.Color = varColor;

            item.Position = varPosition;

            item.Warehouse = varWarehouse;

            item.StackSize = varStackSize;

            item.Inscribed = varInscribed;

            item.Agate = varAgate;


            item.IsNew = false;
            if (System.Web.HttpContext.Current != null)
                item.Save(System.Web.HttpContext.Current.User.Identity.Name);
            else
                item.Save(System.Threading.Thread.CurrentPrincipal.Identity.Name);
        }
        #endregion
        #region Typed Columns


        public static TableSchema.TableColumn EntityIDColumn
        {
            get { return Schema.Columns[0]; }
        }



        public static TableSchema.TableColumn IdColumn
        {
            get { return Schema.Columns[1]; }
        }



        public static TableSchema.TableColumn UidColumn
        {
            get { return Schema.Columns[2]; }
        }



        public static TableSchema.TableColumn PlusColumn
        {
            get { return Schema.Columns[3]; }
        }



        public static TableSchema.TableColumn BlessColumn
        {
            get { return Schema.Columns[4]; }
        }



        public static TableSchema.TableColumn EnchantColumn
        {
            get { return Schema.Columns[5]; }
        }



        public static TableSchema.TableColumn SocketOneColumn
        {
            get { return Schema.Columns[6]; }
        }



        public static TableSchema.TableColumn SocketTwoColumn
        {
            get { return Schema.Columns[7]; }
        }



        public static TableSchema.TableColumn DurabilityColumn
        {
            get { return Schema.Columns[8]; }
        }



        public static TableSchema.TableColumn MaximDurabilityColumn
        {
            get { return Schema.Columns[9]; }
        }



        public static TableSchema.TableColumn SocketProgressColumn
        {
            get { return Schema.Columns[10]; }
        }



        public static TableSchema.TableColumn PlusProgressColumn
        {
            get { return Schema.Columns[11]; }
        }



        public static TableSchema.TableColumn EffectColumn
        {
            get { return Schema.Columns[12]; }
        }



        public static TableSchema.TableColumn BoundColumn
        {
            get { return Schema.Columns[13]; }
        }



        public static TableSchema.TableColumn LockedColumn
        {
            get { return Schema.Columns[14]; }
        }



        public static TableSchema.TableColumn UnlockEndColumn
        {
            get { return Schema.Columns[15]; }
        }



        public static TableSchema.TableColumn SuspiciousColumn
        {
            get { return Schema.Columns[16]; }
        }



        public static TableSchema.TableColumn SuspiciousStartColumn
        {
            get { return Schema.Columns[17]; }
        }



        public static TableSchema.TableColumn ColorColumn
        {
            get { return Schema.Columns[18]; }
        }



        public static TableSchema.TableColumn PositionColumn
        {
            get { return Schema.Columns[19]; }
        }



        public static TableSchema.TableColumn WarehouseColumn
        {
            get { return Schema.Columns[20]; }
        }



        public static TableSchema.TableColumn StackSizeColumn
        {
            get { return Schema.Columns[21]; }
        }

        public static TableSchema.TableColumn InscribedColumn
        {
            get { return Schema.Columns[22]; }
        }

        public static TableSchema.TableColumn AgateColumn
        {
            get { return Schema.Columns[23]; }
        }
        public static TableSchema.TableColumn RefineryItemColumn
        {
            get { return Schema.Columns[24]; }
        }
        public static TableSchema.TableColumn RefineryTimeColumn
        {
            get { return Schema.Columns[25]; }
        }
        #endregion
        #region Columns Struct
        public struct Columns
        {
            public static string EntityID = @"EntityID";
            public static string Id = @"ID";
            public static string Uid = @"UID";
            public static string Plus = @"Plus";
            public static string Bless = @"Bless";
            public static string Enchant = @"Enchant";
            public static string SocketOne = @"SocketOne";
            public static string SocketTwo = @"SocketTwo";
            public static string Durability = @"Durability";
            public static string MaximDurability = @"MaximDurability";
            public static string SocketProgress = @"SocketProgress";
            public static string PlusProgress = @"PlusProgress";
            public static string Effect = @"Effect";
            public static string Bound = @"Bound";
            public static string Locked = @"Locked";
            public static string UnlockEnd = @"UnlockEnd";
            public static string Suspicious = @"Suspicious";
            public static string SuspiciousStart = @"SuspiciousStart";
            public static string Color = @"Color";
            public static string Position = @"Position";
            public static string Warehouse = @"Warehouse";
            public static string StackSize = @"StackSize";
            public static string Inscribed = @"Inscribed";
            public static string Agate = @"agate";
            public static string RefineryItem = @"RefineryItem";
            public static string RefineryTime = @"RefineryTime";

        }
        #endregion
        #region Update PK Collections

        #endregion
        #region Deep Save

        #endregion

    }
}
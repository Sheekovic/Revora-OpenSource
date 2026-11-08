// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Data.Common;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Xml;
using System.Xml.Serialization;
using SubSonic;
using SubSonic.Utilities;
namespace Conquer.Database
{
    /// <summary>
    /// Strongly-typed collection for the PtArsenal class.
    /// </summary>
    [Serializable]
    public partial class PtArsenalCollection : ActiveList<PtArsenal, PtArsenalCollection>
    {
        public PtArsenalCollection() { }

        /// <summary>
        /// Filters an existing collection based on the set criteria. This is an in-memory filter
        /// Thanks to developingchris for this!
        /// </summary>
        /// <returns>PtArsenalCollection</returns>
        public PtArsenalCollection Filter()
        {
            for (int i = this.Count - 1; i > -1; i--)
            {
                PtArsenal o = this[i];
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
    /// <summary>
    /// This is an ActiveRecord class which wraps the pt_arsenal table.
    /// </summary>
    [Serializable]
    public partial class PtArsenal : ActiveRecord<PtArsenal>, IActiveRecord
    {
        #region .ctors and Default Settings

        public PtArsenal()
        {
            SetSQLProps();
            InitSetDefaults();
            MarkNew();
        }

        private void InitSetDefaults() { SetDefaults(); }

        public PtArsenal(bool useDatabaseDefaults)
        {
            SetSQLProps();
            if (useDatabaseDefaults)
                ForceDefaults();
            MarkNew();
        }

        public PtArsenal(object keyID)
        {
            SetSQLProps();
            InitSetDefaults();
            LoadByKey(keyID);
        }

        public PtArsenal(string columnName, object columnValue)
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
                TableSchema.Table schema = new TableSchema.Table("pt_arsenal", TableType.Table, DataService.GetInstance("Conquer_Server"));
                schema.Columns = new TableSchema.TableColumnCollection();
                schema.SchemaName = @"";
                //columns

                TableSchema.TableColumn colvarArsenalDonation = new TableSchema.TableColumn(schema);
                colvarArsenalDonation.ColumnName = "arsenal_donation";
                colvarArsenalDonation.DataType = DbType.Int64;
                colvarArsenalDonation.MaxLength = 0;
                colvarArsenalDonation.AutoIncrement = false;
                colvarArsenalDonation.IsNullable = false;
                colvarArsenalDonation.IsPrimaryKey = true;
                colvarArsenalDonation.IsForeignKey = false;
                colvarArsenalDonation.IsReadOnly = false;
                colvarArsenalDonation.DefaultSetting = @"";
                colvarArsenalDonation.ForeignKeyTableName = "";
                schema.Columns.Add(colvarArsenalDonation);

                TableSchema.TableColumn colvarArsenalId = new TableSchema.TableColumn(schema);
                colvarArsenalId.ColumnName = "arsenal_id";
                colvarArsenalId.DataType = DbType.Boolean;
                colvarArsenalId.MaxLength = 5;
                colvarArsenalId.AutoIncrement = false;
                colvarArsenalId.IsNullable = false;
                colvarArsenalId.IsPrimaryKey = false;
                colvarArsenalId.IsForeignKey = false;
                colvarArsenalId.IsReadOnly = false;
                colvarArsenalId.DefaultSetting = @"";
                colvarArsenalId.ForeignKeyTableName = "";
                schema.Columns.Add(colvarArsenalId);

                TableSchema.TableColumn colvarName = new TableSchema.TableColumn(schema);
                colvarName.ColumnName = "name";
                colvarName.DataType = DbType.String;
                colvarName.MaxLength = 16;
                colvarName.AutoIncrement = false;
                colvarName.IsNullable = true;
                colvarName.IsPrimaryKey = false;
                colvarName.IsForeignKey = false;
                colvarName.IsReadOnly = false;
                colvarName.DefaultSetting = @"";
                colvarName.ForeignKeyTableName = "";
                schema.Columns.Add(colvarName);

                TableSchema.TableColumn colvarItenId = new TableSchema.TableColumn(schema);
                colvarItenId.ColumnName = "iten_id";
                colvarItenId.DataType = DbType.Int64;
                colvarItenId.MaxLength = 18;
                colvarItenId.AutoIncrement = false;
                colvarItenId.IsNullable = true;
                colvarItenId.IsPrimaryKey = false;
                colvarItenId.IsForeignKey = false;
                colvarItenId.IsReadOnly = false;
                colvarItenId.DefaultSetting = @"";
                colvarItenId.ForeignKeyTableName = "";
                schema.Columns.Add(colvarItenId);

                TableSchema.TableColumn colvarItenAtype = new TableSchema.TableColumn(schema);
                colvarItenAtype.ColumnName = "iten_atype";
                colvarItenAtype.DataType = DbType.Boolean;
                colvarItenAtype.MaxLength = 5;
                colvarItenAtype.AutoIncrement = false;
                colvarItenAtype.IsNullable = true;
                colvarItenAtype.IsPrimaryKey = false;
                colvarItenAtype.IsForeignKey = false;
                colvarItenAtype.IsReadOnly = false;
                colvarItenAtype.DefaultSetting = @"";
                colvarItenAtype.ForeignKeyTableName = "";
                schema.Columns.Add(colvarItenAtype);

                TableSchema.TableColumn colvarSynId = new TableSchema.TableColumn(schema);
                colvarSynId.ColumnName = "syn_id";
                colvarSynId.DataType = DbType.Int16;
                colvarSynId.MaxLength = 12;
                colvarSynId.AutoIncrement = false;
                colvarSynId.IsNullable = true;
                colvarSynId.IsPrimaryKey = false;
                colvarSynId.IsForeignKey = false;
                colvarSynId.IsReadOnly = false;
                colvarSynId.DefaultSetting = @"";
                colvarSynId.ForeignKeyTableName = "";
                schema.Columns.Add(colvarSynId);

                TableSchema.TableColumn colvarArsenalUnlocked = new TableSchema.TableColumn(schema);
                colvarArsenalUnlocked.ColumnName = "arsenal_unlocked";
                colvarArsenalUnlocked.DataType = DbType.Boolean;
                colvarArsenalUnlocked.MaxLength = 5;
                colvarArsenalUnlocked.AutoIncrement = false;
                colvarArsenalUnlocked.IsNullable = true;
                colvarArsenalUnlocked.IsPrimaryKey = false;
                colvarArsenalUnlocked.IsForeignKey = false;
                colvarArsenalUnlocked.IsReadOnly = false;
                colvarArsenalUnlocked.DefaultSetting = @"";
                colvarArsenalUnlocked.ForeignKeyTableName = "";
                schema.Columns.Add(colvarArsenalUnlocked);

                BaseSchema = schema;
                //add this schema to the provider
                //so we can query it later
                DataService.Providers["Conquer_Server"].AddSchema("pt_arsenal", schema);
            }
        }
        #endregion

        #region Props

        [XmlAttribute("ArsenalDonation")]
        [Bindable(true)]
        public string ArsenalDonation
        {
            get { return GetColumnValue<string>(Columns.ArsenalDonation); }
            set { SetColumnValue(Columns.ArsenalDonation, value); }
        }

        [XmlAttribute("ArsenalId")]
        [Bindable(true)]
        public bool ArsenalId
        {
            get { return GetColumnValue<bool>(Columns.ArsenalId); }
            set { SetColumnValue(Columns.ArsenalId, value); }
        }

        [XmlAttribute("Name")]
        [Bindable(true)]
        public string Name
        {
            get { return GetColumnValue<string>(Columns.Name); }
            set { SetColumnValue(Columns.Name, value); }
        }

        [XmlAttribute("ItenId")]
        [Bindable(true)]
        public long? ItenId
        {
            get { return GetColumnValue<long?>(Columns.ItenId); }
            set { SetColumnValue(Columns.ItenId, value); }
        }

        [XmlAttribute("ItenAtype")]
        [Bindable(true)]
        public bool? ItenAtype
        {
            get { return GetColumnValue<bool?>(Columns.ItenAtype); }
            set { SetColumnValue(Columns.ItenAtype, value); }
        }

        [XmlAttribute("SynId")]
        [Bindable(true)]
        public short? SynId
        {
            get { return GetColumnValue<short?>(Columns.SynId); }
            set { SetColumnValue(Columns.SynId, value); }
        }

        [XmlAttribute("ArsenalUnlocked")]
        [Bindable(true)]
        public bool? ArsenalUnlocked
        {
            get { return GetColumnValue<bool?>(Columns.ArsenalUnlocked); }
            set { SetColumnValue(Columns.ArsenalUnlocked, value); }
        }

        #endregion




        //no foreign key tables defined (0)



        //no ManyToMany tables defined (0)



        #region ObjectDataSource support


        /// <summary>
        /// Inserts a record, can be used with the Object Data Source
        /// </summary>
        public static void Insert(string varArsenalDonation, bool varArsenalId, string varName, long? varItenId, bool? varItenAtype, short? varSynId, bool? varArsenalUnlocked)
        {
            PtArsenal item = new PtArsenal();

            item.ArsenalDonation = varArsenalDonation;

            item.ArsenalId = varArsenalId;

            item.Name = varName;

            item.ItenId = varItenId;

            item.ItenAtype = varItenAtype;

            item.SynId = varSynId;

            item.ArsenalUnlocked = varArsenalUnlocked;


            if (System.Web.HttpContext.Current != null)
                item.Save(System.Web.HttpContext.Current.User.Identity.Name);
            else
                item.Save(System.Threading.Thread.CurrentPrincipal.Identity.Name);
        }

        /// <summary>
        /// Updates a record, can be used with the Object Data Source
        /// </summary>
        public static void Update(string varArsenalDonation, bool varArsenalId, string varName, long? varItenId, bool? varItenAtype, short? varSynId, bool? varArsenalUnlocked)
        {
            PtArsenal item = new PtArsenal();

            item.ArsenalDonation = varArsenalDonation;

            item.ArsenalId = varArsenalId;

            item.Name = varName;

            item.ItenId = varItenId;

            item.ItenAtype = varItenAtype;

            item.SynId = varSynId;

            item.ArsenalUnlocked = varArsenalUnlocked;

            item.IsNew = false;
            if (System.Web.HttpContext.Current != null)
                item.Save(System.Web.HttpContext.Current.User.Identity.Name);
            else
                item.Save(System.Threading.Thread.CurrentPrincipal.Identity.Name);
        }
        #endregion



        #region Typed Columns


        public static TableSchema.TableColumn ArsenalDonationColumn
        {
            get { return Schema.Columns[0]; }
        }



        public static TableSchema.TableColumn ArsenalIdColumn
        {
            get { return Schema.Columns[1]; }
        }



        public static TableSchema.TableColumn NameColumn
        {
            get { return Schema.Columns[2]; }
        }



        public static TableSchema.TableColumn ItenIdColumn
        {
            get { return Schema.Columns[3]; }
        }



        public static TableSchema.TableColumn ItenAtypeColumn
        {
            get { return Schema.Columns[4]; }
        }



        public static TableSchema.TableColumn SynIdColumn
        {
            get { return Schema.Columns[5]; }
        }



        public static TableSchema.TableColumn ArsenalUnlockedColumn
        {
            get { return Schema.Columns[6]; }
        }



        #endregion
        #region Columns Struct
        public struct Columns
        {
            public static string ArsenalDonation = @"arsenal_donation";
            public static string ArsenalId = @"arsenal_id";
            public static string Name = @"name";
            public static string ItenId = @"iten_id";
            public static string ItenAtype = @"iten_atype";
            public static string SynId = @"syn_id";
            public static string ArsenalUnlocked = @"arsenal_unlocked";

        }
        #endregion

        #region Update PK Collections

        #endregion

        #region Deep Save

        #endregion
    }
}


using System;
using System.Data;
using System.ComponentModel;
using System.Xml;
using System.Xml.Serialization;
using SubSonic;

namespace Conquer.Database
{
    [Serializable]
    public partial class EntityCollection : ActiveList<Entity, EntityCollection>
    {
        public EntityCollection()
        {
        }
        public EntityCollection Filter()
        {
            for (int i = this.Count - 1; i > -1; i--)
            {
                Entity o = this[i];
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
    public partial class Entity : ActiveRecord<Entity>, IActiveRecord
    {
        #region ctors and Default Settings
        public Entity()
        {
            SetSQLProps();
            InitSetDefaults();
            MarkNew();
        }

        private void InitSetDefaults()
        {
            SetDefaults();
        }

        public Entity(bool useDatabaseDefaults)
        {
            SetSQLProps();
            if (useDatabaseDefaults)
                ForceDefaults();
            MarkNew();
        }

        public Entity(object keyID)
        {
            SetSQLProps();
            InitSetDefaults();
            LoadByKey(keyID);
        }

        public Entity(string columnName, object columnValue)
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
                TableSchema.Table schema = new TableSchema.Table("entities", TableType.Table, DataService.GetInstance("Conquer_Server"));
                schema.Columns = new TableSchema.TableColumnCollection();
                schema.SchemaName = @"";
                //columns

                TableSchema.TableColumn colvarName = new TableSchema.TableColumn(schema);
                colvarName.ColumnName = "Name";
                colvarName.DataType = DbType.String;
                colvarName.MaxLength = 16;
                colvarName.AutoIncrement = false;
                colvarName.IsNullable = false;
                colvarName.IsPrimaryKey = false;
                colvarName.IsForeignKey = false;
                colvarName.IsReadOnly = false;
                colvarName.DefaultSetting = @"";
                colvarName.ForeignKeyTableName = "";
                schema.Columns.Add(colvarName);

                TableSchema.TableColumn colvarUid = new TableSchema.TableColumn(schema);
                colvarUid.ColumnName = "UID";
                colvarUid.DataType = DbType.Int64;
                colvarUid.MaxLength = 0;
                colvarUid.AutoIncrement = false;
                colvarUid.IsNullable = false;
                colvarUid.IsPrimaryKey = true;
                colvarUid.IsForeignKey = false;
                colvarUid.IsReadOnly = false;
                colvarUid.DefaultSetting = @"";
                colvarUid.ForeignKeyTableName = "";
                schema.Columns.Add(colvarUid);

                TableSchema.TableColumn colvarFlower = new TableSchema.TableColumn(schema);
                colvarFlower.ColumnName = "Flower";
                colvarFlower.DataType = DbType.Int64;
                colvarFlower.MaxLength = 0;
                colvarFlower.AutoIncrement = false;
                colvarFlower.IsNullable = false;
                colvarFlower.IsPrimaryKey = true;
                colvarFlower.IsForeignKey = false;
                colvarFlower.IsReadOnly = false;
                colvarFlower.DefaultSetting = @"";
                colvarFlower.ForeignKeyTableName = "";
                schema.Columns.Add(colvarFlower);

                TableSchema.TableColumn colvarOwner = new TableSchema.TableColumn(schema);
                colvarOwner.ColumnName = "Owner";
                colvarOwner.DataType = DbType.String;
                colvarOwner.MaxLength = 45;
                colvarOwner.AutoIncrement = false;
                colvarOwner.IsNullable = false;
                colvarOwner.IsPrimaryKey = false;
                colvarOwner.IsForeignKey = false;
                colvarOwner.IsReadOnly = false;
                colvarOwner.DefaultSetting = @"";
                colvarOwner.ForeignKeyTableName = "";
                schema.Columns.Add(colvarOwner);

                TableSchema.TableColumn colvarHairStyle = new TableSchema.TableColumn(schema);
                colvarHairStyle.ColumnName = "HairStyle";
                colvarHairStyle.DataType = DbType.Int16;
                colvarHairStyle.MaxLength = 0;
                colvarHairStyle.AutoIncrement = false;
                colvarHairStyle.IsNullable = true;
                colvarHairStyle.IsPrimaryKey = false;
                colvarHairStyle.IsForeignKey = false;
                colvarHairStyle.IsReadOnly = false;
                colvarHairStyle.DefaultSetting = @"";
                colvarHairStyle.ForeignKeyTableName = "";
                schema.Columns.Add(colvarHairStyle);

                TableSchema.TableColumn colvarClassX = new TableSchema.TableColumn(schema);
                colvarClassX.ColumnName = "Class";
                colvarClassX.DataType = DbType.Boolean;
                colvarClassX.MaxLength = 0;
                colvarClassX.AutoIncrement = false;
                colvarClassX.IsNullable = false;
                colvarClassX.IsPrimaryKey = false;
                colvarClassX.IsForeignKey = false;
                colvarClassX.IsReadOnly = false;
                colvarClassX.DefaultSetting = @"";
                colvarClassX.ForeignKeyTableName = "";
                schema.Columns.Add(colvarClassX);

                TableSchema.TableColumn colvarMoney = new TableSchema.TableColumn(schema);
                colvarMoney.ColumnName = "Money";
                colvarMoney.DataType = DbType.Int64;
                colvarMoney.MaxLength = 0;
                colvarMoney.AutoIncrement = false;
                colvarMoney.IsNullable = true;
                colvarMoney.IsPrimaryKey = false;
                colvarMoney.IsForeignKey = false;
                colvarMoney.IsReadOnly = false;
                colvarMoney.DefaultSetting = @"";
                colvarMoney.ForeignKeyTableName = "";
                schema.Columns.Add(colvarMoney);

                TableSchema.TableColumn colvarConquerPoints = new TableSchema.TableColumn(schema);
                colvarConquerPoints.ColumnName = "ConquerPoints";
                colvarConquerPoints.DataType = DbType.Int64;
                colvarConquerPoints.MaxLength = 0;
                colvarConquerPoints.AutoIncrement = false;
                colvarConquerPoints.IsNullable = true;
                colvarConquerPoints.IsPrimaryKey = false;
                colvarConquerPoints.IsForeignKey = false;
                colvarConquerPoints.IsReadOnly = false;
                colvarConquerPoints.DefaultSetting = @"";
                colvarConquerPoints.ForeignKeyTableName = "";
                schema.Columns.Add(colvarConquerPoints);

                TableSchema.TableColumn colvarBoundCps = new TableSchema.TableColumn(schema);
                colvarBoundCps.ColumnName = "BoundCPs";
                colvarBoundCps.DataType = DbType.Int64;
                colvarBoundCps.MaxLength = 0;
                colvarBoundCps.AutoIncrement = false;
                colvarBoundCps.IsNullable = true;
                colvarBoundCps.IsPrimaryKey = false;
                colvarBoundCps.IsForeignKey = false;
                colvarBoundCps.IsReadOnly = false;
                colvarBoundCps.DefaultSetting = @"";
                colvarBoundCps.ForeignKeyTableName = "";
                schema.Columns.Add(colvarBoundCps);


                TableSchema.TableColumn colvarTreepoints = new TableSchema.TableColumn(schema);
                colvarTreepoints.ColumnName = "Treepoints";
                colvarTreepoints.DataType = DbType.Int16;
                colvarTreepoints.MaxLength = 0;
                colvarTreepoints.AutoIncrement = false;
                colvarTreepoints.IsNullable = true;
                colvarTreepoints.IsPrimaryKey = false;
                colvarTreepoints.IsForeignKey = false;
                colvarTreepoints.IsReadOnly = false;
                colvarTreepoints.DefaultSetting = @"";
                colvarTreepoints.ForeignKeyTableName = "";
                schema.Columns.Add(colvarTreepoints);

                TableSchema.TableColumn colvarBody = new TableSchema.TableColumn(schema);
                colvarBody.ColumnName = "Body";
                colvarBody.DataType = DbType.Int16;
                colvarBody.MaxLength = 0;
                colvarBody.AutoIncrement = false;
                colvarBody.IsNullable = false;
                colvarBody.IsPrimaryKey = false;
                colvarBody.IsForeignKey = false;
                colvarBody.IsReadOnly = false;
                colvarBody.DefaultSetting = @"";
                colvarBody.ForeignKeyTableName = "";
                schema.Columns.Add(colvarBody);

                TableSchema.TableColumn colvarFace = new TableSchema.TableColumn(schema);
                colvarFace.ColumnName = "Face";
                colvarFace.DataType = DbType.Int16;
                colvarFace.MaxLength = 0;
                colvarFace.AutoIncrement = false;
                colvarFace.IsNullable = false;
                colvarFace.IsPrimaryKey = false;
                colvarFace.IsForeignKey = false;
                colvarFace.IsReadOnly = false;
                colvarFace.DefaultSetting = @"";
                colvarFace.ForeignKeyTableName = "";
                schema.Columns.Add(colvarFace);

                TableSchema.TableColumn colvarLevel = new TableSchema.TableColumn(schema);
                colvarLevel.ColumnName = "Level";
                colvarLevel.DataType = DbType.Boolean;
                colvarLevel.MaxLength = 0;
                colvarLevel.AutoIncrement = false;
                colvarLevel.IsNullable = true;
                colvarLevel.IsPrimaryKey = false;
                colvarLevel.IsForeignKey = false;
                colvarLevel.IsReadOnly = false;
                colvarLevel.DefaultSetting = @"";
                colvarLevel.ForeignKeyTableName = "";
                schema.Columns.Add(colvarLevel);

                TableSchema.TableColumn colvarStrength = new TableSchema.TableColumn(schema);
                colvarStrength.ColumnName = "Strength";
                colvarStrength.DataType = DbType.Int16;
                colvarStrength.MaxLength = 0;
                colvarStrength.AutoIncrement = false;
                colvarStrength.IsNullable = true;
                colvarStrength.IsPrimaryKey = false;
                colvarStrength.IsForeignKey = false;
                colvarStrength.IsReadOnly = false;
                colvarStrength.DefaultSetting = @"";
                colvarStrength.ForeignKeyTableName = "";
                schema.Columns.Add(colvarStrength);

                TableSchema.TableColumn colvarAgility = new TableSchema.TableColumn(schema);
                colvarAgility.ColumnName = "Agility";
                colvarAgility.DataType = DbType.Int16;
                colvarAgility.MaxLength = 0;
                colvarAgility.AutoIncrement = false;
                colvarAgility.IsNullable = true;
                colvarAgility.IsPrimaryKey = false;
                colvarAgility.IsForeignKey = false;
                colvarAgility.IsReadOnly = false;
                colvarAgility.DefaultSetting = @"";
                colvarAgility.ForeignKeyTableName = "";
                schema.Columns.Add(colvarAgility);

                TableSchema.TableColumn colvarVitality = new TableSchema.TableColumn(schema);
                colvarVitality.ColumnName = "Vitality";
                colvarVitality.DataType = DbType.Int16;
                colvarVitality.MaxLength = 0;
                colvarVitality.AutoIncrement = false;
                colvarVitality.IsNullable = true;
                colvarVitality.IsPrimaryKey = false;
                colvarVitality.IsForeignKey = false;
                colvarVitality.IsReadOnly = false;
                colvarVitality.DefaultSetting = @"";
                colvarVitality.ForeignKeyTableName = "";
                schema.Columns.Add(colvarVitality);

                TableSchema.TableColumn colvarSpirit = new TableSchema.TableColumn(schema);
                colvarSpirit.ColumnName = "Spirit";
                colvarSpirit.DataType = DbType.Int16;
                colvarSpirit.MaxLength = 0;
                colvarSpirit.AutoIncrement = false;
                colvarSpirit.IsNullable = true;
                colvarSpirit.IsPrimaryKey = false;
                colvarSpirit.IsForeignKey = false;
                colvarSpirit.IsReadOnly = false;
                colvarSpirit.DefaultSetting = @"";
                colvarSpirit.ForeignKeyTableName = "";
                schema.Columns.Add(colvarSpirit);

                TableSchema.TableColumn colvarAtributes = new TableSchema.TableColumn(schema);
                colvarAtributes.ColumnName = "Atributes";
                colvarAtributes.DataType = DbType.Int16;
                colvarAtributes.MaxLength = 0;
                colvarAtributes.AutoIncrement = false;
                colvarAtributes.IsNullable = true;
                colvarAtributes.IsPrimaryKey = false;
                colvarAtributes.IsForeignKey = false;
                colvarAtributes.IsReadOnly = false;
                colvarAtributes.DefaultSetting = @"";
                colvarAtributes.ForeignKeyTableName = "";
                schema.Columns.Add(colvarAtributes);

                TableSchema.TableColumn colvarHitpoints = new TableSchema.TableColumn(schema);
                colvarHitpoints.ColumnName = "Hitpoints";
                colvarHitpoints.DataType = DbType.String;
                colvarHitpoints.MaxLength = 0;
                colvarHitpoints.AutoIncrement = false;
                colvarHitpoints.IsNullable = true;
                colvarHitpoints.IsPrimaryKey = false;
                colvarHitpoints.IsForeignKey = false;
                colvarHitpoints.IsReadOnly = false;
                colvarHitpoints.DefaultSetting = @"";
                colvarHitpoints.ForeignKeyTableName = "";
                schema.Columns.Add(colvarHitpoints);

                TableSchema.TableColumn colvarMana = new TableSchema.TableColumn(schema);
                colvarMana.ColumnName = "Mana";
                colvarMana.DataType = DbType.String;
                colvarMana.MaxLength = 0;
                colvarMana.AutoIncrement = false;
                colvarMana.IsNullable = true;
                colvarMana.IsPrimaryKey = false;
                colvarMana.IsForeignKey = false;
                colvarMana.IsReadOnly = false;
                colvarMana.DefaultSetting = @"";
                colvarMana.ForeignKeyTableName = "";
                schema.Columns.Add(colvarMana);

                TableSchema.TableColumn colvarMapID = new TableSchema.TableColumn(schema);
                colvarMapID.ColumnName = "MapID";
                colvarMapID.DataType = DbType.Int16;
                colvarMapID.MaxLength = 0;
                colvarMapID.AutoIncrement = false;
                colvarMapID.IsNullable = true;
                colvarMapID.IsPrimaryKey = false;
                colvarMapID.IsForeignKey = false;
                colvarMapID.IsReadOnly = false;
                colvarMapID.DefaultSetting = @"";
                colvarMapID.ForeignKeyTableName = "";
                schema.Columns.Add(colvarMapID);

                TableSchema.TableColumn colvarX = new TableSchema.TableColumn(schema);
                colvarX.ColumnName = "X";
                colvarX.DataType = DbType.Int16;
                colvarX.MaxLength = 0;
                colvarX.AutoIncrement = false;
                colvarX.IsNullable = true;
                colvarX.IsPrimaryKey = false;
                colvarX.IsForeignKey = false;
                colvarX.IsReadOnly = false;
                colvarX.DefaultSetting = @"";
                colvarX.ForeignKeyTableName = "";
                schema.Columns.Add(colvarX);

                TableSchema.TableColumn colvarY = new TableSchema.TableColumn(schema);
                colvarY.ColumnName = "Y";
                colvarY.DataType = DbType.Int16;
                colvarY.MaxLength = 0;
                colvarY.AutoIncrement = false;
                colvarY.IsNullable = true;
                colvarY.IsPrimaryKey = false;
                colvarY.IsForeignKey = false;
                colvarY.IsReadOnly = false;
                colvarY.DefaultSetting = @"";
                colvarY.ForeignKeyTableName = "";
                schema.Columns.Add(colvarY);

                TableSchema.TableColumn colvarPKPoints = new TableSchema.TableColumn(schema);
                colvarPKPoints.ColumnName = "PKPoints";
                colvarPKPoints.DataType = DbType.Int16;
                colvarPKPoints.MaxLength = 0;
                colvarPKPoints.AutoIncrement = false;
                colvarPKPoints.IsNullable = true;
                colvarPKPoints.IsPrimaryKey = false;
                colvarPKPoints.IsForeignKey = false;
                colvarPKPoints.IsReadOnly = false;
                colvarPKPoints.DefaultSetting = @"";
                colvarPKPoints.ForeignKeyTableName = "";
                schema.Columns.Add(colvarPKPoints);

                TableSchema.TableColumn colvarExperience = new TableSchema.TableColumn(schema);
                colvarExperience.ColumnName = "Experience";
                colvarExperience.DataType = DbType.Int64;
                colvarExperience.MaxLength = 0;
                colvarExperience.AutoIncrement = false;
                colvarExperience.IsNullable = true;
                colvarExperience.IsPrimaryKey = false;
                colvarExperience.IsForeignKey = false;
                colvarExperience.IsReadOnly = false;
                colvarExperience.DefaultSetting = @"";
                colvarExperience.ForeignKeyTableName = "";
                schema.Columns.Add(colvarExperience);

                TableSchema.TableColumn colvarQuizPoints = new TableSchema.TableColumn(schema);
                colvarQuizPoints.ColumnName = "QuizPoints";
                colvarQuizPoints.DataType = DbType.String;
                colvarQuizPoints.MaxLength = 0;
                colvarQuizPoints.AutoIncrement = false;
                colvarQuizPoints.IsNullable = true;
                colvarQuizPoints.IsPrimaryKey = false;
                colvarQuizPoints.IsForeignKey = false;
                colvarQuizPoints.IsReadOnly = false;
                colvarQuizPoints.DefaultSetting = @"";
                colvarQuizPoints.ForeignKeyTableName = "";
                schema.Columns.Add(colvarQuizPoints);

                TableSchema.TableColumn colvarPreviousMapID = new TableSchema.TableColumn(schema);
                colvarPreviousMapID.ColumnName = "PreviousMapID";
                colvarPreviousMapID.DataType = DbType.Int16;
                colvarPreviousMapID.MaxLength = 0;
                colvarPreviousMapID.AutoIncrement = false;
                colvarPreviousMapID.IsNullable = true;
                colvarPreviousMapID.IsPrimaryKey = false;
                colvarPreviousMapID.IsForeignKey = false;
                colvarPreviousMapID.IsReadOnly = false;
                colvarPreviousMapID.DefaultSetting = @"";
                colvarPreviousMapID.ForeignKeyTableName = "";
                schema.Columns.Add(colvarPreviousMapID);

                TableSchema.TableColumn colvarReborn = new TableSchema.TableColumn(schema);
                colvarReborn.ColumnName = "Reborn";
                colvarReborn.DataType = DbType.Byte;
                colvarReborn.MaxLength = 0;
                colvarReborn.AutoIncrement = false;
                colvarReborn.IsNullable = true;
                colvarReborn.IsPrimaryKey = false;
                colvarReborn.IsForeignKey = false;
                colvarReborn.IsReadOnly = false;
                colvarReborn.DefaultSetting = @"";
                colvarReborn.ForeignKeyTableName = "";
                schema.Columns.Add(colvarReborn);

                TableSchema.TableColumn colvarFirstClass = new TableSchema.TableColumn(schema);
                colvarFirstClass.ColumnName = "FirstClass";
                colvarFirstClass.DataType = DbType.Byte;
                colvarFirstClass.MaxLength = 0;
                colvarFirstClass.AutoIncrement = false;
                colvarFirstClass.IsNullable = true;
                colvarFirstClass.IsPrimaryKey = false;
                colvarFirstClass.IsForeignKey = false;
                colvarFirstClass.IsReadOnly = false;
                colvarFirstClass.DefaultSetting = @"";
                colvarFirstClass.ForeignKeyTableName = "";
                schema.Columns.Add(colvarFirstClass);

                TableSchema.TableColumn colvarSecondClass = new TableSchema.TableColumn(schema);
                colvarSecondClass.ColumnName = "SecondClass";
                colvarSecondClass.DataType = DbType.Byte;
                colvarSecondClass.MaxLength = 0;
                colvarSecondClass.AutoIncrement = false;
                colvarSecondClass.IsNullable = true;
                colvarSecondClass.IsPrimaryKey = false;
                colvarSecondClass.IsForeignKey = false;
                colvarSecondClass.IsReadOnly = false;
                colvarSecondClass.DefaultSetting = @"";
                colvarSecondClass.ForeignKeyTableName = "";
                schema.Columns.Add(colvarSecondClass);

                TableSchema.TableColumn colvarThirdClass = new TableSchema.TableColumn(schema);
                colvarThirdClass.ColumnName = "ThirdClass";
                colvarThirdClass.DataType = DbType.Byte;
                colvarThirdClass.MaxLength = 0;
                colvarThirdClass.AutoIncrement = false;
                colvarThirdClass.IsNullable = true;
                colvarThirdClass.IsPrimaryKey = false;
                colvarThirdClass.IsForeignKey = false;
                colvarThirdClass.IsReadOnly = false;
                colvarThirdClass.DefaultSetting = @"";
                colvarThirdClass.ForeignKeyTableName = "";
                schema.Columns.Add(colvarThirdClass);


                TableSchema.TableColumn colvarSpouse = new TableSchema.TableColumn(schema);
                colvarSpouse.ColumnName = "Spouse";
                colvarSpouse.DataType = DbType.String;
                colvarSpouse.MaxLength = 16;
                colvarSpouse.AutoIncrement = false;
                colvarSpouse.IsNullable = true;
                colvarSpouse.IsPrimaryKey = false;
                colvarSpouse.IsForeignKey = false;
                colvarSpouse.IsReadOnly = false;
                colvarSpouse.DefaultSetting = @"";
                colvarSpouse.ForeignKeyTableName = "";
                schema.Columns.Add(colvarSpouse);

                TableSchema.TableColumn colvarWarehousePW = new TableSchema.TableColumn(schema);
                colvarWarehousePW.ColumnName = "WarehousePW";
                colvarWarehousePW.DataType = DbType.UInt32;
                colvarWarehousePW.MaxLength = 16;
                colvarWarehousePW.AutoIncrement = false;
                colvarWarehousePW.IsNullable = true;
                colvarWarehousePW.IsPrimaryKey = false;
                colvarWarehousePW.IsForeignKey = false;
                colvarWarehousePW.IsReadOnly = false;
                colvarWarehousePW.DefaultSetting = @"";
                colvarWarehousePW.ForeignKeyTableName = "";
                schema.Columns.Add(colvarWarehousePW);

                TableSchema.TableColumn colvarMoneySave = new TableSchema.TableColumn(schema);
                colvarMoneySave.ColumnName = "MoneySave";
                colvarMoneySave.DataType = DbType.Int64;
                colvarMoneySave.MaxLength = 0;
                colvarMoneySave.AutoIncrement = false;
                colvarMoneySave.IsNullable = true;
                colvarMoneySave.IsPrimaryKey = false;
                colvarMoneySave.IsForeignKey = false;
                colvarMoneySave.IsReadOnly = false;
                colvarMoneySave.DefaultSetting = @"";
                colvarMoneySave.ForeignKeyTableName = "";
                schema.Columns.Add(colvarMoneySave);

                TableSchema.TableColumn colvarFirstRebornClass = new TableSchema.TableColumn(schema);
                colvarFirstRebornClass.ColumnName = "FirstRebornClass";
                colvarFirstRebornClass.DataType = DbType.Byte;
                colvarFirstRebornClass.MaxLength = 0;
                colvarFirstRebornClass.AutoIncrement = false;
                colvarFirstRebornClass.IsNullable = true;
                colvarFirstRebornClass.IsPrimaryKey = false;
                colvarFirstRebornClass.IsForeignKey = false;
                colvarFirstRebornClass.IsReadOnly = false;
                colvarFirstRebornClass.DefaultSetting = @"";
                colvarFirstRebornClass.ForeignKeyTableName = "";
                schema.Columns.Add(colvarFirstRebornClass);

                TableSchema.TableColumn colvarSecondRebornClass = new TableSchema.TableColumn(schema);
                colvarSecondRebornClass.ColumnName = "SecondRebornClass";
                colvarSecondRebornClass.DataType = DbType.Byte;
                colvarSecondRebornClass.MaxLength = 0;
                colvarSecondRebornClass.AutoIncrement = false;
                colvarSecondRebornClass.IsNullable = true;
                colvarSecondRebornClass.IsPrimaryKey = false;
                colvarSecondRebornClass.IsForeignKey = false;
                colvarSecondRebornClass.IsReadOnly = false;
                colvarSecondRebornClass.DefaultSetting = @"";
                colvarSecondRebornClass.ForeignKeyTableName = "";
                schema.Columns.Add(colvarSecondRebornClass);

                TableSchema.TableColumn colvarFirstRebornLevel = new TableSchema.TableColumn(schema);
                colvarFirstRebornLevel.ColumnName = "FirstRebornLevel";
                colvarFirstRebornLevel.DataType = DbType.Boolean;
                colvarFirstRebornLevel.MaxLength = 0;
                colvarFirstRebornLevel.AutoIncrement = false;
                colvarFirstRebornLevel.IsNullable = true;
                colvarFirstRebornLevel.IsPrimaryKey = false;
                colvarFirstRebornLevel.IsForeignKey = false;
                colvarFirstRebornLevel.IsReadOnly = false;
                colvarFirstRebornLevel.DefaultSetting = @"";
                colvarFirstRebornLevel.ForeignKeyTableName = "";
                schema.Columns.Add(colvarFirstRebornLevel);

                TableSchema.TableColumn colvarSecondRebornLevel = new TableSchema.TableColumn(schema);
                colvarSecondRebornLevel.ColumnName = "SecondRebornLevel";
                colvarSecondRebornLevel.DataType = DbType.Boolean;
                colvarSecondRebornLevel.MaxLength = 0;
                colvarSecondRebornLevel.AutoIncrement = false;
                colvarSecondRebornLevel.IsNullable = true;
                colvarSecondRebornLevel.IsPrimaryKey = false;
                colvarSecondRebornLevel.IsForeignKey = false;
                colvarSecondRebornLevel.IsReadOnly = false;
                colvarSecondRebornLevel.DefaultSetting = @"";
                colvarSecondRebornLevel.ForeignKeyTableName = "";
                schema.Columns.Add(colvarSecondRebornLevel);

                TableSchema.TableColumn colvarOnline = new TableSchema.TableColumn(schema);
                colvarOnline.ColumnName = "Online";
                colvarOnline.DataType = DbType.Boolean;
                colvarOnline.MaxLength = 0;
                colvarOnline.AutoIncrement = false;
                colvarOnline.IsNullable = true;
                colvarOnline.IsPrimaryKey = false;
                colvarOnline.IsForeignKey = false;
                colvarOnline.IsReadOnly = false;
                colvarOnline.DefaultSetting = @"";
                colvarOnline.ForeignKeyTableName = "";
                schema.Columns.Add(colvarOnline);

                TableSchema.TableColumn colvarEnlightenPoints = new TableSchema.TableColumn(schema);
                colvarEnlightenPoints.ColumnName = "EnlightenPoints";
                colvarEnlightenPoints.DataType = DbType.Int64;
                colvarEnlightenPoints.MaxLength = 0;
                colvarEnlightenPoints.AutoIncrement = false;
                colvarEnlightenPoints.IsNullable = true;
                colvarEnlightenPoints.IsPrimaryKey = false;
                colvarEnlightenPoints.IsForeignKey = false;
                colvarEnlightenPoints.IsReadOnly = false;
                colvarEnlightenPoints.DefaultSetting = @"";
                colvarEnlightenPoints.ForeignKeyTableName = "";
                schema.Columns.Add(colvarEnlightenPoints);

                TableSchema.TableColumn colvarDoubleExpTime = new TableSchema.TableColumn(schema);
                colvarDoubleExpTime.ColumnName = "DoubleExpTime";
                colvarDoubleExpTime.DataType = DbType.Int64;
                colvarDoubleExpTime.MaxLength = 0;
                colvarDoubleExpTime.AutoIncrement = false;
                colvarDoubleExpTime.IsNullable = true;
                colvarDoubleExpTime.IsPrimaryKey = false;
                colvarDoubleExpTime.IsForeignKey = false;
                colvarDoubleExpTime.IsReadOnly = false;
                colvarDoubleExpTime.DefaultSetting = @"";
                colvarDoubleExpTime.ForeignKeyTableName = "";
                schema.Columns.Add(colvarDoubleExpTime);

                TableSchema.TableColumn colvarGwkill = new TableSchema.TableColumn(schema);
                colvarGwkill.ColumnName = "gwkill";
                colvarGwkill.DataType = DbType.Int64;
                colvarGwkill.MaxLength = 0;
                colvarGwkill.AutoIncrement = false;
                colvarGwkill.IsNullable = true;
                colvarGwkill.IsPrimaryKey = false;
                colvarGwkill.IsForeignKey = false;
                colvarGwkill.IsReadOnly = false;
                colvarGwkill.DefaultSetting = @"";
                colvarGwkill.ForeignKeyTableName = "";
                schema.Columns.Add(colvarGwkill);

                TableSchema.TableColumn colvarHeavenBlessingTime = new TableSchema.TableColumn(schema);
                colvarHeavenBlessingTime.ColumnName = "HeavenBlessingTime";
                colvarHeavenBlessingTime.DataType = DbType.Int64;
                colvarHeavenBlessingTime.MaxLength = 0;
                colvarHeavenBlessingTime.AutoIncrement = false;
                colvarHeavenBlessingTime.IsNullable = true;
                colvarHeavenBlessingTime.IsPrimaryKey = false;
                colvarHeavenBlessingTime.IsForeignKey = false;
                colvarHeavenBlessingTime.IsReadOnly = false;
                colvarHeavenBlessingTime.DefaultSetting = @"";
                colvarHeavenBlessingTime.ForeignKeyTableName = "";
                schema.Columns.Add(colvarHeavenBlessingTime);

                TableSchema.TableColumn colvarBlessTime = new TableSchema.TableColumn(schema);
                colvarBlessTime.ColumnName = "BlessTime";
                colvarBlessTime.DataType = DbType.Int64;
                colvarBlessTime.MaxLength = 0;
                colvarBlessTime.AutoIncrement = false;
                colvarBlessTime.IsNullable = true;
                colvarBlessTime.IsPrimaryKey = false;
                colvarBlessTime.IsForeignKey = false;
                colvarBlessTime.IsReadOnly = false;
                colvarBlessTime.DefaultSetting = @"";
                colvarBlessTime.ForeignKeyTableName = "";
                schema.Columns.Add(colvarBlessTime);

                TableSchema.TableColumn colvarLastDragonBallUse = new TableSchema.TableColumn(schema);
                colvarLastDragonBallUse.ColumnName = "LastDragonBallUse";
                colvarLastDragonBallUse.DataType = DbType.Int64;
                colvarLastDragonBallUse.MaxLength = 255;
                colvarLastDragonBallUse.AutoIncrement = false;
                colvarLastDragonBallUse.IsNullable = true;
                colvarLastDragonBallUse.IsPrimaryKey = false;
                colvarLastDragonBallUse.IsForeignKey = false;
                colvarLastDragonBallUse.IsReadOnly = false;
                colvarLastDragonBallUse.DefaultSetting = @"";
                colvarLastDragonBallUse.ForeignKeyTableName = "";
                schema.Columns.Add(colvarLastDragonBallUse);

                TableSchema.TableColumn colvarLastResetTime = new TableSchema.TableColumn(schema);
                colvarLastResetTime.ColumnName = "LastResetTime";
                colvarLastResetTime.DataType = DbType.Int64;
                colvarLastResetTime.MaxLength = 255;
                colvarLastResetTime.AutoIncrement = false;
                colvarLastResetTime.IsNullable = true;
                colvarLastResetTime.IsPrimaryKey = false;
                colvarLastResetTime.IsForeignKey = false;
                colvarLastResetTime.IsReadOnly = false;
                colvarLastResetTime.DefaultSetting = @"";
                colvarLastResetTime.ForeignKeyTableName = "";
                schema.Columns.Add(colvarLastResetTime);

                TableSchema.TableColumn colvarEnlightsReceived = new TableSchema.TableColumn(schema);
                colvarEnlightsReceived.ColumnName = "EnlightsReceived";
                colvarEnlightsReceived.DataType = DbType.Boolean;
                colvarEnlightsReceived.MaxLength = 0;
                colvarEnlightsReceived.AutoIncrement = false;
                colvarEnlightsReceived.IsNullable = true;
                colvarEnlightsReceived.IsPrimaryKey = false;
                colvarEnlightsReceived.IsForeignKey = false;
                colvarEnlightsReceived.IsReadOnly = false;
                colvarEnlightsReceived.DefaultSetting = @"";
                colvarEnlightsReceived.ForeignKeyTableName = "";
                schema.Columns.Add(colvarEnlightsReceived);

                TableSchema.TableColumn colvarFlag = new TableSchema.TableColumn(schema);
                colvarFlag.ColumnName = "Flag";
                colvarFlag.DataType = DbType.UInt16;
                colvarFlag.MaxLength = 0;
                colvarFlag.AutoIncrement = false;
                colvarFlag.IsNullable = true;
                colvarFlag.IsPrimaryKey = false;
                colvarFlag.IsForeignKey = false;
                colvarFlag.IsReadOnly = false;
                colvarFlag.DefaultSetting = @"";
                colvarFlag.ForeignKeyTableName = "";
                schema.Columns.Add(colvarFlag);

                TableSchema.TableColumn colvarEnlightmentWait = new TableSchema.TableColumn(schema);
                colvarEnlightmentWait.ColumnName = "EnlightmentWait";
                colvarEnlightmentWait.DataType = DbType.String;
                colvarEnlightmentWait.MaxLength = 0;
                colvarEnlightmentWait.AutoIncrement = false;
                colvarEnlightmentWait.IsNullable = true;
                colvarEnlightmentWait.IsPrimaryKey = false;
                colvarEnlightmentWait.IsForeignKey = false;
                colvarEnlightmentWait.IsReadOnly = false;
                colvarEnlightmentWait.DefaultSetting = @"";
                colvarEnlightmentWait.ForeignKeyTableName = "";
                schema.Columns.Add(colvarEnlightmentWait);

                TableSchema.TableColumn colvarDoubleExpToday = new TableSchema.TableColumn(schema);
                colvarDoubleExpToday.ColumnName = "DoubleExpToday";
                colvarDoubleExpToday.DataType = DbType.Boolean;
                colvarDoubleExpToday.MaxLength = 0;
                colvarDoubleExpToday.AutoIncrement = false;
                colvarDoubleExpToday.IsNullable = true;
                colvarDoubleExpToday.IsPrimaryKey = false;
                colvarDoubleExpToday.IsForeignKey = false;
                colvarDoubleExpToday.IsReadOnly = false;
                colvarDoubleExpToday.DefaultSetting = @"";
                colvarDoubleExpToday.ForeignKeyTableName = "";
                schema.Columns.Add(colvarDoubleExpToday);

                TableSchema.TableColumn colvarGuildID = new TableSchema.TableColumn(schema);
                colvarGuildID.ColumnName = "GuildID";
                colvarGuildID.DataType = DbType.Int64;
                colvarGuildID.MaxLength = 0;
                colvarGuildID.AutoIncrement = false;
                colvarGuildID.IsNullable = true;
                colvarGuildID.IsPrimaryKey = false;
                colvarGuildID.IsForeignKey = false;
                colvarGuildID.IsReadOnly = false;
                colvarGuildID.DefaultSetting = @"";
                colvarGuildID.ForeignKeyTableName = "";
                schema.Columns.Add(colvarGuildID);

                TableSchema.TableColumn colvarGuildRank = new TableSchema.TableColumn(schema);
                colvarGuildRank.ColumnName = "GuildRank";
                colvarGuildRank.DataType = DbType.Int64;
                colvarGuildRank.MaxLength = 0;
                colvarGuildRank.AutoIncrement = false;
                colvarGuildRank.IsNullable = true;
                colvarGuildRank.IsPrimaryKey = false;
                colvarGuildRank.IsForeignKey = false;
                colvarGuildRank.IsReadOnly = false;
                colvarGuildRank.DefaultSetting = @"";
                colvarGuildRank.ForeignKeyTableName = "";
                schema.Columns.Add(colvarGuildRank);

                TableSchema.TableColumn colvarGuildSilverDonation = new TableSchema.TableColumn(schema);
                colvarGuildSilverDonation.ColumnName = "GuildSilverDonation";
                colvarGuildSilverDonation.DataType = DbType.Int64;
                colvarGuildSilverDonation.MaxLength = 0;
                colvarGuildSilverDonation.AutoIncrement = false;
                colvarGuildSilverDonation.IsNullable = true;
                colvarGuildSilverDonation.IsPrimaryKey = false;
                colvarGuildSilverDonation.IsForeignKey = false;
                colvarGuildSilverDonation.IsReadOnly = false;
                colvarGuildSilverDonation.DefaultSetting = @"";
                colvarGuildSilverDonation.ForeignKeyTableName = "";
                schema.Columns.Add(colvarGuildSilverDonation);

                TableSchema.TableColumn colvarGuildConquerPointDonation = new TableSchema.TableColumn(schema);
                colvarGuildConquerPointDonation.ColumnName = "GuildConquerPointDonation";
                colvarGuildConquerPointDonation.DataType = DbType.Int64;
                colvarGuildConquerPointDonation.MaxLength = 0;
                colvarGuildConquerPointDonation.AutoIncrement = false;
                colvarGuildConquerPointDonation.IsNullable = true;
                colvarGuildConquerPointDonation.IsPrimaryKey = false;
                colvarGuildConquerPointDonation.IsForeignKey = false;
                colvarGuildConquerPointDonation.IsReadOnly = false;
                colvarGuildConquerPointDonation.DefaultSetting = @"";
                colvarGuildConquerPointDonation.ForeignKeyTableName = "";
                schema.Columns.Add(colvarGuildConquerPointDonation);

                TableSchema.TableColumn colvarVIPLevel = new TableSchema.TableColumn(schema);
                colvarVIPLevel.ColumnName = "VIPLevel";
                colvarVIPLevel.DataType = DbType.Boolean;
                colvarVIPLevel.MaxLength = 0;
                colvarVIPLevel.AutoIncrement = false;
                colvarVIPLevel.IsNullable = true;
                colvarVIPLevel.IsPrimaryKey = false;
                colvarVIPLevel.IsForeignKey = false;
                colvarVIPLevel.IsReadOnly = false;
                colvarVIPLevel.DefaultSetting = @"";
                colvarVIPLevel.ForeignKeyTableName = "";
                schema.Columns.Add(colvarVIPLevel);

                TableSchema.TableColumn colvarVirtuePoints = new TableSchema.TableColumn(schema);
                colvarVirtuePoints.ColumnName = "VirtuePoints";
                colvarVirtuePoints.DataType = DbType.Int64;
                colvarVirtuePoints.MaxLength = 0;
                colvarVirtuePoints.AutoIncrement = false;
                colvarVirtuePoints.IsNullable = true;
                colvarVirtuePoints.IsPrimaryKey = false;
                colvarVirtuePoints.IsForeignKey = false;
                colvarVirtuePoints.IsReadOnly = false;
                colvarVirtuePoints.DefaultSetting = @"";
                colvarVirtuePoints.ForeignKeyTableName = "";
                schema.Columns.Add(colvarVirtuePoints);

                TableSchema.TableColumn colvarHeadgearClaim = new TableSchema.TableColumn(schema);
                colvarHeadgearClaim.ColumnName = "HeadgearClaim";
                colvarHeadgearClaim.DataType = DbType.Boolean;
                colvarHeadgearClaim.MaxLength = 0;
                colvarHeadgearClaim.AutoIncrement = false;
                colvarHeadgearClaim.IsNullable = true;
                colvarHeadgearClaim.IsPrimaryKey = false;
                colvarHeadgearClaim.IsForeignKey = false;
                colvarHeadgearClaim.IsReadOnly = false;
                colvarHeadgearClaim.DefaultSetting = @"";
                colvarHeadgearClaim.ForeignKeyTableName = "";
                schema.Columns.Add(colvarHeadgearClaim);

                TableSchema.TableColumn colvarNecklaceClaim = new TableSchema.TableColumn(schema);
                colvarNecklaceClaim.ColumnName = "NecklaceClaim";
                colvarNecklaceClaim.DataType = DbType.Boolean;
                colvarNecklaceClaim.MaxLength = 0;
                colvarNecklaceClaim.AutoIncrement = false;
                colvarNecklaceClaim.IsNullable = true;
                colvarNecklaceClaim.IsPrimaryKey = false;
                colvarNecklaceClaim.IsForeignKey = false;
                colvarNecklaceClaim.IsReadOnly = false;
                colvarNecklaceClaim.DefaultSetting = @"";
                colvarNecklaceClaim.ForeignKeyTableName = "";
                schema.Columns.Add(colvarNecklaceClaim);

                TableSchema.TableColumn colvarArmorClaim = new TableSchema.TableColumn(schema);
                colvarArmorClaim.ColumnName = "ArmorClaim";
                colvarArmorClaim.DataType = DbType.Boolean;
                colvarArmorClaim.MaxLength = 0;
                colvarArmorClaim.AutoIncrement = false;
                colvarArmorClaim.IsNullable = true;
                colvarArmorClaim.IsPrimaryKey = false;
                colvarArmorClaim.IsForeignKey = false;
                colvarArmorClaim.IsReadOnly = false;
                colvarArmorClaim.DefaultSetting = @"";
                colvarArmorClaim.ForeignKeyTableName = "";
                schema.Columns.Add(colvarArmorClaim);

                TableSchema.TableColumn colvarWeaponClaim = new TableSchema.TableColumn(schema);
                colvarWeaponClaim.ColumnName = "WeaponClaim";
                colvarWeaponClaim.DataType = DbType.Boolean;
                colvarWeaponClaim.MaxLength = 0;
                colvarWeaponClaim.AutoIncrement = false;
                colvarWeaponClaim.IsNullable = true;
                colvarWeaponClaim.IsPrimaryKey = false;
                colvarWeaponClaim.IsForeignKey = false;
                colvarWeaponClaim.IsReadOnly = false;
                colvarWeaponClaim.DefaultSetting = @"";
                colvarWeaponClaim.ForeignKeyTableName = "";
                schema.Columns.Add(colvarWeaponClaim);

                TableSchema.TableColumn colvarBootsClaim = new TableSchema.TableColumn(schema);
                colvarBootsClaim.ColumnName = "BootsClaim";
                colvarBootsClaim.DataType = DbType.Boolean;
                colvarBootsClaim.MaxLength = 0;
                colvarBootsClaim.AutoIncrement = false;
                colvarBootsClaim.IsNullable = true;
                colvarBootsClaim.IsPrimaryKey = false;
                colvarBootsClaim.IsForeignKey = false;
                colvarBootsClaim.IsReadOnly = false;
                colvarBootsClaim.DefaultSetting = @"";
                colvarBootsClaim.ForeignKeyTableName = "";
                schema.Columns.Add(colvarBootsClaim);

                TableSchema.TableColumn colvarTowerClaim = new TableSchema.TableColumn(schema);
                colvarTowerClaim.ColumnName = "TowerClaim";
                colvarTowerClaim.DataType = DbType.Boolean;
                colvarTowerClaim.MaxLength = 0;
                colvarTowerClaim.AutoIncrement = false;
                colvarTowerClaim.IsNullable = true;
                colvarTowerClaim.IsPrimaryKey = false;
                colvarTowerClaim.IsForeignKey = false;
                colvarTowerClaim.IsReadOnly = false;
                colvarTowerClaim.DefaultSetting = @"";
                colvarTowerClaim.ForeignKeyTableName = "";
                schema.Columns.Add(colvarTowerClaim);

                TableSchema.TableColumn colvarFanClaim = new TableSchema.TableColumn(schema);
                colvarFanClaim.ColumnName = "FanClaim";
                colvarFanClaim.DataType = DbType.Boolean;
                colvarFanClaim.MaxLength = 0;
                colvarFanClaim.AutoIncrement = false;
                colvarFanClaim.IsNullable = true;
                colvarFanClaim.IsPrimaryKey = false;
                colvarFanClaim.IsForeignKey = false;
                colvarFanClaim.IsReadOnly = false;
                colvarFanClaim.DefaultSetting = @"";
                colvarFanClaim.ForeignKeyTableName = "";
                schema.Columns.Add(colvarFanClaim);

                TableSchema.TableColumn colvarRingClaim = new TableSchema.TableColumn(schema);
                colvarRingClaim.ColumnName = "RingClaim";
                colvarRingClaim.DataType = DbType.Boolean;
                colvarRingClaim.MaxLength = 0;
                colvarRingClaim.AutoIncrement = false;
                colvarRingClaim.IsNullable = true;
                colvarRingClaim.IsPrimaryKey = false;
                colvarRingClaim.IsForeignKey = false;
                colvarRingClaim.IsReadOnly = false;
                colvarRingClaim.DefaultSetting = @"";
                colvarRingClaim.ForeignKeyTableName = "";
                schema.Columns.Add(colvarRingClaim);

                TableSchema.TableColumn colvarVendingDisguise = new TableSchema.TableColumn(schema);
                colvarVendingDisguise.ColumnName = "VendingDisguise";
                colvarVendingDisguise.DataType = DbType.Int16;
                colvarVendingDisguise.MaxLength = 5;
                colvarVendingDisguise.AutoIncrement = false;
                colvarVendingDisguise.IsNullable = true;
                colvarVendingDisguise.IsPrimaryKey = false;
                colvarVendingDisguise.IsForeignKey = false;
                colvarVendingDisguise.IsReadOnly = false;
                colvarVendingDisguise.DefaultSetting = @"";
                colvarVendingDisguise.ForeignKeyTableName = "";
                schema.Columns.Add(colvarVendingDisguise);

                TableSchema.TableColumn colvarTotalDoantionGuild = new TableSchema.TableColumn(schema);
                colvarTotalDoantionGuild.ColumnName = "TotalDoantionGuild";
                colvarTotalDoantionGuild.DataType = DbType.UInt64;
                colvarTotalDoantionGuild.MaxLength = 255;
                colvarTotalDoantionGuild.AutoIncrement = false;
                colvarTotalDoantionGuild.IsNullable = true;
                colvarTotalDoantionGuild.IsPrimaryKey = false;
                colvarTotalDoantionGuild.IsForeignKey = false;
                colvarTotalDoantionGuild.IsReadOnly = false;
                colvarTotalDoantionGuild.DefaultSetting = @"";
                colvarTotalDoantionGuild.ForeignKeyTableName = "";
                schema.Columns.Add(colvarTotalDoantionGuild);

                TableSchema.TableColumn colvarChatBanTime = new TableSchema.TableColumn(schema);
                colvarChatBanTime.ColumnName = "ChatBanTime";
                colvarChatBanTime.DataType = DbType.Int64;
                colvarChatBanTime.MaxLength = 255;
                colvarChatBanTime.AutoIncrement = false;
                colvarChatBanTime.IsNullable = true;
                colvarChatBanTime.IsPrimaryKey = false;
                colvarChatBanTime.IsForeignKey = false;
                colvarChatBanTime.IsReadOnly = false;
                colvarChatBanTime.DefaultSetting = @"";
                colvarChatBanTime.ForeignKeyTableName = "";
                schema.Columns.Add(colvarChatBanTime);

                TableSchema.TableColumn colvarChatBanLasts = new TableSchema.TableColumn(schema);
                colvarChatBanLasts.ColumnName = "ChatBanLasts";
                colvarChatBanLasts.DataType = DbType.Int64;
                colvarChatBanLasts.MaxLength = 255;
                colvarChatBanLasts.AutoIncrement = false;
                colvarChatBanLasts.IsNullable = true;
                colvarChatBanLasts.IsPrimaryKey = false;
                colvarChatBanLasts.IsForeignKey = false;
                colvarChatBanLasts.IsReadOnly = false;
                colvarChatBanLasts.DefaultSetting = @"";
                colvarChatBanLasts.ForeignKeyTableName = "";
                schema.Columns.Add(colvarChatBanLasts);

                TableSchema.TableColumn colvarChatBanned = new TableSchema.TableColumn(schema);
                colvarChatBanned.ColumnName = "ChatBanned";
                colvarChatBanned.DataType = DbType.Boolean;
                colvarChatBanned.MaxLength = 5;
                colvarChatBanned.AutoIncrement = false;
                colvarChatBanned.IsNullable = true;
                colvarChatBanned.IsPrimaryKey = false;
                colvarChatBanned.IsForeignKey = false;
                colvarChatBanned.IsReadOnly = false;
                colvarChatBanned.DefaultSetting = @"";
                colvarChatBanned.ForeignKeyTableName = "";
                schema.Columns.Add(colvarChatBanned);

                TableSchema.TableColumn colvarInLottery = new TableSchema.TableColumn(schema);
                colvarInLottery.ColumnName = "InLottery";
                colvarInLottery.DataType = DbType.Boolean;
                colvarInLottery.MaxLength = 5;
                colvarInLottery.AutoIncrement = false;
                colvarInLottery.IsNullable = true;
                colvarInLottery.IsPrimaryKey = false;
                colvarInLottery.IsForeignKey = false;
                colvarInLottery.IsReadOnly = false;
                colvarInLottery.DefaultSetting = @"";
                colvarInLottery.ForeignKeyTableName = "";
                schema.Columns.Add(colvarInLottery);

                TableSchema.TableColumn colvarLotteryEntries = new TableSchema.TableColumn(schema);
                colvarLotteryEntries.ColumnName = "LotteryEntries";
                colvarLotteryEntries.DataType = DbType.String;
                colvarLotteryEntries.MaxLength = 10;
                colvarLotteryEntries.AutoIncrement = false;
                colvarLotteryEntries.IsNullable = true;
                colvarLotteryEntries.IsPrimaryKey = false;
                colvarLotteryEntries.IsForeignKey = false;
                colvarLotteryEntries.IsReadOnly = false;
                colvarLotteryEntries.DefaultSetting = @"";
                colvarLotteryEntries.ForeignKeyTableName = "";
                schema.Columns.Add(colvarLotteryEntries);

                TableSchema.TableColumn colvarLastLotteryEntry = new TableSchema.TableColumn(schema);
                colvarLastLotteryEntry.ColumnName = "LastLotteryEntry";
                colvarLastLotteryEntry.DataType = DbType.Int64;
                colvarLastLotteryEntry.MaxLength = 255;
                colvarLastLotteryEntry.AutoIncrement = false;
                colvarLastLotteryEntry.IsNullable = true;
                colvarLastLotteryEntry.IsPrimaryKey = false;
                colvarLastLotteryEntry.IsForeignKey = false;
                colvarLastLotteryEntry.IsReadOnly = false;
                colvarLastLotteryEntry.DefaultSetting = @"";
                colvarLastLotteryEntry.ForeignKeyTableName = "";
                schema.Columns.Add(colvarLastLotteryEntry);

                TableSchema.TableColumn colvarPreviousX = new TableSchema.TableColumn(schema);
                colvarPreviousX.ColumnName = "PreviousX";
                colvarPreviousX.DataType = DbType.String;
                colvarPreviousX.MaxLength = 10;
                colvarPreviousX.AutoIncrement = false;
                colvarPreviousX.IsNullable = true;
                colvarPreviousX.IsPrimaryKey = false;
                colvarPreviousX.IsForeignKey = false;
                colvarPreviousX.IsReadOnly = false;
                colvarPreviousX.DefaultSetting = @"";
                colvarPreviousX.ForeignKeyTableName = "";
                schema.Columns.Add(colvarPreviousX);

                TableSchema.TableColumn colvarPreviousY = new TableSchema.TableColumn(schema);
                colvarPreviousY.ColumnName = "PreviousY";
                colvarPreviousY.DataType = DbType.String;
                colvarPreviousY.MaxLength = 10;
                colvarPreviousY.AutoIncrement = false;
                colvarPreviousY.IsNullable = true;
                colvarPreviousY.IsPrimaryKey = false;
                colvarPreviousY.IsForeignKey = false;
                colvarPreviousY.IsReadOnly = false;
                colvarPreviousY.DefaultSetting = @"";
                colvarPreviousY.ForeignKeyTableName = "";
                schema.Columns.Add(colvarPreviousY);

                TableSchema.TableColumn colvarOfflineTGEnterTime = new TableSchema.TableColumn(schema);
                colvarOfflineTGEnterTime.ColumnName = "OfflineTGEnterTime";
                colvarOfflineTGEnterTime.DataType = DbType.Int64;
                colvarOfflineTGEnterTime.MaxLength = 255;
                colvarOfflineTGEnterTime.AutoIncrement = false;
                colvarOfflineTGEnterTime.IsNullable = true;
                colvarOfflineTGEnterTime.IsPrimaryKey = false;
                colvarOfflineTGEnterTime.IsForeignKey = false;
                colvarOfflineTGEnterTime.IsReadOnly = false;
                colvarOfflineTGEnterTime.DefaultSetting = @"";
                colvarOfflineTGEnterTime.ForeignKeyTableName = "";
                schema.Columns.Add(colvarOfflineTGEnterTime);

                TableSchema.TableColumn colvarExpBalls = new TableSchema.TableColumn(schema);
                colvarExpBalls.ColumnName = "ExpBalls";
                colvarExpBalls.DataType = DbType.String;
                colvarExpBalls.MaxLength = 10;
                colvarExpBalls.AutoIncrement = false;
                colvarExpBalls.IsNullable = true;
                colvarExpBalls.IsPrimaryKey = false;
                colvarExpBalls.IsForeignKey = false;
                colvarExpBalls.IsReadOnly = false;
                colvarExpBalls.DefaultSetting = @"";
                colvarExpBalls.ForeignKeyTableName = "";
                schema.Columns.Add(colvarExpBalls);

                TableSchema.TableColumn colvarStatus = new TableSchema.TableColumn(schema);
                colvarStatus.ColumnName = "Status";
                colvarStatus.DataType = DbType.Int64;
                colvarStatus.MaxLength = 18;
                colvarStatus.AutoIncrement = false;
                colvarStatus.IsNullable = true;
                colvarStatus.IsPrimaryKey = false;
                colvarStatus.IsForeignKey = false;
                colvarStatus.IsReadOnly = false;
                colvarStatus.DefaultSetting = @"";
                colvarStatus.ForeignKeyTableName = "";
                schema.Columns.Add(colvarStatus);

                TableSchema.TableColumn colvarStatus2 = new TableSchema.TableColumn(schema);
                colvarStatus2.ColumnName = "Status2";
                colvarStatus2.DataType = DbType.Int64;
                colvarStatus2.MaxLength = 18;
                colvarStatus2.AutoIncrement = false;
                colvarStatus2.IsNullable = true;
                colvarStatus2.IsPrimaryKey = false;
                colvarStatus2.IsForeignKey = false;
                colvarStatus2.IsReadOnly = false;
                colvarStatus2.DefaultSetting = @"";
                colvarStatus2.ForeignKeyTableName = "";
                schema.Columns.Add(colvarStatus2);

                TableSchema.TableColumn colvarClanId = new TableSchema.TableColumn(schema);
                colvarClanId.ColumnName = "ClanId";
                colvarClanId.DataType = DbType.Int32;
                colvarClanId.MaxLength = 0;
                colvarClanId.AutoIncrement = false;
                colvarClanId.IsNullable = true;
                colvarClanId.IsPrimaryKey = false;
                colvarClanId.IsForeignKey = false;
                colvarClanId.IsReadOnly = false;
                colvarClanId.DefaultSetting = @"";
                colvarClanId.ForeignKeyTableName = "";
                schema.Columns.Add(colvarClanId);

                TableSchema.TableColumn colvarClanDonation = new TableSchema.TableColumn(schema);
                colvarClanDonation.ColumnName = "ClanDonation";
                colvarClanDonation.DataType = DbType.Int64;
                colvarClanDonation.MaxLength = 0;
                colvarClanDonation.AutoIncrement = false;
                colvarClanDonation.IsNullable = true;
                colvarClanDonation.IsPrimaryKey = false;
                colvarClanDonation.IsForeignKey = false;
                colvarClanDonation.IsReadOnly = false;
                colvarClanDonation.DefaultSetting = @"";
                colvarClanDonation.ForeignKeyTableName = "";
                schema.Columns.Add(colvarClanDonation);

                TableSchema.TableColumn colvarClanRank = new TableSchema.TableColumn(schema);
                colvarClanRank.ColumnName = "ClanRank";
                colvarClanRank.DataType = DbType.Int32;
                colvarClanRank.MaxLength = 0;
                colvarClanRank.AutoIncrement = false;
                colvarClanRank.IsNullable = true;
                colvarClanRank.IsPrimaryKey = false;
                colvarClanRank.IsForeignKey = false;
                colvarClanRank.IsReadOnly = false;
                colvarClanRank.DefaultSetting = @"";
                colvarClanRank.ForeignKeyTableName = "";
                schema.Columns.Add(colvarClanRank);

                TableSchema.TableColumn colvarSubClass = new TableSchema.TableColumn(schema);
                colvarSubClass.ColumnName = "SubClass";
                colvarSubClass.DataType = DbType.Int32;
                colvarSubClass.MaxLength = 0;
                colvarSubClass.AutoIncrement = false;
                colvarSubClass.IsNullable = false;
                colvarSubClass.IsPrimaryKey = false;
                colvarSubClass.IsForeignKey = false;
                colvarSubClass.IsReadOnly = false;
                colvarSubClass.DefaultSetting = @"";
                colvarSubClass.ForeignKeyTableName = "";
                schema.Columns.Add(colvarSubClass);

                TableSchema.TableColumn colvarSubClassLevel = new TableSchema.TableColumn(schema);
                colvarSubClassLevel.ColumnName = "SubClassLevel";
                colvarSubClassLevel.DataType = DbType.Int32;
                colvarSubClassLevel.MaxLength = 0;
                colvarSubClassLevel.AutoIncrement = false;
                colvarSubClassLevel.IsNullable = false;
                colvarSubClassLevel.IsPrimaryKey = false;
                colvarSubClassLevel.IsForeignKey = false;
                colvarSubClassLevel.IsReadOnly = false;
                colvarSubClassLevel.DefaultSetting = @"";
                colvarSubClassLevel.ForeignKeyTableName = "";
                schema.Columns.Add(colvarSubClassLevel);

                TableSchema.TableColumn colvarStudyPoints = new TableSchema.TableColumn(schema);
                colvarStudyPoints.ColumnName = "StudyPoints";
                colvarStudyPoints.DataType = DbType.Int32;
                colvarStudyPoints.MaxLength = 0;
                colvarStudyPoints.AutoIncrement = false;
                colvarStudyPoints.IsNullable = true;
                colvarStudyPoints.IsPrimaryKey = false;
                colvarStudyPoints.IsForeignKey = false;
                colvarStudyPoints.IsReadOnly = false;
                colvarStudyPoints.DefaultSetting = @"";
                colvarStudyPoints.ForeignKeyTableName = "";
                schema.Columns.Add(colvarStudyPoints);

                TableSchema.TableColumn colvarLastlogin = new TableSchema.TableColumn(schema);
                colvarLastlogin.ColumnName = "LastLogin";
                colvarLastlogin.DataType = DbType.UInt64;
                colvarLastlogin.MaxLength = 0;
                colvarLastlogin.AutoIncrement = false;
                colvarLastlogin.IsNullable = true;
                colvarLastlogin.IsPrimaryKey = false;
                colvarLastlogin.IsForeignKey = false;
                colvarLastlogin.IsReadOnly = false;
                colvarLastlogin.DefaultSetting = @"";
                colvarLastlogin.ForeignKeyTableName = "";
                schema.Columns.Add(colvarLastlogin);

                TableSchema.TableColumn colvarGuildTulips = new TableSchema.TableColumn(schema);
                colvarGuildTulips.ColumnName = "GuildTulips";
                colvarGuildTulips.DataType = DbType.Int64;
                colvarGuildTulips.MaxLength = 0;
                colvarGuildTulips.AutoIncrement = false;
                colvarGuildTulips.IsNullable = true;
                colvarGuildTulips.IsPrimaryKey = false;
                colvarGuildTulips.IsForeignKey = false;
                colvarGuildTulips.IsReadOnly = false;
                colvarGuildTulips.DefaultSetting = @"";
                colvarGuildTulips.ForeignKeyTableName = "";
                schema.Columns.Add(colvarGuildTulips);

                TableSchema.TableColumn colvarStatus3 = new TableSchema.TableColumn(schema);
                colvarStatus3.ColumnName = "Status3";
                colvarStatus3.DataType = DbType.Int64;
                colvarStatus3.MaxLength = 18;
                colvarStatus3.AutoIncrement = false;
                colvarStatus3.IsNullable = true;
                colvarStatus3.IsPrimaryKey = false;
                colvarStatus3.IsForeignKey = false;
                colvarStatus3.IsReadOnly = false;
                colvarStatus3.DefaultSetting = @"";
                colvarStatus3.ForeignKeyTableName = "";
                schema.Columns.Add(colvarStatus3);

                TableSchema.TableColumn colvarStatus4 = new TableSchema.TableColumn(schema);
                colvarStatus4.ColumnName = "Status4";
                colvarStatus4.DataType = DbType.Int64;
                colvarStatus4.MaxLength = 18;
                colvarStatus4.AutoIncrement = false;
                colvarStatus4.IsNullable = true;
                colvarStatus4.IsPrimaryKey = false;
                colvarStatus4.IsForeignKey = false;
                colvarStatus4.IsReadOnly = false;
                colvarStatus4.DefaultSetting = @"";
                colvarStatus4.ForeignKeyTableName = "";
                schema.Columns.Add(colvarStatus4);

                TableSchema.TableColumn colvarArsenalDonation = new TableSchema.TableColumn(schema);
                colvarArsenalDonation.ColumnName = "ArsenalDonation";
                colvarArsenalDonation.DataType = DbType.Int64;
                colvarArsenalDonation.MaxLength = 255;
                colvarArsenalDonation.AutoIncrement = false;
                colvarArsenalDonation.IsNullable = true;
                colvarArsenalDonation.IsPrimaryKey = false;
                colvarArsenalDonation.IsForeignKey = false;
                colvarArsenalDonation.IsReadOnly = false;
                colvarArsenalDonation.DefaultSetting = @"";
                colvarArsenalDonation.ForeignKeyTableName = "";
                schema.Columns.Add(colvarArsenalDonation);

                TableSchema.TableColumn colvarReincarnationLev = new TableSchema.TableColumn(schema);
                colvarReincarnationLev.ColumnName = "ReincarnationLev";
                colvarReincarnationLev.DataType = DbType.Int64;
                colvarReincarnationLev.MaxLength = 18;
                colvarReincarnationLev.AutoIncrement = false;
                colvarReincarnationLev.IsNullable = true;
                colvarReincarnationLev.IsPrimaryKey = false;
                colvarReincarnationLev.IsForeignKey = false;
                colvarReincarnationLev.IsReadOnly = false;
                colvarReincarnationLev.DefaultSetting = @"";
                colvarReincarnationLev.ForeignKeyTableName = "";
                schema.Columns.Add(colvarReincarnationLev);

                TableSchema.TableColumn colvarGuildOrchids = new TableSchema.TableColumn(schema);
                colvarGuildOrchids.ColumnName = "GuildOrchids";
                colvarGuildOrchids.DataType = DbType.Int64;
                colvarGuildOrchids.MaxLength = 255;
                colvarGuildOrchids.AutoIncrement = false;
                colvarGuildOrchids.IsNullable = true;
                colvarGuildOrchids.IsPrimaryKey = false;
                colvarGuildOrchids.IsForeignKey = false;
                colvarGuildOrchids.IsReadOnly = false;
                colvarGuildOrchids.DefaultSetting = @"";
                colvarGuildOrchids.ForeignKeyTableName = "";
                schema.Columns.Add(colvarGuildOrchids);

                TableSchema.TableColumn colvarGuildRouses = new TableSchema.TableColumn(schema);
                colvarGuildRouses.ColumnName = "GuildRouses";
                colvarGuildRouses.DataType = DbType.Int64;
                colvarGuildRouses.MaxLength = 255;
                colvarGuildRouses.AutoIncrement = false;
                colvarGuildRouses.IsNullable = true;
                colvarGuildRouses.IsPrimaryKey = false;
                colvarGuildRouses.IsForeignKey = false;
                colvarGuildRouses.IsReadOnly = false;
                colvarGuildRouses.DefaultSetting = @"";
                colvarGuildRouses.ForeignKeyTableName = "";
                schema.Columns.Add(colvarGuildRouses);

                TableSchema.TableColumn colvarGuildLilies = new TableSchema.TableColumn(schema);
                colvarGuildLilies.ColumnName = "GuildLilies";
                colvarGuildLilies.DataType = DbType.Int64;
                colvarGuildLilies.MaxLength = 255;
                colvarGuildLilies.AutoIncrement = false;
                colvarGuildLilies.IsNullable = true;
                colvarGuildLilies.IsPrimaryKey = false;
                colvarGuildLilies.IsForeignKey = false;
                colvarGuildLilies.IsReadOnly = false;
                colvarGuildLilies.DefaultSetting = @"";
                colvarGuildLilies.ForeignKeyTableName = "";
                schema.Columns.Add(colvarGuildLilies);

                TableSchema.TableColumn colvarCTFCpsReward = new TableSchema.TableColumn(schema);
                colvarCTFCpsReward.ColumnName = "CTFCpsReward";
                colvarCTFCpsReward.DataType = DbType.Int64;
                colvarCTFCpsReward.MaxLength = 255;
                colvarCTFCpsReward.AutoIncrement = false;
                colvarCTFCpsReward.IsNullable = true;
                colvarCTFCpsReward.IsPrimaryKey = false;
                colvarCTFCpsReward.IsForeignKey = false;
                colvarCTFCpsReward.IsReadOnly = false;
                colvarCTFCpsReward.DefaultSetting = @"";
                colvarCTFCpsReward.ForeignKeyTableName = "";
                schema.Columns.Add(colvarCTFCpsReward);

                TableSchema.TableColumn colvarExploits = new TableSchema.TableColumn(schema);
                colvarExploits.ColumnName = "Exploits";
                colvarExploits.DataType = DbType.Int64;
                colvarExploits.MaxLength = 255;
                colvarExploits.AutoIncrement = false;
                colvarExploits.IsNullable = true;
                colvarExploits.IsPrimaryKey = false;
                colvarExploits.IsForeignKey = false;
                colvarExploits.IsReadOnly = false;
                colvarExploits.DefaultSetting = @"";
                colvarExploits.ForeignKeyTableName = "";
                schema.Columns.Add(colvarExploits);

                TableSchema.TableColumn colvarCTFSilverReward = new TableSchema.TableColumn(schema);
                colvarCTFSilverReward.ColumnName = "CTFSilverReward";
                colvarCTFSilverReward.DataType = DbType.Int64;
                colvarCTFSilverReward.MaxLength = 255;
                colvarCTFSilverReward.AutoIncrement = false;
                colvarCTFSilverReward.IsNullable = true;
                colvarCTFSilverReward.IsPrimaryKey = false;
                colvarCTFSilverReward.IsForeignKey = false;
                colvarCTFSilverReward.IsReadOnly = false;
                colvarCTFSilverReward.DefaultSetting = @"";
                colvarCTFSilverReward.ForeignKeyTableName = "";
                schema.Columns.Add(colvarCTFSilverReward);


                BaseSchema = schema;
                //add this schema to the provider
                //so we can query it later
                DataService.Providers["Conquer_Server"].AddSchema("entities", schema);
            }
        }
        #endregion
        #region Props

        [XmlAttribute("Name")]
        [Bindable(true)]
        public string Name
        {
            get { return GetColumnValue<string>(Columns.Name); }
            set { SetColumnValue(Columns.Name, value); }
        }

        [XmlAttribute("Uid")]
        [Bindable(true)]
        public uint Uid
        {
            get { return GetColumnValue<uint>(Columns.Uid); }
            set { SetColumnValue(Columns.Uid, value); }
        }

        [XmlAttribute("Flower")]
        [Bindable(true)]
        public uint Flower
        {
            get { return GetColumnValue<uint>(Columns.Flower); }
            set { SetColumnValue(Columns.Flower, value); }
        }

        [XmlAttribute("Owner")]
        [Bindable(true)]
        public string Owner
        {
            get { return GetColumnValue<string>(Columns.Owner); }
            set { SetColumnValue(Columns.Owner, value); }
        }

        [XmlAttribute("HairStyle")]
        [Bindable(true)]
        public ushort HairStyle
        {
            get { return GetColumnValue<ushort>(Columns.HairStyle); }
            set { SetColumnValue(Columns.HairStyle, value); }
        }

        [XmlAttribute("ClassX")]
        [Bindable(true)]
        public byte ClassX
        {
            get { return GetColumnValue<byte>(Columns.ClassX); }
            set { SetColumnValue(Columns.ClassX, value); }
        }

        [XmlAttribute("Money")]
        [Bindable(true)]
        public ulong Money
        {
            get { return GetColumnValue<ulong>(Columns.Money); }
            set { SetColumnValue(Columns.Money, value); }
        }

        [XmlAttribute("ConquerPoints")]
        [Bindable(true)]
        public ulong ConquerPoints
        {
            get { return GetColumnValue<ulong>(Columns.ConquerPoints); }
            set { SetColumnValue(Columns.ConquerPoints, value); }
        }
        [XmlAttribute("Treepoints")]
        [Bindable(true)]
        public ushort Treepoints
        {
            get { return GetColumnValue<ushort>(Columns.Treepoints); }
            set { SetColumnValue(Columns.ConquerPoints, value); }
        }
        [XmlAttribute("ReincarnationLev")]
        [Bindable(true)]
        public ulong ReincarnationLev
        {
            get { return GetColumnValue<ulong>(Columns.ReincarnationLev); }
            set { SetColumnValue(Columns.ReincarnationLev, value); }
        }

        [XmlAttribute("Body")]
        [Bindable(true)]
        public short Body
        {
            get { return GetColumnValue<short>(Columns.Body); }
            set { SetColumnValue(Columns.Body, value); }
        }

        [XmlAttribute("Face")]
        [Bindable(true)]
        public short Face
        {
            get { return GetColumnValue<short>(Columns.Face); }
            set { SetColumnValue(Columns.Face, value); }
        }

        [XmlAttribute("Level")]
        [Bindable(true)]
        public byte Level
        {
            get { return GetColumnValue<byte>(Columns.Level); }
            set { SetColumnValue(Columns.Level, value); }
        }

        [XmlAttribute("Strength")]
        [Bindable(true)]
        public ushort Strength
        {
            get { return GetColumnValue<ushort>(Columns.Strength); }
            set { SetColumnValue(Columns.Strength, value); }
        }

        [XmlAttribute("Agility")]
        [Bindable(true)]
        public ushort Agility
        {
            get { return GetColumnValue<ushort>(Columns.Agility); }
            set { SetColumnValue(Columns.Agility, value); }
        }

        [XmlAttribute("Vitality")]
        [Bindable(true)]
        public ushort Vitality
        {
            get { return GetColumnValue<ushort>(Columns.Vitality); }
            set { SetColumnValue(Columns.Vitality, value); }
        }

        [XmlAttribute("Spirit")]
        [Bindable(true)]
        public ushort Spirit
        {
            get { return GetColumnValue<ushort>(Columns.Spirit); }
            set { SetColumnValue(Columns.Spirit, value); }
        }

        [XmlAttribute("Atributes")]
        [Bindable(true)]
        public ushort Atributes
        {
            get { return GetColumnValue<ushort>(Columns.Atributes); }
            set { SetColumnValue(Columns.Atributes, value); }
        }

        [XmlAttribute("BoundCPs")]
        [Bindable(true)]
        public ulong BoundCps
        {
            get { return GetColumnValue<ulong>(Columns.BoundCPs); }
            set { SetColumnValue(Columns.BoundCPs, value); }
        }

        [XmlAttribute("Hitpoints")]
        [Bindable(true)]
        public uint Hitpoints
        {
            get { return GetColumnValue<uint>(Columns.Hitpoints); }
            set { SetColumnValue(Columns.Hitpoints, value); }
        }

        [XmlAttribute("Mana")]
        [Bindable(true)]
        public ushort Mana
        {
            get { return GetColumnValue<ushort>(Columns.Mana); }
            set { SetColumnValue(Columns.Mana, value); }
        }

        [XmlAttribute("MapID")]
        [Bindable(true)]
        public ushort MapID
        {
            get { return GetColumnValue<ushort>(Columns.MapID); }
            set { SetColumnValue(Columns.MapID, value); }
        }

        [XmlAttribute("X")]
        [Bindable(true)]
        public ushort X
        {
            get { return GetColumnValue<ushort>(Columns.X); }
            set { SetColumnValue(Columns.X, value); }
        }

        [XmlAttribute("Y")]
        [Bindable(true)]
        public ushort Y
        {
            get { return GetColumnValue<ushort>(Columns.Y); }
            set { SetColumnValue(Columns.Y, value); }
        }

        [XmlAttribute("PKPoints")]
        [Bindable(true)]
        public ushort PKPoints
        {
            get { return GetColumnValue<ushort>(Columns.PKPoints); }
            set { SetColumnValue(Columns.PKPoints, value); }
        }

        [XmlAttribute("Experience")]
        [Bindable(true)]
        public ulong Experience
        {
            get { return GetColumnValue<ulong>(Columns.Experience); }
            set { SetColumnValue(Columns.Experience, value); }
        }

        [XmlAttribute("QuizPoints")]
        [Bindable(true)]
        public uint QuizPoints
        {
            get { return GetColumnValue<uint>(Columns.QuizPoints); }
            set { SetColumnValue(Columns.QuizPoints, value); }
        }

        [XmlAttribute("PreviousMapID")]
        [Bindable(true)]
        public ushort PreviousMapID
        {
            get { return GetColumnValue<ushort>(Columns.PreviousMapID); }
            set { SetColumnValue(Columns.PreviousMapID, value); }
        }

        [XmlAttribute("Reborn")]
        [Bindable(true)]
        public byte Reborn
        {
            get { return GetColumnValue<byte>(Columns.Reborn); }
            set { SetColumnValue(Columns.Reborn, value); }
        }

        [XmlAttribute("FirstClass")]
        [Bindable(true)]
        public bool FirstClass
        {
            get { return GetColumnValue<bool>(Columns.FirstClass); }
            set { SetColumnValue(Columns.FirstClass, value); }
        }

        [XmlAttribute("SecondClass")]
        [Bindable(true)]
        public bool SecondClass
        {
            get { return GetColumnValue<bool>(Columns.SecondClass); }
            set { SetColumnValue(Columns.SecondClass, value); }
        }

        [XmlAttribute("ThirdClass")]
        [Bindable(true)]
        public bool ThirdClass
        {
            get { return GetColumnValue<bool>(Columns.ThirdClass); }
            set { SetColumnValue(Columns.ThirdClass, value); }
        }

        [XmlAttribute("Spouse")]
        [Bindable(true)]
        public string Spouse
        {
            get { return GetColumnValue<string>(Columns.Spouse); }
            set { SetColumnValue(Columns.Spouse, value); }
        }

        [XmlAttribute("WarehousePW")]
        [Bindable(true)]
        public uint WarehousePW
        {
            get { return GetColumnValue<uint>(Columns.WarehousePW); }
            set { SetColumnValue(Columns.WarehousePW, value); }
        }

        [XmlAttribute("MoneySave")]
        [Bindable(true)]
        public ulong MoneySave
        {
            get { return GetColumnValue<ulong>(Columns.MoneySave); }
            set { SetColumnValue(Columns.MoneySave, value); }
        }

        [XmlAttribute("FirstRebornClass")]
        [Bindable(true)]
        public byte FirstRebornClass
        {
            get { return GetColumnValue<byte>(Columns.FirstRebornClass); }
            set { SetColumnValue(Columns.FirstRebornClass, value); }
        }

        [XmlAttribute("SecondRebornClass")]
        [Bindable(true)]
        public byte SecondRebornClass
        {
            get { return GetColumnValue<byte>(Columns.SecondRebornClass); }
            set { SetColumnValue(Columns.SecondRebornClass, value); }
        }

        [XmlAttribute("FirstRebornLevel")]
        [Bindable(true)]
        public byte FirstRebornLevel
        {
            get { return GetColumnValue<byte>(Columns.FirstRebornLevel); }
            set { SetColumnValue(Columns.FirstRebornLevel, value); }
        }

        [XmlAttribute("SecondRebornLevel")]
        [Bindable(true)]
        public byte SecondRebornLevel
        {
            get { return GetColumnValue<byte>(Columns.SecondRebornLevel); }
            set { SetColumnValue(Columns.SecondRebornLevel, value); }
        }

        [XmlAttribute("Online")]
        [Bindable(true)]
        public bool Online
        {
            get { return GetColumnValue<bool>(Columns.Online); }
            set { SetColumnValue(Columns.Online, value); }
        }

        [XmlAttribute("EnlightenPoints")]
        [Bindable(true)]
        public ushort EnlightenPoints
        {
            get { return GetColumnValue<ushort>(Columns.EnlightenPoints); }
            set { SetColumnValue(Columns.EnlightenPoints, value); }
        }

        [XmlAttribute("DoubleExpTime")]
        [Bindable(true)]
        public ushort DoubleExpTime
        {
            get { return GetColumnValue<ushort>(Columns.DoubleExpTime); }
            set { SetColumnValue(Columns.DoubleExpTime, value); }
        }

        [XmlAttribute("Gwkill")]
        [Bindable(true)]
        public ulong Gwkill
        {
            get { return GetColumnValue<ulong>(Columns.Gwkill); }
            set { SetColumnValue(Columns.Gwkill, value); }
        }

        [XmlAttribute("HeavenBlessingTime")]
        [Bindable(true)]
        public uint HeavenBlessingTime
        {
            get { return GetColumnValue<uint>(Columns.HeavenBlessingTime); }
            set { SetColumnValue(Columns.HeavenBlessingTime, value); }
        }

        [XmlAttribute("BlessTime")]
        [Bindable(true)]
        public uint BlessTime
        {
            get { return GetColumnValue<uint>(Columns.BlessTime); }
            set { SetColumnValue(Columns.BlessTime, value); }
        }

        [XmlAttribute("LastDragonBallUse")]
        [Bindable(true)]
        public string LastDragonBallUse
        {
            get { return GetColumnValue<string>(Columns.LastDragonBallUse); }
            set { SetColumnValue(Columns.LastDragonBallUse, value); }
        }

        [XmlAttribute("LastLogin")]
        [Bindable(true)]
        public UInt64 LastLogin
        {
            get { return GetColumnValue<UInt64>(Columns.LastLogin); }
            set { SetColumnValue(Columns.LastLogin, value); }
        }

        [XmlAttribute("LastResetTime")]
        [Bindable(true)]
        public long LastResetTime
        {
            get { return GetColumnValue<long>(Columns.LastResetTime); }
            set { SetColumnValue(Columns.LastResetTime, value); }
        }
        [XmlAttribute("EnlightsReceived")]
        [Bindable(true)]
        public byte EnlightsReceived
        {
            get { return GetColumnValue<byte>(Columns.EnlightsReceived); }
            set { SetColumnValue(Columns.EnlightsReceived, value); }
        }
        [XmlAttribute("Flag")]
        [Bindable(true)]
        public ushort Flag
        {
            get { return GetColumnValue<ushort>(Columns.Flag); }
            set { SetColumnValue(Columns.Flag, value); }
        }

        [XmlAttribute("EnlightmentWait")]
        [Bindable(true)]
        public ushort EnlightmentWait
        {
            get { return GetColumnValue<ushort>(Columns.EnlightmentWait); }
            set { SetColumnValue(Columns.EnlightmentWait, value); }
        }

        [XmlAttribute("DoubleExpToday")]
        [Bindable(true)]
        public bool DoubleExpToday
        {
            get { return GetColumnValue<bool>(Columns.DoubleExpToday); }
            set { SetColumnValue(Columns.DoubleExpToday, value); }
        }

        [XmlAttribute("GuildID")]
        [Bindable(true)]
        public uint GuildID
        {
            get { return GetColumnValue<uint>(Columns.GuildID); }
            set { SetColumnValue(Columns.GuildID, value); }
        }

        [XmlAttribute("GuildRank")]
        [Bindable(true)]
        public ushort GuildRank
        {
            get { return GetColumnValue<ushort>(Columns.GuildRank); }
            set { SetColumnValue(Columns.GuildRank, value); }
        }

        [XmlAttribute("GuildSilverDonation")]
        [Bindable(true)]
        public ulong GuildSilverDonation
        {
            get { return GetColumnValue<ulong>(Columns.GuildSilverDonation); }
            set { SetColumnValue(Columns.GuildSilverDonation, value); }
        }

        [XmlAttribute("GuildConquerPointDonation")]
        [Bindable(true)]
        public ulong GuildConquerPointDonation
        {
            get { return GetColumnValue<ulong>(Columns.GuildConquerPointDonation); }
            set { SetColumnValue(Columns.GuildConquerPointDonation, value); }
        }

        [XmlAttribute("VIPLevel")]
        [Bindable(true)]
        public byte VIPLevel
        {
            get { return GetColumnValue<byte>(Columns.VIPLevel); }
            set { SetColumnValue(Columns.VIPLevel, value); }
        }

        [XmlAttribute("VirtuePoints")]
        [Bindable(true)]
        public ulong VirtuePoints
        {
            get { return GetColumnValue<ulong>(Columns.VirtuePoints); }
            set { SetColumnValue(Columns.VirtuePoints, value); }
        }

        [XmlAttribute("HeadgearClaim")]
        [Bindable(true)]
        public bool HeadgearClaim
        {
            get { return GetColumnValue<bool>(Columns.HeadgearClaim); }
            set { SetColumnValue(Columns.HeadgearClaim, value); }
        }

        [XmlAttribute("NecklaceClaim")]
        [Bindable(true)]
        public bool NecklaceClaim
        {
            get { return GetColumnValue<bool>(Columns.NecklaceClaim); }
            set { SetColumnValue(Columns.NecklaceClaim, value); }
        }

        [XmlAttribute("ArmorClaim")]
        [Bindable(true)]
        public bool ArmorClaim
        {
            get { return GetColumnValue<bool>(Columns.ArmorClaim); }
            set { SetColumnValue(Columns.ArmorClaim, value); }
        }

        [XmlAttribute("WeaponClaim")]
        [Bindable(true)]
        public bool WeaponClaim
        {
            get { return GetColumnValue<bool>(Columns.WeaponClaim); }
            set { SetColumnValue(Columns.WeaponClaim, value); }
        }

        [XmlAttribute("BootsClaim")]
        [Bindable(true)]
        public bool BootsClaim
        {
            get { return GetColumnValue<bool>(Columns.BootsClaim); }
            set { SetColumnValue(Columns.BootsClaim, value); }
        }

        [XmlAttribute("TowerClaim")]
        [Bindable(true)]
        public bool TowerClaim
        {
            get { return GetColumnValue<bool>(Columns.TowerClaim); }
            set { SetColumnValue(Columns.TowerClaim, value); }
        }

        [XmlAttribute("FanClaim")]
        [Bindable(true)]
        public bool FanClaim
        {
            get { return GetColumnValue<bool>(Columns.FanClaim); }
            set { SetColumnValue(Columns.FanClaim, value); }
        }

        [XmlAttribute("RingClaim")]
        [Bindable(true)]
        public bool RingClaim
        {
            get { return GetColumnValue<bool>(Columns.RingClaim); }
            set { SetColumnValue(Columns.RingClaim, value); }
        }

        [XmlAttribute("VendingDisguise")]
        [Bindable(true)]
        public ushort VendingDisguise
        {
            get { return GetColumnValue<ushort>(Columns.VendingDisguise); }
            set { SetColumnValue(Columns.VendingDisguise, value); }
        }

        [XmlAttribute("TotalDoantionGuild")]
        [Bindable(true)]
        public ulong TotalDoantionGuild
        {
            get { return GetColumnValue<ushort>(Columns.TotalDoantionGuild); }
            set { SetColumnValue(Columns.TotalDoantionGuild, value); }
        }

        [XmlAttribute("ChatBanTime")]
        [Bindable(true)]
        public string ChatBanTime
        {
            get { return GetColumnValue<string>(Columns.ChatBanTime); }
            set { SetColumnValue(Columns.ChatBanTime, value); }
        }

        [XmlAttribute("ChatBanLasts")]
        [Bindable(true)]
        public ulong ChatBanLasts
        {
            get { return GetColumnValue<ulong>(Columns.ChatBanLasts); }
            set { SetColumnValue(Columns.ChatBanLasts, value); }
        }

        [XmlAttribute("ChatBanned")]
        [Bindable(true)]
        public ushort ChatBanned
        {
            get { return GetColumnValue<ushort>(Columns.ChatBanned); }
            set { SetColumnValue(Columns.ChatBanned, value); }
        }

        [XmlAttribute("InLottery")]
        [Bindable(true)]
        public bool InLottery
        {
            get { return GetColumnValue<bool>(Columns.InLottery); }
            set { SetColumnValue(Columns.InLottery, value); }
        }

        [XmlAttribute("LotteryEntries")]
        [Bindable(true)]
        public byte LotteryEntries
        {
            get { return GetColumnValue<byte>(Columns.LotteryEntries); }
            set { SetColumnValue(Columns.LotteryEntries, value); }
        }

        [XmlAttribute("LastLotteryEntry")]
        [Bindable(true)]
        public string LastLotteryEntry
        {
            get { return GetColumnValue<string>(Columns.LastLotteryEntry); }
            set { SetColumnValue(Columns.LastLotteryEntry, value); }
        }

        [XmlAttribute("PreviousX")]
        [Bindable(true)]
        public ushort PreviousX
        {
            get { return GetColumnValue<ushort>(Columns.PreviousX); }
            set { SetColumnValue(Columns.PreviousX, value); }
        }

        [XmlAttribute("PreviousY")]
        [Bindable(true)]
        public ushort PreviousY
        {
            get { return GetColumnValue<ushort>(Columns.PreviousY); }
            set { SetColumnValue(Columns.PreviousY, value); }
        }

        [XmlAttribute("OfflineTGEnterTime")]
        [Bindable(true)]
        public string OfflineTGEnterTime
        {
            get { return GetColumnValue<string>(Columns.OfflineTGEnterTime); }
            set { SetColumnValue(Columns.OfflineTGEnterTime, value); }
        }

        [XmlAttribute("ExpBalls")]
        [Bindable(true)]
        public byte ExpBalls
        {
            get { return GetColumnValue<byte>(Columns.ExpBalls); }
            set { SetColumnValue(Columns.ExpBalls, value); }
        }
        [XmlAttribute("ClanId")]
        [Bindable(true)]
        public uint ClanId
        {
            get { return GetColumnValue<uint>(Columns.ClanId); }
            set { SetColumnValue(Columns.ClanId, value); }
        }
        [XmlAttribute("Status")]
        [Bindable(true)]
        public ulong Status
        {
            get { return GetColumnValue<ulong>(Columns.Status); }
            set { SetColumnValue(Columns.Status, value); }
        }

        [XmlAttribute("Status3")]
        [Bindable(true)]
        public ulong Status3
        {
            get { return GetColumnValue<ulong>(Columns.Status3); }
            set { SetColumnValue(Columns.Status3, value); }
        }


        [XmlAttribute("Status4")]
        [Bindable(true)]
        public ulong Status4
        {
            get { return GetColumnValue<ulong>(Columns.Status4); }
            set { SetColumnValue(Columns.Status4, value); }
        }

        [XmlAttribute("ArsenalDonation")]
        [Bindable(true)]
        public ulong ArsenalDonation
        {
            get { return GetColumnValue<ulong>(Columns.ArsenalDonation); }
            set { SetColumnValue(Columns.ArsenalDonation, value); }
        }

        [XmlAttribute("Status2")]
        [Bindable(true)]
        public ulong Status2
        {
            get { return GetColumnValue<ulong>(Columns.Status2); }
            set { SetColumnValue(Columns.Status2, value); }
        }
        [XmlAttribute("ClanDonation")]
        [Bindable(true)]
        public long ClanDonation
        {
            get { return GetColumnValue<long>(Columns.ClanDonation); }
            set { SetColumnValue(Columns.ClanDonation, value); }
        }

        [XmlAttribute("ClanRank")]
        [Bindable(true)]
        public int ClanRank
        {
            get { return GetColumnValue<int>(Columns.ClanRank); }
            set { SetColumnValue(Columns.ClanRank, value); }
        }

        [XmlAttribute("SubClass")]
        [Bindable(true)]
        public int SubClass
        {
            get { return GetColumnValue<int>(Columns.SubClass); }
            set { SetColumnValue(Columns.SubClass, value); }
        }

        [XmlAttribute("SubClassLevel")]
        [Bindable(true)]
        public int SubClassLevel
        {
            get { return GetColumnValue<int>(Columns.SubClassLevel); }
            set { SetColumnValue(Columns.SubClassLevel, value); }
        }

        [XmlAttribute("StudyPoints")]
        [Bindable(true)]
        public int StudyPoints
        {
            get { return GetColumnValue<int>(Columns.StudyPoints); }
            set { SetColumnValue(Columns.StudyPoints, value); }
        }

        [XmlAttribute("GuildTulips")]
        [Bindable(true)]
        public int GuildTulips
        {
            get { return GetColumnValue<int>(Columns.GuildTulips); }
            set { SetColumnValue(Columns.GuildTulips, value); }
        }

        [XmlAttribute("GuildOrchids")]
        [Bindable(true)]
        public int GuildOrchids
        {
            get { return GetColumnValue<int>(Columns.GuildOrchids); }
            set { SetColumnValue(Columns.GuildOrchids, value); }
        }

        [XmlAttribute("GuildRouses")]
        [Bindable(true)]
        public int GuildRouses
        {
            get { return GetColumnValue<int>(Columns.GuildRouses); }
            set { SetColumnValue(Columns.GuildRouses, value); }
        }

        [XmlAttribute("GuildLilies")]
        [Bindable(true)]
        public int GuildLilies
        {
            get { return GetColumnValue<int>(Columns.GuildLilies); }
            set { SetColumnValue(Columns.GuildLilies, value); }
        }

        [XmlAttribute("Exploits")]
        [Bindable(true)]
        public uint Exploits
        {
            get { return GetColumnValue<uint>(Columns.Exploits); }
            set { SetColumnValue(Columns.Exploits, value); }
        }

        [XmlAttribute("CTFCpsReward")]
        [Bindable(true)]
        public uint CTFCpsReward
        {
            get { return GetColumnValue<uint>(Columns.CTFCpsReward); }
            set { SetColumnValue(Columns.CTFCpsReward, value); }
        }

        [XmlAttribute("CTFSilverReward")]
        [Bindable(true)]
        public uint CTFSilverReward
        {
            get { return GetColumnValue<uint>(Columns.CTFSilverReward); }
            set { SetColumnValue(Columns.CTFSilverReward, value); }
        }
        #endregion
        #region ObjectDataSource support
        public static void Insert(string varName, uint varUid, ushort varTreepoints, string varOwner, ushort varHairStyle, byte varClassX, ulong varMoney, ulong varConquerPoints, ulong varBoundCps, short varBody, short varFace, byte varLevel, ushort varStrength, ushort varAgility, ushort varVitality, ushort varSpirit, ushort varAtributes, uint varHitpoints, ushort varMana, ushort varMapID, ushort varX, ushort varY, ushort varPKPoints, ulong varExperience, uint varQuizPoints, ushort varPreviousMapID, byte varReborn, bool varFirstClass, bool varSecondClass, bool varThirdClass, string varSpouse, uint varWarehousePW, ulong varMoneySave, byte varFirstRebornClass, byte varSecondRebornClass, byte varFirstRebornLevel, byte varSecondRebornLevel, bool varOnline, ushort varEnlightenPoints, ushort varDoubleExpTime, ulong varGwkill, uint varHeavenBlessingTime, uint varBlessTime, string varLastDragonBallUse, long varLastResetTime, byte varEnlightsReceived, ushort varEnlightmentWait, bool varDoubleExpToday, uint varGuildID, ushort varGuildRank, ulong varGuildSilverDonation, ulong varGuildConquerPointDonation, byte varVIPLevel, ulong varVirtuePoints, bool varHeadgearClaim, bool varNecklaceClaim, bool varArmorClaim, bool varWeaponClaim, bool varBootsClaim, bool varTowerClaim, bool varFanClaim, bool varRingClaim, ushort varVendingDisguise, string varChatBanTime, ulong varChatBanLasts, ushort varChatBanned, bool varInLottery, byte varLotteryEntries, string varLastLotteryEntry, ushort varPreviousX, ushort varPreviousY, string varOfflineTGEnterTime, byte varExpBalls, ulong varStatus, ulong varStatus2, uint varClanId, long varClanDonation, int varClanRank, int varSubClass, int varSubClassLevel, int varStudyPoints, int varGuildTulips, ulong varStatus3, ulong varStatus4, ulong varQuest, uint varFlower, ulong varReincarnationLev, ulong varTotalDoantionGuild, int varGuildOrchids, int varGuildRouses, uint varCTFCpsReward, uint varExploits, int varGuildLilies, uint varCTFSilverReward)
        {
            Entity item = new Entity();

            item.Name = varName;

            item.Uid = varUid;

            item.Owner = varOwner;

            item.HairStyle = varHairStyle;

            item.ClassX = varClassX;

            item.Money = varMoney;

            item.Treepoints = varTreepoints;

            item.ConquerPoints = varConquerPoints;

            item.BoundCps = varBoundCps;

            item.Body = varBody;

            item.Face = varFace;

            item.Level = varLevel;

            item.Strength = varStrength;

            item.Agility = varAgility;

            item.Vitality = varVitality;

            item.Spirit = varSpirit;

            item.Atributes = varAtributes;

            item.Hitpoints = varHitpoints;

            item.Mana = varMana;

            item.MapID = varMapID;

            item.X = varX;

            item.Y = varY;

            item.PKPoints = varPKPoints;

            item.Experience = varExperience;

            item.QuizPoints = varQuizPoints;

            item.PreviousMapID = varPreviousMapID;

            item.Reborn = varReborn;

            item.FirstClass = varFirstClass;

            item.SecondClass = varSecondClass;

            item.ThirdClass = varThirdClass;

            item.Spouse = varSpouse;

            item.WarehousePW = varWarehousePW;

            item.MoneySave = varMoneySave;

            item.FirstRebornClass = varFirstRebornClass;

            item.SecondRebornClass = varSecondRebornClass;

            item.FirstRebornLevel = varFirstRebornLevel;

            item.SecondRebornLevel = varSecondRebornLevel;

            item.Online = varOnline;

            item.EnlightenPoints = varEnlightenPoints;

            item.DoubleExpTime = varDoubleExpTime;

            item.Gwkill = varGwkill;

            item.HeavenBlessingTime = varHeavenBlessingTime;

            item.BlessTime = varBlessTime;

            item.LastDragonBallUse = varLastDragonBallUse;

            item.LastResetTime = varLastResetTime;

            item.EnlightsReceived = varEnlightsReceived;

            item.EnlightmentWait = varEnlightmentWait;

            item.DoubleExpToday = varDoubleExpToday;

            item.GuildID = varGuildID;

            item.GuildRank = varGuildRank;

            item.GuildSilverDonation = varGuildSilverDonation;

            item.GuildConquerPointDonation = varGuildConquerPointDonation;

            item.VIPLevel = varVIPLevel;

            item.VirtuePoints = varVirtuePoints;

            item.HeadgearClaim = varHeadgearClaim;

            item.NecklaceClaim = varNecklaceClaim;

            item.ArmorClaim = varArmorClaim;

            item.WeaponClaim = varWeaponClaim;

            item.BootsClaim = varBootsClaim;

            item.TowerClaim = varTowerClaim;

            item.FanClaim = varFanClaim;

            item.RingClaim = varRingClaim;

            item.VendingDisguise = varVendingDisguise;

            item.ChatBanTime = varChatBanTime;

            item.ChatBanLasts = varChatBanLasts;

            item.ChatBanned = varChatBanned;

            item.InLottery = varInLottery;

            item.LotteryEntries = varLotteryEntries;

            item.LastLotteryEntry = varLastLotteryEntry;

            item.PreviousX = varPreviousX;

            item.PreviousY = varPreviousY;

            item.OfflineTGEnterTime = varOfflineTGEnterTime;

            item.ExpBalls = varExpBalls;

            item.Status = varStatus;

            item.Status2 = varStatus2;

            item.ClanId = varClanId;

            item.ClanDonation = varClanDonation;

            item.ClanRank = varClanRank;

            item.SubClass = varSubClass;

            item.SubClassLevel = varSubClassLevel;

            item.StudyPoints = varStudyPoints;

            item.GuildTulips = varGuildTulips;

            item.Status3 = varStatus3;

            item.Status4 = varStatus4;

            item.ArsenalDonation = varQuest;

            item.Flower = varFlower;

            item.ReincarnationLev = varReincarnationLev;

            item.TotalDoantionGuild = varTotalDoantionGuild;

            item.GuildOrchids = varGuildOrchids;

            item.GuildRouses = varGuildRouses;

            item.Exploits = varExploits;

            item.CTFCpsReward = varCTFCpsReward;

            item.GuildLilies = varGuildLilies;

            item.CTFSilverReward = varCTFSilverReward;

            if (System.Web.HttpContext.Current != null)
                item.Save(System.Web.HttpContext.Current.User.Identity.Name);
            else
                item.Save(System.Threading.Thread.CurrentPrincipal.Identity.Name);
        }
        public static void Update(string varName, uint varUid, string varOwner, ushort varTreepoints, ushort varHairStyle, byte varClassX, ulong varMoney, ulong varConquerPoints, short varBody, short varFace, byte varLevel, ulong varBoundCps, ushort varStrength, ushort varAgility, ushort varVitality, ushort varSpirit, ushort varAtributes, uint varHitpoints, ushort varMana, ushort varMapID, ushort varX, ushort varY, ushort varPKPoints, ulong varExperience, uint varQuizPoints, ushort varPreviousMapID, byte varReborn, bool varFirstClass, bool varSecondClass, bool varThirdClass, string varSpouse, uint varWarehousePW, ulong varMoneySave, byte varFirstRebornClass, byte varSecondRebornClass, byte varFirstRebornLevel, byte varSecondRebornLevel, bool varOnline, ushort varEnlightenPoints, ushort varDoubleExpTime, ulong varGwkill, uint varHeavenBlessingTime, uint varBlessTime, string varLastDragonBallUse, long varLastResetTime, byte varEnlightsReceived, ushort varEnlightmentWait, bool varDoubleExpToday, uint varGuildID, ushort varGuildRank, ulong varGuildSilverDonation, ulong varGuildConquerPointDonation, byte varVIPLevel, ulong varVirtuePoints, bool varHeadgearClaim, bool varNecklaceClaim, bool varArmorClaim, bool varWeaponClaim, bool varBootsClaim, bool varTowerClaim, bool varFanClaim, bool varRingClaim, ushort varVendingDisguise, string varChatBanTime, ulong varChatBanLasts, ushort varChatBanned, bool varInLottery, byte varLotteryEntries, string varLastLotteryEntry, ushort varPreviousX, ushort varPreviousY, string varOfflineTGEnterTime, byte varExpBalls, ulong varStatus, ulong varStatus2, uint varClanId, long varClanDonation, int varClanRank, int varSubClass, int varSubClassLevel, int varStudyPoints, int varGuildTulips, ulong varStatus3, ulong varStatus4, ulong varQuest, uint varFlower, ulong varReincarnationLev, ulong varTotalDoantionGuild, int varGuildOrchids, int varGuildRouses, uint varExploits, uint varCTFCpsReward, int varGuildLilies, uint varCTFSilverReward)
        {
            Entity item = new Entity();

            item.Name = varName;

            item.Uid = varUid;

            item.Owner = varOwner;

            item.HairStyle = varHairStyle;

            item.ClassX = varClassX;

            item.Money = varMoney;

            item.ConquerPoints = varConquerPoints;

            item.Treepoints = varTreepoints;

            item.BoundCps = varBoundCps;

            item.Body = varBody;

            item.Face = varFace;

            item.Level = varLevel;

            item.Strength = varStrength;

            item.Agility = varAgility;

            item.Vitality = varVitality;

            item.Spirit = varSpirit;

            item.Atributes = varAtributes;

            item.Hitpoints = varHitpoints;

            item.Mana = varMana;

            item.MapID = varMapID;

            item.X = varX;

            item.Y = varY;

            item.PKPoints = varPKPoints;

            item.Experience = varExperience;

            item.QuizPoints = varQuizPoints;

            item.PreviousMapID = varPreviousMapID;

            item.Reborn = varReborn;

            item.FirstClass = varFirstClass;

            item.SecondClass = varSecondClass;

            item.ThirdClass = varThirdClass;

            item.Spouse = varSpouse;

            item.WarehousePW = varWarehousePW;

            item.MoneySave = varMoneySave;

            item.FirstRebornClass = varFirstRebornClass;

            item.SecondRebornClass = varSecondRebornClass;

            item.FirstRebornLevel = varFirstRebornLevel;

            item.SecondRebornLevel = varSecondRebornLevel;

            item.Online = varOnline;

            item.EnlightenPoints = varEnlightenPoints;

            item.DoubleExpTime = varDoubleExpTime;

            item.Gwkill = varGwkill;

            item.HeavenBlessingTime = varHeavenBlessingTime;

            item.BlessTime = varBlessTime;

            item.LastDragonBallUse = varLastDragonBallUse;

            item.LastResetTime = varLastResetTime;

            item.EnlightsReceived = varEnlightsReceived;

            item.EnlightmentWait = varEnlightmentWait;

            item.DoubleExpToday = varDoubleExpToday;

            item.GuildID = varGuildID;

            item.GuildRank = varGuildRank;

            item.GuildSilverDonation = varGuildSilverDonation;

            item.GuildConquerPointDonation = varGuildConquerPointDonation;

            item.VIPLevel = varVIPLevel;

            item.VirtuePoints = varVirtuePoints;

            item.HeadgearClaim = varHeadgearClaim;

            item.NecklaceClaim = varNecklaceClaim;

            item.ArmorClaim = varArmorClaim;

            item.WeaponClaim = varWeaponClaim;

            item.BootsClaim = varBootsClaim;

            item.TowerClaim = varTowerClaim;

            item.FanClaim = varFanClaim;

            item.RingClaim = varRingClaim;

            item.VendingDisguise = varVendingDisguise;

            item.ChatBanTime = varChatBanTime;

            item.ChatBanLasts = varChatBanLasts;

            item.ChatBanned = varChatBanned;

            item.InLottery = varInLottery;

            item.LotteryEntries = varLotteryEntries;

            item.LastLotteryEntry = varLastLotteryEntry;

            item.PreviousX = varPreviousX;

            item.PreviousY = varPreviousY;

            item.OfflineTGEnterTime = varOfflineTGEnterTime;

            item.ExpBalls = varExpBalls;

            item.Status = varStatus;

            item.Status2 = varStatus2;

            item.ClanId = varClanId;

            item.ClanDonation = varClanDonation;

            item.ClanRank = varClanRank;

            item.SubClass = varSubClass;

            item.SubClassLevel = varSubClassLevel;

            item.StudyPoints = varStudyPoints;

            item.GuildTulips = varGuildTulips;

            item.Status3 = varStatus3;

            item.Status4 = varStatus4;

            item.ArsenalDonation = varQuest;

            item.Flower = varFlower;

            item.ReincarnationLev = varReincarnationLev;

            item.TotalDoantionGuild = varTotalDoantionGuild;

            item.GuildOrchids = varGuildOrchids;

            item.GuildRouses = varGuildRouses;

            item.Exploits = varExploits;

            item.CTFCpsReward = varCTFCpsReward;

            item.GuildLilies = varGuildLilies;

            item.CTFSilverReward = varCTFSilverReward;


            item.IsNew = false;
            if (System.Web.HttpContext.Current != null)
                item.Save(System.Web.HttpContext.Current.User.Identity.Name);
            else
                item.Save(System.Threading.Thread.CurrentPrincipal.Identity.Name);
        }
        #endregion
        #region Typed Columns
        public static TableSchema.TableColumn NameColumn
        {
            get { return Schema.Columns[0]; }
        }
        public static TableSchema.TableColumn UidColumn
        {
            get { return Schema.Columns[1]; }
        }
        public static TableSchema.TableColumn OwnerColumn
        {
            get { return Schema.Columns[2]; }
        }
        public static TableSchema.TableColumn HairStyleColumn
        {
            get { return Schema.Columns[3]; }
        }
        public static TableSchema.TableColumn ClassXColumn
        {
            get { return Schema.Columns[4]; }
        }
        public static TableSchema.TableColumn MoneyColumn
        {
            get { return Schema.Columns[5]; }
        }
        public static TableSchema.TableColumn ConquerPointsColumn
        {
            get { return Schema.Columns[6]; }
        }
        public static TableSchema.TableColumn BodyColumn
        {
            get { return Schema.Columns[7]; }
        }
        public static TableSchema.TableColumn FaceColumn
        {
            get { return Schema.Columns[8]; }
        }
        public static TableSchema.TableColumn LevelColumn
        {
            get { return Schema.Columns[9]; }
        }
        public static TableSchema.TableColumn StrengthColumn
        {
            get { return Schema.Columns[10]; }
        }
        public static TableSchema.TableColumn AgilityColumn
        {
            get { return Schema.Columns[11]; }
        }
        public static TableSchema.TableColumn VitalityColumn
        {
            get { return Schema.Columns[12]; }
        }
        public static TableSchema.TableColumn SpiritColumn
        {
            get { return Schema.Columns[13]; }
        }
        public static TableSchema.TableColumn AtributesColumn
        {
            get { return Schema.Columns[14]; }
        }
        public static TableSchema.TableColumn HitpointsColumn
        {
            get { return Schema.Columns[15]; }
        }
        public static TableSchema.TableColumn ManaColumn
        {
            get { return Schema.Columns[16]; }
        }
        public static TableSchema.TableColumn MapIDColumn
        {
            get { return Schema.Columns[17]; }
        }
        public static TableSchema.TableColumn XColumn
        {
            get { return Schema.Columns[18]; }
        }
        public static TableSchema.TableColumn YColumn
        {
            get { return Schema.Columns[19]; }
        }
        public static TableSchema.TableColumn PKPointsColumn
        {
            get { return Schema.Columns[20]; }
        }
        public static TableSchema.TableColumn ExperienceColumn
        {
            get { return Schema.Columns[21]; }
        }
        public static TableSchema.TableColumn QuizPointsColumn
        {
            get { return Schema.Columns[22]; }
        }
        public static TableSchema.TableColumn PreviousMapIDColumn
        {
            get { return Schema.Columns[23]; }
        }
        public static TableSchema.TableColumn RebornColumn
        {
            get { return Schema.Columns[24]; }
        }
        public static TableSchema.TableColumn FirstClassColumn
        {
            get { return Schema.Columns[25]; }
        }
        public static TableSchema.TableColumn SecondClassColumn
        {
            get { return Schema.Columns[26]; }
        }
        public static TableSchema.TableColumn ThirdClassColumn
        {
            get { return Schema.Columns[27]; }
        }
        public static TableSchema.TableColumn SpouseColumn
        {
            get { return Schema.Columns[28]; }
        }
        public static TableSchema.TableColumn WarehousePWColumn
        {
            get { return Schema.Columns[29]; }
        }
        public static TableSchema.TableColumn MoneySaveColumn
        {
            get { return Schema.Columns[30]; }
        }
        public static TableSchema.TableColumn FirstRebornClassColumn
        {
            get { return Schema.Columns[31]; }
        }
        public static TableSchema.TableColumn SecondRebornClassColumn
        {
            get { return Schema.Columns[32]; }
        }
        public static TableSchema.TableColumn FirstRebornLevelColumn
        {
            get { return Schema.Columns[33]; }
        }
        public static TableSchema.TableColumn SecondRebornLevelColumn
        {
            get { return Schema.Columns[34]; }
        }
        public static TableSchema.TableColumn OnlineColumn
        {
            get { return Schema.Columns[35]; }
        }
        public static TableSchema.TableColumn EnlightenPointsColumn
        {
            get { return Schema.Columns[36]; }
        }
        public static TableSchema.TableColumn DoubleExpTimeColumn
        {
            get { return Schema.Columns[37]; }
        }
        public static TableSchema.TableColumn GwkillColumn
        {
            get { return Schema.Columns[38]; }
        }
        public static TableSchema.TableColumn HeavenBlessingTimeColumn
        {
            get { return Schema.Columns[39]; }
        }
        public static TableSchema.TableColumn BlessTimeColumn
        {
            get { return Schema.Columns[40]; }
        }
        public static TableSchema.TableColumn LastDragonBallUseColumn
        {
            get { return Schema.Columns[41]; }
        }
        public static TableSchema.TableColumn LastResetTimeColumn
        {
            get { return Schema.Columns[42]; }
        }
        public static TableSchema.TableColumn EnlightsReceivedColumn
        {
            get { return Schema.Columns[43]; }
        }
        public static TableSchema.TableColumn EnlightmentWaitColumn
        {
            get { return Schema.Columns[44]; }
        }
        public static TableSchema.TableColumn DoubleExpTodayColumn
        {
            get { return Schema.Columns[45]; }
        }
        public static TableSchema.TableColumn GuildIDColumn
        {
            get { return Schema.Columns[46]; }
        }
        public static TableSchema.TableColumn GuildRankColumn
        {
            get { return Schema.Columns[47]; }
        }
        public static TableSchema.TableColumn GuildSilverDonationColumn
        {
            get { return Schema.Columns[48]; }
        }
        public static TableSchema.TableColumn GuildConquerPointDonationColumn
        {
            get { return Schema.Columns[49]; }
        }
        public static TableSchema.TableColumn VIPLevelColumn
        {
            get { return Schema.Columns[50]; }
        }
        public static TableSchema.TableColumn VirtuePointsColumn
        {
            get { return Schema.Columns[51]; }
        }
        public static TableSchema.TableColumn HeadgearClaimColumn
        {
            get { return Schema.Columns[52]; }
        }
        public static TableSchema.TableColumn NecklaceClaimColumn
        {
            get { return Schema.Columns[53]; }
        }
        public static TableSchema.TableColumn ArmorClaimColumn
        {
            get { return Schema.Columns[54]; }
        }
        public static TableSchema.TableColumn WeaponClaimColumn
        {
            get { return Schema.Columns[55]; }
        }
        public static TableSchema.TableColumn BootsClaimColumn
        {
            get { return Schema.Columns[56]; }
        }
        public static TableSchema.TableColumn TowerClaimColumn
        {
            get { return Schema.Columns[57]; }
        }
        public static TableSchema.TableColumn FanClaimColumn
        {
            get { return Schema.Columns[58]; }
        }
        public static TableSchema.TableColumn RingClaimColumn
        {
            get { return Schema.Columns[59]; }
        }
        public static TableSchema.TableColumn VendingDisguiseColumn
        {
            get { return Schema.Columns[60]; }
        }
        public static TableSchema.TableColumn ChatBanTimeColumn
        {
            get { return Schema.Columns[61]; }
        }
        public static TableSchema.TableColumn ChatBanLastsColumn
        {
            get { return Schema.Columns[62]; }
        }
        public static TableSchema.TableColumn ChatBannedColumn
        {
            get { return Schema.Columns[63]; }
        }
        public static TableSchema.TableColumn InLotteryColumn
        {
            get { return Schema.Columns[64]; }
        }
        public static TableSchema.TableColumn LotteryEntriesColumn
        {
            get { return Schema.Columns[65]; }
        }
        public static TableSchema.TableColumn LastLotteryEntryColumn
        {
            get { return Schema.Columns[66]; }
        }
        public static TableSchema.TableColumn PreviousXColumn
        {
            get { return Schema.Columns[67]; }
        }
        public static TableSchema.TableColumn PreviousYColumn
        {
            get { return Schema.Columns[68]; }
        }
        public static TableSchema.TableColumn OfflineTGEnterTimeColumn
        {
            get { return Schema.Columns[69]; }
        }
        public static TableSchema.TableColumn ExpBallsColumn
        {
            get { return Schema.Columns[70]; }
        }
        public static TableSchema.TableColumn StatusColumn
        {
            get { return Schema.Columns[71]; }
        }
        public static TableSchema.TableColumn Status2Column
        {
            get { return Schema.Columns[72]; }
        }
        public static TableSchema.TableColumn ClanIdColumn
        {
            get { return Schema.Columns[73]; }
        }
        public static TableSchema.TableColumn ClanDonationColumn
        {
            get { return Schema.Columns[74]; }
        }
        public static TableSchema.TableColumn ClanRankColumn
        {
            get { return Schema.Columns[75]; }
        }
        public static TableSchema.TableColumn SubClassColumn
        {
            get { return Schema.Columns[76]; }
        }
        public static TableSchema.TableColumn SubClassLevelColumn
        {
            get { return Schema.Columns[77]; }
        }
        public static TableSchema.TableColumn StudyPointsColumn
        {
            get { return Schema.Columns[78]; }
        }
        public static TableSchema.TableColumn GuildTulipsColumn
        {
            get { return Schema.Columns[79]; }
        }
        public static TableSchema.TableColumn Status3Column
        {
            get { return Schema.Columns[80]; }
        }
        public static TableSchema.TableColumn Status4Column
        {
            get { return Schema.Columns[81]; }
        }
        public static TableSchema.TableColumn QuestColumn
        {
            get { return Schema.Columns[82]; }
        }
        public static TableSchema.TableColumn FlowerColumn
        {
            get { return Schema.Columns[83]; }
        }
        public static TableSchema.TableColumn ReincarnationLevColumn
        {
            get { return Schema.Columns[84]; }
        }
        public static TableSchema.TableColumn TreepointsColumn
        {
            get { return Schema.Columns[85]; }
        }
        public static TableSchema.TableColumn GuildOrchidsColumn
        {
            get { return Schema.Columns[86]; }
        }
        public static TableSchema.TableColumn GuildRousesColumn
        {
            get { return Schema.Columns[87]; }
        }
        public static TableSchema.TableColumn ExploitsColumn
        {
            get { return Schema.Columns[88]; }
        }
        public static TableSchema.TableColumn CTFCpsRewardColumn
        {
            get { return Schema.Columns[89]; }
        }
        public static TableSchema.TableColumn GuildLiliesColumn
        {
            get { return Schema.Columns[90]; }
        }
        public static TableSchema.TableColumn CTFSilverRewardColumn
        {
            get { return Schema.Columns[91]; }
        }
        #endregion
        #region Columns Struct
        public struct Columns
        {
            public static string Name = @"Name";
            public static string Uid = @"UID";
            public static string Flower = @"Flower";
            public static string Owner = @"Owner";
            public static string Treepoints = @"Treepoints";
            public static string HairStyle = @"HairStyle";
            public static string ClassX = @"Class";
            public static string Money = @"Money";
            public static string ConquerPoints = @"ConquerPoints";
            public static string BoundCPs = @"BoundCPs";
            public static string Body = @"Body";
            public static string Face = @"Face";
            public static string Level = @"Level";
            public static string Strength = @"Strength";
            public static string Agility = @"Agility";
            public static string Vitality = @"Vitality";
            public static string Spirit = @"Spirit";
            public static string Atributes = @"Atributes";
            public static string Hitpoints = @"Hitpoints";
            public static string Mana = @"Mana";
            public static string MapID = @"MapID";
            public static string X = @"X";
            public static string Y = @"Y";
            public static string PKPoints = @"PKPoints";
            public static string Experience = @"Experience";
            public static string QuizPoints = @"QuizPoints";
            public static string PreviousMapID = @"PreviousMapID";
            public static string Reborn = @"Reborn";
            public static string FirstClass = @"FirstClass";
            public static string SecondClass = @"SecondClass";
            public static string ThirdClass = @"ThirdClass";
            public static string Spouse = @"Spouse";
            public static string WarehousePW = @"WarehousePW";
            public static string MoneySave = @"MoneySave";
            public static string FirstRebornClass = @"FirstRebornClass";
            public static string SecondRebornClass = @"SecondRebornClass";
            public static string FirstRebornLevel = @"FirstRebornLevel";
            public static string SecondRebornLevel = @"SecondRebornLevel";
            public static string Online = @"Online";
            public static string EnlightenPoints = @"EnlightenPoints";
            public static string DoubleExpTime = @"DoubleExpTime";
            public static string Gwkill = @"gwkill";
            public static string HeavenBlessingTime = @"HeavenBlessingTime";
            public static string BlessTime = @"BlessTime";
            public static string LastDragonBallUse = @"LastDragonBallUse";
            public static string LastLogin = @"LastLogin";
            public static string LastResetTime = @"LastResetTime";
            public static string EnlightsReceived = @"EnlightsReceived";
            public static string Flag = @"Flag";
            public static string EnlightmentWait = @"EnlightmentWait";
            public static string DoubleExpToday = @"DoubleExpToday";
            public static string GuildID = @"GuildID";
            public static string GuildRank = @"GuildRank";
            public static string GuildSilverDonation = @"GuildSilverDonation";
            public static string GuildConquerPointDonation = @"GuildConquerPointDonation";
            public static string VIPLevel = @"VIPLevel";
            public static string VirtuePoints = @"VirtuePoints";
            public static string HeadgearClaim = @"HeadgearClaim";
            public static string NecklaceClaim = @"NecklaceClaim";
            public static string ArmorClaim = @"ArmorClaim";
            public static string WeaponClaim = @"WeaponClaim";
            public static string BootsClaim = @"BootsClaim";
            public static string TowerClaim = @"TowerClaim";
            public static string FanClaim = @"FanClaim";
            public static string RingClaim = @"RingClaim";
            public static string VendingDisguise = @"VendingDisguise";
            public static string TotalDoantionGuild = @"TotalDoantionGuild";
            public static string ChatBanTime = @"ChatBanTime";
            public static string ChatBanLasts = @"ChatBanLasts";
            public static string ChatBanned = @"ChatBanned";
            public static string InLottery = @"InLottery";
            public static string LotteryEntries = @"LotteryEntries";
            public static string LastLotteryEntry = @"LastLotteryEntry";
            public static string PreviousX = @"PreviousX";
            public static string PreviousY = @"PreviousY";
            public static string OfflineTGEnterTime = @"OfflineTGEnterTime";
            public static string ExpBalls = @"ExpBalls";
            public static string Status = @"Status";
            public static string Status2 = @"Status2";
            public static string Status3 = @"Status3";
            public static string Status4 = @"Status4";
            public static string ArsenalDonation = @"ArsenalDonation";
            public static string ClanDonation = @"ClanDonation";
            public static string ClanRank = @"ClanRank";
            public static string ClanId = @"ClanId";
            public static string SubClass = @"SubClass";
            public static string SubClassLevel = @"SubClassLevel";
            public static string StudyPoints = @"StudyPoints";
            public static string GuildTulips = @"GuildTulips";
            public static string ReincarnationLev = @"ReincarnationLev";
            public static string GuildOrchids = @"GuildOrchids";
            public static string GuildRouses = @"GuildRouses";
            public static string Exploits = @"Exploits";
            public static string CTFCpsReward = @"CTFCpsReward";
            public static string GuildLilies = @"GuildLilies";
            public static string CTFSilverReward = @"CTFSilverReward";
        }
        #endregion
    }
}
namespace Revora.Interfaces
{
    public unsafe interface INpc
    {
        uint UID { get; set; }
        ushort X { get; set; }
        ushort Y { get; set; }
        ushort Mesh { get; set; }
        ushort MapID { get; set; }
        string Name { get; set; }
        string effect { get; set; }
        Game.Enums.NpcType Type { get; set; }
        void SendSpawn(Client.GameState Client);
        void SendSpawn(Client.GameState Client, bool checkScreen);
    }
    public unsafe interface IProf
    {
        uint Experience { get; set; }
        uint NeededExperience { get; set; }
        ushort ID { get; set; }
        byte Level { get; set; }
        byte TempLevel { get; set; }
        byte PreviousLevel { get; set; }
        bool Available { get; set; }
        byte[] Buffer { get; set; }
        void Send(Client.GameState client);
    }
    public unsafe interface ISkill
    {
        uint Experience { get; set; }
        ushort ID { get; set; }
        byte Level { get; set; }
        byte PreviousLevel { get; set; }
        byte[] Buffer { get; set; }
        void AddFlag(Network.GamePackets.Spell.Soul_Level flag);
        bool ContainsFlag(Network.GamePackets.Spell.Soul_Level flag);
        void RemoveFlag(Network.GamePackets.Spell.Soul_Level flag);
        Network.GamePackets.Spell.Soul_Level Souls { get; set; }
        byte LevelHu2 { get; set; }
        System.Time32 ColdTime { get; set; }
        int GetColdTime { get; }
        bool IsSpellWithColdTime { get; set; }
        bool Available { get; set; }
        int FireCurseDamage { get; set; }
        void Send(Client.GameState client);
    }
    public unsafe interface ISobNpc
    {
        Game.Enums.NpcType Type { get; set; }
        uint UID { get; set; }
        ushort X { get; set; }
        ushort Y { get; set; }
        uint MaxHitpoints { get; set; }
        uint Hitpoints { get; set; }
        string Name { get; set; }
        ushort Mesh { get; set; }
        ushort MapID { get; set; }
        void SendSpawn(Client.GameState Client);
    }
    public unsafe interface IPacket
    {
        byte[] ToArray();
        void Deserialize(byte[] buffer);
        void Send(Client.GameState client);
    }
    public unsafe interface IJiangHu
    {
        System.DateTime CountDownEnd { get; set; }
        string JiangName { get; set; }
        uint FreeCourse { get; set; }
        uint FreeTimeTodey { get; set; }
        uint FreeTimeTodeyUsed { get; set; }
        uint Inner_Strength { get; set; }
        byte Level { get; set; }
        Game.JiangHu.OldStar MyOldStar { get; set; }
        bool OnJiangHu { get; set; }
        string OwnName { get; set; }
        byte Rank { get; set; }
        ushort RoundBuyPoints { get; set; }
        byte Stage { get; set; }
        Game.JiangHu.JiangStages[] Stagers { get; set; }
        byte Star { get; set; }
        System.DateTime StartCountDwon { get; set; }
        byte Talent { get; set; }
        uint Time { get; }
        uint UID { get; set; }
    }
    public unsafe interface IMapObject
    {
        ushort X { get; }
        ushort Y { get; }
        ushort MapID { get; }
        uint UID { get; }
        Client.GameState Owner { get; }
        Game.MapObjectType MapObjType { get; }
        void SendSpawn(Client.GameState client);
        void SendSpawn(Client.GameState client, bool checkScreen);
    }
    public unsafe interface IBaseEntity
    {
        bool Dead { get; set; }
        ushort Defence { get; set; }
        byte Dodge { get; set; }
        Game.EntityFlag EntityFlag { get; set; }
        uint Hitpoints { get; set; }
        uint MagicAttack { get; set; }
        ushort MapID { get; set; }
        uint MaxAttack { get; set; }
        uint MaxHitpoints { get; set; }
        ushort MagicDefence { get; set; }
        uint MinAttack { get; set; }
        Client.GameState Owner { get; set; }
        uint UID { get; set; }
        ushort X { get; set; }
        ushort Y { get; set; }
    }
    public unsafe interface IKnownPerson
    {
        uint ID { get; set; }
        string Name { get; set; }
        bool IsOnline { get; }
        Client.GameState Client { get; }
    }
}
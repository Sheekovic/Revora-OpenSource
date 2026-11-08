using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Revora.Client;
using Revora.Game;
using System.Threading.Generic;
using Revora.Database;
using Revora.Network.GamePackets;
using Revora.Network;
using Revora.Game.ConquerStructures;

namespace Revora
{
    public class Pet
    {
        public enum PetType
        {
            Normal = 0,
            Looter,
            Stiger,
            Attacker,
            defender

        }
        public class PetInfo
        {
            public Entity Entity;
            public PetType Type;
            public GameState Owner;

        }
        #region Static Actions
        private static TimerRule<GameState> PetsAction;
        public static void CreateTimerFactories()
        {
            PetsAction = new TimerRule<GameState>(PetsActionCallback, 500);
        }
        private static void PetsActionCallback(GameState client, int time)
        {
            if (!client.Socket.Alive)
            {
                client.Pet.DisposeTimers();
                return;
            }
            if (client.Entity == null)
                return;
            if (client.Map == null)
                return;
            Time32 Now = new Time32(time);
            foreach (var pet in client.Pet.Pets.Values)
            {

                if (pet != null)
                {

                    #region Back To Owner
                    short distance = Kernel.GetDistance(pet.Entity.X, pet.Entity.Y, client.Entity.X, client.Entity.Y);
                    if (distance >= 8)
                    {
                        ushort X = (ushort)(client.Entity.X + Kernel.Random.Next(2));
                        ushort Y = (ushort)(client.Entity.Y + Kernel.Random.Next(2));
                        if (!client.Map.SelectCoordonates(ref X, ref Y))
                        {
                            X = client.Entity.X;
                            Y = client.Entity.Y;
                        }
                        pet.Entity.X = X;
                        pet.Entity.Y = Y;
                        Network.GamePackets.Data data = new Revora.Network.GamePackets.Data(true);
                        data.ID = Network.GamePackets.Data.Jump;
                        data.dwParam = (uint)((Y << 16) | X);
                        data.wParam1 = X;
                        data.wParam2 = Y;
                        data.UID = pet.Entity.UID;
                        pet.Entity.MonsterInfo.SendScreen(data);
                    }
                    else if (distance > 4)
                    {
                        Enums.ConquerAngle facing = Kernel.GetAngle(pet.Entity.X, pet.Entity.Y, pet.Entity.Owner.Entity.X, pet.Entity.Owner.Entity.Y);
                        if (!pet.Entity.Move(facing))
                        {
                            facing = (Enums.ConquerAngle)Kernel.Random.Next(7);
                            if (pet.Entity.Move(facing))
                            {
                                pet.Entity.Facing = facing;
                                Network.GamePackets.GroundMovement move = new Revora.Network.GamePackets.GroundMovement(true);
                                move.Direction = facing;
                                move.UID = pet.Entity.UID;
                                move.GroundMovementType = Network.GamePackets.GroundMovement.Run;
                                pet.Entity.MonsterInfo.SendScreen(move);
                            }
                        }
                        else
                        {
                            pet.Entity.Facing = facing;
                            Network.GamePackets.GroundMovement move = new Revora.Network.GamePackets.GroundMovement(true);
                            move.Direction = facing;
                            move.UID = pet.Entity.UID;
                            move.GroundMovementType = Network.GamePackets.GroundMovement.Run;
                            pet.Entity.MonsterInfo.SendScreen(move);
                        }
                    }
                    #endregion
                    switch (pet.Type)
                    {
                        case PetType.Normal:
                            {
                                #region Normal Attack Guard
                                {
                                    var monster = pet.Entity;
                                    if (monster.MonsterInfo.InSight == 0)
                                    {
                                        if (client.Entity.AttackPacket != null)
                                        {
                                            if (client.Entity.AttackPacket.AttackType == Network.GamePackets.Attack.Magic)
                                            {
                                                if (client.Entity.AttackPacket.Decoded)
                                                {
                                                    if (Database.SpellTable.SpellInformations.ContainsKey((ushort)client.Entity.AttackPacket.Damage))
                                                    {
                                                        var info = Database.SpellTable.SpellInformations[(ushort)client.Entity.AttackPacket.Damage].Values.ToArray()[client.Spells[(ushort)client.Entity.AttackPacket.Damage].Level];
                                                        if (info.CanKill)
                                                        {
                                                            monster.MonsterInfo.InSight = client.Entity.AttackPacket.Attacked;
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                monster.MonsterInfo.InSight = client.Entity.AttackPacket.Attacked;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (monster.MonsterInfo.InSight > 400000 && monster.MonsterInfo.InSight < 600000 || monster.MonsterInfo.InSight > 800000 && monster.MonsterInfo.InSight != monster.UID)
                                        {
                                            Entity attacked = null;

                                            if (client.Screen.TryGetValue(monster.MonsterInfo.InSight, out attacked))
                                            {
                                                if (Now > monster.AttackStamp.AddMilliseconds(monster.MonsterInfo.AttackSpeed))
                                                {
                                                    monster.AttackStamp = Now;
                                                    if (attacked.Dead)
                                                    {
                                                        monster.MonsterInfo.InSight = 0;
                                                    }
                                                    else
                                                        new Game.Attacking.Handle(null, monster, attacked);
                                                }
                                            }
                                            else
                                                monster.MonsterInfo.InSight = 0;
                                        }
                                    }
                                }
                                #endregion
                                break;
                            }
                        case PetType.Stiger:
                            {
                                #region Princess (Stigma and Invisibility)
                                if (client.Entity.TiempoMascotas.AddSeconds(120) < Time32.Now)
                                {
                                    SpellUse suse = new SpellUse(true);
                                    suse.Attacker = pet.Entity.UID;
                                    suse.SpellID = 1095;
                                    suse.SpellLevel = 4;
                                    suse.X = client.Entity.X;
                                    suse.Y = client.Entity.Y;

                                    suse.AddTarget(client.Entity.UID, 0, null);

                                    //Stigma
                                    client.Entity.AddFlag((ulong)FlagsUpdate.Flags.Stigma,80,true);
                                    client.Entity.StigmaStamp = Time32.Now;
                                    client.Entity.StigmaIncrease = 50;
                                    client.Entity.StigmaTime = (byte)60;
                                    if (client.Entity.EntityFlag == EntityFlag.Player)
                                        client.Entity.Owner.Send(Constants.Stigma(50, 60));

                                    //Invisibility
                                    client.Entity.AddFlag((ulong)FlagsUpdate.Flags.Invisibility,60,true);
                                    client.Entity.InvisibilityStamp = Time32.Now;
                                    client.Entity.InvisibilityTime = (byte)60;
                                    if (client.Entity.EntityFlag == EntityFlag.Player)
                                        client.Entity.Owner.Send(Constants.Invisibility(60));
                                    client.Entity.TiempoMascotas = Time32.Now;
                                }
                                #endregion
                                break;
                            }
                        case PetType.Looter:
                            {
                                #region Love Fox (Accuracy and Magic Shield)
                                if (client.Entity.TiempoMascotas.AddSeconds(120) < Time32.Now)
                                {
                                    SpellUse suse = new SpellUse(true);
                                    suse.Attacker = pet.Entity.UID;
                                    suse.SpellID = 1085;
                                    suse.SpellLevel = 4;
                                    suse.X = client.Entity.X;
                                    suse.Y = client.Entity.Y;

                                    suse.AddTarget(client.Entity.UID, 0, null);

                                    //Accuracy
                                    client.Entity.AccuracyStamp = Time32.Now;
                                    client.Entity.StarOfAccuracyStamp = Time32.Now;
                                    client.Entity.StarOfAccuracyTime = 0;
                                    client.Entity.AccuracyTime = 0;
                                    client.Entity.AddFlag((ulong)FlagsUpdate.Flags.StarOfAccuracy,60,true);
                                    client.Entity.StarOfAccuracyStamp = Time32.Now;
                                    client.Entity.StarOfAccuracyTime = (byte)60;
                                    if (client.Entity.EntityFlag == EntityFlag.Player)
                                        client.Entity.Owner.Send(Constants.Accuracy(60));

                                    //Invisibility
                                    client.Entity.ShieldTime = 0;
                                    client.Entity.ShieldStamp = Time32.Now;
                                    client.Entity.MagicShieldStamp = Time32.Now;
                                    client.Entity.MagicShieldTime = 0;
                                    client.Entity.AddFlag((ulong)FlagsUpdate.Flags.MagicShield,150,true);
                                    client.Entity.MagicShieldStamp = Time32.Now;
                                    client.Entity.MagicShieldIncrease = 15;
                                    client.Entity.MagicShieldTime = 60;
                                    if (client.Entity.EntityFlag == EntityFlag.Player)
                                        client.Entity.Owner.Send(Constants.Shield(50, 60));
                                    client.Entity.TiempoMascotas = Time32.Now;
                                }
                                #endregion
                                break;
                            }
                        case PetType.Attacker:
                            {
                                #region VoltaicWarg (grant Taoist Shield and Stigma)
                                if (client.Entity.TiempoMascotas.AddSeconds(120) < Time32.Now)
                                {
                                    SpellUse suse = new SpellUse(true);
                                    suse.Attacker = pet.Entity.UID;
                                    suse.SpellID = 30000;
                                    suse.SpellLevel = 4;
                                    suse.X = client.Entity.X;
                                    suse.Y = client.Entity.Y;

                                    suse.AddTarget(client.Entity.UID, 0, null);
                                    //pure Taoist shields
                                    client.Entity.AzureShieldDefence = 12000;
                                    client.Entity.MagicShieldStamp = Time32.Now;
                                    client.Entity.AzureShieldStamp = DateTime.Now;
                                    client.Entity.AddFlag((ulong)FlagsUpdate.Flags.AzureShield, 60, true);
                                    client.Entity.MagicShieldTime = 60;
                                    client.Entity.AzureShieldPacket();
                                    if (client.Entity.EntityFlag == EntityFlag.Player)
                                        client.Entity.Owner.Send(Constants.Shield(50, 60));
                                    //Stigma
                                    client.Entity.AddFlag((ulong)FlagsUpdate.Flags.Stigma, 80, true);
                                    client.Entity.StigmaStamp = Time32.Now;
                                    client.Entity.StigmaIncrease = 50;
                                    client.Entity.StigmaTime = (byte)60;
                                    if (client.Entity.EntityFlag == EntityFlag.Player)
                                        client.Entity.Owner.Send(Constants.Stigma(50, 60));
                                    client.Entity.TiempoMascotas = Time32.Now;
                                }
                                #endregion
                                break;
                            }
                        case PetType.defender:
                            {
                                #region Battle Master Panda (grant Stigma and Magic Shields)
                                if (client.Entity.TiempoMascotas.AddSeconds(120) < Time32.Now)
                                {
                                    SpellUse suse = new SpellUse(true);
                                    suse.Attacker = pet.Entity.UID;
                                    suse.SpellID = 1095;
                                    suse.SpellLevel = 4;
                                    suse.X = client.Entity.X;
                                    suse.Y = client.Entity.Y;

                                    suse.AddTarget(client.Entity.UID, 0, null);

                                    //Stigma
                                    client.Entity.AddFlag((ulong)FlagsUpdate.Flags.Stigma,60,true);
                                    client.Entity.StigmaStamp = Time32.Now;
                                    client.Entity.StigmaIncrease = 50;
                                    client.Entity.StigmaTime = (byte)60;
                                    if (client.Entity.EntityFlag == EntityFlag.Player)
                                        client.Entity.Owner.Send(Constants.Stigma(50, 60));
                                    //Magic Shields
                                    client.Entity.ShieldTime = 0;
                                    client.Entity.ShieldStamp = Time32.Now;
                                    client.Entity.MagicShieldStamp = Time32.Now;
                                    client.Entity.MagicShieldTime = 0;
                                    client.Entity.AddFlag((ulong)FlagsUpdate.Flags.MagicShield,60,true);
                                    client.Entity.MagicShieldStamp = Time32.Now;
                                    client.Entity.MagicShieldIncrease = 15;
                                    client.Entity.MagicShieldTime = 60;
                                    if (client.Entity.EntityFlag == EntityFlag.Player)
                                        client.Entity.Owner.Send(Constants.Shield(50, 60));
                                    client.Entity.TiempoMascotas = Time32.Now;
                                }
                                #endregion
                                break;
                            }
                    }
                }
                else
                    break;
            }
        }
        #endregion
        #region Timers
        private IDisposable[] TimerSubscriptions;
        private object DisposalSyncRoot;
        ~Pet()
        {
            DisposeTimers();
            Owner = null;
            Pets = null;
        }
        internal void DisposeTimers()
        {
            if (DisposalSyncRoot == null) return;
            lock (DisposalSyncRoot)
            {
                if (TimerSubscriptions == null) return;
                for (int i = 0; i < TimerSubscriptions.Length; i++)
                {
                    if (TimerSubscriptions[i] != null)
                    {
                        TimerSubscriptions[i].Dispose();
                        TimerSubscriptions[i] = null;
                    }
                }
            }
        }
        #endregion

        public uint MaxAllowed = 1;
        public GameState Owner;
        public SafeDictionary<PetType, PetInfo> Pets;
        public Pet(GameState client)
        {
            Owner = client;
            Pets = new SafeDictionary<PetType, PetInfo>();
            DisposalSyncRoot = new object();
            TimerSubscriptions = new IDisposable[] { PetsAction.Add(client) };
        }

        public void AddPet(MonsterInformation Mob, PetType Type = PetType.Normal)
        {
            if (Pets.Count == MaxAllowed)
                ClearAll();
            if (Mob.Mesh == 847)
                Type = PetType.Stiger;
            if (Mob.Mesh == 850)
                Type = PetType.Attacker;
            if (Mob.Mesh == 848 || Mob.Mesh == 849)
                Type = PetType.defender;
            if (Mob.Mesh == 296)
                Type = PetType.Looter;
            if (Mob.SpellID == 0)
                Mob.SpellID = 1002;

            if (Type != PetType.Normal)
            {
                var mesh = Mob.Mesh;
                var Name = Mob.Name;

                MonsterInformation.MonsterInformations.TryGetValue(9003, out Mob);

                Mob.Mesh = mesh;
                Mob.Name = Name;
            }

            if (Pets.ContainsKey(Type))
            {
                Data data = new Data(true);
                data.UID = Pets[Type].Entity.UID;
                data.ID = Data.RemoveEntity;
                Pets[Type].Entity.MonsterInfo.SendScreen(data);
                Pets[Type].Entity = null;
                Pets.Remove(Type);
            }
            PetInfo pet = new PetInfo();
            pet.Type = Type;
            pet.Owner = Owner;
            pet.Entity = new Entity(EntityFlag.Monster, true);
            pet.Entity.MonsterInfo = new MonsterInformation();
            pet.Entity.Owner = Owner;
            pet.Entity.MapObjType = MapObjectType.Monster;
            pet.Entity.MonsterInfo = Mob.Copy();
            pet.Entity.MonsterInfo.Owner = pet.Entity;

            pet.Entity.Name = Mob.Name;
            if (Type != PetType.Normal)
                pet.Entity.Name = Mob.Name + "(" + Owner.Entity.Name + ")";

            pet.Entity.MinAttack = Mob.MinAttack;
            pet.Entity.MaxAttack = pet.Entity.MagicAttack = Math.Max(Mob.MinAttack, Mob.MaxAttack);
            pet.Entity.Hitpoints = pet.Entity.MaxHitpoints = Mob.Hitpoints;
            pet.Entity.Body = Mob.Mesh;
            pet.Entity.Level = Mob.Level;
            pet.Entity.UID = (uint)(Owner.Entity.UID - (200000 + Pets.Count));
            pet.Entity.MapID = Owner.Map.ID;
            pet.Entity.SendUpdates = true;
            pet.Entity.X = Owner.Entity.X;
            pet.Entity.Y = Owner.Entity.Y;
            pet.Entity.pettype = Type;
            Pets.Add(pet.Type, pet);

            Owner.SendScreenSpawn(pet.Entity, false);
            pet.Entity.SendSpawn(Owner);
        }
        public void RemovePet(PetType Type)
        {
            if (Pets.Count == 0)
                return;
            Data data = new Data(true);
            data.UID = Pets[Type].Entity.UID;
            data.ID = Data.RemoveEntity;
            Pets[Type].Entity.MonsterInfo.SendScreen(data);
            Pets.Remove(Type);
        }
        public void ClearAll()
        {
            if (Pets.Count > 0)
            {
                foreach (var pet in Pets.Values)
                {
                    Data data = new Data(true);
                    data.UID = pet.Entity.UID;
                    data.ID = Data.RemoveEntity;
                    pet.Entity.MonsterInfo.SendScreen(data);
                    pet.Entity = null;
                }
                Pets.Clear();
            }
        }
    }
}

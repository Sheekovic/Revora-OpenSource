// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Collections.Generic;

namespace Revora.Game.Attacking
{
    public unsafe class Fan
    {
        private int Range = 0;
        private int Width = 0;
        private ushort UserX, UserY = 0;
        private ushort SourceX, SourceY = 0;
        unsafe static float SquareRootFloat(float number)
        {
            long i;
            float x, y;
            const float f = 1.5F;
            x = number * 0.5F;
            y = number;
            i = *(long*)&y;
            i = 0x5f3759df - (i >> 1);
            y = *(float*)&i;
            y = y * (f - (x * y * y));
            y = y * (f - (x * y * y));
            return number * y;
        }
        public bool IsInFan(ushort TargetX, ushort TargetY)
        {
            if (UserX == SourceX && UserY == SourceY) return false;
            if (GetDistance(UserX, UserY, TargetX, TargetY) > Range) return false;
            double PI = Math.PI;
            double fRadianDelta = (PI * Width / 180) / 2;
            float fCenterLine = GetRadian(UserX, UserY, TargetX, TargetY);
            float fTargetLine = GetRadian(SourceX, SourceY, UserX, UserY);
            float fDelta = Math.Abs(fCenterLine - fTargetLine);
            if (fDelta <= fRadianDelta || fDelta >= 2 * PI - fRadianDelta)
                return false;
            return true;
        }
        private static short GetDistance(ushort X, ushort Y, ushort X2, ushort Y2)
        {
            short x = 0;
            short y = 0;
            if (X >= X2)
            {
                x = (short)(X - X2);
            }
            else if (X2 >= X)
            {
                x = (short)(X2 - X);
            }
            if (Y >= Y2)
            {
                y = (short)(Y - Y2);
            }
            else if (Y2 >= Y)
            {
                y = (short)(Y2 - Y);
            }
            if (x > y)
                return x;
            else return y;
        }
        public Fan(ushort x, ushort y, ushort x2, ushort y2, int nRange, int nWidth)
        {
            UserX = x;
            UserY = y;
            SourceX = x2;
            SourceY = y2;
            Range = nRange;
            Width = nWidth;
        }
        private static float GetRadian(float posSourX, float posSourY, float posTargetX, float posTargetY)
        {
            float PI = 3.1415926535f;
            float fDeltaX = posTargetX - posSourX;
            float fDeltaY = posTargetY - posSourY;
            float fDistance = SquareRootFloat(fDeltaX * fDeltaX + fDeltaY * fDeltaY);
            double fRadian = (float)Math.Asin(fDeltaX / fDistance);
            return (float)(fDeltaY > 0 ? (PI / 2 - fRadian) : (PI + fRadian + PI / 2));
        }
    }
    public unsafe class Sector
    {
        private int attackerX, attackerY, attackX, attackY;
        private int degree, sectorsize, leftside, rightside;
        private int distance;
        private bool addextra;
        public Sector(int attackerX, int attackerY, int attackX, int attackY)
        {
            this.attackerX = attackerX;
            this.attackerY = attackerY;
            this.attackX = attackX;
            this.attackY = attackY;
            this.degree = Kernel.GetDegree(attackerX, attackX, attackerY, attackY);
            this.addextra = false;
        }
        public void Arrange(int sectorsize, int distance)
        {
            this.distance = Math.Min(distance, 14);
            this.sectorsize = sectorsize;
            this.leftside = this.degree - (sectorsize / 2);
            if (this.leftside < 0)
                this.leftside += 360;
            this.rightside = this.degree + (sectorsize / 2);
            if (this.leftside < this.rightside || this.rightside - this.leftside != this.sectorsize)
            {
                this.rightside += 360;
                this.addextra = true;
            }
        }
        public bool Inside(int X, int Y)
        {
            if (Kernel.GetDistance((ushort)X, (ushort)Y, (ushort)attackerX, (ushort)attackerY) <= distance)
            {
                int degree = Kernel.GetDegree(attackerX, X, attackerY, Y);
                if (this.addextra)
                    degree += 360;
                if (degree >= this.leftside && degree <= this.rightside)
                    return true;
            }
            return false;
        }
    }
    public unsafe class RangeMove
    {
        public class Coords
        {
            public int X;
            public int Y;
        }
        public List<Coords> MoveCoords(ushort X, ushort Y, ushort XX, ushort YY, byte useDistance = 0)
        {
            List<Coords> lis = new List<Coords>();
            Enums.ConquerAngle angle = GetAngle(X, Y, XX, YY);
            byte distance = (byte)GetDistance(X, Y, XX, YY);
            if (useDistance != 0)
                distance = useDistance;
            int dx = XX - X, dy = YY - Y, steps, k;
            float xincrement, yincrement, x = X, y = Y;

            if (Math.Abs(dx) > Math.Abs(dy))
                steps = Math.Abs(dx);
            else
                steps = Math.Abs(dy);
            xincrement = dx / (float)steps;
            yincrement = dy / (float)steps;
            lis.Add(new Coords() { X = (int)Math.Round(x), Y = (int)Math.Round(y) });
            for (k = 0; k < distance; k++)
            {
                x += xincrement;
                y += yincrement;
                lis.Add(new Coords() { X = (int)Math.Round(x), Y = (int)Math.Round(y) });
            }
            return lis;
        }
        public Enums.ConquerAngle GetAngle(ushort X, ushort Y, ushort X2, ushort Y2)
        {
            double direction = 0;
            double AddX = X2 - X;
            double AddY = Y2 - Y;
            double r = (double)Math.Atan2(AddY, AddX);
            if (r < 0) r += (double)Math.PI * 2;
            direction = 360 - (r * 180 / (double)Math.PI);
            byte Dir = (byte)((7 - (Math.Floor(direction) / 45 % 8)) - 1 % 8);
            return (Enums.ConquerAngle)(byte)((int)Dir % 8);
        }
        public short GetDistance(ushort X, ushort Y, ushort X2, ushort Y2)
        {
            short x = 0;
            short y = 0;
            if (X >= X2) x = (short)(X - X2);
            else if (X2 >= X) x = (short)(X2 - X);
            if (Y >= Y2) y = (short)(Y - Y2);
            else if (Y2 >= Y) y = (short)(Y2 - Y);
            if (x > y)
                return x;
            else
                return y;
        }
        public bool InRange(ushort X, ushort Y, byte Range, List<Coords> bas)
        {
            foreach (Coords line in bas)
            {
                byte distance = (byte)GetDistance((ushort)X, (ushort)Y, (ushort)line.X, (ushort)line.Y);
                if (distance <= Range)
                    return true;
            }
            return false;
        }
    }
    public unsafe class FloorSpell
    {
        public FloorSpell(uint ID, ushort MapID, ushort X, ushort Y, ushort color, Database.SpellInformation _DBSkill, int MillisecondsStamp)
        {
            FloorPacket = new Network.GamePackets.FloorItem(true);
            FloorPacket.UID = Network.GamePackets.FloorItem.FloorUID.Next;
            FloorPacket.ItemID = ID;
            FloorPacket.X = X;
            FloorPacket.Y = Y;
            FloorPacket.ItemColor = (Enums.Color)color;
            FloorPacket.Type = Network.GamePackets.FloorItem.Effect;
            if (_DBSkill.ID == 12370 || _DBSkill.ID == 12380)
            {
                FloorPacket.MapID = MapID;
                Stamp = Time32.Now.AddSeconds(MillisecondsStamp);
                FloorPacket.mColor = 13;
                FloorPacket.OnFloor = Time32.Now;
                FloorPacket.FlowerType = 3;
                FloorPacket.time = Kernel.TqTimer(DateTime.Now.AddSeconds(7));
            }
            else
            {
                Stamp = Time32.Now.AddMilliseconds(MillisecondsStamp);
            }
            DBSkill = _DBSkill;
        }
        public Time32 Stamp = new Time32();
        public Network.GamePackets.FloorItem FloorPacket;
        public Database.SpellInformation DBSkill;
        public unsafe class ClientFloorSpells
        {
            public List<FloorSpell> Spells = new List<FloorSpell>();
            public Network.GamePackets.SpellUse SpellPacket;
            public Database.SpellInformation DBSkill;
            public ushort X;
            public ushort Y;
            public uint UID;
            private object SyncRoot;
            public ClientFloorSpells(uint _UID, ushort _X, ushort _Y, byte _LevelHu, Database.SpellInformation _DBSkill)
            {
                DBSkill = _DBSkill;
                SyncRoot = new object();
                X = _X;
                Y = _Y;
                UID = _UID;
            }
            public void CreateMsgSpell(uint Target)
            {
                SpellPacket = new Network.GamePackets.SpellUse(true);
                SpellPacket.Attacker = UID;
                SpellPacket.Attacker1 = Target;
                SpellPacket.X = X;
                SpellPacket.Y = Y;
                SpellPacket.SpellID = DBSkill.ID;
                SpellPacket.SpellLevel = DBSkill.Level;
            }
            public bool CheckInvocke(Time32 Now, FloorSpell spell)
            {
                if (Now > spell.Stamp)
                {
                    return true;
                }
                return false;
            }
            public void RemoveItem(FloorSpell item)
            {
                lock (SyncRoot)
                    Spells.Remove(item);
            }
            public void SendView(Client.GameState client)
            {
                client.SendScreen(SpellPacket, true);
            }
            public void AddItem(FloorSpell item)
            {
                lock (SyncRoot)
                    Spells.Add(item);
            }
        }
    }
    public unsafe class LayTrapThree
    {
        public struct coords
        {
            public int X;
            public int Y;
            public coords(double x, double y)
            {
                this.X = (int)x;
                this.Y = (int)y;
            }
        }
        public ushort X1 { get; set; }
        public ushort Y1 { get; set; }
        public ushort X2 { get; set; }
        public ushort Y2 { get; set; }
        public byte Direction { get; set; }
        public List<coords> LCoords;
        public byte MaxDistance = 18;
        public LayTrapThree(ushort X1, ushort X2, ushort Y1, ushort Y2, byte MaxDistance)
        {
            this.X1 = X1;
            this.Y1 = Y1;
            this.X2 = X2;
            this.Y2 = Y2;
            this.MaxDistance = MaxDistance;
            LCoords = LineCoords(X1, Y1, X2, Y2);
        }
        List<coords> LineCoords(ushort userx, ushort usery, ushort shotx, ushort shoty)
        {
            return linedda(userx, usery, shotx, shoty);
        }
        List<coords> linedda(int xa, int ya, int xb, int yb)
        {
            int dx = xb - xa, dy = yb - ya, steps, k;
            float xincrement, yincrement, x = xa, y = ya;
            if (Math.Abs(dx) > Math.Abs(dy))
                steps = Math.Abs(dx);
            else
                steps = Math.Abs(dy);
            xincrement = dx / (float)steps;
            yincrement = dy / (float)steps;
            List<coords> ThisLine = new List<coords>();
            ThisLine.Add(new coords(Math.Round(x), Math.Round(y)));
            for (k = 0; k < MaxDistance; k++)
            {
                x += xincrement;
                y += yincrement;
                ThisLine.Add(new coords(Math.Round(x), Math.Round(y)));
            }
            return GenerateTrapCoord(ThisLine);
        }
        List<coords> GenerateTrapCoord(List<coords> LineCoord)
        {
            List<coords> ThreeCoords = new List<coords>();
            for (int x = 3; x < LineCoord.Count; x += 5)
            {
                if (ThreeCoords.Count == 3)
                    break;
                if (LineCoord.Count > x)
                {
                    ThreeCoords.Add(LineCoord[x]);
                }
            }
            return ThreeCoords;
        }
    }
}
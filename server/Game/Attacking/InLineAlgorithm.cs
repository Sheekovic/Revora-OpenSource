// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Revora.Game.Attacking
{
    public unsafe class InLineAlgorithm
    {
        public enum Algorithm
        {
            DDA,
            SomeMath
        }
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
        bool Contains(List<coords> Coords, coords Check)
        {
            foreach (coords Coord in Coords)
                if (Coord.X == Check.X && Check.Y == Coord.Y)
                    return true;
            return false;
        }
        List<coords> LineCoords(ushort userx, ushort usery, ushort shotx, ushort shoty)
        {
            return linedda(userx, usery, shotx, shoty);
        }
        void Add(List<coords> Coords, int x, int y)
        {
            coords add = new coords((ushort)x, (ushort)y);
            if (!Coords.Contains(add))
                Coords.Add(add);
        }
        List<coords> linedda(int xa, int ya, int xb, int yb)
        {
            int dx = xb - xa, dy = yb - ya, steps, k;
            float xincrement, yincrement, x = xa, y = ya;

            if (Math.Abs(dx) > Math.Abs(dy)) steps = Math.Abs(dx);
            else steps = Math.Abs(dy);
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
            return ThisLine;
        }
        public List<coords> lcoords;
        public byte MaxDistance = 10;
        public InLineAlgorithm(ushort X1, ushort X2, ushort Y1, ushort Y2, byte MaxDistance, Algorithm algo)
        {
            algorithm = algo;
            this.X1 = X1;
            this.Y1 = Y1;
            this.X2 = X2;
            this.Y2 = Y2;
            if (algo == Algorithm.DDA)
                lcoords = LineCoords(X1, Y1, X2, Y2);
            this.MaxDistance = MaxDistance;
            Direction = (byte)Kernel.GetAngle(X1, Y1, X2, Y2); ;
        }
        public InLineAlgorithm(ushort X1, ushort X2, ushort Y1, ushort Y2, byte MaxDistance)
        {
            algorithm = Algorithm.DDA;
            this.X1 = X1;
            this.Y1 = Y1;
            this.X2 = X2;
            this.Y2 = Y2;
            this.MaxDistance = MaxDistance;
            lcoords = LineCoords(X1, Y1, X2, Y2);
            Direction = (byte)Kernel.GetAngle(X1, Y1, X2, Y2); ;
        }
        private Algorithm algorithm;
        public ushort X1 { get; set; }
        public ushort Y1 { get; set; }
        public ushort X2 { get; set; }
        public ushort Y2 { get; set; }
        public byte Direction { get; set; }
        public bool InLine(ushort X, ushort Y)
        {
            int mydst = Kernel.GetDistance((ushort)X1, (ushort)Y1, X, Y);
            byte dir = (byte)Kernel.GetAngle(X1, Y1, X, Y);

            if (mydst <= MaxDistance)
            {
                if (algorithm == Algorithm.SomeMath)
                {
                    if (dir != Direction)
                        return false;
                    if (X2 - X1 == 0)
                    {
                        return X == X1;
                    }
                    else if (Y2 - Y1 == 0)
                    {
                        return Y == Y1;
                    }
                    else
                    {
                        double val1 = ((double)(X - X1)) / ((double)(X2 - X1));
                        double val2 = ((double)(Y + Y1)) / ((double)(Y2 + Y1));
                        bool works = Math.Floor(val1) == Math.Floor(val2);
                        return works;
                    }
                }
                else
                    if (algorithm == Algorithm.DDA)
                        return Contains(lcoords, new coords(X, Y));
            }
            return false;
        }
    }
    public class NewInLineAlgorithm
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
        bool Contains(List<coords> Coords, coords Check)
        {
            foreach (coords Coord in Coords)
                if (Coord.X == Check.X && Check.Y == Coord.Y)
                    return true;
            return false;
        }
        List<coords> LineCoords(ushort userx, ushort usery, ushort shotx, ushort shoty)
        {
            return linedda(userx, usery, shotx, shoty);
        }
        void Add(List<coords> Coords, int x, int y)
        {
            coords add = new coords((ushort)x, (ushort)y);
            if (!Coords.Contains(add))
                Coords.Add(add);
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
            if (SpellID != 14530 && SpellID != 11980 && SpellID != 14680 && SpellID != 14250 && SpellID != 12690 && SpellID != 12670
                    && SpellID != 11650 && SpellID != 11990 && SpellID != 12890 && SpellID != 11190 && SpellID != 10315)
            {
                if (!map.IsFlagPresent((ushort)Math.Round(x), (ushort)Math.Round(y), Map.MapFlagType.Valid))
                    return ThisLine;
            }
            ThisLine.Add(new coords(Math.Round(x), Math.Round(y)));

            for (k = 0; k < MaxDistance; k++)
            {
                x += xincrement;
                y += yincrement;
                if (SpellID != 14530 && SpellID != 11980 && SpellID != 14680 && SpellID != 14250 && SpellID != 12690 && SpellID != 12670
                    && SpellID != 11650 && SpellID != 11990 && SpellID != 12890 && SpellID != 11190 && SpellID != 10315)
                {
                    if (!map.IsFlagPresent((ushort)Math.Round(x), (ushort)Math.Round(y), Map.MapFlagType.Valid))
                        return ThisLine;
                }
                ThisLine.Add(new coords(Math.Round(x), Math.Round(y)));
            }
            return ThisLine;
        }
        public List<coords> lcoords;
        public byte MaxDistance = 10;
        private Map map;
        private ushort SpellID = 0;
        public NewInLineAlgorithm(ushort X1, ushort X2, ushort Y1, ushort Y2, Map _map, byte MaxDistance, ushort spelldid = 0)
        {
            map = _map;

            this.X1 = X1;
            this.Y1 = Y1;
            this.X2 = X2;
            this.Y2 = Y2;

            this.MaxDistance = MaxDistance;
            SpellID = spelldid;
            lcoords = LineCoords(X1, Y1, X2, Y2);

        }
        public ushort X1 { get; set; }
        public ushort Y1 { get; set; }
        public ushort X2 { get; set; }
        public ushort Y2 { get; set; }
        public byte Direction { get; set; }

        public bool InLine(ushort X, ushort Y, byte Range, bool IncreaseRange = false)
        {

            int mydst = GetDistance((ushort)X1, (ushort)Y1, X, Y);

            if (mydst <= MaxDistance)
            {
                if (Range == 0)
                {
                    return Contains(lcoords, new coords(X, Y));
                }
                else
                    return InRange(X, Y, Range, lcoords, IncreaseRange);
            }

            return false;
        }
        public bool GetNewCoords(ref ushort X, ref ushort Y)
        {
            if (lcoords.Count > 0)
            {
                var coord = lcoords.Last();

                X = (ushort)coord.X;
                Y = (ushort)coord.Y;

                return true;
            }

            return false;
        }
        public bool InRange(ushort X, ushort Y, byte Range, List<coords> bas, bool IncreaseRange = false)
        {
            if (IncreaseRange)
            {
                uint _Range = Range;
                foreach (coords line in bas)
                {
                    byte distance = (byte)GetDistance((ushort)X, (ushort)Y, (ushort)line.X, (ushort)line.Y);
                    if (distance <= _Range)
                        return true;
                    if (_Range < 8)
                        _Range++;
                }
            }
            else
            {
                foreach (coords line in bas)
                {
                    byte distance = (byte)GetDistance((ushort)X, (ushort)Y, (ushort)line.X, (ushort)line.Y);
                    if (distance <= Range)
                        return true;
                }
            }
            return false;
        }

        public static Enums.ConquerAngle GetAngle(ushort X, ushort Y, ushort X2, ushort Y2)
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
        public static short GetDistance(ushort X, ushort Y, ushort X2, ushort Y2)
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
            else
                return y;
        }
    }
}
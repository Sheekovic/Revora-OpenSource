using System;

namespace Revora.Game
{
    public unsafe class Calculations
    {
        public static double PointDirecton(double x1, double y1, double x2, double y2)
        {
            double direction = 0;

            double AddX = x2 - x1;
            double AddY = y2 - y1;
            double r = (double)Math.Atan2(AddY, AddX);

            if (r < 0) r += (double)Math.PI * 2;

            direction = 360 - (r * 180 / (double)Math.PI);
            return direction;
        }
        public static double PointDirectonRad(double x1, double y1, double x2, double y2)
        {
            double AddX = x2 - x1;
            double AddY = y2 - y1;
            double r = (double)Math.Atan2(AddY, AddX);

            return r;
        }
        public static double PointDirecton2(double x1, double y1, double x2, double y2)
        {
            double direction = 0;

            double AddX = x2 - x1;
            double AddY = y2 - y1;
            double r = (double)Math.Atan2(AddY, AddX);

            direction = (r * 180 / (double)Math.PI);
            return direction;
        }
        public static double RadianToDegree(double r)
        {
            if (r < 0) r += (double)Math.PI * 2;

            double direction = 360 - (r * 180 / (double)Math.PI);
            return direction;
        }
        public static double DegreeToRadian(double degr)
        {
            return degr * Math.PI / 180;
        }
        public static int PointDistance(double x1, double y1, double x2, double y2)
        {
            return (int)Math.Sqrt(((x1 - x2) * (x1 - x2)) + ((y1 - y2) * (y1 - y2)));
        }
        public static bool InBox(double x1, double y1, double x2, double y2, byte Range)
        {
            return (Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2)) <= Range);
        }
        public static uint Percent(Entity attacked, float percent)
        {
            return (uint)(attacked.Hitpoints * percent);
        }
        public static uint Percent(Entity attacked, double percent)
        {
            return (uint)(attacked.Hitpoints * percent);
        }
        public static uint Percent(int target, float percent)
        {
            return (uint)(target * percent);
        }
        internal static bool GuildGateBlocks(ushort map, ushort oldX, ushort oldY, ushort x, ushort y)
        {
            Network.GamePackets.SobNpcSpawn left, right;
            if (map == 1038) { left = GuildWar.LeftGate; right = GuildWar.RightGate; }
            else if (map == 1234) { left = SuperGuildWar.LeftGate; right = SuperGuildWar.RightGate; }
            else return false;
            // A missing battlefield is unavailable; another event's gates cannot
            // substitute for this map's structures.
            if (left == null || right == null) return true;
            return right.Mesh / 10 == 27 && oldX >= right.X && x <= right.X && y < left.Y
                || left.Mesh / 10 == 24 && oldY >= left.Y && y <= left.Y && x < right.X;
        }
        public static void IsBreaking(Client.GameState client, ushort oldX, ushort oldY)
        {
            if (!GuildGateBlocks(client.Entity.MapID, oldX, oldY, client.Entity.X, client.Entity.Y)) return;
            client.Entity.X = oldX;
            client.Entity.Y = oldY;
            client.Disconnect();
        }
    }
}

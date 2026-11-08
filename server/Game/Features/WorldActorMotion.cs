using System;
using Revora.Client;
using Revora.Interfaces;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public static class WorldActorMotion
    {
        public static void Approach(Entity body, ushort destX, ushort destY)
        {
            if (body == null || body.Owner == null || body.Dead) return;
            short dist = Kernel.GetDistance(body.X, body.Y, destX, destY);
            if (dist <= 0) return;
            if (dist >= 5)
                Jump(body, destX, destY);
            else if (dist >= 2)
                Walk(body, destX, destY, true);
            else
                Walk(body, destX, destY, false);
        }

        public static bool Walk(Entity body, ushort destX, ushort destY, bool run)
        {
            if (body == null || body.Owner == null) return false;
            int steps = run ? 2 : 1;
            bool moved = false;
            Enums.ConquerAngle facing = body.Facing;
            for (int i = 0; i < steps; i++)
            {
                if (body.X == destX && body.Y == destY) break;
                facing = Kernel.GetAngle(body.X, body.Y, destX, destY);
                if (!body.Move(facing, 0))
                    break;
                moved = true;
                SendWalk(body, facing, run);
            }
            return moved;
        }

        public static bool Jump(Entity body, ushort destX, ushort destY)
        {
            if (body == null || body.Owner == null || body.Owner.Map == null) return false;
            Map map = body.Owner.Map;
            int span = Kernel.Random.Next(5, 9);
            short dx = (short)(destX - body.X);
            short dy = (short)(destY - body.Y);
            int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (steps == 0) return false;
            if (steps > span) steps = span;
            ushort nx = body.X;
            ushort ny = body.Y;
            int denom = Math.Max(Math.Abs(dx), Math.Abs(dy));
            for (int i = 1; i <= steps; i++)
            {
                int tx = body.X + dx * i / denom;
                int ty = body.Y + dy * i / denom;
                if (tx < 1 || ty < 1) break;
                if (!map.Floor[tx, ty, MapObjectType.Player, null])
                    break;
                nx = (ushort)tx;
                ny = (ushort)ty;
            }
            if (nx == body.X && ny == body.Y)
                return Walk(body, destX, destY, true);
            return SendJump(body, nx, ny);
        }

        internal static bool JumpClear(Entity body, ushort x, ushort y)
        {
            if (body == null || body.Dead || body.Owner == null || body.Owner.Map == null
                || body.Owner.Map.Floor == null) return false;
            int distance = Kernel.GetDistance(body.X, body.Y, x, y);
            if (distance < 3 || distance > 6) return false;
            Map map = body.Owner.Map;
            if (!CompanionNavigation.HasSight(new System.Drawing.Point(body.X, body.Y),
                new System.Drawing.Point(x, y), (tx, ty) => map.Floor[tx, ty, MapObjectType.Player, null]))
                return false;
            return SendJump(body, x, y);
        }

        private static bool SendJump(Entity body, ushort nx, ushort ny)
        {
            ushort oldX = body.X;
            ushort oldY = body.Y;
            body.Facing = Kernel.GetAngle(oldX, oldY, nx, ny);
            body.Action = Enums.ConquerAction.Jump;
            body.PX = oldX;
            body.PY = oldY;
            body.X = nx;
            body.Y = ny;
            Data jump = new Data(true);
            jump.ID = Data.Jump;
            jump.UID = body.UID;
            jump.wParam1 = oldX;
            jump.wParam2 = oldY;
            jump.Xt1 = nx;
            jump.Yt1 = ny;
            jump.Facing = body.Facing;
            jump.TimeStamp = Time32.Now;
            Notify(body, jump);
            return true;
        }

        private static void SendWalk(Entity body, Enums.ConquerAngle facing, bool run)
        {
            GroundMovement move = new GroundMovement(true);
            move.Direction = facing;
            move.UID = body.UID;
            move.GroundMovementType = run ? GroundMovement.Run : GroundMovement.Walk;
            move.TimeStamp = (uint)Time32.Now.GetHashCode();
            move.MapID = body.MapID;
            body.Action = Enums.ConquerAction.None;
            Notify(body, move);
        }

        private static void Notify(Entity body, IPacket packet)
        {
            foreach (GameState client in Kernel.GamePool.Values)
            {
                if (client == null || client.Fake || client.Entity == null) continue;
                if (client.Entity.MapID != body.MapID) continue;
                int dist = Kernel.GetDistance(client.Entity.X, client.Entity.Y, body.X, body.Y);
                if (dist > Constants.pScreenDistance)
                    continue;
                body.SendSpawn(client, true);
                client.Send(packet);
            }
        }
    }
}

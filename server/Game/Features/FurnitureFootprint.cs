using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Revora.Interfaces;

namespace Revora.Game.Features
{
    // Derived access masks for the twelve hash-audited terrain scenes.
    // Native placement is anchor + start - column/row; all twelve starts are zero.
    internal static class FurnitureFootprint
    {
        private sealed class Shape
        {
            internal readonly int Width, Height, Mask;
            internal Shape(int width,int height,int mask){Width=width;Height=height;Mask=mask;}
        }
        private static readonly Shape[] shapes={
            new Shape(1,1,1),new Shape(3,2,15),new Shape(2,3,23),new Shape(2,1,0),
            new Shape(3,1,0),new Shape(3,1,7),new Shape(3,1,0),new Shape(1,4,15),
            new Shape(3,2,1),new Shape(1,1,1),new Shape(1,3,5),new Shape(2,4,87)};
        internal static bool Known(INpc npc){return HouseFurniture.BoxActor(npc) || npc!=null && npc.Type==Enums.NpcType.Furniture && npc.Mesh/10>=1 && npc.Mesh/10<=12 && npc.Mesh%10<=7;}
        internal static IEnumerable<Point> Cells(int x,int y,ushort look,bool blockedOnly=false)
        {
            int model=look/10;
            // Revora's ordinary-NPC clearance policy, not a terrain-scene mask.
            if(model==820 && look%10<=7){for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)yield return new Point(x+dx,y+dy);yield break;}
            if(model<1 || model>12 || look%10>7)throw new ArgumentException("Unknown furniture appearance.");
            var shape=shapes[model-1];
            for(int row=0;row<shape.Height;row++)for(int col=0;col<shape.Width;col++)
                if(!blockedOnly || (shape.Mask & (1<<(row*shape.Width+col)))!=0)yield return new Point(x-col,y-row);
        }
        internal static void Add(Map map,INpc npc)
        {
            var cells=Cells(npc.X,npc.Y,npc.Mesh).ToArray();
            if(cells.Any(p=>p.X<0 || p.Y<0 || p.X>=map.Floor.Bounds.Width || p.Y>=map.Floor.Bounds.Height))
                throw new InvalidOperationException("Saved furniture footprint is outside its map.");
            foreach(var p in Cells(npc.X,npc.Y,npc.Mesh,true))map.Floor.AddFlag(p.X,p.Y,Floor.DMapPointFlag.Furniture);
        }
        internal static void Remove(Map map,INpc npc)
        {
            foreach(var p in Cells(npc.X,npc.Y,npc.Mesh,true)){
                if(p.X<0 || p.Y<0 || p.X>=map.Floor.Bounds.Width || p.Y>=map.Floor.Bounds.Height)continue;
                bool occupied=map.Npcs.Values.Any(other=>Known(other) && Cells(other.X,other.Y,other.Mesh,true).Contains(p));
                if(!occupied)map.Floor.TakeFlag(p.X,p.Y,Floor.DMapPointFlag.Furniture);
            }
        }
    }
}

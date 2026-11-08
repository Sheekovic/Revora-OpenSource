namespace Revora.Game.Features
{
    internal static class DesertTreasurePopulation
    {
        // Keep configured regions/counts. If random placement misses sparse terrain,
        // scan only this quest family's bounded rectangle for a free valid tile.
        internal static bool Place(Map map,Entity body){
            if(map==null || map.ID!=1001 || map.BaseID!=1001 || body==null || body.MonsterInfo==null)return false;
            var info=body.MonsterInfo;uint id=info.ID;if(id!=20 && id!=80 && id!=56 && id!=81 && id!=86)return false;
            if(map.Floor[body.X,body.Y,MapObjectType.Monster,null])return true;
            int endX=System.Math.Min(map.Floor.Bounds.Width,(int)info.BoundX+info.BoundCX),endY=System.Math.Min(map.Floor.Bounds.Height,(int)info.BoundY+info.BoundCY);
            for(int y=info.BoundY;y<endY;y++)for(int x=info.BoundX;x<endX;x++)if(map.Floor[x,y,MapObjectType.Monster,null]){
                body.X=(ushort)x;body.Y=(ushort)y;return true;
            }
            return false;
        }
    }
}

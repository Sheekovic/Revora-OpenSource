using System;
using Revora.Client;
namespace Revora.Game.Features
{
    // Existing Spun Gold API; both quests share one appearance owner per character.
    internal static class SpunGoldAppearance
    {
        internal const uint BeautyMesh=QuestAppearance.BeautyMesh;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Guid Apply(GameState p,Func<bool> context){return QuestAppearance.Apply(p,BeautyMesh,context);}
        internal static bool Owns(GameState p,Guid token){return QuestAppearance.Owns(p,token,BeautyMesh);}
        internal static Guid Token(GameState p){return QuestAppearance.Token(p,BeautyMesh);}
        internal static uint Project(Entity body,uint ordinary){return QuestAppearance.Project(body,ordinary);}
        internal static void Clear(GameState p,Guid token){QuestAppearance.Clear(p,token,BeautyMesh);}
        internal static void Tick(){QuestAppearance.Tick();}
    }
}

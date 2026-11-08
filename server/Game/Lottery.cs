namespace Revora.Game
{
    public static class Lottery
    {
        public static void GiveLotteryPrize(Client.GameState player) { Features.LotteryDraws.Claim(player); }
        public static void LuckyBox(uint npcID,Client.GameState player,bool jade=false) {
            Features.LotteryDraws.Open(player,npcID==0 && player!=null?player.ActiveNpc:npcID,jade);
        }
        public static void Process(byte[] packet,Client.GameState player) { Features.LotteryDraws.Process(packet,player); }
    }
}

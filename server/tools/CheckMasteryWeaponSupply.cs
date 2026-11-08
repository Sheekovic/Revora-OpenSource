using System;
using System.Linq;
using Revora.Database;

// Loaded stock evidence only; no database, server entry point or item grants.
internal static class CheckMasteryWeaponSupply
{
    static void Main() {
        ConquerItemInformation.Load();ShopFile.Load();EShopFile.Load();EShopV2File.Load();
        ushort[] families={420,410,460,540,580,560,490,561,510,530,430,450,480,481,440,500,610,601,421,900,611,612,511,613,614,616,617,620,622,624,626,671,670};
        Console.WriteLine("family,catalog_count,priced_shop_rows,example_shop,example_item,money_type,price,required_class,level,proficiency");
        foreach(ushort family in families) {
            var catalog=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID/1000==family).ToArray();
            var stock=ShopFile.Shops.Values.Concat(EShopFile.Shops.Values).Where(s=>s.Items!=null)
                .SelectMany(s=>s.Items.Distinct().Where(id=>id/1000==family && ConquerItemInformation.BaseInformations.ContainsKey(id))
                .Select(id=>new {Shop=s,Item=ConquerItemInformation.BaseInformations[id]}))
                .Select(x=>new {x.Shop,x.Item,Price=x.Shop.MoneyType==ShopFile.MoneyType.Gold?x.Item.GoldWorth:x.Shop.MoneyType==ShopFile.MoneyType.ConquerPoints?x.Item.ConquerPointsWorth:0u})
                .Where(x=>x.Price>0).OrderBy(x=>x.Item.Proficiency).ThenBy(x=>x.Item.Level).ThenBy(x=>x.Price).ThenBy(x=>x.Shop.UID).ThenBy(x=>x.Item.ID).ToArray();
            if(stock.Length==0)Console.WriteLine(family+","+catalog.Length+",0,,,,,,,");
            else {var x=stock[0];Console.WriteLine(family+","+catalog.Length+","+stock.Length+","+x.Shop.UID+","+x.Item.ID+","+(int)x.Shop.MoneyType+","+x.Price+","+x.Item.Class+","+x.Item.Level+","+x.Item.Proficiency);}
        }
    }
}

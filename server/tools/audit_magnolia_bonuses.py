"""Read-only identity audit of the implemented Magnolia bonus pool."""
import json
import re
from pathlib import Path
from audit_warrior_epic import digest, item_rows, require


def audit():
    root = Path(__file__).resolve().parents[1]
    names = {1088001: "Meteor", 1088000: "DragonBall", 723017: "ExpPotion",
             723903: "Saddle", 753003: "3Orchids", 753009: "9Orchids",
             754001: "1Tulip", 754003: "3Tulips", 754009: "9Tulips",
             756001: "1LoveLetter", 756003: "3LoveLetters", 756009: "9LoveLetters",
             720128: "PenitenceAmulet", 723714: "Class2MoneyBag",
             720027: "MeteorScroll", 720578: "PrayingStone(M)", 721259: "CelestialStone"}
    for base, name in ((700000,"PhoenixGem"),(700010,"DragonGem"),(700020,"FuryGem"),
                       (700030,"RainbowGem"),(700040,"KylinGem"),(700050,"VioletGem"),
                       (700060,"MoonGem"),(700070,"TortoiseGem"),(700100,"ThunderGem"),(700120,"GloryGem")):
        for quality in (1,3):
            names[base + quality] = name
    source = (root / "Game/Features/MagnoliaBonuses.cs").read_text()
    pools = source.split("private static readonly uint[][] BonusPools=", 1)[1].split("};", 1)[0]
    require({int(x) for x in re.findall(r"\b\d{6,7}\b", pools)} == set(names),
            "Bonus identities differ from reviewed pool")
    client = (root.parents[1] / "6711/ini/itemtype.dat").read_bytes()
    server = (root / "bin/Debug/database/itemtype-new.txt").read_bytes()
    require(digest(client) == "d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a", "Client catalog changed")
    require(digest(server) == "1ed7a23b7a34b5e3706277f97e421e3cdf0b269eeb5be7367a4ebc0e4f22b3ef", "Server catalog changed")
    native, hosted = item_rows(client, True, names), item_rows(server, False, names)
    for identity, name in names.items():
        require(native[identity][1].decode("ascii") == name, "Unexpected bonus name")
        require(native[identity][:8] == hosted[identity][:8], "Client/server bonus requirements differ")
    moon = {721090: "MoonBox"}
    require("BonusPools[grade].Concat(new[]{721090u})" in source, "Moon Box pool extension changed")
    native_moon, hosted_moon = item_rows(client, True, moon), item_rows(server, False, moon)
    require(native_moon[721090][1] == b"MoonBox" and native_moon[721090][:8] == hosted_moon[721090][:8],
            "Canonical Moon Box identity differs")
    ticket = {710212: "LotteryTicket"}
    require("BonusPools[grade].Concat(new[]{710212u})" in source, "Lottery Ticket pool extension changed")
    native_ticket, hosted_ticket = item_rows(client, True, ticket), item_rows(server, False, ticket)
    require(native_ticket[710212][1] == b"LotteryTicket" and native_ticket[710212][:8] == hosted_ticket[710212][:8],
            "Lottery Ticket identity differs")
    custom = {721269: "MeteorPack", 720957: "DragonSoulPack"}
    native_custom, hosted_custom = item_rows(client, True, custom), item_rows(server, False, custom)
    for identity, name in custom.items():
        require(native_custom[identity][1].decode("ascii") == name and native_custom[identity][:8] == hosted_custom[identity][:8], "Custom pack identity differs")
    require("Concat(new[]{721090u,721269u})" in source and "Concat(new[]{710212u,720957u})" in source, "Version-four custom pool changed")
    return {"identities": len(names)+len(moon)+len(ticket)+len(custom), "client_sha256": digest(client), "server_sha256": digest(server),
            "limits": "Identity/requirements only; no original pool odds or downstream item-use parity certified."}


if __name__ == "__main__":
    print(json.dumps(audit(), indent=2))

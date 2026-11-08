using System.Buffers.Binary;
using Revora.NativeLogin.Core;
using Revora.Protocol5165;

foreach (string characterName in new[] { "SHEEKO", "SHEEKOVIC", "Hero7x" })
{
    byte[] registration = new byte[104];
    BinaryPrimitives.WriteUInt16LittleEndian(registration, 104);
    BinaryPrimitives.WriteUInt16LittleEndian(registration.AsSpan(2), 1001);
    System.Text.Encoding.ASCII.GetBytes(characterName, registration.AsSpan(20));
    // The exact client allows the 16-byte legacy/reserved field to remain empty.
    BinaryPrimitives.WriteUInt16LittleEndian(registration.AsSpan(52), 1003);
    BinaryPrimitives.WriteUInt16LittleEndian(registration.AsSpan(54), 100);
    BinaryPrimitives.WriteUInt32LittleEndian(registration.AsSpan(56), 0x44332211);
    using CharacterRegistration5165 parsed = GamePacketsAfterAuthentication5165.ParseRegistration(registration);
    Assert(parsed.Account.Length == 0 && parsed.Name == characterName &&
        parsed.SensitiveReserved.AsSpan().IndexOfAnyExcept((byte)0) < 0 && parsed.Mesh == 1003 &&
        parsed.Profession == 100 && parsed.AccountId == 0x44332211);
}

byte[] success = GamePacketsAfterAuthentication5165.EncodeTalk(2100, "ANSWER_OK");
Assert(BinaryPrimitives.ReadUInt16LittleEndian(success) == success.Length &&
    BinaryPrimitives.ReadUInt16LittleEndian(success.AsSpan(2)) == 1004 &&
    BinaryPrimitives.ReadUInt16LittleEndian(success.AsSpan(8)) == 2100);
byte[] logout = GamePacketsAfterAuthentication5165.EncodeTalk(2000, "@logout");
Assert(GamePacketsAfterAuthentication5165.ParseClientTalkText(logout) == "@logout");
byte[] genuineClientTalk = new byte[logout.Length + 3];
logout.CopyTo(genuineClientTalk, 0);
BinaryPrimitives.WriteUInt16LittleEndian(genuineClientTalk, checked((ushort)genuineClientTalk.Length));
Assert(GamePacketsAfterAuthentication5165.ParseClientTalkText(genuineClientTalk) == "@logout");
byte[] nonzeroTalkTail = genuineClientTalk.ToArray();
nonzeroTalkTail[^1] = 1;
AssertThrows(() => GamePacketsAfterAuthentication5165.ParseClientTalkText(nonzeroTalkTail));
byte[] oversizedTalkTail = new byte[logout.Length + 4];
logout.CopyTo(oversizedTalkTail, 0);
BinaryPrimitives.WriteUInt16LittleEndian(oversizedTalkTail, checked((ushort)oversizedTalkTail.Length));
AssertThrows(() => GamePacketsAfterAuthentication5165.ParseClientTalkText(oversizedTalkTail));
byte[] malformedTalk = logout.ToArray();
malformedTalk[24] = 3;
AssertThrows(() => GamePacketsAfterAuthentication5165.ParseClientTalkText(malformedTalk));
byte[] user = GamePacketsAfterAuthentication5165.EncodeUserInfo(1000000, "SHEEKO", 1003, 10);
UserInformation5165 decodedUser = GamePacketsAfterAuthentication5165.ParseUserInfoAsExactClient(user);
byte[] exactClientDerivedUser = new byte[100];
BinaryPrimitives.WriteUInt16LittleEndian(exactClientDerivedUser, 100);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientDerivedUser.AsSpan(2), 1006);
BinaryPrimitives.WriteUInt32LittleEndian(exactClientDerivedUser.AsSpan(4), 1000000);
BinaryPrimitives.WriteUInt32LittleEndian(exactClientDerivedUser.AsSpan(8), 1003);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientDerivedUser.AsSpan(12), 410);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientDerivedUser.AsSpan(50), 5);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientDerivedUser.AsSpan(52), 2);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientDerivedUser.AsSpan(54), 3);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientDerivedUser.AsSpan(60), 100);
exactClientDerivedUser[66] = 1;
exactClientDerivedUser[67] = 10;
exactClientDerivedUser[70] = 1;
exactClientDerivedUser[87] = 2;
exactClientDerivedUser[88] = 6;
"SHEEKO"u8.CopyTo(exactClientDerivedUser.AsSpan(89));
exactClientDerivedUser[95] = 4;
"None"u8.CopyTo(exactClientDerivedUser.AsSpan(96));
Assert(user.Length == 100 && BinaryPrimitives.ReadUInt16LittleEndian(user) == user.Length &&
    BinaryPrimitives.ReadUInt16LittleEndian(user.AsSpan(2)) == 1006 &&
    user[GamePacketsAfterAuthentication5165.UserInfoStringPackOffset] == 2 &&
    decodedUser.Id == 1000000 && decodedUser.Name == "SHEEKO" && decodedUser.Spouse == "None" &&
    user.AsSpan().SequenceEqual(exactClientDerivedUser));
byte[] deadUser = GamePacketsAfterAuthentication5165.EncodeUserInfo(1000001, "DEADONE", 1003, 10,
    1, 0, 0, 0, 5, 2, 3, 0, 0, 0, 0, 0);
Assert(GamePacketsAfterAuthentication5165.ParseUserInfoAsExactClient(deadUser).HealthPoints == 0);
byte[] rejectedLegacyShortUserInfo = new byte[80];
BinaryPrimitives.WriteUInt16LittleEndian(rejectedLegacyShortUserInfo, 80);
BinaryPrimitives.WriteUInt16LittleEndian(rejectedLegacyShortUserInfo.AsSpan(2), 1006);
AssertThrows(() => GamePacketsAfterAuthentication5165.ParseUserInfoAsExactClient(rejectedLegacyShortUserInfo));
byte[] enter = GamePacketsAfterAuthentication5165.EncodeEnterMap(1000000, 1002, 430, 378, 0, 0x12345678);
GameAction5165 decodedAction = GamePacketsAfterAuthentication5165.ParseAction(enter);
Assert(enter.Length == 32 && BinaryPrimitives.ReadUInt16LittleEndian(enter) == 32 &&
    BinaryPrimitives.ReadUInt16LittleEndian(enter.AsSpan(2)) == 10010 &&
    decodedAction == new GameAction5165(1000000, 1002, 0x12345678, 74, 0, 430, 378));
byte[] rejectedOlderAction = new byte[24];
BinaryPrimitives.WriteUInt16LittleEndian(rejectedOlderAction, 24);
BinaryPrimitives.WriteUInt16LittleEndian(rejectedOlderAction.AsSpan(2), 1010);
AssertThrows(() => GamePacketsAfterAuthentication5165.ParseAction(rejectedOlderAction));
byte[] map = GamePacketsAfterAuthentication5165.EncodeMapInfo(1002);
Assert(BinaryPrimitives.ReadUInt16LittleEndian(map.AsSpan(2)) == 1110);

byte[] movement = GamePacketsAfterAuthentication5165.EncodeMovement(14, 1000000, 1, 0x55667788);
GameMovement5165 decodedMovement = GamePacketsAfterAuthentication5165.ParseMovement(movement);
Assert(decodedMovement == new GameMovement5165(14, 1000000, 1, 0x55667788));
byte[] removed = GamePacketsAfterAuthentication5165.EncodeRemoveEntity(1000000);
GameAction5165 decodedRemoval = GamePacketsAfterAuthentication5165.ParseAction(removed);
Assert(decodedRemoval.Id == 1000000 && decodedRemoval.Action == 135 && decodedRemoval.Data == 0 &&
    decodedRemoval.X == 0 && decodedRemoval.Y == 0);
movement[5] = 1;
AssertThrows(() => GamePacketsAfterAuthentication5165.ParseMovement(movement));

byte[] jump = GamePacketsAfterAuthentication5165.EncodeJump(1000000, 440, 381, 431, 378, 6, 0x11223344);
GameAction5165 decodedJump = GamePacketsAfterAuthentication5165.ParseAction(jump);
Assert(decodedJump.Action == 137 && decodedJump.Id == 1000000 &&
    (decodedJump.Data & 0xffff) == 440 && (decodedJump.Data >> 16) == 381 &&
    decodedJump.X == 431 && decodedJump.Y == 378 &&
    BinaryPrimitives.ReadUInt32LittleEndian(jump.AsSpan(24)) == 0 &&
    BinaryPrimitives.ReadUInt32LittleEndian(jump.AsSpan(28)) == uint.MaxValue);
// Independently transcribed from exact-client constructor 0x54da8c/caller
// 0x517202. Coordinates are sanitized fixture values; the genuine failed
// run intentionally retained no raw target coordinate.
byte[] exactClientJump = new byte[32];
BinaryPrimitives.WriteUInt16LittleEndian(exactClientJump, 32);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientJump.AsSpan(2), 10010);
BinaryPrimitives.WriteUInt32LittleEndian(exactClientJump.AsSpan(4), 1000000);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientJump.AsSpan(8), 440);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientJump.AsSpan(10), 381);
BinaryPrimitives.WriteUInt32LittleEndian(exactClientJump.AsSpan(12), 0x11223344);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientJump.AsSpan(16), 137);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientJump.AsSpan(18), 6);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientJump.AsSpan(20), 431);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientJump.AsSpan(22), 378);
BinaryPrimitives.WriteUInt32LittleEndian(exactClientJump.AsSpan(28), uint.MaxValue);
Assert(jump.AsSpan().SequenceEqual(exactClientJump));
byte[] syntheticWrongTail = jump.ToArray();
BinaryPrimitives.WriteUInt32LittleEndian(syntheticWrongTail.AsSpan(28), 0);
AssertThrows(() => GamePacketsAfterAuthentication5165.ParseAction(syntheticWrongTail));

// Independently transcribed from exact-client constructor 0x54da8c and its
// portal caller at 0x518082. The caller supplies source coordinates in Data,
// destination coordinates in X/Y, a zero word at 24, and -1 at 28.
byte[] exactClientPortal = new byte[32];
BinaryPrimitives.WriteUInt16LittleEndian(exactClientPortal, 32);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientPortal.AsSpan(2), 10010);
BinaryPrimitives.WriteUInt32LittleEndian(exactClientPortal.AsSpan(4), 1000000);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientPortal.AsSpan(8), 401);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientPortal.AsSpan(10), 387);
BinaryPrimitives.WriteUInt32LittleEndian(exactClientPortal.AsSpan(12), 0x22334455);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientPortal.AsSpan(16), 85);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientPortal.AsSpan(18), 4);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientPortal.AsSpan(20), 401);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientPortal.AsSpan(22), 387);
BinaryPrimitives.WriteUInt32LittleEndian(exactClientPortal.AsSpan(28), uint.MaxValue);
GameAction5165 decodedPortal = GamePacketsAfterAuthentication5165.ParseAction(exactClientPortal);
Assert(decodedPortal == new GameAction5165(1000000,
    (uint)401 | ((uint)387 << 16), 0x22334455, 85, 4, 401, 387));
byte[] portalWithGenericTail = exactClientPortal.ToArray();
BinaryPrimitives.WriteUInt32LittleEndian(portalWithGenericTail.AsSpan(28), 0);
AssertThrows(() => GamePacketsAfterAuthentication5165.ParseAction(portalWithGenericTail));
byte[] correction = GamePacketsAfterAuthentication5165.EncodePositionCorrection(1000000, 431, 378, 0x11223344);
GameAction5165 decodedCorrection = GamePacketsAfterAuthentication5165.ParseAction(correction);
Assert(decodedCorrection.Action == 108 && decodedCorrection.X == 431 && decodedCorrection.Y == 378);

byte[] spawn = GamePacketsAfterAuthentication5165.EncodePlayerSpawn(1000000, "SHEEKOVIC", 1003, 410,
    431, 378, 6, 100, 130,
    new(Status: 0x0102030405060708, Headwear: 111309, Garment: 181305, Armor: 130309,
        LeftHand: 601339, RightHand: 601339, Steed: 300000, ArmorColor: 3,
        LeftHandColor: 4, HeadwearColor: 5, Rebirth: 2, EntityType: 0));
GamePlayerSpawn5165 decodedSpawn = GamePacketsAfterAuthentication5165.ParsePlayerSpawnAsExactClient(spawn);
Assert(decodedSpawn.Id == 1000000 && decodedSpawn.Name == "SHEEKOVIC" && decodedSpawn.Mesh == 1003 &&
    decodedSpawn.X == 431 && decodedSpawn.Y == 378 && decodedSpawn.Level == 130 &&
    decodedSpawn.Status == 0x0102030405060708 && decodedSpawn.Headwear == 111309 &&
    decodedSpawn.Garment == 181305 && decodedSpawn.Armor == 130309 &&
    decodedSpawn.LeftHand == 601339 && decodedSpawn.RightHand == 601339 && decodedSpawn.Steed == 300000 &&
    decodedSpawn.ArmorColor == 3 && decodedSpawn.LeftHandColor == 4 && decodedSpawn.HeadwearColor == 5);
Assert(GamePacketsAfterAuthentication5165.ToPlayerPresentationUid(2) == 1000002 &&
    GamePacketsAfterAuthentication5165.ToPlayerPresentationUid(1000002) == 1000002);
AssertThrows(() => GamePacketsAfterAuthentication5165.ToPlayerPresentationUid(0));
Assert(BinaryPrimitives.ReadUInt32LittleEndian(spawn.AsSpan(24)) == 111309 &&
    BinaryPrimitives.ReadUInt32LittleEndian(spawn.AsSpan(28)) == 181305 &&
    BinaryPrimitives.ReadUInt32LittleEndian(spawn.AsSpan(32)) == 130309 &&
    BinaryPrimitives.ReadUInt32LittleEndian(spawn.AsSpan(36)) == 601339 &&
    BinaryPrimitives.ReadUInt32LittleEndian(spawn.AsSpan(40)) == 601339 &&
    BinaryPrimitives.ReadUInt16LittleEndian(spawn.AsSpan(92)) == 3 &&
    BinaryPrimitives.ReadUInt16LittleEndian(spawn.AsSpan(94)) == 4 &&
    BinaryPrimitives.ReadUInt16LittleEndian(spawn.AsSpan(96)) == 5);

byte[] fly = GamePacketsAfterAuthentication5165.EncodeFlyMap(1000000, 1004, 51, 70, 0, 0x01020304);
GameAction5165 decodedFly = GamePacketsAfterAuthentication5165.ParseAction(fly);
Assert(decodedFly.Action == 86 && decodedFly.Data == 1004 && decodedFly.X == 51 && decodedFly.Y == 70);
byte[] npc = GamePacketsAfterAuthentication5165.EncodeNpcSpawn(10001, 33, 65, 166, 2);
GameNpcSpawn5165 decodedNpc = GamePacketsAfterAuthentication5165.ParseNpcSpawnAsExactClient(npc);
Assert(decodedNpc == new GameNpcSpawn5165(10001, 33, 65, 166, 2, 1));
byte[] activate = new byte[16];
BinaryPrimitives.WriteUInt16LittleEndian(activate, 16);
BinaryPrimitives.WriteUInt16LittleEndian(activate.AsSpan(2), 2031);
BinaryPrimitives.WriteUInt32LittleEndian(activate.AsSpan(4), 10001);
Assert(GamePacketsAfterAuthentication5165.ParseNpcInteraction(activate) == new NpcInteraction5165(10001, 0, 0, 0));
byte[] dialogText = GamePacketsAfterAuthentication5165.EncodeNpcDialogText("Welcome to Revora.");
Assert(BinaryPrimitives.ReadUInt16LittleEndian(dialogText) == dialogText.Length &&
    BinaryPrimitives.ReadUInt16LittleEndian(dialogText.AsSpan(2)) == 2032 && dialogText[10] == byte.MaxValue &&
    dialogText[11] == 1 && dialogText[12] == 1 && dialogText[13] == 18 &&
    dialogText.AsSpan(^3).IndexOfAnyExcept((byte)0) < 0);
byte[] dialogAnswer = new byte[16];
BinaryPrimitives.WriteUInt16LittleEndian(dialogAnswer, 16);
BinaryPrimitives.WriteUInt16LittleEndian(dialogAnswer.AsSpan(2), 2032);
dialogAnswer[10] = 1;
dialogAnswer[11] = 101;
Assert(GamePacketsAfterAuthentication5165.ParseNpcDialogAnswer(dialogAnswer).Option == 1);
dialogAnswer[15] = 1;
AssertThrows(() => GamePacketsAfterAuthentication5165.ParseNpcDialogAnswer(dialogAnswer));
byte[] exactClientDialogClose = new byte[16];
BinaryPrimitives.WriteUInt16LittleEndian(exactClientDialogClose, 16);
BinaryPrimitives.WriteUInt16LittleEndian(exactClientDialogClose.AsSpan(2), 2032);
exactClientDialogClose[11] = 102;
NpcDialogAnswer5165 decodedDialogClose =
    GamePacketsAfterAuthentication5165.ParseNpcDialogAnswer(exactClientDialogClose);
Assert(decodedDialogClose == new NpcDialogAnswer5165(0, 102, string.Empty));
exactClientDialogClose[15] = 1;
AssertThrows(() => GamePacketsAfterAuthentication5165.ParseNpcDialogAnswer(exactClientDialogClose));
byte[] rejectedFifteenByteClose = exactClientDialogClose[..15];
BinaryPrimitives.WriteUInt16LittleEndian(rejectedFifteenByteClose, 15);
AssertThrows(() => GamePacketsAfterAuthentication5165.ParseNpcDialogAnswer(rejectedFifteenByteClose));

// Exact-client 32/1022 field positions and reversible transforms. The fixture
// constructor below is independent of the production parser.
const uint ninjaUid = 1_000_002;
byte[] ninjaAttack = ExactClientSkillAttack(ninjaUid, 450_001, 352, 356, 6001, 0xc921, 0x12345678);
SkillAttack5165 parsedNinjaAttack = NinjaPackets5165.ParseSkillAttack(ninjaAttack, ninjaUid);
Assert(parsedNinjaAttack == new SkillAttack5165(ninjaUid, 450_001, 352, 356, 6001, 0, 0x12345678));
AssertThrows(() => NinjaPackets5165.ParseSkillAttack(ninjaAttack, ninjaUid + 1));
byte[] wrongNinjaAction = ninjaAttack.ToArray();
BinaryPrimitives.WriteUInt32LittleEndian(wrongNinjaAction.AsSpan(20), 2);
AssertThrows(() => NinjaPackets5165.ParseSkillAttack(wrongNinjaAction, ninjaUid));
byte[] nonzeroNinjaTail = ninjaAttack.ToArray();
nonzeroNinjaTail[31] = 1;
AssertThrows(() => NinjaPackets5165.ParseSkillAttack(nonzeroNinjaTail, ninjaUid));

byte[] normalAttack = new byte[32];
BinaryPrimitives.WriteUInt16LittleEndian(normalAttack, 32);
BinaryPrimitives.WriteUInt16LittleEndian(normalAttack.AsSpan(2), 1022);
BinaryPrimitives.WriteUInt32LittleEndian(normalAttack.AsSpan(4), 0x10203040);
BinaryPrimitives.WriteUInt32LittleEndian(normalAttack.AsSpan(8), ninjaUid);
BinaryPrimitives.WriteUInt32LittleEndian(normalAttack.AsSpan(12), 4_000_001);
BinaryPrimitives.WriteUInt16LittleEndian(normalAttack.AsSpan(16), 352);
BinaryPrimitives.WriteUInt16LittleEndian(normalAttack.AsSpan(18), 356);
BinaryPrimitives.WriteUInt32LittleEndian(normalAttack.AsSpan(20), 2);
NormalAttack5165 parsedNormalAttack = NinjaPackets5165.ParseNormalAttack(normalAttack, ninjaUid);
Assert(parsedNormalAttack == new NormalAttack5165(ninjaUid, 4_000_001, 352, 356, 2, 0x10203040));
byte[] normalResult = NinjaPackets5165.EncodeNormalAttackResult(
    ninjaUid, 4_000_001, 352, 356, 2, 123, 0x10203040);
Assert(BinaryPrimitives.ReadUInt32LittleEndian(normalResult.AsSpan(24)) == 123 &&
    normalResult.AsSpan(28, 4).IndexOfAnyExcept((byte)0) < 0);
byte[] normalForeignSender = normalAttack.ToArray();
BinaryPrimitives.WriteUInt32LittleEndian(normalForeignSender.AsSpan(8), ninjaUid + 1);
AssertThrows(() => NinjaPackets5165.ParseNormalAttack(normalForeignSender, ninjaUid));
byte[] normalNonzeroReserved = normalAttack.ToArray();
normalNonzeroReserved[31] = 1;
AssertThrows(() => NinjaPackets5165.ParseNormalAttack(normalNonzeroReserved, ninjaUid));
byte[] monsterSpawn = CombatPackets5165.EncodeMonsterSpawn(104, 450_001, 33, 1,
    454, 455, 0, 100, "Pheasant");
MonsterSpawn5165 parsedMonster = CombatPackets5165.ParseMonsterSpawnAsExactClient(monsterSpawn);
Assert(parsedMonster == new MonsterSpawn5165(104, 450_001, 33, 1, 454, 455, 0, 100, "Pheasant"));
Assert(BinaryPrimitives.ReadUInt16LittleEndian(monsterSpawn.AsSpan(52)) == 33 &&
    BinaryPrimitives.ReadUInt16LittleEndian(monsterSpawn.AsSpan(54)) == 1 && monsterSpawn[136] == 1);
AssertThrows(() => CombatPackets5165.EncodeMonsterSpawn(104, 400_000_001, 33, 1,
    454, 455, 0, 100, "Pheasant"));
byte[] proficiency = CombatPackets5165.EncodeProficiency(601, 7, 123_456);
Assert(proficiency.Length == 16 && BinaryPrimitives.ReadUInt16LittleEndian(proficiency) == 16 &&
    BinaryPrimitives.ReadUInt16LittleEndian(proficiency.AsSpan(2)) == 1025 &&
    BinaryPrimitives.ReadUInt32LittleEndian(proficiency.AsSpan(4)) == 601 &&
    BinaryPrimitives.ReadUInt16LittleEndian(proficiency.AsSpan(8)) == 7 &&
    BinaryPrimitives.ReadUInt16LittleEndian(proficiency.AsSpan(10)) == 0 &&
    BinaryPrimitives.ReadUInt32LittleEndian(proficiency.AsSpan(12)) == 123_456);
byte[] groundSpawn = GroundItemPackets5165.Encode(300_000_001, 150005, 454, 455,
    GroundItemPackets5165.SpawnAction);
Assert(GroundItemPackets5165.Parse(groundSpawn) == new GroundItemAction5165(
    300_000_001, 150005, 454, 455, 3, 1));
byte[] groundRemove = GroundItemPackets5165.Encode(300_000_001, 150005, 454, 455,
    GroundItemPackets5165.RemoveAction);
Assert(GroundItemPackets5165.Parse(groundRemove).Action == 2);
byte[] pickup = new byte[20];
BinaryPrimitives.WriteUInt16LittleEndian(pickup, 20);
BinaryPrimitives.WriteUInt16LittleEndian(pickup.AsSpan(2), 1101);
BinaryPrimitives.WriteUInt32LittleEndian(pickup.AsSpan(4), 300_000_001);
BinaryPrimitives.WriteUInt32LittleEndian(pickup.AsSpan(8), 0x7914A09A);
BinaryPrimitives.WriteUInt16LittleEndian(pickup.AsSpan(12), 439);
BinaryPrimitives.WriteUInt16LittleEndian(pickup.AsSpan(14), 432);
pickup[18] = 3;
Assert(GroundItemPackets5165.ParsePickupUid(pickup) == 300_000_001);
pickup[16] = 3;
AssertThrows(() => GroundItemPackets5165.ParsePickupUid(pickup));
byte[] killed = NinjaPackets5165.EncodeKillResult(ninjaUid, 4_000_001, 352, 356, 0x10203040);
Assert(BinaryPrimitives.ReadUInt32LittleEndian(killed.AsSpan(20)) == CombatPackets5165.KillAction &&
    BinaryPrimitives.ReadUInt32LittleEndian(killed.AsSpan(24)) == 1);

byte[] skillUse = NinjaPackets5165.EncodeSkillUse(ninjaUid, 352, 356, 6001, 2,
    [new SkillTarget5165(4_000_001, 123), new SkillTarget5165(4_000_002, 456)]);
SkillUse5165 parsedSkillUse = NinjaPackets5165.ParseSkillUseAsExactClient(skillUse);
Assert(skillUse.Length == 44 && parsedSkillUse.ActorUid == ninjaUid && parsedSkillUse.SkillId == 6001 &&
    parsedSkillUse.Targets.Count == 2 && parsedSkillUse.Targets[1] == new SkillTarget5165(4_000_002, 456));
byte[] nonzeroTargetTail = skillUse.ToArray();
nonzeroTargetTail[28] = 1;
AssertThrows(() => NinjaPackets5165.ParseSkillUseAsExactClient(nonzeroTargetTail));
byte[] skillInfo = NinjaPackets5165.EncodeSkillInformation(1234, 6001, 2);
Assert(NinjaPackets5165.ParseSkillInformation(skillInfo) == new SkillInformation5165(1234, 6001, 2));

Console.WriteLine("Revora.ProtocolTests: exact 5165 character, movement, portal, NPC, Ninja skill, dialog, correction, and spawn fixtures passed.");
return 0;

static byte[] ExactClientSkillAttack(uint sender, uint target, ushort x, ushort y, ushort skill, ushort level, uint timestamp)
{
    var packet = new byte[32];
    BinaryPrimitives.WriteUInt16LittleEndian(packet, 32);
    BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 1022);
    BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), timestamp);
    BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), sender);
    uint targetIntermediate = unchecked((target + 0x746f4ae6u) ^ 0x5f2d2463u ^ sender);
    BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), RotateLeft(targetIntermediate, 13));
    BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(16), unchecked((ushort)(RotateRight16((ushort)(x + 0x22ee), 1) ^ (ushort)sender ^ 0x2ed6)));
    BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(18), unchecked((ushort)(RotateRight16((ushort)(y + 0x8922), 5) ^ (ushort)sender ^ 0xb99b)));
    BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(20), 24);
    BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(24), unchecked((ushort)(RotateRight16((ushort)(skill + 0xeb42), 3) ^ 0x915d ^ (ushort)sender)));
    BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(26), level);
    return packet;
}

static ushort RotateRight16(ushort value, int count) => unchecked((ushort)((value >> count) | (value << (16 - count))));
static uint RotateLeft(uint value, int count) => (value << count) | (value >> (32 - count));

static void Assert(bool condition)
{
    if (!condition) throw new InvalidOperationException("Protocol fixture assertion failed.");
}

static void AssertThrows(Action action)
{
    try { action(); }
    catch (Exception error) when (error is InvalidDataException or ArgumentOutOfRangeException) { return; }
    throw new InvalidOperationException("Protocol fixture did not fail closed.");
}

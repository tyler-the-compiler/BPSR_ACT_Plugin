using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPSR_ACT_Plugin.Core
{
    public static class BPSR_Enums_Constants
    {
        public static readonly string ENT_NAME_IGNORE = "IGNORE";
        public static readonly string ENT_JOB_UNKNOWN = "???";
        public static Dictionary<long, string> LocationMap = new Dictionary<long, string>{
            {7, "Asteria Plains" },
            {8, "Asterleeds" },
            {71, "Duskdye Woods" },
            {72, "Everfall Forest" },
            {73, "Windhowl Canyon" },
            {75, "Skimmer's Lair" },
            {6005, "Goblin Lair (Unstable)" },
            {6007, "Goblin Lair (Normal)" },
            {6008, "Goblin Lair (Hard)" },
            {6009, "Goblin Lair (Master 1)" },
            {1123, "Towering Ruin (Master 1)" },
            {1331, "Dark Mist Fortress (Normal)" },
            {13001, "Clash! Floating Island - Dragon Shackles" }
        };

        public enum LogEventIds
        {
            EVENT_INFO_GENERAL = 0,
            EVENT_ENTER_COMBAT = 1,
            EVENT_EXIT_COMBAT = 2,
            EVENT_BUFF_GAIN = 3,
            EVENT_BUFF_LOST = 4,
            EVENT_DAMAGE = 5,
            EVENT_HEAL = 6,
            EVENT_TARGET_KILL = 7,
            EVENT_PLAYER_DIE = 8,
            EVENT_ZONE_LOAD = 9
        }

        public enum IdentityType
        {
            NONE,
            NPC,
            PLAYER,
            COMPANION,
            ENEMY,
            ENVIRONMENT
        }

        public enum MessageType
        {
            None = 0,
            Call = 1,
            Notify = 2,
            Return = 3,
            Echo = 4,
            FrameUp = 5,
            FrameDown = 6
        };

        public enum NotifyMethod
        {
            SyncNearEntities = 0x00000006,
            SyncContainerData = 0x00000015,
            SyncContainerDirtyData = 0x00000016,
            SyncServerTime = 0x0000002b,
            SyncNearDeltaInfo = 0x0000002d,
            SyncToMeDeltaInfo = 0x0000002e
        };

        public enum AttrType
        {
            AttrName = 0x01,
            AttrId = 0x0a,
            AttrProfessionId = 0xdc,
            AttrFightPoint = 0x272e,
            AttrLevel = 0x2710,
            AttrRankLevel = 0x274c,
            AttrCri = 0x2b66,
            AttrLucky = 0x2b7a,
            AttrHp = 0x2c2e,
            AttrMaxHp = 0x2c38,
            AttrElementFlag = 0x646d6c,
            AttrReductionLevel = 0x64696d,
            AttrReductionId = 0x6f6c65,
            AttrEnergyFlag = 0x543cd3c6
        };

        public enum ProfessionType
        {
            Stormblade = 1,
            FrostMage = 2,
            FireWarrior = 3,
            WindKnight = 4,
            VerdantOracle = 5,
            Marksman_Cannon = 8,
            HeavyGuardian = 9,
            SoulMusician_Scythe = 10,
            Marksman = 11,
            ShieldKnight = 12,
            SoulMusician = 13
        };

        public enum NonProfessionDamageClasses
        {
            Environment = -1,
            Any = 0
        }

        public enum EDamageSource
        {
            EDamageSourceSkill = 0,
            EDamageSourceBullet = 1,
            EDamageSourceBuff = 2,
            EDamageSourceFall = 3,
            EDamageSourceFakeBullet = 4,
            EDamageSourceOther = 100
        };

        public enum EDamageProperty
        {
            General = 0,
            Fire = 1,
            Water = 2,
            Lightning = 3,
            Forest = 4,
            Wind = 5,
            Earth = 6,
            Light = 7,
            Dark = 8,
            Count = 9
        };

        public struct SkillDataDef
        {
            public int DamageClass { get; set; }
            public string SkillName { get; set; }
        }

        public static Dictionary<long, string> MonsterMap = new Dictionary<long, string>
        {
            {107, "Kartgriff" },
            {1100, "Defensive Type 04" },
            {1101, "Defensive Type 06" },
            {1102, "Defensive Type 04" },
            {1103, "Defensive Type 06" },
            {1104, "Sentinel Type 04" },
            {1110, "Kartgriff" },
            {1150, "Kartgriff" },
            {1174, "Kartgriff" },
            {1230, "Kartgriff" },
            {1309, "Kartgriff" },
            {1320, "Sentinel Type 01" },
            {1321, "Sentinel Type 01" },
            {1330, "Sentinel Type 01" },
            {1331, "Defensive Type 04" },
            {1332, "Defensive Type 06" },
            {1338, "Self-destruct Robot 08" },
            {1345, "Elite Defensive Type 06" },
            {1360, "Kartgriff" },
            {10007, "Storm Goblin King" },
            {10010, "Tempest Ogre" },
            {1701, "Denvel" },
            {1702, "Void Denvel" },
            {1703, "Denvel" },
            {1705, "Void Denvel" },
            {11110, "Kartgriff" },
            {40010, "Shuro Barot" },
            {530354, "Void Tempest Ogre" },
            {920012, "Tempest Ogre" },
            {2000131, "Tempest Ogre" },
            {2000137, "Tempest Ogre" },
            {2004109, "Tempest Ogre" },
            {2004126, "Tempest Ogre" },
            {2004131, "Tempest Ogre" },
            {2004152, "Tempest Ogre" }
        };

        public static Dictionary<string, SkillDataDef> SkillKVPair = new Dictionary<string, SkillDataDef>{
            {"873101", new SkillDataDef(){SkillName = "Shockwave", DamageClass = (int)NonProfessionDamageClasses.Environment } },
            {"1006940", new SkillDataDef(){SkillName = "Arcane! Cocoon Tech", DamageClass = (int)NonProfessionDamageClasses.Any } },
            {"1002830", new SkillDataDef(){SkillName = "Arcane! Frostquake", DamageClass = (int)NonProfessionDamageClasses.Any} },
            {"2002440", new SkillDataDef(){SkillName = "Arcane! Thunderfall Grasp", DamageClass = (int)NonProfessionDamageClasses.Any } },
            {"2002840", new SkillDataDef(){SkillName = "Arcane! Swift Vortex", DamageClass = (int)NonProfessionDamageClasses.Any } },
            {"1700440", new SkillDataDef(){SkillName = "Arcane! Furious Hammer", DamageClass = (int)NonProfessionDamageClasses.Any } },
            {"1201", new SkillDataDef(){SkillName = "Raincall Surge 1", DamageClass = (int)ProfessionType.FrostMage } },
            {"1202", new SkillDataDef(){SkillName = "Raincall Surge 2", DamageClass = (int)ProfessionType.FrostMage } },
            {"1203", new SkillDataDef(){SkillName = "Raincall Surge 3", DamageClass = (int)ProfessionType.FrostMage } },
            {"1204", new SkillDataDef(){SkillName = "Raincall Surge 4", DamageClass = (int)ProfessionType.FrostMage } },
            {"1210", new SkillDataDef(){SkillName = "Maelstrom", DamageClass = (int)ProfessionType.FrostMage } },
            {"1211", new SkillDataDef(){SkillName = "Crystal Veil", DamageClass = (int)ProfessionType.FrostMage } },
            {"1239", new SkillDataDef() { SkillName = "Meteor Storm", DamageClass =(int) ProfessionType.FrostMage } },
            {"1240", new SkillDataDef() { SkillName = "Frozen Gale", DamageClass =(int) ProfessionType.FrostMage } },
            {"1241", new SkillDataDef() { SkillName = "Frostbeam", DamageClass =(int) ProfessionType.FrostMage } },
            {"1242", new SkillDataDef() { SkillName = "Frost Lance", DamageClass =(int) ProfessionType.FrostMage } },
            {"1243", new SkillDataDef() { SkillName = "Permafrost", DamageClass =(int) ProfessionType.FrostMage } },
            {"1244", new SkillDataDef() { SkillName = "Blizzard", DamageClass =(int) ProfessionType.FrostMage } },
            {"1245", new SkillDataDef() { SkillName = "Frost Shelter", DamageClass =(int) ProfessionType.FrostMage } },
            {"1246", new SkillDataDef() { SkillName = "Tidepool", DamageClass =(int) ProfessionType.FrostMage } },
            {"1247", new SkillDataDef() { SkillName = "Frost Comet", DamageClass =(int) ProfessionType.FrostMage } },
            {"1248", new SkillDataDef() { SkillName = "Glacier Hymn", DamageClass =(int) ProfessionType.FrostMage } },
            {"1401", new SkillDataDef() { SkillName = "Windborne Grace - Sweep", DamageClass =(int) ProfessionType.WindKnight } },
            {"1402", new SkillDataDef() { SkillName = "Windborne Grace - Charge/Smash", DamageClass =(int) ProfessionType.WindKnight } },
            {"1403", new SkillDataDef() { SkillName = "Windborne Grace - Spin Kick", DamageClass =(int) ProfessionType.WindKnight } },
            {"1404", new SkillDataDef() { SkillName = "Windborne Grace - Slam", DamageClass =(int) ProfessionType.WindKnight } },
            {"1409", new SkillDataDef() { SkillName = "Typhoon Cleave", DamageClass =(int) ProfessionType.WindKnight } },
            {"1411", new SkillDataDef() { SkillName = "Swift Blade", DamageClass =(int) ProfessionType.WindKnight } },
            {"1418", new SkillDataDef() { SkillName = "Gale Thrust", DamageClass =(int) ProfessionType.WindKnight } },
            {"1419", new SkillDataDef() { SkillName = "Skyfall", DamageClass =(int) ProfessionType.WindKnight } },
            {"1420", new SkillDataDef() { SkillName = "Galeform", DamageClass =(int) ProfessionType.WindKnight } },
            {"1421", new SkillDataDef() { SkillName = "Spiral Thrust", DamageClass =(int) ProfessionType.WindKnight } },
            {"1422", new SkillDataDef() { SkillName = "Breach Pursuit", DamageClass =(int) ProfessionType.WindKnight } },
            {"1423", new SkillDataDef() { SkillName = "Aegis Gale", DamageClass =(int) ProfessionType.WindKnight } },
            {"1424", new SkillDataDef() { SkillName = "Instant Edge", DamageClass =(int) ProfessionType.WindKnight } },
            {"1425", new SkillDataDef() { SkillName = "Falcon Toss", DamageClass =(int) ProfessionType.WindKnight } },
            {"1426", new SkillDataDef() { SkillName = "Typhoon Cleave", DamageClass =(int) ProfessionType.WindKnight } },
            {"1430", new SkillDataDef() { SkillName = "Valor Cyclone", DamageClass =(int) ProfessionType.WindKnight } },
            {"1431", new SkillDataDef() { SkillName = "Sharp Impact", DamageClass =(int) ProfessionType.WindKnight } },
            {"1433", new SkillDataDef() { SkillName = "Azure Sever", DamageClass =(int) ProfessionType.WindKnight } },
            {"1434", new SkillDataDef() { SkillName = "Vortex Strike", DamageClass =(int) ProfessionType.WindKnight } },
            {"1435", new SkillDataDef() { SkillName = "Drake Cannon", DamageClass =(int) ProfessionType.WindKnight } },
            {"1501", new SkillDataDef() { SkillName = "Vines' Embrace 1", DamageClass =(int) ProfessionType.VerdantOracle } },
            {"1502", new SkillDataDef() { SkillName = "Vines' Embrace 2", DamageClass =(int) ProfessionType.VerdantOracle } },
            {"1503", new SkillDataDef() { SkillName = "Vines' Embrace 3", DamageClass =(int) ProfessionType.VerdantOracle } },
            {"1504", new SkillDataDef() { SkillName = "Vines' Embrace 4", DamageClass =(int) ProfessionType.VerdantOracle } },
            {"1507", new SkillDataDef() { SkillName = "Life Bloom", DamageClass =(int) ProfessionType.VerdantOracle } },
            {"1509", new SkillDataDef() { SkillName = "Divine Circle Bloom", DamageClass =(int) ProfessionType.VerdantOracle } },
            {"1518", new SkillDataDef() { SkillName = "Wild Bloom", DamageClass =(int) ProfessionType.VerdantOracle } },
            {"1519", new SkillDataDef() { SkillName = "Feral Seed", DamageClass =(int) ProfessionType.VerdantOracle } },
            {"1520", new SkillDataDef() { SkillName = "Infusion", DamageClass =(int) ProfessionType.VerdantOracle } },
            {"1521", new SkillDataDef() { SkillName = "Grove Wish", DamageClass =(int) ProfessionType.VerdantOracle } },
            {"1522", new SkillDataDef() { SkillName = "Nourish", DamageClass =(int) ProfessionType.VerdantOracle } },
            {"1523", new SkillDataDef() { SkillName = "Regen Pulse", DamageClass =(int) ProfessionType.VerdantOracle } },
            {"1524", new SkillDataDef() { SkillName = "Fast Growth", DamageClass =(int) ProfessionType.VerdantOracle } },
            {"1525", new SkillDataDef() { SkillName = "Stag Charge", DamageClass =(int) ProfessionType.VerdantOracle } },
            {"1527", new SkillDataDef() { SkillName = "Bloomheal", DamageClass =(int) ProfessionType.VerdantOracle } },
            {"1528", new SkillDataDef() { SkillName = "Vital Surge", DamageClass =(int) ProfessionType.VerdantOracle } },
            {"1529", new SkillDataDef() { SkillName = "Blossom Charge", DamageClass =(int) ProfessionType.VerdantOracle } },
            {"1531", new SkillDataDef() { SkillName = "Nature Ward", DamageClass =(int) ProfessionType.VerdantOracle } },
            {"1701", new SkillDataDef() { SkillName = "Judgment Cut 1", DamageClass =(int) ProfessionType.Stormblade } },
            {"1702", new SkillDataDef() { SkillName = "Judgment Cut 2", DamageClass =(int) ProfessionType.Stormblade } },
            {"1703", new SkillDataDef() { SkillName = "Judgment Cut 3", DamageClass =(int) ProfessionType.Stormblade } },
            {"1704", new SkillDataDef() { SkillName = "Judgment Cut 4", DamageClass =(int) ProfessionType.Stormblade } },
            {"1705", new SkillDataDef() { SkillName = "Overdrive (Blade)", DamageClass =(int) ProfessionType.Stormblade } },
            {"1713", new SkillDataDef() { SkillName = "Oblivion Combo", DamageClass =(int) ProfessionType.Stormblade } },
            {"1714", new SkillDataDef() { SkillName = "Iaido Slash", DamageClass =(int) ProfessionType.Stormblade } },
            {"1715", new SkillDataDef() { SkillName = "Moonstrike", DamageClass =(int) ProfessionType.Stormblade } },
            {"1716", new SkillDataDef() { SkillName = "Overdrive (Scythe)", DamageClass =(int) ProfessionType.Stormblade } },
            {"1717", new SkillDataDef() { SkillName = "Flash Strike", DamageClass =(int) ProfessionType.Stormblade } },
            {"1718", new SkillDataDef() { SkillName = "Raijin Dash", DamageClass =(int) ProfessionType.Stormblade } },
            {"1719", new SkillDataDef() { SkillName = "Scythe Wheel", DamageClass =(int) ProfessionType.Stormblade } },
            {"44701", new SkillDataDef() { SkillName = "Scythe Wheel (DoT)", DamageClass =(int) ProfessionType.Stormblade } },
            {"1720", new SkillDataDef() { SkillName = "True Sight", DamageClass =(int) ProfessionType.Stormblade } },
            {"1724", new SkillDataDef() { SkillName = "Thundercut", DamageClass =(int) ProfessionType.Stormblade } },
            {"1730", new SkillDataDef() { SkillName = "Volt Surge", DamageClass =(int) ProfessionType.Stormblade } },
            {"1731", new SkillDataDef() { SkillName = "Stormflash", DamageClass =(int) ProfessionType.Stormblade } },
            {"1733", new SkillDataDef() { SkillName = "Storm Scythe", DamageClass =(int) ProfessionType.Stormblade } },
            {"1734", new SkillDataDef() { SkillName = "Thunder Cut", DamageClass =(int) ProfessionType.Stormblade } },
            {"1735", new SkillDataDef() { SkillName = "Dracoflash", DamageClass =(int) ProfessionType.Stormblade } },
            {"1736", new SkillDataDef() { SkillName = "Phantom Slash", DamageClass =(int) ProfessionType.Stormblade } },
            {"1737", new SkillDataDef() { SkillName = "Divine Sickle", DamageClass =(int) ProfessionType.Stormblade } },
            {"1738", new SkillDataDef() { SkillName = "Chaos Breaker", DamageClass =(int) ProfessionType.Stormblade } },
            {"1739", new SkillDataDef() { SkillName = "Piercing Slash", DamageClass =(int) ProfessionType.Stormblade } },
            {"1901", new SkillDataDef() { SkillName = "Halberd's Edge 1", DamageClass =(int) ProfessionType.HeavyGuardian } },
            {"1902", new SkillDataDef() { SkillName = "Halberd's Edge 2", DamageClass =(int) ProfessionType.HeavyGuardian } },
            {"1903", new SkillDataDef() { SkillName = "Halberd's Edge 3", DamageClass =(int) ProfessionType.HeavyGuardian } },
            {"1904", new SkillDataDef() { SkillName = "Halberd's Edge 4", DamageClass =(int) ProfessionType.HeavyGuardian } },
            {"1907", new SkillDataDef() { SkillName = "Tectonic Ring", DamageClass =(int) ProfessionType.HeavyGuardian } },
            {"1922", new SkillDataDef() { SkillName = "Shield Bash", DamageClass =(int) ProfessionType.HeavyGuardian } },
            {"1923", new SkillDataDef() { SkillName = "Sandward", DamageClass =(int) ProfessionType.HeavyGuardian } },
            {"1924", new SkillDataDef() { SkillName = "Star Shatter", DamageClass =(int) ProfessionType.HeavyGuardian } },
            {"1925", new SkillDataDef() { SkillName = "Rage Burst", DamageClass =(int) ProfessionType.HeavyGuardian } },
            {"1926", new SkillDataDef() { SkillName = "Sandgrip", DamageClass =(int) ProfessionType.HeavyGuardian } },
            {"1927", new SkillDataDef() { SkillName = "Sandshroud", DamageClass =(int) ProfessionType.HeavyGuardian } },
            {"1930", new SkillDataDef() { SkillName = "Countercrush", DamageClass =(int) ProfessionType.HeavyGuardian } },
            {"1936", new SkillDataDef() { SkillName = "Stoneform", DamageClass =(int) ProfessionType.HeavyGuardian } },
            {"1937", new SkillDataDef() { SkillName = "Granite Fury", DamageClass =(int) ProfessionType.HeavyGuardian } },
            {"1938", new SkillDataDef() { SkillName = "Brave Bastion", DamageClass =(int) ProfessionType.HeavyGuardian } },
            {"1941", new SkillDataDef() { SkillName = "Starfall", DamageClass =(int) ProfessionType.HeavyGuardian } },
            {"1942", new SkillDataDef() { SkillName = "Rupture", DamageClass =(int) ProfessionType.HeavyGuardian } },
            {"1943", new SkillDataDef() { SkillName = "Stone Fist", DamageClass =(int) ProfessionType.HeavyGuardian } },
            {"2201", new SkillDataDef() { SkillName = "Bullseye", DamageClass =(int) ProfessionType.Marksman } },
            {"2209", new SkillDataDef() { SkillName = "Luminary Bolt", DamageClass =(int) ProfessionType.Marksman } },
            {"2220", new SkillDataDef() { SkillName = "Storm Arrow", DamageClass =(int) ProfessionType.Marksman } },
            {"2222", new SkillDataDef() { SkillName = "Double Arrow", DamageClass =(int) ProfessionType.Marksman } },
            {"2230", new SkillDataDef() { SkillName = "Torrent Volley", DamageClass =(int) ProfessionType.Marksman } },
            {"2231", new SkillDataDef() { SkillName = "Focus", DamageClass =(int) ProfessionType.Marksman } },
            {"2232", new SkillDataDef() { SkillName = "Arrow Rain", DamageClass =(int) ProfessionType.Marksman } },
            {"2233", new SkillDataDef() { SkillName = "Powerdraw", DamageClass =(int) ProfessionType.Marksman } },
            {"2234", new SkillDataDef() { SkillName = "Radiance Barrage", DamageClass =(int) ProfessionType.Marksman } },
            {"2235", new SkillDataDef() { SkillName = "Deter Shot", DamageClass =(int) ProfessionType.Marksman } },
            {"2237", new SkillDataDef() { SkillName = "Wildcall", DamageClass =(int) ProfessionType.Marksman } },
            {"2238", new SkillDataDef() { SkillName = "Blast Shot", DamageClass =(int) ProfessionType.Marksman } },
            {"2240", new SkillDataDef() { SkillName = "Lumi Torrent", DamageClass =(int) ProfessionType.Marksman } },
            {"2301", new SkillDataDef() { SkillName = "Resonant Strings 1", DamageClass =(int) ProfessionType.SoulMusician } },
            {"2302", new SkillDataDef() { SkillName = "Resonant Strings 2", DamageClass =(int) ProfessionType.SoulMusician } },
            {"2303", new SkillDataDef() { SkillName = "Resonant Strings 3", DamageClass =(int) ProfessionType.SoulMusician } },
            {"2304", new SkillDataDef() { SkillName = "Resonant Strings 4", DamageClass =(int) ProfessionType.SoulMusician } },
            {"2306", new SkillDataDef() { SkillName = "Amplified Beat", DamageClass =(int) ProfessionType.SoulMusician} },
            {"2307", new SkillDataDef() { SkillName = "Healing Beat", DamageClass =(int) ProfessionType.SoulMusician } },
            {"2308", new SkillDataDef() { SkillName = "Harmonic Anthem", DamageClass =(int) ProfessionType.SoulMusician } },
            {"2309", new SkillDataDef() { SkillName = "Rhapsody of Flame", DamageClass =(int) ProfessionType.SoulMusician } },
            {"2310", new SkillDataDef() { SkillName = "Heroic Melody", DamageClass =(int) ProfessionType.SoulMusician } },
            {"2311", new SkillDataDef() { SkillName = "Healing Melody", DamageClass =(int) ProfessionType.SoulMusician } },
            {"2312", new SkillDataDef() { SkillName = "Fivefold Crescendo", DamageClass =(int) ProfessionType.SoulMusician } },
            {"2313", new SkillDataDef() { SkillName = "Passion Burst", DamageClass =(int) ProfessionType.SoulMusician } },
            {"2314", new SkillDataDef() { SkillName = "Rock the Stage/Infinite Rhapsody", DamageClass =(int) ProfessionType.SoulMusician } },
            {"2315", new SkillDataDef() { SkillName = "Encore", DamageClass =(int) ProfessionType.SoulMusician } },
            {"2316", new SkillDataDef() { SkillName = "Center Stage", DamageClass =(int) ProfessionType.SoulMusician } },
            {"2317", new SkillDataDef() { SkillName = "Fierce Strike", DamageClass =(int) ProfessionType.SoulMusician } },
            {"2321", new SkillDataDef() { SkillName = "String Strike", DamageClass =(int) ProfessionType.SoulMusician } },
            {"2332", new SkillDataDef() { SkillName = "Passion Fury", DamageClass =(int) ProfessionType.SoulMusician } },
            {"2335", new SkillDataDef() { SkillName = "Infinite Rhapsody", DamageClass =(int) ProfessionType.SoulMusician } },
            {"2336", new SkillDataDef() { SkillName = "Concert Circuit", DamageClass =(int) ProfessionType.SoulMusician } },
            {"2401", new SkillDataDef() { SkillName = "Blade of Justice 1", DamageClass =(int) ProfessionType.ShieldKnight } },
            {"2402", new SkillDataDef() { SkillName = "Blade of Justice 2", DamageClass =(int) ProfessionType.ShieldKnight } },
            {"2403", new SkillDataDef() { SkillName = "Blade of Justice 3", DamageClass =(int) ProfessionType.ShieldKnight } },
            {"2404", new SkillDataDef() { SkillName = "Blade of Justice 4", DamageClass =(int) ProfessionType.ShieldKnight } },
            {"2405", new SkillDataDef() { SkillName = "Valor Bash", DamageClass =(int) ProfessionType.ShieldKnight } },
            {"2406", new SkillDataDef() { SkillName = "Vanguard Strike", DamageClass =(int) ProfessionType.ShieldKnight } },
            {"2407", new SkillDataDef() { SkillName = "Radiant Infusion", DamageClass =(int) ProfessionType.ShieldKnight } },
            {"2408", new SkillDataDef() { SkillName = "Shield Toss", DamageClass =(int) ProfessionType.ShieldKnight } },
            {"2409", new SkillDataDef() { SkillName = "Divine Circle", DamageClass =(int) ProfessionType.ShieldKnight } },
            {"2410", new SkillDataDef() { SkillName = "Judgment", DamageClass =(int) ProfessionType.ShieldKnight } },
            {"2412", new SkillDataDef() { SkillName = "Reckoning", DamageClass =(int) ProfessionType.ShieldKnight } },
            {"2413", new SkillDataDef() { SkillName = "Inferno Reckon", DamageClass =(int) ProfessionType.ShieldKnight } },
            {"2415", new SkillDataDef() { SkillName = "Aegis Ward", DamageClass =(int) ProfessionType.ShieldKnight } },
            {"2416", new SkillDataDef() { SkillName = "Condemn", DamageClass =(int) ProfessionType.ShieldKnight } },
            {"2417", new SkillDataDef() { SkillName = "Enhanced Condemn", DamageClass =(int) ProfessionType.ShieldKnight } },
            {"2419", new SkillDataDef() { SkillName = "Zeal Crusade", DamageClass =(int) ProfessionType.ShieldKnight } },
            {"2420", new SkillDataDef() { SkillName = "Radiance", DamageClass =(int) ProfessionType.ShieldKnight } },
            {"2421", new SkillDataDef() { SkillName = "Sacred Blade", DamageClass =(int) ProfessionType.ShieldKnight } },
            {"179908", new SkillDataDef() { SkillName = "Blade Intent Thunder Strike", DamageClass =(int) ProfessionType.Stormblade } }
        };
    }
}

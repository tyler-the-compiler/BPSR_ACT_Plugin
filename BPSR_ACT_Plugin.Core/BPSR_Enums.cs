using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPSR_ACT_Plugin.Core
{
    public static class BPSR_Enums
    {
        public static Dictionary<long, string> LocationMap = new Dictionary<long, string>{
            {7, "Asteria Plains" },
            {8, "Asterleeds" },
            {71, "Duskdye Woods" },
            {72, "Everfall Forest" },
            {73, "Windhowl Canyon" },
            {75, "Skimmer's Lair" },
            {6007, "Goblin Lair (Normal)" },
            {6008, "Goblin Lair (Hard)" },
            {6009, "Goblin Lair (Master 1)" },
            {1331, "Dark Mist Fortress (Normal)" },
            {13001, "Clash! Floating Island - Dragon Shackles" }
        };
        public static Dictionary<long, string> MonsterMap = new Dictionary<long, string>
        {
            {10007, "Storm Goblin King" },
            {10010, "Tempest Ogre" },
            {6788743168, "Denvel" }
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
            COMPANION
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

        public static Dictionary<string, string> SkillKVPair = new Dictionary<string, string>{
            {"1006940", "Arcane! Cocoon Tech" },
            {"1002830", "Arcane! Frostquake" },
            {"2002440", "Arcane! Thunderfall Grasp" },
            {"2002840", "Arcane! Swift Vortex" },
            {"1700440", "Arcane! Furious Hammer" },
            {"1201", "Raincall Surge 1" },
            {"1202", "Raincall Surge 2" },
            {"1203", "Raincall Surge 3" },
            {"1204", "Raincall Surge 4" },
            {"1210", "Maelstrom" },
            {"1211", "Crystal Veil" },
            {"1239", "Meteor Storm" },
            {"1240", "Frozen Gale" },
            {"1241", "Frostbeam" },
            {"1242", "Frost Lance" },
            {"1243", "Permafrost" },
            {"1244", "Blizzard" },
            {"1245", "Frost Shelter" },
            {"1246", "Tidepool" },
            {"1247", "Frost Comet" },
            {"1248", "Glacier Hymn" },
            {"1401", "Windborne Grace - Sweep" },
            {"1402", "Windborne Grace - Charge/Smash" },
            {"1403", "Windborne Grace - Spin Kick" },
            {"1404", "Windborne Grace - Slam" },
            {"1409", "Typhoon Cleave" },
            {"1411", "Swift Blade" },
            {"1418", "Gale Thrust" },
            {"1419", "Skyfall" },
            {"1420", "Galeform" },
            {"1421", "Spiral Thrust" },
            {"1422", "Breach Pursuit" },
            {"1423", "Aegis Gale" },
            {"1424", "Instant Edge" },
            {"1425", "Falcon Toss" },
            {"1426", "Typhoon Cleave" },
            {"1430", "Valor Cyclone" },
            {"1431", "Sharp Impact" },
            {"1433", "Azure Sever" },
            {"1434", "Vortex Strike" },
            {"1435", "Drake Cannon" },
            {"1501", "Vines' Embrace 1" },
            {"1502", "Vines' Embrace 2" },
            {"1503", "Vines' Embrace 3" },
            {"1504", "Vines' Embrace 4" },
            {"1507", "Life Bloom" },
            {"1509", "Divine Circle Bloom" },
            {"1518", "Wild Bloom" },
            {"1519", "Feral Seed" },
            {"1520", "Infusion" },
            {"1521", "Grove Wish" },
            {"1522", "Nourish" },
            {"1523", "Regen Pulse" },
            {"1524", "Fast Growth" },
            {"1525", "Stag Charge" },
            {"1527", "Bloomheal" },
            {"1528", "Vital Surge" },
            {"1529", "Blossom Charge" },
            {"1531", "Nature Ward" },
            {"1701", "Judgment Cut 1" },
            {"1702", "Judgment Cut 2" },
            {"1703", "Judgment Cut 3" },
            {"1704", "Judgment Cut 4" },
            {"1705", "Overdrive (Blade)" },
            {"1713", "Oblivion Combo" },
            {"1714", "Iaido Slash" },
            {"1715", "Moonstrike" },
            {"1716", "Overdrive (Scythe)" },
            {"1717", "Flash Strike" },
            {"1718", "Raijin Dash" },
            {"1719", "Scythe Wheel" },
            {"44701", "Scythe Wheel (DoT)" },
            {"1720", "True Sight" },
            {"1724", "Thundercut" },
            {"1730", "Volt Surge" },
            {"1731", "Stormflash" },
            {"1733", "Storm Scythe" },
            {"1734", "Thunder Cut" },
            {"1735", "Dracoflash" },
            {"1736", "Phantom Slash" },
            {"1737", "Divine Sickle" },
            {"1738", "Chaos Breaker" },
            {"1739", "Piercing Slash" },
            {"1901", "Halberd's Edge 1" },
            {"1902", "Halberd's Edge 2" },
            {"1903", "Halberd's Edge 3" },
            {"1904", "Halberd's Edge 4" },
            {"1907", "Tectonic Ring" },
            {"1922", "Shield Bash" },
            {"1923", "Sandward" },
            {"1924", "Star Shatter" },
            {"1925", "Rage Burst" },
            {"1926", "Sandgrip" },
            {"1927", "Sandshroud" },
            {"1930", "Countercrush" },
            {"1936", "Stoneform" },
            {"1937", "Granite Fury" },
            {"1938", "Brave Bastion" },
            {"1941", "Starfall" },
            {"1942", "Rupture" },
            {"1943", "Stone Fist" },
            {"2201", "Bullseye" },
            {"2209", "Luminary Bolt" },
            {"2220", "Storm Arrow" },
            {"2222", "Double Arrow" },
            {"2230", "Torrent Volley" },
            {"2231", "Focus" },
            {"2232", "Arrow Rain" },
            {"2233", "Powerdraw" },
            {"2234", "Radiance Barrage" },
            {"2235", "Deter Shot" },
            {"2237", "Wildcall" },
            {"2238", "Blast Shot" },
            {"2240", "Lumi Torrent" },
            {"2301", "Resonant Strings 1" },
            {"2302", "Resonant Strings 2" },
            {"2303", "Resonant Strings 3" },
            {"2304", "Resonant Strings 4" },
            {"2306", "Amplified Beat" },
            {"2307", "Healing Beat" },
            {"2308", "Harmonic Anthem" },
            {"2309", "Rhapsody of Flame" },
            {"2310", "Heroic Melody" },
            {"2311", "Healing Melody" },
            {"2312", "Fivefold Crescendo" },
            {"2313", "Passion Burst" },
            {"2314", "Rock the Stage/Infinite Rhapsody" },
            {"2315", "Encore" },
            {"2316", "Center Stage" },
            {"2317", "Fierce Strike" },
            {"2321", "String Strike" },
            {"2332", "Passion Fury" },
            {"2335", "Infinite Rhapsody" },
            {"2336", "Concert Circuit" },
            {"2401", "Blade of Justice 1" },
            {"2402", "Blade of Justice 2" },
            {"2403", "Blade of Justice 3" },
            {"2404", "Blade of Justice 4" },
            {"2405", "Valor Bash" },
            {"2406", "Vanguard Strike" },
            {"2407", "Radiant Infusion" },
            {"2408", "Shield Toss" },
            {"2409", "Divine Circle" },
            {"2410", "Judgment" },
            {"2412", "Reckoning" },
            {"2413", "Inferno Reckon" },
            {"2415", "Aegis Ward" },
            {"2416", "Condemn" },
            {"2417", "Enhanced Condemn" },
            {"2419", "Zeal Crusade" },
            {"2420", "Radiance" },
            {"2421", "Sacred Blade" },
            {"179908", "Blade Intent Thunder Strike" }
        };
    }
}

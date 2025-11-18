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
            {1331, "Dark Mist Fortress (Normal)" }
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
            {"1701", "Judgment Blade 1" },
            {"1702", "Judgment Blade 2" },
            {"1703", "Judgment Blade 3" },
            {"1704", "Judgment Blade 4" },
            {"1705", "Overdrive" },
            {"1713", "Oblivion Combo" },
            {"1714", "Iaido Slash" },
            {"1715", "Moonstrike" },
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
            {"179908", "Blade Intent Thunder Strike" }
        };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static BPSR_ACT_Plugin.Core.BPSR_Enums;

namespace BPSR_ACT_Plugin.Core
{
    class BPSR_Line_Parser
    {
        public string encounterSceneId;
        public string encounterLevelUUID;
        //public static Regex parseLine = new Regex(@"^\[(.*)\] \[(.*)\] DS: (.*) SRC: (.*) TGT: (.*) ID: (.*) VAL: (.*) HPLSN: (.*) ELEM: (.*) EXT: (.*) SCENEID: (.*) LVLID: (.*)", RegexOptions.Compiled);
        public static Regex entityNameCleanupRegex = new Regex(@"(\#(.*))", RegexOptions.Compiled);
        public string line;
        public string timestamp;

        public struct Action
        {

            public string name;
            public string type;
            public string skillDSource;
            public string element;
            private string id;
            public string modifier;
            public int dmgValue;
            public void Reset()
            {
                name = String.Empty;
                id = String.Empty;
                modifier = String.Empty;
                type = String.Empty;
                skillDSource = String.Empty;
                dmgValue = 0;
            }
            public void Set(string skillId)
            {
                if (skillKVPair.ContainsKey(skillId))
                {
                    name = skillKVPair[skillId];
                }
                id = skillId;
            }

            public void Set(string skillId, int skillDmg)
            {
                if (skillKVPair.ContainsKey(skillId))
                {
                    name = skillKVPair[skillId];
                }
                id = skillId;
                dmgValue = skillDmg;
            }

            public void Set(string aId, int skillDmg, string damageModifier)
            {
                if (skillKVPair.ContainsKey(aId))
                {
                    name = skillKVPair[aId];
                }
                id = aId;
                dmgValue = skillDmg;
                modifier = damageModifier;
            }

            public void Set(string aId, int skillDmg, string damageModifier, string dType, string dSource)
            {
                if (skillKVPair.ContainsKey(aId))
                {
                    name = skillKVPair[aId];
                }
                id = aId;
                dmgValue = skillDmg;
                modifier = damageModifier;
                type = dType;
                skillDSource = dSource;
            }
            public void Set(string aId, int skillDmg, string dMod, string dType, string dSource, string element)
            {
                if (skillKVPair.ContainsKey(aId))
                {
                    name = skillKVPair[aId];
                }
                else
                {
                    name = $"Unknown Skill ID: {aId}";
                }
                id = aId;
                dmgValue = skillDmg;
                modifier = dMod;
                type = dType;
                skillDSource = dSource;
                this.element = element;
            }
        }
        public struct Entity
        {
            private string raw;

            private string name;
            private string id;
            private Int64 instance;

            private bool empty;
            private string displayName;
            private IdentityType type;

            public void Set(Entity i)
            {
                raw = i.raw;
                name = i.name;
                id = i.id;
                instance = i.instance;
                empty = i.empty;
                displayName = i.displayName;
                type = i.type;
            }

            public void Set(string name, string id, string instance, string raw)
            {
                this.raw = raw;
                this.name = name;

                if ((name.Length == 0) && (id.Length == 0))
                {
                    empty = true;
                    type = IdentityType.NONE;
                }
                else
                {
                    empty = false;


                    if (name.Length > 0)
                    {
                        if (!name.Contains("(enemy)"))
                        {
                            type = IdentityType.PLAYER;
                        }
                    }
                }
            }

            public void Set(string name)
            {
                raw = name;

                if (name.Length == 0)
                {
                    empty = true;
                    type = IdentityType.NONE;
                }
                else
                {
                    empty = false;
                    this.name = name;

                    if (name.StartsWith("@"))
                    {
                        if (name.Contains(":"))
                        {
                            type = IdentityType.COMPANION;
                        }
                        else
                        {
                            type = IdentityType.PLAYER;
                        }
                    }
                    else
                    {
                        type = IdentityType.NPC;
                    }
                }
            }


            public void Reset()
            {
                raw = String.Empty;
                name = String.Empty;
                id = String.Empty;
                instance = 0;
                empty = true;
                displayName = String.Empty;
                type = IdentityType.NONE;
            }

            public string DisplayName
            {
                get { return displayName; }
                set { displayName = value; }
            }

            public bool Empty
            {
                get { return empty; }
            }

            public string Name
            {
                get { return name; }
            }

            public string Id
            {
                get { return id; }
            }

            public Int64 Instance
            {
                get { return instance; }
            }

            public IdentityType Type
            {
                get { return type; }
            }

            public string Raw
            {
                get { return raw; }
            }

            public void Fix(String replacementSource)
            {
                // Fix bad sources...
                if (empty)
                {
                    displayName = replacementSource;
                }
            }
        }
        public Entity source;
        public Entity target;
        public Action action1;
        public Action action2;
        public BPSR_Line_Parser()
        {
            Reset();
        }

        public void Reset()
        {
            line = null;
            timestamp = null;

            source.Reset();
            target.Reset();
            action1.Reset();
            action2.Reset();
        }

        public static Dictionary<string, string> skillKVPair = new Dictionary<string, string>{
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

        public static Dictionary<string, string> enemyIDKVPair = new Dictionary<string, string>
        {

        };

        public bool Parse(string line)
        {
            Reset();
            this.line = line;

            var lineFields = line.Split('|');
            if (lineFields.Length < 3)
            {
                return false;
            }

            if (lineFields[1] == LogEventIds.EVENT_DAMAGE.ToString() || lineFields[1] == LogEventIds.EVENT_PLAYER_DIE.ToString())
            {
                //var line = $"{DateTimeOffset.Now.ToUnixTimeMilliseconds()}|{LogEventIds.EVENT_DAMAGE}|{sourceUid}|{destinationUid}|{skillId}|{element}|{damageValue}|{targetDamageReceived}|{isCrit}|{isLucky}";
                timestamp = lineFields[0];
                var actionType = lineFields[1];
                var sName = lineFields[2];
                var tName = lineFields[3];
                var skillId = lineFields[4];
                var element = lineFields[5];
                var skillDmg = lineFields[6];
                var hpLessen = lineFields[7];
                var actionCategory = "";
                var damageModifier = lineFields[8] == "True" ? "Crit" : lineFields[9] == "True" ? "Lucky" : "";
                encounterSceneId = ""; //lineFields.Groups[11].Value;
                encounterLevelUUID = ""; //lineFields.Groups[12].Value;
                var isAttackerSelf = lineFields[10] == "True";
                var isTargetSelf = lineFields[11] == "True";

                if (isAttackerSelf)
                {
                    sName = "YOU";
                }
                if (isTargetSelf)
                {
                    tName = "YOU";
                }
                source.Set(sName);
                target.Set(tName);
                action1.Set(skillId, Convert.ToInt32(skillDmg), damageModifier, actionType, actionCategory, element);

                var regexMatchSourceName = entityNameCleanupRegex.Match(source.Name);
                var regexMatchTargetName = entityNameCleanupRegex.Match(target.Name);
                return true;
            }
            if (lineFields[1] == LogEventIds.EVENT_HEAL.ToString())
            {
                //var line = $"{DateTimeOffset.Now.ToUnixTimeMilliseconds()}|{LogEventIds.EVENT_DAMAGE}|{sourceUid}|{destinationUid}|{skillId}|{element}|{damageValue}|{targetDamageReceived}|{isCrit}|{isLucky}";
                timestamp = lineFields[0];
                var actionType = lineFields[1];
                var sName = lineFields[2];
                var tName = lineFields[3];
                var skillId = lineFields[4];
                var element = lineFields[5];
                var skillDmg = lineFields[6];
                var actionCategory = "";
                var damageModifier = lineFields[7] == "True" ? "Crit" : lineFields[8] == "True" ? "Lucky" : "";
                encounterSceneId = ""; //lineFields.Groups[11].Value;
                encounterLevelUUID = ""; //lineFields.Groups[12].Value;
                var isAttackerSelf = lineFields[9] == "True";
                var isTargetSelf = lineFields[10] == "True";

                if (isAttackerSelf)
                {
                    sName = "YOU";
                }
                if (isTargetSelf)
                {
                    tName = "YOU";
                }

                source.Set(sName);
                target.Set(tName);
                action1.Set(skillId, Convert.ToInt32(skillDmg), damageModifier, actionType, actionCategory, element);

                var regexMatchSourceName = entityNameCleanupRegex.Match(source.Name);
                var regexMatchTargetName = entityNameCleanupRegex.Match(target.Name);

                //if (source.Name.Contains($"#{_myUID}(player)"))
                //{
                //    source.Set("YOU");
                //}
                //else
                //{
                //    var displayName = source.Name.Replace(regexMatchSourceName.Groups[1].Value, "");
                //    displayName = String.IsNullOrEmpty(displayName) ? $"Placeholder #{regexMatchSourceName.Groups[2].Value}" : source.DisplayName;
                //}
                //if (source.Name.Contains("(enemy)") && ActGlobals.oFormActMain.SelectiveListGetSelected(source.Name))
                //{
                //    ActGlobals.oFormActMain.SelectiveListRemove(source.Name, true);
                //}

                //if (target.Name.Contains($"#{_myUID}(player)"))
                //{
                //    target.Set("YOU");
                //}
                //else
                //{
                //    var displayName = target.Name.Replace(regexMatchSourceName.Groups[1].Value, "");
                //    displayName = String.IsNullOrEmpty(displayName) ? $"Placeholder #{regexMatchSourceName.Groups[2].Value}" : target.DisplayName;
                //}

                //if (target.Name.Contains("(enemy)") && ActGlobals.oFormActMain.SelectiveListGetSelected(target.Name))
                //{
                //    ActGlobals.oFormActMain.SelectiveListRemove(target.Name, true);
                //}

                return true;
            }
            if (lineFields[1] == LogEventIds.EVENT_ZONE_LOAD.ToString())
            {
                long zoneKey;
                var success = long.TryParse(lineFields[2], out zoneKey);

                if (!success) { return false; }
                var zn = LocationMap.ContainsKey(zoneKey) ? LocationMap[zoneKey] : $"Unknown Location ID {zoneKey}";
                action1.type = lineFields[1];
                action1.name = zn;

                return true;
            }
            //if (lineFields[1] == LogEventIds.EVENT_PLAYER_DIE.ToString())
            //{
            //    action1.type = lineFields[1];
            //    source.Set(lineFields[2]);

            //    return true;
            //}
            return false;
        }
        private string ConvertCNToENElement(char CN)
        {

            int uniValue = CN;
            var uniString = $"{uniValue:X4}";

            switch (uniString)
            {
                case "7269":
                    return "General";
                case "51B0":
                    return "Water";
                case "96F7":
                    return "Lightning";
                case "68EE":
                    return "Forest";
                case "98CE":
                    return "Wind";
                case "5CA9":
                    return "Earth";
                case "5149":
                    return "Light";
                case "6697":
                    return "Dark";
                default:
                    return "???";
            }
        }
    }
}

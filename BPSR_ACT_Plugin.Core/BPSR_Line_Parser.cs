using Advanced_Combat_Tracker;
using PcapDotNet.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static BPSR_ACT_Plugin.Core.BPSR_Enums_Constants;

namespace BPSR_ACT_Plugin.Core
{
    class BPSR_Line_Parser
    {
        private object locker = new object();
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
            public void Set(string aId, int skillDmg, string dMod, string dType, string dSource, string element)
            {
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

            private string job;
            private string abilityScore;

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
                set { name = value; }
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
                set { type = value; }
            }

            public string Raw
            {
                get { return raw; }
            }

            public string Job { get { return job; } set { job = value; } }
            public string AbilityScore { get { return abilityScore; } set { abilityScore = value; } }
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

                var spIdSuccess = int.TryParse(lineFields[12], out int sourceProfessionId);
                var tpIdSuccess = int.TryParse(lineFields[13], out int targetProfessionId);
                var sFpSuccess = int.TryParse(lineFields[14], out int sourceFightPoint);
                var tFpSuccess = int.TryParse(lineFields[15], out int targetFightPoint);

                source.DisplayName = sName;
                target.DisplayName = tName;

                if (isAttackerSelf)
                {
                    sName = "YOU";
                    source.DisplayName = "YOU";
                }
                else if ((!sName.StartsWith("#")) && sName.Contains("#"))
                {
                    var split = sName.Split('#');
                    source.DisplayName = split[0];
                    sName = "#" + split[1];
                }
                else if ((!sName.StartsWith("$")) && sName.Contains("$"))
                {
                    var split = sName.Split('$');
                    source.DisplayName = split[0];
                    sName = "$" + split[1];
                }

                if (isTargetSelf)
                {
                    tName = "YOU";
                    target.DisplayName = "YOU";
                }
                else if ((!tName.StartsWith("#")) && tName.Contains("#"))
                {
                    var split = tName.Split('#');
                    target.DisplayName = split[0];
                    tName = "#" + split[1];
                }
                else if ((!tName.StartsWith("$")) && tName.Contains("$"))
                {
                    var split = tName.Split('$');
                    target.DisplayName = split[0];
                    tName = "$" + split[1];
                }

                if (spIdSuccess)
                {
                    source.Job = GetProfessionFromId(sourceProfessionId);
                }

                if (sFpSuccess)
                {
                    source.AbilityScore = sourceFightPoint.ToString();
                }

                if (tpIdSuccess)
                {
                    target.Job = GetProfessionFromId(targetProfessionId);
                }

                if (tFpSuccess)
                {
                    target.AbilityScore = targetFightPoint.ToString();
                }



                source.Name = sName;
                target.Name = tName;
                source.Type = sName == "YOU" || sName.Contains("#") ? IdentityType.PLAYER : IdentityType.ENEMY;
                target.Type = tName == "YOU" || tName.Contains("#") ? IdentityType.PLAYER : IdentityType.ENEMY;
                action1.Set(skillId, Convert.ToInt32(skillDmg), damageModifier, actionType, actionCategory, element);
                string skillName = $"Unknown Skill ID: {skillId}";
                var getSkillSuccess = SkillKVPair.TryGetValue(skillId, out var skillData);
                if (getSkillSuccess)
                {
                    skillName = skillData.SkillName;
                    if (skillData.DamageClass > 0 && (source.Job.IsNullOrEmpty() || source.Job == ENT_JOB_UNKNOWN))
                    {
                        source.Job = GetProfessionFromId(skillData.DamageClass); //we can extrapolate source entity profession from skill usage
                    }
                    if (skillData.DamageClass == (int)NonProfessionDamageClasses.Environment)
                    {
                        source.Name = ENT_NAME_IGNORE;
                        source.Job = ENT_JOB_UNKNOWN;
                        source.Type = IdentityType.ENVIRONMENT;
                    }
                }
                action1.name = skillName;

                return true;
            }
            else if (lineFields[1] == LogEventIds.EVENT_HEAL.ToString())
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
                var spIdSuccess = int.TryParse(lineFields[11], out int sourceProfessionId);
                var tpIdSuccess = int.TryParse(lineFields[12], out int targetProfessionId);
                var sFpSuccess = int.TryParse(lineFields[13], out int sourceFightPoint);
                var tFpSuccess = int.TryParse(lineFields[14], out int targetFightPoint);

                if (isAttackerSelf)
                {
                    sName = "YOU";
                    source.DisplayName = "YOU";
                }
                else if (!sName.StartsWith("#") && sName.Contains("#"))
                {
                    var split = sName.Split('#');
                    source.DisplayName = split[0];
                    sName = "#" + split[1];
                }

                if (isTargetSelf)
                {
                    tName = "YOU";
                    target.DisplayName = "YOU";
                }
                else if (!tName.StartsWith("#") && tName.Contains("#"))
                {
                    var split = tName.Split('#');
                    target.DisplayName = split[0];
                    tName = "#" + split[1];
                }

                if (spIdSuccess)
                {
                    source.Job = GetProfessionFromId(sourceProfessionId);
                }

                if (sFpSuccess)
                {
                    source.AbilityScore = sourceFightPoint.ToString();
                }

                if (tpIdSuccess)
                {
                    target.Job = GetProfessionFromId(targetProfessionId);
                }

                if (tFpSuccess)
                {
                    target.AbilityScore = targetFightPoint.ToString();
                }

                source.Set(sName);
                target.Set(tName);
                action1.Set(skillId, Convert.ToInt32(skillDmg), damageModifier, actionType, actionCategory, element);
                string skillName = $"Unknown Skill ID: {skillId}";
                var getSkillSuccess = SkillKVPair.TryGetValue(skillId, out var skillData);
                if (getSkillSuccess)
                {
                    skillName = skillData.SkillName;
                    if (skillData.DamageClass > 0 && (source.Job.IsNullOrEmpty() || source.Job == "???"))
                    {
                        source.Job = GetProfessionFromId(skillData.DamageClass); //we can extrapolate source entity profession from skill usage
                    }
                    if (skillData.DamageClass == (int)NonProfessionDamageClasses.Environment)
                    {
                        source.Name = ENT_NAME_IGNORE;
                        source.Job = ENT_JOB_UNKNOWN;
                        source.Type = IdentityType.ENVIRONMENT; //I guess the environment could heal?
                    }
                }
                action1.name = skillName;

                return true;
            }
            else if (lineFields[1] == LogEventIds.EVENT_ZONE_LOAD.ToString())
            {
                long zoneKey;
                var success = long.TryParse(lineFields[2], out zoneKey);
                var isDirtySync = lineFields[3] == "True";
                if (!success) { return false; }
                var zn = LocationMap.ContainsKey(zoneKey) ? LocationMap[zoneKey] : $"Unknown Location ID {zoneKey}";
                action1.type = lineFields[1];
                action1.name = zn;
                action1.modifier = isDirtySync ? "DirtySync" : "None";
                return true;
            }

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

        private string GetProfessionFromId(int professionId)
        {
            switch ((ProfessionType)professionId)
            {
                case ProfessionType.Stormblade:
                    return "Stormblade";
                case ProfessionType.FrostMage:
                    return "Frost Mage";
                case ProfessionType.FireWarrior:
                    return "Fire Warrior";
                case ProfessionType.WindKnight:
                    return "Wind Knight";
                case ProfessionType.VerdantOracle:
                    return "Verdant Oracle";
                case ProfessionType.Marksman_Cannon:
                    return "Gunner";
                case ProfessionType.HeavyGuardian:
                    return "Heavy Guardian";
                case ProfessionType.SoulMusician_Scythe:
                    return "Reaper";
                case ProfessionType.Marksman:
                    return "Marksman";
                case ProfessionType.ShieldKnight:
                    return "Shield Knight";
                case ProfessionType.SoulMusician:
                    return "Beat Performer";
                default:
                    return ENT_JOB_UNKNOWN;
            }
        }
    }
}

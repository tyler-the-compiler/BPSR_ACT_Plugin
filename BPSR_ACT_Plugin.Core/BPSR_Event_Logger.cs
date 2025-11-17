using Advanced_Combat_Tracker;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BPSR_ACT_Plugin.Core.BPSR_Enums;

namespace BPSR_ACT_Plugin.Core
{
    class BPSR_Event_Logger
    {
        private string rootDir = Path.Combine(ActGlobals.oFormActMain.AppDataFolder.FullName, @"BPSRLogs");
        private string logFileName;

        public BPSR_Event_Logger()
        {
            var directoryObject = new DirectoryInfo(rootDir);
            var mostRecentFile = (from f in directoryObject.GetFiles()
                                  orderby f.LastWriteTime descending
                                  select f).First();
            logFileName = mostRecentFile.FullName;
        }

        public void AddZoneChangeLogLine(long currentLevelId)
        {
            var line = $"{LogEventIds.EVENT_ZONE_LOAD}|{currentLevelId}";
            AddLogLine(line);
        }

        public void AddDeathLogLine(string sourceUid, string destinationUid, int skillId, string element, long damageValue, long targetDamageReceived, bool isCrit, bool isLucky, bool isAttackerUser, bool isTargetUser, string sourceProfessionId, string targetProfessionId, string sourcePlayerFightPoint, string targetPlayerFightPoint)
        {
            var line = $"{LogEventIds.EVENT_PLAYER_DIE}|{sourceUid}|{destinationUid}|{skillId}|{element}|{damageValue}|{targetDamageReceived}|{isCrit}|{isLucky}|{isAttackerUser}|{isTargetUser}|{sourceProfessionId}|{targetProfessionId}|{sourcePlayerFightPoint}|{targetPlayerFightPoint}";
            AddLogLine(line);
        }

        public void AddDamageLogLine(string sourceUid, string destinationUid, int skillId, string element, long damageValue, long targetDamageReceived, bool isCrit, bool isLucky, bool isAttackerUser, bool isTargetUser, string sourceProfessionId, string targetProfessionId, string sourcePlayerFightPoint, string targetPlayerFightPoint)
        {
            var line = $"{LogEventIds.EVENT_DAMAGE}|{sourceUid}|{destinationUid}|{skillId}|{element}|{damageValue}|{targetDamageReceived}|{isCrit}|{isLucky}|{isAttackerUser}|{isTargetUser}|{sourceProfessionId}|{targetProfessionId}|{sourcePlayerFightPoint}|{targetPlayerFightPoint}";
            AddLogLine(line);
        }

        public void AddHealingLogLine(string sourceUid, string destinationUid, int skillId, string element, long damageValue, bool isCrit, bool isLucky, bool isAttackerUser, bool isTargetUser, string sourceProfessionId, string targetProfessionId, string sourcePlayerFightPoint, string targetPlayerFightPoint)
        {
            var line = $"{LogEventIds.EVENT_HEAL}|{sourceUid}|{destinationUid}|{skillId}|{element}|{damageValue}|{isCrit}|{isLucky}|{isAttackerUser}|{isTargetUser}|{sourceProfessionId}|{targetProfessionId}|{sourcePlayerFightPoint}|{targetPlayerFightPoint}";
            AddLogLine(line);
        }

        private void AddLogLine(string line)
        {
            line = $"{DateTimeOffset.Now.ToUnixTimeMilliseconds()}|{line}";

            File.AppendAllText(logFileName, line + "\n");

        }
    }
}

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
        public static BPSR_Event_Logger Instance
        {
            get
            {
                lock (padlock)
                {
                    if (instance == null)
                    {
                        instance = new BPSR_Event_Logger();
                    }
                    return instance;
                }
            }
        }
        private static BPSR_Event_Logger instance = null;
        private static readonly object padlock = new object();
        private string rootDir = Path.Combine(ActGlobals.oFormActMain.AppDataFolder.FullName, @"BPSRLogs");
        private string logFileName;
        private Queue<Func<Task>> taskQueue;
        private bool isProcessingTask = false;

        private BPSR_Event_Logger()
        {
            taskQueue = new Queue<Func<Task>>();
            var directoryObject = new DirectoryInfo(rootDir);
            var mostRecentFile = (from f in directoryObject.GetFiles()
                                  orderby f.LastWriteTime descending
                                  select f).First();
            logFileName = mostRecentFile.FullName;
        }

        public void AddZoneChangeLogLine(long currentLevelId)
        {
            var line = $"{LogEventIds.EVENT_ZONE_LOAD}|{currentLevelId}|False";
            AddLogLine(line);
        }

        public void AddZoneChangeLogLine(long currentLevelId, bool isDirtySync)
        {
            var line = $"{LogEventIds.EVENT_ZONE_LOAD}|{currentLevelId}|{isDirtySync}";
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
            var timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            EnqueueTask(async () => await Task.Run(() => File.AppendAllText(logFileName, $"{timestamp}|{line}" + "\n")));
        }

        private void EnqueueTask(Func<Task> task)
        {
            lock (padlock)
            {
                taskQueue.Enqueue(task);
                if (!isProcessingTask)
                {
                    isProcessingTask = true;
                    Task.Run(ExecuteTasks);
                }
            }
        }

        private async Task ExecuteTasks()
        {
            while (true)
            {
                Func<Task> nextTask = null;
                lock (padlock)
                {
                    if (taskQueue.Count > 0)
                    {
                        nextTask = taskQueue.Dequeue();
                    }
                    else
                    {
                        isProcessingTask = false;
                        break;
                    }
                }
                if (nextTask != null)
                {
                    await nextTask();
                }
            }
        }
    }
}

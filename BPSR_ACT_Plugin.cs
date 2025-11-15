using ACT_Plugin.BlueProtobuf;
using Advanced_Combat_Tracker;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json.Linq;
using PcapDotNet.Base;
using PcapDotNet.Core;
using PcapDotNet.Packets;
using PcapDotNet.Packets.Ethernet;
using PcapDotNet.Packets.Ip;
using PcapDotNet.Packets.IpV4;
using PcapDotNet.Packets.Transport;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using ZstdSharp;
using static ACT_Plugin.BPSR_Enums;
using static PacketDotNet.Ieee80211.InformationElement;
using static System.Windows.Forms.AxHost;


[assembly: AssemblyTitle("BPSR_ACT_Plugin")]
[assembly: AssemblyDescription("A parsing plugin for Blue Protocol: Star Resonance in ACT")]
[assembly: AssemblyCompany("Subtract")]
[assembly: AssemblyVersion("1.0.0.2")]

namespace ACT_Plugin
{
    public static class Extensions
    {
        public static T[] SubArray<T>(this T[] data, long index, long length)
        {
            var internalLen = length;
            T[] result = new T[internalLen];
            Array.Copy(data, index, result, 0, internalLen);
            return result;
        }

        public static T[] SubArray<T>(this T[] data, long index)
        {
            var internalLen = (data.Length - index);
            T[] result = new T[internalLen];
            Array.Copy(data, index, result, 0, internalLen);
            return result;
        }
    }
    public class BPSR_ACT_Plugin : UserControl, IActPluginV1
    {
        private object locker = new object();
        //private BPSR_Packet_Interceptor packetInterceptor;
        private TextBox ui_logfileParentFolder;
        private System.Windows.Forms.Label ui_logfileParentFolder_label;
        Label lblStatus;
        string settingsFile = Path.Combine(ActGlobals.oFormActMain.AppDataFolder.FullName, "Config\\BPSR_ACT_Plugin.config.xml");
        SettingsSerializer xmlSettings;
        private int NumLinesRead = 0;
        private int NumLinesFailedParse = 0;
        private BPSR_Line_Parser bpsrLineParser;
        private BPSR_Line_Parser bpsrPreviousLine;
        private BPSR_Encounter encounter = new BPSR_Encounter();
        private Dictionary<string, string> entityCache = new Dictionary<string, string>();
        private const int
            DMG = 1,
            HEALS = 7;
        private string latestDirectoryPath = "";
        private JObject allPlayersJSONFile;
        private DateTime lastAccessedAllPlayersJSONFile;
        private class NPCInstances
        {
            public int nextId;

            // Instance ID -> pretty number
            public Dictionary<Int64, int> instances = null;
        }
        private Dictionary<string, NPCInstances> npcInstances = null;
        public BPSR_ACT_Plugin()
        {
            InitializeComponent();
        }

        #region Boilerplate
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.ui_logfileParentFolder_label = new System.Windows.Forms.Label();
            this.ui_logfileParentFolder = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // ui_logfileParentFolder_label
            // 
            this.ui_logfileParentFolder_label.AutoSize = true;
            this.ui_logfileParentFolder_label.Location = new System.Drawing.Point(3, 0);
            this.ui_logfileParentFolder_label.Name = "label1";
            this.ui_logfileParentFolder_label.Size = new System.Drawing.Size(434, 13);
            this.ui_logfileParentFolder_label.TabIndex = 0;
            this.ui_logfileParentFolder_label.Text = "Put the path here";
            // 
            // textBox1
            // 
            this.ui_logfileParentFolder.Location = new System.Drawing.Point(6, 16);
            this.ui_logfileParentFolder.Name = "textBox1";
            this.ui_logfileParentFolder.Size = new System.Drawing.Size(431, 20);
            this.ui_logfileParentFolder.TabIndex = 1;
            this.ui_logfileParentFolder.Text = "Put your log file parent folder path here";
            // 
            // PluginSample
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.ui_logfileParentFolder);
            this.Controls.Add(this.ui_logfileParentFolder_label);
            this.Name = "BPSR_ACT_Plugin";
            this.Size = new System.Drawing.Size(686, 384);
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        #endregion Boilerplate

        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            var rootDir = Path.Combine(ActGlobals.oFormActMain.AppDataFolder.FullName, @"BPSRLogs");
            if (!Directory.Exists(rootDir))
            {
                Directory.CreateDirectory(rootDir);
            }

            lblStatus = pluginStatusText;   // Hand the status label's reference to our local var
            pluginScreenSpace.Controls.Add(this);   // Add this UserControl to the tab ACT provides
            this.Dock = DockStyle.Fill; // Expand the UserControl to fill the tab's client space
            xmlSettings = new SettingsSerializer(this); // Create a new settings serializer and pass it this instance
            LoadSettings();

            bpsrLineParser = new BPSR_Line_Parser();
            bpsrPreviousLine = new BPSR_Line_Parser();

            this.SetupBPSREnvironment();

            var now = DateTime.Now;
            var logFileName = $"BPSR-Log-{now.Month}{now.Day}{now.Year}-{Guid.NewGuid()}.log";
            using (File.Create(Path.Combine(rootDir, logFileName))) { }


            new Thread(new ThreadStart(new BPSR_Packet_Interceptor().Start)).Start();
            //packetInterceptor.Start();

            try
            {
                ActGlobals.oFormActMain.ResetCheckLogs();
            }
            catch { }
            npcInstances = new Dictionary<string, NPCInstances>();



            ActGlobals.oFormActMain.BeforeLogLineRead += new LogLineEventDelegate(ParseLine);
            ActGlobals.oFormActMain.LogFileChanged += new LogFileChangedDelegate(oFormActMain_LogFileChanged);
            ActGlobals.oFormActMain.LogFileParentFolderName = rootDir;
            ActGlobals.oFormActMain.LogFilePath = Path.Combine(rootDir, logFileName);
            ActGlobals.oFormActMain.OpenLog(false, false);
            //this.WatchLogFolder();
            lblStatus.Text = "BP:SR ACT Plugin Started";
        }
        public void DeInitPlugin()
        {
            ActGlobals.oFormActMain.BeforeLogLineRead -= ParseLine;
            ActGlobals.oFormActMain.LogFileChanged -= oFormActMain_LogFileChanged;

            SaveSettings();
            lblStatus.Text = "BP:SR ACT Plugin Exited";
        }

        private string GetIntCommas()
        {
            return ActGlobals.mainTableShowCommas ? "#,0" : "0";
        }

        private string GetFloatCommas()
        {
            return ActGlobals.mainTableShowCommas ? "#,0.00" : "0.00";
        }
        private void SetupBPSREnvironment()
        {
            CultureInfo usCulture = new CultureInfo("en-US");	// This is for SQL syntax; do not change

            EncounterData.ColumnDefs.Clear();
            // Do not change the SqlDataName while doing localization

            // Columns "Zone View Options"
            EncounterData.ColumnDefs.Add("EncId", new EncounterData.ColumnDef("EncId", false, "CHAR(8)", "EncId", (Data) => { return string.Empty; }, (Data) => { return Data.EncId; }));
            EncounterData.ColumnDefs.Add("Title", new EncounterData.ColumnDef("Title", true, "VARCHAR(64)", "Title", (Data) => { return Data.Title; }, (Data) => { return Data.Title; }));
            EncounterData.ColumnDefs.Add("StartTime", new EncounterData.ColumnDef("StartTime", true, "TIMESTAMP", "StartTime", (Data) => { return Data.StartTime == DateTime.MaxValue ? "--:--:--" : String.Format("{0} {1}", Data.StartTime.ToShortDateString(), Data.StartTime.ToLongTimeString()); }, (Data) => { return Data.StartTime == DateTime.MaxValue ? "0000-00-00 00:00:00" : Data.StartTime.ToString("u").TrimEnd(new char[] { 'Z' }); }));
            EncounterData.ColumnDefs.Add("EndTime", new EncounterData.ColumnDef("EndTime", true, "TIMESTAMP", "EndTime", (Data) => { return Data.EndTime == DateTime.MinValue ? "--:--:--" : Data.EndTime.ToString("T"); }, (Data) => { return Data.EndTime == DateTime.MinValue ? "0000-00-00 00:00:00" : Data.EndTime.ToString("u").TrimEnd(new char[] { 'Z' }); }));
            EncounterData.ColumnDefs.Add("Duration", new EncounterData.ColumnDef("Duration", true, "INT", "Duration", (Data) => { return Data.DurationS; }, (Data) => { return Data.Duration.TotalSeconds.ToString("0"); }));
            EncounterData.ColumnDefs.Add("Damage", new EncounterData.ColumnDef("Damage", true, "BIGINT", "Damage", (Data) => { return Data.Damage.ToString(GetIntCommas()); }, (Data) => { return Data.Damage.ToString(); }));
            EncounterData.ColumnDefs.Add("EncDPS", new EncounterData.ColumnDef("EncDPS", true, "DOUBLE", "EncDPS", (Data) => { return Data.DPS.ToString(GetFloatCommas()); }, (Data) => { return Data.DPS.ToString(usCulture); }));
            EncounterData.ColumnDefs.Add("Zone", new EncounterData.ColumnDef("Zone", false, "VARCHAR(64)", "Zone", (Data) => { return Data.ZoneName; }, (Data) => { return Data.ZoneName; }));
            EncounterData.ColumnDefs.Add("Kills", new EncounterData.ColumnDef("Kills", true, "INT", "Kills", (Data) => { return Data.AlliedKills.ToString(GetIntCommas()); }, (Data) => { return Data.AlliedKills.ToString(); }));
            EncounterData.ColumnDefs.Add("Deaths", new EncounterData.ColumnDef("Deaths", true, "INT", "Deaths", (Data) => { return Data.AlliedDeaths.ToString(); }, (Data) => { return Data.AlliedDeaths.ToString(); }));


            EncounterData.ExportVariables.Clear();
            EncounterData.ExportVariables.Add("n", new EncounterData.TextExportFormatter("n", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-newline"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-newline"].DisplayedText, (Data, SelectiveAllies, Extra) => { return "\n"; }));
            EncounterData.ExportVariables.Add("t", new EncounterData.TextExportFormatter("t", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-tab"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-tab"].DisplayedText, (Data, SelectiveAllies, Extra) => { return "\t"; }));
            EncounterData.ExportVariables.Add("title", new EncounterData.TextExportFormatter("title", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-title"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-title"].DisplayedText, (Data, SelectiveAllies, Extra) => { return EncounterFormatSwitch(Data, SelectiveAllies, "title", Extra); }));
            EncounterData.ExportVariables.Add("duration", new EncounterData.TextExportFormatter("duration", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-duration"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-duration"].DisplayedText, (Data, SelectiveAllies, Extra) => { return EncounterFormatSwitch(Data, SelectiveAllies, "duration", Extra); }));
            EncounterData.ExportVariables.Add("DURATION", new EncounterData.TextExportFormatter("DURATION", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-DURATION"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-DURATION"].DisplayedText, (Data, SelectiveAllies, Extra) => { return EncounterFormatSwitch(Data, SelectiveAllies, "DURATION", Extra); }));
            EncounterData.ExportVariables.Add("damage", new EncounterData.TextExportFormatter("damage", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-damage"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-damage"].DisplayedText, (Data, SelectiveAllies, Extra) => { return EncounterFormatSwitch(Data, SelectiveAllies, "damage", Extra); }));
            EncounterData.ExportVariables.Add("damage-m", new EncounterData.TextExportFormatter("damage-m", "Damage M", "Damage divided by 1,000,000 (with two decimal places)", (Data, SelectiveAllies, Extra) => { return EncounterFormatSwitch(Data, SelectiveAllies, "damage-m", Extra); }));
            EncounterData.ExportVariables.Add("DAMAGE-k", new EncounterData.TextExportFormatter("DAMAGE-k", "Short Damage K", "Damage divided by 1,000 (with no decimal places)", (Data, SelectiveAllies, Extra) => { return EncounterFormatSwitch(Data, SelectiveAllies, "DAMAGE-k", Extra); }));
            EncounterData.ExportVariables.Add("DAMAGE-m", new EncounterData.TextExportFormatter("DAMAGE-m", "Short Damage M", "Damage divided by 1,000,000 (with no decimal places)", (Data, SelectiveAllies, Extra) => { return EncounterFormatSwitch(Data, SelectiveAllies, "DAMAGE-m", Extra); }));
            EncounterData.ExportVariables.Add("dps", new EncounterData.TextExportFormatter("dps", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-dps"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-dps"].DisplayedText, (Data, SelectiveAllies, Extra) => { return EncounterFormatSwitch(Data, SelectiveAllies, "dps", Extra); }));
            EncounterData.ExportVariables.Add("DPS", new EncounterData.TextExportFormatter("DPS", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-DPS"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-DPS"].DisplayedText, (Data, SelectiveAllies, Extra) => { return EncounterFormatSwitch(Data, SelectiveAllies, "DPS", Extra); }));
            EncounterData.ExportVariables.Add("DPS-k", new EncounterData.TextExportFormatter("DPS-k", "DPS K", "DPS divided by 1,000 (with no decimal places)", (Data, SelectiveAllies, Extra) => { return EncounterFormatSwitch(Data, SelectiveAllies, "DPS-k", Extra); }));
            EncounterData.ExportVariables.Add("encdps", new EncounterData.TextExportFormatter("encdps", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-extdps"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-extdps"].DisplayedText, (Data, SelectiveAllies, Extra) => { return EncounterFormatSwitch(Data, SelectiveAllies, "encdps", Extra); }));
            EncounterData.ExportVariables.Add("ENCDPS", new EncounterData.TextExportFormatter("ENCDPS", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-EXTDPS"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-EXTDPS"].DisplayedText, (Data, SelectiveAllies, Extra) => { return EncounterFormatSwitch(Data, SelectiveAllies, "ENCDPS", Extra); }));
            EncounterData.ExportVariables.Add("ENCDPS-k", new EncounterData.TextExportFormatter("ENCDPS-k", "Short DPS K", "ENCDPS divided by 1,000 (with no decimal places)", (Data, SelectiveAllies, Extra) => { return EncounterFormatSwitch(Data, SelectiveAllies, "ENCDPS-k", Extra); }));
            EncounterData.ExportVariables.Add("hits", new EncounterData.TextExportFormatter("hits", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-hits"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-hits"].DisplayedText, (Data, SelectiveAllies, Extra) => { return EncounterFormatSwitch(Data, SelectiveAllies, "hits", Extra); }));
            EncounterData.ExportVariables.Add("crithits", new EncounterData.TextExportFormatter("crithits", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-crithits"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-crithits"].DisplayedText, (Data, SelectiveAllies, Extra) => { return EncounterFormatSwitch(Data, SelectiveAllies, "crithits", Extra); }));
            EncounterData.ExportVariables.Add("crithit%", new EncounterData.TextExportFormatter("crithit%", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-crithit%"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-crithit%"].DisplayedText, (Data, SelectiveAllies, Extra) => { return EncounterFormatSwitch(Data, SelectiveAllies, "crithit%", Extra); }));
            EncounterData.ExportVariables.Add("luckyhit%", new EncounterData.TextExportFormatter("luckyhit%", "Similar to crits, but luckier", "Not a direct hit either", (Data, SelectiveAllies, Extra) => { return EncounterFormatSwitch(Data, SelectiveAllies, "luckyhit%", Extra); }));
            EncounterData.ExportVariables.Add("deaths", new EncounterData.TextExportFormatter("deaths", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-deaths"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-deaths"].DisplayedText, (Data, SelectiveAllies, Extra) => { return EncounterFormatSwitch(Data, SelectiveAllies, "deaths", Extra); }));

            CombatantData.ColumnDefs.Clear();
            CombatantData.ColumnDefs.Add("EncId", new CombatantData.ColumnDef("EncId", false, "CHAR(8)", "EncId", (Data) => { return string.Empty; }, (Data) => { return Data.Parent.EncId; }, (Left, Right) => { return 0; }));
            CombatantData.ColumnDefs.Add("Ally", new CombatantData.ColumnDef("Ally", false, "CHAR(1)", "Ally", (Data) => { return Data.Parent.GetAllies().Contains(Data).ToString(); }, (Data) => { return Data.Parent.GetAllies().Contains(Data) ? "T" : "F"; }, (Left, Right) => { return Left.Parent.GetAllies().Contains(Left).CompareTo(Right.Parent.GetAllies().Contains(Right)); }));

            CombatantData.ColumnDefs.Add("Name", new CombatantData.ColumnDef("Name", true, "VARCHAR(64)", "Name", (Data) => { return Data.Name; }, (Data) => { return Data.Name; }, (Left, Right) =>
            {
                return Left.Name.CompareTo(Right.Name);
            }));

            CombatantData.ColumnDefs.Add("StartTime", new CombatantData.ColumnDef("StartTime", true, "TIMESTAMP", "StartTime", (Data) => { return Data.StartTime == DateTime.MaxValue ? "--:--:--" : Data.StartTime.ToString("T"); }, (Data) => { return Data.StartTime == DateTime.MaxValue ? "0000-00-00 00:00:00" : Data.StartTime.ToString("u").TrimEnd(new char[] { 'Z' }); }, (Left, Right) => { return Left.StartTime.CompareTo(Right.StartTime); }));
            CombatantData.ColumnDefs.Add("EndTime", new CombatantData.ColumnDef("EndTime", false, "TIMESTAMP", "EndTime", (Data) => { return Data.EndTime == DateTime.MinValue ? "--:--:--" : Data.StartTime.ToString("T"); }, (Data) => { return Data.EndTime == DateTime.MinValue ? "0000-00-00 00:00:00" : Data.EndTime.ToString("u").TrimEnd(new char[] { 'Z' }); }, (Left, Right) => { return Left.EndTime.CompareTo(Right.EndTime); }));
            CombatantData.ColumnDefs.Add("Duration", new CombatantData.ColumnDef("Duration", true, "INT", "Duration", (Data) => { return Data.DurationS; }, (Data) => { return Data.Duration.TotalSeconds.ToString("0"); }, (Left, Right) => { return Left.Duration.CompareTo(Right.Duration); }));
            CombatantData.ColumnDefs.Add("Damage", new CombatantData.ColumnDef("Damage", true, "BIGINT", "Damage", (Data) => { return Data.Damage.ToString(GetIntCommas()); }, (Data) => { return Data.Damage.ToString(); }, (Left, Right) => { return Left.Damage.CompareTo(Right.Damage); }));
            CombatantData.ColumnDefs.Add("Damage%", new CombatantData.ColumnDef("Damage%", true, "VARCHAR(4)", "DamagePerc", (Data) => { return Data.DamagePercent; }, (Data) => { return Data.DamagePercent; }, (Left, Right) => { return Left.Damage.CompareTo(Right.Damage); }));
            CombatantData.ColumnDefs.Add("Kills", new CombatantData.ColumnDef("Kills", false, "INT", "Kills", (Data) => { return Data.Kills.ToString(GetIntCommas()); }, (Data) => { return Data.Kills.ToString(); }, (Left, Right) => { return Left.Kills.CompareTo(Right.Kills); }));
            CombatantData.ColumnDefs.Add("Healed", new CombatantData.ColumnDef("Healed", false, "BIGINT", "Healed", (Data) => { return Data.Healed.ToString(GetIntCommas()); }, (Data) => { return Data.Healed.ToString(); }, (Left, Right) => { return Left.Healed.CompareTo(Right.Healed); }));
            CombatantData.ColumnDefs.Add("Healed%", new CombatantData.ColumnDef("Healed%", false, "VARCHAR(4)", "HealedPerc", (Data) => { return Data.HealedPercent; }, (Data) => { return Data.HealedPercent; }, (Left, Right) => { return Left.Healed.CompareTo(Right.Healed); }));
            CombatantData.ColumnDefs.Add("CritHeals", new CombatantData.ColumnDef("CritHeals", false, "INT", "CritHeals", (Data) => { return Data.CritHeals.ToString(GetIntCommas()); }, (Data) => { return Data.CritHeals.ToString(); }, (Left, Right) => { return Left.CritHeals.CompareTo(Right.CritHeals); }));
            CombatantData.ColumnDefs.Add("Heals", new CombatantData.ColumnDef("Heals", false, "INT", "Heals", (Data) => { return Data.Heals.ToString(GetIntCommas()); }, (Data) => { return Data.Heals.ToString(); }, (Left, Right) => { return Left.Heals.CompareTo(Right.Heals); }));
            CombatantData.ColumnDefs.Add("DPS", new CombatantData.ColumnDef("DPS", false, "DOUBLE", "DPS", (Data) => { return Data.DPS.ToString(GetFloatCommas()); }, (Data) => { return Data.DPS.ToString(usCulture); }, (Left, Right) => { return Left.DPS.CompareTo(Right.DPS); }));
            CombatantData.ColumnDefs.Add("EncDPS", new CombatantData.ColumnDef("EncDPS", true, "DOUBLE", "EncDPS", (Data) => { return Data.EncDPS.ToString(GetFloatCommas()); }, (Data) => { return Data.EncDPS.ToString(usCulture); }, (Left, Right) => { return Left.Damage.CompareTo(Right.Damage); }));
            CombatantData.ColumnDefs.Add("EncHPS", new CombatantData.ColumnDef("EncHPS", true, "DOUBLE", "EncHPS", (Data) => { return Data.EncHPS.ToString(GetFloatCommas()); }, (Data) => { return Data.EncHPS.ToString(usCulture); }, (Left, Right) => { return Left.Healed.CompareTo(Right.Healed); }));
            CombatantData.ColumnDefs.Add("Hits", new CombatantData.ColumnDef("Hits", false, "INT", "Hits", (Data) => { return Data.Hits.ToString(GetIntCommas()); }, (Data) => { return Data.Hits.ToString(); }, (Left, Right) => { return Left.Hits.CompareTo(Right.Hits); }));
            CombatantData.ColumnDefs.Add("CritHits", new CombatantData.ColumnDef("CritHits", false, "INT", "CritHits", (Data) => { return Data.CritHits.ToString(GetIntCommas()); }, (Data) => { return Data.CritHits.ToString(); }, (Left, Right) => { return Left.CritHits.CompareTo(Right.CritHits); }));
            CombatantData.ColumnDefs.Add("Avoids", new CombatantData.ColumnDef("Avoids", false, "INT", "Blocked", (Data) => { return Data.Blocked.ToString(GetIntCommas()); }, (Data) => { return Data.Blocked.ToString(); }, (Left, Right) => { return Left.Blocked.CompareTo(Right.Blocked); }));
            CombatantData.ColumnDefs.Add("Misses", new CombatantData.ColumnDef("Misses", false, "INT", "Misses", (Data) => { return Data.Misses.ToString(GetIntCommas()); }, (Data) => { return Data.Misses.ToString(); }, (Left, Right) => { return Left.Misses.CompareTo(Right.Misses); }));
            CombatantData.ColumnDefs.Add("Swings", new CombatantData.ColumnDef("Swings", false, "INT", "Swings", (Data) => { return Data.Swings.ToString(GetIntCommas()); }, (Data) => { return Data.Swings.ToString(); }, (Left, Right) => { return Left.Swings.CompareTo(Right.Swings); }));
            CombatantData.ColumnDefs.Add("HealingTaken", new CombatantData.ColumnDef("HealingTaken", false, "BIGINT", "HealsTaken", (Data) => { return Data.HealsTaken.ToString(GetIntCommas()); }, (Data) => { return Data.HealsTaken.ToString(); }, (Left, Right) => { return Left.HealsTaken.CompareTo(Right.HealsTaken); }));
            CombatantData.ColumnDefs.Add("DamageTaken", new CombatantData.ColumnDef("DamageTaken", true, "BIGINT", "DamageTaken", (Data) => { return Data.DamageTaken.ToString(GetIntCommas()); }, (Data) => { return Data.DamageTaken.ToString(); }, (Left, Right) => { return Left.DamageTaken.CompareTo(Right.DamageTaken); }));
            CombatantData.ColumnDefs.Add("Deaths", new CombatantData.ColumnDef("Deaths", true, "INT", "Deaths", (Data) => { return Data.Deaths.ToString(GetIntCommas()); }, (Data) => { return Data.Deaths.ToString(); }, (Left, Right) => { return Left.Deaths.CompareTo(Right.Deaths); }));
            CombatantData.ColumnDefs.Add("ToHit%", new CombatantData.ColumnDef("ToHit%", false, "FLOAT", "ToHit", (Data) => { return Data.ToHit.ToString(GetFloatCommas()); }, (Data) => { return Data.ToHit.ToString(usCulture); }, (Left, Right) => { return Left.ToHit.CompareTo(Right.ToHit); }));
            CombatantData.ColumnDefs.Add("CritDam%", new CombatantData.ColumnDef("CritDam%", false, "VARCHAR(8)", "CritDamPerc", (Data) => { return Data.CritDamPerc.ToString("0'%"); }, (Data) => { return Data.CritDamPerc.ToString("0'%"); }, (Left, Right) => { return Left.CritDamPerc.CompareTo(Right.CritDamPerc); }));
            CombatantData.ColumnDefs.Add("CritHeal%", new CombatantData.ColumnDef("CritHeal%", false, "VARCHAR(8)", "CritHealPerc", (Data) => { return Data.CritHealPerc.ToString("0'%"); }, (Data) => { return Data.CritHealPerc.ToString("0'%"); }, (Left, Right) => { return Left.CritHealPerc.CompareTo(Right.CritHealPerc); }));
            CombatantData.ColumnDefs.Add("Threat +/-", new CombatantData.ColumnDef("Threat +/-", false, "VARCHAR(32)", "ThreatStr", (Data) => { return Data.GetThreatStr("Threat (Out)"); }, (Data) => { return Data.GetThreatStr("Threat (Out)"); }, (Left, Right) => { return Left.GetThreatDelta("Threat (Out)").CompareTo(Right.GetThreatDelta("Threat (Out)")); }));
            CombatantData.ColumnDefs.Add("ThreatDelta", new CombatantData.ColumnDef("ThreatDelta", false, "INT", "ThreatDelta", (Data) => { return Data.GetThreatDelta("Threat (Out)").ToString(GetIntCommas()); }, (Data) => { return Data.GetThreatDelta("Threat (Out)").ToString(); }, (Left, Right) => { return Left.GetThreatDelta("Threat (Out)").CompareTo(Right.GetThreatDelta("Threat (Out)")); }));
            CombatantData.ColumnDefs.Add("Job", new CombatantData.ColumnDef("Job", true, "VARCHAR(64)", "Name", (Data) => { return "SAM"; }, (Data) => { return "SAM"; }, (Left, Right) => { return "SAM".CompareTo("SAM"); }));



            CombatantData.ColumnDefs["Damage"].GetCellForeColor = (Data) => { return Color.DarkRed; };
            CombatantData.ColumnDefs["Damage%"].GetCellForeColor = (Data) => { return Color.DarkRed; };
            CombatantData.ColumnDefs["Healed"].GetCellForeColor = (Data) => { return Color.DarkBlue; };
            CombatantData.ColumnDefs["Healed%"].GetCellForeColor = (Data) => { return Color.DarkBlue; };
            //CombatantData.ColumnDefs["PowerDrain"].GetCellForeColor = (Data) => { return Color.DarkMagenta; };
            CombatantData.ColumnDefs["DPS"].GetCellForeColor = (Data) => { return Color.DarkRed; };
            CombatantData.ColumnDefs["EncDPS"].GetCellForeColor = (Data) => { return Color.DarkRed; };
            CombatantData.ColumnDefs["EncHPS"].GetCellForeColor = (Data) => { return Color.DarkBlue; };
            CombatantData.ColumnDefs["DamageTaken"].GetCellForeColor = (Data) => { return Color.DarkOrange; };

            CombatantData.ExportVariables.Clear();
            CombatantData.ExportVariables.Add("n", new CombatantData.TextExportFormatter("n", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-newline"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-newline"].DisplayedText, (Data, Extra) => { return "\n"; }));
            CombatantData.ExportVariables.Add("t", new CombatantData.TextExportFormatter("t", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-tab"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-tab"].DisplayedText, (Data, Extra) => { return "\t"; }));
            CombatantData.ExportVariables.Add("name", new CombatantData.TextExportFormatter("name", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-name"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-name"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "name", Extra); }));
            CombatantData.ExportVariables.Add("NAME", new CombatantData.TextExportFormatter("NAME", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-NAME"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-NAME"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "NAME", Extra); }));
            CombatantData.ExportVariables.Add("duration", new CombatantData.TextExportFormatter("duration", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-duration"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-duration"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "duration", Extra); }));
            CombatantData.ExportVariables.Add("DURATION", new CombatantData.TextExportFormatter("DURATION", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-DURATION"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-DURATION"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "DURATION", Extra); }));
            CombatantData.ExportVariables.Add("damage", new CombatantData.TextExportFormatter("damage", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-damage"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-damage"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "damage", Extra); }));
            CombatantData.ExportVariables.Add("damage-m", new CombatantData.TextExportFormatter("damage-m", "Damage M", "Damage divided by 1,000,000 (with two decimal places)", (Data, Extra) => { return CombatantFormatSwitch(Data, "damage-m", Extra); }));
            CombatantData.ExportVariables.Add("DAMAGE-k", new CombatantData.TextExportFormatter("DAMAGE-k", "Short Damage K", "Damage divided by 1,000 (with no decimal places)", (Data, Extra) => { return CombatantFormatSwitch(Data, "DAMAGE-k", Extra); }));
            CombatantData.ExportVariables.Add("DAMAGE-m", new CombatantData.TextExportFormatter("DAMAGE-m", "Short Damage M", "Damage divided by 1,000,000 (with no decimal places)", (Data, Extra) => { return CombatantFormatSwitch(Data, "DAMAGE-m", Extra); }));
            CombatantData.ExportVariables.Add("damage%", new CombatantData.TextExportFormatter("damage%", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-damage%"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-damage%"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "damage%", Extra); }));
            CombatantData.ExportVariables.Add("dps", new CombatantData.TextExportFormatter("dps", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-dps"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-dps"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "dps", Extra); }));
            CombatantData.ExportVariables.Add("DPS", new CombatantData.TextExportFormatter("DPS", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-DPS"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-DPS"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "DPS", Extra); }));
            CombatantData.ExportVariables.Add("DPS-k", new CombatantData.TextExportFormatter("DPS-k", "Short DPS K", "Short DPS divided by 1,000 (with no decimal places)", (Data, Extra) => { return CombatantFormatSwitch(Data, "DPS-k", Extra); }));
            CombatantData.ExportVariables.Add("encdps", new CombatantData.TextExportFormatter("encdps", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-extdps"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-extdps"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "encdps", Extra); }));
            CombatantData.ExportVariables.Add("ENCDPS", new CombatantData.TextExportFormatter("ENCDPS", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-EXTDPS"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-EXTDPS"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "ENCDPS", Extra); }));
            CombatantData.ExportVariables.Add("ENCDPS-k", new CombatantData.TextExportFormatter("ENCDPS-k", "Short Encounter DPS K", "Short Encounter DPS divided by 1,000 (with no decimal places)", (Data, Extra) => { return CombatantFormatSwitch(Data, "ENCDPS-k", Extra); }));
            CombatantData.ExportVariables.Add("hits", new CombatantData.TextExportFormatter("hits", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-hits"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-hits"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "hits", Extra); }));
            CombatantData.ExportVariables.Add("crithits", new CombatantData.TextExportFormatter("crithits", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-crithits"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-crithits"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "crithits", Extra); }));
            CombatantData.ExportVariables.Add("crithit%", new CombatantData.TextExportFormatter("crithit%", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-crithit%"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-crithit%"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "crithit%", Extra); }));
            CombatantData.ExportVariables.Add("misses", new CombatantData.TextExportFormatter("misses", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-misses"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-misses"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "misses", Extra); }));
            CombatantData.ExportVariables.Add("hitfailed", new CombatantData.TextExportFormatter("hitfailed", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-hitfailed"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-hitfailed"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "hitfailed", Extra); }));
            CombatantData.ExportVariables.Add("swings", new CombatantData.TextExportFormatter("swings", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-swings"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-swings"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "swings", Extra); }));
            CombatantData.ExportVariables.Add("tohit", new CombatantData.TextExportFormatter("tohit", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-tohit"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-tohit"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "tohit", Extra); }));
            CombatantData.ExportVariables.Add("TOHIT", new CombatantData.TextExportFormatter("TOHIT", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-TOHIT"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-TOHIT"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "TOHIT", Extra); }));
            CombatantData.ExportVariables.Add("maxhit", new CombatantData.TextExportFormatter("maxhit", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-maxhit"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-maxhit"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "maxhit", Extra); }));
            CombatantData.ExportVariables.Add("MAXHIT", new CombatantData.TextExportFormatter("MAXHIT", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-MAXHIT"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-MAXHIT"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "MAXHIT", Extra); }));
            CombatantData.ExportVariables.Add("healed", new CombatantData.TextExportFormatter("healed", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-healed"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-healed"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "healed", Extra); }));
            CombatantData.ExportVariables.Add("healed%", new CombatantData.TextExportFormatter("healed%", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-healed%"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-healed%"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "healed%", Extra); }));
            CombatantData.ExportVariables.Add("enchps", new CombatantData.TextExportFormatter("enchps", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-exthps"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-exthps"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "enchps", Extra); }));
            CombatantData.ExportVariables.Add("ENCHPS", new CombatantData.TextExportFormatter("ENCHPS", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-EXTHPS"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-EXTHPS"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "ENCHPS", Extra); }));
            CombatantData.ExportVariables.Add("ENCHPS-k", new CombatantData.TextExportFormatter("ENCHPS-k", "Short Encounter HPS K", "Short Encounter HPS divided by 1,000 (with no decimal places)", (Data, Extra) => { return CombatantFormatSwitch(Data, "ENCHPS-k", Extra); }));
            CombatantData.ExportVariables.Add("critheals", new CombatantData.TextExportFormatter("critheals", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-critheals"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-critheals"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "critheals", Extra); }));
            CombatantData.ExportVariables.Add("critheal%", new CombatantData.TextExportFormatter("critheal%", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-critheal%"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-critheal%"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "critheal%", Extra); }));
            CombatantData.ExportVariables.Add("heals", new CombatantData.TextExportFormatter("heals", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-heals"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-heals"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "heals", Extra); }));
            //            CombatantData.ExportVariables.Add("cures", new CombatantData.TextExportFormatter("cures", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-cures"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-cures"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "cures", Extra); }));
            CombatantData.ExportVariables.Add("maxheal", new CombatantData.TextExportFormatter("maxheal", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-maxheal"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-maxheal"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "maxheal", Extra); }));
            CombatantData.ExportVariables.Add("MAXHEAL", new CombatantData.TextExportFormatter("MAXHEAL", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-MAXHEAL"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-MAXHEAL"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "MAXHEAL", Extra); }));
            //            CombatantData.ExportVariables.Add("maxhealward", new CombatantData.TextExportFormatter("maxhealward", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-maxhealward"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-maxhealward"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "maxhealward", Extra); }));
            //            CombatantData.ExportVariables.Add("MAXHEALWARD", new CombatantData.TextExportFormatter("MAXHEALWARD", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-MAXHEALWARD"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-MAXHEALWARD"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "MAXHEALWARD", Extra); }));
            CombatantData.ExportVariables.Add("damagetaken", new CombatantData.TextExportFormatter("damagetaken", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-damagetaken"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-damagetaken"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "damagetaken", Extra); }));
            CombatantData.ExportVariables.Add("healstaken", new CombatantData.TextExportFormatter("healstaken", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-healstaken"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-healstaken"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "healstaken", Extra); }));
            //            CombatantData.ExportVariables.Add("powerdrain", new CombatantData.TextExportFormatter("powerdrain", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-powerdrain"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-powerdrain"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "powerdrain", Extra); }));
            //            CombatantData.ExportVariables.Add("powerheal", new CombatantData.TextExportFormatter("powerheal", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-powerheal"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-powerheal"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "powerheal", Extra); }));
            CombatantData.ExportVariables.Add("kills", new CombatantData.TextExportFormatter("kills", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-kills"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-kills"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "kills", Extra); }));
            CombatantData.ExportVariables.Add("deaths", new CombatantData.TextExportFormatter("deaths", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-deaths"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-deaths"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "deaths", Extra); }));
            CombatantData.ExportVariables.Add("threatstr", new CombatantData.TextExportFormatter("threatstr", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-threatstr"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-threatstr"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "threatstr", Extra); }));
            CombatantData.ExportVariables.Add("threatdelta", new CombatantData.TextExportFormatter("threatdelta", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-threatdelta"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-threatdelta"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "threatdelta", Extra); }));
            CombatantData.ExportVariables.Add("NAME3", new CombatantData.TextExportFormatter("NAME3", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-NAME3"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-NAME3"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "NAME3", Extra); }));
            CombatantData.ExportVariables.Add("NAME4", new CombatantData.TextExportFormatter("NAME4", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-NAME4"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-NAME4"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "NAME4", Extra); }));
            CombatantData.ExportVariables.Add("NAME5", new CombatantData.TextExportFormatter("NAME5", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-NAME5"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-NAME5"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "NAME5", Extra); }));
            CombatantData.ExportVariables.Add("NAME6", new CombatantData.TextExportFormatter("NAME6", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-NAME6"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-NAME6"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "NAME6", Extra); }));
            CombatantData.ExportVariables.Add("NAME7", new CombatantData.TextExportFormatter("NAME7", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-NAME7"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-NAME7"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "NAME7", Extra); }));
            CombatantData.ExportVariables.Add("NAME8", new CombatantData.TextExportFormatter("NAME8", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-NAME8"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-NAME8"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "NAME8", Extra); }));
            CombatantData.ExportVariables.Add("NAME9", new CombatantData.TextExportFormatter("NAME9", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-NAME9"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-NAME9"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "NAME9", Extra); }));
            CombatantData.ExportVariables.Add("NAME10", new CombatantData.TextExportFormatter("NAME10", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-NAME10"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-NAME10"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "NAME10", Extra); }));
            CombatantData.ExportVariables.Add("NAME11", new CombatantData.TextExportFormatter("NAME11", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-NAME11"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-NAME11"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "NAME11", Extra); }));
            CombatantData.ExportVariables.Add("NAME12", new CombatantData.TextExportFormatter("NAME12", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-NAME12"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-NAME12"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "NAME12", Extra); }));
            CombatantData.ExportVariables.Add("NAME13", new CombatantData.TextExportFormatter("NAME13", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-NAME13"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-NAME13"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "NAME13", Extra); }));
            CombatantData.ExportVariables.Add("NAME14", new CombatantData.TextExportFormatter("NAME14", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-NAME14"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-NAME14"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "NAME14", Extra); }));
            CombatantData.ExportVariables.Add("NAME15", new CombatantData.TextExportFormatter("NAME15", ActGlobals.ActLocalization.LocalizationStrings["exportFormattingLabel-NAME15"].DisplayedText, ActGlobals.ActLocalization.LocalizationStrings["exportFormattingDesc-NAME15"].DisplayedText, (Data, Extra) => { return CombatantFormatSwitch(Data, "NAME15", Extra); }));
            CombatantData.ExportVariables.Add("Job", new CombatantData.TextExportFormatter("Job", "Importing and hardcoding to samurai type shit", "your mom guey", (Data, Extra) => { return CombatantFormatSwitch(Data, "Job", Extra); }));
            CombatantData.ExportVariables.Add("LuckyHitCount", new CombatantData.TextExportFormatter("LuckyHitCount", "lucky", "luck", (Data, Extra) => { return CombatantFormatSwitch(Data, "LuckyHitCount", Extra); }));
            CombatantData.ExportVariables.Add("fightpoint", new CombatantData.TextExportFormatter("fightpoint", "lucky", "luck", (Data, Extra) => { return CombatantFormatSwitch(Data, "fightpoint", Extra); }));


            DamageTypeData.ColumnDefs.Clear();
            DamageTypeData.ColumnDefs.Add("EncId", new DamageTypeData.ColumnDef("EncId", false, "CHAR(8)", "EncId", (Data) => { return string.Empty; }, (Data) => { return Data.Parent.Parent.EncId; }));
            DamageTypeData.ColumnDefs.Add("Combatant", new DamageTypeData.ColumnDef("Combatant", false, "VARCHAR(64)", "Combatant", (Data) => { return Data.Parent.Name; }, (Data) => { return Data.Parent.Name; }));
            DamageTypeData.ColumnDefs.Add("Grouping", new DamageTypeData.ColumnDef("Grouping", false, "VARCHAR(92)", "Grouping", (Data) => { return string.Empty; }, GetDamageTypeGrouping));
            DamageTypeData.ColumnDefs.Add("Type", new DamageTypeData.ColumnDef("Type", true, "VARCHAR(64)", "Type", (Data) => { return Data.Type; }, (Data) => { return Data.Type; }));
            DamageTypeData.ColumnDefs.Add("StartTime", new DamageTypeData.ColumnDef("StartTime", false, "TIMESTAMP", "StartTime", (Data) => { return Data.StartTime == DateTime.MaxValue ? "--:--:--" : Data.StartTime.ToString("T"); }, (Data) => { return Data.StartTime == DateTime.MaxValue ? "0000-00-00 00:00:00" : Data.StartTime.ToString("u").TrimEnd(new char[] { 'Z' }); }));
            DamageTypeData.ColumnDefs.Add("EndTime", new DamageTypeData.ColumnDef("EndTime", false, "TIMESTAMP", "EndTime", (Data) => { return Data.EndTime == DateTime.MinValue ? "--:--:--" : Data.StartTime.ToString("T"); }, (Data) => { return Data.EndTime == DateTime.MinValue ? "0000-00-00 00:00:00" : Data.StartTime.ToString("u").TrimEnd(new char[] { 'Z' }); }));
            DamageTypeData.ColumnDefs.Add("Duration", new DamageTypeData.ColumnDef("Duration", false, "INT", "Duration", (Data) => { return Data.DurationS; }, (Data) => { return Data.Duration.TotalSeconds.ToString("0"); }));
            DamageTypeData.ColumnDefs.Add("Damage", new DamageTypeData.ColumnDef("Damage", true, "BIGINT", "Damage", (Data) => { return Data.Damage.ToString(GetIntCommas()); }, (Data) => { return Data.Damage.ToString(); }));
            DamageTypeData.ColumnDefs.Add("EncDPS", new DamageTypeData.ColumnDef("EncDPS", true, "DOUBLE", "EncDPS", (Data) => { return Data.EncDPS.ToString(GetFloatCommas()); }, (Data) => { return Data.EncDPS.ToString(usCulture); }));
            DamageTypeData.ColumnDefs.Add("CharDPS", new DamageTypeData.ColumnDef("CharDPS", false, "DOUBLE", "CharDPS", (Data) => { return Data.CharDPS.ToString(GetFloatCommas()); }, (Data) => { return Data.CharDPS.ToString(usCulture); }));
            DamageTypeData.ColumnDefs.Add("DPS", new DamageTypeData.ColumnDef("DPS", false, "DOUBLE", "DPS", (Data) => { return Data.DPS.ToString(GetFloatCommas()); }, (Data) => { return Data.DPS.ToString(usCulture); }));
            DamageTypeData.ColumnDefs.Add("Average", new DamageTypeData.ColumnDef("Average", true, "FLOAT", "Average", (Data) => { return Data.Average.ToString(GetFloatCommas()); }, (Data) => { return Data.Average.ToString(usCulture); }));
            DamageTypeData.ColumnDefs.Add("Median", new DamageTypeData.ColumnDef("Median", false, "INT", "Median", (Data) => { return Data.Median.ToString(GetIntCommas()); }, (Data) => { return Data.Median.ToString(); }));
            DamageTypeData.ColumnDefs.Add("MinHit", new DamageTypeData.ColumnDef("MinHit", true, "INT", "MinHit", (Data) => { return Data.MinHit.ToString(GetIntCommas()); }, (Data) => { return Data.MinHit.ToString(); }));
            DamageTypeData.ColumnDefs.Add("MaxHit", new DamageTypeData.ColumnDef("MaxHit", true, "INT", "MaxHit", (Data) => { return Data.MaxHit.ToString(GetIntCommas()); }, (Data) => { return Data.MaxHit.ToString(); }));
            DamageTypeData.ColumnDefs.Add("Hits", new DamageTypeData.ColumnDef("Hits", true, "INT", "Hits", (Data) => { return Data.Hits.ToString(GetIntCommas()); }, (Data) => { return Data.Hits.ToString(); }));
            DamageTypeData.ColumnDefs.Add("CritHits", new DamageTypeData.ColumnDef("CritHits", false, "INT", "CritHits", (Data) => { return Data.CritHits.ToString(GetIntCommas()); }, (Data) => { return Data.CritHits.ToString(); }));
            DamageTypeData.ColumnDefs.Add("Avoids", new DamageTypeData.ColumnDef("Avoids", false, "INT", "Blocked", (Data) => { return Data.Blocked.ToString(GetIntCommas()); }, (Data) => { return Data.Blocked.ToString(); }));
            DamageTypeData.ColumnDefs.Add("Misses", new DamageTypeData.ColumnDef("Misses", false, "INT", "Misses", (Data) => { return Data.Misses.ToString(GetIntCommas()); }, (Data) => { return Data.Misses.ToString(); }));
            DamageTypeData.ColumnDefs.Add("Swings", new DamageTypeData.ColumnDef("Swings", true, "INT", "Swings", (Data) => { return Data.Swings.ToString(GetIntCommas()); }, (Data) => { return Data.Swings.ToString(); }));
            DamageTypeData.ColumnDefs.Add("ToHit", new DamageTypeData.ColumnDef("ToHit", false, "FLOAT", "ToHit", (Data) => { return Data.ToHit.ToString(GetFloatCommas()); }, (Data) => { return Data.ToHit.ToString(); }));
            DamageTypeData.ColumnDefs.Add("AvgDelay", new DamageTypeData.ColumnDef("AvgDelay", false, "FLOAT", "AverageDelay", (Data) => { return Data.AverageDelay.ToString(GetFloatCommas()); }, (Data) => { return Data.AverageDelay.ToString(); }));
            DamageTypeData.ColumnDefs.Add("Crit%", new DamageTypeData.ColumnDef("Crit%", true, "VARCHAR(8)", "CritPerc", (Data) => { return Data.CritPerc.ToString("0'%"); }, (Data) => { return Data.CritPerc.ToString("0'%"); }));


            AttackType.ColumnDefs.Clear();
            AttackType.ColumnDefs.Add("EncId", new AttackType.ColumnDef("EncId", false, "CHAR(8)", "EncId", (Data) => { return string.Empty; }, (Data) => { return Data.Parent.Parent.Parent.EncId; }, (Left, Right) => { return 0; }));
            AttackType.ColumnDefs.Add("Attacker", new AttackType.ColumnDef("Attacker", false, "VARCHAR(64)", "Attacker", (Data) => { return Data.Parent.Outgoing ? Data.Parent.Parent.Name : string.Empty; }, (Data) => { return Data.Parent.Outgoing ? Data.Parent.Parent.Name : string.Empty; }, (Left, Right) => { return 0; }));
            AttackType.ColumnDefs.Add("Victim", new AttackType.ColumnDef("Victim", false, "VARCHAR(64)", "Victim", (Data) => { return Data.Parent.Outgoing ? string.Empty : Data.Parent.Parent.Name; }, (Data) => { return Data.Parent.Outgoing ? string.Empty : Data.Parent.Parent.Name; }, (Left, Right) => { return 0; }));
            //AttackType.ColumnDefs.Add("SwingType", new AttackType.ColumnDef("SwingType", false, "TINYINT", "SwingType", GetAttackTypeSwingType, GetAttackTypeSwingType, (Left, Right) => { return 0; }));
            AttackType.ColumnDefs.Add("Type", new AttackType.ColumnDef("Type", true, "VARCHAR(64)", "Type", (Data) => { return Data.Type; }, (Data) => { return Data.Type; }, (Left, Right) => { return Left.Type.CompareTo(Right.Type); }));
            AttackType.ColumnDefs.Add("StartTime", new AttackType.ColumnDef("StartTime", false, "TIMESTAMP", "StartTime", (Data) => { return Data.StartTime == DateTime.MaxValue ? "--:--:--" : Data.StartTime.ToString("T"); }, (Data) => { return Data.StartTime == DateTime.MaxValue ? "0000-00-00 00:00:00" : Data.StartTime.ToString("u").TrimEnd(new char[] { 'Z' }); }, (Left, Right) => { return Left.StartTime.CompareTo(Right.StartTime); }));
            AttackType.ColumnDefs.Add("EndTime", new AttackType.ColumnDef("EndTime", false, "TIMESTAMP", "EndTime", (Data) => { return Data.EndTime == DateTime.MinValue ? "--:--:--" : Data.EndTime.ToString("T"); }, (Data) => { return Data.EndTime == DateTime.MinValue ? "0000-00-00 00:00:00" : Data.EndTime.ToString("u").TrimEnd(new char[] { 'Z' }); }, (Left, Right) => { return Left.EndTime.CompareTo(Right.EndTime); }));
            AttackType.ColumnDefs.Add("Duration", new AttackType.ColumnDef("Duration", false, "INT", "Duration", (Data) => { return Data.DurationS; }, (Data) => { return Data.Duration.TotalSeconds.ToString("0"); }, (Left, Right) => { return Left.Duration.CompareTo(Right.Duration); }));
            AttackType.ColumnDefs.Add("Damage", new AttackType.ColumnDef("Damage", true, "BIGINT", "Damage", (Data) => { return Data.Damage.ToString(GetIntCommas()); }, (Data) => { return Data.Damage.ToString(); }, (Left, Right) => { return Left.Damage.CompareTo(Right.Damage); }));
            AttackType.ColumnDefs.Add("EncDPS", new AttackType.ColumnDef("EncDPS", true, "DOUBLE", "EncDPS", (Data) => { return Data.EncDPS.ToString(GetFloatCommas()); }, (Data) => { return Data.EncDPS.ToString(usCulture); }, (Left, Right) => { return Left.EncDPS.CompareTo(Right.EncDPS); }));
            AttackType.ColumnDefs.Add("CharDPS", new AttackType.ColumnDef("CharDPS", false, "DOUBLE", "CharDPS", (Data) => { return Data.CharDPS.ToString(GetFloatCommas()); }, (Data) => { return Data.CharDPS.ToString(usCulture); }, (Left, Right) => { return Left.CharDPS.CompareTo(Right.CharDPS); }));
            AttackType.ColumnDefs.Add("DPS", new AttackType.ColumnDef("DPS", false, "DOUBLE", "DPS", (Data) => { return Data.DPS.ToString(GetFloatCommas()); }, (Data) => { return Data.DPS.ToString(usCulture); }, (Left, Right) => { return Left.DPS.CompareTo(Right.DPS); }));
            AttackType.ColumnDefs.Add("Average", new AttackType.ColumnDef("Average", true, "FLOAT", "Average", (Data) => { return Data.Average.ToString(GetFloatCommas()); }, (Data) => { return Data.Average.ToString(usCulture); }, (Left, Right) => { return Left.Average.CompareTo(Right.Average); }));
            AttackType.ColumnDefs.Add("Median", new AttackType.ColumnDef("Median", true, "INT", "Median", (Data) => { return Data.Median.ToString(GetIntCommas()); }, (Data) => { return Data.Median.ToString(); }, (Left, Right) => { return Left.Median.CompareTo(Right.Median); }));
            AttackType.ColumnDefs.Add("MinHit", new AttackType.ColumnDef("MinHit", true, "INT", "MinHit", (Data) => { return Data.MinHit.ToString(GetIntCommas()); }, (Data) => { return Data.MinHit.ToString(); }, (Left, Right) => { return Left.MinHit.CompareTo(Right.MinHit); }));
            AttackType.ColumnDefs.Add("MaxHit", new AttackType.ColumnDef("MaxHit", true, "INT", "MaxHit", (Data) => { return Data.MaxHit.ToString(GetIntCommas()); }, (Data) => { return Data.MaxHit.ToString(); }, (Left, Right) => { return Left.MaxHit.CompareTo(Right.MaxHit); }));

            ActGlobals.oFormActMain.ValidateLists();
            ActGlobals.oFormActMain.ValidateTableSetup();
            ActGlobals.oFormActMain.TimeStampLen = 14;
        }

        private string GetDisplayName(CombatantData Data)
        {
            string displayName = Data.Name;
            var split = new string[2];
            var dName = Data.Name;

            if (displayName.StartsWith("#"))
            {
                displayName = displayName.Substring(1);
            }

            if (displayName.Contains("#"))
            {
                split = displayName.Split('#');
            }

            if (!String.IsNullOrEmpty(split[1]))
            {
                if (!entityCache.ContainsKey(split[1]))
                {
                    entityCache.Add(split[1], split[0]);
                }
                dName = entityCache[split[1]];
            }

            if (entityCache.ContainsKey(displayName))
            {
                dName = entityCache[displayName];
            }


            return dName;
        }

        private string EncounterFormatSwitch(EncounterData Data, List<CombatantData> SelectiveAllies, string VarName, string Extra)
        {
            long damage = 0;
            long healed = 0;
            int swings = 0;
            int hits = 0;
            int crits = 0;
            int heals = 0;
            int critheals = 0;
            int misses = 0;
            int hitfail = 0;
            float tohit = 0;
            double dps = 0;
            double hps = 0;
            long healstaken = 0;
            long damagetaken = 0;
            int kills = 0;
            int deaths = 0;

            switch (VarName)
            {
                case "maxheal":
                    return Data.GetMaxHeal(true, false);
                case "MAXHEAL":
                    return Data.GetMaxHeal(false, false);
                case "maxhealward":
                    return Data.GetMaxHeal(true, true);
                case "MAXHEALWARD":
                    return Data.GetMaxHeal(false, true);
                case "maxhit":
                    return Data.GetMaxHit(true);
                case "MAXHIT":
                    return Data.GetMaxHit(false);
                case "duration":
                    return Data.DurationS;
                case "DURATION":
                    return Data.Duration.TotalSeconds.ToString("0");
                case "damage":
                    foreach (CombatantData cd in SelectiveAllies)
                        damage += cd.Damage;
                    return damage.ToString();
                case "damage-m":
                    foreach (CombatantData cd in SelectiveAllies)
                        damage += cd.Damage;
                    return (damage / 1000000.0).ToString("0.00");
                case "DAMAGE-k":
                    foreach (CombatantData cd in SelectiveAllies)
                        damage += cd.Damage;
                    return (damage / 1000.0).ToString("0");
                case "DAMAGE-m":
                    foreach (CombatantData cd in SelectiveAllies)
                        damage += cd.Damage;
                    return (damage / 1000000.0).ToString("0");
                case "healed":
                    foreach (CombatantData cd in SelectiveAllies)
                        healed += cd.Healed;
                    return healed.ToString();
                case "swings":
                    foreach (CombatantData cd in SelectiveAllies)
                        swings += cd.Swings;
                    return swings.ToString();
                case "hits":
                    foreach (CombatantData cd in SelectiveAllies)
                        hits += cd.Hits;
                    return hits.ToString();
                case "crithits":
                    foreach (CombatantData cd in SelectiveAllies)
                        crits += cd.CritHits;
                    return crits.ToString();
                case "crithit%":
                    foreach (CombatantData cd in SelectiveAllies)
                        crits += cd.CritHits;
                    foreach (CombatantData cd in SelectiveAllies)
                        hits += cd.Hits;
                    float critdamperc = (float)crits / (float)hits;
                    return critdamperc.ToString("0'%");
                case "heals":
                    foreach (CombatantData cd in SelectiveAllies)
                        heals += cd.Heals;
                    return heals.ToString();
                case "critheals":
                    foreach (CombatantData cd in SelectiveAllies)
                        critheals += cd.CritHits;
                    return critheals.ToString();
                case "critheal%":
                    foreach (CombatantData cd in SelectiveAllies)
                        critheals += cd.CritHeals;
                    foreach (CombatantData cd in SelectiveAllies)
                        heals += cd.Heals;
                    float crithealperc = (float)critheals / (float)heals;
                    return crithealperc.ToString("0'%");
                case "misses":
                    foreach (CombatantData cd in SelectiveAllies)
                        misses += cd.Misses;
                    return misses.ToString();
                case "hitfailed":
                    foreach (CombatantData cd in SelectiveAllies)
                        hitfail += cd.Blocked;
                    return hitfail.ToString();
                case "TOHIT":
                    foreach (CombatantData cd in SelectiveAllies)
                        tohit += cd.ToHit;
                    tohit /= SelectiveAllies.Count;
                    return tohit.ToString("0");
                case "DPS":
                case "ENCDPS":
                    foreach (CombatantData cd in SelectiveAllies)
                        damage += cd.Damage;
                    dps = damage / Data.Duration.TotalSeconds;
                    return dps.ToString("0");
                case "DPS-k":
                case "ENCDPS-k":
                    foreach (CombatantData cd in SelectiveAllies)
                        damage += cd.Damage;
                    dps = damage / Data.Duration.TotalSeconds;
                    return (dps / 1000.0).ToString("0");
                case "ENCHPS":
                    foreach (CombatantData cd in SelectiveAllies)
                        healed += cd.Healed;
                    hps = healed / Data.Duration.TotalSeconds;
                    return hps.ToString("0");
                case "ENCHPS-k":
                    foreach (CombatantData cd in SelectiveAllies)
                        healed += cd.Healed;
                    hps = healed / Data.Duration.TotalSeconds;
                    return (hps / 1000.0).ToString("0");
                case "tohit":
                    foreach (CombatantData cd in SelectiveAllies)
                        tohit += cd.ToHit;
                    tohit /= SelectiveAllies.Count;
                    return tohit.ToString("F");
                case "dps":
                case "encdps":
                    foreach (CombatantData cd in SelectiveAllies)
                        damage += cd.Damage;
                    dps = damage / Data.Duration.TotalSeconds;
                    return dps.ToString("F");
                case "dps-k":
                case "encdps-k":
                    foreach (CombatantData cd in SelectiveAllies)
                        damage += cd.Damage;
                    dps = damage / Data.Duration.TotalSeconds;
                    return (dps / 1000.0).ToString("F");
                case "enchps":
                    foreach (CombatantData cd in SelectiveAllies)
                        healed += cd.Healed;
                    hps = healed / Data.Duration.TotalSeconds;
                    return hps.ToString("F");
                case "enchps-k":
                    foreach (CombatantData cd in SelectiveAllies)
                        healed += cd.Healed;
                    hps = healed / Data.Duration.TotalSeconds;
                    return (hps / 1000.0).ToString("F");
                case "healstaken":
                    foreach (CombatantData cd in SelectiveAllies)
                        healstaken += cd.HealsTaken;
                    return healstaken.ToString();
                case "damagetaken":
                    foreach (CombatantData cd in SelectiveAllies)
                        damagetaken += cd.DamageTaken;
                    return damagetaken.ToString();
                case "kills":
                    foreach (CombatantData cd in SelectiveAllies)
                        kills += cd.Kills;
                    return kills.ToString();
                case "deaths":
                    foreach (CombatantData cd in SelectiveAllies)
                        deaths += cd.Deaths;
                    return deaths.ToString();
                case "title":
                    return $"{Data.ZoneName} - {Data.Title}";

                default:
                    return VarName;
            }
        }
        private string CombatantFormatSwitch(CombatantData Data, string VarName, string Extra)
        {
            var displayName = GetDisplayName(Data);

            int len = 0;
            switch (VarName)
            {
                case "name":
                    return displayName;
                case "NAME":
                    len = Int32.Parse(Extra);
                    return displayName.Length - len > 0 ? displayName.Remove(len, displayName.Length - len).Trim() : displayName;
                case "NAME3":
                    len = 3;
                    return displayName.Length - len > 0 ? displayName.Remove(len, displayName.Length - len).Trim() : displayName;
                case "NAME4":
                    len = 4;
                    return displayName.Length - len > 0 ? displayName.Remove(len, displayName.Length - len).Trim() : displayName;
                case "NAME5":
                    len = 5;
                    return displayName.Length - len > 0 ? displayName.Remove(len, displayName.Length - len).Trim() : displayName;
                case "NAME6":
                    len = 6;
                    return displayName.Length - len > 0 ? displayName.Remove(len, displayName.Length - len).Trim() : displayName;
                case "NAME7":
                    len = 7;
                    return displayName.Length - len > 0 ? displayName.Remove(len, displayName.Length - len).Trim() : displayName;
                case "NAME8":
                    len = 8;
                    return displayName.Length - len > 0 ? displayName.Remove(len, displayName.Length - len).Trim() : displayName;
                case "NAME9":
                    len = 9;
                    return displayName.Length - len > 0 ? displayName.Remove(len, displayName.Length - len).Trim() : displayName;
                case "NAME10":
                    len = 10;
                    return displayName.Length - len > 0 ? displayName.Remove(len, displayName.Length - len).Trim() : displayName;
                case "NAME11":
                    len = 11;
                    return displayName.Length - len > 0 ? displayName.Remove(len, displayName.Length - len).Trim() : displayName;
                case "NAME12":
                    len = 12;
                    return displayName.Length - len > 0 ? displayName.Remove(len, displayName.Length - len).Trim() : displayName;
                case "NAME13":
                    len = 13;
                    return displayName.Length - len > 0 ? displayName.Remove(len, displayName.Length - len).Trim() : displayName;
                case "NAME14":
                    len = 14;
                    return displayName.Length - len > 0 ? displayName.Remove(len, displayName.Length - len).Trim() : displayName;
                case "NAME15":
                    len = 15;
                    return displayName.Length - len > 0 ? displayName.Remove(len, displayName.Length - len).Trim() : displayName;
                case "DURATION":
                    return Data.Duration.TotalSeconds.ToString("0");
                case "duration":
                    return Data.DurationS;
                case "maxhit":
                    return Data.GetMaxHit(true);
                case "MAXHIT":
                    return Data.GetMaxHit(false);
                case "maxheal":
                    return Data.GetMaxHeal(true, false);
                case "MAXHEAL":
                    return Data.GetMaxHeal(false, false);
                case "maxhealward":
                    return Data.GetMaxHeal(true, true);
                case "MAXHEALWARD":
                    return Data.GetMaxHeal(false, true);
                case "damage":
                    return Data.Damage.ToString();
                case "damage-k":
                    return (Data.Damage / 1000.0).ToString("0.00");
                case "damage-m":
                    return (Data.Damage / 1000000.0).ToString("0.00");
                case "DAMAGE-k":
                    return (Data.Damage / 1000.0).ToString("0");
                case "DAMAGE-m":
                    return (Data.Damage / 1000000.0).ToString("0");
                case "healed":
                    return Data.Healed.ToString();
                case "swings":
                    return Data.Swings.ToString();
                case "hits":
                    return Data.Hits.ToString();
                case "crithits":
                    return Data.CritHits.ToString();
                case "critheals":
                    return Data.CritHeals.ToString();
                case "crithit%":
                    return Data.CritDamPerc.ToString("0'%");
                case "critheal%":
                    return Data.CritHealPerc.ToString("0'%");
                case "heals":
                    return Data.Heals.ToString();
                //                case "cures":
                //                    return Data.CureDispels.ToString();
                case "misses":
                    return Data.Misses.ToString();
                case "hitfailed":
                    return Data.Blocked.ToString();
                case "TOHIT":
                    return Data.ToHit.ToString("0");
                case "DPS":
                    return Data.DPS.ToString("0");
                case "DPS-k":
                    return (Data.DPS / 1000.0).ToString("0");
                case "ENCDPS":
                    return Data.EncDPS.ToString("0");
                case "ENCDPS-k":
                    return (Data.EncDPS / 1000.0).ToString("0");
                case "ENCHPS":
                    return Data.EncHPS.ToString("0");
                case "ENCHPS-k":
                    return (Data.EncHPS / 1000.0).ToString("0");
                case "tohit":
                    return Data.ToHit.ToString("F");
                case "dps":
                    return Data.DPS.ToString("F");
                case "dps-k":
                    return (Data.DPS / 1000.0).ToString("F");
                case "encdps":
                    return Data.EncDPS.ToString("F");
                case "encdps-k":
                    return (Data.EncDPS / 1000.0).ToString("F");
                case "enchps":
                    return Data.EncHPS.ToString("F");
                case "enchps-k":
                    return (Data.EncHPS / 1000.0).ToString("F");
                case "healstaken":
                    return Data.HealsTaken.ToString();
                case "damagetaken":
                    return Data.DamageTaken.ToString();
                case "kills":
                    return Data.Kills.ToString();
                case "deaths":
                    return Data.Deaths.ToString();
                case "damage%":
                    return Data.DamagePercent;
                case "healed%":
                    return Data.HealedPercent;
                case "threatstr":
                    return Data.GetThreatStr("Threat (Out)");
                case "threatdelta":
                    return Data.GetThreatDelta("Threat (Out)").ToString();
                case "n":
                    return "\n";
                case "t":
                    return "\t";
                case "Job":


                    return "Stormblade";

                case "fightpoint":
                    return "1234";

                case "LuckyHitCount":
                    return "0";

                default:
                    return VarName;
            }
        }

        private string GetDamageTypeGrouping(DamageTypeData Data)
        {
            string grouping = string.Empty;

            int swingTypeIndex = 0;
            if (Data.Outgoing)
            {
                grouping += "attacker=" + Data.Parent.Name;
                foreach (KeyValuePair<int, List<string>> links in CombatantData.SwingTypeToDamageTypeDataLinksOutgoing)
                {
                    foreach (string damageTypeLabel in links.Value)
                    {
                        if (Data.Type == damageTypeLabel)
                        {
                            grouping += String.Format("&swingtype{0}={1}", swingTypeIndex++ == 0 ? string.Empty : swingTypeIndex.ToString(), links.Key);
                        }
                    }
                }
            }
            else
            {
                grouping += "victim=" + Data.Parent.Name;
                foreach (KeyValuePair<int, List<string>> links in CombatantData.SwingTypeToDamageTypeDataLinksIncoming)
                {
                    foreach (string damageTypeLabel in links.Value)
                    {
                        if (Data.Type == damageTypeLabel)
                        {
                            grouping += String.Format("&swingtype{0}={1}", swingTypeIndex++ == 0 ? string.Empty : swingTypeIndex.ToString(), links.Key);
                        }
                    }
                }
            }

            return grouping;
        }

        private void ParseLine(bool isImport, LogLineEventArgs log)
        {
            ActGlobals.oFormActMain.GlobalTimeSorter++;
            DateTime time = ActGlobals.oFormActMain.LastKnownTime;


            NumLinesRead++;
            String line = log.logLine;
            if (line.StartsWith("}"))
            {
                line = line.Substring(1);
            }
            //BPSR_Line_Parser temp = bpsrPreviousLine;
            //bpsrPreviousLine = bpsrLineParser;
            //bpsrLineParser = temp;

            if (!bpsrLineParser.Parse(line))
            {
                // PARSE FAILED
                log.detectedType = Color.Pink.ToArgb();
                NumLinesFailedParse++;
                return;
            }

            //SetDisplayName(ref bpsrLineParser.source);
            //SetDisplayName(ref bpsrLineParser.target);
            bpsrLineParser.source.DisplayName = bpsrLineParser.source.Name;
            bpsrLineParser.target.DisplayName = bpsrLineParser.target.Name;
            //ActGlobals.charName = bpsrLineParser.source.DisplayName;


            if (bpsrLineParser.action1.type == LogEventIds.EVENT_PLAYER_DIE.ToString())
            {

                encounter.CombatEvent(bpsrLineParser, isImport, time);

                if (!ActGlobals.oFormActMain.InCombat)
                {
                    return;
                }

                if (!ActGlobals.oFormActMain.SetEncounter(time, bpsrLineParser.source.Name, bpsrLineParser.target.Name))
                {
                    return;
                }

                var mSwing = new MasterSwing(
                    DMG,
                    bpsrLineParser.action1.modifier?.Contains("Crit") ?? false,
                    Dnum.Death,
                    DateTime.Now,
                    ActGlobals.oFormActMain.GlobalTimeSorter,
                    bpsrLineParser.action1.name,
                    bpsrLineParser.source.Name,
                    bpsrLineParser.action1.element,
                    bpsrLineParser.target.Name
                );
                ActGlobals.oFormActMain.AddCombatAction(mSwing);
            }
            if (bpsrLineParser.action1.type == LogEventIds.EVENT_ZONE_LOAD.ToString())
            {
                if (ActGlobals.oFormActMain.InCombat)
                {
                    encounter.ExitCombat(bpsrLineParser, false, time);
                }
                lock (locker)
                {
                    if (!ActGlobals.oFormActMain.CurrentZone.Equals(bpsrLineParser.action1.name))
                    {
                        ActGlobals.oFormActMain.BeginInvoke(new MethodInvoker(delegate
                        {
                            ActGlobals.oFormActMain.ChangeZone(bpsrLineParser.action1.name);
                        }));
                    }
                }
                npcInstances.Clear();
            }
            if (bpsrLineParser.action1.type == LogEventIds.EVENT_DAMAGE.ToString())
            {
                log.detectedType = (bpsrLineParser.source.Name == ActGlobals.charName) ?
                        Color.DarkRed.ToArgb() :
                        Color.Red.ToArgb();
                ParseDamage(false, time);
            }
            if (bpsrLineParser.action1.type == LogEventIds.EVENT_HEAL.ToString())
            {
                if (!ActGlobals.oFormActMain.InCombat)
                    return;
                ParseHealing(false, time);
            }

            encounter.TimeoutCheck(isImport, time);
        }
        public void ParseDamage(bool isImport, DateTime time)
        {
            encounter.CombatEvent(bpsrLineParser, isImport, time);

            if (!ActGlobals.oFormActMain.InCombat)
            {
                //return;
                encounter.EnterCombat(bpsrLineParser, isImport, time);
            }

            if (!ActGlobals.oFormActMain.SetEncounter(time, bpsrLineParser.source.Name, bpsrLineParser.target.Name))
            {
                return;
            }

            Dnum dnum = null;
            dnum = new Dnum(bpsrLineParser.action1.dmgValue);

            var mSwing = new MasterSwing(
                DMG,
                bpsrLineParser.action1.modifier?.Contains("Crit") ?? false,
                dnum,
                DateTime.Now,
                ActGlobals.oFormActMain.GlobalTimeSorter,
                bpsrLineParser.action1.name,
                bpsrLineParser.source.Name,
                bpsrLineParser.action1.element,
                bpsrLineParser.target.Name
            );
            ActGlobals.oFormActMain.AddCombatAction(mSwing);
        }
        public void ParseHealing(bool isImport, DateTime time)
        {
            encounter.CombatEvent(bpsrLineParser, isImport, time);

            if (!ActGlobals.oFormActMain.InCombat)
            {
                return;
            }

            if (!ActGlobals.oFormActMain.SetEncounter(time, bpsrLineParser.source.Name, bpsrLineParser.target.Name))
            {
                return;
            }

            Dnum dnum = null;
            dnum = new Dnum(bpsrLineParser.action1.dmgValue);

            var mSwing = new MasterSwing(
                HEALS,
                bpsrLineParser.action1.modifier?.Contains("Crit") ?? false,
                dnum,
                DateTime.Now,
                ActGlobals.oFormActMain.GlobalTimeSorter,
                bpsrLineParser.action1.name,
                bpsrLineParser.source.Name,
                bpsrLineParser.action1.element,
                bpsrLineParser.target.Name
            );
            ActGlobals.oFormActMain.AddCombatAction(mSwing);
        }
        void oFormActMain_LogFileChanged(bool IsImport, string NewLogFileName)
        {
            bpsrLineParser.Reset();
            bpsrPreviousLine.Reset();
            npcInstances.Clear();
            return;
        }

        private void WatchLogFolder()
        {
            var fsWatcher = new FileSystemWatcher
            {
                Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "bpsr_pso\\app-2.0.1\\logs\\"),
                EnableRaisingEvents = true,
                IncludeSubdirectories = true
            };

            fsWatcher.Created += new FileSystemEventHandler(HandleFSChange);
            //fsWatcher.Changed += new FileSystemEventHandler(HandleFSChange);
        }
        private void HandleFSChange(object sender, FileSystemEventArgs e)
        {
            string logFullPath = Path.Combine(e.FullPath, "fight.log");
            string jsonFullpath = Path.Combine(e.FullPath, "allUserData.json");

            do
            {
                if (File.Exists(logFullPath))
                {
                    ActGlobals.oFormActMain.LogFilePath = logFullPath;
                    ActGlobals.oFormActMain.OpenLog(false, false);
                    break;
                }
            } while (!File.Exists(logFullPath));
            //do
            //{
            //    if (File.Exists(jsonFullpath))
            //    {

            //        allPlayersJSONFile = JObject.Parse(File.ReadAllText(jsonFullpath));
            //        break;
            //    }
            //} while (!File.Exists(jsonFullpath));
        }
        void LoadSettings()
        {
            // Add any controls you want to save the state of
            xmlSettings.AddControlSetting(ui_logfileParentFolder.Name, ui_logfileParentFolder);

            if (File.Exists(settingsFile))
            {
                FileStream fs = new FileStream(settingsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                XmlTextReader xReader = new XmlTextReader(fs);

                try
                {
                    while (xReader.Read())
                    {
                        if (xReader.NodeType == XmlNodeType.Element)
                        {
                            if (xReader.LocalName == "SettingsSerializer")
                            {
                                xmlSettings.ImportFromXml(xReader);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    lblStatus.Text = "Error loading settings: " + ex.Message;
                }
                xReader.Close();
            }
        }
        void SaveSettings()
        {
            FileStream fs = new FileStream(settingsFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            XmlTextWriter xWriter = new XmlTextWriter(fs, Encoding.UTF8);
            xWriter.Formatting = System.Xml.Formatting.Indented;
            xWriter.Indentation = 1;
            xWriter.IndentChar = '\t';
            xWriter.WriteStartDocument(true);
            xWriter.WriteStartElement("Config");    // <Config>
            xWriter.WriteStartElement("SettingsSerializer");    // <Config><SettingsSerializer>
            xmlSettings.ExportToXml(xWriter);   // Fill the SettingsSerializer XML
            xWriter.WriteEndElement();  // </SettingsSerializer>
            xWriter.WriteEndElement();  // </Config>
            xWriter.WriteEndDocument(); // Tie up loose ends (shouldn't be any)
            xWriter.Flush();    // Flush the file buffer to disk
            xWriter.Close();
        }

    }
    class BPSR_Encounter
    {
        BPSR_Line_Parser bpsrLineParser;
        bool isImport;
        DateTime time;

        public bool hasCombatStartedYet = false;
        private EncounterStateBase state = EncounterStateOutOfCombat.Instance();
        private DateTime? lastEncounterEvent;
        private TimeSpan timeout = new TimeSpan(0, 0, 6);
        public delegate void EncounterEvent(DateTime time);
        public event EncounterEvent BeforeEndCombat;

        public void EnterCombat(BPSR_Line_Parser bpsrLineParser, bool isImport, DateTime time)
        {
            hasCombatStartedYet = true;
            this.bpsrLineParser = bpsrLineParser;
            this.isImport = isImport;
            this.time = time;
            state.EnterCombat(this);
        }

        public void ExitCombat(BPSR_Line_Parser bpsrLineParser, bool isImport, DateTime time)
        {
            hasCombatStartedYet = false;
            this.bpsrLineParser = bpsrLineParser;
            this.isImport = isImport;
            this.time = time;
            state.ExitCombat(this);
        }
        public void CombatEvent(BPSR_Line_Parser bpsrLineParser, bool isImport, DateTime time)
        {
            this.bpsrLineParser = bpsrLineParser;
            this.isImport = isImport;
            this.time = time;
            state.CombatEvent(this);
        }
        public void TimeoutCheck(bool isImport, DateTime time)
        {
            this.isImport = isImport;
            this.time = time;
            state.TimeoutCheck(this);
        }

        public void Reset()
        {
            state = EncounterStateOutOfCombat.Instance();
        }

        private class EncounterStateBase
        {
            protected virtual void EnterState() { }

            public virtual void EnterCombat(BPSR_Encounter enc) { }
            public virtual void ExitCombat(BPSR_Encounter enc) { }
            public virtual void CombatEvent(BPSR_Encounter enc) { }
            public virtual void TimeoutCheck(BPSR_Encounter enc) { }
            public virtual void Timeout() { }
        }

        private class EncounterStateOutOfCombat : EncounterStateBase
        {
            public static EncounterStateOutOfCombat Instance() { return instance; }
            private static EncounterStateOutOfCombat instance = new EncounterStateOutOfCombat();

            public override void EnterCombat(BPSR_Encounter enc)
            {

                if (ActGlobals.oFormActMain.InCombat)
                {
                    ActGlobals.oFormActMain.EndCombat(!enc.isImport);
                }
                try
                {
                    if (ActGlobals.oFormActMain.SetEncounter(enc.time, enc.bpsrLineParser.source.Name, enc.bpsrLineParser.target.Name))
                    {
                        if (ActGlobals.oFormActMain.InCombat)
                        {
                            enc.state = EncounterStateInCombat.Instance();
                        }
                    }
                }
                catch { }
            }
        }

        private class EncounterStateInCombat : EncounterStateBase
        {
            public static EncounterStateInCombat Instance() { return instance; }
            private static EncounterStateInCombat instance = new EncounterStateInCombat();
            public override void ExitCombat(BPSR_Encounter enc)
            {
                if (ActGlobals.oFormActMain.InCombat)
                {
                    enc.lastEncounterEvent = enc.time;
                    enc.state = EncounterStateSoftCombat.Instance();

                    if (!enc.isImport)
                    {
                        //timer.Start();
                    }
                }
                else
                {
                    enc.state = EncounterStateOutOfCombat.Instance();
                }
            }
        }

        private class EncounterStateSoftCombat : EncounterStateBase
        {
            public static EncounterStateSoftCombat Instance() { return instance; }
            private static EncounterStateSoftCombat instance = new EncounterStateSoftCombat();

            public override void EnterCombat(BPSR_Encounter enc)
            {
                TimeSpan diff = (enc.lastEncounterEvent is null) ? new TimeSpan() : enc.time - (DateTime)enc.lastEncounterEvent;

                // timer.Stop();

                if (diff > enc.timeout)
                {
                    // Too long - new encounter.
                    ActGlobals.oFormActMain.EndCombat(!enc.isImport);
                    ActGlobals.oFormActMain.SetEncounter(enc.time, enc.bpsrLineParser.source.Name, enc.bpsrLineParser.target.Name);
                    if (ActGlobals.oFormActMain.InCombat)
                    {
                        enc.state = EncounterStateInCombat.Instance();
                    }
                    else
                    {
                        enc.state = EncounterStateOutOfCombat.Instance();
                    }
                }
                else
                {
                    enc.state = EncounterStateInCombat.Instance();
                }
            }

            public override void CombatEvent(BPSR_Encounter enc)
            {
                TimeSpan diff = (enc.lastEncounterEvent is null) ? new TimeSpan() : enc.time - (DateTime)enc.lastEncounterEvent;

                // timer.Stop();

                if (diff > enc.timeout)
                {
                    // Too long - end encounter.

                    DateTime endTime = (DateTime)(enc.lastEncounterEvent + enc.timeout);
                    enc.BeforeEndCombat(endTime);

                    ActGlobals.oFormActMain.EndCombat(!enc.isImport);
                    enc.state = EncounterStateOutOfCombat.Instance();
                }
            }

            public override void TimeoutCheck(BPSR_Encounter enc)
            {
                TimeSpan diff = (enc.lastEncounterEvent is null) ? new TimeSpan() : enc.time - (DateTime)enc.lastEncounterEvent;

                if (diff > enc.timeout)
                {
                    // Too long - end encounter.
                    // timer.Stop();

                    DateTime endTime = (DateTime)(enc.lastEncounterEvent + enc.timeout);
                    enc.BeforeEndCombat(endTime);

                    ActGlobals.oFormActMain.EndCombat(!enc.isImport);
                    enc.state = EncounterStateOutOfCombat.Instance();
                }
            }
        }
    }

    class BPSR_Line_Parser
    {
        /*
         * grammar: 
         * [timestamp] [dmgtype] DS: Skill/FBullet/etc. SRC: src TGT: target ID: skId VAL: amt HPLSN: hplessen ELEM: CN EXT: extradetail(crit, lucky, etc.)
         */
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

    class BPSR_Packet_Interceptor
    {
        private DateTime cleanupLastTime;
        private const long FRAGMENT_TIMEOUT = 30000;
        private LivePacketDevice device;
        private string currentServer;
        private byte[] tcpPayloadData;
        private long tcpNextSeq = -1;
        private Dictionary<long, byte[]> tcpCache;
        private Dictionary<string, ipFragments> fragmentIPCache = new Dictionary<string, ipFragments>();
        private DateTime tcpLastTime;
        private object tcpLock = new object();
        private readonly byte[] sceneChangeSignature = new byte[] { 0x00, 0x63, 0x33, 0x53, 0x42, 0x00 };
        private readonly byte[] loginReturnSceneSignature = new byte[] { 0x00, 0x00, 0x00, 0x62, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, 0x11, 0x45, 0x14, 0x00, 0x00, 0x00, 0x00, 0x0a, 0x4e, 0x08, 0x01, 0x22, 0x24 };
        private BPSR_Packet_Processor packetProcessor;

        private struct ipFragments
        {
            public ipFragments Init()
            {
                Fragments = new List<IpDatagram> { };
                Timestamp = DateTime.Now;
                return this;
            }
            public List<IpDatagram> Fragments;
            public DateTime Timestamp;
        }

        private struct fragmentData
        {
            public int offset;
            public Datagram payload;
        }

        public BPSR_Packet_Interceptor()
        {
            device = AutoFindNetworkDevice();

            if (device == null)
            {
                return;
            }
            packetProcessor = new BPSR_Packet_Processor();

            ClearTCPCache();
        }

        public void Start()
        {

            using (var communicator = device.Open(65536, PacketDeviceOpenAttributes.Promiscuous, 1000))
            {
                if (communicator.DataLink.Kind != DataLinkKind.Ethernet) { return; }
                communicator.SetKernelMinimumBytesToCopy(0);
                communicator.SetFilter("ip and tcp");
                Packet packet;
                while (true)
                {
                    var result = communicator.ReceivePacket(out packet);

                    if (result != PacketCommunicatorReceiveResult.Ok) { continue; }
                    ProcessEthernetPacket(packet);

                }
            }
        }

        private LivePacketDevice AutoFindNetworkDevice()
        {
            Debug.WriteLine("Auto detecting network interface");
            var allDevices = LivePacketDevice.AllLocalMachine;
            if (allDevices.Count == 0)
            {
                Debug.WriteLine("No Devices Found");
                return null;
            }
            var routePrintString = ExecuteRouteCommand();
            try
            {
                var trimmedOption = routePrintString
                .Split('\n')
                .FirstOrDefault(l => l.Trim().StartsWith("0.0.0.0"))
                .Trim();
                var defaultInterface = Regex.Split(trimmedOption, @"\s+")[3];

                var iface = allDevices.FirstOrDefault(d => d.Addresses.FirstOrDefault(a => a.Address.ToString().Replace(a.Address.Family.ToString(), "").Trim().Equals(defaultInterface)) != null);
                Debug.WriteLine($"Using network interface: {iface.Description}");
                return iface;
            }
            catch { return null; }
        }

        private string ExecuteRouteCommand()
        {
            var psi = new ProcessStartInfo("CMD.exe", @"/C route print 0.0.0.0")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            var exitCode = proc.ExitCode;
            proc.Close();
            return output;
        }

        private void ClearTCPCache()
        {
            tcpPayloadData = new byte[] { };
            tcpNextSeq = -1;
            tcpLastTime = DateTime.MinValue;
            tcpCache = new Dictionary<long, byte[]>();
        }

        private void ProcessEthernetPacket(Packet packet)
        {
            if (DateTime.Now - cleanupLastTime > TimeSpan.FromSeconds(10))
            {
                CleanUpExpiredFragments();
                cleanupLastTime = DateTime.Now;
            }

            var ethernetPacket = packet.Ethernet;
            if (ethernetPacket.EtherType != EthernetType.IpV4) { return; }

            var ipPacket = ethernetPacket.Ip;
            var sourceAddress = ((IpV4Datagram)ipPacket).Source.ToString();
            var destinationAddress = ((IpV4Datagram)ipPacket).Destination.ToString();

            var tcpBuffer = GetTCPBuffer(packet);
            if (tcpBuffer == null) { return; }

            var fullPacketBytes = new byte[ethernetPacket.HeaderLength + ((IpV4Datagram)ipPacket).RealHeaderLength + tcpBuffer.Length];
            ethernetPacket.ToArray().SubArray(0, ethernetPacket.HeaderLength).CopyTo(fullPacketBytes, 0);
            ipPacket.ToArray().SubArray(0, ((IpV4Datagram)ipPacket).RealHeaderLength).CopyTo(fullPacketBytes, ethernetPacket.HeaderLength);
            tcpBuffer.CopyTo(fullPacketBytes, ethernetPacket.HeaderLength + ((IpV4Datagram)ipPacket).RealHeaderLength);
            var reconstructedPacket = new Packet(fullPacketBytes, DateTime.Now, DataLinkKind.Ethernet);

            var tcpPacket = reconstructedPacket.Ethernet.Ip.Tcp;

            var buf = tcpBuffer.SubArray(tcpPacket.HeaderLength);
            var sourcePort = tcpPacket.SourcePort.ToString();
            var destinationPort = tcpPacket.DestinationPort.ToString();
            var sourceServer = $"{sourceAddress}:{sourcePort} -> {destinationAddress}:{destinationPort}";
            lock (tcpLock)
            {
                try
                {
                    if (currentServer != sourceServer)
                    {
                        try
                        {
                            if (buf.Length > 10 && buf[4] == 0)
                            {
                                var data = buf.SubArray(10);
                                if (data.Length > 0)
                                {
                                    using (var bReader = new BinaryReader(new MemoryStream(data)))
                                    {
                                        var data1 = new byte[0];
                                        do
                                        {
                                            var len_buf = bReader.ReadBytes(4);
                                            if (len_buf.Length == 0) { break; }
                                            var packetLength = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(len_buf, 0));
                                            if (packetLength > 0x100000 || packetLength < 4)
                                            {
                                                Debug.WriteLine($"Invalid Packet Length During Server Identification: {packetLength}. Discarding Buffer");
                                                bReader.Close();
                                                break;
                                            }
                                            data1 = bReader.ReadBytes((int)(packetLength - 4));
                                            if (data1.Length == 0) { break; }

                                            if (!data1.SubArray(5, sceneChangeSignature.Length).SequenceEqual(sceneChangeSignature))
                                            {
                                                break;
                                            }

                                            if (currentServer != sourceServer)
                                            {
                                                currentServer = sourceServer;
                                                ClearTCPCache();
                                                tcpNextSeq = tcpPacket.SequenceNumber + buf.Length;
                                                Debug.WriteLine($"Got Scene Server Address: {sourceServer}");
                                            }

                                        } while (bReader.BaseStream.Position < data.Length);
                                    }
                                }
                            }
                            if (buf.Length == 0x62)
                            {
                                if (buf.SubArray(0, 10).SequenceEqual(loginReturnSceneSignature.SubArray(0, 10)) && buf.SubArray(14, 6).SequenceEqual(loginReturnSceneSignature.SubArray(14, 6)))
                                {
                                    if (currentServer != sourceServer)
                                    {
                                        currentServer = sourceServer;
                                        ClearTCPCache();
                                        tcpNextSeq = tcpPacket.SequenceNumber + buf.Length;
                                        Debug.WriteLine($"Got Scene Server Address by Login Return Packet: {sourceServer}");
                                    }
                                }

                            }
                        }
                        catch (Exception e) { throw e; }
                        return;
                    }
                    if (tcpNextSeq == -1)
                    {
                        var bufAsInt = BitConverter.ToUInt32(buf, 0);
                        var bufAsIntReverseEndian = BinaryPrimitives.ReverseEndianness(bufAsInt);
                        if (buf.Length > 4 && bufAsIntReverseEndian < 0x0fffff)
                        {
                            tcpNextSeq = tcpPacket.SequenceNumber;
                        }
                        else
                        {
                            Debug.WriteLine("Unexpected TCP Capture Error, tcpNextSeq is -1");
                        }
                    }
                    if (tcpNextSeq - tcpPacket.SequenceNumber <= 0 || tcpNextSeq == -1)
                    {
                        if (!tcpCache.ContainsKey(tcpPacket.SequenceNumber))
                        {
                            tcpCache.Add(tcpPacket.SequenceNumber, buf);
                        }
                        else
                        {
                            tcpCache[tcpPacket.SequenceNumber] = buf;
                        }
                    }

                    while (true)
                    {
                        if (!tcpCache.ContainsKey(tcpNextSeq))
                        {
                            break;
                        }
                        var seq = tcpNextSeq;
                        var cachedTcpData = tcpCache[seq];
                        tcpPayloadData = tcpPayloadData.Length == 0 ? cachedTcpData : tcpPayloadData.Concat(cachedTcpData).ToArray();
                        tcpNextSeq = unchecked((long)((ulong)(seq + cachedTcpData.Length) >> 0));
                        tcpCache.Remove(seq);
                        tcpLastTime = DateTime.Now;
                    }

                    while (tcpPayloadData.Length > 4)
                    {
                        var packetSize = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(tcpPayloadData, 0));
                        if (tcpPayloadData.Length < packetSize)
                        {
                            break;
                        }
                        if (packetSize > 0x0fffff)
                        {
                            Debug.WriteLine("Invalid Length of Packet");
                            tcpPayloadData = new byte[0];
                            break;
                        }
                        if (tcpPayloadData.Length >= packetSize)
                        {
                            var pkt = tcpPayloadData.SubArray(0, (long)packetSize);
                            tcpPayloadData = tcpPayloadData.SubArray((long)packetSize);
                            packetProcessor.ProcessPacket(pkt);
                        }
                    }
                }
                catch (Exception e) { throw e; }
            }
        }

        private byte[] GetTCPBuffer(Packet packet)
        {
            lock (packet)
            {
                var ipPacket = packet.Ethernet.Ip;
                var ipId = ((IpV4Datagram)ipPacket).Identification.ToString();
                var isFragment = ((IpV4Datagram)ipPacket).Fragmentation.Options == IpV4FragmentationOptions.MoreFragments;
                var key = $"{ipId}-{((IpV4Datagram)ipPacket).Source.ToString()}-{((IpV4Datagram)ipPacket).Destination.ToString()}-{((IpV4Datagram)ipPacket).Protocol}";

                if (isFragment || ((IpV4Datagram)ipPacket).Fragmentation.Offset > 0)
                {
                    if (!fragmentIPCache.ContainsKey(key))
                    {
                        fragmentIPCache.Add(key, new ipFragments().Init());
                    }
                    var cacheEntry = fragmentIPCache[key];
                    cacheEntry.Fragments.Add(ipPacket);
                    cacheEntry.Timestamp = DateTime.Now;

                    if (isFragment)
                    {
                        return null;
                    }

                    var fragments = cacheEntry.Fragments;

                    var totalLength = 0;
                    var fragmentData = new List<fragmentData>();
                    foreach (var buffer in fragments)
                    {
                        var ip = buffer;
                        var fragmentOffset = ((IpV4Datagram)ip).Fragmentation.Offset / 8;
                        var payload = ip.Payload;

                        fragmentData.Add(new BPSR_Packet_Interceptor.fragmentData { offset = fragmentOffset, payload = payload });

                        var endOffset = fragmentOffset + payload.Length;

                        if (endOffset > totalLength) { totalLength = endOffset; }
                    }

                    var fullPayload = new byte[totalLength];
                    foreach (var fragment in fragmentData)
                    {
                        fragment.payload.ToArray().CopyTo(fullPayload, fragment.offset);
                    }
                    fragmentIPCache.Remove(key);
                    return fullPayload;
                }

                return ipPacket.Tcp.ToArray();
            }

        }

        private void CleanUpExpiredFragments()
        {
            var now = DateTime.Now;
            var fipc_copy = fragmentIPCache;
            var clearedFragments = 0;
            foreach (var cacheEntry in fipc_copy)
            {
                if (now - cacheEntry.Value.Timestamp > TimeSpan.FromSeconds(FRAGMENT_TIMEOUT))
                {
                    fragmentIPCache.Remove(cacheEntry.Key);
                    clearedFragments++;
                }
            }

            if (clearedFragments > 0)
            {
                Debug.WriteLine($"Cleared {clearedFragments} expired IP Fragment caches");
            }

            if (tcpLastTime != DateTime.MinValue && now - tcpLastTime > TimeSpan.FromSeconds(FRAGMENT_TIMEOUT))
            {
                Debug.WriteLine("Cannot capture next packet. Is the game closed or disconnected?");
                currentServer = "";
                ClearTCPCache();
            }
        }

    }

    class BPSR_Packet_Processor
    {
        private readonly int MIN_PACKET_SIZE = 6;
        private readonly int MAX_PACKET_SIZE = 1024 * 1024;

        public long currentUserUuid = 0;
        public long CurrentUserUID { get; set; }
        private Decompressor decompressor;

        public Dictionary<long, Dictionary<string, string>> PlayerCache;
        public Dictionary<long, Dictionary<string, string>> EnemyCache;
        private BPSR_Event_Logger eventLogger;
        private bool inCombat;
        private long currentLevelId;
        public BPSR_Packet_Processor()
        {
            inCombat = false;
            decompressor = new Decompressor();
            PlayerCache = new Dictionary<long, Dictionary<string, string>>();
            EnemyCache = new Dictionary<long, Dictionary<string, string>>();
            eventLogger = new BPSR_Event_Logger();
            currentLevelId = 0;
        }


        public void ProcessPacket(byte[] packet)
        {
            try
            {
                using (var packetsReader = new BinaryReader(new MemoryStream(packet)))
                {
                    while ((packetsReader.BaseStream.Length - packetsReader.BaseStream.Position) >= MIN_PACKET_SIZE)
                    {

                        var packetSize = BinaryPrimitives.ReverseEndianness(packetsReader.ReadUInt32());
                        packetsReader.BaseStream.Seek(-4, SeekOrigin.Current);
                        if (packetSize < MIN_PACKET_SIZE || packetSize > MAX_PACKET_SIZE)
                        {
                            Debug.WriteLine("Invalid packet length, discarding corrupted buffer");
                            return;
                        }
                        if (packetsReader.BaseStream.Length - packetsReader.BaseStream.Position < MIN_PACKET_SIZE)
                        {
                            return;
                        }

                        using (var packetReader = new BinaryReader(new MemoryStream(packetsReader.ReadBytes((int)packetSize))))
                        {
                            packetReader.ReadUInt32();
                            var prShort = packetReader.ReadUInt16();
                            var packetType = BinaryPrimitives.ReverseEndianness(prShort);
                            var isZstdCompressed = (packetType & 0x8000) != 0;
                            var msgTypeId = packetType & 0x7fff;
                            switch ((MessageType)msgTypeId)
                            {
                                case MessageType.Notify:
                                    this.ProcessNotifyMessage(packetReader, isZstdCompressed);
                                    break;
                                case MessageType.Return:
                                    break;
                                case MessageType.FrameDown:
                                    //Debug.WriteLine("MessageType FrameDown");
                                    packetReader.ReadUInt32();
                                    if (packetReader.BaseStream.Position == packetReader.BaseStream.Length)
                                    {
                                        break;
                                    }
                                    var nestedPacket = packetReader.ReadBytes((int)(packetReader.BaseStream.Length - packetReader.BaseStream.Position));
                                    if (isZstdCompressed)
                                    {
                                        nestedPacket = DecompressPayload(nestedPacket);
                                    }
                                    ProcessPacket(nestedPacket);
                                    break;

                                default:
                                    break;
                            }
                        }

                    }
                }

            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error while parsing packet data for player {currentUserUuid >> 16}");
            }
        }

        private void ProcessNotifyMessage(BinaryReader packetReader, bool isZstdCompressed)
        {
            var serviceUuid = BinaryPrimitives.ReverseEndianness(packetReader.ReadUInt64());
            packetReader.ReadUInt32();
            var methodId = BinaryPrimitives.ReverseEndianness(packetReader.ReadUInt32());
            if (serviceUuid != 0x0000000063335342)
            {
                Debug.WriteLine("Skipping NotifyMsg");
                return;
            }
            var msgPayload = packetReader.ReadBytes((int)(packetReader.BaseStream.Length - packetReader.BaseStream.Position));
            if (isZstdCompressed)
            {
                msgPayload = DecompressPayload(msgPayload);
                return;
            }
            switch ((NotifyMethod)methodId)
            {
                case NotifyMethod.SyncNearEntities:
                    ProcessSyncNearEntities(msgPayload);
                    break;
                case NotifyMethod.SyncContainerData:
                    ProcessSyncContainerData(msgPayload);
                    break;
                case NotifyMethod.SyncContainerDirtyData:
                    ProcessSyncContainerDirtyData(msgPayload);
                    break;
                case NotifyMethod.SyncToMeDeltaInfo:
                    ProcessSyncToMeDeltaInfo(msgPayload);
                    break;
                case NotifyMethod.SyncNearDeltaInfo:
                    ProcessSyncNearDeltaInfo(msgPayload);
                    break;
                default:
                    Debug.WriteLine($"Skipping NotifyMsg with methodId: {methodId}");
                    break;
            }
        }

        private void ProcessAoiSyncDelta(AoiSyncDelta aoiSyncDelta)
        {
            var targetUuid = aoiSyncDelta.Uuid;
            if (targetUuid < 1) { return; }

            var isTargetPlayer = IsUuidPlayer(targetUuid);
            var isTargetMonster = IsUuidMonster(targetUuid);
            var targetUid = targetUuid >> 16;

            var attrCollection = aoiSyncDelta.Attrs;
            if (!attrCollection?.Attrs.IsNullOrEmpty() ?? false)
            {
                if (isTargetPlayer)
                {
                    ProcessPlayerAttrs(targetUid, attrCollection.Attrs.ToList());
                }
                else if (isTargetMonster)
                {
                    ProcessEnemyAttrs(targetUid, attrCollection.Attrs.ToList());
                }
            }

            var skillEffect = aoiSyncDelta.SkillEffects;
            if (skillEffect == null || skillEffect.Damages.IsNullOrEmpty())
            {
                return;
            }

            foreach (var syncDamageInfo in skillEffect.Damages)
            {
                var skillId = syncDamageInfo.OwnerId;
                if (skillId < 1) { continue; }

                var attackerUuid = syncDamageInfo.TopSummonerId != 0 ? syncDamageInfo.TopSummonerId : syncDamageInfo.AttackerUuid;
                if (attackerUuid < 1) { continue; }

                var isAttackerPlayer = IsUuidPlayer(attackerUuid);
                var attackerUid = attackerUuid >> 16;

                var value = syncDamageInfo.Value;
                var luckyValue = syncDamageInfo.LuckyValue;
                var damage = value > 0 ? value : luckyValue > 0 ? luckyValue : 0;
                if (damage < 1) { continue; }

                var isCrit = (syncDamageInfo.TypeFlag & 1) == 1;
                var isLucky = luckyValue > 0;
                var isHeal = (EDamageType)syncDamageInfo.Type == EDamageType.Heal;
                var isDead = syncDamageInfo.IsDead;
                var hpLessenValue = syncDamageInfo.HpLessenValue;
                var damageElement = GetDamageElement((EDamageProperty)syncDamageInfo.Property);
                var damageSource = syncDamageInfo.DamageSource;
                var isAttackerUser = CurrentUserUID > 0 && attackerUid == CurrentUserUID;
                var isTargetUser = CurrentUserUID > 0 && targetUid == CurrentUserUID;

                if (isTargetPlayer)
                {
                    if (isHeal)
                    {
                        eventLogger.AddHealingLogLine(attackerUid, targetUid, skillId, damageElement, damage, isCrit, isLucky, isAttackerUser, isTargetUser);
                    }

                    if (isDead && PlayerCache.ContainsKey(targetUid) && PlayerCache[targetUid].ContainsKey("dead") && PlayerCache[targetUid]["dead"] == "no")
                    {
                        var name = this.GetPlayerAttribute(targetUid, "name");
                        var attackTarget = $"{name ?? ""}#{targetUid}";

                        eventLogger.AddDeathLogLine(attackerUid.ToString(), attackTarget, skillId, damageElement, damage, hpLessenValue, isCrit, isLucky, isAttackerUser, isTargetUser);
                        SetPlayerAttribute(targetUid, "hp", 0);
                    }

                    SetPlayerAttribute(targetUid, "dead", isDead ? "yes" : "no");

                }
                else
                {
                    if (isAttackerPlayer)
                    {
                        if (!isHeal)
                        {
                            var playerName = this.GetPlayerAttribute(attackerUid, "name");
                            var attackSource = $"{playerName ?? ""}#{attackerUid}";

                            var enemyName = this.GetEnemyAttribute(targetUid, "name");
                            var attackTarget = $"{enemyName ?? ""}#{targetUid}";

                            eventLogger.AddDamageLogLine(attackSource, attackTarget, skillId, damageElement, damage, hpLessenValue, isCrit, isLucky, isAttackerUser, isTargetUser);
                        }

                        //SetPlayerAttribute(attackerUid, "dead", isDead ? "yes" : "no");
                    }
                }
            }
        }

        private void ProcessSyncNearEntities(byte[] payloadBuffer)
        {

            var syncNearEntities = SyncNearEntities.Parser.ParseFrom(payloadBuffer);

            if (syncNearEntities.Appear.Count == 0)
            {
                return;
            }

            foreach (var entity in syncNearEntities.Appear)
            {
                var entityUuid = entity.Uuid;
                if (entityUuid < 1) { continue; }
                var entityUid = entityUuid >> 16;
                var attrCollection = entity.Attrs;
                if (attrCollection.Attrs.Count > 0)
                {
                    switch (entity.EntType)
                    {
                        case EEntityType.EntMonster:
                            ProcessEnemyAttrs(entityUid, attrCollection.Attrs.ToList());
                            break;
                        case EEntityType.EntChar:
                            ProcessPlayerAttrs(entityUid, attrCollection.Attrs.ToList());
                            break;
                    }
                }
            }
        }

        private void ProcessSyncContainerData(byte[] payloadBuffer)
        {
            var syncContainerData = SyncContainerData.Parser.ParseFrom(payloadBuffer);
            if (syncContainerData.VData != null)
            {
                var sceneData = syncContainerData.VData.SceneData;
                if (sceneData.LevelMapId != currentLevelId)
                {
                    //var currentAreaId = sceneData.LevelAreaId;
                    currentLevelId = sceneData.LevelMapId;
                    eventLogger.AddZoneChangeLogLine(currentLevelId);
                }
            }
            else { return; }

            var vData = syncContainerData.VData;
            if (vData.CharId < 1) { return; }
            var playerUid = vData.CharId;

            var charBase = vData.CharBase;
            if (charBase == null) { return; }

            if (!String.IsNullOrEmpty(charBase.Name))
            {
                SetPlayerAttribute(playerUid, "name", charBase.Name);
            }
            if (charBase.FightPoint > 0)
            {
                SetPlayerAttribute(playerUid, "fight_point", charBase.FightPoint);
            }

        }

        private void ProcessSyncContainerDirtyData(byte[] payloadBuffer)
        {
            var syncContainerDirtyData = SyncContainerDirtyData.Parser.ParseFrom(payloadBuffer);
            if (syncContainerDirtyData.VData.Buffer.IsNullOrEmpty()) { return; }

            //uhh I think this means that when there is a userfightattr, we are in combat and when there isn't, we are not?
            //and the sequence we are looking for in here is [16, 0, 0, 0], which equates to tag 16 in the protobuf data, which is
            //the userfight attr within vdata
            var fightDataSequenceExists = !new BoyerMoore(new byte[] { 16, 0, 0, 0 }).Search(syncContainerDirtyData.VData.Buffer.ToArray()).IsNullOrEmpty();

            if (fightDataSequenceExists && !inCombat)
            {
                inCombat = true;
                //add eventlogger in combat log line
            }

            if (!fightDataSequenceExists && inCombat)
            {
                inCombat = false;
                //add eventlogger out of combat log line
            }

            //var vDataBufferArray = syncContainerDirtyData.VData.Buffer.ToArray();
            //var sceneDataSequenceSearch = new BoyerMoore(new byte[] { 3, 0, 0, 0 }).Search(vDataBufferArray);
            //var sceneDataSequenceExists = !sceneDataSequenceSearch.IsNullOrEmpty();

            //if (sceneDataSequenceExists)
            //{
            //    var mainIndex = sceneDataSequenceSearch.First();
            //    var sceneDataSubArray = vDataBufferArray.SubArray(mainIndex);
            //    var levelMapIdSearch = new BoyerMoore(new byte[] { 6, 0, 0, 0 }).Search(sceneDataSubArray);
            //    if (levelMapIdSearch.IsNullOrEmpty()) { return; }
            //    var levelMapIdIndex = levelMapIdSearch.First();

            //    using (var reader = new BinaryReader(new MemoryStream(sceneDataSubArray)))
            //    {
            //        reader.BaseStream.Seek(levelMapIdIndex + 16, SeekOrigin.Begin);
            //        var levelMapId = reader.ReadUInt32();
            //        var levelMapIdReverse = BinaryPrimitives.ReverseEndianness(levelMapId);
            //        var a = 1;
            //    }
            //}
        }

        private void ProcessSyncToMeDeltaInfo(byte[] payloadBuffer)
        {
            var syncToMeDeltaInfo = SyncToMeDeltaInfo.Parser.ParseFrom(payloadBuffer);
            var aoiSyncToMeDelta = syncToMeDeltaInfo.DeltaInfo;

            var uuid = aoiSyncToMeDelta.Uuid;

            if (uuid != currentUserUuid)
            {
                currentUserUuid = uuid;
                var uid = currentUserUuid >> 16;
                CurrentUserUID = uid;
                Debug.WriteLine($"Got a player UUID: {currentUserUuid}. UID: {uid}");
            }

            var aoiSyncDelta = aoiSyncToMeDelta.BaseDelta;

            ProcessAoiSyncDelta(aoiSyncDelta);
        }

        private void ProcessSyncNearDeltaInfo(byte[] payloadBuffer)
        {
            var syncNearDeltaInfo = SyncNearDeltaInfo.Parser.ParseFrom(payloadBuffer);
            if (syncNearDeltaInfo.DeltaInfos.Count == 0)
            {
                return;
            }
            foreach (var deltaInfos in syncNearDeltaInfo.DeltaInfos)
            {
                ProcessAoiSyncDelta(deltaInfos);
            }
        }

        private byte[] DecompressPayload(byte[] buffer)
        {
            var decompressed = decompressor.Unwrap(buffer);
            return decompressed.ToArray();
        }

        private void ProcessPlayerAttrs(long playerUid, List<Attr> attrs)
        {
            foreach (var attr in attrs)
            {
                if (attr.Id < 1 || attr.RawData.IsNullOrEmpty()) { continue; }
                var b = attr.RawData.ToByteArray();
                using (var stream = new CodedInputStream(b))
                {
                    switch ((AttrType)attr.Id)
                    {
                        case AttrType.AttrName:
                            var name = stream.ReadString();
                            SetPlayerAttribute(playerUid, "name", name);
                            break;

                        case AttrType.AttrProfessionId:
                            var professionId = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "profession", professionId);
                            break;

                        case AttrType.AttrFightPoint:
                            var fightPoint = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "fight_point", fightPoint);
                            break;

                        case AttrType.AttrLevel:
                            var level = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "level", level);
                            break;

                        case AttrType.AttrRankLevel:
                            var rank = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "rank_level", rank);
                            break;

                        case AttrType.AttrCri:
                            var cri = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "cri", cri);
                            break;

                        case AttrType.AttrLucky:
                            var lucky = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "lucky", lucky);
                            break;

                        case AttrType.AttrHp:
                            var hp = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "hp", hp);
                            break;

                        case AttrType.AttrMaxHp:
                            var maxHp = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "max_hp", maxHp);
                            break;

                        case AttrType.AttrElementFlag:
                            var elementFlag = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "element_flag", elementFlag);
                            break;

                        case AttrType.AttrEnergyFlag:
                            var energyFlag = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "energy_flag", energyFlag);
                            break;

                        case AttrType.AttrReductionLevel:
                            var reductionLevel = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "reduction_flag", reductionLevel);
                            break;

                        default:
                            break;

                    }
                }
            }
        }

        private void ProcessEnemyAttrs(long enemyUid, List<Attr> attrs)
        {
            foreach (var attr in attrs)
            {
                if (attr.Id < 1 || attr.RawData.IsNullOrEmpty()) { continue; }
                var b = attr.RawData.ToByteArray();
                using (var stream = new CodedInputStream(b))
                {
                    switch ((AttrType)attr.Id)
                    {
                        case AttrType.AttrName:
                            var name = stream.ReadString();
                            SetEnemyAttribute(enemyUid, "name", name);
                            break;
                        case AttrType.AttrId:
                            var attrId = stream.ReadInt32();
                            if (MonsterMap.ContainsKey(attrId))
                            {
                                SetEnemyAttribute(enemyUid, "name", MonsterMap[attrId]);
                            }
                            break;
                    }
                }
            }
        }

        private bool IsUuidPlayer(long Uuid)
        {
            return (Uuid & 0xffff) == 640;
        }

        private bool IsUuidMonster(long Uuid)
        {
            return (Uuid & 0xffff) == 64;
        }

        private void SetPlayerAttribute(long uid, string key, object value)
        {

            if (!PlayerCache.ContainsKey(uid))
            {
                PlayerCache.Add(uid, new Dictionary<string, string>());
            }

            if (!PlayerCache[uid].ContainsKey(key))
            {
                PlayerCache[uid].Add(key, value.ToString());
            }
            else
            {
                PlayerCache[uid][key] = value.ToString();
            }
        }

        private void SetEnemyAttribute(long uid, string key, object value)
        {

            if (!EnemyCache.ContainsKey(uid))
            {
                EnemyCache.Add(uid, new Dictionary<string, string>());
            }

            if (!EnemyCache[uid].ContainsKey(key))
            {
                EnemyCache[uid].Add(key, value.ToString());
            }
            else
            {
                EnemyCache[uid][key] = value.ToString();
            }
        }

        private string GetPlayerAttribute(long uid, string key)
        {
            if (!PlayerCache.ContainsKey(uid))
            {
                return null;
            }
            if (!PlayerCache[uid].ContainsKey(key))
            {
                return null;
            }

            return PlayerCache[uid][key];
        }

        private string GetEnemyAttribute(long uid, string key)
        {
            if (!EnemyCache.ContainsKey(uid))
            {
                return null;
            }
            if (!EnemyCache[uid].ContainsKey(key))
            {
                return null;
            }

            return EnemyCache[uid][key];
        }

        private string GetDamageElement(EDamageProperty damagePropery)
        {
            switch (damagePropery)
            {
                case EDamageProperty.General:
                    return "Physical";
                case EDamageProperty.Fire:
                    return "Fire";
                case EDamageProperty.Water:
                    return "Water";
                case EDamageProperty.Lightning:
                    return "Lightning";
                case EDamageProperty.Forest:
                    return "Forest";
                case EDamageProperty.Wind:
                    return "Wind";
                case EDamageProperty.Earth:
                    return "Earth";
                case EDamageProperty.Light:
                    return "Light";
                case EDamageProperty.Dark:
                    return "Dark";
                case EDamageProperty.Count:
                    return "Almighty?";
                default:
                    return "Physical";
            }
        }

        private bool DoesStreamHaveIdentifier(BinaryReader reader)
        {
            var startPosition = reader.BaseStream.Position;
            if ((reader.BaseStream.Length - reader.BaseStream.Position) < 8) { return false; }

            var identifier = reader.ReadUInt32();
            reader.ReadInt32();

            if (identifier != 0xfffffffe)
            {
                reader.BaseStream.Seek(startPosition, SeekOrigin.Begin);
                return false;
            }

            var uIdentifier = BinaryPrimitives.ReverseEndianness(reader.ReadInt32());
            reader.ReadInt32();

            reader.BaseStream.Seek(startPosition, SeekOrigin.Begin);

            return true;
        }

    }

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

        public void AddDeathLogLine(string sourceUid, string destinationUid, int skillId, string element, long damageValue, long targetDamageReceived, bool isCrit, bool isLucky, bool isAttackerUser, bool isTargetUser)
        {
            var line = $"{LogEventIds.EVENT_PLAYER_DIE}|{sourceUid}|{destinationUid}|{skillId}|{element}|{damageValue}|{targetDamageReceived}|{isCrit}|{isLucky}|{isAttackerUser}|{isTargetUser}";
            AddLogLine(line);
        }

        public void AddDamageLogLine(string sourceUid, string destinationUid, int skillId, string element, long damageValue, long targetDamageReceived, bool isCrit, bool isLucky, bool isAttackerUser, bool isTargetUser)
        {
            var line = $"{LogEventIds.EVENT_DAMAGE}|{sourceUid}|{destinationUid}|{skillId}|{element}|{damageValue}|{targetDamageReceived}|{isCrit}|{isLucky}|{isAttackerUser}|{isTargetUser}";
            AddLogLine(line);
        }

        public void AddHealingLogLine(long sourceUid, long destinationUid, int skillId, string element, long damageValue, bool isCrit, bool isLucky, bool isAttackerUser, bool isTargetUser)
        {
            var line = $"{LogEventIds.EVENT_HEAL}|{sourceUid}|{destinationUid}|{skillId}|{element}|{damageValue}|{isCrit}|{isLucky}|{isAttackerUser}|{isTargetUser}";
            AddLogLine(line);
        }

        private void AddLogLine(string line)
        {
            line = $"{DateTimeOffset.Now.ToUnixTimeMilliseconds()}|{line}";

            File.AppendAllText(logFileName, line + "\n");

        }
    }

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
            {6009, "Goblin Lair (Master 1)" }
        };
        public static Dictionary<int, string> MonsterMap = new Dictionary<int, string>
        {
            {10007, "Storm Goblin King" },
            {10010, "Tempest Ogre" }
            
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
    }

    public sealed class BoyerMoore
    {
        readonly byte[] needle;
        readonly int[] charTable;
        readonly int[] offsetTable;

        public BoyerMoore(byte[] needle)
        {
            this.needle = needle;
            this.charTable = makeByteTable(needle);
            this.offsetTable = makeOffsetTable(needle);
        }

        public IEnumerable<int> Search(byte[] haystack)
        {
            if (needle.Length == 0)
                yield break;

            for (int i = needle.Length - 1; i < haystack.Length;)
            {
                int j;

                for (j = needle.Length - 1; needle[j] == haystack[i]; --i, --j)
                {
                    if (j != 0)
                        continue;

                    yield return i;
                    i += needle.Length - 1;
                    break;
                }

                i += Math.Max(offsetTable[needle.Length - 1 - j], charTable[haystack[i]]);
            }
        }

        static int[] makeByteTable(byte[] needle)
        {
            const int ALPHABET_SIZE = 256;
            int[] table = new int[ALPHABET_SIZE];

            for (int i = 0; i < table.Length; ++i)
                table[i] = needle.Length;

            for (int i = 0; i < needle.Length - 1; ++i)
                table[needle[i]] = needle.Length - 1 - i;

            return table;
        }

        static int[] makeOffsetTable(byte[] needle)
        {
            int[] table = new int[needle.Length];
            int lastPrefixPosition = needle.Length;

            for (int i = needle.Length - 1; i >= 0; --i)
            {
                if (isPrefix(needle, i + 1))
                    lastPrefixPosition = i + 1;

                table[needle.Length - 1 - i] = lastPrefixPosition - i + needle.Length - 1;
            }

            for (int i = 0; i < needle.Length - 1; ++i)
            {
                int slen = suffixLength(needle, i);
                table[slen] = needle.Length - 1 - i + slen;
            }

            return table;
        }

        static bool isPrefix(byte[] needle, int p)
        {
            for (int i = p, j = 0; i < needle.Length; ++i, ++j)
                if (needle[i] != needle[j])
                    return false;

            return true;
        }

        static int suffixLength(byte[] needle, int p)
        {
            int len = 0;

            for (int i = p, j = needle.Length - 1; i >= 0 && needle[i] == needle[j]; --i, --j)
                ++len;

            return len;
        }
    }
}
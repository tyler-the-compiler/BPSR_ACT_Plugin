using Advanced_Combat_Tracker;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using BPSR_ACT_Plugin;
using ACT_Plugin.Core;


[assembly: AssemblyTitle("BPSR_ACT_Plugin")]
[assembly: AssemblyDescription("A parsing plugin for Blue Protocol: Star Resonance in ACT")]
[assembly: AssemblyCompany("Subtract")]
[assembly: AssemblyVersion("1.0.0.2")]

namespace ACT_Plugin
{
    public class BPSR_ACT_Plugin : UserControl, IActPluginV1
    {
        PluginMain pluginMain;
        private TextBox ui_logfileParentFolder;
        private Label ui_logfileParentFolder_label;
        Label lblStatus;
        string settingsFile = Path.Combine(ActGlobals.oFormActMain.AppDataFolder.FullName, "Config\\BPSR_ACT_Plugin.config.xml");
        SettingsSerializer xmlSettings;
        static AssemblyResolver assemblyResolver;
        string pluginDirectory;

        public BPSR_ACT_Plugin()
        {
            InitializeComponent();
        }

        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            pluginDirectory = GetPluginDirectory();
            if (assemblyResolver == null)
            {
                assemblyResolver = new AssemblyResolver(new List<string>
                {
                    pluginDirectory
                    //Path.Combine(pluginDirectory, "resources")
                });
            }

            ACTSetup(pluginScreenSpace, pluginStatusText);
            lblStatus.Text = "BP:SR ACT Plugin Started";

            Initialize(pluginScreenSpace, pluginStatusText);
        }
        public void DeInitPlugin()
        {
            SaveSettings();
            lblStatus.Text = "BP:SR ACT Plugin Exited";

            if (ActGlobals.oFormActMain.IsActClosing)
            {
                assemblyResolver.Dispose();
            }
        }
        private string GetPluginDirectory()
        {
            var plugin = ActGlobals.oFormActMain.ActPlugins.Where(x => x.pluginObj == this).FirstOrDefault();
            if (plugin != null)
            {
                return Path.GetDirectoryName(plugin.pluginFile.FullName);
            }
            else
            {
                throw new Exception("Could not find ourselves in the plugin list!");
            }
        }
        private void ACTSetup(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            lblStatus = pluginStatusText;   // Hand the status label's reference to our local var
            pluginScreenSpace.Controls.Add(this);   // Add this UserControl to the tab ACT provides
            this.Dock = DockStyle.Fill; // Expand the UserControl to fill the tab's client space
            xmlSettings = new SettingsSerializer(this); // Create a new settings serializer and pass it this instance
            LoadSettings();
        }
        private async void Initialize(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            assemblyResolver.ExceptionOccured += (o, e) => Debug.WriteLine(e.Exception);
            assemblyResolver.AssemblyLoaded += (o, e) => Debug.WriteLine(e.LoadedAssembly.FullName);
            pluginMain = new PluginMain();
            ActGlobals.oFormActMain.Invoke((Action)(() =>
            {
                try
                {
                    pluginMain.InitPlugin(pluginScreenSpace, pluginStatusText);
                }
                catch (Exception ex) { 
                }
            }));
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

    }













}
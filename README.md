An ACT plugin for Blue Protocol: Star Resonance

utilizes and adapts code from

- SWTOR Parsing plugin: https://advancedcombattracker.com/download.php
- TSW Parsing plugin: https://advancedcombattracker.com/download.php
- BPSR-PSO: https://github.com/Chase-Simmons/BPSR-PSO
- StarResonanceDamageCounter: https://github.com/dmlgzs/StarResonanceDamageCounter
- OverlayPlugin: https://github.com/OverlayPlugin/OverlayPlugin
- BlueMeter: https://github.com/caaatto/BlueMeter

Install Instructions:

- Requires ACT: https://advancedcombattracker.com/download.php

- Copy the entire folder into your ACT plugins folder (usually appdata/roaming/Advanced Combat Tracker/Plugins)
- Copy "System.Runtime.CompilerServices.Unsafe.dll" from the folder to ACT's install location (usually C:\Program Files(x86)\Advanced Combat Tracker). This will hopefully be rectified in the future but it's a stopgap solution for now
- In ACT, click on the "Plugins" tab
- Click "Browse..."
- Navigate to the directory where you dropped the folder and select "BPSR_ACT_Plugin.dll" (NOT "BPSR_ACT_Plugin.Core.dll")
- Click "Add/Enable Plugin"
- Start fighting something and you should begin to see data populate on the "Main" tab in ACT

Additionally install OverlayPlugin to have an on-screen DPS meter - I have ported the popular Kagerou Overlay from FFXIV to BP:SR

- Go back to the "Plugins" tab
- Click "Get Plugins..."
- Select "OverlayPlugin"
- Once that is installed, Click on the "OverlayPlugin.dll" tab
- Select "New" and name it whatever you want
- Select "Custom" for the Preset
- Select "MiniParse" for Type
- Once that overlay has been created, select it from the sidebar and in the URL field, enter: https://tyler-the-compiler.github.io/kagerou-bpsr/overlay/
- The meter should now show up on your screen
- That's it!

Currently Known Issues:

- Install folder can be slimmed down
- There is occasionally a thread-unsafe operation happening somewhere that will cause ACT to crash. Still working on it
- Ability Score and Profession are hardcoded, these will be updated to reflect actual values soon(TM)

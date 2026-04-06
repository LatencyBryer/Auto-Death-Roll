# **🎲 Auto Death Roll**

Auto Death Roll automates and assists the **Death Roll** minigame.

It tracks your target, reads `/random` chat rolls in real time, automatically reacts to match flow and helps manage the full Death Roll sequence through a clean in-game interface.

#

## **✨ Features**

- 🔹 Fully in-game UI for Death Roll matches
- 🔹 Track your current target as the opponent
- 🔹 One-click match start workflow
- 🔹 Automatic detection of:
  - Opening `/random 10` rolls
  - Normal `/random` rolls
  - Limited `/random X` rolls
- 🔹 Automatically detect who starts the round
- 🔹 Automatically sends your next `/random` command at the correct time
- 🔹 Live match log with colored result tracking in the UI
- 🔹 Auto win/loss detection when someone rolls **1**
- 🔹 Match state handling:
  - Start Match
  - Cancel Match
  - Abort Match
  - End Match
- 🔹 Help popup explaining how Death Roll works
- 🔹 Short command aliases for opening the plugin quickly _(/dr; /drt; /drc;)_
- 🔹 Configurable emotes and `/say` messages for:
  - Opening win
  - Opening loss
  - Roll phases
  - Final win
  - Final loss
- 🔹 Built-in test execution for configured actions

#

## **📦 Installation**

### **🔧 Custom Repository Installation**

1. Open Dalamud settings
2. Navigate to **Experimental → Custom Plugin Repositories**
3. Add: `https://raw.githubusercontent.com/seventity7/Auto-Death-Roll/main/repo.json`
4. Open plugin installer
5. Search for **Auto Death Roll**
6. Install

#

## **🧠 How It Works**

### **Main Window**
Open the main UI through `/dr`, `/drt` or `/deathroll` commands

<img width="427" height="409" alt="image" src="https://github.com/user-attachments/assets/bc542663-8568-44db-8a09-89c50e472168" />

Target your opponent and hit the button `Target Enemy` in the plugin interface *(Targeted wrong? hit the `"X"` button)*
The interface will automaticaly update when a `Enemy` is targeted

<img width="426" height="340" alt="image" src="https://github.com/user-attachments/assets/64e81de2-8c67-486b-b95f-ecd5796be3fa" />

Before you both start the `/random 10`'s, hit the button `Start Match` in the plugin interface
The `Tracker` will start and new `Logs` about the on-going match will appear bellow the buttons

<img width="422" height="278" alt="image" src="https://github.com/user-attachments/assets/d7ea4286-222a-4f26-993a-d5030c37ce39" />

You need to manualy type `/random 10` in the game chat to start, it doesnt matter if you do before/after your enemy
The plugin will automaticaly detect who `Won` and understand who will start. If you `Won`, the plugin will automaticaly start for you
From there the plugin will do **everything** automaticaly, you <ins>dont</ins> need to do anything more.

The plugin will automaticaly detect and show who `Win`/`Lose` the DR match.
After a match ends, hit the button `End Match` to clear it for the next one and repeat the steps

<img width="424" height="507" alt="image" src="https://github.com/user-attachments/assets/810d611b-d9b0-4bac-8d33-c37db08b77f8" />

If for some reason you need to `Cancel` the match **during** the `/random 10` phase, hit the `Cancel Match` button.
If for some reason you need to `Abort` the match **after** the `/random 10` phase, hit the `Abort Match` button.
> *They will only become available to click durint and after the `/random 10` phases.*

<img width="428" height="72" alt="image" src="https://github.com/user-attachments/assets/a4d84131-e22a-44dc-9d87-a76d065a226b" />

### **Settings Window**
Open the main UI through `/drc`, `/dr settings` or `/dr config` commands

In this window, you can fully customize your Matchs through the `Opening`, `Rolls Phase` and `End Match` categories

**`Opening:`**
 - Win out of 10: Set any `emote` and `/say` message to run when you **`Win`** the `/random 10`'s phase
 - Loss out of 10: Set any `emote` and `/say` message to run when you **`Lose`** the `/random 10`'s phase

**`Rolls Phase:`**
 - 1° Roll: Set any `emote` and `/say` message to run on your **`First`** roll
 Goes from 1° Roll up to 10° Roll with the same options for they respective rolls

**`End Match:`**
 - You win: Set any `emote` and `/say` message to run if you win the `DR` Match *(When `Enemy` rolls `[1]`)*
 - You lost: Set any `emote` and `/say` message to run if you lose the `DR` Match *(When `You` rolls `[1]`)*

> *You can test any of the respective lines clicking the `▶` button*

<img width="750" height="500" alt="image" src="https://github.com/user-attachments/assets/a7b19902-30ed-4107-92c0-dc81eb72058c" />

#

## **⚠️ Important**

- ✅ The opponent must be properly targeted before starting
- ✅ Both players must use the correct `/random` format for the plugin to detect rolls *(it will automaticaly know if you or the enemy do a wrong `/random` and it will not count it)*
- ✅ Opening phase must be manualy typed and begin with `/random 10` *(Normal DR rules)*
- ⚠️ The plugin only reacts to the tracked opponent's rolls
- ⚠️ Match automation depends on chat messages being visible

#

## **📌 Supported Commands**

### **Main Window**
- `/deathroll`
- `/dr`
- `/drt`

### **Config Window**
- `/drc`
- `/dr config`
- `/dr settings`
- `/drt config`
- `/drt settings`

#

## **📌 Known Limitations**

- The tracked enemy must match the actual opponent in the match *(it wont work if you target X player and its playing against Y)*
- The plugin is intended for one active Death Roll match at a time
- The plugin does not enforce game rules by itself; it assists and automates the normal Death Roll flow

#

## **🚀 Will be added next update** *(Doesnt mean will be all at once and is not limited to only those)*

- 🔹 Total matchs `Wins`/`Loss`
- 🔹 Option to register tournament `Prize`
- 🔹 Full history with all past matchs
- 🔹 Always-ON mode

#

## 👤 Author

**Bryer**

#

## 💬 Support

If you encounter issues or have suggestions:

- Open an issue on GitHub
- Or contact via discord

#

## 📜 License

This project is provided as-is for personal use within FFXIV © Do not redistribute or modify it without permission.
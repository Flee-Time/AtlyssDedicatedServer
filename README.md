# 🖧 ATLYSS Dedicated Server Plugin

This BepInEx plugin adds **headless dedicated server support** to the game **ATLYSS**, enabling you to run the game in a terminal as a dedicated server — no graphics or UI needed.

> ⚠️ This is for hosting servers only. It will disable itself when the game is being launched normally.

---

## ✅ Mod Compatibility

- Fully compatible with other BepInEx mods that modify or enhance hosting behavior.  
  This includes:
  - **Chat formatting mods** (e.g., colored messages)
  - **Uncapped party size** or connection limit mods
  - **Custom stat or balance changes**
  - **Lobby tweak plugins**

As long as the mod loads under BepInEx and applies during host/server initialization, it will work seamlessly with this dedicated server plugin.

---

## 🔍 Known Bugs

- **Console input does not echo back while typing.**  
  When using the BepInEx console, typed characters may not appear on screen. However, input is still being received and processed correctly — pressing `Enter` will submit the full command.

- **No feedback for partially typed commands.**  
  Since input isn't echoed, it's easy to lose track of what you've typed. To mitigate this, commands should be typed carefully and can be confirmed once submitted.


---

## ✅ Requirements

- **BepInEx 5.x**
- **ATLYSS game**
- Must launch the game with the following arguments:

```sh
-batchmode -nographics -server
```

---

## 🛠️ Launch Syntax

```sh
ATLYSS.exe -batchmode -nographics -server [options...]
```

You **must** include `-batchmode -nographics` before your own custom arguments.

---

## 🔧 Available Arguments

| Argument             | Description                                                                 |
|----------------------|-----------------------------------------------------------------------------|
| `-server`            | Enables dedicated server mode                                               |
| `-name "MyServer"`   | Sets the server name (max 20 characters)                                    |
| `-password "1234"`   | Sets a join password                                                        |
| `-motd "Message"`    | Sets a Message of the Day                                                   |
| `-maxplayers N`      | Max number of players (between 2 and 250, default: 16)                      |
| `-public`            | Makes server public (default if no type is given)                           |
| `-private`           | Makes server private                                                        |
| `-friends`           | Makes server visible only to Steam friends                                  |
| `-pve`               | Lobby focus: PvE (default)                                                  |
| `-pvp`               | Lobby focus: PvP                                                            |
| `-social`            | Lobby focus: Social                                                         |

> ⚠️ Only one of `-public`, `-private`, or `-friends` can be used.  
> ⚠️ Only one of `-pve`, `-pvp`, or `-social` can be used.

---

## 📦 Example Usages

### Start a public PvE server with 16 players:

```sh
ATLYSS.exe -batchmode -nographics -server -name "MyServer" -motd "Welcome!" -maxplayers 16 -public -pve
```

### Start a private PvP server with a password:

```sh
ATLYSS.exe -batchmode -nographics -server -name "Private Warzone" -password "hunter2" -maxplayers 10 -private -pvp
```

### Start a friends-only social server:

```sh
ATLYSS.exe -batchmode -nographics -server -name "CozyHub" -motd "Grab tea and chill." -friends -social
```

---

## 🧠 Behavior Notes

- The host is teleported to **the fishing area 30 seconds** after spawning.
- Server shuts down **5 seconds after host stop** (clean shutdown).
- In-game chat is mirrored to the terminal (color tags handled).
- Console input works for typing commands directly.
- Audio is disabled via `AudioListener.volume = 0`.
- Server config and startup logs are shown in the console.

---

## 🧪 Troubleshooting

- ❌ **Nothing happens?** Make sure you're running with `-batchmode -nographics -server`
- ❌ **Can't see the terminal?** Run the game via `cmd.exe` or a `.bat` script
- ❌ **Server name reset?** Must be under 20 characters

using BepInEx;
using BepInEx.Logging;
using System.Linq;
using System;
using UnityEngine;
using Steamworks;
using Mirror;
using HarmonyLib;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Collections;

namespace AtlyssDedicatedServer;

// TODO : Fix console not echoing back.
// clean console output

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInProcess("ATLYSS.exe")]
public class Plugin : BaseUnityPlugin
{
    public enum LobbyTypeTag : byte
    {
        PUBLIC,
        FRIENDS,
        PRIVATE
    }

    private string serverName = "ATLYSS Server";
    private LobbyTypeTag serverType = LobbyTypeTag.PUBLIC;
    private AtlyssSteamLobbyTag serverTag = AtlyssSteamLobbyTag.GAIA;
    private string serverPassword = string.Empty;
    private string serverMOTD = string.Empty;
    private int serverMaxPlayers = 16;
    private int hostCharSaveSlot = 0;

    private bool shouldHostServer = false;
    private bool hostSpawned = false;
    private bool actionTriggered = false;
    private float timeSinceSpawn = 0f;

    private string playerName = string.Empty;

    internal static new ManualLogSource Logger;

    private string GetArgValue(string[] args, string key)
    {
        int index = Array.IndexOf(args, key);
        return (index >= 0 && index + 1 < args.Length) ? args[index + 1] : null;
    }

    private void LogServerConfig()
    {
        Logger.LogInfo("=== Server Configuration ===");
        Logger.LogInfo($"Server Name      : {serverName}");
        Logger.LogInfo($"Server Type      : {serverType}");
        Logger.LogInfo($"Server Tag       : {serverTag}");
        Logger.LogInfo($"Server Password  : {(string.IsNullOrEmpty(serverPassword) ? "[None]" : serverPassword)}");
        Logger.LogInfo($"Server MOTD      : {(string.IsNullOrEmpty(serverMOTD) ? "[None]" : serverMOTD)}");
        Logger.LogInfo($"Max Players      : {serverMaxPlayers}");
        Logger.LogInfo("============================");
    }

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

        if (!Application.isBatchMode)
        {
            Logger.LogWarning("Not running in batchmode, DedicatedServer plugin exiting.");
            return;
        }

        string[] args = Environment.GetCommandLineArgs();
        if (args.Contains("-server"))
        {
            Logger.LogInfo("Starting in dedicated server mode.");
            shouldHostServer = true;

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

            // Server host char save slot with range check
            if (int.TryParse(GetArgValue(args, "-hostsave"), out int hostSlot))
            {
                if (hostSlot >= 0 && hostSlot <= 104)
                {
                    hostCharSaveSlot = hostSlot;
                }
                else
                {
                    hostCharSaveSlot = 0;
                    Logger.LogWarning("HostSave must be between 0 and 104. Defaulting to 0.");
                }
            }
            else
            {
                hostCharSaveSlot = 0;
            }

            serverName = GetArgValue(args, "-name") ?? "ATLYSS Server";

            if (serverName.Length > 20)
            {
                Logger.LogWarning($"Server name \"{serverName}\" is too long ({serverName.Length}/20). Defaulting to \"ATLYSS Server\".");
                serverName = "ATLYSS Server";
            }

            serverPassword = GetArgValue(args, "-password") ?? "";
            serverMOTD = GetArgValue(args, "-motd") ?? "Welcome to the server!";

            // Handle server type
            var typeFlags = new[] { "-public", "-private", "-friends" }.Where(args.Contains).ToList();
            if (typeFlags.Count > 1)
            {
                Logger.LogWarning($"Multiple server type flags detected ({string.Join(", ", typeFlags)}). Defaulting to PUBLIC.");
                serverType = LobbyTypeTag.PUBLIC;
            }
            else if (typeFlags.Count == 1)
            {
                serverType = typeFlags[0] switch
                {
                    "-public" => LobbyTypeTag.PUBLIC,
                    "-private" => LobbyTypeTag.PRIVATE,
                    "-friends" => LobbyTypeTag.FRIENDS,
                    _ => LobbyTypeTag.PUBLIC
                };
            }
            else
            {
                serverType = LobbyTypeTag.PUBLIC;
            }

            // Handle server focus
            var focusFlags = new[] { "-pve", "-pvp", "-social", "-rp" }.Where(args.Contains).ToList();
            if (focusFlags.Count > 1)
            {
                Logger.LogWarning($"Multiple lobby focus flags detected ({string.Join(", ", focusFlags)}). Defaulting to PVE.");
                serverTag = AtlyssSteamLobbyTag.GAIA;
            }
            else if (focusFlags.Count == 1)
            {
                serverTag = focusFlags[0] switch
                {
                    "-pve" => AtlyssSteamLobbyTag.GAIA,
                    "-pvp" => AtlyssSteamLobbyTag.DUALOS,
                    "-social" => AtlyssSteamLobbyTag.NOTH,
                    "-rp" => AtlyssSteamLobbyTag.LOODIA,
                    _ => AtlyssSteamLobbyTag.GAIA
                };
            }
            else
            {
                serverTag = AtlyssSteamLobbyTag.GAIA;
            }

            // Max players with range check
            if (int.TryParse(GetArgValue(args, "-maxplayers"), out int maxPlayers))
            {
                if (maxPlayers >= 2 && maxPlayers <= 250)
                {
                    serverMaxPlayers = maxPlayers;
                }
                else
                {
                    serverMaxPlayers = 16;
                    Logger.LogWarning("MaxPlayers must be between 2 and 250. Defaulting to 16.");
                }
            }
            else
            {
                serverMaxPlayers = 16;
            }

            LogServerConfig();
        }
    }

    private bool detected = false;

    void Update()
    {
        if (!shouldHostServer) return;

        if (!hostSpawned)
        {
            if (NetworkClient.localPlayer != null)
            {
                hostSpawned = true;
                Logger.LogInfo("[HostSpawnDetector] Host player spawned. Starting delay...");
            }
        }
        else if (!actionTriggered)
        {
            timeSinceSpawn += Time.deltaTime;

            if (timeSinceSpawn >= 30f)
            {
                actionTriggered = true;
                OnHostReady();
            }
        }

        if (detected) return;

        if (GameObject.FindObjectOfType<MainMenuManager>() != null)
        {
            detected = true;
            HostServer();
        }
    }

    private void OnHostReady()
    {
        Logger.LogInfo("[HostSpawnDetector] 30 seconds passed since host spawned — teleporting!");

        Player hostPlayer = GameObject.Find("[connID: 0] _player(" + playerName + ")").GetComponent<Player>();
        CharacterController hostCharacterController = hostPlayer.GetComponent<CharacterController>();

        hostCharacterController.enabled = false;
        hostPlayer.transform.SetPositionAndRotation(new Vector3(500, 50, 510), new Quaternion(0, 0, 0, 0));
        hostCharacterController.enabled = true;
    }

    private void HostServer()
    {
        Logger.LogInfo("[DedicatedServer] Hosting Server");

        ServerHostSettings_Profile hostSettingsProfile = ProfileDataManager._current._hostSettingsProfile;
        AtlyssNetworkManager anm = AtlyssNetworkManager._current;
        ProfileDataManager pdm = ProfileDataManager._current;
        LobbyListManager llm = LobbyListManager._current;

        anm._steamworksMode = true;

        anm._soloMode = false;
        anm._serverMode = false;

        anm._serverName = serverName;
        anm._serverPassword = serverPassword;
        anm._sentPassword = serverPassword;
        anm._serverMotd = serverMOTD;
        anm.maxConnections = serverMaxPlayers;

        llm._lobbyPasswordInput.text = anm._serverPassword;
        llm._lobbyTypeDropdown.value = (int)serverType;
        llm._hostLobbyRealm = serverTag;

        anm._bannedClientList.Clear();
        anm._mutedClientList.Clear();

        if (hostSettingsProfile._banList != null)
        {
            anm._bannedClientList.AddRange(hostSettingsProfile._banList);
        }
        if (hostSettingsProfile._mutedList != null)
        {
            anm._mutedClientList.AddRange(hostSettingsProfile._mutedList);
        }

        ELobbyType lobbyType = ELobbyType.k_ELobbyTypePublic;
        switch ((int)serverType)
        {
            case 0:
                lobbyType = ELobbyType.k_ELobbyTypePublic;
                break;
            case 1:
                lobbyType = ELobbyType.k_ELobbyTypeFriendsOnly;
                break;
            case 2:
                lobbyType = ELobbyType.k_ELobbyTypePrivate;
                break;
        }


        playerName = pdm._characterFiles[hostCharSaveSlot]._nickName;
        pdm._characterFile = pdm._characterFiles[hostCharSaveSlot];

        SteamLobby._current.HostLobby(lobbyType);
        MainMenuManager._current._characterSelectManager.Send_CharacterFile();
    }

    [HarmonyPatch(typeof(ChatBehaviour), "New_ChatMessage")]
    class ChatMirrorPatch
    {
        static void Postfix(string _message)
        {
            string cleanMessage = StripUnityRichText(_message);
            Console.WriteLine($"[Chat] {cleanMessage}");
        }

        static string StripUnityRichText(string input)
        {
            return Regex.Replace(input, "<.*?>", string.Empty);
        }
    }

    // Mutes game audio
    [HarmonyPatch(typeof(SettingsManager), "Handle_AudioParameters")]
    class AudioParamsPatch
    {
        static bool Prefix(SettingsManager __instance)
        {
            AudioListener.volume = 0f;

            return false;
        }
    }

    [HarmonyPatch(typeof(NetworkManager), "StopHost")]
    public class StopHostPatch
    {
        static void Postfix(NetworkManager __instance)
        {
            Debug.Log("[StopHostPatch] Host stopped. Scheduling shutdown in 5 seconds.");

            var obj = new GameObject("ShutdownScheduler");
            GameObject.DontDestroyOnLoad(obj);
            obj.AddComponent<ShutdownDelay>();
        }
    }

    public class ShutdownDelay : MonoBehaviour
    {
        private void Start()
        {
            StartCoroutine(DelayedShutdown());
        }

        private IEnumerator DelayedShutdown()
        {
            yield return new WaitForSeconds(5f);

            Debug.Log("[ShutdownDelay] Shutting down game...");
            Application.Quit(); // clean exit
        }
    }
}

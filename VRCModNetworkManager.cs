using CCom;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VRC.Core;
using VRCMelonCore;
using VRCMelonCore.Extensions;
using VRCModNetwork.commands;
using VRCModNetwork.Serializables;
using VRCModNetwork.utils;

namespace VRCModNetwork
{
    public class VRCModNetworkManager : IConnectionListener
    {

        private static readonly string SERVER_ADDRESS = Environment.CommandLine.Contains("--vrctools.dev") ? "localhost" : "vrchat2.survival-machines.fr";
        private static readonly int SERVER_PORT = (Application.platform == RuntimePlatform.WindowsPlayer ? (Environment.CommandLine.Contains("--vrctools.dev") ? 26345 : 26342) : 26342);
        private static readonly string VRCMODNW_VERSION = "1.2";

        private static Client client;
        public static ConnectionState State { private set; get; }
        public static bool IsAuthenticated
        {
            get
            {
                return authenticated;
            }
            internal set
            {
                if (value != authenticated)
                {
                    authenticated = value;
                    MelonModLogger.Log("IsAuthenticated: " + authenticated);
                    //if (value) OnAuthenticated?.Invoke();
                    //else OnLogout?.Invoke();
                    OnLogout?.Invoke();
                }
            }
        }
        private static bool authenticated;

        public static int VRCAuthStatus
        {
            get => vrcAuthStatus;
            internal set
            {
                vrcAuthStatus = value;
                if (value == 1)
                    OnAuthenticated?.Invoke();
            }
        }
        private static int vrcAuthStatus = 0;

        public static event Action OnConnected;
        public static event Action<string> OnDisconnected;
        public static event Action OnAuthenticated;
        public static event Action OnLogout;

        private static Dictionary<string, Action<string, string>> rpcListeners = new Dictionary<string, Action<string, string>>();
        private static VRCModNetworkManager instance;
        private static readonly object userDatasLock = new object();

        internal static string userUuid = "";
        private static string userInstanceId = "";
        private static string roomSecret = "";
        private static List<ModDesc> modlist = new List<ModDesc>();
        private static string credentials = "";
        internal static string authError = "";

        private static Thread modsCheckerThread;
        private static List<Action> sheduled = new List<Action>();

        private VRCModNetworkManager()
        {
            client.SetConnectionListener(this);
            CommandManager.RegisterCommand("RPC", typeof(RPCCommand));
            CommandManager.RegisterCommand("AUTH", typeof(AuthCommand));
            CommandManager.RegisterCommand("LOGOUT", typeof(LogoutCommand));
            CommandManager.RegisterCommand("INSTANCECHANGED", typeof(InstanceChangedCommand));
            CommandManager.RegisterCommand("MODLISTCHANGED", typeof(ModListChangedCommand));
            CommandManager.RegisterCommand("VRCLINK", typeof(VRCLinkCommand));
        }

        internal static void ConnectAsync()
        {
            if (State != ConnectionState.DISCONNECTED)
                MelonModLogger.LogError("Trying to connect to server, but client is not disconnected");
            else if (client != null && client.autoReconnect)
                MelonModLogger.LogError("Trying to connect to server, but client already exist and is tagged as auto-reconnecting");
            else
            {
                if (client == null)
                {
                    client = new Client(SERVER_ADDRESS, SERVER_PORT, VRCMODNW_VERSION);
                    if (instance == null) instance = new VRCModNetworkManager();
                    client.SetConnectionListener(instance);
                    //client.autoReconnect = true;
                    MelonModLogger.Log("Client autoReconnect set to true");
                    if (modsCheckerThread == null)
                    {
                        modsCheckerThread = new Thread(ModCheckThread)
                        {
                            Name = "Mod Check Thread",
                            IsBackground = true
                        };
                        modsCheckerThread.Start();
                    }
                }
                State = ConnectionState.CONNECTING;
                client.StartConnection();
            }
        }

        internal static IEnumerator ConnectInit()
        {
            bool retry = true;
            do
            {
                MelonModLogger.Log("Connecting to VRCModNetwork...");
                MelonModLogger.Log("VRCUiPopupManager: " + VRCUiPopupManager.prop_VRCUiPopupManager_0);
                VRCUiPopupManager.prop_VRCUiPopupManager_0.ShowStandardPopup("VRCTools", "Connecting to VRCModNetwork...", p => { });

                MelonModLogger.Log("ConnectAsync");
                ConnectAsync();

                MelonModLogger.Log("Waiting for connection");
                while (VRCModNetworkManager.State != VRCModNetworkManager.ConnectionState.CONNECTED && VRCModNetworkManager.State != VRCModNetworkManager.ConnectionState.DISCONNECTED)
                    yield return null;
                MelonModLogger.Log("Done.");

                VRCUiPopupManager.prop_VRCUiPopupManager_0.HideCurrentPopup();

                if (VRCModNetworkManager.State == ConnectionState.DISCONNECTED)
                {
                    MelonModLogger.Log("Showing disconnected popup");
                    client.autoReconnect = false; // avoid doing 2 reconnections at the same time
                    bool waitforpopup = true;
                    VRCUiPopupManager.prop_VRCUiPopupManager_0.ShowStandardPopup("VRCTools", "Connection to the VRCModNetwork failed", "Retry", () => waitforpopup = false, "Ignore", () => retry = waitforpopup = false);
                    while (waitforpopup)
                        yield return null;
                }
                else
                    retry = false;
            }
            while (retry);

            client.autoReconnect = true;
        }

        /// <summary>
        /// Send a RPC to the VRCMod Network dispatcher.
        /// <para>Only works with RPCs of type: CLIENT_TO_SERVER, SERVER_TO_ALL_CLIENTS, CLIENT_TO_CLIENTROOM</para>
        /// </summary>
        public static void SendRPC(string rpcId, string rpcData = "", Action onSuccess = null, Action<string> onError = null)
        {
            if (rpcData.Contains("\n") || rpcData.Contains("\r")) throw new ArgumentException("Invalid rpcData. It have to be a one-line rpc");
            RPCCommand rpccommand = CommandManager.CreateInstance("RPC", client, true) as RPCCommand;
            rpccommand.SendCommand(rpcId, rpcData, onSuccess, onError);
        }

        /// <summary>
        /// Send a RPC to the VRCMod Network dispatcher.
        /// <para>Only works with RPCs of type: CLIENT_TO_SERVER, SERVER_TO_ALL_CLIENTS, CLIENT_TO_CLIENTROOM</para>
        /// </summary>
        public static void SendRPCNoLog(string rpcId, string rpcData = "", Action onSuccess = null, Action<string> onError = null)
        {
            if (rpcData.Contains("\n") || rpcData.Contains("\r")) throw new ArgumentException("Invalid rpcData. It have to be a one-line rpc");
            RPCCommand rpccommand = CommandManager.CreateInstance("RPC", client, false) as RPCCommand;
            rpccommand.SendCommand(rpcId, rpcData, onSuccess, onError);
        }

        /// <summary>
        /// Send a RPC to the VRCMod Network dispatcher.
        /// <para>Only works with RPCs of type: SERVER_TO_CLIENT, CLIENT_TO_CLIENT</para>
        /// </summary>
        public static void SendRPCToTarget(string rpcId, string targetId, string rpcData = "", Action onSuccess = null, Action<string> onError = null)
        {
            if (rpcData.Contains("\n") || rpcData.Contains("\r")) throw new ArgumentException("Invalid rpcData. It have to be a one-line rpc");
            RPCCommand rpccommand = CommandManager.CreateInstance("RPC", client, true) as RPCCommand;
            rpccommand.SendCommand(rpcId, targetId, rpcData, onSuccess, onError);
        }

        /// <summary>
        /// Send a RPC to the VRCMod Network dispatcher.
        /// <para>Only works with RPCs of type: SERVER_TO_CLIENT, CLIENT_TO_CLIENT</para>
        /// </summary>
        public static void SendRPCToTargetNoLog(string rpcId, string targetId, string rpcData = "", Action onSuccess = null, Action<string> onError = null)
        {
            if (rpcData.Contains("\n") || rpcData.Contains("\r")) throw new ArgumentException("Invalid rpcData. It have to be a one-line rpc");
            RPCCommand rpccommand = CommandManager.CreateInstance("RPC", client, false) as RPCCommand;
            rpccommand.SendCommand(rpcId, targetId, rpcData, onSuccess, onError);
        }

        public static void SetRPCListener(string rpcId, Action<string, string> listener) => rpcListeners[rpcId] = listener;
        public static void ClearRPCListener(string rpcId) => rpcListeners.Remove(rpcId);




        internal static void HandleRpc(string sender, string rpcId, string data)
        {
            SheduleForMainThread(() =>
            {
                if (rpcListeners.TryGetValue(rpcId, out Action<string, string> listener))
                {
                    try
                    {
                        listener(sender, data);
                    }
                    catch (Exception e)
                    {
                        MelonModLogger.LogError("Error while handling rpc " + rpcId + ": " + e);
                    }
                }
            });

        }




        public void ConnectionStarted() => State = ConnectionState.CONNECTING;
        public void WaitingForConnection() => State = ConnectionState.CONNECTION_ETABLISHED;
        public void Connecting() => State = ConnectionState.CONNECTION_ETABLISHED;
        public void ConnectionFailed(string error) => State = ConnectionState.DISCONNECTED;
        public void Connected()
        {
            State = ConnectionState.CONNECTED;
            OnConnected?.Invoke();
        }
        public void Disconnected(string error)
        {
            State = ConnectionState.DISCONNECTED;
            ResetDatas();
            OnDisconnected?.Invoke(error);
        }

        private static void ResetDatas()
        {
            lock (userDatasLock)
            {
                userUuid = "";
                userInstanceId = "";
                modlist.Clear();
                VRCAuthStatus = 0;
                IsAuthenticated = false;
                authError = "";
            }
        }

        internal static void Update()
        {
            lock (sheduled)
            {
                foreach (Action a in sheduled)
                {
                    try
                    {
                        a?.Invoke();
                    }
                    catch (Exception e)
                    {
                        MelonModLogger.LogError("An error occured while running sheduled action: " + e);
                    }
                }
                sheduled.Clear();
            }

            if (State == ConnectionState.CONNECTED || State == ConnectionState.NEED_REAUTH)
            {
                lock (userDatasLock)
                {
                    // Check if user changed
                    string uuid = APIUser.CurrentUser?.id ?? "";

                    if (!uuid.Equals(userUuid))
                    {
                        MelonModLogger.Log("new UUID: " + (uuid ?? "{null string object}") + ". Old uuid: " + (userUuid ?? "{null string object}"));

                        userUuid = uuid; // use it as a lock to avoid spamming
                        if (uuid.Equals(""))
                        {
                            MelonModLogger.Log("Resetting data");
                            ResetDatas();
                            MelonModLogger.Log("Logging out");
                            LogoutCommand logoutCommand = CommandManager.CreateInstance("LOGOUT", client) as LogoutCommand;
                            logoutCommand.LogOut();
                            MelonModLogger.Log("Done");
                        }
                        else
                        {
                            if (SecurePlayerPrefs.HasKey("vrcmnw_un_" + uuid) && SecurePlayerPrefs.HasKey("vrcmnw_pw_" + uuid))
                            {
                                string username = SecurePlayerPrefs.GetString("vrcmnw_un_" + uuid, "vl9u1grTnvXA");
                                string password = SecurePlayerPrefs.GetString("vrcmnw_pw_" + uuid, "vl9u1grTnvXA");

                                Auth(username, password, uuid, () =>
                                {
                                    SecurePlayerPrefs.SetString("vrcmnw_un_" + uuid, username, "vl9u1grTnvXA");
                                    SecurePlayerPrefs.SetString("vrcmnw_pw_" + uuid, password, "vl9u1grTnvXA");
                                }, (error) =>
                                {
                                    if (error.StartsWith("BANNED_ACCOUNT"))
                                        authError = "Banned: " + error.Substring("BANNED_ACCOUNT ".Length);
                                    else if (error == "INVALID_VRCID")
                                        authError = "VRChat account not owned";

                                    State = ConnectionState.NEED_REAUTH;
                                });
                            }
                            else
                            {
                                State = ConnectionState.NEED_REAUTH;
                            }
                        }
                    }

                    if (IsAuthenticated)
                    {
                        string roomId = "";
                        if (RoomManagerBase.field_ApiWorld_0?.currentInstanceIdOnly != null)
                        {
                            roomId = RoomManagerBase.field_ApiWorld_0.id + ":" + RoomManagerBase.field_ApiWorld_0.currentInstanceIdWithTags;
                        }
                        if (!userInstanceId.Equals(roomId))
                        {
                            MelonModLogger.Log("Updating instance id. Current room: " + roomId);
                            userInstanceId = roomId;
                            ((InstanceChangedCommand)CommandManager.CreateInstance("INSTANCECHANGED", client)).Send(roomId);
                            MelonModLogger.Log("Done");
                        }
                    }
                }
            }
        }
        /* OLD
        private static void TryAuthenticate(string authData)
        {
            if (RoomManagerBase.currentRoom != null && RoomManagerBase.currentRoom.id != null && RoomManagerBase.currentRoom.currentInstanceIdWithTags != null)
                userInstanceId = RoomManagerBase.currentRoom.id + ":" + RoomManagerBase.currentRoom.currentInstanceIdWithTags;
            modlist = ModDesc.GetAllMods();
            ApiServerEnvironment env = VRCApplicationSetup._instance.ServerEnvironment;
            string stringEnv = "release";
            if (env == ApiServerEnvironment.Dev) stringEnv = "dev";
            MelonModLogger.Log("Authenticating...");
            AuthCommand authCommand = CommandManager.CreateInstance("AUTH", client, false) as AuthCommand;
            authCommand.Auth(userUuid, authData, stringEnv, userInstanceId, roomSecret, modlist);
        }
        */

        internal static void Auth(string username, string password, string uuid, Action onSuccess, Action<string> onError)
        {
            MelonModLogger.Log("VRCModNetworkManager::Auth called");
            //userUuid = uuid;
            if (RoomManagerBase.field_ApiWorld_0 != null && RoomManagerBase.field_ApiWorld_0.id != null && RoomManagerBase.field_ApiWorld_0.currentInstanceIdWithTags != null)
                userInstanceId = RoomManagerBase.field_ApiWorld_0.id + ":" + RoomManagerBase.field_ApiWorld_0.currentInstanceIdWithTags;
            MelonModLogger.Log("userInstanceId: " + userInstanceId);
            modlist = ModDesc.GetAllMods();
            MelonModLogger.Log("Authenticating...");
            AuthCommand authCommand = CommandManager.CreateInstance("AUTH", client, false) as AuthCommand;
            authCommand.Auth(username, password, uuid, userInstanceId, roomSecret, modlist, onSuccess, onError);
        }

        internal static void LinkVRCAccount()
        {
            //VRCUiPopupManager.prop_VRCUiPopupManager_0.ShowStandardPopup("Login Failed", "Unable to link account: Not Supported", "Close", () => VRCUiPopupManager.prop_VRCUiPopupManager_0.HideCurrentPopup());
            //Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials))
            VRCLinkCommand command = CommandManager.CreateInstance("VRCLINK", client, false) as VRCLinkCommand;
            MelonModLogger.Log("LinkVRCAccount called. Token provider is " + ApiCredentials.GetAuthTokenProvider() + ".");
            if (ApiCredentials.GetAuthTokenProvider() == "steam")
            {
                MelonModLogger.Log("Logging in using Steam token");
                VRCUiPopupManager.prop_VRCUiPopupManager_0.ShowStandardPopup("VRCTools", "Linking VRChat account...");
                command.LinkSteam(APIUser.CurrentUser.id, SteamUtils.GetSteamTicket(), () =>
                {
                    VRCAuthStatus = 1;
                    VRCUiPopupManager.field_VRCUiPopupManager_0.HideCurrentPopup();
                }, (e) =>
                {
                    VRCUiPopupManager.prop_VRCUiPopupManager_0.ShowStandardPopup("VRCTools", "Unable to link account using Steam: " + e, "Close", () => VRCUiPopupManager.prop_VRCUiPopupManager_0.HideCurrentPopup());
                });
            }
            else if (ApiCredentials.GetAuthTokenProvider() == "vrchat")
            {
                if (!string.IsNullOrEmpty(credentials))
                {
                    MelonModLogger.Log("Logging in using VRChat credentials");
                    command.LinkCrendentials(APIUser.CurrentUser.id, Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)), () =>
                    {
                        VRCAuthStatus = 1;
                        VRCUiPopupManager.prop_VRCUiPopupManager_0.HideCurrentPopup();
                    }, (e) =>
                    {
                        VRCUiPopupManager.prop_VRCUiPopupManager_0.ShowStandardPopup("VRCTools", "Unable to link account using VRChat crendentials: " + e, "Close", () => VRCUiPopupManager.prop_VRCUiPopupManager_0.HideCurrentPopup());
                    });
                    credentials = "";
                }
                else
                {
                    MelonModLogger.LogError("Unable to auth: Required auth datas are missing");
                    VRCUiPopupManager.prop_VRCUiPopupManager_0.ShowStandardPopup("VRCTools", "Unable to link account using VRChat crendentials: Required auth datas are missing. Please log out and log back in.", "Close", () => VRCUiPopupManager.prop_VRCUiPopupManager_0.HideCurrentPopup());
                    State = ConnectionState.NEED_REAUTH;
                    return;
                }
            }
            else
            {
                MelonModLogger.LogError("Unable to auth: Unsupported VRChat token provider (" + ApiCredentials.GetAuthTokenProvider() + ")");
                VRCUiPopupManager.prop_VRCUiPopupManager_0.ShowStandardPopup("VRCTools", "Unable to link account: Unsupported VRChat token provider. Please report this error to Slaynash#2879 on discord.", "Close", () => VRCUiPopupManager.prop_VRCUiPopupManager_0.HideCurrentPopup());
                State = ConnectionState.NEED_REAUTH;
                return;
            }
        }

        internal static void SetCredentials(string credentials_)
        {
            MelonModLogger.Log("SetCredentials called with a length of " + credentials_.Length + ".");
            credentials = credentials_;
        }

        internal static void SheduleForMainThread(Action a)
        {
            lock (sheduled) sheduled.Add(a);
        }

        private static void ModCheckThread()
        {
            while (true)
            {
                lock (userDatasLock)
                {
                    if (IsAuthenticated)
                    {
                        List<ModDesc> newModlist = ModDesc.GetAllMods();
                        bool identical = true;
                        if (newModlist.Count != modlist.Count)
                        {
                            identical = false;
                        }
                        else
                        {
                            foreach (ModDesc mod in newModlist)
                            {
                                bool found = false;
                                foreach (ModDesc mod2 in modlist)
                                {
                                    if (
                                        mod2.name.Equals(mod.name) &&
                                        mod2.version.Equals(mod.version) &&
                                        mod2.author.Equals(mod.author) &&
                                        ((mod2.downloadLink == null && mod.downloadLink == null) || (mod2.downloadLink != null && mod2.downloadLink.Equals(mod.downloadLink))) &&
                                        mod2.baseClass.Equals(mod.baseClass))
                                    {
                                        found = true;
                                        break;
                                    }
                                }
                                if (!found)
                                {
                                    identical = false;
                                    break;
                                }
                            }
                        }
                        if (!identical)
                        {
                            modlist = newModlist;
                            ((ModListChangedCommand)CommandManager.CreateInstance("MODLISTCHANGED", client)).Send("{\"modlist\":[" + ModDesc.CreateModlistJson(modlist) + "]}");
                        }
                    }
                }
                Thread.Sleep(1000);
            }
        }

        public enum ConnectionState
        {
            DISCONNECTED,
            CONNECTION_ETABLISHED,
            CONNECTING,
            CONNECTED,
            NEED_REAUTH
        }
    }
}

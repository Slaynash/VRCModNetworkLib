using BestHTTP;
using Harmony;
using I2.Loc;
using MelonLoader;
using NET_SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using VRC.Core;
using VRCMelonCore.Extensions;
using static UnityEngine.UI.Button;

namespace VRCModNetwork
{
    internal class VRCModNetworkLogin
    {
        private static bool vrcmnwConnected = false;
        internal static bool VrcmnwConnected { get => vrcmnwConnected; private set => vrcmnwConnected = value; }
        private static ApiContainer vrcmnwLoginCallbackContainer = null;
        private static Il2CppSystem.Action<ApiContainer> vrcmnwLoginCallback = null;
        private static GameObject vrcmnwLoginPageGO = null;

        private static UiInputField vrcmnwUsernameField;
        private static UiInputField vrcmnwPasswordField;

        private static Il2CppSystem.Action popupCompleteCallback;
        private static IntPtr sendRequestMethodPtr;
        private static NET_SDK.Harmony.Patch sendRequestPatch;
        private static IntPtr originalsendRequestI2CMethodPtr;
        private static PatchedMethodDelegateType originalsendRequestI2CMethod;


        //private static HarmonyMethod GetPatch(string name) => new HarmonyMethod(typeof(VRCModNetworkLogin).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic));

        internal static void SetupVRCModNetworkLoginPage()
        {
            //Duplicate UI Page
            MelonModLogger.Log("Creating VRCMNWLoginPage");
            GameObject vrchatLoginScreen = GameObject.Find("UserInterface/MenuContent/Screens/Authentication/LoginUserPass");
            vrcmnwLoginPageGO = GameObject.Instantiate(vrchatLoginScreen, vrchatLoginScreen.transform.parent, false);
            if (vrcmnwLoginPageGO != null)
            {
                vrcmnwLoginPageGO.name = "VRCMNWLoginPage";

                UiInputField[] fields = vrcmnwLoginPageGO.GetComponentsInChildren<UiInputField>();
                vrcmnwUsernameField = fields[0];
                vrcmnwPasswordField = fields[1];

                //Overwrite Back Button to "Skip"
                MelonModLogger.Log("Overwriting Back Button");
                Button buttonBack = vrcmnwLoginPageGO.transform.Find("ButtonBack (1)")?.GetComponent<Button>();
                if (buttonBack != null)
                {
                    buttonBack.transform.GetComponentInChildren<Text>().text = "Skip";
                    buttonBack.onClick = new ButtonClickedEvent();
                    buttonBack.onClick.AddListener(new Action(() =>
                    {
                        if (vrcmnwLoginCallback != null && vrcmnwLoginCallbackContainer != null)
                            try
                            {
                                VRCUiPopupManager.prop_VRCUiPopupManager_0.ShowStandardPopup("VRChat", "Logging in...", p => { });
                                FinishLogin();
                            }
                            catch (Exception e)
                            {
                                MelonModLogger.Log("An error occured while calling login callback: " + e);
                            }
                        else
                            MelonModLogger.LogError("[VRCTools] vrcmnwLoginCallback or vrcmnwLoginCallbackContainer not set ! (" + (vrcmnwLoginCallback != null ? "true" : "false") + " / " + (vrcmnwLoginCallbackContainer != null ? "true" : "false") + ")");
                    }));

                }
                else
                    MelonModLogger.LogError("[VRCTools] Unable to find ButtonDone (1){UnityEngine.UI.Text}");

                //Overwrite Done Button
                MelonModLogger.Log("Overwriting Done Button");
                Button buttonDone = vrcmnwLoginPageGO.transform.Find("ButtonDone (1)")?.GetComponent<Button>();
                if (buttonDone != null)
                {
                    buttonDone.onClick = new ButtonClickedEvent();
                    buttonDone.onClick.AddListener(new Action(() =>
                    {
                        MelonModLogger.Log("Validating form");
                        if (InputFieldValidatorExtension.IsFormInputValid(vrcmnwLoginPageGO))
                        {
                            MelonModLogger.Log("Fetching form values");
                            string username = vrcmnwUsernameField.field_String_2;
                            string password = vrcmnwPasswordField.field_String_2;

                            TryLoginToVRCModNetwork(username, password, (error) =>
                            {
                                string errorHR = null;
                                if (error == "INTERNAL_SERVER_ERROR")
                                    errorHR = "Internal server error";
                                else if (error == "INVALID_CREDENTIALS")
                                    errorHR = "Invalid credentials";
                                else if (error.StartsWith("BANNED_ACCOUNT"))
                                    errorHR = "Your account is currently banned. Reason: " + error.Substring("BANNED_ACCOUNT ".Length);
                                else if (error == "INVALID_VRCID")
                                    errorHR = "The current VRChat account isn't owned by this VRCModNetwork account";
                                else
                                    errorHR = error;
                                VRCUiPopupManager.prop_VRCUiPopupManager_0.ShowStandardPopup("Login Failed", "Unable to login to the VRCModNetwork: " + errorHR, "Close", () => VRCUiPopupManager.prop_VRCUiPopupManager_0.HideCurrentPopup(), p => { });
                            });
                        }
                        else
                            VRCUiPopupManager.prop_VRCUiPopupManager_0.ShowStandardPopup("Cannot Login", "Please fill out valid data for each input.", "Close", () => VRCUiPopupManager.prop_VRCUiPopupManager_0.HideCurrentPopup(), p => { });
                    }));

                }
                else
                    MelonModLogger.LogError("[VRCTools] Unable to find ButtonDone (1){UnityEngine.UI.Text}");

                //Change "Login" title to "VRCModNetwork Login"
                MelonModLogger.Log("Overwriting BoxLogin title");
                Text boxTitle = vrcmnwLoginPageGO.transform.Find("BoxLogin/Text").GetComponent<Text>();
                if (boxTitle != null)
                {
                    boxTitle.GetComponent<Localize>().enabled = false;
                    boxTitle.text = "VRCModNetwork Login";
                }
                else
                    MelonModLogger.LogError("[VRCTools] Unable to find BoxLogin/Text{UnityEngine.UI.Text}");

                MelonModLogger.Log("Overwriting TextWelcome Text");
                Text textWelcome = vrcmnwLoginPageGO.transform.Find("TextWelcome").GetComponent<Text>();
                if (textWelcome != null)
                    textWelcome.text = "Welcome VRCTools User !";
                else
                    MelonModLogger.LogError("[VRCTools] Unable to find BoxLogin/Text{UnityEngine.UI.Text}");

                //Add "Register" panel
                MelonModLogger.Log("Adding register panel");
                GameObject vrchatLoginCreateScreen = GameObject.Find("UserInterface/MenuContent/Screens/Authentication/LoginCreateFromWebsite");
                GameObject vrcmnwLoginCreatePageGO = GameObject.Instantiate(vrchatLoginCreateScreen, vrchatLoginCreateScreen.transform.parent, false);
                if (vrcmnwLoginCreatePageGO != null)
                {
                    vrcmnwLoginCreatePageGO.GetComponent<LaunchVRChatWebsiteRegistration>().enabled = false;
                    vrcmnwLoginCreatePageGO.name = "VRCMNLoginCreate";
                    vrcmnwLoginCreatePageGO.transform.Find("ButtonAboutUs").gameObject.SetActive(false);
                    Button buttonLogin = vrcmnwLoginCreatePageGO.transform.Find("ButtonLogin").GetComponent<Button>();
                    if (buttonLogin != null)
                    {
                        buttonLogin.onClick.RemoveAllListeners();
                        buttonLogin.onClick.AddListener(new Action(() =>
                        {
                            ShowVRCMNWLoginMenu(false);
                        }));

                    }
                    else
                        MelonModLogger.LogError("[VRCTools] Unable to find ButtonLogin{UnityEngine.UI.Text}");
                }
                else
                    MelonModLogger.LogError("[VRCTools] Unable to find UserInterface/MenuContent/Screens/Authentication/LoginCreateFromWebsite");

                //Add "Register" button
                MelonModLogger.Log("Adding register button");
                GameObject aboutusButton = vrcmnwLoginPageGO.transform.Find("ButtonAboutUs").gameObject;

                GameObject registerButtonGO = GameObject.Instantiate(aboutusButton, vrcmnwLoginPageGO.transform, false);
                if (registerButtonGO != null)
                {
                    Button registerButton = registerButtonGO.GetComponent<Button>();
                    registerButtonGO.GetComponentInChildren<Localize>().enabled = false;
                    registerButtonGO.GetComponentInChildren<Text>().text = "Register";
                    RectTransform rt = registerButtonGO.GetComponent<RectTransform>();
                    rt.localPosition = new Vector3(0, -270, 0);
                    rt.sizeDelta -= new Vector2(0, 30);
                    registerButton.onClick = new ButtonClickedEvent();
                    registerButton.onClick.AddListener(new Action(() =>
                    {
                        Application.OpenURL("https://vrchat.survival-machines.fr/register");
                        VRCUiManager.prop_VRCUiManager_0.ShowScreen("UserInterface/MenuContent/Screens/Authentication/VRCMNLoginCreate");
                    }));
                }
                else
                    MelonModLogger.LogError("[VRCTools] Unable to find ButtonAboutUs");


                //Remove "About Us" Button
                MelonModLogger.Log("Removing ButtonAboutUs");
                vrcmnwLoginPageGO.transform.Find("ButtonAboutUs").gameObject.SetActive(false);

                //Remove VRChat Logo
                MelonModLogger.Log("Removing VRChat Logo");
                vrcmnwLoginPageGO.transform.Find("VRChat_LOGO (1)").gameObject.SetActive(false);

                RectTransform box = vrcmnwLoginPageGO.transform.Find("BoxLogin").GetComponent<RectTransform>();
                box.localPosition += new Vector3(0, 20, 0);



                Text welc = vrcmnwLoginPageGO.transform.Find("TextWelcome").GetComponent<Text>();

                /*Text patreon = GameObject.Instantiate(welc, welc.transform.parent);
                patreon.color = new Color(0.98f, 0.41f, 0.33f);
                patreon.text = "patreon.com/Slaynash";
                patreon.GetComponent<RectTransform>().localPosition = new Vector3(300, -460);
                patreon.fontSize /= 2;

                Text discord = GameObject.Instantiate(welc, welc.transform.parent);
                discord.color = new Color(0.44f, 0.54f, 0.85f);
                discord.text = "discord.gg/rCqKSvR";
                discord.GetComponent<RectTransform>().localPosition = new Vector3(-300, -460);
                discord.fontSize /= 2;
                */

                Text discord = GameObject.Instantiate(welc, welc.transform.parent);
                discord.color = new Color(0.44f, 0.54f, 0.85f);
                discord.text = "Discord: discord.gg/rCqKSvR";
                discord.GetComponent<RectTransform>().localPosition = new Vector3(0, -460);
                discord.fontSize /= 2;
            }
            else
            {
                MelonModLogger.LogError("[VRCTools] Unable to find UserInterface/MenuContent/Screens/Authentication/LoginUserPass");
            }
        }

        internal static void TryLoginToVRCModNetwork(string username, string password, Action<string> onError)
        {
            APIUser user = vrcmnwLoginCallbackContainer.Model.TryCast<APIUser>();
            MelonModLogger.Log("Invoking auth (uuid: " + (VRCModNetworkManager.userUuid ?? "{null}") + ", vrcmnwuuid: " + VRCModNetworkManager.userUuid + ")");

            VRCUiPopupManager.prop_VRCUiPopupManager_0.ShowStandardPopup("Login", "Logging in to VRCModNework", p => { });

            VRCModNetworkManager.Auth(username, password, string.IsNullOrEmpty(VRCModNetworkManager.userUuid) ? user.id : VRCModNetworkManager.userUuid, () => // might be bugged
            {
                SecurePlayerPrefs.SetString("vrcmnw_un_" + user.id, username, "vl9u1grTnvXA");
                SecurePlayerPrefs.SetString("vrcmnw_pw_" + user.id, password, "vl9u1grTnvXA");

                FinishLogin();
            }, onError);
        }


        private static IntPtr GetDetourMethod(string name) => typeof(VRCModNetworkLogin).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).MethodHandle.GetFunctionPointer();

        private static void Hook(IntPtr orig, IntPtr reflect)
        {
            typeof(Imports).GetMethod("Hook", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { orig, reflect });
        }


        public delegate void PatchedMethodDelegateType(IntPtr endpoint, HTTPMethods method, IntPtr responseContainer, IntPtr requestParams, bool authenticationRequired, bool disableCache, float cacheLifeTime, int retryCount, IntPtr credentials);

        internal static unsafe void InjectVRCModNetworkLoginPage()
        {
            MelonModLogger.Log("Patching VRC.Core.API::SendRequest");

            Hook(SDK.GetClass("VRC.Core.API").GetMethod("SendRequest").Ptr, GetDetourMethod("SendRequestPatch"));

            Il2CppSystem.Reflection.MethodInfo sendRequestI2CMethodInfo = API.Il2CppType.GetMethod("SendRequest");
            originalsendRequestI2CMethodPtr = *(IntPtr*)IL2CPP.il2cpp_method_get_from_reflection(sendRequestI2CMethodInfo.Pointer);
            originalsendRequestI2CMethod = Marshal.GetDelegateForFunctionPointer<PatchedMethodDelegateType>(originalsendRequestI2CMethodPtr);

            MelonModLogger.Log("Done patching");
        }


        private static void SendRequestPatch(IntPtr endpointPtr, HTTPMethods method, IntPtr responseContainerPtr, IntPtr requestParams, bool authenticationRequired, bool disableCache, float cacheLifeTime, int retryCount, IntPtr credentials)
        {
            string endpoint = IL2CPP.IntPtrToString(endpointPtr);
            if(endpoint == "auth/user" || endpoint == "auth/steam" || endpoint == "auth/oculus" || endpoint.StartsWith("auth/twofactorauth/"))
            {
                IntPtr responseContainerExt = OverrideContainer(responseContainerPtr);
                originalsendRequestI2CMethod(endpointPtr, method, responseContainerExt, requestParams, authenticationRequired, disableCache, cacheLifeTime, retryCount, credentials);
            }
            else
                originalsendRequestI2CMethod(endpointPtr, method, responseContainerPtr, requestParams, authenticationRequired, disableCache, cacheLifeTime, retryCount, credentials);
        }

        private static IntPtr OverrideContainer(IntPtr responseContainerPtr)
        {
            ApiDictContainer responseContainer = new ApiDictContainer(responseContainerPtr);
            MelonModLogger.Log("responseContainer: " + responseContainer);

            ApiDictContainer responseContainerExt = new ApiDictContainer(new string[0])
            {
                OnSuccess = new Action<ApiContainer>((c) =>
                {
                    if (VRCModNetworkManager.State == VRCModNetworkManager.ConnectionState.DISCONNECTED || APIUser.IsLoggedIn)
                    {
                        responseContainer.OnSuccess.Invoke(c);
                    }
                    else
                    {
                        ApiModelContainer<APIUser> apiModelContainer = new ApiModelContainer<APIUser>();
                        apiModelContainer.setFromContainer(c);
                        if (apiModelContainer.ValidModelData())
                        {
                            vrcmnwLoginCallbackContainer = apiModelContainer;
                            vrcmnwLoginCallback = responseContainer.OnSuccess;

                            try
                            {
                                popupCompleteCallback = VRCUiManager.prop_VRCUiManager_0.field_VRCUiPopup_0.field_Action_0;
                                VRCUiPopupManager.prop_VRCUiPopupManager_0.HideCurrentPopup();
                                APIUser currentUser = apiModelContainer.Model.TryCast<APIUser>();
                                try
                                {
                                    if (SecurePlayerPrefs.HasKey("vrcmnw_un_" + currentUser.id) && SecurePlayerPrefs.HasKey("vrcmnw_pw_" + currentUser.id))
                                    {
                                        string username = SecurePlayerPrefs.GetString("vrcmnw_un_" + currentUser.id, "vl9u1grTnvXA");
                                        string password = SecurePlayerPrefs.GetString("vrcmnw_pw_" + currentUser.id, "vl9u1grTnvXA");
                                        TryLoginToVRCModNetwork(username, password, (error) => ShowVRCMNWLoginMenu(true));
                                    }
                                    else
                                    {
                                        ShowVRCMNWLoginMenu(true);
                                    }
                                }
                                catch (Exception e)
                                {
                                    MelonModLogger.LogError("SendGetRequestLoginPatch - Unable to log in: " + e);
                                    responseContainer.OnSuccess.Invoke(c);
                                }
                            }
                            catch (Exception e)
                            {
                                MelonModLogger.LogError("SendGetRequestLoginPatch - Unable to show popup: " + e);
                                responseContainer.OnSuccess.Invoke(c);
                            }
                        }
                        else
                        {
                            responseContainer.OnSuccess.Invoke(c);
                        }
                    }
                }),
                OnError = new Action<ApiContainer>((c) =>
                {
                    MelonModLogger.Log("API RETURNED ERROR: " + c);
                    responseContainer.OnError.Invoke(c);
                })
            };

            MelonModLogger.Log("responseContainerExt: " + responseContainerExt);

            return responseContainerExt.Pointer;
        }

        private static void ShowVRCMNWLoginMenu(bool pause)
        {
            VRCUiPopupManager.prop_VRCUiPopupManager_0.HideCurrentPopup();
            /*
            if (pause)
            {
                VRCUiManager.prop_VRCUiManager_0.ShowUi(false, true);
                MelonCoroutines.Start(QuickMenuExtension.PlaceUiAfterPause());
            }
            */
            VRCUiManager.prop_VRCUiManager_0.ShowScreen("UserInterface/MenuContent/Screens/Authentication/VRCMNWLoginPage");
        }





        private static void FinishLogin()
        {
            vrcmnwLoginCallback.Invoke(vrcmnwLoginCallbackContainer);
            popupCompleteCallback?.Invoke();
        }
    }
}

using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VRCMelonCore;
using static UnityEngine.UI.Button;

namespace VRCModNetwork
{
    public class VRCModNetworkLib : MelonMod
    {
        public bool Initialized { get; private set; }

        public override void OnApplicationStart()
        {
            VRCMelonCoreMod.RunBeforeFlowManager(Setup());
        }

        private IEnumerator Setup()
        {
            // TODO check for updates

            MelonModLogger.Log("Initialising VRCModNetwork");

            MelonModLogger.Log("Overwriting login button event");
            VRCUiPageAuthentication loginPage = Resources.FindObjectsOfTypeAll<VRCUiPageAuthentication>().FirstOrDefault((page) => page.gameObject.name == "LoginUserPass");
            MelonModLogger.Log("loginPage: " + loginPage);
            if (loginPage != null)
            {
                Button loginButton = loginPage.transform.Find("ButtonDone (1)")?.GetComponent<Button>();
                if (loginButton != null)
                {
                    ButtonClickedEvent bce = loginButton.onClick;
                    loginButton.onClick = new ButtonClickedEvent();
                    loginButton.onClick.AddListener(new Action(() =>
                    {
                        VRCModNetworkManager.SetCredentials(Uri.EscapeDataString(loginPage.loginUserName.field_String_2) + ":" + Uri.EscapeDataString(loginPage.loginPassword.field_String_2));
                        bce?.Invoke();
                    }));
                }
                else
                    MelonModLogger.Log("Unable to find login button in login page");
            }

            try
            {
                VRCModNetworkStatus.Setup();
                VRCModNetworkLogin.SetupVRCModNetworkLoginPage();
                //ModdedUsersManager.Init();
            }
            catch (Exception e)
            {
                MelonModLogger.LogError(e.ToString());
            }

            MelonModLogger.Log("Injecting VRCModNetwork login page");
            VRCModNetworkLogin.InjectVRCModNetworkLoginPage();

            MelonModLogger.Log("Connecting");
            yield return VRCModNetworkManager.ConnectInit();

            MelonModLogger.Log("VRCModNetwork sucessfully initialized!");

            Initialized = true;
        }

        public override void OnUpdate()
        {
            if (!Initialized) return;
            VRCModNetworkManager.Update();
            VRCModNetworkStatus.Update();
            //ModdedUsersManager.Update();
        }
    }
}

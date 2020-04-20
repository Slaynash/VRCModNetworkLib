using MelonLoader;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace VRCModNetwork
{
    internal static class VRCModNetworkStatus
    {
        private static Text networkstatusText;
        private static VRCModNetworkManager.ConnectionState currentState = (VRCModNetworkManager.ConnectionState)(-1);
        private static bool currentAuthState = false;
        private static int currentVRCAuthStatus = 0;
        private static RectTransform vrcmnwButton;

        public static void Setup()
        {
            try
            {
                Transform baseTextTransform = QuickMenu.prop_QuickMenu_0.transform.Find("ShortcutMenu/BuildNumText");
                if (baseTextTransform != null)
                {
                    Transform vrcmodNetworkTransform = new GameObject("VRCModNetworkStatusText", new Il2CppSystem.Type[] { RectTransform.Il2CppType, Text.Il2CppType }).transform;
                    vrcmodNetworkTransform.SetParent(baseTextTransform.parent, false);
                    vrcmodNetworkTransform.SetSiblingIndex(baseTextTransform.GetSiblingIndex() + 1);

                    networkstatusText = vrcmodNetworkTransform.GetComponent<Text>();
                    RectTransform networkstatusRT = vrcmodNetworkTransform.GetComponent<RectTransform>();

                    networkstatusRT.localScale = baseTextTransform.localScale;

                    networkstatusRT.anchorMin = baseTextTransform.GetComponent<RectTransform>().anchorMin;
                    networkstatusRT.anchorMax = baseTextTransform.GetComponent<RectTransform>().anchorMax;
                    networkstatusRT.anchoredPosition = baseTextTransform.GetComponent<RectTransform>().anchoredPosition;
                    networkstatusRT.sizeDelta = new Vector2(2000, baseTextTransform.GetComponent<RectTransform>().sizeDelta.y);
                    networkstatusRT.pivot = baseTextTransform.GetComponent<RectTransform>().pivot;

                    Vector3 newPos = baseTextTransform.localPosition;
                    newPos.x -= baseTextTransform.GetComponent<RectTransform>().sizeDelta.x * 0.5f;
                    newPos.x += 2000 * 0.5f;
                    newPos.y += -85;

                    networkstatusRT.localPosition = newPos;
                    networkstatusText.text = "VRCModNetworkStatus: <color=orange>Unknown</color>";
                    networkstatusText.color = baseTextTransform.GetComponent<Text>().color;
                    networkstatusText.font = baseTextTransform.GetComponent<Text>().font;
                    networkstatusText.fontSize = baseTextTransform.GetComponent<Text>().fontSize;
                    networkstatusText.fontStyle = baseTextTransform.GetComponent<Text>().fontStyle;
                    networkstatusText.horizontalOverflow = HorizontalWrapMode.Overflow;

                    Transform settingsButtonTransform = QuickMenu.prop_QuickMenu_0.transform.Find("ShortcutMenu/SettingsButton");
                    vrcmnwButton = UnityUiUtils.DuplicateButton(settingsButtonTransform, "", new Vector2(-230, -930)).GetComponent<RectTransform>();
                    MelonModLogger.Log("vrcmnwButton pos: " + vrcmnwButton.position.x + ", " + vrcmnwButton.position.y + ", " + vrcmnwButton.position.z);
                    MelonModLogger.Log("networkstatusRT pos: " + networkstatusRT.position.x + ", " + networkstatusRT.position.y + ", " + networkstatusRT.position.z);
                    //vrcmnwButton.gameObject.SetActive(true);
                    //vrcmnwButton.GetComponentInChildren<Text>().text = "Mod/Game\nSettings";
                    //vrcmnwButton.GetComponent<UiTooltip>().text = "Link current VRChat account to your VRCTools account";
                    //vrcmnwButton.position = networkstatusRT.position;
                    Button vrcmnwButtonButton = vrcmnwButton.GetComponent<Button>();
                    vrcmnwButtonButton.onClick = new Button.ButtonClickedEvent();
                    vrcmnwButtonButton.onClick.AddListener(new Action(VRCModNetworkManager.LinkVRCAccount));
                    vrcmnwButton.sizeDelta = new Vector2(850, 140);

                    Transform infobarpanelTransform = QuickMenu.prop_QuickMenu_0.transform.Find("QuickMenu_NewElements/_InfoBar/Panel");
                    RectTransform infobarpanelRectTransform = infobarpanelTransform.GetComponent<RectTransform>();
                    infobarpanelRectTransform.sizeDelta = new Vector2(infobarpanelRectTransform.sizeDelta.x, infobarpanelRectTransform.sizeDelta.y + 80);
                    infobarpanelRectTransform.anchoredPosition = new Vector2(infobarpanelRectTransform.anchoredPosition.x, infobarpanelRectTransform.anchoredPosition.y - 40);

                    ExpendCollider(QuickMenu.prop_QuickMenu_0.GetComponent<BoxCollider>(), new Vector2(0, -400));

                    Update();
                }
                else
                {
                    MelonModLogger.Log("QuickMenu/ShortcutMenu/BuildNumText is null");
                }
            }
            catch (Exception ex)
            {
                MelonModLogger.Log(ex.ToString());
            }
        }

        private static void ExpendCollider(BoxCollider boxCollider, Vector2 expendSize)
        {
            boxCollider.size += new Vector3(Mathf.Abs(expendSize.x), Mathf.Abs(expendSize.y), 1f);
            boxCollider.center += new Vector3(expendSize.x * 0.5f, expendSize.y * 0.5f);
        }


        internal static void Update()
        {
            if (networkstatusText != null && (currentState != VRCModNetworkManager.State || currentAuthState != VRCModNetworkManager.IsAuthenticated || currentVRCAuthStatus != VRCModNetworkManager.VRCAuthStatus))
            {
                currentState = VRCModNetworkManager.State;
                currentAuthState = VRCModNetworkManager.IsAuthenticated;
                currentVRCAuthStatus = VRCModNetworkManager.VRCAuthStatus;
                vrcmnwButton.gameObject.SetActive(false);
                if (!string.IsNullOrEmpty(VRCModNetworkManager.authError))
                    networkstatusText.text = "VRCModNetwork status: <color=red>" + VRCModNetworkManager.authError + "</color>";
                else if (VRCModNetworkManager.IsAuthenticated)
                {
                    //networkstatusText.text = "VRCModNetwork status: <color=lime>Authenticated</color>";

                    if (VRCModNetworkManager.VRCAuthStatus == 0)
                        networkstatusText.text = "VRCModNetwork status: <color=orange>Waiting for VRChat auth...</color>";
                    else if (VRCModNetworkManager.VRCAuthStatus == 1)
                        networkstatusText.text = "VRCModNetwork status: <color=lime>Authenticated</color>";
                    else if (VRCModNetworkManager.VRCAuthStatus == 2)
                        networkstatusText.text = "VRCModNetwork status: <color=red>Invalid VRChat account</color>";
                    else if (VRCModNetworkManager.VRCAuthStatus == 3)
                    {
                        networkstatusText.text = "VRCModNetwork status: <color=orange>VRChat linking required</color>";
                        vrcmnwButton.gameObject.SetActive(true);
                    }
                    else if (VRCModNetworkManager.State == VRCModNetworkManager.ConnectionState.NEED_REAUTH)
                        networkstatusText.text = "VRCModNetwork status: <color=orange>Authenticated - Relogin required</color>";
                }
                else if (VRCModNetworkManager.State == VRCModNetworkManager.ConnectionState.CONNECTED)
                    networkstatusText.text = "VRCModNetwork status: <color=orange>Not Authenticated</color>";
                else if (VRCModNetworkManager.State == VRCModNetworkManager.ConnectionState.NEED_REAUTH)
                    networkstatusText.text = "VRCModNetwork status: <color=orange>Not Authenticated - Relogin required</color>";
                else if (VRCModNetworkManager.State == VRCModNetworkManager.ConnectionState.CONNECTING)
                    networkstatusText.text = "VRCModNetwork status: <color=orange>Connecting</color>";
                else
                    networkstatusText.text = "VRCModNetwork status: <color=red>Disconnected</color>";
            }
        }
    }
}

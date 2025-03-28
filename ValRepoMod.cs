using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using Unity.VisualScripting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoModding
{
    [BepInPlugin("com.valkyriebae.TalkingDeathHeadsMod", "Talking Death Heads", "0.0.15")]
    public class ValRepoMod : BaseUnityPlugin
    {
        public static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            // Called when the game starts & your plugin is loaded
            Log.LogInfo("Talking Death Heads loaded!");

            var harmony = new Harmony("com.valkyriebae.TalkingDeathHeadsMod");
            harmony.PatchAll();
        }

        public class TalkingDeathHeadsMod : MonoBehaviour, IPunObservable
        {
            private bool IsEnabled = true;
            private bool IsLobby = true;
            private PlayerAvatar _playerAvatar;

            public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
            {
                throw new NotImplementedException();
            }

            private void Awake()
            {
                _playerAvatar = this.gameObject.GetComponent<PlayerAvatar>();
            }

            public bool GetIsEnabled()
            {
                return IsEnabled;
            }

            public void LocalSetLobbyToggle(bool SetIsLobby)
            {
                if (this.IsLobby == SetIsLobby) return;
                SetLobbyToggleRPC(SetIsLobby);
                if (!GameManager.Multiplayer()) return;
                PlayerAvatar.instance.photonView.RPC("SetLobbyToggleRPC", (RpcTarget.Others),
                    SetIsLobby);
            }

            [PunRPC]
            public void SetLobbyToggleRPC(bool SetIsLobby)
            {
                this.IsLobby = SetIsLobby;
            }

            public void LocalSetIsEnabled(bool SetIsEnabled)
            {
                if (this.IsEnabled == SetIsEnabled) return;
                ValRepoMod.Log.LogInfo($"LocalSetIsEnabled: {SetIsEnabled}");
                SetTalkingDeathHeadRPC(SetIsEnabled);
                if (!GameManager.Multiplayer()) return;
                PlayerAvatar.instance.photonView.RPC("SetTalkingDeathHeadRPC", (RpcTarget.Others),
                    SetIsEnabled);
            }

            [PunRPC]
            public void SetTalkingDeathHeadRPC(bool SetIsEnabled)
            {
                this.IsEnabled = SetIsEnabled;
                if (_playerAvatar.GetPrivateField<bool>("isDisabled") && SemiFunc.RunIsLevel())
                {
                    _playerAvatar.GetPrivateField<PlayerVoiceChat>("voiceChat")
                        .ToggleLobby(!IsEnabled);
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), "Update")]
    public static class PlayerAvatar_Update_Transpiler_Patch
    {
        // This Transpiler completely removes the IL instructions behind:
        // if ((double)this.deadVoiceTimer <= 0.0 && this.voiceChatFetched)
        //     this.voiceChat.ToggleLobby(true);

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = new List<CodeInstruction>(instructions);

            // We'll look for the snippet that checks "this.voiceChatFetched"
            // and ends with "callvirt ... ToggleLobby(true)".
            for (var i = 0; i < codeList.Count; i++)
            {
                if (codeList[i].opcode == OpCodes.Callvirt &&
                    codeList[i].operand != null &&
                    codeList[i].operand.ToString().Contains("ToggleLobby"))
                {
                    codeList[i - 1] = new CodeInstruction(OpCodes.Ldc_I4_0);
                    break;
                }
            }

            return codeList.AsEnumerable();
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar))]
    public static class PlayerAvatarPatches
    {
        private static string _playerName = "Wait, who is this?";

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        public static void Start_Postfix(ref bool ___isLocal, PlayerAvatar? __instance)
        {
            if (!___isLocal) return;
            // ValRepoMod.Log.LogInfo("PlayerAvatar.Start postfix!");
            _playerName = __instance.GetPrivateField<string>("playerName");
            // ValRepoMod.Log.LogInfo($"PlayerName: {_playerName}");
        }

        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        public static void Awake_Postfix(ref PhotonView ___photonView, PlayerAvatar __instance)
        {
            __instance.gameObject.AddComponent<ValRepoMod.TalkingDeathHeadsMod>();
        }

        [HarmonyPatch("PlayerDeathDone")]
        [HarmonyPostfix]
        public static void PlayerDeathDone_Postfix(ref bool ___isLocal, PlayerAvatar __instance)
        {
            if (!__instance.GetPrivateField<bool>("isLocal")) return;
            SemiFunc.HUDSpectateSetName(_playerName);
        }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void Update_Postfix(ref bool ___deadSet, PlayerAvatar __instance)
        {
            if (SemiFunc.MenuLevel())
            {
                PlayerAvatar.instance.GetComponent<ValRepoMod.TalkingDeathHeadsMod>()
                    .LocalSetIsEnabled(true);
            }
        }

        [HarmonyPatch("ReviveRPC")]
        [HarmonyPostfix]
        public static void ReviveRPC_Postfix(PlayerAvatar __instance)
        {
            __instance.GetComponent<ValRepoMod.TalkingDeathHeadsMod>().SetTalkingDeathHeadRPC(true);
        }
    }

    [HarmonyPatch(typeof(RunManager))]
    public static class RunManagerPatches
    {
        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void Start_Postfix(RunManager __instance)
        {
            if (!SemiFunc.RunIsArena() || !PlayerAvatar.instance) return;
            PlayerAvatar.instance.GetComponent<ValRepoMod.TalkingDeathHeadsMod>().LocalSetIsEnabled(false);
        }
    }

    [HarmonyPatch(typeof(MenuSpectateList))]
    public static class MenuSpectateListPatches
    {
        // private static bool WasPrinted;

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void MenuSpectateList_Update_Postfix(MenuSpectateList __instance)
        {
            // if (!WasPrinted)
            // {
            //     WasPrinted = true;
            //     Utilities.PrintFullHierarchy(__instance.gameObject);
            // }

            if (PlayerAvatar.instance &&
                PlayerAvatar.instance.GetComponent<ValRepoMod.TalkingDeathHeadsMod>().GetIsEnabled())
            {
                __instance.Hide();
            }
        }
    }

    [HarmonyPatch(typeof(PlayerVoiceChat))]
    public static class PlayerVoiceChatPatches
    {
        // [HarmonyPatch("Update")]
        // [HarmonyPostfix]
        // public static void Update_Postfix(ref PhotonView ___photonView, ref bool ___isTalking,
        //     ref bool ___isTalkingPrevious,
        //     ref PlayerVoiceChat __instance)
        // {
        //     if (!___photonView.IsMine) return;
        //     if (___isTalking != ___isTalkingPrevious)
        //     {
        //         ___isTalkingPrevious = this.isTalking;
        //         if (this.isTalking)
        //             this.isTalkingStartTime = Time.time;
        //         this.photonView.RPC("IsTalkingRPC", RpcTarget.Others, (object)this.isTalking);
        //     }
        // }

        [HarmonyPatch("LateUpdate")]
        [HarmonyPostfix]
        public static void LateUpdate_Postfix(ref PlayerAvatar ___playerAvatar,
            ref PlayerVoiceChat __instance)
        {
            if (!___playerAvatar ||
                !(___playerAvatar.GetPrivateField<bool>("isDisabled")) ||
                !___playerAvatar.GetComponent<ValRepoMod.TalkingDeathHeadsMod>().GetIsEnabled() ||
                !___playerAvatar.playerDeathHead || !SemiFunc.RunIsLevel())
            {
                return;
            }

            // ValRepoMod.Log.LogInfo($"LateUpdate player steamID: {___playerAvatar.GetPrivateField<string>("steamID")}");

            __instance.transform.position = Vector3.Lerp(__instance.transform.position,
                ___playerAvatar.playerDeathHead.transform.position, 30f * Time.deltaTime);
            // ValRepoMod.Log.LogInfo($"PlayerVoiceChat: {__instance.transform.position}");
            // foreach (var gameObject in GameObject.FindGameObjectsWithTag("Player"))
            // {
            // ValRepoMod.Log.LogInfo($"Player ${gameObject.transform.name}: {gameObject.transform.position}");
            // }
        }

        [HarmonyPatch("ToggleLobby")]
        [HarmonyPrefix]
        public static bool ToggleLobby_Prefix(bool _toggle, ref PlayerAvatar ___playerAvatar,
            PlayerVoiceChat __instance)
        {
            if (!SemiFunc.RunIsArena() || !___playerAvatar.GetPrivateField<bool>("deadSet")) return true;
            if (_toggle) return true;
            __instance.ToggleLobby(true);
            return false;
        }
    }

    [HarmonyPatch(typeof(SpectateCamera))]
    public static class SpectateCameraPatches
    {
        [HarmonyPatch("SetDeath")]
        [HarmonyPostfix]
        public static void SetDeath_Postfix(ref Transform ___deathPlayerSpectatePoint, ref PlayerAvatar ___player,
            ref SpectateCamera __instance)
        {
            if (!___player || !___player.GetComponent<ValRepoMod.TalkingDeathHeadsMod>().GetIsEnabled() ||
                !___player.playerDeathHead ||
                !___player.GetPrivateField<bool>("isLocal"))
            {
                // ValRepoMod.Log.LogInfo("Skipping SetDeath_Postfix!");
                return;
            }

            // ValRepoMod.Log.LogInfo("Running SetDeath_Postfix!");
            ___deathPlayerSpectatePoint = ___player.playerDeathHead.transform;
        }

        [HarmonyPatch("StateNormal")]
        [HarmonyPrefix]
        public static bool StateNormal_Prefix(ref PlayerAvatar? ___player, ref bool ___stateImpulse,
            ref Camera ___MainCamera, ref float ___previousFarClipPlane, ref float ___previousFieldOfView,
            ref Camera ___TopCamera, ref float ___normalMaxDistance, ref float ___normalMinDistance,
            ref float ___normalAimHorizontal, ref float ___normalAimVertical, ref Vector3 ___normalPreviousPosition,
            ref Transform ___normalTransformPivot, ref float ___normalDistanceTarget, SpectateCamera __instance)
        {
            if (!PlayerAvatar.instance) return true;
            if (SemiFunc.InputDown(InputKey.Jump) || SemiFunc.InputDown(InputKey.SpectateNext) ||
                SemiFunc.InputDown(InputKey.SpectatePrevious))
            {
                PlayerAvatar.instance.GetComponent<ValRepoMod.TalkingDeathHeadsMod>().LocalSetIsEnabled(false);
            }

            if (!PlayerAvatar.instance.GetComponent<ValRepoMod.TalkingDeathHeadsMod>().GetIsEnabled())
            {
                return true;
            }

            ___player = PlayerAvatar.instance;
            // var steamID = ___player?.GetPrivateField<string>("steamID");
            // var playerName = "NullName";
            // if (___player)
            // {
            //     playerName = ___player.GetPrivateField<string>("playerName");
            // }

            // ValRepoMod.Log.LogInfo($"StateNormal player steamID: {steamID}");
            if (!___player || !___player.GetPrivateField<bool>("isDisabled") ||
                !___player.GetComponent<ValRepoMod.TalkingDeathHeadsMod>().GetIsEnabled() ||
                !___player?.playerDeathHead)
            {
                // ValRepoMod.Log.LogInfo($"Running original StateNormal for {playerName}!");
                return true;
            }

            // ValRepoMod.Log.LogInfo($"Running our StateNormal for {playerName}");

            var playerDeathHead = ___player.playerDeathHead.transform;
            if (___stateImpulse)
            {
                RenderSettings.fog = true;
                var mainCamera = ___MainCamera;
                mainCamera.farClipPlane = ___previousFarClipPlane;
                mainCamera.fieldOfView = ___previousFieldOfView;
                ___TopCamera.fieldOfView = mainCamera.fieldOfView;
                mainCamera.transform.localPosition = Vector3.zero;
                mainCamera.transform.localRotation = Quaternion.identity;
                AudioManager.instance.AudioListener.TargetPositionTransform = mainCamera.transform;
                ___stateImpulse = false;
                SemiFunc.LightManagerSetCullTargetTransform(playerDeathHead);
                CameraGlitch.Instance.PlayTiny();
                GameDirector.instance.CameraImpact.Shake(1f, 0.1f);
                AudioManager.instance.RestartAudioLoopDistances();
                ___normalMaxDistance = 3f;
            }

            CameraNoise.Instance.Override(0.03f, 0.25f);
            var num1 = SemiFunc.InputMouseX();
            var num2 = SemiFunc.InputMouseY();
            var num3 = SemiFunc.InputScrollY();
            if (CameraAim.Instance.GetPrivateField<bool>("overrideAimStop"))
            {
                num1 = 0.0f;
                num2 = 0.0f;
                num3 = 0.0f;
            }

            ___normalAimHorizontal +=
                (float)(num1 * (double)CameraAim.Instance.AimSpeedMouse * 1.5);
            if (___normalAimHorizontal > 360.0)
                ___normalAimHorizontal -= 360f;
            if (___normalAimHorizontal < -360.0)
                ___normalAimHorizontal += 360f;
            var normalAimVertical = ___normalAimVertical;
            var num4 = (float)(-(num2 * (double)CameraAim.Instance.AimSpeedMouse) * 1.5);
            ___normalAimVertical += num4;
            ___normalAimVertical =
                Mathf.Clamp(___normalAimVertical, -70f, 70f);
            if (num3 != 0.0)
                ___normalMaxDistance =
                    Mathf.Clamp(___normalMaxDistance - num3 * (1f / 400f), ___normalMinDistance + 1f, 6f);
            var b1 = playerDeathHead.position;
            b1 += Vector3.up * 0.45f;
            ___normalPreviousPosition = b1;
            __instance.normalTransformPivot.position =
                Vector3.Lerp(___normalTransformPivot.position, b1,
                    10f * Time.deltaTime);
            __instance.normalTransformPivot.rotation = Quaternion.Lerp(
                ___normalTransformPivot.rotation,
                Quaternion.Euler(___normalAimVertical,
                    ___normalAimHorizontal, 0.0f),
                Mathf.Lerp(50f, 6.25f, GameplayManager.instance.GetPrivateField<float>("cameraSmoothing") / 100f) *
                Time.deltaTime);
            __instance.normalTransformPivot.localRotation = Quaternion.Euler(
                ___normalTransformPivot.localRotation.eulerAngles.x,
                ___normalTransformPivot.localRotation.eulerAngles.y, 0.0f);
            var flag = false;
            var num5 = ___normalMaxDistance;
            var raycastHitArray = Physics.SphereCastAll(
                ___normalTransformPivot.position, 0.1f,
                -___normalTransformPivot.forward,
                ___normalMaxDistance,
                SemiFunc.LayerMaskGetVisionObstruct());
            if (raycastHitArray.Length != 0)
            {
                foreach (var raycastHit in raycastHitArray)
                {
                    if (!(bool)(Object)raycastHit.transform.GetComponent<PlayerHealthGrab>() &&
                        !(bool)(Object)raycastHit.transform.GetComponent<PlayerAvatar>() &&
                        !(bool)(Object)raycastHit.transform.GetComponent<PlayerTumble>())
                    {
                        num5 = Mathf.Min(num5, raycastHit.distance);
                        if (raycastHit.transform.CompareTag("Wall"))
                            flag = true;
                        if (raycastHit.collider.bounds.size.magnitude > 2.0)
                            flag = true;
                    }
                }

                ___normalDistanceTarget = Mathf.Max(___normalMinDistance, num5);
            }
            else
                ___normalDistanceTarget = ___normalMaxDistance;

            __instance.normalTransformDistance.localPosition = Vector3.Lerp(
                __instance.normalTransformDistance.localPosition,
                new Vector3(0.0f, 0.0f, -___normalDistanceTarget),
                Time.deltaTime * 5f);
            var num6 = -__instance.normalTransformDistance.localPosition.z;
            var direction = ___normalTransformPivot.position -
                            __instance.normalTransformDistance.position;
            var num7 = direction.magnitude;
            RaycastHit hitInfo;
            if (Physics.SphereCast(__instance.normalTransformDistance.position, 0.15f, direction, out hitInfo,
                    ___normalMaxDistance, LayerMask.GetMask("PlayerVisuals"),
                    QueryTriggerInteraction.Collide))
                num7 = hitInfo.distance;
            var b2 = (float)(num6 - (double)num7 - 0.10000000149011612);
            if (flag)
            {
                var num8 = Mathf.Max(num5, b2);
                ___MainCamera.nearClipPlane = Mathf.Max(num6 - num8, 0.01f);
            }
            else
                ___MainCamera.nearClipPlane = 0.01f;

            RenderSettings.fogStartDistance = ___MainCamera.nearClipPlane;
            __instance.transform.position = __instance.normalTransformDistance.position;
            __instance.transform.rotation = __instance.normalTransformDistance.rotation;
            if (__instance.transform.position.y <
                playerDeathHead.position.y + 0.25 &&
                num4 < 0.0)
                ___normalAimVertical = normalAimVertical;
            return false;
        }
    }

    [HarmonyPatch(typeof(LoadingUI))]
    public static class LoadingUIPatches
    {
        [HarmonyPatch("StartLoading")]
        [HarmonyPostfix]
        public static void StartLoading_Postfix(ref LoadingUI __instance)
        {
            if (!PlayerAvatar.instance) return;
            var voiceChat = PlayerAvatar.instance.GetPrivateField<PlayerVoiceChat>("voiceChat");
            if (voiceChat)
            {
                PlayerAvatar.instance.GetComponent<ValRepoMod.TalkingDeathHeadsMod>().LocalSetLobbyToggle(true);
            }
        }
    }

    public static class Utilities
    {
        // Call this method to print the full hierarchy for a given GameObject
        public static void PrintFullHierarchy(GameObject go)
        {
            if (go == null) return;

            // Print parent chain (from root down to the current GameObject)
            var parentChain = new List<GameObject>();
            var current = go.transform;
            while (current != null)
            {
                parentChain.Add(current.gameObject);
                current = current.parent;
            }

            parentChain.Reverse();

            ValRepoMod.Log.LogInfo("=== Parent Chain ===");
            for (var i = 0; i < parentChain.Count; i++)
            {
                var indent = new string(' ', i * 2);
                ValRepoMod.Log.LogInfo(indent + parentChain[i].name + " - Components: " +
                                       GetComponentsString(parentChain[i]));
            }

            // Print children hierarchy starting from the given GameObject
            ValRepoMod.Log.LogInfo("=== Children Hierarchy ===");
            PrintChildren(go.transform, 1);
        }

        // Recursively print children of a Transform with indentation based on depth
        private static void PrintChildren(Transform t, int depth)
        {
            var indent = new string(' ', depth * 2);
            foreach (Transform child in t)
            {
                ValRepoMod.Log.LogInfo(
                    indent + child.gameObject.name + " - Components: " + GetComponentsString(child.gameObject));
                PrintChildren(child, depth + 1);
            }
        }

        // Helper function that builds a comma-separated list of component (script) names
        private static string GetComponentsString(GameObject go)
        {
            var components = go.GetComponents<Component>();
            var compNames = components.OfType<Component>().Select(comp => comp.GetType().Name).ToList();
            return string.Join(", ", compNames);
        }

        public static TResult InvokePrivateMethod<TClass, TResult>(TClass __instance, string methodName)
        {
            var method = AccessTools.Method(typeof(TClass), methodName);
            if (method == null)
            {
                ValRepoMod.Log.LogWarning($"Method '{methodName}' not found on type '{typeof(TClass)}'.");
                return default;
            }

            var returnValue = (TResult)method.Invoke(__instance, null);
            var returnString = returnValue == null ? "null" : returnValue.ToString();
            // ValRepoMod.Log.LogInfo($"Method '{methodName}' returned '{returnString}'.");
            return returnValue;
        }

        public static void InvokeVoidPrivateMethod<TClass>(TClass __instance, string methodName,
            object[]? parameters = null)
        {
            var method = AccessTools.Method(typeof(TClass), methodName);
            if (method == null)
            {
                ValRepoMod.Log.LogWarning($"Method '{methodName}' not found on type '{typeof(TClass)}'.");
                return;
            }

            method.Invoke(__instance, parameters);
            var testString = parameters == null ? "null" : string.Join(", ", parameters);
            // ValRepoMod.Log.LogInfo($"Method '{methodName}' invoked with parameters: {testString}");
        }


        public static TField GetPrivateField<TField>(this object? instance, string fieldName)
        {
            if (instance == null) return default;
            var field = AccessTools.Field(instance!.GetType(), fieldName);
            if (field == null)
            {
                // ValRepoMod.Log.LogWarning($"Field '{fieldName}' not found on type '{instance.GetType()}'.");
                // var fields = instance.GetType().GetFields(
                //     System.Reflection.BindingFlags.Instance |
                //     System.Reflection.BindingFlags.NonPublic |
                //     System.Reflection.BindingFlags.Public);
                // foreach (var f in fields)
                // {
                // ValRepoMod.Log.LogInfo($"Found field: {f.Name}");
                // }

                return default;
            }

            var returnValue = (TField)field.GetValue(instance);
            // var returnString = returnValue == null ? "null" : returnValue.ToString();
            // ValRepoMod.Log.LogInfo($"Field '{fieldName}' returned '{returnString}'.");
            return returnValue;
        }

        public static void SetPrivateField<TField>(this object instance, string fieldName, TField newValue)
        {
            var field = AccessTools.Field(instance.GetType(), fieldName);
            if (field == null)
            {
                ValRepoMod.Log.LogWarning($"Field '{fieldName}' not found on type '{instance.GetType()}'.");
                return;
            }

            field.SetValue(instance, newValue);
            // var returnString = newValue == null ? "null" : newValue.ToString();
            // ValRepoMod.Log.LogInfo($"Field '{fieldName}' set to '{returnString}'.");
        }
    }

    [HarmonyPatch(typeof(GameDirector))]
    public static class GameDirectorPatches
    {
        // [HarmonyPatch("gameStateDeath")]
        // [HarmonyPostfix]
        // public static void GameStateDeath_Postfix(GameDirector __instance)
        // {
        //     //I question this check deeply.
        //     if (!PlayerAvatar.instance ||
        //         !PlayerAvatar.instance.GetComponent<ValRepoMod.TalkingDeathHeadsMod>().GetIsEnabled()) return;
        //     AudioManager.instance.SetSoundSnapshot(AudioManager.SoundSnapshot.On, 0.1f);
        // }
    }
} // RepoModding
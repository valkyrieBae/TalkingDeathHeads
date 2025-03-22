using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using Unity.VisualScripting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoModding
{
    [BepInPlugin("com.valkyriebae.TalkingDeathHeadsMod", "Talking Death Heads", "0.0.1")]
    public class ValRepoMod : BaseUnityPlugin
    {
        public static ManualLogSource Log;
        public static bool TestCalled = false;

        private void Awake()
        {
            Log = Logger;
            // Called when the game starts & your plugin is loaded
            Log.LogInfo("Talking Death Heads loaded!");

            var harmony = new Harmony("com.valkyriebae.TalkingDeathHeadsMod");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(PlayerDeathHead))]
    public static class PlayerDeathHeadPatches
    {
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
        public static readonly Dictionary<int, bool> IsPlayerDead = new Dictionary<int, bool>();
        public static PlayerAvatar _localPlayer;
        private static string _playerName = "Wait, who is this?";

        // Patch for PlayerAvatar.Start
        // [HarmonyPatch("Start")]
        // [HarmonyPrefix]
        public static bool Start_Prefix()
        {
            // ValRepoMod.Log.LogInfo("PlayerAvatar.Start prefix!");
            return true;
        }

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        public static void Start_Postfix(ref bool ___isLocal, PlayerAvatar __instance)
        {
            if (!___isLocal) return;
            // ValRepoMod.Log.LogInfo("PlayerAvatar.Start postfix!");
            _playerName = __instance.GetPrivateField<string>("playerName");
            // ValRepoMod.Log.LogInfo($"PlayerName: {_playerName}");
            _localPlayer = __instance;
        }

        // [HarmonyPatch("PlayerDeathDone")]
        // [HarmonyPrefix]
        // public static bool PlayerDeathDone_Prefix(ref bool ___isLocal, PlayerAvatar __instance)
        // {
        //     ValRepoMod.Log.LogInfo($"PlayerDeathDone player Hashcode: {__instance.GetHashCode()}");
        //     IsPlayerDead[__instance.GetHashCode()] = true;
        //     ValRepoMod.Log.LogInfo("PlayerAvatar.PlayerDeathDone prefix!");
        //     return true;
        // }

        [HarmonyPatch("PlayerDeathDone")]
        [HarmonyPostfix]
        public static void PlayerDeathDone_Postfix(ref bool ___isLocal, PlayerAvatar __instance)
        {
            if (!__instance.GetPrivateField<bool>("isLocal")) return;
            SemiFunc.HUDSpectateSetName(_playerName);
        }

        // Patch for PlayerAvatar.ChatMessageSend
        [HarmonyPatch("ChatMessageSend")]
        [HarmonyPrefix]
        public static bool ChatMessageSend_Prefix(string _message, bool _debugMessage)
        {
            // ValRepoMod.Log.LogInfo("PlayerAvatar.ChatMessageSend prefix!");
            if (_message.ToLower().StartsWith("/rage") || _message.ToLower().StartsWith("/revive"))
            {
                return false;
            }

            return true;
        }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void Update_Postfix(ref bool ___deadSet, PlayerAvatar __instance)
        {
            IsPlayerDead[__instance.GetHashCode()] = ___deadSet;
        }
    }

    [HarmonyPatch(typeof(DebugComputerCheck))]
    public static class DebugComputerCheckPatches
    {
        [HarmonyPatch("Start")]
        [HarmonyPrefix]
        public static bool Start_Prefix(ref DebugComputerCheck.StartMode ___Mode, DebugComputerCheck __instance)
        {
            // __instance.computerNames.AddItem(SystemInfo.deviceName);
            // __instance.DebugDisable = false;
            // ___Mode = DebugComputerCheck.StartMode.Multiplayer;
            return true;
        }

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        public static void Start_Postfix(ref DebugComputerCheck.StartMode ___Mode,
            ref bool ___Active,
            DebugComputerCheck __instance)
        {
            // ValRepoMod.Log.LogInfo($"Debug mode? {SemiFunc.DebugDev()}");
            // ValRepoMod.Log.LogInfo($"IsActive? {___Active}");
            // ValRepoMod.Log.LogInfo($"Instance? {DebugComputerCheck.instance}");
            // ___Active = true;
            // ___gameObject.SetActive(true);
            // DebugComputerCheck.instance = __instance;
        }

        // [HarmonyPatch("Start")]
        // [HarmonyTranspiler]
        // public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        // {
            //     var codeList = new List<CodeInstruction>(instructions);
            //     foreach (var codeInstruction in codeList)
            //     {
            //         ValRepoMod.Log.LogInfo(codeInstruction.ToString());
            //         var operandString = codeInstruction.operand switch
            //         {
            //             Label label => label.GetHashCode().ToString(),
            //             null => "null",
            //             _ => codeInstruction.operand.ToString()
            //         };
            //         ValRepoMod.Log.LogInfo(codeInstruction.opcode + " : " + operandString);
            //         if (codeInstruction.opcode == OpCodes.Brtrue && operandString.Contains("Label1"))
            //         {
            //             codeInstruction.opcode = OpCodes.Brfalse;
            //             ValRepoMod.Log.LogInfo($"Changed: {codeInstruction}");
            //         }
            //         else if (codeInstruction.opcode == OpCodes.Brtrue &&
            //                  operandString.Contains("Label5"))
            //         {
            //             codeInstruction.opcode = OpCodes.Brfalse;
            //             ValRepoMod.Log.LogInfo($"Changed: {codeInstruction}");
            //         }
            //         else if (codeInstruction.opcode == OpCodes.Brfalse &&
            //                  operandString.Contains("Label6"))
            //         {
            //             codeInstruction.opcode = OpCodes.Brtrue;
            //             ValRepoMod.Log.LogInfo($"Changed: {codeInstruction}");
            //         }
            //     }
            //
            //     return codeList.AsEnumerable();
        // }
    }

    [HarmonyPatch(typeof(SteamManager))]
    public static class SteamManagerPatches
    {
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        public static void SteamManager_Awake_Postfix(ref bool ___developerMode, SteamManager __instance)
        {
            ___developerMode = true;
        }
    }

    [HarmonyPatch(typeof(MenuSpectateList))]
    public static class MenuSpectateListPatches
    {
        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void MenuSpectateList_Update_Postfix(MenuSpectateList __instance)
        {
            __instance.Hide();
        }
    }

    [HarmonyPatch(typeof(PlayerVoiceChat))]
    public static class PlayerVoiceChatPatches
    {
        [HarmonyPatch("LateUpdate")]
        [HarmonyPostfix]
        public static void LateUpdate_Postfix(ref PlayerAvatar ___playerAvatar,
            ref PlayerVoiceChat __instance)
        {
            if (!___playerAvatar ||
                !(PlayerAvatarPatches.IsPlayerDead.GetValueOrDefault(___playerAvatar.GetHashCode(), false)) ||
                !___playerAvatar.playerDeathHead)
            {
                return;
            }

            // ValRepoMod.Log.LogInfo($"LateUpdate player Hashcode: {___playerAvatar.GetHashCode()}");

            __instance.transform.position = Vector3.Lerp(__instance.transform.position,
                ___playerAvatar.playerDeathHead.transform.position, 30f * Time.deltaTime);
            // ValRepoMod.Log.LogInfo($"PlayerVoiceChat: {__instance.transform.position}");
            foreach (var gameObject in GameObject.FindGameObjectsWithTag("Player"))
            {
                // ValRepoMod.Log.LogInfo($"Player ${gameObject.transform.name}: {gameObject.transform.position}");
            }
        }

        // Technically this is for TTS but I'm hoping it'll work just as well.
        // Hell, I could technically probably just put this on LateUpdate...fuck it.
        [HarmonyPatch("TtsFollowVoiceSettings")]
        [HarmonyPostfix]
        public static void OnTtsFollowVoiceSettings(
            ref PlayerAvatar ___playerAvatar,
            ref AudioSource ___audioSource,
            ref bool ___inLobbyMixer,
            PlayerVoiceChat __instance)
        {
            if (!___playerAvatar || !___playerAvatar.playerDeathHead ||
                !__instance.ttsAudioSource || !__instance.ttsVoice ||
                !__instance.mixerTTSSound || !GameManager.Multiplayer() ||
                GameDirector.instance.currentState < GameDirector.gameState.Main ||
                ((___playerAvatar.isActiveAndEnabled
                    ? 0
                    : (___playerAvatar.playerDeathHead.isActiveAndEnabled ? 1 : 0)) == 0))
                return;
            if (___audioSource.outputAudioMixerGroup != __instance.mixerMicrophoneSound)
            {
                ___audioSource.outputAudioMixerGroup = __instance.mixerMicrophoneSound;
            }

            ___inLobbyMixer = false;
            AudioManager.instance.SetSoundSnapshot(AudioManager.SoundSnapshot.On, 0.1f);
        }
    }

    [HarmonyPatch(typeof(PhotonView), "RPC", new Type[] { typeof(string), typeof(RpcTarget), typeof(object[]) })]
    public static class PhotonViewPatches
    {
        public static void RPC_Postfix(string methodName, RpcTarget target, params object[] parameters)
        {
            // ValRepoMod.Log.LogInfo($"PhotonView.RPC {methodName} {target} {string.Join(", ", parameters)}");
        }
    }

    [HarmonyPatch(typeof(AudioManager))]
    public static class AudioManagerPatches
    {
        // [HarmonyPatch("SetSoundSnapshot")]
        // [HarmonyPrefix]
        // public static bool SetSoundSnapshot_Prefix(AudioManager __instance, AudioManager.SoundSnapshot _snapShot,
        //     float _transitionTime)
        // {
        //     if (_snapShot == AudioManager.SoundSnapshot.Spectate &&
        //         RunManager.instance.levelCurrent != RunManager.instance.levelLobbyMenu)
        //     {
        //         __instance.SetSoundSnapshot(AudioManager.SoundSnapshot.On, _transitionTime);
        //         return false;
        //     }
        //
        //     ValRepoMod.Log.LogInfo("Allowing spectate snapshot!");
        //     return true;
        // }
    }

    [HarmonyPatch(typeof(SpectateCamera))]
    public static class SpectateCameraPatches
    {
        // [HarmonyPatch("StopSpectate")]
        // [HarmonyPrefix]
        // public static bool StopSpectate_Prefix(ref PlayerAvatar ___player, SpectateCamera __instance)
        // {
        //     ValRepoMod.Log.LogInfo($"StopSpectate player Hashcode: {___player.GetHashCode()}");
        //     PlayerAvatarPatches.IsPlayerDead[___player.GetHashCode()] = false;
        //     ValRepoMod.Log.LogInfo("SpectateCamera.StopSpectate prefix!");
        //     return true;
        // }

        [HarmonyPatch("SetDeath")]
        [HarmonyPostfix]
        public static void SetDeath_Postfix(ref Transform ___deathPlayerSpectatePoint, ref PlayerAvatar ___player,
            ref SpectateCamera __instance)
        {
            if (!___player || !___player.playerDeathHead || !___player.GetPrivateField<bool>("isLocal"))
            {
                // ValRepoMod.Log.LogInfo("Skipping SetDeath_Postfix!");
                return;
            }

            // ValRepoMod.Log.LogInfo("Running SetDeath_Postfix!");
            ___deathPlayerSpectatePoint = ___player.playerDeathHead.transform;
        }

        [HarmonyPatch("StateNormal")]
        [HarmonyPrefix]
        public static bool StateNormal_Prefix(ref PlayerAvatar ___player, ref bool ___stateImpulse,
            ref Camera ___MainCamera, ref float ___previousFarClipPlane, ref float ___previousFieldOfView,
            ref Camera ___TopCamera, ref float ___normalMaxDistance, ref float ___normalMinDistance,
            ref float ___normalAimHorizontal, ref float ___normalAimVertical, ref Vector3 ___normalPreviousPosition,
            ref Transform ___normalTransformPivot, ref float ___normalDistanceTarget, SpectateCamera __instance)
        {
            ___player = PlayerAvatarPatches._localPlayer;
            var hashCode = ___player?.GetHashCode();
            var playerName = "NullName";
            if (___player)
            {
                playerName = ___player.GetPrivateField<string>("playerName");
            }

            // ValRepoMod.Log.LogInfo($"StateNormal player Hashcode: {hashCode}");
            if (!___player || !PlayerAvatarPatches.IsPlayerDead.GetValueOrDefault(___player.GetHashCode(), false) ||
                !___player.playerDeathHead)
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
                (float)((double)num1 * (double)CameraAim.Instance.AimSpeedMouse * 1.5);
            if ((double)___normalAimHorizontal > 360.0)
                ___normalAimHorizontal -= 360f;
            if ((double)___normalAimHorizontal < -360.0)
                ___normalAimHorizontal += 360f;
            var normalAimVertical = ___normalAimVertical;
            var num4 = (float)(-((double)num2 * (double)CameraAim.Instance.AimSpeedMouse) * 1.5);
            ___normalAimVertical += num4;
            ___normalAimVertical =
                Mathf.Clamp(___normalAimVertical, -70f, 70f);
            if ((double)num3 != 0.0)
                ___normalMaxDistance = Mathf.Clamp(___normalMaxDistance - num3 * (1f / 400f), ___normalMinDistance, 6f);
            var b1 = playerDeathHead.position;
            b1 += Vector3.up * 0.3f;
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
                (int)SemiFunc.LayerMaskGetVisionObstruct());
            if (raycastHitArray.Length != 0)
            {
                foreach (var raycastHit in raycastHitArray)
                {
                    if (!(bool)(UnityEngine.Object)raycastHit.transform.GetComponent<PlayerHealthGrab>() &&
                        !(bool)(UnityEngine.Object)raycastHit.transform.GetComponent<PlayerAvatar>() &&
                        !(bool)(UnityEngine.Object)raycastHit.transform.GetComponent<PlayerTumble>())
                    {
                        num5 = Mathf.Min(num5, raycastHit.distance);
                        if (raycastHit.transform.CompareTag("Wall"))
                            flag = true;
                        if ((double)raycastHit.collider.bounds.size.magnitude > 2.0)
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
            var b2 = (float)((double)num6 - (double)num7 - 0.10000000149011612);
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
            if ((double)__instance.transform.position.y <
                (double)playerDeathHead.position.y + 0.25 &&
                (double)num4 < 0.0)
                ___normalAimVertical = normalAimVertical;
            return false;
        }
    }

    public static class Utilities
    {
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


        public static TField GetPrivateField<TField>(this object instance, string fieldName)
        {
            var field = AccessTools.Field(instance.GetType(), fieldName);
            if (field == null)
            {
                ValRepoMod.Log.LogWarning($"Field '{fieldName}' not found on type '{instance.GetType()}'.");
                var fields = instance.GetType().GetFields(
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);
                foreach (var f in fields)
                {
                    // ValRepoMod.Log.LogInfo($"Found field: {f.Name}");
                }

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
        [HarmonyPatch("gameStateDeath")]
        [HarmonyPostfix]
        public static void GameStateDeath_Postfix(GameDirector __instance)
        {
            AudioManager.instance.SetSoundSnapshot(AudioManager.SoundSnapshot.On, 0.1f);
        }
    }

    [HarmonyPatch(typeof(AudioLowPassLogic))]
    public static class AudioLowPassLogicPatches
    {
        [HarmonyPatch("CheckLogic")]
        [HarmonyPostfix]
        public static void CheckLogic_Postfix(ref Transform ___audioListener, ref AudioSource ___AudioSource,
            AudioLowPassLogic __instance)
        {
            if (!___audioListener || !___AudioSource ||
                ___AudioSource.spatialBlend <= 0.0)
            {
                return;
            }

            // __instance.LowPass = true;
        }
    }

    [HarmonyPatch(typeof(ChatManager))]
    public static class ChatManagerPatches
    {
        private static string _lastMessage = "";

        [HarmonyPatch("StateSend")]
        [HarmonyPrefix]
        public static bool StateSend_Prefix(ref string ___chatMessage, ChatManager __instance)
        {
            // ValRepoMod.Log.LogInfo("StateSend prefix!");
            _lastMessage = ___chatMessage;
            return true;
        }

        [HarmonyPatch("StateSend")]
        [HarmonyPostfix]
        public static void StateSend_Postfix(ChatManager __instance)
        {
            // ValRepoMod.Log.LogInfo("StateSend postfix!");
            if (_lastMessage.ToLower().StartsWith("/rage"))
            {
                _lastMessage = _lastMessage.Substring("/rage ".Length);
                ChatManager.instance.PossessChatScheduleStart(10000);
                ChatManager.instance.PossessChat(
                    ChatManager.PossessChatID.SelfDestruct,
                    _lastMessage,
                    1.5f,
                    new Color(1f, 0f, 0f, 1f)
                );
                ChatManager.instance.PossessChatScheduleEnd();
                // ValRepoMod.Log.LogInfo("Valkyrie test message sent!");
            }
            else if (_lastMessage.ToLower().StartsWith("/test"))
            {
                foreach (var audioSource in Object.FindObjectsOfType<PlayerVoiceChat>())
                {
                    PrintFullHierarchy(audioSource.gameObject);
                }
            }

            _lastMessage = "";
        }

        // Call this method to print the full hierarchy for a given GameObject
        private static void PrintFullHierarchy(GameObject go)
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

            // ValRepoMod.Log.LogInfo("=== Parent Chain ===");
            for (var i = 0; i < parentChain.Count; i++)
            {
                var indent = new string(' ', i * 2);
                // ValRepoMod.Log.LogInfo(indent + parentChain[i].name + " - Components: " +
                                       // GetComponentsString(parentChain[i]));
            }

            // Print children hierarchy starting from the given GameObject
            // ValRepoMod.Log.LogInfo("=== Children Hierarchy ===");
            PrintChildren(go.transform, 1);
        }

        // Recursively print children of a Transform with indentation based on depth
        private static void PrintChildren(Transform t, int depth)
        {
            var indent = new string(' ', depth * 2);
            foreach (Transform child in t)
            {
                // ValRepoMod.Log.LogInfo(
                    // indent + child.gameObject.name + " - Components: " + GetComponentsString(child.gameObject));
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
    } // ChatManagerPatches
} // RepoModding
using System;
using System.Collections.Generic;
using System.Reflection;
using BoneLib;
using BoneLib.BoneMenu.Elements;
using HarmonyLib;
using LabFusion.Data;
using LabFusion.Extensions;
using LabFusion.MarrowIntegration;
using LabFusion.Network;
using LabFusion.Patching;
using LabFusion.Representation;
using LabFusion.SDK.Gamemodes;
using LabFusion.Senders;
using LabFusion.Utilities;
using MelonLoader;
using SLZ.Interaction;
using SLZ.Rig;
using SwipezGamemodeLib.Spawning;
using SwipezGamemodeLib.Spectator;
using SwipezGamemodeLib.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace TroubleInBoneTown
{
    public static class AssetBundleExtensioner
    {
        public static T LoadPersistentAsset<T>(this AssetBundle bundle, string name) where T : UnityEngine.Object {
            var asset = bundle.LoadAsset(name);

            if (asset != null) {
                asset.hideFlags = HideFlags.DontUnloadUnusedAsset;
                return asset.TryCast<T>();
            }

            return null;
        }
    }
    
    public class TroubleInBoneTownMainClass : MelonMod
    {
        private static GameObject wristUISpawned;
        private static TMP_Text timeText;
        
        public override void OnInitializeMelon()
        {
            GamemodeRegistration.LoadGamemodes(Assembly.GetExecutingAssembly());
            var assetBundle = EmbeddedAssetBundle.LoadFromAssembly(Assembly.GetExecutingAssembly(), "TroubleInBoneTown.Resources.tttassets.gamemode");
            TTTAssetsLoader.LoadAssets(assetBundle);
        }

        public override void OnLateInitializeMelon()
        {
            HarmonyInstance.Patch(typeof(Grip).GetMethod(nameof(Grip.OnAttachedToHand), AccessTools.all), new HarmonyMethod(typeof(GrabPatches.GrabPatch).GetMethod(nameof(GrabPatches.GrabPatch.Prefix))));
            HarmonyInstance.Patch(typeof(Hand).GetMethod(nameof(Hand.DetachObject), AccessTools.all), new HarmonyMethod(typeof(GrabPatches.GrabDetachPatch).GetMethod(nameof(GrabPatches.GrabDetachPatch.Prefix))));
        }

        public static void UpdateViewDegreeWristUI(float minX, float maxX)
        {
            if (wristUISpawned)
            {
                Vector3 euler = wristUISpawned.transform.eulerAngles;
                if (euler.x > minX && euler.x < maxX)
                {
                    wristUISpawned.SetActive(true);
                }
                else
                {
                    wristUISpawned.SetActive(false);
                }
            }
        }

        public static void ClearRoleWristUI(bool iconOnly)
        {
            if (wristUISpawned)
            {
                if (iconOnly)
                {
                    GameObject.Destroy(wristUISpawned.transform.Find("RoleDisplay").gameObject);
                }
                else
                {
                    GameObject.Destroy(wristUISpawned);
                }
            }
        }

        public static void MakeWristUI(string role)
        {
            if (!wristUISpawned)
            {
                float wristX = Player.GetCurrentAvatar().wristEllipse.XRadius;
                GameObject newWristUI = Object.Instantiate(TTTAssetsLoader.wristUI);
                newWristUI.transform.SetParent(Player.leftHand.transform);
                newWristUI.transform.localPosition = new Vector3(wristX*4f, 0, -0.07f);
                newWristUI.transform.localRotation = Quaternion.Euler(0, -2f, 0);
                wristUISpawned = newWristUI;
                Texture2D texture = TTTAssetsLoader.innocentLong;
                if (role == TroubleInBoneTownGamemode.traitor_role)
                {
                    texture = TTTAssetsLoader.traitorLong;
                }
                else if (role == TroubleInBoneTownGamemode.detective_role)
                {
                    texture = TTTAssetsLoader.detectiveLong;
                }

                RawImage roleImage = wristUISpawned.transform.Find("RoleDisplay").GetComponent<RawImage>();
                roleImage.texture = texture;
            }
        }

        public static void UpdateTime(long initialElapsed, long goalElapsed)
        {
            TimeSpan initialTime = TimeSpan.FromMilliseconds(initialElapsed);
            TimeSpan goalTime = TimeSpan.FromMilliseconds(goalElapsed);
            TimeSpan timeLeft = goalTime.Subtract(initialTime);
            if (!timeText)
            {
                timeText = wristUISpawned.transform.Find("TimeRemaining").GetComponent<TMP_Text>();
            }
            timeText.text = "Time Remaining: \n"+ timeLeft.ToString(@"mm\:ss");
        }
    }

    public static class TTTAssetsLoader
    {
        public static Texture2D innocentLogo;
        public static Texture2D traitorLogo;
        public static Texture2D detectiveLogo;
        public static Texture2D innocentLong;
        public static Texture2D traitorLong;
        public static Texture2D detectiveLong;
        public static GameObject inspectionGui;
        public static GameObject spectatorVolume;
        public static GameObject wristUI;

        public static void LoadAssets(AssetBundle assetBundle)
        {
            detectiveLogo = assetBundle.LoadPersistentAsset<Texture2D>("assets/tttassets/detective.png");
            traitorLogo = assetBundle.LoadPersistentAsset<Texture2D>("assets/tttassets/traitor.png");
            innocentLogo = assetBundle.LoadPersistentAsset<Texture2D>("assets/tttassets/innocent.png");
            detectiveLong = assetBundle.LoadPersistentAsset<Texture2D>("assets/tttassets/detectivelong.png");
            traitorLong = assetBundle.LoadPersistentAsset<Texture2D>("assets/tttassets/traitorlong.png");
            innocentLong = assetBundle.LoadPersistentAsset<Texture2D>("assets/tttassets/innocentlong.png");
            spectatorVolume = assetBundle.LoadPersistentAsset<GameObject>("assets/tttassets/specvolume.prefab");
            inspectionGui = assetBundle.LoadPersistentAsset<GameObject>("assets/tttassets/inspectiongui.prefab");
            wristUI = assetBundle.LoadPersistentAsset<GameObject>("assets/tttassets/wristui.prefab");
        }
    }

    public class TroubleInBoneTownGamemode : Gamemode
    {
        public static TroubleInBoneTownGamemode Instance { get; private set; }

        public override bool DisableDevTools => true;
        public override bool DisableSpawnGun => true;
        public override bool DisableManualUnragdoll => true;

        public override string GamemodeCategory => "Trouble in Bonetown";
        public override string GamemodeName => "Trouble In Bonetown";

        public const string DefaultPrefix = "InternalTTTMetadata";
        public const string PlayerRoleKey = DefaultPrefix + ".Role";

        public const string traitor_role = "TRAITOR";
        public const string innocent_role = "INNOCENT";
        public const string spectator_role = "SPECTATOR";
        public const string detective_role = "DETECTIVE";
        
        private const string traitor_color = "#ff0000";
        private const string innocent_color = "#00ff00";
        private const string spectator_color = "#ffffff";
        private const string detective_color = "#0000ff";
        
        private string winState = "none";

        private int traitorCount = 1;
        private int prepDurationSeconds = 10;
        private int roundDurationMinutes = 5;
        
        private int serverRoundDurationMinutes = 0;

        public static Dictionary<PlayerId, string> playerRoles = new Dictionary<PlayerId, string>();
        private List<RigManager> ragdolls = new List<RigManager>();

        private GamemodeTimer prepTimeTimer = new GamemodeTimer();
        private GamemodeTimer roundTimer = new GamemodeTimer();

        private GameObject spectatorVolume;
        private bool ignoreNextPlayerDeath = false;
        private bool checkRolesNow = false;
        
        private string _localPlayerRole = "none";

        private Dictionary<PlayerId, HeadLogoIcon> _headLogoIcons = new Dictionary<PlayerId, HeadLogoIcon>();

        public static Dictionary<RigManager, ChestInspection> _ChestInspections =
            new Dictionary<RigManager, ChestInspection>();

        public class ChestInspection
        {
            public GameObject inspection;
            public RigManager manager;

            public PlayerRep rep;
            private GamemodeTimer _gamemodeTimer;
            private TMP_Text _timeText;

            public ChestInspection(RigManager rigManager, PlayerId playerId)
            {
                GameObject go = GameObject.Instantiate(TTTAssetsLoader.inspectionGui);
                GameObject.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.DontUnloadUnusedAsset;
                go.name = $"InspectionGUI {playerId.LongId}";
                inspection = go;
                _gamemodeTimer = new GamemodeTimer();
                _gamemodeTimer.Start();

                manager = rigManager;
                
                PlayerRepManager.TryGetPlayerRep(playerId, out rep);
                string name;
                playerId.TryGetDisplayName(out name);

                inspection.transform.Find("Name").gameObject.GetComponent<TextMeshPro>().text = name;

                Texture2D texture = TTTAssetsLoader.innocentLogo;
                string role = playerRoles[playerId];
                if (role == traitor_role)
                {
                    texture = TTTAssetsLoader.traitorLogo;
                }
                if (role == detective_role)
                {
                    texture = TTTAssetsLoader.detectiveLogo;
                }

                inspection.transform.Find("RoleIcon").gameObject.GetComponent<RawImage>().texture = texture;
                _timeText = inspection.transform.Find("TimeSinceDeath").gameObject.GetComponent<TextMeshPro>();
            }
            
            public void Toggle(bool value) {
                inspection.SetActive(value);
            }

            public void Cleanup()
            {
                GameObject.Destroy(inspection);
            }

            private void UpdateTime()
            {
                _timeText.text = "Time since Death: \n" + _gamemodeTimer.ConvertToReadableTime();
            }

            public void Update()
            {
                if (manager)
                {
                    UpdateTime();
                    var chest = manager.physicsRig.m_chest;
                    inspection.transform.position = chest.position + Vector3.up * GetInspectionOffset(manager);
                    inspection.transform.LookAtPlayer();
                }
            }
            
            private float GetInspectionOffset(RigManager rm)
            {
                float offset = 0.2f;

                if (rm._avatar)
                    offset *= rm._avatar.height;

                return offset;
            }
        }

        public class HeadLogoIcon
        {
            protected const float LogoDivider = 270f;

            public GameObject go;
            public Canvas canvas;
            public RawImage image;
            
            public PlayerRep rep;
            
            public HeadLogoIcon(PlayerId id) 
            {
                go = new GameObject($"{id.SmallId} Head Logo");

                canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 100000;
                go.transform.localScale = Vector3.one / LogoDivider;

                image = go.AddComponent<RawImage>();

                GameObject.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.DontUnloadUnusedAsset;
                
                PlayerRepManager.TryGetPlayerRep(id, out rep);
            }
            
            public void Cleanup() 
            {
                if (go)
                {
                    GameObject.Destroy(go);
                }
            }

            public void Toggle(bool value) {
                go.SetActive(value);
            }

            public void SetLogo(Texture2D texture2D)
            {
                image.texture = texture2D;
            }
            
            public bool IsShown() => go.activeSelf;

            public void Update() 
            {
                if (rep != null) {
                    var rm = rep.RigReferences.RigManager;

                    if (rm) {
                        var head = rm.physicsRig.m_head;

                        go.transform.position = head.position + Vector3.up * rep.GetNametagOffset();
                        go.transform.LookAtPlayer();
                    }
                }
            }
        }

        public override void OnBoneMenuCreated(MenuCategory category) {
            base.OnBoneMenuCreated(category);
            category.CreateIntElement("Round Minutes", Color.yellow, roundDurationMinutes, 1, 1, 15, (num) => {
                roundDurationMinutes = num;
            });
            
            category.CreateIntElement("Prep Time Seconds", Color.yellow, prepDurationSeconds, 1, 5, 30, (num) => {
                prepDurationSeconds = num;
            });
            
            category.CreateIntElement("Traitor Count", Color.red, traitorCount, 1, 1, 5, (num) => {
                traitorCount = num;
            });
        }
        
        public override void OnGamemodeRegistered()
        {
            base.OnGamemodeRegistered();

            Instance = this;

            MultiplayerHooking.OnPlayerAction += OnPlayerAction;
            MultiplayerHooking.OnPlayerJoin += OnPlayerJoin;
            MultiplayerHooking.OnPlayerLeave += OnPlayerLeave;
            FusionOverrides.OnValidateNametag += OnValidateNametag;
        }

        public override void OnGamemodeUnregistered()
        {
            base.OnGamemodeUnregistered();
            
            if (Instance == this)
                Instance = null;
            
            MultiplayerHooking.OnPlayerAction -= OnPlayerAction;
            MultiplayerHooking.OnPlayerJoin -= OnPlayerJoin;
            MultiplayerHooking.OnPlayerLeave -= OnPlayerLeave;
            FusionOverrides.OnValidateNametag -= OnValidateNametag;
        }

        protected override void OnStartGamemode()
        {
            base.OnStartGamemode();
            checkRolesNow = false;
            _localPlayerRole = "none";
            FusionPlayer.SetAvatarOverride(Player.rigManager.AvatarCrate._barcode);
            FusionPlayer.SetAmmo(10000);
            FusionPlayer.SetMortality(true);
            FusionOverrides.ForceUpdateOverrides();
            if (NetworkInfo.IsServer)
            {
                TryInvokeTrigger("PrepPhase;"+prepDurationSeconds);
                prepTimeTimer.Start();
            }

            foreach (var playerId in PlayerIdManager.PlayerIds)
            {
                HeadLogoIcon headLogo = new HeadLogoIcon(playerId);
                headLogo.Toggle(false);
                _headLogoIcons.Add(playerId, headLogo);
            }
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (IsActive())
            {
                foreach (var keyPair in _headLogoIcons)
                {
                    keyPair.Value.Update();
                }
                
                foreach (var keyPair in _ChestInspections)
                {
                    keyPair.Value.Update();
                }
                
                if (roundTimer.isRunning)
                {
                    TroubleInBoneTownMainClass.UpdateViewDegreeWristUI(1, 70);
                    TroubleInBoneTownMainClass.UpdateTime(roundTimer.GetTimeMs(), serverRoundDurationMinutes * 60 * 1000);
                }
                
                if (NetworkInfo.IsServer)
                {
                    if (roundTimer.IsFinishedInMinutes(roundDurationMinutes))
                    {
                        roundTimer.Reset();
                        TryInvokeTrigger("RoundEndTimer");
                        StopGamemode();
                    }

                    if (prepTimeTimer.IsFinishedInSeconds(prepDurationSeconds))
                    {
                        prepTimeTimer.Reset();
                        TryInvokeTrigger("StartPhase;"+roundDurationMinutes);

                        List<PlayerId> roleLessPlayers = new List<PlayerId>();

                        roleLessPlayers.AddRange(PlayerIdManager.PlayerIds);

                        Random random = new Random();

                        List<PlayerId> traitors = new List<PlayerId>();

                        for (int i = 0; i < traitorCount; i++)
                        {
                            int randomIndex = random.Next(0, roleLessPlayers.Count);
                            PlayerId randomPlayerId = roleLessPlayers[randomIndex];
                            roleLessPlayers.RemoveAt(randomIndex);
                            traitors.Add(randomPlayerId);
                            SetRole(randomPlayerId, traitor_role);
                        }

                        if (roleLessPlayers.Count > 0)
                        {
                            // Detective
                            int detectiveIndex = random.Next(0, roleLessPlayers.Count);
                            PlayerId detectivePlayerId = roleLessPlayers[detectiveIndex];
                            roleLessPlayers.RemoveAt(detectiveIndex);
                            SetRole(detectivePlayerId, detective_role);

                            // Innocents
                            foreach (PlayerId playerId in roleLessPlayers)
                            {
                                SetRole(playerId, innocent_role);
                            }
                        }

                        string triggerTraitorsDisplay = "TRAITORS;";

                        foreach (var playerId in traitors)
                        {
                            triggerTraitorsDisplay += playerId.LongId + ";";
                        }

                        TryInvokeTrigger(triggerTraitorsDisplay);
                    }
                }
            }
        }

        protected void OnPlayerJoin(PlayerId playerId)
        {
            if (IsActive())
            {
                if (NetworkInfo.IsServer)
                {
                    if (roundTimer.isRunning)
                    {
                        SetRole(playerId, spectator_role);
                    }
                }
            }
        }
        
        protected void OnPlayerLeave(PlayerId playerId)
        {
            if (IsActive())
            {
                if (NetworkInfo.IsServer)
                {
                    if (roundTimer.isRunning)
                    {
                        CheckWinCondition();
                    }
                }
            }
        }

        protected void OnPlayerAction(PlayerId player, PlayerActionType action, PlayerId otherPlayer)
        {
            if (IsActive() && roundTimer.isRunning)
            {
                if (action == PlayerActionType.DEATH)
                {
                    checkRolesNow = true;
                    if (player == PlayerIdManager.LocalId)
                    {
                        if (ignoreNextPlayerDeath)
                        {
                            ignoreNextPlayerDeath = false;
                            return;
                        }

                        ignoreNextPlayerDeath = true;
                    }
                    
                    if (NetworkInfo.IsServer)
                    {
                        SetRole(player, spectator_role);
                    }
                    
                    if (player != PlayerIdManager.LocalId)
                    {
                        if (PlayerRepManager.TryGetPlayerRep(player, out var rep))
                        {
                            Vector3 storedPosition = rep.RigReferences.RigManager.physicsRig.feet.transform.position + new Vector3(0, 0.3f, 0);
                            player.Hide();
                            SpawnManager.SpawnRagdoll(rep.RigReferences.RigManager.AvatarCrate._barcode, storedPosition,
                                Quaternion.identity,
                                manager =>
                                {
                                    ChestInspection chestInspection = new ChestInspection(manager, player);
                                    chestInspection.Toggle(false);
                                    _ChestInspections.Add(manager, chestInspection);
                                    manager.name = $"Ragdoll [GAMEMODE_TTT] {player.LongId}";
                                    ragdolls.Add(manager);
                                });
                        }
                    }
                    else
                    {
                        Vector3 storedPosition = Player.rigManager.physicsRig.m_head.position +
                                                 Vector3Extensions.up * GetRagdollOffset(Player.rigManager);
                        Player.leftHand.DetachObject();
                        Player.rightHand.DetachObject();
                        SpawnManager.SpawnRagdoll(Player.rigManager.AvatarCrate._barcode, storedPosition,
                            Quaternion.identity,
                            manager =>
                            {
                                manager.name = $"Ragdoll [GAMEMODE_TTT] {player.LongId}";
                                ragdolls.Add(manager);
                            });
                    }
                }
            }
        }

        public float GetRagdollOffset(RigManager rm)
        {
            float offset = 0.1f;

            if (rm._avatar)
                offset *= rm._avatar.height;

            return offset;
        }

        protected override void OnEventTriggered(string value)
        {
            if (value == "RoundEndTimer")
            {
                string subtitle = "YOU WON! (Time Ran Out)";
                if (_localPlayerRole == traitor_role)
                {
                    subtitle = "YOU LOST! (Time Ran Out)";
                }

                FusionNotifier.Send(new FusionNotification()
                {
                    title = $"<color={innocent_color}>INNOCENTS WIN!",
                    showTitleOnPopup = true,
                    message = subtitle,
                    isMenuItem = false,
                    isPopup = true,
                });
            }

            if (value == "InnocentWin")
            {
                string subtitle = "YOU WON!";
                if (_localPlayerRole == traitor_role)
                {
                    subtitle = "YOU LOST!";
                }
                FusionNotifier.Send(new FusionNotification()
                {
                    title = $"<color={innocent_color}>INNOCENTS WIN!",
                    showTitleOnPopup = true,
                    message = subtitle,
                    isMenuItem = false,
                    isPopup = true,
                });
            }

            if (value == "TraitorWin")
            {
                string subtitle = "YOU WON!";
                if (_localPlayerRole == detective_role || _localPlayerRole == innocent_role)
                {
                    subtitle = "YOU LOST!";
                }

                FusionNotifier.Send(new FusionNotification()
                {
                    title = $"<color={traitor_color}>TRAITORS WIN!",
                    showTitleOnPopup = true,
                    message = subtitle,
                    isMenuItem = false,
                    isPopup = true,
                });
            }

            if (value.StartsWith("StartPhase"))
            {
                int minutes = int.Parse(value.Split(';')[1]);
                serverRoundDurationMinutes = minutes;
                roundTimer.Start();
                FusionPlayer.SetPlayerVitality(1);
                
                // Teleport players to a random spawn point. This uses DEATHMATCH spawn points.
                List<Transform> transforms = new List<Transform>();
                foreach (var point in DeathmatchSpawnpoint.Cache.Components) {
                    transforms.Add(point.transform);
                }

                if (transforms.Count > 0)
                {
                    Random random = new Random();
                    // get random position
                    int randomIndex = random.Next(0, transforms.Count);
                    Transform randomPosition = transforms[randomIndex];
                
                    FusionPlayer.Teleport(randomPosition.position, randomPosition.forward);
                }
            }

            if (value.StartsWith("PrepPhase"))
            {
                FusionPlayer.SetPlayerVitality(1000);
                int seconds = int.Parse(value.Split(';')[1]);
                FusionNotifier.Send(new FusionNotification()
                {
                    title = "Preparing Round...",
                    showTitleOnPopup = true,
                    message = $"Get ready to receive your roles in {seconds} seconds!",
                    isMenuItem = false,
                    isPopup = true,
                });
            }

            if (value.StartsWith("TRAITORS;"))
            {
                string[] split = value.Split(';');
                List<PlayerId> ids = new List<PlayerId>();
                int index = 0;
                foreach (var stringSplit in split)
                {
                    if (index != 0)
                    {
                        try
                        {
                            PlayerId playerId = PlayerIdManager.GetPlayerId(ulong.Parse(stringSplit));
                            ids.Add(playerId);
                        }
                        catch (Exception e)
                        {
                            // Ignore
                        }
                    }
                    index++;
                }
                TryDisplayTraitors(ids);
            }
        }

        private void TryDisplayTraitors(List<PlayerId> playerId)
        {
            if (GetRole(PlayerIdManager.LocalId) == traitor_role)
            {
                string partnerSubtitle = "Your partners are: ";
                
                int index = 0;
                foreach (var id in playerId)
                {
                    // Dont display ourselves in the partners list.
                    if (id == PlayerIdManager.LocalId)
                    {
                        continue;
                    }

                    _headLogoIcons[id].SetLogo(TTTAssetsLoader.traitorLogo);
                    _headLogoIcons[id].Toggle(true);
                    
                    if (id.TryGetDisplayName(out var displayName))
                    {
                        if (index != 0)
                        {
                            partnerSubtitle += ", ";
                        }

                        partnerSubtitle += displayName;
                    }

                    index++;
                }
                
                // We are the only traitor.
                if (playerId.Count == 1)
                {
                    partnerSubtitle = "You're on your own...";
                }
                
                FusionNotifier.Send(new FusionNotification()
                {
                    title = $"<color={traitor_color}>YOU ARE A TRAITOR!",
                    showTitleOnPopup = true,
                    message = partnerSubtitle,
                    isMenuItem = false,
                    popupLength = 5f,
                    isPopup = true,
                });
            }
        }

        protected override void OnMetadataChanged(string key, string value)
        {
            base.OnMetadataChanged(key, value);
            
            if (key.StartsWith(PlayerRoleKey))
            {
                string[] split = key.Split('.');
                ulong id = ulong.Parse(split[2]);
                PlayerId associatedId = PlayerIdManager.GetPlayerId(id);
                if (associatedId != null)
                {
                    OnRoleChanged(associatedId, value); 
                }
            }
        }

        private void CheckWinCondition()
        {
            if (GetAliveTraitors() == 0)
            {
                TryInvokeTrigger("InnocentWin");
                StopGamemode();
            }

            if (GetAliveInnocents() == 0)
            {
                TryInvokeTrigger("TraitorWin");
                StopGamemode();
            }
        }

        private void OnRoleChanged(PlayerId playerId, string role)
        {
            if (!playerRoles.ContainsKey(playerId))
            {
                playerRoles.Add(playerId, role);
            }

            playerRoles[playerId] = role;

            if (playerId == PlayerIdManager.LocalId)
            {
                if (_localPlayerRole == "none")
                {
                    _localPlayerRole = role;
                }
            }

            // Dont flood with notifs
            // No reason to tell the player they're a spectator if the round is over.
            // Check if win condition has been met already.
            bool ignoreSpecNotification = GetAliveInnocents() == 0 || GetAliveTraitors() == 0;

            if (role == spectator_role)
            {
                if (playerId == PlayerIdManager.LocalId)
                {
                    if (!ignoreSpecNotification)
                    {
                        FusionNotifier.Send(new FusionNotification()
                        {
                            title = "YOU ARE NOW SPECTATING!",
                            showTitleOnPopup = true,
                            message = "Wait for the match to be over!",
                            isMenuItem = false,
                            popupLength = 5f,
                            isPopup = true,
                        });
                    }
                    
                    spectatorVolume = GameObject.Instantiate(TTTAssetsLoader.spectatorVolume);
                    FusionPlayerExtended.SetWorldInteractable(false);
                    FusionPlayerExtended.SetCanDamageOthers(false);
                    FusionPlayer.SetAmmo(0);
                    FusionPlayer.SetPlayerVitality(100);
                    TroubleInBoneTownMainClass.ClearRoleWristUI(true);
                }
                else
                {
                    playerId.Hide();
                    if (_headLogoIcons.ContainsKey(playerId))
                    {
                        _headLogoIcons[playerId].Toggle(false);
                    }
                }
            }

            if (role == innocent_role)
            {
                if (playerId == PlayerIdManager.LocalId)
                {
                    FusionNotifier.Send(new FusionNotification()
                    {
                        title = $"<color={innocent_color}>YOU ARE AN INNOCENT!",
                        showTitleOnPopup = true,
                        message = "Find and kill the Traitors!",
                        isMenuItem = false,
                        popupLength = 5f,
                        isPopup = true,
                    });
                    TroubleInBoneTownMainClass.MakeWristUI(role);
                }
            }
            
            if (role == detective_role)
            {
                if (playerId == PlayerIdManager.LocalId)
                {
                    FusionNotifier.Send(new FusionNotification()
                    {
                        title = $"<color={detective_color}>YOU ARE THE DETECTIVE!",
                        showTitleOnPopup = true,
                        message = "Find and kill the Traitors!",
                        isMenuItem = false,
                        popupLength = 5f,
                        isPopup = true,
                    });
                    TroubleInBoneTownMainClass.MakeWristUI(role);
                }
                else
                {
                    HeadLogoIcon headLogoIcon = _headLogoIcons[playerId];
                    headLogoIcon.SetLogo(TTTAssetsLoader.detectiveLogo);
                    headLogoIcon.Toggle(true);
                }
            }

            if (role == traitor_role)
            {
                if (playerId == PlayerIdManager.LocalId)
                {
                    TroubleInBoneTownMainClass.MakeWristUI(role);
                }
            }

            if (NetworkInfo.IsServer)
            {
                if (checkRolesNow)
                {
                    CheckWinCondition();
                }
            }
        }

        private int GetAlivePlayers()
        {
            return GetPlayersWithRole(innocent_role).Count + GetPlayersWithRole(detective_role).Count + GetPlayersWithRole(traitor_role).Count;
        }
        
        private int GetAliveInnocents()
        {
            return GetPlayersWithRole(innocent_role).Count + GetPlayersWithRole(detective_role).Count;
        }
        
        private int GetAliveTraitors()
        {
            return GetPlayersWithRole(traitor_role).Count;
        }

        private string GetRole(PlayerId playerId)
        {
            if (TryGetMetadata(GetRoleKey(playerId), out var value))
            {
                return value;
            }
            return "UNDEFINED";
        }

        private List<PlayerId> GetPlayersWithRole(string role)
        {
            List<PlayerId> players = new List<PlayerId>();
            foreach (KeyValuePair<PlayerId, string> pair in playerRoles)
            {
                if (pair.Value == role)
                {
                    players.Add(pair.Key);
                }
            }

            return players;
        }

        protected override void OnStopGamemode()
        {
            base.OnStopGamemode();
            TroubleInBoneTownMainClass.ClearRoleWristUI(false);
            FusionPlayer.ClearAvatarOverride();
            foreach (var headLogoIcon in _headLogoIcons.Values)
            {
                headLogoIcon.Cleanup();
            }
            foreach (var chestInspection in _ChestInspections.Values)
            {
                chestInspection.Cleanup();
            }

            _headLogoIcons.Clear();
            _ChestInspections.Clear();
            
            roundTimer.Reset();
            prepTimeTimer.Reset();
            
            FusionPlayerExtended.SetWorldInteractable(true);
            FusionPlayerExtended.SetCanDamageOthers(true);
            FusionPlayer.SetAmmo(1000);
            FusionPlayer.ClearPlayerVitality();
            FusionOverrides.ForceUpdateOverrides();
            if (spectatorVolume != null)
            {
                Object.DestroyObject(spectatorVolume);
            }

            foreach (var rigmanager in ragdolls)
            {
                GameObject.Destroy(rigmanager.gameObject);
            }
            ragdolls.Clear();

            foreach (var spectator in GetPlayersWithRole(spectator_role))
            {
                try
                {
                    spectator.Show();
                }
                catch
                {
                    // ignored
                }
            }
            playerRoles.Clear();
        }

        private void SetRole(PlayerId playerId, string role)
        {
            if (NetworkInfo.IsServer)
            {
                TrySetMetadata(GetRoleKey(playerId), role);
            }
        }

        private string GetRoleKey(PlayerId playerId)
        {
            return PlayerRoleKey + "." + playerId.LongId;
        }

        protected bool OnValidateNametag(PlayerId id) {
            if (!IsActive())
            {
                return true;
            }
            return false;
        }
    }
}
using Accord.Controls.Vision;
using BepInEx;
using BepInEx.Logging;
using GlobalEnums;
using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

using static SilksongNeuralNetwork.Utils;

namespace SilksongNeuralNetwork
{
    [BepInPlugin("com.hackwhiz.nnagent", "Neural Net Agent for Silksong", "1.0.0")]
    public class Agent : BaseUnityPlugin
    {
        public static Agent Instance { get; private set; }

        public HeroController hero;
        public Rigidbody2D rb;
        public bool initialized = false;

        public InputHandler myInputHandler;
        public HeroActions myInputActions;

        // Контролер для бота

        // Публічний доступ до контролера

        // Змінна для зберігання посилання на екземпляр SceneBounds
        private SceneBounds sceneBoundsInstance;

        private void Awake()
        {
            Logger.LogInfo("Plugin loaded and initialized");

            Instance = this;

            // Перевіряємо, чи існує SceneBoundsManager в сцені.
            // Якщо ні, створюємо його.
            if (SceneBounds.Instance == null)
            {
                GameObject sceneBoundsObject = new GameObject("SceneBoundsManager");
                sceneBoundsInstance = sceneBoundsObject.AddComponent<SceneBounds>();
                // Зробіть його постійним між завантаженнями сцен, якщо потрібно
                DontDestroyOnLoad(sceneBoundsObject);
            }
            else
            {
                sceneBoundsInstance = SceneBounds.Instance;
            }

            hero = GameObject.FindFirstObjectByType<HeroController>();
            Harmony.CreateAndPatchAll(typeof(Agent), null);
        }

        private bool Initialize()
        {
            try
            {
                hero = GameObject.FindFirstObjectByType<HeroController>();

                if (hero != null)
                {
                    rb = hero.GetComponent<Rigidbody2D>();
                    if (rb == null)
                    {
                        Logger.LogError("Failed to get Rigidbody2D component");
                        return false;
                    }

                    var field = typeof(HeroController).GetField("inputHandler",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    if (field == null)
                    {
                        Logger.LogError("Failed to find inputHandler field");
                        return false;
                    }

                    myInputHandler = (InputHandler)field.GetValue(hero);
                    if (myInputHandler == null)
                    {
                        Logger.LogError("inputHandler is null");
                        return false;
                    }

                    myInputActions = myInputHandler.inputActions;
                    if (myInputActions == null)
                    {
                        Logger.LogError("inputActions is null");
                        return false;
                    }

                    // Ініціалізуємо контролер для бота

                    initialized = true;
                    Logger.LogInfo("Agent initialized successfully.");
                    return true;
                }
                else
                {
                    Logger.LogWarning("HeroController not found. Initialization failed.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error during initialization: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), "BeginScene")]
        public static void SceneLoadPostFix()
        {
            // Тепер використовуємо sceneBoundsInstance, який буде ініціалізовано
            if (Instance.sceneBoundsInstance != null)
            {
                Instance.sceneBoundsInstance.UpdateBounds(); // THIS DON'T REALLY WORKS!!
                Instance.Logger.LogInfo($"UPDATED BOUNDS. MinX: {Instance.sceneBoundsInstance.minX}, MaxX: {Instance.sceneBoundsInstance.maxX}");
            }
            else
            {
                Instance.Logger.LogInfo("SceneBounds instance is null after scene load.");
            }
            Instance.Logger.LogInfo("LOADED SCENE");
        }

        private List<float> GetData()
        {
            List<float> inputData = new List<float>();
            List<float> outputData = new List<float>();

            List<float> hornetState = new List<float>();
            List<float> globalActions = new List<float>();

            if (PlayerData.instance != null)
            {
                hornetState.Add(Normalize(PlayerData.instance.health, PlayerData.instance.maxHealth));
                hornetState.Add(Normalize(PlayerData.instance.silk, PlayerData.instance.silkMax));
                // coords x and y
                hornetState.Add(Normalize(hero.transform.position.x, 1000)); // should try to find max dynamicly
                hornetState.Add(Normalize(hero.transform.position.y, 1000)); // should try to find max dynamicly

                // velocity
                hornetState.Add(Normalize(hero.Body.linearVelocity.x, hero.GetRunSpeed()));
                hornetState.Add(Normalize(hero.Body.linearVelocity.y, hero.JUMP_SPEED));

                // state
                hornetState.Add(BoolToFloat(hero.cState.facingRight));
                hornetState.Add(BoolToFloat(hero.cState.onGround));
                hornetState.Add(BoolToFloat(hero.cState.jumping));
                hornetState.Add(BoolToFloat(hero.cState.shuttleCock));
                hornetState.Add(BoolToFloat(hero.cState.floating));
                hornetState.Add(BoolToFloat(hero.cState.wallJumping));
                hornetState.Add(BoolToFloat(hero.cState.doubleJumping));
                hornetState.Add(BoolToFloat(hero.cState.nailCharging));
                hornetState.Add(BoolToFloat(hero.cState.shadowDashing));
                hornetState.Add(BoolToFloat(hero.cState.swimming));
                hornetState.Add(BoolToFloat(hero.cState.falling));
                hornetState.Add(BoolToFloat(hero.cState.dashing));
                hornetState.Add(BoolToFloat(hero.cState.isSprinting));
                hornetState.Add(BoolToFloat(hero.cState.isBackSprinting));
                hornetState.Add(BoolToFloat(hero.cState.isBackScuttling));
                hornetState.Add(BoolToFloat(hero.cState.airDashing));
                hornetState.Add(BoolToFloat(hero.cState.superDashing));
                hornetState.Add(BoolToFloat(hero.cState.superDashOnWall));
                hornetState.Add(BoolToFloat(hero.cState.backDashing));
                hornetState.Add(BoolToFloat(hero.cState.touchingWall));
                hornetState.Add(BoolToFloat(hero.cState.wallSliding));
                hornetState.Add(BoolToFloat(hero.cState.wallClinging));
                hornetState.Add(BoolToFloat(hero.cState.wallScrambling));
                hornetState.Add(BoolToFloat(hero.cState.transitioning));
                hornetState.Add(BoolToFloat(hero.cState.attacking));
                hornetState.Add(BoolToFloat(hero.cState.lookingUp));
                hornetState.Add(BoolToFloat(hero.cState.lookingDown));
                hornetState.Add(BoolToFloat(hero.cState.lookingUpRing));
                hornetState.Add(BoolToFloat(hero.cState.lookingDownRing));
                hornetState.Add(BoolToFloat(hero.cState.lookingUpAnim));
                hornetState.Add(BoolToFloat(hero.cState.lookingDownAnim));
                hornetState.Add(BoolToFloat(hero.cState.altAttack));
                hornetState.Add(BoolToFloat(hero.cState.upAttacking));
                hornetState.Add(BoolToFloat(hero.cState.downAttacking));
                hornetState.Add(BoolToFloat(hero.cState.downTravelling));
                hornetState.Add(BoolToFloat(hero.cState.downSpikeAntic));
                hornetState.Add(BoolToFloat(hero.cState.downSpiking));
                hornetState.Add(BoolToFloat(hero.cState.downSpikeBouncing));
                hornetState.Add(BoolToFloat(hero.cState.downSpikeBouncingShort));
                hornetState.Add(BoolToFloat(hero.cState.downSpikeRecovery));
                hornetState.Add(BoolToFloat(hero.cState.bouncing));
                hornetState.Add(BoolToFloat(hero.cState.shroomBouncing));
                hornetState.Add(BoolToFloat(hero.cState.recoilingRight));
                hornetState.Add(BoolToFloat(hero.cState.recoilingLeft));
                hornetState.Add(BoolToFloat(hero.cState.recoilingDrill));
                hornetState.Add(BoolToFloat(hero.cState.dead));
                hornetState.Add(BoolToFloat(hero.cState.isFrostDeath));
                hornetState.Add(BoolToFloat(hero.cState.hazardDeath));
                hornetState.Add(BoolToFloat(hero.cState.hazardRespawning));
                hornetState.Add(BoolToFloat(hero.cState.willHardLand));
                hornetState.Add(BoolToFloat(hero.cState.recoilFrozen));
                hornetState.Add(BoolToFloat(hero.cState.recoiling));
                hornetState.Add(BoolToFloat(hero.cState.invulnerable));
                hornetState.Add(BoolToFloat(hero.cState.casting));
                hornetState.Add(BoolToFloat(hero.cState.castRecoiling));
                hornetState.Add(BoolToFloat(hero.cState.preventDash));
                hornetState.Add(BoolToFloat(hero.cState.preventBackDash));
                hornetState.Add(BoolToFloat(hero.cState.dashCooldown));
                hornetState.Add(BoolToFloat(hero.cState.backDashCooldown));
                hornetState.Add(BoolToFloat(hero.cState.nearBench));
                hornetState.Add(BoolToFloat(hero.cState.inWalkZone));
                hornetState.Add(BoolToFloat(hero.cState.isPaused));
                hornetState.Add(BoolToFloat(hero.cState.onConveyor));
                hornetState.Add(BoolToFloat(hero.cState.onConveyorV));
                hornetState.Add(BoolToFloat(hero.cState.inConveyorZone));
                hornetState.Add(BoolToFloat(hero.cState.spellQuake));
                hornetState.Add(BoolToFloat(hero.cState.freezeCharge));
                hornetState.Add(BoolToFloat(hero.cState.focusing));
                hornetState.Add(BoolToFloat(hero.cState.inAcid));
                hornetState.Add(BoolToFloat(hero.cState.touchingNonSlider));
                hornetState.Add(BoolToFloat(hero.cState.wasOnGround));
                hornetState.Add(BoolToFloat(hero.cState.parrying));
                hornetState.Add(BoolToFloat(hero.cState.parryAttack));
                hornetState.Add(BoolToFloat(hero.cState.mantling));
                hornetState.Add(BoolToFloat(hero.cState.mantleRecovery));
                hornetState.Add(BoolToFloat(hero.cState.inUpdraft));
                hornetState.Add(BoolToFloat(hero.cState.isToolThrowing));
                hornetState.Add(BoolToFloat(hero.cState.isInCancelableFSMMove));
                hornetState.Add(BoolToFloat(hero.cState.inWindRegion));
                hornetState.Add(BoolToFloat(hero.cState.isMaggoted));
                hornetState.Add(BoolToFloat(hero.cState.inFrostRegion));
                hornetState.Add(BoolToFloat(hero.cState.isFrosted));
                hornetState.Add(BoolToFloat(hero.cState.isTouchingSlopeLeft));
                hornetState.Add(BoolToFloat(hero.cState.isTouchingSlopeRight));
                hornetState.Add(BoolToFloat(hero.cState.isBinding));
                hornetState.Add(BoolToFloat(hero.cState.needolinPlayingMemory));
                hornetState.Add(BoolToFloat(hero.cState.isScrewDownAttacking));
                hornetState.Add(BoolToFloat(hero.cState.evading));
                hornetState.Add(BoolToFloat(hero.cState.whipLashing));
                hornetState.Add(BoolToFloat(hero.cState.fakeHurt));
                hornetState.Add(BoolToFloat(hero.cState.isInCutsceneMovement));
                hornetState.Add(BoolToFloat(hero.cState.isTriggerEventsPaused));
            }

            // Global Actions
            if (PlayerData.instance != null)
            {
                globalActions.Add(BoolToFloat(PlayerData.instance.hasDash));
                globalActions.Add(BoolToFloat(PlayerData.instance.hasBrolly));
                globalActions.Add(BoolToFloat(PlayerData.instance.hasWalljump));
                globalActions.Add(BoolToFloat(PlayerData.instance.hasDoubleJump));
                globalActions.Add(BoolToFloat(PlayerData.instance.hasQuill));
                globalActions.Add(BoolToFloat(PlayerData.instance.hasChargeSlash));
                globalActions.Add(BoolToFloat(PlayerData.instance.hasSuperJump));
                globalActions.Add(BoolToFloat(PlayerData.instance.hasParry));
                globalActions.Add(BoolToFloat(PlayerData.instance.hasHarpoonDash));
            }


            inputData.Add(0f);

            // var data = [inputData, outputData]

            // Logger.LogInfo($"RIGHT IS: {myInputActions.Right.IsPressed}");
            // Logger.LogInfo($"RSRIGHT IS: {myInputActions.RsRight.IsPressed}");

            return inputData;
        }

        private void Update()
        {
            if (GameManager._instance != null && GameManager._instance.GameState == GameState.PLAYING)
            {
                if (!initialized)
                {
                    initialized = Initialize();
                }

                if (initialized && hero != null && rb != null)
                {

                    if (Input.GetKeyDown(KeyCode.W))
                    {
                        Logger.LogInfo("Action");
                        GameAction.BigJump.Execute();
                    }
                }
            }
        }
    }
}
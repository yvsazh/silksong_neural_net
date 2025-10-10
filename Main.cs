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

        private NeuralNet _nn;
        private bool _isTrainingMode = true; // Додайте перемикач для режимів

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

                    if(myInputActions != null)
                    {
                        // Отримуємо розміри входу та виходу з DataCollector
                        var inputData = DataCollector.GetInputData();
                        var outputData = DataCollector.GetOutputData();

                        // Створюємо екземпляр нейронної мережі
                        _nn = new NeuralNet(inputData.Count, outputData.Count);
                        Logger.LogInfo($"NeuralNet created with {inputData.Count} inputs and {outputData.Count} outputs.");

                        initialized = true;
                        Logger.LogInfo("Agent initialized successfully.");
                        return true;
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

        private void Update()
        {
            if (GameManager._instance != null && GameManager._instance.GameState == GameState.PLAYING)
            {
                if (!initialized)
                {
                    initialized = Initialize();
                }

                if (initialized && hero != null && rb != null && _nn != null)
                {
                    var input = DataCollector.GetInputData().ToArray();
                    var target = DataCollector.GetOutputData().ToArray();

                    var predictedProbabilities = _nn.Predict(input);
                    var predictedActions = _nn.ToActions(predictedProbabilities, 0.5f);

                    if (!_isTrainingMode)
                    {
                        // Bot plays
                    }
                    if (_isTrainingMode)
                    {
                        double error = _nn.TrainOnline(input, target);

                        if (Time.frameCount % 100 == 0)
                        {
                            Logger.LogInfo($"[NeuralNet] Instant training error: {error}");
                            Logger.LogInfo($"[NeuralNet] Prediction: {string.Join(",", predictedActions)} | Real Action: {string.Join(",", target)}");
                        }
                    }

                    if (Input.GetKeyDown(KeyCode.F10))
                    {
                        _isTrainingMode = !_isTrainingMode;
                        Logger.LogInfo($"TRAINING MODE set to: {_isTrainingMode}");
                    }
                    if (Input.GetKeyDown(KeyCode.F11))
                    {
                        _nn.Save("SilksongNN.bin");
                    }
                    if (Input.GetKeyDown(KeyCode.F12))
                    {
                        _nn = NeuralNet.Load("SilksongNN.bin");
                    }
                }
            }
        }
    }
}
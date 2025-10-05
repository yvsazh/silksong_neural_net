using System;
using BepInEx;
using BepInEx.Logging;
using GlobalEnums;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System.IO;
using System.Reflection;
using JetBrains.Annotations;

namespace SilksongNeuralNetwork
{
    [BepInPlugin("com.hackwhiz.nnagent", "Neural Net Agent for Silksong", "1.0.0")]
    public class Agent : BaseUnityPlugin
    {
        public static Agent Instance { get; private set; }

        private HeroController hero;
        private bool initialized = false;

        private InputHandler myInputHandler;
        private HeroActions myInputActions;

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

        private void Initialize()
        {
            try
            {
                hero = GameObject.FindFirstObjectByType<HeroController>();
                if (hero != null)
                {
                    initialized = true;
                    var field = typeof(HeroController).GetField("inputHandler",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    myInputHandler = (InputHandler)field.GetValue(hero);
                    myInputActions = myInputHandler.inputActions;
                    Logger.LogInfo("Agent initialized successfully.");
                }
                else
                {
                    Logger.LogWarning("HeroController not found. Initialization failed.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error during initialization: {ex.Message}");
            }
        }

        public float BoolToFloat(bool value)
        {
            return value ? 1f : 0f;
        }

        public float Normalize(float value, float maxValue)
        {
            if (maxValue == 0f) return 0f;
            return value / maxValue;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), "BeginScene")]
        public static void SceneLoadPostFix()
        {
            // Тепер використовуємо sceneBoundsInstance, який буде ініціалізовано
            if (Instance.sceneBoundsInstance != null)
            {
                Instance.sceneBoundsInstance.UpdateBounds();
                Instance.Logger.LogInfo($"UPDATED BOUNDS. MinX: {Instance.sceneBoundsInstance.minX}, MaxX: {Instance.sceneBoundsInstance.maxX}");
            }
            else
            {
                Instance.Logger.LogInfo("SceneBounds instance is null after scene load.");
            }
            Instance.Logger.LogInfo("LOADED SCENE");
        }

        public float NormalizeWithMinMax(float value, float min, float max)
        {
            if (max - min == 0) return 0;
            return Mathf.Clamp01((value - min) / (max - min));
        }

        private List<float> GetData()
        {
            List<float> inputData = new List<float>();
            // ... (ваш код для збору даних) ...

            // Приклад використання нормалізованих координат
            if (hero != null && PlayerData.instance != null && sceneBoundsInstance != null)
            {
                float normalizedX = NormalizeWithMinMax(hero.transform.position.x, sceneBoundsInstance.minX, sceneBoundsInstance.maxX);
                float normalizedY = NormalizeWithMinMax(hero.transform.position.y, sceneBoundsInstance.minY, sceneBoundsInstance.maxY);
                inputData.Add(normalizedX);
                inputData.Add(normalizedY);
            }

            return inputData;
        }

        private void Update()
        {
            if (GameManager._instance != null && GameManager._instance.GameState == GameState.PLAYING)
            {
                if (!initialized)
                {
                    Initialize();
                }
            }

            if (initialized)
            {
                if (hero)
                {
                    // Тепер можна безпечно звертатися до sceneBoundsInstance
                    if (sceneBoundsInstance != null)
                    {
                        Logger.LogInfo($"Current X: {hero.transform.position.x}, Max X: {sceneBoundsInstance.maxX}");
                        Logger.LogInfo($"Current X: {hero.transform.position.y}, Max X: {sceneBoundsInstance.maxY}");
                    }
                }
            }
        }
    }
}
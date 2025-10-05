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

namespace SilksongNeuralNetwork
{
    [BepInPlugin("com.hackwhiz.nnagent", "Neural Net Agent for Silksong", "1.0.0")]
    public class Agent : BaseUnityPlugin
    {
        private HeroController hero;
        private bool initialized = false;

        private InputHandler myInputHandler;
        private HeroActions myInputActions;

        private void Awake()
        {
            Logger.LogInfo("Plugin loaded and initialized");

            NeuralNet.Test(Logger);

            hero = GameObject.FindFirstObjectByType<HeroController>();
            Harmony.CreateAndPatchAll(typeof(Agent), null);
        }

        private void Initialize()
        {

            // have to init nn here too

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

        // Normalizes a value to a range between 0 and 1
        public float Normalize(float value, float maxValue)
        {
            if (maxValue == 0f) return 0f; // avoid division by zero
            return value / maxValue;
        }

        private List<float> GetData()
        {
            List<float> data = new List<float>();

            // Hornet State

            List<float> hornetState = new List<float>();

            if (PlayerData.instance != null)
            {
                hornetState.Add(Normalize(PlayerData.instance.health, PlayerData.instance.maxHealth));
                hornetState.Add(Normalize(PlayerData.instance.silk, PlayerData.instance.silkMax));
            }


            data.Add(0f);

            // Logger.LogInfo($"ONGROUND: {hero.cState.onGround}"); // TRUE
            Logger.LogInfo($"RIGHT IS: {myInputActions.Right.IsPressed}");
            Logger.LogInfo($"RSRIGHT IS: {myInputActions.RsRight.IsPressed}");

            return data;
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
                // Example: Make the hero jump when on the ground
                if (hero)
                {
                    var data = GetData();
                    // var prediction = NeuralNet(data)
                }
            }

        }
    }
}

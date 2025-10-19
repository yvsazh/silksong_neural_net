using Accord.Controls.Vision;
using BepInEx;
using BepInEx.Logging;
using GlobalEnums;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using InControl;
using JetBrains.Annotations;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

using UnityEngine;

using static SilksongNeuralNetwork.Utils;

namespace SilksongNeuralNetwork
{
    public enum AgentMode
    {
        Disabled,
        Training,
        Inference
    }

    [BepInPlugin("com.hackwhiz.nnagent", "Neural Net Agent for Silksong", "1.0.0")]
    public class Agent : BaseUnityPlugin
    {
        public static Agent Instance { get; private set; }

        public HeroController hero;
        public Rigidbody2D rb;
        public bool initialized = false;

        public InputHandler myInputHandler;
        public HeroActions myInputActions;

        public PlayMakerFSM[] fsms;
        public ListenForCast castAction;

        private NeuralNet _nn;
        public AgentMode _currentMode = AgentMode.Disabled;
        private string _currentModelName = null;

        private SceneBounds sceneBoundsInstance;
        private FunctionLogger _functionLogger;
        private NNInterface _interface;

        // Багатопотоковість
        private Thread _trainingThread;
        private volatile bool _trainingThreadRunning = false;
        private readonly object _modeLock = new object();

        public static string ModelsPath => Path.Combine(Application.persistentDataPath, "models");

        private void Awake()
        {
            Logger.LogInfo("Plugin loaded and initialized");

            Instance = this;

            if (!Directory.Exists(ModelsPath))
            {
                Directory.CreateDirectory(ModelsPath);
            }

            _functionLogger = new FunctionLogger(
                "com.hackwhiz.nnagent.functionlogger",
                Path.Combine(Paths.GameRootPath, "function_logs.txt"),
                Logger
            );

            if (SceneBounds.Instance == null)
            {
                GameObject sceneBoundsObject = new GameObject("SceneBoundsManager");
                sceneBoundsInstance = sceneBoundsObject.AddComponent<SceneBounds>();
                DontDestroyOnLoad(sceneBoundsObject);
            }
            else
            {
                sceneBoundsInstance = SceneBounds.Instance;
            }

            hero = GameObject.FindFirstObjectByType<HeroController>();
            Harmony.CreateAndPatchAll(typeof(Agent), null);

            GameObject interfaceObj = new GameObject("NNInterface");
            _interface = interfaceObj.AddComponent<NNInterface>();
            DontDestroyOnLoad(interfaceObj);

            // Запускаємо фоновий потік для навчання
            StartTrainingThread();
        }

        private void OnDestroy()
        {
            StopTrainingThread();
        }

        private void StartTrainingThread()
        {
            if (_trainingThread != null && _trainingThread.IsAlive)
                return;

            _trainingThreadRunning = true;
            _trainingThread = new Thread(TrainingLoop)
            {
                IsBackground = true,
                Priority = System.Threading.ThreadPriority.BelowNormal
            };
            _trainingThread.Start();
            Logger.LogInfo("Training thread started");
        }

        private void StopTrainingThread()
        {
            _trainingThreadRunning = false;
            if (_trainingThread != null && _trainingThread.IsAlive)
            {
                _trainingThread.Join(1000);
                Logger.LogInfo("Training thread stopped");
            }
        }

        private void TrainingLoop()
        {
            while (_trainingThreadRunning)
            {
                try
                {
                    AgentMode currentMode;
                    lock (_modeLock)
                    {
                        currentMode = _currentMode;
                    }

                    if (currentMode == AgentMode.Training && _nn != null)
                    {
                        // Навчання відбувається тут, в окремому потоці
                        double error = _nn.TrainBatch();

                        // Невелика затримка, щоб не навантажувати CPU на 100%
                        Thread.Sleep(10);
                    }
                    else
                    {
                        // Якщо не тренуємо, чекаємо довше
                        Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error in training thread: {ex.Message}");
                    Thread.Sleep(1000);
                }
            }
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

                    if (myInputActions != null)
                    {
                        var inputData = DataCollector.GetInputData();
                        var outputData = DataCollector.GetOutputData();

                        _nn = new NeuralNet(inputData.Count, outputData.Count);
                        Logger.LogInfo($"NeuralNet created with {inputData.Count} inputs and {outputData.Count} outputs.");
                    }

                    initialized = true;

                    Logger.LogInfo("Agent initialized successfully.");

                    fsms = hero.GetComponents<PlayMakerFSM>();
                    Logger.LogInfo($"Found {fsms.Length} FSMs on hero");

                    castAction = null;
                    foreach (var fsm in fsms)
                    {
                        foreach (var state in fsm.FsmStates)
                        {
                            foreach (var action in state.Actions)
                            {
                                if (action is ListenForCast cast)
                                {
                                    castAction = cast;
                                    Logger.LogInfo($"Found ListenForCast in FSM: {fsm.FsmName}, State: {state.Name}");
                                    break;
                                }
                            }
                            if (castAction != null) break;
                        }
                        if (castAction != null) break;
                    }

                    if (castAction == null)
                    {
                        Logger.LogWarning("ListenForCast не знайдено!");
                    }

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
                    var predictedActions = _nn.ToActions(predictedProbabilities, 0.3f);

                    hero.AddSilk(999, false);

                    AgentMode currentMode;
                    lock (_modeLock)
                    {
                        currentMode = _currentMode;
                    }

                    switch (currentMode)
                    {
                        case AgentMode.Training:
                            HandleTrainingMode(input, target, predictedActions);
                            break;
                        case AgentMode.Inference:
                            HandleInferenceMode(predictedActions);
                            break;
                        case AgentMode.Disabled:
                            break;
                    }

                    HandleHotkeys();
                }
            }
        }

        private void HandleTrainingMode(float[] input, float[] target, bool[] predictedActions)
        {
            bool playerDidAction = target.Any(actionValue => actionValue > 0.5f);

            if (playerDidAction)
            {
                // Тільки збираємо дані, навчання відбувається в іншому потоці
                _nn.CollectExperience(input, target);

                // Логування рідше, щоб не навантажувати
                if (Time.frameCount % 300 == 0)
                {
                    Logger.LogInfo($"[NeuralNet] {_nn.GetStats()}");
                    Logger.LogInfo($"[NeuralNet] Prediction: {string.Join(",", predictedActions)} | Real Action: {string.Join(",", target)}");
                }
            }
        }

        private void HandleInferenceMode(bool[] predictedActions)
        {
            List<int> answers = new List<int>();

            for (int i = 0; i < predictedActions.Length; i++)
            {
                if (predictedActions[i])
                {
                    answers.Add(i);
                }
            }

            if (answers.Count > 0)
            {
                Logger.LogInfo($"Bot actions: {string.Join(" ", answers)}");
            }

            foreach (var answerId in answers)
            {
                GameAction.GetById(answerId + 1).Execute();
            }
        }

        private void HandleHotkeys()
        {
            if (Input.GetKeyDown(KeyCode.W))
            {
                CycleMode();
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                _interface.ShowSaveDialog();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                _interface.ShowLoadDialog();
            }

            if (Input.GetKeyDown(KeyCode.Y) && !string.IsNullOrEmpty(_currentModelName))
            {
                SaveModel(_currentModelName);
                Logger.LogInfo($"Model '{_currentModelName}' quick saved!");
            }

            if (Input.GetKeyDown(KeyCode.L))
            {
                if (DebugTools.Instance != null)
                {
                    DebugTools.Instance.Visible = !DebugTools.Instance.Visible;
                }
            }

            if (Input.GetKey(KeyCode.L))
            {
                for (int i = 0; i < 32; i++)
                {
                    string layerName = LayerMask.LayerToName(i);
                    if (!string.IsNullOrEmpty(layerName))
                        Logger.LogInfo($"Layer {i}: {layerName}");
                }
            }
        }

        private void CycleMode()
        {
            lock (_modeLock)
            {
                _currentMode = (AgentMode)(((int)_currentMode + 1) % 3);
                Logger.LogInfo($"Mode changed to: {_currentMode}");
                _interface.UpdateModeText(_currentMode);
            }
        }

        public void SaveModel(string modelName)
        {
            if (_nn == null)
            {
                Logger.LogWarning("Cannot save: Neural network is not initialized");
                return;
            }

            string modelPath = Path.Combine(ModelsPath, $"{modelName}.bin");
            _nn.Save(modelPath);
            _currentModelName = modelName;
            Logger.LogInfo($"Model saved as '{modelName}'");
        }

        public void LoadModel(string modelName)
        {
            if (_nn == null)
            {
                Logger.LogWarning("Cannot load: Neural network is not initialized");
                return;
            }

            string modelPath = Path.Combine(ModelsPath, $"{modelName}.bin");
            if (File.Exists(modelPath))
            {
                _nn = NeuralNet.Load(modelPath);
                _currentModelName = modelName;
                Logger.LogInfo($"Model '{modelName}' loaded!");
            }
            else
            {
                Logger.LogError($"Model file not found: {modelPath}");
            }
        }

        public AgentMode GetCurrentMode()
        {
            lock (_modeLock)
            {
                return _currentMode;
            }
        }

        public string GetCurrentModelName()
        {
            return _currentModelName ?? "Без назви";
        }
    }
}
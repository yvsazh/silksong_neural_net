using System;
using System.IO;
using Accord.Neuro;
using Accord.Neuro.Learning;
using BepInEx.Logging;
using UnityEngine;

namespace SilksongNeuralNetwork
{
    /// <summary>
    /// Lightweight online (per-frame) behaviour-cloning neural network using Accord.NET
    /// - Multi-label outputs (each output neuron uses sigmoid -> independent probabilities)
    /// - Trains on a single sample each call to TrainOnline(...) using BackPropagationLearning.Run
    /// </summary>
    public class NeuralNet
    {
        private ActivationNetwork _network;
        private BackPropagationLearning _teacher;

        private readonly object _lock = new object();

        public int InputSize { get; private set; }
        public int OutputSize { get; private set; }

        // Hyper-parameters (changeable at runtime)
        public double LearningRate { get; set; } = 0.01;       // typical small LR for online updates
        public double Momentum { get; set; } = 0.9;

        // Construct with dynamic hidden layer sizing if you don't want to pass explicit layers
        public NeuralNet(int inputSize, int outputSize, int[] hiddenLayers = null, double learningRate = 0.01, double momentum = 0.9)
        {
            InputSize = inputSize;
            OutputSize = outputSize;
            LearningRate = learningRate;
            Momentum = momentum;

            if (hiddenLayers == null || hiddenLayers.Length == 0)
            {
                // heuristic: make network sufficiently expressive but not huge
                int h1 = Math.Max(64, Math.Min(512, inputSize * 2));
                int h2 = Math.Max(32, Math.Min(256, inputSize));
                hiddenLayers = new int[] { h1, h2 };
            }

            InitializeNetwork(hiddenLayers);
        }

        private void InitializeNetwork(int[] hiddenLayers)
        {
            // ActivationNetwork expects: (IActivationFunction function, int inputsCount, params int[] neuronsCount)
            // we will construct layers: hidden1, hidden2, ..., output
            int totalLayers = hiddenLayers.Length + 1;
            int[] allLayers = new int[totalLayers];
            for (int i = 0; i < hiddenLayers.Length; i++) allLayers[i] = hiddenLayers[i];
            allLayers[allLayers.Length - 1] = OutputSize; // last layer is output

            // Sigmoid squashes to (0,1) so each output neuron is an independent probability
            _network = new ActivationNetwork(new SigmoidFunction(), InputSize, allLayers);

            // Better initialization speeds up training
            var init = new NguyenWidrow(_network);
            init.Randomize();

            // Create an online teacher (per-sample updates)
            _teacher = new BackPropagationLearning(_network)
            {
                LearningRate = LearningRate,
                Momentum = Momentum
            };

            Debug.Log($"[NeuralNet] Initialized network (in:{InputSize}, out:{OutputSize}, hidden: {string.Join(",", hiddenLayers)})");
        }

        /// <summary>
        /// Run the network forward and return raw output probabilities (range ~0..1)
        /// </summary>
        public float[] Predict(float[] input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Length != InputSize) throw new ArgumentException("Input length mismatch");

            double[] inD = ToDouble(input);
            double[] outD;
            lock (_lock)
            {
                outD = _network.Compute(inD);
            }

            return ToFloat(outD);
        }

        /// <summary>
        /// Train once on a single sample (online / per-frame style). Returns the instantaneous training error.
        /// Uses BackPropagationLearning.Run(...) which performs one update. Keep LearningRate small for stability.
        /// </summary>
        public double TrainOnline(float[] input, float[] target)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (input.Length != InputSize) throw new ArgumentException("Input length mismatch");
            if (target.Length != OutputSize) throw new ArgumentException("Target length mismatch");

            double[] inD = ToDouble(input);
            double[] tgtD = ToDouble(target);
            double error;

            lock (_lock)
            {
                // Run returns the squared error for this sample (depends on the teacher implementation)
                error = _teacher.Run(inD, tgtD);
            }

            return error;
        }

        /// <summary>
        /// Convert network outputs into boolean actions using a threshold (default 0.5).
        /// Multiple outputs can be true simultaneously (multi-label).
        /// </summary>
        public bool[] ToActions(float[] outputs, float threshold = 0.5f)
        {
            bool[] actions = new bool[outputs.Length];
            for (int i = 0; i < outputs.Length; i++) actions[i] = outputs[i] >= threshold;
            return actions;
        }

        /// <summary>
        /// Save network to disk (binary serialization via Accord's Network.Save method)
        /// </summary>
        public void Save(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            lock (_lock)
            {
                _network.Save(path);
            }
            Debug.Log($"[NeuralNet] Saved network to: {path}");
        }

        /// <summary>
        /// Load an ActivationNetwork saved with Network.Save
        /// </summary>
        public static NeuralNet Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException(path);

            // Network.Load is static and will return Network; cast to ActivationNetwork
            var net = Network.Load(path) as ActivationNetwork;
            if (net == null) throw new InvalidDataException("Saved file is not an ActivationNetwork or incompatible version.");

            // Create wrapper
            var wrapper = new NeuralNet(net.InputsCount, net.Layers[net.Layers.Length - 1].Neurons.Length);
            lock (wrapper._lock)
            {
                wrapper._network = net;
                // re-create teacher with the wrapper hyperparams
                wrapper._teacher = new BackPropagationLearning(wrapper._network)
                {
                    LearningRate = wrapper.LearningRate,
                    Momentum = wrapper.Momentum
                };
            }

            Debug.Log($"[NeuralNet] Loaded network from: {path}");
            return wrapper;
        }

        private static double[] ToDouble(float[] arr)
        {
            double[] d = new double[arr.Length];
            for (int i = 0; i < arr.Length; i++) d[i] = arr[i];
            return d;
        }

        private static float[] ToFloat(double[] arr)
        {
            float[] f = new float[arr.Length];
            for (int i = 0; i < arr.Length; i++) f[i] = (float)arr[i];
            return f;
        }
    }

    // Example MonoBehaviour showing per-frame training usage
    public class BehaviourCloningAgent : MonoBehaviour
    {
        private NeuralNet _nn;

        void Start()
        {
            var input = DataCollector.GetInputData();
            var output = DataCollector.GetOutputData();
            _nn = new NeuralNet(input.Count, output.Count);
        }

        void Update()
        {
            // 1) Collect current frame data
            var input = DataCollector.GetInputData().ToArray();
            var target = DataCollector.GetOutputData().ToArray();

            // 2) Predict (you can use probabilities to blend or pick actions via threshold)
            var pred = _nn.Predict(input);
            var actions = _nn.ToActions(pred, 0.5f);

            // 3) (Optional) apply actions to your agent here. Be careful: direct injection into input systems
            // may require additional code; for now we just log a compact representation
            Debug.Log($"[BehaviourCloningAgent] Pred: {string.Join(",", pred)} | Act: {string.Join(",", actions)}");

            // 4) Train online with the (input, target) pair — one learning iteration per frame
            double err = _nn.TrainOnline(input, target);

            // (Optional) monitor training error
            if (Time.frameCount % 60 == 0) // every 60 frames
                Debug.Log($"[BehaviourCloningAgent] Instant error: {err}");
        }
    }
}

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Accord.Neuro;
using Accord.Neuro.Learning;
using BepInEx.Logging;
using UnityEngine;

namespace SilksongNeuralNetwork
{
    /// <summary>
    /// Optimized neural network with experience replay and balanced training
    /// </summary>
    public class NeuralNet
    {
        private ActivationNetwork _network;
        private BackPropagationLearning _teacher;
        private readonly object _lock = new object();

        public int InputSize { get; private set; }
        public int OutputSize { get; private set; }

        // Hyper-parameters
        public double LearningRate { get; set; } = 0.05; // Збільшено для швидшого навчання
        public double Momentum { get; set; } = 0.9;

        // Experience replay buffer
        private Queue<Experience> _replayBuffer = new Queue<Experience>();
        private const int MAX_BUFFER_SIZE = 10000;
        private const int MIN_BUFFER_SIZE = 500; // Мінімум для початку навчання
        private const int BATCH_SIZE = 32;

        // Training control
        private int _frameCounter = 0;
        private const int TRAIN_EVERY_N_FRAMES = 5; // Навчаємось кожні 5 кадрів

        // Statistics
        public int TotalSamplesCollected { get; private set; } = 0; // ЗМІНЕНО: Залишаємо тільки загальну кількість
        public double LastBatchError { get; private set; } = 0;

        // ЗМІНЕНО: Спрощено клас Experience, бо всі дані тепер "активні"
        private class Experience
        {
            public float[] Input;
            public float[] Target;

            public Experience(float[] input, float[] target)
            {
                Input = input;
                Target = target;
            }
        }

        public NeuralNet(int inputSize, int outputSize, int[] hiddenLayers = null, double learningRate = 0.05, double momentum = 0.9)
        {
            InputSize = inputSize;
            OutputSize = outputSize;
            LearningRate = learningRate;
            Momentum = momentum;

            if (hiddenLayers == null || hiddenLayers.Length == 0)
            {
                // Глибша мережа для кращого навчання
                int h1 = Math.Max(128, Math.Min(512, inputSize * 2));
                int h2 = Math.Max(64, Math.Min(256, inputSize));
                int h3 = Math.Max(32, Math.Min(128, outputSize * 4));
                hiddenLayers = new int[] { h1, h2, h3 };
            }

            InitializeNetwork(hiddenLayers);
        }

        private void InitializeNetwork(int[] hiddenLayers)
        {
            int totalLayers = hiddenLayers.Length + 1;
            int[] allLayers = new int[totalLayers];
            for (int i = 0; i < hiddenLayers.Length; i++) allLayers[i] = hiddenLayers[i];
            allLayers[allLayers.Length - 1] = OutputSize;

            _network = new ActivationNetwork(new SigmoidFunction(), InputSize, allLayers);

            var init = new NguyenWidrow(_network);
            init.Randomize();



            _teacher = new BackPropagationLearning(_network)
            {
                LearningRate = LearningRate,
                Momentum = Momentum
            };

            Debug.Log($"[NeuralNet] Initialized network (in:{InputSize}, out:{OutputSize}, hidden: {string.Join(",", hiddenLayers)})");
        }

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
        /// Collect experience into replay buffer instead of immediate training
        /// </summary>
        public void CollectExperience(float[] input, float[] target)
        {
            if (input == null || target == null) return;
            if (input.Length != InputSize || target.Length != OutputSize) return;

            var exp = new Experience(input, target);

            // ЗМІНЕНО: Спрощена статистика
            TotalSamplesCollected++;

            lock (_lock)
            {
                _replayBuffer.Enqueue(exp);

                // Обмежуємо розмір буфера
                while (_replayBuffer.Count > MAX_BUFFER_SIZE)
                {
                    _replayBuffer.Dequeue();
                }
            }
        }

        /// <summary>
        /// Train on a batch from replay buffer.
        /// Call this periodically (not every frame)
        /// </summary>
        public double TrainBatch()
        {
            _frameCounter++;

            // Не навчаємось кожен кадр
            if (_frameCounter % TRAIN_EVERY_N_FRAMES != 0)
                return LastBatchError;

            // ЗМІНЕНО: Повністю перероблена і спрощена логіка
            lock (_lock)
            {
                // Навчаємось, тільки якщо є достатньо даних для одного батчу
                if (_replayBuffer.Count < BATCH_SIZE)
                    return 0;

                var experiences = _replayBuffer.ToArray();
                var batch = new List<Experience>();
                var random = new System.Random();

                // Випадково вибираємо дані для батчу. Всі дані в буфері є "активними".
                for (int i = 0; i < BATCH_SIZE; i++)
                {
                    batch.Add(experiences[random.Next(experiences.Length)]);
                }

                // Навчаємось на батчі
                double totalError = 0;
                foreach (var exp in batch)
                {
                    double[] inD = ToDouble(exp.Input);
                    double[] tgtD = ToDouble(exp.Target);
                    totalError += _teacher.Run(inD, tgtD);
                }

                LastBatchError = totalError / batch.Count;
                return LastBatchError;
            }
        }

        /// <summary>
        /// Legacy method for immediate training (not recommended)
        /// </summary>
        public double Train(float[] input, float[] target)
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
                error = _teacher.Run(inD, tgtD);
            }

            return error;
        }

        public bool[] ToActions(float[] outputs, float threshold = 0.5f)
        {
            bool[] actions = new bool[outputs.Length];
            for (int i = 0; i < outputs.Length; i++) actions[i] = outputs[i] >= threshold;
            return actions;
        }

        public void Save(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            lock (_lock)
            {
                _network.Save(path);
            }
            Debug.Log($"[NeuralNet] Saved network to: {path}");
        }

        public static NeuralNet Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException(path);

            var net = Network.Load(path) as ActivationNetwork;
            if (net == null) throw new InvalidDataException("Saved file is not an ActivationNetwork or incompatible version.");

            var wrapper = new NeuralNet(net.InputsCount, net.Layers[net.Layers.Length - 1].Neurons.Length);
            lock (wrapper._lock)
            {
                wrapper._network = net;
                wrapper._teacher = new BackPropagationLearning(wrapper._network)
                {
                    LearningRate = wrapper.LearningRate,
                    Momentum = wrapper.Momentum
                };
            }

            Debug.Log($"[NeuralNet] Loaded network from: {path}");
            return wrapper;
        }

        public void ClearBuffer()
        {
            lock (_lock)
            {
                _replayBuffer.Clear();
            }
        }

        // ЗМІНЕНО: Спрощено вивід статистики
        public string GetStats()
        {
            return $"Buffer: {_replayBuffer.Count}/{MAX_BUFFER_SIZE} | " +
                   $"Total Samples: {TotalSamplesCollected} | " +
                   $"Error: {LastBatchError:F4}";
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
}
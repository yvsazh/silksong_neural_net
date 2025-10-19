using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Threading;

namespace SilksongNeuralNetwork
{
    public class NeuralNet
    {
        private Layer[] _layers;
        private readonly object _trainingLock = new object();
        private readonly object _predictionLock = new object();

        private double[] _inputCache;
        private double[] _outputCache;

        public int InputSize { get; private set; }
        public int OutputSize { get; private set; }

        public double LearningRate { get; set; } = 0.002; // Підвищено для швидшого навчання
        public double Momentum { get; set; } = 0.9;
        public double GradientClipValue { get; set; } = 1.0;
        public double L2Regularization { get; set; } = 0.0001;

        public OptimizerType Optimizer { get; set; } = OptimizerType.Adam;
        public double Beta1 { get; set; } = 0.9;
        public double Beta2 { get; set; } = 0.999;
        public double Epsilon { get; set; } = 1e-8;
        private int _timestep = 0;

        private Experience[] _replayBuffer;
        private int _bufferHead = 0;
        private int _bufferCount = 0;
        private const int MAX_BUFFER_SIZE = 10000;
        private const int BATCH_SIZE = 32;
        private const int MIN_BUFFER_SIZE = 128;

        private int _trainingCounter = 0;
        private const int TRAIN_EVERY_N_CALLS = 50;

        private int _totalSamplesCollected = 0;
        public int TotalSamplesCollected => _totalSamplesCollected;

        private double _lastBatchError = 0;
        public double LastBatchError => _lastBatchError;

        private double _runningAvgError = 0;
        public double RunningAvgError => _runningAvgError;

        private const double ERROR_SMOOTHING = 0.99;

        private int[] _actionCounts;
        private int _statsCounter = 0;

        // КРИТИЧНО: Ваги для loss function - помірні, щоб не переборщити
        private static readonly double[] ACTION_LOSS_WEIGHTS = {
            0.1,   // 0: Right - мінімальна вага
            0.1,   // 1: Left - мінімальна вага
            5.0,   // 2: Jump - висока вага
            5.0,   // 3: Dash - висока вага
            7.0,   // 4: Attack - середня вага
            7.0,   // 5: DownAttack - висока вага
            7.0,   // 6: UpAttack - висока вага
            10.0,   // 7: Cast - висока вага
            12.0,   // 8: MainAbility - висока вага
            12.0,   // 9: FirstTool - висока вага
            12.0,   // 10: SecondTool - висока вага
            10.0    // 11: HarpoonDash - висока вага
        };

        private struct Experience
        {
            public double[] Input;
            public double[] Target;
            public float Priority;
        }

        private class Layer
        {
            public double[,] Weights;
            public double[] Biases;
            public double[] Output;
            public double[] Input;
            public double[] Delta;
            public double[] PreActivation;

            public double[,] WeightVelocity;
            public double[] BiasVelocity;

            public double[,] WeightM;
            public double[,] WeightV;
            public double[] BiasM;
            public double[] BiasV;

            public ActivationType Activation;
            public int InputSize => Weights.GetLength(0);
            public int OutputSize => Weights.GetLength(1);

            public Layer(int inputSize, int outputSize, ActivationType activation)
            {
                Weights = new double[inputSize, outputSize];
                Biases = new double[outputSize];
                Output = new double[outputSize];
                Input = new double[inputSize];
                Delta = new double[outputSize];
                PreActivation = new double[outputSize];

                WeightVelocity = new double[inputSize, outputSize];
                BiasVelocity = new double[outputSize];

                WeightM = new double[inputSize, outputSize];
                WeightV = new double[inputSize, outputSize];
                BiasM = new double[outputSize];
                BiasV = new double[outputSize];

                Activation = activation;
                InitializeWeights(inputSize, outputSize);
            }

            private void InitializeWeights(int inputSize, int outputSize)
            {
                var random = new System.Random(Guid.NewGuid().GetHashCode());

                double scale = Activation == ActivationType.ReLU || Activation == ActivationType.LeakyReLU
                    ? Math.Sqrt(2.0 / inputSize)
                    : Math.Sqrt(1.0 / inputSize);

                for (int i = 0; i < inputSize; i++)
                {
                    for (int j = 0; j < outputSize; j++)
                    {
                        Weights[i, j] = (random.NextDouble() * 2 - 1) * scale;
                    }
                }

                double biasInit = Activation == ActivationType.ReLU ? 0.01 : 0.0;
                for (int j = 0; j < outputSize; j++)
                {
                    Biases[j] = biasInit;
                }
            }

            public void Forward(double[] input, double[] output)
            {
                Array.Copy(input, Input, input.Length);

                for (int j = 0; j < OutputSize; j++)
                {
                    double sum = Biases[j];
                    for (int i = 0; i < InputSize; i++)
                    {
                        sum += input[i] * Weights[i, j];
                    }
                    PreActivation[j] = sum;
                    output[j] = ApplyActivation(sum);
                }

                Array.Copy(output, Output, output.Length);
            }

            private double ApplyActivation(double x)
            {
                switch (Activation)
                {
                    case ActivationType.ReLU:
                        return Math.Max(0, x);

                    case ActivationType.LeakyReLU:
                        return x > 0 ? x : 0.01 * x;

                    case ActivationType.Tanh:
                        return Math.Tanh(x);

                    case ActivationType.Sigmoid:
                        return x >= 0
                            ? 1.0 / (1.0 + Math.Exp(-x))
                            : Math.Exp(x) / (1.0 + Math.Exp(x));

                    default:
                        return x;
                }
            }

            public double ActivationDerivative(int index)
            {
                double output = Output[index];

                switch (Activation)
                {
                    case ActivationType.ReLU:
                        return output > 0 ? 1.0 : 0.0;

                    case ActivationType.LeakyReLU:
                        return output > 0 ? 1.0 : 0.01;

                    case ActivationType.Tanh:
                        return 1.0 - output * output;

                    case ActivationType.Sigmoid:
                        return output * (1.0 - output);

                    default:
                        return 1.0;
                }
            }
        }

        private enum ActivationType
        {
            ReLU,
            LeakyReLU,
            Sigmoid,
            Tanh
        }

        public enum OptimizerType
        {
            SGD,
            Adam
        }

        public NeuralNet(int inputSize, int outputSize, int[] hiddenLayers = null,
                         double learningRate = 0.002, double momentum = 0.9)
        {
            if (inputSize <= 0 || outputSize <= 0)
                throw new ArgumentException("Input/Output size must be positive");

            InputSize = inputSize;
            OutputSize = outputSize;
            LearningRate = learningRate;
            Momentum = momentum;

            if (hiddenLayers == null || hiddenLayers.Length == 0)
            {
                // Більша мережа для кращого навчання складних паттернів
                // int h1 = Math.Max(128, Math.Min(512, inputSize * 3));
                // int h2 = Math.Max(64, Math.Min(256, inputSize * 2));
                // int h3 = Math.Max(32, Math.Min(128, inputSize));
                hiddenLayers = new int[] { 128, 64 };
            }

            InitializeNetwork(hiddenLayers);
            InitializeBuffers();

            _actionCounts = new int[outputSize];
        }

        private void InitializeNetwork(int[] hiddenLayers)
        {
            var layersList = new List<Layer>();

            int previousSize = InputSize;
            foreach (int size in hiddenLayers)
            {
                layersList.Add(new Layer(previousSize, size, ActivationType.LeakyReLU));
                previousSize = size;
            }

            layersList.Add(new Layer(previousSize, OutputSize, ActivationType.Sigmoid));

            _layers = layersList.ToArray();

            Debug.Log($"[NeuralNet] Ініціалізовано мережу: in={InputSize}, out={OutputSize}, " +
                     $"hidden=[{string.Join(",", hiddenLayers)}]");
            Debug.Log($"[NeuralNet] Параметрів: {CountParameters()}");
        }

        private void InitializeBuffers()
        {
            _replayBuffer = new Experience[MAX_BUFFER_SIZE];
            _inputCache = new double[InputSize];
            _outputCache = new double[OutputSize];
        }

        private int CountParameters()
        {
            int count = 0;
            foreach (var layer in _layers)
            {
                count += layer.Weights.Length + layer.Biases.Length;
            }
            return count;
        }

        public float[] Predict(float[] input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Length != InputSize)
                throw new ArgumentException($"Input size mismatch: expected {InputSize}, got {input.Length}");

            lock (_predictionLock)
            {
                for (int i = 0; i < InputSize; i++)
                    _inputCache[i] = input[i];

                double[] current = _inputCache;
                double[] temp = new double[_layers[0].OutputSize];

                for (int l = 0; l < _layers.Length; l++)
                {
                    var layer = _layers[l];
                    var output = l == _layers.Length - 1 ? _outputCache :
                               (l == 0 ? temp : new double[layer.OutputSize]);

                    layer.Forward(current, output);
                    current = output;
                }

                float[] result = new float[OutputSize];
                for (int i = 0; i < OutputSize; i++)
                    result[i] = (float)_outputCache[i];

                return result;
            }
        }

        public void CollectExperience(float[] input, float[] target)
        {
            if (input == null || target == null) return;
            if (input.Length != InputSize || target.Length != OutputSize) return;

            // Фільтрація: збираємо тільки цікаві семпли
            bool hasImportantAction = false;
            for (int i = 2; i < target.Length; i++) // Пропускаємо Right/Left
            {
                if (target[i] > 0.5f)
                {
                    hasImportantAction = true;
                    break;
                }
            }

            // Збираємо рухи тільки з 20% ймовірністю (було 5% - занадто мало)
            bool collectMovement = UnityEngine.Random.value < 0.2f;
            bool hasMovement = target[0] > 0.5f || target[1] > 0.5f;

            // Не збираємо нудні семпли
            if (!hasImportantAction && hasMovement && !collectMovement)
                return;

            float priority = CalculatePriority(target);

            var exp = new Experience
            {
                Input = new double[InputSize],
                Target = new double[OutputSize],
                Priority = priority
            };

            for (int i = 0; i < InputSize; i++)
                exp.Input[i] = input[i];
            for (int i = 0; i < OutputSize; i++)
                exp.Target[i] = target[i];

            lock (_trainingLock)
            {
                _replayBuffer[_bufferHead] = exp;
                _bufferHead = (_bufferHead + 1) % MAX_BUFFER_SIZE;
                if (_bufferCount < MAX_BUFFER_SIZE)
                    _bufferCount++;

                for (int i = 0; i < OutputSize; i++)
                {
                    if (target[i] > 0.5f)
                        _actionCounts[i]++;
                }
            }

            Interlocked.Increment(ref _totalSamplesCollected);
        }

        private float CalculatePriority(float[] target)
        {
            float priority = 1.0f;

            if (target[0] > 0.5f || target[1] > 0.5f)
                priority *= 0.3f; // Помірний пріоритет для рухів

            if (target[2] > 0.5f)
                priority *= 10.0f; // Jump

            if (target[3] > 0.5f)
                priority *= 8.0f; // Dash

            if (target[4] > 0.5f)
                priority *= 6.0f; // Attack

            if (target[5] > 0.5f)
                priority *= 8.0f; // DownAttack

            if (target[6] > 0.5f)
                priority *= 8.0f; // UpAttack

            if (target[7] > 0.5f)
                priority *= 10.0f; // Cast

            if (target[8] > 0.5f)
                priority *= 10.0f; // MainAbility

            if (target[9] > 0.5f)
                priority *= 8.0f; // FirstTool

            if (target[10] > 0.5f)
                priority *= 8.0f; // SecondTool

            if (target[11] > 0.5f)
                priority *= 10.0f; // HarpoonDash

            return priority;
        }

        public double TrainBatch()
        {
            _trainingCounter++;
            _statsCounter++;

            if (_statsCounter >= 500)
            {
                LogActionStatistics();
                _statsCounter = 0;
            }

            if (_trainingCounter % TRAIN_EVERY_N_CALLS != 0)
                return _lastBatchError;

            lock (_trainingLock)
            {
                if (_bufferCount < MIN_BUFFER_SIZE)
                    return 0;

                double totalError = 0;
                var random = new System.Random();
                var usedIndices = new HashSet<int>();

                double totalPriority = 0;
                for (int i = 0; i < _bufferCount; i++)
                {
                    totalPriority += _replayBuffer[i].Priority;
                }

                int actualBatchSize = Math.Min(BATCH_SIZE, _bufferCount);

                for (int i = 0; i < actualBatchSize; i++)
                {
                    int idx = SampleByPriority(random, totalPriority);

                    int attempts = 0;
                    while (usedIndices.Contains(idx) && attempts < 100)
                    {
                        idx = SampleByPriority(random, totalPriority);
                        attempts++;
                    }

                    usedIndices.Add(idx);
                    var exp = _replayBuffer[idx];
                    totalError += TrainSingle(exp.Input, exp.Target);
                }

                _lastBatchError = totalError / actualBatchSize;
                _runningAvgError = _runningAvgError * ERROR_SMOOTHING + _lastBatchError * (1 - ERROR_SMOOTHING);

                return _lastBatchError;
            }
        }

        private int SampleByPriority(System.Random random, double totalPriority)
        {
            double randomValue = random.NextDouble() * totalPriority;
            double cumulative = 0;

            for (int i = 0; i < _bufferCount; i++)
            {
                cumulative += _replayBuffer[i].Priority;
                if (randomValue <= cumulative)
                    return i;
            }

            return _bufferCount - 1;
        }

        private void LogActionStatistics()
        {
            string[] actionNames = {
                "Right", "Left", "Jump", "Dash", "Attack", "DownAttack",
                "UpAttack", "Cast", "MainAbility", "FirstTool", "SecondTool", "HarpoonDash"
            };

            string stats = "[NeuralNet] Action Distribution (in buffer):\n";
            int total = _actionCounts.Sum();
            for (int i = 0; i < Math.Min(_actionCounts.Length, actionNames.Length); i++)
            {
                float percentage = total > 0 ? (_actionCounts[i] * 100.0f / total) : 0;
                stats += $"  {actionNames[i]}: {_actionCounts[i]} ({percentage:F1}%)\n";
            }
            Debug.Log(stats);
        }

        private double TrainSingle(double[] input, double[] target)
        {
            double[] current = input;
            double[][] layerOutputs = new double[_layers.Length][];

            for (int l = 0; l < _layers.Length; l++)
            {
                layerOutputs[l] = new double[_layers[l].OutputSize];
                _layers[l].Forward(current, layerOutputs[l]);
                current = layerOutputs[l];
            }

            // BINARY CROSS-ENTROPY LOSS з вагами
            double weightedError = 0;
            double totalWeight = 0;

            for (int i = 0; i < OutputSize; i++)
            {
                double predicted = Math.Max(1e-7, Math.Min(1 - 1e-7, current[i])); // Clip для стабільності
                double actual = target[i];

                // Binary Cross-Entropy: -[y*log(p) + (1-y)*log(1-p)]
                double bce = -(actual * Math.Log(predicted) + (1 - actual) * Math.Log(1 - predicted));

                // Застосовуємо вагу
                double weight = ACTION_LOSS_WEIGHTS[i];
                weightedError += bce * weight;
                totalWeight += weight;
            }

            double error = weightedError / totalWeight;

            BackpropagateWeighted(target);
            UpdateWeights();

            return error;
        }

        private void BackpropagateWeighted(double[] target)
        {
            var outputLayer = _layers[_layers.Length - 1];

            for (int i = 0; i < outputLayer.OutputSize; i++)
            {
                double output = Math.Max(1e-7, Math.Min(1 - 1e-7, outputLayer.Output[i]));
                double actual = target[i];

                // Градієнт для Binary Cross-Entropy: (predicted - actual) / [predicted * (1 - predicted)]
                // Але для sigmoid це спрощується до: (predicted - actual)
                double error = output - actual;

                // Застосовуємо вагу + derivative
                double weight = ACTION_LOSS_WEIGHTS[i];
                outputLayer.Delta[i] = error * weight * outputLayer.ActivationDerivative(i);
            }

            for (int l = _layers.Length - 2; l >= 0; l--)
            {
                var currentLayer = _layers[l];
                var nextLayer = _layers[l + 1];

                for (int i = 0; i < currentLayer.OutputSize; i++)
                {
                    double sum = 0;
                    for (int j = 0; j < nextLayer.OutputSize; j++)
                    {
                        sum += nextLayer.Delta[j] * nextLayer.Weights[i, j];
                    }
                    currentLayer.Delta[i] = sum * currentLayer.ActivationDerivative(i);
                }
            }
        }

        private void UpdateWeights()
        {
            _timestep++;

            if (Optimizer == OptimizerType.Adam)
                UpdateWeightsAdam();
            else
                UpdateWeightsSGD();
        }

        private void UpdateWeightsSGD()
        {
            foreach (var layer in _layers)
            {
                for (int i = 0; i < layer.InputSize; i++)
                {
                    for (int j = 0; j < layer.OutputSize; j++)
                    {
                        double gradient = layer.Input[i] * layer.Delta[j];
                        gradient += L2Regularization * layer.Weights[i, j];
                        gradient = Math.Max(-GradientClipValue, Math.Min(GradientClipValue, gradient));

                        double velocity = Momentum * layer.WeightVelocity[i, j] - LearningRate * gradient;
                        layer.WeightVelocity[i, j] = velocity;
                        layer.Weights[i, j] += velocity;
                    }
                }

                for (int j = 0; j < layer.OutputSize; j++)
                {
                    double gradient = layer.Delta[j];
                    gradient = Math.Max(-GradientClipValue, Math.Min(GradientClipValue, gradient));

                    double velocity = Momentum * layer.BiasVelocity[j] - LearningRate * gradient;
                    layer.BiasVelocity[j] = velocity;
                    layer.Biases[j] += velocity;
                }
            }
        }

        private void UpdateWeightsAdam()
        {
            double beta1_t = Math.Pow(Beta1, _timestep);
            double beta2_t = Math.Pow(Beta2, _timestep);
            double lr_t = LearningRate * Math.Sqrt(1 - beta2_t) / (1 - beta1_t);

            foreach (var layer in _layers)
            {
                for (int i = 0; i < layer.InputSize; i++)
                {
                    for (int j = 0; j < layer.OutputSize; j++)
                    {
                        double gradient = layer.Input[i] * layer.Delta[j];
                        gradient += L2Regularization * layer.Weights[i, j];
                        gradient = Math.Max(-GradientClipValue, Math.Min(GradientClipValue, gradient));

                        layer.WeightM[i, j] = Beta1 * layer.WeightM[i, j] + (1 - Beta1) * gradient;
                        layer.WeightV[i, j] = Beta2 * layer.WeightV[i, j] + (1 - Beta2) * gradient * gradient;

                        layer.Weights[i, j] -= lr_t * layer.WeightM[i, j] / (Math.Sqrt(layer.WeightV[i, j]) + Epsilon);
                    }
                }

                for (int j = 0; j < layer.OutputSize; j++)
                {
                    double gradient = layer.Delta[j];
                    gradient = Math.Max(-GradientClipValue, Math.Min(GradientClipValue, gradient));

                    layer.BiasM[j] = Beta1 * layer.BiasM[j] + (1 - Beta1) * gradient;
                    layer.BiasV[j] = Beta2 * layer.BiasV[j] + (1 - Beta2) * gradient * gradient;

                    layer.Biases[j] -= lr_t * layer.BiasM[j] / (Math.Sqrt(layer.BiasV[j]) + Epsilon);
                }
            }
        }

        public bool[] ToActions(float[] outputs, float threshold = 0.5f)
        {
            bool[] actions = new bool[outputs.Length];
            for (int i = 0; i < outputs.Length; i++)
                actions[i] = outputs[i] >= threshold;
            return actions;
        }

        public void Save(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            lock (_trainingLock)
            {
                using (var writer = new BinaryWriter(File.Open(path, FileMode.Create)))
                {
                    writer.Write(InputSize);
                    writer.Write(OutputSize);
                    writer.Write(_layers.Length);

                    foreach (var layer in _layers)
                    {
                        writer.Write(layer.InputSize);
                        writer.Write(layer.OutputSize);
                        writer.Write((int)layer.Activation);

                        for (int i = 0; i < layer.InputSize; i++)
                            for (int j = 0; j < layer.OutputSize; j++)
                                writer.Write(layer.Weights[i, j]);

                        for (int j = 0; j < layer.OutputSize; j++)
                            writer.Write(layer.Biases[j]);
                    }
                }
            }

            Debug.Log($"[NeuralNet] Збережено в: {path}");
        }

        public static NeuralNet Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException($"Файл не знайдено: {path}");

            using (var reader = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                int inputSize = reader.ReadInt32();
                int outputSize = reader.ReadInt32();
                int layerCount = reader.ReadInt32();

                var hiddenSizes = new List<int>();
                long startPos = reader.BaseStream.Position;

                for (int l = 0; l < layerCount - 1; l++)
                {
                    reader.ReadInt32();
                    int outSize = reader.ReadInt32();
                    int activation = reader.ReadInt32();
                    hiddenSizes.Add(outSize);

                    int inSize = l == 0 ? inputSize : hiddenSizes[l - 1];
                    reader.BaseStream.Position += (inSize * outSize + outSize) * sizeof(double);
                }

                var net = new NeuralNet(inputSize, outputSize, hiddenSizes.ToArray());

                reader.BaseStream.Position = startPos;

                lock (net._trainingLock)
                {
                    foreach (var layer in net._layers)
                    {
                        reader.ReadInt32();
                        reader.ReadInt32();
                        reader.ReadInt32();

                        for (int i = 0; i < layer.InputSize; i++)
                            for (int j = 0; j < layer.OutputSize; j++)
                                layer.Weights[i, j] = reader.ReadDouble();

                        for (int j = 0; j < layer.OutputSize; j++)
                            layer.Biases[j] = reader.ReadDouble();
                    }
                }

                Debug.Log($"[NeuralNet] Завантажено з: {path}");
                return net;
            }
        }

        public void ClearBuffer()
        {
            lock (_trainingLock)
            {
                _bufferHead = 0;
                _bufferCount = 0;
                Array.Clear(_actionCounts, 0, _actionCounts.Length);
            }
        }

        public string GetStats()
        {
            return $"Buffer: {_bufferCount}/{MAX_BUFFER_SIZE} | " +
                   $"Samples: {_totalSamplesCollected} | " +
                   $"Error: {_lastBatchError:F5} | " +
                   $"AvgError: {_runningAvgError:F5}";
        }
    }
}
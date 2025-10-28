using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace SilksongNeuralNetwork
{
    public class NeuralNet : IDisposable
    {
        private Layer[] _layers;
        private readonly object _trainingLock = new object();
        private readonly object _bufferLock = new object();

        [ThreadStatic] private static double[] _threadInputCache;
        [ThreadStatic] private static double[][] _threadLayerCache;

        public int InputSize { get; private set; }
        public int OutputSize { get; private set; }

        public double LearningRate { get; set; } = 0.001;
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
        private const int BATCH_SIZE = 64;
        private const int MIN_BUFFER_SIZE = 128;

        private Task _trainingTask;
        private CancellationTokenSource _trainingCts;
        private readonly SemaphoreSlim _trainingSignal = new SemaphoreSlim(0);
        private volatile bool _isTraining = false;
        private const int TRAINING_INTERVAL_MS = 5;

        private int _totalSamplesCollected = 0;
        public int TotalSamplesCollected => _totalSamplesCollected;

        private double _lastBatchError = 0;
        public double LastBatchError => _lastBatchError;

        private double _runningAvgError = 0;
        public double RunningAvgError => _runningAvgError;

        private const double ERROR_SMOOTHING = 0.99;

        private int[] _actionCounts;
        private int _statsCounter = 0;

        private static readonly double[] ACTION_LOSS_WEIGHTS = {
            0.8, 0.8, 5.0, 5.0, 8.0, 8.0, 8.0, 12.0, 12.0, 12.0, 12.0, 10.0
        };

        private const int FILE_FORMAT_VERSION = 2;

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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ForwardOptimized(double[] input, double[] output)
            {
                int outSize = OutputSize;
                int inSize = InputSize;

                for (int j = 0; j < outSize; j++)
                {
                    double sum = Biases[j];

                    int i = 0;
                    int unrollCount = inSize - (inSize % 4);

                    for (; i < unrollCount; i += 4)
                    {
                        sum += input[i] * Weights[i, j];
                        sum += input[i + 1] * Weights[i + 1, j];
                        sum += input[i + 2] * Weights[i + 2, j];
                        sum += input[i + 3] * Weights[i + 3, j];
                    }

                    for (; i < inSize; i++)
                    {
                        sum += input[i] * Weights[i, j];
                    }

                    output[j] = ApplyActivation(sum);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private double ApplyActivation(double x)
            {
                switch (Activation)
                {
                    case ActivationType.ReLU:
                        return x > 0 ? x : 0;
                    case ActivationType.LeakyReLU:
                        return x > 0 ? x : 0.01 * x;
                    case ActivationType.Tanh:
                        return Math.Tanh(x);
                    case ActivationType.Sigmoid:
                        return x >= 0 ? 1.0 / (1.0 + Math.Exp(-x)) : Math.Exp(x) / (1.0 + Math.Exp(x));
                    default:
                        return x;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public double ActivationDerivative(double output)
            {
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
                hiddenLayers = new int[] { 128, 64 };
            }

            InitializeNetwork(hiddenLayers);
            InitializeBuffers();

            _actionCounts = new int[outputSize];

            StartAsyncTraining();

            Debug.Log($"[NeuralNet] Ініціалізовано мережу: in={InputSize}, out={OutputSize}, " +
                     $"hidden=[{string.Join(",", hiddenLayers)}]");
            Debug.Log($"[NeuralNet] Параметрів: {CountParameters()}");
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
        }

        private void InitializeBuffers()
        {
            _replayBuffer = new Experience[MAX_BUFFER_SIZE];
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

        private void StartAsyncTraining()
        {
            _trainingCts = new CancellationTokenSource();
            _isTraining = true;

            _trainingTask = Task.Run(async () =>
            {
                Debug.Log("[NeuralNet] Асинхронне навчання запущено");

                while (!_trainingCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await _trainingSignal.WaitAsync(TRAINING_INTERVAL_MS, _trainingCts.Token);

                        if (_bufferCount >= MIN_BUFFER_SIZE)
                        {
                            TrainBatchAsync();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[NeuralNet] Помилка навчання: {ex.Message}\n{ex.StackTrace}");
                    }
                }

                Debug.Log("[NeuralNet] Асинхронне навчання зупинено");
            }, _trainingCts.Token);
        }

        public void StopAsyncTraining()
        {
            if (_isTraining)
            {
                _isTraining = false;
                _trainingCts?.Cancel();
                _trainingTask?.Wait(1000);
                Debug.Log("[NeuralNet] Навчання зупинено");
            }
        }

        public float[] Predict(float[] input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Length != InputSize)
                throw new ArgumentException($"Input size mismatch: expected {InputSize}, got {input.Length}");

            if (_threadInputCache == null || _threadInputCache.Length != InputSize)
            {
                _threadInputCache = new double[InputSize];
            }

            if (_threadLayerCache == null || _threadLayerCache.Length != _layers.Length)
            {
                _threadLayerCache = new double[_layers.Length][];
                for (int i = 0; i < _layers.Length; i++)
                {
                    if (_layers[i] != null)
                        _threadLayerCache[i] = new double[_layers[i].OutputSize];
                }
            }

            for (int i = 0; i < InputSize; i++)
                _threadInputCache[i] = input[i];

            double[] current = _threadInputCache;

            if (_layers == null)
                throw new InvalidOperationException("Neural network layers are not initialized");

            for (int l = 0; l < _layers.Length; l++)
            {
                if (_layers[l] == null)
                    throw new InvalidOperationException($"Layer {l} is null");

                if (_threadLayerCache[l] == null || _threadLayerCache[l].Length != _layers[l].OutputSize)
                    _threadLayerCache[l] = new double[_layers[l].OutputSize];

                _layers[l].ForwardOptimized(current, _threadLayerCache[l]);
                current = _threadLayerCache[l];
            }

            float[] result = new float[OutputSize];
            for (int i = 0; i < OutputSize; i++)
                result[i] = (float)current[i];

            return result;
        }

        public void CollectExperience(float[] input, float[] target)
        {
            if (input == null || target == null) return;
            if (input.Length != InputSize || target.Length != OutputSize) return;

            bool hasImportantAction = false;
            for (int i = 2; i < target.Length; i++)
            {
                if (target[i] > 0.5f)
                {
                    hasImportantAction = true;
                    break;
                }
            }

            bool collectMovement = UnityEngine.Random.value < 0.2f;
            bool hasMovement = target[0] > 0.5f || target[1] > 0.5f;

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

            lock (_bufferLock)
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

            if (_trainingSignal.CurrentCount == 0)
            {
                try
                {
                    _trainingSignal.Release();
                }
                catch (SemaphoreFullException)
                {
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float CalculatePriority(float[] target)
        {
            float priority = 1.0f;

            if (target[0] > 0.5f || target[1] > 0.5f)
                priority *= 0.3f;

            for (int i = 2; i < Math.Min(target.Length, ACTION_LOSS_WEIGHTS.Length); i++)
            {
                if (target[i] > 0.5f)
                    priority *= (float)ACTION_LOSS_WEIGHTS[i];
            }

            return priority;
        }

        public double TrainBatch()
        {
            return TrainBatchInternal();
        }

        private double TrainBatchInternal()
        {
            _statsCounter++;
            if (_statsCounter >= 500)
            {
                LogActionStatistics();
                _statsCounter = 0;
            }

            Experience[] batch;
            int batchCount;
            double totalPriority;

            lock (_bufferLock)
            {
                if (_bufferCount < MIN_BUFFER_SIZE)
                    return _lastBatchError;

                batchCount = Math.Min(BATCH_SIZE, _bufferCount);
                batch = new Experience[batchCount];

                totalPriority = 0;
                for (int i = 0; i < _bufferCount; i++)
                {
                    totalPriority += _replayBuffer[i].Priority;
                }

                var random = new System.Random();
                var usedIndices = new HashSet<int>();

                for (int i = 0; i < batchCount; i++)
                {
                    int idx = SampleByPriority(random, totalPriority, _bufferCount);

                    int attempts = 0;
                    while (usedIndices.Contains(idx) && attempts < 100)
                    {
                        idx = SampleByPriority(random, totalPriority, _bufferCount);
                        attempts++;
                    }

                    usedIndices.Add(idx);
                    batch[i] = _replayBuffer[idx];
                }
            }

            double totalError = 0;

            lock (_trainingLock)
            {
                for (int i = 0; i < batchCount; i++)
                {
                    totalError += TrainSingle(batch[i].Input, batch[i].Target);
                }

                UpdateWeights();
            }

            _lastBatchError = totalError / batchCount;
            _runningAvgError = _runningAvgError * ERROR_SMOOTHING + _lastBatchError * (1 - ERROR_SMOOTHING);

            return _lastBatchError;
        }

        private void TrainBatchAsync()
        {
            try
            {
                TrainBatchInternal();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NeuralNet] TrainBatchAsync error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SampleByPriority(System.Random random, double totalPriority, int count)
        {
            double randomValue = random.NextDouble() * totalPriority;
            double cumulative = 0;

            for (int i = 0; i < count; i++)
            {
                cumulative += _replayBuffer[i].Priority;
                if (randomValue <= cumulative)
                    return i;
            }

            return count - 1;
        }

        private void LogActionStatistics()
        {
            string[] actionNames = {
                "Right", "Left", "Jump", "Dash", "Attack", "DownAttack",
                "UpAttack", "Cast", "MainAbility", "FirstTool", "SecondTool", "HarpoonDash"
            };

            string stats = "[NeuralNet] Action Distribution:\n";
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
            if (_layers == null || input == null || target == null)
                return 0;

            double[] current = input;

            for (int l = 0; l < _layers.Length; l++)
            {
                var layer = _layers[l];
                if (layer == null) continue;

                Buffer.BlockCopy(current, 0, layer.Input, 0,
                    Math.Min(current.Length, layer.Input.Length) * sizeof(double));
                layer.ForwardOptimized(current, layer.Output);
                current = layer.Output;
            }

            double weightedError = 0;
            double totalWeight = 0;

            for (int i = 0; i < OutputSize; i++)
            {
                double predicted = Math.Max(1e-7, Math.Min(1 - 1e-7, current[i]));
                double actual = target[i];

                double bce = -(actual * Math.Log(predicted) + (1 - actual) * Math.Log(1 - predicted));

                double weight = i < ACTION_LOSS_WEIGHTS.Length ? ACTION_LOSS_WEIGHTS[i] : 1.0;
                weightedError += bce * weight;
                totalWeight += weight;
            }

            double error = weightedError / totalWeight;

            BackpropagateWeighted(target);

            return error;
        }

        private void BackpropagateWeighted(double[] target)
        {
            if (_layers == null || _layers.Length == 0)
                return;

            var outputLayer = _layers[_layers.Length - 1];
            if (outputLayer == null) return;

            for (int i = 0; i < outputLayer.OutputSize; i++)
            {
                double output = Math.Max(1e-7, Math.Min(1 - 1e-7, outputLayer.Output[i]));
                double actual = target[i];
                double error = output - actual;
                double weight = i < ACTION_LOSS_WEIGHTS.Length ? ACTION_LOSS_WEIGHTS[i] : 1.0;
                outputLayer.Delta[i] = error * weight * outputLayer.ActivationDerivative(output);
            }

            for (int l = _layers.Length - 2; l >= 0; l--)
            {
                var currentLayer = _layers[l];
                var nextLayer = _layers[l + 1];

                if (currentLayer == null || nextLayer == null) continue;

                for (int i = 0; i < currentLayer.OutputSize; i++)
                {
                    double sum = 0;
                    for (int j = 0; j < nextLayer.OutputSize; j++)
                    {
                        sum += nextLayer.Delta[j] * nextLayer.Weights[i, j];
                    }
                    currentLayer.Delta[i] = sum * currentLayer.ActivationDerivative(currentLayer.Output[i]);
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
            if (_layers == null) return;

            foreach (var layer in _layers)
            {
                if (layer == null) continue;

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
            if (_layers == null) return;

            double beta1_t = Math.Pow(Beta1, _timestep);
            double beta2_t = Math.Pow(Beta2, _timestep);
            double lr_t = LearningRate * Math.Sqrt(1 - beta2_t) / (1 - beta1_t);

            foreach (var layer in _layers)
            {
                if (layer == null) continue;

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
            if (outputs == null)
                return new bool[OutputSize];

            bool[] actions = new bool[outputs.Length];
            for (int i = 0; i < outputs.Length; i++)
                actions[i] = outputs[i] >= threshold;
            return actions;
        }

        // ============================================================================
        // ЗБЕРЕЖЕННЯ/ЗАВАНТАЖЕННЯ (SINGLE COPY, З OPTIMIZER STATE)
        // ============================================================================

        public void Save(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                lock (_trainingLock)
                {
                    using (var writer = new BinaryWriter(File.Open(path, FileMode.Create)))
                    {
                        // === HEADER ===
                        writer.Write(FILE_FORMAT_VERSION);
                        writer.Write(InputSize);
                        writer.Write(OutputSize);
                        writer.Write(_layers.Length);

                        // === OPTIMIZER METADATA ===
                        writer.Write((int)Optimizer);
                        writer.Write(_timestep);
                        writer.Write(LearningRate);
                        writer.Write(Momentum);
                        writer.Write(Beta1);
                        writer.Write(Beta2);
                        writer.Write(Epsilon);

                        // === LAYERS DATA ===
                        foreach (var layer in _layers)
                        {
                            if (layer == null) continue;

                            // Layer architecture
                            writer.Write(layer.InputSize);
                            writer.Write(layer.OutputSize);
                            writer.Write((int)layer.Activation);

                            // Weights and biases
                            for (int i = 0; i < layer.InputSize; i++)
                                for (int j = 0; j < layer.OutputSize; j++)
                                    writer.Write(layer.Weights[i, j]);

                            for (int j = 0; j < layer.OutputSize; j++)
                                writer.Write(layer.Biases[j]);

                            // === OPTIMIZER STATE ===
                            // SGD momentum
                            for (int i = 0; i < layer.InputSize; i++)
                                for (int j = 0; j < layer.OutputSize; j++)
                                    writer.Write(layer.WeightVelocity[i, j]);

                            for (int j = 0; j < layer.OutputSize; j++)
                                writer.Write(layer.BiasVelocity[j]);

                            // Adam first moment (m)
                            for (int i = 0; i < layer.InputSize; i++)
                                for (int j = 0; j < layer.OutputSize; j++)
                                    writer.Write(layer.WeightM[i, j]);

                            for (int j = 0; j < layer.OutputSize; j++)
                                writer.Write(layer.BiasM[j]);

                            // Adam second moment (v)
                            for (int i = 0; i < layer.InputSize; i++)
                                for (int j = 0; j < layer.OutputSize; j++)
                                    writer.Write(layer.WeightV[i, j]);

                            for (int j = 0; j < layer.OutputSize; j++)
                                writer.Write(layer.BiasV[j]);
                        }
                    }
                }

                Debug.Log($"[NeuralNet] Збережено в: {path} (версія {FILE_FORMAT_VERSION}, timestep={_timestep})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NeuralNet] Помилка збереження: {ex.Message}");
                throw;
            }
        }

        public static NeuralNet Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException($"Файл не знайдено: {path}");

            try
            {
                using (var reader = new BinaryReader(File.Open(path, FileMode.Open)))
                {
                    // === READ HEADER ===
                    int version = reader.ReadInt32();

                    if (version != FILE_FORMAT_VERSION && version != 1)
                    {
                        Debug.LogWarning($"[NeuralNet] Несумісна версія файлу: {version}");
                    }

                    int inputSize = reader.ReadInt32();
                    int outputSize = reader.ReadInt32();
                    int layerCount = reader.ReadInt32();

                    // === READ OPTIMIZER METADATA (v2+) ===
                    OptimizerType optimizerType = OptimizerType.Adam;
                    int timestep = 0;
                    double learningRate = 0.002;
                    double momentum = 0.9;
                    double beta1 = 0.9;
                    double beta2 = 0.999;
                    double epsilon = 1e-8;

                    if (version >= 2)
                    {
                        optimizerType = (OptimizerType)reader.ReadInt32();
                        timestep = reader.ReadInt32();
                        learningRate = reader.ReadDouble();
                        momentum = reader.ReadDouble();
                        beta1 = reader.ReadDouble();
                        beta2 = reader.ReadDouble();
                        epsilon = reader.ReadDouble();
                    }

                    // === READ ARCHITECTURE ===
                    var layerInfos = new List<(int inSize, int outSize, int activation)>();
                    long weightsStartPosition = reader.BaseStream.Position;

                    for (int l = 0; l < layerCount; l++)
                    {
                        int inSize = reader.ReadInt32();
                        int outSize = reader.ReadInt32();
                        int activation = reader.ReadInt32();
                        layerInfos.Add((inSize, outSize, activation));

                        // Calculate bytes to skip based on version
                        long weightsAndBiasesBytes = (inSize * outSize + outSize) * sizeof(double);
                        long optimizerStateBytes = 0;

                        if (version >= 2)
                        {
                            // WeightVelocity + BiasVelocity + WeightM + BiasM + WeightV + BiasV
                            optimizerStateBytes = (
                                inSize * outSize + outSize + // Velocity
                                inSize * outSize + outSize + // M
                                inSize * outSize + outSize   // V
                            ) * sizeof(double);
                        }

                        reader.BaseStream.Position += weightsAndBiasesBytes + optimizerStateBytes;
                    }

                    // === BUILD NETWORK ===
                    var hiddenSizes = new List<int>();
                    for (int l = 0; l < layerCount - 1; l++)
                    {
                        hiddenSizes.Add(layerInfos[l].outSize);
                    }

                    var net = new NeuralNet(inputSize, outputSize, hiddenSizes.ToArray(), learningRate, momentum);

                    // Apply loaded hyperparameters
                    net.Optimizer = optimizerType;
                    net.Beta1 = beta1;
                    net.Beta2 = beta2;
                    net.Epsilon = epsilon;
                    net._timestep = timestep;

                    // === LOAD WEIGHTS AND OPTIMIZER STATE ===
                    reader.BaseStream.Position = weightsStartPosition;

                    lock (net._trainingLock)
                    {
                        for (int l = 0; l < layerCount; l++)
                        {
                            var layer = net._layers[l];

                            if (layer == null) continue;

                            // Read metadata
                            int inSize = reader.ReadInt32();
                            int outSize = reader.ReadInt32();
                            int activation = reader.ReadInt32();

                            // Verify architecture match
                            if (layer.InputSize != inSize || layer.OutputSize != outSize)
                            {
                                throw new InvalidDataException(
                                    $"Layer {l} mismatch: file={inSize}x{outSize}, network={layer.InputSize}x{layer.OutputSize}");
                            }

                            // === LOAD WEIGHTS ===
                            for (int i = 0; i < inSize; i++)
                                for (int j = 0; j < outSize; j++)
                                    layer.Weights[i, j] = reader.ReadDouble();

                            // === LOAD BIASES ===
                            for (int j = 0; j < outSize; j++)
                                layer.Biases[j] = reader.ReadDouble();

                            // === LOAD OPTIMIZER STATE (v2+) ===
                            if (version >= 2)
                            {
                                // WeightVelocity
                                for (int i = 0; i < inSize; i++)
                                    for (int j = 0; j < outSize; j++)
                                        layer.WeightVelocity[i, j] = reader.ReadDouble();

                                // BiasVelocity
                                for (int j = 0; j < outSize; j++)
                                    layer.BiasVelocity[j] = reader.ReadDouble();

                                // WeightM
                                for (int i = 0; i < inSize; i++)
                                    for (int j = 0; j < outSize; j++)
                                        layer.WeightM[i, j] = reader.ReadDouble();

                                // BiasM
                                for (int j = 0; j < outSize; j++)
                                    layer.BiasM[j] = reader.ReadDouble();

                                // WeightV
                                for (int i = 0; i < inSize; i++)
                                    for (int j = 0; j < outSize; j++)
                                        layer.WeightV[i, j] = reader.ReadDouble();

                                // BiasV
                                for (int j = 0; j < outSize; j++)
                                    layer.BiasV[j] = reader.ReadDouble();
                            }
                        }
                    }

                    string stateInfo = version >= 2
                        ? $", optimizer={optimizerType}, timestep={timestep}"
                        : " (legacy v1, optimizer state reset)";

                    Debug.Log($"[NeuralNet] Завантажено з: {path} (версія {version}{stateInfo})");
                    return net;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NeuralNet] Помилка завантаження: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        public void ClearBuffer()
        {
            lock (_bufferLock)
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
                   $"AvgError: {_runningAvgError:F5} | " +
                   $"Timestep: {_timestep} | " +
                   $"Training: {(_isTraining ? "Active" : "Stopped")}";
        }

        public void Dispose()
        {
            StopAsyncTraining();

            if (_trainingCts != null)
            {
                _trainingCts.Dispose();
                _trainingCts = null;
            }

            if (_trainingSignal != null)
            {
                _trainingSignal.Dispose();
            }
        }
    }
}
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;

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

        public double LearningRate { get; set; } = 0.002;
        public double Momentum { get; set; } = 0.9;
        public double GradientClipValue { get; set; } = 1.0;
        public double L2Regularization { get; set; } = 0.0001;

        public OptimizerType Optimizer { get; set; } = OptimizerType.Adam;
        public double Beta1 { get; set; } = 0.9;
        public double Beta2 { get; set; } = 0.999;
        public double Epsilon { get; set; } = 1e-8;
        private int _timestep = 0;

        // Learning rate decay
        private double _initialLearningRate = 0.002;
        private const int LR_DECAY_STEP_1 = 10000;
        private const int LR_DECAY_STEP_2 = 50000;
        private const double LR_DECAY_FACTOR_1 = 0.25;
        private const double LR_DECAY_FACTOR_2 = 0.2;

        private Experience[] _replayBuffer;
        private int _bufferHead = 0;
        private int _bufferCount = 0;
        private const int MAX_BUFFER_SIZE = 10000;
        private const int BATCH_SIZE = 32;
        private const int MIN_BUFFER_SIZE = 128;

        // ПОСЛІДОВНОСТІ ДІЙ - оптимізовано з фіксованим масивом
        private FrameData[] _recentFramesArray;
        private int _recentFramesStart = 0;
        private int _recentFramesCount = 0;
        private const int SEQUENCE_LENGTH = 30;
        private int _frameCounter = 0;
        private int _sequencesSaved = 0;
        private int _totalFramesInSequences = 0;

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

        // Кеш для рандому
        private System.Random _random = new System.Random(Guid.NewGuid().GetHashCode());

        // Async training
        private Task _trainingTask;
        private CancellationTokenSource _trainingCancellation;
        private volatile bool _isTraining = false;

        // ОНОВЛЕНІ ВАГИ
        private static readonly double[] ACTION_LOSS_WEIGHTS = {
            0.1, 0.1, 5.0, 5.0, 9.0, 9.0,
            9.0, 12.0, 12.0, 10.0, 10.0, 12.0
        };

        private struct FrameData
        {
            public float[] Input;
            public float[] Target;
            public int FrameId;
        }

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

            // ОПТИМІЗАЦІЯ: inline forward pass
            public void Forward(double[] input, double[] output)
            {
                int inputSize = InputSize;
                int outputSize = OutputSize;

                // Копіюємо input один раз
                Buffer.BlockCopy(input, 0, Input, 0, inputSize * sizeof(double));

                // Основний цикл без викликів функцій
                for (int j = 0; j < outputSize; j++)
                {
                    double sum = Biases[j];
                    for (int i = 0; i < inputSize; i++)
                    {
                        sum += input[i] * Weights[i, j];
                    }
                    PreActivation[j] = sum;

                    // Inline activation
                    double activated;
                    switch (Activation)
                    {
                        case ActivationType.ReLU:
                            activated = sum > 0 ? sum : 0;
                            break;
                        case ActivationType.LeakyReLU:
                            activated = sum > 0 ? sum : 0.01 * sum;
                            break;
                        case ActivationType.Tanh:
                            activated = Math.Tanh(sum);
                            break;
                        case ActivationType.Sigmoid:
                            activated = sum >= 0
                                ? 1.0 / (1.0 + Math.Exp(-sum))
                                : Math.Exp(sum) / (1.0 + Math.Exp(sum));
                            break;
                        default:
                            activated = sum;
                            break;
                    }

                    output[j] = activated;
                    Output[j] = activated;
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
            _initialLearningRate = learningRate;
            Momentum = momentum;

            if (hiddenLayers == null || hiddenLayers.Length == 0)
            {
                hiddenLayers = new int[] { 256, 128, 64 };
            }

            InitializeNetwork(hiddenLayers);
            InitializeBuffers();

            _actionCounts = new int[outputSize];

            // Ініціалізація async training
            _trainingCancellation = new CancellationTokenSource();
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

            // Фіксований масив замість Queue
            _recentFramesArray = new FrameData[SEQUENCE_LENGTH];
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
                // ОПТИМІЗАЦІЯ: прямий доступ без додаткових копій
                for (int i = 0; i < InputSize; i++)
                    _inputCache[i] = input[i];

                double[] current = _inputCache;

                for (int l = 0; l < _layers.Length; l++)
                {
                    var layer = _layers[l];
                    var output = l == _layers.Length - 1 ? _outputCache : new double[layer.OutputSize];
                    layer.Forward(current, output);
                    current = output;
                }

                float[] result = new float[OutputSize];
                for (int i = 0; i < OutputSize; i++)
                    result[i] = (float)_outputCache[i];

                return result;
            }
        }

        // ОПТИМІЗАЦІЯ: CollectExperience без Queue
        public void CollectExperience(float[] input, float[] target)
        {
            if (input == null || target == null) return;
            if (input.Length != InputSize || target.Length != OutputSize) return;

            _frameCounter++;

            // Circular buffer замість Queue
            int index = _recentFramesCount % SEQUENCE_LENGTH;

            if (_recentFramesArray[index].Input == null)
            {
                _recentFramesArray[index].Input = new float[InputSize];
                _recentFramesArray[index].Target = new float[OutputSize];
            }

            Array.Copy(input, _recentFramesArray[index].Input, InputSize);
            Array.Copy(target, _recentFramesArray[index].Target, OutputSize);
            _recentFramesArray[index].FrameId = _frameCounter;

            if (_recentFramesCount < SEQUENCE_LENGTH)
                _recentFramesCount++;

            // Перевірка на важливу дію
            bool hasImportantAction = false;
            for (int i = 2; i < OutputSize; i++)
            {
                if (target[i] > 0.5f)
                {
                    hasImportantAction = true;
                    break;
                }
            }

            if (hasImportantAction)
            {
                int framesInSequence = _recentFramesCount;
                _sequencesSaved++;
                _totalFramesInSequences += framesInSequence;

                if (_sequencesSaved % 100 == 0)
                {
                    float avgLength = _totalFramesInSequences / (float)_sequencesSaved;
                    Debug.Log($"[Collector] Збережено {_sequencesSaved} послідовностей, " +
                             $"середня довжина: {avgLength:F1} кадрів");
                }

                int oldestFrameId = _frameCounter - framesInSequence;

                for (int i = 0; i < framesInSequence; i++)
                {
                    var frame = _recentFramesArray[i];
                    float temporalProgress = (frame.FrameId - oldestFrameId) / (float)framesInSequence;
                    float temporalBoost = 1.0f + temporalProgress;

                    float basePriority = CalculatePriority(frame.Target);
                    float finalPriority = basePriority * temporalBoost;

                    AddToBuffer(frame.Input, frame.Target, finalPriority);
                }

                _recentFramesCount = 0;
            }
            else if (UnityEngine.Random.value < 0.02f)
            {
                float priority = CalculatePriority(target);
                AddToBuffer(input, target, priority * 0.5f);
            }
        }

        private void AddToBuffer(float[] input, float[] target, float priority)
        {
            lock (_trainingLock)
            {
                // ОПТИМІЗАЦІЯ: Переиспользуем існуючі об'єкти
                ref Experience exp = ref _replayBuffer[_bufferHead];

                if (exp.Input == null)
                {
                    exp.Input = new double[InputSize];
                    exp.Target = new double[OutputSize];
                }

                for (int i = 0; i < InputSize; i++)
                    exp.Input[i] = input[i];
                for (int i = 0; i < OutputSize; i++)
                    exp.Target[i] = target[i];

                exp.Priority = priority;

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

            if (target[0] > 0.5f || target[1] > 0.5f) priority *= 0.1f;
            if (target[2] > 0.5f) priority *= 5.0f;
            if (target[3] > 0.5f) priority *= 5.0f;
            if (target[4] > 0.5f) priority *= 9.0f;
            if (target[5] > 0.5f) priority *= 9.0f;
            if (target[6] > 0.5f) priority *= 9.0f;
            if (target[7] > 0.5f) priority *= 12.0f;
            if (target[8] > 0.5f) priority *= 12.0f;
            if (target[9] > 0.5f) priority *= 10.0f;
            if (target[10] > 0.5f) priority *= 10.0f;
            if (target[11] > 0.5f) priority *= 8.0f;

            return priority;
        }

        // КРИТИЧНА ОПТИМІЗАЦІЯ: Async training
        public double TrainBatch()
        {
            _trainingCounter++;
            _statsCounter++;

            if (_trainingCounter % 10000 == 0)
                ApplyLearningRateDecay();

            if (_statsCounter >= 500)
            {
                LogActionStatistics();
                _statsCounter = 0;
            }

            if (_trainingCounter % TRAIN_EVERY_N_CALLS != 0)
                return _lastBatchError;

            // Якщо вже тренується - пропускаємо
            if (_isTraining)
                return _lastBatchError;

            lock (_trainingLock)
            {
                if (_bufferCount < MIN_BUFFER_SIZE)
                    return 0;

                // Запускаємо асинхронно
                _isTraining = true;
                Task.Run(() => TrainBatchAsync());
            }

            return _lastBatchError;
        }

        private void TrainBatchAsync()
        {
            try
            {
                double totalError = 0;
                var usedIndices = new HashSet<int>();

                double totalPriority = 0;
                for (int i = 0; i < _bufferCount; i++)
                {
                    totalPriority += _replayBuffer[i].Priority;
                }

                int actualBatchSize = Math.Min(BATCH_SIZE, _bufferCount);

                for (int i = 0; i < actualBatchSize; i++)
                {
                    int idx = SampleByPriority(_random, totalPriority);

                    int attempts = 0;
                    while (usedIndices.Contains(idx) && attempts < 100)
                    {
                        idx = SampleByPriority(_random, totalPriority);
                        attempts++;
                    }

                    usedIndices.Add(idx);
                    var exp = _replayBuffer[idx];
                    totalError += TrainSingle(exp.Input, exp.Target);
                }

                _lastBatchError = totalError / actualBatchSize;
                _runningAvgError = _runningAvgError * ERROR_SMOOTHING + _lastBatchError * (1 - ERROR_SMOOTHING);

                if (_timestep % 1000 == 0)
                {
                    LogPredictionSample();
                }
            }
            finally
            {
                _isTraining = false;
            }
        }

        private void ApplyLearningRateDecay()
        {
            if (_timestep == LR_DECAY_STEP_1)
            {
                LearningRate = _initialLearningRate * LR_DECAY_FACTOR_1;
                Debug.Log($"[NeuralNet] Learning rate decay: {LearningRate:F6} (timestep {_timestep})");
            }
            else if (_timestep == LR_DECAY_STEP_2)
            {
                LearningRate = _initialLearningRate * LR_DECAY_FACTOR_1 * LR_DECAY_FACTOR_2;
                Debug.Log($"[NeuralNet] Learning rate decay: {LearningRate:F6} (timestep {_timestep})");
            }
        }

        private void LogPredictionSample()
        {
            if (_bufferCount == 0) return;

            var sample = _replayBuffer[UnityEngine.Random.Range(0, _bufferCount)];

            double[] predictions = new double[OutputSize];
            double[] current = sample.Input;

            for (int l = 0; l < _layers.Length; l++)
            {
                var output = new double[_layers[l].OutputSize];
                _layers[l].Forward(current, output);
                current = output;
            }

            Array.Copy(current, predictions, OutputSize);

            string log = "[NeuralNet] Sample prediction vs target:\n";
            string[] actionNames = {
                "Right", "Left", "Jump", "Dash", "Attack", "DownAttack",
                "UpAttack", "Cast", "MainAbility", "FirstTool", "SecondTool", "HarpoonDash"
            };

            for (int i = 0; i < Math.Min(OutputSize, actionNames.Length); i++)
            {
                log += $"  {actionNames[i]}: pred={predictions[i]:F3}, target={sample.Target[i]:F3}\n";
            }
            Debug.Log(log);
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

            string stats = $"[NeuralNet] Action Distribution (buffer={_bufferCount}/{MAX_BUFFER_SIZE}):\n";
            int total = _actionCounts.Sum();

            for (int i = 0; i < Math.Min(_actionCounts.Length, actionNames.Length); i++)
            {
                float percentage = total > 0 ? (_actionCounts[i] * 100.0f / total) : 0;
                stats += $"  {actionNames[i]}: {_actionCounts[i]} ({percentage:F1}%)\n";
            }

            stats += $"Last batch error: {_lastBatchError:F5} | " +
                    $"Running avg: {_runningAvgError:F5} | " +
                    $"LR: {LearningRate:F6} | " +
                    $"Timestep: {_timestep}";

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

            double weightedError = 0;
            double totalWeight = 0;

            for (int i = 0; i < OutputSize; i++)
            {
                double predicted = Math.Max(1e-7, Math.Min(1 - 1e-7, current[i]));
                double actual = target[i];

                double bce = -(actual * Math.Log(predicted) + (1 - actual) * Math.Log(1 - predicted));

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

                double error = output - actual;
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
                    int inSize = reader.ReadInt32();
                    int outSize = reader.ReadInt32();
                    int activation = reader.ReadInt32();
                    hiddenSizes.Add(outSize);

                    reader.BaseStream.Position += (inSize * outSize + outSize) * sizeof(double);
                }

                var net = new NeuralNet(inputSize, outputSize, hiddenSizes.ToArray());

                reader.BaseStream.Position = startPos;

                lock (net._trainingLock)
                {
                    foreach (var layer in net._layers)
                    {
                        reader.ReadInt32(); // InputSize
                        reader.ReadInt32(); // OutputSize
                        reader.ReadInt32(); // Activation

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
                _recentFramesCount = 0;
                _frameCounter = 0;
                _sequencesSaved = 0;
                _totalFramesInSequences = 0;
                Array.Clear(_actionCounts, 0, _actionCounts.Length);
            }
        }

        public string GetStats()
        {
            float avgSeqLength = _sequencesSaved > 0 ? _totalFramesInSequences / (float)_sequencesSaved : 0;

            return $"Buffer: {_bufferCount}/{MAX_BUFFER_SIZE} | " +
                   $"Samples: {_totalSamplesCollected} | " +
                   $"Sequences: {_sequencesSaved} (avg {avgSeqLength:F1} frames) | " +
                   $"Error: {_lastBatchError:F5} | " +
                   $"AvgError: {_runningAvgError:F5} | " +
                   $"LR: {LearningRate:F6}";
        }

        private void LogMultiLabelStats()
        {
            lock (_trainingLock)
            {
                int[] comboCounts = new int[OutputSize * OutputSize];

                for (int i = 0; i < _bufferCount; i++)
                {
                    var target = _replayBuffer[i].Target;

                    for (int a = 0; a < OutputSize; a++)
                    {
                        if (target[a] > 0.5)
                        {
                            for (int b = a + 1; b < OutputSize; b++)
                            {
                                if (target[b] > 0.5)
                                {
                                    comboCounts[a * OutputSize + b]++;
                                }
                            }
                        }
                    }
                }

                Debug.Log("[NeuralNet] Top action combinations:");
                var topCombos = comboCounts
                    .Select((count, idx) => new { Count = count, A = idx / OutputSize, B = idx % OutputSize })
                    .Where(x => x.Count > 0)
                    .OrderByDescending(x => x.Count)
                    .Take(10);

                string[] actionNames = {
                    "Right", "Left", "Jump", "Dash", "Attack", "DownAttack",
                    "UpAttack", "Cast", "MainAbility", "FirstTool", "SecondTool", "HarpoonDash"
                };

                foreach (var combo in topCombos)
                {
                    Debug.Log($"  {actionNames[combo.A]} + {actionNames[combo.B]}: {combo.Count}");
                }

                int rightLeftConflicts = comboCounts[0 * OutputSize + 1];
                if (rightLeftConflicts > 0)
                {
                    Debug.LogWarning($"⚠️ Right+Left conflicts detected: {rightLeftConflicts}");
                }
            }
        }

        public Dictionary<string, float> GetDetailedMetrics()
        {
            var metrics = new Dictionary<string, float>();
            LogMultiLabelStats();

            lock (_trainingLock)
            {
                for (int i = 0; i < OutputSize; i++)
                {
                    float avgActivation = 0;
                    int count = 0;

                    for (int j = 0; j < _bufferCount; j++)
                    {
                        avgActivation += (float)_replayBuffer[j].Target[i];
                        count++;
                    }

                    metrics[$"action_{i}_avg"] = count > 0 ? avgActivation / count : 0;
                }

                metrics["buffer_fill"] = _bufferCount / (float)MAX_BUFFER_SIZE;
                metrics["sequences_saved"] = _sequencesSaved;
                metrics["avg_sequence_length"] = _sequencesSaved > 0 ?
                    _totalFramesInSequences / (float)_sequencesSaved : 0;
            }

            return metrics;
        }

        // Cleanup при знищенні об'єкта
        ~NeuralNet()
        {
            _trainingCancellation?.Cancel();
            _trainingCancellation?.Dispose();
        }
    }
}
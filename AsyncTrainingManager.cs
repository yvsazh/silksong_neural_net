using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace SilksongNeuralNetwork
{
    /// <summary>
    /// Менеджер асинхронного навчання нейромережі
    /// Використовує окремий потік для навчання, щоб не блокувати основний ігровий потік
    /// </summary>
    public class AsyncTrainingManager : IDisposable
    {
        private NeuralNet _nn;
        private Thread _trainingThread;
        private volatile bool _isRunning;
        private volatile bool _shouldTrain;

        private readonly Queue<TrainingData> _trainingQueue = new Queue<TrainingData>();
        private readonly object _queueLock = new object();

        private const int MAX_QUEUE_SIZE = 2000;
        private const int MIN_BATCH_SIZE = 32;
        private const int TRAINING_INTERVAL_MS = 50; // Тренувати кожні 50мс

        public int QueuedSamples
        {
            get
            {
                lock (_queueLock)
                {
                    return _trainingQueue.Count;
                }
            }
        }

        public bool IsTraining => _shouldTrain;
        public double LastError { get; private set; }
        public int TotalSamplesTrained { get; private set; }

        private struct TrainingData
        {
            public float[] Input;
            public float[] Target;
            public long Timestamp;
        }

        public AsyncTrainingManager(NeuralNet neuralNet)
        {
            _nn = neuralNet ?? throw new ArgumentNullException(nameof(neuralNet));
            _isRunning = true;
            _shouldTrain = false;

            // Запускаємо фоновий потік
            _trainingThread = new Thread(TrainingLoop)
            {
                IsBackground = true,
                Priority = System.Threading.ThreadPriority.BelowNormal,
                Name = "NeuralNet Training Thread"
            };
            _trainingThread.Start();

            Debug.Log("[AsyncTraining] Training thread started");
        }

        /// <summary>
        /// Додає новий семпл для навчання (викликається з основного потоку)
        /// </summary>
        public void QueueTraining(float[] input, float[] target)
        {
            if (!_shouldTrain) return;
            if (input == null || target == null) return;

            lock (_queueLock)
            {
                // Якщо черга переповнена, видаляємо найстарші семпли
                while (_trainingQueue.Count >= MAX_QUEUE_SIZE)
                {
                    _trainingQueue.Dequeue();
                }

                _trainingQueue.Enqueue(new TrainingData
                {
                    Input = (float[])input.Clone(),
                    Target = (float[])target.Clone(),
                    Timestamp = DateTime.Now.Ticks
                });
            }
        }

        /// <summary>
        /// Вмикає/вимикає навчання
        /// </summary>
        public void SetTraining(bool enabled)
        {
            _shouldTrain = enabled;
            Debug.Log($"[AsyncTraining] Training {(enabled ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Основний цикл навчання (виконується в окремому потоці)
        /// </summary>
        private void TrainingLoop()
        {
            Debug.Log("[AsyncTraining] Training loop started");

            while (_isRunning)
            {
                try
                {
                    if (_shouldTrain)
                    {
                        ProcessTrainingBatch();
                    }

                    // Затримка між ітераціями
                    Thread.Sleep(TRAINING_INTERVAL_MS);
                }
                catch (ThreadAbortException)
                {
                    Debug.Log("[AsyncTraining] Training thread aborted");
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AsyncTraining] Error in training loop: {ex.Message}\n{ex.StackTrace}");
                    Thread.Sleep(1000); // Більша затримка після помилки
                }
            }

            Debug.Log("[AsyncTraining] Training loop ended");
        }

        /// <summary>
        /// Обробляє батч семплів з черги
        /// </summary>
        private void ProcessTrainingBatch()
        {
            List<TrainingData> batch = new List<TrainingData>();

            // Забираємо семпли з черги
            lock (_queueLock)
            {
                // Чекаємо поки накопичиться мінімальна кількість
                if (_trainingQueue.Count < MIN_BATCH_SIZE)
                    return;

                // Забираємо весь доступний батч (до MAX)
                int count = Math.Min(_trainingQueue.Count, 128);
                for (int i = 0; i < count; i++)
                {
                    if (_trainingQueue.Count > 0)
                        batch.Add(_trainingQueue.Dequeue());
                }
            }

            if (batch.Count == 0) return;

            // Збираємо досвід в replay buffer
            foreach (var sample in batch)
            {
                _nn.CollectExperience(sample.Input, sample.Target);
            }

            // Виконуємо навчання
            double error = _nn.TrainBatch();
            LastError = error;
            TotalSamplesTrained += batch.Count;

            // Логування кожні 500 семплів
            if (TotalSamplesTrained % 500 < batch.Count)
            {
                Debug.Log($"[AsyncTraining] Batch: {batch.Count} samples | " +
                         $"Queue: {QueuedSamples} | Error: {error:F5} | " +
                         $"Total trained: {TotalSamplesTrained}");
            }
        }

        /// <summary>
        /// Очищає чергу навчання
        /// </summary>
        public void ClearQueue()
        {
            lock (_queueLock)
            {
                _trainingQueue.Clear();
            }
            TotalSamplesTrained = 0;
            Debug.Log("[AsyncTraining] Training queue cleared");
        }

        /// <summary>
        /// Статистика для UI
        /// </summary>
        public string GetStats()
        {
            return $"Queue: {QueuedSamples}/{MAX_QUEUE_SIZE} | " +
                   $"Training: {(IsTraining ? "ON" : "OFF")} | " +
                   $"Trained: {TotalSamplesTrained} | " +
                   $"Error: {LastError:F5}";
        }

        /// <summary>
        /// Зупиняє навчальний потік
        /// </summary>
        public void Dispose()
        {
            Debug.Log("[AsyncTraining] Shutting down training thread...");

            _isRunning = false;
            _shouldTrain = false;

            if (_trainingThread != null && _trainingThread.IsAlive)
            {
                // Даємо потоку час завершитися нормально
                if (!_trainingThread.Join(2000))
                {
                    Debug.LogWarning("[AsyncTraining] Training thread did not stop gracefully, aborting");
                    try
                    {
                        _trainingThread.Abort();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[AsyncTraining] Error aborting thread: {ex.Message}");
                    }
                }
            }

            Debug.Log("[AsyncTraining] Training thread stopped");
        }
    }
}
using System.Collections.Generic;
using UnityEngine;

namespace SilksongNeuralNetwork
{
    public class RaySensorData
    {
        public Vector2 direction;
        public float normalizedDistance; // 1 = max distance, 0 = hit close
        public bool hitDetected;
        public Vector2 hitPoint;
        public string targetTag; // Додатково: можна зберігати тег об'єкта
    }

    public enum RaySensorType
    {
        Obstacles,  // Для карти/перешкод
        Enemies,     // Для ворогів
        EnemiesProjectiles,     
    }

    public static class RaySensorSystem
    {
        // Налаштування для променів перешкод
        private static int _obstacleRayCount = 16;
        private static float _obstacleMaxDistance = 20f;
        private static LayerMask _obstacleLayerMask;

        // Налаштування для променів ворогів
        private static int _enemyRayCount = 12;
        private static float _enemyMaxDistance = 20f;
        private static LayerMask _enemyLayerMask;

        // Налаштування для снарядів
        private static int _enemyProjectilesRayCount = 20;
        private static float _enemyProjectilesMaxDistance = 20f;
        private static LayerMask _enemyProjectilesLayerMask;

        private static bool _initialized = false;

        // Ініціалізація системи променів
        public static void Initialize(
            int obstacleRayCount = 16,
            float obstacleMaxDistance = 20f,
            int enemyRayCount = 12,
            float enemyMaxDistance = 20f,
            int enemyProjectilesRayCount = 20,
            float enemyProjectilesMaxDistance = 20f
            )
        {
            // Налаштування променів для перешкод
            _obstacleRayCount = obstacleRayCount;
            _obstacleMaxDistance = obstacleMaxDistance;
            _obstacleLayerMask = LayerMask.GetMask("Soft Terrain", "Terrain", "Default");

            // Налаштування променів для ворогів
            _enemyRayCount = enemyRayCount;
            _enemyMaxDistance = enemyMaxDistance;
            _enemyLayerMask = LayerMask.GetMask("Enemies");

            _enemyProjectilesRayCount = enemyProjectilesRayCount;
            _enemyProjectilesMaxDistance = enemyProjectilesMaxDistance;
            _enemyProjectilesLayerMask = LayerMask.GetMask("Attack");

            _initialized = true;
        }

        // Універсальний метод для кастування променів
        private static List<RaySensorData> CastRaysInternal(
            Vector2 origin,
            int rayCount,
            float maxDistance,
            LayerMask layerMask,
            RaySensorType sensorType)
        {
            List<RaySensorData> sensorData = new List<RaySensorData>();
            float angleStep = 360f / rayCount;

            for (int i = 0; i < rayCount; i++)
            {
                float angle = i * angleStep;
                Vector2 direction = GetDirectionFromAngle(angle);

                RaycastHit2D hit = Physics2D.Raycast(
                    origin,
                    direction,
                    maxDistance,
                    layerMask
                );

                RaySensorData data = new RaySensorData
                {
                    direction = direction,
                    hitDetected = hit.collider != null
                };

                if (hit.collider != null)
                {
                    data.normalizedDistance = hit.distance / maxDistance;
                    data.hitPoint = hit.point;
                    data.targetTag = hit.collider.tag;
                }
                else
                {

                    data.normalizedDistance = 1f;
                    data.hitPoint = origin + direction * maxDistance;
                }

                sensorData.Add(data);
            }

            // Відмальовуємо промені для дебагу
            if (DebugTools.Instance != null)
            {
                DebugTools.Instance.DrawRaySensors(origin, sensorData, maxDistance, sensorType);
            }

            return sensorData;
        }

        // Отримання даних з променів для перешкод
        public static List<RaySensorData> CastObstacleRays(Vector2 origin)
        {
            if (!_initialized)
            {
                Initialize();
            }

            return CastRaysInternal(origin, _obstacleRayCount, _obstacleMaxDistance, _obstacleLayerMask, RaySensorType.Obstacles);
        }

        // Отримання даних з променів для ворогів
        public static List<RaySensorData> CastEnemyRays(Vector2 origin)
        {
            if (!_initialized)
            {
                Initialize();
            }

            return CastRaysInternal(origin, _enemyRayCount, _enemyMaxDistance, _enemyLayerMask, RaySensorType.Enemies);
        }

        public static List<RaySensorData> CastEnemyProjectilesRays(Vector2 origin)
        {
            if (!_initialized)
            {
                Initialize();
            }

            return CastRaysInternal(origin, _enemyProjectilesRayCount, _enemyProjectilesMaxDistance, _enemyProjectilesLayerMask, RaySensorType.EnemiesProjectiles);
        }

        // Отримання даних у вигляді списку float для нейромережі (перешкоди)
        public static List<float> GetObstacleRaySensorFloatData(Vector2 origin)
        {
            List<RaySensorData> sensors = CastObstacleRays(origin);
            List<float> floatData = new List<float>();

            foreach (var sensor in sensors)
            {
                floatData.Add(sensor.normalizedDistance);
            }

            return floatData;
        }

        // Отримання даних у вигляді списку float для нейромережі (вороги)
        public static List<float> GetEnemyRaySensorFloatData(Vector2 origin)
        {
            List<RaySensorData> sensors = CastEnemyRays(origin);
            List<float> floatData = new List<float>();

            foreach (var sensor in sensors)
            {
                floatData.Add(sensor.normalizedDistance);
            }

            return floatData;
        }

        public static List<float> GetEnemyProjectilesRaySensorFloatData(Vector2 origin)
        {
            List<RaySensorData> sensors = CastEnemyProjectilesRays(origin);
            List<float> floatData = new List<float>();

            foreach (var sensor in sensors)
            {
                floatData.Add(sensor.normalizedDistance);
            }

            return floatData;
        }

        // Отримання ВСІХ даних з обох систем променів
        public static List<float> GetAllRaySensorFloatData(Vector2 origin)
        {
            List<float> allData = new List<float>();

            // Спочатку дані про перешкоди
            allData.AddRange(GetObstacleRaySensorFloatData(origin));

            // Потім дані про ворогів
            allData.AddRange(GetEnemyRaySensorFloatData(origin));

            allData.AddRange(GetEnemyProjectilesRaySensorFloatData(origin));

            return allData;
        }

        // Допоміжний метод для отримання напрямку з кута
        private static Vector2 GetDirectionFromAngle(float angleDegrees)
        {
            float angleRadians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians));
        }

        // Налаштування параметрів для перешкод
        public static void SetObstacleRayCount(int count)
        {
            _obstacleRayCount = Mathf.Max(4, count);
        }

        public static void SetObstacleMaxDistance(float distance)
        {
            _obstacleMaxDistance = Mathf.Max(1f, distance);
        }

        // Налаштування параметрів для ворогів
        public static void SetEnemyRayCount(int count)
        {
            _enemyRayCount = Mathf.Max(4, count);
        }

        public static void SetEnemyMaxDistance(float distance)
        {
            _enemyMaxDistance = Mathf.Max(1f, distance);
        }

        public static void SetEnemyProjectilesRayCount(int count)
        {
            _enemyProjectilesRayCount = Mathf.Max(4, count);
        }

        public static void SetEnemyProjectilesMaxDistance(float distance)
        {
            _enemyProjectilesMaxDistance = Mathf.Max(1f, distance);
        }

        public static void SetObstacleLayerMask(LayerMask mask)
        {
            _obstacleLayerMask = mask;
        }

        public static void SetEnemyLayerMask(LayerMask mask)
        {
            _enemyLayerMask = mask;
        }

        public static void SetEnemyProjectilesLayerMask(LayerMask mask)
        {
            _enemyLayerMask = mask;
        }

        // Getters
        public static int GetObstacleRayCount() => _obstacleRayCount;
        public static float GetObstacleMaxDistance() => _obstacleMaxDistance;
        public static int GetEnemyRayCount() => _enemyRayCount;
        public static float GetEnemyMaxDistance() => _enemyMaxDistance;
        public static int GetEnemyProjectilesRayCount() => _enemyRayCount;
        public static float GetEnemyProjectilesMaxDistance() => _enemyMaxDistance;
    }
}
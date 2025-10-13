using System.Collections.Generic;
using UnityEngine;

namespace SilksongNeuralNetwork
{
    public class RaySensorData
    {
        public Vector2 direction;
        public float normalizedDistance; // 0 = max distance, 1 = hit close
        public bool hitDetected;
        public Vector2 hitPoint;
    }

    public static class RaySensorSystem
    {
        private static int _rayCount = 16;
        private static float _maxRayDistance = 20f;
        private static LayerMask _obstacleLayerMask;
        private static bool _initialized = false;

        // Ініціалізація системи променів
        public static void Initialize(int rayCount = 16, float maxDistance = 20f)
        {
            _rayCount = rayCount;
            _maxRayDistance = maxDistance;

            // Створюємо LayerMask для всіх твердих об'єктів
            // Layer 8 - Terrain (стіни, платформи)
            // Layer 9 - Default (різні об'єкти)
            // Можеш додати інші шари за потреби
            _obstacleLayerMask = LayerMask.GetMask("Terrain", "Default");

            // Альтернативний спосіб - виключити тільки ігрові об'єкти
            // _obstacleLayerMask = ~(1 << 11); // Виключаємо шар ворогів

            _initialized = true;
        }

        // Отримання даних з усіх променів
        public static List<RaySensorData> CastRays(Vector2 origin)
        {
            if (!_initialized)
            {
                Initialize();
            }

            List<RaySensorData> sensorData = new List<RaySensorData>();
            float angleStep = 360f / _rayCount;

            for (int i = 0; i < _rayCount; i++)
            {
                float angle = i * angleStep;
                Vector2 direction = GetDirectionFromAngle(angle);

                RaycastHit2D hit = Physics2D.Raycast(
                    origin,
                    direction,
                    _maxRayDistance,
                    _obstacleLayerMask
                );

                RaySensorData data = new RaySensorData
                {
                    direction = direction,
                    hitDetected = hit.collider != null
                };

                if (hit.collider != null)
                {
                    // Якщо є зіткнення - нормалізуємо відстань (0 = далеко, 1 = близько)
                    data.normalizedDistance = 1f - (hit.distance / _maxRayDistance);
                    data.hitPoint = hit.point;
                }
                else
                {
                    // Якщо нема зіткнення - відстань 0 (далеко/нічого нема)
                    data.normalizedDistance = 0f;
                    data.hitPoint = origin + direction * _maxRayDistance;
                }

                sensorData.Add(data);
            }

            // Відмальовуємо промені для дебагу
            if (DebugTools.Instance != null)
            {
                DebugTools.Instance.DrawRaySensors(origin, sensorData, _maxRayDistance);
            }

            return sensorData;
        }

        // Отримання даних у вигляді списку float для нейромережі
        public static List<float> GetRaySensorFloatData(Vector2 origin)
        {
            List<RaySensorData> sensors = CastRays(origin);
            List<float> floatData = new List<float>();

            foreach (var sensor in sensors)
            {
                floatData.Add(sensor.normalizedDistance);
                // Опціонально можеш додати також напрямок променя:
                // floatData.Add(sensor.direction.x);
                // floatData.Add(sensor.direction.y);
            }

            return floatData;
        }

        // Допоміжний метод для отримання напрямку з кута
        private static Vector2 GetDirectionFromAngle(float angleDegrees)
        {
            float angleRadians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians));
        }

        // Налаштування параметрів
        public static void SetRayCount(int count)
        {
            _rayCount = Mathf.Max(4, count); // Мінімум 4 промені
        }

        public static void SetMaxDistance(float distance)
        {
            _maxRayDistance = Mathf.Max(1f, distance);
        }

        public static void SetObstacleLayerMask(LayerMask mask)
        {
            _obstacleLayerMask = mask;
        }

        public static int GetRayCount() => _rayCount;
        public static float GetMaxDistance() => _maxRayDistance;
    }
}
using System.Collections.Generic;
using UnityEngine;

namespace SilksongNeuralNetwork
{
    public class DebugTools : MonoBehaviour
    {
        private static DebugTools _instance;

        public static DebugTools Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<DebugTools>();

                    if (_instance == null)
                    {
                        GameObject obj = new GameObject("DebugToolsManager");
                        _instance = obj.AddComponent<DebugTools>();
                    }
                }
                return _instance;
            }
        }

        public bool Visible = true;

        // Лінії для відображення шляхів до ворогів
        private List<LineRenderer> _enemyLineRenderers = new List<LineRenderer>();
        private bool _enemyLinesInitialized = false;

        // Лінії для відображення променів-сенсорів (для перешкод)
        private List<LineRenderer> _obstacleRaySensorRenderers = new List<LineRenderer>();
        private List<GameObject> _obstacleRaySensorCircles = new List<GameObject>();
        private bool _obstacleRaySensorsInitialized = false;

        // Лінії для відображення променів-сенсорів (для ворогів)
        private List<LineRenderer> _enemyRaySensorRenderers = new List<LineRenderer>();
        private List<GameObject> _enemyRaySensorCircles = new List<GameObject>();
        private bool _enemyRaySensorsInitialized = false;

        private List<LineRenderer> _projectileRaySensorRenderers = new List<LineRenderer>();
        private List<GameObject> _projectileRaySensorCircles = new List<GameObject>();
        private bool _projectileRaySensorsInitialized = false;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        // ========== ENEMY LINES (старий функціонал) ==========

        private void InitializeEnemyLinePool(int count)
        {
            if (_enemyLinesInitialized) return;

            var material = new Material(Shader.Find("Hidden/Internal-Colored"));
            material.hideFlags = HideFlags.HideAndDontSave;

            for (int i = 0; i < count; i++)
            {
                GameObject lineObject = new GameObject($"EnemyDebugLine_{i}");
                DontDestroyOnLoad(lineObject);
                LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();

                lineRenderer.material = material;
                lineRenderer.startColor = Color.red;
                lineRenderer.endColor = new Color(1, 0.2f, 0.2f, 0.5f);
                lineRenderer.startWidth = 0.05f;
                lineRenderer.endWidth = 0.02f;
                lineRenderer.positionCount = 2;
                lineRenderer.sortingOrder = 10;

                _enemyLineRenderers.Add(lineRenderer);
                lineObject.SetActive(false);
            }

            _enemyLinesInitialized = true;
        }

        public void DrawLinesToTargets(Vector3 startPoint, List<Vector3> endPoints)
        {
            if (!Visible)
            {
                HideEnemyLines();
                return;
            }

            if (!_enemyLinesInitialized || _enemyLineRenderers.Count < endPoints.Count)
            {
                InitializeEnemyLinePool(endPoints.Count > 0 ? endPoints.Count : 5);
            }

            for (int i = 0; i < _enemyLineRenderers.Count; i++)
            {
                if (i < endPoints.Count)
                {
                    LineRenderer line = _enemyLineRenderers[i];
                    if (!line.gameObject.activeSelf) line.gameObject.SetActive(true);
                    line.SetPosition(0, startPoint);
                    line.SetPosition(1, endPoints[i]);
                }
                else
                {
                    if (_enemyLineRenderers[i].gameObject.activeSelf)
                        _enemyLineRenderers[i].gameObject.SetActive(false);
                }
            }
        }

        private void HideEnemyLines()
        {
            if (!_enemyLinesInitialized) return;

            foreach (var line in _enemyLineRenderers)
            {
                if (line != null && line.gameObject.activeSelf)
                {
                    line.gameObject.SetActive(false);
                }
            }
        }

        // ========== RAY SENSORS (універсальний метод) ==========

        private GameObject CreateCircle(string name, float radius, int segments = 16)
        {
            GameObject circleObj = new GameObject(name);
            DontDestroyOnLoad(circleObj);
            LineRenderer lr = circleObj.AddComponent<LineRenderer>();

            var material = new Material(Shader.Find("Hidden/Internal-Colored"));
            material.hideFlags = HideFlags.HideAndDontSave;

            lr.material = material;
            lr.startWidth = 0.02f;
            lr.endWidth = 0.02f;
            lr.positionCount = segments + 1;
            lr.useWorldSpace = false;
            lr.sortingOrder = 11;
            lr.loop = true;

            // Створюємо точки кола
            float angle = 0f;
            for (int i = 0; i <= segments; i++)
            {
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;
                lr.SetPosition(i, new Vector3(x, y, 0));
                angle += (2f * Mathf.PI) / segments;
            }

            circleObj.SetActive(false);
            return circleObj;
        }

        private void InitializeProjectileRaySensorPool(int count)
        {
            if (_projectileRaySensorsInitialized) return;

            var material = new Material(Shader.Find("Hidden/Internal-Colored"));
            material.hideFlags = HideFlags.HideAndDontSave;

            for (int i = 0; i < count; i++)
            {
                // Створюємо лінію променя
                GameObject lineObject = new GameObject($"ProjectileRaySensor_{i}");
                DontDestroyOnLoad(lineObject);
                LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();

                lineRenderer.material = material;
                lineRenderer.startWidth = 0.03f;
                lineRenderer.endWidth = 0.01f;
                lineRenderer.positionCount = 2;
                lineRenderer.sortingOrder = 9;

                _projectileRaySensorRenderers.Add(lineRenderer);
                lineObject.SetActive(false);

                // Створюємо кружечок для точки зіткнення
                GameObject circle = CreateCircle($"ProjectileRaySensorCircle_{i}", 0.07f);
                _projectileRaySensorCircles.Add(circle);
            }

            _projectileRaySensorsInitialized = true;
        }

        private void InitializeObstacleRaySensorPool(int count)
        {
            if (_obstacleRaySensorsInitialized) return;

            var material = new Material(Shader.Find("Hidden/Internal-Colored"));
            material.hideFlags = HideFlags.HideAndDontSave;

            for (int i = 0; i < count; i++)
            {
                // Створюємо лінію променя
                GameObject lineObject = new GameObject($"ObstacleRaySensor_{i}");
                DontDestroyOnLoad(lineObject);
                LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();

                lineRenderer.material = material;
                lineRenderer.startWidth = 0.03f;
                lineRenderer.endWidth = 0.01f;
                lineRenderer.positionCount = 2;
                lineRenderer.sortingOrder = 9;

                _obstacleRaySensorRenderers.Add(lineRenderer);
                lineObject.SetActive(false);

                // Створюємо кружечок для точки зіткнення
                GameObject circle = CreateCircle($"ObstacleRaySensorCircle_{i}", 0.08f);
                _obstacleRaySensorCircles.Add(circle);
            }

            _obstacleRaySensorsInitialized = true;
        }

        private void InitializeEnemyRaySensorPool(int count)
        {
            if (_enemyRaySensorsInitialized) return;

            var material = new Material(Shader.Find("Hidden/Internal-Colored"));
            material.hideFlags = HideFlags.HideAndDontSave;

            for (int i = 0; i < count; i++)
            {
                // Створюємо лінію променя
                GameObject lineObject = new GameObject($"EnemyRaySensor_{i}");
                DontDestroyOnLoad(lineObject);
                LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();

                lineRenderer.material = material;
                lineRenderer.startWidth = 0.03f;
                lineRenderer.endWidth = 0.01f;
                lineRenderer.positionCount = 2;
                lineRenderer.sortingOrder = 9;

                _enemyRaySensorRenderers.Add(lineRenderer);
                lineObject.SetActive(false);

                // Створюємо кружечок для точки зіткнення
                GameObject circle = CreateCircle($"EnemyRaySensorCircle_{i}", 0.1f);
                _enemyRaySensorCircles.Add(circle);
            }

            _enemyRaySensorsInitialized = true;
        }

        public void DrawRaySensors(Vector2 origin, List<RaySensorData> sensors, float maxDistance, RaySensorType sensorType)
        {
            if (!Visible)
            {
                HideRaySensors(sensorType);
                return;
            }

            // Вибираємо правильні списки залежно від типу сенсора
            List<LineRenderer> renderers;
            List<GameObject> circles;
            Color primaryColor, secondaryColor;

            if (sensorType == RaySensorType.Obstacles)
            {
                if (!_obstacleRaySensorsInitialized || _obstacleRaySensorRenderers.Count < sensors.Count)
                {
                    InitializeObstacleRaySensorPool(sensors.Count);
                }
                renderers = _obstacleRaySensorRenderers;
                circles = _obstacleRaySensorCircles;

                // Фіолетовий колір для перешкод
                primaryColor = new Color(0.6f, 0.2f, 0.8f, 0.9f);
                secondaryColor = new Color(0.6f, 0.2f, 0.8f, 0.3f);
            }
            else if (sensorType == RaySensorType.Enemies)
            {
                if (!_enemyRaySensorsInitialized || _enemyRaySensorRenderers.Count < sensors.Count)
                {
                    InitializeEnemyRaySensorPool(sensors.Count);
                }
                renderers = _enemyRaySensorRenderers;
                circles = _enemyRaySensorCircles;

                // Синій колір для ворогів
                primaryColor = new Color(0.2f, 0.5f, 1f, 0.9f);
                secondaryColor = new Color(0.2f, 0.5f, 1f, 0.3f);
            }
            else // RaySensorType.EnemiesProjectiles
            {
                if (!_enemyRaySensorsInitialized || _enemyRaySensorRenderers.Count < sensors.Count)
                {
                    InitializeProjectileRaySensorPool(sensors.Count);
                }
                renderers = _enemyRaySensorRenderers;
                circles = _enemyRaySensorCircles;

                // Яскраво-жовтий колір для снарядів
                primaryColor = new Color(1f, 1f, 0f, 0.9f);
                secondaryColor = new Color(1f, 1f, 0f, 0.3f);
            }


            for (int i = 0; i < renderers.Count; i++)
            {
                if (i < sensors.Count)
                {
                    LineRenderer line = renderers[i];
                    GameObject circle = circles[i];
                    RaySensorData sensor = sensors[i];

                    if (!line.gameObject.activeSelf)
                        line.gameObject.SetActive(true);

                    line.startColor = primaryColor;
                    line.endColor = secondaryColor;

                    // Малюємо лінію
                    line.SetPosition(0, origin);
                    line.SetPosition(1, sensor.hitPoint);

                    // Показуємо кружечок тільки якщо є зіткнення
                    if (sensor.hitDetected)
                    {
                        if (!circle.activeSelf)
                            circle.SetActive(true);

                        circle.transform.position = sensor.hitPoint;

                        // Колір кружечка залежить від відстані
                        LineRenderer circleLR = circle.GetComponent<LineRenderer>();
                        float t = sensor.normalizedDistance;
                        Color circleColor;

                        if (sensorType == RaySensorType.Obstacles)
                        {
                            // Для перешкод: жовтий -> червоний
                            circleColor = Color.Lerp(
                                new Color(1f, 1f, 0f, 1f),   // Жовтий (далеко)
                                new Color(1f, 0f, 0f, 1f),   // Червоний (близько)
                                t
                            );
                        }
                        else
                        {
                            // Для ворогів: блакитний -> темно-синій
                            circleColor = Color.Lerp(
                                new Color(0.5f, 0.8f, 1f, 1f),   // Світло-блакитний (далеко)
                                new Color(0f, 0f, 0.8f, 1f),     // Темно-синій (близько)
                                t
                            );
                        }

                        circleLR.startColor = circleColor;
                        circleLR.endColor = circleColor;
                    }
                    else
                    {
                        if (circle.activeSelf)
                            circle.SetActive(false);
                    }
                }
                else
                {
                    if (renderers[i].gameObject.activeSelf)
                        renderers[i].gameObject.SetActive(false);
                    if (circles[i].activeSelf)
                        circles[i].SetActive(false);
                }
            }
        }

        private void HideRaySensors(RaySensorType sensorType)
        {
            List<LineRenderer> renderers;
            List<GameObject> circles;
            bool initialized;

            if (sensorType == RaySensorType.Obstacles)
            {
                renderers = _obstacleRaySensorRenderers;
                circles = _obstacleRaySensorCircles;
                initialized = _obstacleRaySensorsInitialized;
            }
            else
            {
                renderers = _enemyRaySensorRenderers;
                circles = _enemyRaySensorCircles;
                initialized = _enemyRaySensorsInitialized;
            }

            if (!initialized) return;

            foreach (var line in renderers)
            {
                if (line != null && line.gameObject.activeSelf)
                {
                    line.gameObject.SetActive(false);
                }
            }

            foreach (var circle in circles)
            {
                if (circle != null && circle.activeSelf)
                {
                    circle.SetActive(false);
                }
            }
        }

        // ========== ЗАГАЛЬНІ МЕТОДИ ==========

        public void HideAllLines()
        {
            HideEnemyLines();
            HideRaySensors(RaySensorType.Obstacles);
            HideRaySensors(RaySensorType.Enemies);
        }
    }
}
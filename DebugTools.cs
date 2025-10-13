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

        // Лінії для відображення променів-сенсорів
        private List<LineRenderer> _raySensorRenderers = new List<LineRenderer>();
        private List<GameObject> _raySensorCircles = new List<GameObject>();
        private bool _raySensorsInitialized = false;

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

        // ========== RAY SENSORS (новий функціонал) ==========

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

        private void InitializeRaySensorPool(int count)
        {
            if (_raySensorsInitialized) return;

            var material = new Material(Shader.Find("Hidden/Internal-Colored"));
            material.hideFlags = HideFlags.HideAndDontSave;

            for (int i = 0; i < count; i++)
            {
                // Створюємо лінію променя
                GameObject lineObject = new GameObject($"RaySensor_{i}");
                DontDestroyOnLoad(lineObject);
                LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();

                lineRenderer.material = material;
                lineRenderer.startWidth = 0.03f;
                lineRenderer.endWidth = 0.01f;
                lineRenderer.positionCount = 2;
                lineRenderer.sortingOrder = 9;

                _raySensorRenderers.Add(lineRenderer);
                lineObject.SetActive(false);

                // Створюємо кружечок для точки зіткнення
                GameObject circle = CreateCircle($"RaySensorCircle_{i}", 0.08f);
                _raySensorCircles.Add(circle);
            }

            _raySensorsInitialized = true;
        }

        public void DrawRaySensors(Vector2 origin, List<RaySensorData> sensors, float maxDistance)
        {
            if (!Visible)
            {
                HideRaySensors();
                return;
            }

            if (!_raySensorsInitialized || _raySensorRenderers.Count < sensors.Count)
            {
                InitializeRaySensorPool(sensors.Count);
            }

            for (int i = 0; i < _raySensorRenderers.Count; i++)
            {
                if (i < sensors.Count)
                {
                    LineRenderer line = _raySensorRenderers[i];
                    GameObject circle = _raySensorCircles[i];
                    RaySensorData sensor = sensors[i];

                    if (!line.gameObject.activeSelf)
                        line.gameObject.SetActive(true);

                    // Фіолетовий колір для променів
                    Color purpleColor = new Color(0.6f, 0.2f, 0.8f, 0.9f); // Насичений фіолетовий
                    Color purpleColorEnd = new Color(0.6f, 0.2f, 0.8f, 0.3f); // Прозорий фіолетовий

                    line.startColor = purpleColor;
                    line.endColor = purpleColorEnd;

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
                        Color circleColor = Color.Lerp(
                            new Color(1f, 1f, 0f, 1f),   // Жовтий (далеко)
                            new Color(1f, 0f, 0f, 1f),   // Червоний (близько)
                            t
                        );
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
                    if (_raySensorRenderers[i].gameObject.activeSelf)
                        _raySensorRenderers[i].gameObject.SetActive(false);
                    if (_raySensorCircles[i].activeSelf)
                        _raySensorCircles[i].SetActive(false);
                }
            }
        }

        private void HideRaySensors()
        {
            if (!_raySensorsInitialized) return;

            foreach (var line in _raySensorRenderers)
            {
                if (line != null && line.gameObject.activeSelf)
                {
                    line.gameObject.SetActive(false);
                }
            }

            foreach (var circle in _raySensorCircles)
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
            HideRaySensors();
        }
    }
}
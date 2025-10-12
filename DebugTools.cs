using System.Collections.Generic;
using UnityEngine;

namespace SilksongNeuralNetwork
{
    public class DebugTools : MonoBehaviour
    {
        // Робимо приватну статичну змінну для зберігання екземпляра
        private static DebugTools _instance;

        // Створюємо публічну статичну властивість для доступу
        public static DebugTools Instance
        {
            get
            {
                // Якщо екземпляр ще не існує...
                if (_instance == null)
                {
                    // ...спробуємо знайти його на сцені
                    _instance = FindObjectOfType<DebugTools>();

                    // Якщо на сцені його немає, створимо його самі
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject("DebugToolsManager");
                        _instance = obj.AddComponent<DebugTools>();
                    }
                }
                // Повертаємо гарантовано існуючий екземпляр
                return _instance;
            }
        }

        public bool Visible = true;

        private List<LineRenderer> _lineRenderers = new List<LineRenderer>();
        private bool _isInitialized = false;

        // Метод Awake тепер відповідає за те, щоб не було дублікатів
        private void Awake()
        {
            // Якщо _instance ще не встановлено, то цей об'єкт стає головним
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            // Якщо _instance вже існує, але це не цей об'єкт, то цей об'єкт - дублікат і його треба знищити
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        // ... (решта методів: InitializeLinePool, DrawLinesToTargets, HideAllLines залишаються без змін) ...
        private void InitializeLinePool(int count)
        {
            if (_isInitialized) return;

            var material = new Material(Shader.Find("Hidden/Internal-Colored"));
            material.hideFlags = HideFlags.HideAndDontSave;

            for (int i = 0; i < count; i++)
            {
                GameObject lineObject = new GameObject($"DebugLine_{i}");
                DontDestroyOnLoad(lineObject);
                LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();

                lineRenderer.material = material;
                lineRenderer.startColor = Color.red;
                lineRenderer.endColor = new Color(1, 0.2f, 0.2f, 0.5f);
                lineRenderer.startWidth = 0.05f;
                lineRenderer.endWidth = 0.02f;
                lineRenderer.positionCount = 2;
                lineRenderer.sortingOrder = 10;

                _lineRenderers.Add(lineRenderer);
                lineObject.SetActive(false);
            }

            _isInitialized = true;
        }

        public void DrawLinesToTargets(Vector3 startPoint, List<Vector3> endPoints)
        {
            if (!Visible)
            {
                HideAllLines();
                return;
            }

            // Робимо пул достатньо великим, якщо раптом цілей стало більше
            if (!_isInitialized || _lineRenderers.Count < endPoints.Count)
            {
                InitializeLinePool(endPoints.Count > 0 ? endPoints.Count : 5);
            }

            for (int i = 0; i < _lineRenderers.Count; i++)
            {
                if (i < endPoints.Count)
                {
                    LineRenderer line = _lineRenderers[i];
                    if (!line.gameObject.activeSelf) line.gameObject.SetActive(true);
                    line.SetPosition(0, startPoint);
                    line.SetPosition(1, endPoints[i]);
                }
                else
                {
                    if (_lineRenderers[i].gameObject.activeSelf) _lineRenderers[i].gameObject.SetActive(false);
                }
            }
        }

        public void HideAllLines()
        {
            if (!_isInitialized) return;

            foreach (var line in _lineRenderers)
            {
                if (line != null && line.gameObject.activeSelf)
                {
                    line.gameObject.SetActive(false);
                }
            }
        }
    }
}
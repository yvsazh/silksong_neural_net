using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SilksongNeuralNetwork
{
    public class SceneBounds : MonoBehaviour
    {
        public static SceneBounds Instance { get; private set; }

        public float minX, maxX, minY, maxY;

        void Awake()
        {
            // Єдиний екземпляр
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this) // Якщо вже існує інший екземпляр, видаляємо цей
            {
                Destroy(gameObject);
                return; // Важливо вийти, щоб уникнути подальшого коду для цього об'єкта
            }
        }

        // Метод, який рахує межі сцени
        public void UpdateBounds()
        {
            // FindObjectsByType це новий метод в Unity 2022+, для старіших версій (як Hollow Knight) використовуйте FindObjectsOfType
            // Замість FindObjectsByType використовуємо FindObjectsOfType
            Collider2D[] colliders = UnityEngine.Object.FindObjectsOfType<Collider2D>(); // Без FindObjectsSortMode.None для старіших версій

            minX = float.MaxValue;
            maxX = float.MinValue;
            minY = float.MaxValue;
            maxY = float.MinValue;

            // Збираємо лише colliders, які мають Layer "Terrain" або "Environment"
            // Це допоможе уникнути включення коллайдерів ворогів, гравця, снарядів тощо.
            // Вам потрібно буде перевірити, які саме шари використовуються в Hollow Knight для статичних об'єктів сцени.
            // Зазвичай це Layer 8, 9, 10 або інше, залежить від гри.
            // Можливо, вам буде достатньо просто ігнорувати коллайдери з isTrigger = true.
            int environmentLayer = LayerMask.NameToLayer("Environment"); // Приклад, можливо, потрібно інше ім'я шару
            int terrainLayer = LayerMask.NameToLayer("Terrain"); // Приклад

            bool foundAnyCollider = false; // Додаємо прапорець, щоб перевірити, чи знайшли ми взагалі якісь коллайдери

            foreach (var col in colliders)
            {
                // Пропускаємо тригери, бо вони зазвичай не є фізичними межами сцени
                if (col.isTrigger) continue;

                // Можна також фільтрувати за шарами, якщо це необхідно
                // if (col.gameObject.layer == environmentLayer || col.gameObject.layer == terrainLayer)
                // { ... }

                minX = Mathf.Min(minX, col.bounds.min.x);
                maxX = Mathf.Max(maxX, col.bounds.max.x);
                minY = Mathf.Min(minY, col.bounds.min.y);
                maxY = Mathf.Max(maxY, col.bounds.max.y);
                foundAnyCollider = true;
            }
        }
    }
}
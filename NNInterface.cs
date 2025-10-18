using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SilksongNeuralNetwork
{
    public class NNInterface : MonoBehaviour
    {
        // UI стан
        private bool _showSaveDialog = false;
        private bool _showLoadDialog = false;
        private string _modelNameInput = "";
        private Vector2 _scrollPosition = Vector2.zero;

        // UI стилі
        private GUIStyle _backgroundStyle;
        private GUIStyle _overlayStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _textFieldStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _modeTextStyle;
        private GUIStyle _scrollViewStyle;
        private bool _stylesInitialized = false;

        private string _modeText = "Тренування вимкнене";

        // Для фокусу на TextField
        private string _textFieldControlName = "ModelNameField";
        private bool _shouldFocusTextField = false;

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            // Напівпрозорий оверлей на весь екран
            _overlayStyle = new GUIStyle();
            _overlayStyle.normal.background = MakeTexture(2, 2, new Color(0f, 0f, 0f, 0.7f));

            // Фон діалогу (непрозорий)
            _backgroundStyle = new GUIStyle(GUI.skin.box);
            _backgroundStyle.normal.background = MakeTexture(2, 2, new Color(0.15f, 0.15f, 0.2f, 1f));
            _backgroundStyle.border = new RectOffset(8, 8, 8, 8);
            _backgroundStyle.padding = new RectOffset(20, 20, 20, 20);

            // Заголовок
            _titleStyle = new GUIStyle(GUI.skin.label);
            _titleStyle.fontSize = 24;
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.normal.textColor = new Color(1f, 0.95f, 0.8f);
            _titleStyle.alignment = TextAnchor.MiddleCenter;
            _titleStyle.padding = new RectOffset(0, 0, 10, 15);

            // Кнопка
            _buttonStyle = new GUIStyle(GUI.skin.button);
            _buttonStyle.fontSize = 16;
            _buttonStyle.fontStyle = FontStyle.Bold;
            _buttonStyle.normal.background = MakeTexture(2, 2, new Color(0.3f, 0.27f, 0.25f, 1f));
            _buttonStyle.hover.background = MakeTexture(2, 2, new Color(0.4f, 0.37f, 0.33f, 1f));
            _buttonStyle.active.background = MakeTexture(2, 2, new Color(0.5f, 0.47f, 0.4f, 1f));
            _buttonStyle.normal.textColor = new Color(0.95f, 0.9f, 0.8f);
            _buttonStyle.hover.textColor = Color.white;
            _buttonStyle.padding = new RectOffset(12, 12, 10, 10);
            _buttonStyle.margin = new RectOffset(5, 5, 5, 5);
            _buttonStyle.border = new RectOffset(4, 4, 4, 4);

            // Текстове поле
            _textFieldStyle = new GUIStyle(GUI.skin.textField);
            _textFieldStyle.fontSize = 18;
            _textFieldStyle.normal.background = MakeTexture(2, 2, new Color(0.2f, 0.2f, 0.25f, 1f));
            _textFieldStyle.normal.textColor = Color.white;
            _textFieldStyle.focused.background = MakeTexture(2, 2, new Color(0.25f, 0.25f, 0.3f, 1f));
            _textFieldStyle.focused.textColor = Color.white;
            _textFieldStyle.hover.background = MakeTexture(2, 2, new Color(0.22f, 0.22f, 0.27f, 1f));
            _textFieldStyle.padding = new RectOffset(10, 10, 8, 8);
            _textFieldStyle.margin = new RectOffset(0, 0, 5, 10);
            _textFieldStyle.border = new RectOffset(4, 4, 4, 4);

            // Лейбл
            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 16;
            _labelStyle.normal.textColor = new Color(0.9f, 0.85f, 0.75f);
            _labelStyle.padding = new RectOffset(0, 0, 5, 5);

            // Текст режиму
            _modeTextStyle = new GUIStyle(GUI.skin.label);
            _modeTextStyle.fontSize = 16;
            _modeTextStyle.fontStyle = FontStyle.Bold;
            _modeTextStyle.normal.textColor = new Color(1f, 0.95f, 0.8f);
            _modeTextStyle.alignment = TextAnchor.LowerRight;
            _modeTextStyle.padding = new RectOffset(0, 15, 0, 15);

            // ScrollView
            _scrollViewStyle = new GUIStyle(GUI.skin.scrollView);
            _scrollViewStyle.padding = new RectOffset(5, 5, 5, 5);

            _stylesInitialized = true;
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;

            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private void OnGUI()
        {
            InitializeStyles();

            // Відображення поточного режиму (тільки якщо немає діалогів)
            if (!_showSaveDialog && !_showLoadDialog)
            {
                DrawModeIndicator();
            }

            // Діалог збереження
            if (_showSaveDialog)
            {
                DrawOverlay();
                DrawSaveDialog();
            }

            // Діалог завантаження
            if (_showLoadDialog)
            {
                DrawOverlay();
                DrawLoadDialog();
            }
        }

        private void DrawOverlay()
        {
            // Напівпрозорий чорний фон на весь екран
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", _overlayStyle);
        }

        private void DrawModeIndicator()
        {
            float width = 280f;
            float height = 50f;
            Rect modeRect = new Rect(Screen.width - width - 20, Screen.height - height - 20, width, height);

            // Невеликий фон для тексту режиму
            GUI.Box(modeRect, "", _backgroundStyle);
            GUI.Label(modeRect, _modeText, _modeTextStyle);
        }

        private void DrawSaveDialog()
        {
            // Розблоковуємо курсор
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            float dialogWidth = 550f;
            float dialogHeight = 280f;
            Rect dialogRect = new Rect(
                (Screen.width - dialogWidth) / 2,
                (Screen.height - dialogHeight) / 2,
                dialogWidth,
                dialogHeight
            );

            GUI.Box(dialogRect, "", _backgroundStyle);

            GUILayout.BeginArea(new Rect(dialogRect.x, dialogRect.y, dialogRect.width, dialogRect.height));
            GUILayout.BeginVertical();

            // Заголовок
            GUILayout.Label("Зберегти модель", _titleStyle);
            GUILayout.Space(15);

            // Поле введення
            GUILayout.Label("Назва моделі:", _labelStyle);
            GUILayout.Space(5);

            GUI.SetNextControlName(_textFieldControlName);
            _modelNameInput = GUILayout.TextField(_modelNameInput, 50, _textFieldStyle, GUILayout.Height(40));

            // Автоматичний фокус на TextField при першому відкритті
            if (_shouldFocusTextField)
            {
                GUI.FocusControl(_textFieldControlName);
                _shouldFocusTextField = false;
            }

            // Enter для збереження
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                if (!string.IsNullOrWhiteSpace(_modelNameInput))
                {
                    SaveAndClose();
                }
                Event.current.Use();
            }

            // Escape для скасування
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                CancelAndClose();
                Event.current.Use();
            }

            GUILayout.Space(25);

            // Кнопки
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Зберегти", _buttonStyle, GUILayout.Width(160), GUILayout.Height(45)))
            {
                SaveAndClose();
            }

            GUILayout.Space(15);

            if (GUILayout.Button("Скасувати", _buttonStyle, GUILayout.Width(160), GUILayout.Height(45)))
            {
                CancelAndClose();
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawLoadDialog()
        {
            // Розблоковуємо курсор
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            float dialogWidth = 650f;
            float dialogHeight = 550f;
            Rect dialogRect = new Rect(
                (Screen.width - dialogWidth) / 2,
                (Screen.height - dialogHeight) / 2,
                dialogWidth,
                dialogHeight
            );

            GUI.Box(dialogRect, "", _backgroundStyle);

            GUILayout.BeginArea(new Rect(dialogRect.x, dialogRect.y, dialogRect.width, dialogRect.height));
            GUILayout.BeginVertical();

            // Заголовок
            GUILayout.Label("Завантажити модель", _titleStyle);
            GUILayout.Space(15);

            // Поточна модель
            GUILayout.Label($"Поточна модель: {Agent.Instance.GetCurrentModelName()}", _labelStyle);
            GUILayout.Space(15);

            // Escape для закриття
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                CancelAndClose();
                Event.current.Use();
            }

            // Список моделей
            string[] modelFiles = GetModelFiles();

            if (modelFiles.Length == 0)
            {
                GUILayout.Space(20);
                GUILayout.Label("Моделі не знайдені", _labelStyle);
            }
            else
            {
                _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, _scrollViewStyle, GUILayout.ExpandHeight(true));

                foreach (string modelFile in modelFiles)
                {
                    string modelName = Path.GetFileNameWithoutExtension(modelFile);
                    FileInfo fileInfo = new FileInfo(modelFile);
                    string dateStr = fileInfo.LastWriteTime.ToString("dd.MM.yyyy HH:mm");

                    string buttonText = $"{modelName}\n({dateStr})";

                    if (GUILayout.Button(buttonText, _buttonStyle, GUILayout.Height(55)))
                    {
                        Agent.Instance.LoadModel(modelName);
                        _showLoadDialog = false;
                        RestoreCursor();
                    }

                    GUILayout.Space(8);
                }

                GUILayout.EndScrollView();
            }

            GUILayout.Space(15);

            // Кнопка закриття
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Закрити", _buttonStyle, GUILayout.Width(180), GUILayout.Height(45)))
            {
                CancelAndClose();
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private string[] GetModelFiles()
        {
            string modelsPath = Agent.ModelsPath;

            if (!Directory.Exists(modelsPath))
            {
                return new string[0];
            }

            var files = Directory.GetFiles(modelsPath, "*.bin")
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .ToArray();

            return files;
        }

        private void RestoreCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void SaveAndClose()
        {
            string trimmedName = _modelNameInput.Trim();
            if (!string.IsNullOrEmpty(trimmedName))
            {
                Agent.Instance.SaveModel(trimmedName);
                _showSaveDialog = false;
                RestoreCursor();
            }
        }

        private void CancelAndClose()
        {
            _showSaveDialog = false;
            _showLoadDialog = false;
            RestoreCursor();
        }

        public void ShowSaveDialog()
        {
            _showSaveDialog = true;
            _modelNameInput = "";
            _shouldFocusTextField = true;
        }

        public void ShowLoadDialog()
        {
            _showLoadDialog = true;
            _scrollPosition = Vector2.zero;
        }

        public void UpdateModeText(AgentMode mode)
        {
            switch (mode)
            {
                case AgentMode.Disabled:
                    _modeText = "Тренування вимкнене";
                    break;
                case AgentMode.Training:
                    _modeText = "Тренування активне";
                    break;
                case AgentMode.Inference:
                    _modeText = "Нейромережа грає";
                    break;
            }
        }

        // Метод для блокування Input гри коли відкрито діалог
        public bool IsDialogOpen()
        {
            return _showSaveDialog || _showLoadDialog;
        }
    }
}
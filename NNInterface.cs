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

            // Заголовок
            _titleStyle = new GUIStyle(GUI.skin.label);
            _titleStyle.fontSize = 24;
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.normal.textColor = Color.white;
            _titleStyle.alignment = TextAnchor.MiddleCenter;
            _titleStyle.padding = new RectOffset(0, 0, 10, 15);

            // Кнопка
            _buttonStyle = new GUIStyle(GUI.skin.button);
            _buttonStyle.fontSize = 16;
            _buttonStyle.fontStyle = FontStyle.Bold;
            _buttonStyle.normal.textColor = Color.white;
            _buttonStyle.hover.textColor = Color.yellow;
            _buttonStyle.padding = new RectOffset(12, 12, 10, 10);
            _buttonStyle.margin = new RectOffset(5, 5, 5, 5);

            // Текстове поле
            _textFieldStyle = new GUIStyle(GUI.skin.textField);
            _textFieldStyle.fontSize = 18;
            _textFieldStyle.normal.textColor = Color.white;
            _textFieldStyle.focused.textColor = Color.white;
            _textFieldStyle.padding = new RectOffset(10, 10, 8, 8);
            _textFieldStyle.margin = new RectOffset(0, 0, 5, 10);

            // Лейбл
            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 16;
            _labelStyle.normal.textColor = Color.white;
            _labelStyle.padding = new RectOffset(0, 0, 5, 5);

            // Текст режиму
            _modeTextStyle = new GUIStyle(GUI.skin.label);
            _modeTextStyle.fontSize = 16;
            _modeTextStyle.fontStyle = FontStyle.Bold;
            _modeTextStyle.normal.textColor = Color.yellow;
            _modeTextStyle.alignment = TextAnchor.LowerRight;
            _modeTextStyle.padding = new RectOffset(0, 15, 0, 15);

            // ScrollView
            _scrollViewStyle = new GUIStyle(GUI.skin.scrollView);
            _scrollViewStyle.padding = new RectOffset(5, 5, 5, 5);

            _stylesInitialized = true;
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
            // Малюємо напівпрозорий чорний прямокутник на весь екран
            Color oldColor = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.8f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private void DrawModeIndicator()
        {
            float width = 280f;
            float height = 50f;
            float padding = 10f;
            Rect modeRect = new Rect(Screen.width - width - 20, Screen.height - height - 20, width, height);

            // Фон для індикатора режиму
            Color oldColor = GUI.color;
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
            GUI.DrawTexture(modeRect, Texture2D.whiteTexture);
            GUI.color = oldColor;

            GUI.Label(modeRect, _modeText, _modeTextStyle);
        }

        private void DrawSaveDialog()
        {
            // Розблоковуємо курсор
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            float dialogWidth = 550f;
            float dialogHeight = 280f;
            float padding = 20f;

            Rect dialogRect = new Rect(
                (Screen.width - dialogWidth) / 2,
                (Screen.height - dialogHeight) / 2,
                dialogWidth,
                dialogHeight
            );

            // Малюємо фон діалогу
            DrawDialogBackground(dialogRect);

            // Внутрішня область з відступами
            Rect contentRect = new Rect(
                dialogRect.x + padding,
                dialogRect.y + padding,
                dialogRect.width - padding * 2,
                dialogRect.height - padding * 2
            );

            GUILayout.BeginArea(contentRect);
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
            float padding = 20f;

            Rect dialogRect = new Rect(
                (Screen.width - dialogWidth) / 2,
                (Screen.height - dialogHeight) / 2,
                dialogWidth,
                dialogHeight
            );

            // Малюємо фон діалогу
            DrawDialogBackground(dialogRect);

            // Внутрішня область з відступами
            Rect contentRect = new Rect(
                dialogRect.x + padding,
                dialogRect.y + padding,
                dialogRect.width - padding * 2,
                dialogRect.height - padding * 2
            );

            GUILayout.BeginArea(contentRect);
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

            // Кнопки
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Відкрити папку", _buttonStyle, GUILayout.Width(180), GUILayout.Height(45)))
            {
                OpenModelsFolder();
            }

            GUILayout.Space(15);

            if (GUILayout.Button("Закрити", _buttonStyle, GUILayout.Width(180), GUILayout.Height(45)))
            {
                CancelAndClose();
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawDialogBackground(Rect rect)
        {
            // Темна рамка (бордер)
            Color oldColor = GUI.color;
            GUI.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            // Світліший внутрішній фон
            Rect innerRect = new Rect(rect.x + 3, rect.y + 3, rect.width - 6, rect.height - 6);
            GUI.color = new Color(0.15f, 0.15f, 0.2f, 1f);
            GUI.DrawTexture(innerRect, Texture2D.whiteTexture);

            GUI.color = oldColor;
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

        private void OpenModelsFolder()
        {
            string modelsPath = Agent.ModelsPath;

            // Створюємо папку якщо не існує
            if (!Directory.Exists(modelsPath))
            {
                Directory.CreateDirectory(modelsPath);
            }

            try
            {
                // Відкриваємо папку в провіднику
                System.Diagnostics.Process.Start(modelsPath);
            }
            catch (Exception ex)
            {
                // Якщо не вдалося відкрити стандартним способом, пробуємо альтернативні методи
                try
                {
                    if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
                    {
                        System.Diagnostics.Process.Start("explorer.exe", modelsPath);
                    }
                    else if (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor)
                    {
                        System.Diagnostics.Process.Start("open", modelsPath);
                    }
                    else if (Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxEditor)
                    {
                        System.Diagnostics.Process.Start("xdg-open", modelsPath);
                    }
                }
                catch
                {
                    Debug.LogError($"Не вдалося відкрити папку: {modelsPath}\nПомилка: {ex.Message}");
                }
            }
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
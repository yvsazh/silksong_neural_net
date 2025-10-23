using System;
using System.Collections;
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
        private GUIStyle _notificationStyle;
        private GUIStyle _modelNameStyle;
        private bool _stylesInitialized = false;

        private string _modeText = "";

        // Для фокусу на TextField
        private string _textFieldControlName = "ModelNameField";
        private bool _shouldFocusTextField = false;

        // Для відстеження зміни мови
        private TeamCherry.Localization.LanguageCode _lastLanguageCode;

        // Система сповіщень
        private string _notificationText = "";
        private float _notificationTimer = 0f;
        private const float NOTIFICATION_DURATION = 3f;

        private void Start()
        {
            UpdateLanguage();
        }

        private void Update()
        {
            // Перевіряємо зміну мови
            var currentLanguage = TeamCherry.Localization.Language.CurrentLanguage();
            if (currentLanguage != _lastLanguageCode)
            {
                UpdateLanguage();
            }

            // Оновлюємо таймер сповіщень
            if (_notificationTimer > 0f)
            {
                _notificationTimer -= Time.deltaTime;
            }
        }

        private void UpdateLanguage()
        {
            _lastLanguageCode = TeamCherry.Localization.Language.CurrentLanguage();
            UILocalization.SetLanguageFromCode(_lastLanguageCode);
            UpdateModeText(Agent.Instance._currentMode);
        }

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
            _modeTextStyle.alignment = TextAnchor.MiddleRight;
            _modeTextStyle.padding = new RectOffset(0, 10, 5, 5);

            // Назва моделі
            _modelNameStyle = new GUIStyle(GUI.skin.label);
            _modelNameStyle.fontSize = 14;
            _modelNameStyle.normal.textColor = new Color(0.7f, 0.9f, 1f);
            _modelNameStyle.alignment = TextAnchor.MiddleRight;
            _modelNameStyle.padding = new RectOffset(0, 10, 5, 5);

            // ScrollView
            _scrollViewStyle = new GUIStyle(GUI.skin.scrollView);
            _scrollViewStyle.padding = new RectOffset(5, 5, 5, 5);

            // Стиль для сповіщень
            _notificationStyle = new GUIStyle(GUI.skin.label);
            _notificationStyle.fontSize = 18;
            _notificationStyle.fontStyle = FontStyle.Bold;
            _notificationStyle.normal.textColor = Color.white;
            _notificationStyle.alignment = TextAnchor.MiddleCenter;
            _notificationStyle.padding = new RectOffset(15, 15, 10, 10);

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitializeStyles();

            // Відображення поточного режиму та моделі (тільки якщо немає діалогів)
            if (!_showSaveDialog && !_showLoadDialog)
            {
                DrawStatusPanel();
            }

            // Сповіщення
            if (_notificationTimer > 0f)
            {
                DrawNotification();
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

        private void DrawStatusPanel()
        {
            float width = 320f;
            float height = 80f;
            Rect panelRect = new Rect(Screen.width - width - 20, Screen.height - height - 20, width, height);

            // Фон для панелі
            Color oldColor = GUI.color;
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = oldColor;

            // Внутрішня область
            GUILayout.BeginArea(panelRect);
            GUILayout.BeginVertical();

            GUILayout.Space(8);

            // Режим
            GUILayout.Label(_modeText, _modeTextStyle);

            GUILayout.Space(3);

            // Назва моделі
            string modelName = Agent.Instance.GetCurrentModelName();
            string modelDisplayText = string.IsNullOrEmpty(modelName) || modelName == "Без назви"
                ? UILocalization.Get("model_untitled")
                : $"{UILocalization.Get("model_prefix")}{modelName}";

            GUILayout.Label(modelDisplayText, _modelNameStyle);

            GUILayout.Space(8);

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawNotification()
        {
            float width = 500f;
            float height = 70f;
            Rect notificationRect = new Rect(
                (Screen.width - width) / 2,
                Screen.height - height - 120,
                width,
                height
            );

            // Анімація появи/зникнення
            float alpha = 1f;
            if (_notificationTimer < 0.5f)
            {
                alpha = _notificationTimer / 0.5f; // Fade out
            }
            else if (_notificationTimer > NOTIFICATION_DURATION - 0.3f)
            {
                alpha = (NOTIFICATION_DURATION - _notificationTimer) / 0.3f; // Fade in
            }

            // Фон сповіщення
            Color oldColor = GUI.color;
            GUI.color = new Color(0.2f, 0.6f, 0.3f, 0.9f * alpha);
            GUI.DrawTexture(notificationRect, Texture2D.whiteTexture);

            // Бордер
            GUI.color = new Color(0.3f, 0.8f, 0.4f, alpha);
            DrawBorder(notificationRect, 3);

            // Текст
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(notificationRect, _notificationText, _notificationStyle);
            GUI.color = oldColor;
        }

        private void DrawBorder(Rect rect, int thickness)
        {
            // Верхня
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            // Нижня
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), Texture2D.whiteTexture);
            // Ліва
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            // Права
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        }

        private void DrawOverlay()
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.8f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private void DrawSaveDialog()
        {
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

            DrawDialogBackground(dialogRect);

            Rect contentRect = new Rect(
                dialogRect.x + padding,
                dialogRect.y + padding,
                dialogRect.width - padding * 2,
                dialogRect.height - padding * 2
            );

            GUILayout.BeginArea(contentRect);
            GUILayout.BeginVertical();

            GUILayout.Label(UILocalization.Get("save_title"), _titleStyle);
            GUILayout.Space(15);

            GUILayout.Label(UILocalization.Get("model_name_label"), _labelStyle);
            GUILayout.Space(5);

            GUI.SetNextControlName(_textFieldControlName);
            _modelNameInput = GUILayout.TextField(_modelNameInput, 50, _textFieldStyle, GUILayout.Height(40));

            if (_shouldFocusTextField)
            {
                GUI.FocusControl(_textFieldControlName);
                _shouldFocusTextField = false;
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                if (!string.IsNullOrWhiteSpace(_modelNameInput))
                {
                    SaveAndClose();
                }
                Event.current.Use();
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                CancelAndClose();
                Event.current.Use();
            }

            GUILayout.Space(25);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(UILocalization.Get("save_button"), _buttonStyle, GUILayout.Width(160), GUILayout.Height(45)))
            {
                SaveAndClose();
            }

            GUILayout.Space(15);

            if (GUILayout.Button(UILocalization.Get("cancel_button"), _buttonStyle, GUILayout.Width(160), GUILayout.Height(45)))
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

            DrawDialogBackground(dialogRect);

            Rect contentRect = new Rect(
                dialogRect.x + padding,
                dialogRect.y + padding,
                dialogRect.width - padding * 2,
                dialogRect.height - padding * 2
            );

            GUILayout.BeginArea(contentRect);
            GUILayout.BeginVertical();

            GUILayout.Label(UILocalization.Get("load_title"), _titleStyle);
            GUILayout.Space(15);

            GUILayout.Label($"{UILocalization.Get("current_model")} {Agent.Instance.GetCurrentModelName()}", _labelStyle);
            GUILayout.Space(15);

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                CancelAndClose();
                Event.current.Use();
            }

            string[] modelFiles = GetModelFiles();

            if (modelFiles.Length == 0)
            {
                GUILayout.Space(20);
                GUILayout.Label(UILocalization.Get("no_models"), _labelStyle);
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
                        ShowNotification(string.Format(UILocalization.Get("notification_loaded"), modelName));
                        _showLoadDialog = false;
                        RestoreCursor();
                    }

                    GUILayout.Space(8);
                }

                GUILayout.EndScrollView();
            }

            GUILayout.Space(15);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(UILocalization.Get("open_folder_button"), _buttonStyle, GUILayout.Width(180), GUILayout.Height(45)))
            {
                OpenModelsFolder();
            }

            GUILayout.Space(15);

            if (GUILayout.Button(UILocalization.Get("close_button"), _buttonStyle, GUILayout.Width(180), GUILayout.Height(45)))
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
            Color oldColor = GUI.color;
            GUI.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

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

            if (!Directory.Exists(modelsPath))
            {
                Directory.CreateDirectory(modelsPath);
            }

            try
            {
                System.Diagnostics.Process.Start(modelsPath);
            }
            catch (Exception ex)
            {
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
                ShowNotification(string.Format(UILocalization.Get("notification_saved"), trimmedName));
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

        public void ShowNotification(string message)
        {
            _notificationText = message;
            _notificationTimer = NOTIFICATION_DURATION;
        }

        public void UpdateModeText(AgentMode mode)
        {
            switch (mode)
            {
                case AgentMode.Disabled:
                    _modeText = UILocalization.Get("mode_disabled");
                    break;
                case AgentMode.Training:
                    _modeText = UILocalization.Get("mode_training");
                    break;
                case AgentMode.Inference:
                    _modeText = UILocalization.Get("mode_inference");
                    break;
            }
        }

        public bool IsDialogOpen()
        {
            return _showSaveDialog || _showLoadDialog;
        }
    }
}
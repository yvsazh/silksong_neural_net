using System.Collections.Generic;

namespace SilksongNeuralNetwork
{
    public static class UILocalization
    {
        // Словник з перекладами
        private static Dictionary<string, Dictionary<string, string>> _translations = new Dictionary<string, Dictionary<string, string>>
        {
            // Українська (для RU)
            ["UK"] = new Dictionary<string, string>
            {
                ["mode_disabled"] = "Тренування вимкнене",
                ["mode_training"] = "Тренування активне",
                ["mode_inference"] = "Нейромережа грає",
                ["save_title"] = "Зберегти модель",
                ["load_title"] = "Завантажити модель",
                ["model_name_label"] = "Назва моделі:",
                ["current_model"] = "Поточна модель:",
                ["no_models"] = "Моделі не знайдені",
                ["save_button"] = "Зберегти",
                ["cancel_button"] = "Скасувати",
                ["close_button"] = "Закрити",
                ["open_folder_button"] = "Відкрити папку"
            },

            // English
            ["EN"] = new Dictionary<string, string>
            {
                ["mode_disabled"] = "Training disabled",
                ["mode_training"] = "Training active",
                ["mode_inference"] = "Neural network playing",
                ["save_title"] = "Save Model",
                ["load_title"] = "Load Model",
                ["model_name_label"] = "Model name:",
                ["current_model"] = "Current model:",
                ["no_models"] = "No models found",
                ["save_button"] = "Save",
                ["cancel_button"] = "Cancel",
                ["close_button"] = "Close",
                ["open_folder_button"] = "Open Folder"
            },

            // Deutsch
            ["DE"] = new Dictionary<string, string>
            {
                ["mode_disabled"] = "Training deaktiviert",
                ["mode_training"] = "Training aktiv",
                ["mode_inference"] = "Neuronales Netz spielt",
                ["save_title"] = "Modell speichern",
                ["load_title"] = "Modell laden",
                ["model_name_label"] = "Modellname:",
                ["current_model"] = "Aktuelles Modell:",
                ["no_models"] = "Keine Modelle gefunden",
                ["save_button"] = "Speichern",
                ["cancel_button"] = "Abbrechen",
                ["close_button"] = "Schließen",
                ["open_folder_button"] = "Ordner öffnen"
            },

            // Español
            ["ES"] = new Dictionary<string, string>
            {
                ["mode_disabled"] = "Entrenamiento desactivado",
                ["mode_training"] = "Entrenamiento activo",
                ["mode_inference"] = "Red neuronal jugando",
                ["save_title"] = "Guardar modelo",
                ["load_title"] = "Cargar modelo",
                ["model_name_label"] = "Nombre del modelo:",
                ["current_model"] = "Modelo actual:",
                ["no_models"] = "No se encontraron modelos",
                ["save_button"] = "Guardar",
                ["cancel_button"] = "Cancelar",
                ["close_button"] = "Cerrar",
                ["open_folder_button"] = "Abrir carpeta"
            },

            // Français
            ["FR"] = new Dictionary<string, string>
            {
                ["mode_disabled"] = "Entraînement désactivé",
                ["mode_training"] = "Entraînement actif",
                ["mode_inference"] = "Réseau neuronal joue",
                ["save_title"] = "Enregistrer le modèle",
                ["load_title"] = "Charger le modèle",
                ["model_name_label"] = "Nom du modèle:",
                ["current_model"] = "Modèle actuel:",
                ["no_models"] = "Aucun modèle trouvé",
                ["save_button"] = "Enregistrer",
                ["cancel_button"] = "Annuler",
                ["close_button"] = "Fermer",
                ["open_folder_button"] = "Ouvrir le dossier"
            },

            // Italiano
            ["IT"] = new Dictionary<string, string>
            {
                ["mode_disabled"] = "Addestramento disattivato",
                ["mode_training"] = "Addestramento attivo",
                ["mode_inference"] = "Rete neurale gioca",
                ["save_title"] = "Salva modello",
                ["load_title"] = "Carica modello",
                ["model_name_label"] = "Nome del modello:",
                ["current_model"] = "Modello attuale:",
                ["no_models"] = "Nessun modello trovato",
                ["save_button"] = "Salva",
                ["cancel_button"] = "Annulla",
                ["close_button"] = "Chiudi",
                ["open_folder_button"] = "Apri cartella"
            },

            // 日本語 (Japanese)
            ["JA"] = new Dictionary<string, string>
            {
                ["mode_disabled"] = "トレーニング無効",
                ["mode_training"] = "トレーニング中",
                ["mode_inference"] = "ニューラルネットワークがプレイ中",
                ["save_title"] = "モデルを保存",
                ["load_title"] = "モデルを読み込む",
                ["model_name_label"] = "モデル名:",
                ["current_model"] = "現在のモデル:",
                ["no_models"] = "モデルが見つかりません",
                ["save_button"] = "保存",
                ["cancel_button"] = "キャンセル",
                ["close_button"] = "閉じる",
                ["open_folder_button"] = "フォルダを開く"
            },

            // 한국어 (Korean)
            ["KO"] = new Dictionary<string, string>
            {
                ["mode_disabled"] = "훈련 비활성화",
                ["mode_training"] = "훈련 활성",
                ["mode_inference"] = "신경망 플레이 중",
                ["save_title"] = "모델 저장",
                ["load_title"] = "모델 불러오기",
                ["model_name_label"] = "모델 이름:",
                ["current_model"] = "현재 모델:",
                ["no_models"] = "모델을 찾을 수 없음",
                ["save_button"] = "저장",
                ["cancel_button"] = "취소",
                ["close_button"] = "닫기",
                ["open_folder_button"] = "폴더 열기"
            },

            // Português
            ["PT"] = new Dictionary<string, string>
            {
                ["mode_disabled"] = "Treinamento desativado",
                ["mode_training"] = "Treinamento ativo",
                ["mode_inference"] = "Rede neural jogando",
                ["save_title"] = "Salvar modelo",
                ["load_title"] = "Carregar modelo",
                ["model_name_label"] = "Nome do modelo:",
                ["current_model"] = "Modelo atual:",
                ["no_models"] = "Nenhum modelo encontrado",
                ["save_button"] = "Salvar",
                ["cancel_button"] = "Cancelar",
                ["close_button"] = "Fechar",
                ["open_folder_button"] = "Abrir pasta"
            },

            // 中文 (Chinese Simplified)
            ["ZH"] = new Dictionary<string, string>
            {
                ["mode_disabled"] = "训练已禁用",
                ["mode_training"] = "训练进行中",
                ["mode_inference"] = "神经网络游玩中",
                ["save_title"] = "保存模型",
                ["load_title"] = "加载模型",
                ["model_name_label"] = "模型名称：",
                ["current_model"] = "当前模型：",
                ["no_models"] = "未找到模型",
                ["save_button"] = "保存",
                ["cancel_button"] = "取消",
                ["close_button"] = "关闭",
                ["open_folder_button"] = "打开文件夹"
            }
        };

        private static string _currentLanguage = "UK"; // За замовчуванням українська

        /// <summary>
        /// Встановлює мову на основі LanguageCode з гри
        /// </summary>
        public static void SetLanguageFromCode(TeamCherry.Localization.LanguageCode languageCode)
        {
            string code = languageCode.ToString();

            // RU -> Українська (UK)
            if (code.StartsWith("RU"))
            {
                _currentLanguage = "UK";
                return;
            }

            // Перевіряємо основні коди мов
            if (code.StartsWith("DE")) _currentLanguage = "DE";
            else if (code.StartsWith("EN")) _currentLanguage = "EN";
            else if (code.StartsWith("ES")) _currentLanguage = "ES";
            else if (code.StartsWith("FR")) _currentLanguage = "FR";
            else if (code.StartsWith("IT")) _currentLanguage = "IT";
            else if (code.StartsWith("JA")) _currentLanguage = "JA";
            else if (code.StartsWith("KO")) _currentLanguage = "KO";
            else if (code.StartsWith("PT")) _currentLanguage = "PT";
            else if (code.StartsWith("ZH")) _currentLanguage = "ZH";
            else _currentLanguage = "EN"; // За замовчуванням англійська
        }

        /// <summary>
        /// Отримує переклад за ключем
        /// </summary>
        public static string Get(string key)
        {
            if (_translations.ContainsKey(_currentLanguage) &&
                _translations[_currentLanguage].ContainsKey(key))
            {
                return _translations[_currentLanguage][key];
            }

            // Fallback на англійську
            if (_translations.ContainsKey("EN") &&
                _translations["EN"].ContainsKey(key))
            {
                return _translations["EN"][key];
            }

            // Якщо нічого не знайдено, повертаємо ключ
            return key;
        }

        /// <summary>
        /// Отримує поточний код мови
        /// </summary>
        public static string GetCurrentLanguage()
        {
            return _currentLanguage;
        }
    }
}
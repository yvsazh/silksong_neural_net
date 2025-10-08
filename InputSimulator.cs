using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SilksongNeuralNetwork
{
    public static class InputSimulator
    {
        // Windows API для емуляції натискання клавіш
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const int KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const int KEYEVENTF_KEYUP = 0x0002;

        // Мапінг Virtual Key кодів
        private static class VK
        {
            public const byte VK_LEFT = 0x25;  // Left Arrow
            public const byte VK_UP = 0x26;    // Up Arrow
            public const byte VK_RIGHT = 0x27; // Right Arrow
            public const byte VK_DOWN = 0x28;  // Down Arrow
            public const byte VK_Z = 0x5A;     // Z key
            public const byte VK_X = 0x58;     // X key
            public const byte VK_C = 0x43;     // C key
            // ...
        }

        public static void PressKey(byte keyCode)
        {
            keybd_event(keyCode, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
        }

        public static void ReleaseKey(byte keyCode)
        {
            keybd_event(keyCode, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        // Зручні методи для конкретних дій
        public static void PressLeft() => PressKey(VK.VK_LEFT);
        public static void ReleaseLeft() => ReleaseKey(VK.VK_LEFT);

        public static void PressRight() => PressKey(VK.VK_RIGHT);
        public static void ReleaseRight() => ReleaseKey(VK.VK_RIGHT);

        public static void PressUp() => PressKey(VK.VK_UP);
        public static void ReleaseUp() => ReleaseKey(VK.VK_UP);

        public static void PressDown() => PressKey(VK.VK_DOWN);
        public static void ReleaseDown() => ReleaseKey(VK.VK_DOWN);

        public static void PressJump() => PressKey(VK.VK_Z);
        public static void ReleaseJump() => ReleaseKey(VK.VK_Z);

        public static void PressDash() => PressKey(VK.VK_X);
        public static void ReleaseDash() => ReleaseKey(VK.VK_X);

        public static void PressAttack() => PressKey(VK.VK_C);
        public static void ReleaseAttack() => ReleaseKey(VK.VK_C);

        // Метод для симуляції короткого натискання
        public static void TapKey(byte keyCode)
        {
            PressKey(keyCode);
            System.Threading.Thread.Sleep(50); // Коротка затримка
            ReleaseKey(keyCode);
        }
    }
}
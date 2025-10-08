using UnityEngine;
using System;

namespace SilksongNeuralNetwork
{
    public static class Utils
    {
        public static float BoolToFloat(bool value)
        {
            return value ? 1f : 0f;
        }

        public static float Normalize(float value, float maxValue)
        {
            if (maxValue == 0f) return 0f;
            return value / maxValue;
        }

        public static float NormalizeWithMinMax(float value, float min, float max)
        {
            if (max - min == 0) return 0;
            return Mathf.Clamp01((value - min) / (max - min));
        }

        public static int random(int min, int max)
        {
            System.Random random = new System.Random();
            return random.Next(min, max + 1);
        }
    }
}
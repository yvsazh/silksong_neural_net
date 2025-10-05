using System;
using Accord.Neuro;
using Accord.Neuro.Learning;
using BepInEx.Logging;

namespace SilksongNeuralNetwork
{
    public class NeuralNet
    {
        public static void Test(ManualLogSource logger)
        {
            double[][] inputs =
            {
                new double[] { 0, 0 },
                new double[] { 0, 1 },
                new double[] { 1, 0 },
                new double[] { 1, 1 }
            };

            double[][] outputs =
            {
                new double[] { 0 },
                new double[] { 0 },
                new double[] { 0 },
                new double[] { 1 } // 1 && 1 = 1
            };

            var network = new ActivationNetwork(new SigmoidFunction(), 2, 2, 1);
            new NguyenWidrow(network).Randomize();

            var teacher = new BackPropagationLearning(network)
            {
                LearningRate = 0.5
            };

            for (int epoch = 0; epoch < 5000; epoch++)
            {
                double error = teacher.RunEpoch(inputs, outputs);
                if (epoch % 1000 == 0)
                    logger.LogInfo($"Epoch {epoch}, Error = {error:F6}");
            }

            logger.LogInfo("Testing trained network:");
            foreach (var input in inputs)
            {
                double[] output = network.Compute(input);
                logger.LogInfo($"{input[0]} AND {input[1]} = {output[0]:F3}");
            }

            logger.LogInfo("Neural network test completed!");
        }
    }
}
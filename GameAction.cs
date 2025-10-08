using BepInEx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace SilksongNeuralNetwork
{
    public class GameAction
    {
        public int Id { get; private set; }
        public string Name { get; private set; }
        private readonly Action _action;

        public GameAction(int id, string name, Action action)
        {
            Id = id;
            Name = name;
            _action = action;
        }

        public void Execute()
        {
            _action?.Invoke();
        }

        // Статичний список усіх доступних дій
        public static readonly GameAction Dash = new GameAction(1, "Dash", () =>
        {
            var type = Agent.Instance.hero.GetType();
            MethodInfo method = type.GetMethod("HeroDash", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(Agent.Instance.hero, new object[] { false });
        });

        public static readonly GameAction Jump = new GameAction(2, "HeroJump", () =>
        {
            var type = Agent.Instance.hero.GetType();

            MethodInfo method = type.GetMethod(
                "HeroJump",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null
            );
            if (Agent.Instance.hero.cState.onGround)
            {
                // SMALL JUMP
                method.Invoke(Agent.Instance.hero, null);
            }    
        });


        public static readonly GameAction BigJump = new GameAction(2, "BigJump", () =>
        {
            var type = Agent.Instance.hero.GetType();

            MethodInfo method = type.GetMethod(
                "HeroJump",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null
            );
            if (Agent.Instance.hero.cState.onGround)
            {
                Agent.Instance.hero.SetDoFullJump();
                method.Invoke(Agent.Instance.hero, null);
            }
        });

        public static readonly GameAction DoubleJump = new GameAction(3, "DoubleJump", () =>
        {
            var type = Agent.Instance.hero.GetType();

            MethodInfo method = type.GetMethod(
                "DoDoubleJump",
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            if (Agent.Instance.hero.cState.onGround == false)
            {
                method.Invoke(Agent.Instance.hero, null);
            }
        });

        public static readonly GameAction GoRight = new GameAction(4, "Go Right", () =>
        {
            InputSimulator.PressRight();
            System.Threading.Thread.Sleep(100);
            InputSimulator.ReleaseRight();
        });

        public static readonly GameAction GoLeft = new GameAction(5, "Go Left", () =>
        {
            InputSimulator.PressLeft();
            System.Threading.Thread.Sleep(100);
            InputSimulator.ReleaseLeft();
        });

        public static readonly GameAction Attack = new GameAction(6, "Attack", () =>
        {
            var type = Agent.Instance.hero.GetType();
            MethodInfo method = type.GetMethod("DoAttack", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(Agent.Instance.hero, null);
        });

        public static readonly GameAction DownAttack = new GameAction(7, "DownAttack", () =>
        {
            var type = Agent.Instance.hero.GetType();
            MethodInfo method = type.GetMethod("DownAttack", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(Agent.Instance.hero, new object[] {false});
        });

        public static readonly List<GameAction> AllActions = new List<GameAction>
        {
            Dash,
            BigJump,
            Jump,
            DoubleJump,
            GoRight,
            GoLeft,
            Attack,
            DownAttack,
        };

        public static GameAction GetById(int id)
        {
            return AllActions.Find(a => a.Id == id);
        }
    }
}
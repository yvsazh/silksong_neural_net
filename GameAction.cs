using BepInEx;
using GlobalEnums;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using TMProOld;
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

        public static readonly GameAction GoRight = new GameAction(1, "Go Right", () =>
        {
            InputSimulator.PressRight();
            InputSimulator.ReleaseRight();
        });

        public static readonly GameAction GoLeft = new GameAction(2, "Go Left", () =>
        {
            InputSimulator.PressRight();
            InputSimulator.ReleaseRight();
        });

        public static readonly GameAction Jump = new GameAction(3, "Jump", () =>
        {
            var type = Agent.Instance.hero.GetType();

            MethodInfo method = type.GetMethod(
                "Jump",
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


        public static readonly GameAction BigJump = new GameAction(4, "BigJump", () =>
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

        public static readonly GameAction DoubleJump = new GameAction(5, "DoubleJump", () =>
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

        // Статичний список усіх доступних дій
        public static readonly GameAction Dash = new GameAction(6, "Dash", () =>
        {
            var type = Agent.Instance.hero.GetType();
            MethodInfo method = type.GetMethod("HeroDash", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(Agent.Instance.hero, new object[] { false });
        });

        public static readonly GameAction Attack = new GameAction(7, "Attack", () =>
        {
            var type = Agent.Instance.hero.GetType();
            MethodInfo method = type.GetMethod("DoAttack", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(Agent.Instance.hero, null);
        });

        public static readonly GameAction DownAttack = new GameAction(8, "DownAttack", () =>
        {
            var type = Agent.Instance.hero.GetType();
            MethodInfo method = type.GetMethod("DownAttack", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(Agent.Instance.hero, new object[] { false });
        });

        public static readonly GameAction UpAttack = new GameAction(9, "UpAttack", () =>
        {
            var type = Agent.Instance.hero.GetType();
            MethodInfo method = type.GetMethod("Attack", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(Agent.Instance.hero, new object[] { AttackDirection.upward });
        });

        public static readonly GameAction Bind = new GameAction(10, "Bind", () =>
        {

            // 4. Викликай івент
            if (Agent.Instance.castAction != null)
            {
                Agent.Instance.castAction.Fsm.Event(Agent.Instance.castAction.wasPressed);
            }
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
            UpAttack,
            Bind,
        };

        public static GameAction GetById(int id)
        {
            return AllActions.Find(a => a.Id == id);
        }
    }
}
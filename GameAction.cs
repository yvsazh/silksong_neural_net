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
            InputSimulator.PressLeft();
            InputSimulator.ReleaseLeft();
        });

        /*
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
        */

        public static readonly GameAction BigJump = new GameAction(3, "BigJump", () =>
        {
            var type = Agent.Instance.hero.GetType();

            MethodInfo method = type.GetMethod(
                "HeroJump",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null
            );
            if (Agent.Instance.hero.CanJump())
            {
                Agent.Instance.hero.SetDoFullJump();
                method.Invoke(Agent.Instance.hero, null);
            }

            if (Agent.Instance.hero.CanDoubleJump())
            {
                MethodInfo method2 = type.GetMethod(
                     "DoDoubleJump",
                     BindingFlags.NonPublic | BindingFlags.Instance
                 );

                if (Agent.Instance.hero.cState.onGround == false)
                {
                    method2.Invoke(Agent.Instance.hero, null);
                }
               ;
            }
        });

        /*
        public static readonly GameAction DoubleJump = new GameAction(4, "DoubleJump", () =>
        {
            var type = Agent.Instance.hero.GetType();

            if (Agent.Instance.hero.CanDoubleJump())
            {
                MethodInfo method = type.GetMethod(
                     "DoDoubleJump",
                     BindingFlags.NonPublic | BindingFlags.Instance
                 );

                if (Agent.Instance.hero.cState.onGround == false)
                {
                    method.Invoke(Agent.Instance.hero, null);
                }
                ;
            }
        });
        */

        // Статичний список усіх доступних дій
        public static readonly GameAction Dash = new GameAction(4, "Dash", () =>
        {
            if (Agent.Instance.hero.CanDash())
            {
                
                var type = Agent.Instance.hero.GetType();
                MethodInfo method = type.GetMethod("HeroDashPressed", BindingFlags.NonPublic | BindingFlags.Instance);
                method.Invoke(Agent.Instance.hero, null);
               
            }
        });

        public static readonly GameAction Attack = new GameAction(5, "Attack", () =>
        {
            if (Agent.Instance.hero.CanAttack())
            {
                var type = Agent.Instance.hero.GetType();
                MethodInfo method = type.GetMethod("Attack", BindingFlags.NonPublic | BindingFlags.Instance);
                method.Invoke(Agent.Instance.hero, new object[] { AttackDirection.normal });
            }

        });

        public static readonly GameAction DownAttack = new GameAction(6, "DownAttack", () =>
        {
            if (Agent.Instance.hero.CanAttack() && !Agent.Instance.hero.cState.onGround)
            {
                var type = Agent.Instance.hero.GetType();
                MethodInfo method = type.GetMethod("DownAttack", BindingFlags.NonPublic | BindingFlags.Instance);
                method.Invoke(Agent.Instance.hero, new object[] { false });
            }
        });

        public static readonly GameAction UpAttack = new GameAction(7, "UpAttack", () =>
        {
            if (Agent.Instance.hero.CanAttack())
            {
                var type = Agent.Instance.hero.GetType();
                MethodInfo method = type.GetMethod("Attack", BindingFlags.NonPublic | BindingFlags.Instance);
                method.Invoke(Agent.Instance.hero, new object[] { AttackDirection.upward });
            }
        });

        public static readonly GameAction Bind = new GameAction(8, "Bind", () =>
        {
            if (Agent.Instance.castAction != null)
            {
                Agent.Instance.castAction.Fsm.Event(Agent.Instance.castAction.wasPressed);
            }
        });

        public static readonly GameAction MainAbility = new GameAction(9, "MainAbility", () =>
        {
            var type = Agent.Instance.hero.GetType();
            MethodInfo method = type.GetMethod("ThrowTool", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(Agent.Instance.hero, new object[] { false });
        });

        public static readonly GameAction FirstTool = new GameAction(10, "FirstTool", () =>
        {
            InputSimulator.PressDown();
            Agent.Instance.StartCoroutine(DelayedToolExecution(
                () => {
                    var type = Agent.Instance.hero.GetType();
                    MethodInfo method = type.GetMethod("ThrowTool", BindingFlags.NonPublic | BindingFlags.Instance);
                    method.Invoke(Agent.Instance.hero, new object[] { false });
                },
                InputSimulator.ReleaseDown
            ));
        });

        public static readonly GameAction SecondTool = new GameAction(11, "SecondTool", () =>
        {
            InputSimulator.PressUp();
            Agent.Instance.StartCoroutine(DelayedToolExecution(
                () => {
                    var type = Agent.Instance.hero.GetType();
                    MethodInfo method = type.GetMethod("ThrowTool", BindingFlags.NonPublic | BindingFlags.Instance);
                    method.Invoke(Agent.Instance.hero, new object[] { false });
                },
                InputSimulator.ReleaseUp
            ));

        });

        public static readonly GameAction HarpoonDash = new GameAction(12, "HarpoonDash", () =>
        {
            if (Agent.Instance.hero.CanHarpoonDash())
            {
                Agent.Instance.hero.harpoonDashFSM.SendEventSafe("DO MOVE");
            }
        });

        // help functions
        private static IEnumerator DelayedToolExecution(Action reflectionMethod, Action releaseAction)
        {
            yield return new WaitForSeconds(0.1f);
            reflectionMethod();
            yield return new WaitForSeconds(0.1f);
            releaseAction();
        }

        public static readonly List<GameAction> AllActions = new List<GameAction>
        {
            Dash,
            BigJump,
            // Jump,
            // DoubleJump,
            GoRight,
            GoLeft,
            Attack,
            DownAttack,
            UpAttack,
            Bind,
            MainAbility,
            FirstTool,
            SecondTool,
            HarpoonDash,
        };

        public static GameAction GetById(int id)
        {
            return AllActions.Find(a => a.Id == id);
        }
    }
}
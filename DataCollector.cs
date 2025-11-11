using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

using static SilksongNeuralNetwork.Utils;

namespace SilksongNeuralNetwork
{
    public class EnemyDistance
    {
        public Transform enemyTransform;
        public float distance;
    }

    public static class DataCollector
    {
        // Ініціалізація системи променів (викликати один раз на початку)
        static DataCollector()
        {
            // Налаштовуємо всі системи променів
            RaySensorSystem.Initialize(
                obstacleRayCount: 20,
                obstacleMaxDistance: 40f,
                enemyRayCount: 30,
                enemyMaxDistance: 40f,
                enemyProjectilesRayCount: 30,
                enemyProjectilesMaxDistance: 40f,
                interactiveObjectRayCount: 20,
                interactiveObjectMaxDistance: 40f
            );
        }

        private static List<float> GetEnemyData(HeroController hero, int enemyCount = 5)
        {
            List<float> enemyData = new List<float>();
            Vector3 heroPosition = hero.transform.position;

            float searchRadius = 100f;
            int enemyLayer = 11;
            int enemyLayerMask = 1 << enemyLayer;
            Collider2D[] enemyColliders = Physics2D.OverlapCircleAll(heroPosition, searchRadius, enemyLayerMask);
            List<EnemyDistance> sortedEnemies = new List<EnemyDistance>();

            foreach (var enemyCollider in enemyColliders)
            {
                if (enemyCollider.gameObject == hero.gameObject) continue;
                float dist = Vector2.Distance(heroPosition, enemyCollider.transform.position);
                sortedEnemies.Add(new EnemyDistance { enemyTransform = enemyCollider.transform, distance = dist });
            }
            sortedEnemies.Sort((a, b) => a.distance.CompareTo(b.distance));

            var closestEnemiesTransforms = sortedEnemies.Take(enemyCount).Select(e => e.enemyTransform.position).ToList();

            if (DebugTools.Instance != null)
            {
                DebugTools.Instance.DrawLinesToTargets(heroPosition, closestEnemiesTransforms);
            }

            for (int i = 0; i < enemyCount; i++)
            {
                if (i < sortedEnemies.Count)
                {
                    var enemy = sortedEnemies[i];

                    // Відстань (нормалізована)
                    enemyData.Add(Normalize(enemy.distance, searchRadius));

                    //Напрямок до ворога (X, Y)
                    Vector2 directionToEnemy = (enemy.enemyTransform.position - heroPosition).normalized;
                    enemyData.Add((directionToEnemy.x + 1f) / 2f); // Нормалізуємо [-1,1] -> [0,1]
                    enemyData.Add((directionToEnemy.y + 1f) / 2f);

                    // Чи ворог в межах атаки?
                    bool inAttackRange = enemy.distance < 3f; // Налаштуйте під вашу гру
                    enemyData.Add(BoolToFloat(inAttackRange));
                }
                else
                {
                    enemyData.Add(1f); // Відстань
                    enemyData.Add(0.5f); // X напрямок (центр)
                    enemyData.Add(0.5f); // Y напрямок (центр)
                    enemyData.Add(0f); // Не в межах атаки
                }
            }

            return enemyData;
        }

        // Отримання даних з променів для перешкод (карти)
        private static List<float> GetObstacleRaySensorData(HeroController hero)
        {
            Vector2 heroPosition = hero.transform.position;
            return RaySensorSystem.GetObstacleRaySensorFloatData(heroPosition);
        }

        // Отримання даних з променів для ворогів
        private static List<float> GetEnemyRaySensorData(HeroController hero)
        {
            Vector2 heroPosition = hero.transform.position;
            return RaySensorSystem.GetEnemyRaySensorFloatData(heroPosition);
        }

        private static List<float> GetEnemyProjectilesRaySensorData(HeroController hero)
        {
            Vector2 heroPosition = hero.transform.position;
            return RaySensorSystem.GetEnemyProjectilesRaySensorFloatData(heroPosition);
        }

        // Отримання даних з променів для інтерактивних об'єктів
        private static List<float> GetInteractiveObjectRaySensorData(HeroController hero)
        {
            Vector2 heroPosition = hero.transform.position;
            return RaySensorSystem.GetInteractiveObjectRaySensorFloatData(heroPosition);
        }

        public static List<float> GetInputData()
        {
            List<float> inputData = new List<float>();

            List<float> hornetState = new List<float>();

            HeroController hero = Agent.Instance.hero;

            if (PlayerData.instance != null && hero != null)
            {
                // ==== БАЗОВІ ПАРАМЕТРИ ====
                hornetState.Add(Normalize(PlayerData.instance.health, PlayerData.instance.maxHealth));
                hornetState.Add(Normalize(PlayerData.instance.silk, PlayerData.instance.silkMax));

                // Швидкість (більш інформативна ніж координати)
                hornetState.Add(Normalize(hero.Body.linearVelocity.x, hero.GetRunSpeed()));
                hornetState.Add(Normalize(hero.Body.linearVelocity.y, hero.JUMP_SPEED));

                // ==== КЛЮЧОВІ СТАНИ РУХУ (8 значень) ====
                hornetState.Add(BoolToFloat(hero.cState.facingRight));
                hornetState.Add(BoolToFloat(hero.cState.onGround));
                hornetState.Add(BoolToFloat(hero.cState.falling));
                hornetState.Add(BoolToFloat(hero.cState.touchingWall));
                hornetState.Add(BoolToFloat(hero.cState.wallSliding));
                hornetState.Add(BoolToFloat(hero.cState.wallClinging));
                hornetState.Add(BoolToFloat(hero.cState.swimming));
                hornetState.Add(BoolToFloat(hero.cState.floating));

                // ==== КЛЮЧОВІ СТАНИ ДЕШІВ (4 значення) ====
                bool isAnyDashing = hero.cState.dashing || hero.cState.airDashing ||
                                   hero.cState.superDashing || hero.cState.backDashing ||
                                   hero.cState.shadowDashing;
                hornetState.Add(BoolToFloat(isAnyDashing));
                hornetState.Add(BoolToFloat(hero.cState.dashCooldown));
                hornetState.Add(BoolToFloat(hero.cState.isSprinting));
                hornetState.Add(BoolToFloat(hero.cState.superDashOnWall));

                // ==== СТАНИ СТРИБКІВ (3 значення) ====
                hornetState.Add(BoolToFloat(hero.cState.jumping));
                hornetState.Add(BoolToFloat(hero.cState.doubleJumping));
                hornetState.Add(BoolToFloat(hero.cState.wallJumping));

                // ==== СТАНИ АТАК (7 значень) ====
                hornetState.Add(BoolToFloat(hero.cState.attacking));
                hornetState.Add(BoolToFloat(hero.cState.upAttacking));
                hornetState.Add(BoolToFloat(hero.cState.downAttacking));
                hornetState.Add(BoolToFloat(hero.cState.downSpiking));
                hornetState.Add(BoolToFloat(hero.cState.nailCharging));
                hornetState.Add(BoolToFloat(hero.cState.altAttack));
                hornetState.Add(BoolToFloat(hero.cState.isToolThrowing));

                // ==== СТАНИ ЗАХИСТУ/ВІДСКОКУ (5 значень) ====
                hornetState.Add(BoolToFloat(hero.cState.invulnerable));
                hornetState.Add(BoolToFloat(hero.cState.recoiling));
                hornetState.Add(BoolToFloat(hero.cState.parrying));
                hornetState.Add(BoolToFloat(hero.cState.parryAttack));
                hornetState.Add(BoolToFloat(hero.cState.bouncing));

                // ==== СПЕЦІАЛЬНІ ЗДІБНОСТІ (4 значення) ====
                hornetState.Add(BoolToFloat(hero.cState.casting));
                hornetState.Add(BoolToFloat(hero.cState.whipLashing));
                hornetState.Add(BoolToFloat(hero.cState.mantling));
                hornetState.Add(BoolToFloat(hero.cState.evading));
            }


            // ==== ДАНІ ПРО ВОРОГІВ (5 значень) ====
            List<float> enemyData = GetEnemyData(hero);

            // ==== RAY SENSOR DATA (найважливіша частина!) ====
            List<float> obstacleRaySensorData = GetObstacleRaySensorData(hero);
            List<float> enemyRaySensorData = GetEnemyRaySensorData(hero);
            List<float> enemyProjectilesRaySensorData = GetEnemyProjectilesRaySensorData(hero);
            List<float> interactiveObjectRaySensorData = GetInteractiveObjectRaySensorData(hero);

            // ==== ОБ'ЄДНАННЯ ВСІХ ДАНИХ ====
            inputData.AddRange(hornetState);           // ~48 значень
            inputData.AddRange(enemyData);             // 5 значень
            inputData.AddRange(obstacleRaySensorData); // 16 значень (ray count)
            inputData.AddRange(enemyRaySensorData);    // 12 значень (ray count)
            inputData.AddRange(enemyProjectilesRaySensorData); // залежить від налаштувань
            inputData.AddRange(interactiveObjectRaySensorData); // 8 значень (ray count)

            return inputData;
        }

        public static List<float> GetOutputData()
        {
            List<float> outputData = new List<float>();

            bool attack = false;
            bool downAttack = false;
            bool upAttack = false;

            bool usedFirstTool = false;
            bool usedSecondTool = false;

            bool usedHarpoon = false;

            var heroType = Agent.Instance.hero.GetType();

            // Movement
            outputData.Add(BoolToFloat(Agent.Instance.myInputActions.Right.IsPressed));
            outputData.Add(BoolToFloat(Agent.Instance.myInputActions.Left.IsPressed));


            outputData.Add(BoolToFloat(Agent.Instance.myInputActions.Jump.WasPressed));

            outputData.Add(BoolToFloat(Agent.Instance.myInputActions.Dash.WasPressed));

            // Attacks in different directions
            if (Agent.Instance.hero.cState.attacking && !Agent.Instance.myInputActions.Up.IsPressed && !Agent.Instance.myInputActions.Down.IsPressed)
            {
                attack = true;
                upAttack = false;
                downAttack = false;
            }
            if (Agent.Instance.hero.cState.attacking && Agent.Instance.myInputActions.Up.IsPressed && !Agent.Instance.myInputActions.Down.IsPressed)
            {
                attack = false;
                upAttack = true;
                downAttack = false;
            }
            if (Agent.Instance.myInputActions.Attack.WasPressed && !Agent.Instance.myInputActions.Up.IsPressed && Agent.Instance.myInputActions.Down.IsPressed && !Agent.Instance.hero.cState.onGround)
            {
                attack = false;
                upAttack = false;
                downAttack = true;
            }

            outputData.Add(BoolToFloat(attack));
            outputData.Add(BoolToFloat(downAttack));
            outputData.Add(BoolToFloat(upAttack));

            outputData.Add(BoolToFloat(Agent.Instance.myInputActions.Cast.WasPressed));

            if (Agent.Instance.myInputActions.Down.IsPressed && Agent.Instance.hero.cState.isToolThrowing)
            {
                usedFirstTool = true;
                usedSecondTool = false;
            }

            if (Agent.Instance.myInputActions.Up.IsPressed && Agent.Instance.hero.cState.isToolThrowing)
            {
                usedSecondTool = true;
                usedFirstTool = false;
            }

            var type = Agent.Instance.hero.GetType();
            FieldInfo field = type.GetField("skillEventTarget", BindingFlags.NonPublic | BindingFlags.Instance);
            PlayMakerFSM fsm = (PlayMakerFSM)field.GetValue(Agent.Instance.hero);

            outputData.Add(BoolToFloat(fsm.ActiveStateName != "Idle"));
            outputData.Add(BoolToFloat(usedFirstTool));
            outputData.Add(BoolToFloat(usedSecondTool));

            if (Agent.Instance.hero.harpoonDashFSM.ActiveStateName == "Antic" || Agent.Instance.hero.harpoonDashFSM.ActiveStateName == "Throw" || Agent.Instance.hero.harpoonDashFSM.ActiveStateName == "Dash")
            {
                usedHarpoon = true;
            }
            else
            {
                usedHarpoon = false;
            }

            outputData.Add(BoolToFloat(usedHarpoon));

            return outputData;
        }
    }
}
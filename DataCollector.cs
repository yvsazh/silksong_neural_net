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
            // Можеш налаштувати кількість променів і максимальну відстань
            RaySensorSystem.Initialize(rayCount: 16, maxDistance: 20f);
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
                    Transform enemy = sortedEnemies[i].enemyTransform;
                    Vector2 relativePosition = enemy.position - hero.transform.position;

                    enemyData.Add(Normalize(relativePosition.x, searchRadius));
                    enemyData.Add(Normalize(relativePosition.y, searchRadius));
                    enemyData.Add(Normalize(sortedEnemies[i].distance, searchRadius));
                }
                else
                {
                    enemyData.Add(0f);
                    enemyData.Add(0f);
                    enemyData.Add(0f);
                }
            }

            return enemyData;
        }

        // Новий метод для отримання даних з променів-сенсорів
        private static List<float> GetRaySensorData(HeroController hero)
        {
            Vector2 heroPosition = hero.transform.position;
            return RaySensorSystem.GetRaySensorFloatData(heroPosition);
        }

        public static List<float> GetInputData()
        {
            List<float> inputData = new List<float>();

            List<float> hornetState = new List<float>();
            List<float> globalActions = new List<float>();

            HeroController hero = Agent.Instance.hero;
            var heroType = hero.GetType();

            if (PlayerData.instance != null)
            {
                // ---- Basics ---- 
                hornetState.Add(Normalize(PlayerData.instance.health, PlayerData.instance.maxHealth));
                hornetState.Add(Normalize(PlayerData.instance.silk, PlayerData.instance.silkMax));
                // ----  coords x and y ---- 
                hornetState.Add(Normalize(hero.transform.position.x, 1000)); // should try to find max dynamicly
                hornetState.Add(Normalize(hero.transform.position.y, 1000)); // should try to find max dynamicly

                // ---- velocity ---- 
                hornetState.Add(Normalize(hero.Body.linearVelocity.x, hero.GetRunSpeed()));
                hornetState.Add(Normalize(hero.Body.linearVelocity.y, hero.JUMP_SPEED));

                // ---- STATES ---- 
                hornetState.Add(BoolToFloat(hero.cState.facingRight));
                hornetState.Add(BoolToFloat(hero.cState.onGround));
                hornetState.Add(BoolToFloat(hero.cState.jumping));
                hornetState.Add(BoolToFloat(hero.cState.shuttleCock));
                hornetState.Add(BoolToFloat(hero.cState.floating));
                hornetState.Add(BoolToFloat(hero.umbrellaFSM.ActiveStateName == "Float Idle"));
                hornetState.Add(BoolToFloat(hero.cState.wallJumping));
                hornetState.Add(BoolToFloat(hero.cState.doubleJumping));
                hornetState.Add(BoolToFloat(hero.cState.nailCharging));
                hornetState.Add(BoolToFloat(hero.cState.shadowDashing));
                hornetState.Add(BoolToFloat(hero.cState.swimming));
                hornetState.Add(BoolToFloat(hero.cState.falling));
                hornetState.Add(BoolToFloat(hero.cState.dashing));
                hornetState.Add(BoolToFloat(hero.cState.isSprinting));
                hornetState.Add(BoolToFloat(hero.cState.isBackSprinting));
                hornetState.Add(BoolToFloat(hero.cState.isBackScuttling));
                hornetState.Add(BoolToFloat(hero.cState.airDashing));
                hornetState.Add(BoolToFloat(hero.cState.superDashing));
                hornetState.Add(BoolToFloat(hero.cState.superDashOnWall));
                hornetState.Add(BoolToFloat(hero.cState.backDashing));
                hornetState.Add(BoolToFloat(hero.cState.touchingWall));
                hornetState.Add(BoolToFloat(hero.cState.wallSliding));
                hornetState.Add(BoolToFloat(hero.cState.wallClinging));
                hornetState.Add(BoolToFloat(hero.cState.wallScrambling));
                hornetState.Add(BoolToFloat(hero.cState.transitioning));
                hornetState.Add(BoolToFloat(hero.cState.attacking));
                hornetState.Add(BoolToFloat(hero.cState.lookingUp));
                hornetState.Add(BoolToFloat(hero.cState.lookingDown));
                hornetState.Add(BoolToFloat(hero.cState.lookingUpRing));
                hornetState.Add(BoolToFloat(hero.cState.lookingDownRing));
                hornetState.Add(BoolToFloat(hero.cState.lookingUpAnim));
                hornetState.Add(BoolToFloat(hero.cState.lookingDownAnim));
                hornetState.Add(BoolToFloat(hero.cState.altAttack));
                hornetState.Add(BoolToFloat(hero.cState.upAttacking));
                hornetState.Add(BoolToFloat(hero.cState.downAttacking));
                hornetState.Add(BoolToFloat(hero.cState.downTravelling));
                hornetState.Add(BoolToFloat(hero.cState.downSpikeAntic));
                hornetState.Add(BoolToFloat(hero.cState.downSpiking));
                hornetState.Add(BoolToFloat(hero.cState.downSpikeBouncing));
                hornetState.Add(BoolToFloat(hero.cState.downSpikeBouncingShort));
                hornetState.Add(BoolToFloat(hero.cState.downSpikeRecovery));
                hornetState.Add(BoolToFloat(hero.cState.bouncing));
                hornetState.Add(BoolToFloat(hero.cState.shroomBouncing));
                hornetState.Add(BoolToFloat(hero.cState.recoilingRight));
                hornetState.Add(BoolToFloat(hero.cState.recoilingLeft));
                hornetState.Add(BoolToFloat(hero.cState.recoilingDrill));
                hornetState.Add(BoolToFloat(hero.cState.dead));
                hornetState.Add(BoolToFloat(hero.cState.isFrostDeath));
                hornetState.Add(BoolToFloat(hero.cState.hazardDeath));
                hornetState.Add(BoolToFloat(hero.cState.hazardRespawning));
                hornetState.Add(BoolToFloat(hero.cState.willHardLand));
                hornetState.Add(BoolToFloat(hero.cState.recoilFrozen));
                hornetState.Add(BoolToFloat(hero.cState.recoiling));
                hornetState.Add(BoolToFloat(hero.cState.invulnerable));
                hornetState.Add(BoolToFloat(hero.cState.casting));
                hornetState.Add(BoolToFloat(hero.cState.castRecoiling));
                hornetState.Add(BoolToFloat(hero.cState.preventDash));
                hornetState.Add(BoolToFloat(hero.cState.preventBackDash));
                hornetState.Add(BoolToFloat(hero.cState.dashCooldown));
                hornetState.Add(BoolToFloat(hero.cState.backDashCooldown));
                hornetState.Add(BoolToFloat(hero.cState.nearBench));
                hornetState.Add(BoolToFloat(hero.cState.inWalkZone));
                hornetState.Add(BoolToFloat(hero.cState.isPaused));
                hornetState.Add(BoolToFloat(hero.cState.onConveyor));
                hornetState.Add(BoolToFloat(hero.cState.onConveyorV));
                hornetState.Add(BoolToFloat(hero.cState.inConveyorZone));
                hornetState.Add(BoolToFloat(hero.cState.spellQuake));
                hornetState.Add(BoolToFloat(hero.cState.freezeCharge));
                hornetState.Add(BoolToFloat(hero.cState.focusing));
                hornetState.Add(BoolToFloat(hero.cState.inAcid));
                hornetState.Add(BoolToFloat(hero.cState.touchingNonSlider));
                hornetState.Add(BoolToFloat(hero.cState.wasOnGround));
                hornetState.Add(BoolToFloat(hero.cState.parrying));
                hornetState.Add(BoolToFloat(hero.cState.parryAttack));
                hornetState.Add(BoolToFloat(hero.cState.mantling));
                hornetState.Add(BoolToFloat(hero.cState.mantleRecovery));
                hornetState.Add(BoolToFloat(hero.cState.inUpdraft));
                hornetState.Add(BoolToFloat(hero.cState.isToolThrowing));
                hornetState.Add(BoolToFloat(hero.cState.isInCancelableFSMMove));
                hornetState.Add(BoolToFloat(hero.cState.inWindRegion));
                hornetState.Add(BoolToFloat(hero.cState.isMaggoted));
                hornetState.Add(BoolToFloat(hero.cState.inFrostRegion));
                hornetState.Add(BoolToFloat(hero.cState.isFrosted));
                hornetState.Add(BoolToFloat(hero.cState.isTouchingSlopeLeft));
                hornetState.Add(BoolToFloat(hero.cState.isTouchingSlopeRight));
                hornetState.Add(BoolToFloat(hero.cState.isBinding));
                hornetState.Add(BoolToFloat(hero.cState.needolinPlayingMemory));
                hornetState.Add(BoolToFloat(hero.cState.isScrewDownAttacking));
                hornetState.Add(BoolToFloat(hero.cState.evading));
                hornetState.Add(BoolToFloat(hero.cState.whipLashing));
                hornetState.Add(BoolToFloat(hero.cState.fakeHurt));
                hornetState.Add(BoolToFloat(hero.cState.isInCutsceneMovement));
                hornetState.Add(BoolToFloat(hero.cState.isTriggerEventsPaused));

                // ------ CAN PLAYER DO SOMETHING?? ------ 

                
                MethodInfo canFloatMethod = heroType.GetMethod("CanFloat", BindingFlags.NonPublic | BindingFlags.Instance);
                bool canFloat = (bool)canFloatMethod.Invoke(hero, null);
                hornetState.Add(BoolToFloat(canFloat));

                MethodInfo canWallSlideMethod = heroType.GetMethod("CanWallSlide", BindingFlags.NonPublic | BindingFlags.Instance);
                bool canWallSlide = (bool)canWallSlideMethod.Invoke(hero, null);
                hornetState.Add(BoolToFloat(canWallSlide));

                MethodInfo canRecoilMethod = heroType.GetMethod("CanRecoil", BindingFlags.NonPublic | BindingFlags.Instance);
                bool canRecoil = (bool)canRecoilMethod.Invoke(hero, null);
                hornetState.Add(BoolToFloat(canRecoil));

                MethodInfo canDownAttackMethod = heroType.GetMethod("CanDownAttack", BindingFlags.NonPublic | BindingFlags.Instance);
                bool canDownAttack = (bool)canDownAttackMethod.Invoke(hero, null);
                hornetState.Add(BoolToFloat(canDownAttack));

                MethodInfo canAttackActionMethod = heroType.GetMethod("CanAttackAction", BindingFlags.NonPublic | BindingFlags.Instance);
                bool canAttackAction = (bool)canAttackActionMethod.Invoke(hero, null);
                hornetState.Add(BoolToFloat(canAttackAction));

                MethodInfo canWallScrambleMethod = heroType.GetMethod("CanWallScramble", BindingFlags.NonPublic | BindingFlags.Instance);
                bool canWallScramble = (bool)canWallScrambleMethod.Invoke(hero, null);
                hornetState.Add(BoolToFloat(canWallScramble));
                

                hornetState.Add(BoolToFloat(hero.CanJump()));
                hornetState.Add(BoolToFloat(hero.CanDoubleJump()));
                hornetState.Add(BoolToFloat(hero.CanDash()));
                hornetState.Add(BoolToFloat(hero.CanAttack()));
                hornetState.Add(BoolToFloat(hero.CanTakeDamage()));
                hornetState.Add(BoolToFloat(hero.CanTryHarpoonDash()));
                hornetState.Add(BoolToFloat(hero.CanHarpoonDash()));
                hornetState.Add(BoolToFloat(hero.CanCast()));
                hornetState.Add(BoolToFloat(hero.CanBind()));
                hornetState.Add(BoolToFloat(hero.CanNailArt()));
                hornetState.Add(BoolToFloat(hero.CanSprint()));
                hornetState.Add(BoolToFloat(hero.CanSuperJump()));
                hornetState.Add(BoolToFloat(hero.CanThrowTool()));
            }

            // Global Actions
            if (PlayerData.instance != null)
            {
                globalActions.Add(BoolToFloat(PlayerData.instance.hasDash));
                globalActions.Add(BoolToFloat(PlayerData.instance.hasBrolly));
                globalActions.Add(BoolToFloat(PlayerData.instance.hasWalljump));
                globalActions.Add(BoolToFloat(PlayerData.instance.hasDoubleJump));
                globalActions.Add(BoolToFloat(PlayerData.instance.hasQuill));
                globalActions.Add(BoolToFloat(PlayerData.instance.hasChargeSlash));
                globalActions.Add(BoolToFloat(PlayerData.instance.hasSuperJump));
                globalActions.Add(BoolToFloat(PlayerData.instance.hasParry));
                globalActions.Add(BoolToFloat(PlayerData.instance.hasHarpoonDash));
            }

            // ENEMY DATA
            List<float> enemyData = GetEnemyData(hero);

            // RAY SENSOR DATA (NEW!)
            List<float> raySensorData = GetRaySensorData(hero);

            // MERGE
            inputData.AddRange(hornetState);
            inputData.AddRange(globalActions);
            inputData.AddRange(enemyData);
            inputData.AddRange(raySensorData); // Додаємо дані з променів

            return inputData;
        }

        public static List<float> GetOutputData()
        {
            List<float> outputData = new List<float>();

            // standart
            bool jump = false;
            bool bigJump = false;
            bool doubleJump = false;

            bool attack = false;
            bool downAttack = false;
            bool upAttack = false;

            var heroType = Agent.Instance.hero.GetType();

            // HELP VARIABLES
            FieldInfo jumped_stepsField = heroType.GetField("jumped_steps", BindingFlags.NonPublic | BindingFlags.Instance);
            int jumped_steps = (int)jumped_stepsField.GetValue(Agent.Instance.hero);

            FieldInfo doubleJumpedField = heroType.GetField("doubleJumped", BindingFlags.NonPublic | BindingFlags.Instance);
            bool doubleJumped = (bool)doubleJumpedField.GetValue(Agent.Instance.hero);

            // Movement
            outputData.Add(BoolToFloat(Agent.Instance.myInputActions.Right.IsPressed));
            outputData.Add(BoolToFloat(Agent.Instance.myInputActions.Left.IsPressed));

            // Jump logic
            if (jumped_steps < 7 && jumped_steps > 1 && !doubleJumped)
            {
                jump = true;
                bigJump = false;
            }
            if (jumped_steps > 7 && !doubleJumped)
            {
                jump = false;
                bigJump = true;
            }
            if (doubleJumped)
            {
                doubleJump = true;
                jump = false;
                bigJump = false;
            }

            if (Agent.Instance.hero.cState.onGround == true)
            {
                jumped_stepsField.SetValue(Agent.Instance.hero, 0);
            }

            outputData.Add(BoolToFloat(jump));
            outputData.Add(BoolToFloat(bigJump));
            outputData.Add(BoolToFloat(doubleJump));

            outputData.Add(BoolToFloat(Agent.Instance.myInputActions.Dash.IsPressed));

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
            if (Agent.Instance.myInputActions.Attack.IsPressed && !Agent.Instance.myInputActions.Up.IsPressed && Agent.Instance.myInputActions.Down.IsPressed && !Agent.Instance.hero.cState.onGround)
            {
                attack = false;
                upAttack = false;
                downAttack = true;
            }

            outputData.Add(BoolToFloat(attack));
            outputData.Add(BoolToFloat(downAttack));
            outputData.Add(BoolToFloat(upAttack));

            return outputData;
        }
    }
}
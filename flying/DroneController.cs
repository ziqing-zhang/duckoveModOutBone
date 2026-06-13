using System.Collections.Generic;
using System.Timers;
using Duckov.Buffs;
using Pathfinding;
using Unity.VisualScripting;
using UnityEngine;

namespace TestMod.Drone
{
    [RequireComponent(typeof(Animator), typeof(DamageReceiver))]
    public class DroneController : MonoBehaviour
    {
        private static readonly int DistanceToTarget = Animator.StringToHash("DistanceToTarget");
        private static readonly int HasTarget = Animator.StringToHash("HasTarget");
        private static readonly int FlySpeed = Animator.StringToHash("FlySpeed");

        // ========== 模块引用 ==========
        
        public Animator animator;           // 动画控制器
        public AudioSource audioSource;     // 声音播放器
        public Transform modelRoot;         // 模型根节点（用于旋转）
       // public CharacterMainControl mainControl; // 可选，用于获取属性/队伍

        // ========== 移动参数 ==========
        public Seeker seeker;
        public float flySpeed = 8f;
        public float rotateSpeed = 360f;
        public float stoppingDistance = 1.5f;
        public enum MoveMode { DirectTransform, NavMeshAgent }
        public MoveMode moveMode = MoveMode.DirectTransform;
        private UnityEngine.AI.NavMeshAgent navAgent; // 可选
        
        public float maxTiltAngle = 30f;          // 最大倾斜角度（度）
        public float tiltSmoothSpeed = 5f;        // 倾斜平滑速度
        public float rollFactor = 0.5f;           // 转向时滚转系数

        private Vector3 currentTilt;              // 当前倾斜欧拉角（X轴俯仰，Z轴滚转）
        // ========== 感知与目标 ==========
        
        public float detectRadius = 12f;
        public float hearingRadius = 15f;
        public LayerMask enemyLayers;
        private Transform currentTarget;
        private CharacterMainControl attacktarget = null;
        private bool hasPlayerTarget;
        private Collider[] _explosionHitBuffer = new Collider[30];

        // ========== 声音设置 ==========
       
        public AudioClip flyLoopSound;      // 飞行循环音效
        
        public float flySoundVolume = 0.5f;
        

        // ========== 爆炸效果 ==========
        public enum exportType
        {
            normal
        }

        public DroneExplosionHandler explos;
     
        // ========== 生命周期 ==========
        
        public float lifeTime = 10f;
        private float spawnTime;

        // ========== 事件（供外部订阅） ==========
        public System.Action<Transform> OnTargetChanged;
        public System.Action<GameObject> OnExploded;

        // ========== 内部状态 ==========
        private bool isExploded = false;
        private bool isFlying = false;
        private Vector3 moveDirection;
        private float lastScanTime;

        //public void Initialize(CharacterMainControl cmc,AudioClip flysound, LayerMask enemyMask, float scanRadius = 12f, float hearRadius = 15f)
        public void Initialize(AudioClip flysound, LayerMask enemyMask, float scanRadius = 12f, float hearRadius = 15f)
        {
           // mainControl = cmc;
            seeker=gameObject.GetComponent<Seeker>();
            if (!seeker)
            {
                seeker = gameObject.AddComponent<Seeker>();
            }
            // 1. 获取必要组件（如果未在 Inspector 中拖拽）
            if (animator == null) animator = GetComponent<Animator>();
            //todo 设置动画状态机目前不需要复杂动作
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (modelRoot == null)
            {
                Transform found = transform.Find("Model");
                if (found != null) modelRoot = found;
            }
            
            // 2. 移动模式相关（假设 moveMode 已在 Inspector 设置，或在此强制指定）
            if (moveMode == MoveMode.NavMeshAgent)
            {
                
                navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (navAgent == null) navAgent = gameObject.AddComponent<UnityEngine.AI.NavMeshAgent>();
                navAgent.speed = flySpeed;
                navAgent.stoppingDistance = stoppingDistance;
            }
            
            
            // 3. 感知参数
            enemyLayers = enemyMask;
            detectRadius = scanRadius;
            hearingRadius = hearRadius;
            
            // 4. 声音资源（需要从外部传入或加载，此处示例从 Resources 加载）
            if (flyLoopSound == null)
                flyLoopSound = flysound;
            
                
            
            if (flyLoopSound != null && audioSource != null)
            {
                audioSource.clip = flyLoopSound;
                audioSource.loop = true;
                audioSource.volume = flySoundVolume;
                audioSource.Play();
            }
            
            
        
            // 6. 生命周期
            
            
            // 7. 可选：免疫 Buff（示例免疫眩晕）
            // mainControl.buffResist.Add(Buff.BuffExclusiveTags.Stun);
            
            // 8. 启用脚本（如果之前被禁用）
            enabled = true;
            
            // 9. 可选：设置初始动画参数
            if (animator != null)
            {
                animator.SetFloat(FlySpeed, 0);
                animator.SetBool(HasTarget, false);
                animator.SetFloat(DistanceToTarget, float.MaxValue);
            }

            Health.OnDead += OnDead;
        }

        

        void OnEnable()
        {
            AIMainBrain.OnSoundSpawned += OnSoundHeard;
        }

        void OnDisable()
        {
            AIMainBrain.OnSoundSpawned -= OnSoundHeard;
            
            if (currentPath != null)
            {
                currentPath.Release(this);
                currentPath = null;
            }
        }

        void Start()
        {
            // 播放起飞动画和声音
            spawnTime = Time.time;
            PlaySound(flyLoopSound, true, flySoundVolume); // 循环飞行声
            isFlying = true;
        }
        
       
        
        // ========== 主循环 ==========
        private float timer = 0f;
        void Update()
        {
            if (isExploded) return;

            // 生命周期结束
            if (Time.time - spawnTime >= lifeTime)
            {
                Debug.Log("无人机生命到期");
                Explode();
                return;
            }

            if (!attacktarget)
            {
                hasPlayerTarget = false;
            }
            // 定期扫描敌人（如果没有玩家主动目标）
            if (!hasPlayerTarget && Time.time - lastScanTime >= 0.2f)
            {
                lastScanTime = Time.time;
                ScanForEnemies();
            }
            else if(Time.time - lastScanTime >= 0.2f)
            {
                currentTarget = attacktarget.transform;
            }
            
            // 移动逻辑
            if (currentTarget)
            {
                
                MoveTowardTarget();
                CheckExplodeDistance();
            }
            else
            {
                // 无目标时悬停（播放悬停动画）
                if (isFlying) PlayAnimation("Hover");
                StopMoving();
            }
            
            //推送声音,
            timer += Time.deltaTime;
            if (timer >= 0.5f)
            {
                timer = 0f;
                AISound spentsound=new AISound();
                //spentsound.fromCharacter = mainControl;
                //spentsound.fromObject = mainControl.gameObject;
                spentsound.fromTeam=Teams.player;
                spentsound.pos = transform.position;
                spentsound.radius = 8f;
                spentsound.soundType = SoundTypes.unknowNoise;
                AIMainBrain.MakeSound(spentsound);
            }
            
            // 更新动画参数（例如飞行速度、是否靠近目标等）
            UpdateTilt();
            UpdateAnimationParams();
        }

        // ========== 移动模块 ==========
        // 寻路相关
        private Path currentPath;              // 当前使用的路径
        private int currentWaypointIndex;      // 当前目标路点索引
        private bool waitingForPath;           // 是否正在等待寻路结果
        private float nextWaypointDistance = 1.5f;   // 距离当前路点多近时切换到下一个
        private float pathStopDistance = 0.5f;       // 到达终点的停止距离（可与 stoppingDistance 统一）

        // 可选：控制重新寻路的间隔（避免每帧请求）
        private float lastPathRequestTime;
        private float pathRequestInterval = 0.5f;
        void RequestNewPath(Vector3 target)
        {
            waitingForPath = true;
            lastPathRequestTime = Time.time;

            // 取消当前未完成的路径请求
            seeker.CancelCurrentPathRequest(true);

            // 发起新寻路，回调为 OnPathComplete
            seeker.StartPath(transform.position, target, OnPathComplete);
        }
        void OnPathComplete(Path p)
        {
            waitingForPath = false;

            if (p.error)
            {
                Debug.LogWarning("Drone 寻路失败: " + p.errorLog);
                return;
            }

            // 释放旧路径
            if (currentPath != null)
                currentPath.Release(this);

            // 持有新路径（增加引用计数，防止被池回收）
            p.Claim(this);
            currentPath = p;

            // 重置路点索引
            currentWaypointIndex = 0;

            // 可选：确保路径至少有一个点
            if (currentPath.vectorPath.Count == 0)
            {
                currentPath.Release(this);
                currentPath = null;
            }
        }
        /// <summary>
        /// 根据当前保存的路径和当前位置，计算下一步移动方向（归一化）。
        /// 如果无路径、路径已完成或已到达终点，返回 Vector3.zero。
        /// </summary>
        Vector3 CalculateMoveDirectionFromPath()
        {
            if (currentPath == null || currentPath.vectorPath.Count == 0)
                return Vector3.zero;

            List<Vector3> vectorPath = currentPath.vectorPath;
            Vector3 currentPos = transform.position;

            // 确保索引有效
            if (currentWaypointIndex < 0) currentWaypointIndex = 0;
            if (currentWaypointIndex >= vectorPath.Count)
                currentWaypointIndex = vectorPath.Count - 1;

            Vector3 targetWaypoint = vectorPath[currentWaypointIndex];
            float distToWaypoint = Vector3.Distance(currentPos, targetWaypoint);

            // 距离当前路点足够近，则切换下一个
            if (distToWaypoint < nextWaypointDistance)
            {
                if (currentWaypointIndex + 1 < vectorPath.Count)
                {
                    currentWaypointIndex++;
                    targetWaypoint = vectorPath[currentWaypointIndex];
                }
                else
                {
                    // 已是最后一个路点，检查是否真的到达终点（全局停止距离）
                    float distToEnd = Vector3.Distance(currentPos, vectorPath[vectorPath.Count - 1]);
                    if (distToEnd < pathStopDistance)
                    {
                        // 到达终点，清理路径
                        currentPath.Release(this);
                        currentPath = null;
                        return Vector3.zero;
                    }
                    // 尚未到达终点，继续走向最后一个路点
                }
            }

            Vector3 direction = (targetWaypoint - currentPos).normalized;
            return direction;
        }
        
        void MoveTowardTarget()
        {
            if (!currentTarget) return;

            Vector3 targetPos = currentTarget.position;
            float distanceToTarget = Vector3.Distance(transform.position, targetPos);

            // 如果已经到达停止距离，直接爆炸（或悬停）
            if (distanceToTarget <= stoppingDistance)
            {
                StopMoving();
                return;
            }

            // 是否需要重新寻路？（无路径、路径已用完、超过请求间隔）
            bool needNewPath = (currentPath == null || currentWaypointIndex >= (currentPath.vectorPath?.Count ?? 0)) ||
                               (Time.time - lastPathRequestTime > pathRequestInterval);

            if (needNewPath && !waitingForPath)
            {
                RequestNewPath(targetPos);
            }

            // 如果有有效路径，计算移动方向
            Vector3 moveDir = CalculateMoveDirectionFromPath();
            if (moveDir != Vector3.zero)
            {
                // 移动
                if (moveMode == MoveMode.DirectTransform)
                {
                    transform.position += moveDir * (flySpeed * Time.deltaTime);
                    // 旋转面向移动方向
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
                }
                else if (moveMode == MoveMode.NavMeshAgent && navAgent)
                {
                    // 如果用 NavMeshAgent，可以设置 destination 让 agent 自己移动，但这里我们手动控制
                    // 为了与寻路结果结合，建议直接使用 transform 移动或者将 moveDir 传给 agent
                    navAgent.velocity = moveDir * flySpeed;
                }
            }
            else
            {
                // 无有效移动方向（可能已到达终点），悬停
                StopMoving();
            }

            // 更新动画 todo 动画效果播放未知
            float speed = moveDir.magnitude * flySpeed;
            animator.SetFloat(FlySpeed, speed);
        }
        private void UpdateTilt()
        {
            return;
            // 获取当前移动方向（例如通过输入或朝向目标的单位向量）
            Vector3 moveDir = CalculateMoveDirectionFromPath();  
            Vector3 forward = transform.forward;
    
            // 计算俯仰（Pitch）：前后加速度影响 X 轴旋转
            float pitch = 0f;
            if (moveDir != Vector3.zero)
            {
                float forwardAcc = Vector3.Dot(moveDir, forward);
                pitch = forwardAcc * maxTiltAngle;
            }
    
            // 计算滚转（Roll）：转向速度或横向加速度影响 Z 轴旋转
            float roll = 0f;
            if (moveDir != Vector3.zero)
            {
                Vector3 lateral = moveDir - Vector3.Project(moveDir, forward);
                float sideAcc = lateral.magnitude * Mathf.Sign(Vector3.Dot(lateral, transform.right));
                roll = -sideAcc * maxTiltAngle * rollFactor;
            }
    
            // 目标欧拉角（无人机通常不需要偏航Yaw，由根物体转向控制）
            Vector3 targetTilt = new Vector3(pitch, 0f, roll);
    
            // 平滑过渡
            currentTilt = Vector3.Lerp(currentTilt, targetTilt, tiltSmoothSpeed * Time.deltaTime);
    
            //todo 应用到模型子物体（注意：根物体的旋转用于方向，模型的局部旋转用于倾斜）
            
        }

        
        void StopMoving()
        {
            if (moveMode == MoveMode.DirectTransform)
            {
                // 什么都不做，下一帧会继续移动直到距离条件满足
            }
            else if (navAgent && navAgent.isActiveAndEnabled)
            {
                navAgent.isStopped = true;
            }
            animator.SetFloat(FlySpeed, 0);
        }

        void CheckExplodeDistance()
        {
            if (!currentTarget) return;
            if(!attacktarget) return;
            float dist = Vector3.Distance(transform.position, currentTarget.position);
            if (dist <= stoppingDistance)
            {
                Debug.Log("贴近目标");
                Explode();
            }
        }

        // ========== 感知模块 ==========
        void ScanForEnemies()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectRadius, _explosionHitBuffer, enemyLayers);
    
            float bestDist = float.MaxValue;
            if(!hasPlayerTarget) attacktarget = null;
            for (int i = 0; i < hitCount; i++)  // 只遍历实际碰撞到的数量
            {
                Collider hit = _explosionHitBuffer[i];
                if (hit == null) continue;
        
                DamageReceiver dr = hit.GetComponent<DamageReceiver>();
                if (dr && Team.IsEnemy(Teams.player, dr.Team))
                {
                    float d = Vector3.Distance(transform.position, hit.transform.position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        currentTarget = hit.transform;
                        attacktarget = hit.GetComponent<CharacterMainControl>();
                        OnTargetChanged?.Invoke(currentTarget);
                    }
                }
            }
        }
        
        
        void OnSoundHeard(AISound sound)
        {
            // 忽略友军声音
            if ( sound.fromTeam == Teams.player) return;

            float dist = Vector3.Distance(transform.position, sound.pos);
            if (dist <= hearingRadius && !hasPlayerTarget)
            {
                // 声音吸引：飞向声源（并临时设为目标？这里简单移动位置）
                if (moveMode == MoveMode.NavMeshAgent && navAgent != null)
                    navAgent.SetDestination(sound.pos);
                else
                    moveDirection = (sound.pos - transform.position).normalized;
                // 可选：播放“警惕”动画
                
            }
        }

        // 公开方法：玩家主动指定目标
        public void SetTarget(CharacterMainControl target)
        {
            if(!target) Debug.LogError("攻击目标为空");
            if(attacktarget&& attacktarget == target) return;
            
            hasPlayerTarget = (target != null);
            attacktarget = target;
            currentTarget = target.transform;
            OnTargetChanged?.Invoke(currentTarget);
            //target.GetComponent<Health>().OnDeadEvent.AddListener(TargetDead);
        }

        public void SetTarget(Transform target)
        {
            currentTarget = target;
        }
        
        
        // ========== 动画模块 ==========
        void PlayAnimation(string stateName, float crossFade = 0.1f)
        {
            if (animator)
                animator.CrossFadeInFixedTime(stateName, crossFade);
        }

        bool IsPlayingAnimation(string stateName)
        {
            if (animator == null) return false;
            return animator.GetCurrentAnimatorStateInfo(0).IsName(stateName);
        }

        void UpdateAnimationParams()
        {
            // 举例：传递是否靠近敌人、是否自爆等参数
            if (currentTarget)
            {
                float dist = Vector3.Distance(transform.position, currentTarget.position);
                animator.SetFloat(DistanceToTarget, dist);
                animator.SetBool(HasTarget, true);
            }
            else
            {
                animator.SetBool(HasTarget, false);
            }
        }

        // ========== 声音模块 ==========
        void PlaySound(AudioClip clip, bool loop = false, float volume = 1f)
        {
            if (!audioSource || !clip) return;
            if (loop)
            {
                audioSource.clip = clip;
                audioSource.loop = true;
                audioSource.volume = volume;
                audioSource.Play();
            }
            else
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }

        void StopSound()
        {
            if (audioSource) audioSource.Stop();
        }

        // ========== 爆炸模块 ==========
        public void Explode()
        {
            if (isExploded) return;
            isExploded = true;
            Debug.Log($"Explode called! currentTarget={currentTarget?.name}, dist={(currentTarget ? Vector3.Distance(transform.position, currentTarget.position) : 0)}");
            StopMoving();
            StopSound();
            
            
            OnExploded?.Invoke(this.gameObject);
            explos.DoExplosion( transform.position);
            // 延迟销毁（让动画播放完）
            // float animLength = GetAnimationLength("Explode");
            // Destroy(gameObject, Mathf.Max(animLength, 0.5f));
            Destroy(gameObject);
        }

        float GetAnimationLength(string stateName)
        {
            if (!animator) return 0;
            RuntimeAnimatorController ac = animator.runtimeAnimatorController;
            foreach (var clip in ac.animationClips)
                if (clip.name == stateName) return clip.length;
            return 0;
        }

        // ========== 调试可视化 ==========
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, stoppingDistance);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, hearingRadius);
        }
        //受伤
        private void OnDead(Health health,DamageInfo damageInfo)
        {
            
            if (health != GetComponent<Health>() )
            {
                return;
            }

            Debug.Log("被击毁");
            Explode();
        }
        public void ReplaceModelRuntime(GameObject newModelPrefab)
        {
            // 找到挂载点（如果不存在则创建）
            Transform mount = transform.Find("ModelMount");
            if (mount == null)
            {
                mount = new GameObject("ModelMount").transform;
                mount.SetParent(transform);
                mount.localPosition = Vector3.zero;
                mount.localRotation = Quaternion.identity;
            }

            // 删除旧的模型子物体
            foreach (Transform child in mount)
            {
                Destroy(child.gameObject);
            }

            // 实例化新模型
            GameObject modelInstance = Instantiate(newModelPrefab, mount);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;

            // 如果 DroneController 中引用了模型根节点（如 modelRoot），重新赋值
            if (modelRoot != null) modelRoot = modelInstance.transform;
        }
    }



    public abstract class DroneExplosionHandler : MonoBehaviour
    {
        public abstract void DoExplosion( Vector3 explosionPosition);
    }


    public class StandardExplosion : DroneExplosionHandler
    {
        public float damage = 40f;
        public float radius = 5f;
        public GameObject effectPrefab;
        public AudioClip sound;
        public bool ignoreArmor = false;
        private Collider[] _explosionHitBuffer = new Collider[30];
        public float explosionDelay = 0f;
        public override void DoExplosion( Vector3 explosionPosition)
        {
            if (effectPrefab)
            {
                Instantiate(effectPrefab, explosionPosition, Quaternion.identity);
                Debug.Log($"爆炸于{explosionPosition}");
            }
            else
            {
                Debug.LogError("爆炸动画缺失");
            }
            if (sound)
                
                AudioSource.PlayClipAtPoint(sound, explosionPosition);
            else
            {
                Debug.LogError("爆炸声音缺失");
            }
            
            // 构造伤害信息
            DamageInfo dmgInfo = new DamageInfo(LevelManager.Instance?.MainCharacter)
            {
                damageValue = damage,
                isExplosion = true,
                damageType = DamageTypes.normal, // 如果枚举中没有，可用 DamageTypes.normal
                ignoreArmor = ignoreArmor
                
            };
            dmgInfo.damagePoint = explosionPosition;
            dmgInfo.damageNormal = Vector3.up;
            
            // 范围伤害：检测所有碰撞体，对敌方 DamageReceiver 造成伤害
            int hitCount = Physics.OverlapSphereNonAlloc(
                explosionPosition,
                radius,
                _explosionHitBuffer
            );

            for (int i = 0; i < hitCount; i++)
            {
                DamageReceiver dr = _explosionHitBuffer[i].GetComponent<DamageReceiver>();
                if (dr && Team.IsEnemy(Teams.player, dr.Team))
                {
                    if(dr.health)
                    {
                        if(dr.Team == Teams.player)
                        {
                            dmgInfo.damageValue *= 0.2f;
                            dr.Hurt(dmgInfo);
                            dmgInfo.damageValue *= 5f;
                            continue;
                        }
                        dr.Hurt(dmgInfo);
                    }
                }
            }

            // 发出爆炸声音事件（供其他AI感知）
            AIMainBrain.MakeSound(new AISound
            {
                fromCharacter = GetComponent<CharacterMainControl>(),//tocheck
                fromObject = gameObject,//to check
                pos = transform.position,
                fromTeam =  Teams.player,
                soundType = SoundTypes.combatSound,
                radius = 50f
            });
            
        }
    }
}
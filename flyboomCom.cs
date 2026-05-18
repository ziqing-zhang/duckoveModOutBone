using System;
using ItemStatsSystem;
using Pathfinding;
using UnityEngine;

namespace TestMod
{
    
    using UnityEngine;
using Duckov;
using Duckov.Scenes;

[RequireComponent(typeof(AICharacterController))]
public class DroneBehavior : MonoBehaviour
{
    public float explosionRadius = 3f;
    public float explosionDamage = 100f;
    public GameObject explosionEffect;
    public LayerMask enemyLayers;   // 敌人的 Layer
    
    public float safeDistanceToPlayer = 5f;   // 与玩家保持的最小距离（大于爆炸半径）
    
    public float detectEnemyRadius = 15f;     // 主动探测敌人的范围
    public float soundReactionRadius = 20f;   // 对枪声的反应范围

    private AICharacterController _aiController;
    private CharacterMainControl _playerCharacter;
    private CharacterMainControl _currentTarget;  // 攻击目标（CharacterMainControl类型）
    private Vector3? _soundMoveTarget;            // 由声音触发的移动目标点
    private bool _hasSoundTarget = false;
    
    public float autoDestructTime = 30f;   // 自动销毁时间（秒）
    private bool hasExploded = false;
    void Start()
    {
        _aiController = GetComponent<AICharacterController>();
        if (_aiController == null)
        {
            Debug.LogError("DroneBehavior 需要 AICharacterController 组件");
            enabled = false;
            return;
        }

        _playerCharacter = CharacterMainControl.Main;
        if (_playerCharacter == null)
            Debug.LogWarning("未找到玩家角色，无人机无法保持安全距离");
        Invoke(nameof(AutoSelfDestruct), autoDestructTime);
    }
    void AutoSelfDestruct()
    {
        if (hasExploded) return;
        hasExploded = true;
    
        // 触发技能自爆
        if (_aiController  && _aiController.CharacterMainControl )
            _aiController.CharacterMainControl.ReleaseSkill(SkillTypes.characterSkill);
    }
    private void OnEnable()
    {
        AIMainBrain.OnSoundSpawned += OnSoundHeard;
    }

    private void OnDisable()
    {
        AIMainBrain.OnSoundSpawned -= OnSoundHeard;
    }

    void Update()
    {
        // 1. 如果没有当前目标或目标无效，重新寻找
        if (_currentTarget == null || IsTargetInvalid(_currentTarget))
        {
            FindNearestEnemy();
        }

        // 2. 移动决策
        if (_currentTarget )
        {
            // 有攻击目标时，取消声音移动标记
            _hasSoundTarget = false;
            _soundMoveTarget = null;

            Vector3 targetPos = _currentTarget.transform.position;
            Vector3 movePos = targetPos;

            // 检查目标是否离玩家太近，如果是则调整移动点，保持安全距离
            if (_playerCharacter )
            {
                float distToPlayer = Vector3.Distance(targetPos, _playerCharacter.transform.position);
                if (distToPlayer < safeDistanceToPlayer)
                {
                    Vector3 dirFromPlayer = (targetPos - _playerCharacter.transform.position).normalized;
                    movePos = targetPos + dirFromPlayer * (safeDistanceToPlayer - distToPlayer + 1f);
                }
            }

            _aiController.MoveToPos(movePos);

            // 3. 检查自爆条件
            if (CanSelfDestruct())
            {
                if (hasExploded) return;
                hasExploded = true;
                _aiController.CharacterMainControl.ReleaseSkill(SkillTypes.characterSkill);
            }
        }
        else if (_hasSoundTarget && _soundMoveTarget.HasValue)
        {
            // 没有攻击目标，但有声音目标，向声音位置移动
            _aiController.MoveToPos(_soundMoveTarget.Value);

            // 如果已经到达声音目标附近，清除标记
            if (Vector3.Distance(transform.position, _soundMoveTarget.Value) < 1f)
            {
                _hasSoundTarget = false;
                _soundMoveTarget = null;
                _aiController.StopMove();
            }
        }
        else
        {
            // 无任何目标，静止不动
            _aiController.StopMove();
        }
    }

    /// <summary>
    /// 主动寻找最近的敌人（非玩家阵营）
    /// </summary>
    void FindNearestEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectEnemyRadius, enemyLayers);
        float closestDist = float.MaxValue;
        CharacterMainControl nearest = null;

        foreach (var hit in hits)
        {
            CharacterMainControl character = hit.GetComponent<CharacterMainControl>();
            if (character != null && character.Team != Teams.player && !character.Health.IsDead)
            {
                float d = Vector3.Distance(transform.position, character.transform.position);
                if (d < closestDist)
                {
                    closestDist = d;
                    nearest = character;
                }
            }
        }
        _currentTarget = nearest;
    }

    /// <summary>
    /// 检查目标是否无效（死亡或超出探测范围）
    /// </summary>
    bool IsTargetInvalid(CharacterMainControl target)
    {
        if (target == null || target.Health.IsDead)
            return true;
        float dist = Vector3.Distance(transform.position, target.transform.position);
        return dist > detectEnemyRadius * 1.5f;
    }

    /// <summary>
    /// 声音感知：只对战斗声音（枪声）反应，且忽略友军
    /// </summary>
    void OnSoundHeard(AISound sound)
    {
        // 忽略友军声音
        if (sound.fromTeam == Teams.player) return;
        // 只对战斗声音敏感
        if (sound.soundType != SoundTypes.combatSound) return;

        float dist = Vector3.Distance(transform.position, sound.pos);
        if (dist <= soundReactionRadius)
        {
            // 如果没有攻击目标，则设置声音移动目标
            if (_currentTarget == null)
            {
                _soundMoveTarget = sound.pos;
                _hasSoundTarget = true;
            }
        }
    }

    /// <summary>
    /// 判断是否可以自爆：爆炸范围内有非玩家敌人，且与玩家保持安全距离
    /// </summary>
    bool CanSelfDestruct()
    {
        
        // 检查与玩家的距离
        if (_playerCharacter)
        {
            float distToPlayer = Vector3.Distance(transform.position, _playerCharacter.transform.position);
            if (distToPlayer < safeDistanceToPlayer)
                return false;
        }

        // 检查爆炸范围内是否有非玩家敌人
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (var hit in hits)
        {
            CharacterMainControl character = hit.GetComponent<CharacterMainControl>();
            if (character && character.Team != Teams.player && !character.Health.IsDead)
            {
                return true;
            }
        }
        return false;
    }

    

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectEnemyRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, soundReactionRadius);
    }
    
}




public class DroneSelfDestructSkill : SkillBase
{
    public float explosionRadius = 3f;
    public float explosionDamage = 100f;
    public GameObject explosionEffect;
    
    // 可选：是否忽略护甲，是否使用元素伤害等
    public bool ignoreArmor = true;
    private Collider[] _explosionHitBuffer = new Collider[30];
    public override void OnRelease()
    {
        if (fromCharacter == null) return;
        // 播放特效
        if (explosionEffect != null)
            Instantiate(explosionEffect, fromCharacter.transform.position, Quaternion.identity);
        
        // 构造伤害信息
        DamageInfo dmgInfo = new DamageInfo(fromCharacter)
        {
            damageValue = explosionDamage,
            isExplosion = true,
            damageType = DamageTypes.normal, // 如果枚举中没有，可用 DamageTypes.normal
            ignoreArmor = ignoreArmor,
        };
        dmgInfo.damagePoint = fromCharacter.transform.position;
        dmgInfo.damageNormal = Vector3.up;
        
        // 范围伤害：检测所有碰撞体，对敌方 DamageReceiver 造成伤害
        int hitCount = Physics.OverlapSphereNonAlloc(
            fromCharacter.transform.position,
            explosionRadius,
            _explosionHitBuffer
        );

        for (int i = 0; i < hitCount; i++)
        {
            DamageReceiver dr = _explosionHitBuffer[i].GetComponent<DamageReceiver>();
            if (dr && Team.IsEnemy(fromCharacter.Team, dr.Team))
            {
                dr.Hurt(dmgInfo);
            }
        }
        // 爆炸后销毁无人机
        Destroy(fromCharacter.gameObject);
    }
}
}
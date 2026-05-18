using System;
using Duckov.Buffs;
using System.Collections.Generic;
using ItemStatsSystem;
using UnityEngine;
using Duckov.Utilities;
using ItemStatsSystem.Items;
using OutBone.MyTagManager;
using TestMod;
using Unity.Mathematics;

public class DamageToCapacityComponent : MonoBehaviour
{
    // 全局字典：记录装备了此物品的角色对应的 Health 实例和已扣除的生命上限
    public static Dictionary<Health, float> EquippedHealths = new Dictionary<Health, float>();

    [Header("转换设置")]
    public float conversionRatio = 1f;
    public bool onlyEnemyDamage = true;
    public bool preventOverkill = true;
    public float durabilityCostPerDamage = 1f;
    public bool applyPunishmentOnUnequip = true;

    [SerializeField]
    private Item ownerItem;

    private bool _isEquipped;
    private Health _boundHealth;
    private CharacterMainControl _characterController;
    private float _totalLifeDrained = 0f;          // 当前装备累计扣除量（用于卸下惩罚）
    private bool _isApplyingPunishment = false;
    private readonly string[] _tagsName=new string[]{"Equipment","Backpack"};
    private Tag[] _myTags=new Tag[2];//与上面一致
    private const string TotalDrainedKey = "DamageToCapacity_TotalDrained";
    
    
    
    // 加载状态（在Awake或OnEnable时调用）
    private void LoadFromVariables()
    {
        if (ownerItem == null) return;
        _totalLifeDrained = ownerItem.GetFloat(TotalDrainedKey, 0f);
    }

    // 保存状态（每次totalLifeDrained变化时调用）
    private void SaveToVariables()
    {
        if (ownerItem == null) return;
        ownerItem.SetFloat(TotalDrainedKey, _totalLifeDrained);
    }

    
    
    
    private void Awake()
    {
        
        
        if (ownerItem == null)
            ownerItem = GetComponent<Item>();
        if (ownerItem == null)
        {
            Debug.LogError("DamageToCapacityComponent 必须挂在 Item 物体上！", this);
            enabled = false;
            return;
        }
        ownerItem.onDurabilityChanged += OnDurabilityChanged;

        for (int i = 0; i < _tagsName.Length; i++)
        {
            switch (_tagsName[i])
            {
                case "Backpack":
                    _myTags[i] = GameplayDataSettings.Tags.Backpack;
                    break;
                case "Equipment":
                    _myTags[i] = MyTagManager.Tags.Equipment;
                    break;
                
            }
        }
        
    }
    

    private void OnEnable()
    {
        ownerItem.onPluggedIntoSlot += OnEquipped;
        ownerItem.onUnpluggedFromSlot += OnUnequipped;
        if (ownerItem.PluggedIntoSlot != null)
            OnEquipped(ownerItem);
    }

    private void OnDisable()
    {
        ownerItem.onPluggedIntoSlot -= OnEquipped;
        ownerItem.onUnpluggedFromSlot -= OnUnequipped;
        if (_isEquipped)
            OnUnequipped(ownerItem);
    }

    private void OnEquipped(Item item)
    {
        if (_isEquipped)
        {
            Debug.Log("已装备");
            return;
        }

        // 获取角色控制器
        CharacterMainControl characterCtrl = item.GetCharacterMainControl();
        if (characterCtrl == null)
        {
            Debug.LogWarning("无法获取角色控制器");
            return;
        }
        
        _boundHealth = characterCtrl.GetComponent<Health>();
        _characterController = characterCtrl;
        if (_boundHealth == null)
        {
            Debug.LogWarning("角色无 Health 组件");
            return;
        }
        LoadFromVariables();
        // 注册到全局字典（记录初始扣除量，可能之前装备过）
        if (!EquippedHealths.ContainsKey(_boundHealth))
            EquippedHealths[_boundHealth] = _totalLifeDrained;
        else
            EquippedHealths[_boundHealth] = _totalLifeDrained;

        // 应用当前扣除量（强制压制生命值一次）
       
        
        // 然后应用效果
        if (_totalLifeDrained > 0f)
        {
            float effectiveMax = _boundHealth.MaxHealth - _totalLifeDrained;
            if (effectiveMax < 1f) effectiveMax = 1f;
            if (_boundHealth.CurrentHealth > effectiveMax)
                _boundHealth.CurrentHealth = effectiveMax;
        }

        Health.OnHurt += OnCharacterHurt;
        _boundHealth.CurrentHealth = _boundHealth.CurrentHealth + 10f;
        Debug.Log("完成装备");
        _isEquipped = true;
    }

    private void OnUnequipped(Item item)
    {
        if (!_isEquipped) return;
        //Debug.Log($"[OnUnequipped] Enter, isEquipped={isEquipped}, boundHealth={boundHealth?.name ?? "null"}, totalLifeDrained={totalLifeDrained}");
        Health.OnHurt -= OnCharacterHurt;
        // 从全局字典中移除
        if (EquippedHealths.ContainsKey(_boundHealth))
            EquippedHealths.Remove(_boundHealth);
        // 惩罚：造成等量真实伤害
        if (applyPunishmentOnUnequip && _totalLifeDrained > 0f && _boundHealth != null)
        {
            if (_boundHealth.CurrentHealth < _boundHealth.MaxHealth - 10)
            {
                _boundHealth.CurrentHealth = Math.Max(1f, _boundHealth.CurrentHealth - 10f);
            }
            //1.考虑给个流血，而不是直伤 
            /*_isApplyingPunishment = true;
            DamageInfo realDamage = new DamageInfo(ownerItem?.GetCharacterMainControl() ?? ownerItem?.GetComponentInParent<CharacterMainControl>());
            realDamage.damageType = DamageTypes.realDamage;
            realDamage.damageValue = _totalLifeDrained;
            _boundHealth.Hurt(realDamage);
            _isApplyingPunishment = false;*/
            
            if(_characterController!= null)
            {
                //要么重写buff逻辑以制作一个buff，要么用资源库里的资源
                Buff newbleeding = MyBuffManager.myBufffs.mybleeding;
                if (newbleeding!=null)
                {
                    _characterController.AddBuff(newbleeding);
                    Debug.Log("施加流血buff");
                }
                else
                {
                    Debug.Log("流血buff未初始化");
                }
                
            }
            else
            {
                Debug.Log("获取角色buff控制器失败,物品可能已经被卸除");
            }
        }
        _boundHealth = null;
        _isEquipped = false;
        if (_totalLifeDrained > 20f)
        {
            for (int i=0; i < _myTags.Length; i++)
            {
                if (ownerItem.Tags.Contains(_tagsName[i]))
                    ownerItem.Tags.Remove(_myTags[i]);
            }
            
        }
        _characterController = null;
        Debug.Log("卸下装备");
    }

    private void OnCharacterHurt(Health health, DamageInfo damageInfo)
    {
        if (_boundHealth == null)
            Debug.Log("绑定的生命条为null");
        if (health != _boundHealth || _isApplyingPunishment)
        {
            Debug.Log("非目标生命条或拦截自施加伤害");
            return;
        }
        if (onlyEnemyDamage && !IsEnemy(damageInfo.fromCharacter) && !damageInfo.isFromBuffOrEffect)
        {
            Debug.Log("非敌人伤害");
            return;
        }

        float damage = damageInfo.finalDamage;
        if (damage <= 0)
        {
            Debug.Log("非法伤害");
            return;
        }
        if (health.CurrentHealth <= 0f) return;
        // 1. 恢复等量生命（抵消原伤害）
        if (health.CurrentHealth > health.MaxHealth / 2 )
        {
            Debug.Log("尚未启用");
            return;
        }

        if (_totalLifeDrained >= health.MaxHealth * 0.8f)
        {
            Debug.Log("过量承伤");
            return;
        }

       
        health.CurrentHealth = Mathf.Min(health.CurrentHealth + damage*0.5f, health.MaxHealth);
        Debug.Log("恢复生命");
        // 2. 更新累计扣除量（虚拟上限降低）
        _totalLifeDrained += damage*0.5f;
        if (_totalLifeDrained > health.MaxHealth * 0.8f)
        {
            _totalLifeDrained = health.MaxHealth * 0.8f;
        }
        SaveToVariables(); 
        // 同步到全局字典
        if (EquippedHealths.ContainsKey(health))
            EquippedHealths[health] = _totalLifeDrained;
        else
            EquippedHealths[health] = _totalLifeDrained;

        // 3. 强制压制当前生命值（可选，Harmony 会做，但这里立即生效）
        float effectiveMax = health.MaxHealth - _totalLifeDrained;
        if (effectiveMax < 1f) effectiveMax = 1f;
        if (health.CurrentHealth > effectiveMax)
            health.CurrentHealth = effectiveMax;
        //ownerItem.Durability -= damage * 0.5f * this.durabilityCostPerDamage;
        
    }

    private void OnDurabilityChanged(Item it)
    {
        _totalLifeDrained = it.GetFloat("DamageToCapacity_TotalDrained");
        if (_isEquipped)
        {
            EquippedHealths[_boundHealth] = _totalLifeDrained;
        }
        if (it != ownerItem) return;
        if (_totalLifeDrained <= 1f)
        {
            for (int i = 0; i < _myTags.Length; i++)
            {
                if (!ownerItem.Tags.Contains(_tagsName[i]))
                    ownerItem.Tags.Add(_myTags[i]);
            }
            
        }
        
    }

    private bool IsEnemy(CharacterMainControl fromCharacter)
    {
        if (fromCharacter == null) return true;
        return fromCharacter.Team != Teams.player;
    }
    
}

public class SubSlot : MonoBehaviour
{
    private Slot repairSlot;
    

    

    private void Start()
    {
        var slots = GetComponent<SlotCollection>();
        if (slots != null)
        {
            repairSlot = slots.GetSlot("RepairSlot");
            if (repairSlot != null)
            {
                repairSlot.onSlotContentChanged += OnRepairSlotChanged;
                // 处理已有物品
                if (repairSlot.Content != null) OnRepairSlotChanged(repairSlot);
            }
        }
        else
        {
            Debug.Log("未找到插槽：RepairSlot");
        }
    }

    private void OnDestroy()
    {
        if (repairSlot != null)
            repairSlot.onSlotContentChanged -= OnRepairSlotChanged;
    }

    private void OnRepairSlotChanged(Slot useslot)
    {
        if (useslot.Content == null)
        {
            Debug.Log("清空维修槽");
            return;
        }
        //TODO 触发使用，一段时间后卸除
        //TODO 检查耐久值是否正确
        Debug.Log($"useslot.Content.Durability:{useslot.Content.Durability}");
        Debug.Log($"useslot.Master.GetFloat(\"DamageToCapacity_TotalDrained\"):{useslot.Master.GetFloat("DamageToCapacity_TotalDrained")}");
        float heal=math.min( useslot.Master.GetFloat("DamageToCapacity_TotalDrained"),useslot.Content.Durability);
        useslot.Master.SetFloat("DamageToCapacity_TotalDrained", useslot.Master.GetFloat("DamageToCapacity_TotalDrained")-heal);
        useslot.Content.Durability = useslot.Master.Durability - heal;
        useslot.Master.DurabilityLoss += heal * 0.2f ;
        useslot.Master.Durability=useslot.Master.MaxDurability-useslot.Master.DurabilityLoss;
        Debug.Log($"修复耐久{heal}");
        Debug.Log($"上限损失{useslot.Master.GetFloat("DurabilityLoss")}");
        //useslot.Unplug();
        ItemUtilities.SendToPlayerCharacter(useslot.Content);
        useslot.Unplug();
        //如果耐久归零，考虑注销对象
    }
}
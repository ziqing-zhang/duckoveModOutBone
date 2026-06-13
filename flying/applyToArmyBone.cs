
using System;
using System.Collections.Generic;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using TestMod;
using TestMod.Drone;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;


public class AppendToArmyBone : MonoBehaviour
{
    
    

    [SerializeField]
    private Item ownerItem;

    private bool _isEquipped;
    private Health _boundHealth;
    
    private List<GameObject> flyingDrones = new List<GameObject>();
    
    
    private float spawnTime=0f;
    private float spawnDelay=5f;
    
    
    private void Awake()
    {
        
        if (ownerItem == null)
            ownerItem = GetComponent<Item>();
        if (ownerItem == null)
        {
            Debug.LogError("DamageToCapacityComponent 必须挂在 Item 物体上！", this);
            enabled = false;
            
        }
        
        
    }
    private Slot repairSlot;
    private Slot flyingSlot;
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
            else
            {
                Debug.LogError("armybone空维修槽");
            }
        }
        else
        {
            Debug.Log("armybone未找到插槽");
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
        
        if (_boundHealth == null)
        {
            Debug.LogWarning("角色无 Health 组件");
            return;
        }
        

        Health.OnHurt += OnCharacterHurt;
        Debug.Log("完成装备");
        _isEquipped = true;
    }

    private void OnUnequipped(Item item)
    {
        if (!_isEquipped) return;
        Health.OnHurt -= OnCharacterHurt;
        
        _boundHealth = null;
        _isEquipped = false;
        
        Debug.Log("卸下装备");
    }

    private void OnCharacterHurt(Health health, DamageInfo damageInfo)
    {
        if (_boundHealth == null)
            Debug.Log("绑定的生命条为null");
        if (health != _boundHealth )
        {
            return;
        }
        if ( !IsEnemy(damageInfo.fromCharacter) && !damageInfo.isFromBuffOrEffect)
        {
            Debug.Log("非敌人伤害");
            return;
        }

        if (damageInfo.isFromBuffOrEffect)
        {
            return;
        }
        ownerItem.Durability -= damageInfo.damageValue * 0.5f;
        if (spawnTime > 0f)
        {
            if(Time.time-spawnTime<spawnDelay) return;
        }
        CharacterMainControl fromCharacter = health.TryGetCharacter();
        Vector3 spawnPos = fromCharacter.transform.position + fromCharacter.transform.forward * 1.5f;
        spawnPos+=Vector3.up*0.5f;
        //todo 释放无人机似乎需要消耗插槽内物品，检查有没有实现,尚未实现，应该不是这里报错
        if (!spawnDrone(spawnPos))return;
        foreach (var onedrone in flyingDrones)
        {
            if (onedrone != null && Vector3.Distance(onedrone.transform.position, transform.position) < 20f)
            {
                if(damageInfo.fromCharacter!=null) onedrone.GetComponent<DroneController>().SetTarget(damageInfo.fromCharacter);
                else
                {
                    Debug.Log("受伤无来源");
                }
            }
        }
    }

    private void Update()
    {
        if (!_isEquipped)
        {
            return;
        }
        if (Input.GetKeyDown(KeyCode.F1))
        {
            CharacterMainControl mainc = LevelManager.Instance?.MainCharacter;
            if (!mainc) return;
            Vector3 spawnPos =  mainc.transform.position + mainc.transform.forward * 1.5f +Vector3.up * 2.5f;
            spawnDrone(spawnPos);
        }
    }

    bool spawnDrone(Vector3 spawnPos)
    {
        var slots = GetComponent<SlotCollection>();
        if (slots != null)
        {
            flyingSlot = slots.GetSlot("flyingSlot");
            if (flyingSlot != null)
            {
                flyingSlot.Content.StackCount -= 1;
            }
            else
            {
                Debug.Log("armybone未找到插槽flyingslot");
                return false;
            }
        }
        else
        {
            Debug.Log("armybone未找到插槽");
        }
        
        GameObject drone=MyPrefebManager.Instance.SpawnDrone(spawnPos,Quaternion.Euler(0,0,0),DroneController.exportType.normal);
        if (!drone)
        {
            Debug.Log("无人机初始化失败");
            return false;
        }
        spawnTime = Time.time;
        
        // 初始化 AI 控制器
        
        /*CharacterMainControl character = drone.GetComponent<CharacterMainControl>();
        if (character == null)
        {
            Debug.Log("无人机maincontrol初始化失败");
            return;
        }
        character.SetTeam(fromCharacter.Team); */
        //在队列中记录无人机
        flyingDrones.Add(drone);
        drone.GetComponent<DroneController>().OnExploded += FlyDroneExport;
        return true;
    }
    private void FlyDroneExport(GameObject drone)
    {
        if (!drone)
        {
            flyingDrones.RemoveAll(d => !d);
        }
        else
        {
            flyingDrones.Remove(drone);
        }
        
    }

    private bool IsEnemy(CharacterMainControl fromCharacter)
    {
        if (fromCharacter == null) return true;
        return fromCharacter.Team != Teams.player && fromCharacter.Team != Teams.all;
    }

    private void OnRepairSlotChanged(Slot useslot)
    {
        if (useslot.Content == null) return;
        float heal=math.min( useslot.Master.MaxDurability-useslot.Master.DurabilityLoss-useslot.Master.Durability,useslot.Content.Durability);
        useslot.Content.Durability -=  heal;
        useslot.Master.DurabilityLoss += heal * 0.2f ;
        useslot.Master.Durability=math.min(useslot.Master.MaxDurability-useslot.Master.DurabilityLoss,useslot.Master.Durability+heal);
        
        ItemUtilities.SendToPlayerCharacter(useslot.Content);
        useslot.Unplug();
    }
    
}
//跟具体的飞行物绑定
/*public class DeployDroneSkill : SkillBase
{
    public GameObject dronePrefab;

    public override void OnRelease()
    {
        Vector3 spawnPos = fromCharacter.transform.position + fromCharacter.transform.forward * 1.5f;
        GameObject drone = Instantiate(dronePrefab, spawnPos, Quaternion.identity);
        
        // 初始化 AI 控制器
        AICharacterController ai = drone.GetComponent<AICharacterController>();
        CharacterMainControl character = drone.GetComponent<CharacterMainControl>();
        ai.Init(character, spawnPos);
        character.SetTeam(fromCharacter.Team); // 与施法者同队
        
        
    }
}*/

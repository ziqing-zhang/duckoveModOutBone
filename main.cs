using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using OutBone.MyTagManager;
using Unity.Mathematics;
using UnityEngine;
namespace TestMod
{
    
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        //public Harmony harmony;
        
        public void Start()
        {
            HarmonyLoad.Load0Harmony();
            var harmony = new Harmony("com.testmod.iteminject");
            harmony.PatchAll();
            //加载asset bound
            LoadMyAssetBundle();
            
            
        }
       

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                //ItemUtilities.SendToPlayerCharacter(ItemAssetsCollection.InstantiateSync(361));
                ItemUtilities.SendToPlayerCharacter(ItemAssetsCollection.InstantiateSync(362));
                
                foreach (var formula in CraftingFormulaCollection.Instance.Entries)
                {
                    if (formula.id == "baseoutbone")
                    {
                        Debug.Log("在CraftingFormulaCollection里有");
                        Debug.Log(JsonUtility.ToJson(formula));
                    }
                }

                foreach (var formula in CraftingManager.UnlockedFormulaIDs)
                {
                    if (formula == "baseoutbone")
                    {
                        Debug.Log("在解锁物品id里有");
                        Debug.Log(JsonUtility.ToJson(formula));
                    }
                }
                
                
            }
            
        }
        public void LoadMyAssetBundle()
        {
            // 指定 AssetBundle 在玩家电脑上的存放路径
            string modFolder = Path.GetDirectoryName(this.GetType().Assembly.Location);
            string assetBundlePath = Path.Combine(modFolder, "outbone"); // 需根据实际情况调整
            // 使用 Unity 的标准 API 加载 AssetBundle
            AssetBundle myLoadedAssetBundle = AssetBundle.LoadFromFile(assetBundlePath);
            if (myLoadedAssetBundle == null)
            {
                Debug.LogError($"加载 AssetBundle 失败，请检查路径: {assetBundlePath}");
                return;
            }

            foreach (string assetPath in myLoadedAssetBundle.GetAllAssetNames())
            {
                Debug.Log("Asset: " + assetPath);
            }
            
            //加载tag
            MyTagManager.InitializeFromBundle(myLoadedAssetBundle);
            //初始化流血buff
            MyBuffManager.InitializeFromBundle(myLoadedAssetBundle);
            RegisterOutbone(myLoadedAssetBundle);
            RegisterOutboneRepairTool(myLoadedAssetBundle);
            
            
        }
        private static bool RegisterOutbone(AssetBundle bundle)
        {
            GameObject myPrefab = bundle.LoadAsset<GameObject>("OUTBONE");
            
            //可调用 `ItemStatsSystem.ItemAssetsCollection.AddDynamicEntry(Item prefab)` 添加自定义物品
            if (myPrefab != null)
            {
                // 实例化资源到场景中
                //GameObject newins=Instantiate(myPrefab);
                Debug.Log("资源实例化成功！");
                // 获取预制体上的 Item 组件（必须存在）
                // 在加载出来的预制体资产上直接添加组件
                
                Item customItemPrefab=myPrefab.GetComponent<Item>();
                if (customItemPrefab == null)
                {
                    Debug.LogError("预制体 OUTBONE 上缺少 Item 组件，无法注册为物品");
                    bundle.Unload(false);
                    return false;
                }

                customItemPrefab.MaxDurability = 40f;
                customItemPrefab.Durability = 40f;
                DamageToCapacityComponent comp = myPrefab.AddComponent<DamageToCapacityComponent>();
                // 设置组件参数（可选，也可以在组件的 Awake 中设置默认值）
                comp.conversionRatio = 1f;
                comp.onlyEnemyDamage = true;
                comp.durabilityCostPerDamage = 1f;
                comp.applyPunishmentOnUnequip = true;
                
                Debug.Log($"成功加载物品预制体");
                //customItemPrefab.CreateSlotsComponent();
                
                myPrefab.AddComponent<SubSlot>();
                
                
                ItemAssetsCollection.AddDynamicEntry(customItemPrefab);//测试方案
                return true;
            }

            Debug.LogError("在 AssetBundle 中未找到指定资源：OUTBONE");
            return false;
        }

        
        private static bool RegisterOutboneRepairTool(AssetBundle bundle)
        {
            GameObject myPrefab = bundle.LoadAsset<GameObject>("Assets/mod old/MYMOD/OUTBONE/OutBoneRepairTool.prefab");
            //可调用 `ItemStatsSystem.ItemAssetsCollection.AddDynamicEntry(Item prefab)` 添加自定义物品
            if (myPrefab != null)
            {
                // 实例化资源到场景中
                Instantiate(myPrefab);
                
                Item customItemPrefab=myPrefab.GetComponent<Item>();
                if (customItemPrefab == null)
                {
                    Debug.LogError("预制体 OUTBONE 上缺少 Item 组件，无法注册为物品");
                    bundle.Unload(false);
                    return false;
                }

                customItemPrefab.MaxDurability = 40f;
                customItemPrefab.Durability = 40f;
                ItemAssetsCollection.AddDynamicEntry(customItemPrefab);
                return true;
            }

            Debug.LogError("在 AssetBundle 中未找到指定资源：OUTBONE");
            return false;
        }
    }
    
    [HarmonyPatch(typeof(CraftingManager), "Load")]
    class Patch_CraftingManager_Load
    {
        static void Prefix()  // 注意：Prefix 不需要参数，除非你想访问 __instance
        {
            // 获取配方集合的单例
            var collection = CraftingFormulaCollection.Instance;
            if (collection == null) return;

            // 访问私有字段 "list"
            var listField = Traverse.Create(collection).Field<List<CraftingFormula>>("list");
            var formulas = listField.Value;
            if (formulas == null) return;

            
            // 构造你的自定义配方
            CraftingFormulaManger myFormulas = new CraftingFormulaManger();
            myFormulas.InitializeFormulas();
            if (formulas.All(f => f.id != "baseoutbone"))
            {
                formulas.Add(myFormulas.baseoutbone);
            }
            if (formulas.All(f => f.id != "repairoutbone"))
            {
                formulas.Add(myFormulas.repairoutbone);
            }
            
            
            
            Traverse.Create(collection).Field("_entries_ReadOnly").SetValue(null);
            
            Debug.Log($"已注入自定义配方 {myFormulas.baseoutbone.id}");
        }
    }
    [HarmonyPatch]
    public static class Health_CurrentHealth_LimitPatch
    {
        [HarmonyPatch(typeof(Health), "CurrentHealth", MethodType.Setter)]
        [HarmonyPrefix]
        public static void Prefix(Health __instance, ref float value)
        {
            
            // 从 EquippedHealths 字典中读取该角色的已扣除上限
            if (DamageToCapacityComponent.EquippedHealths.TryGetValue(__instance, out float deducted))
            {
                float effectiveMax = __instance.MaxHealth - deducted;
                if (effectiveMax < 1f) effectiveMax = 1f;
                if (value > effectiveMax)
                {
                    value = effectiveMax;
                }
            }
        }
    }
    //TODO 可能不需要
    [HarmonyPatch(typeof(LevelManager), "LoadOrCreateCharacterItemInstance")]
    static class Patch_AddDamageToCapacityComponent
    {
        static void Postfix(LevelManager __instance, ref UniTask<Item> __result)
        {
            __result.ContinueWith(item =>
            {
                if (item == null) return;
                if (item.GetComponent<DamageToCapacityComponent>() != null)
                {
                    Debug.Log("加载时已有组件");
                    return;
                }

                // 判断这个物品是否应该拥有该组件（可以根据物品类型ID、Tag等判断）
                if (ShouldHaveComponent(item))
                {
                    var comp = item.gameObject.AddComponent<DamageToCapacityComponent>();
                    // 组件会在Awake中自动调用LoadFromVariables，无需额外代码
                    Debug.Log($"为物品 {item.DisplayName} 附加 DamageToCapacityComponent");
                }
            }).Forget();
        }

        private static bool ShouldHaveComponent(Item item)
        {
            // 示例：根据物品的typeID或Tag决定
            return item.TypeID == Somedata.OutboneId;
        }
    }
}
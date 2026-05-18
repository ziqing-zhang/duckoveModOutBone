using UnityEngine;
using Duckov.Buffs;
using static UnityEngine.Object;

namespace TestMod
{
    public class MyBuffs
    {
        public Buff mybleeding { get; set; }
    }
    public class MyBuffManager
    {
        private static MyBuffs pribuffs;
        private static bool _initialized;
        public static bool InitializeFromBundle(AssetBundle bundle)
        {
            if (bundle == null)
            {
                Debug.LogError("InitializeFromBundle: bundle is null");
                return false;
            }
            MyBuffs slbu=new MyBuffs();
            // 加载 bundle 中所有类型为 Tag 的资产
            GameObject buffMyBleeding = bundle.LoadAsset<GameObject>("Assets/mod old/MYMOD/OUTBONE/mybleeding.prefab");
            if (buffMyBleeding != null )
            {
                Instantiate(buffMyBleeding);
                Debug.Log("myBleeding实例化成功！");
                slbu.mybleeding = buffMyBleeding.GetComponent<Buff>();
                if (slbu.mybleeding == null)
                {
                    Debug.LogError("mybleeding prefab 上没有 Buff 组件");
                    return false;
                }
            }
            else
            {
                Debug.LogError("No myBleeding buff found in bundle");
                return false;
            }
            pribuffs = slbu;
            _initialized = true;
            return true;
        }

        public static MyBuffs myBufffs
        {
            get
            {
                if (!_initialized)
                {
                    Debug.LogError("MyBuffs not initialized! Call MyBuffManager.Initialize(settings) first.");
                    return null;
                }
                return pribuffs;
            }
        }
        
    }
    
}
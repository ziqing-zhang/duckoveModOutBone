using System.Collections.Generic;
using ItemStatsSystem;
using UnityEngine.SceneManagement;

namespace TestMod
{ using System.Collections;
using System.IO;
using UnityEngine;
using Drone;

public class MyPrefebManager : MonoBehaviour
{
    
    private GameObject dronePrefab;
    private bool isLoading = false;
    private bool isLoaded = false;
    private Dictionary<string, GameObject> effectPrefabs = new Dictionary<string, GameObject>();
    private Dictionary<string, AudioClip> soundClips = new Dictionary<string, AudioClip>();
    private Dictionary<string,GameObject> dronePrefabs = new Dictionary<string, GameObject>();
   
    
    private static MyPrefebManager _instance;
    public static MyPrefebManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // 先在场景中查找（支持预先挂载的情况）
                _instance = FindObjectOfType<MyPrefebManager>();
                if (_instance == null)
                {
                    // 动态创建
                    GameObject go = new GameObject(nameof(MyPrefebManager));
                    _instance = go.AddComponent<MyPrefebManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    // 防止场景中意外添加多个实例
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 开始加载无人机资源（应在游戏启动时调用一次）
    /// </summary>
    public void StartLoadDroneAsset()
    {
        if (isLoaded || isLoading) return;
        StartCoroutine(LoadDroneAssetCoroutine());
    }

    private IEnumerator LoadDroneAssetCoroutine()
    {
        Debug.Log("starload asset");
        isLoading = true;
        isLoaded = false;
        // 确定平台相关路径
        string modFolder = Path.GetDirectoryName(this.GetType().Assembly.Location);
        string bundlePath = Path.Combine(modFolder, "myprefabs");//todo 在unity里改正确
        string flybundlePath = Path.Combine(modFolder, "flymod");
        if (!File.Exists(bundlePath))
        {
            Debug.LogError($"myprefabs not found at {bundlePath}");
            isLoading = false;
            yield break;
        }

        // 异步加载 Bundle
        AssetBundleCreateRequest bundleRequest = AssetBundle.LoadFromFileAsync(bundlePath);
        yield return bundleRequest;
        AssetBundleCreateRequest flybundleRequest=AssetBundle.LoadFromFileAsync(flybundlePath);
        yield return flybundleRequest;
        AssetBundle bundle = bundleRequest.assetBundle;
        AssetBundle flybundle = flybundleRequest.assetBundle;
        if (!bundle || !flybundle)
        {
            Debug.LogError("Failed to load AssetBundle");
            isLoading = false;
            yield break;
        }

        // 异步加载预制体
        
        AssetBundleRequest assetRequest = bundle.LoadAssetAsync<GameObject>("Assets/mod old/MYMOD/OUTBONE/flying.prefab");
        yield return assetRequest;

        dronePrefab = assetRequest.asset as GameObject;
        // 预加载爆炸特效和声音
        AssetBundleRequest effectRequest = bundle.LoadAssetAsync<GameObject>("Assets/mod old/MYMOD/OUTBONE/Fx_export.prefab");
        yield return effectRequest;
        effectPrefabs["normal"] = effectRequest.asset as GameObject;
        soundClips["normal"] = bundle.LoadAsset<AudioClip>("Assets/mod old/MYMOD/OUTBONE/explode-1.mp3");
        soundClips["flysound"]=bundle.LoadAsset<AudioClip>("Assets/mod old/MYMOD/OUTBONE/handheld-quadcopter-takes-off.mp3");
        
        
        
        // 假设你有一个模型预制体路径
        GameObject modelRequest = flybundle.LoadAsset<GameObject>("立柱无人机"); 
        yield return modelRequest;
        dronePrefabs["normal"] = modelRequest;
        if (!dronePrefab || !dronePrefabs["normal"])
        {
            Debug.Log("Failed to load DronePrefab from bundle");
        }
        else
        {
            if(!dronePrefab.GetComponent<DroneController>())
            {
                dronePrefab.AddComponent<DroneController>();
                
                dronePrefab.GetComponent<DroneController>().Initialize( soundClips["flysound"],0);
            }
            else
            {
                Debug.LogError("drone controller load faile");
                yield break;
            }
            
            isLoaded = true;
            isLoading = false;
            Debug.Log("Drone asset loaded successfully");
            
        }
        
        
    }

   

    /// <summary>
    /// 获取无人机预制体（仅当加载完成后才可用）
    /// </summary>
    public GameObject GetDronePrefab()
    {
        if (!isLoaded)
        {
            Debug.Log("Drone asset not ready yet, please wait for loading to complete.");
            return null;
        }
        return dronePrefab;
    }

    
    /// <summary>
    /// 实例化无人机（推荐使用此方法，自动检查就绪状态）
    /// </summary>
    public GameObject SpawnDrone(Vector3 position, Quaternion rotation,DroneController.exportType exportType)
    {
        if (!isLoaded || GetDronePrefab() == null)
        {
            Debug.LogError("Cannot spawn drone: asset not loaded.");
            return null;
        }
        if (dronePrefab == null)
        {
            Debug.Log("Drone prefab is null.");
            return null;
        }
        
        GameObject toreturn = Instantiate(dronePrefab, position, rotation);
        
        
        CharacterMainControl player = LevelManager.Instance?.MainCharacter;
        
        if (player != null)
        {
            Scene targetScene = player.gameObject.scene;
            if (toreturn.scene != targetScene)
                SceneManager.MoveGameObjectToScene(toreturn, targetScene);
            Debug.Log($"无人机部署于{toreturn.transform.position},主角在{player.transform.position}");
        }
        else
        {
            Debug.Log("获取角色失败");
        }
        switch (exportType)
        {
            case DroneController.exportType.normal:
                StandardExplosion toadd = null;
                
                toadd = toreturn.AddComponent<StandardExplosion>();
                toadd.effectPrefab=effectPrefabs["normal"];
                toadd.sound = soundClips["normal"];
                if (!toadd) return null;
                toreturn.GetComponent<DroneController>().explos = toadd;
                DroneController ctrl = toreturn.GetComponent<DroneController>();
                
                ctrl.ReplaceModelRuntime(dronePrefabs["normal"]);
                
                break;
        }
        
        toreturn.GetComponent<Animation>().Play("Scene");
       
        return toreturn;
        
    }
}

}

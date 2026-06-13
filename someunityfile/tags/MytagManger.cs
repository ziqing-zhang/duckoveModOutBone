

using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using Duckov.Utilities;   // 原有Tag命名空间，根据实际情况修改

namespace OutBone.MyTagManager
{
   
    public class MyTagSettings 
    {
        public Tag Outbone { get; set; }
        public Tag Equipment { get; set; }
        private List<Tag> _allTags = new List<Tag>();
        private ReadOnlyCollection<Tag> _readonlyTags;

        public ReadOnlyCollection<Tag> AllTags
        {
            get
            {
                if (_readonlyTags == null)
                    _readonlyTags = _allTags.AsReadOnly();
                return _readonlyTags;
            }
        }

        public void SetAllTags(List<Tag> tags)
        {
            _allTags = tags ?? new List<Tag>();
            _readonlyTags = null; // 重置
        }

        public Tag Get(string name)
        {
            foreach (var tag in _allTags)
            {
                if (tag != null && tag.name == name)
                    return tag;
            }
            return null;
        }
    
    }

    /// <summary>
    /// 静态管理器。需要手动初始化并传入一个MyTagSettings实例。
    /// </summary>
    public static class MyTagManager
    {
        private static MyTagSettings _tags;
        private static bool _initialized = false;

        public static void InitializeFromBundle(AssetBundle bundle, string outboneTagName, string equipmentTagName)
        {
            if (bundle == null)
            {
                Debug.LogError("InitializeFromBundle: bundle is null");
                return;
            }

            // 加载 bundle 中所有类型为 Tag 的资产
            Tag[] allTagAssets = bundle.LoadAllAssets<Tag>();
            if (allTagAssets == null || allTagAssets.Length == 0)
            {
                Debug.LogError("No Tag assets found in bundle");
                return;
            }

            var tagList = new List<Tag>(allTagAssets);
            var container = new MyTagSettings();
            container.SetAllTags(tagList);

            // 根据名称查找特殊 Tag
            container.Outbone = container.Get(outboneTagName);
            if (container.Outbone == null)
                Debug.LogWarning($"Outbone tag '{outboneTagName}' not found in bundle");

            container.Equipment = container.Get(equipmentTagName);
            if (container.Equipment == null)
                Debug.LogWarning($"Equipment tag '{equipmentTagName}' not found in bundle");

            _tags = container;
            _initialized = true;
            Debug.Log($"MyTagManager initialized with {tagList.Count} tags");
        }

        /// <summary>
        /// 访问Tags。使用前必须调用Initialize。
        /// </summary>
        public static MyTagSettings Tags
        {
            get
            {
                if (!_initialized)
                {
                    Debug.LogError("MyTagManager not initialized! Call MyTagManager.Initialize(settings) first.");
                    return null;
                }
                return _tags;
            }
        }
    }
}
﻿using System;
using System.Collections.Generic;
using BepInEx;
using UnityEngine;
using VNEI.UI;

namespace VNEI.Logic {
    public class Item {
        public readonly string internalName;
        public readonly string localizedName;
        public readonly string description;
        public readonly GameObject gameObject;
        public readonly bool isOnBlacklist;
        public readonly ItemType itemType;
        public readonly BepInPlugin mod;

        public readonly List<RecipeInfo> result = new List<RecipeInfo>();
        public readonly List<RecipeInfo> ingredient = new List<RecipeInfo>();

        private Sprite icon;

        public Item(string name, string localizeName, string description, Sprite icon, ItemType itemType, GameObject prefab) {
            internalName = name;
            localizedName = Localization.instance.Localize(localizeName);
            this.description = description;
            SetIcon(icon);
            gameObject = prefab;
            isOnBlacklist = Plugin.ItemBlacklist.Contains(name) || Plugin.ItemBlacklist.Contains(Indexing.CleanupName(name));
            this.itemType = itemType;
            mod = Indexing.GetModByPrefabName(prefab.name);
        }

        public string GetName() {
            string modName = mod != null ? mod.Name : string.Empty;
            return $"<color=orange><b>{localizedName}</b></color>{Environment.NewLine}({internalName}){Environment.NewLine}{modName}";
        }

        public string GetDescription() {
            return description;
        }

        public string GetTooltip() {
            if ((bool)gameObject && gameObject.TryGetComponent(out ItemDrop itemDrop)) {
                return itemDrop.m_itemData.GetTooltip();
            }

            return description;
        }

        public void SetIcon(Sprite sprite) {
            if (icon == null) {
                icon = sprite;
            } else {
                Log.LogInfo($"cannot set sprite for '{internalName}', icon already exists");
            }
        }

        public Sprite GetIcon() {
            return icon != null ? icon : RecipeUI.Instance.noSprite;
        }

        public string GetPrimaryName() {
            return localizedName.Length > 0 ? localizedName : internalName;
        }
    }
}

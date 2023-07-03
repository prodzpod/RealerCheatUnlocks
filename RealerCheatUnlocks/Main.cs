using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using UnityEngine;
using System;
using RoR2.UI;
using UnityEngine.EventSystems;
using RoR2;
using System.Linq;
using Aetherium.Achievements;
using System.ComponentModel;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Globalization;

// you can technically WRBStandalone download this, evil tho give mystic downloads :infdownload:
namespace RealerCheatUnlocks
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class Main : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "prodzpod";
        public const string PluginName = "RealerCheatUnlocks";
        public const string PluginVersion = "1.0.1";
        public static ManualLogSource Log;
        public static PluginInfo pluginInfo;
        public static Harmony Harmony;

        public void Awake()
        {
            pluginInfo = Info;
            Log = Logger;
            Harmony = new Harmony(PluginGUID);
            CheatUnlocksLoadoutButton.lockedIcon = LegacyResourcesAPI.Load<Sprite>("Textures/MiscIcons/texUnlockIcon");

            IL.RoR2.UI.LoadoutPanelController.Row.AddButton += il =>
            {
                ILCursor c = new(il);
                c.GotoNext(MoveType.After, x => x.MatchCallOrCallvirt<GameObject>(nameof(GameObject.GetComponent)));
                c.Emit(OpCodes.Dup); // HGButton
                c.Emit(OpCodes.Ldarg, 2); // icon
                c.Emit(OpCodes.Ldarg, 3); // titleToken
                c.Emit(OpCodes.Ldarg, 4); // bodyToken
                c.Emit(OpCodes.Ldarg, 5); // tooltipColor
                c.Emit(OpCodes.Ldarg, 6); // callback
                c.Emit(OpCodes.Ldarg, 7); // unlockableName
                c.Emit(OpCodes.Ldarg, 8); // viewableNode
                c.EmitDelegate<Action<HGButton, Sprite, string, string, Color, UnityAction, string, ViewablesCatalog.Node>>((button, icon, titleToken, bodyToken, tooltipColor, callback, unlockableName, viewableNode) =>
                {
                    if (unlockableName.IsNullOrWhiteSpace()) return;
                    CheatUnlocksLoadoutButton manager = button.gameObject.AddComponent<CheatUnlocksLoadoutButton>();
                    manager.hgButton = button;
                    manager.unlockableDef = UnlockableCatalog.GetUnlockableDef(unlockableName);
                    manager.achievementDef = AchievementManager.GetAchievementDefFromUnlockable(manager.unlockableDef.cachedName);
                    if (viewableNode != null) manager.viewableNode = viewableNode;
                    manager.icon = icon;
                    manager.title = titleToken;
                    manager.body = bodyToken;
                    manager.color = tooltipColor;
                    manager.onClick = callback;
                });
            };
            On.RoR2.UI.SurvivorIconController.Rebuild += (orig, self) =>
            {
                orig(self);
                if (self.GetComponent<CheatUnlocksSurvivorButton>() != null || self.survivorDef.unlockableDef == null) return;
                CheatUnlocksSurvivorButton manager = self.gameObject.AddComponent<CheatUnlocksSurvivorButton>();
                manager.hgButton = self.hgButton;
                manager.unlockableDef = self.survivorDef.unlockableDef;
                manager.achievementDef = AchievementManager.GetAchievementDefFromUnlockable(manager.unlockableDef.cachedName);
                manager.controller = self;
            };
        }
        public abstract class CheatUnlocksButton : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
        {
            public AchievementDef achievementDef;
            public UnlockableDef unlockableDef;
            public bool canRelock = true;
            public HGButton hgButton;
            public bool unlocked => GetUserProfile()?.HasUnlockable(unlockableDef.cachedName) ?? false;
            public bool achieved => GetUserProfile()?.HasAchievement(achievementDef.identifier) ?? false;
            public abstract void OnPointerClick(PointerEventData eventData);
            public UserProfile GetUserProfile() => LocalUserManager.readOnlyLocalUsersList.FirstOrDefault(v => v != null)?.userProfile;
            public bool TryToggle()
            {
                UserProfile userProfile = GetUserProfile();
                bool shouldGrantAchievement = achievementDef != null && !achieved;
                bool shouldGrantUnlockable = unlockableDef != null && !unlocked;
                if (shouldGrantAchievement || shouldGrantUnlockable)
                {
                    Util.PlaySound("Play_UI_menuClick", RoR2Application.instance.gameObject);
                    if (shouldGrantAchievement) userProfile.AddAchievement(achievementDef.identifier, isExternal: true);
                    if (shouldGrantUnlockable) userProfile.GrantUnlockable(unlockableDef);
                    return true;
                } 
                if (canRelock)
                {
                    Util.PlaySound("Play_UI_artifactDeselect", RoR2Application.instance.gameObject);
                    if (achievementDef != null) userProfile.RevokeAchievement(achievementDef.identifier);
                    if (unlockableDef != null)
                    {
                        userProfile.RevokeUnlockable(unlockableDef);
                        userProfile.RequestEventualSave();
                    }
                    return false;
                }
                return true;
            }
        }
        public class CheatUnlocksLoadoutButton : CheatUnlocksButton // the epicest
        {
            public Sprite icon;
            public string title;
            public string body;
            public Color color;
            public UnityAction onClick;
            public ViewablesCatalog.Node viewableNode;
            public static Sprite lockedIcon;
            public override void OnPointerClick(PointerEventData eventData)
            {
                if (eventData.button != PointerEventData.InputButton.Right) return;
                TooltipProvider providence = hgButton.GetComponent<TooltipProvider>();
                ViewableTag component = viewableNode != null ? hgButton.gameObject.GetComponent<ViewableTag>() : null;
                if (TryToggle())
                { // unlocked
                    hgButton.disableGamepadClick = false;
                    hgButton.disablePointerClick = false;
                    providence.titleColor = color;
                    providence.overrideTitleText = Language.GetString(title);
                    providence.overrideBodyText = Language.GetString(body);
                    hgButton.onClick.AddListener(onClick);
                    if ((bool)component)
                    {
                        component.viewableName = viewableNode.fullName;
                        component.Refresh();
                    }
                    ((Image)hgButton.targetGraphic).sprite = icon;
                }
                else
                { // relocked
                    hgButton.disableGamepadClick = true;
                    hgButton.disablePointerClick = true;
                    providence.titleColor = Color.gray;
                    providence.overrideTitleText = Language.GetString("UNIDENTIFIED");
                    providence.overrideBodyText = unlockableDef.getHowToUnlockString();
                    hgButton.onClick.RemoveListener(onClick);
                    if ((bool)component && (bool)component.tagInstance)
                    {
                        Destroy(component.tagInstance);
                        component.tagInstance = null;
                    }
                    ((Image)hgButton.targetGraphic).sprite = lockedIcon;
                }
            }
        }
        public class CheatUnlocksSurvivorButton : CheatUnlocksButton
        {
            public SurvivorIconController controller;
            public override void OnPointerClick(PointerEventData eventData)
            {
                if (eventData.button != PointerEventData.InputButton.Right) return;
                if (TryToggle())
                { // unlocked
                    controller.survivorIsUnlocked = true;
                    hgButton.disableGamepadClick = false;
                    hgButton.disablePointerClick = false;
                }
                else
                { // relocked
                    controller.survivorIsUnlocked = false;
                    hgButton.disableGamepadClick = true;
                    hgButton.disablePointerClick = true;
                }
                controller.UpdateAvailability();
                controller.Rebuild();
                // no need to do anything else; Rebuild() is called automatically
            }
        }
    }
}

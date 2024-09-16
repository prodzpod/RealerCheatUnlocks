using BepInEx;
using RoR2;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using System.Linq;
using UnityEngine.UI;

// yeah
namespace CheatUnlocks
{
    public class CheatUnlocksPlugin : BaseUnityPlugin
    {
        public static void Awake()
        {
            IL.RoR2.UI.LogBook.LogBookController.BuildEntriesPage += (il) =>
            {
                ILCursor c = new ILCursor(il);

                int hgButtonPos = -1;
                int entryDisplayClassPos = -1;
                Mono.Cecil.FieldReference entryFieldRef = null;

                if (c.TryGotoNext(
                    x => x.MatchLdloc(out hgButtonPos),
                    x => x.MatchLdcI4(out _),
                    x => x.MatchStfld<RoR2.UI.HGButton>("updateTextOnHover")
                ) && c.TryGotoNext(
                    x => x.MatchLdloc(out entryDisplayClassPos),
                    x => x.MatchLdfld(out entryFieldRef),
                    x => x.MatchLdfld<RoR2.UI.LogBook.Entry>("viewableNode")
                ) && c.TryGotoPrev(
                    MoveType.After,
                    x => x.MatchLdloc(hgButtonPos),
                    x => x.MatchLdcI4(1),
                    x => x.MatchCallOrCallvirt<UnityEngine.UI.Selectable>("set_interactable")
                ))
                {
                    c.Emit(OpCodes.Ldloc, hgButtonPos);
                    c.Emit(OpCodes.Ldloc, entryDisplayClassPos);
                    c.Emit(OpCodes.Ldfld, entryFieldRef);
                    c.Emit(OpCodes.Ldarg, 0);
                    c.EmitDelegate<System.Action<RoR2.UI.HGButton, RoR2.UI.LogBook.Entry, RoR2.UI.LogBook.LogBookController>>((button, entry, logbookController) =>
                    {
                        CheatUnlocksLogbookButton MakeCheatButton()
                        {
                            CheatUnlocksLogbookButton component = button.gameObject.AddComponent<CheatUnlocksLogbookButton>();
                            component.entry = entry;
                            component.logbookController = logbookController;
                            component.hgButton = button;
                            component.hgButtonDisableGamepadClick = button.disableGamepadClick;
                            component.hgButtonDisablePointerClick = button.disablePointerClick;
                            component.hgButtonImageOnInteractable = button.imageOnInteractable;
                            return component;
                        }

                        switch (entry.category.nameToken)
                        {
                            case "LOGBOOK_CATEGORY_ACHIEVEMENTS":
                                {
                                    CheatUnlocksLogbookButton component = MakeCheatButton();

                                    component.achievementName = ((AchievementDef)entry.extraData).identifier;
                                    component.unlockableDef = UnlockableCatalog.GetUnlockableDef(((AchievementDef)entry.extraData).unlockableRewardIdentifier);

                                    component.initializeGraphics = CheatUnlocksLogbookButton.InitializeGraphicsChallenge;
                                }
                                break;
                            case "LOGBOOK_CATEGORY_ITEMANDEQUIPMENT":
                                {
                                    CheatUnlocksLogbookButton component = MakeCheatButton();

                                    component.pickupIndex = (PickupIndex)entry.extraData;

                                    PickupDef pickupDef = PickupCatalog.GetPickupDef(component.pickupIndex);
                                    ItemIndex itemIndex = pickupDef.itemIndex;
                                    EquipmentIndex equipmentIndex = pickupDef.equipmentIndex;
                                    if (itemIndex != ItemIndex.None)
                                    {
                                        ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                                        if (itemDef && itemDef.unlockableDef)
                                        {
                                            AchievementDef achievementDef = AchievementManager.allAchievementDefs.FirstOrDefault(x => x.unlockableRewardIdentifier == itemDef.unlockableDef.cachedName);
                                            if (achievementDef != null) component.achievementName = achievementDef.identifier;
                                            component.unlockableDef = itemDef.unlockableDef;
                                        }
                                    }
                                    else if (equipmentIndex != EquipmentIndex.None)
                                    {
                                        EquipmentDef equipmentDef = EquipmentCatalog.GetEquipmentDef(equipmentIndex);
                                        if (equipmentDef && equipmentDef.unlockableDef)
                                        {
                                            AchievementDef achievementDef = AchievementManager.allAchievementDefs.FirstOrDefault(x => x.unlockableRewardIdentifier == equipmentDef.unlockableDef.cachedName);
                                            if (achievementDef != null) component.achievementName = achievementDef.identifier;
                                            component.unlockableDef = equipmentDef.unlockableDef;
                                        }
                                    }
                                }
                                break;
                            case "LOGBOOK_CATEGORY_MONSTER":
                                {
                                    CheatUnlocksLogbookButton component = MakeCheatButton();

                                    CharacterBody characterBody = (CharacterBody)entry.extraData;
                                    DeathRewards deathRewards = characterBody.GetComponent<DeathRewards>();
                                    if (deathRewards) component.unlockableDef = deathRewards.logUnlockableDef;

                                    component.perBodyStatDef = RoR2.Stats.PerBodyStatDef.killsAgainst;
                                    component.bodyIndex = characterBody.bodyIndex;
                                }
                                break;
                            case "LOGBOOK_CATEGORY_STAGE":
                                {
                                    CheatUnlocksLogbookButton component = MakeCheatButton();

                                    UnlockableDef unlockableLogFromSceneName = SceneCatalog.GetUnlockableLogFromBaseSceneName(((SceneDef)entry.extraData).baseSceneName);
                                    if (unlockableLogFromSceneName) component.unlockableDef = unlockableLogFromSceneName;
                                }
                                break;
                            case "LOGBOOK_CATEGORY_SURVIVOR":
                                {
                                    CheatUnlocksLogbookButton component = MakeCheatButton();
                                    
                                    CharacterBody characterBody = (CharacterBody)entry.extraData;
                                    SurvivorDef survivorDef = SurvivorCatalog.FindSurvivorDefFromBody(characterBody.gameObject);
                                    UnlockableDef unlockableDef = survivorDef.unlockableDef;
                                    if (unlockableDef)
                                    {
                                        AchievementDef achievementDef = AchievementManager.allAchievementDefs.FirstOrDefault(x => x.unlockableRewardIdentifier == unlockableDef.cachedName);
                                        if (achievementDef != null) component.achievementName = achievementDef.identifier;
                                        component.unlockableDef = unlockableDef;
                                    }

                                    component.perBodyStatDef = RoR2.Stats.PerBodyStatDef.totalWins;
                                    component.bodyIndex = characterBody.bodyIndex;
                                }
                                break;
                        }
                    });
                }
            };

            On.EclipseDifficultyMedalDisplay.Refresh += (orig, self) =>
            {
                orig(self);
                var button = self.GetComponent<CheatUnlocksEclipseButton>();
                if (!button) button = self.gameObject.AddComponent<CheatUnlocksEclipseButton>();
                button.medalDisplay = self;
                button.localUser = LocalUserManager.GetFirstLocalUser();
                button.survivorDef = (button.localUser != null) ? button.localUser.userProfile.GetSurvivorPreference() : null;
            };
        }

        public class CheatUnlocksLogbookButton : MonoBehaviour, IPointerClickHandler
        {
            public RoR2.UI.LogBook.Entry entry;
            public RoR2.UI.LogBook.LogBookController logbookController;
            public string achievementName;
            public PickupIndex pickupIndex = PickupIndex.none;
            public UnlockableDef unlockableDef;
            public RoR2.Stats.PerBodyStatDef perBodyStatDef;
            public BodyIndex bodyIndex = BodyIndex.None;
            public UnityAction entryOnClick;
            public bool canRelock = true;

            public RoR2.UI.HGButton hgButton;
            public bool hgButtonDisableGamepadClick;
            public bool hgButtonDisablePointerClick;
            public UnityEngine.UI.Image hgButtonImageOnInteractable;

            public System.Action<GameObject, RoR2.UI.LogBook.EntryStatus> initializeGraphics = InitializeGraphicsDefault;

            public static void InitializeGraphicsDefault(GameObject buttonObject, RoR2.UI.LogBook.EntryStatus entryStatus)
            {
                ChildLocator childLocator = buttonObject.GetComponent<ChildLocator>();
                RawImage rawImage = null;
                if (childLocator)
                {
                    rawImage = childLocator.FindChild("BG").GetComponent<RawImage>();
                }
                if (rawImage)
                {
                    switch (entryStatus)
                    {
                        case RoR2.UI.LogBook.EntryStatus.Available:
                        case RoR2.UI.LogBook.EntryStatus.New:
                            rawImage.enabled = true;
                            break;
                    }
                }
            }

            public static void InitializeGraphicsChallenge(GameObject buttonObject, RoR2.UI.LogBook.EntryStatus entryStatus)
            {
                ChildLocator childLocator = buttonObject.GetComponent<ChildLocator>();
                switch (entryStatus)
                {
                    case RoR2.UI.LogBook.EntryStatus.Locked:
                        childLocator.FindChild("HasBeenUnlocked").gameObject.SetActive(false);
                        break;
                    case RoR2.UI.LogBook.EntryStatus.Unencountered:
                        childLocator.FindChild("HasBeenUnlocked").gameObject.SetActive(false);
                        childLocator.FindChild("CantBeAchieved").gameObject.SetActive(false);
                        break;
                    case RoR2.UI.LogBook.EntryStatus.Available:
                        childLocator.FindChild("CantBeAchieved").gameObject.SetActive(false);
                        break;
                    case RoR2.UI.LogBook.EntryStatus.New:
                        childLocator.FindChild("CantBeAchieved").gameObject.SetActive(false);
                        break;
                }
            }

            public void Start()
            {
                entryOnClick = delegate ()
                {
                    if (entry != null && logbookController) logbookController.ViewEntry(entry);
                };
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData.button == PointerEventData.InputButton.Right)
                {
                    UserProfile userProfile = logbookController.LookUpUserProfile();

                    bool achievementExists = !string.IsNullOrEmpty(achievementName);
                    bool userHasAchievement = achievementExists ? userProfile.HasAchievement(achievementName) : false;
                    bool unlockableExists = unlockableDef;
                    bool userHasUnlockable = unlockableExists ? userProfile.HasUnlockable(unlockableDef) : false;
                    bool pickupExists = pickupIndex != PickupIndex.none;
                    bool statExists = perBodyStatDef != null;

                    if (achievementExists && !userHasAchievement || unlockableExists && !userHasUnlockable)
                    {
                        Util.PlaySound("Play_UI_menuClick", RoR2Application.instance.gameObject);
                        if (achievementExists && !userHasAchievement)
                        {
                            foreach (var notification in RoR2.UI.AchievementNotificationPanel.instancesList) Destroy(notification.gameObject);
                            userProfile.AddAchievement(achievementName, true);
                        }
                        if (unlockableExists && !userHasUnlockable)
                        {
                            userProfile.GrantUnlockable(unlockableDef);
                        }
                    }
                    else
                    {
                        if (pickupExists && !userProfile.HasDiscoveredPickup(pickupIndex))
                        {
                            userProfile.SetPickupDiscovered(pickupIndex, true);
                            Util.PlaySound("Play_UI_menuClick", RoR2Application.instance.gameObject);
                        }
                        else if (statExists && userProfile.statSheet.GetStatValueULong(perBodyStatDef.FindStatDef(bodyIndex)) <= 0u)
                        {
                            userProfile.statSheet.PushStatValue(perBodyStatDef, bodyIndex, 1u);
                            Util.PlaySound("Play_UI_menuClick", RoR2Application.instance.gameObject);
                        }
                        else if (canRelock)
                        {
                            if (achievementExists && userHasAchievement)
                            {
                                foreach (var notification in RoR2.UI.AchievementNotificationPanel.instancesList) Destroy(notification.gameObject);
                                userProfile.RevokeAchievement(achievementName);
                            }
                            if (unlockableExists && userHasUnlockable)
                            {
                                userProfile.RevokeUnlockable(unlockableDef);
                                userProfile.RequestEventualSave();
                            }
                            if (pickupExists) userProfile.SetPickupDiscovered(pickupIndex, false);

                            if (entry.viewableNode != null)
                            {
                                var viewableTag = hgButton.gameObject.GetComponent<RoR2.UI.ViewableTag>();
                                if (viewableTag && viewableTag.tagInstance)
                                {
                                    Destroy(viewableTag.tagInstance);
                                    viewableTag.tagInstance = null;
                                }
                            }

                            Util.PlaySound("Play_UI_artifactDeselect", RoR2Application.instance.gameObject);
                        }
                    }

                    RoR2.UI.LogBook.EntryStatus entryStatus = entry.GetStatus(userProfile);
                    RoR2.UI.TooltipContent tooltipContent = entry.GetTooltipContent(userProfile, entryStatus);

                    var initializeElementGraphics = entry.category.initializeElementGraphics;
                    if (initializeElementGraphics != null)
                    {
                        initializeElementGraphics(hgButton.gameObject, entry, entryStatus, userProfile);
                    }
                    if (initializeGraphics != null) initializeGraphics(hgButton.gameObject, entryStatus);

                    if (entryStatus >= RoR2.UI.LogBook.EntryStatus.Available)
                    {
                        hgButton.onClick.AddListener(entryOnClick);
                        hgButton.disableGamepadClick = hgButtonDisableGamepadClick;
                        hgButton.disablePointerClick = hgButtonDisablePointerClick;
                        hgButton.imageOnInteractable = hgButtonImageOnInteractable;
                    }
                    else
                    {
                        hgButton.onClick.RemoveListener(entryOnClick);
                        hgButton.disableGamepadClick = true;
                        hgButton.disablePointerClick = true;
                        hgButton.imageOnInteractable = null;
                    }

                    Color titleColor = tooltipContent.titleColor;
                    titleColor.a = 0.2f;
                    hgButton.hoverToken = Language.GetStringFormatted("LOGBOOK_HOVER_DESCRIPTION_FORMAT", new object[]
                    {
                        tooltipContent.GetTitleText(),
                        tooltipContent.GetBodyText(),
                        ColorUtility.ToHtmlStringRGBA(titleColor)
                    });
                }
            }
        }

        public class CheatUnlocksEclipseButton : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
        {
            public LocalUser localUser;
            public SurvivorDef survivorDef;
            public EclipseDifficultyMedalDisplay medalDisplay;

            public void OnEnable()
            {
                InstanceTracker.Add(this);
            }

            public void OnDisable()
            {
                InstanceTracker.Remove(this);
            }

            public void SetEclipseLevelUnlocked(bool unlock)
            {
                var unlockableDef = HG.ListUtils.GetSafe(EclipseRun.GetEclipseLevelUnlockablesForSurvivor(survivorDef), medalDisplay.eclipseLevel - EclipseRun.minUnlockableEclipseLevel + 1);
                if (unlock)
                {
                    localUser.userProfile.GrantUnlockable(unlockableDef);

                    var eclipseCompletedAsAllSurvivors = true;
                    foreach (var otherSurvivor in SurvivorCatalog.orderedSurvivorDefs)
                    {
                        if (medalDisplay.ShouldDisplaySurvivor(otherSurvivor, localUser))
                        {
                            var eclipseLevelAsOtherSurvivor = EclipseRun.GetLocalUserSurvivorCompletedEclipseLevel(localUser, otherSurvivor);
                            if (eclipseLevelAsOtherSurvivor < medalDisplay.eclipseLevel)
                            {
                                eclipseCompletedAsAllSurvivors = false;
                                break;
                            }
                        }
                    }

                    if (medalDisplay.iconImage)
                    {
                        medalDisplay.iconImage.sprite = eclipseCompletedAsAllSurvivors ? medalDisplay.completeSprite : medalDisplay.incompleteSprite;
                    }
                }
                else
                {
                    localUser.userProfile.RevokeUnlockable(unlockableDef);
                    localUser.userProfile.RequestEventualSave();
                    if (medalDisplay.iconImage) medalDisplay.iconImage.sprite = medalDisplay.unearnedSprite;
                }
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData.button == PointerEventData.InputButton.Right)
                {
                    var unlockableDef = HG.ListUtils.GetSafe(EclipseRun.GetEclipseLevelUnlockablesForSurvivor(survivorDef), medalDisplay.eclipseLevel - EclipseRun.minUnlockableEclipseLevel + 1);
                    if (unlockableDef)
                    {
                        if (!localUser.userProfile.HasUnlockable(unlockableDef))
                        {
                            foreach (var medal in InstanceTracker.GetInstancesList<CheatUnlocksEclipseButton>().Where(x => x.survivorDef == survivorDef && x.medalDisplay.eclipseLevel <= medalDisplay.eclipseLevel))
                            {
                                medal.SetEclipseLevelUnlocked(true);
                            }
                            Util.PlaySound("Play_UI_menuClick", RoR2Application.instance.gameObject);
                            Util.PlaySound("Play_UI_achievementUnlock", RoR2Application.instance.gameObject);
                        }
                        else
                        {
                            foreach (var medal in InstanceTracker.GetInstancesList<CheatUnlocksEclipseButton>().Where(x => x.survivorDef == survivorDef && x.medalDisplay.eclipseLevel >= medalDisplay.eclipseLevel))
                            {
                                medal.SetEclipseLevelUnlocked(false);
                            }
                            Util.PlaySound("Play_UI_artifactDeselect", RoR2Application.instance.gameObject);
                        }
                    }
                }
            }
        }
    }
}

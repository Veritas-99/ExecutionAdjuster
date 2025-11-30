using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Base.Global;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.GameComponents;

namespace ExecutionAdjuster
{
    public class MainModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            var harmony = new Harmony("ExecutionAdjuster");
            harmony.PatchAll();
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            InformationManager.DisplayMessage(new InformationMessage("Loaded Execution Adjuster", new Color(1f, 0f, 0f)));
        }
    }

    [HarmonyPatch(
        typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultExecutionRelationModel),
        nameof(TaleWorlds.CampaignSystem.GameComponents.DefaultExecutionRelationModel.GetRelationChangeForExecutingHero)
    )]
    class GetRelationChangeForExecutingHeroPatch
    {
        static bool Prefix(
            ref Hero victim,
            ref Hero hero,
            out bool showQuickNotification,
            ref int __result,
            DefaultExecutionRelationModel __instance
        )
        {
            __result = GetRelationChangeForExecutingHero(__instance, victim, hero, out showQuickNotification);
            return false;
        }

        static void Postfix(ref int __result)
        {
            __result = Convert.ToInt32(__result * MCMUISettings.Instance?.RelationMultiplier ?? 1f);
        }

        public static int GetRelationChangeForExecutingHero(DefaultExecutionRelationModel instance, Hero victim, Hero hero, out bool showQuickNotification)
        {
            int result = 0;
            showQuickNotification = false;
            if (victim.GetTraitLevel(DefaultTraits.Honor) < 0)
            {
                if (!hero.IsHumanPlayerCharacter && hero != victim && hero.Clan != null && hero.Clan.Leader == hero)
                {
                    if (hero.Clan == victim.Clan)
                    {
                        result = Convert.ToInt32(instance.PlayerExecutingHeroClanRelationPenaltyDishonorable * MCMUISettings.Instance?.RelationMultiplierClanOfExecutee ?? 1f);
                        showQuickNotification = true;
                    }
                    else if (victim.IsFriend(hero))
                    {
                        result = instance.PlayerExecutingHeroFriendRelationPenaltyDishonorable;
                        showQuickNotification = true;
                    }
                    else if (hero.MapFaction == victim.MapFaction && hero.CharacterObject.Occupation == Occupation.Lord)
                    {
                        result = instance.PlayerExecutingHeroFactionRelationPenaltyDishonorable;
                        showQuickNotification = true;
                    }
                    else if (MCMUISettings.Instance?.RelationGainOnExecutingLordEnemy ?? false)
                        if (victim.IsEnemy(hero))
                        {
                            result = instance.PlayerExecutingHeroFriendRelationPenalty * -1;
                            showQuickNotification = true;
                        }
                }
            }
            else if (!hero.IsHumanPlayerCharacter && hero != victim && hero.Clan != null && hero.Clan.Leader == hero)
            {
                if (hero.Clan == victim.Clan)
                {
                    result = Convert.ToInt32(instance.PlayerExecutingHeroClanRelationPenalty * MCMUISettings.Instance?.RelationMultiplierClanOfExecutee ?? 1f);
                    showQuickNotification = true;
                }
                else if (victim.IsFriend(hero))
                {
                    result = instance.PlayerExecutingHeroFriendRelationPenalty;
                    showQuickNotification = true;
                }
                else if (hero.MapFaction == victim.MapFaction && hero.CharacterObject.Occupation == Occupation.Lord)
                {
                    result = instance.PlayerExecutingHeroFactionRelationPenalty;
                    showQuickNotification = false;
                }
                else if (hero.GetTraitLevel(DefaultTraits.Honor) > 0 && !victim.Clan.IsRebelClan)
                {
                    result = instance.PlayerExecutingHeroHonorableNobleRelationPenalty;
                    showQuickNotification = true;
                }
                else if (MCMUISettings.Instance?.RelationGainOnExecutingLordEnemy ?? false)
                    if (victim.IsEnemy(hero))
                    {
                        result = instance.PlayerExecutingHeroFriendRelationPenaltyDishonorable * -1;
                        showQuickNotification = true;
                    }
            }

            return result;
        }
    }

    [HarmonyPatch(
        typeof(TaleWorlds.CampaignSystem.CharacterDevelopment.TraitLevelingHelper),
        nameof(TaleWorlds.CampaignSystem.CharacterDevelopment.TraitLevelingHelper.OnLordExecuted)
    )]
    class OnLordExecutedPatch
    {
        static bool Prefix()
        {
            AffectHonor(-1000);

            return false;
        }

        public static void AffectHonor(int val)
        {
            var method = typeof(TraitLevelingHelper).GetMethod("AddPlayerTraitXPAndLogEntry", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            method.Invoke(null, new object[]{
                DefaultTraits.Honor,
                Convert.ToInt32(val * MCMUISettings.Instance?.HonorMultiplier ?? 1f),
                ActionNotes.SacrificedTroops,
                null
            });
        }
    }

    [HarmonyPatch(
        typeof(TaleWorlds.CampaignSystem.Actions.KillCharacterAction),
        nameof(ApplyInternal)
    )]
    class KillCharacterActionInternalPatch
    {
        static bool Prefix(
            ref Hero victim,
            ref Hero killer,
            ref KillCharacterAction.KillCharacterActionDetail actionDetail,
            ref bool showNotification,
            ref bool isForced
        )
        {
            ApplyInternal(victim, killer, actionDetail, showNotification, isForced);

            return false;
        }

        private static void ApplyInternal(
            Hero victim,
            Hero killer,
            KillCharacterAction.KillCharacterActionDetail actionDetail,
            bool showNotification,
            bool isForced = false
        )
        {
            if (!victim.CanDie(actionDetail) && !isForced)
                return;
            if (!victim.IsAlive)
            {
                Debug.FailedAssert("Victim: " + (object)victim.Name + " is already dead!", "C:\\Develop\\MB3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Actions\\KillCharacterAction.cs", nameof(ApplyInternal), 40);
            }
            else
            {
                if (victim.IsNotable && victim.Issue?.IssueQuest != null)
                    Debug.FailedAssert("Trying to kill a notable that has quest!", "C:\\Develop\\MB3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Actions\\KillCharacterAction.cs", nameof(ApplyInternal), 47);
                if ((victim.PartyBelongedTo?.MapEvent != null || victim.PartyBelongedTo?.SiegeEvent != null) && victim.DeathMark == KillCharacterAction.KillCharacterActionDetail.None)
                {
                    victim.AddDeathMark(killer, actionDetail);
                }
                else
                {
                    CampaignEventDispatcher.Instance.OnBeforeHeroKilled(victim, killer, actionDetail, showNotification);
                    if (victim.IsHumanPlayerCharacter && !isForced)
                    {
                        CampaignEventDispatcher.Instance.OnBeforeMainCharacterDied(victim, killer, actionDetail, showNotification);
                    }
                    else
                    {
                        victim.AddDeathMark(killer, actionDetail);

                        var CreateObituary = typeof(KillCharacterAction).GetMethod("CreateObituary", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                        victim.EncyclopediaText = CreateObituary.Invoke(null, new object[] { victim, actionDetail }) as TextObject;

                        if (victim.Clan != null && (victim.Clan.Leader == victim || victim == Hero.MainHero))
                        {
                            if (!victim.Clan.IsEliminated && victim != Hero.MainHero && victim.Clan.Heroes.Any<Hero>((Func<Hero, bool>)(x => !x.IsChild && x != victim && x.IsAlive && x.IsLord)))
                                ChangeClanLeaderAction.ApplyWithoutSelectedNewLeader(victim.Clan);
                            if (victim.Clan.Kingdom != null && victim.Clan.Kingdom.RulingClan == victim.Clan)
                            {
                                List<Clan> list = victim.Clan.Kingdom.Clans.Where<Clan>((Func<Clan, bool>)(t => !t.IsEliminated && t.Leader != victim && !t.IsUnderMercenaryService)).ToList<Clan>();
                                if (list.IsEmpty<Clan>())
                                {
                                    if (!victim.Clan.Kingdom.IsEliminated)
                                        DestroyKingdomAction.ApplyByKingdomLeaderDeath(victim.Clan.Kingdom);
                                }
                                else if (!victim.Clan.Kingdom.IsEliminated)
                                {
                                    if (list.Count > 1)
                                    {
                                        Clan clanToExclude = victim.Clan.Leader == victim || victim.Clan.Leader == null ? victim.Clan : (Clan)null;
                                        victim.Clan.Kingdom.AddDecision((KingdomDecision)new KingSelectionKingdomDecision(victim.Clan, clanToExclude), true);
                                        if (clanToExclude != null)
                                        {
                                            Clan elementWithPredicate = victim.Clan.Kingdom.Clans.GetRandomElementWithPredicate<Clan>((Func<Clan, bool>)(t => t != clanToExclude && Campaign.Current.Models.DiplomacyModel.IsClanEligibleToBecomeRuler(t)));
                                            ChangeRulingClanAction.Apply(victim.Clan.Kingdom, elementWithPredicate);
                                        }
                                    }
                                    else
                                        ChangeRulingClanAction.Apply(victim.Clan.Kingdom, list[0]);
                                }
                            }
                        }
                        if (victim.PartyBelongedTo != null && (victim.PartyBelongedTo.LeaderHero == victim || victim == Hero.MainHero))
                        {
                            MobileParty partyBelongedTo = victim.PartyBelongedTo;
                            if (victim.PartyBelongedTo.Army != null)
                            {
                                if (victim.PartyBelongedTo.Army.LeaderParty == victim.PartyBelongedTo)
                                    DisbandArmyAction.ApplyByArmyLeaderIsDead(victim.PartyBelongedTo.Army);
                                else
                                    victim.PartyBelongedTo.Army = (Army)null;
                            }
                            if (partyBelongedTo != MobileParty.MainParty)
                            {
                                partyBelongedTo.Ai.SetMoveModeHold();
                                if (victim.Clan != null && victim.Clan.IsRebelClan)
                                    DestroyPartyAction.Apply((PartyBase)null, partyBelongedTo);
                            }
                        }

                        OnDeathPatch.Skip = false;
                        var MakeDead = typeof(KillCharacterAction).GetMethod("MakeDead", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                        MakeDead.Invoke(null, new object[] { victim, true });
                        OnDeathPatch.Skip = true;

                        if (MCMUISettings.Instance?.GetEquipment == true)
                        {
                            EquipmentElement[] battleEquip = new EquipmentElement[12];
                            EquipmentElement[] civEquip = new EquipmentElement[12];
                            for (int i = 0; i < 12; i++)
                            {
                                battleEquip[i] = victim.BattleEquipment.GetEquipmentFromSlot((EquipmentIndex)i);
                                civEquip[i] = victim.CivilianEquipment.GetEquipmentFromSlot((EquipmentIndex)i);
                            }
                            EquipmentElement[] equip = battleEquip.Where(_ => !_.IsEmpty).Concat(civEquip.Where(_ => !_.IsEmpty)).ToArray();
                            foreach (EquipmentElement equipItem in equip)
                            {
                                var itemRoster = killer?.PartyBelongedTo?.ItemRoster;

                                if (itemRoster != null)
                                    itemRoster.Add(new ItemRosterElement(equipItem, 1));

                                var name = equipItem.GetModifiedItemName();

                                if (killer == Campaign.Current.MainParty.LeaderHero)
                                    InformationManager.DisplayMessage(new InformationMessage($"Received: {name.ToString()}", new Color(1f, 1f, 1f)));
                            }
                        }

                        try
                        {
                            if (victim.GovernorOf != null)
                                ChangeGovernorAction.RemoveGovernorOf(victim);
                            if (actionDetail == KillCharacterAction.KillCharacterActionDetail.Executed && killer == Hero.MainHero && victim.Clan != null)
                            {
                                var isHonorable = victim.GetTraitLevel(DefaultTraits.Honor) >= 0;
                                var isMerciful = victim.GetTraitLevel(DefaultTraits.Mercy) >= 0;

                                if (isHonorable && (isMerciful && MCMUISettings.Instance?.HonorGainOnExecutingLordNegativeMercy == true))
                                    TraitLevelingHelper.OnLordExecuted();
                                else
                                {
                                    if (!isHonorable && MCMUISettings.Instance?.HonorGainOnExecutingLordNegativeHonor == true)
                                        OnLordExecutedPatch.AffectHonor(MCMUISettings.Instance?.HonorGain ?? 0);
                                    else if (!isMerciful && MCMUISettings.Instance?.HonorGainOnExecutingLordNegativeMercy == true)
                                        OnLordExecutedPatch.AffectHonor(MCMUISettings.Instance?.HonorGain ?? 0);
                                }

                                foreach (Clan clan in (List<Clan>)Clan.All)
                                {
                                    if (!clan.IsEliminated && !clan.IsBanditFaction && clan != Clan.PlayerClan)
                                    {
                                        bool showQuickNotification;
                                        int forExecutingHero = Campaign.Current.Models.ExecutionRelationModel.GetRelationChangeForExecutingHero(victim, clan.Leader, out showQuickNotification);
                                        if (forExecutingHero != 0)
                                            ChangeRelationAction.ApplyPlayerRelation(clan.Leader, forExecutingHero, showQuickNotification);
                                    }
                                }
                            }
                            if (victim.Clan != null && !victim.Clan.IsEliminated && !victim.Clan.IsBanditFaction && victim.Clan != Clan.PlayerClan)
                            {
                                if (victim.Clan.Leader == victim)
                                    DestroyClanAction.ApplyByClanLeaderDeath(victim.Clan);
                                else if (victim.Clan.Leader == null)
                                    DestroyClanAction.Apply(victim.Clan);
                            }
                            CampaignEventDispatcher.Instance.OnHeroKilled(victim, killer, actionDetail, showNotification);
                            if (victim.Spouse != null)
                                victim.Spouse = (Hero)null;
                            if (victim.CompanionOf != null)
                                RemoveCompanionAction.ApplyByDeath(victim.CompanionOf, victim);
                            if (victim.CurrentSettlement == null)
                                return;
                            if (victim.CurrentSettlement == Settlement.CurrentSettlement)
                                LocationComplex.Current?.RemoveCharacterIfExists(victim);
                            if (victim.StayingInSettlement == null)
                                return;
                            victim.StayingInSettlement = (Settlement)null;
                        }
                        finally
                        {
                            var OnDeath = typeof(Hero).GetMethod("OnDeath", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                            OnDeath.Invoke(victim, null);
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(
        typeof(TaleWorlds.CampaignSystem.Hero),
        "OnDeath"
    )]
    class OnDeathPatch
    {
        public static bool Skip = true;

        static bool Prefix()
        {
            return Skip;
        }
    }

    internal sealed class MCMUISettings : AttributeGlobalSettings<MCMUISettings>
    {
        public override string Id => "ExecutionAdjuster";
        public override string DisplayName => $"Execution Adjuster";
        public override string FolderName => "ExecutionAdjuster";
        public override string FormatType => "json";

        [SettingPropertyFloatingInteger("Honor Effect", 0f, 3f, "0%", Order = 1, RequireRestart = false, HintText = "Percentage of how much honor is lost in relation to the original calculation")]
        [SettingPropertyGroup("General")]
        public float HonorMultiplier
        {
            get => _HonorMultiplier;
            set => _HonorMultiplier = (float)Math.Round(value, 2);
        }
        private float _HonorMultiplier = 1f;

        [SettingPropertyFloatingInteger("Relation Effect", 0f, 3f, "0%", Order = 2, RequireRestart = false, HintText = "Percentage of how much NPC-Relation is lost in relation to the original calculation")]
        [SettingPropertyGroup("General")]
        public float RelationMultiplier
        {
            get => _RelationMultiplier;
            set => _RelationMultiplier = (float)Math.Round(value, 2);
        }
        private float _RelationMultiplier = 1f;

        [SettingPropertyFloatingInteger("Relation Effect\nClan of Prisoner", 0f, 3f, "0%", Order = 2, RequireRestart = false, HintText = "Percentage of how much NPC-Relation is lost in relation to the original calculation\nregarding only the clan of the executed lord (still subject to overall relation multiplier)")]
        [SettingPropertyGroup("General")]
        public float RelationMultiplierClanOfExecutee
        {
            get => _RelationMultiplierClanOfExecutee;
            set => _RelationMultiplierClanOfExecutee = (float)Math.Round(value, 2);
        }
        private float _RelationMultiplierClanOfExecutee = 1f;

        [SettingPropertyBool("Get Equipment on Execution", Order = 3, RequireRestart = false)]
        [SettingPropertyGroup("Items")]
        public bool GetEquipment { get; set; } = false;

        [SettingPropertyBool("Gain Honor for executing lords with negative honor", Order = 4, RequireRestart = false)]
        [SettingPropertyGroup("Honor Gain")]
        public bool HonorGainOnExecutingLordNegativeHonor { get; set; } = false;

        [SettingPropertyBool("Gain Honor for executing lords with negative mercy", Order = 5, RequireRestart = false)]
        [SettingPropertyGroup("Honor Gain")]
        public bool HonorGainOnExecutingLordNegativeMercy { get; set; } = false;

        [SettingPropertyInteger("Honor Gain", 0, 1000, "0", Order = 6, RequireRestart = false, HintText = "Honor gained when beheading certain lords\n(subject to % multiplier from \"Honor Effect\")")]
        [SettingPropertyGroup("Honor Gain")]
        public int HonorGain { get; set; } = 0;

        [SettingPropertyBool("Gain Relation for executing lords with enemies", Order = 7, RequireRestart = false, HintText = "Gain relation with lords for executing lords they are enemies with\n(subject to % multiplier from \"Relation Effect\"")]
        [SettingPropertyGroup("Relation Gain")]
        public bool RelationGainOnExecutingLordEnemy { get; set; } = false;
    }
}

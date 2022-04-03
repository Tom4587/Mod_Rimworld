﻿using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace VFEM.HarmonyPatches
{
    using System;
    using System.Linq;
    using System.Text;
    using Verse.AI;
    using VFEMech;
	[HarmonyPatch(typeof(GenHostility), "AnyHostileActiveThreatTo_NewTemp",
		new Type[] { typeof(Map), typeof(Faction), typeof(IAttackTarget), typeof(bool)},
		new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal })]
	internal static class AnyHostileActiveThreatTo_Patch
	{
		[HarmonyPriority(Priority.Last)]
		public static void Postfix(ref bool __result, Map map, Faction faction, ref IAttackTarget threat, bool countDormantPawnsAsHostile = false)
		{
			if (!__result && map.ParentFaction?.def == VFEMDefOf.VFE_Mechanoid)
            {
				threat = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial)
					.FirstOrDefault(b => b.Faction?.def == VFEMDefOf.VFE_Mechanoid && b.def.defName.Contains("_Turret_")) as IAttackTarget;
				__result = threat != null;
            }
		}
	}

	[HarmonyPatch(typeof(SettlementDefeatUtility), "IsDefeated")]
    [HarmonyAfter("vanillaexpanded.achievements")]
    internal static class SettlementDefeatedUtility_Patch
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Map map, Faction faction, ref bool __result)
        {
            if (map.listerThings.ThingsOfDef(VFEMDefOf.VFEM_MissileIncoming).Any()) __result = true;
            else if (map.mapPawns.SpawnedPawnsInFaction(faction).Any(p => p.Faction?.def == VFEMDefOf.VFE_Mechanoid && GenHostility.IsActiveThreatToPlayer(p))) __result = false;
            else if (map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial).Any(b => b.Faction?.def == VFEMDefOf.VFE_Mechanoid && b.def.defName.Contains("_Turret_"))) __result     = false;
        }
    }

	[HarmonyPatch(typeof(SettlementDefeatUtility), nameof(SettlementDefeatUtility.CheckDefeated))]
	public class Faction_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static bool Prefix(Settlement factionBase)
		{
			if (factionBase.Faction?.def == VFEMDefOf.VFE_Mechanoid)
			{
				CheckDefeated(factionBase);
				return false;
			}
			return true;
		}

		public static void CheckDefeated(Settlement factionBase)
		{
			if (factionBase.Faction == Faction.OfPlayer)
			{
				return;
			}
			Map map = factionBase.Map;
			if (map == null || !IsDefeated(map, factionBase.Faction))
			{
				return;
			}
			bool defeated = IsDefeated(map, factionBase.Faction);
			SettlementDefeatedUtility_Patch.Postfix(map, factionBase.Faction, ref defeated);
			if (!defeated)
            {
				return;
            }
			IdeoUtility.Notify_PlayerRaidedSomeone(map.mapPawns.FreeColonistsSpawned);
			DestroyedSettlement destroyedSettlement = (DestroyedSettlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.DestroyedSettlement);
			destroyedSettlement.Tile = factionBase.Tile;
			destroyedSettlement.SetFaction(factionBase.Faction);
			Find.WorldObjects.Add(destroyedSettlement);
			TimedDetectionRaids component = destroyedSettlement.GetComponent<TimedDetectionRaids>();
			component.CopyFrom(factionBase.GetComponent<TimedDetectionRaids>());
			component.SetNotifiedSilently();
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("LetterFactionBaseDefeated".Translate(factionBase.Label, component.DetectionCountdownTimeLeftString));
			if (!HasAnyOtherBase(factionBase))
			{
				factionBase.Faction.defeated = true;
				stringBuilder.AppendLine();
				stringBuilder.AppendLine();
				stringBuilder.Append("LetterFactionBaseDefeated_FactionDestroyed".Translate(factionBase.Faction.Name));
			}
			if (factionBase.def.GetModExtension<MechanoidBaseExtension>()?.defeatingGivesOutOtherFactionsGoodwill ?? false)
			{
				foreach (Faction allFaction in Find.FactionManager.AllFactions)
				{
					if (!allFaction.Hidden && !allFaction.IsPlayer && allFaction != factionBase.Faction && allFaction.HostileTo(factionBase.Faction))
					{
						FactionRelationKind playerRelationKind = allFaction.PlayerRelationKind;
						Faction.OfPlayer.TryAffectGoodwillWith(allFaction, 20, canSendMessage: false, canSendHostilityLetter: false, HistoryEventDefOf.DestroyedEnemyBase);
						stringBuilder.AppendLine();
						stringBuilder.AppendLine();
						stringBuilder.Append("RelationsWith".Translate(allFaction.Name) + ": " + 20.ToStringWithSign());
						allFaction.TryAppendRelationKindChangedInfo(stringBuilder, playerRelationKind, allFaction.PlayerRelationKind);
					}
				}
			}
			Find.LetterStack.ReceiveLetter("LetterLabelFactionBaseDefeated".Translate(), stringBuilder.ToString(), LetterDefOf.PositiveEvent, new GlobalTargetInfo(factionBase.Tile), factionBase.Faction);
			map.info.parent = destroyedSettlement;
			factionBase.Destroy();
			TaleRecorder.RecordTale(TaleDefOf.CaravanAssaultSuccessful, map.mapPawns.FreeColonists.RandomElement());
		}

		public static bool IsDefeated(Map map, Faction faction)
		{
			List<Pawn> list = map.mapPawns.SpawnedPawnsInFaction(faction);
			for (int i = 0; i < list.Count; i++)
			{
				Pawn pawn = list[i];
				if (pawn.RaceProps.IsMechanoid && GenHostility.IsActiveThreatToPlayer(pawn))
				{
					return false;
				}
			}
			return true;
		}

		private static bool HasAnyOtherBase(Settlement defeatedFactionBase)
		{
			List<Settlement> settlements = Find.WorldObjects.Settlements;
			for (int i = 0; i < settlements.Count; i++)
			{
				Settlement settlement = settlements[i];
				if (settlement.Faction == defeatedFactionBase.Faction && settlement != defeatedFactionBase)
				{
					return true;
				}
			}
			return false;
		}
	}
}
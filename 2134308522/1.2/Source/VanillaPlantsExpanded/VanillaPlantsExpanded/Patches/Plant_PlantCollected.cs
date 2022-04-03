﻿using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaPlantsExpanded
{
    [HarmonyPatch(typeof(Plant))]
    [HarmonyPatch("PlantCollected")]
    public static class VanillaCookingExpanded_Plant_PlantCollected_Patch
    {
        [HarmonyPrefix]
        public static void AccessMap(Plant __instance, out Map __state)
        {
            __state = __instance.Map;
        }

        [HarmonyPostfix]
        public static void RemoveTilled(Plant __instance, Map __state)
        {
            if (__state.terrainGrid.TerrainAt(__instance.Position).defName == "VCE_TilledSoil") {
                if (__instance.def.plant.HarvestDestroys) {
                    __state.terrainGrid.RemoveTopLayer(__instance.Position);
                }
            }
        }
    }
}

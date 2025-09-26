using System;
using System.Linq;
using HarmonyLib;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using UnityEngine;
using Utils;

namespace BlockDecorBehindWalls
{
    public class BlockDecorBehindWalls : KMod.UserMod2
    {
        private static Tag fancyWallTag = new Tag("BlockDecorBehindWalls_FancyWall");
        private static readonly Tag[] ignoreTags = new Tag[] { fancyWallTag };

        public static bool CheckBackwall(int cell)
        {
            GameObject go = Grid.Objects[cell, (int)ObjectLayer.Backwall];
            if (go == null) return false;
            return go.GetComponent<BuildingComplete>();
        }

        [HarmonyPatch(typeof(DecorProvider), "AddDecor")]
        private static class DecorProviderPatches
        {
            private static bool Prefix(DecorProvider __instance)
            {
                BuildingComplete building = __instance.GetComponent<BuildingComplete>();
                BuildingDef def = building?.Def;
                if (def 
                    && (def.SceneLayer < Grid.SceneLayer.LogicGatesFront)
                    && (Options.Instance.AffectHeavyWires
                        || def.BuildLocationRule != BuildLocationRule.NotInTiles)
                    && building.PlacementCells.All(CheckBackwall)
                    && !building.HasAnyTags(ignoreTags))
                {
                    __instance.currDecor = 0.0f;
                    return false;
                }
                return true;
            }
        }

        public static void UpdateDecorBehindWall(BuildingComplete building)
        {
            if (building.Def?.ObjectLayer == ObjectLayer.Backwall)
            {
                foreach (int cell in building.PlacementCells)
                {
                    for (int layer = 0; layer < (int)ObjectLayer.NumLayers; ++layer)
                    {
                        GameObject go = Grid.Objects[cell, layer];
                        if (go == null) continue;
                        DecorProvider decorProvider = go.GetComponent<DecorProvider>();
                        BuildingDef def = go.GetComponent<BuildingComplete>()?.Def;
                        if (decorProvider && def)
                        {
                            decorProvider.Refresh();
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(BuildingComplete), "OnSpawn")]
        private static class ConstructBackwallPatches
        {
            private static void Postfix(BuildingComplete __instance)
            {
                UpdateDecorBehindWall(__instance);
            }
        }

        [HarmonyPatch(typeof(BuildingComplete), "OnCleanUp")]
        private static class DeconstructBackwallPatches
        {
            private static void Postfix(BuildingComplete __instance)
            {
                UpdateDecorBehindWall(__instance);
            }
        }

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            PUtil.InitLibrary();
            new POptions().RegisterOptions(this, typeof(Options));
        }

        [HarmonyPatch(typeof(Localization), nameof(Localization.Initialize))]
        private static class Localization_Initialize_Patch
        {
            private static void Postfix() => LocalizationUtils.Translate(typeof(STRINGS));
        }
    }
}

using System;
using HarmonyLib;
using UnityEngine;
using Rendering;
using System.Collections.Generic;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using Utils;

namespace MaterialColoredTilesAndMore
{
    public class Patches : KMod.UserMod2
    {
        private static readonly HashSet<Tag> acceptedTags = new HashSet<Tag>();
        public static HashSet<Tag> AcceptedTags
        {
            get
            {
                if (Options.Instance != null && acceptedTags.Count == 0)
                {
                    if (Options.Instance.Tiles) acceptedTags.Add(GameTags.FloorTiles);
                    if (Options.Instance.Walls) acceptedTags.Add(GameTags.Backwall);
                    if (Options.Instance.FarmTiles) acceptedTags.Add(GameTags.FarmTiles);
                    if (Options.Instance.Pipes)
                    {
                        acceptedTags.Add(GameTags.Pipes);
                        acceptedTags.Add(GameTags.Vents);
                    }
                }
                return acceptedTags;
            }
        }
        private static readonly List<string> acceptedKeywords = new List<string>();
        public static List<string> AcceptedKeywords {
            get
            {
                if (Options.Instance != null && acceptedKeywords.Count == 0)
                {
                    if (Options.Instance.Doors) acceptedKeywords.Add("Door");
                    if (Options.Instance.Sculptures) acceptedKeywords.Add("Sculpture");
                    if (Options.Instance.Moulding) acceptedKeywords.Add("Moulding");
                    if (Options.Instance.LogicWires) acceptedKeywords.Add("LogicWire");
                }
                return acceptedKeywords;
            }
        }

        public static Color GetColor(PrimaryElement element)
        {
            Color color = element.Element?.substance?.colour ?? Color.clear;
            color *= Options.Instance.Brightness;
            color.a = 1f;
            return color;
        }

        [HarmonyPatch(typeof(BlockTileRenderer), nameof(BlockTileRenderer.GetCellColour))]
        private static class BlockTileRendererPatches
        {
            private static void Postfix(ref Color __result, int cell, SimHashes element)
            {
                GameObject tile = Grid.Objects[cell, (int)ObjectLayer.FoundationTile];
                if (!(Options.Instance.Tiles && tile
                    && tile.TryGetComponent(out BuildingComplete _)))
                    return;
                if (tile.TryGetComponent(out PrimaryElement elem))
                {
                    __result *= GetColor(elem);
                }
            }
        }

        public static void ChangeBuildingColor(BuildingComplete building)
        {
            KAnimControllerBase kAnimController;
            if (!building.TryGetComponent(out kAnimController)
                || building.TryGetComponent(out KAnimGridTileVisualizer _))
                return;
            if (!building.prefabid.Tags.Overlaps(AcceptedTags)
                && AcceptedKeywords.FindIndex(x => building.prefabid.name.Contains(x)) == -1)
                return;
            PrimaryElement element = building.primaryElement;
            if (element != null)
            {
                kAnimController.TintColour = GetColor(element);
            }
        }

        [HarmonyPatch(typeof(BuildingComplete), "OnSpawn")]
        private static class BuildingCompletePatches
        {
            private static void Postfix(BuildingComplete __instance)
            {
                ChangeBuildingColor(__instance);
            }
        }

        [HarmonyPatch(typeof(OverlayScreen), nameof(OverlayScreen.ToggleOverlay))]
        private static class OverlayMenuPatches
        {
            private static void Postfix(HashedString newMode)
            {
                if (newMode.Equals(OverlayModes.None.ID))
                {
                    foreach (BuildingComplete building in Components.BuildingCompletes.Items)
                    {
                        ChangeBuildingColor(building);
                    }
                }
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

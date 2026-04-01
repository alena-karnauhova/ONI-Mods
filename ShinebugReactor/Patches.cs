using System;
using HarmonyLib;
using UnityEngine;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using PeterHan.PLib.Buildings;
using System.Reflection;
using Database;
using Utils;

namespace ShinebugReactor
{
    public class Patches : KMod.UserMod2
    {
        [HarmonyPatch(typeof(IncubationMonitor.Instance), "UpdateIncubationState")]
        private static class IncubationMonitor_UpdateIncubationState_Patch
        {
            private static bool Prefix(IncubationMonitor.Instance __instance, bool stored)
            {
                if (!stored) return true;
                var storage = __instance.GetStorage();
                if (storage && storage.GetComponent<ShinebugReactor>())
                {
                    __instance.sm.isSuppressed.Set(false, __instance);
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(IncubationMonitor), "DropSelfFromStorage")]
        private static class IncubationMonitor_DropSelfFromStorage_Patch
        {
            private static bool Prefix(IncubationMonitor.Instance smi)
            {
                var storage = smi.GetStorage();
                if (storage && storage.GetComponent<ShinebugReactor>())
                {
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(IncubationMonitor), "SpawnBaby")]
        private static class IncubationMonitor_SpawnBaby_Patch
        {
            private static readonly Func<IncubationMonitor.Instance, GameObject> spawnShell
                = AccessTools.MethodDelegate<Func<IncubationMonitor.Instance, GameObject>>(
                    AccessTools.Method(typeof(IncubationMonitor), "SpawnShell"));
            private static bool Prefix(IncubationMonitor.Instance smi)
            {
                var storage = smi.GetStorage();
                if (!storage) return true;
                ShinebugReactor reactor = smi.GetStorage().GetComponent<ShinebugReactor>();
                if (reactor)
                {
                    spawnShell(smi);
                    reactor.EggHatched(smi.gameObject);
                    //smi.GetStorage().Drop(smi.gameObject);
                    //smi.gameObject.AddTag(GameTags.StoredPrivate);
                    SaveLoader.Instance.saveManager.Unregister(smi.GetComponent<SaveLoadRoot>());
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(HighEnergyParticleDirectionSideScreen), nameof(HighEnergyParticleDirectionSideScreen.IsValidForTarget))]
        private static class HEPDirectionSideScreen_Patch
        {
            private static void Postfix(ref bool __result, GameObject target)
            {
                if (DlcManager.FeatureRadiationEnabled() && target.GetComponent<ShinebugReactor>())
                {
                    __result = true;
                }
            }
        }
        [HarmonyPatch(typeof(SingleSliderSideScreen), nameof(SingleSliderSideScreen.IsValidForTarget))]
        private static class SliderSideScreen_Patch
        {
            private static void Postfix(ref bool __result, GameObject target)
            {
                if (!DlcManager.FeatureRadiationEnabled() && target.GetComponent<ShinebugReactor>())
                {
                    __result = false;
                }
            }
        }

        private static void FixStoragePriority(Storage storage)
        {
            if (Options.Instance.FixIncubatorPriority)
            {
                storage.onlyTransferFromLowerPriority = true;
            }
        }
        [HarmonyPatch(typeof(EggCrackerConfig), nameof(EggCrackerConfig.DoPostConfigureComplete))]
        private static class EggCrackerConfig_Patch
        {
            private static void Postfix(GameObject go)
            {
                FixStoragePriority(go.GetComponent<ComplexFabricator>().inStorage);
            }
        }
        [HarmonyPatch(typeof(EggIncubatorConfig), nameof(EggIncubatorConfig.DoPostConfigureComplete))]
        private static class EggIncubatorConfig_Patch
        {
            private static void Postfix(GameObject go)
            {
                FixStoragePriority(go.GetComponent<Storage>());
            }
        }

        [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
        private static class Tooltips
        {
            private static void Postfix(Db __instance)
            {
                ShinebugReactor.AddStatusItemsToDatabase(__instance.BuildingStatusItems);
                ShinebugReactor.InitializeStatusCategory(__instance.StatusItemCategories);
            }
        }

        [HarmonyPatch(typeof(StructureTemperatureComponents), "InitializeStatusItem")]
        private static class StructureTemperatures_Patch
        {
            private static void Postfix(StructureTemperatureComponents __instance)
            {
                ShinebugReactor.OperatingEnergyStatus =
                    Traverse.Create(__instance).Field("operatingEnergyStatusItem")
                    .GetValue<StatusItem>();
            }
        }

        [HarmonyPatch(typeof(Localization), nameof(Localization.Initialize))]
        private static class Localization_Initialize_Patch
        {
            private static void Postfix()
            {
                LocalizationUtils.Translate(typeof(STRINGS));
                LocalizationUtils.Translate(typeof(Commons.STRINGS));
            }
        }

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            PUtil.InitLibrary();
            new POptions().RegisterOptions(this, typeof(Options));
            new PBuildingManager().Register(ShinebugReactorConfig.PBuilding);
        }
    }
}
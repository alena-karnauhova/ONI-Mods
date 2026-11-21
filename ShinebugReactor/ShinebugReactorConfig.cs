using Commons;
using Utils;
using System.Collections.Generic;
using TUNING;
using UnityEngine;
using PeterHan.PLib.Buildings;
using GameStrings = STRINGS;
using static STRINGS.BUILDINGS.PREFABS;

namespace ShinebugReactor
{
    using static STRINGS.BUILDINGS.PREFABS;

    public class ShinebugReactorConfig : IBuildingConfig
    {
        public const string ID = "ShinebugReactor";
        //public const string category = "Power";
        //public const string tech = "RenewableEnergy";
        public const int width = 9;//10
        public const int height = 4;//5;
        public const string FIRE_PORT_ID = "ShinebugReactorFirePort";
        public const string FULL_PORT_ID = "ShinebugReactorFullPort";
        public const float RADIUS_FACTOR = 26.5f;
        //public const float WattageRating = BUILDINGS.ENERGY_CONSUMPTION_WHEN_ACTIVE.TIER7;
        public const float WattageRequired = BUILDINGS.ENERGY_CONSUMPTION_WHEN_ACTIVE.TIER5;
        //public const float PowerSaveEnergyRequired = BUILDINGS.ENERGY_CONSUMPTION_WHEN_ACTIVE.TIER3;
        public const short EmitRange = 4;
        public const float EmitLeakRate = 0.1f;
        public static float HeatPerSecond;
        public static readonly PBuilding PBuilding
            = new PBuilding(ID, SHINEBUGREACTOR.NAME)
        {
            Width = width,
            Height = height,
            Animation = "shinebug_reactor_kanim",
            HP = BUILDINGS.HITPOINTS.TIER0,
            ConstructionTime = BUILDINGS.CONSTRUCTION_TIME_SECONDS.TIER4,
            Ingredients =
            {
                new BuildIngredient(MATERIALS.GLASS, tier: 5),
                new BuildIngredient(MATERIALS.REFINED_METAL, tier: 4),
                //new BuildIngredient(MATERIALS.REFINED_METAL, tier: 3),
                //new BuildIngredient(MATERIALS.BUILDABLERAW, tier: 4),
            },
            Placement = BuildLocationRule.Anywhere,
            //IndustrialMachine = true,
            AudioCategory = "HollowMetal",
            AudioSize = "large",
            ViewMode = OverlayModes.Power.ID,
            OverheatTemperature = BUILDINGS.OVERHEAT_TEMPERATURES.LOW_2,
            //PowerOutput = new PowerRequirement(WattageRating, CellOffset.none),
            DefaultPriority = 5,
            Category = "Power",
            SubCategory = BUILDINGS.PLANSUBCATEGORYSORTING[SolarPanelConfig.ID],
            Tech = "RenewableEnergy",
            Description = null,
            EffectText = null,
            RotateMode = PermittedRotations.FlipH,
        };

        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef buildingDef = PBuilding.CreateDef();
            buildingDef.RequiresPowerOutput = true;
            buildingDef.PowerOutputOffset = CellOffset.none;
            buildingDef.GeneratorBaseCapacity = buildingDef.GeneratorWattageRating
                = Options.Instance.MaxPowerOutput;
            ConfigureDescriptors(buildingDef);
            if (Sim.IsRadiationEnabled())
            {
                buildingDef.HighEnergyParticleOutputOffset = new CellOffset(1, 2);
                buildingDef.UseHighEnergyParticleOutputPort = true;
                GeneratedBuildings.RegisterWithOverlay(OverlayScreen.RadiationIDs, ID);
                buildingDef.LogicInputPorts = new List<LogicPorts.Port>()
                {
                    LogicPorts.Port.InputPort(FIRE_PORT_ID, new CellOffset(1, 0),
                    SHINEBUGREACTOR.LOGIC_PORT_FIRE,
                    SHINEBUGREACTOR.LOGIC_PORT_FIRE_ACTIVE,
                    SHINEBUGREACTOR.LOGIC_PORT_FIRE_INACTIVE)
                };
                ConfigureHEPDescriptors(buildingDef);
            }
            buildingDef.LogicOutputPorts = new List<LogicPorts.Port>()
            {
                LogicPorts.Port.OutputPort(FULL_PORT_ID, new CellOffset(-4, 1),
                STORAGELOCKERSMART.LOGIC_PORT,
                STORAGELOCKERSMART.LOGIC_PORT_ACTIVE,
                STORAGELOCKERSMART.LOGIC_PORT_INACTIVE)
            };
            buildingDef.UtilityInputOffset = new CellOffset(-4, 1);
            buildingDef.InputConduitType = ConduitType.Solid;
            return buildingDef;
        }
        private static void ConfigureDescriptors(BuildingDef buildingDef)
        {
            List<Descriptor> descriptors = buildingDef.EffectDescription
                ?? (buildingDef.EffectDescription = new List<Descriptor>(3));
            if (Options.Instance.PowerGenerationMode == Options.PowerGenerationModeType.SolarPanel)
            {
                string formattedLuxPerWatt = GameUtil.GetFormattedLux(Mathf.CeilToInt(1f/(RADIUS_FACTOR * SolarPanelConfig.WATTS_PER_LUX)));
                descriptors.Add(new Descriptor(
                    string.Format(STRINGS.UI.BUILDINGEFFECTS.SHINEBUGREACTORWATTSPERLUX, formattedLuxPerWatt),
                    string.Format(STRINGS.UI.BUILDINGEFFECTS.TOOLTIPS.SHINEBUGREACTORWATTSPERLUX, formattedLuxPerWatt, GameUtil.GetFormattedWattage(1f)),
                    Descriptor.DescriptorType.Effect));
            }
        }
        private static void ConfigureHEPDescriptors(BuildingDef buildingDef)
        {
            List<Descriptor> descriptors = buildingDef.EffectDescription;

            string formattedWattage = GameUtil.GetFormattedWattage(WattageRequired);
            descriptors.Add(new Descriptor(
                string.Format(GameStrings.UI.BUILDINGEFFECTS.REQUIRESPOWER, formattedWattage),
                string.Format(STRINGS.UI.BUILDINGEFFECTS.TOOLTIPS.SHINEBUGREACTORREQUIRESPOWER, formattedWattage),
                Descriptor.DescriptorType.Requirement));

            BuildingDef HEPSpawnerBuildingDef = Assets.GetBuildingDef(HighEnergyParticleSpawnerConfig.ID);
            HeatPerSecond = HEPSpawnerBuildingDef.SelfHeatKilowattsWhenActive + HEPSpawnerBuildingDef.ExhaustKilowattsWhenActive;

            string formattedHeatEnergy = GameUtil.GetFormattedHeatEnergy(HeatPerSecond * 1000f);
            descriptors.Add(new Descriptor(
                string.Format(GameStrings.UI.BUILDINGEFFECTS.HEATGENERATED, formattedHeatEnergy),
                string.Format(STRINGS.UI.BUILDINGEFFECTS.TOOLTIPS.SHINEBUGREACTORHEATGENERATED, formattedHeatEnergy)));
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            PBuilding.ConfigureBuildingTemplate(go);
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
            PBuilding.DoPostConfigureComplete(go);
            ConfigureSolidBase(go);
            go.AddOrGet<Automatable>();
            ConfigureStorage(go);
            go.AddOrGet<ItemsRemovable>();
            SolidConduitFilteredConsumer conduitConsumer = go.AddOrGet<SolidConduitFilteredConsumer>();
            conduitConsumer.alwaysConsume = true;
            go.AddOrGetDef<PoweredActiveController.Def>();
            if (Sim.IsRadiationEnabled())
            {
                HighEnergyParticleStorage particleStorage = go.AddOrGet<HighEnergyParticleStorage>();
                particleStorage.capacity = HighEnergyParticleSpawnerConfig.MAX_SLIDER;
                particleStorage.showCapacityStatusItem = true;
                ConfigureRadiation(go);
            }
            ConfigureLight(go);
            go.AddOrGet<ShinebugReactor>();
            ConfigureVisualSize(go);
        }

        private static Storage ConfigureStorage(GameObject go)
        {
            Storage storage = go.AddOrGet<Storage>();
            storage.capacityKg = 400f;
            storage.showInUI = true;
            storage.SetDefaultStoredItemModifiers(Storage.StandardFabricatorStorage);
            List<Tag> tags = new List<Tag>(3)
            {
                GameTags.Egg,
                //GameTags.BagableCreature, GameTags.SwimmingCreature
            };

            storage.storageFilters = tags;
            return storage;
        }
        private static void ConfigureSolidBase(GameObject go)
        {
            MakeBaseSolid.Def solidBase = go.AddOrGetDef<MakeBaseSolid.Def>();
            solidBase.occupyFoundationLayer = false;
            solidBase.solidOffsets = new CellOffset[width];
            for (int counter = 0, index = -(Mathf.CeilToInt(width / 2f) - 1); counter < width; ++counter, ++index)
                solidBase.solidOffsets[counter] = new CellOffset(index, 0);
        }
        private static RadiationEmitter ConfigureRadiation(GameObject go)
        {
            RadiationEmitter emitter = go.AddOrGet<RadiationEmitter>();
            emitter.emitType = RadiationEmitter.RadiationEmitterType.Constant;
            emitter.radiusProportionalToRads = false;
            emitter.emitRadiusX = EmitRange;
            emitter.emitRadiusY = EmitRange;
            emitter.emissionOffset = new Vector3(0f, (int)(height / 2f));
            emitter.emitRate = 0.1f;
            return emitter;
        }
        private static Light2D ConfigureLight(GameObject go)
        {
            Light2D light = go.AddOrGet<Light2D>();
            light.drawOverlay = true;
            light.overlayColour = LIGHT2D.LIGHTBUG_OVERLAYCOLOR;
            light.Color = LIGHT2D.LIGHTBUG_COLOR;
            light.Direction = LIGHT2D.LIGHTBUG_DIRECTION;
            light.shape = LightShape.Circle;
            light.Angle = 0f;
            light.Range = EmitRange;
            light.Offset = new Vector2(0f, (int)(height / 2f));
            light.Lux = 1800;
            return light;
        }

        private static void ConfigureVisualSize(GameObject go)
        {
            var animController = go.GetComponent<KBatchedAnimController>();
            animController.animWidth = 0.9f;
            animController.animHeight = 0.9f;
        }

        public override void DoPostConfigurePreview(BuildingDef def, GameObject go)
        {
            base.DoPostConfigurePreview(def, go);
            ConfigureVisualSize(go);
        }
        public override void DoPostConfigureUnderConstruction(GameObject go)
        {
            base.DoPostConfigureUnderConstruction(go);
            ConfigureVisualSize(go);
        }
    }
}
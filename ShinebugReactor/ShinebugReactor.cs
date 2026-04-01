using KSerialization;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static STRINGS.UI;
using GameStrings = STRINGS;
using HarmonyLib;
using System.Reflection;
using Database;
using static GameComps;

namespace ShinebugReactor
{
    using static STRINGS.BUILDING.STATUSITEMS;

    [SerializationConfig(MemberSerialization.OptIn)]
    public class ShinebugReactor : Generator, ISim1000ms, IHighEnergyParticleDirection,
        ISingleSliderControl, IUserControlledCapacity
    {
        public const string EGG_POSTFIX = "Egg";
        public const string BABY_POSTFIX = "Baby";
        public const float CYCLE_LENGTH = 600f;
        #region StatusItems
        public const string RadboltProductionStatusCategoryID = "ShinebugReactorRadboltProduction";
        protected static StatusItemCategory RadboltProductionStatusCategory;
        public const string StatusItemPrefix = "BUILDING";
        public static readonly StatusItem CreatureCountStatus
            = new StatusItem(nameof(SHINEBUGREACTORCREATURES), StatusItemPrefix,
            string.Empty, StatusItem.IconType.Info, NotificationType.Neutral,
            false, OverlayModes.None.ID,
            resolve_string_callback: ((str, data) =>
            {
                int value = ((ShinebugReactor)data).Creatures.Count;
                str = string.Format(str, value.ToString());
                return str;
            }));
        public static readonly StatusItem EggCountStatus
            = new StatusItem(nameof(SHINEBUGREACTOREGGS), StatusItemPrefix,
            string.Empty, StatusItem.IconType.Info, NotificationType.Neutral,
            false, OverlayModes.None.ID,
            resolve_string_callback: ((str, data) =>
            {
                int value = Mathf.RoundToInt(((ShinebugReactor)data).storage.UnitsStored());
                str = string.Format(str, value.ToString());
                return str;
            }));
        public static readonly StatusItem WattageStatus
            = new StatusItem(nameof(SHINEBUGREACTORWATTAGE), StatusItemPrefix,
            string.Empty, StatusItem.IconType.Info, NotificationType.Neutral,
            false, OverlayModes.Power.ID,
            resolve_string_callback: ((str, data) =>
            {
                float value = ((ShinebugReactor)data).CurrentWattage;
                str = string.Format(str, GameUtil.GetFormattedWattage(value));
                return str;
            }));
        public static readonly StatusItem HEPStatus
            = new StatusItem(nameof(SHINEBUGREACTORHEP), StatusItemPrefix,
            string.Empty, StatusItem.IconType.Info, NotificationType.Neutral,
            false, OverlayModes.Radiation.ID,
            resolve_string_callback: ((str, data) =>
            {
                ShinebugReactor reactor = (ShinebugReactor)data;
                float value = reactor.CurrentHEP;
                str = string.Format(str, Util.FormatWholeNumber(value), UNITSUFFIXES.HIGHENERGYPARTICLES.PARTRICLES);
                return str;
            }));
        /*public static readonly StatusItem HEPStorageStatus
            = new StatusItem(StorageLockerConfig.ID, StatusItemPrefix,
            string.Empty, StatusItem.IconType.Info, NotificationType.Neutral,
            false, OverlayModes.Radiation.ID,
            resolve_string_callback: ((str, data) =>
            {
                ShinebugReactor reactor = (ShinebugReactor)data;
                float value = reactor.particleStorage.Particles;
                float capacity = reactor.particleStorage.capacity;
                str = str.Replace("{Stored}", Util.FormatWholeNumber(value))
                .Replace("{Capacity}", Util.FormatWholeNumber(capacity))
                .Replace("{Units}", UNITSUFFIXES.HIGHENERGYPARTICLES.PARTRICLES);
                return str;
            }));*/
        public static readonly StatusItem NoHEPProductionWarningStatus
            = new StatusItem(nameof(SHINEBUGREACTORNOHEPPRODUCTIONWARNING), StatusItemPrefix,
            string.Empty, StatusItem.IconType.Info, NotificationType.BadMinor,
            false, OverlayModes.Radiation.ID);
        public static readonly StatusItem HEPProductionDisabledStatus
            = new StatusItem(nameof(SHINEBUGREACTORHEPPRODUCTIONDISABLED), StatusItemPrefix,
            string.Empty, StatusItem.IconType.Info, NotificationType.Neutral,
            false, OverlayModes.Radiation.ID);
        public static StatusItem OperatingEnergyStatus;
        #endregion

        #region Components
        [MyCmpReq]
        protected readonly TreeFilterable treeFilterable;
        [MyCmpReq]
        protected readonly LogicPorts logicPorts;
        [MyCmpReq]
        protected readonly Light2D light;
        [MyCmpReq]
        protected readonly Storage storage;
        [MyCmpGet]
        protected readonly HighEnergyParticleStorage particleStorage;
        [MyCmpGet]
        protected readonly RadiationEmitter radiationEmitter;

        //[Serialize]
        //public Ref<HighEnergyParticlePort> capturedByRef = new Ref<HighEnergyParticlePort>();
        #endregion
        protected HandleVector<int>.Handle accumulator;
        protected HandleVector<int>.Handle structureTemperature;
        [Serialize]
        protected EightDirection _direction;
        protected EightDirectionController directionController;

        //protected Guid[] statusHandles = new Guid[3];
        protected Traverse<FetchList2> fetchListEggs;//, fetchListCreatures;
        protected FilteredStorage filteredStorageEggs;//, filteredStorageCreatures;
        //protected Action<object> fsOnStorageChanged;
        [Serialize]
        protected int UserCapacity = 50;
        [Serialize]
        public float ParticleThreshold = 100f;
        protected bool isLogicActive;
        //public bool radiationEnabled;
        protected bool isFull;
        protected int storageDropCell;
        protected float emitRads;
        /*[Serialize]
        private List<ShinebugEggSimulator> _shinebugEggs;*/
        [Serialize]
        public readonly List<ShinebugSimulator> Creatures = new List<ShinebugSimulator>(100);
        [NonSerialized]
        public float CurrentWattage;
        [NonSerialized]
        public float CurrentHEP;
        [NonSerialized]
        public float CurrentLight;

        public EightDirection Direction
        {
            get => _direction;
            set
            {
                _direction = value;
                if (directionController == null) return;
                directionController.SetRotation(45f * EightDirectionUtil.GetDirectionIndex(_direction));
                directionController.controller.enabled = false;
                directionController.controller.enabled = true;
            }
        }

        #region IUserControlledCapacity
        public float UserMaxCapacity
        {
            get => UserCapacity;
            set
            {
                UserCapacity = Mathf.RoundToInt(value);
                UpdateLogic();
                //Trigger((int)GameHashes.StorageCapacityChanged);
                Trigger((int)GameHashes.UserSettingsChanged);
            }
        }
        public float AmountStored => Creatures.Count + storage.UnitsStored();
        public float MinCapacity => 1f;
        public float MaxCapacity => 100f;
        public bool WholeValues => true;
        public LocString CapacityUnits => UNITSUFFIXES.CRITTERS;
        public bool ControlEnabled() => true;
        #endregion
        #region ISliderControl
        public string SliderTitleKey => "STRINGS.UI.UISIDESCREENS.RADBOLTTHRESHOLDSIDESCREEN.TITLE";
        public string SliderUnits => (string)UNITSUFFIXES.HIGHENERGYPARTICLES.PARTRICLES;
        public int SliderDecimalPlaces(int index) => 1;
        public float GetSliderMin(int index) => HighEnergyParticleSpawnerConfig.MIN_SLIDER;
        public float GetSliderMax(int index) => HighEnergyParticleSpawnerConfig.MAX_SLIDER;
        public float GetSliderValue(int index) => ParticleThreshold;
        public void SetSliderValue(float percent, int index) => ParticleThreshold = percent;
        public string GetSliderTooltipKey(int index) => "STRINGS.UI.UISIDESCREENS.RADBOLTTHRESHOLDSIDESCREEN.TOOLTIP";
        public string GetSliderTooltip(int index) => string.Format(Strings.Get("STRINGS.UI.UISIDESCREENS.RADBOLTTHRESHOLDSIDESCREEN.TOOLTIP"), ParticleThreshold);
        #endregion
        public static void AddStatusItemsToDatabase(BuildingStatusItems statusItemsList)
        {
            statusItemsList.Add(CreatureCountStatus);
            statusItemsList.Add(EggCountStatus);
            statusItemsList.Add(WattageStatus);
            statusItemsList.Add(HEPStatus);
            //statusItemsList.Add(HEPStorageStatus);
            statusItemsList.Add(NoHEPProductionWarningStatus);
            statusItemsList.Add(HEPProductionDisabledStatus);
        }
        public static void InitializeStatusCategory(StatusItemCategories categories)
        {
            RadboltProductionStatusCategory = new StatusItemCategory(
                RadboltProductionStatusCategoryID, categories, RadboltProductionStatusCategoryID);
        }
        protected (FilteredStorage FilteredStorage, Traverse<FetchList2> FetchList)
            MakeFilteredStorage(in Tag requiredTag, MethodInfo method)
        {
            FilteredStorage fs = new FilteredStorage(this, null, this, false, Db.Get().ChoreTypes.PowerFetch);
            fs.SetRequiredTag(requiredTag);
            var onStorageChanged = AccessTools.MethodDelegate<Action<object>>(method, fs);
            Unsubscribe((int)GameHashes.OnStorageChange, onStorageChanged);
            var traverse = Traverse.Create(fs).Field<FetchList2>("fetchList");
            return (fs, traverse);
        }
        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            MethodInfo method = AccessTools.Method(typeof(FilteredStorage), "OnStorageChanged");
            //Action<object> fsEggsOnStorageChanged, fsCreaturesOnStorageChanged;
            (filteredStorageEggs, fetchListEggs) = MakeFilteredStorage(in GameTags.Egg, method);
            //(filteredStorageCreatures, fetchListCreatures) = MakeFilteredStorage(in GameTags.Creatures.Deliverable, method);
            //fsOnStorageChanged = fsEggsOnStorageChanged + fsCreaturesOnStorageChanged;
        }
        protected override void OnSpawn()
        {
            base.OnSpawn();
            storageDropCell = GetComponent<Building>().GetUtilityInputCell();
            directionController = new EightDirectionController(GetComponent<KBatchedAnimController>(),
                "redirector_target", "redirect", EightDirectionController.Offset.Infront);
            Direction = Direction;
            Subscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
            Subscribe((int)GameHashes.DeconstructComplete, OnDeconstruct);
            treeFilterable.OnFilterChanged += OnFilterChanged;
            filteredStorageEggs.FilterChanged();
            //filteredStorageCreatures.FilterChanged();

            if (particleStorage)
            {
                Subscribe((int)GameHashes.LogicEvent, OnLogicValueChanged);

                radiationEmitter.SetEmitting(true);
            }
            accumulator = Game.Instance.accumulators.Add(name, this);
            structureTemperature = StructureTemperatures.GetHandle(gameObject);
            //UpdateLogic();

            selectable.SetStatusItem(Db.Get().StatusItemCategories.Main, CreatureCountStatus, this);
            selectable.AddStatusItem(EggCountStatus, this);
            /*if (particleStorage)
            {
                selectable.AddStatusItem(HEPStorageStatus, this);
            }*/
        }
        protected override void OnCleanUp()
        {
            treeFilterable.OnFilterChanged -= OnFilterChanged;
            //fsOnStorageChanged = null;
            Game.Instance.accumulators.Remove(accumulator);
            //filteredStorageCreatures.CleanUp();
            filteredStorageEggs.CleanUp();
            base.OnCleanUp();
        }
        protected LogicCircuitNetwork GetNetwork() => Game.Instance.logicCircuitManager
            .GetNetworkForCell(logicPorts.GetPortCell(ShinebugReactorConfig.FIRE_PORT_ID));

        protected void OnLogicValueChanged(object data)
        {
            LogicValueChanged logicValueChanged = (LogicValueChanged)data;
            if (logicValueChanged.portID != ShinebugReactorConfig.FIRE_PORT_ID) return;
            bool hasLogicWire = GetNetwork() != null;
            isLogicActive = (logicValueChanged.newValue > 0) && hasLogicWire;
        }

        public void UpdateLogic()
        {
            isFull = Mathf.RoundToInt(AmountStored) >= UserCapacity;
            logicPorts.SendSignal(ShinebugReactorConfig.FULL_PORT_ID, isFull ? 1 : 0);
        }

        public void UpdateFetch(bool dirty = true)
        {
            if (dirty)
            {
                UpdateLogic();

                FetchList2 fetchList1 = fetchListEggs.Value;//, fetchList2 = fetchListCreatures.Value;
                if (fetchList1 == null || !fetchList1.InProgress)
                {
                    filteredStorageEggs.FilterChanged();
                }
                /*if (fetchList2 == null || !fetchList2.InProgress)
                {
                    filteredStorageCreatures.FilterChanged();
                }*/
                //fsOnStorageChanged(null);
            }
        }

        public void EggHatched(GameObject egg, float age = 0f)
        {
            if (egg == null) return;
            Unsubscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
            storage.Drop(egg);
            Subscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
            var shinebugName = egg.PrefabID().Name.Replace(EGG_POSTFIX, string.Empty).Replace(BABY_POSTFIX, string.Empty);
            Creatures.Add(new ShinebugSimulator(shinebugName, age));
            UpdateFetch();
        }

        public void SpawnCreature(ShinebugSimulator shinebug)
        {
            GameObject go = Util.KInstantiate(Assets.GetPrefab(shinebug.Id),
                Grid.CellToPosCBC(storageDropCell, Grid.SceneLayer.Creatures));
            go.SetActive(true);
            AgeMonitor.Instance smi = go.GetSMI<AgeMonitor.Instance>();
            if (smi != null)
            {
                smi.age.value = shinebug.Age / CYCLE_LENGTH;
            }
        }

        public static void SpawnDrops(ShinebugSimulator shinebug)
        {
            Butcherable butcherable = Assets.GetPrefab(shinebug.Id)?.GetComponent<Butcherable>();
            butcherable?.CreateDrops();
        }

        public void SpawnHEP()
        {
            int particleOutputCell = building.GetHighEnergyParticleOutputCell();
            GameObject particle = GameUtil.KInstantiate(Assets.GetPrefab(HighEnergyParticleConfig.ID),
                Grid.CellToPosCCC(particleOutputCell, Grid.SceneLayer.FXFront2), Grid.SceneLayer.FXFront2);
            if (particle != null)
            {
                particle.SetActive(true);
                HighEnergyParticle component = particle.GetComponent<HighEnergyParticle>();
                component.payload = particleStorage.ConsumeAndGet(ParticleThreshold);
                component.SetDirection(Direction);
                directionController.PlayAnim("redirect_send");
                directionController.controller.Queue("redirect");
                //this.particleController.meterController.Play((HashedString)"orb_send");
                //this.particleController.meterController.Queue((HashedString)"orb_off");
                //this.particleVisualPlaying = false;
            }
        }

        protected void OnFilterChanged(HashSet<Tag> tags)
        {
            if (Options.Instance.DropHatched)
            {
                for (int i = Creatures.Count - 1; i >= 0; --i)
                {
                    if (!(tags.Contains(Creatures[i].Id) || tags.Contains(Creatures[i].Data.Egg)))
                    {
                        SpawnCreature(Creatures[i]);
                        Creatures.RemoveAt(i);
                    }
                }
            }
        }

        protected void OnStorageChanged(GameObject go, bool dirty)
        {
            if (go == null) return;
            bool dropped = !storage.items.Contains(go);
            if (!dropped && (Mathf.RoundToInt(AmountStored) > UserCapacity
                || !treeFilterable.ContainsTag(go.PrefabID())))
            {
                Unsubscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
                storage.Drop(go);
                Subscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
                dropped = true;
            }
            //filterable.UpdateFilters(new List<Tag>(filterable.AcceptedTags));
            if (dropped)
            {
                go.transform.SetPosition(Grid.CellToPosCBC(storageDropCell,
                    go.HasTag(GameTags.Creature) ? Grid.SceneLayer.Creatures : Grid.SceneLayer.Ore));
            }
            else if (go.HasTag(GameTags.Creature))
            {
                float age = go.GetSMI<AgeMonitor.Instance>().age.value * CYCLE_LENGTH;
                EggHatched(go, age);
                go.DeleteObject();
                dirty = false;
            }
            UpdateFetch(dirty);
        }
        protected void OnStorageChanged(object data)
        {
            OnStorageChanged(data as GameObject, true);
        }

        protected void OnDeconstruct(object data)
        {
            foreach (ShinebugSimulator shinebug in Creatures)
            {
                SpawnCreature(shinebug);
            }
        }

        protected void DoConsumeParticlesWhileDisabled(float dt)
        {
            if (particleStorage)
            {
                CurrentHEP = -particleStorage.ConsumeAndGet(dt * HighEnergyParticleSpawnerConfig.DISABLED_CONSUMPTION_RATE);
            }
        }

        public override void EnergySim200ms(float dt)
        {
            base.EnergySim200ms(dt);
            operational.SetFlag(wireConnectedFlag, CircuitID != CircuitManager.INVALID_ID);
            operational.SetActive(operational.IsOperational && Creatures.Count > 0);
            light.enabled = operational.IsActive;
            CurrentWattage = 0f;
            CurrentHEP = 0f;
            CurrentLight = 0f;
            if (operational.IsActive)
            {
                float rad = 0;
                foreach (ShinebugSimulator shinebug in Creatures)
                {
                    var shinebugData = shinebug.Data;
                    CurrentLight += shinebugData.Lux;
                    switch (Options.Instance.PowerGenerationMode)
                    {
                        /*case Options.PowerGenerationModeType.SolarPanel:
                            CurrentWattage += shinebugData.Lux;
                            break;*/
                        case Options.PowerGenerationModeType.Ratio:
                            if (shinebugData.Lux > 0f)
                                CurrentWattage++;
                            break;
                    }
                    rad += shinebugData.Rad;
                }
                switch (Options.Instance.PowerGenerationMode)
                {
                    case Options.PowerGenerationModeType.SolarPanel:
                        CurrentWattage = ShinebugReactorConfig.RADIUS_FACTOR
                            * SolarPanelConfig.WATTS_PER_LUX * CurrentLight;
                        break;
                    case Options.PowerGenerationModeType.Ratio:
                        CurrentWattage *= Options.Instance.MaxPowerOutput / MaxCapacity;
                        break;
                }
                if (!Options.Instance.StaticLightEmission)
                {
                    var newLightAmount = (int)(CurrentLight * ShinebugReactorConfig.EmitLeakRate);
                    if (light.Lux != newLightAmount)
                    {
                        light.Lux = newLightAmount;
                        light.FullRefresh();
                    }
                }
                if (particleStorage)
                {
                    emitRads = rad * ShinebugReactorConfig.EmitLeakRate;
                    //radiationEmitter.emitRads = rad * ShinebugReactorConfig.EmitLeakRate;
                    if (isLogicActive)
                    {
                        if (CurrentWattage >= ShinebugReactorConfig.WattageRequired)
                        {
                            CurrentWattage -= ShinebugReactorConfig.WattageRequired;
                        }
                        else
                        {
                            rad = 0f;
                        }
                    }
                    /*else if (!particleStorage.IsEmpty())
                    {
                        CurrentWattage -= ShinebugReactorConfig.PowerSaveEnergyRequired;
                    }*/
                }
                CurrentWattage = Mathf.Clamp(CurrentWattage, 0.0f, Options.Instance.MaxPowerOutput);
                if (CurrentWattage > 0.0f)
                {
                    Game.Instance.accumulators.Accumulate(accumulator, CurrentWattage * dt);
                    GenerateJoules(Mathf.Max(CurrentWattage * dt, dt));
                }
                if (particleStorage && isLogicActive && (rad > 0f))
                {
                    CurrentHEP = rad * HighEnergyParticleSpawnerConfig.HEP_PER_RAD;
                    StructureTemperatures.ProduceEnergy(structureTemperature,
                        ShinebugReactorConfig.HeatPerSecond * dt,
                        GameStrings.BUILDING.STATUSITEMS.OPERATINGENERGY.OPERATING, dt);
                    if (particleStorage.RemainingCapacity() > 0f)
                    {
                        particleStorage.Store(Mathf.Min((CurrentHEP / CYCLE_LENGTH) * dt,
                            particleStorage.RemainingCapacity()));
                    }
                }
            }
            else
            {
                emitRads = 0f;
            }
            if (CurrentWattage <= 0f)
            {
                DoConsumeParticlesWhileDisabled(dt);
            }
            UpdateStatusItem();
        }

        public void Sim1000ms(float dt)
        {
            if (particleStorage)
            {
                if (isLogicActive && particleStorage.Particles >= ParticleThreshold)
                {
                    SpawnHEP();
                }
                if (radiationEmitter.emitRads != emitRads)
                {
                    radiationEmitter.emitRads = emitRads;
                    radiationEmitter.Refresh();
                }
            }
            if (Options.Instance.ReproductionMode != Options.ReproductionModeType.Immortality)
            {
                bool dirty = false;
                Unsubscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
                for (int i = Creatures.Count - 1; i >= 0; --i)
                {
                    ShinebugSimulator shinebug = Creatures[i];
                    if (shinebug.Simulate(dt))
                    {
                        dirty = true;
                        Creatures.RemoveAt(i);
                        SpawnDrops(shinebug);
                        if (Options.Instance.ReproductionMode == Options.ReproductionModeType.Reproduction)
                        {
                            GameObject egg = GameUtil.KInstantiate(Assets.GetPrefab(shinebug.Data.Egg),
                                transform.position, Grid.SceneLayer.Ore);
                            egg.SetActive(true);
                            storage.Store(egg);
                            OnStorageChanged(egg, false);
                        }
                    }
                }
                Subscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
                UpdateFetch(dirty);
            }
        }

        protected void UpdateStatusItem()
        {
            Db db = Db.Get();
            //selectable.ReplaceStatusItem(statusHandles[0], CreatureCountStatus, this);
            //selectable.SetStatusItem(Db.Get().StatusItemCategories.Main, CreatureCountStatus, this);
            //selectable.ToggleStatusItem(EggCountStatus, true, this);
            selectable.SetStatusItem(db.StatusItemCategories.Power, operational.IsActive ?
                WattageStatus : db.BuildingStatusItems.GeneratorOffline, this);
            if (particleStorage)
            {
                StatusItem currentHEPStatus = HEPStatus;
                if (CurrentWattage <= 0f && particleStorage.HasRadiation())
                {
                    currentHEPStatus = db.BuildingStatusItems.LosingRadbolts;
                }
                else if (!isLogicActive)
                {
                    currentHEPStatus = HEPProductionDisabledStatus;
                }
                else if (CurrentHEP <= 0f && CurrentWattage < ShinebugReactorConfig.WattageRequired)
                {
                    currentHEPStatus = NoHEPProductionWarningStatus;
                }
                selectable.SetStatusItem(RadboltProductionStatusCategory, currentHEPStatus, this);
                selectable.SetStatusItem(db.StatusItemCategories.OperatingEnergy,
                    (CurrentHEP > 0f) ? OperatingEnergyStatus : null,
                    StructureTemperatures.GetPayload(structureTemperature).simHandleCopy);
                /*selectable.SetStatusItem(Db.Get().StatusItemCategories.Stored,
                //statusHandles[2] = selectable.ReplaceStatusItem(statusHandles[2],
                    CurrentWattage > 0f || particleStorage.IsEmpty() ?
                    HEPStatus : Db.Get().BuildingStatusItems.LosingRadbolts, this);*/
            }
        }
    }
}
﻿using Klei.AI;
using KSerialization;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PeterHan.PLib.Options;
using static STRINGS.UI;
using GameStrings = STRINGS;
using HarmonyLib;
using System.Reflection;

namespace ShinebugReactor
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public class ShinebugReactor : Generator, ISim1000ms, IHighEnergyParticleDirection,
        ISingleSliderControl, IUserControlledCapacity
    {
        public const string EGG_POSTFIX = "Egg";
        public const string BABY_POSTFIX = "Baby";

        #region Components
        [MyCmpReq]
        protected TreeFilterable treeFilterable;
        [MyCmpReq]
        protected LogicPorts logicPorts;
        [MyCmpReq]
        protected Light2D light;
        [MyCmpReq]
        protected Storage storage;
        [MyCmpGet]
        protected HighEnergyParticleStorage particleStorage;
        [MyCmpGet]
        protected RadiationEmitter radiationEmitter;

        [Serialize]
        public Ref<HighEnergyParticlePort> capturedByRef = new Ref<HighEnergyParticlePort>();
        #endregion
        protected HandleVector<int>.Handle accumulator;
        protected HandleVector<int>.Handle structureTemperature;
        [Serialize]
        protected EightDirection _direction;
        protected EightDirectionController directionController;

        //protected Guid[] statusHandles = new Guid[3];
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
        #region StatusItems
        public static readonly StatusItem CreatureCountStatus
            = new StatusItem("ShinebugReactorCreatures", "BUILDING",
            string.Empty, StatusItem.IconType.Info, NotificationType.Neutral,
            false, OverlayModes.None.ID,
            resolve_string_callback: ((str, data) =>
            {
                int value = ((ShinebugReactor)data).Creatures.Count;
                str = string.Format(str, value);
                return str;
            }));
        public static readonly StatusItem EggCountStatus
            = new StatusItem("ShinebugReactorEggs", "BUILDING",
            string.Empty, StatusItem.IconType.Info, NotificationType.Neutral,
            false, OverlayModes.None.ID,
            resolve_string_callback: ((str, data) =>
            {
                int value = Mathf.RoundToInt(((ShinebugReactor)data).storage.UnitsStored());
                str = string.Format(str, value);
                return str;
            }));
        public static readonly StatusItem WattageStatus
            = new StatusItem("ShinebugReactorWattage", "BUILDING",
            string.Empty, StatusItem.IconType.Info, NotificationType.Neutral,
            false, OverlayModes.Power.ID,
            resolve_string_callback: ((str, data) =>
            {
                float value = ((ShinebugReactor)data).CurrentWattage;
                str = string.Format(str, GameUtil.GetFormattedWattage(value));
                return str;
            }));
        public static readonly StatusItem HEPStatus
            = new StatusItem("ShinebugReactorHEP", "BUILDING",
            string.Empty, StatusItem.IconType.Info, NotificationType.Neutral,
            false, OverlayModes.Radiation.ID,
            resolve_string_callback: ((str, data) =>
            {
                ShinebugReactor reactor = (ShinebugReactor)data;
                float value = reactor.CurrentHEP;
                float HEPInStorage = reactor.particleStorage.Particles;
                str = string.Format(str, Util.FormatWholeNumber(value), UNITSUFFIXES.HIGHENERGYPARTICLES.PARTRICLES,
                    Util.FormatWholeNumber(HEPInStorage));
                return str;
            }));
        public static readonly StatusItem HEPStorageStatus
            = new StatusItem("StorageLocker", "BUILDING",
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
            }));
        public static readonly StatusItem NoHEPProductionWarningStatus
            = new StatusItem("ShinebugReactorNoHEPProductionWarning", "BUILDING",
            string.Empty, StatusItem.IconType.Info, NotificationType.BadMinor,
            false, OverlayModes.Radiation.ID);
        public static readonly StatusItem HEPProductionDisabledStatus
            = new StatusItem("ShinebugReactorHEPProductionDisabled", "BUILDING",
            string.Empty, StatusItem.IconType.Info, NotificationType.Neutral,
            false, OverlayModes.Radiation.ID);
        public static StatusItem OperatingEnergyStatusItem;
        #endregion
        protected float emitRads;
        /*[Serialize]
        private List<ShinebugEggSimulator> _shinebugEggs;*/
        [Serialize]
        public readonly List<ShinebugSimulator> Creatures = new List<ShinebugSimulator>(100);
        [NonSerialized]
        public float CurrentWattage;
        [NonSerialized]
        public float CurrentHEP;

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
        #endregion
        #region ISliderControl
        public string SliderTitleKey => "STRINGS.UI.UISIDESCREENS.RADBOLTTHRESHOLDSIDESCREEN.TITLE";
        public string SliderUnits => (string)UNITSUFFIXES.HIGHENERGYPARTICLES.PARTRICLES;
        public int SliderDecimalPlaces(int index) => 0;
        public float GetSliderMin(int index) => HighEnergyParticleSpawnerConfig.MIN_SLIDER;
        public float GetSliderMax(int index) => HighEnergyParticleSpawnerConfig.MAX_SLIDER;
        public float GetSliderValue(int index) => ParticleThreshold;
        public void SetSliderValue(float percent, int index) => ParticleThreshold = percent;
        public string GetSliderTooltipKey(int index) => "STRINGS.UI.UISIDESCREENS.RADBOLTTHRESHOLDSIDESCREEN.TOOLTIP";
        public string GetSliderTooltip() => string.Format(Strings.Get("STRINGS.UI.UISIDESCREENS.RADBOLTTHRESHOLDSIDESCREEN.TOOLTIP"), ParticleThreshold);
        #endregion
        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            Tag[] requiredTagsEggs = { GameTags.Egg };
            Tag[] requiredTagsCreatures = { GameTags.Creatures.Deliverable };
            //filteredStorageEggs = new FilteredStorage(this, requiredTagsEggs, null, this, false, Db.Get().ChoreTypes.PowerFetch);
            filteredStorageEggs = new FilteredStorage(this, null, this, false, Db.Get().ChoreTypes.PowerFetch);
            //filteredStorageCreatures = new FilteredStorage(this, /*null*/requiredTagsCreatures, null, /*null*/this, false, Db.Get().ChoreTypes.PowerFetch);
            MethodInfo method = typeof(FilteredStorage).GetMethod("OnStorageChanged",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Action<object> fsEggsOnStorageChanged, fsCreaturesOnStorageChanged;
            fsEggsOnStorageChanged = (Action<object>)method.CreateDelegate(typeof(Action<object>), filteredStorageEggs);
            //fsCreaturesOnStorageChanged = (Action<object>)method.CreateDelegate(typeof(Action<object>), filteredStorageCreatures);
            Unsubscribe((int)GameHashes.OnStorageChange, fsEggsOnStorageChanged);
            //Unsubscribe((int)GameHashes.OnStorageChange, fsCreaturesOnStorageChanged);
            //fsOnStorageChanged = fsEggsOnStorageChanged + fsCreaturesOnStorageChanged;

            OperatingEnergyStatusItem = Traverse.Create(GameComps.StructureTemperatures).Field("operatingEnergyStatusItem")
                .GetValue<StatusItem>();
            Db.Get().BuildingStatusItems.Add(CreatureCountStatus);
            Db.Get().BuildingStatusItems.Add(EggCountStatus);
            Db.Get().BuildingStatusItems.Add(WattageStatus);
            Db.Get().BuildingStatusItems.Add(HEPStatus);
            Db.Get().BuildingStatusItems.Add(HEPStorageStatus);
            Db.Get().BuildingStatusItems.Add(NoHEPProductionWarningStatus);
            Db.Get().BuildingStatusItems.Add(HEPProductionDisabledStatus);
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
            filteredStorageEggs.FilterChanged();
            //filteredStorageCreatures.FilterChanged();

            if (particleStorage)
            {
                Subscribe((int)GameHashes.LogicEvent, OnLogicValueChanged);

                radiationEmitter.SetEmitting(true);
            }
            treeFilterable.OnFilterChanged += OnFilterChanged;
            accumulator = Game.Instance.accumulators.Add("Element", this);
            structureTemperature = GameComps.StructureTemperatures.GetHandle(gameObject);
            //UpdateLogic();

            selectable.SetStatusItem(Db.Get().StatusItemCategories.Main, CreatureCountStatus, this);
            selectable.AddStatusItem(EggCountStatus, this);
            if (particleStorage)
            {
                selectable.AddStatusItem(HEPStorageStatus, this);
            }
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

                FetchList2 fetchList1 = Traverse.Create(filteredStorageEggs)
                    .Field("fetchList").GetValue<FetchList2>();
                //FetchList2 fetchList2 = Traverse.Create(filteredStorageCreatures)
                    //.Field("fetchList").GetValue<FetchList2>();
                if (fetchList1 == null || !fetchList1.InProgress)
                {
                    filteredStorageEggs.FilterChanged();
                }
                //if (fetchList2 == null || !fetchList2.InProgress)
                //{
                    //filteredStorageCreatures.FilterChanged();
                //}
                //fsOnStorageChanged(null);
            }
        }

        public void EggHatched(GameObject egg, float age = 0f)
        {
            if (egg == null) return;
            Unsubscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
            storage.Drop(egg).AddTag(GameTags.StoredPrivate);
            Subscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
            string shinebugName = egg.PrefabID().Name.Replace(EGG_POSTFIX, string.Empty).Replace(BABY_POSTFIX, string.Empty);
            Creatures.Add(new ShinebugSimulator(shinebugName, age));
            UpdateFetch();
        }

        public void SpawnCreature(ShinebugSimulator shinebug)
        {
            GameObject go = Util.KInstantiate(Assets.GetPrefab(shinebug.Name),
                Grid.CellToPosCBC(storageDropCell, Grid.SceneLayer.Creatures));
            go.SetActive(true);
            AgeMonitor.Instance smi = go.GetSMI<AgeMonitor.Instance>();
            if (smi != null)
            {
                smi.age.value = shinebug.Age / 600f;
            }
        }

        public void SpawnDrops(ShinebugSimulator shinebug)
        {
            string[] drops = Assets.GetPrefab(shinebug.Name)?.GetComponent<Butcherable>()?.drops;
            if (drops != null)
            {
                foreach (string drop in drops)
                {
                    GameObject go = Util.KInstantiate(Assets.GetPrefab(drop),
                        Grid.CellToPosCBC(storageDropCell, Grid.SceneLayer.Ore));
                    go.SetActive(true);
                    Edible edible = go.GetComponent<Edible>();
                    if (edible)
                    {
                        ReportManager.Instance.ReportValue(ReportManager.ReportType.CaloriesCreated,
                            edible.Calories, StringFormatter.Replace(ENDOFDAYREPORT.NOTES.BUTCHERED,
                            "{0}", go.GetProperName()), ENDOFDAYREPORT.NOTES.BUTCHERED_CONTEXT);
                    }
                }
            }
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
                    if (!(tags.Contains(Creatures[i].Name + EGG_POSTFIX) || tags.Contains(Creatures[i].Name)))
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
                || !go.HasAnyTags(treeFilterable.GetTags().ToArray())))
            {
                Unsubscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
                storage.Drop(go);
                Subscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
                dropped = true;
            }
            //filterable.UpdateFilters(new List<Tag>(filterable.AcceptedTags));
            if (dropped)
            {
                /*go.transform.SetPosition(go.HasTag(GameTags.Creature) ?
                    Grid.CellToPosCBC(Grid.PosToCell(storageDropPosition), Grid.SceneLayer.Creatures)
                    : storageDropPosition);*/
                go.transform.SetPosition(Grid.CellToPosCBC(storageDropCell,
                    go.HasTag(GameTags.Creature) ? Grid.SceneLayer.Creatures : Grid.SceneLayer.Ore));
            }
            else if (go.HasTag(GameTags.Creature))
            {
                float age = (go.GetSMI<AgeMonitor.Instance>()?.age.value * 600f).GetValueOrDefault();
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
            //this.progressMeterController.SetPositionPercent(this.GetProgressBarFillPercentage());
        }

        public override void EnergySim200ms(float dt)
        {
            base.EnergySim200ms(dt);
            operational.SetFlag(wireConnectedFlag, CircuitID != ushort.MaxValue);
            operational.SetActive(operational.IsOperational && Creatures.Count > 0);
            light.enabled = operational.IsActive;
            CurrentWattage = 0f;
            CurrentHEP = 0f;
            if (operational.IsOperational)
            {
                float rad = 0;
                foreach (ShinebugSimulator.ShinebugData shinebugData
                    in Creatures.Select(shinebug => shinebug.Data))
                {
                    switch (Options.Instance.PowerGenerationMode)
                    {
                        case Options.PowerGenerationModeType.SolarPanel:
                            CurrentWattage += shinebugData.Lux;
                            break;
                        case Options.PowerGenerationModeType.Ratio:
                            if (shinebugData.Lux > 0)
                                CurrentWattage++;
                            break;
                    }
                    rad += shinebugData.Rad;
                }
                switch (Options.Instance.PowerGenerationMode)
                {
                    case Options.PowerGenerationModeType.SolarPanel:
                        CurrentWattage *= 26.5f * SolarPanelConfig.WATTS_PER_LUX;
                        break;
                    case Options.PowerGenerationModeType.Ratio:
                        CurrentWattage *= Options.Instance.MaxPowerOutput / MaxCapacity;
                        break;
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
                    GameComps.StructureTemperatures.ProduceEnergy(structureTemperature,
                        ShinebugReactorConfig.HeatPerSecond * dt,
                        GameStrings.BUILDING.STATUSITEMS.OPERATINGENERGY.OPERATING, dt);
                    if (particleStorage.RemainingCapacity() > 0f)
                    {
                        particleStorage.Store(Mathf.Min((CurrentHEP / 600f) * dt,
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
            if (isLogicActive && particleStorage?.Particles >= ParticleThreshold)
            {
                SpawnHEP();
            }
            if (radiationEmitter && radiationEmitter.emitRads != emitRads)
            {
                radiationEmitter.emitRads = emitRads;
                radiationEmitter.Refresh();
            }
            if (Options.Instance.ReproductionMode != Options.ReproductionModeType.Immortality)
            {
                bool dirty = false;
                Unsubscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
                for (int i = Creatures.Count - 1; i >= 0; --i)
                {
                    if (Creatures[i].Simulate(dt))
                    {
                        dirty = true;
                        ShinebugSimulator shinebug = Creatures[i];
                        Creatures.RemoveAt(i);
                        SpawnDrops(shinebug);
                        if (Options.Instance.ReproductionMode == Options.ReproductionModeType.Reproduction)
                        {
                            GameObject egg = GameUtil.KInstantiate(Assets.GetPrefab(shinebug.Name + EGG_POSTFIX),
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
            //selectable.ReplaceStatusItem(statusHandles[0], CreatureCountStatus, this);
            //selectable.SetStatusItem(Db.Get().StatusItemCategories.Main, CreatureCountStatus, this);
            //selectable.ToggleStatusItem(EggCountStatus, true, this);
            selectable.SetStatusItem(Db.Get().StatusItemCategories.Power,
                operational.IsActive ? WattageStatus : Db.Get().BuildingStatusItems.GeneratorOffline, this);
            if (particleStorage)
            {
                StatusItem currentHEPStatus = HEPStatus;
                if (CurrentWattage <= 0f && particleStorage.HasRadiation())
                {
                    currentHEPStatus = Db.Get().BuildingStatusItems.LosingRadbolts;
                }
                else if (!isLogicActive)
                {
                    currentHEPStatus = HEPProductionDisabledStatus;
                }
                else if (CurrentHEP <= 0f && CurrentWattage < ShinebugReactorConfig.WattageRequired)
                {
                    currentHEPStatus = NoHEPProductionWarningStatus;
                }
                selectable.SetStatusItem(Db.Get().StatusItemCategories.Stored, currentHEPStatus, this);
                selectable.SetStatusItem(Db.Get().StatusItemCategories.OperatingEnergy,
                    (currentHEPStatus == HEPStatus) ? OperatingEnergyStatusItem : null,
                    structureTemperature.index);
                /*selectable.SetStatusItem(Db.Get().StatusItemCategories.Stored,
                //statusHandles[2] = selectable.ReplaceStatusItem(statusHandles[2],
                    CurrentWattage > 0f || particleStorage.IsEmpty() ?
                    HEPStatus : Db.Get().BuildingStatusItems.LosingRadbolts, this);*/
            }
        }
    }
}
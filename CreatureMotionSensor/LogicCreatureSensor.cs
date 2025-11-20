using KSerialization;
using STRINGS;
using System.Collections.Generic;

namespace Creature_Motion_Sensor
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public class LogicCreatureSensor : Switch, IIntSliderControl, ISim1000ms, ISim200ms
    {
        #region Components
        [MyCmpReq]
        private readonly KSelectable selectable;
        [MyCmpGet]
        private readonly Rotatable rotatable;
        [MyCmpReq]
        private readonly RangeVisualizer rangeVisualizer;
        [MyCmpReq]
        private readonly LogicPorts logicPorts;
        [MyCmpReq]
        private readonly KBatchedAnimController animController;
        #endregion
        [Serialize]
        protected int pickupRange = 5;
        private readonly List<Pickupable> creatures = new List<Pickupable>();
        private readonly List<int> reachableCells = new List<int>(100);
        private bool wasOn;
        private HandleVector<int>.Handle pickupablesChangedEntry;
        private bool pickupablesDirty;
        private Extents pickupableExtents;

        #region ISliderControl
        public string SliderTitleKey => "STRINGS.UI.STARMAP.ROCKETSTATS.TOTAL_RANGE";
        public string SliderUnits => UI.UNITSUFFIXES.DISTANCE.METER;
        public int SliderDecimalPlaces(int index) => 0;
        public float GetSliderMin(int index) => 3f;
        public float GetSliderMax(int index) => 21f;
        public float GetSliderValue(int index) => pickupRange;
        public void SetSliderValue(float value, int index)
        {
            int num = (int)((value + 1f) / 2f) * 2 - 1;
            if (pickupRange == num)
                return;
            pickupRange = num;
            RefreshReachableCells();
            RefreshVisualCells();
        }
        public string GetSliderTooltipKey(int index) => string.Format(STRINGS.UI.UISIDESCREENS.LOGICCREATURESENSORSIDESCREEN.TOOLTIP, pickupRange);
        public string GetSliderTooltip(int index) => string.Format(STRINGS.UI.UISIDESCREENS.LOGICCREATURESENSORSIDESCREEN.TOOLTIP, pickupRange);
        #endregion

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            simRenderLoadBalance = true;
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            OnToggle += OnSwitchToggled;
            UpdateLogicCircuit();
            UpdateVisualState(true);
            RefreshReachableCells();
            wasOn = switchedOn;
            RefreshVisualCells();
        }

        protected override void OnCleanUp()
        {
            GameScenePartitioner.Instance.Free(ref pickupablesChangedEntry);
            base.OnCleanUp();
        }

        public void Sim1000ms(float dt)
        {
            RefreshReachableCells();
        }

        public void Sim200ms(float dt)
        {
            RefreshPickupables();
        }

        private void RefreshVisualCells()
        {
            rangeVisualizer.RangeMin.x = -pickupRange / 2;
            rangeVisualizer.RangeMax.x = pickupRange / 2;
            rangeVisualizer.RangeMax.y = pickupRange - 1;
            int cell = this.NaturalBuildingCell();
            CellOffset offset = new CellOffset(0, pickupRange / 2);
            if (rotatable)
            {
                offset = rotatable.GetRotatedCellOffset(offset);
            }
            if (Grid.IsCellOffsetValid(cell, offset))
                cell = Grid.OffsetCell(cell, offset);
            pickupableExtents = new Extents(cell, pickupRange / 2);
            GameScenePartitioner.Instance.Free(ref pickupablesChangedEntry);
            pickupablesChangedEntry = GameScenePartitioner.Instance.Add("CreatureSensor.PickupablesChanged", gameObject, pickupableExtents, GameScenePartitioner.Instance.pickupablesChangedLayer, OnPickupablesChanged);
            pickupablesDirty = true;
        }

        private void RefreshReachableCells()
        {
            reachableCells.Clear();
            int x, y;
            Grid.CellToXY(this.NaturalBuildingCell(), out x, out y);
            int num = x - pickupRange / 2;
            for (int indexY = y; indexY < y + pickupRange; ++indexY)
            {
                for (int indexX = num; indexX < num + pickupRange; ++indexX)
                {
                    int cell = Grid.InvalidCell;
                    Vector2I xy = Vector2I.zero;
                    if (rotatable)
                    {
                        CellOffset offset = new CellOffset(indexX - x, indexY - y);
                        offset = rotatable.GetRotatedCellOffset(offset);
                        if (Grid.IsCellOffsetValid(this.NaturalBuildingCell(), offset))
                        {
                            cell = Grid.OffsetCell(this.NaturalBuildingCell(), offset);
                            xy = Grid.CellToXY(cell);
                        }
                    }
                    else
                    {
                        cell = Grid.XYToCell(indexX, indexY);
                        xy = new Vector2I(indexX, indexY);
                    }
                    if (Grid.IsValidCell(cell) && Grid.IsPhysicallyAccessible(x, y, xy.x, xy.y, true))
                        reachableCells.Add(cell);
                }
            }
        }

        private void RefreshPickupables()
        {
            if (!pickupablesDirty) return;
            creatures.Clear();
            var pooledList = ListPool<ScenePartitionerEntry, LogicCreatureSensor>.Allocate();
            GameScenePartitioner.Instance.GatherEntries(pickupableExtents, GameScenePartitioner.Instance.pickupablesLayer, pooledList);
            int cell = Grid.PosToCell(this);
            for (int index = 0; index < pooledList.Count; ++index)
            {
                Pickupable pickupable = pooledList[index].obj as Pickupable;
                int pickupableCell = GetPickupableCell(pickupable);
                int cellRange = Grid.GetCellRange(cell, pickupableCell);
                if (IsPickupableRelevantToMyInterestsAndReachable(pickupable) && cellRange <= pickupRange)
                    creatures.Add(pickupable);
            }
            pooledList.Recycle();
            SetState(creatures.Count > 0);
            pickupablesDirty = false;
        }

        private void OnPickupablesChanged(object data)
        {
            Pickupable pickupable = data as Pickupable;
            if (!(pickupable && IsPickupableRelevantToMyInterests(pickupable)))
                return;
            pickupablesDirty = true;
        }

        public bool IsCellReachable(int cell) => reachableCells.Contains(cell);

        private static bool IsPickupableRelevantToMyInterests(Pickupable pickupable) => pickupable.KPrefabID.HasTag(GameTags.CreatureBrain);

        private bool IsPickupableRelevantToMyInterestsAndReachable(Pickupable pickupable) => IsPickupableRelevantToMyInterests(pickupable) && IsCellReachable(GetPickupableCell(pickupable));

        private static int GetPickupableCell(Pickupable pickupable) => pickupable.cachedCell;

        private void OnSwitchToggled(bool toggled_on)
        {
            UpdateLogicCircuit();
            UpdateVisualState();
        }

        private void UpdateLogicCircuit() => logicPorts.SendSignal(LogicSwitch.PORT_ID, switchedOn ? 1 : 0);

        private void UpdateVisualState(bool force = false)
        {
            if (!(wasOn != switchedOn || force))
                return;
            wasOn = switchedOn;
            animController.Play(switchedOn ? "on_pre" : "on_pst");
            animController.Queue(switchedOn ? "on" : "off");
        }

        protected override void UpdateSwitchStatus()
        {
            Db db = Db.Get();
            selectable.SetStatusItem(db.StatusItemCategories.Power, switchedOn ?
                db.BuildingStatusItems.LogicSensorStatusActive : db.BuildingStatusItems.LogicSensorStatusInactive);
        }
    }
}
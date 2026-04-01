using HarmonyLib;
using System;
using UnityEngine;
using static HarmonyLib.AccessTools;

namespace Commons
{
    [SkipSaveFileSerialization]
    public class SolidConduitFilteredConsumer : SolidConduitConsumer
    {
        //[MyCmpGet]
        protected IUserControlledCapacity capacityControl;
        [MyCmpGet]
        protected TreeFilterable treeFilterable;
        [MyCmpReq]
        protected Operational operational;
        protected Traverse<int> utilityCellField;
        protected Traverse<bool> consumingField;
        protected Func<SolidConduitFlow> getConduitFlow;
        protected Func<int> getConnectedNetworkID;

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            capacityControl = GetComponent<IUserControlledCapacity>();
            utilityCellField = Traverse.Create(this).Field<int>("utilityCell");
            consumingField = Traverse.Create(this).Field<bool>("consuming");
            getConduitFlow = MethodDelegate<Func<SolidConduitFlow>>(
                Method(typeof(SolidConduitConsumer), "GetConduitFlow"), this);
            getConnectedNetworkID = MethodDelegate<Func<int>>(
                Method(typeof(SolidConduitConsumer), "GetConnectedNetworkID"), this);
        }
        protected override void OnSpawn()
        {
            base.OnSpawn();
            Action<float> baseConduitUpdate = MethodDelegate<Action<float>>(
                Method(typeof(SolidConduitConsumer), "ConduitUpdate"), this);
            getConduitFlow().RemoveConduitUpdater(baseConduitUpdate);
            getConduitFlow().AddConduitUpdater(ConduitUpdate, ConduitFlowPriority.Default);
        }

        protected override void OnCleanUp()
        {
            getConduitFlow().RemoveConduitUpdater(ConduitUpdate);
            base.OnCleanUp();
        }
        protected void ConduitUpdate(float dt)
        {
            bool consumed = false;
            if (IsConnected && storage != null)
            {
                SolidConduitFlow conduitFlow = getConduitFlow();
                SolidConduitFlow.ConduitContents contents = conduitFlow.GetContents(utilityCellField.Value);
                Pickupable pickupable1 = conduitFlow.GetPickupable(contents.pickupableHandle);
                if (pickupable1 != null && (alwaysConsume || operational.IsOperational))
                {
                    float occupiedAmount;
                    float capacity;
                    if (capacityControl != null)
                    {
                        occupiedAmount = capacityControl.AmountStored;
                        capacity = capacityControl.UserMaxCapacity;
                    }
                    else
                    {
                        occupiedAmount = capacityTag != GameTags.Any ? storage.GetMassAvailable(capacityTag) : storage.MassStored();
                        capacity = Mathf.Min(storage.capacityKg, capacityKG);
                    }
                    float spaceAvailable = Mathf.Max(0.0f, capacity - occupiedAmount);
                    if (spaceAvailable > 0.0f)
                    {
                        bool canConsume = (capacityControl != null) ?
                            pickupable1.TotalAmount <= spaceAvailable || pickupable1.TotalAmount > capacity
                            : pickupable1.PrimaryElement.Mass <= spaceAvailable || pickupable1.PrimaryElement.Mass > capacity;
                        if (treeFilterable)
                        {
                            canConsume &= treeFilterable.ContainsTag(pickupable1.PrefabID());
                        }
                        if (canConsume)
                        {
                            Pickupable pickupable2 = conduitFlow.RemovePickupable(utilityCellField.Value);
                            if (pickupable2 != null)
                            {
                                storage.Store(pickupable2.gameObject, true);
                                consumed = true;
                            }
                        }
                    }
                }
                storage.storageNetworkID = getConnectedNetworkID();
            }
            consumingField.Value = consumed;
        }
    }
}
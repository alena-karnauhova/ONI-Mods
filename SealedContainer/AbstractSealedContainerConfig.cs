using System.Collections.Generic;
using UnityEngine;
using PeterHan.PLib.Buildings;

namespace SealedContainer
{
    public abstract class AbstractSealedContainerConfig : StorageLockerConfig
    {
        protected PBuilding InstancePBuilding;
        protected List<Storage.StoredItemModifier> StorageItemModifiers;
        public override BuildingDef CreateBuildingDef()
        {
            return InstancePBuilding.CreateDef();
        }
        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            base.ConfigureBuildingTemplate(go, prefab_tag);
            InstancePBuilding.ConfigureBuildingTemplate(go);

            Storage storage = go.AddOrGet<Storage>();
            storage.capacityKg = Options.Instance.Capacity;
            storage.SetDefaultStoredItemModifiers(StorageItemModifiers);
            Automatable automatable = go.AddOrGet<Automatable>();
            //automatable.SetAutomationOnly(false); // doesn't work
        }
        /*public override void DoPostConfigureComplete(GameObject go)
        {
            base.DoPostConfigureComplete(go);
            InstancePBuilding.DoPostConfigureComplete(go);
        }*/
    }
}

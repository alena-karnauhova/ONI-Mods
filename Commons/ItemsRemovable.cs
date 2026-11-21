using KSerialization;
using System;
using UnityEngine;
using static Commons.STRINGS.UI.UISIDESCREENS;

namespace Commons
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public class ItemsRemovable : KMonoBehaviour, ICheckboxControl
    {
        [MyCmpReq]
        protected readonly Storage storage;
        [MyCmpAdd]
        private readonly CopyBuildingSettings copyBuildingSettings;
        private static readonly Action<object, object> OnCopySettingsDelegate
            = ((component, data) => ((ItemsRemovable)component).OnCopySettings(data));

        [Serialize]
        private bool allowItemRemoval = true;

        #region ICheckboxControl
        public string CheckboxTitleKey => "STRINGS.UI.UISIDESCREENS.ITEMSREMOVABLE_SIDE_SCREEN.ALLOWREMOVALBUTTON";
        public string CheckboxLabel => ITEMSREMOVABLE_SIDE_SCREEN.ALLOWREMOVALBUTTON;
        public string CheckboxTooltip => ITEMSREMOVABLE_SIDE_SCREEN.ALLOWREMOVALBUTTONTOOLTIP;
        public bool GetCheckboxValue() => allowItemRemoval;
        public void SetCheckboxValue(bool value)
        {
            allowItemRemoval = value;
            UpdateStorageModifiers();
        }
        #endregion

        public void UpdateStorageModifiers()
        {
            storage.allowItemRemoval = allowItemRemoval;
            storage.RenotifyAll();
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            Subscribe((int)GameHashes.CopySettings, OnCopySettingsDelegate, this);
            UpdateStorageModifiers();
        }

        protected void OnCopySettings(object data)
        {
            ItemsRemovable component = ((GameObject)data).GetComponent<ItemsRemovable>();
            if (component == null) return;
            SetCheckboxValue(component.GetCheckboxValue());
        }
    }
}
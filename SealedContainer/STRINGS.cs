using static STRINGS.UI;
using GameStrings = STRINGS;

namespace SealedContainer
{
    public static class STRINGS
    {
        public static class BUILDINGS
        {
            public static class PREFABS
            {
                public static class SEALEDCONTAINER
                {
                    public static readonly LocString NAME = FormatAsLink("Sealed Container", SealedContainerConfig.ID);
                    public static readonly LocString DESC = $"Nicely sealed container. Its {FormatAsLink("contents", "ELEMENTSSOLID")} will stay forever!";
                    public static readonly LocString EFFECT = $"Prevents its {FormatAsLink("contents", "ELEMENTSSOLID")} from sublimating.";
                }
                public static class INSULATEDCONTAINER
                {
                    public static readonly LocString NAME = FormatAsLink("Insulated Container", InsulatedContainerConfig.ID);
                    public static readonly LocString DESC = $"This container is not only sealed but also insulated. Force all those {FormatAsLink("DTUs", nameof(GameStrings.CODEX.HEAT))} to stay where they are!";
                    public static readonly LocString EFFECT = $"Prevents sublimation and heat exchange of its {FormatAsLink("contents", "ELEMENTSSOLID")}.";
                }
            }
        }

        public static class OPTIONS
        {
            public static class CAPACITY
            {
                public static readonly LocString NAME = "Capacity (kg)";
                public static readonly LocString DESC = "Determines max capacity of the container.";
            }
            public static class REQUIRESUPERINSULATOR
            {
                public static readonly LocString NAME = "Insulated Container requires SuperInsulator";
                public static readonly LocString DESC = "Only Insulation can be used for Insulated Container construction if enabled.";
            }
        }

        public static class MISC
        {
            public static class TAGS
            {
                public static readonly LocString SUPERINSULATOR
                    = GameStrings.ELEMENTS.SUPERINSULATOR.NAME;
            }
        }
    }
}
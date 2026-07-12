using UnityEngine;

namespace PizzaVR.Core
{
    public enum PizzaFlavor
    {
        Margherita,
        Pepperoni,
        Hawaiian,
        Mushroom
    }

    public static class PizzaFlavorInfo
    {
        public static Color GetColor(PizzaFlavor flavor)
        {
            switch (flavor)
            {
                case PizzaFlavor.Margherita: return new Color(0.85f, 0.15f, 0.15f);
                case PizzaFlavor.Pepperoni: return new Color(0.9f, 0.45f, 0.05f);
                case PizzaFlavor.Hawaiian: return new Color(0.95f, 0.85f, 0.1f);
                case PizzaFlavor.Mushroom: return new Color(0.45f, 0.3f, 0.15f);
                default: return Color.white;
            }
        }
    }
}

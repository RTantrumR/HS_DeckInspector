using UnityEngine;

namespace HsDeckInspector
{
    /// <summary>A flat actor's measurements at identity: where it starts, and its size.</summary>
    internal struct CardPlacement
    {
        public Vector3 Anchor;
        public float Width;
        public float Height;

        public CardPlacement(Bounds bounds)
        {
            Anchor = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Width = bounds.size.x;
            Height = bounds.size.z;
        }
    }

    /// <summary>Fits a card-shaped actor onto a deck's flat side.</summary>
    internal static class CardFit
    {
        /// <summary>Measures a flat actor at identity and hangs it under parent; false, and gone, if it has no card mesh.</summary>
        public static bool Measure(Actor actor, Transform parent, int layer, out CardPlacement placement)
        {
            placement = default;
            var go = actor.gameObject;
            go.transform.SetParent(null, false);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var card = actor.m_cardMesh != null ? actor.m_cardMesh.GetComponent<Renderer>() : null;
            if (card == null)
            {
                Plugin.Log.LogWarning($"{go.name}: no card mesh to measure");
                Object.Destroy(go);
                return false;
            }
            placement = new CardPlacement(card.bounds);
            go.transform.SetParent(parent, false);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            return true;
        }

        /// <summary>
        /// Lays a card on one side of the deck, the deck's width and height, its top up and its art facing outward.
        /// It has to cover that side: the deck's own surface carries the flat picture of the card back, and any of it
        /// left showing around or over the rim reads as a second, misaligned copy. For the same reason a card must
        /// never sink into the deck: a card back's rim sits at its base. Depth squashes the card towards that base:
        /// from the front it reads the same, from the side it hugs the deck, where a hand card's raised layers and a
        /// deep card back's model would otherwise stand well off it. Distance is from the parent's origin to the card.
        /// </summary>
        public static void Place(Transform card, CardPlacement placement, float deckWidth, float deckHeight, float side, float depth, float distance)
        {
            float height = deckHeight / placement.Height;
            var scale = new Vector3(deckWidth / placement.Width, height * depth, height);
            var rotation = Quaternion.LookRotation(Vector3.up, new Vector3(0f, 0f, side));
            card.localScale = scale;
            card.localRotation = rotation;
            card.localPosition = new Vector3(0f, 0f, side) * distance - rotation * Vector3.Scale(placement.Anchor, scale);
        }
    }
}

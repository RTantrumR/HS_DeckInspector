using System.Collections;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace HsDeckInspector
{
    /// <summary>
    /// Lays both decks on the board, card backs up, tilted enough to read their thickness. These are the game's own
    /// five thickness actors, moved, not copies: draws start from the top-of-deck bones and cards go back to the mesh's
    /// bounds, so the whole game follows. Each mesh is posed on its own because a step's bottom has to stay on the same
    /// spot while its top rises. Nothing here changes renderer.enabled (see DeckInspector).
    ///
    /// The pose is taken from the camera once, so a camera shake doesn't move the decks.
    /// </summary>
    internal static class BoardDeck
    {
        private const float SettleTime = 0.6f;
        private const float BackLift = 0.004f;
        private const float AtTop = 0.01f; // how close to a bone counts as a card just placed there
        private const float InDeckScale = 0.88f; // what ZoneDeck.SetCardToInDeckState gives a card

        private sealed class Step
        {
            public Actor Actor;
            public Renderer Mesh;
            public MeshFilter Filter;
            public Transform Bone;
            public Vector3 Scale;
            public Vector3 ActorScale;
            public Vector3 MeshOffset; // the mesh from the actor, in the actor's space
            public Quaternion MeshRotation; // the mesh within the actor
            public Vector3 ActorPosition, BonePosition;
            public Quaternion ActorRotation, BoneRotation;
        }

        private sealed class Laid
        {
            public ZoneDeck Deck;
            public Step[] Steps;
            public Vector3 Slot;
            public Quaternion Pose;
            public Vector3 Base; // the middle of the deck's underside
            public float Progress;
            public GameObject Holder;
            public Transform Back;
            public CardPlacement Placement;
            public Step Fitted;
            public Follower[] Followers;
        }

        /// <summary>Something of the deck's that isn't the deck: it keeps its shape and goes where the deck went.</summary>
        private struct Follower
        {
            public Transform Transform;
            public Vector3 Position;

            public Follower(Transform transform)
            {
                Transform = transform;
                Position = transform.position;
            }
        }

        /// <summary>How a card comes off a laid deck.</summary>
        public struct OffDeck
        {
            public Quaternion Rotation;
            public Vector3 Slide;
            public Vector3 Scale;
            public bool Opposing;
        }

        private static Laid _mine, _theirs;
        private static bool _disabled;
        private static float _nextConfigCheck;
        private static System.DateTime _configTime;

        public static void Update()
        {
            if (_disabled) return;
            bool retuned = WatchConfig();
            var state = GameState.Get();
            var mgr = GameMgr.Get();
            bool inMatch = state != null && mgr != null
                && !mgr.IsBattlegrounds() && !mgr.IsMercenaries() && Camera.main != null;
            if (inMatch && KeyRules.Pressed(Plugin.LayKey.Value) && !DeckInspector.IsAnyOut && DeckInspector.KeysFree())
                Plugin.LayDecks.Value = !Plugin.LayDecks.Value;
            bool on = Plugin.LayDecks.Value;
            Side(ref _mine, inMatch ? state.GetFriendlySidePlayer()?.GetDeckZone() : null, state, on, retuned);
            Side(ref _theirs, inMatch ? state.GetOpposingSidePlayer()?.GetDeckZone() : null, state, on, retuned);
        }

        /// <summary>Puts everything back and stays out of the way until the game restarts.</summary>
        public static void Disable()
        {
            _disabled = true;
            Release(ref _mine);
            Release(ref _theirs);
        }

        /// <summary>For a card sitting on top of a laid deck. Nobody draws towards the opponent: yours comes down, theirs up.</summary>
        public static bool OnTop(GameObject card, out OffDeck off)
        {
            off = default;
            var laid = Holding(card, _mine) ?? Holding(card, _theirs);
            if (laid == null) return false;
            off.Opposing = laid == _theirs;
            // A card's local +Z is its top. Your card's +Y is its face, so it goes down. An unseen opposing card's actor
            // carries the back's art on +Y, so that goes up: the game's own in-deck angles differ by the same half turn.
            off.Rotation = laid.Pose * Quaternion.LookRotation(Vector3.up, off.Opposing ? Vector3.forward : Vector3.back);
            off.Slide = laid.Pose * Vector3.up * (off.Opposing ? Plugin.DrawSlide.Value : -Plugin.DrawSlide.Value)
                + laid.Pose * Vector3.forward * Plugin.DrawRise.Value;
            off.Scale = Vector3.one * (InDeckScale * Plugin.LayScale.Value);
            return true;
        }

        private static Laid Holding(GameObject card, Laid laid)
        {
            if (laid == null || laid.Deck == null || laid.Progress < 1f) return null;
            var top = laid.Deck.GetTopOfDeckBoneForThickness();
            if (top == null || (card.transform.position - top.position).sqrMagnitude > AtTop * AtTop) return null;
            return card.GetComponent<Card>() != null ? laid : null;
        }

        private static bool WatchConfig()
        {
            if (Time.unscaledTime < _nextConfigCheck) return false;
            _nextConfigCheck = Time.unscaledTime + 0.5f;
            var time = File.GetLastWriteTimeUtc(Plugin.ConfigPath);
            if (time == _configTime) return false;
            bool first = _configTime == default;
            _configTime = time;
            if (first) return false;
            Plugin.ReloadConfig();
            return true;
        }

        private static void Side(ref Laid laid, ZoneDeck deck, GameState state, bool on, bool retuned)
        {
            if (laid != null && laid.Deck != deck) Release(ref laid);
            if (deck == null) return;
            if (laid == null)
            {
                if (!on || !state.IsPastBeginPhase()) return;
                laid = Lay(deck);
            }
            if (retuned) Aim(laid);
            laid.Progress = Mathf.MoveTowards(laid.Progress, on ? 1f : 0f, Time.unscaledDeltaTime / SettleTime);
            Apply(laid);
            if (!on && laid.Progress <= 0f) Release(ref laid);
        }

        private static Laid Lay(ZoneDeck deck)
        {
            var laid = new Laid { Deck = deck };
            laid.Steps = new[]
            {
                Measure(deck.m_Thickness1, deck.m_Tickness1TopOfDeck),
                Measure(deck.m_Thickness25, deck.m_Tickness25TopOfDeck),
                Measure(deck.m_Thickness50, deck.m_Tickness50TopOfDeck),
                Measure(deck.m_Thickness75, deck.m_Tickness75TopOfDeck),
                Measure(deck.m_ThicknessFull, deck.m_TicknessFullTopOfDeck),
            };
            var full = laid.Steps[laid.Steps.Length - 1];
            laid.Slot = full.Mesh.transform.TransformPoint(full.Filter.sharedMesh.bounds.center);
            laid.Followers = Followers(deck);
            Aim(laid);
            BuildBack(laid);
            Plugin.Log.LogInfo($"Deck laid ({deck.m_Side}): slot {laid.Slot}, mesh rotation {full.Mesh.transform.rotation.eulerAngles}, lossy {full.Scale}");
            return laid;
        }

        /// <summary>
        /// The glows that mark the deck, and its hover area. The drop area for trades belongs to the board, which is
        /// why it needs no moving. The Prepare glow's root also holds effects for the mana crystals, so only its vortex goes.
        /// </summary>
        private static Follower[] Followers(ZoneDeck deck)
        {
            var followers = new System.Collections.Generic.List<Follower>();
            void Add(Component c)
            {
                if (c != null) followers.Add(new Follower(c.transform));
            }
            Add(deck.m_DeckFatigueGlow);
            Add(deck.m_DeckTradeableGlow);
            Add(deck.m_DeckForgeableGlow);
            Add(deck.m_deckTooltipZone);
            if (deck.m_DeckPrepareableGlow != null)
                foreach (var t in deck.m_DeckPrepareableGlow.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Vortex_")) followers.Add(new Follower(t));
            return followers.ToArray();
        }

        private static Step Measure(Actor actor, Transform bone)
        {
            var mesh = actor.GetMeshRenderer();
            var a = actor.transform;
            var m = mesh.transform;
            return new Step
            {
                Actor = actor,
                Mesh = mesh,
                Filter = actor.GetComponentInChildren<MeshFilter>(),
                Bone = bone,
                Scale = m.lossyScale,
                ActorScale = a.localScale,
                MeshOffset = Quaternion.Inverse(a.rotation) * (m.position - a.position),
                MeshRotation = Quaternion.Inverse(a.rotation) * m.rotation,
                ActorPosition = a.position,
                ActorRotation = a.rotation,
                BonePosition = bone.position,
                BoneRotation = bone.rotation,
            };
        }

        private static void Aim(Laid laid)
        {
            var view = Camera.main.transform;
            laid.Base = laid.Slot + view.right * Plugin.LayRight.Value + view.up * Plugin.LayUp.Value - view.forward * Plugin.LayLift.Value;
            // Each deck faces the camera itself, not the camera's plane: the decks sit off its axis, each at its own height,
            // and a pose shared with the plane leans them on screen, the further one more. Turned from there around its
            // own up, a deck's long sides stay upright on screen and it shows no top or bottom edge.
            laid.Pose = Quaternion.LookRotation(view.position - laid.Base, view.up)
                * Quaternion.Euler(Plugin.LayTiltX.Value, Plugin.LayTiltY.Value, Plugin.LayTiltZ.Value);
            laid.Fitted = null;
        }

        private static void Apply(Laid laid)
        {
            float t = Mathf.SmoothStep(0f, 1f, laid.Progress);
            var normal = laid.Pose * Vector3.forward;
            float size = Plugin.LayScale.Value;
            foreach (var step in laid.Steps)
            {
                var bounds = step.Filter.sharedMesh.bounds;
                var underside = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z);
                var meshPosition = laid.Base - laid.Pose * Vector3.Scale(step.Scale * size, underside);
                var actorRotation = laid.Pose * Quaternion.Inverse(step.MeshRotation);
                var actorPosition = meshPosition - actorRotation * (step.MeshOffset * size);
                step.Actor.transform.SetPositionAndRotation(
                    Vector3.Lerp(step.ActorPosition, actorPosition, t),
                    Quaternion.Slerp(step.ActorRotation, actorRotation, t));
                step.Actor.transform.localScale = step.ActorScale * Mathf.Lerp(1f, size, t);
            }
            // After every actor: a bone may hang off any of them.
            foreach (var step in laid.Steps)
            {
                var top = laid.Base + normal * (step.Filter.sharedMesh.bounds.size.z * Mathf.Abs(step.Scale.z) * size);
                step.Bone.position = Vector3.Lerp(step.BonePosition, top, t);
            }
            var moved = (laid.Base - laid.Slot) * t;
            foreach (var follower in laid.Followers)
                if (follower.Transform != null) follower.Transform.position = follower.Position + moved;
            FitBack(laid);
        }

        private static void BuildBack(Laid laid)
        {
            if (!Plugin.AnimatedBack.Value) return;
            int id = laid.Deck.GetController().GetCardBackId();
            var loaded = CardBackManager.Get()?.LoadCardBackByIndex(id, unlit: true);
            var actor = loaded?.m_GameObject != null ? loaded.m_GameObject.GetComponent<Actor>() : null;
            if (actor == null)
            {
                Plugin.Log.LogWarning($"Card back {id} did not load; the laid deck keeps its flat picture");
                if (loaded?.m_GameObject != null) Object.Destroy(loaded.m_GameObject);
                return;
            }
            actor.gameObject.name = "BoardDeckCardBack";
            laid.Holder = new GameObject("BoardDeck");
            int layer = laid.Steps[0].Mesh.gameObject.layer;
            laid.Holder.layer = layer;
            if (!CardFit.Measure(actor, laid.Holder.transform, layer, out laid.Placement))
            {
                Object.Destroy(laid.Holder);
                laid.Holder = null;
                return;
            }
            laid.Back = actor.transform;
            // Clicks and hovers belong to the deck.
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) Object.Destroy(collider);
        }

        private static void FitBack(Laid laid)
        {
            if (laid.Holder == null) return;
            var active = laid.Deck.GetActiveThickness();
            bool show = active != null && !DeckInspector.IsOut(laid.Deck);
            if (laid.Holder.activeSelf != show) laid.Holder.SetActive(show);
            if (!show) return;

            var step = System.Array.Find(laid.Steps, s => s.Actor == active);
            var bounds = step.Filter.sharedMesh.bounds;
            var mesh = step.Mesh.transform;
            laid.Holder.transform.SetPositionAndRotation(
                mesh.TransformPoint(new Vector3(bounds.center.x, bounds.center.y, bounds.max.z)), mesh.rotation);
            // Fitted at full size, so while the deck settles the holder just follows its scale.
            laid.Holder.transform.localScale = Vector3.one * (mesh.lossyScale.y / step.Scale.y);
            if (laid.Fitted == step) return;
            laid.Fitted = step;
            CardFit.Place(laid.Back, laid.Placement, bounds.size.x * Mathf.Abs(step.Scale.x), bounds.size.y * Mathf.Abs(step.Scale.y),
                1f, Mathf.Clamp(Plugin.BackDepth.Value, 0.05f, 1f), BackLift);
        }

        private static void Release(ref Laid laid)
        {
            if (laid == null) return;
            foreach (var step in laid.Steps)
            {
                if (step.Actor == null) continue;
                step.Actor.transform.SetPositionAndRotation(step.ActorPosition, step.ActorRotation);
                step.Actor.transform.localScale = step.ActorScale;
            }
            foreach (var step in laid.Steps)
                if (step.Bone != null) step.Bone.SetPositionAndRotation(step.BonePosition, step.BoneRotation);
            foreach (var follower in laid.Followers)
                if (follower.Transform != null) follower.Transform.position = follower.Position;
            if (laid.Holder != null) Object.Destroy(laid.Holder);
            laid = null;
        }
    }

    /// <summary>
    /// A drawn card comes off a laid deck as off a real one. iTween reads a tween's start when its delay ends, so a card
    /// laid flat here still turns to its usual angle from there.
    ///
    /// The opponent's unseen draw goes straight from the deck to the hand: the game's path has it pass a bone between
    /// the two, which after a slide up is a zigzag. Drawn cards turn through TurnTo, the short way: iTween blends each
    /// Euler angle apart, and from a laid card that spins it. Your card also waits to turn: the game's timing shows it
    /// right beside the deck, as iTween's default easing does most of a turn at the start.
    /// </summary>
    [HarmonyPatch]
    internal static class DrawFollowsDeck
    {
        private const float FriendlyTurnTime = 1.35f; // what Card.DrawFriendlyCardWithTiming asks for at normal speed

        [HarmonyPatch(typeof(iTween), nameof(iTween.MoveTo), new[] { typeof(GameObject), typeof(Hashtable) })]
        [HarmonyPrefix]
        private static void Move(GameObject target, Hashtable args)
        {
            if (!BoardDeck.OnTop(target, out var off) || !(args["path"] is Vector3[] path)) return;
            if (path.Length == 3) path[1] = path[0] + off.Slide;
            else if (path.Length == 4) args["path"] = new[] { path[0], path[0] + off.Slide, path[3] };
        }

        [HarmonyPatch(typeof(iTween), nameof(iTween.RotateTo), new[] { typeof(GameObject), typeof(Hashtable) })]
        [HarmonyPrefix]
        private static bool Rotate(GameObject target, Hashtable args)
        {
            if (!BoardDeck.OnTop(target, out var off)) return true;
            target.transform.rotation = off.Rotation;
            target.transform.localScale = off.Scale;
            if (!(args["rotation"] is Vector3 rotation)) return true;
            float delay = System.Convert.ToSingle(args["delay"]);
            float time = System.Convert.ToSingle(args["time"]);
            if (!off.Opposing)
            {
                float speed = time / FriendlyTurnTime; // the game scales its draws for some cards
                delay = Plugin.DrawRevealDelay.Value * speed;
                time = Plugin.DrawRevealTime.Value * speed;
            }
            TurnTo.Begin(target, Quaternion.Euler(rotation), delay, time);
            return false;
        }
    }

    /// <summary>Turns a card the short way, as a rotation and not angle by angle.</summary>
    internal sealed class TurnTo : MonoBehaviour
    {
        private Quaternion _from, _to;
        private float _delay, _time, _elapsed;

        public static void Begin(GameObject card, Quaternion to, float delay, float time)
        {
            var turn = card.GetComponent<TurnTo>() ?? card.AddComponent<TurnTo>();
            turn._from = card.transform.rotation;
            turn._to = to;
            turn._delay = delay;
            turn._time = Mathf.Max(time, 0.01f);
            turn._elapsed = 0f;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01((_elapsed - _delay) / _time);
            transform.rotation = Quaternion.Slerp(_from, _to, Mathf.SmoothStep(0f, 1f, t));
            if (t >= 1f) Destroy(this);
        }
    }
}

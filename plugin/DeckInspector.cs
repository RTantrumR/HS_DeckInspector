using UnityEngine;

namespace HsDeckInspector
{
    /// <summary>
    /// Pulls the friendly deck off the board and lets the player spin it.
    ///
    /// The object is a copy built from the deck's live meshes and materials, so it carries the player's card back
    /// (custom deck meshes included). The board deck only stops drawing, through forceRenderingOff: gameplay finds
    /// the active thickness by reading renderer.enabled (ZoneDeck.GetActiveThickness), so that flag must never change.
    ///
    /// Root faces the camera (+Z towards it, +Y up) and carries the flight; Spin turns under it around Y; Body holds
    /// the mesh, the card's back towards +Z; the unknown card sits on the far side, facing -Z, and the player's full
    /// animated card back on the near side, facing +Z. The mesh's own back is the game's flat picture of it, and shows
    /// only if the animated one can't load.
    /// </summary>
    internal static class DeckInspector
    {
        private const float FlyTime = 0.35f;
        private const float CameraDistance = 10f;
        private const float SpinFriction = 3.5f;
        private const float FaceLift = 0.004f; // keeps the card clear of the deck's bottom surface

        private enum State { Closed, Opening, Open, Closing }

        private static State _state;
        private static ZoneDeck _deck;
        private static Actor[] _steps;
        private static Renderer[] _hidden;

        private static GameObject _root;
        private static Transform _spin;
        private static Transform _body;
        private static MeshFilter _bodyFilter;
        private static MeshRenderer _bodyRenderer;
        private static Transform _face;
        private static Actor _faceActor;
        private static CardPlacement _facePlacement;
        private static Transform _back;
        private static CardPlacement _backPlacement;

        /// <summary>A flat actor's measurements at identity: where it starts, and how tall it is.</summary>
        private struct CardPlacement
        {
            public Vector3 Anchor;
            public float Height;

            public CardPlacement(Bounds bounds)
            {
                Anchor = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                Height = bounds.size.z;
            }
        }

        private static Quaternion _bodyRotation;
        private static Vector3 _baseScale;
        private static float _thinnest, _thickest; // the one-card and full meshes' thickness
        private static float _halfThickness;
        private static int _shownCount = -1;
        private static float _progress;
        private static float _angle, _closeAngle, _velocity;
        private static bool _dragging;
        private static float _lastMouseX;

        public static void Update()
        {
            if (_state == State.Closed)
            {
                if (Input.GetKeyDown(Plugin.InspectKey.Value) && CanOpen()) Open();
                return;
            }
            if (!GameAlive())
            {
                Abort();
                return;
            }

            if (_state == State.Opening || _state == State.Open)
            {
                if (Input.GetKeyDown(Plugin.InspectKey.Value) || Input.GetKeyUp(KeyCode.Escape) || Input.GetMouseButtonDown(1))
                    Close();
                else
                    Spin();
            }

            float step = Time.unscaledDeltaTime / FlyTime;
            if (_state == State.Opening)
            {
                _progress = Mathf.Min(1f, _progress + step);
                if (_progress >= 1f) _state = State.Open;
            }
            else if (_state == State.Closing)
            {
                _progress = Mathf.Max(0f, _progress - step);
                if (_progress <= 0f)
                {
                    Abort();
                    return;
                }
            }

            int count = _deck.GetCards().Count;
            if (count != _shownCount) ShowCount(count);
            HideBoardDeck(); // the game may swap the active thickness mid-flight
            if (_faceActor != null) FaceCard.Hold(_faceActor);
            Pose();
        }

        /// <summary>Drops everything at once and gives the board its deck and the mouse back.</summary>
        public static void Abort()
        {
            if (_hidden != null)
            {
                foreach (var r in _hidden)
                    if (r != null) r.forceRenderingOff = false;
            }
            _hidden = null;
            if (_root != null) Object.Destroy(_root);
            _root = null;
            _deck = null;
            _steps = null;
            _state = State.Closed;
            InspectInputBlock.Set(false);
        }

        private static bool GameAlive()
        {
            var state = GameState.Get();
            return state != null && !state.IsGameOver() && _deck != null && Camera.main != null;
        }

        private static bool CanOpen()
        {
            var state = GameState.Get();
            if (state == null || state.IsGameOver() || Camera.main == null) return false;
            var mgr = GameMgr.Get();
            if (mgr == null || mgr.IsBattlegrounds() || mgr.IsMercenaries()) return false;
            if (UniversalInputManager.Get()?.IsTextInputActive() == true) return false;
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1)) return false;
            var input = InputManager.Get();
            if (input == null || input.GetHeldCard() != null || state.IsInTargetMode()) return false;
            var deck = state.GetFriendlySidePlayer()?.GetDeckZone();
            if (deck == null || deck.GetCards().Count == 0) return false;
            _deck = deck;
            return true;
        }

        private static void Open()
        {
            Plugin.ReloadConfig(); // look settings can be tuned between presses, with the game running
            _steps = new[] { _deck.m_Thickness1, _deck.m_Thickness25, _deck.m_Thickness50, _deck.m_Thickness75, _deck.m_ThicknessFull };
            _hidden = new Renderer[_steps.Length];
            for (int i = 0; i < _steps.Length; i++) _hidden[i] = _steps[i].GetMeshRenderer();

            var source = _deck.GetThicknessForLayout();
            var sourceRenderer = source.GetMeshRenderer();
            var sourceTransform = sourceRenderer.transform;
            var camera = Camera.main;

            // Which of the mesh's sides is up on the board, and which way the camera reads the card.
            Vector3 boardUp = sourceTransform.InverseTransformDirection(Vector3.up);
            Vector3 screenUp = sourceTransform.InverseTransformDirection(camera.transform.up);
            float backSign = boardUp.z >= 0f ? 1f : -1f;
            float heightSign = screenUp.y >= 0f ? 1f : -1f;
            _bodyRotation = Quaternion.Inverse(Quaternion.LookRotation(new Vector3(0f, 0f, backSign), new Vector3(0f, heightSign, 0f)));

            Vector3 lossy = sourceTransform.lossyScale;
            _baseScale = new Vector3(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));
            _thinnest = MeshOf(_steps[0]).bounds.size.z;
            _thickest = MeshOf(_steps[_steps.Length - 1]).bounds.size.z;

            _root = new GameObject("DeckInspector");
            _spin = new GameObject("Spin").transform;
            _spin.SetParent(_root.transform, false);
            _body = new GameObject("Body").transform;
            _body.SetParent(_spin, false);
            _body.localRotation = _bodyRotation;
            _bodyFilter = _body.gameObject.AddComponent<MeshFilter>();
            _bodyRenderer = _body.gameObject.AddComponent<MeshRenderer>();
            _bodyRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
            _root.layer = _body.gameObject.layer = sourceTransform.gameObject.layer;

            BuildFace(sourceTransform.gameObject.layer);
            BuildBack(sourceTransform.gameObject.layer);

            _shownCount = -1;
            _progress = 0f;
            _angle = _velocity = 0f;
            _dragging = false;
            _state = State.Opening;
            InspectInputBlock.Set(true);
            ShowCount(_deck.GetCards().Count);
            HideBoardDeck();
            Pose();

            Plugin.Log.LogInfo($"Deck out: {_deck.GetCards().Count} cards, mesh z {_thinnest:0.###}..{_thickest:0.###}, " +
                $"board up {boardUp}, screen up {screenUp}, lossy {lossy}, camera {camera.name} fov {camera.fieldOfView:0.#} " +
                $"near {camera.nearClipPlane:0.##} at {camera.transform.position}, layer {sourceTransform.gameObject.layer}");
        }

        private static void BuildFace(int layer)
        {
            _face = null;
            _faceActor = null;
            var actor = FaceCard.Build(Plugin.FaceStyle.Value);
            if (actor == null) return;
            if (!Measure(actor, layer, out _facePlacement)) return;
            _face = actor.transform;
            _faceActor = actor;
        }

        /// <summary>The player's card back as the collection shows it: its own mesh, material and effects.</summary>
        private static void BuildBack(int layer)
        {
            _back = null;
            if (!Plugin.AnimatedBack.Value) return;
            int id = GameState.Get().GetFriendlySidePlayer().GetCardBackId();
            var loaded = CardBackManager.Get()?.LoadCardBackByIndex(id, unlit: true);
            var actor = loaded?.m_GameObject != null ? loaded.m_GameObject.GetComponent<Actor>() : null;
            if (actor == null)
            {
                Plugin.Log.LogWarning($"Card back {id} did not load; the deck keeps its flat picture");
                if (loaded?.m_GameObject != null) Object.Destroy(loaded.m_GameObject);
                return;
            }
            actor.gameObject.name = "DeckInspectorCardBack";
            if (!Measure(actor, layer, out _backPlacement)) return;
            _back = actor.transform;
            Plugin.Log.LogInfo($"Card back {id} ({loaded.m_Name}): height {_backPlacement.Height:0.###}, anchor {_backPlacement.Anchor}");
        }

        /// <summary>Measures a flat actor at identity and hangs it under Spin; false, and gone, if it has no card mesh.</summary>
        private static bool Measure(Actor actor, int layer, out CardPlacement placement)
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
            go.transform.SetParent(_spin, false);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            return true;
        }

        /// <summary>
        /// Lays a card on one side of the deck, as tall as the deck, its top up and its art facing outward. Depth
        /// squashes the card towards its base: from the front it reads the same, from the side it hugs the deck, where
        /// a hand card's raised layers and a deep card back's model would otherwise stand well off it. Gap is the
        /// distance from the deck's surface, negative to sink into it.
        /// </summary>
        private static void Place(Transform card, CardPlacement placement, float deckHeight, float side, float depth, float gap)
        {
            float size = deckHeight / placement.Height;
            var scale = new Vector3(size, size * depth, size);
            var rotation = Quaternion.LookRotation(Vector3.up, new Vector3(0f, 0f, side));
            card.localScale = scale;
            card.localRotation = rotation;
            card.localPosition = new Vector3(0f, 0f, side) * (_halfThickness + gap) - rotation * Vector3.Scale(placement.Anchor, scale);
        }

        private static Mesh MeshOf(Actor step) => step.GetComponentInChildren<MeshFilter>().sharedMesh;

        /// <summary>
        /// Width grows with every card: the game only has five thickness meshes and stops at 26 cards, so the closest
        /// mesh is stretched to the exact thickness. A bigger deck than the full mesh keeps growing.
        /// </summary>
        private static void ShowCount(int count)
        {
            _shownCount = count;
            if (count <= 0)
            {
                Close();
                return;
            }
            float target = _thinnest + (_thickest - _thinnest) * (count - 1) / 29f;
            Mesh best = null;
            float bestGap = float.MaxValue;
            foreach (var step in _steps)
            {
                var mesh = MeshOf(step);
                float gap = Mathf.Abs(mesh.bounds.size.z - target);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = mesh;
                }
            }
            _bodyFilter.sharedMesh = best;
            Bounds bounds = best.bounds;
            Vector3 scale = _baseScale;
            scale.z *= target / bounds.size.z;
            _body.localScale = scale;
            _body.localPosition = -(_bodyRotation * Vector3.Scale(bounds.center, scale));

            float height = bounds.size.y * scale.y;
            _halfThickness = target * _baseScale.z / 2f;
            if (_face != null) Place(_face, _facePlacement, height, -1f, Mathf.Clamp(Plugin.FaceDepth.Value, 0.05f, 1f), FaceLift);
            if (_back != null) Place(_back, _backPlacement, height, 1f, Mathf.Clamp(Plugin.BackDepth.Value, 0.05f, 1f), -Plugin.BackSink.Value);
        }

        private static void Spin()
        {
            if (_state == State.Open && Input.GetMouseButtonDown(0))
            {
                _dragging = true;
                _velocity = 0f;
                _lastMouseX = Input.mousePosition.x;
            }
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            if (_dragging && Input.GetMouseButton(0))
            {
                float x = Input.mousePosition.x;
                float degrees = -(x - _lastMouseX) * Plugin.DragSpeed.Value * (1080f / Screen.height);
                _lastMouseX = x;
                _angle += degrees;
                _velocity = Mathf.Lerp(_velocity, degrees / dt, 0.35f);
                return;
            }
            _dragging = false;
            _angle += _velocity * dt;
            _velocity *= Mathf.Exp(-SpinFriction * dt);
        }

        private static void Close()
        {
            _state = State.Closing;
            _dragging = false;
            _velocity = 0f;
            _closeAngle = Mathf.DeltaAngle(0f, _angle); // the short way round
            InspectInputBlock.Set(false);
        }

        private static void HideBoardDeck()
        {
            foreach (var r in _hidden)
                if (r != null) r.forceRenderingOff = true;
        }

        /// <summary>Between the board deck's place (progress 0) and in front of the camera (1).</summary>
        private static void Pose()
        {
            var camera = Camera.main;
            float t = Mathf.SmoothStep(0f, 1f, _progress);

            var source = _deck.GetThicknessForLayout().GetMeshRenderer();
            Quaternion boardRotation = source.transform.rotation * Quaternion.Inverse(_bodyRotation);

            float distance = Mathf.Max(CameraDistance, camera.nearClipPlane * 3f);
            Vector3 viewPosition = camera.transform.position + camera.transform.forward * distance;
            Quaternion viewRotation = Quaternion.LookRotation(-camera.transform.forward, camera.transform.up);
            float visible = 2f * distance * (float)System.Math.Tan(camera.fieldOfView * 0.5 * System.Math.PI / 180.0);
            float fit = Plugin.DeckHeight.Value * visible / (_bodyFilter.sharedMesh.bounds.size.y * _baseScale.y);

            var root = _root.transform;
            root.position = Vector3.Lerp(source.bounds.center, viewPosition, t);
            root.rotation = Quaternion.Slerp(boardRotation, viewRotation, t);
            root.localScale = Vector3.one * Mathf.Lerp(1f, fit, t);

            float angle = _state == State.Closing ? _closeAngle * t : _angle;
            _spin.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }
}

using System;
using System.Collections.Generic;
using AbilityKit.World.ECS;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Game.EntityCreation;
using AbilityKit.Game.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AbilityKit.Game.Editor
{
    public sealed class EditorGameFlowPumpWindow : EditorWindow
    {
        private bool _running;
        private bool _paused;
        private float _timeScale = 1f;
        private double _lastTime;

        private bool _useSceneViewPreview = true;
        private bool _autoFrameSceneView = false;
        private double _lastFrameSceneViewTime;
        private float _frameSceneViewIntervalSeconds = 0.35f;

        private bool _enableKeyboardInput = true;
        private readonly HashSet<KeyCode> _keysDown = new HashSet<KeyCode>();
        private int _queuedSkillSlot;
        private double _lastKeyEventTime;

        private string _lastError;

        private Scene _sandboxScene;
        private bool _sandboxSceneCreated;
        private Scene _prevActiveScene;
        private bool _prevActiveSceneCaptured;

        private EntityWorld _world;
        private IEntity _root;
        private GameFlowDomain _flow;

        [MenuItem("Tools/AbilityKit/Preview/编辑器驱动(Flow Pump)")]
        private static void Open()
        {
            GetWindow<EditorGameFlowPumpWindow>("Flow Pump");
        }

        private void OnEnable()
        {
            _lastTime = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            StopInternal();
        }

        private void OnGUI()
        {
            CaptureWindowKeyboardInput();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginDisabledGroup(_running);
                if (GUILayout.Button("启动", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    StartInternal();
                }
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(!_running);
                if (GUILayout.Button("停止", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    StopInternal();
                }

                _paused = GUILayout.Toggle(_paused, "暂停", EditorStyles.toolbarButton, GUILayout.Width(60));

                if (GUILayout.Button("单步", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    StepInternal();
                }
                EditorGUI.EndDisabledGroup();

                GUILayout.FlexibleSpace();

                GUILayout.Label("倍速", GUILayout.Width(30));
                _timeScale = EditorGUILayout.Slider(_timeScale, 0f, 4f, GUILayout.Width(180));
            }

            EditorGUILayout.Space(6);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("输入", EditorStyles.boldLabel);
                _enableKeyboardInput = EditorGUILayout.ToggleLeft("启用键盘输入(WASD 移动 / JKL 技能)", _enableKeyboardInput);

                using (new EditorGUI.DisabledScope(!_enableKeyboardInput))
                {
                    EditorGUILayout.LabelField("最近输入", _lastKeyEventTime <= 0 ? "(无)" : _lastKeyEventTime.ToString("F3"));
                    EditorGUILayout.LabelField("按键状态", _keysDown.Count == 0 ? "(无)" : string.Join(",", _keysDown));
                }
            }

            EditorGUILayout.LabelField("状态", _running ? (_paused ? "运行中(暂停)" : "运行中") : "未启动");

            if (!string.IsNullOrEmpty(_lastError))
            {
                EditorGUILayout.HelpBox(_lastError, MessageType.Error);
            }

            if (_flow != null)
            {
                EditorGUILayout.LabelField("Phase", _flow.CurrentPhase.ToString());
            }

            if (_root.IsValid && _root.TryGetComponent(out BattleContext battle) && battle != null)
            {
                EditorGUILayout.LabelField("Battle Frame", battle.LastFrame.ToString());
                EditorGUILayout.LabelField("LogicTimeSeconds", battle.LogicTimeSeconds.ToString("F3"));
            }
            else
            {
                EditorGUILayout.LabelField("Battle", "(未进入/未创建)");
            }

            EditorGUILayout.HelpBox(
                "该窗口在非 PlayMode 下通过 EditorApplication.update 驱动 GameFlowDomain.Tick。\n" +
                "注意：这里仅驱动逻辑与事件；若要看到完整特效，请确保相关 view/vfx feature 在当前上下文可用并且不会污染场景。",
                MessageType.Info);
        }

        private void StartInternal()
        {
            if (_running) return;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _lastError = "当前处于 PlayMode 或正在切换 PlayMode，无法启动编辑器预览。";
                ShowNotification(new GUIContent("无法启动：PlayMode"));
                Repaint();
                return;
            }

            StopInternal();

            try
            {
                _lastError = null;

                _prevActiveScene = SceneManager.GetActiveScene();
                _prevActiveSceneCaptured = true;

                _sandboxScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                _sandboxSceneCreated = _sandboxScene.IsValid();
                if (_sandboxSceneCreated)
                {
                    SceneManager.SetActiveScene(_sandboxScene);
                }

                _world = new EntityWorld();
                _root = EntityGenerator.CreateRoot(_world, "EditorGameRoot");

                _flow = new GameFlowDomain(entry: null, rootOverride: _root);
                _root.WithRef(_flow);

                _flow.StartWithPersistentSettingsSync();
                _flow.EnterBattle(new TestBattleBootstrapper());

                if (_useSceneViewPreview)
                {
                    OpenSceneView();
                }

                _lastTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += OnEditorUpdate;
                SceneView.duringSceneGui += OnSceneViewGUI;
                _running = true;
                _paused = false;

                Repaint();
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[EditorGameFlowPumpWindow] Start failed");
                Debug.LogException(ex);
                _lastError = ex.ToString();
                ShowNotification(new GUIContent("启动失败(见窗口/Console)"));
                StopInternal();
            }
        }

        private void StopInternal()
        {
            if (_running)
            {
                EditorApplication.update -= OnEditorUpdate;
                SceneView.duringSceneGui -= OnSceneViewGUI;
            }

            _running = false;
            _paused = false;

            RemoveNotification();

            _keysDown.Clear();
            _queuedSkillSlot = 0;

            try
            {
                if (_root.IsValid)
                {
                    _root.World.DestroyRecursive(_root.Id);
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[EditorGameFlowPumpWindow] Destroy preview entities failed");
            }

            if (_sandboxSceneCreated)
            {
                try
                {
                    if (_prevActiveSceneCaptured && _prevActiveScene.IsValid())
                    {
                        SceneManager.SetActiveScene(_prevActiveScene);
                    }

                    if (_sandboxScene.IsValid())
                    {
                        var roots = _sandboxScene.GetRootGameObjects();
                        for (var i = 0; i < roots.Length; i++)
                        {
                            if (roots[i] != null)
                            {
                                DestroyImmediate(roots[i]);
                            }
                        }

                        EditorSceneManager.CloseScene(_sandboxScene, removeScene: true);
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "[EditorGameFlowPumpWindow] Close sandbox scene failed");
                }
            }

            _sandboxScene = default;
            _sandboxSceneCreated = false;

            _prevActiveScene = default;
            _prevActiveSceneCaptured = false;

            _flow = null;
            _root = default;
            _world = null;

            Repaint();
        }

        private void OnSceneViewGUI(SceneView sv)
        {
            if (!_running) return;
            if (!_enableKeyboardInput) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var e = Event.current;
            if (e == null) return;

            if (e.type == EventType.KeyDown)
            {
                if (TryHandleKeyEvent(e.keyCode, isDown: true))
                {
                    e.Use();
                }
            }
            else if (e.type == EventType.KeyUp)
            {
                if (TryHandleKeyEvent(e.keyCode, isDown: false))
                {
                    e.Use();
                }
            }
        }

        private void CaptureWindowKeyboardInput()
        {
            if (!_running) return;
            if (!_enableKeyboardInput) return;

            var e = Event.current;
            if (e == null) return;

            if (e.type == EventType.KeyDown)
            {
                if (TryHandleKeyEvent(e.keyCode, isDown: true))
                {
                    e.Use();
                }
            }
            else if (e.type == EventType.KeyUp)
            {
                if (TryHandleKeyEvent(e.keyCode, isDown: false))
                {
                    e.Use();
                }
            }
        }

        private bool TryHandleKeyEvent(KeyCode keyCode, bool isDown)
        {
            if (keyCode == KeyCode.None) return false;

            var isRelevant =
                keyCode == KeyCode.W || keyCode == KeyCode.A || keyCode == KeyCode.S || keyCode == KeyCode.D ||
                keyCode == KeyCode.J || keyCode == KeyCode.K || keyCode == KeyCode.L;

            if (!isRelevant) return false;

            _lastKeyEventTime = EditorApplication.timeSinceStartup;

            if (isDown)
            {
                _keysDown.Add(keyCode);

                if (keyCode == KeyCode.J) _queuedSkillSlot = 1;
                else if (keyCode == KeyCode.K) _queuedSkillSlot = 2;
                else if (keyCode == KeyCode.L) _queuedSkillSlot = 3;

                return true;
            }

            _keysDown.Remove(keyCode);
            return true;
        }

        private void ApplyEditorKeyboardInput()
        {
            if (!_enableKeyboardInput) return;
            if (!_root.IsValid) return;
            if (!_root.TryGetComponent(out BattleContext ctx) || ctx == null) return;

            float dx = 0f;
            float dz = 0f;

            var hasEditorKeyState = _keysDown.Count > 0;
            if (hasEditorKeyState)
            {
                if (_keysDown.Contains(KeyCode.A)) dx -= 1f;
                if (_keysDown.Contains(KeyCode.D)) dx += 1f;
                if (_keysDown.Contains(KeyCode.W)) dz += 1f;
                if (_keysDown.Contains(KeyCode.S)) dz -= 1f;
            }
           

            var hasMove = Math.Abs(dx) > 0.0001f || Math.Abs(dz) > 0.0001f;
            ctx.HudHasMove = hasMove;
            ctx.HudMoveDx = dx;
            ctx.HudMoveDz = dz;

            var slot = _queuedSkillSlot;
            if (slot > 0)
            {
                ctx.HudSkillClickSlot = slot;
                _queuedSkillSlot = 0;
            }
        }

        private void StepInternal()
        {
            if (!_running) return;
            if (_flow == null) return;

            _flow.Tick(1f / 60f);
            Repaint();
        }

        private void OnEditorUpdate()
        {
            if (!_running) return;
            if (_paused) return;
            if (_flow == null) return;

            ApplyEditorKeyboardInput();

            var now = EditorApplication.timeSinceStartup;
            var delta = Math.Max(0.0, now - _lastTime);
            _lastTime = now;

            var scaled = (float)(delta * Math.Max(0f, _timeScale));

            _flow.Tick(scaled);

            if (_useSceneViewPreview && _autoFrameSceneView)
            {
                TryFrameSceneView(force: false);
            }

            Repaint();
        }

        private static SceneView OpenSceneView()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null)
            {
                sv = GetWindow<SceneView>();
            }

            sv.Show();
            return sv;
        }

        private static SceneView OpenAndFocusSceneView()
        {
            return OpenSceneView();
        }

        private void TryFrameSceneView(bool force)
        {
            if (!_sandboxSceneCreated) return;
            if (!_sandboxScene.IsValid()) return;

            var now = EditorApplication.timeSinceStartup;
            if (!force && now - _lastFrameSceneViewTime < Math.Max(0.05, _frameSceneViewIntervalSeconds))
            {
                return;
            }

            _lastFrameSceneViewTime = now;

            var bounds = CalculateSandboxSceneBounds(_sandboxScene);
            if (bounds.size.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var sv = SceneView.lastActiveSceneView;
            if (sv == null)
            {
                return;
            }

            sv.Frame(bounds, instant: false);
            sv.Repaint();
        }

        private static Bounds CalculateSandboxSceneBounds(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var hasBounds = false;
            var bounds = default(Bounds);

            for (var i = 0; i < roots.Length; i++)
            {
                var go = roots[i];
                if (go == null) continue;

                var renderers = go.GetComponentsInChildren<Renderer>(includeInactive: true);
                for (var j = 0; j < renderers.Length; j++)
                {
                    var r = renderers[j];
                    if (r == null) continue;
                    if (!hasBounds)
                    {
                        bounds = r.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(r.bounds);
                    }
                }
            }

            if (!hasBounds)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            return bounds;
        }
    }
}

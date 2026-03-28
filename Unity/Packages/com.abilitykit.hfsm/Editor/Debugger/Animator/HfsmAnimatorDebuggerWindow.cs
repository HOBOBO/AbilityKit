#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityHFSM.Visualization;

namespace UnityHFSM.Editor.Debugger
{
	/// <summary>
	/// HFSM Animator 调试器窗口
	/// 将运行中的 HFSM 状态机绑定到 Unity Animator 窗口进行可视化调试
	/// </summary>
	public sealed class HfsmAnimatorDebuggerWindow : EditorWindow
	{
		[MenuItem("Window/AbilityKit/HFSM Animator Debugger")]
		static void Open()
		{
			GetWindow<HfsmAnimatorDebuggerWindow>(utility: false, title: "HFSM Animator Debugger");
		}

		GameObject _previewGo;
		Animator _previewAnimator;
		AnimatorController _controller;
		HfsmAnimatorGraph.IPreviewer _previewer;

		int _selectedIndex = -1;
		string _outputFolderPath = "Assets/DebugAnimators";
		string _animatorName = "HfsmLivePreview.controller";

		double _lastTickTime;
		float _tickInterval = 0.1f;

		void OnEnable()
		{
			EditorApplication.update += OnEditorUpdate;
			HfsmLiveRegistry.Changed += Repaint;
		}

		void OnDisable()
		{
			EditorApplication.update -= OnEditorUpdate;
			HfsmLiveRegistry.Changed -= Repaint;
			CleanupPreviewObjects();
		}

		void OnGUI()
		{
			DrawHeader();

			using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
			{
				EditorGUILayout.Space(6);

				DrawRegistrySelection();
				EditorGUILayout.Space(8);

				DrawSettings();
				EditorGUILayout.Space(8);

				DrawActions();
				EditorGUILayout.Space(8);

				DrawStatus();
			}

			if (!EditorApplication.isPlaying)
			{
				EditorGUILayout.HelpBox("Enter Play Mode to use the HFSM Animator Debugger.", MessageType.Info);
			}
		}

		void DrawHeader()
		{
			EditorGUILayout.LabelField("HFSM Animator Debugger", EditorStyles.boldLabel);
			EditorGUILayout.LabelField("Bind running HFSM to Unity Animator window", EditorStyles.miniLabel);
		}

		void DrawRegistrySelection()
		{
			var entries = HfsmLiveRegistry.GetEntries();
			var names = BuildEntryNames(entries);

			if (names.Length == 0)
			{
				EditorGUILayout.HelpBox("No registered state machines. Call LiveRegistry.Register(name, fsm) from your runtime code.", MessageType.Warning);
				_selectedIndex = -1;
				return;
			}

			_selectedIndex = Mathf.Clamp(_selectedIndex, 0, names.Length - 1);
			_selectedIndex = EditorGUILayout.Popup("Running HFSM", _selectedIndex, names);
		}

		static string[] BuildEntryNames(IReadOnlyList<HfsmLiveRegistry.Entry> entries)
		{
			var list = new List<string>(entries.Count);
			for (var i = 0; i < entries.Count; i++)
			{
				var e = entries[i];
				var typeName = e.FsmType != null ? e.FsmType.Name : "<null>";
				list.Add($"{i}: {e.Name} ({typeName})");
			}
			return list.ToArray();
		}

		void DrawSettings()
		{
			EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
			_outputFolderPath = EditorGUILayout.TextField("Output Folder", _outputFolderPath);
			_animatorName = EditorGUILayout.TextField("Animator Name", _animatorName);
			_tickInterval = EditorGUILayout.Slider("Preview Tick (s)", _tickInterval, 0.02f, 1.0f);
		}

		void DrawActions()
		{
			EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Bind / Refresh"))
				{
					BindSelected();
				}

				if (GUILayout.Button("Open Animator Window"))
				{
					OpenAnimatorWindow();
				}
			}
		}

		void DrawStatus()
		{
			EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
			EditorGUILayout.LabelField("Controller", _controller != null ? _controller.name : "<none>");
			EditorGUILayout.LabelField("Preview Animator", _previewAnimator != null ? _previewAnimator.name : "<none>");
			EditorGUILayout.LabelField("Previewer", _previewer != null ? _previewer.GetType().Name : "<none>");
		}

		void BindSelected()
		{
			var entries = HfsmLiveRegistry.GetEntries();
			if (_selectedIndex < 0 || _selectedIndex >= entries.Count)
				return;

			var target = entries[_selectedIndex].Fsm.Target;
			if (target == null)
				return;

			EnsurePreviewObjects();

			var fsmType = target.GetType();
			var method = typeof(HfsmAnimatorGraph)
				.GetMethod("CreateAnimatorFromStateMachine", BindingFlags.Public | BindingFlags.Static);

			if (method == null)
			{
				Debug.LogError("Cannot find HfsmAnimatorGraph.CreateAnimatorFromStateMachine method.");
				return;
			}

			if (!method.IsGenericMethodDefinition)
			{
				Debug.LogError("HfsmAnimatorGraph.CreateAnimatorFromStateMachine is not a generic method definition.");
				return;
			}

			var genericArgs = fsmType.GetGenericArguments();
			if (genericArgs == null || genericArgs.Length != 3)
			{
				Debug.LogError($"Selected HFSM type {fsmType.FullName} does not have 3 generic arguments.");
				return;
			}

			var closed = method.MakeGenericMethod(genericArgs[0], genericArgs[1], genericArgs[2]);
			object result = closed.Invoke(null, new object[] { target, _outputFolderPath, _animatorName });

			// ValueTuple<AnimatorController, IPreviewer>
			_controller = (AnimatorController)result.GetType().GetField("Item1").GetValue(result);
			_previewer = (HfsmAnimatorGraph.IPreviewer)result.GetType().GetField("Item2").GetValue(result);

			_previewAnimator.runtimeAnimatorController = _controller;

			Selection.activeObject = _previewGo;
			OpenAnimatorWindow();
		}

		void OpenAnimatorWindow()
		{
			EditorApplication.ExecuteMenuItem("Window/Animation/Animator");
			Selection.activeObject = _previewGo;
		}

		void EnsurePreviewObjects()
		{
			if (_previewGo != null && _previewAnimator != null)
				return;

			_previewGo = new GameObject("[HFSM Animator Preview]");
			_previewGo.hideFlags = HideFlags.HideAndDontSave;

			_previewAnimator = _previewGo.AddComponent<Animator>();
			_previewAnimator.hideFlags = HideFlags.HideAndDontSave;
			_previewAnimator.enabled = true;
		}

		void CleanupPreviewObjects()
		{
			if (_previewGo != null)
			{
				try
				{
					DestroyImmediate(_previewGo);
				}
				catch
				{
					// ignored
				}
			}

			_previewGo = null;
			_previewAnimator = null;
			_controller = null;
			_previewer = null;
		}

		void OnEditorUpdate()
		{
			if (!EditorApplication.isPlaying)
				return;

			if (_previewer == null || _previewAnimator == null)
				return;

			var now = EditorApplication.timeSinceStartup;
			if (now - _lastTickTime < _tickInterval)
				return;

			_lastTickTime = now;
			_previewer.PreviewStateMachineInAnimator(_previewAnimator);
		}
	}
}

#endif

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mfuscator.CSharp {

	public sealed class Config : EditorWindow {

		public static List<string> assembliesToObfuscate;
		public static bool rename;
		public static bool disableSplashScreen;

		[MenuItem("Window/" + nameof(Mfuscator) + " Settings (WIP!!!)")]
		private static void MenuItemShow() {
			_ = GetWindow<Config>(utility: false, title: nameof(Mfuscator) + " Configuration");
		}

		private static string GetPlayerPrefsKey(string subKey) {
			return string.Concat(nameof(Mfuscator), '_', subKey);
		}

		public static void Load() {
			string pPKey;
			MemoryStream stream;
			BinaryReader reader;

			// assemblies to obfuscate
			assembliesToObfuscate = new();
			pPKey = GetPlayerPrefsKey(nameof(assembliesToObfuscate));
			if (PlayerPrefs.HasKey(pPKey)) {
				stream = new(System.Convert.FromBase64String(PlayerPrefs.GetString(pPKey)));
				reader = new(stream);
				reader.ReadStringList(assembliesToObfuscate);
				reader.Dispose();
				stream.Dispose();
			} else {
				// default
				assembliesToObfuscate.Add("Assembly-CSharp");
			}

			// rename
			pPKey = GetPlayerPrefsKey(nameof(rename));
			if (PlayerPrefs.HasKey(pPKey)) {
				rename = PlayerPrefs.GetInt(pPKey) == 1;
			}

			// disable splash screen
			pPKey = GetPlayerPrefsKey(nameof(disableSplashScreen));
			if (PlayerPrefs.HasKey(pPKey)) {
				disableSplashScreen = PlayerPrefs.GetInt(pPKey) == 1;
			}
		}

		private static void Save() {
			string pPKey;
			MemoryStream stream;
			BinaryWriter writer;

			// assemblies to obfuscate
			pPKey = GetPlayerPrefsKey(nameof(assembliesToObfuscate));
			stream = new();
			writer = new(stream);
			writer.WriteStringList(assembliesToObfuscate);
			PlayerPrefs.SetString(pPKey, System.Convert.ToBase64String(stream.ToArray()));
			writer.Dispose();
			stream.Dispose();

			// rename
			PlayerPrefs.SetInt(GetPlayerPrefsKey(nameof(rename)), rename ? 1 : 0);

			// disable splash screen
			PlayerPrefs.SetInt(GetPlayerPrefsKey(nameof(disableSplashScreen)), disableSplashScreen ? 1 : 0);
		}

		private void OnEnable() {
			Load();

			minSize = new(512f, 384f);
		}

		private void OnFocus() {
			Load();
		}

		private void OnLostFocus() {
			Save();
		}

		private void OnDestroy() {
			Save();
		}

		private static readonly GUILayoutOption _maxHeight96 = GUILayout.MaxHeight(96f);
		private static readonly GUILayoutOption _minWidth128 = GUILayout.MinWidth(128f);
		private static readonly GUILayoutOption _minWidth64 = GUILayout.MinWidth(64f);
		private static readonly GUILayoutOption _minWidth256 = GUILayout.MinWidth(256f);

		private static bool DrawToggle(string title, bool value, bool flexSpace, params GUILayoutOption[] options) {
			GUILayout.BeginHorizontal();
			GUILayout.Label(title);
			GUILayout.Space(2f);
			value = GUILayout.Toggle(value, string.Empty, options);
			if (flexSpace) {
				GUILayout.FlexibleSpace();
			}
			GUILayout.EndHorizontal();
			return value;
		}

		private static string DrawInputField(string title, string value, bool flexSpace, params GUILayoutOption[] options) {
			GUILayout.BeginHorizontal();
			GUILayout.Label(title);
			GUILayout.Space(2f);
			value = GUILayout.TextField(value, options);
			if (flexSpace) {
				GUILayout.FlexibleSpace();
			}
			GUILayout.EndHorizontal();
			return value;
		}

		private static void DrawElementsEditor(string title, string iFTitle, List<string> list, ref Vector2 scrollPos, ref string iFContent, ref int selIndex, System.Func<string, bool> validate) {
			GUILayout.Label(title, EditorStyles.boldLabel);
			GUILayout.Space(4f);
			scrollPos = GUILayout.BeginScrollView(scrollPos, EditorStyles.helpBox, _maxHeight96);
			for (int i = 0; i < list.Count; i++) {
				GUILayout.BeginHorizontal();
				// content
				if (GUILayout.Button(list[i], EditorStyles.selectionRect)) {
					selIndex = i;
				}
				// selection indicator
				if (selIndex == i) {
					GUILayout.Space(8f);
					GUILayout.Label("<-", EditorStyles.boldLabel);
				}
				GUILayout.EndHorizontal();
				// space
				if (i != list.Count - 1) {
					GUILayout.Space(4f);
				}
			}
			GUILayout.EndScrollView();
			GUILayout.Space(4f);
			GUILayout.BeginHorizontal();
			iFContent = DrawInputField(iFTitle, iFContent, false, _minWidth128);
			GUILayout.Space(8f);
			if (GUILayout.Button("Add", _minWidth64) && !list.Contains(iFContent) && validate.Invoke(iFContent)) {
				list.Add(iFContent);
				iFContent = string.Empty;
			}
			if (GUILayout.Button("Remove", _minWidth64) && selIndex < list.Count) {
				list.RemoveAt(selIndex);
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}

		private static Vector2 _assembliesToObfuscateScrollPos;
		private static string _assembliesToObfuscateIFContent;
		private static int _assembliesToObfuscateSelIndex;

		private void OnGUI() {
			if (EditorApplication.isCompiling) {
				Close();
				return;
			}

			GUILayout.Space(8f);
			GUILayout.Label("Properties", EditorStyles.boldLabel);
			GUILayout.Space(4f);
			rename = DrawToggle("Rename", rename, true);
			disableSplashScreen = DrawToggle("Disable Splash Screen", disableSplashScreen, true);

			GUILayout.Space(16f);
			DrawElementsEditor("Assemblies To Obfuscate", "Name (Without \".dll\")", assembliesToObfuscate, ref _assembliesToObfuscateScrollPos, ref _assembliesToObfuscateIFContent, ref _assembliesToObfuscateSelIndex,
				value => !string.IsNullOrWhiteSpace(value) && !value.EndsWith(".dll"));
		}
	}
}

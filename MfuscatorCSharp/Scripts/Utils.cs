using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEditor;

namespace Mfuscator.CSharp {

	public static class Utils {

		// source: https://docs.unity3d.com/2023.2/Documentation/ScriptReference/MonoBehaviour.html (April 26, 2023)
		public static readonly HashSet<string> defaultUnityMethods = new() {
			"CancelInvoke",
			"Invoke",
			"InvokeRepeating",
			"IsInvoking",
			"StartCoroutine",
			"StopAllCoroutines",
			"StopCoroutine",
			"Awake",
			"FixedUpdate",
			"LateUpdate",
			"OnAnimatorIK",
			"OnAnimatorMove",
			"OnApplicationFocus",
			"OnApplicationPause",
			"OnApplicationQuit",
			"OnAudioFilterRead",
			"OnBecameInvisible",
			"OnBecameVisible",
			"OnCollisionEnter",
			"OnCollisionEnter2D",
			"OnCollisionExit",
			"OnCollisionExit2D",
			"OnCollisionStay",
			"OnCollisionStay2D",
			"OnConnectedToServer",
			"OnControllerColliderHit",
			"OnDestroy",
			"OnDisable",
			"OnDisconnectedFromServer",
			"OnDrawGizmos",
			"OnDrawGizmosSelected",
			"OnEnable",
			"OnFailedToConnect",
			"OnFailedToConnectToMasterServer",
			"OnGUI",
			"OnJointBreak",
			"OnJointBreak2D",
			"OnMasterServerEvent",
			"OnMouseDown",
			"OnMouseDrag",
			"OnMouseEnter",
			"OnMouseExit",
			"OnMouseOver",
			"OnMouseUp",
			"OnMouseUpAsButton",
			"OnNetworkInstantiate",
			"OnParticleCollision",
			"OnParticleSystemStopped",
			"OnParticleTrigger",
			"OnParticleUpdateJobScheduled",
			"OnPlayerConnected",
			"OnPlayerDisconnected",
			"OnPostRender",
			"OnPreCull",
			"OnPreRender",
			"OnRenderImage",
			"OnRenderObject",
			"OnSerializeNetworkView",
			"OnServerInitialized",
			"OnTransformChildrenChanged",
			"OnTransformParentChanged",
			"OnTriggerEnter",
			"OnTriggerEnter2D",
			"OnTriggerExit",
			"OnTriggerExit2D",
			"OnTriggerStay",
			"OnTriggerStay2D",
			"OnValidate",
			"OnWillRenderObject",
			"Reset",
			"Start",
			"Update",
			"BroadcastMessage",
			"CompareTag",
			"GetComponent",
			"GetComponentInChildren",
			"GetComponentInParent",
			"GetComponents",
			"GetComponentsInChildren",
			"GetComponentsInParent",
			"SendMessage",
			"SendMessageUpwards",
			"TryGetComponent",
			"GetInstanceID",
			"ToString",
			"Destroy",
			"DestroyImmediate",
			"DontDestroyOnLoad",
			"FindAnyObjectByType",
			"FindFirstObjectByType",
			"FindObjectsByType",
			"Instantiate"
		};

		/// <summary>Uses StringComparison.InvariantCultureIgnoreCase</summary>
		public static bool Contains(IEnumerable<string> enumerable, string value) {
			foreach (string e in enumerable) {
				if (string.Equals(value, e, StringComparison.InvariantCultureIgnoreCase)) {
					return true;
				}
			}
			return false;
		}

		/// <returns>Namespace.Type</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string GetCustomFullName(this TypeReference type) {
			return string.Concat(type.Namespace, '.', type.Name);
		}

		/// <returns>Namespace.Type.Name</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string GetCustomFullName(this MemberReference member) {
			return string.Concat(member.DeclaringType.GetCustomFullName(), '.', member.Name);
		}

		public static void GetOperandsFromAllMethods(TypeDefinition[] allTypes, Action<object> callback) {
			for (int i = 0; i < allTypes.Length; i++) {
				TypeDefinition t = allTypes[i];
				for (int j = 0; j < t.Methods.Count; j++) {
					MethodDefinition m = t.Methods[j];
					if (!m.HasBody) {
						continue;
					}
					for (int k = 0; k < m.Body.Instructions.Count; k++) {
						Instruction instr = m.Body.Instructions[k];
						if (instr.Operand is not null) {
							callback.Invoke(instr.Operand);
						}
					}
				}
			}
		}

		public static bool CompareParameters(MethodReference a, MethodReference b) {
			if (a.Parameters.Count != b.Parameters.Count) {
				return false;
			}
			for (int i = 0; i < a.Parameters.Count; i++) {
				if (a.Parameters[i].ParameterType.GetCustomFullName() != b.Parameters[i].ParameterType.GetCustomFullName()) {
					return false;
				}
			}
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsValidName(string value, string sourceStr) {
			foreach (char c in value) {
				if (!sourceStr.Contains(c)) {
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Example: MonoBehaviour, ScriptableObject...
		/// </summary>
		public static bool CheckIfInheritsUnityType(TypeDefinition type, TypeDefinition[] allTypes) {
			if (type is null || type.BaseType is null) {
				return false;
			}
			if (type.BaseType.Namespace.Contains("Unity")) {
				return true;
			}
			return CheckIfInheritsUnityType(allTypes.Where(e => e.GetCustomFullName() == type.BaseType.GetCustomFullName()).FirstOrDefault(), allTypes);
		}

		#region Log
		public const string LOG_TAG = nameof(Mfuscator);

		private static string GetTaggedText(string text) {
			return string.Concat('[', LOG_TAG, "] ", text);
		}

		public static void Log(string text) {
			UnityEngine.Debug.Log(GetTaggedText(text));
		}

		public static void LogError(string text) {
			UnityEngine.Debug.LogError(GetTaggedText(text));
		}
		#endregion

		public static void WriteStringList(this BinaryWriter writer, List<string> value) {
			writer.Write(value.Count);
			for (int i = 0; i < value.Count; i++) {
				byte[] strBytes = Encoding.UTF8.GetBytes(value[i]);
				writer.Write(strBytes.Length);
				writer.Write(strBytes);
			}
		}

		public static void ReadStringList(this BinaryReader reader, List<string> value) {
			int count = reader.ReadInt32();
			for (int i = 0; i < count; i++) {
				int size = reader.ReadInt32();
				value.Add(Encoding.UTF8.GetString(reader.ReadBytes(size)));
			}
		}
	}
}

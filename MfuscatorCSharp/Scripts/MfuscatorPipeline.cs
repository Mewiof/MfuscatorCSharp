using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Mono.Cecil;

namespace Mfuscator.CSharp {

	public sealed class MfuscatorPipeline : IPreprocessBuildWithReport, IPostBuildPlayerScriptDLLs, IPostprocessBuildWithReport {

		private static readonly Dictionary<string, FileStream> _streams = new();
		private static readonly Dictionary<string, AssemblyDefinition> _managedAssemblies = new();

		// IPostBuildPlayerScriptDLLs
		public int callbackOrder => int.MaxValue;

		private sealed class AssemblyResolver : IAssemblyResolver {

			public static readonly Dictionary<byte[], AssemblyDefinition> cache = new();

			public AssemblyDefinition Resolve(AssemblyNameReference name) {
				// NOTE: sowwry
				if (!cache.TryGetValue(name.PublicKeyToken, out AssemblyDefinition result)) {
					result = _managedAssemblies.Values.FirstOrDefault(e => e.Name.PublicKeyToken == name.PublicKeyToken);
				}
				return result;
			}

			public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters) {
				return Resolve(name);
			}

			public void Dispose() { }
		}

		private static void Obfuscate(AssemblyDefinition assembly, TypeDefinition[] allTypes) {
			ModuleDefinition module = assembly.MainModule;

			if (Config.rename) {
				Renamer.Execute(module, allTypes);
			}
		}

		// IPostBuildPlayerScriptDLLs
		public void OnPostBuildPlayerScriptDLLs(BuildReport report) {
			EditorApplication.LockReloadAssemblies();

			// read
			foreach (BuildFile file in report.GetFiles()) {
				// ignore?
				if (file.role != "ManagedLibrary") {
					continue;
				}

				AssemblyResolver resolver = new();

				try {
					FileStream newStream = new(file.path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
					_streams[file.path] = newStream;
					_managedAssemblies[file.path] = AssemblyDefinition.ReadAssembly(newStream, new() {
						ReadingMode = ReadingMode.Immediate,
						InMemory = true,
						AssemblyResolver = resolver
					});
				} catch (Exception e) {
					Utils.LogError("Failed to read from \"" + file.path + "\"\n" + e.ToString());
				} finally {
					resolver.Dispose();
				}
			}

			// get all types & obfuscate
			TypeDefinition[] allTypes = _managedAssemblies.Values.SelectMany(e => e.Modules.SelectMany(e => e.GetTypes())).ToArray();
			foreach (var pair in _managedAssemblies) {
				// ignore?
				if (!Utils.Contains(Config.assembliesToObfuscate, Path.GetFileNameWithoutExtension(pair.Key))) {
					continue;
				}
				Obfuscate(pair.Value, allTypes);
			}

			// write
			foreach (var pair in _managedAssemblies) {
				Utils.Log("Writing to \"" + pair.Key + "\"...");
				try {
					FileStream stream = _streams[pair.Key];
					stream.Position = 0;
					pair.Value.Write(stream);
					stream.SetLength(stream.Position);
				} catch (Exception e) {
					Utils.LogError("Failed to write to \"" + pair.Key + "\"\n" + e.ToString());
				}
			}

			// dispose & clear
			foreach (FileStream stream in _streams.Values) {
				stream.Dispose();
			}
			_streams.Clear();
			foreach (AssemblyDefinition assembly in _managedAssemblies.Values) {
				assembly.Dispose();
			}
			_managedAssemblies.Clear();
			AssemblyResolver.cache.Clear();

			EditorApplication.UnlockReloadAssemblies();
		}

		public void OnPreprocessBuild(BuildReport report) {
			// load config in memory
			Config.Load();
		}

		public void OnPostprocessBuild(BuildReport report) {
			foreach (BuildFile file in report.GetFiles()) {
				// splash screen
				if (file.path.EndsWith("globalgamemanagers") && Config.disableSplashScreen) {
					Utils.Log("Disabling the splash screen...");
					byte[] fileBytes = File.ReadAllBytes(file.path);
					if (SplashScreenRemover.DisableIn(fileBytes)) {
						File.WriteAllBytes(file.path, fileBytes);
					}
				}
			}

			// reset
			NameGenerator.ResetUsedNames();
		}
	}
}

using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mfuscator.CSharp {

	public static class Renamer {

		private static bool CheckForRuntimeInitAttribute(MethodDefinition method) {
			foreach (CustomAttribute attribute in method.CustomAttributes) {
				if (attribute.AttributeType.Name.Contains("Init")) {
					return true;
				}
			}
			return false;
		}

		private static bool CheckForRuntimeInitMethods(TypeDefinition type) {
			foreach (MethodDefinition method in type.Methods) {
				if (CheckForRuntimeInitAttribute(method)) {
					return true;
				}
			}
			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void UpdateTypeName(TypeReference type, string oldCustomFullName, TypeDefinition targetType) {
			if (type.GetCustomFullName() == oldCustomFullName) {
				type.Name = targetType.Name;
			}
		}

		private static void UpdateMethod(MethodDefinition method, string oldCustomFullName, TypeDefinition targetType) {
			// params
			foreach (ParameterDefinition parameter in method.Parameters) {
				UpdateTypeName(parameter.ParameterType, oldCustomFullName, targetType);
			}
			// return
			UpdateTypeName(method.ReturnType, oldCustomFullName, targetType);
			// body
			if (method.HasBody) {
				foreach (Instruction instr in method.Body.Instructions) {
					if (instr.Operand is MemberReference opMember && opMember.DeclaringType is not null) {
						UpdateTypeName(opMember.DeclaringType, oldCustomFullName, targetType);
					}
				}
			}
		}

		private static void DrillNestedTypes(TypeDefinition targetType, string oldCustomFullName) {
			foreach (TypeDefinition nestedType in targetType.NestedTypes) {
				UpdateTypeName(nestedType.DeclaringType, oldCustomFullName, targetType);

				foreach (TypeDefinition nestedType1 in nestedType.NestedTypes) {
					UpdateTypeName(nestedType1.DeclaringType.DeclaringType, oldCustomFullName, targetType);

					foreach (TypeDefinition nestedType2 in nestedType1.NestedTypes) {
						UpdateTypeName(nestedType2.DeclaringType.DeclaringType.DeclaringType, oldCustomFullName, targetType);
					}
				}
			}
		}

		private static void UpdateType(TypeDefinition targetType, string oldCustomFullName, TypeDefinition[] allTypes) {
			foreach (TypeDefinition type in allTypes) {
				UpdateTypeName(type, oldCustomFullName, targetType);
				foreach (TypeReference typeRef in type.Module.GetTypeReferences()) {
					UpdateTypeName(typeRef, oldCustomFullName, targetType);
				}

				// NOTE: not sure about this one
				if (type.BaseType is not null) {
					UpdateTypeName(type.BaseType, oldCustomFullName, targetType);
				}
				foreach (FieldDefinition field in type.Fields) {
					UpdateTypeName(field.FieldType, oldCustomFullName, targetType);
				}
				foreach (PropertyDefinition property in type.Properties) {
					UpdateTypeName(property.PropertyType, oldCustomFullName, targetType);

					// NOTE: not sure about this one
					if (property.GetMethod is not null) {
						UpdateMethod(property.GetMethod, oldCustomFullName, targetType);
					}
					// NOTE: not sure about this one
					if (property.SetMethod is not null) {
						UpdateMethod(property.SetMethod, oldCustomFullName, targetType);
					}
				}
				foreach (MethodDefinition method in type.Methods) {
					UpdateMethod(method, oldCustomFullName, targetType);
				}
				foreach (EventDefinition @event in type.Events) {
					UpdateTypeName(@event.EventType, oldCustomFullName, targetType);
				}
				foreach (InterfaceImplementation @interface in type.Interfaces) {
					UpdateTypeName(@interface.InterfaceType, oldCustomFullName, targetType);
				}
				// NOTE: not sure about this one
				DrillNestedTypes(targetType, oldCustomFullName);
			}
		}

		private static void RenameType(TypeDefinition targetType, TypeDefinition[] allTypes) {
			string oldCustomFullName = targetType.GetCustomFullName();
			targetType.Name = NameGenerator.GetUniqueName();

			// resolve
			UpdateType(targetType, oldCustomFullName, allTypes);
		}

		public static void Execute(ModuleDefinition module, TypeDefinition[] allTypes) {
			int i = 0;
			foreach (TypeDefinition @class in module.Types) {
				if (
					// avoid weird names
					@class.IsSpecialName ||
					@class.IsRuntimeSpecialName ||
					!Utils.IsValidName(@class.Name, NameGenerator.SOURCE_STR)) {
					continue;
				}

				int progress = (int)System.MathF.Floor((float)++i / module.Types.Count * 100f);

				// methods
				UnityEditor.EditorUtility.DisplayProgressBar("Mfuscator -> \"" + module.Name + '"', "Renaming methods for \"" + @class.FullName + "\" (" + progress + "%)...", 0f);
				foreach (MethodDefinition method in @class.Methods) {
					if (
						// avoid weird names
						method.IsSpecialName ||
						method.IsRuntimeSpecialName ||
						!Utils.IsValidName(method.Name, NameGenerator.SOURCE_STR) ||
						// other
						method.IsGetter ||
						method.IsSetter ||
						method.IsConstructor ||
						method.IsRuntime ||
						method.IsWindowsRuntimeProjection ||
						method.IsCompilerControlled ||
						method.HasGenericParameters ||
						method.IsAbstract ||
						method.IsVirtual ||
						method.HasOverrides ||
						// runtime init & unity method
						CheckForRuntimeInitAttribute(method) ||
						Utils.Contains(Utils.defaultUnityMethods, method.Name)) {
						continue;
					}

					string oldCustomFullName = method.GetCustomFullName();
					method.Name = NameGenerator.GetUniqueName();

					// resolve
					Utils.GetOperandsFromAllMethods(allTypes, op => {
						if (op is MethodReference methodRef &&
							methodRef.DeclaringType is not null &&
							methodRef.GetCustomFullName() == oldCustomFullName &&
							Utils.CompareParameters(methodRef, method)) {

							methodRef.Name = method.Name;
						}
					});

					// params
					foreach (ParameterDefinition mParam in method.Parameters) {
						mParam.Name = NameGenerator.GetUniqueName();
					}
				}

				bool serializable = @class.IsSerializable ||
					@class.HasCustomAttributes ||
					Utils.CheckIfInheritsUnityType(@class, allTypes);

				// fields
				UnityEditor.EditorUtility.DisplayProgressBar("Mfuscator -> \"" + module.Name + '"', "Renaming fields for \"" + @class.FullName + "\" (" + progress + "%)...", 0f);
				foreach (FieldDefinition field in @class.Fields) {
					if (
						// avoid weird names
						field.IsSpecialName ||
						field.IsRuntimeSpecialName ||
						!Utils.IsValidName(field.Name, "@_" + NameGenerator.SOURCE_STR) ||
						// other
						field.ContainsGenericParameter ||
						field.IsWindowsRuntimeProjection ||
						// avoid serialization
						field.HasCustomAttributes ||
						(field.IsPublic && !field.IsStatic && serializable)) {
						continue;
					}

					string oldCustomFullName = field.GetCustomFullName();
					field.Name = NameGenerator.GetUniqueName();

					// resolve
					Utils.GetOperandsFromAllMethods(allTypes, op => {
						if (op is FieldReference fieldRef &&
						fieldRef.DeclaringType is not null &&
						fieldRef.GetCustomFullName() == oldCustomFullName) {

							fieldRef.Name = field.Name;
						}
					});
				}

				// class
				UnityEditor.EditorUtility.DisplayProgressBar("Mfuscator -> \"" + module.Name + '"', "Renaming \"" + @class.FullName + "\" (" + progress + "%)...", 0f);
				if (
					// avoid serialization
					serializable ||
					// classes only
					!@class.IsClass ||
					@class.IsEnum ||
					@class.IsValueType ||
					@class.IsArray ||
					@class.IsFunctionPointer ||
					@class.IsInterface ||
					@class.IsPointer ||
					// runtime init
					CheckForRuntimeInitMethods(@class)) {
					continue;
				}

				RenameType(@class, allTypes);
			}
			UnityEditor.EditorUtility.ClearProgressBar();
		}
	}
}

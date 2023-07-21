using System;
using System.Collections.Generic;

namespace Mfuscator.CSharp {

	public static class NameGenerator {

		public const int LENGTH = 12;
		public const string SOURCE_STR = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

		public static readonly Random random = new();
		private static readonly HashSet<string> _usedNames = new();

		public static string GetString() {
			string result = string.Empty;
			for (int i = 0; i < LENGTH; i++) {
				result += SOURCE_STR[random.Next(SOURCE_STR.Length)];
			}
			return result;
		}

		public static string GetUniqueName() {
			string result;
			do {
				result = GetString();
			} while (!_usedNames.Add(result));
			return result;
		}

		public static void ResetUsedNames() {
			_usedNames.Clear();
		}
	}
}

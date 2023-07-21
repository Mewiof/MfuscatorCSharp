using System.Text;

/* NOTE: this script was developed to speed up the testing of builds on a
 * local machine. Use in releases without a proper license may lead to
 * legal problems with Unity
 */

namespace Mfuscator.CSharp {

	public static class SplashScreenRemover {

		/// <summary>Util</summary>
		private static int FindIndex(byte[] inBytes, byte[] bytes, int startIndex = 0, int skip = 0) {
			int skipped = 0;
			for (int i = startIndex; i < inBytes.Length - bytes.Length + 1; i++) {
				bool matchFound = true;
				for (int j = 0; j < bytes.Length; j++) {
					if (inBytes[i + j] != bytes[j]) {
						matchFound = false;
						break;
					}
				}
				if (matchFound) {
					if (skipped < skip) {
						skipped++;
						continue;
					}
					return i;
				}
			}
			return -1;
		}

		/// <summary>Util</summary>
		private static int FindIndex(byte[] inBytes, string aSCIIText, int startIndex = 0, int skip = 0) {
			return FindIndex(inBytes, Encoding.ASCII.GetBytes(aSCIIText), startIndex, skip);
		}

		// tested on Unity 2022
		/// <param name="fileBytes">"globalgamemanagers"</param>
		public static bool DisableIn(byte[] fileBytes) {
			int secondUnityVersionStrIndex = FindIndex(fileBytes, UnityEngine.Application.unityVersion, skip: 1);
			if (secondUnityVersionStrIndex == -1) {
				Utils.LogError("Failed to find an index for \"" + nameof(secondUnityVersionStrIndex) + "\" (" + UnityEngine.Application.unityVersion + ')');
				return false;
			}
			int isProBoolIndex = secondUnityVersionStrIndex - 20;
			if (fileBytes[isProBoolIndex] != 0x00) {
				Utils.LogError("The value of \"" + nameof(isProBoolIndex) + "\" (" + fileBytes[isProBoolIndex] + ") is different from the expected one (0)");
				return false;
			}
			fileBytes[isProBoolIndex] = 0x01;

			int productNameStrIndex = FindIndex(fileBytes, UnityEngine.Application.productName);
			if (productNameStrIndex == -1) {
				Utils.LogError("Failed to find an index for \"" + nameof(productNameStrIndex) + "\" (" + UnityEngine.Application.productName + ')');
				return false;
			}
			int showSplashScreenBoolIndex = FindIndex(fileBytes, new byte[] { 0x80, 0x3F, 0x01, 0x01 }, startIndex: productNameStrIndex);
			if (showSplashScreenBoolIndex == -1) {
				Utils.LogError("Failed to find an index for \"" + nameof(showSplashScreenBoolIndex) + '"');
				return false;
			}
			showSplashScreenBoolIndex += 2;
			fileBytes[showSplashScreenBoolIndex] = 0x00;
			return true;
		}
	}
}

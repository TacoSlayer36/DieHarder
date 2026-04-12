using MelonLoader;
using Il2CppTMPro;
using UnityEngine;

namespace DieHarder
{
	public static class Debug
	{
		public static bool debugMode => Core.Instance.DebugEnabled;
		public static void Log(string message, bool debugOnly = false, int logLevel = 0)
		{

			if (debugOnly && !debugMode)
				return;

			switch (logLevel)
			{
				case 1:
					Melon<Core>.Logger.Warning("Warn: " + message);
					break;
				case 2:
					Melon<Core>.Logger.Error("Error: " + message);
					break;
				default:
					Melon<Core>.Logger.Msg(message);
					break;
			}

		}

		public static GameObject DebugUi { get; private set; }
		public static TextMeshPro DebugUiText { get; private set; }

		public static void PrintInGame(string message)
		{
			if (!(DebugUi is null))
			{
				//DebugUi.gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1, 0.21);
				DebugUiText.enableWordWrapping = false;
				DebugUiText.text = message;
			}
			else
			{
				Log($"Can't print message: \"{message}\" to debug ui. Not created and assigned", true, 2);
			}
		}
	}
}

using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using BaseX;
using System;

namespace Neos_Varjo_Eye
{
	public class Neos_Varjo_Eye_Integration : NeosMod
	{
		public static MemoryData memoryData;

		public override string Name => "Neos-Varjo-Eye-Integration";
		public override string Author => "dfgHiatus";
		public override string Version => "1.0.0";
		public override string Link => "https://github.com/dfgHiatus/Neos-Eye-Face-API/";
		public override void OnEngineInit()
		{
			// Harmony.DEBUG = true;
			Harmony harmony = new Harmony("net.dfgHiatus.Neos-Eye-Face-API");
			harmony.PatchAll();
		}

		[HarmonyPatch(typeof(InputInterface), MethodType.Constructor)]
		[HarmonyPatch(new[] { typeof(Engine) })]
		public class InputInterfaceCtorPatch
		{
			public static void Postfix(InputInterface __instance)
			{
				try
				{
					if (!VarjoTrackingModule.tracker.ConnectToPipe())
                    {
						Warn("Gaze tracking is not allowed! Please enable it in the Varjo Base!");
					}
					else
                    {
						Debug("Gaze tracking enabled for Varjo Areo");
						GenericInputDevice gen = new GenericInputDevice();
						Debug("Module Name: " + gen.ToString());
						__instance.RegisterInputDriver(gen);
					}

				}
				catch (Exception e)
				{
					Warn("Module failed to initiallize.");
					Warn(e.ToString());
				}
			}
		}

		[HarmonyPatch(typeof(Engine), "Shutdown")]
		public class ShutdownPatch
		{
			public static bool Prefix()
			{
				VarjoTrackingModule.tracker.Teardown();
				return true;
			}
		}

		public class GenericInputDevice : IInputDriver
		{
			public Eyes eyes;
			public int UpdateOrder => 100;
			public float eyeTimestamp = 0f;
			// These values need to be tweaked per user
			public float Alpha = 2f;
			public float Beta = 2f;

			public void CollectDeviceInfos(DataTreeList list)
			{
				DataTreeDictionary EyeDataTreeDictionary = new DataTreeDictionary();
				EyeDataTreeDictionary.Add("Name", "Varjo Eye Tracking");
				EyeDataTreeDictionary.Add("Type", "Eye Tracking");
				EyeDataTreeDictionary.Add("Model", "Varjo Areo");
				list.Add(EyeDataTreeDictionary);
			}

			public void RegisterInputs(InputInterface inputInterface)
			{
				eyes = new Eyes(inputInterface, "Varjo Eye Tracking");
			}

			public void UpdateInputs(float deltaTime)
			{
				VarjoTrackingModule.tracker.Update();

				// Current eye data assumes a data format similar to the Pimax
				// Widen doesn't show up in the Pimax tracker, see if omitting this makes a difference
				eyes.IsEyeTrackingActive = Engine.Current.InputInterface.VR_Active;
				
				eyes.LeftEye.IsTracking = Engine.Current.InputInterface.VR_Active;
				eyes.LeftEye.IsDeviceActive = Engine.Current.InputInterface.VR_Active;
				eyes.LeftEye.Direction = new float3(MathX.Tan(Alpha * (float)memoryData.leftEye.x),
													  MathX.Tan(Beta * (-1f) * (float)memoryData.leftEye.y),
													  1f).Normalized;
				eyes.LeftEye.RawPosition = new float3((float)memoryData.leftEye.y * (-1f),
													  (float)memoryData.leftEye.x,
													  0f);
				eyes.LeftEye.PupilDiameter = (float)memoryData.leftEye.pupilSize;
				eyes.LeftEye.Openness = memoryData.leftEye.opened ? 0f : 1f;

				// eyes.LeftEye.Widen = (float)MathX.Clamp01(memoryData.leftEye.y);
				eyes.LeftEye.Squeeze = (float)MathX.Remap(MathX.Clamp(memoryData.leftEye.y, -1f, 0f), -1f, 0f, 0f, 1f);
				eyes.LeftEye.Frown = 0f;


				eyes.RightEye.IsTracking = Engine.Current.InputInterface.VR_Active;
				eyes.RightEye.IsDeviceActive = Engine.Current.InputInterface.VR_Active;
				eyes.RightEye.Direction = new float3(MathX.Tan(Alpha * (float)memoryData.rightEye.x),
					  MathX.Tan(Beta * (-1f) * (float)memoryData.rightEye.y),
					  1f).Normalized;
				eyes.RightEye.RawPosition = new float3((float)memoryData.rightEye.y * (-1f),
													  (float)memoryData.rightEye.x,
													  0f);
				eyes.RightEye.PupilDiameter = (float)memoryData.rightEye.pupilSize;
				eyes.RightEye.Openness = memoryData.rightEye.opened ? 0f : 1f;
				// eyes.RightEye.Widen = (float)MathX.Clamp01(memoryData.rightEye.y);
				eyes.RightEye.Squeeze = (float)MathX.Remap(MathX.Clamp(memoryData.rightEye.y, -1f, 0f), -1f, 0f, 0f, 1f);
				eyes.RightEye.Frown = 0f;


				eyes.CombinedEye.IsTracking = Engine.Current.InputInterface.VR_Active;
				eyes.CombinedEye.IsDeviceActive = Engine.Current.InputInterface.VR_Active;
				eyes.CombinedEye.Direction = new float3(MathX.Average((float)MathX.Tan(Alpha * memoryData.leftEye.y), (float)MathX.Tan(Alpha * memoryData.rightEye.y)),
										 MathX.Average((float)MathX.Tan(Alpha * memoryData.leftEye.x), (float)MathX.Tan(Alpha * memoryData.rightEye.x)),
										 1f).Normalized;
				eyes.CombinedEye.RawPosition = new float3(MathX.Average((float)(memoryData.leftEye.x + memoryData.rightEye.x)),
														  MathX.Average((float)(memoryData.leftEye.y + memoryData.rightEye.x)),
														  0f);
				eyes.CombinedEye.PupilDiameter = (float)memoryData.combined.pupilSize;
				eyes.CombinedEye.Openness = memoryData.combined.opened ? 0f : 1f;
				// eyes.CombinedEye.Widen = MathX.Average((float)MathX.Clamp01(memoryData.leftEye.x), (float)MathX.Clamp01(memoryData.rightEye.y));
				eyes.CombinedEye.Squeeze = MathX.Average((float)MathX.Remap(MathX.Clamp(memoryData.leftEye.y, -1f, 0f), -1f, 0f, 0f, 1f),
                                                                (float)MathX.Remap(MathX.Clamp(memoryData.rightEye.y, -1f, 0f), -1f, 0f, 0f, 1f));
				eyes.CombinedEye.Frown = 0f;


				eyes.Timestamp = eyeTimestamp;
				eyeTimestamp += deltaTime;
			}
		}
	}
}

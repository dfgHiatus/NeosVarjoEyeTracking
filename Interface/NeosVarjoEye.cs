using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using BaseX;
using System;

namespace NeosVarjoEye
{
	public class NeosVarjoEye : NeosMod
	{
		public static GazeData gazeData;

		public override string Name => "Neos-Varjo-Eye-Integration";
		public override string Author => "dfgHiatus";
		public override string Version => "1.0.1";
		public override string Link => "https://github.com/dfgHiatus/NeosVarjoEyeTracking";
		public override void OnEngineInit()
		{
			// Harmony.DEBUG = true;
			Harmony harmony = new Harmony("net.dfgHiatus.NeosVarjoEyeTracking");
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
						Warn("Varjo headset isn't detected");
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

			// public float Alpha = 1f; // Eye Swing Up/Down
			// public float Beta = 1f; // Eye Swing Left/Right

			// Idle pupil size in mm. Did you know the average human pupil size is between 4 and 6mm?
			// pupilSize is normalized from 0 to 1, so idle is 0.5 or ~3mm
			public float userPupilDiameter = 0.06f;

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

				// Current eye data takes on a data format similar to the Pimax, x and y values normalized from -1 to 1
				eyes.IsEyeTrackingActive = Engine.Current.InputInterface.VR_Active;

				eyes.LeftEye.IsDeviceActive = Engine.Current.InputInterface.VR_Active;
				eyes.LeftEye.IsTracking = gazeData.leftStatus == GazeEyeStatus.Compensated ||
					gazeData.leftStatus == GazeEyeStatus.Tracked;
				eyes.LeftEye.Direction = (float3)new double3(gazeData.leftEye.forward.x,
													  gazeData.leftEye.forward.y,
													  gazeData.leftEye.forward.z);
				eyes.LeftEye.RawPosition = (float3)new double3(gazeData.leftEye.origin.x,
													  gazeData.leftEye.origin.y,
													  gazeData.leftEye.origin.z);
				eyes.LeftEye.PupilDiameter = (float)(gazeData.leftPupilSize * userPupilDiameter);
				eyes.LeftEye.Openness = gazeData.leftStatus == GazeEyeStatus.Invalid ? 0f : 1f; 
				eyes.LeftEye.Widen = (float)MathX.Clamp01(gazeData.leftEye.forward.y);
				eyes.LeftEye.Squeeze = 0f;
				eyes.LeftEye.Frown = 0f;

				eyes.RightEye.IsDeviceActive = Engine.Current.InputInterface.VR_Active;
				eyes.RightEye.IsTracking = gazeData.rightStatus == GazeEyeStatus.Compensated ||
					gazeData.rightStatus == GazeEyeStatus.Tracked;
				eyes.RightEye.Direction = (float3)new double3(gazeData.rightEye.forward.x,
													  gazeData.rightEye.forward.y,
													  gazeData.rightEye.forward.z);
				eyes.RightEye.RawPosition = (float3)new double3(gazeData.rightEye.origin.x,
													  gazeData.rightEye.origin.y,
													  gazeData.rightEye.origin.z);
				eyes.RightEye.PupilDiameter = (float)(gazeData.rightPupilSize * userPupilDiameter);
				eyes.RightEye.Openness = gazeData.rightStatus == GazeEyeStatus.Invalid ? 0f : 1f;
				eyes.RightEye.Widen = (float)MathX.Clamp01(gazeData.rightEye.forward.y);
				eyes.RightEye.Squeeze = 0f;
				eyes.RightEye.Frown = 0f;

				eyes.CombinedEye.IsDeviceActive = Engine.Current.InputInterface.VR_Active;
				eyes.CombinedEye.IsTracking = gazeData.status == GazeStatus.Valid;
				eyes.CombinedEye.Direction = (float3)new double3(gazeData.gaze.forward.x,
													  gazeData.gaze.forward.y,
													  gazeData.gaze.forward.z);
				eyes.CombinedEye.RawPosition = (float3)new double3(gazeData.gaze.origin.x,
													  gazeData.gaze.origin.y,
													  gazeData.gaze.origin.z);
				eyes.CombinedEye.PupilDiameter = MathX.Average((float)(gazeData.leftPupilSize * userPupilDiameter),
					(float)(gazeData.rightPupilSize * userPupilDiameter));
				eyes.CombinedEye.Openness = gazeData.leftStatus == GazeEyeStatus.Invalid ||
					gazeData.rightStatus == GazeEyeStatus.Invalid ? 0f : 1f;
				eyes.CombinedEye.Widen = (float)MathX.Clamp01(gazeData.gaze.forward.y);
				eyes.CombinedEye.Squeeze = 0f;
				eyes.CombinedEye.Frown = 0f;

				// Convergence Distance is NOT Focus Distance, but we need to get it in somehow
				if (gazeData.stability > 0.75) {
					eyes.ConvergenceDistance = (float) gazeData.focusDistance;
				}

				// eyes.Timestamp = gazeData.captureTime / 1000000000; // Convert nanoseconds to seconds with this one nifty trick!
				eyes.Timestamp = gazeData.frameNumber;
			}
		}
	}
}

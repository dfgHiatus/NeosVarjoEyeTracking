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
		public override string Version => "1.0.4";
		public override string Link => "https://github.com/dfgHiatus/NeosVarjoEyeTracking";

		public override void OnEngineInit() => new Harmony("net.dfgHiatus.NeosVarjoEyeTracking").PatchAll();

		[HarmonyPatch(typeof(InputInterface), MethodType.Constructor)]
		[HarmonyPatch(new System.Type[] { typeof(Engine) })]
		public class InputInterfaceCtorPatch
		{
			public static void Postfix(InputInterface __instance)
			{
				try
				{
					if (VarjoTrackingModule.tracker.ConnectToPipe())
					{
						Debug("Gaze tracking enabled for Varjo HMD.");
						GenericInputDevice genericInputDevice = new GenericInputDevice();
						__instance.RegisterInputDriver(genericInputDevice);
					}
					else
						Warn("Varjo headset isn't detected.");
				}
				catch (Exception ex)
				{
					Warn("Module failed to initiallize.");
					Warn(ex.ToString());
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

			public float userPupilDiameter = 0.0065f;
			public int UpdateOrder => 100;

			public float smoothing = 0.02f;

			public void CollectDeviceInfos(DataTreeList list)
			{
				DataTreeDictionary dataTreeDictionary = new DataTreeDictionary();
				dataTreeDictionary.Add("Name", "Varjo Eye Tracking");
				dataTreeDictionary.Add("Type", "Eye Tracking");
				dataTreeDictionary.Add("Model", "Varjo HMD");
				list.Add(dataTreeDictionary);
			}

			public void RegisterInputs(InputInterface inputInterface)
			{
				eyes = new Eyes(inputInterface, "Varjo Eye Tracking");
			}

			public void UpdateInputs(float deltaTime)
			{
				// Updating this on the main thread isn't the optimal idea.
				// But giving it its own thread messes around with things for some reason. So here it lies
				VarjoTrackingModule.tracker.Update();

				var leftPupil = (float)(gazeData.leftPupilSize * userPupilDiameter);
				var rightPupil = (float)(gazeData.leftPupilSize * userPupilDiameter);

				var leftOpen =
					gazeData.leftStatus == GazeEyeStatus.Tracked ? 1f : (
					gazeData.leftStatus == GazeEyeStatus.Compensated ? 0.5f : (
					gazeData.leftStatus == GazeEyeStatus.Visible ? 0.25f
					: 0f)); // GazeEyeStatus.Invalid

				var rightOpen =
					gazeData.rightStatus == GazeEyeStatus.Tracked ? 1f : (
					gazeData.rightStatus == GazeEyeStatus.Compensated ? 0.5f : (
					gazeData.rightStatus == GazeEyeStatus.Visible ? 0.25f
					: 0f)); // GazeEyeStatus.Invalid

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

				eyes.LeftEye.PupilDiameter = leftPupil;
				eyes.LeftEye.Openness = MathX.SmoothLerp(eyes.LeftEye.Openness, leftOpen, ref smoothing, deltaTime);
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

				eyes.RightEye.PupilDiameter = rightPupil;
				eyes.RightEye.Openness = MathX.SmoothLerp(eyes.RightEye.Openness, rightOpen, ref smoothing, deltaTime);
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
				eyes.CombinedEye.PupilDiameter = MathX.Average(leftPupil, rightPupil);
				eyes.CombinedEye.Openness = MathX.SmoothLerp(MathX.Average(leftOpen, rightOpen), eyes.CombinedEye.Openness, ref smoothing, deltaTime);
				eyes.CombinedEye.Widen = (float)MathX.Clamp01(gazeData.gaze.forward.y);
				eyes.CombinedEye.Squeeze = 0f;
				eyes.CombinedEye.Frown = 0f;

				// Yes, I am aware convergence distance and focus distance are NOT the same thing.
				// But we ought to get it in somehow.
				if (gazeData.stability > 0.75) {
					eyes.ConvergenceDistance = (float) gazeData.focusDistance;
				}

				/*
				* Yeah, gazeData.captureTime is wonky. gazeData.frameNumber it is! 
				* We need a hardcoded value to divide it by bc mod config go brr
				* I mean, you don't need 100hz+ sampling in a networked social context anyways, right? Right!
				*/

				eyes.Timestamp = gazeData.frameNumber / 100;
			}
		}
	}
}

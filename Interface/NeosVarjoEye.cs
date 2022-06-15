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

			public float userPupilDiameter = 0.001f;
			public int UpdateOrder => 100;

			public float smoothing = 0.1f;
			public float speed = 4.0f;

			private float _leftOpen = 1.0f;
			private float _rightOpen = 1.0f;
			private float _combinedOpen = 1.0f;

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

				_leftOpen = MathX.Lerp(_leftOpen, leftOpen, deltaTime * speed);

				var rightOpen =
					gazeData.rightStatus == GazeEyeStatus.Tracked ? 1f : (
					gazeData.rightStatus == GazeEyeStatus.Compensated ? 0.5f : (
					gazeData.rightStatus == GazeEyeStatus.Visible ? 0.25f
					: 0f)); // GazeEyeStatus.Invalid

				_rightOpen = MathX.Lerp(_rightOpen, rightOpen, deltaTime * speed);

				bool leftStatus = gazeData.leftStatus == GazeEyeStatus.Compensated ||
				                  gazeData.leftStatus == GazeEyeStatus.Tracked;
				bool rightStatus = gazeData.rightStatus == GazeEyeStatus.Compensated ||
				                   gazeData.rightStatus == GazeEyeStatus.Tracked;

				eyes.IsEyeTrackingActive = Engine.Current.InputInterface.VR_Active;
				
				UpdateEye(in gazeData.leftEye, in leftStatus, in leftPupil, _leftOpen, deltaTime, eyes.LeftEye);
				UpdateEye(in gazeData.rightEye, in rightStatus, in rightPupil, _rightOpen, deltaTime, eyes.RightEye);
				
				bool combinedStatus = gazeData.status == GazeStatus.Valid;
				float combinedPupil = MathX.Average(leftPupil, rightPupil);
				float combinedOpen = MathX.Average(leftOpen, rightOpen);

				_combinedOpen = MathX.Lerp(_combinedOpen, combinedOpen, deltaTime * speed);
				
				UpdateEye(in gazeData.gaze, in combinedStatus, in combinedPupil, _combinedOpen, deltaTime, eyes.CombinedEye);

				eyes.ComputeCombinedEyeParameters();

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
				
				eyes.FinishUpdate();
			}

			void UpdateEye(in GazeRay data, in bool status, in float pupilSize, in float openness, in float deltaTime, Eye eye)
			{
				eye.IsDeviceActive = Engine.Current.InputInterface.VR_Active;
				eye.IsTracking = status;

				if (eye.IsTracking)
				{
					eye.UpdateWithDirection((float3)new double3(data.forward.x,
						data.forward.y,
						data.forward.z).Normalized);
					
					eye.RawPosition = (float3)new double3(data.origin.x,
						data.origin.y,
						data.origin.z);
					
					eye.PupilDiameter = pupilSize;
				}

				eye.Openness = openness;
				eye.Widen = (float)MathX.Clamp01(data.forward.y);
				eye.Squeeze = 0f;
				eye.Frown = 0f;
			}
		}
	}
}

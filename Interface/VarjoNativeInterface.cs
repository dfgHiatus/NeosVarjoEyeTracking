using Elements.Core;
using FrooxEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ResoniteVarjoEye;

public class VarjoNativeInterface : VarjoInterface
{
    private IntPtr _session;

    public override bool Initialize()
    {
        //if (!VarjoAvailable())
        //{
        //    UniLog.Error("Varjo headset isn't detected");
        //    return false;
        //}
        LoadLibrary();
        _session = varjo_SessionInit();
        if (_session == IntPtr.Zero)
        {
            return false;
        }
        if (!varjo_IsGazeAllowed(_session))
        {
            UniLog.Error("Gaze tracking is not allowed! Please enable it in the Varjo Base!");
            return false;
        }
        varjo_GazeInit(_session);
        varjo_SyncProperties(_session);
        return true;
    }

    public override void Update()
    {
        if (_session == IntPtr.Zero)
            return;

        // Get's GazeData and EyeMeasurements from the Varjo SDK
        // Return value states whether or not the request was successful (true = has Data; false = Error occured)
        bool hasData = varjo_GetGazeData(_session, out gazeData, out eyeMeasurements);

        if (!hasData)
            UniLog.Log("Error while getting Gaze Data");
    }

    public override void Teardown() { }

    public override string GetName() => "Varjo Native Interface";

    private bool LoadLibrary()
    {;
        var path = "VarjoLib.dll";
        if (LoadLibrary(path) == IntPtr.Zero)
        {
            UniLog.Error(string.Concat("Unable to load library ", path));
            return false;
        }
        UniLog.Log(string.Concat("Loaded library ", path));
        return true;
    }

    [DllImport("kernel32", CharSet = CharSet.Unicode, ExactSpelling = false, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("VarjoLib", CharSet = CharSet.Auto)]
    private static extern bool varjo_IsAvailable();

    [DllImport("VarjoLib", CharSet = CharSet.Auto)]
    private static extern IntPtr varjo_SessionInit();

    [DllImport("VarjoLib", CharSet = CharSet.Auto)]
    private static extern void varjo_SessionShutDown(IntPtr session);

    [DllImport("VarjoLib", CharSet = CharSet.Auto)]
    private static extern void varjo_GazeInit(IntPtr session);

    [DllImport("VarjoLib", CharSet = CharSet.Auto)]
    private static extern int varjo_GetError(IntPtr session);

    [DllImport("VarjoLib", CharSet = CharSet.Auto)]
    private static extern string varjo_GetErrorDesc(int errorCode);

    [DllImport("VarjoLib", CharSet = CharSet.Auto)]
    private static extern bool varjo_IsGazeAllowed(IntPtr session);

    [DllImport("VarjoLib", CharSet = CharSet.Auto)]
    private static extern bool varjo_IsGazeCalibrated(IntPtr session);

    [DllImport("VarjoLib", CharSet = CharSet.Auto)]
    private static extern GazeData varjo_GetGaze(IntPtr session);

    [DllImport("VarjoLib", CharSet = CharSet.Auto)]
    private static extern bool varjo_GetGazeData(IntPtr session, out GazeData gaze, out EyeMeasurements eyeMeasurements);

    [DllImport("VarjoLib", CharSet = CharSet.Auto)]
    private static extern void varjo_RequestGazeCalibration(IntPtr session);

    [DllImport("VarjoLib", CharSet = CharSet.Auto)]
    private static extern bool varjo_GetPropertyBool(IntPtr session, int propertyKey);

    [DllImport("VarjoLib", CharSet = CharSet.Auto)]
    private static extern int varjo_GetPropertyInt(IntPtr session, int propertyKey);

    [DllImport("VarjoLib", CharSet = CharSet.Auto)]
    private static extern void varjo_SyncProperties(IntPtr session);
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityUI;
using UnityPlotter;

public static class SceneObjectHandler
{
    public static Dropdown          deviceName;
    public static UIExtension       deviceNameExtension;
    public static Button            connectButton;
    public static Button            disconnectButton;
    public static ProgressIndicator connectionIndicator;
    public static Text              rawDataText;
    public static Plotter           dataPlotter;
    public static Browser           fileBrowser;
    public static InputField        fileNameInputField;
    public static BlinkImage        exportSuccessIndicator;
    public static CanvasGroup       recordControllers;
    public static Button            recordButton;
    public static Button            pauseButton;
    public static Button            restartButton;
    public static Button            stopButton;
    public static ProgressIndicator recordingIndicator;
    public static GameObject        recordSlider;
    public static PanelFader        panelFader;

    public static void Initialize()
    {
        deviceName             = GameObject.Find("Device Name").GetComponent<Dropdown>();
        deviceNameExtension    = GameObject.Find("Device Name").GetComponent<UIExtension>();
        connectButton          = GameObject.Find("Connect Button").GetComponent<Button>();
        disconnectButton       = GameObject.Find("Disconnect Button").GetComponent<Button>();
        connectionIndicator    = GameObject.Find("Progress Indicator").GetComponent<ProgressIndicator>();
        rawDataText            = GameObject.Find("Raw Data Text").GetComponent<Text>();
        dataPlotter            = GameObject.Find("Data Plotter").GetComponent<Plotter>();
        fileBrowser            = GameObject.Find("File Directory Browser").GetComponent<Browser>();
        fileNameInputField     = GameObject.Find("File Name Input Field").GetComponent<InputField>();
        exportSuccessIndicator = GameObject.Find("Export Success Indicator").GetComponent<BlinkImage>();
        recordControllers      = GameObject.Find("Record Controllers").GetComponent<CanvasGroup>();
        recordButton           = GameObject.Find("Record").GetComponent<Button>();
        pauseButton            = GameObject.Find("Pause").GetComponent<Button>();
        restartButton          = GameObject.Find("Restart").GetComponent<Button>();
        stopButton             = GameObject.Find("Stop").GetComponent<Button>();
        recordingIndicator     = GameObject.Find("Recording Indicator").GetComponent<ProgressIndicator>();
        recordSlider           = GameObject.Find("Record Slider");
        panelFader             = GameObject.Find("Scene").GetComponent<PanelFader>();
    }
}

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using SerialManager;
using UnityUI;
using UnityEngine.UI;
using static SceneObjectHandler;

public class SceneManager : MonoBehaviour
{
    public SerialHandleUnity serialHandle;
    private bool _isConnected;

    private List<float> _plotData = new List<float>();
    private int _dataCountLimit = 1000;

    private string _fileSaveDirectory = "";
    private string _fileName = "";

    private List<int>    _recordData     = new List<int>();
    private List<double> _recordTime     = new List<double>();
    private List<double> _connectionTime = new List<double>();
    private int _dataCount = 0;
    private bool _isRecording = false;
    private List<int> _recordStartPoint = new List<int>();
    private List<int> _recordEndPoint = new List<int>();
    private List<int> _pausedStartPoint = new List<int>();
    private List<int> _pausedEndPoint = new List<int>();
    private List<RectTransform> _sliderRecorededRegionImages = new List<RectTransform>();
    private List<RectTransform> _sliderPausedRegionImaged = new List<RectTransform>();
    private bool _isRecordPauseRequested = false;

    // 데이터 저장 시간 확인
    private TimeTracker _recordTimeTracker = new TimeTracker();
    private TimeTracker _connectionTimeTracker = new TimeTracker();

    private void Start()
    {
        // 시리얼 매니저 스캔 완료 이벤트 등록
        serialHandle.onScanEnded.AddListener(OnSerialDeviceScanEnded);
    
        Initialize();

        // 장치 이름 선택 dropdown 이벤트 등록
        deviceNameExtension.onPointerClick.AddListener(OnDeviceNameClicked);
        OnDeviceNameClicked();
        deviceName.onValueChanged.AddListener(OnDeviceNameChanged);

        // 장치 connect 및 disconnect 버튼 이벤트 등록
        disconnectButton.gameObject.SetActive(false);
        connectButton.onClick.AddListener(ConnectDevice);
        disconnectButton.onClick.AddListener(DisconnectDevice);

        // 시리얼 매니저 연결 완료, 연결 실패, 연결 종료 이벤트 등록
        serialHandle.onConnected.AddListener(OnDeviceConnected);
        serialHandle.onConnectionFailed.AddListener(OnDeviceConnectionFailed);
        serialHandle.onDisconnected.AddListener(OnDeviceDisconnected);

        dataPlotter.Clear();

        // 저장 경로 변경 이벤트 등록
        fileBrowser.onDirectoryChanged.AddListener(OnExportDirectoryChanged);
        // 저장 파일 이름 변경 이벤트 등록
        fileNameInputField.onEndEdit.AddListener(OnFileNameChanged);
        // 데이터 기록 버튼 이벤트 등록
        SetControllersActive(false);
        restartButton.gameObject.SetActive(false);
        recordButton.onClick.AddListener(StartRecord);
        pauseButton.onClick.AddListener(PauseRecord);
        restartButton.onClick.AddListener(RestartRecord);
        stopButton.onClick.AddListener(StopRecord);
    }

    private void SetControllersActive(bool isActive)
    {
        if (isActive) {
            recordControllers.alpha = 1f;
            recordControllers.interactable = true;
            recordControllers.blocksRaycasts = true;
        }
        else {
            recordControllers.alpha = 0.3f;
            recordControllers.interactable = false;
            recordControllers.blocksRaycasts = false;
        }
    }

    private void OnSerialDeviceScanEnded(SerialLog e)
    {
        List<string> devices = new List<string>();
        for (int i = 0; i < e.devices.Length; i++) {
            if (e.devices[i].Contains("[USB] ")) {
                devices.Add(e.devices[i].Replace("[USB] ", ""));
            }
        }

        deviceName.ClearOptions();
        deviceName.AddOptions(devices);
        OnDeviceNameChanged(deviceName.value);
    }

    private void OnDeviceNameClicked()
    {
        serialHandle.ScanDevices();
    }

    private void OnDeviceNameChanged(int index)
    {
        if (deviceName.options.Count < 1) {
            UnityEngine.Debug.Log("조회된 연결 가능한 장치가 없습니다");
            return;
        }

        serialHandle.portName = deviceName.options[index].text;
    }

    private void ConnectDevice()
    {
        serialHandle.Connect();
        connectionIndicator.StartProgressing();
    }

    private void DisconnectDevice()
    {
        serialHandle.Disconnect();
        if (_isRecording) {
            StopRecord();
        }

        connectionIndicator.ClearProgressing();
        connectButton.gameObject.SetActive(true);
        disconnectButton.gameObject.SetActive(false);
    }

    private void OnDeviceConnected()
    {
        _isConnected = true;
        connectionIndicator.StopProgressing(true);
        connectButton.gameObject.SetActive(false);
        disconnectButton.gameObject.SetActive(true);
        dataPlotter.Clear();
        SetControllersActive(_isConnected && _fileSaveDirectory != "" && _fileName != "");
        _connectionTimeTracker.Reset();
    }

    private void OnDeviceConnectionFailed()
    {
        _isConnected = false;
        connectionIndicator.StopProgressing(false);
        connectButton.gameObject.SetActive(true);
        disconnectButton.gameObject.SetActive(false);
        rawDataText.text = "";
        dataPlotter.Clear();
        _plotData.Clear();
        _dataCount = 0;
        _recordStartPoint.Clear();
        _recordEndPoint.Clear();
        _pausedStartPoint.Clear();
        _pausedEndPoint.Clear();
        recordSlider.AdjustChildObjectCount(0, "Recorded Region", (obj) => OnSliderRegionCreated(obj, 1f,   ref _sliderRecorededRegionImages));
        recordSlider.AdjustChildObjectCount(0, "Paused Region",   (obj) => OnSliderRegionCreated(obj, 0.3f, ref _sliderPausedRegionImaged));
        SetControllersActive(_isConnected && _fileSaveDirectory != "" && _fileName != "");
    }

    private void OnDeviceDisconnected()
    {
        _isConnected = false;
        rawDataText.text = "";
        dataPlotter.Clear();
        _plotData.Clear();
        _dataCount = 0;
        _recordStartPoint.Clear();
        _recordEndPoint.Clear();
        _pausedStartPoint.Clear();
        _pausedEndPoint.Clear();
        recordSlider.AdjustChildObjectCount(0, "Recorded Region", (obj) => OnSliderRegionCreated(obj, 1f,   ref _sliderRecorededRegionImages));
        recordSlider.AdjustChildObjectCount(0, "Paused Region",   (obj) => OnSliderRegionCreated(obj, 0.3f, ref _sliderPausedRegionImaged));
        SetControllersActive(_isConnected && _fileSaveDirectory != "" && _fileName != "");
    }

    public void OnDataReceived(SerialData e)
    {
        string[] tokens = e.packet.Split('-');

        if (tokens.Length != 6) return;

        int data = 0;
        if (int.TryParse(tokens[2], out int quotient)) {
            data += quotient * 254;
        }
        if (int.TryParse(tokens[3], out int remainder)) {
            data += remainder;
        }

        // Data Panel의 raw data text에 raw data 표시
        rawDataText.text = data.ToString("0000");

        // Data Panel의 Plotter에 data plot
        _plotData.Add((float)data);
        if (_plotData.Count > _dataCountLimit) {
            _plotData.RemoveAt(0);
        }

        dataPlotter.Plot(_plotData.ToArray(), lineWidth : 5f);
        dataPlotter.Draw();

        if (_isRecording && !_isRecordPauseRequested) {
            _recordData.Add(data);
            _recordTime.Add((double)_recordTimeTracker.GetElapsedTime() / 1000 / 1000); // 초 단위로 기록
            _connectionTime.Add((double)_connectionTimeTracker.GetElapsedTime() / 1000 / 1000); // 초 단위로 기록
        }

        _dataCount += 1;
        // Record slider 조절
        recordSlider.AdjustChildObjectCount(_recordStartPoint.Count, "Recorded Region", (obj) => OnSliderRegionCreated(obj, 1f,   ref _sliderRecorededRegionImages));
        recordSlider.AdjustChildObjectCount(_pausedStartPoint.Count, "Paused Region",   (obj) => OnSliderRegionCreated(obj, 0.3f, ref _sliderPausedRegionImaged));
        for (int i = 0; i < _recordStartPoint.Count; i++) {
            float startPos = (float)_recordStartPoint[i] / (float)_dataCount * 290f; // 290 : record slider 넓이
            _sliderRecorededRegionImages[i].offsetMin = new Vector2(startPos, _sliderRecorededRegionImages[i].offsetMin.y); // 좌측 오프셋 설정
        }
        for (int i = 0; i < _recordEndPoint.Count; i++) {
            float endPos = 290f - (float)_recordEndPoint[i] / (float)_dataCount * 290f; // 290 : record slider 넓이
            _sliderRecorededRegionImages[i].offsetMax = new Vector2(-endPos,  _sliderRecorededRegionImages[i].offsetMax.y); // 우측 오프셋 설정
        }
        for (int i = 0; i < _pausedStartPoint.Count; i++) {
            float startPos = (float)_pausedStartPoint[i] / (float)_dataCount * 290f; // 290 : record slider 넓이
            _sliderPausedRegionImaged[i].offsetMin = new Vector2(startPos, _sliderPausedRegionImaged[i].offsetMin.y); // 좌측 오프셋 설정
        }
        for (int i = 0; i < _pausedEndPoint.Count; i++) {
            float endPos = 290f - (float)_pausedEndPoint[i] / (float)_dataCount * 290f; // 290 : record slider 넓이
            _sliderPausedRegionImaged[i].offsetMax = new Vector2(-endPos,  _sliderPausedRegionImaged[i].offsetMax.y); // 우측 오프셋 설정
        }
    }

    private void OnSliderRegionCreated(GameObject obj, float alpha, ref List<RectTransform> list)
    {
        var image = obj.InitializeComponent<Image>();
        var rect = obj.InitializeComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, rect.anchorMin.y); // 좌측 앵커를 왼쪽으로 설정
        rect.anchorMax = new Vector2(1f, rect.anchorMax.y); // 우측 앵커를 오른쪽으로 설정
        rect.offsetMin = new Vector2(0f, rect.offsetMin.y); // 좌측 오프셋 설정
        rect.offsetMax = new Vector2(0f, rect.offsetMax.y); // 우측 오프셋 설정
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, 10f); // 높이(Height) 설정
        image.color = new Color(30f/255f, 215f/255f, 171f/255f, alpha);
        list.Add(rect);
    }

    private void OnExportDirectoryChanged(string directory)
    {
        _fileSaveDirectory = directory;
        SetControllersActive(_isConnected && _fileSaveDirectory != "" && _fileName != "");
    }

    private void OnFileNameChanged(string fileName)
    {
        // 공백인 지 확인
        if (fileName == "") {
            _fileName = "";
        }
        else {
            // .csv로 끝나는지 확인
            bool isCsvFile = fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
            if (isCsvFile) {
                _fileName = fileName;
            }
            else {
                _fileName = fileName + ".csv";
                fileNameInputField.text = _fileName;
            }
        }
        SetControllersActive(_isConnected && _fileSaveDirectory != "" && _fileName != "");
    }

    private void StartRecord()
    {
        if (_isRecording) {
            StopRecord();
        }
        _recordData.Clear();
        _recordTime.Clear();
        _connectionTime.Clear();
        _isRecording = true;
        recordingIndicator.StartProgressing();
        _recordStartPoint.Add(_dataCount);

        _recordTimeTracker.Reset();
    }

    private void PauseRecord()
    {
        if (!_isRecording) return;

        _isRecordPauseRequested = true;
        pauseButton.gameObject.SetActive(false);
        restartButton.gameObject.SetActive(true);
        recordingIndicator.HoldProgressing();
        _recordEndPoint.Add(_dataCount);
        _pausedStartPoint.Add(_dataCount);
    }

    private void RestartRecord()
    {
        if (!_isRecording) return;

        _isRecordPauseRequested = false;
        pauseButton.gameObject.SetActive(true);
        restartButton.gameObject.SetActive(false);
        recordingIndicator.StartProgressing();
        _pausedEndPoint.Add(_dataCount);
        _recordStartPoint.Add(_dataCount);
    }

    private void StopRecord()
    {
        if (!_isRecording) return;
        if (_isRecordPauseRequested) {
            RestartRecord();
        }

        _isRecording = false;
        CSVFile.CheckDuplicatedFileName(_fileSaveDirectory, ref _fileName);
        // 데이터 저장
        string[,] exportData = new string[_recordData.Count + 1, 3];
        // 헤더
        exportData[0, 0] = "Elapsed time since device connected (s)";
        exportData[0, 1] = "Elapsed time since recording started (s)";
        exportData[0, 2] = "Raw data of sensor";
        for (int i = 1; i < _recordData.Count; i++) {
            exportData[i, 0] = _connectionTime[i].ToString();
            exportData[i, 1] = _recordTime[i].ToString();
            exportData[i, 2] = _recordData[i].ToString();
        }
        // 데이터 CSV 파일로 저장
        CSVFile.Write(exportData, _fileSaveDirectory + "\\" + _fileName);
        // 데이터 리스트 초기화
        _recordData.Clear();
        _recordTime.Clear();
        _connectionTime.Clear();
        // record progress indicator 초기화
        recordingIndicator.ClearProgressing();
        // 저장 성공 시 출력 파일 명 주변을 녹색으로 blink
        exportSuccessIndicator.Blink(0.6f);
        // Record end point 추가
        _recordEndPoint.Add(_dataCount);
        // 연속 저장을 위해 파일명 조정
        CSVFile.AdjustFileName(ref _fileName);
        fileNameInputField.text = _fileName;
    }

    public void ChangePanel()
    {
        panelFader.Fade();
    }
}

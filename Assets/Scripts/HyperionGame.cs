using System.Collections.Generic;
using UnityEngine;

/// <summary>Runtime race director and resolution-independent orbital racing interface.</summary>
public sealed class HyperionGame : MonoBehaviour
{
    private enum GameMode { Garage, Countdown, Racing, Paused, Finished }
    private struct DifficultySpec
    {
        public string Name, Subtitle;
        public float PaceMin, PaceMax, Catchup, MinBand, MaxBand, LaneResponse, Mistakes;
        public DifficultySpec(string name, string subtitle, float min, float max, float catchup,
            float minBand, float maxBand, float response, float mistakes)
        {
            Name = name; Subtitle = subtitle; PaceMin = min; PaceMax = max; Catchup = catchup;
            MinBand = minBand; MaxBand = maxBand; LaneResponse = response; Mistakes = mistakes;
        }
    }
    private static readonly DifficultySpec[] Difficulties =
    {
        new DifficultySpec("ROOKIE", "FIND YOUR LINE", .77f, .86f, .000075f, .90f, 1.08f, 1.25f, .18f),
        new DifficultySpec("SPORT", "CHASE THE PODIUM", .89f, .98f, .000032f, .94f, 1.05f, 1.65f, .08f),
        new DifficultySpec("APEX", "EARN EVERY METRE", .98f, 1.04f, .000010f, .97f, 1.025f, 2.05f, .025f)
    };
    private const int TotalLaps = 3, RivalCount = 6;
    private const float CanvasWidth = 1600f, CanvasHeight = 900f;
    private GameMode mode = GameMode.Garage, resumeMode = GameMode.Racing;
    private StellarTrack track;
    private StellarFollowCamera followCamera;
    private StellarRaceAudio raceAudio;
    private int countdownTick;
    private Transform vehiclesRoot;
    private GameObject playerObject;
    private ArcadeCarController player;
    private readonly List<StellarAICar> rivals = new List<StellarAICar>();
    private readonly List<float> lapTimes = new List<float>();
    private readonly List<Standing> standings = new List<Standing>();
    private struct Standing { public string Name; public float Progress, FinishTime; public bool Player, Finished; }
    private int selectedCar, selectedDifficulty = 1, finalPlace = 7, lastLapIndex;
    private bool timeTrial, showSettings, lapTimingStarted, recordSet, lastLapRecord;
    private float countdown, goFlash, raceTime, lapStartTime;
    private float bestSavedLap, lastSplit, splitFlash;
    private Vector2[] mapPoints;
    private Vector2 mapMin, mapMax;
    private Texture2D white;
    private GUIStyle logo, eyebrow, title, heading, body, small, number, centerNumber, centered, button, selectedButton, primary;
    private readonly Color cyan = new Color(.34f, .96f, .94f);
    private readonly Color violet = new Color(.65f, .45f, 1f);
    private readonly Color muted = new Color(.55f, .65f, .72f);
    private readonly Color panel = new Color(.025f, .045f, .065f, .95f);
    private readonly Color card = new Color(.07f, .105f, .135f, .95f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeGame()
    {
        if (FindObjectOfType<HyperionGame>() == null) new GameObject("HYPERION // V2").AddComponent<HyperionGame>();
    }
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Application.targetFrameRate = 120;
        Time.fixedDeltaTime = 1f / 60f;
        Time.maximumDeltaTime = .1f;
        QualitySettings.vSyncCount = 1;
        Physics.gravity = Vector3.zero;
        StellarSettings.Load();
        raceAudio = gameObject.AddComponent<StellarRaceAudio>();
        selectedCar = Mathf.Clamp(PlayerPrefs.GetInt("Hyperion.V2.Car", 0), 0, StellarCarCatalog.Cars.Length - 1);
        selectedDifficulty = Mathf.Clamp(PlayerPrefs.GetInt("Hyperion.V2.Difficulty", 1), 0, Difficulties.Length - 1);
        BuildWorld();
    }
    private void BuildWorld()
    {
        SpaceEnvironment.Build(transform);
        GameObject trackObject = new GameObject("Stellar Circuit");
        trackObject.transform.SetParent(transform, false);
        track = trackObject.AddComponent<StellarTrack>();
        track.Build();
        BuildMap();
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Race Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.backgroundColor = new Color(.003f, .006f, .018f);
        camera.allowHDR = true;
        followCamera = camera.GetComponent<StellarFollowCamera>();
        if (followCamera == null) followCamera = camera.gameObject.AddComponent<StellarFollowCamera>();
        BuildVehicles(true);
    }
    private void BuildVehicles(bool showroom)
    {
        if (vehiclesRoot != null)
        {
            vehiclesRoot.gameObject.SetActive(false);
            Destroy(vehiclesRoot.gameObject);
        }
        vehiclesRoot = new GameObject("Race Vehicles").transform;
        vehiclesRoot.SetParent(transform, false);
        rivals.Clear();
        StellarCarSpec spec = StellarCarCatalog.Cars[selectedCar];
        playerObject = StellarCarFactory.CreateCar(spec, "YOU // " + spec.DisplayName, true);
        playerObject.transform.SetParent(vehiclesRoot, true);
        player = playerObject.AddComponent<ArcadeCarController>();
        float playerStart = showroom ? .001f : -(timeTrial ? 7f : 28f) / track.TrackLength;
        player.Initialize(track, spec, playerStart);
        playerObject.AddComponent<AudioSource>();
        playerObject.AddComponent<StellarEngineAudio>();
        if (!showroom && !timeTrial)
        {
            DifficultySpec difficulty = Difficulties[selectedDifficulty];
            Color[] colors = { new Color(1f,.13f,.28f), cyan, new Color(1f,.72f,.2f), violet, new Color(.2f,1f,.55f), new Color(1f,.4f,.12f) };
            for (int i = 0; i < RivalCount; i++)
            {
                StellarCarSpec baseSpec = StellarCarCatalog.Cars[(selectedCar + i + 1) % StellarCarCatalog.Cars.Length];
                StellarCarSpec rivalSpec = new StellarCarSpec(baseSpec.Id, baseSpec.DisplayName, baseSpec.ClassName,
                    baseSpec.Description, colors[i], Color.Lerp(colors[i], Color.white, .6f), baseSpec.TopSpeedKph,
                    baseSpec.Acceleration, baseSpec.Handling, baseSpec.Nitro, baseSpec.BodyStyle);
                GameObject rivalObject = StellarCarFactory.CreateCar(rivalSpec, "RIVAL " + (i + 1), false);
                rivalObject.transform.SetParent(vehiclesRoot, true);
                StellarAICar ai = rivalObject.AddComponent<StellarAICar>();
                float start = -(21f - (i / 2) * 7f) / track.TrackLength;
                float lane = i % 2 == 0 ? -.42f : .42f;
                float pace = Mathf.Lerp(difficulty.PaceMin, difficulty.PaceMax, Hash01(i * 19 + selectedDifficulty * 7));
                ai.Initialize(track, rivalSpec, player, start, lane, pace, difficulty.Catchup,
                    difficulty.MinBand, difficulty.MaxBand, difficulty.LaneResponse, difficulty.Mistakes,
                    (Hash01(i * 31 + 8) - .5f) * 2f);
                ai.SetRunning(false);
                rivals.Add(ai);
            }
        }
        bestSavedLap = PlayerPrefs.GetFloat(BestLapKey(), 0f);
        if (showroom)
        {
            mode = GameMode.Garage;
            player.SetDriving(false);
            followCamera.SetTarget(playerObject.transform, true);
            followCamera.SnapToCurrentMode();
        }
    }
    private string BestLapKey() { return "Hyperion.V2.VesperRing.BestLap." + StellarCarCatalog.Cars[selectedCar].Id; }
    private static float Hash01(int value) { return Mathf.Repeat(Mathf.Sin(value * 91.3458f + 17.17f) * 47453.5453f, 1f); }
    private void StartRace()
    {
        showSettings = false;
        PlayerPrefs.SetInt("Hyperion.V2.Car", selectedCar);
        PlayerPrefs.SetInt("Hyperion.V2.Difficulty", selectedDifficulty);
        PlayerPrefs.Save();
        BuildVehicles(false);
        raceTime = lapStartTime = 0f;
        lapTimingStarted = recordSet = false;
        lastLapIndex = 0;
        lapTimes.Clear();
        finalPlace = rivals.Count + 1;
        countdown = 3f; countdownTick = 3; raceAudio.Countdown(); goFlash = splitFlash = 0f;
        mode = GameMode.Countdown;
        player.SetDriving(false);
        foreach (StellarAICar rival in rivals) rival.SetRunning(false);
        followCamera.SetTarget(playerObject.transform, false);
        followCamera.SnapToCurrentMode();
        Cursor.visible = false;
    }
    private void Update()
    {
        if (mode != GameMode.Paused)
        {
            goFlash = Mathf.Max(0f, goFlash - Time.deltaTime);
            splitFlash = Mathf.Max(0f, splitFlash - Time.deltaTime);
        }
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (mode == GameMode.Racing || mode == GameMode.Countdown) Pause();
            else if (mode == GameMode.Paused) Resume();
            else if (showSettings) { showSettings = false; StellarSettings.Save(); }
        }
        if (Input.GetKeyDown(KeyCode.R) && (mode == GameMode.Racing || mode == GameMode.Countdown)) StartRace();
        if (Input.GetKeyDown(KeyCode.Return) && mode == GameMode.Garage && !showSettings) StartRace();
        if (mode == GameMode.Countdown)
        {
            countdown -= Time.deltaTime;
            int tick = Mathf.CeilToInt(countdown);
            if (tick > 0 && tick < countdownTick) { countdownTick = tick; raceAudio.Countdown(); }
            if (countdown <= 0f)
            {
                mode = GameMode.Racing;
                goFlash = .9f;
                raceAudio.Go();
                player.SetDriving(true);
                foreach (StellarAICar rival in rivals) rival.SetRunning(true);
            }
        }
        else if (mode == GameMode.Racing) UpdateRace();
    }
    private void UpdateRace()
    {
        raceTime += Time.deltaTime;
        foreach (StellarAICar rival in rivals)
            if (!rival.Finished && rival.TotalProgress >= TotalLaps) rival.MarkFinished(raceTime);
        float progress = player.TotalProgress;
        if (!lapTimingStarted && progress >= 0f)
        {
            // Grid run-up belongs to the race time, but never to the saved flying lap.
            lapTimingStarted = true;
            lapStartTime = raceTime;
        }
        int lapIndex = Mathf.Clamp(Mathf.FloorToInt(progress), 0, TotalLaps);
        if (lapTimingStarted && lapIndex > lastLapIndex)
        {
            RecordLap(raceTime - lapStartTime);
            lapStartTime = raceTime;
            lastLapIndex = lapIndex;
        }
        if (progress >= TotalLaps)
        {
            finalPlace = GetPlace();
            player.SetDriving(false);
            foreach (StellarAICar rival in rivals) rival.SetRunning(false);
            mode = GameMode.Finished;
            raceAudio.Finish();
            Cursor.visible = true;
            PlayerPrefs.Save();
        }
    }
    private void RecordLap(float duration)
    {
        if (duration <= 0f) return;
        lapTimes.Add(duration);
        if (lapTimes.Count < TotalLaps) raceAudio.Lap();
        lastSplit = bestSavedLap > 0f ? duration - bestSavedLap : 0f;
        splitFlash = 3.5f;
        lastLapRecord = bestSavedLap <= 0f || duration < bestSavedLap;
        if (lastLapRecord)
        {
            bestSavedLap = duration;
            recordSet = true;
            PlayerPrefs.SetFloat(BestLapKey(), bestSavedLap);
            PlayerPrefs.Save();
        }
    }
    private void Pause()
    {
        resumeMode = mode;
        mode = GameMode.Paused;
        player.SetPaused(true);
        foreach (StellarAICar rival in rivals) rival.SetPaused(true);
        Cursor.visible = true;
    }
    private void Resume()
    {
        showSettings = false;
        StellarSettings.Save();
        mode = resumeMode;
        player.SetPaused(false);
        foreach (StellarAICar rival in rivals) rival.SetPaused(false);
        Cursor.visible = false;
    }
    private void ReturnToGarage()
    {
        showSettings = false;
        StellarSettings.Save();
        BuildVehicles(true);
        Cursor.visible = true;
    }
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && (mode == GameMode.Racing || mode == GameMode.Countdown)) Pause();
    }
    private int GetPlace()
    {
        if (mode == GameMode.Finished) return finalPlace;
        int place = 1;
        foreach (StellarAICar rival in rivals)
            if (rival.Finished || rival.TotalProgress > player.TotalProgress) place++;
        return place;
    }
    private void OnGUI()
    {
        EnsureStyles();
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        float scale = Mathf.Max(.05f, Mathf.Min(Screen.width / CanvasWidth, Screen.height / CanvasHeight));
        Vector3 offset = new Vector3((Screen.width - CanvasWidth * scale) * .5f, (Screen.height - CanvasHeight * scale) * .5f, 0f);
        GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one * scale);
        bool wasEnabled = GUI.enabled;
        if (showSettings) GUI.enabled = false;
        if (mode == GameMode.Garage) DrawGarage();
        else
        {
            DrawHud();
            if (mode == GameMode.Countdown) DrawCountdown();
            if (goFlash > 0f) CenterFlash("GO", cyan, goFlash / .9f);
            if (mode == GameMode.Paused) DrawPause();
            if (mode == GameMode.Finished) DrawResults();
        }
        GUI.enabled = wasEnabled;
        if (showSettings) DrawSettings();
        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }
    private void DrawGarage()
    {
        Fill(new Rect(24, 24, 556, 852), panel);
        Fill(new Rect(24, 24, 4, 852), cyan);
        DrawLogo(52, 47);
        Label(new Rect(52, 110, 490, 21), "V2  /  ORBITAL MOTORSPORT", eyebrow);
        Label(new Rect(50, 142, 500, 100), "OWN THE\nIMPOSSIBLE.", title);
        Label(new Rect(52, 257, 480, 28), "01  /  SELECT YOUR MACHINE", eyebrow);
        for (int i = 0; i < StellarCarCatalog.Cars.Length; i++)
        {
            StellarCarSpec car = StellarCarCatalog.Cars[i];
            Rect r = new Rect(52 + i % 2 * 246, 291 + i / 2 * 61, 236, 52);
            if (GUI.Button(r, car.DisplayName + "   /   " + Mathf.RoundToInt(car.TopSpeedKph), i == selectedCar ? selectedButton : button))
            {
                selectedCar = i;
                raceAudio.Select();
                BuildVehicles(true);
            }
        }
        StellarCarSpec selected = StellarCarCatalog.Cars[selectedCar];
        Label(new Rect(52, 425, 480, 20), selected.ClassName, eyebrow);
        Label(new Rect(52, 449, 480, 48), selected.DisplayName, heading);
        Label(new Rect(52, 503, 478, 44), selected.Description, body);
        Stat(new Rect(52, 566, 224, 6), "ACCELERATION", selected.Acceleration / 10f);
        Stat(new Rect(302, 566, 224, 6), "HANDLING", selected.Handling / 10f);
        Label(new Rect(52, 594, 470, 20), "02  /  CHOOSE YOUR EVENT", eyebrow);
        if (GUI.Button(new Rect(52, 626, 236, 42), "GRAND PRIX", !timeTrial ? selectedButton : button)) timeTrial = false;
        if (GUI.Button(new Rect(298, 626, 236, 42), "TIME ATTACK", timeTrial ? selectedButton : button)) timeTrial = true;
        for (int i = 0; i < Difficulties.Length; i++)
        {
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && !timeTrial;
            if (GUI.Button(new Rect(52 + i * 164, 679, 154, 43), Difficulties[i].Name, selectedDifficulty == i ? selectedButton : button)) selectedDifficulty = i;
            GUI.enabled = oldEnabled;
        }
        if (GUI.Button(new Rect(52, 741, 482, 65), "LAUNCH  /  " + (timeTrial ? "TIME ATTACK" : "GRAND PRIX") + "   >", primary)) StartRace();
        if (GUI.Button(new Rect(52, 820, 150, 33), "SETTINGS", button)) showSettings = true;
        Label(new Rect(219, 821, 195, 31), "ENTER TO LAUNCH", small);
#if !UNITY_EDITOR && !UNITY_WEBGL
        if (GUI.Button(new Rect(422, 820, 112, 33), "QUIT", button)) Application.Quit();
#endif
        Label(new Rect(1035, 50, 515, 22), "VESPER RING   /   DEEP ORBIT", eyebrow);
        Label(new Rect(1035, 80, 515, 32), (track.TrackLength / 1000f).ToString("0.00") + " KM   /   " + (timeTrial ? "SOLO RUN" : "7 CAR GRID"), body);
        Fill(new Rect(1085, 579, 465, 250), new Color(.025f,.045f,.065f,.72f));
        Label(new Rect(1110, 598, 410, 24), "CIRCUIT INTELLIGENCE", eyebrow);
        DrawMap(new Rect(1105, 641, 230, 166), false);
        Label(new Rect(1360, 650, 170, 20), "PERSONAL BEST", small);
        Label(new Rect(1360, 678, 175, 30), bestSavedLap > 0f ? FormatTime(bestSavedLap) : "SET A LAP", centered);
        Label(new Rect(1360, 727, 170, 56), "Banked corners.\nNo gravity.\nNo excuses.", body);
        Label(new Rect(610, 842, 940, 30), "WASD  DRIVE    SHIFT / SPACE  BOOST    CTRL  DRIFT    C  CAMERA    F  RECOVER", small);
    }
    private void DrawLogo(float x, float y)
    {
        Fill(new Rect(x, y + 5, 6, 30), cyan);
        Fill(new Rect(x + 12, y + 5, 2, 30), cyan);
        Label(new Rect(x + 27, y, 350, 43), "HYPERION", logo);
    }
    private void DrawHud()
    {
        Fill(new Rect(28, 28, 282, 146), panel);
        Fill(new Rect(28, 28, 3, 146), cyan);
        Label(new Rect(47, 40, 240, 20), timeTrial ? "TIME ATTACK" : "GRAND PRIX", eyebrow);
        Label(new Rect(43, 64, 100, 83), timeTrial ? "TT" : GetPlace().ToString("00"), number);
        Label(new Rect(158, 85, 135, 22), timeTrial ? "SOLO SESSION" : "/ 07  POSITION", small);
        Label(new Rect(158, 118, 135, 22), "LAP " + Mathf.Clamp(Mathf.FloorToInt(player.TotalProgress) + 1, 1, TotalLaps) + " / 03", eyebrow);
        Fill(new Rect(610, 28, 380, 88), panel);
        Label(new Rect(635, 40, 330, 20), "RACE TIME", eyebrow);
        Label(new Rect(630, 65, 340, 36), FormatTime(raceTime), centered);
        float progress = Mathf.Clamp01(player.TotalProgress / TotalLaps);
        Fill(new Rect(610, 112, 380, 4), card);
        Fill(new Rect(610, 112, 380 * progress, 4), cyan);
        Fill(new Rect(1290, 28, 282, 146), panel);
        Label(new Rect(1310, 41, 240, 20), "CURRENT LAP", small);
        Label(new Rect(1310, 64, 240, 28), lapTimingStarted ? FormatTime(raceTime - lapStartTime) : "GRID RUN-UP", centered);
        Label(new Rect(1310, 110, 240, 20), "BEST  /  " + (bestSavedLap > 0f ? FormatTime(bestSavedLap) : "--:--.---"), eyebrow);
        DrawLeaderboard();
        Fill(new Rect(28, 628, 258, 222), panel);
        Label(new Rect(48, 642, 220, 22), "VESPER RING / LIVE", eyebrow);
        DrawMap(new Rect(46, 676, 220, 156), true);
        Fill(new Rect(1248, 636, 324, 214), panel);
        Color active = player.IsBoosting ? cyan : Color.white;
        Label(new Rect(1272, 647, 280, 80), Mathf.RoundToInt(player.SpeedKph).ToString("000"), number, active);
        Label(new Rect(1456, 705, 90, 20), "KM/H", small);
        Label(new Rect(1274, 745, 275, 24), player.IsBoosting ? "BOOST ENGAGED" : player.IsDrifting ? "DRIFT / RECHARGING" : "NITRO RESERVE", eyebrow, player.IsDrifting ? violet : cyan);
        for (int i = 0; i < 20; i++)
            Fill(new Rect(1276 + i * 13, 782, 9, 29), player.Nitro >= (i + 1) * 5f ? cyan : card);
        Label(new Rect(318, 856, 925, 22), "SHIFT / SPACE  BOOST    CTRL  DRIFT    C  CAMERA    F  RECOVER    R  RESTART    ESC  PAUSE", small);
        if (splitFlash > 0f && lapTimes.Count > 0)
        {
            Fill(new Rect(608, 148, 384, 71), panel);
            Label(new Rect(625, 158, 350, 22), "LAP " + lapTimes.Count + "  /  " + FormatTime(lapTimes[lapTimes.Count - 1]), centered);
            Label(new Rect(625, 187, 350, 22), lastLapRecord ? "NEW PERSONAL BEST" : (lastSplit <= 0f ? "-" : "+") + Mathf.Abs(lastSplit).ToString("0.000") + " TO PERSONAL BEST", small, lastLapRecord || lastSplit <= 0f ? cyan : new Color(1f,.6f,.35f));
        }
    }
    private void DrawLeaderboard()
    {
        if (timeTrial) return;
        standings.Clear();
        standings.Add(new Standing { Name = "YOU", Progress = player.TotalProgress, Player = true, Finished = mode == GameMode.Finished, FinishTime = raceTime });
        for (int i = 0; i < rivals.Count; i++)
            standings.Add(new Standing { Name = "PILOT " + (i + 1).ToString("00"), Progress = rivals[i].TotalProgress, Finished = rivals[i].Finished, FinishTime = rivals[i].FinishTime });
        standings.Sort((a, b) =>
        {
            if (a.Finished && b.Finished) return a.FinishTime.CompareTo(b.FinishTime);
            if (a.Finished != b.Finished) return a.Finished ? -1 : 1;
            return b.Progress.CompareTo(a.Progress);
        });
        Fill(new Rect(28, 194, 258, 284), panel);
        Label(new Rect(47, 206, 220, 23), "RACE ORDER", eyebrow);
        for (int i = 0; i < standings.Count; i++)
        {
            Standing s = standings[i];
            float y = 241 + i * 32;
            if (s.Player) Fill(new Rect(40, y - 1, 234, 29), new Color(.08f,.25f,.28f,.9f));
            Label(new Rect(48, y, 35, 23), (i + 1).ToString("00"), small, s.Player ? cyan : muted);
            Label(new Rect(89, y, 98, 23), s.Name, small, s.Player ? Color.white : muted);
            float gap = (s.Progress - player.TotalProgress) * track.TrackLength;
            Label(new Rect(184, y, 83, 23), s.Player ? "" : (gap > 0f ? "+" : "-") + Mathf.Abs(gap).ToString("0") + "m", small);
        }
    }
    private void DrawCountdown()
    {
        Fill(new Rect(622, 300, 356, 285), new Color(.025f,.045f,.065f,.82f));
        Label(new Rect(635, 324, 330, 25), "SYSTEMS ARMED / STAND BY", eyebrow);
        Label(new Rect(635, 356, 330, 178), Mathf.Max(1, Mathf.CeilToInt(countdown)).ToString(), centerNumber, cyan);
        Label(new Rect(635, 543, 330, 24), "W / UP  TO ACCELERATE", centered);
    }
    private void CenterFlash(string text, Color color, float alpha)
    {
        Color c = color; c.a = Mathf.Clamp01(alpha);
        Label(new Rect(550, 300, 500, 220), text, centerNumber, c);
    }
    private void DrawPause()
    {
        Shade();
        Fill(new Rect(545, 196, 510, 510), panel);
        Label(new Rect(580, 232, 440, 23), "FLIGHT CONTROL / ON HOLD", eyebrow);
        Label(new Rect(577, 275, 450, 60), "RACE PAUSED", heading);
        if (GUI.Button(new Rect(580, 367, 440, 61), "RESUME RACE   >", primary)) Resume();
        if (GUI.Button(new Rect(580, 441, 440, 48), "RESTART EVENT", button)) StartRace();
        if (GUI.Button(new Rect(580, 501, 440, 48), "SETTINGS", button)) showSettings = true;
        if (GUI.Button(new Rect(580, 561, 440, 48), "RETURN TO GARAGE", button)) ReturnToGarage();
        Label(new Rect(580, 641, 440, 24), "ESC TO RESUME", small);
    }
    private void DrawResults()
    {
        Shade();
        Fill(new Rect(462, 87, 676, 726), panel);
        Fill(new Rect(462, 87, 676, 4), cyan);
        Label(new Rect(505, 116, 590, 22), "VESPER RING / OFFICIAL CLASSIFICATION", eyebrow);
        Label(new Rect(505, 149, 590, 145), timeTrial ? "COMPLETE" : finalPlace.ToString("00") + " / 07", centerNumber, cyan);
        Label(new Rect(505, 305, 590, 42), timeTrial ? "TIME ATTACK COMPLETE" : finalPlace == 1 ? "ORBIT BELONGS TO YOU." : finalPlace <= 3 ? "PODIUM SECURED." : "RACE COMPLETE.", centered);
        Label(new Rect(505, 354, 590, 25), recordSet ? "NEW PERSONAL LAP RECORD" : "TELEMETRY LOGGED / READY FOR ANOTHER RUN", eyebrow, recordSet ? cyan : muted);
        Cell(new Rect(505, 406, 284, 70), "EVENT TIME", FormatTime(raceTime));
        Cell(new Rect(809, 406, 284, 70), "PERSONAL BEST", bestSavedLap > 0f ? FormatTime(bestSavedLap) : "--:--.---");
        for (int i = 0; i < lapTimes.Count; i++)
        {
            Label(new Rect(514 + i * 196, 493, 180, 23), "LAP " + (i + 1), small);
            Label(new Rect(514 + i * 196, 519, 180, 27), FormatTime(lapTimes[i]), centered);
        }
        if (GUI.Button(new Rect(505, 596, 588, 63), "RACE AGAIN   >", primary)) StartRace();
        if (GUI.Button(new Rect(505, 674, 588, 48), "RETURN TO GARAGE", button)) ReturnToGarage();
        Label(new Rect(505, 750, 588, 24), StellarCarCatalog.Cars[selectedCar].DisplayName + "  /  " + (timeTrial ? "SOLO" : Difficulties[selectedDifficulty].Name), small);
    }
    private void DrawSettings()
    {
        float oldVolume = StellarSettings.MasterVolume, oldShake = StellarSettings.Shake;
        bool oldQuality = StellarSettings.HighQuality;
        Shade();
        Fill(new Rect(500, 168, 600, 564), panel);
        Label(new Rect(545, 204, 510, 23), "FLIGHT CONTROL / PREFERENCES", eyebrow);
        Label(new Rect(542, 245, 510, 50), "TUNE YOUR EXPERIENCE", heading);
        Label(new Rect(545, 324, 510, 22), "MASTER VOLUME  /  " + Mathf.RoundToInt(StellarSettings.MasterVolume * 100) + "%", body);
        StellarSettings.MasterVolume = GUI.HorizontalSlider(new Rect(549, 365, 495, 24), StellarSettings.MasterVolume, 0f, 1f);
        Label(new Rect(545, 405, 510, 22), "CAMERA SHAKE  /  " + Mathf.RoundToInt(StellarSettings.Shake * 100) + "%", body);
        StellarSettings.Shake = GUI.HorizontalSlider(new Rect(549, 446, 495, 24), StellarSettings.Shake, 0f, 1f);
        if (GUI.Button(new Rect(545, 496, 510, 48), "VISUAL QUALITY  /  " + (StellarSettings.HighQuality ? "HIGH" : "PERFORMANCE"), button)) StellarSettings.HighQuality = !StellarSettings.HighQuality;
        if (oldVolume != StellarSettings.MasterVolume || oldShake != StellarSettings.Shake || oldQuality != StellarSettings.HighQuality) StellarSettings.Apply();
        if (GUI.Button(new Rect(545, 587, 510, 61), "SAVE & RETURN   >", primary)) { StellarSettings.Save(); showSettings = false; }
        Label(new Rect(545, 676, 510, 22), "SETTINGS ARE SAVED ON THIS COMPUTER", small);
    }
    private void BuildMap()
    {
        mapPoints = new Vector2[121];
        mapMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        mapMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < mapPoints.Length; i++)
        {
            track.GetFrame(i / (float)(mapPoints.Length - 1), out Vector3 c, out Vector3 t, out Vector3 r, out Vector3 u);
            mapPoints[i] = new Vector2(c.x, c.z);
            mapMin = Vector2.Min(mapMin, mapPoints[i]);
            mapMax = Vector2.Max(mapMax, mapPoints[i]);
        }
    }
    private Vector2 MapPoint(Vector3 world, Rect rect)
    {
        Vector2 size = mapMax - mapMin;
        float scale = Mathf.Min(rect.width / Mathf.Max(1f, size.x), rect.height / Mathf.Max(1f, size.y));
        Vector2 center = (mapMin + mapMax) * .5f;
        return rect.center + new Vector2(world.x - center.x, -(world.z - center.y)) * scale;
    }
    private void DrawMap(Rect rect, bool live)
    {
        if (mapPoints == null) return;
        for (int i = 1; i < mapPoints.Length; i++)
        {
            Vector2 a = MapPoint(new Vector3(mapPoints[i - 1].x, 0, mapPoints[i - 1].y), rect);
            Vector2 b = MapPoint(new Vector3(mapPoints[i].x, 0, mapPoints[i].y), rect);
            Line(a, b, 3f, new Color(.35f,.48f,.55f));
        }
        track.GetFrame(0f, out Vector3 start, out Vector3 t, out Vector3 r, out Vector3 u);
        Vector2 p = MapPoint(start, rect);
        Fill(new Rect(p.x - 3, p.y - 3, 6, 6), Color.white);
        if (!live || player == null) return;
        foreach (StellarAICar rival in rivals)
        {
            Vector2 dot = MapPoint(rival.transform.position, rect);
            Fill(new Rect(dot.x - 2, dot.y - 2, 4, 4), violet);
        }
        Vector2 you = MapPoint(player.transform.position, rect);
        Fill(new Rect(you.x - 5, you.y - 5, 10, 10), new Color(.015f,.025f,.035f));
        Fill(new Rect(you.x - 3, you.y - 3, 6, 6), cyan);
    }
    private void Line(Vector2 a, Vector2 b, float width, Color color)
    {
        Matrix4x4 old = GUI.matrix;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg, a);
        Fill(new Rect(a.x, a.y - width * .5f, Vector2.Distance(a, b), width), color);
        GUI.matrix = old;
    }
    private void Stat(Rect rect, string name, float value)
    {
        Label(new Rect(rect.x, rect.y - 26, rect.width, 22), name, small);
        Fill(rect, card);
        Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), cyan);
    }
    private void Cell(Rect rect, string name, string value)
    {
        Fill(rect, card);
        Label(new Rect(rect.x + 16, rect.y + 7, rect.width - 32, 21), name, small);
        Label(new Rect(rect.x + 16, rect.y + 34, rect.width - 32, 29), value, centered);
    }
    private void Shade() { Fill(new Rect(-2000, -2000, 6000, 6000), new Color(.005f,.01f,.02f,.87f)); }
    private void Fill(Rect rect, Color color)
    {
        Color old = GUI.color; GUI.color = color;
        GUI.DrawTexture(rect, white);
        GUI.color = old;
    }
    private void Label(Rect rect, string text, GUIStyle style) { GUI.Label(rect, text, style); }
    private void Label(Rect rect, string text, GUIStyle style, Color color)
    {
        Color old = style.normal.textColor;
        style.normal.textColor = color;
        GUI.Label(rect, text, style);
        style.normal.textColor = old;
    }
    private static string FormatTime(float time)
    {
        time = Mathf.Max(0f, time);
        int minutes = Mathf.FloorToInt(time / 60f), seconds = Mathf.FloorToInt(time % 60f);
        int ms = Mathf.FloorToInt((time - Mathf.Floor(time)) * 1000f);
        return minutes.ToString("00") + ":" + seconds.ToString("00") + "." + ms.ToString("000");
    }
    private void EnsureStyles()
    {
        if (white != null) return;
        white = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "Hyperion UI", hideFlags = HideFlags.DontSave };
        white.SetPixel(0, 0, Color.white); white.Apply();
        logo = Style(34, FontStyle.BoldAndItalic, TextAnchor.MiddleLeft, Color.white);
        eyebrow = Style(13, FontStyle.Bold, TextAnchor.MiddleLeft, cyan);
        title = Style(44, FontStyle.BoldAndItalic, TextAnchor.UpperLeft, Color.white);
        title.wordWrap = false;
        heading = Style(28, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        body = Style(16, FontStyle.Normal, TextAnchor.UpperLeft, muted); body.wordWrap = true;
        small = Style(12, FontStyle.Bold, TextAnchor.MiddleLeft, muted);
        number = Style(68, FontStyle.BoldAndItalic, TextAnchor.UpperLeft, Color.white);
        centerNumber = Style(90, FontStyle.BoldAndItalic, TextAnchor.MiddleCenter, Color.white);
        centered = Style(21, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        button = ButtonStyle(card, Color.white, 14);
        selectedButton = ButtonStyle(new Color(.09f,.28f,.30f), cyan, 14);
        primary = ButtonStyle(new Color(.21f,.72f,.70f), new Color(.01f,.04f,.055f), 20);
    }
    private static GUIStyle Style(int size, FontStyle weight, TextAnchor align, Color color)
    {
        GUIStyle s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = weight, alignment = align, richText = false };
        s.normal.textColor = color;
        return s;
    }
    private static GUIStyle ButtonStyle(Color color, Color text, int size)
    {
        Texture2D normal = Solid(color), hover = Solid(Color.Lerp(color, Color.white, .12f));
        GUIStyle s = new GUIStyle(GUI.skin.button)
        {
            fontSize = size, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(8,8,6,6), border = new RectOffset(0,0,0,0), richText = false
        };
        s.normal.background = normal; s.hover.background = hover; s.active.background = hover; s.focused.background = normal;
        s.normal.textColor = text; s.hover.textColor = text; s.active.textColor = text; s.focused.textColor = text;
        return s;
    }
    private static Texture2D Solid(Color color)
    {
        Texture2D t = new Texture2D(1,1,TextureFormat.RGBA32,false) { hideFlags = HideFlags.DontSave };
        t.SetPixel(0,0,color); t.Apply(); return t;
    }
}

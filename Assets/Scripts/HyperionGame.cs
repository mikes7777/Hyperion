using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime bootstrap, race director, garage, and IMGUI presentation for Stellar Apex.
/// Drop the project into Unity and press Play: no prefabs or scene references are required.
/// </summary>
public sealed class StellarApexGame : MonoBehaviour
{
    private enum GameMode { Garage, Countdown, Racing, Paused, Finished }

    private struct DifficultySpec
    {
        public string Name;
        public string Subtitle;
        public float PaceMin, PaceMax, Catchup, MinBand, MaxBand, LaneResponse, Mistakes;

        public DifficultySpec(string name, string subtitle, float paceMin, float paceMax,
            float catchup, float minBand, float maxBand, float laneResponse, float mistakes)
        {
            Name = name; Subtitle = subtitle; PaceMin = paceMin; PaceMax = paceMax;
            Catchup = catchup; MinBand = minBand; MaxBand = maxBand;
            LaneResponse = laneResponse; Mistakes = mistakes;
        }
    }

    private static readonly DifficultySpec[] Difficulties =
    {
        new DifficultySpec("ROOKIE", "FORGIVING", 0.79f, 0.90f, 0.000075f, 0.82f, 1.12f, 1.25f, 0.24f),
        new DifficultySpec("SPORT", "COMPETITIVE", 0.91f, 1.005f, 0.000032f, 0.89f, 1.08f, 1.65f, 0.11f),
        new DifficultySpec("APEX", "RELENTLESS", 0.985f, 1.065f, 0.000010f, 0.95f, 1.045f, 2.05f, 0.035f)
    };

    private const int TotalLaps = 3;
    private const int RivalCount = 6;

    private GameMode mode = GameMode.Garage;
    private GameMode resumeMode = GameMode.Racing;
    private StellarTrack track;
    private StellarFollowCamera followCamera;
    private Transform vehiclesRoot;
    private GameObject playerObject;
    private ArcadeCarController player;
    private readonly List<StellarAICar> rivals = new List<StellarAICar>();
    private int selectedCar;
    private int selectedDifficulty = 1;
    private float countdown;
    private float goFlash;
    private float raceTime;
    private float lapStartTime;
    private int lastLapIndex;
    private readonly List<float> lapTimes = new List<float>();
    private int finalPlace = 7;

    private GUIStyle logoStyle, overlineStyle, heroStyle, bodyStyle, carButtonStyle, selectedCarButtonStyle;
    private GUIStyle buttonStyle, selectedButtonStyle, launchStyle, hudBigStyle, hudLabelStyle;
    private GUIStyle centerTitleStyle, centerBodyStyle, resultPlaceStyle;
    private Texture2D panelTexture, cardTexture, buttonTexture, selectedTexture, launchTexture, lineTexture, blackTexture;
    private readonly Color cyan = new Color(0.25f, 0.94f, 1f, 1f);
    private readonly Color violet = new Color(0.43f, 0.24f, 1f, 1f);
    private readonly Color muted = new Color(0.58f, 0.63f, 0.73f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeGame()
    {
        if (FindObjectOfType<StellarApexGame>() != null) return;
        GameObject root = new GameObject("STELLAR APEX // Runtime");
        root.AddComponent<StellarApexGame>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Application.targetFrameRate = 120;
        QualitySettings.vSyncCount = 0;
        Physics.gravity = Vector3.zero;
        BuildWorld();
    }

    private void BuildWorld()
    {
        SpaceEnvironment.Build(transform);

        GameObject trackObject = new GameObject("Stellar Circuit");
        trackObject.transform.SetParent(transform, false);
        track = trackObject.AddComponent<StellarTrack>();
        track.Build();

        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Race Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.002f, 0.003f, 0.012f, 1f);
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

        StellarCarSpec playerSpec = StellarCarCatalog.Cars[selectedCar];
        playerObject = StellarCarFactory.CreateCar(playerSpec, "PLAYER // " + playerSpec.DisplayName, true);
        playerObject.transform.SetParent(vehiclesRoot, true);
        player = playerObject.AddComponent<ArcadeCarController>();
        player.Initialize(track, playerSpec, 0f);
        playerObject.AddComponent<AudioSource>();
        playerObject.AddComponent<StellarEngineAudio>();

        DifficultySpec difficulty = Difficulties[selectedDifficulty];
        float[] lanes = { -0.62f, 0.45f, -0.18f, 0.68f, -0.44f, 0.15f };
        Color[] rivalColors =
        {
            new Color(1f,.08f,.32f), new Color(.04f,.82f,1f), new Color(1f,.76f,.05f),
            new Color(.52f,.20f,1f), new Color(.08f,.93f,.49f), new Color(1f,.26f,.06f)
        };

        for (int i = 0; i < RivalCount; i++)
        {
            StellarCarSpec baseSpec = StellarCarCatalog.Cars[(selectedCar + i + 1) % StellarCarCatalog.Cars.Length];
            StellarCarSpec rivalSpec = CloneWithColor(baseSpec, rivalColors[i]);
            GameObject rivalObject = StellarCarFactory.CreateCar(rivalSpec, "RIVAL " + (i + 1) + " // " + baseSpec.DisplayName, false);
            rivalObject.transform.SetParent(vehiclesRoot, true);
            StellarAICar ai = rivalObject.AddComponent<StellarAICar>();
            float pace = Mathf.Lerp(difficulty.PaceMin, difficulty.PaceMax, Hash01(i * 19 + selectedDifficulty * 7));
            ai.Initialize(track, rivalSpec, player, 0.009f + i * 0.008f, lanes[i], pace,
                difficulty.Catchup, difficulty.MinBand, difficulty.MaxBand,
                difficulty.LaneResponse, difficulty.Mistakes, (Hash01(i * 31 + 8) - 0.5f) * 2f);
            ai.SetRunning(false);
            rivalObject.SetActive(!showroom);
            rivals.Add(ai);
        }

        if (showroom)
        {
            player.SetDriving(false);
            player.WarpToProgress(0.001f, 0f);
            mode = GameMode.Garage;
            followCamera.SetTarget(playerObject.transform, true);
            followCamera.SnapToCurrentMode();
        }
    }

    private static StellarCarSpec CloneWithColor(StellarCarSpec source, Color color)
    {
        return new StellarCarSpec(source.Id, source.DisplayName, source.ClassName, source.Description,
            color, Color.Lerp(color, Color.white, 0.6f), source.TopSpeedKph,
            source.Acceleration, source.Handling, source.Nitro, source.BodyStyle);
    }

    private static float Hash01(int value)
    {
        return Mathf.Repeat(Mathf.Sin(value * 91.3458f + 17.17f) * 47453.5453f, 1f);
    }

    private void StartRace()
    {
        BuildVehicles(false);
        raceTime = 0f;
        lapStartTime = 0f;
        lastLapIndex = 0;
        lapTimes.Clear();
        finalPlace = RivalCount + 1;
        countdown = 3.25f;
        goFlash = 0f;
        mode = GameMode.Countdown;
        player.SetDriving(false);
        foreach (StellarAICar rival in rivals) rival.SetRunning(false);
        followCamera.SetTarget(playerObject.transform, false);
        followCamera.SnapToCurrentMode();
        Cursor.visible = true;
    }

    private void Update()
    {
        if (goFlash > 0f) goFlash -= Time.unscaledDeltaTime;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (mode == GameMode.Racing || mode == GameMode.Countdown) Pause();
            else if (mode == GameMode.Paused) Resume();
        }
        if (Input.GetKeyDown(KeyCode.R) && (mode == GameMode.Racing || mode == GameMode.Countdown)) StartRace();

        if (mode == GameMode.Countdown)
        {
            countdown -= Time.deltaTime;
            if (countdown <= 0f)
            {
                mode = GameMode.Racing;
                goFlash = 0.85f;
                player.SetDriving(true);
                foreach (StellarAICar rival in rivals) rival.SetRunning(true);
            }
        }
        else if (mode == GameMode.Racing)
        {
            UpdateRace();
        }
    }

    private void UpdateRace()
    {
        raceTime += Time.deltaTime;
        foreach (StellarAICar rival in rivals)
            if (!rival.Finished && rival.TotalProgress >= TotalLaps) rival.MarkFinished(raceTime);

        int lapIndex = Mathf.Clamp(Mathf.FloorToInt(player.TotalProgress), 0, TotalLaps - 1);
        if (lapIndex > lastLapIndex)
        {
            lapTimes.Add(raceTime - lapStartTime);
            lapStartTime = raceTime;
            lastLapIndex = lapIndex;
        }

        if (player.TotalProgress >= TotalLaps)
        {
            lapTimes.Add(raceTime - lapStartTime);
            finalPlace = 1;
            foreach (StellarAICar rival in rivals)
                if (rival.Finished || rival.TotalProgress > player.TotalProgress) finalPlace++;
            player.SetDriving(false);
            foreach (StellarAICar rival in rivals) rival.SetRunning(false);
            mode = GameMode.Finished;
        }
    }

    private void Pause()
    {
        resumeMode = mode;
        mode = GameMode.Paused;
        player.SetPaused(true);
        foreach (StellarAICar rival in rivals) rival.SetPaused(true);
    }

    private void Resume()
    {
        mode = resumeMode;
        player.SetPaused(false);
        foreach (StellarAICar rival in rivals) rival.SetPaused(false);
    }

    private void ReturnToGarage()
    {
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
            if (rival.TotalProgress > player.TotalProgress) place++;
        return place;
    }

    private void OnGUI()
    {
        EnsureStyles();
        float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.62f, 1.22f);
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
        float w = Screen.width / scale;
        float h = Screen.height / scale;

        if (mode == GameMode.Garage) DrawGarage(w, h);
        else
        {
            DrawLogo(45f, 34f);
            DrawHud(w, h);
            if (mode == GameMode.Countdown) DrawCountdown(w, h);
            if (goFlash > 0f) DrawCenteredFlash(w, h, "GO!", cyan, goFlash / 0.85f);
            if (mode == GameMode.Paused) DrawPause(w, h);
            if (mode == GameMode.Finished) DrawResults(w, h);
        }

        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }

    private void DrawGarage(float w, float h)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.24f);
        GUI.DrawTexture(new Rect(0f, 0f, w, h), blackTexture);
        GUI.color = Color.white;
        DrawLogo(54f, 35f);

        Rect panel = new Rect(50f, 92f, 650f, h - 142f);
        GUI.DrawTexture(panel, panelTexture);
        GUI.Label(new Rect(82f, 125f, 530f, 24f), "ORBITAL RACING DIVISION  //  GARAGE 01", overlineStyle);
        GUI.Label(new Rect(80f, 157f, 560f, 72f), "CHOOSE YOUR MACHINE.", heroStyle);
        GUI.Label(new Rect(82f, 225f, 540f, 48f), "Four hypercars. One impossible circuit.\nLeave gravity behind.", bodyStyle);

        GUI.Label(new Rect(82f, 288f, 300f, 22f), "01  SELECT CAR", overlineStyle);
        float cardWidth = 133f;
        for (int i = 0; i < StellarCarCatalog.Cars.Length; i++)
        {
            StellarCarSpec car = StellarCarCatalog.Cars[i];
            Rect card = new Rect(82f + i * (cardWidth + 8f), 318f, cardWidth, 84f);
            GUIStyle style = i == selectedCar ? selectedCarButtonStyle : carButtonStyle;
            if (GUI.Button(card, car.DisplayName + "\n" + Mathf.RoundToInt(car.TopSpeedKph) + " KM/H", style))
            {
                selectedCar = i;
                BuildVehicles(true);
            }
        }

        StellarCarSpec selected = StellarCarCatalog.Cars[selectedCar];
        GUI.Label(new Rect(82f, 430f, 300f, 18f), selected.ClassName, overlineStyle);
        GUI.Label(new Rect(80f, 452f, 520f, 50f), selected.DisplayName, centerTitleStyle);
        GUI.Label(new Rect(82f, 502f, 535f, 40f), selected.Description, bodyStyle);
        DrawStat(new Rect(82f, 557f, 250f, 12f), "TOP SPEED", Mathf.InverseLerp(290f, 350f, selected.TopSpeedKph), Mathf.RoundToInt(selected.TopSpeedKph).ToString());
        DrawStat(new Rect(360f, 557f, 250f, 12f), "ACCEL", selected.Acceleration / 10f, selected.Acceleration.ToString("0.0"));
        DrawStat(new Rect(82f, 608f, 250f, 12f), "HANDLING", selected.Handling / 10f, selected.Handling.ToString("0.0"));
        DrawStat(new Rect(360f, 608f, 250f, 12f), "NITRO", selected.Nitro / 10f, selected.Nitro.ToString("0.0"));

        float difficultyY = Mathf.Max(670f, h - 300f);
        GUI.Label(new Rect(82f, difficultyY, 300f, 22f), "02  NPC DIFFICULTY", overlineStyle);
        float difficultyWidth = 171f;
        for (int i = 0; i < Difficulties.Length; i++)
        {
            Rect button = new Rect(82f + i * (difficultyWidth + 8f), difficultyY + 29f, difficultyWidth, 60f);
            GUIStyle style = i == selectedDifficulty ? selectedButtonStyle : buttonStyle;
            if (GUI.Button(button, Difficulties[i].Name + "\n" + Difficulties[i].Subtitle, style))
            {
                selectedDifficulty = i;
                BuildVehicles(true);
            }
        }

        Rect launch = new Rect(82f, h - 134f, 528f, 72f);
        if (GUI.Button(launch, "3 LAPS  //  6 RIVALS\nLAUNCH RACE  →", launchStyle)) StartRace();
        GUI.Label(new Rect(w - 615f, h - 74f, 560f, 30f), "WASD / ARROWS  DRIVE       SPACE / SHIFT  NITRO       ESC  PAUSE", hudLabelStyle);
    }

    private void DrawLogo(float x, float y)
    {
        GUI.color = cyan;
        GUI.DrawTexture(new Rect(x, y + 4f, 7f, 27f), lineTexture);
        GUI.DrawTexture(new Rect(x + 12f, y + 4f, 2f, 27f), lineTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(x + 27f, y, 240f, 30f), "STELLAR APEX", logoStyle);
        GUI.Label(new Rect(x + 29f, y + 26f, 210f, 18f), "ORBITAL RACING // 01", overlineStyle);
    }

    private void DrawHud(float w, float h)
    {
        int place = GetPlace();
        int lap = Mathf.Clamp(Mathf.FloorToInt(player.TotalProgress) + 1, 1, TotalLaps);
        GUI.Label(new Rect(48f, 100f, 150f, 82f), place.ToString(), hudBigStyle);
        GUI.Label(new Rect(150f, 119f, 110f, 22f), "/ 7  POSITION", hudLabelStyle);
        GUI.Label(new Rect(150f, 145f, 140f, 24f), "LAP " + lap + " / " + TotalLaps, overlineStyle);

        GUI.Label(new Rect(w * 0.5f - 130f, 54f, 260f, 28f), FormatTime(raceTime), centerBodyStyle);
        Rect progress = new Rect(w * 0.5f - 245f, 91f, 490f, 4f);
        GUI.color = new Color(1f, 1f, 1f, 0.15f); GUI.DrawTexture(progress, lineTexture);
        GUI.color = cyan;
        float raceProgress = Mathf.Clamp01(player.TotalProgress / TotalLaps);
        GUI.DrawTexture(new Rect(progress.x, progress.y, progress.width * raceProgress, 4f), lineTexture);
        GUI.DrawTexture(new Rect(progress.x + progress.width * raceProgress - 5f, progress.y - 3f, 10f, 10f), lineTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(w - 270f, 96f, 215f, 78f), Mathf.RoundToInt(player.SpeedKph).ToString("000"), hudBigStyle);
        GUI.Label(new Rect(w - 151f, 151f, 95f, 20f), "KM/H", hudLabelStyle);
        GUI.Label(new Rect(w - 272f, 183f, 75f, 18f), "NITRO", hudLabelStyle);
        Rect nitro = new Rect(w - 195f, 188f, 140f, 7f);
        GUI.color = new Color(1f, 1f, 1f, .14f); GUI.DrawTexture(nitro, lineTexture);
        GUI.color = cyan; GUI.DrawTexture(new Rect(nitro.x, nitro.y, nitro.width * player.Nitro / 100f, nitro.height), lineTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(45f, h - 49f, 650f, 24f), "WASD / ARROWS  DRIVE    •    SPACE / SHIFT  NITRO    •    R  RESTART    •    ESC  PAUSE", hudLabelStyle);
    }

    private void DrawCountdown(float w, float h)
    {
        string value = Mathf.Max(1, Mathf.CeilToInt(countdown)).ToString();
        GUI.Label(new Rect(w * .5f - 160f, h * .5f - 145f, 320f, 250f), value, resultPlaceStyle);
    }

    private void DrawCenteredFlash(float w, float h, string text, Color color, float alpha)
    {
        GUI.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        GUI.Label(new Rect(w * .5f - 250f, h * .5f - 100f, 500f, 180f), text, resultPlaceStyle);
        GUI.color = Color.white;
    }

    private void DrawPause(float w, float h)
    {
        DrawModalShade(w, h);
        Rect panel = new Rect(w * .5f - 245f, h * .5f - 255f, 490f, 510f);
        GUI.DrawTexture(panel, panelTexture);
        GUI.Label(new Rect(panel.x + 44f, panel.y + 42f, 400f, 22f), "SYSTEM HOLD  //  PAUSED", overlineStyle);
        GUI.Label(new Rect(panel.x + 42f, panel.y + 90f, 405f, 60f), "RACE PAUSED.", centerTitleStyle);
        GUI.Label(new Rect(panel.x + 44f, panel.y + 156f, 400f, 35f), "Take a breath. The vacuum can wait.", centerBodyStyle);
        if (GUI.Button(new Rect(panel.x + 44f, panel.y + 225f, 402f, 64f), "RESUME RACE  →", launchStyle)) Resume();
        if (GUI.Button(new Rect(panel.x + 44f, panel.y + 302f, 402f, 54f), "RESTART RACE", buttonStyle)) StartRace();
        if (GUI.Button(new Rect(panel.x + 44f, panel.y + 368f, 402f, 54f), "RETURN TO GARAGE", buttonStyle)) ReturnToGarage();
    }

    private void DrawResults(float w, float h)
    {
        DrawModalShade(w, h);
        Rect panel = new Rect(w * .5f - 300f, h * .5f - 360f, 600f, 720f);
        GUI.DrawTexture(panel, panelTexture);
        GUI.Label(new Rect(panel.x + 50f, panel.y + 38f, 500f, 22f), "RACE COMPLETE  //  OFFICIAL RESULT", overlineStyle);
        GUI.Label(new Rect(panel.x + 50f, panel.y + 72f, 500f, 170f), finalPlace + Ordinal(finalPlace), resultPlaceStyle);
        string heading = finalPlace == 1 ? "STARBORN VICTORY." : finalPlace <= 3 ? "PODIUM SECURED." : "RACE COMPLETE.";
        GUI.Label(new Rect(panel.x + 50f, panel.y + 245f, 500f, 55f), heading, centerTitleStyle);
        GUI.Label(new Rect(panel.x + 50f, panel.y + 312f, 500f, 35f), finalPlace == 1 ? "First across the line. Orbit belongs to you." : "Telemetry saved. The next launch is yours.", centerBodyStyle);

        float bestLap = lapTimes.Count > 0 ? Mathf.Min(lapTimes.ToArray()) : raceTime;
        DrawResultCell(new Rect(panel.x + 50f, panel.y + 380f, 160f, 72f), "FINAL TIME", FormatTime(raceTime));
        DrawResultCell(new Rect(panel.x + 220f, panel.y + 380f, 160f, 72f), "BEST LAP", FormatTime(bestLap));
        DrawResultCell(new Rect(panel.x + 390f, panel.y + 380f, 160f, 72f), "DIFFICULTY", Difficulties[selectedDifficulty].Name);
        if (GUI.Button(new Rect(panel.x + 50f, panel.y + 500f, 500f, 66f), "RACE AGAIN  ↻", launchStyle)) StartRace();
        if (GUI.Button(new Rect(panel.x + 50f, panel.y + 580f, 500f, 54f), "CHANGE CAR", buttonStyle)) ReturnToGarage();
    }

    private void DrawModalShade(float w, float h)
    {
        GUI.color = new Color(0f, 0f, 0f, .72f);
        GUI.DrawTexture(new Rect(0f, 0f, w, h), blackTexture);
        GUI.color = Color.white;
    }

    private void DrawResultCell(Rect rect, string label, string value)
    {
        GUI.DrawTexture(rect, cardTexture);
        GUI.Label(new Rect(rect.x + 10f, rect.y + 13f, rect.width - 20f, 18f), label, hudLabelStyle);
        GUI.Label(new Rect(rect.x + 10f, rect.y + 36f, rect.width - 20f, 26f), value, centerBodyStyle);
    }

    private void DrawStat(Rect rect, string label, float value, string number)
    {
        GUI.Label(new Rect(rect.x, rect.y - 24f, rect.width * .7f, 18f), label, hudLabelStyle);
        GUI.Label(new Rect(rect.x + rect.width - 45f, rect.y - 24f, 45f, 18f), number, hudLabelStyle);
        GUI.color = new Color(1f, 1f, 1f, .12f); GUI.DrawTexture(rect, lineTexture);
        GUI.color = cyan; GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), lineTexture);
        GUI.color = Color.white;
    }

    private static string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        int milliseconds = Mathf.FloorToInt((time - Mathf.Floor(time)) * 1000f);
        return minutes.ToString("00") + ":" + seconds.ToString("00") + "." + milliseconds.ToString("000");
    }

    private static string Ordinal(int value)
    {
        if (value == 1) return "ST";
        if (value == 2) return "ND";
        if (value == 3) return "RD";
        return "TH";
    }

    private void EnsureStyles()
    {
        if (panelTexture != null) return;
        panelTexture = Solid(new Color(.025f, .035f, .085f, .92f));
        cardTexture = Solid(new Color(.08f, .10f, .17f, .84f));
        buttonTexture = Solid(new Color(.08f, .10f, .17f, .92f));
        selectedTexture = Solid(new Color(.08f, .35f, .43f, .92f));
        launchTexture = Solid(new Color(.36f, .16f, .94f, .96f));
        lineTexture = Solid(Color.white);
        blackTexture = Solid(Color.black);

        logoStyle = Style(28, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
        overlineStyle = Style(15, FontStyle.Bold, TextAnchor.UpperLeft, cyan);
        heroStyle = Style(54, FontStyle.BoldAndItalic, TextAnchor.UpperLeft, Color.white);
        bodyStyle = Style(19, FontStyle.Normal, TextAnchor.UpperLeft, muted);
        bodyStyle.wordWrap = true;
        centerTitleStyle = Style(39, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        centerBodyStyle = Style(20, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        hudBigStyle = Style(77, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
        hudLabelStyle = Style(14, FontStyle.Bold, TextAnchor.MiddleLeft, muted);
        resultPlaceStyle = Style(132, FontStyle.BoldAndItalic, TextAnchor.MiddleCenter, cyan);

        carButtonStyle = ButtonStyle(buttonTexture, cardTexture, 18, TextAnchor.MiddleCenter, Color.white);
        selectedCarButtonStyle = ButtonStyle(selectedTexture, selectedTexture, 18, TextAnchor.MiddleCenter, Color.white);
        buttonStyle = ButtonStyle(buttonTexture, cardTexture, 17, TextAnchor.MiddleCenter, Color.white);
        selectedButtonStyle = ButtonStyle(selectedTexture, selectedTexture, 17, TextAnchor.MiddleCenter, Color.white);
        launchStyle = ButtonStyle(launchTexture, selectedTexture, 22, TextAnchor.MiddleCenter, Color.white);
    }

    private static GUIStyle Style(int size, FontStyle fontStyle, TextAnchor alignment, Color color)
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = fontStyle,
            alignment = alignment,
            normal = { textColor = color },
            richText = false
        };
    }

    private static GUIStyle ButtonStyle(Texture2D normal, Texture2D hover, int size, TextAnchor alignment, Color color)
    {
        GUIStyle style = new GUIStyle(GUI.skin.button)
        {
            fontSize = size,
            fontStyle = FontStyle.Bold,
            alignment = alignment,
            wordWrap = true,
            padding = new RectOffset(12, 12, 8, 8)
        };
        style.normal.background = normal;
        style.hover.background = hover;
        style.active.background = hover;
        style.focused.background = normal;
        style.normal.textColor = color;
        style.hover.textColor = color;
        style.active.textColor = color;
        return style;
    }

    private static Texture2D Solid(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "Stellar UI " + color;
        texture.hideFlags = HideFlags.DontSave;
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}

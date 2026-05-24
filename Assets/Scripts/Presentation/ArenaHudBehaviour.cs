using UnityEngine;

// 씬에 하나만 존재하는 HUD MonoBehaviour.
// ArenaHudSystem(ECS)이 매 프레임 이 인스턴스에 수치를 기록하고,
// OnGUI에서 IMGUI로 화면에 렌더링한다.
public class ArenaHudBehaviour : MonoBehaviour
{
    public static ArenaHudBehaviour Instance { get; private set; }

    // ArenaHudSystem이 기록하는 공개 필드
    public float TimeRemaining;
    public int   ScoreTeam0;
    public int   ScoreTeam1;
    public float LavaRiseY;
    public float LocalHealth;
    public float LocalMana;
    public float LocalManaMax = 100f;
    public int   LocalActiveSlot;
    public string LocalSpellType = "Bullet";

    // 방 상태 (RoomPhase RPC 수신 시 외부에서 설정)
    public RoomPhase CurrentPhase = RoomPhase.Solo;
    public int       CountdownCount = 3; // 3·2·1·0(GO)

    // 결과 화면 상태
    bool  _showResult;
    int   _winnerTeamId;
    int   _resultScore0, _resultScore1;
    bool  _isDraw;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ShowResult(int winnerTeamId, int score0, int score1, bool isDraw)
    {
        _showResult    = true;
        _winnerTeamId  = winnerTeamId;
        _resultScore0  = score0;
        _resultScore1  = score1;
        _isDraw        = isDraw;
    }

    public void OnRoomReset()
    {
        _showResult  = false;
        CurrentPhase = RoomPhase.Countdown;
    }

    public void OnCountdown(int count)
    {
        CurrentPhase   = count > 0 ? RoomPhase.Countdown : RoomPhase.Battle;
        CountdownCount = count;
    }

    void OnGUI()
    {
        if (_showResult) { DrawResult(); return; }

        switch (CurrentPhase)
        {
            case RoomPhase.Solo:      DrawWaiting();   break;
            case RoomPhase.Countdown: DrawCountdown(); break;
            case RoomPhase.Battle:    DrawHud();       break;
        }
    }

    void DrawWaiting()
    {
        float sw = Screen.width;
        var style = new GUIStyle(GUI.skin.label)
            { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
        GUI.Label(new Rect(sw / 2f - 160, 12, 320, 36), "상대를 기다리는 중...", style);
    }

    void DrawCountdown()
    {
        float sw = Screen.width, sh = Screen.height;
        var style = new GUIStyle(GUI.skin.label)
            { fontSize = 96, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.white;
        string text = CountdownCount > 0 ? CountdownCount.ToString() : "GO!";
        GUI.Label(new Rect(sw / 2f - 120, sh / 2f - 80, 240, 160), text, style);
    }

    void DrawHud()
    {
        float sw = Screen.width, sh = Screen.height;

        // ── 타이머 (상단 중앙) ────────────────────────────────────────
        int mins = Mathf.FloorToInt(TimeRemaining / 60f);
        int secs = Mathf.FloorToInt(TimeRemaining % 60f);
        var timerStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 28, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(sw / 2f - 60, 8, 120, 40),
            $"{mins:00}:{secs:00}", timerStyle);

        // ── 점수 (상단 좌우) ──────────────────────────────────────────
        var scoreStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(sw / 2f - 120, 8, 60, 40), $"A {ScoreTeam0}", scoreStyle);
        GUI.Label(new Rect(sw / 2f + 60,  8, 60, 40), $"B {ScoreTeam1}", scoreStyle);

        // ── HP 바 (하단 좌) ───────────────────────────────────────────
        float hpRatio = Mathf.Clamp01(LocalHealth / 100f);
        DrawBar(new Rect(20, sh - 50, 200, 20), hpRatio, Color.green, "HP");

        // ── 마나 바 (하단 좌, HP 아래) ────────────────────────────────
        float mpRatio = LocalManaMax > 0 ? Mathf.Clamp01(LocalMana / LocalManaMax) : 0f;
        DrawBar(new Rect(20, sh - 25, 200, 16), mpRatio, Color.cyan, "MP");

        // ── 완드 슬롯 (하단 중앙) ─────────────────────────────────────
        var wandStyle = new GUIStyle(GUI.skin.box)
            { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        wandStyle.normal.textColor = Color.yellow;
        GUI.Box(new Rect(sw / 2f - 120, sh - 45, 240, 35), 
            $"SLOT {LocalActiveSlot + 1} : {LocalSpellType}", wandStyle);

        // ── 용암 경고 (용암 상승 시) ──────────────────────────────────
        if (LavaRiseY > 4f)
        {
            var warnStyle = new GUIStyle(GUI.skin.box)
                { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            warnStyle.normal.textColor = Color.red;
            GUI.Box(new Rect(sw / 2f - 80, sh - 90, 160, 32), "⚠ LAVA RISING", warnStyle);
        }
    }

    void DrawResult()
    {
        float sw = Screen.width, sh = Screen.height;
        var boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 28, alignment = TextAnchor.MiddleCenter };

        string title = _isDraw ? "DRAW"
            : _winnerTeamId == 0 ? "TEAM A WINS!" : "TEAM B WINS!";

        GUI.Box(new Rect(sw / 2f - 180, sh / 2f - 80, 360, 160), "");
        GUI.Label(new Rect(sw / 2f - 160, sh / 2f - 60, 320, 50), title, boxStyle);
        GUI.Label(new Rect(sw / 2f - 160, sh / 2f,      320, 40),
            $"A: {_resultScore0}  —  B: {_resultScore1}", boxStyle);
    }

    static void DrawBar(Rect rect, float ratio, Color color, string label)
    {
        GUI.color = Color.black;
        GUI.Box(rect, GUIContent.none);
        GUI.color = color;
        GUI.Box(new Rect(rect.x, rect.y, rect.width * ratio, rect.height), GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 4, rect.y, 40, rect.height), label);
    }
}

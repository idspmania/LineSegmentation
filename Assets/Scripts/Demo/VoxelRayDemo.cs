using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using VoxelRay;

// 테스트 씬 진행. T: 타겟 생성, R: 총구 → 각 타겟 중심 레이를 복셀 단위로 segmentation해
// 복셀마다 Checkbox를 놓는다, C: 모두 지우기, L: 실시간 갱신(매 프레임 segmentation) 토글.
public class VoxelRayDemo : MonoBehaviour
{
    const int MaxTargets = 10;

    [Header("Scene")]
    [SerializeField] Transform player;
    [SerializeField] Transform muzzle;               // 레이 시작점
    [SerializeField] GameObject checkboxPrefab;      // Assets/Checkbox/Checkbox.prefab
    [SerializeField] Material targetMaterial;
    [SerializeField] Material rayMaterial;

    [Header("Targets")]
    [SerializeField, Range(1, MaxTargets)] int targetCount = 5;
    [SerializeField] float spawnRadius = 30f;        // 플레이어 중심에서 타겟 중심까지 최대 거리
    [SerializeField] float minSpawnDistance = 2f;
    [SerializeField] bool snapTargetsToGrid = true;  // 타겟이 복셀 하나를 차지하고 락온 포인트 = 그 복셀의 중점

    [Header("Voxel grid")]
    [SerializeField] Vector3 gridOrigin = Vector3.zero;
    [SerializeField] float voxelSize = 1f;
    [SerializeField] float checkboxScale = 0.98f;    // 1이면 타겟 복셀의 Checkbox가 타겟 큐브와 겹쳐 깜빡인다
    [SerializeField] float rayWidth = 0.05f;

    [Header("UI")]
    [SerializeField] UnityEngine.UI.Slider targetCountSlider;
    [SerializeField] TMP_Text targetCountLabel;
    [SerializeField] UnityEngine.UI.Button spawnButton;
    [SerializeField] UnityEngine.UI.Button segmentButton;
    [SerializeField] UnityEngine.UI.Button clearButton;
    [SerializeField] UnityEngine.UI.Toggle liveToggle;
    [SerializeField] TMP_Text statsText;

    sealed class RayGroup
    {
        public Transform Root;
        public LineRenderer Line;
        public readonly List<Transform> Boxes = new List<Transform>();
        public readonly List<int3> Shown = new List<int3>();  // 각 Checkbox가 지금 표시하는 복셀
        public int Active;
    }

    readonly List<Transform> targets = new List<Transform>();
    readonly List<RayGroup> rays = new List<RayGroup>();
    readonly List<int3> voxels = new List<int3>(2048);   // 모든 레이의 결과를 이어 붙인다
    readonly int[] rayStart = new int[MaxTargets + 1];
    readonly HashSet<int3> unique = new HashSet<int3>();
    Transform targetRoot, rayRoot;
    bool live;
    bool hasResult;
    double lastTraverseMicros;

    void Awake()
    {
        targetRoot = new GameObject("Targets").transform;
        targetRoot.SetParent(transform, false);
        rayRoot = new GameObject("Rays").transform;
        rayRoot.SetParent(transform, false);

        // 첫 Segment 측정값에 JIT 컴파일 시간이 섞이지 않도록 한 번 미리 돌린다.
        var warmUp = new VoxelListCollector(voxels);
        VoxelSupercover.Traverse(new double3(0.5), new double3(3.7, -2.2, 5.1), ref warmUp);
        voxels.Clear();

        if (targetCountSlider != null)
        {
            targetCountSlider.minValue = 1;
            targetCountSlider.maxValue = MaxTargets;
            targetCountSlider.wholeNumbers = true;
            targetCountSlider.SetValueWithoutNotify(targetCount);
            targetCountSlider.onValueChanged.AddListener(v => { targetCount = Mathf.Clamp((int)v, 1, MaxTargets); RefreshUI(); });
        }
        if (spawnButton != null) spawnButton.onClick.AddListener(SpawnTargets);
        if (segmentButton != null) segmentButton.onClick.AddListener(Segment);
        if (clearButton != null) clearButton.onClick.AddListener(ClearAll);
        if (liveToggle != null)
        {
            liveToggle.SetIsOnWithoutNotify(live);
            liveToggle.onValueChanged.AddListener(SetLive);
        }
        RefreshUI();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.tKey.wasPressedThisFrame) SpawnTargets();
            if (kb.rKey.wasPressedThisFrame) Segment();
            if (kb.cKey.wasPressedThisFrame) ClearAll();
            if (kb.lKey.wasPressedThisFrame) SetLive(!live);
        }

        // 클릭한 UI가 선택된 채로 남으면 WASD가 UI 내비게이션(슬라이더 값 변경 등)으로 먹힌다.
        var es = EventSystem.current;
        var mouse = Mouse.current;
        if (es != null && es.currentSelectedGameObject != null && (mouse == null || !mouse.leftButton.isPressed))
            es.SetSelectedGameObject(null);
    }

    // 플레이어 이동(Update) 뒤에 갱신한다.
    void LateUpdate()
    {
        if (live && targets.Count > 0) Segment();
    }

    void SetLive(bool on)
    {
        live = on;
        if (liveToggle != null) liveToggle.SetIsOnWithoutNotify(on);
        RefreshUI();
    }

    public void SpawnTargets()
    {
        ClearAll();
        Vector3 center = player.position;
        var used = new HashSet<Vector3Int>();
        for (int attempt = 0; targets.Count < targetCount && attempt < 10000; attempt++)
        {
            Vector3 p = center + UnityEngine.Random.insideUnitSphere * spawnRadius;
            if (snapTargetsToGrid)
            {
                Vector3Int cell = Vector3Int.FloorToInt((p - gridOrigin) / voxelSize);
                p = gridOrigin + ((Vector3)cell + Vector3.one * 0.5f) * voxelSize;
                if (used.Contains(cell)) continue;
                float snappedDist = Vector3.Distance(p, center);
                if (snappedDist > spawnRadius || snappedDist < minSpawnDistance) continue;
                used.Add(cell);
            }
            else
            {
                float dist = Vector3.Distance(p, center);
                if (dist < minSpawnDistance) continue;
            }

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"Target {targets.Count:00} {Format(p)}";
            cube.transform.SetParent(targetRoot, false);
            cube.transform.position = p;
            cube.transform.localScale = Vector3.one; // 크기 1유닛
            if (targetMaterial != null) cube.GetComponent<MeshRenderer>().sharedMaterial = targetMaterial;
            targets.Add(cube.transform);
        }

        if (live) Segment();
        RefreshUI();
    }

    public void Segment()
    {
        if (targets.Count == 0) { RefreshUI(); return; }

        double3 origin = new double3(gridOrigin.x, gridOrigin.y, gridOrigin.z);
        double3 start = VoxelSupercover.WorldToVoxel(muzzle.position, origin, voxelSize);

        // 1) 순회만 따로 잰다(표시 비용 제외).
        voxels.Clear();
        var collector = new VoxelListCollector(voxels);
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < targets.Count; i++)
        {
            rayStart[i] = voxels.Count;
            VoxelSupercover.Traverse(start, VoxelSupercover.WorldToVoxel(targets[i].position, origin, voxelSize), ref collector);
        }
        sw.Stop();
        rayStart[targets.Count] = voxels.Count;
        lastTraverseMicros = sw.Elapsed.TotalMilliseconds * 1000.0;
        hasResult = true;

        // 2) 레이마다 선과 segment(복셀)별 Checkbox를 놓는다.
        for (int i = 0; i < targets.Count; i++)
            ShowRay(GetRayGroup(i), i, muzzle.position, targets[i].position, rayStart[i], rayStart[i + 1]);
        for (int i = targets.Count; i < rays.Count; i++)
            rays[i].Root.gameObject.SetActive(false);

        RefreshUI();
    }

    public void ClearAll()
    {
        foreach (var t in targets) Destroy(t.gameObject);
        targets.Clear();
        foreach (var g in rays) g.Root.gameObject.SetActive(false);
        voxels.Clear();
        hasResult = false;
        RefreshUI();
    }

    RayGroup GetRayGroup(int index)
    {
        while (rays.Count <= index)
        {
            var go = new GameObject($"Ray {rays.Count:00}");
            go.transform.SetParent(rayRoot, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = line.endWidth = rayWidth;
            line.sharedMaterial = rayMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            rays.Add(new RayGroup { Root = go.transform, Line = line });
        }
        return rays[index];
    }

    void ShowRay(RayGroup g, int index, Vector3 from, Vector3 to, int begin, int end)
    {
        g.Root.gameObject.SetActive(true);
        g.Line.SetPosition(0, from);
        g.Line.SetPosition(1, to);

        int count = end - begin;
        while (g.Boxes.Count < count)
        {
            var box = Instantiate(checkboxPrefab, g.Root);
            DisablePhysics(box);
            box.transform.localScale = Vector3.one * (voxelSize * checkboxScale);
            g.Boxes.Add(box.transform);
            g.Shown.Add(new int3(int.MinValue));
        }

        for (int j = 0; j < count; j++)
        {
            int3 v = voxels[begin + j];
            Transform box = g.Boxes[j];
            if (!box.gameObject.activeSelf) box.gameObject.SetActive(true);
            if (g.Shown[j].Equals(v)) continue;
            g.Shown[j] = v;
            box.position = gridOrigin + new Vector3(v.x + 0.5f, v.y + 0.5f, v.z + 0.5f) * voxelSize;
            box.name = $"Voxel ({v.x}, {v.y}, {v.z})";
        }
        for (int j = count; j < g.Active; j++)
            g.Boxes[j].gameObject.SetActive(false);

        if (g.Active != count || !live)
            g.Root.name = $"Ray {index:00} → {Format(to)} · {count} voxels";
        g.Active = count;
    }

    // Checkbox 프리팹에는 Rigidbody(중력 사용)와 BoxCollider가 있다. 표시용이라 인스턴스에서만 물리를 끈다.
    static void DisablePhysics(GameObject go)
    {
        if (go.TryGetComponent(out Rigidbody rb)) { rb.isKinematic = true; rb.detectCollisions = false; }
        foreach (var c in go.GetComponents<Collider>()) c.enabled = false;
    }

    void RefreshUI()
    {
        if (targetCountLabel != null) targetCountLabel.text = $"Target count: {targetCount}";
        if (targetCountSlider != null) targetCountSlider.SetValueWithoutNotify(targetCount);
        if (statsText == null) return;

        if (!hasResult)
        {
            statsText.text = $"Targets: {targets.Count}\nPress R to segment rays.";
            return;
        }

        unique.Clear();
        foreach (var v in voxels) unique.Add(v);
        int rayCount = Mathf.Min(targets.Count, MaxTargets);
        statsText.text =
            $"Targets: {targets.Count}   Rays: {rayCount}{(live ? "   [LIVE]" : "")}\n" +
            $"Segments: {voxels.Count}   Unique voxels: {unique.Count}\n" +
            $"Traverse: {lastTraverseMicros:F1} µs total, {lastTraverseMicros / Mathf.Max(1, rayCount):F1} µs/ray";
    }

    static string Format(Vector3 p) => $"({p.x:0.##}, {p.y:0.##}, {p.z:0.##})";
}

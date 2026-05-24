using System;
using System.Collections.Generic;
using UnityEngine;

namespace SandBlast.Arena
{
    public enum MapMode { OneVsOne, TwoVsTwo }
    public enum FillShape { Rect, HLine, VLine }

    // 셀 배치 규칙 — 그리드 픽셀 좌표 기준
    [Serializable]
    public struct CellFillRule
    {
        public FillShape Shape;
        public int X, Y, W, H;       // Rect: 기준 좌하단, HLine/VLine: X,Y=시작, W=길이
        public CellType CellType;
        public byte Flammability;
    }

    [Serializable]
    public struct MapSpawnPoint
    {
        public Vector2Int GridCell;  // 픽셀 좌표
        public int TeamId;           // 0=A팀, 1=B팀
    }

    [Serializable]
    public struct MapWandSpawn
    {
        public Vector2Int GridCell;
        public int WandPresetId;     // WandPickupSystem 프리셋 0~3
    }

    // 아레나 맵 데이터 — Unity Editor 에서 직접 편집 가능
    [CreateAssetMenu(fileName = "NewMapTemplate", menuName = "SandBlast/Arena/Map Template")]
    public class MapTemplate : ScriptableObject
    {
        public MapMode Mode     = MapMode.OneVsOne;
        public int GridWidth    = 512;
        public int GridHeight   = 256;

        [Tooltip("픽셀 좌표 기준 셀 배치 규칙 목록 (위에서 아래로 순서 적용)")]
        public List<CellFillRule> CellRules = new();

        [Tooltip("플레이어 스폰 위치")]
        public MapSpawnPoint[] SpawnPoints = Array.Empty<MapSpawnPoint>();

        [Tooltip("맵에 배치할 완드 아이템 목록")]
        public MapWandSpawn[] WandSpawnPoints = Array.Empty<MapWandSpawn>();

        [Tooltip("스폰 지점 주변 SOLID_INDESTRUCTIBLE 반경 (픽셀)")]
        public int IndestructibleRadius = 6;
    }
}

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>
/// SPUM_Prefabs 인스턴스에 랜덤 외형을 적용하는 유틸리티.
/// PlayerVisualSystem에서 엔티티 첫 프레임에 호출된다.
/// </summary>
public static class SpumAppearanceRandomizer
{
    // 반드시 존재해야 하는 파츠 (빈 값 불가)
    private static readonly string[] RequiredParts = { "Body", "Eye" };

    // 확률적으로 표시되는 파츠 (확률 = 표시될 확률)
    private static readonly (string part, float showChance)[] OptionalParts =
    {
        ("Hair",     0.85f),
        ("Cloth",    0.70f),
        ("Pant",     0.70f),
        ("Helmet",   0.20f),
        ("FaceHair", 0.25f),
        ("Armor",    0.30f),
        ("Back",     0.15f),
    };

    public static void Randomize(SPUM_Prefabs spum, int seed = 0)
    {
        if (spum == null) return;

        var packages = LoadAllPackages();
        if (packages.Count == 0)
        {
            Debug.LogWarning("[SpumAppearanceRandomizer] 패키지 데이터를 찾지 못했습니다.");
            return;
        }

        var rng = new Unity.Mathematics.Random((uint)(seed == 0 ? Random.Range(1, int.MaxValue) : seed));
        string unitType = string.IsNullOrEmpty(spum.UnitType) ? "Unit" : spum.UnitType;

        spum.ImageElement.Clear();

        foreach (var part in RequiredParts)
            PickPart(spum, packages, unitType, part, canBeEmpty: false, rng: ref rng);

        foreach (var (part, chance) in OptionalParts)
            PickPart(spum, packages, unitType, part, canBeEmpty: true, showChance: chance, rng: ref rng);

        ApplyToRenderers(spum);
    }

    private static void PickPart(
        SPUM_Prefabs spum,
        List<SpumPackage> packages,
        string unitType,
        string partType,
        bool canBeEmpty,
        ref Unity.Mathematics.Random rng,
        float showChance = 1f)
    {
        if (canBeEmpty && rng.NextFloat() > showChance) return;

        // 해당 UnitType + PartType의 텍스쳐를 이름 기준으로 그룹화
        var groups = packages
            .SelectMany(p => p.SpumTextureData.Select(t => new { p, t }))
            .Where(x => x.t.UnitType == unitType && x.t.PartType == partType)
            .GroupBy(x => x.t.Name)
            .ToList();

        if (groups.Count == 0) return;

        var chosen = groups[rng.NextInt(0, groups.Count)];

        // 눈 색상만 랜덤, 나머지는 흰색(원본 색)
        Color color = partType == "Eye"
            ? new Color(rng.NextFloat(0f, 0.6f), rng.NextFloat(0f, 0.5f), rng.NextFloat(0f, 0.4f))
            : Color.white;


        foreach (var item in chosen)
        {
            spum.ImageElement.Add(new PreviewMatchingElement
            {
                UnitType    = unitType,
                PartType    = partType,
                PartSubType = item.t.PartSubType,
                Dir         = "",
                ItemPath    = item.t.Path,
                // SubType == Name인 경우 Structure는 PartType, 아니면 SubType(스프라이트 이름)
                Structure   = item.t.SubType == item.t.Name ? partType : item.t.SubType,
                MaskIndex   = 0,
                Color       = color,
            });
        }
    }

    private static void ApplyToRenderers(SPUM_Prefabs spum)
    {
        var matchingLists = spum.GetComponentsInChildren<SPUM_MatchingList>(true);

        foreach (var ml in matchingLists)
        {
            foreach (var me in ml.matchingTables)
            {
                var match = spum.ImageElement.FirstOrDefault(ie =>
                    ie.UnitType    == me.UnitType    &&
                    ie.PartType    == me.PartType    &&
                    ie.Dir         == me.Dir         &&
                    ie.Structure   == me.Structure   &&
                    ie.PartSubType == me.PartSubType);

                if (match != null)
                {
                    me.renderer.sprite           = LoadSprite(match.ItemPath, match.Structure);
                    me.renderer.color            = match.Color;
                    me.renderer.maskInteraction  = (SpriteMaskInteraction)match.MaskIndex;
                    me.ItemPath                  = match.ItemPath;
                    me.Color                     = match.Color;
                }
                else
                {
                    me.renderer.sprite = null;
                    me.ItemPath        = "";
                }
            }
        }
    }

    private static Sprite LoadSprite(string path, string spriteName)
    {
        var sprites = Resources.LoadAll<Sprite>(path);
        if (sprites == null || sprites.Length == 0) return null;
        return System.Array.Find(sprites, s => s.name == spriteName) ?? sprites[0];
    }

    private static List<SpumPackage> LoadAllPackages()
    {
        var packages = new List<SpumPackage>();
        foreach (var asset in Resources.LoadAll<TextAsset>(""))
        {
            if (!asset.name.Contains("Index")) continue;
            var pkg = JsonUtility.FromJson<SpumPackage>(asset.text);
            if (pkg != null) packages.Add(pkg);
        }

        return packages
            .OrderBy(p =>
            {
                System.DateTime.TryParseExact(
                    p.CreationDate, "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dt);
                return dt;
            })
            .ToList();
    }
}

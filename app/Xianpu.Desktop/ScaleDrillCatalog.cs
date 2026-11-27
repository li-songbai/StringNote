namespace Xianpu.Desktop;

public enum DrillPositionMode
{
    FollowCaged,          // Uses currently selected CAGED shape (or lowest CAGED box >= Capo)
    FixedWindow5To8,      // Locks left hand strictly in frets 5..8 (classic position study & Circle of 5ths)
    ThreeNotesPerString   // 3NPS modern speed/sequence layout across 6 strings
}

public enum DrillSubdivision
{
    Eighth = 2,      // 2 notes per beat (8 cols per 4/4 bar)
    Triplet = 3,     // 3 notes per beat (12 cols per 4/4 bar)
    Sixteenth = 4    // 4 notes per beat (16 cols per 4/4 bar)
}

public enum DrillAccentMode
{
    None,
    BeatDownbeat,       // Accent every beat start (creates metric displacement on 3-note sequences in 16ths!)
    GroupOf3CrossBeat,  // Accent every 3rd note inside 16ths (classic 3-against-4 cross-rhythm!)
    OffbeatSecond16th,  // Accent 2nd note of each beat (1 >2 3 4)
    OffbeatUpbeat,      // Accent '&' upbeat (1 2 >3 4 or 1 >&)
    Compound332         // 3+3+2 Tresillo groove accents
}

public enum DrillPatternKind
{
    Spider1234,
    Spider1324,
    SlideHighwayMajorPent,
    SlideHighwayMinorPent,
    SlideHighwayMajor,
    ScaleUpDown,
    Seq3,
    Seq4,
    Seq5,
    Compound332Scale,
    SkipPentatonic,
    SkipPentatonicReverse,
    Skip3rdParallel,
    Skip3rdAlternating,
    Skip3rdReverse,
    Skip4thParallel,
    Skip4thAlternating,
    Skip5thParallel,
    Skip5thAlternating,
    Skip6thParallel,
    Skip6thAlternating,
    ArpTriadUp,
    ArpTriadDown,
    ArpTriadAltUp,
    ArpTriadAltDown,
    Arp7thUp,
    Arp7thDown,
    Arp7thAltUp,
    Arp7thAltDown,
    CircleOf4thsPentatonic,
    CircleOf4thsMajor,
    CircleOf5thsPentatonic
}

public sealed record ScaleDrillPreset(
    string Id,
    string UnitTitle,
    string CodeBadge,
    string Title,
    string CoachingTip,
    string RecommendedScaleId,
    DrillPatternKind Pattern,
    DrillSubdivision Subdivision,
    DrillAccentMode Accent,
    DrillPositionMode DefaultPosMode,
    int SuggestedBpm = 80);

public sealed record DrillStepEvent(
    int StepIndex,
    int BarIndex,
    int Column,
    int StringIdx,            // 0 = 6E (bottom) .. 5 = 1e (top)
    int Fret,
    int Midi,
    bool IsAccent,
    bool IsBeatStart,
    bool IsBarDownbeat,
    int EffectiveTonicPitch,
    string EffectiveCagedLetter,
    string? KeyBannerText,
    string? NextKeyPreviewText);

public sealed record GeneratedScaleDrill(
    string Title,
    string Subtitle,
    TabSection Section,
    List<DrillStepEvent> Events,
    int NotesPerBeat);

public static class ScaleDrillCatalog
{
    public static readonly IReadOnlyList<ScaleDrillPreset> Presets =
    [
        // ─── Unit 1: 机能与把位地基 (01–25 精选) ────────────────────────────
        new("u1_spider_1234", "1. 把位与热身地基", "01", "半音阶蜘蛛爬格子 (1-2-3-4)",
            "四指独立保留指热身：弹 2/3/4 指时前序手指轻触保留在品格上，不要飞指。",
            "chromatic", DrillPatternKind.Spider1234, DrillSubdivision.Sixteenth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FixedWindow5To8, 76),
        new("u1_spider_1324", "1. 把位与热身地基", "05", "交替跨指爬格子 (1-3-2-4)",
            "训练无名指与中指独立解耦，右手严格保持『下-上-下-上』交替拨弦。",
            "chromatic", DrillPatternKind.Spider1324, DrillSubdivision.Sixteenth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FixedWindow5To8, 72),
        new("u1_box_pent", "1. 把位与热身地基", "12", "五声音阶单把位顺爬（每弦两音）",
            "先在单个 CAGED 把位内把根音（橙色大圆）位置烙进肌肉记忆，换把不迷路。",
            "major_pent", DrillPatternKind.ScaleUpDown, DrillSubdivision.Eighth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 88),
        new("u1_slide_mpent", "1. 把位与热身地基", "15", "小调五声 · 全指板对角线滑音高速公路",
            "【跨把位串联】每根弦弹 2 音后直接顺势向高把位滑音 (s) 2 品切入下一把位，一口气从低把位纵贯到高把位！",
            "minor_pent", DrillPatternKind.SlideHighwayMinorPent, DrillSubdivision.Eighth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 84),
        new("u1_slide_majpent", "1. 把位与热身地基", "16", "大调五声 · 跨三把位甜蜜滑音串联",
            "民谣与乡村 Solo 最常用连接线：利用 6→5→4→3→2→1 弦经典 2–3 度无名指滑音无缝横穿三个 CAGED 把位。",
            "major_pent", DrillPatternKind.SlideHighwayMajorPent, DrillSubdivision.Eighth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 86),
        new("u1_3nps_major", "1. 把位与热身地基", "18", "自然大调 · 每弦三音 (3NPS) 速弹地基",
            "每根弦严格 3 个音，跨弦拨弦规律完全对称，可在上方指板切换 1～7 把位逐一攻克。",
            "major", DrillPatternKind.ScaleUpDown, DrillSubdivision.Triplet, DrillAccentMode.BeatDownbeat, DrillPositionMode.ThreeNotesPerString, 84),
        new("u1_slide_major", "1. 把位与热身地基", "22", "自然大调 · 跨把位斜向滑音长线条",
            "每弦弹 3 音并在换把点加入食指/无名指滑音推进，彻底消除把位盒子的边界感。",
            "major", DrillPatternKind.SlideHighwayMajor, DrillSubdivision.Triplet, DrillAccentMode.BeatDownbeat, DrillPositionMode.ThreeNotesPerString, 80),

        // ─── Unit 2: 音阶模进与重音移位 (26–39 课) ──────────────────────────
        new("u2_26_pent_seq3", "2. 模进与重音移位", "26", "三音模进 · 大调五声音阶（一）",
            "三连音工整版：每拍 3 个音对应一组模进（1-2-3, 2-3-5, 3-5-6...），先摸熟手指折返路径。",
            "major_pent", DrillPatternKind.Seq3, DrillSubdivision.Triplet, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 80),
        new("u2_27_mpent_seq3", "2. 模进与重音移位", "27", "三音模进 · 小调五声音阶（一）",
            "摇滚/蓝调必练：在小调五声指型内做三音一组递推，注意跨弦时手腕放松。",
            "minor_pent", DrillPatternKind.Seq3, DrillSubdivision.Triplet, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 80),
        new("u2_28_disp_beat", "2. 模进与重音移位", "28", "五声三音模进 + 十六分重音移位（二）",
            "【点石成金】把 3 音一组的模进装进每拍 4 个十六分音符！重音落在每拍拍头，组内重音自动轮转（组头→组中→组尾）！",
            "major_pent", DrillPatternKind.Seq3, DrillSubdivision.Sixteenth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 74),
        new("u2_29_mdisp_cross", "2. 模进与重音移位", "29", "小调五声三音模进 + 跨拍重音移位",
            "【3 对 4 套拍律动】十六分音符下每隔 3 个音打一次重音（>），每 3 拍完成一轮循环，瞬间弹出 Fusion 大师味！",
            "minor_pent", DrillPatternKind.Seq3, DrillSubdivision.Sixteenth, DrillAccentMode.GroupOf3CrossBeat, DrillPositionMode.FollowCaged, 74),
        new("u2_30_disp_off", "2. 模进与重音移位", "30", "三音模进 + 反拍重音后移（三）",
            "故意把重音放在每拍第 3 个十六分音符（反拍 &），彻底治好右手只会死砸正拍的习惯。",
            "major_pent", DrillPatternKind.Seq3, DrillSubdivision.Sixteenth, DrillAccentMode.OffbeatUpbeat, DrillPositionMode.FollowCaged, 72),
        new("u2_32_maj_disp", "2. 模进与重音移位", "32", "自然大小调 · 三音模进 + 重音移位",
            "七声音阶的半音关系更密，装进十六分音符跨拍重音后，线条极具现代爵士流动感。",
            "major", DrillPatternKind.Seq3, DrillSubdivision.Sixteenth, DrillAccentMode.GroupOf3CrossBeat, DrillPositionMode.FollowCaged, 74),
        new("u2_33_pent_seq4", "2. 模进与重音移位", "33", "四音模进 · 五声音阶（速弹颗粒）",
            "Eric Johnson / 现代摇滚标志性手法：4 音一组天然契合十六分音符（1-2-3-5, 2-3-5-6...）。",
            "minor_pent", DrillPatternKind.Seq4, DrillSubdivision.Sixteenth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 78),
        new("u2_35_maj_seq4", "2. 模进与重音移位", "35", "四音模进 · 自然大调音阶",
            "每拍 4 个音稳步上推（1-2-3-4, 2-3-4-5...），重点监听每个十六分音符颗粒的时值均匀度。",
            "major", DrillPatternKind.Seq4, DrillSubdivision.Sixteenth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 78),
        new("u2_38_comp_332", "2. 模进与重音移位", "38", "复合节奏 (3+3+2) 音阶律动练习",
            "流行与放克神级 Tresillo 律动（>1 2 3 >4 5 6 >7 8），重音按 3+3+2 切分推进。",
            "minor_pent", DrillPatternKind.Compound332Scale, DrillSubdivision.Eighth, DrillAccentMode.Compound332, DrillPositionMode.FollowCaged, 88),
        new("u2_39_seq5", "2. 模进与重音移位", "39", "五音模进 · 跨拍长线条",
            "5 音一组装入十六分音符，重音在每拍间自然漂移，打破机械方整感。",
            "major", DrillPatternKind.Seq5, DrillSubdivision.Sixteenth, DrillAccentMode.GroupOf3CrossBeat, DrillPositionMode.ThreeNotesPerString, 74),

        // ─── Unit 3: 音程跳音与跨弦控制 (40–49 课) ──────────────────────────
        new("u3_40_skip_pent", "3. 音程跳音篇", "40", "跳音 · 五声音阶正向",
            "在五声音阶中隔一音跳进（1→3, 2→5, 3→6, 5→1'），瞬间摆脱爬楼梯听感。",
            "major_pent", DrillPatternKind.SkipPentatonic, DrillSubdivision.Eighth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 82),
        new("u3_41_skip_pent_rev", "3. 音程跳音篇", "41", "跳音 · 五声音阶反向",
            "上行途中每组先弹高音再落低音（3→1, 5→2, 6→3），极具 R&B 与 Neo-Soul 灵动感。",
            "major_pent", DrillPatternKind.SkipPentatonicReverse, DrillSubdivision.Eighth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 80),
        new("u3_42_skip_3rd", "3. 音程跳音篇", "42", "跳音 · 三度同向 & 折返",
            "三度双音与旋律线的根基（1→3, 4→2, 3→5, 6→4 蛇形折返），甜美歌唱感拉满。",
            "major", DrillPatternKind.Skip3rdAlternating, DrillSubdivision.Eighth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 84),
        new("u3_43_skip_3rd_rev", "3. 音程跳音篇", "43", "跳音 · 三度反向",
            "每组高音切入（3→1, 4→2, 5→3...），训练手指超前预判高音落点。",
            "major", DrillPatternKind.Skip3rdReverse, DrillSubdivision.Eighth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 82),
        new("u3_44_skip_4th_1", "3. 音程跳音篇", "44", "跳音 · 四度（一：同向滚指）",
            "【滚指核心】吉他相邻弦同品即纯四度！用同一根手指指尖与指腹交替滚压两弦，严禁两音混响。",
            "major", DrillPatternKind.Skip4thParallel, DrillSubdivision.Eighth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 76),
        new("u3_45_skip_4th_2", "3. 音程跳音篇", "45", "跳音 · 四度（二：一上一下折返）",
            "实战最丝滑的四度线条（1→4, 5→2, 3→6, 7→4），现代融合与摇滚 Solo 必杀技。",
            "major", DrillPatternKind.Skip4thAlternating, DrillSubdivision.Eighth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 78),
        new("u3_47_skip_5th", "3. 音程跳音篇", "47", "跳音 · 五度折返交替",
            "开阔硬朗的五度跨度（1→5, 6→2, 3→7...），训练左手大跨度定位与右手护弦消音。",
            "major", DrillPatternKind.Skip5thAlternating, DrillSubdivision.Eighth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 76),
        new("u3_49_skip_6th", "3. 音程跳音篇", "49", "跳音 · 六度跨弦唯美线条",
            "经典乡村 / Soul / 流行前奏灵魂音程（跨一根弦跳跃），注意右手跨弦拨弦准度。",
            "major", DrillPatternKind.Skip6thAlternating, DrillSubdivision.Eighth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 74),

        // ─── Unit 4: 把位顺阶和弦琶音 (50–57 课) ────────────────────────────
        new("u4_50_triad_up", "4. 顺阶和弦琶音", "50", "音阶琶音 · 三和弦（上行）",
            "左手不离把位，依次勾勒 I→ii→iii→IV→V→vi→vii° 七个顺阶三和弦（1-3-5, 2-4-6...）。",
            "major", DrillPatternKind.ArpTriadUp, DrillSubdivision.Triplet, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 76),
        new("u4_51_triad_down", "4. 顺阶和弦琶音", "51", "音阶琶音 · 三和弦（下行）",
            "从高音区顺阶三和弦逐级下行回落，强化逆向和声轮廓记忆。",
            "major", DrillPatternKind.ArpTriadDown, DrillSubdivision.Triplet, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 76),
        new("u4_52_triad_alt", "4. 顺阶和弦琶音", "52", "音阶琶音 · 三和弦反向交替（上）",
            "【黄金衔接】奇数组上行（1-3-5）紧接偶数组就近下行（6-4-2），零大跳丝滑勾勒和弦走向！",
            "major", DrillPatternKind.ArpTriadAltUp, DrillSubdivision.Triplet, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 78),
        new("u4_54_7th_up", "4. 顺阶和弦琶音", "54", "音阶琶音 · 七和弦（上行）",
            "每拍 4 个十六分音符刚好弹完一个顺阶七和弦（Imaj7 → ii7 → iii7 → IVmaj7...），爵士/R&B 必修！",
            "major", DrillPatternKind.Arp7thUp, DrillSubdivision.Sixteenth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 70),
        new("u4_56_7th_alt_up", "4. 顺阶和弦琶音", "56", "音阶琶音 · 七和弦反向交替（上）",
            "【Bebop 核心】Cmaj7 上行 (1-3-5-7) 到顶后，直接顺摸 1' 下行弹 Dm7 (1'-6-4-2)，手指如行云流水！",
            "major", DrillPatternKind.Arp7thAltUp, DrillSubdivision.Sixteenth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 72),
        new("u4_57_7th_alt_down", "4. 顺阶和弦琶音", "57", "音阶琶音 · 七和弦反向交替（下）",
            "从高把位级数一路交替折返下行，彻底消除七和弦把位盲区。",
            "major", DrillPatternKind.Arp7thAltDown, DrillSubdivision.Sixteenth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FollowCaged, 70),

        // ─── Unit 5: 同一把位的五度圈终极大招 (58–59 课) ────────────────────
        new("u5_58_cof_pent", "5. 同一把位五度圈", "58", "同一把位的四度/逆五度圈 — 五声音阶",
            "【王炸大招】左手死锁在 5–8 品不准挪窝！按 C → F → Bb → Eb → Ab 连续换 5 个调，原地轮转 G→C→E→A→D 全部五种指型！",
            "major_pent", DrillPatternKind.CircleOf4thsPentatonic, DrillSubdivision.Eighth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FixedWindow5To8, 78),
        new("u5_59_cof_major", "5. 同一把位五度圈", "59", "同一把位的四度/逆五度圈 — 七声音阶",
            "终极毕业试炼：同样锁死在 5–8 品不移动手腕，连续切换 5 个大调的完整七声音阶，真正实现全指板自由转调！",
            "major", DrillPatternKind.CircleOf4thsMajor, DrillSubdivision.Eighth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FixedWindow5To8, 74),
        new("u5_60_cof5_pent", "5. 同一把位五度圈", "60", "同一把位的顺五度圈 (C→G→D→A→E)",
            "按顺时针五度圈（C→G→D→A→E）在固定 5–8 品把位原地换调，吉他常用民谣/摇滚调一网打尽。",
            "major_pent", DrillPatternKind.CircleOf5thsPentatonic, DrillSubdivision.Eighth, DrillAccentMode.BeatDownbeat, DrillPositionMode.FixedWindow5To8, 78)
    ];

    public static GeneratedScaleDrill Generate(
        int tonicPitch,
        ScaleDefinition scale,
        GuitarTuning tuning,
        int capo,
        string activeCagedFilter,
        DrillPositionMode posMode,
        DrillPatternKind pattern,
        DrillSubdivision subdivision,
        DrillAccentMode accentMode,
        int fixedWindowStartFret = 5,
        int cofKeyCount = 5,
        int cofBarsPerKey = 2,
        int threeNpsPosition = 1)
    {
        var notesPerBeat = (int)subdivision;
        var colsPerBar = notesPerBeat * 4; // 4/4 time: 8, 12, or 16 columns
        var colDuration = subdivision == DrillSubdivision.Sixteenth ? TabDuration.Sixteenth : TabDuration.Eighth;

        // Special multi-key Circle-of-Fifths / Fourths in fixed position (Lessons 58–60)
        if (pattern is DrillPatternKind.CircleOf4thsPentatonic or DrillPatternKind.CircleOf4thsMajor or DrillPatternKind.CircleOf5thsPentatonic)
        {
            return GenerateCircleOfFifthsInPosition(
                tonicPitch, tuning, capo, pattern, subdivision, accentMode, colsPerBar, colDuration,
                fixedWindowStartFret, cofKeyCount, cofBarsPerKey);
        }

        // Cross-position Diagonal Sliding Highway Drills (Lessons 15, 16, 22)
        if (pattern is DrillPatternKind.SlideHighwayMinorPent or DrillPatternKind.SlideHighwayMajorPent or DrillPatternKind.SlideHighwayMajor)
        {
            return GenerateSlideHighwayDrill(tonicPitch, tuning, capo, pattern, subdivision, accentMode, colsPerBar, colDuration);
        }

        // Chromatic Spider Drills
        if (pattern is DrillPatternKind.Spider1234 or DrillPatternKind.Spider1324)
        {
            return GenerateSpiderDrill(tuning, capo, activeCagedFilter, posMode, pattern, subdivision, accentMode, colsPerBar, colDuration, fixedWindowStartFret);
        }

        var (spots, resolvedCagedLetter, posDesc) = ExtractPositionScaleSpots(tonicPitch, scale, tuning, capo, posMode, activeCagedFilter, fixedWindowStartFret, threeNpsPosition);
        if (spots.Count < 4)
        {
            spots = ExtractPositionScaleSpots(tonicPitch, scale, tuning, capo, DrillPositionMode.FixedWindow5To8, "ALL", fixedWindowStartFret, threeNpsPosition).Spots;
        }

        var rawSequence = BuildPatternSequence(spots, tonicPitch, scale, pattern);
        if (rawSequence.Count == 0)
            rawSequence = spots.Select(s => (s.Str, s.Fret, s.Midi, (string?)null)).ToList();

        return PackIntoTabAndEvents(
            rawSequence,
            tonicPitch,
            resolvedCagedLetter,
            $"{MusicTheoryCatalog.NoteName(tonicPitch, MusicTheoryCatalog.PreferFlatsForTonic(tonicPitch))} {scale.Name}",
            posDesc,
            notesPerBeat,
            colsPerBar,
            colDuration,
            subdivision,
            accentMode);
    }

    private static GeneratedScaleDrill GenerateSlideHighwayDrill(
        int tonicPitch,
        GuitarTuning tuning,
        int capo,
        DrillPatternKind pattern,
        DrillSubdivision subdivision,
        DrillAccentMode accentMode,
        int colsPerBar,
        TabDuration colDuration)
    {
        var scaleId = pattern switch
        {
            DrillPatternKind.SlideHighwayMinorPent => "minor_pent",
            DrillPatternKind.SlideHighwayMajorPent => "major_pent",
            _ => "major"
        };
        var effScale = MusicTheoryCatalog.GetScale(scaleId);
        var pcSet = new HashSet<int>(effScale.Intervals.Select(iv => ((tonicPitch + iv) % 12 + 12) % 12));

        // Walk diagonally from string 0 (low 6E) up to string 5 (high 1e), sliding up 1 scale note on key strings!
        var steps = new List<(int Str, int Fret, int Midi, bool SlideToNext, string? Tag)>();
        var lastMidi = -1;
        var minStartFret = capo;

        for (var s = 0; s < 6; s++)
        {
            var strNotes = new List<(int Fret, int Midi)>();
            for (var f = minStartFret; f <= FretboardCanvas.TotalFrets; f++)
            {
                var midi = tuning.OpenMidi[s] + f;
                if (midi <= lastMidi) continue;
                if (!pcSet.Contains(((midi % 12) + 12) % 12)) continue;
                strNotes.Add((f, midi));
                if (strNotes.Count == 3) break; // 2 base notes + 1 slide-up target note on the same string!
            }

            if (strNotes.Count == 0) continue;
            for (var k = 0; k < strNotes.Count; k++)
            {
                var slide = strNotes.Count >= 3 && k == 1; // Slide from 2nd note up to 3rd note on the same string!
                var tag = s == 0 && k == 0 ? "跨把滑音上行" : (slide ? "滑把 (s)" : null);
                steps.Add((s, strNotes[k].Fret, strNotes[k].Midi, slide, tag));
                lastMidi = strNotes[k].Midi;
            }

            if (strNotes.Count >= 3)
                minStartFret = Math.Max(capo, strNotes[1].Fret - 1);
        }

        // Add smooth descending return down the diagonal highway
        var upCount = steps.Count;
        for (var i = upCount - 2; i >= 1; i--)
        {
            var cur = steps[i];
            var nxt = steps[i - 1];
            var slideBack = cur.Str == nxt.Str && Math.Abs(cur.Fret - nxt.Fret) >= 2 && (upCount - i) % 3 == 0;
            steps.Add((cur.Str, cur.Fret, cur.Midi, slideBack, i == upCount - 2 ? "斜向滑回低把" : null));
        }

        var notesPerBeat = (int)subdivision;
        var bars = new List<TabBar>();
        var events = new List<DrillStepEvent>(steps.Count);

        for (var i = 0; i < steps.Count; i++)
        {
            var barIdx = i / colsPerBar;
            var col = i % colsPerBar;
            if (col == 0)
                bars.Add(new TabBar { ColumnCount = colsPerBar });

            var bar = bars[barIdx];
            var item = steps[i];
            var isAccent = ComputeIsAccent(accentMode, col, i, notesPerBeat);
            if (!string.IsNullOrWhiteSpace(item.Tag))
                bar.ChordMarks.Add(new TabChordMark { Column = col, Text = item.Tag! });

            bar.Notes.Add(new TabNote
            {
                Column = col,
                StringIndex = 5 - item.Str,
                Fret = item.Fret.ToString(),
                LinkToNext = item.SlideToNext ? TabLinkKind.Slide : TabLinkKind.None
            });
            bar.ColumnMetas.Add(new TabColumnMeta
            {
                Column = col,
                Duration = colDuration,
                Accent = isAccent,
                TupletGroup = subdivision == DrillSubdivision.Triplet ? 3 : 0
            });

            events.Add(new DrillStepEvent(
                StepIndex: i,
                BarIndex: barIdx,
                Column: col,
                StringIdx: item.Str,
                Fret: item.Fret,
                Midi: item.Midi,
                IsAccent: isAccent,
                IsBeatStart: col % notesPerBeat == 0,
                IsBarDownbeat: col == 0,
                EffectiveTonicPitch: tonicPitch,
                EffectiveCagedLetter: "ALL",
                KeyBannerText: null,
                NextKeyPreviewText: null));
        }

        var pf = MusicTheoryCatalog.PreferFlatsForTonic(tonicPitch);
        var minF = steps.Count > 0 ? steps.Min(x => x.Fret) : capo;
        var maxF = steps.Count > 0 ? steps.Max(x => x.Fret) : capo + 10;
        var sec = new TabSection
        {
            Kind = TabSectionKind.Interlude,
            Title = $"{MusicTheoryCatalog.NoteName(tonicPitch, pf)} {effScale.Name} · 跨把位滑音直通车 ({minF}–{maxF}品)",
            TimeSignature = "4/4",
            ColumnsPerBar = colsPerBar,
            ShowRhythmStems = true,
            Bars = bars
        };

        return new GeneratedScaleDrill(sec.Title, $"横向贯通 {minF}–{maxF} 品（含同弦滑音换把 s）", sec, events, notesPerBeat);
    }

    private static GeneratedScaleDrill GenerateSpiderDrill(
        GuitarTuning tuning,
        int capo,
        string activeCagedFilter,
        DrillPositionMode posMode,
        DrillPatternKind pattern,
        DrillSubdivision subdivision,
        DrillAccentMode accentMode,
        int colsPerBar,
        TabDuration colDuration,
        int fixedWindowStartFret = 5)
    {
        var baseFret = Math.Max(capo + 1, fixedWindowStartFret);
        if (posMode == DrillPositionMode.FollowCaged && !string.Equals(activeCagedFilter, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var boxes = MusicTheoryCatalog.ComputeCagedBoxes(0, false, tuning, FretboardCanvas.TotalFrets);
            var matched = boxes.FirstOrDefault(b => string.Equals(b.ShapeLetter, activeCagedFilter, StringComparison.OrdinalIgnoreCase) && b.MinFret >= capo);
            if (matched is not null)
                baseFret = Math.Clamp(matched.MinFret, Math.Max(1, capo), 12);
        }

        int[] offsets = pattern == DrillPatternKind.Spider1324 ? [0, 2, 1, 3] : [0, 1, 2, 3];
        int[] revOffsets = pattern == DrillPatternKind.Spider1324 ? [3, 1, 2, 0] : [3, 2, 1, 0];

        var raw = new List<(int Str, int Fret, int Midi, string? ChordTag)>(48);
        for (var s = 0; s < 6; s++)
        {
            foreach (var off in offsets)
            {
                var f = baseFret + off;
                raw.Add((s, f, tuning.OpenMidi[s] + f, s == 0 && off == offsets[0] ? $"{baseFret}–{baseFret + 3}品上行" : null));
            }
        }
        for (var s = 5; s >= 0; s--)
        {
            foreach (var off in revOffsets)
            {
                var f = baseFret + off;
                raw.Add((s, f, tuning.OpenMidi[s] + f, s == 5 && off == revOffsets[0] ? "下行折返" : null));
            }
        }

        return PackIntoTabAndEvents(
            raw,
            0,
            "ALL",
            pattern == DrillPatternKind.Spider1324 ? "半音阶交替跨指 (1-3-2-4)" : "半音阶蜘蛛爬格子 (1-2-3-4)",
            $"固定 {baseFret}–{baseFret + 3} 品",
            (int)subdivision,
            colsPerBar,
            colDuration,
            subdivision,
            accentMode);
    }

    private static GeneratedScaleDrill GenerateCircleOfFifthsInPosition(
        int startTonic,
        GuitarTuning tuning,
        int capo,
        DrillPatternKind pattern,
        DrillSubdivision subdivision,
        DrillAccentMode accentMode,
        int colsPerBar,
        TabDuration colDuration,
        int fixedWindowStartFret = 5,
        int cofKeyCount = 5,
        int cofBarsPerKey = 2)
    {
        var stepSemitones = pattern == DrillPatternKind.CircleOf5thsPentatonic ? 7 : 5; // +5 semitones = up a 4th (CCW Circle of 5ths: C->F->Bb->Eb->Ab)
        var targetScale = MusicTheoryCatalog.GetScale(pattern == DrillPatternKind.CircleOf4thsMajor ? "major" : "major_pent");
        var winMin = Math.Clamp(Math.Max(capo, fixedWindowStartFret), 0, 12);
        var winMax = Math.Min(FretboardCanvas.TotalFrets, winMin + 3);

        var bars = new List<TabBar>();
        var events = new List<DrillStepEvent>();
        var notesPerBeat = (int)subdivision;
        var globalIdx = 0;

        var keyCount = Math.Clamp(cofKeyCount, 2, 12);
        var barsPerKey = Math.Clamp(cofBarsPerKey, 1, 4);
        var keyInfos = new List<(int Tonic, string KeyName, string CagedLetter, string RootHint)>(keyCount + 1);
        for (var k = 0; k <= keyCount; k++)
        {
            var t = (startTonic + k * stepSemitones) % 12;
            var pf = MusicTheoryCatalog.PreferFlatsForTonic(t);
            var kName = $"{MusicTheoryCatalog.NoteName(t, pf)} 大调";
            var boxes = MusicTheoryCatalog.ComputeCagedBoxes(t, false, tuning, FretboardCanvas.TotalFrets);
            var bestBox = boxes
                .OrderBy(b => Math.Abs((b.MinFret + b.MaxFret) / 2.0 - (winMin + winMax) / 2.0))
                .FirstOrDefault();
            var letter = bestBox?.ShapeLetter ?? "C";

            // Find lowest root inside window for preview hint
            var rootHint = "";
            for (var s = 0; s < 6; s++)
            {
                for (var f = winMin; f <= winMax + 1; f++)
                {
                    if ((tuning.OpenMidi[s] + f) % 12 == t)
                    {
                        rootHint = $"{6 - s}弦{f}品";
                        break;
                    }
                }
                if (!string.IsNullOrEmpty(rootHint)) break;
            }
            keyInfos.Add((t, kName, letter, rootHint));
        }

        for (var k = 0; k < keyCount; k++)
        {
            var cur = keyInfos[k];
            var nxt = keyInfos[k + 1];
            var spots = ExtractFixedWindowSpots(cur.Tonic, targetScale, tuning, winMin, winMax);
            if (spots.Count == 0) continue;

            var targetLen = colsPerBar * barsPerKey;
            var cycle = new List<(int Str, int Fret, int Midi)>(spots);
            for (var i = spots.Count - 2; i >= 1; i--)
                cycle.Add(spots[i]);

            var keyNotes = new List<(int Str, int Fret, int Midi)>(targetLen);
            for (var n = 0; n < targetLen; n++)
                keyNotes.Add(cycle[n % cycle.Count]);

            for (var b = 0; b < barsPerKey; b++)
            {
                var barIdx = bars.Count;
                var bar = new TabBar { ColumnCount = colsPerBar };
                if (b == 0)
                    bar.ChordMarks.Add(new TabChordMark { Column = 0, Text = $"#{k + 1} {cur.KeyName} [{cur.CagedLetter}形]" });

                for (var col = 0; col < colsPerBar; col++)
                {
                    var item = keyNotes[b * colsPerBar + col];
                    var isAccent = ComputeIsAccent(accentMode, col, globalIdx, notesPerBeat);
                    bar.Notes.Add(new TabNote
                    {
                        Column = col,
                        StringIndex = 5 - item.Str,
                        Fret = item.Fret.ToString()
                    });
                    bar.ColumnMetas.Add(new TabColumnMeta
                    {
                        Column = col,
                        Duration = colDuration,
                        Accent = isAccent,
                        TupletGroup = subdivision == DrillSubdivision.Triplet ? 3 : 0
                    });

                    events.Add(new DrillStepEvent(
                        StepIndex: events.Count,
                        BarIndex: barIdx,
                        Column: col,
                        StringIdx: item.Str,
                        Fret: item.Fret,
                        Midi: item.Midi,
                        IsAccent: isAccent,
                        IsBeatStart: col % notesPerBeat == 0,
                        IsBarDownbeat: col == 0,
                        EffectiveTonicPitch: cur.Tonic,
                        EffectiveCagedLetter: cur.CagedLetter,
                        KeyBannerText: $"当前：{cur.KeyName}（原地 {cur.CagedLetter} 形 · 根音 {cur.RootHint}）",
                        NextKeyPreviewText: $"下一调预告：{nxt.KeyName}（将原地切为 {nxt.CagedLetter} 形 · 根音 {nxt.RootHint}）"));
                    globalIdx++;
                }
                bars.Add(bar);
            }
        }

        var sec = new TabSection
        {
            Kind = TabSectionKind.Interlude,
            Title = $"同一把位五度圈 ({winMin}–{winMax}品锁死)",
            TimeSignature = "4/4",
            ColumnsPerBar = colsPerBar,
            ShowRhythmStems = true,
            Bars = bars
        };

        return new GeneratedScaleDrill(
            sec.Title,
            $"连续 {keyCount} 调 · 每调 {barsPerKey} 小节 · {string.Join(" → ", keyInfos.Take(Math.Min(6, keyCount)).Select(x => $"{x.KeyName}({x.CagedLetter}形)"))}{(keyCount > 6 ? " …" : "")}",
            sec,
            events,
            notesPerBeat);
    }

    public static (List<(int Str, int Fret, int Midi)> Spots, string CagedLetter, string PosDesc) ExtractPositionScaleSpots(
        int tonicPitch,
        ScaleDefinition scale,
        GuitarTuning tuning,
        int capo,
        DrillPositionMode posMode,
        string activeCagedFilter,
        int fixedWindowStartFret = 5,
        int threeNpsPosition = 1)
    {
        if (posMode == DrillPositionMode.FixedWindow5To8)
        {
            var winMin = Math.Clamp(Math.Max(capo, fixedWindowStartFret), 0, 12);
            var winMax = Math.Min(FretboardCanvas.TotalFrets, winMin + 3);
            return (ExtractFixedWindowSpots(tonicPitch, scale, tuning, winMin, winMax), "ALL", $"固定把位 {winMin}–{winMax} 品");
        }

        if (posMode == DrillPositionMode.ThreeNotesPerString)
        {
            var boxes3 = MusicTheoryCatalog.ComputeThreeNpsBoxes(tonicPitch, scale, tuning, FretboardCanvas.TotalFrets);
            var target3 = boxes3.FirstOrDefault(b => b.PositionNumber == threeNpsPosition && b.MinFret >= capo)
                          ?? boxes3.FirstOrDefault(b => b.MinFret >= capo);
            if (target3 is not null)
            {
                var list3 = new List<(int Str, int Fret, int Midi)>(18);
                for (var s = 0; s < 6; s++)
                {
                    foreach (var f in target3.StringFrets[s])
                        list3.Add((s, f, tuning.OpenMidi[s] + f));
                }
                return (list3, "ALL", $"3NPS 第 {target3.PositionNumber} 把位 ({target3.MinFret}–{target3.MaxFret}品)");
            }
            var spots3 = ExtractThreeNotesPerStringSpots(tonicPitch, scale, tuning, capo);
            if (spots3.Count >= 12)
                return (spots3, "ALL", "每弦三音 (3NPS)");
        }

        // CAGED Box position
        var boxes = MusicTheoryCatalog.ComputeCagedBoxes(tonicPitch, scale.IsMinorFamily, tuning, FretboardCanvas.TotalFrets);
        CagedBoxInstance? targetBox = null;
        if (!string.IsNullOrWhiteSpace(activeCagedFilter) && !string.Equals(activeCagedFilter, "ALL", StringComparison.OrdinalIgnoreCase))
            targetBox = boxes.FirstOrDefault(b => string.Equals(b.ShapeLetter, activeCagedFilter, StringComparison.OrdinalIgnoreCase) && b.MinFret >= capo);
        targetBox ??= boxes.FirstOrDefault(b => b.MinFret >= capo);

        if (targetBox is not null)
        {
            var scaleSet = new HashSet<int>(scale.Intervals.Select(iv => (tonicPitch + iv) % 12));
            var byMidi = new Dictionary<int, (int Str, int Fret, int Midi, int Penalty)>();

            for (var s = 0; s < 6; s++)
            {
                var minF = Math.Max(capo, targetBox.LeftFrets[s] - (scale.Intervals.Length > 5 ? 1 : 0));
                var maxF = Math.Min(FretboardCanvas.TotalFrets, targetBox.RightFrets[s] + (scale.Intervals.Length > 5 ? 1 : 0));
                for (var f = minF; f <= maxF; f++)
                {
                    var midi = tuning.OpenMidi[s] + f;
                    if (!scaleSet.Contains(((midi % 12) + 12) % 12)) continue;
                    var inCore = f >= targetBox.LeftFrets[s] && f <= targetBox.RightFrets[s];
                    var pen = inCore ? 0 : 10;
                    if (!byMidi.TryGetValue(midi, out var existing) || pen < existing.Penalty)
                        byMidi[midi] = (s, f, midi, pen);
                }
            }

            var ordered = byMidi.Values.OrderBy(x => x.Midi).Select(x => (x.Str, x.Fret, x.Midi)).ToList();
            return (ordered, targetBox.ShapeLetter, $"CAGED {targetBox.ShapeLetter} 形 ({targetBox.MinFret}–{targetBox.MaxFret}品)");
        }

        var fallbackMin = Math.Max(capo, 2);
        return (ExtractFixedWindowSpots(tonicPitch, scale, tuning, fallbackMin, fallbackMin + 4), "ALL", $"{fallbackMin}–{fallbackMin + 4} 品");
    }

    private static List<(int Str, int Fret, int Midi)> ExtractFixedWindowSpots(
        int tonicPitch,
        ScaleDefinition scale,
        GuitarTuning tuning,
        int minFret,
        int maxFret)
    {
        var scaleSet = new HashSet<int>(scale.Intervals.Select(iv => (tonicPitch + iv) % 12));
        var byMidi = new Dictionary<int, (int Str, int Fret, int Midi, int Penalty)>();

        for (var s = 0; s < 6; s++)
        {
            // Allow +1 fret shift on 2nd (B) string so major-third tuning gap never drops a scale degree!
            var sMin = s == 4 ? minFret : minFret;
            var sMax = Math.Min(FretboardCanvas.TotalFrets, s >= 4 ? maxFret + 1 : maxFret);
            for (var f = sMin; f <= sMax; f++)
            {
                var midi = tuning.OpenMidi[s] + f;
                if (!scaleSet.Contains(((midi % 12) + 12) % 12)) continue;
                var pen = f > maxFret ? 5 : 0;
                if (!byMidi.TryGetValue(midi, out var ex) || pen < ex.Penalty)
                    byMidi[midi] = (s, f, midi, pen);
            }
        }
        return byMidi.Values.OrderBy(x => x.Midi).Select(x => (x.Str, x.Fret, x.Midi)).ToList();
    }

    private static List<(int Str, int Fret, int Midi)> ExtractThreeNotesPerStringSpots(
        int tonicPitch,
        ScaleDefinition scale,
        GuitarTuning tuning,
        int capo)
    {
        var scaleSet = new HashSet<int>(scale.Intervals.Select(iv => (tonicPitch + iv) % 12));
        var list = new List<(int Str, int Fret, int Midi)>(18);
        var lastMidi = -1;

        for (var s = 0; s < 6; s++)
        {
            var addedOnStr = 0;
            for (var f = capo; f <= FretboardCanvas.TotalFrets; f++)
            {
                var midi = tuning.OpenMidi[s] + f;
                if (midi <= lastMidi) continue;
                if (!scaleSet.Contains(((midi % 12) + 12) % 12)) continue;
                list.Add((s, f, midi));
                lastMidi = midi;
                addedOnStr++;
                if (addedOnStr == 3) break;
            }
        }
        return list;
    }

    private static List<(int Str, int Fret, int Midi, string? ChordTag)> BuildPatternSequence(
        List<(int Str, int Fret, int Midi)> p,
        int tonicPitch,
        ScaleDefinition scale,
        DrillPatternKind pattern)
    {
        var res = new List<(int Str, int Fret, int Midi, string? ChordTag)>();
        var n = p.Count;
        if (n < 3) return res;

        void Add(int idx, string? tag = null)
        {
            var clamped = Math.Clamp(idx, 0, n - 1);
            res.Add((p[clamped].Str, p[clamped].Fret, p[clamped].Midi, tag));
        }

        switch (pattern)
        {
            case DrillPatternKind.ScaleUpDown:
                for (var i = 0; i < n; i++) Add(i, i == 0 ? "上行" : null);
                for (var i = n - 2; i >= 1; i--) Add(i, i == n - 2 ? "下行" : null);
                break;

            case DrillPatternKind.Seq3:
                for (var i = 0; i <= n - 3; i++) { Add(i); Add(i + 1); Add(i + 2); }
                for (var i = n - 1; i >= 2; i--) { Add(i); Add(i - 1); Add(i - 2); }
                break;

            case DrillPatternKind.Seq4:
                if (n >= 4)
                {
                    for (var i = 0; i <= n - 4; i++) { Add(i); Add(i + 1); Add(i + 2); Add(i + 3); }
                    for (var i = n - 1; i >= 3; i--) { Add(i); Add(i - 1); Add(i - 2); Add(i - 3); }
                }
                break;

            case DrillPatternKind.Seq5:
                if (n >= 5)
                {
                    for (var i = 0; i <= n - 5; i++)
                    {
                        for (var k = 0; k < 5; k++) Add(i + k);
                    }
                }
                break;

            case DrillPatternKind.Compound332Scale:
                // 8-note rhythmic cells grouped 3 + 3 + 2 stepping up the position
                for (var i = 0; i <= Math.Max(0, n - 4); i++)
                {
                    Add(i); Add(i + 1); Add(i + 2);
                    Add(i + 1); Add(i + 2); Add(Math.Min(n - 1, i + 3));
                    Add(i + 2); Add(Math.Min(n - 1, i + 3));
                }
                break;

            case DrillPatternKind.SkipPentatonic:
            case DrillPatternKind.Skip3rdParallel:
                for (var i = 0; i <= n - 3; i++) { Add(i); Add(i + 2); }
                for (var i = n - 1; i >= 2; i--) { Add(i); Add(i - 2); }
                break;

            case DrillPatternKind.SkipPentatonicReverse:
            case DrillPatternKind.Skip3rdReverse:
                for (var i = 0; i <= n - 3; i++) { Add(i + 2); Add(i); }
                for (var i = n - 1; i >= 2; i--) { Add(i - 2); Add(i); }
                break;

            case DrillPatternKind.Skip3rdAlternating:
                BuildSkipSequence(n, step: 2, alternating: true, Add);
                break;

            case DrillPatternKind.Skip4thParallel:
                BuildSkipSequence(n, step: 3, alternating: false, Add);
                break;

            case DrillPatternKind.Skip4thAlternating:
                BuildSkipSequence(n, step: 3, alternating: true, Add);
                break;

            case DrillPatternKind.Skip5thParallel:
                BuildSkipSequence(n, step: 4, alternating: false, Add);
                break;

            case DrillPatternKind.Skip5thAlternating:
                BuildSkipSequence(n, step: 4, alternating: true, Add);
                break;

            case DrillPatternKind.Skip6thParallel:
                BuildSkipSequence(n, step: 5, alternating: false, Add);
                break;

            case DrillPatternKind.Skip6thAlternating:
                BuildSkipSequence(n, step: 5, alternating: true, Add);
                break;

            case DrillPatternKind.ArpTriadUp:
            case DrillPatternKind.ArpTriadDown:
            case DrillPatternKind.ArpTriadAltUp:
            case DrillPatternKind.ArpTriadAltDown:
                BuildDiatonicArpeggios(p, tonicPitch, scale, isSeventh: false, pattern, res);
                break;

            case DrillPatternKind.Arp7thUp:
            case DrillPatternKind.Arp7thDown:
            case DrillPatternKind.Arp7thAltUp:
            case DrillPatternKind.Arp7thAltDown:
                BuildDiatonicArpeggios(p, tonicPitch, scale, isSeventh: true, pattern, res);
                break;
        }

        return res;
    }

    private static void BuildSkipSequence(int n, int step, bool alternating, Action<int, string?> add)
    {
        if (n <= step) return;
        for (var i = 0; i < n - step; i++)
        {
            if (!alternating || i % 2 == 0)
            {
                add(i, null);
                add(i + step, null);
            }
            else
            {
                add(i + step, null);
                add(i, null);
            }
        }
        for (var i = n - 1; i >= step; i--)
        {
            var revIdx = (n - 1) - i;
            if (!alternating || revIdx % 2 == 0)
            {
                add(i, null);
                add(i - step, null);
            }
            else
            {
                add(i - step, null);
                add(i, null);
            }
        }
    }

    private static void BuildDiatonicArpeggios(
        List<(int Str, int Fret, int Midi)> p,
        int tonicPitch,
        ScaleDefinition scale,
        bool isSeventh,
        DrillPatternKind kind,
        List<(int Str, int Fret, int Midi, string? ChordTag)> res)
    {
        var span = isSeventh ? 6 : 4;
        if (p.Count <= span) return;

        var preferFlats = MusicTheoryCatalog.PreferFlatsForTonic(tonicPitch);
        var diatonics = MusicTheoryCatalog.GetDiatonicChords(tonicPitch, scale.IsMinorFamily, preferFlats);

        string ResolveChordTag(int midi)
        {
            var pc = ((midi % 12) + 12) % 12;
            var match = diatonics.FirstOrDefault(d => d.RootPitch == pc);
            if (match is null) return MusicTheoryCatalog.NoteName(pc, preferFlats);
            return isSeventh ? match.SeventhSymbol : match.TriadSymbol;
        }

        var descendingDegrees = kind is DrillPatternKind.ArpTriadDown or DrillPatternKind.ArpTriadAltDown
                                     or DrillPatternKind.Arp7thDown or DrillPatternKind.Arp7thAltDown;
        var alternating = kind is DrillPatternKind.ArpTriadAltUp or DrillPatternKind.ArpTriadAltDown
                               or DrillPatternKind.Arp7thAltUp or DrillPatternKind.Arp7thAltDown;

        if (!descendingDegrees)
        {
            var groupCount = 0;
            for (var i = 0; i < p.Count - span; i++)
            {
                int[] idxs = isSeventh ? [i, i + 2, i + 4, i + 6] : [i, i + 2, i + 4];
                var tag = ResolveChordTag(p[i].Midi);
                if (alternating && groupCount % 2 == 1)
                    Array.Reverse(idxs);
                for (var k = 0; k < idxs.Length; k++)
                    res.Add((p[idxs[k]].Str, p[idxs[k]].Fret, p[idxs[k]].Midi, k == 0 ? tag : null));
                groupCount++;
            }
        }
        else
        {
            var groupCount = 0;
            for (var i = p.Count - 1 - span; i >= 0; i--)
            {
                int[] idxs = isSeventh ? [i + 6, i + 4, i + 2, i] : [i + 4, i + 2, i];
                var tag = ResolveChordTag(p[i].Midi);
                if (alternating && groupCount % 2 == 1)
                    Array.Reverse(idxs);
                for (var k = 0; k < idxs.Length; k++)
                    res.Add((p[idxs[k]].Str, p[idxs[k]].Fret, p[idxs[k]].Midi, k == 0 ? tag : null));
                groupCount++;
            }
        }
    }

    private static GeneratedScaleDrill PackIntoTabAndEvents(
        List<(int Str, int Fret, int Midi, string? ChordTag)> raw,
        int tonicPitch,
        string cagedLetter,
        string title,
        string subtitle,
        int notesPerBeat,
        int colsPerBar,
        TabDuration colDuration,
        DrillSubdivision subdivision,
        DrillAccentMode accentMode)
    {
        var bars = new List<TabBar>();
        var events = new List<DrillStepEvent>(raw.Count);

        for (var i = 0; i < raw.Count; i++)
        {
            var barIdx = i / colsPerBar;
            var col = i % colsPerBar;
            if (col == 0)
                bars.Add(new TabBar { ColumnCount = colsPerBar });

            var bar = bars[barIdx];
            var item = raw[i];
            var isAccent = ComputeIsAccent(accentMode, col, i, notesPerBeat);

            if (!string.IsNullOrWhiteSpace(item.ChordTag))
                bar.ChordMarks.Add(new TabChordMark { Column = col, Text = item.ChordTag! });

            bar.Notes.Add(new TabNote
            {
                Column = col,
                StringIndex = 5 - item.Str,
                Fret = item.Fret.ToString()
            });
            bar.ColumnMetas.Add(new TabColumnMeta
            {
                Column = col,
                Duration = colDuration,
                Accent = isAccent,
                TupletGroup = subdivision == DrillSubdivision.Triplet ? 3 : 0
            });

            events.Add(new DrillStepEvent(
                StepIndex: i,
                BarIndex: barIdx,
                Column: col,
                StringIdx: item.Str,
                Fret: item.Fret,
                Midi: item.Midi,
                IsAccent: isAccent,
                IsBeatStart: col % notesPerBeat == 0,
                IsBarDownbeat: col == 0,
                EffectiveTonicPitch: tonicPitch,
                EffectiveCagedLetter: cagedLetter,
                KeyBannerText: null,
                NextKeyPreviewText: null));
        }

        // Pad final partial bar with rests so rhythm stems stay mathematically complete
        if (bars.Count > 0 && raw.Count % colsPerBar != 0)
        {
            var lastBar = bars[^1];
            for (var c = raw.Count % colsPerBar; c < colsPerBar; c++)
            {
                lastBar.ColumnMetas.Add(new TabColumnMeta
                {
                    Column = c,
                    Duration = colDuration,
                    IsRest = true,
                    TupletGroup = subdivision == DrillSubdivision.Triplet ? 3 : 0
                });
            }
        }

        var sec = new TabSection
        {
            Kind = TabSectionKind.Interlude,
            Title = $"{title} · {subtitle}",
            TimeSignature = "4/4",
            ColumnsPerBar = colsPerBar,
            ShowRhythmStems = true,
            Bars = bars
        };

        return new GeneratedScaleDrill(title, subtitle, sec, events, notesPerBeat);
    }

    private static bool ComputeIsAccent(DrillAccentMode mode, int colInBar, int globalIdx, int notesPerBeat) =>
        mode switch
        {
            DrillAccentMode.BeatDownbeat => colInBar % notesPerBeat == 0,
            DrillAccentMode.GroupOf3CrossBeat => globalIdx % 3 == 0,
            DrillAccentMode.OffbeatSecond16th => colInBar % notesPerBeat == Math.Min(1, notesPerBeat - 1),
            DrillAccentMode.OffbeatUpbeat => notesPerBeat >= 4 ? colInBar % 4 == 2 : colInBar % notesPerBeat == notesPerBeat - 1,
            DrillAccentMode.Compound332 => (colInBar % 8) is 0 or 3 or 6,
            _ => false
        };
}

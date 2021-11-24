using Koioto.Support;
using Koioto.Support.FileReader;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Koioto.SamplePlugin.OpenTaikoChart
{
    public static class CourseParser
    {
        public static Playable Parse(OpenTaikoChartInfomation otci, OpenTaikoChartCourse otcc)
        {
            // メドレーではないので大きさは1。
            var playable = new Playable
            {
                Sections = new List<Chip>[1]
            };
            var sections = playable.Sections;
            sections[0] = new List<Chip>();
            var balloonIndex = 0;

            var courseJson = otcc;

            // パースする。
            var list = sections[0];

            // 初めから1秒開けておく。

            var nowTime = 0L;
            var nowBPM = otci.BPM ?? 120.0;
            var nowMeasure = new Measure(4, 4);
            var nowScroll = 1.0;
            var nowRotate = 0.0;
            var isGoGoTime = false;
            var measureCount = 0;
            var isFirstNoteInMeasure = true;
            var movieOffset = GetMovieOffset(otci.Background, otci.Movieoffset);
            var barVisible = true;
            Chip rollstartChip = null;

            // オフセットする。
            var offset = otci.Offset ?? 0;
            // オフセットが0未満だったらBGMの開始を0にしてそれ以降をずらす。
            // そうでないならそのままオフセットする。
            if (offset < 0)
            {
                //
                //var oneMeasure = GetMeasureDuration(nowMeasure, nowBPM);
                var bgmStartChip = new Chip
                {
                    ChipType = Chips.BGMStart,
                    Time = nowTime - (long)(Math.Abs(offset) * 1000.0 * 1000.0),
                    BPM = nowBPM
                };
                list.Add(bgmStartChip);
            }
            else
            {
                var bgmStartChip = new Chip
                {
                    ChipType = Chips.BGMStart,
                    Time = nowTime,
                    BPM = nowBPM
                };
                list.Add(bgmStartChip);
                // その時間分ずらす
                nowTime += (long)(offset * 1000.0 * 1000.0);
            }

            var origin = list.Where(c => c.ChipType == Chips.BGMStart).First();

            foreach (var measure in courseJson.Measures)
            {
                // その小節にいくつ音符があるかカウントする
                var notesCount = 0;
                var notesElementCount = 0;
                foreach (var line in measure)
                {
                    if (!line.Trim().StartsWith("#") && !string.IsNullOrWhiteSpace(line.Trim()))
                    {
                        // 命令行ではない
                        notesElementCount++;
                        foreach (var digit in line)
                        {
                            if (digit >= '0' && digit <= '9')
                            {
                                // 数字だ
                                notesCount++;
                            }
                        }
                    }
                }

                foreach (var line in measure)
                {
                    // 行
                    // ひとつの音符あたりの数
                    var timePerNotes = (long)(GetMeasureDuration(nowMeasure, nowBPM) / notesCount);

                    if (!line.Trim().StartsWith("#"))
                    {
                        // 命令行ではない
                        if (isFirstNoteInMeasure)
                        {
                            // 小節
                            var measureChip = new Chip
                            {
                                ChipType = Chips.Measure,
                                CanShow = barVisible,
                                Scroll = nowScroll,
                                Direction = nowRotate.ToRad(),
                                BPM = nowBPM,
                                IsGoGoTime = isGoGoTime,
                                Measure = nowMeasure,
                                MeasureCount = measureCount,
                                Time = nowTime
                            };
                            list.Add(measureChip);

                            isFirstNoteInMeasure = false;
                        }
                        foreach (var digit in line)
                        {
                            if (digit >= '0' && digit <= '9')
                            {
                                // 数字だ
                                var note = GetNotesFromChar(digit);

                                // 音符の追加
                                var noteChip = new Chip
                                {
                                    ChipType = Chips.Note,
                                    NoteType = note,
                                    Scroll = nowScroll,
                                    Direction = nowRotate.ToRad(),
                                    BPM = nowBPM,
                                    CanShow = true,
                                    IsGoGoTime = isGoGoTime,
                                    Measure = nowMeasure,
                                    MeasureCount = measureCount,
                                    Time = nowTime
                                };

                                if (note == Notes.RollStart || note == Notes.ROLLStart || note == Notes.Balloon)
                                {
                                    // 連打開始なので記憶しておく。
                                    rollstartChip = noteChip;

                                    if (note == Notes.Balloon)
                                    {
                                        if (courseJson.Balloon != null)
                                        {
                                            if (courseJson.Balloon.Length > balloonIndex)
                                            {
                                                noteChip.RollObjective = courseJson.Balloon[balloonIndex] ?? DefaultBalloon;
                                                balloonIndex++;
                                            }
                                            else
                                            {
                                                noteChip.RollObjective = DefaultBalloon;
                                            }
                                        }
                                        else
                                        {
                                            noteChip.RollObjective = DefaultBalloon;
                                        }
                                    }
                                }
                                else if (note == Notes.RollEnd)
                                {
                                    // 連打開始なので記憶した連打をrollendにり当てる。
                                    if (rollstartChip != null)
                                    {
                                        rollstartChip.RollEnd = noteChip;
                                        rollstartChip = null;
                                    }
                                }

                                list.Add(noteChip);

                                // 時間をひとつすすめる
                                nowTime += timePerNotes;
                            }
                        }
                    }
                    else
                    {
                        // 命令行
                        var eventChip = new Chip();

                        var command = line.Trim();
                        var param = command.IndexOf(' ') >= 0 ? command.Substring(command.IndexOf(' ')) : "";

                        bool commandMatch(string name)
                        {
                            return command.StartsWith(name, StringComparison.InvariantCultureIgnoreCase);
                        }

                        if (commandMatch("#bpm"))
                        {
                            // #bpm n1
                            if (double.TryParse(param, out var bpm))
                            {
                                eventChip.ChipType = Chips.BPMChange;
                                nowBPM = bpm;
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else if (commandMatch("#scroll"))
                        {
                            // #scroll n1
                            if (double.TryParse(param, out var scroll))
                            {
                                eventChip.ChipType = Chips.ScrollChange;
                                nowScroll = scroll;
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else if (commandMatch("#rotate"))
                        {
                            // #rotate n1
                            if (double.TryParse(param, out var rotate))
                            {
                                eventChip.ChipType = Chips.RotateChange;
                                nowRotate = rotate;
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else if (commandMatch("#tsign"))
                        {
                            // #tsign n1/n2
                            var n1 = param.Substring(0, param.IndexOf('/'));
                            var n2 = param.Substring(param.IndexOf('/') + 1);

                            if (double.TryParse(n1, out var part) && double.TryParse(n2, out var beat))
                            {
                                eventChip.ChipType = Chips.MeasureChange;
                                nowMeasure = new Measure(part, beat);
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else if (commandMatch("#gogobegin"))
                        {
                            eventChip.ChipType = Chips.GoGoStart;
                            isGoGoTime = true;
                        }
                        else if (commandMatch("#gogoend"))
                        {
                            eventChip.ChipType = Chips.GoGoEnd;
                            isGoGoTime = false;
                        }
                        else if (commandMatch("#delay"))
                        {
                            // #delay n1
                            if (double.TryParse(param, out var delay))
                            {
                                nowTime += (long)(delay * 1000.0 * 1000.0);
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else if (commandMatch("#bar"))
                        {
                            // #bar show/hide
                            var visible = param.Trim();
                            if (visible == "show")
                            {
                                barVisible = true;
                            }
                            else if (visible == "hide")
                            {
                                barVisible = false;
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            continue;
                        }

                        eventChip.Scroll = nowScroll;
                        eventChip.Direction = nowRotate.ToRad();
                        eventChip.BPM = nowBPM;
                        eventChip.IsGoGoTime = isGoGoTime;
                        eventChip.Measure = nowMeasure;
                        eventChip.MeasureCount = measureCount;
                        eventChip.Time = nowTime;
                        list.Add(eventChip);
                    }
                }

                // ノーツが空だったときの処理
                if (notesElementCount <= 0)
                {
                    // 小節
                    var measureChip = new Chip
                    {
                        ChipType = Chips.Measure,
                        CanShow = barVisible,
                        Scroll = nowScroll,
                        Direction = nowRotate.ToRad(),
                        BPM = nowBPM,
                        IsGoGoTime = isGoGoTime,
                        Measure = nowMeasure,
                        MeasureCount = measureCount,
                        Time = nowTime
                    };
                    list.Add(measureChip);

                    nowTime += (long)GetMeasureDuration(nowMeasure, nowBPM);
                }


                measureCount++;
                isFirstNoteInMeasure = true;
            }

            // 後処理。

            // 動画の開始時間を表すチップを挿入する。
            if (movieOffset.HasValue)
            {
                var offsetValue = movieOffset.Value;
                // 0未満だったら、全体をずらす必要がある。
                if (offsetValue < 0)
                {
                    // すべてのチップの時間をずらす
                    list.ForEach(c => c.Time += (long)(Math.Abs(offsetValue) * 1000.0 * 1000.0));

                    var offsetChip = new Chip
                    {
                        ChipType = Chips.MovieStart,
                        Time = origin.Time - (long)(Math.Abs(offsetValue) * 1000.0 * 1000.0)
                    };
                    //list.Add(offsetChip);
                    var nearestChip = list.Where(c => c.Time <= offsetChip.Time);
                    list.Insert(nearestChip.Count() > 0 ? list.IndexOf(nearestChip.Last()) + 1 : 0, offsetChip);
                }
                else
                {
                    // そのまま入れることができる。
                    var offsetChip = new Chip
                    {
                        ChipType = Chips.MovieStart,
                        Time = origin.Time + (long)(Math.Abs(offsetValue) * 1000.0 * 1000.0)
                    };
                    var nearestChip = list.Where(c => c.Time <= offsetChip.Time);
                    list.Insert(nearestChip.Count() > 0 ? list.IndexOf(nearestChip.Last()) + 1 : 0, offsetChip);
                }
            }

            // オフセットを追加する。前に1小節、後ろに1小節。
            {
                // 3秒。
                var offsetTime = 3L * 1000 * 1000;
                {
                    // 前
                    var origBPM = origin.BPM;
                    list.ForEach(c => c.Time += offsetTime);
                }
                {
                    // 後
                    var last = list.Last();
                    var lastChip = new Chip
                    {
                        BPM = last.BPM,
                        Scroll = last.Scroll,
                        CanShow = false,
                        ChipType = Chips.Measure,
                        Time = last.Time + offsetTime
                    };

                    list.Add(lastChip);
                }
            }

            return playable;
        }

        /// <summary>
        /// ふうせん連打のデフォルトの目標打数。
        /// </summary>
        private static readonly int DefaultBalloon = 5;

        /// <summary>
        /// 1小節の時間を求める。
        /// </summary>
        /// <param name="measure">n分のn拍子。</param>
        /// <param name="bpm">BPM。</param>
        /// <returns>1小節の時間(秒)</returns>
        private static double GetMeasureDuration(Measure measure, double bpm)
        {
            return measure.GetRate() / bpm * 1000 * 1000.0;
        }

        /// <summary>
        /// 弧度法の角度に変換する。
        /// </summary>
        /// <param name="deg">度数法の角度。</param>
        /// <returns>弧度法の角度。</returns>
        private static double ToRad(this double deg)
        {
            return deg * Math.PI / 180.0;
        }

        private static Notes GetNotesFromChar(char ch)
        {
            switch (ch)
            {
                case '0': return Notes.Space;
                case '1': return Notes.Don;
                case '2': return Notes.Ka;
                case '3': return Notes.DON;
                case '4': return Notes.KA;
                case '5': return Notes.RollStart;
                case '6': return Notes.ROLLStart;
                case '7': return Notes.Balloon;
                case '8': return Notes.RollEnd;
                default: return Notes.Space;
            }
        }

        private static double? GetMovieOffset(string path, double? movieoffset)
        {
            if (Util.GetMimeType(path).StartsWith("video"))
            {
                return movieoffset ?? 0;
            }
            return null;
        }
    }
}
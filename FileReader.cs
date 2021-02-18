using Koioto.Support;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Text;

namespace Koioto.SamplePlugin.OpenTaikoChart
{
    /// <summary>
    /// Open Taiko Chart Rev2.2 に準拠したクラス。
    /// </summary>
    public class FileReader : Koioto.Plugin.IFileReadable
    {
        public string Name => "OpenTaikoChart";

        public string[] Creator => new string[] { "AioiLight" };

        public string Description => "Koioto file Reader plugin for Open Taiko Chart.";

        public string Version => "2.1";

        public string[] GetExtensions()
        {
            // サポートする拡張子をreturn。
            return new string[] { ".tci", ".tcm" };
        }

        public Infomationable GetSelectable(string filePath)
        {
            if (Path.GetExtension(filePath) == ".tci")
            {
                return TCIParser(filePath);
            }
            else if (Path.GetExtension(filePath) == ".tcm")
            {
                return TCMParser(filePath);
            }
            return null;
        }

        private Infomationable TCIParser(string filePath)
        {
            var info = GetOpenTaikoChartInfomation(filePath);
            var result = new Infomationable
            {
                FilePath = filePath,
                Title = info.Title,
                SubTitle = info.Subtitle,
                BPM = info.BPM,
                Artist = info.Artist,
                Creator = info.Creator,
                PreviewSong = info.Audio != null ? Path.Combine(Path.GetDirectoryName(filePath), info.Audio) : null,
                SongPreviewTime = info.SongPreview,
                AlbumartPath = info.Albumart != null ? Path.Combine(Path.GetDirectoryName(filePath), info.Albumart) : null
            };

            foreach (var item in info.Courses)
            {
                var diff = new Difficulty();
                diff.Level = item.Level;
                result[GetCoursesFromStirng(item.Difficulty)] = diff;
            }

            return result;
        }

        private Infomationable TCMParser(string filePath)
        {
            // not implemented
            return null;
        }

        public Player<Playable> GetPlayable(string filePath, Koioto.Support.FileReader.Courses courses)
        {
            if (Path.GetExtension(filePath) == ".tci")
            {
                return TCIPlayable(filePath, courses);
            }
            else if (Path.GetExtension(filePath) == ".tcm")
            {
                return TCMPlayable(filePath, courses);
            }
            return null;
        }

        public ChartInfo GetChartInfo(string filePath)
        {
            if (Path.GetExtension(filePath) == ".tci")
            {
                return TCIChartInfo(filePath);
            }
            else if (Path.GetExtension(filePath) == ".tcm")
            {
                return TCMChartInfo(filePath);
            }
            return null;
        }

        private ChartInfo TCIChartInfo(string filePath)
        {
            var info = GetOpenTaikoChartInfomation(filePath);

            var chartInfo = new ChartInfo();

            chartInfo.Title = new string[1];
            chartInfo.Title[0] = info.Title;

            chartInfo.Subtitle = new string[1];
            chartInfo.Subtitle[0] = info.Subtitle;

            chartInfo.Artist = new string[1][];
            if (info.Artist != null)
            {
                chartInfo.Artist[0] = new string[info.Artist.Length];
                chartInfo.Artist[0] = info.Artist;
            }

            chartInfo.Creator = new string[1][];
            if (info.Creator != null)
            {
                chartInfo.Creator[0] = new string[info.Creator.Length];
                chartInfo.Creator[0] = info.Creator;
            }

            chartInfo.Audio = new string[1];
            chartInfo.Audio[0] = info.Audio != null ? Path.Combine(Path.GetDirectoryName(filePath), info.Audio) : null;

            chartInfo.Background = new string[1];
            chartInfo.Background[0] = info.Background != null ? Path.Combine(Path.GetDirectoryName(filePath), info.Background) : null;

            chartInfo.Movieoffset = new double?[1];
            chartInfo.Movieoffset[0] = info.Movieoffset;

            chartInfo.BPM = new double?[1];
            chartInfo.BPM[0] = info.BPM;

            chartInfo.Offset = new double?[1];
            chartInfo.Offset[0] = info.Offset;

            return chartInfo;
        }

        private ChartInfo TCMChartInfo(string filePath)
        {
            // not implemented
            return null;
        }

        private Player<Playable> TCIPlayable(string filePath, Koioto.Support.FileReader.Courses course)
        {
            var info = GetOpenTaikoChartInfomation(filePath);

            var result = new Player<Playable>();

            // とりあえず、難易度を入れる
            var neededCourse = info.Courses.Where(d => GetCoursesFromStirng(d.Difficulty) == course);

            if (!neededCourse.Any())
            {
                // コースが無ければnullを返す。
                return null;
            }

            // コースが存在すれば処理を続行。
            var diff = neededCourse.First();

            var singleFile = File.ReadAllText(Path.Combine(Path.GetDirectoryName(filePath), diff.Single), Encoding.UTF8);

            var multipleFile = new string[0];
            if (diff.Multiple != null)
            {
                multipleFile = new string[diff.Multiple.Length];
                for (var i = 0; i < multipleFile.Length; i++)
                {
                    multipleFile[i] = File.ReadAllText(Path.Combine(Path.GetDirectoryName(filePath), diff.Multiple[i]), Encoding.UTF8);
                }
            }

            var single = JsonConvert.DeserializeObject<OpenTaikoChartCourse>(singleFile);
            var multiple = new OpenTaikoChartCourse[multipleFile.Length];
            for (var i = 0; i < multiple.Length; i++)
            {
                multiple[i] = JsonConvert.DeserializeObject<OpenTaikoChartCourse>(multipleFile[i]);
            }

            result.Single = CourseParser.Parse(info, single);
            result.Multiple = multiple.Select(m => CourseParser.Parse(info, m)).ToArray();

            return result;
        }

        private Player<Playable> TCMPlayable(string filePath, Koioto.Support.FileReader.Courses courses)
        {
            var medley = GetOpenTaikoChartMedley(filePath);

            var chartInfo = new ChartInfo();

            chartInfo.Title = new string[medley.Charts.Length];

            // コースの読み込み。
            foreach (var item in medley.Charts)
            {
                var course = GetCoursesFromStirng(item.Difficulty);
            }

            return null;
        }

        private static OpenTaikoChartInfomation GetOpenTaikoChartInfomation(string filePath)
        {
            var infoText = File.ReadAllText(filePath, Encoding.UTF8);
            var info = JsonConvert.DeserializeObject<OpenTaikoChartInfomation>(infoText);
            return info;
        }

        private static OpenTaikoChart_Medley GetOpenTaikoChartMedley(string filePath)
        {
            var medleyText = File.ReadAllText(filePath, Encoding.UTF8);
            var medley = JsonConvert.DeserializeObject<OpenTaikoChart_Medley>(medleyText);
            return medley;
        }

        private Koioto.Support.FileReader.Courses GetCoursesFromStirng(string str)
        {
            switch (str)
            {
                case "easy":
                    return Support.FileReader.Courses.Easy;

                case "normal":
                    return Support.FileReader.Courses.Normal;

                case "hard":
                    return Support.FileReader.Courses.Hard;

                case "edit":
                    return Support.FileReader.Courses.Edit;

                case "oni":
                default:
                    return Support.FileReader.Courses.Oni;
            }
        }
    }

    public class OpenTaikoChartInfomation
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string[] Artist { get; set; }
        public string[] Creator { get; set; }
        public string Audio { get; set; }
        public string Background { get; set; }
        public double? Movieoffset { get; set; }
        public double? BPM { get; set; }
        public double? Offset { get; set; }
        public double? SongPreview { get; set; }
        public string Albumart { get; set; }
        public OpenTaikoChartInfomation_Courses[] Courses { get; set; }
    }

    public class OpenTaikoChartInfomation_Courses
    {
        public string Difficulty { get; set; }
        public int? Level { get; set; }
        public string Single { get; set; }
        public string[] Multiple { get; set; }
    }

    public class OpenTaikoChartCourse
    {
        public int? Scoreinit { get; set; }
        public int? Scorediff { get; set; }
        public int? Scoreshinuchi { get; set; }
        public int?[] Balloon { get; set; }
        public string[][] Measures { get; set; }
    }

    public class OpenTaikoChart_Difficluty
    {
        public OpenTaikoChartCourse Single { get; set; }
        public OpenTaikoChartCourse[] Multiple { get; set; }
    }

    public class OpenTaikoChart_Medley
    {
        public string Title { get; set; }
        public OpenTaikoChart_Medley_Exams[] Exams { get; set; }

        public OpenTaikoChart_Medley_Charts[] Charts { get; set; }
    }

    public class OpenTaikoChart_Medley_Exams
    {
        public string Type { get; set; }
        public string Range { get; set; }
        public int[] Value { get; set; }
    }

    public class OpenTaikoChart_Medley_Charts
    {
        public string File { get; set; }
        public string Difficulty { get; set; }
    }
}
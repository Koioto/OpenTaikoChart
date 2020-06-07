using System.IO;
using System.Linq;
using System.Text;
using Koioto.Support;
using Newtonsoft.Json;

namespace Koioto.SamplePlugin.OpenTaikoChart
{
    /// <summary>
    /// Open Taiko Chart Rev.1 に準拠したクラス。
    /// </summary>
    public class FileReader : Koioto.Plugin.IFileReadable
    {
        public string Name => "Open Taiko Chart";

        public string[] Creator => new string[] { "AioiLight" };

        public string Description => "Koioto file Reader plugin for Open Taiko Chart.";

        public string Version => "1.3";

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
            var infoText = File.ReadAllText(filePath, Encoding.UTF8);
            var info = JsonConvert.DeserializeObject<OpenTaikoChartInfomation>(infoText);

            var courses = new OpenTaikoChart_Difficluty[info.Courses.Length];
            for (var courseIndex = 0; courseIndex < courses.Length; courseIndex++)
            {
                // SP譜面
                var path = Path.Combine(Path.GetDirectoryName(filePath), info.Courses[courseIndex].Single);
                if (!File.Exists(path))
                {
                    courses[courseIndex] = null;
                    continue;
                }
                var single = File.ReadAllText(path, Encoding.UTF8);
                courses[courseIndex] = new OpenTaikoChart_Difficluty();
                courses[courseIndex].Single = JsonConvert.DeserializeObject<OpenTaikoChartCourse>(single);

                // DP譜面
                if (info.Courses[courseIndex].Multiple == null)
                {
                    continue;
                }

                var multipleAmount = info.Courses[courseIndex].Multiple.Length;
                courses[courseIndex].Multiple = new OpenTaikoChartCourse[multipleAmount];
                for (int multipleIndex = 0; multipleIndex < multipleAmount; multipleIndex++)
                {
                    var multiple = File.ReadAllText(Path.Combine(Path.GetDirectoryName(filePath), info.Courses[courseIndex].Multiple[multipleIndex]), Encoding.UTF8);
                    courses[courseIndex].Multiple[multipleIndex] = JsonConvert.DeserializeObject<OpenTaikoChartCourse>(multiple);
                }
            }

            var result = new Infomationable();
            result.FilePath = filePath;
            result.Title = info.Title;
            result.SubTitle = info.Subtitle;
            result.BPM = info.BPM;
            result.Artist = info.Artist;
            result.Creator = info.Creator;
            if (info.Audio != null)
            {
                result.PreviewSong = Path.Combine(Path.GetDirectoryName(filePath), info.Audio);
            }
            else
            {
                result.PreviewSong = null;
            }
            result.SongPreviewTime = info.SongPreview;
            result.AlbumartPath = info.Albumart;

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

        public (Playable[], ChartInfo) GetPlayable(string filePath, Koioto.Support.FileReader.Courses courses)
        {
            if (Path.GetExtension(filePath) == ".tci")
            {
                return TCIPlayable(filePath, courses);
            }
            else if (Path.GetExtension(filePath) == ".tcm")
            {
                return TCMPlayable(filePath, courses);
            }
            return (null, null);
        }

        private (Playable[], ChartInfo) TCIPlayable(string filePath, Koioto.Support.FileReader.Courses course)
        {
            var infoText = File.ReadAllText(filePath, Encoding.UTF8);
            var info = JsonConvert.DeserializeObject<OpenTaikoChartInfomation>(infoText);

            var courses = new OpenTaikoChart_Difficluty[info.Courses.Length];

            // とりあえず、難易度を入れる

            var neededCourse = info.Courses.Where(d => GetCoursesFromStirng(d.Difficulty) == course);

            if (!neededCourse.Any())
            {
                // コースが無ければnullを返す。
                return (null, null);
            }

            // コースが存在すれば処理を続行。
            var diff = neededCourse.First();

            var singleFile = File.ReadAllText(Path.Combine(Path.GetDirectoryName(filePath), diff.Single), Encoding.UTF8);

            var multipleFile = new string[0];
            if (diff.Multiple != null)
            {
                multipleFile = new string[diff.Multiple.Length];
                for (int i = 0; i < multipleFile.Length; i++)
                {
                    multipleFile[i] = File.ReadAllText(Path.Combine(Path.GetDirectoryName(filePath), diff.Multiple[i]), Encoding.UTF8);
                }
            }

            var single = JsonConvert.DeserializeObject<OpenTaikoChartCourse>(singleFile);
            var multiple = new OpenTaikoChartCourse[multipleFile.Length];
            for (int i = 0; i < multiple.Length; i++)
            {
                multiple[i] = JsonConvert.DeserializeObject<OpenTaikoChartCourse>(multipleFile[i]);
            }

            var resultJson = new OpenTaikoChartCourse[] { single }.Concat(multiple);
            var playable = resultJson.Select(c => CourseParser.Parse(info, c)).ToArray();

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
            if (info.Audio != null)
            {
                chartInfo.Audio[0] = Path.Combine(Path.GetDirectoryName(filePath), info.Audio);
            }
            else
            {
                chartInfo.Audio[0] = null;
            }

            chartInfo.Background = new string[1];
            if (info.Background != null)
            {
                chartInfo.Background[0] = Path.Combine(Path.GetDirectoryName(filePath), info.Background);
            }
            else
            {
                chartInfo.Background[0] = null;
            }

            chartInfo.Movieoffset = new double?[1];
            chartInfo.Movieoffset[0] = info.Movieoffset;

            chartInfo.BPM = new double?[1];
            chartInfo.BPM[0] = info.BPM;

            chartInfo.Offset = new double?[1];
            chartInfo.Offset[0] = info.Offset;

            return (playable, chartInfo);
        }

        private (Playable[], ChartInfo) TCMPlayable(string filePath, Koioto.Support.FileReader.Courses courses)
        {
            return (null, null);
        }

        private Koioto.Support.FileReader.Courses GetCoursesFromStirng(string str)
        {
            switch (str)
            {
                case "easy":
                    return Koioto.Support.FileReader.Courses.Easy;

                case "normal":
                    return Koioto.Support.FileReader.Courses.Normal;

                case "hard":
                    return Koioto.Support.FileReader.Courses.Hard;

                case "edit":
                    return Koioto.Support.FileReader.Courses.Edit;

                case "oni":
                default:
                    return Koioto.Support.FileReader.Courses.Oni;
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
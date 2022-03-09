using Koioto.Support;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Text;

namespace Koioto.SamplePlugin.OpenTaikoChart
{
    /// <summary>
    /// Open Taiko Chart Rev2.3 に準拠したクラス。
    /// </summary>
    public class FileReader : Koioto.Plugin.IChartReadable
    {
        public string Name => "OpenTaikoChart";

        public string[] Creator => new string[] { "AioiLight" };

        public string Description => "Koioto file Reader plugin for Open Taiko Chart.\n" +
            "Supported Rev.: 2.4";

        public string Version => "3.0";

        public string[] GetExtensions()
        {
            // サポートする拡張子をreturn。
            return new string[] { ".tci", ".tcm"};
        }

        public SongSelectMetadata GetSelectable(string filePath)
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

        private SongSelectMetadata TCIParser(string filePath)
        {
            var info = GetOpenTaikoChartInfomation(filePath);
            var result = new SongSelectMetadata
            {
                FilePath = filePath,
                Title = info.Title,
                SubTitle = info.Subtitle,
                BPM = info.BPM,
                Artist = info.Artist,
                Creator = info.Creator,
                PreviewSong = info.Audio != null ? GetPath(filePath, info.Audio) : null,
                SongPreviewTime = info.SongPreview,
                AlbumartPath = info.Albumart != null ? GetPath(filePath, info.Albumart) : null
            };

            foreach (var item in info.Courses)
            {
                var diff = new Difficulty();
                diff.Level = item.Level;
                result[GetCoursesFromStirng(item.Difficulty)] = diff;
            }

            return result;
        }

        private SongSelectMetadata TCMParser(string filePath)
        {
            var medley = GetOpenTaikoChartMedley(filePath);
            var charts = medley.Charts.Select(c => TCIParser(GetPath(filePath, c.File)));

            var result = new SongSelectMetadata
            {
                FilePath = filePath,
                Title = medley.Title,
                SubTitle = medley.Subtitle,
                BPM = null,
                Artist = charts.SelectMany(c => c.Artist).ToArray(),
                Creator = charts.SelectMany(c => c.Creator).ToArray(),
                PreviewSong = charts.First().PreviewSong,
                SongPreviewTime = charts.First().SongPreviewTime,
                AlbumartPath =
                    string.IsNullOrWhiteSpace(medley.Albumart)
                        ? charts.First().AlbumartPath : GetPath(filePath, medley.Albumart)
            };

            if (medley.Exams != null && medley.Exams.Length > 0)
            {
                result[Support.FileReader.Courses.Dan] = new Difficulty();
            }
            else
            {
                var course = GetCoursesFromStirng(medley.Charts.First().Difficulty);
                result[course] = new Difficulty()
                {
                    Level = charts.First()[course].Level
                };
            }

            return result;
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

        public ChartMetadata GetChartInfo(string filePath)
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

        private ChartMetadata TCIChartInfo(string filePath)
        {
            var info = GetOpenTaikoChartInfomation(filePath);

            var chartMetadata = new ChartMetadata();

            chartMetadata.Title = new string[1];
            chartMetadata.Title[0] = info.Title;

            chartMetadata.Subtitle = new string[1];
            chartMetadata.Subtitle[0] = info.Subtitle;

            chartMetadata.Artist = new string[1][];
            if (info.Artist != null)
            {
                chartMetadata.Artist[0] = new string[info.Artist.Length];
                chartMetadata.Artist[0] = info.Artist;
            }

            chartMetadata.Creator = new string[1][];
            if (info.Creator != null)
            {
                chartMetadata.Creator[0] = new string[info.Creator.Length];
                chartMetadata.Creator[0] = info.Creator;
            }

            chartMetadata.Audio = new string[1];
            chartMetadata.Audio[0] = info.Audio != null ? GetPath(filePath, info.Audio) : null;

            chartMetadata.Background = new string[1];
            chartMetadata.Background[0] = info.Background != null ? GetPath(filePath, info.Background) : null;

            chartMetadata.Movieoffset = new double?[1];
            chartMetadata.Movieoffset[0] = info.Movieoffset;

            chartMetadata.BPM = new double?[1];
            chartMetadata.BPM[0] = info.BPM;

            chartMetadata.Offset = new double?[1];
            chartMetadata.Offset[0] = info.Offset;

            return chartMetadata;
        }

        private ChartMetadata TCMChartInfo(string filePath)
        {
            var medley = GetOpenTaikoChartMedley(filePath);
            var charts = medley.Charts.Select(
                c => TCIChartInfo(GetPath(filePath, c.File)));

            var chartMetadata = new ChartMetadata();

            var sections = medley.Charts.Length;

            chartMetadata.Title = charts.SelectMany(c => c.Title).ToArray();

            chartMetadata.Subtitle = charts.SelectMany(c => c.Subtitle).ToArray();

            chartMetadata.Artist = charts.SelectMany(c => c.Artist).ToArray();

            chartMetadata.Creator = charts.SelectMany(c => c.Creator).ToArray();

            chartMetadata.Audio = charts.SelectMany(c => c.Audio).ToArray();

            chartMetadata.Background = charts.SelectMany(c => c.Background).ToArray();

            chartMetadata.Movieoffset = charts.SelectMany(c => c.Movieoffset).ToArray();

            chartMetadata.BPM = charts.SelectMany(c => c.BPM).ToArray();

            chartMetadata.Offset = charts.SelectMany(c => c.Offset).ToArray();

            return chartMetadata;
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

            var singleFile = File.ReadAllText(GetPath(filePath, diff.Single), Encoding.UTF8);

            var multipleFile = new string[0];
            if (diff.Multiple != null)
            {
                multipleFile = new string[diff.Multiple.Length];
                for (var i = 0; i < multipleFile.Length; i++)
                {
                    multipleFile[i] = File.ReadAllText(GetPath(filePath, diff.Multiple[i]), Encoding.UTF8);
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

            var charts = medley.Charts.Select(
                c => TCIPlayable(GetPath(filePath, c.File), GetCoursesFromStirng(c.Difficulty)));

            var result = new Player<Playable>();
            result.Single = new Playable();
            result.Single.Sections = charts.SelectMany(c => c.Single.Sections).ToArray();

            return result;
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

        private string GetPath(string origin, string target)
        {
            return Path.Combine(Path.GetDirectoryName(origin), target);
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
        public string Subtitle { get; set; }
        public string Albumart { get; set; }
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
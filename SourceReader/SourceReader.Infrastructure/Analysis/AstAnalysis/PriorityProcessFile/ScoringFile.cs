using SourceReader.Infrastructure.DataModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace SourceReader.Infrastructure.Analysis.AstAnalysis.PriorityProcessFile
{
    public class ScoringFile
    {
        public static void CalculateFiles(ProjectIndex index)
        {
            var sizes = index.Files.Values.Select(f => (double)f.FileSize).ToList();
            var mean = sizes.Average();

            // tính độ lệch chuẩn để đánh giá mức độ phân tán của kích thước file, từ đó xác định mức độ quan trọng của file dựa trên kích thước so với trung bình
            // exp : if stdDev =13 kb => majority file have size range from +- 13kb around mean size , so we can use that to calc score of size file
            var stdDev = Math.Sqrt(sizes.Select(s => Math.Pow(s - mean, 2)).Average());

            foreach (var (id, file) in index.Files)
            {
                var inDegree = index.InEdge.TryGetValue(id, out var inEdges) ? inEdges.Count : 0;
                var score = CalcFileScore(file, inDegree, mean, stdDev);

                //with owr đây để tạo bản sao của file record với giá trị inDegree và score mới mà không thay đổi các thuộc tính khác
                index.Files[id] = file with
                {
                    InDegree = inDegree,
                    PriorityScore = score
                };
            }
        }

        public static double CalcFileScore(SRFileRecord file, int inDegree, double mean, double stdDev)
        {
            // file have more file import it => more important
            var s1 = Math.Min(inDegree * 15, 80);
            //if all file have same size => stdDev =0 => have a problem devide by 0 that's why have to check stdDev>0
            //when calculate s2 , with size < avg of mean => z <0 => score be negative  =>
            var z = stdDev > 0 ? (file.FileSize - mean) / stdDev : 0;
            // file be less important (that seem not like what we want now) so set it >=0
            var s2 = Math.Min(Math.Max(z * 20, 0), 60);
            //file name contains config, setting => important
            var s4 = ConfigScore(file.FileName);
            // file have depth lower => more near to root => more important
            var s3 = Math.Max(0.3, 1.0 - file.Depth * 0.15);
            // file with name contains layer or popular important file in many language like core, main..vv => more important
            var s5 = LayerMultiplier(file.FilePath);

            return (s1 + s2 + s4) * s3 * s5;
        }

        // calc score for file name contains config, setting words.
        public static double ConfigScore(string fileName)
        {
            var lower = fileName.ToLower();
            if (lower.Contains("config") || lower.Contains("setting")) return 70;

            var known = new HashSet<string>
            { "package.json","cargo.toml","go.mod","pom.xml",
              "makefile","dockerfile","gemfile","composer.json" };
            if (known.Contains(lower)) return 70;
            return Path.GetExtension(lower) switch
            {
                ".env" => 65,
                ".toml" => 55,
                ".yaml" or ".yml" => 50,
                _ => 0
            };
        }

        //calc score for file with filename contains words like layer or popular important file in many language like core, main..vv
        public static double LayerMultiplier(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path).ToLower();
            if (name is "main" or "index" or "app" or "program" or "server") return 1.5;

            return path.ToLower().Split(Path.DirectorySeparatorChar).Select(p => p switch
            {
                "core" or "domain" or "model" or "models" => 1.3,
                "api" or "controller" or "controllers" => 1.2,
                "service" or "services" or "usecase" => 1.2,
                "test" or "tests" or "spec" or "__tests__" => 0.2,
                "vendor" or "third_party" => 0.1,
                _ => 1.0
            }).FirstOrDefault(m => m != 1.0, 1.0);
        }

        public static void RecalcScores(ProjectIndex index)
        {
            var sizes = index.Files.Values.Select(f => (double)f.FileSize).ToList();
            var mean = sizes.Average();
            var stdDev = Math.Sqrt(sizes.Select(s => Math.Pow(s - mean, 2)).Average());

            foreach (var (id, file) in index.Files)
            {
                var inDegree = index.InEdge.TryGetValue(id, out var ins)
                    ? ins.Count : 0;
                index.Files[id] = file with
                {
                    InDegree = inDegree,
                    PriorityScore = CalcFileScore(file, inDegree, mean, stdDev)
                };
            }
        }

    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CliWrap;
using Svg;
using Svg.Pathing;
using CommandLine;
using CommandLine.Text;

namespace Svg2Stl
{

    class Options
    {
        [Value(0, Required = true, HelpText = "SVG file to process.", MetaName = "input file")]
        public string InputFile { get; set; }

        [Value(1, Required = false, HelpText = "STL file to output. Defaults to the input file name with .stl extension.", MetaName = "output file")]
        public string OutputFile { get; set; }

        [Option(Default = 96, HelpText = "The DPI to use when scaling pixel units to millimeters.")]
        public int Dpi { get; set; }

        [Option(Default = 10, HelpText = "How many segments to use when generating points for curved paths. Higher = smoother curves.")]
        public double CurveSteps { get; set; }
    }

    class Program
    {
        const double MM_PER_IN = 25.4;

        static void DisplayHelp<T>(ParserResult<T> result, IEnumerable<Error> errs)
        {
            var helpText = HelpText.AutoBuild(result, h =>
            {
                h.AdditionalNewLineAfterOption = false;
                h.Copyright = "";
                return HelpText.DefaultParsingErrorsHandler(result, h);
            }, e => e);
            Console.WriteLine(helpText);
        }

        static void Main(string[] args)
        {
            var parser = new CommandLine.Parser(with => with.HelpWriter = null);
            var result = parser.ParseArguments<Options>(args);
            result
                .WithParsed(async options => await Run(options))
                .WithNotParsed(errors => DisplayHelp(result, errors));
        }

        private static async Task Run(Options options)
        {
            if (string.IsNullOrEmpty(options.InputFile))
                return;

            if (string.IsNullOrEmpty(options.OutputFile))
                options.OutputFile = options.InputFile.Replace(".svg", "", StringComparison.OrdinalIgnoreCase) + ".stl";

            var svg = SvgDocument.Open(options.InputFile);

            // TODO: support other units - this assumes if the units are not already mm, they must be px
            var scale = (svg.Width.Type == SvgUnitType.Millimeter) ? 1 : MM_PER_IN / options.Dpi;
            var curveStep = 1 / options.CurveSteps;

            var scad = new StringBuilder();

            var lib = typeof(Program).Assembly.GetManifestResourceStream($"{typeof(Program).Namespace}.lib.scad");
            using (var sr = new StreamReader(lib))
            {
                scad.AppendLine(sr.ReadToEnd());
            }

            scad.AppendLine(@$"scale([{scale}, {scale}, 1]) {{ linear_extrude(height = 1.6, center = false) {{ difference() {{");

            foreach (var e in svg.Descendants())
            {
                switch (e)
                {
                    case SvgPath path:
                        scad.AppendLine(ConvertPath(path, curveStep));
                        break;
                    case SvgCircle circle:
                        scad.AppendLine(@$"translate([{circle.CenterX}, {circle.CenterY}, 0]) circle({circle.Radius}, $fn=30);");
                        break;
                    case SvgRectangle rect:
                        scad.AppendLine($@"translate([{rect.X}, {rect.Y}, 0]) square([{rect.Width}, {rect.Height}]);");
                        break;
                }
            }

            scad.AppendLine($"}} }} }}");

            var tmp = Path.GetTempFileName();

            await File.WriteAllTextAsync(tmp, scad.ToString());

            await Cli.Wrap("openscad")
                .WithArguments($"-o {options.OutputFile} {tmp}")
                .ExecuteAsync();
        }

        private static string ConvertPath(SvgPath path, double curveStep)
        {
            var output = new StringBuilder();

            var closed = false;
            var points = new List<string>();
            foreach (var seg in path.PathData)
            {
                switch (seg)
                {
                    case SvgMoveToSegment move:
                        // TODO: review this - the Svg library appears to make all path points absolute
                        continue;
                    case SvgLineSegment line:
                        points.Add($"[[{line.Start.X}, {line.Start.Y}], [{line.End.X}, {line.End.Y}]]");
                        break;
                    case SvgCubicCurveSegment curve:
                        points.Add(@$"bezier_curve_2d({curveStep},
                            [{curve.Start.X}, {curve.Start.Y}],
                            [{curve.FirstControlPoint.X}, {curve.FirstControlPoint.Y}],
                            [{curve.SecondControlPoint.X}, {curve.SecondControlPoint.Y}],
                            [{curve.End.X}, {curve.End.Y}]
                        )");
                        break;
                    case SvgClosePathSegment _:
                        closed = true;
                        break;
                    default:
                        throw new ApplicationException($"{seg.GetType().FullName} at {seg.Start.X}, {seg.Start.Y} is not a supported path segment.");
                }
            }

            var flattenedPoints = $"flatten([{string.Join(",", points)}])";

            if (closed)
                output.AppendLine($"polygon(points={flattenedPoints}, convexity=10);");
            else // lol idk
                output.AppendLine($"polyline({flattenedPoints}, 1);");

            return output.ToString();
        }
    }
}

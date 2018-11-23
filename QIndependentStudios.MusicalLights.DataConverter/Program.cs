﻿using QIndependentStudios.MusicalLights.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace QIndependentStudios.MusicalLights.DataConverter
{
    internal class Program
    {
        private const int MillisecondsPerSecond = 1000;
        private const double FramesPerSecond = 39.467;

        private static readonly List<Color> _colors = new List<Color>
        {
            Color.FromArgb(255, 0, 0),
            Color.FromArgb(255, 78, 0),
            Color.FromArgb(255, 231, 0),
            Color.FromArgb(81, 255, 0),
            Color.FromArgb(0, 255, 140),
            Color.FromArgb(0, 140, 255),
            Color.FromArgb(81, 0, 255),
            Color.FromArgb(255, 0, 231),
            Color.FromArgb(255, 0, 78)
        };

        private static void Main(string[] args)
        {
            Console.WriteLine("Ready for command. Enter \"convert\" or \"gen\"");
            var command = Console.ReadLine().Trim();

            switch (command)
            {
                case "convert":
                case "c":
                    ConvertCsv();
                    break;
                case "gen":
                case "g":
                    Console.WriteLine("What type? Enter \"twinkle\" or \"rainbow\"");
                    var type = Console.ReadLine().Trim();
                    Generate(type, 28);
                    break;
                default:
                    break;
            }

#if DEBUG
            Console.Write($"Press any key to continue . . .");
            Console.ReadKey();
#endif
        }

        private static void ConvertCsv()
        {
            Console.WriteLine("Enter csv data file path:");
            var path = Console.ReadLine().Trim('"');

            Console.WriteLine("Reading csv data...");
            var text = File.ReadAllText(path);

            Console.WriteLine("Parsing data...");
            var data = ParseData(text);

            Console.WriteLine("Constructing sequence...");
            var sequence = ConvertToModel($"{Path.GetFileNameWithoutExtension(path)}.mp3", data);

            Console.WriteLine("Writing sequence json data...");
            var outputPath = Path.ChangeExtension(path, ".json");
            File.WriteAllText($"{outputPath}", sequence.ToJson());

            Console.WriteLine($"Json data written to {outputPath}");
        }

        private static void Generate(string type, int lightCount)
        {
            var sequenceData = new Dictionary<(TimeSpan, int), Color>();
            var outputPath = "";
            switch (type)
            {
                case "twinkle":
                    sequenceData = GenerateTwinkle(lightCount);
                    outputPath = @"C:\Users\qngo\Documents\Twinkle.json";
                    break;
                case "rainbow":
                    sequenceData = GenerateRainbow(lightCount);
                    outputPath = @"C:\Users\qngo\Documents\Rainbow.json";
                    break;
                default:
                    break;
            }

            var sequence = ConvertToModel(null, sequenceData);

            Console.WriteLine("Writing sequence json data...");
            File.WriteAllText(outputPath, sequence.ToJson());

            Console.WriteLine($"Json data written to {outputPath}");
        }

        private static Dictionary<(TimeSpan, int), Color> GenerateTwinkle(int lightCount)
        {
            Console.WriteLine("Generating twinkle sequence...");
            const double minSeparation = 2;
            const double maxSeparation = 4;
            const double minStartDelay = 0;
            const double maxStartDelay = 3;
            var twinkleDuration = TimeSpan.FromSeconds(2);

            var rand = new Random();
            var sequenceData = new Dictionary<(TimeSpan, int), Color>();

            for (var i = 0; i < lightCount; i++)
            {
                sequenceData.Add((new TimeSpan(), i + 1), Color.FromArgb(0));
                sequenceData.Add((TimeSpan.FromSeconds(rand.NextDouble() * (maxStartDelay - minStartDelay) + minStartDelay), i + 1), Color.FromArgb(0));
            }

            for (var i = 0; i < lightCount; i++)
            {
                var frames = sequenceData.Where(x => x.Key.Item2 == i + 1).ToList();
                var moment = frames.Any() ? frames.Last().Key.Item1 : new TimeSpan();

                while (moment < TimeSpan.FromMinutes(1))
                {
                    moment = moment.Add(TimeSpan.FromSeconds(rand.NextDouble() * (maxSeparation - minSeparation) + minSeparation));
                    sequenceData.Add((moment, i + 1), _colors[rand.Next(_colors.Count)]);
                    moment = moment.Add(twinkleDuration);
                    sequenceData.Add((moment, i + 1), Color.FromArgb(0));
                }
            }

            return sequenceData;
        }

        private static Dictionary<(TimeSpan, int), Color> GenerateRainbow(int lightCount)
        {
            Console.WriteLine("Generating rainbow sequence...");
            var transitionDuration = TimeSpan.FromSeconds(0.5);
            var sequenceData = new Dictionary<(TimeSpan, int), Color>();

            var moment = new TimeSpan();
            var offset = 0;
            while (moment < TimeSpan.FromMinutes(1))
            {
                for (var i = 0; i < lightCount; i++)
                {
                    sequenceData.Add((moment, i + 1), _colors[(i + offset) % _colors.Count]);
                }

                moment = moment.Add(transitionDuration);
                offset++;
            }

            return sequenceData;
        }

        private static IDictionary<(double, int), Color> ParseData(string text)
        {
            var sequenceData = new Dictionary<(double, int), Color>();

            foreach (var line in text.Split(Environment.NewLine))
            {
                var values = line.Split(',');
                if (values.Length >= 5
                    && double.TryParse(values[1], out var frame)
                    && int.TryParse(values[0], out var lightId)
                    && int.TryParse(values[3], out var r)
                    && int.TryParse(values[4], out var g)
                    && int.TryParse(values[5], out var b))
                {
                    var color = Color.FromArgb(r, g, b);
                    if (color.ToArgb() == Color.White.ToArgb())
                        color = Color.FromArgb(255, 160, 72);

                    sequenceData[(frame, lightId)] = color;
                }
            }

            var frames = sequenceData.Keys.Select(k => k.Item1).Distinct().OrderBy(x => x);
            var previous = frames.First();
            foreach (var frame in frames.Skip(1))
            {
                var delta = frame - previous;
                if (delta < 0.5)
                    throw new ArgumentException($"{previous} and {frame} are too close together.");
                previous = frame;
            }

            return sequenceData;
        }

        private static Sequence ConvertToModel(string audio, IDictionary<(double, int), Color> data)
        {
            var frames = new List<KeyFrame>();
            foreach (var framePosition in data.Keys.Select(k => k.Item1).Distinct().OrderBy(x => x))
            {
                var lightValues = data.Where(p => p.Key.Item1 == framePosition)
                    .ToDictionary(x => x.Key.Item2, x => x.Value);
                var msPosition = framePosition / FramesPerSecond * MillisecondsPerSecond;
                frames.Add(new KeyFrame(TimeSpan.FromMilliseconds(msPosition), lightValues));
            }
            return new Sequence(frames, audio);
        }

        private static Sequence ConvertToModel(string audio, IDictionary<(TimeSpan, int), Color> data)
        {
            var frames = new List<KeyFrame>();
            foreach (var framePosition in data.Keys.Select(k => k.Item1).Distinct().OrderBy(x => x))
            {
                var lightValues = data.Where(p => p.Key.Item1 == framePosition)
                    .ToDictionary(x => x.Key.Item2, x => x.Value);
                frames.Add(new KeyFrame(framePosition, lightValues));
            }
            return new Sequence(frames, audio);
        }
    }
}

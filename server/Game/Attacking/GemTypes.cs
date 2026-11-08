
     public class GemTypes
    {
        public const int
            Phoenix = 0,
            Dragon = 1,
            Fury = 2,
            Rainbow = 3,
            Kylin = 4,
            Violet = 5,
            Moon = 6,
            Tortoise = 7,
            Thunder = 10,
            Glory = 12,

            First = Phoenix,
            Last = Glory + 1;

        public static readonly string[] Animation = new string[]
            {
                "phoenix",
                "goldendragon",
                "fastflash",
                "rainbow",
                "goldenkylin",
                "purpleray",
                "moon",
                "recovery"
            };

        public static readonly int[][] Effects = new[]
            {
                new[] { 0, 5, 10, 15 },
                new[] { 0, 5, 10, 15 },
                new[] { 0, 5, 10, 15 },
                new[] { 0, 10, 15, 25 },
                new[] { 0, 50, 100, 200 },
                new[] { 0, 30, 50, 100 },
                new[] { 0, 15, 30, 50 },
                new[] { 0, 2, 4, 6 },
                new[] { 0, 0, 0, 0 },
                new[] { 0, 0, 0, 0 },
                new[] { 0, 100, 300, 500 },
                new[] { 0, 0, 0, 0 },
                new[] { 0, 100, 300, 500 }
            };
    }
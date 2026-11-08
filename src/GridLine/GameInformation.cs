using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace GridLine
{
    [DataContract]
    internal sealed class GameInformation
    {
        [DataMember] public string Summary;
        [DataMember] public string Developer;
        [DataMember] public string Publisher;
        [DataMember] public string Release;
        [DataMember] public string OfficialSite;
        [DataMember] public string Trailer;
        [DataMember] public string RigGuide;
        [DataMember] public string Wheel;
        [DataMember] public string VR;
        [DataMember] public string Telemetry;

        private static readonly Dictionary<string, GameInformation> Catalog =
            new Dictionary<string, GameInformation>(StringComparer.Ordinal)
        {
            { "805550", Info("A GT racing sim centered on endurance and sprint competition, with changing track conditions and race strategy.", "Kunos Simulazioni", "505 Games", "2019", "https://assettocorsa.gg/assetto-corsa-competizione/", trailer: "https://www.youtube.com/watch?v=2__vPcihU10", vr: "Supported on PC", telemetry: "SimHub supported", guide: "https://support.505games.com/support/solutions/articles/150000147331-does-assetto-corsa-competizione-have-vr-support-") },
            { "244210", Info("A circuit driving sim with road and race cars, free practice, race weekends, and a large PC modding community.", "Kunos Simulazioni", "Kunos Simulazioni", "2014", "https://assettocorsa.gg/assetto-corsa/") },
            { "1849250", Info("Drive against the clock on rally stages with changing surfaces and weather. The co-driver calls the route ahead.", "Codemasters", "Electronic Arts", "2023", "https://www.ea.com/games/ea-sports-wrc/wrc", "See EA's wheel support list", "https://www.youtube.com/watch?v=PhFuQ7sDboQ", vr: "PC VR beta", guide: "https://forums.ea.com/discussions/wrc-general-discussion-en/ea-sports%E2%84%A2-wrc-controllers-and-peripherals-support/8342926") },
            { "1066890", Info("Run sprint and endurance races across a range of contemporary and historic cars and circuits.", "Reiza Studios", "Reiza Studios", "2020", "https://www.game-automobilista2.com/") },
            { "284160", Info("A vehicle sandbox with soft-body physics, free roam, driving scenarios, and vehicle customization.", "BeamNG", "BeamNG", "2015 on Steam", "https://www.beamng.com/game/", telemetry: "SimHub supported", guide: "https://www.simhubdash.com/supported-games/") },
            { "211500", Info("A circuit racing sim with touring cars, GT machinery, ranked racing, and practice sessions.", "KW Studios", "KW Studios", "2013", "https://www.raceroom.com/") },
            { "266410", Info("A subscription racing service with scheduled online events, license progression, and multiple motorsport disciplines.", "iRacing", "iRacing", null, "https://www.iracing.com/") },
            { "3058630", Info("Kunos' next-generation driving sim brings road and race cars together with detailed circuits.", "Kunos Simulazioni", "505 Games", "2025 on Steam", "https://assettocorsa.gg/assetto-corsa-evo/") },
            { "2399420", Info("Run sports car endurance races inspired by the FIA World Endurance Championship and the 24 Hours of Le Mans.", "Studio 397", "Motorsport Games", "2024 on Steam", "https://lemansultimate.com/") },
            { "365960", Info("A PC circuit sim with race rules, weather, and evolving track grip during sessions.", "Studio 397", "Studio 397", null, "https://www.studio-397.com/") },
            { "690790", Info("Take on timed rally stages and head-to-head rallycross on loose surfaces.", "Codemasters", "Codemasters", "2019", null) },
            { "1551360", Info("Explore an open-world version of Mexico, collect cars, and enter road, dirt, or cross-country events.", "Playground Games", "Xbox Game Studios", "2021", "https://forza.net/horizon") },
            { "270880", Info("Haul freight across the United States, manage jobs and trucks, and grow a transport business.", "SCS Software", "SCS Software", "2016", "https://americantrucksimulator.com/", trailer: "https://www.youtube.com/watch?v=Yu32OisFgWI") },
            { "227300", Info("Drive freight routes across Europe, expand a truck fleet, and choose your own pace and destinations.", "SCS Software", "SCS Software", "2012", "https://eurotrucksimulator2.com/", telemetry: "SimHub supported", guide: "https://www.simhubdash.com/supported-games/") },
            { "491540", Info("Drive scheduled public transit routes through a recreation of Berlin.", "TML-Studios", "Aerosoft", "2021 on Steam", null) },
            { "252530", Info("Drive city bus routes in a recreation of Berlin with timetable and passenger operations.", "MR-Software", "Aerosoft", "2013", null) },
            { "976590", Info("Operate city bus routes, plan a transit network, and manage a fleet of buses.", "stillalive studios", "astragon Entertainment", "2021", null) },
            { "427100", Info("Drive intercity coaches on scheduled routes through Germany and neighboring regions.", "TML-Studios", "Aerosoft", "2016", null) },
            { "953580", Info("Run passenger and tourism routes on Fuerteventura while managing a coach fleet.", "TML-Studios", "Aerosoft", "2018", null) }
        };

        private static GameInformation Info(string summary, string developer, string publisher, string release,
            string site, string wheel = null, string trailer = null, string vr = null,
            string telemetry = null, string guide = null)
        {
            return new GameInformation
            {
                Summary = summary, Developer = developer, Publisher = publisher,
                Release = release, OfficialSite = site, Wheel = wheel, Trailer = trailer,
                VR = vr, Telemetry = telemetry, RigGuide = guide
            };
        }

        public static GameInformation For(Game game, string directory, GameInformation enrichment = null)
        {
            GameInformation defaults;
            if (!Catalog.TryGetValue(game.Id, out defaults)) defaults = new GameInformation();
            if (game.Id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || game.Id == "." || game.Id == "..") return defaults;
            var local = JsonDisk.Load<GameInformation>(Path.Combine(directory, "artwork", game.Id, "game.json"));
            if (local == null) local = new GameInformation();
            if (enrichment == null) enrichment = new GameInformation();
            return new GameInformation
            {
                Summary = local.Summary ?? enrichment.Summary ?? defaults.Summary,
                Developer = local.Developer ?? enrichment.Developer ?? defaults.Developer,
                Publisher = local.Publisher ?? enrichment.Publisher ?? defaults.Publisher,
                Release = local.Release ?? defaults.Release ?? enrichment.Release,
                OfficialSite = local.OfficialSite ?? defaults.OfficialSite, Trailer = local.Trailer ?? defaults.Trailer,
                RigGuide = local.RigGuide ?? defaults.RigGuide,
                Wheel = local.Wheel ?? defaults.Wheel, VR = local.VR ?? defaults.VR, Telemetry = local.Telemetry ?? defaults.Telemetry
            };
        }
    }
}

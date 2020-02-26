using LoableTech;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TimeZoneConverter;

namespace GeonamesToJSON
{
    class Program
    {
        static void Main(string[] args)
        {
            Dictionary<string, List<string>> admin1codes;
            Dictionary<string, List<string>> admin2codes;
            Dictionary<string, List<string>> countries;
            Dictionary<string, List<string>> features;
            List<string> filters;

            FlagParser.ContinueOnError = true;
            FlagParser.Parse(args);
            bool help = FlagParser.BoolFlag("help", false, false);
            if (help || args.Length == 0)
            {
                DisplayUsage();
                return;
            }
            // process input flags
            string admin1File = FlagParser.StringFlag("admin1", string.Empty, true);
            string admin2File = FlagParser.StringFlag("admin2", string.Empty, true);
            string countryFile = FlagParser.StringFlag("countries", string.Empty, true);
            string featureFile = FlagParser.StringFlag("feature", string.Empty, true);
            string input = FlagParser.StringFlag("input", string.Empty, true);
            string output = FlagParser.StringFlag("output", string.Empty, true);
            string filter = FlagParser.StringFlag("filter", string.Empty, false);
            string outputType = FlagParser.StringFlag("type", "default", false);

            if (FlagParser.Errors.Count > 0)
            {
                foreach (var e in FlagParser.Errors)
                {
                    Console.WriteLine(e);
                }
                return;
            }

            // read admin1 codes
            admin1codes = ReadAdmin1File(admin1File);
            if (admin1codes == null)
                return;

            // read admin2 codes
            admin2codes = ReadAdmin2File(admin2File);
            if (admin2codes == null)
                return;

            // read country codes
            countries = ReadCountryFile(countryFile);
            if (countries == null)
                return;

            // read feature codes
            features = ReadFeatureFile(featureFile);
            if (features == null)
                return;

            // put filters as list
            filters = new List<string>();
            var fltrs = filter.Split(new char[] { ',' });
            foreach (var fltr in fltrs)
                filters.Add(fltr);

            switch (outputType)
            {
                case "full-structured":
                    GenerateFullStructuredOutput(input, output, features, filters, countries, admin1codes, admin2codes);
                    break;
                case "full-flat":
                default:
                    GenerateDefaultOutput(input, output, features, filters, countries, admin1codes, admin2codes);
                    break;
            }
            
        }

        private static void GenerateFullStructuredOutput(
            string input,
            string output,
            Dictionary<string, List<string>> features,
            List<string> filters,
            Dictionary<string, List<string>> countries,
            Dictionary<string, List<string>> admin1codes,
            Dictionary<string, List<string>> admin2codes)
        {
            Console.WriteLine("Start GenerateFullStructuredOutput");
            // check input file if it exists
            if (!File.Exists(input))
            {
                Console.WriteLine("Input file not found");
                return;
            }

            // check output file if it exists
            if (File.Exists(output))
            {
                File.Delete(output);
            }

            // read input file
            try
            {
                using StreamReader sr = new StreamReader(input);
                using StreamWriter sw = new StreamWriter(output);
                var semaphore = new Semaphore(1000, 1000);
                object filelock = new object();

                string inputLine = string.Empty;
                var options = new JsonSerializerOptions()
                {
                    IgnoreNullValues = true
                };
                var tracker = new Dictionary<string, geonameStructured>();
                var tasks = new List<Task>();
                while ((inputLine = sr.ReadLine()) != null)
                {
                    semaphore.WaitOne();                    
                    var s = inputLine.Split(new char[] { '\t' });
                    var handle = new EventWaitHandle(false, EventResetMode.ManualReset);
                    var task = Task.Run(action: () =>
                    {
                        Console.WriteLine("Processing GeonameID: " + s[0]);
                        var geo = new geonameFlat();

                        // check if feature is included in filters
                        geo.feature = $"{s[6]}.{s[7]}";
                        if (filters.FirstOrDefault(r => r.Equals(geo.feature, StringComparison.OrdinalIgnoreCase)) == null)
                        {
                            return;
                        }

                        geo.countryCode = s[8];
                        var admin1code = $"{geo.countryCode}.{s[10]}";
                        var admin2code = $"{admin1code}.{s[11]}";

                        geonameStructured geostruct = null;
                        var key = $"{geo.countryCode}-{admin1code}-{admin2code}";
                        lock (filelock)
                        {
                            if (!tracker.TryGetValue(key, out geostruct))
                            {
                                geostruct = new geonameStructured()
                                {
                                    countryCode = geo.countryCode,
                                    admin1Code = admin1code,
                                    admin2Code = admin2code,
                                    tempFilename = Path.GetTempFileName(),
                                    isNewTempFile = true
                                };
                                if (countries.TryGetValue(geo.countryCode, out List<string> ctry))
                                {
                                    geostruct.countryCodeISO3 = ctry[0];
                                    geostruct.country = ctry[3];
                                    geostruct.continent = ctry[7];
                                    geostruct.tld = ctry[8];
                                    geostruct.currencyCode = ctry[9];
                                    geostruct.currency = ctry[10];
                                    geostruct.currency = ctry[10];
                                    var langs = ctry[14].Split(new char[] { ',' });
                                    geostruct.languages = string.IsNullOrWhiteSpace(langs[0]) ? null : langs;
                                    var neighbors = ctry[16].Split(new char[] { ',' });
                                    geostruct.neighborCountries = string.IsNullOrWhiteSpace(neighbors[0]) ? null : neighbors;
                                }
                                if (admin1codes.TryGetValue(admin1code, out List<string> a1))
                                {
                                    geostruct.admin1 = a1[0] ?? "None"; 
                                }

                                if (admin2codes.TryGetValue(admin2code, out List<string> a2))
                                {
                                    geostruct.admin2 = a2[0] ?? "None";
                                }
                                tracker.Add(geostruct.key, geostruct);
                                File.WriteAllText(geostruct.tempFilename, "[");
                            }
                        }                        

                        if (long.TryParse(s[0], out long geoid))
                        {
                            geo.geonameId = geoid;
                        }
                        geo.name = s[1].Trim();
                        geo.asciiName = s[2].Trim();
                        var altNames = s[3].Split(new char[] { ',' });
                        geo.alternateNames = string.IsNullOrWhiteSpace(altNames[0]) ? null : altNames;
                        if (decimal.TryParse(s[4], out decimal lat))
                        {
                            geo.latitude = lat;
                        }
                        if (decimal.TryParse(s[5], out decimal lng))
                        {
                            geo.longitude = lng;
                        }

                        //if (countries.TryGetValue(geo.countryCode, out List<string> ctry))
                        //{
                        //    geo.countryCodeISO3 = ctry[0];
                        //    geo.country = ctry[3];
                        //    geo.continent = ctry[7];
                        //    geo.tld = ctry[8];
                        //    geo.currencyCode = ctry[9];
                        //    geo.currency = ctry[10];
                        //    var langs = ctry[14].Split(new char[] { ',' });
                        //    geo.languages = string.IsNullOrWhiteSpace(langs[0]) ? null : langs;
                        //    var neighbors = ctry[16].Split(new char[] { ',' });
                        //    geo.neighborCountries = string.IsNullOrWhiteSpace(neighbors[0]) ? null : neighbors;
                        //}

                        //if (admin1codes.TryGetValue(admin1code, out List<string> a1))
                        //{
                        //    geo.admin1 = a1[0].Trim();
                        //}

                        //if (admin2codes.TryGetValue(admin2code, out List<string> a2))
                        //{
                        //    geo.admin2 = a2[0].Trim();
                        //}

                        geo.timezone = s[17];
                        if (!string.IsNullOrWhiteSpace(geo.timezone))
                        {
                            geo.windowsTimezone = TZConvert.IanaToWindows(geo.timezone);
                        }
                        geo.dateModified = DateTime.Parse(s[18]);

                        lock (geostruct.filelock)
                        {
                            if (!geostruct.isNewTempFile)
                            {
                                File.AppendAllText(geostruct.tempFilename, ",");
                            }
                            geostruct.isNewTempFile = false;
                            File.AppendAllText(geostruct.tempFilename, JsonSerializer.Serialize(geo, options));
                        }
                    });
                    tasks.Add(task);
                    semaphore.Release();
                    Console.WriteLine("--- Done GeonameID: " + s[0]);
                }
                Console.WriteLine("Waiting Tasks");
                Task.WaitAll(tasks.ToArray());
                // close array
                Console.WriteLine("Closing Array in Files");
                foreach (var t in tracker)
                {
                    File.AppendAllText(t.Value.tempFilename, "]");
                }
                Console.WriteLine("Flatten Tracker");
                // flatten tracker
                List<geonameStructured> flattracker = new List<geonameStructured>();
                foreach(var f in tracker)
                {
                    flattracker.Add(f.Value);
                }
                // Write output
                sw.Write("[{");
                var ctrys = flattracker.GroupBy(r => r.countryCode).Select(r => r.First());
                bool isFirstCountry = true;
                foreach(var ctry in ctrys)
                {
                    if (!isFirstCountry) sw.Write(",");
                    isFirstCountry = false;
                    sw.Write($"\"{ctry.country ?? "None"}\":{{");
                    sw.Write($"\"CountryCode\":\"{ctry.countryCode}\",");
                    sw.Write($"\"CountryISO\":\"{ctry.countryCodeISO3}\",");
                    sw.Write($"\"Continent\":\"{ctry.continent}\",");
                    sw.Write($"\"Currency\":\"{ctry.currency}\",");
                    sw.Write($"\"CurrencyCode\":\"{ctry.currencyCode}\",");
                    sw.Write($"\"Languages\":{JsonSerializer.Serialize(ctry.languages, options)},");
                    sw.Write($"\"Neighbors\":{JsonSerializer.Serialize(ctry.neighborCountries, options)},");
                    sw.Write($"\"TLD\":\"{ctry.tld}\",");
                    sw.Write("\"Regions\":[{");
                    var admin1s = flattracker.Where(r => r.countryCode.Equals(ctry.countryCode, StringComparison.OrdinalIgnoreCase)).GroupBy(r => r.admin1Code).Select(r => r.First());
                    bool isFirstAdmin1 = true;
                    foreach(var a1 in admin1s)
                    {
                        if (!isFirstAdmin1) sw.Write(",");
                        isFirstAdmin1 = false;
                        sw.Write($"\"{a1.admin1 ?? "None"}\":{{");
                        var admin2s = flattracker.Where(r => r.admin1Code.Equals(a1.admin1Code, StringComparison.OrdinalIgnoreCase)).GroupBy(r => r.admin2Code).Select(r => r.First());
                        bool isFirstAdmin2 = true;
                        foreach(var a2 in admin2s)
                        {
                            if (!isFirstAdmin2) sw.Write(",");
                            isFirstAdmin2 = false;
                            sw.Write($"\"{a2.admin2 ?? "None"}\":");
                            var files = flattracker.Where(r => r.admin2Code.Equals(a2.admin2Code, StringComparison.OrdinalIgnoreCase));
                            bool isFirstFile = true;
                            foreach(var f in files)
                            {
                                var content = File.ReadAllText(f.tempFilename);
                                if (!isFirstFile) sw.Write(",");
                                isFirstFile = false;
                                sw.Write(content);
                            }
                        }
                        sw.Write("}");
                    }
                    sw.Write("}]}");
                }
                sw.Write("}]");
                sw.Flush();
                Console.WriteLine("Output Done");
                // delete tracker temp files
                foreach (var t in tracker)
                {
                    if (File.Exists(t.Value.tempFilename))
                    {
                        File.Delete(t.Value.tempFilename);
                    }
                }
                Console.WriteLine("Delete Temp Files");
            }
            catch (IOException e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static void GenerateDefaultOutput(
        string input,
        string output,
        Dictionary<string, List<string>> features,
        List<string> filters,
        Dictionary<string, List<string>> countries,
        Dictionary<string, List<string>> admin1codes,
        Dictionary<string, List<string>> admin2codes)
        {
            // check input file if it exists
            if (!File.Exists(input))
            {
                Console.WriteLine("Input file not found");
                return;
            }

            // check output file if it exists
            if (File.Exists(output))
            {
                File.Delete(output);
            }

            // read input file
            try
            {
                using StreamReader sr = new StreamReader(input);
                using StreamWriter sw = new StreamWriter(output);

                sw.Write("[");
                string inputLine = string.Empty;
                bool isfirst = true;
                var options = new JsonSerializerOptions()
                {
                    IgnoreNullValues = true
                };
                while ((inputLine = sr.ReadLine()) != null)
                {
                    var s = inputLine.Split(new char[] { '\t' });
                    // check if feature is included in filters
                    var featurecode = $"{s[6]}.{s[7]}";
                    if (filters.FirstOrDefault(r => r.Equals(featurecode, StringComparison.OrdinalIgnoreCase)) == null)
                    {
                        continue;
                    }
                    var geo = new geonameFlat();
                    if (long.TryParse(s[0], out long geoid))
                    {
                        geo.geonameId = geoid;
                    }
                    geo.name = s[1].Trim();
                    geo.asciiName = s[2].Trim();
                    var altNames = s[3].Split(new char[] { ',' });
                    geo.alternateNames = string.IsNullOrWhiteSpace(altNames[0]) ? null : altNames;
                    if (decimal.TryParse(s[4], out decimal lat))
                    {
                        geo.latitude = lat;
                    }
                    if (decimal.TryParse(s[5], out decimal lng))
                    {
                        geo.longitude = lng;
                    }
                    if (features.TryGetValue(featurecode, out List<string> feats))
                    {
                        geo.feature = feats[0];
                    }
                    geo.countryCode = s[8];
                    if (countries.TryGetValue(geo.countryCode, out List<string> ctry))
                    {
                        geo.countryCodeISO3 = ctry[0];
                        geo.country = ctry[3];
                        geo.continent = ctry[7];
                        geo.tld = ctry[8];
                        geo.currencyCode = ctry[9];
                        geo.currency = ctry[10];
                        var langs = ctry[14].Split(new char[] { ',' });
                        geo.languages = string.IsNullOrWhiteSpace(langs[0]) ? null : langs;
                        var neighbors = ctry[16].Split(new char[] { ',' });
                        geo.neighborCountries = string.IsNullOrWhiteSpace(neighbors[0]) ? null : neighbors;
                    }
                    var admin1code = $"{geo.countryCode}.{s[10]}";
                    if (admin1codes.TryGetValue(admin1code, out List<string> a1))
                    {
                        geo.admin1 = a1[0].Trim();
                    }
                    var admin2code = $"{admin1code}.{s[11]}";
                    if (admin2codes.TryGetValue(admin2code, out List<string> a2))
                    {
                        geo.admin2 = a2[0].Trim();
                    }
                    geo.timezone = s[17];
                    if (!string.IsNullOrWhiteSpace(geo.timezone))
                    {
                        geo.windowsTimezone = TZConvert.IanaToWindows(geo.timezone);
                    }
                    geo.dateModified = DateTime.Parse(s[18]);

                    var towrite = JsonSerializer.Serialize(geo, options);
                    if (isfirst)
                    {
                        isfirst = false;
                    }
                    else
                    {
                        sw.Write(',');
                    }
                    sw.Write(towrite);
                }
                sw.Write("]");
                sw.Flush();
            }
            catch (IOException e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static Dictionary<string, List<string>> ReadFeatureFile(string filename)
        {
            var retval = new Dictionary<string, List<string>>();
            if (string.IsNullOrEmpty(filename))
            {
                Console.WriteLine("Invalid feature file");
                return null;
            }
            else if (!File.Exists(filename))
            {
                Console.WriteLine($"File [{filename}] not found");
                return null;
            }
            try
            {
                using StreamReader sr = new StreamReader(filename);
                string line = string.Empty;
                while ((line = sr.ReadLine()) != null)
                {
                    var s = line.Split(new char[] { '\t' });
                    if (s[0].Trim().Contains("#"))
                        continue;
                    retval.Add(s[0].Trim(), s.Skip(1).ToList());
                }
            }
            catch (IOException e)
            {
                Console.WriteLine(e.Message);
            }
            return retval;
        }

        private static Dictionary<string, List<string>> ReadCountryFile(string filename)
        {
            var retval = new Dictionary<string, List<string>>();
            if (string.IsNullOrEmpty(filename))
            {
                Console.WriteLine("Invalid country file");
                return null;
            }
            else if (!File.Exists(filename))
            {
                Console.WriteLine($"File [{filename}] not found");
                return null;
            }
            try
            {
                using StreamReader sr = new StreamReader(filename);
                string line = string.Empty;
                while ((line = sr.ReadLine()) != null)
                {
                    var s = line.Split(new char[] { '\t' });
                    if (s[0].Trim().Contains("#"))
                        continue;
                    retval.Add(s[0].Trim(), s.Skip(1).ToList());
                }
            }
            catch (IOException e)
            {
                Console.WriteLine(e.Message);
            }
            return retval;
        }

        private static Dictionary<string, List<string>> ReadAdmin2File(string filename)
        {
            var retval = new Dictionary<string, List<string>>();
            if (string.IsNullOrEmpty(filename))
            {
                Console.WriteLine("Invalid admin2 file");
                return null;
            }
            else if (!File.Exists(filename))
            {
                Console.WriteLine($"File [{filename}] not found");
                return null;
            }
            try
            {
                using StreamReader sr = new StreamReader(filename);
                string line = string.Empty;
                while ((line = sr.ReadLine()) != null)
                {
                    var s = line.Split(new char[] { '\t' });
                    if (s[0].Trim().Contains("#"))
                        continue;
                    retval.Add(s[0].Trim(), s.Skip(1).ToList());
                }
            }
            catch (IOException e)
            {
                Console.WriteLine(e.Message);
            }
            return retval;
        }

        private static Dictionary<string, List<string>> ReadAdmin1File(string filename)
        {
            var retval = new Dictionary<string, List<string>>();
            if (string.IsNullOrEmpty(filename))
            {
                Console.WriteLine("Invalid admin1 file");
                return null;
            }
            else if (!File.Exists(filename))
            {
                Console.WriteLine($"File [{filename}] not found");
                return null;
            }
            try
            {
                using StreamReader sr = new StreamReader(filename);
                string line = string.Empty;
                while ((line = sr.ReadLine()) != null)
                {
                    var s = line.Split(new char[] { '\t' });
                    if (s[0].Trim().Contains("#"))
                        continue;
                    retval.Add(s[0].Trim(), s.Skip(1).ToList());
                }
            }
            catch (IOException e)
            {
                Console.WriteLine(e.Message);
            }
            return retval;
        }

        private static void DisplayUsage()
        {
            const string format = "{0, -30} \t- {1, -10}";
            Console.WriteLine("GeonamesToJSON Usage");
            Console.WriteLine(format, "Flag", "Description");
            Console.WriteLine(format, "-help", "Show this usage screen");
            Console.WriteLine(format, "-admin1=<filename>", "Set the Admin 1 Codes text file name [required]");
            Console.WriteLine(format, "-admin2=<filename>", "Set the Admin 2 Codes text file name [required]");
            Console.WriteLine(format, "-countries=<filename>", "Set the country info text file name [required]");
            Console.WriteLine(format, "-feature=<filename>", "Set the feature code text file name [required]");
            Console.WriteLine(format, "-timezones=<filename>", "Set the time zone info text file name [required]");
            Console.WriteLine(format, "-input=<filename>", "Set the input filename [required]");
            Console.WriteLine(format, "-output=<filename>", "Set the output filename [required]");
            Console.WriteLine(format, "-filter=<featurecode1,...>", "List of feature codes to include. All feature codes are used by default");
            Console.WriteLine(format, "", "sample feature code: P.PPLA, R.TRL");
            Console.WriteLine(format, "-type=<type>", "default: full-flat");
            Console.WriteLine(format, "", "options: full-flat, full-structure, basic-flat, basic-structured");
        }
    }
}

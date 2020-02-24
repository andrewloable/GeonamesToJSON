using LoableTech;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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

                string inputLine = string.Empty;
                var options = new JsonSerializerOptions()
                {
                    IgnoreNullValues = true
                };
                List<geonameFlat> list = new List<geonameFlat>();
                while ((inputLine = sr.ReadLine()) != null)
                {
                    var geo = new geonameFlat();
                    var s = inputLine.Split(new char[] { '\t' });
                    // check if feature is included in filters
                    geo.feature = $"{s[6]}.{s[7]}";
                    if (filters.FirstOrDefault(r => r.Equals(geo.feature, StringComparison.OrdinalIgnoreCase)) == null)
                    {
                        continue;
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

                    list.Add(geo);
                }
                foreach(var o in admin1codes)
                {
                    var s = o.Key.Split(new char[] { '.' });
                    o.Value.Add(s[0]);
                }
                foreach(var o in admin2codes)
                {
                    var s = o.Key.Split(new char[] { '.' });
                    o.Value.Add(string.Join(".", s.Take(2).ToArray()));
                }
                var t = admin2codes;
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

﻿using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using SmarterApp;

namespace UnitTest
{
    class Program
    {
        const string c_legacyIdFilename = "AllLegacyIdsInUse.txt";
        const string c_enhancedIdFilename = "AllEnhancedElaIds.txt";

        static string s_workingDirectory;

        static void Main(string[] args)
        {
            try
            {
                // Locate the working directory in the parent path of the directory containing the executable
                s_workingDirectory = Path.GetFullPath(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
                while (!File.Exists(Path.Combine(s_workingDirectory, c_legacyIdFilename)))
                {
                    s_workingDirectory = Path.GetDirectoryName(s_workingDirectory);
                    if (s_workingDirectory == null)
                    {
                        throw new ApplicationException($"Unable to find working directory containing '{c_legacyIdFilename}' in the parent path of the executable '{Assembly.GetEntryAssembly().Location}'.");
                    }
                }

                Console.WriteLine($"Working directory: {s_workingDirectory}");

                TestLegacyFormatIds(Path.Combine(s_workingDirectory, c_legacyIdFilename));
                TestEnhancedFormatIds(Path.Combine(s_workingDirectory, c_enhancedIdFilename));
                TestEnhancedFormatIds(Path.Combine(s_workingDirectory, "AllEnhancedMathIds.txt"));
            }
            catch (Exception err)
            {
                Console.WriteLine(err.ToString());
            }
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
            }
#endif
        }

        static bool TestLegacyFormatIds(string path)
        {
            Console.WriteLine($"Testing legacy IDs in '{path}'.");

            int idCount = 0;
            int errorCount = 0;
            using (var idFile = new StreamReader(path))
            {
                for (; ; )
                {
                    // Read grade and ID
                    string line = idFile.ReadLine();
                    if (line == null) break;

                    // Separate the grade and ID fields
                    int space = line.IndexOf(' ');
                    if (space < 0) continue;
                    string strGrade = line.Substring(0, space);
                    string strId = line.Substring(space + 1);

                    ++idCount;

                    if (!TestLegacyFormatId(strGrade, strId))
                        ++errorCount;
                }
            }

            // Report results
            Console.WriteLine($"{idCount,4} Ids Tested");
            Console.WriteLine($"{errorCount,4} Errors Reported");
            Console.WriteLine();

            return errorCount == 0;
        }

        static bool TestLegacyFormatId(string strGrade, string strId)
        {
            try
            {
                // Parse the grade and ID and check for errors
                var grade = ContentSpecId.ParseGrade(strGrade);
                var id = ContentSpecId.TryParse(strId, grade);
                if (id.ParseErrorSeverity != ErrorSeverity.NoError)
                {
                    // Report a parse error
                    WriteLine($"{strGrade} {strId}");
                    WriteLine(id.ParseErrorSeverity == ErrorSeverity.Corrected ? ConsoleColor.Green : ConsoleColor.Red,
                        $"   {id.ParseErrorDescription}");
                    return false;
                }

                // Check for format-ability
                if (id.ValidateFor(id.ParseFormat) != ErrorSeverity.NoError)
                {
                    // Report a validation error
                    WriteLine(ConsoleColor.Red, $"   {id.ValidationErrorDescription}");
                    return false;
                }

                // Check for round-trip match
                string roundTrip = id.ToString();
                string compId = RemedyMissingGrade(strId, grade);
                if (!string.Equals(compId, roundTrip, StringComparison.OrdinalIgnoreCase))
                {
                    WriteLine($"{strGrade} {strId}");
                    WriteLine(ConsoleColor.Red, $"   ID doesn't match: {roundTrip}");
                    return false;
                }

                // See if can be reformatted to enhanced
                if (id.ValidateFor(ContentSpecIdFormat.Enhanced) != ErrorSeverity.NoError)
                {
                    WriteLine($"{strGrade} {strId}");
                    WriteLine(ConsoleColor.Red, $"   Cannot convert to enhanced format: {id.ValidationErrorDescription}");
                    return false;
                }

                // Round-trip through Enhanced ID format
                var enhancedIdStr = id.ToString(ContentSpecIdFormat.Enhanced);
                var enhancedId = ContentSpecId.TryParse(enhancedIdStr);
                if (!enhancedId.ParseSucceeded)
                {
                    WriteLine($"{strGrade} {strId}");
                    WriteLine(ConsoleColor.Red, $"   {enhancedIdStr}");
                    WriteLine(ConsoleColor.Red, $"   Failed to parse enhanced format: {enhancedId.ParseErrorDescription}");
                    return false;
                }

                if (!id.Equals(enhancedId))
                {
                    WriteLine($"{strGrade} {strId}");
                    WriteLine(ConsoleColor.Red, $"   {enhancedIdStr}");
                    WriteLine(ConsoleColor.Red, $"   Enhanced ID conversion is not equal.");
                    return false;
                }

                if (enhancedId.ValidateFor(id.ParseFormat) != ErrorSeverity.NoError)
                {
                    WriteLine($"{strGrade} {strId}");
                    WriteLine(ConsoleColor.Red, $"   {enhancedIdStr}");
                    WriteLine(ConsoleColor.Red, $"   Cannot format enhanced to original format: ${enhancedId.ValidationErrorDescription}");
                    return false;
                }

                roundTrip = enhancedId.ToString(id.ParseFormat);
                if (!string.Equals(compId, roundTrip, StringComparison.OrdinalIgnoreCase))
                {
                    WriteLine($"{strGrade} {strId}");
                    WriteLine(ConsoleColor.Red, $"   {enhancedIdStr}");
                    WriteLine(ConsoleColor.Red, $"   ID doesn't match when round-tripped through enahcned ID: {roundTrip}");
                    return false;
                }
            }
            catch (Exception)
            {
                WriteLine(ConsoleColor.Red, $"{strGrade} {strId}");
                throw;
            }

            return true;
        }

        static bool TestEnhancedFormatIds(string path)
        {
            Console.WriteLine($"Testing enhanced IDs in '{path}'.");

            int idCount = 0;
            int errorCount = 0;
            using (var idFile = new StreamReader(path))
            {
                for (; ; )
                {
                    // Read ID
                    string line = idFile.ReadLine();
                    if (line == null) break;

                    ++idCount;

                    if (!TestEnhancedFormatId(line))
                        ++errorCount;
                }
            }

            // Report results
            Console.WriteLine($"{idCount,4} Ids Tested");
            Console.WriteLine($"{errorCount,4} Errors Reported");
            Console.WriteLine();

            return errorCount == 0;
        }

        static bool TestEnhancedFormatId(string idStr)
        {
            try
            {
                // Parse the ID and check for errors
                var id = ContentSpecId.TryParse(idStr);
                if (id.ParseErrorSeverity != ErrorSeverity.NoError)
                {
                    // Report a parse error
                    WriteLine(idStr);
                    WriteLine(id.ParseErrorSeverity == ErrorSeverity.Corrected ? ConsoleColor.Green : ConsoleColor.Red,
                        $"   {id.ParseErrorDescription}");
                    return false;
                }

                if (id.ValidateFor(id.ParseFormat) != ErrorSeverity.NoError)
                {
                    // Report a validation error
                    WriteLine(idStr);
                    WriteLine(ConsoleColor.Red, $"   {id.ValidationErrorDescription}");
                    return false;
                }

                // Check for round-trip match
                string roundTrip = id.ToString();
                if (!string.Equals(idStr, roundTrip, StringComparison.OrdinalIgnoreCase))
                {
                    WriteLine(idStr);
                    WriteLine(ConsoleColor.Red, $"   ID doesn't match: {roundTrip}");
                    return false;
                }

                // If claim or target is not specfied then it cannot be converted to
                // legacy format. Do not perform the test.
                if (id.Claim == ContentSpecClaim.Unspecified || string.IsNullOrEmpty(id.Target))
                    return true;

                // Pick legacy format according to subject
                var legacyFormat = id.Subject == ContentSpecSubject.Math
                    ? ContentSpecIdFormat.MathV4
                    : ContentSpecIdFormat.ElaV1;

                // See if can be reformatted to enhanced
                if (id.ValidateFor(legacyFormat) != ErrorSeverity.NoError)
                {
                    WriteLine(idStr);
                    WriteLine(ConsoleColor.Red, $"   Cannot convert to legacy format: {id.ValidationErrorDescription}");
                    return false;
                }

                // Round-trip through Legacy format
                var legacyIdStr = id.ToString(legacyFormat);
                var legacyId = ContentSpecId.TryParse(legacyIdStr);
                if (!legacyId.ParseSucceeded)
                {
                    WriteLine(idStr);
                    WriteLine(ConsoleColor.Red, $"   {legacyIdStr}");
                    WriteLine(ConsoleColor.Red, $"   Failed to parse enhanced format: {legacyId.ParseErrorDescription}");
                    return false;
                }

                if (!id.Equals(legacyId))
                {
                    WriteLine(idStr);
                    WriteLine(ConsoleColor.Red, $"   {legacyIdStr}");
                    WriteLine(ConsoleColor.Red, $"   Enhanced ID conversion is not equal.");
                    return false;
                }

                if (legacyId.ValidateFor(id.ParseFormat) != ErrorSeverity.NoError)
                {
                    WriteLine(idStr);
                    WriteLine(ConsoleColor.Red, $"   {legacyIdStr}");
                    WriteLine(ConsoleColor.Red, $"   Cannot format enhanced to original format: ${legacyId.ValidationErrorDescription}");
                    return false;
                }

                roundTrip = legacyId.ToString(id.ParseFormat);
                if (!string.Equals(idStr, roundTrip, StringComparison.OrdinalIgnoreCase))
                {
                    WriteLine(idStr);
                    WriteLine(ConsoleColor.Red, $"   {legacyIdStr}");
                    WriteLine(ConsoleColor.Red, $"   ID doesn't match when round-tripped through enahcned ID: {roundTrip}");
                    return false;
                }
            }
            catch (Exception)
            {
                WriteLine(ConsoleColor.Red, idStr);
                throw;
            }

            return true;
        }

        static void ExtractIdsFromCASE(string inputPath, string outputPath)
        {
            // Read input from JSON into XML
            System.Xml.XPath.XPathDocument doc;
            using (var stream = new FileStream(inputPath, FileMode.Open, FileAccess.Read))
            {
                using (var reader = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(stream, new System.Xml.XmlDictionaryReaderQuotas()))
                {
                    doc = new System.Xml.XPath.XPathDocument(reader);
                }
            }

            using (var writer = new StreamWriter(outputPath, false, System.Text.Encoding.UTF8))
            {
                foreach (System.Xml.XPath.XPathNavigator node in doc.CreateNavigator().Select("//humanCodingScheme"))
                {
                    writer.WriteLine(node.Value);
                }
            }
        }

        // Legacy IDs frequently are missing the grade. This method adds the grade back
        // so that we can compare it with the round-tripped identifier.
        static string RemedyMissingGrade(string id, ContentSpecGrade grade)
        {
            int colon = id.IndexOf(':');
            if (colon <= 0)
            {
                return id;
            }

            int targetPart = -1;
            switch (id.Substring(0, colon))
            {
                case "SBAC-ELA-v1":
                    targetPart = 1;
                    break;
                case "SBAC-MA-v4":
                case "SBAC-MA-v5":
                    targetPart = 2;
                    break;

                case "SBAC-MA-v6":
                    targetPart = 3;
                    break;
                default:
                    return id;
            }

            string[] parts = id.Split('|');
            if (parts[targetPart].IndexOf('-') > 0)
            {
                return id;
            }

            parts[targetPart] = $"{parts[targetPart]}-{(int)grade}";
            return string.Join('|', parts);
        }

        static void DumpTargetSets()
        {
            int[,] targetSets = new int[13, 16];

            using (var idFile = new StreamReader(Path.Combine(s_workingDirectory, c_legacyIdFilename)))
            {
                for (; ; )
                {
                    string line = idFile.ReadLine();
                    if (line == null) break;

                    string[] parts = line.Split(' ', ':', '|');
                    if (parts.Length != 6) continue;

                    if (parts[1].Equals("SBAC-MA-v6"))
                    {
                        var target = parts[5];
                        int dash = target.IndexOf('-');
                        string grade = null;
                        if (dash >= 0)
                        {
                            grade = target.Substring(dash + 1);
                            target = target.Substring(0, dash);
                        }

                        if (parts[4].StartsWith("TS")
                            && target.Length == 1
                            && target[0] >= 'A'
                            && target[0] <= 'P')
                        {
                            int targetSet = int.Parse(parts[4].Substring(2));
                            int nGrade = int.Parse(parts[0]);
                            int nTarget = target[0] - 'A';
                            if (targetSets[nGrade, nTarget] != 0 && targetSets[nGrade, nTarget] != targetSet)
                            {
                                Console.WriteLine("Target set mismatch!!");
                            }
                            targetSets[nGrade, nTarget] = targetSet;
                        }
                    }
                }
            }

            for (int i = 3; i < 12; ++i)
            {
                Console.Write($"/* Grade  {i} */ {{ ");
                for (int j = 0; j < 16; ++j)
                {
                    if (j != 0) Console.Write(" ,");
                    Console.Write($"{targetSets[i, j]}");
                }
                Console.WriteLine(" },");
            }
        }

        static void DumpEmphasis()
        {
            int[,] emphasisPrimary = new int[13, 16];
            int[,] emphasisCount = new int[13, 16];

            using (var idFile = new StreamReader(Path.Combine(s_workingDirectory, c_legacyIdFilename)))
            {
                for (; ; )
                {
                    string line = idFile.ReadLine();
                    if (line == null) break;

                    string[] parts = line.Split(' ', ':', '|');
                    if (parts.Length != 7) continue;

                    if (parts[1].Equals("SBAC-MA-v4") || parts[1].Equals("SBAC-MA-v5"))
                    {
                        var target = parts[4];
                        int dash = target.IndexOf('-');
                        string grade = null;
                        if (dash >= 0)
                        {
                            grade = target.Substring(dash + 1);
                            target = target.Substring(0, dash);
                        }
                        else
                        {
                            grade = parts[0];
                        }

                        bool primary = parts[5].Equals("m");
                        if (!primary && !parts[5].Equals("a/s"))
                        {
                            //Console.WriteLine($"Err {parts[5]}");
                            continue;
                        }

                        int intGrade;
                        if (!int.TryParse(grade, out intGrade) || intGrade > 12)
                        {
                            //Console.WriteLine($"ErrGrade {grade}");
                            continue;
                        }

                        int intTarget = target[0] - 'A';
                        if (intTarget < 0 || intTarget >= 16)
                        {
                            //Console.WriteLine($"ErrTarget {target}");
                            continue;
                        }

                        if (primary) ++emphasisPrimary[intGrade, intTarget];
                        ++emphasisCount[intGrade, intTarget];

                        if (intGrade == 11 && intTarget == 14)
                        {
                            Console.WriteLine(line);
                        }
                    }
                }
            }

            for (int i = 3; i < 12; ++i)
            {
                Console.Write($"/* Grade  {i} */ {{ ");
                for (int j = 0; j < 16; ++j)
                {
                    if (j != 0) Console.Write(", ");
                    if (emphasisCount[i,j] == 0)
                    {
                        Console.Write("---");
                    }
                    else
                    {
                        Console.Write($"{((emphasisPrimary[i, j] * 100) / emphasisCount[i, j]),3}");
                    }
                }
                Console.WriteLine(" },");
            }
        }

        static void DumpHighSchoolDomains()
        {
            string[] domains = new string[16];

            using (var idFile = new StreamReader(Path.Combine(s_workingDirectory, c_legacyIdFilename)))
            {
                for (; ; )
                {
                    string line = idFile.ReadLine();
                    if (line == null) break;

                    string[] parts = line.Split(' ', ':', '|');
                    if (parts.Length != 7)
                        continue;

                    if (!parts[0].Equals("11"))
                        continue;

                    if (!parts[1].Equals("SBAC-MA-v4") && !parts[1].Equals("SBAC-MA-v5"))
                        continue;

                    if (!parts[2].Equals("1")) // Claim 1
                        continue;

                    var target = parts[4];
                    int dash = target.IndexOf('-');
                    string grade = null;
                    if (dash >= 0)
                    {
                        grade = target.Substring(dash + 1);
                        target = target.Substring(0, dash);
                    }

                    if (target.Length != 1)
                        continue;

                    int intTarget = target[0] - 'A';
                    if (intTarget < 0 || intTarget >= domains.Length)
                        continue;

                    if (domains[intTarget] != null
                        && !domains[intTarget].Equals(parts[3]))
                    {
                        Console.WriteLine($"Domain conflict: {domains[intTarget]} != {parts[3]}");
                    }

                    if (domains[intTarget] == null || domains[intTarget].Length < parts[3].Length)
                        domains[intTarget] = parts[3];
                }
            }

            for (int j = 0; j < domains.Length; ++j)
            {
                if (j != 0) Console.Write(", ");
                Console.Write($"\"{domains[j]}\"");
            }
            Console.WriteLine();

        }

        static void Test1()
        {
            string workingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

            Dictionary<string, int> targetCounts = new Dictionary<string, int>();
            Dictionary<string, int> targetSetCounts = new Dictionary<string, int>();
            Dictionary<string, int> ccssCounts = new Dictionary<string, int>();

            int[,] targetSets = new int[13, 16];

            using (var idFile = new StreamReader(Path.Combine(workingDirectory, c_legacyIdFilename)))
            {
                for (; ; )
                {
                    string line = idFile.ReadLine();

                    if (line == null) break;
                    string[] parts = line.Split(' ', ':', '|');

                    if (parts.Length < 2) continue;

                    int claimPart = 0;
                    int targetPart = 0;
                    int targetSetPart = -1;
                    int ccssPart = -1;
                    switch (parts[1])
                    {
                        case "SBAC-MA-v4":
                        case "SBAC-MA-v5":
                            claimPart = 2;
                            targetPart = 4;
                            ccssPart = 6;
                            break;

                        case "SBAC-MA-v6":
                            claimPart = 2;
                            targetPart = 5;
                            targetSetPart = 4;
                            break;

                        case "SBAC-ELA-v1":
                            claimPart = 2;
                            targetPart = 3;
                            ccssPart = 4;
                            break;

                        default:
                            continue;
                    }

                    if (targetPart >= parts.Length) continue;

                    var target = parts[targetPart];
                    int dash = target.IndexOf('-');
                    string grade = null;
                    if (dash >= 0)
                    {
                        grade = target.Substring(dash + 1);
                        target = target.Substring(0, dash);
                    }

                    targetCounts.Increment(target);

                    if (targetSetPart > 0 && targetSetPart < parts.Length && !string.IsNullOrEmpty(parts[0]))
                    {
                        targetSetCounts.Increment($"G{parts[0]}.C{parts[claimPart]}.T{target} = {parts[targetSetPart]}");
                        if (parts[targetSetPart].StartsWith("TS")
                            && target.Length == 1
                            && target[0] >= 'A'
                            && target[0] <= 'P')
                        {
                            int targetSet = int.Parse(parts[targetSetPart].Substring(2));
                            int nGrade = int.Parse(parts[0]);
                            int nTarget = target[0] - 'A';
                            if (targetSets[nGrade, nTarget] != 0 && targetSets[nGrade, nTarget] != targetSet)
                            {
                                Console.WriteLine("Target set mismatch!!");
                            }
                            targetSets[nGrade, nTarget] = targetSet;
                        }
                    }

                    if (parts[0].StartsWith("SBAC-MA"))
                    {
                        if (parts[claimPart].StartsWith('1') && ccssPart < parts.Length && ccssPart > 0)
                        {
                            ccssCounts.Increment(parts[ccssPart]);
                        }
                    }
                }
            }

            targetCounts.Dump2(Console.Out);
            Console.WriteLine();

            targetSetCounts.Dump2(Console.Out);
            Console.WriteLine();

            //ccssCounts.Dump2(Console.Out);

            for (int i = 3; i < 12; ++i)
            {
                Console.Write($"/* Grade  {i} */ {{ ");
                for (int j = 0; j < 16; ++j)
                {
                    if (j != 0) Console.Write(" ,");
                    Console.Write($"{targetSets[i, j]}");
                }
                Console.WriteLine(" },");
            }
        }

        static void WriteLine(string text)
        {
            Console.WriteLine(text);
        }

        static void WriteLine(ConsoleColor color, string text)
        {
            var save = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = save;
        }
    }

    internal static class ClassHelp
    {
        public static void Increment(this Dictionary<string, int> dict, string key)
        {
            int count;
            if (!dict.TryGetValue(key, out count)) count = 0;
            dict[key] = count + 1;
        }

        public static int Count(this Dictionary<string, int> dict, string key)
        {
            int count;
            if (!dict.TryGetValue(key, out count)) count = 0;
            return count;
        }

        public static void Dump(this Dictionary<string, int> dict, TextWriter writer)
        {
            List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>(dict);
            list.Sort(delegate (KeyValuePair<string, int> a, KeyValuePair<string, int> b)
            {
                int diff = b.Value - a.Value;
                return (diff != 0) ? diff : string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
            });
            foreach (var pair in list)
            {
                writer.WriteLine("{0,6}: {1}", pair.Value, pair.Key);
            }
        }

        public static void Dump2(this Dictionary<string, int> dict, TextWriter writer)
        {
            List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>(dict);
            list.Sort(delegate (KeyValuePair<string, int> a, KeyValuePair<string, int> b)
            {
                int diff = string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
                if (diff == 0)
                {
                    diff = b.Value - a.Value;
                }
                return diff;
            });
            foreach (var pair in list)
            {
                writer.WriteLine("{0,6}: {1}", pair.Value, pair.Key);
            }
        }
    }
}
﻿// <copyright file="L10n.cs" company="Paul Mattes">
// Copyright (c) Paul Mattes. All rights reserved.
// </copyright>

/// <summary>
/// Wx3270 localization utility.
/// </summary>
namespace L10ntool
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    using Newtonsoft.Json;

    /// <summary>
    /// Localization utility.
    /// </summary>
    public class L10n
    {
        /// <summary>
        /// Type of entry in CSV file.
        /// </summary>
        private enum EntryStatus
        {
            /// <summary>
            /// Translated and current.
            /// </summary>
            Translated,

            /// <summary>
            /// Not translated.
            /// </summary>
            NotTranslated,

            /// <summary>
            /// Message has changed between old and new; new translation needed.
            /// </summary>
            EnglishChanged,

            /// <summary>
            /// Translation is orphaned (path may have changed).
            /// </summary>
            Orphaned,
        }

        /// <summary>
        /// Create a CSV file from a message catalog.
        /// </summary>
        /// <param name="inNewMsgcat">New message catalog.</param>
        /// <param name="outCsv">Output CSV file.</param>
        public void CreateCsv(string inNewMsgcat, string outCsv)
        {
            // Read the message catalog.
            var msgcat = ReadMessageCatalog(inNewMsgcat);

            // Create the CSV from it.
            using (var t = new StreamWriter(outCsv, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
            {
                // Write the header.
                t.WriteLine($"#Status,Message,US English,Translation");

                // Dump out the translated messages.
                foreach (var kv in msgcat.OrderBy(k => k.Key))
                {
                    t.WriteLine("Untranslated," + CsvIfy(kv.Key) + "," + CsvIfy(kv.Value) + ",");
                }
            }
        }

        /// <summary>
        /// Create an updated CSV file from old and new message catalogs and an old translated message catalog.
        /// </summary>
        /// <param name="inOldMsgcat">Old message catalog.</param>
        /// <param name="inNewMsgcat">New message catalog.</param>
        /// <param name="inOldTraslatedMsgcat">Old translated message catalog.</param>
        /// <param name="outCsv">New CSV file.</param>
        public void UpdateCsv(string inOldMsgcat, string inNewMsgcat, string inOldTraslatedMsgcat, string outCsv)
        {
            // Read in the message catalogs.
            var oldMsgcat = ReadMessageCatalog(inOldMsgcat);
            var newMsgcat = ReadMessageCatalog(inNewMsgcat);
            var oldTranslatedMsgcat = ReadMessageCatalog(inOldTraslatedMsgcat);

            // Create a new dictionary that includes all of the new messages and all of the orphaned translated messages,
            // with the correct status in each.
            var classified = new Dictionary<string, Tuple<EntryStatus, string, string>>();
            foreach (var msg in newMsgcat)
            {
                if (oldMsgcat.ContainsKey(msg.Key))
                {
                    // Existing path.
                    if (msg.Value == oldMsgcat[msg.Key])
                    {
                        // Unchanged text.
                        if (oldTranslatedMsgcat.ContainsKey(msg.Key) && oldTranslatedMsgcat[msg.Key] != oldMsgcat[msg.Key])
                        {
                            // Already translated.
                            classified[msg.Key] = new Tuple<EntryStatus, string, string>(EntryStatus.Translated, msg.Value, oldTranslatedMsgcat[msg.Key]);
                        }
                        else
                        {
                            // Not translated.
                            classified[msg.Key] = new Tuple<EntryStatus, string, string>(EntryStatus.NotTranslated, msg.Value, string.Empty);
                        }
                    }
                    else
                    {
                        // Changed text.
                        if (oldTranslatedMsgcat.ContainsKey(msg.Key) && oldTranslatedMsgcat[msg.Key] != oldMsgcat[msg.Key])
                        {
                            // Already translated, but the US English text has changed.
                            classified[msg.Key] = new Tuple<EntryStatus, string, string>(EntryStatus.EnglishChanged, msg.Value, oldTranslatedMsgcat[msg.Key]);
                        }
                        else
                        {
                            // Not translated.
                            classified[msg.Key] = new Tuple<EntryStatus, string, string>(EntryStatus.NotTranslated, msg.Value, string.Empty);
                        }
                    }
                }
                else
                {
                    // New path.
                    classified[msg.Key] = new Tuple<EntryStatus, string, string>(EntryStatus.NotTranslated, msg.Value, string.Empty);
                }
            }

            foreach (var msg in oldTranslatedMsgcat)
            {
                if (!newMsgcat.ContainsKey(msg.Key))
                {
                    // Orphaned translation.
                    classified[msg.Key] = new Tuple<EntryStatus, string, string>(EntryStatus.Orphaned, "???", msg.Value);
                }
            }

            // Write the new CSV file.
            using (var t = new StreamWriter(outCsv, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
            {
                // Write the header.
                t.WriteLine($"#Status,Message,US English,Translation");

                // Dump out the translated messages.
                foreach (var kv in classified.OrderBy(k => k.Key))
                {
                    t.WriteLine(kv.Value.Item1 + "," + CsvIfy(kv.Key) + "," + CsvIfy(kv.Value.Item2) + "," + CsvIfy(kv.Value.Item3));
                }
            }
        }

        /// <summary>
        /// Create a message catalog from a CSV file.
        /// </summary>
        /// <param name="inCsv">CSV file.</param>
        /// <param name="outMsgcat">New message catalog file.</param>
        public void CsvToMsgcat(string inCsv, string outMsgcat)
        {
            var msgcat = new Dictionary<string, string>();

            // Read in the CSV file.
            using (StreamReader t = new StreamReader(inCsv, new UTF8Encoding()))
            {
                string line;
                var lno = 0;
                while ((line = ReadCsvLine(t)) != null)
                {
                    lno++;
                    if (line.StartsWith("#"))
                    {
                        continue;
                    }

                    var fields = UnCsvIfy(line);
                    if (fields.Length != 3 && fields.Length != 4)
                    {
                        Console.Error.WriteLine($"Line {lno}: Ignoring line with wrong number of fields ({fields.Length})");
                        continue;
                    }

                    if (fields.Length == 3)
                    {
                        msgcat[fields[1]] = fields[2];
                    }
                    else
                    {
                        msgcat[fields[1]] = fields[3];
                    }
                }
            }

            // Dump out the message catalog.
            using (var t = new StreamWriter(outMsgcat, append: false, new UTF8Encoding()))
            {
                var serializer = new JsonSerializer()
                {
                    Formatting = Formatting.Indented,
                };
                using (JsonWriter writer = new JsonTextWriter(t))
                {
                    serializer.Serialize(writer, msgcat);
                }
            }
        }

        /// <summary>
        /// Translate a string to valid CSV format.
        /// </summary>
        /// <param name="s">String to quote.</param>
        /// <returns>Quoted string.</returns>
        private static string CsvIfy(string s)
        {
            if (!"\"\n\r,".Any(c => s.Contains(c)))
            {
                return s;
            }

            return "\"" + s.Replace("\"", "\"\"").Replace("\r\n", "\n") + "\"";
        }

        /// <summary>
        /// Read a line of text from a CSV file, embedding newline characters instead of treating them as line terminators.
        /// </summary>
        /// <param name="t">Stream to read.</param>
        /// <returns>Line of text, or null.</returns>
        private static string ReadCsvLine(StreamReader t)
        {
            int c;
            var any = false;
            var line = string.Empty;
            var cr = false;
            while ((c = t.Read()) != -1)
            {
                any = true;
                if (cr)
                {
                    cr = false;
                    if (c == '\n')
                    {
                        // End of line.
                        break;
                    }

                    line += '\r';
                    if (c == '\r')
                    {
                        cr = true;
                    }
                    else
                    {
                        line += (char)c;
                    }
                }
                else
                {
                    if (c == '\r')
                    {
                        cr = true;
                    }
                    else
                    {
                        line += (char)c;
                    }
                }
            }

            if (cr)
            {
                line += '\r';
            }

            if (!any)
            {
                return null;
            }

            return line;
        }

        /// <summary>
        /// Translate a line of CSV to an array of fields.
        /// </summary>
        /// <param name="s">String to translate.</param>
        /// <returns>Array of fields.</returns>
        private static string[] UnCsvIfy(string s)
        {
            var ret = new List<string>();

            while (s != string.Empty)
            {
                var field = string.Empty;
                if (s[0] == '"')
                {
                    // The field begins with a double quote. Scan until another un-doubled double quote.
                    s = s.Substring(1);
                    var quote = false;
                    var done = false;
                    while (!done && s.Length != 0)
                    {
                        if (quote)
                        {
                            quote = false;
                            if (s[0] == '"')
                            {
                                // Doubled quote.
                                field += '"';
                            }
                            else if (s[0] == ',')
                            {
                                done = true;
                            }
                            else
                            {
                                Console.Error.WriteLine($"Invalid format: closing double quote not followed by comma (got '{s[0]}')");
                                return new string[0];
                            }
                        }
                        else if (s[0] == '"')
                        {
                            quote = true;
                        }
                        else
                        {
                            field += s[0];
                        }

                        s = s.Substring(1);
                    }
                }
                else
                {
                    // Scan until a comma.
                    while (s.Length != 0 && s[0] != ',')
                    {
                        field += s[0];
                        s = s.Substring(1);
                    }

                    if (s != string.Empty)
                    {
                        // Remove the comma.
                        s = s.Substring(1);
                    }
                }

                ret.Add(field);
            }

            // Translate newline characters into CR + LF.
            return ret.Select(f => f.Replace("\n", "\r\n")).ToArray();
        }

        /// <summary>
        /// Deserialize a message catalog.
        /// </summary>
        /// <param name="fileName">File name.</param>
        /// <returns>Message catalog.</returns>
        private static Dictionary<string, string> ReadMessageCatalog(string fileName)
        {
            var serializer = new JsonSerializer();
            using (StreamReader t = new StreamReader(fileName, new UTF8Encoding()))
            {
                using (JsonReader reader = new JsonTextReader(t))
                {
                    try
                    {
                        return serializer.Deserialize<Dictionary<string, string>>(reader);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Cannot deserialize message catalog '{fileName}': {e.Message}");
                    }
                }
            }
        }
    }
}

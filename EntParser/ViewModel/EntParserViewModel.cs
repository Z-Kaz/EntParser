using CsvHelper.Configuration;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace EntParser.ViewModel
{
    public class EntParserViewModel : ViewModelBase
    {
        public EntParserViewModel()
        {
            // Selected Index set to default by 15
            SelectedTimeZoneIndex = 15;

            // Reads Save file if it exists
            if (File.Exists(saveFile))
            {
                string jsonString = File.ReadAllText(saveFile);
                SaveFileData saveFileDate = JsonSerializer.Deserialize<SaveFileData>(jsonString);
                SourceDirectory = saveFileDate.Source;
                ArchiveDirectory = saveFileDate.Archive;
                SelectedTimeZoneIndex = saveFileDate.TimeZone;
            }

            if (SourceDirectory == null || ArchiveDirectory == null)
                Reset();

            if (!File.Exists(SourceDirectory))
                Directory.CreateDirectory(SourceDirectory);

            if (!File.Exists(ArchiveDirectory))
                Directory.CreateDirectory(ArchiveDirectory);

            TimeZoneData();
            SaveCommand = new RelayCommand(Save);
            ResetCommand = new RelayCommand(Reset);
            ParseCommand = new RelayCommand(Parse);
        }

        public ICommand SaveCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand ParseCommand { get; }

        public string SourceDirectory
        {
            get => _sourceDirectory;
            set
            {
                _sourceDirectory = value;
                RaisePropertyChanged(nameof(SourceDirectory));
            }
        }
        public string ArchiveDirectory
        {
            get => _archiveDirectory;
            set
            {
                _archiveDirectory = value;
                RaisePropertyChanged(nameof(ArchiveDirectory));
            }
        }

        public ObservableCollection<TimeZoneEntry> ComboBoxTimeZoneItemSource
        {
            get => _comboBoxTimeZomeItemSource;
            set
            {
                _comboBoxTimeZomeItemSource = value;
                RaisePropertyChanged(nameof(ComboBoxTimeZoneItemSource));
            }
        }

        public int SelectedTimeZoneIndex
        {
            get => _selectedTimeZoneIndex;
            set
            {
                _selectedTimeZoneIndex = value;
                RaisePropertyChanged(nameof(SelectedTimeZoneIndex));
            }
        }
        private void TimeZoneData()
        {
            ComboBoxTimeZoneItemSource = new ObservableCollection<TimeZoneEntry>();
            int i = 0;
            foreach (TimeZoneInfo timeZone in TimeZoneInfo.GetSystemTimeZones())
            {
                ComboBoxTimeZoneItemSource.Add(new TimeZoneEntry(i, timeZone.Id));
            }
        }

        private void Save()
        {
            SaveFileData directories = new SaveFileData(SourceDirectory, ArchiveDirectory, SelectedTimeZoneIndex);
            string jsonString = JsonSerializer.Serialize(directories);
            WriteSaveFile(jsonString);
        }

        public void Reset()
        {
            SourceDirectory = DefaultSourceDirectory;
            ArchiveDirectory = DefaultArchiveDirectory;
        }

        public void Parse()
        {
            ParsedLines = new List<ParsedLines>();
            TimeZoneEntry timeZoneEntry = ComboBoxTimeZoneItemSource[SelectedTimeZoneIndex];
            string userTimeZone = timeZoneEntry.Name;

            try
            {
                var txtFiles = Directory.EnumerateFiles(SourceDirectory, "*.log", SearchOption.TopDirectoryOnly);

                foreach (string currentFile in txtFiles)
                {
                    string fileName = currentFile.Substring(SourceDirectory.Length + 1);

                    // Gets the year, day, and month from the filename
                    Int32.TryParse(fileName.Substring(8, 4), out int year);
                    Int32.TryParse(fileName.Substring(13, 2), out int month);
                    Int32.TryParse(fileName.Substring(16, 2), out int day);

                    // Gets all lines that contain "/5"
                    var lines = from line in File.ReadLines(currentFile)
                                where line.Contains("/5")
                                select line;

                    foreach (string l in lines)
                    {
                        // Checks to see if lines have a location that matches with the regex
                        MatchCollection matches = Regex.Matches(l, regexPattern, RegexOptions.IgnoreCase);

                        if (matches.Count != 0)
                        {
                            // Gets the hour, minute, and second from each line
                            Int32.TryParse(l.Substring(0, 2), out int hour);
                            Int32.TryParse(l.Substring(3, 2), out int minute);
                            Int32.TryParse(l.Substring(6, 2), out int second);

                            DateTime dateTime = new DateTime(year, month, day, hour, minute, second);
                            dateTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(dateTime, userTimeZone, "UTC");

                            // Gets the number of ents pruned out of 5
                            string prunedEnts = l.Substring(l.IndexOf("/") - 1, 1);

                            ParsedLines.Add(new ParsedLines(dateTime, matches[0].Groups[1].Value, prunedEnts));
                        }
                    }
                }
                // Checks to see if any two lines that are within 1 minute of each other have the same location
                // keeps the latest sent message
                for (int i = 0; i < ParsedLines.Count - 1; i++)
                {
                    if ((ParsedLines[i + 1].DateTime - ParsedLines[i].DateTime).TotalMinutes < 1 && (ParsedLines[i + 1].Location == ParsedLines[i].Location))
                    {
                        ParsedLines[i + 1].PrunedEnts = ParsedLines[i + 1].PrunedEnts + ", " + ParsedLines[i].PrunedEnts;
                        ParsedLines.RemoveAt(i);
                        i--;
                    }
                }

                OutputFileName = ParsedLines.First().DateTime.ToString("MM-dd-yyyy") + " - " + ParsedLines.Last().DateTime.ToString("MM-dd-yyyy");

                using (StreamWriter sw = File.CreateText($@"{ArchiveDirectory}\{OutputFileName}.txt"))
                {
                    for (int i = 0; i < ParsedLines.Count; i++)
                    {
                        sw.WriteLine($"{ParsedLines[i].DateTime.ToString("MM/dd/yyyy HH:mm:ss")} {ParsedLines[i].Location} {ParsedLines[i].PrunedEnts}");
                        if (ParsedLines[i] != ParsedLines.Last())
                        {
                            if (ParsedLines[i + 1].DateTime.Day != ParsedLines[i].DateTime.Day)
                                sw.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"); 
                        }
                    }
                }
                //var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                //{
                //    NewLine = Environment.NewLine,
                //};
                ParsedLines.Clear();
            }
            catch (UnauthorizedAccessException uAEx)
            {
                Console.WriteLine(uAEx.Message);
            }
            catch (PathTooLongException pathEx)
            {
                Console.WriteLine(pathEx.Message);
            }
        }

        public static void WriteSaveFile(string jsonString)
        {
            if (!File.Exists(saveFile))
                Directory.CreateDirectory(saveFolder);

            using (StreamWriter sw = File.CreateText(saveFile))
                sw.WriteLine(jsonString);
        }

        private ObservableCollection<TimeZoneEntry> _comboBoxTimeZomeItemSource;
        private int _selectedTimeZoneIndex;
        private string _sourceDirectory;
        private string _archiveDirectory;
        private string OutputFileName;
        private List<ParsedLines> ParsedLines;
        private List<DateTime> DateTimeList;
        private static string DefaultSourceDirectory = @"C:\Users\Public\Documents\ChatLogEntParser\ToBeParsed";
        private static string DefaultArchiveDirectory = @"C:\Users\Public\Documents\ChatLogEntParser\Parsed";
        private static string saveFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\ChatLogEntParser";
        private static string saveFile = saveFolder + "\\Save.txt";
        private static string regexPattern = $@"^.*(?=.*\b(p(rif+)?\s*(teak|mahog(any)?|mag(e|ic))|(n(orth)?|s(outh)?)?\s*seer|(n(orth)?|s(outh)?)\s*mag(e|ic)|(w(est)?|s(outh)?)\s*legend|(xerics?\b)?(glade|heart|lookout)|dray(nor)?" +
                                  @"|rada(\b(e(ast)?|w(est)?))?|zalc(ano)?|edge|rim(mington)?|ape(\batoll)?|l*grave|lumb(y|ridge)?" +
                                  @"|ge|v*castle|church|myth|soul|wcg|battlefront|botd3|shayzien|bjp|bkp|arc|cir|corsair|barb|cw|hspirit|nieve|shay|fally|grotto|Gwennith|ncabbage|scabbage|gnome|lumber|nwseer|farm|hos|phas|pisc|sarim|" +
                                  @"ltree|cam|legend|Oasis|Fortis|zanaris|n*yak|flax|MTA)s?(\b|$))";
    }
    public class TimeZoneEntry
    {
        public int ID { get; set; }
        public string Name { get; set; }

        public TimeZoneEntry(int id, string name)
        {
            ID = id;
            Name = name;
        }
    }

    public class SaveFileData
    {
        public string Source { get; set; }
        public string Archive { get; set; }
        public int TimeZone {  get; set; }

        public SaveFileData(string source, string archive, int timeZone)
        {
            Source = source;
            Archive = archive;
            TimeZone = timeZone;
        }
    }

    public class ParsedLines
    {
        public DateTime DateTime { get; set; }
        public string Location { get; set; }
        public string PrunedEnts { get; set; }

        public ParsedLines(DateTime dateTime, string location, string prunedEnts)
        {
            DateTime = dateTime;
            Location = location;
            PrunedEnts = prunedEnts;
        }
    }
}

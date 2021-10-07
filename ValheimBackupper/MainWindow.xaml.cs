using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Path = System.IO.Path;

namespace ValheimBackupper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string ValheimBackUpDirectory = "D:\\ValheimBackup";

        private string valheimHomeDirectory = Path.Combine(Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)).FullName, "LocalLow\\IronGate\\Valheim");
        private FileSystemWatcher fileSystemWatcher;
        private readonly DispatcherTimer dispatcherTimer = new();
        private const int FileSystemChangeHandlingDelaySeconds = 30;
        private const int MaxBackUpCount = 100;

        private bool isWatching = false;

        public MainWindow()
        {
            InitializeComponent();

            //NOTE: AutoStart
            StartWatching();

            dispatcherTimer.Tick += (_, _) =>
            {
                dispatcherTimer.Stop();
                BackUp();
            };
        }

        private void StartWatching()
        {
            isWatching = true;
            StartStopButton.Content = "Stop";

            fileSystemWatcher = new FileSystemWatcher(valheimHomeDirectory);
            fileSystemWatcher.Changed += (_, args) => { HandleChange(args); };
            fileSystemWatcher.Created += (_, args) => { HandleChange(args); };
            fileSystemWatcher.Deleted += (_, args) => { HandleChange(args); };
            fileSystemWatcher.Renamed += (_, args) => { HandleChange(args); };

            fileSystemWatcher.IncludeSubdirectories = true;
            fileSystemWatcher.EnableRaisingEvents = true;

            WriteToConsole("Watching Started");
        }

        private void HandleChange(FileSystemEventArgs args)
        {
            //Note: We intend to watch only the "Worlds" and "Characters" folders
            if (
                (
                    args.FullPath.StartsWith(Path.Combine(valheimHomeDirectory, "worlds"), StringComparison.InvariantCultureIgnoreCase) ||
                    args.FullPath.StartsWith(Path.Combine(valheimHomeDirectory, "characters"), StringComparison.InvariantCultureIgnoreCase)
                ) &&
                Path.GetFileName(args.Name) != "steam_autocloud.vdf"
                )
            {
                dispatcherTimer.Stop();
                dispatcherTimer.Interval = TimeSpan.FromSeconds(FileSystemChangeHandlingDelaySeconds);
                dispatcherTimer.Start();
                WriteToConsole($"Change detected ({args.Name} - {args.ChangeType})");
            }
        }

        private void StopWatching()
        {
            isWatching = false;
            StartStopButton.Content = "Start";
            fileSystemWatcher?.Dispose();
            fileSystemWatcher = null;
            WriteToConsole("Watching Stopped");
        }

        private static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));

            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
        }

        private void BackUp()
        {
            WriteToConsole("Backup Started ...");

            var backupDirectory = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");

            try
            {
                if (!Directory.Exists(ValheimBackUpDirectory))
                    Directory.CreateDirectory(ValheimBackUpDirectory);

                var currentValheimBackUpDirectory = Path.Combine(ValheimBackUpDirectory, backupDirectory);

                Directory.CreateDirectory(currentValheimBackUpDirectory);
                CopyFilesRecursively(valheimHomeDirectory, currentValheimBackUpDirectory);

                foreach (var dir in Directory.GetDirectories(ValheimBackUpDirectory).OrderByDescending(d => d).Skip(MaxBackUpCount))
                    Directory.Delete(dir, true);
            }
            catch (Exception e)
            {
                WriteToConsole(e.ToString());
            }

            WriteToConsole("Backup Finished!");
        }

        private void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (isWatching)
                StopWatching();
            else
                StartWatching();
        }

        private void BackUpNowButton_Click(object sender, RoutedEventArgs e) => BackUp();

        private void WriteToConsole(string message)
        {
            var timeStampedMessage = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss:fff") + " - " + message;

            string consoleText = Dispatcher.Invoke(() => Console.Text);

            var lines = consoleText.Split(Environment.NewLine).ToList();
            lines = new List<string> { timeStampedMessage }.Concat(lines).Take(100).ToList();

            Dispatcher.Invoke(() => Console.Text = string.Join(Environment.NewLine, lines));
        }
    }
}

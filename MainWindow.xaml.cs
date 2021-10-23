using HostManager.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Path = System.IO.Path;

namespace HostManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly FileSystemWatcher hostsFileWatcher;
        private StateModel appState { get; set; } = new();
        private readonly string etcFolder = Path.Join(Environment.GetEnvironmentVariable("windir"), @"System32\drivers\etc");
        private readonly System.Timers.Timer persistTimer = new();
        private readonly SemaphoreSlim refreshSemaphore = new(1, 1);
        private readonly CancellationTokenSource cancellationSource = new();
        private readonly CancellationToken cancellationToken;

        public MainWindow()
        {
            this.DataContext = this.appState;
            this.cancellationToken = this.cancellationSource.Token;

            hostsFileWatcher = new(etcFolder);
            hostsFileWatcher.NotifyFilter = NotifyFilters.LastWrite;
            hostsFileWatcher.Filter = "hosts";
            hostsFileWatcher.Changed += OnChanged;
            hostsFileWatcher.EnableRaisingEvents = true;

            persistTimer.Elapsed += (_, _) => PersistToHosts();
            persistTimer.Interval = 2000;
            persistTimer.Start();

            _ = Refresh(cancellationToken);

            InitializeComponent();
        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            cancellationSource.Cancel();
            persistTimer.Stop();
            PersistToHosts();
        }

        private void PersistToHosts()
        {
            if (!this.appState.HostCollection.Any(x => x.Dirty))
            {
                return;
            }

            string[] lines = File.ReadAllLines(Path.Join(etcFolder, "hosts"));

            foreach (var host in this.appState.HostCollection.Where(x => x.Dirty))
            {
                if (host.Enabled)
                {
                    lines[host.LineNumber] = lines[host.LineNumber].Trim(new char[] { '#', ' ', '\t' });
                }
                else
                {
                    lines[host.LineNumber] = $"#{lines[host.LineNumber]}";
                }

                host.Clean();
            }

            hostsFileWatcher.EnableRaisingEvents = false;
            File.WriteAllLines(Path.Join(etcFolder, "hosts"), lines);
            hostsFileWatcher.EnableRaisingEvents = true;
        }

        private async void OnChanged(object sender, FileSystemEventArgs e)
        {
            await Refresh(cancellationToken);
        }

        private async Task Refresh(CancellationToken cancellationToken = default)
        {
            List<string> lines = new();

            while (true)
            {
                await Task.Delay(300);

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    lines = File.ReadAllLines(Path.Join(etcFolder, "hosts"))
                        .Select(x => x.Trim())
                        .ToList();
                    break;
                }
                catch (IOException)
                {
                    // This happens when file is used by another process (we are just modifying hosts).
                }
            }

            try
            {
                refreshSemaphore.Wait(cancellationToken);

                await this.Dispatcher.InvokeAsync(() => this.appState.HostCollection.Clear());

                for (int lineNumber = 0; lineNumber < lines.Count; lineNumber++)
                {
                    string line = lines[lineNumber];

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    string trimmedLine = line
                        .Trim(new char[] { ' ', '\t', '#' });

                    string ip = trimmedLine
                        .TakeWhile(x => !char.IsWhiteSpace(x))
                        .Aggregate("", (m, x) => m + x);

                    string host = trimmedLine.Substring(ip.Length).Trim()
                        .TakeWhile(x => !char.IsWhiteSpace(x) && x != '#')
                        .Aggregate("", (m, x) => m + x);

                    int commentStartIx = trimmedLine.IndexOf("#");
                    string comment = commentStartIx != -1 ? trimmedLine.Substring(trimmedLine.IndexOf("#") + 1).Trim() : "";

                    if (IPAddress.TryParse(ip, out _))
                    {
                        HostModel hostModel = new()
                        {
                            Host = host,
                            Address = ip,
                            Enabled = line[0] != '#',
                            LineNumber = lineNumber,
                            Comment = comment
                        };
                        hostModel.Clean();

                        await this.Dispatcher.InvokeAsync(() => this.appState.HostCollection.Add(hostModel));
                    }
                }
            }
            finally
            {
                refreshSemaphore.Release();
            }
        }
    }
}

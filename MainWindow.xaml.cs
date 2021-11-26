using HostManager.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Path = System.IO.Path;

namespace HostManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly FileSystemWatcher hostsFileWatcher;
        private StateModel AppState { get; set; } = new();
        private readonly string etcFolder = Path.Join(Environment.GetEnvironmentVariable("windir"), @"System32\drivers\etc");
        private readonly System.Timers.Timer persistTimer = new();
        private readonly SemaphoreSlim refreshSemaphore = new(1, 1);
        private readonly CancellationTokenSource cancellationSource = new();
        private readonly CancellationToken cancellationToken;

        public MainWindow()
        {
            this.DataContext = this.AppState;
            this.cancellationToken = this.cancellationSource.Token;

            hostsFileWatcher = new(etcFolder);
            hostsFileWatcher.NotifyFilter = NotifyFilters.LastWrite;
            hostsFileWatcher.Filter = "hosts";
            hostsFileWatcher.Changed += OnChanged;
            hostsFileWatcher.EnableRaisingEvents = true;

            persistTimer.Elapsed += (_, _) => PersistToHosts();
            persistTimer.Interval = 500;
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
            if (!this.AppState.HostCollection.Any(x => x.Dirty || x.Deleted))
            {
                return;
            }

            bool mustRefresh = false;
            List<string> lines = null;
            try
            {
                hostsFileWatcher.EnableRaisingEvents = false;

                lines = File.ReadAllLines(Path.Join(etcFolder, "hosts")).ToList();

                foreach (var host in this.AppState.HostCollection.Where(x => x.Dirty && x.LineNumber.HasValue))
                {
                    if (!host.IsValid)
                    {
                        continue;
                    }

                    lines[host.LineNumber.Value] = host.ToString();
                    host.Clean();
                }

                foreach (var host in this.AppState.HostCollection.Where(x => x.Dirty && !x.LineNumber.HasValue))
                {
                    if (!host.IsValid)
                    {
                        continue;
                    }

                    lines.Add(host.ToString());
                    host.LineNumber = lines.Count - 1;
                }

                foreach (var host in this.AppState.HostCollection.Where(x => x.Deleted))
                {
                    if (host.LineNumber.HasValue)
                    {
                        lines.RemoveAt(host.LineNumber.Value);
                        mustRefresh = true;     // line numbers for hosts are different, so it's easier to simply refresh from file
                    }
                    else
                    {
                        _ = this.Dispatcher.InvokeAsync(() => this.AppState.HostCollection.Remove(host));
                    }
                }

                File.WriteAllLines(Path.Join(etcFolder, "hosts"), lines);
            }
            finally
            {
                hostsFileWatcher.EnableRaisingEvents = true;

                if (lines is not null && mustRefresh)
                {
                    _ = this.Refresh(lines);
                }
            }
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
                await Task.Delay(500, cancellationToken);

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

            await Refresh(lines);
        }

        public async Task Refresh(List<string> lines)
        {
            try
            {
                refreshSemaphore.Wait(cancellationToken);

                await this.Dispatcher.InvokeAsync(() => this.AppState.HostCollection.Clear());

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
                    string comment = commentStartIx != -1 
                        ? trimmedLine.Substring(trimmedLine.IndexOf("#") + 1).Trim() 
                        : "";

                    HostModel hostModel = new()
                    {
                        Host = host,
                        Address = ip,
                        Enabled = line[0] != '#',
                        LineNumber = lineNumber,
                        Comment = comment
                    };

                    if (hostModel.IsValid)
                    {
                        hostModel.Clean();

                        await this.Dispatcher.InvokeAsync(() => this.AppState.HostCollection.Add(hostModel));
                    }
                }
            }
            finally
            {
                refreshSemaphore.Release();
            }
        }

        private void HostsDataGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Delete)
            {
                return;
            }

            if (sender is not DataGrid)
            {
                return;
            }

            var grid = sender as DataGrid;

            if (grid.SelectedItem is not HostModel host)
            {
                return;
            }

            var confirmation = MessageBox.Show($"Are you sure want to delete selected host {host.Host}?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);

            host.Deleted = confirmation == MessageBoxResult.Yes;

            e.Handled = true;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                _ = Refresh();
            }
        }
    }
}

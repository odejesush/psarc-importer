using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PsarcImporter.Models;

namespace PsarcImporter.Gui.ViewModels;

internal class PsarcFileItem : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public string FileName { get; }
    public string FilePath { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public PsarcFileItem(string fileName, string filePath)
    {
        FileName = fileName;
        FilePath = filePath;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal class MainWindowViewModel : INotifyPropertyChanged
{
    private string _sourcePath = string.Empty;
    private string _outputPath = string.Empty;
    private bool _isConverting;
    private string _statusMessage = "Ready";
    private int _progress;
    private int _progressMax;
    private readonly Window _window;

    public ObservableCollection<PsarcFileItem> Files { get; } = new();

    public string SourcePath
    {
        get => _sourcePath;
        set { _sourcePath = value; OnPropertyChanged(); RefreshFiles(); }
    }

    public string OutputPath
    {
        get => _outputPath;
        set { _outputPath = value; OnPropertyChanged(); }
    }

    public bool IsConverting
    {
        get => _isConverting;
        set { _isConverting = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanConvert)); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public int Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    public int ProgressMax
    {
        get => _progressMax;
        set { _progressMax = value; OnPropertyChanged(); }
    }

    public bool CanConvert => !IsConverting && Files.Any(f => f.IsSelected);

    public ICommand BrowseSourceCommand { get; }
    public ICommand BrowseOutputCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand ConvertCommand { get; }

    public MainWindowViewModel(Window window)
    {
        _window = window;
        BrowseSourceCommand = new AsyncCommand(BrowseSource);
        BrowseOutputCommand = new AsyncCommand(BrowseOutput);
        SelectAllCommand = new ActionCommand(SelectAll);
        DeselectAllCommand = new ActionCommand(DeselectAll);
        ConvertCommand = new AsyncCommand(Convert);
    }

    private async Task BrowseSource()
    {
        var result = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Source Directory",
            AllowMultiple = false
        });

        if (result.Count > 0)
            SourcePath = result[0].Path.LocalPath;
    }

    private async Task BrowseOutput()
    {
        var result = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Output Directory",
            AllowMultiple = false
        });

        if (result.Count > 0)
            OutputPath = result[0].Path.LocalPath;
    }

    private void RefreshFiles()
    {
        Files.Clear();

        if (string.IsNullOrWhiteSpace(SourcePath) || !Directory.Exists(SourcePath))
            return;

        string[] psarcFiles = Directory.GetFiles(SourcePath, "*.psarc", SearchOption.TopDirectoryOnly);

        foreach (string file in psarcFiles)
            Files.Add(new PsarcFileItem(Path.GetFileName(file), file));

        OnPropertyChanged(nameof(CanConvert));
    }

    private void SelectAll()
    {
        foreach (var item in Files)
            item.IsSelected = true;
        OnPropertyChanged(nameof(CanConvert));
    }

    private void DeselectAll()
    {
        foreach (var item in Files)
            item.IsSelected = false;
        OnPropertyChanged(nameof(CanConvert));
    }

    private async Task Convert()
    {
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            StatusMessage = "Error: No output directory selected";
            return;
        }

        var selectedFiles = Files.Where(f => f.IsSelected).ToList();
        if (selectedFiles.Count == 0)
        {
            StatusMessage = "Error: No files selected";
            return;
        }

        IsConverting = true;
        ProgressMax = selectedFiles.Count;
        Progress = 0;

        string workDir = Path.Combine(Path.GetTempPath(), $"PsarcImporter_GUI_{Guid.NewGuid():N}");
        Directory.CreateDirectory(OutputPath);

        int succeeded = 0;
        int failed = 0;

        try
        {
            foreach (var item in selectedFiles)
            {
                StatusMessage = $"Importing '{item.FileName}'...";
                string fileWorkDir = Path.Combine(workDir, Guid.NewGuid().ToString("N"));
                string tempOutput = Path.Combine(fileWorkDir, "temp.theory");

                try
                {
                    Directory.CreateDirectory(fileWorkDir);
                    var (title, artist) = await PsarcImporter.PsarcConverter.ConvertAsync(
                        item.FilePath, tempOutput, fileWorkDir);

                    string sanitizedTitle = PsarcImporter.PsarcConverter.SanitizeFileName(title);
                    string sanitizedArtist = PsarcImporter.PsarcConverter.SanitizeFileName(artist);
                    string fileName = string.IsNullOrWhiteSpace(artist)
                        ? $"{sanitizedTitle}.theory"
                        : $"{sanitizedArtist} - {sanitizedTitle}.theory";
                    string finalPath = Path.Combine(OutputPath, fileName);

                    if (File.Exists(finalPath))
                    {
                        StatusMessage = $"Skipping '{title}' (already exists)";
                    }
                    else
                    {
                        File.Move(tempOutput, finalPath);
                        StatusMessage = $"Imported '{title}'";
                    }
                    succeeded++;
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Failed '{item.FileName}': {ex.Message}";
                    failed++;
                }
                finally
                {
                    try { if (Directory.Exists(fileWorkDir)) Directory.Delete(fileWorkDir, true); }
                    catch { }
                }

                Progress++;
            }

            StatusMessage = $"Done: {succeeded} imported, {failed} failed";
        }
        finally
        {
            try { if (Directory.Exists(workDir)) Directory.Delete(workDir, true); }
            catch { }
            IsConverting = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal class AsyncCommand : ICommand
{
    private readonly Func<Task> _execute;
    private bool _isExecuting;

    public AsyncCommand(Func<Task> execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isExecuting;

    public async void Execute(object? parameter)
    {
        if (_isExecuting) return;
        _isExecuting = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await _execute(); }
        finally
        {
            _isExecuting = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

internal class ActionCommand : ICommand
{
    private readonly Action _execute;

    public ActionCommand(Action execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute();
}

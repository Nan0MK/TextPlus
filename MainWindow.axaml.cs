using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TextPlus;

public class FileTreeItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string Icon { get; set; } = "📄";
    public bool IsDirectory { get; set; }
    public ObservableCollection<FileTreeItem> Children { get; set; } = new();
    public bool IsExpanded { get; set; }
}

public enum DialogResult
{
    Ok,
    Cancel,
    Yes,
    No
}

public partial class MainWindow : Window
{
    private string? _currentFilePath;
    private string? _currentFolderPath;
    private bool _isModified = false;
    private double _zoomLevel = 1.0;
    private const double ZoomStep = 0.1;
    private const double MinZoom = 0.5;
    private const double MaxZoom = 3.0;

    public MainWindow() : this(new List<string>()) { }

    public MainWindow(List<string> args)
    {
        InitializeComponent();

        if (args.Count > 0 && File.Exists(args[0]))
        {
            _ = LoadFileAsync(args[0]);
        }

        Editor.TextChanged += (s, e) =>
        {
            if (!_isModified)
            {
                _isModified = true;
                UpdateTitle();
            }
            UpdatePosition();
            UpdateLineNumbers();
        };

        Editor.PointerPressed += (s, e) => UpdatePosition();
        
        Editor.PointerWheelChanged += (s, e) =>
        {
            if (e.KeyModifiers == KeyModifiers.Control)
            {
                if (e.Delta.Y > 0)
                    ZoomIn();
                else
                    ZoomOut();
                e.Handled = true;
            }
        };
        
        FileTree.PointerPressed += (s, e) =>
        {
            if (FileTree.SelectedItem is FileTreeItem item && !item.IsDirectory)
            {
                _ = OpenFileFromTreeAsync(item.FullPath);
            }
        };
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
                case Key.N:
                    NewFile(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.O:
                    OpenFile(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.S:
                    SaveFile(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.Z:
                    Undo(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.Y:
                    Redo(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.X:
                    Cut(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.C:
                    Copy(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.V:
                    Paste(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.A:
                    SelectAll(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D0:
                case Key.NumPad0:
                    ResetZoom();
                    e.Handled = true;
                    break;
                case Key.OemPlus:
                case Key.Add:
                    ZoomIn();
                    e.Handled = true;
                    break;
                case Key.OemMinus:
                case Key.Subtract:
                    ZoomOut();
                    e.Handled = true;
                    break;
            }
        }
        else if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            if (e.Key == Key.S)
            {
                SaveFileAs(this, new RoutedEventArgs());
                e.Handled = true;
            }
            // else if (e.Key == Key.O)
            // {
            //     OpenFolder(this, new RoutedEventArgs());
            //     e.Handled = true;
            // }
        }
    }

    private void UpdateLineNumbers()
    {
        var text = Editor.Text ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            LineNumbers.Text = "1";
            StatusText.Text = "Ln 1, Col 1 | Lines: 1 | Chars: 0";
            return;
        }
        
        int lineCount = text.Split('\n').Length;
        
        var lines = new System.Text.StringBuilder();
        for (int i = 1; i <= lineCount; i++)
        {
            lines.AppendLine(i.ToString());
        }
        LineNumbers.Text = lines.ToString().TrimEnd();
        
        StatusText.Text = $"Ln {GetCurrentLine()}, Col {GetCurrentColumn()} | Lines: {lineCount} | Chars: {text.Length} | Zoom: {(_zoomLevel * 100):F0}%";
    }

    private void ZoomIn()
    {
        if (_zoomLevel < MaxZoom)
        {
            _zoomLevel = Math.Min(MaxZoom, _zoomLevel + ZoomStep);
            ApplyZoom();
        }
    }

    private void ZoomOut()
    {
        if (_zoomLevel > MinZoom)
        {
            _zoomLevel = Math.Max(MinZoom, _zoomLevel - ZoomStep);
            ApplyZoom();
        }
    }

    private void ResetZoom()
    {
        _zoomLevel = 1.0;
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        double baseFontSize = 14;
        Editor.FontSize = baseFontSize * _zoomLevel;
        LineNumbers.FontSize = baseFontSize * _zoomLevel;
        UpdatePosition();
    }

    private async Task LoadFileAsync(string filePath)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            Editor.Text = content;
            _currentFilePath = filePath;
            _isModified = false;
            UpdateTitle();
            UpdateLineNumbers();
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Error", $"Failed to open file: {ex.Message}");
        }
    }

    private int GetCurrentLine()
    {
        var text = Editor.Text ?? string.Empty;
        var caretIndex = Editor.CaretIndex;
        int line = 1;
        for (int i = 0; i < caretIndex && i < text.Length; i++)
        {
            if (text[i] == '\n')
                line++;
        }
        return line;
    }

    private int GetCurrentColumn()
    {
        var text = Editor.Text ?? string.Empty;
        var caretIndex = Editor.CaretIndex;
        int col = 1;
        for (int i = caretIndex - 1; i >= 0 && i < text.Length; i--)
        {
            if (text[i] == '\n')
                break;
            col++;
        }
        return col;
    }

    private void UpdateTitle()
    {
        var fileName = string.IsNullOrEmpty(_currentFilePath) ? "Untitled" : Path.GetFileName(_currentFilePath);
        Title = $"{fileName}{( _isModified ? "*" : "" )} - TextPlus";
    }

    private void UpdatePosition()
    {
        var text = Editor.Text ?? string.Empty;
        int line = GetCurrentLine();
        int col = GetCurrentColumn();
        int lineCount = text.Split('\n').Length;
        
        PositionText.Text = $"Ln {line}, Col {col}";
        StatusText.Text = $"Ln {line}, Col {col} | Lines: {lineCount} | Chars: {text.Length}";
    }

    private async void NewFile(object? sender, RoutedEventArgs e)
    {
        if (_isModified)
        {
            var result = await ShowSaveConfirmationAsync();
            if (result == DialogResult.Cancel)
                return;
        }

        Editor.Text = string.Empty;
        _currentFilePath = null;
        _isModified = false;
        UpdateTitle();
        UpdateLineNumbers();
    }

    private async void OpenFile(object? sender, RoutedEventArgs e)
    {
        if (_isModified)
        {
            var result = await ShowSaveConfirmationAsync();
            if (result == DialogResult.Cancel)
                return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt", "*.md", "*.json", "*.cs", "*.xml", "*.js", "*.ts", "*.py", "*.html", "*.css" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count > 0)
        {
            var file = files[0];
            try
            {
                using var stream = await file.OpenReadAsync();
                using var reader = new StreamReader(stream);
                var content = await reader.ReadToEndAsync();
                Editor.Text = content;
                _currentFilePath = file.Path.LocalPath;
                _isModified = false;
                UpdateTitle();
                UpdateLineNumbers();
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Error", $"Failed to open file: {ex.Message}");
            }
        }
    }

    private async void SaveFile(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            SaveFileAs(sender, e);
            return;
        }

        await SaveToFileAsync(_currentFilePath);
    }

    private async void SaveFileAs(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save File As",
            DefaultExtension = ".txt",
            SuggestedFileName = "Untitled",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (file != null)
        {
            await SaveToFileAsync(file.Path.LocalPath);
            _currentFilePath = file.Path.LocalPath;
            UpdateTitle();
        }
    }

    private async Task SaveToFileAsync(string filePath)
    {
        try
        {
            await File.WriteAllTextAsync(filePath, Editor.Text ?? string.Empty);
            _isModified = false;
            UpdateTitle();
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Error", $"Failed to save file: {ex.Message}");
        }
    }

    private async Task<DialogResult> ShowSaveConfirmationAsync()
    {
        var result = DialogResult.Cancel;

        var dialog = new Window
        {
            Title = "Save Changes",
            Width = 350,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = new SolidColorBrush(Color.Parse("#2D2D2D"))
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(20) };
        panel.Children.Add(new TextBlock { 
            Text = "Do you want to save changes?", 
            Margin = new Avalonia.Thickness(0, 0, 0, 20),
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"))
        });
        
        var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        
        var yesButton = new Button { Content = "Yes", Width = 80, Margin = new Avalonia.Thickness(0, 0, 10, 0) };
        yesButton.Click += async (s, args) =>
        {
            result = DialogResult.Yes;
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save File",
                    DefaultExtension = ".txt",
                    SuggestedFileName = "Untitled"
                });

                if (file != null)
                {
                    await SaveToFileAsync(file.Path.LocalPath);
                    _currentFilePath = file.Path.LocalPath;
                }
                else
                {
                    result = DialogResult.Cancel;
                }
            }
            else
            {
                await SaveToFileAsync(_currentFilePath);
            }
            dialog.Close();
        };
        
        var noButton = new Button { Content = "No", Width = 80, Margin = new Avalonia.Thickness(0, 0, 10, 0) };
        noButton.Click += (s, args) => { result = DialogResult.No; dialog.Close(); };
        
        var cancelButton = new Button { Content = "Cancel", Width = 80 };
        cancelButton.Click += (s, args) => { result = DialogResult.Cancel; dialog.Close(); };
        
        buttonPanel.Children.Add(yesButton);
        buttonPanel.Children.Add(noButton);
        buttonPanel.Children.Add(cancelButton);
        panel.Children.Add(buttonPanel);
        
        dialog.Content = panel;
        
        await dialog.ShowDialog(this);
        return result;
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 350,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = new SolidColorBrush(Color.Parse("#2D2D2D"))
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(20) };
        panel.Children.Add(new TextBlock { 
            Text = message, 
            Margin = new Avalonia.Thickness(0, 0, 0, 20), 
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"))
        });
        
        var okButton = new Button { Content = "OK", Width = 80, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        okButton.Click += (s, args) => dialog.Close();
        
        panel.Children.Add(okButton);
        dialog.Content = panel;
        
        await dialog.ShowDialog(this);
    }

    private void Exit(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Undo(object? sender, RoutedEventArgs e)
    {
        Editor.Undo();
    }

    private void Redo(object? sender, RoutedEventArgs e)
    {
        Editor.Redo();
    }

    private void Cut(object? sender, RoutedEventArgs e)
    {
        Editor.Cut();
    }

    private void Copy(object? sender, RoutedEventArgs e)
    {
        Editor.Copy();
    }

    private void Paste(object? sender, RoutedEventArgs e)
    {
        Editor.Paste();
    }

    private void SelectAll(object? sender, RoutedEventArgs e)
    {
        Editor.SelectAll();
    }

    private void ZoomInMenu(object? sender, RoutedEventArgs e)
    {
        ZoomIn();
    }

    private void ZoomOutMenu(object? sender, RoutedEventArgs e)
    {
        ZoomOut();
    }

    private void ResetZoomMenu(object? sender, RoutedEventArgs e)
    {
        ResetZoom();
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (_isModified)
        {
            e.Cancel = true;
            var result = await ShowSaveConfirmationAsync();
            if (result != DialogResult.Cancel)
            {
                _isModified = false;
                Close();
            }
        }

        base.OnClosing(e);
    }

    private async void OpenFolder(object? sender, RoutedEventArgs e)
    {
        var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Folder",
            AllowMultiple = false
        });

        if (folder.Count > 0)
        {
            _currentFolderPath = folder[0].Path.LocalPath;
            LoadFileTree(_currentFolderPath);
            FileTreePanel.IsVisible = true;
        }
    }

    private void GoUpFolder(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentFolderPath))
        {
            var parent = Directory.GetParent(_currentFolderPath);
            if (parent != null)
            {
                _currentFolderPath = parent.FullName;
                LoadFileTree(_currentFolderPath);
            }
        }
    }

    private void LoadFileTree(string path)
    {
        try
        {
            var rootItem = new FileTreeItem
            {
                Name = Path.GetFileName(path),
                FullPath = path,
                Icon = "📁",
                IsDirectory = true
            };

            LoadDirectory(rootItem, path);
            FileTree.ItemsSource = new ObservableCollection<FileTreeItem> { rootItem };
            FolderPath.Text = path;

            rootItem.IsExpanded = true;
        }
        catch (Exception ex)
        {
            _ = ShowMessageAsync("Error", $"Failed to load folder: {ex.Message}");
        }
    }

    private void LoadDirectory(FileTreeItem parent, string path)
    {
        try
        {
            var directories = Directory.GetDirectories(path);
            var files = Directory.GetFiles(path);

            foreach (var dir in directories)
            {
                var dirItem = new FileTreeItem
                {
                    Name = Path.GetFileName(dir),
                    FullPath = dir,
                    Icon = "📁",
                    IsDirectory = true
                };
                LoadDirectory(dirItem, dir);
                parent.Children.Add(dirItem);
            }

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file).ToLower();
                string icon = "📄";
                
                if (ext == ".txt" || ext == ".md") icon = "📝";
                else if (ext == ".cs" || ext == ".js" || ext == ".ts" || ext == ".py") icon = "💻";
                else if (ext == ".json" || ext == ".xml" || ext == ".yaml" || ext == ".yml") icon = "⚙️";
                else if (ext == ".html" || ext == ".css") icon = "🌐";

                parent.Children.Add(new FileTreeItem
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    Icon = icon,
                    IsDirectory = false
                });
            }

            parent.Children = new ObservableCollection<FileTreeItem>(
                parent.Children.OrderBy(x => !x.IsDirectory).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            );
        }
        catch { }
    }

    private async void FileTree_ItemPressed(object? sender, RoutedEventArgs e)
    {
        if (FileTree.SelectedItem is FileTreeItem item && !item.IsDirectory)
        {
            try
            {
                var content = await File.ReadAllTextAsync(item.FullPath);
                Editor.Text = content;
                _currentFilePath = item.FullPath;
                _isModified = false;
                UpdateTitle();
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Error", $"Failed to open file: {ex.Message}");
            }
        }
    }
    
    private async Task OpenFileFromTreeAsync(string filePath)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            Editor.Text = content;
            _currentFilePath = filePath;
            _isModified = false;
            UpdateTitle();
            UpdateLineNumbers();
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Error", $"Failed to open file: {ex.Message}");
        }
    }
}

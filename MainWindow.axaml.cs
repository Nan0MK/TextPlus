using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Threading.Tasks;

namespace TextPlus;

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
    private bool _isModified = false;

    public MainWindow()
    {
        InitializeComponent();

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
    }

    private void UpdateLineNumbers()
    {
        var text = Editor.Text ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            LineNumbers.Text = "1";
            return;
        }
        
        int lineCount = 1;
        foreach (char c in text)
        {
            if (c == '\n')
                lineCount++;
        }
        
        var lines = new System.Text.StringBuilder();
        for (int i = 1; i <= lineCount; i++)
        {
            lines.AppendLine(i.ToString());
        }
        LineNumbers.Text = lines.ToString().TrimEnd();
    }

    private void UpdateTitle()
    {
        var fileName = string.IsNullOrEmpty(_currentFilePath) ? "Untitled" : Path.GetFileName(_currentFilePath);
        Title = $"{fileName}{( _isModified ? "*" : "" )} - TextPlus";
    }

    private void UpdatePosition()
    {
        var text = Editor.Text ?? string.Empty;
        var caretIndex = Editor.CaretIndex;
        
        var line = 1;
        var col = 1;
        
        for (int i = 0; i < caretIndex && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                col = 1;
            }
            else
            {
                col++;
            }
        }
        
        PositionText.Text = $"Ln {line}, Col {col}";
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
}
